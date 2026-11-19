# JASS 经济与对战 AI 的依赖补全

范围：只支持官方对战图。运行时纯 C#，不执行 JASS 脚本。对战 AI 与经济的规则以 1.27 的 `common.ai`、`common.j`、四族 `.ai` 为唯一依据，逐行复刻。

## 0 结论

缺口不在"AI 会不会决策"，在它下面的三层：**查询**、**账本与队列**、**native 语义**。这三层没有，四族序列写得再准也只是空壳。

实测数据：

| 项 | 数量 |
|---|---:|
| `common.j` native | 50 |
| `common.ai` 脚本函数 | 120 |
| `common.ai` 依赖的引擎调用 | 48 |
| 四族 `.ai` 外部依赖 | 每族 5（`ClearHarvestAI`、`HarvestGold`、`HarvestWood`、`Sleep`、`StartThread`） |
| 客户端这 48 个调用的实现 | 48 / 48，全部已实现 |
| 该实现体量 | 12340 行，零渲染/UI 依赖 |

客户端实现集中在 `GameCore/L3_Simulation/Simulation/Jass/`：`JassGameNatives.Ai.cs`（1651）、`Ai.Captain.cs`（1877）、`JassGameNatives.Harvest.cs`（1692）、`Ai.HeroesAndSpells.cs`（1218）、`Ai.HarvestAndExpansion.cs`（1205）、`Production.cs`（1107）、`Ai.Queries.cs`（754）、`Ai.Production.cs`（668）等。它们依赖 `OpenWC3`（Unit/World）、`OpenWC3.Pathing`、`War3Formats`、`Jass`，Sim 里都有对应物。

## 1 依赖分层

自底向上七层。下层不通，上层写了也验不了。

### L0 世界查询

需要：按玩家统计单位/建筑数量（含在建）、资源、人口、矿点归属、城镇索引。

| | 内容 |
|---|---|
| 客户端 | `Ai.Queries.cs`（754）、`AiCollections.cs`（217） |
| Sim 现状 | `World.Units`、`Player` 资源字段、`Unit` 类型信息齐全；缺"按玩家聚合"的查询层 |
| 缺口 | `IMeleeAiWorld`：`UnitCountDone(player,id)`、`CountBuilding`、`Gold/Lumber/FoodUsed/FoodMade`、`OwnedMines(player)`、`TownIndexAt(x,y)` |
| 验收 | 已通过：`--ai-world` 输出 存活=71 建筑=10 金矿=6，与 `[INIT] 单位汇总` 一致；P0/P1 起始工人 `count=5 done=5` |

### L1 账本与队列

需要："我要 N 个 X"的账本——`SetBuildUnit` 记下目标数，`SetBuildUpgr` 记科技目标，`SetBuildNext` 追加而不是覆盖，`InitBuildArray` 复位。这是 AMAI 与 `common.ai` 之间唯一的接口。

| | 内容 |
|---|---|
| 客户端 | `JassGameNatives.Ai.cs`（1651）里的 `MeleeAiRuntime`、`JassGameNatives.Ai.Production.cs`（668） |
| Sim 现状 | 无。现有 `MeleeAiState.Plan` 是 Launcher 手写的计划表，语义与 `SetBuildUnit` 不同（前者是"目标数量"，后者是"追加/覆盖"两套行为） |
| 缺口 | `MeleeAiBuildBook`：目标表 + 追加表 + 科技表 + 复位，语义照 `InitBuildArray`/`SetBuildUnit`/`SetBuildNext`/`SetBuildUpgr` |
| 验收 | 同一串 `SetBuildUnit` 调用，两次 `InitBuildArray` 之间账本可重现；`SetBuildNext(6, PEON)` 后再 `SetBuildNext(8, PEON)` 结果为 8 而不是 6+8 |

### L2 采集调度

需要：工人分配（金/木比例、修理、闲工、慢伐木）、采集状态机（前往→装载→返回→交付）、矿点枯竭与再分配、`ClearHarvestAI`/`StopGathering`。

| | 内容 |
|---|---|
| 客户端 | `JassGameNatives.Harvest.cs`（1692）、`HarvestTiming.cs`（270）、`Ai.HarvestAndExpansion.cs`（1205） |
| Sim 现状 | `SimEconomy.cs`（931，Godot 侧，场景耦合）、Sim 的 `RuntimeProductionQueueAbility`、`ConstructionProgress` |
| 缺口 | 采集状态机下沉到 Sim；`SetPeonsRepair`/`SetSlowChopping`/`HarvestGold`/`HarvestWood`/`ClearHarvestAI` 的语义 |
| 验收 | `--gather-test`、`--lumber-test` 通过；三轮采集的收入曲线与 1.27 一致（金 / 木 / 携带量） |

### L3 生产与扩张

需要：按目标数下单训练/建造、建造选址投影、二矿判断（`TownHasMine`/`TownHasHall`/`SecondaryTown`）、扩张后的城镇索引。

| | 内容 |
|---|---|
| 客户端 | `Production.cs`（1107）、`Ai.BuildProjection.cs` |
| Sim 现状 | 训练/建造通道已有（`SimWorldHost.Spawn`、建造站点、`ConstructionProgress`）；缺"按目标数补单"与选址评分 |
| 缺口 | `SetBuildUnit` 的执行端：找大厅/兵营下单、找工人建造、缺资源时排队而不是丢弃；选址沿用 Sim 现有 footprint 判定 |
| 验收 | 兽族开局能走到 "10 苦工 + 祭坛 + 2 地洞"；人族 "8 农民 + 民房 + 祭坛"；建造不重叠、不缺工人 |

### L4 战斗与防守

需要：编队（captain）、进攻波次（`InitAssault`/`AddAssault`/`AttackMoveXY`/`AttackMoveKill`）、守卫点（`FillGuardPosts`/`ReturnGuardPosts`/`GuardSecondary`）、撤退阈值（`SetGroupsFlee`/`SetUnitsFlee`/`SetHeroesFlee`/`SetIgnoreInjured`）、目标选择（`SetTargetHeroes`/`SetAllianceTarget`）。

| | 内容 |
|---|---|
| 客户端 | `Ai.Captain.cs`（1877）、`Ai.BaseDefense.cs`、`AiActions.cs`（391）、`Ai.SpellTargets.*` |
| Sim 现状 | 订单与攻击移动齐全；无编队与波次概念 |
| 缺口 | `MeleeAiCaptain`：编队、波次、撤退、守卫点 |
| 验收 | `--housekeeping` 60 秒后 AI 能出第一波并攻击；`--aggro --fast` 下对手掉血 |

### L5 英雄与物品

需要：英雄选择顺序（`SetNewHeroes`/`SetHeroLevels`）、买物品（`SetHeroesBuyItems`/`SetHeroesTakeItems`）、技能学习（`set_skills`）。

| | 内容 |
|---|---|
| 客户端 | `Ai.HeroesAndSpells.cs`（1218）、`Ai.HeroItems.cs` |
| Sim 现状 | 技能、物品、复活、酒馆能力已在 Sim；无 AI 侧的英雄决策 |
| 缺口 | 英雄优先级表（四族各 3 位）、学习顺序、买物品触发点 |
| 验收 | 兽族开局出先知；`--hire-probe`、`--revive-probe` 覆盖 |

### L6 脚本辅助层

需要：把 `common.ai` 里对战用到的辅助函数逐行转 C#（不是全部 120 个）。

首批（约 30 个）：`InitBuildArray`、`SetBuildUnit`、`SetBuildUpgr`、`SetBuildNext`、`SetBuildUnitEx`、`TownHasHall`、`TownHasMine`、`TownCount`、`TownCountDone`、`SecondaryTown`、`MeleeTownHall`、`GuardSecondary`、`ReturnGuardPosts`、`BasicExpansion`、`WaitForUnits`、`GetZeppelin`、`PurchaseZeppelin`、`SetPeonsRepair`、`SetSlowChopping`、`ClearHarvestAI`、`HarvestGold`、`HarvestWood`、`CreateCaptains`、`FillGuardPosts`、`InitAssault`、`AddAssault`、`AttackMoveXY`、`AttackMoveKill`、`LoadZepWave`、`SuicideUnit`。

| | 内容 |
|---|---|
| 客户端 | `HarvestGold`/`InitAssault`/`SetMeleeAI` 一类是引擎 native，客户端在 `JassGameNatives.Ai.cs` 注册；`SetBuildUnit`/`SetBuildUpgr`/`SetBuildNext`/`TownHasMine`/`MeleeTownHall`/`GuardSecondary`/`BasicExpansion` 是 `common.ai` 里的 JASS 函数，没有 native 可参考，只能照脚本原文转写 |
| Sim 现状 | 无 |
| 缺口 | `Sim/JassToSharp/Ai/Common/`：一次实现，四族共用 |
| 验收 | 每个函数带一条对照用例（输入账本状态 → 输出计划/订单），与 JASS 原文逐条核对 |

### L7 四族序列

需要：`build_sequence`、`attack_sequence`、`set_vars`、`do_upgrades`、`pick_heroes`，逐行转写。

| 族 | 函数数 | 外部依赖 |
|---|---:|---|
| human | 10 | 5 |
| orc | 10 | 5 |
| elf | 11 | 5 |
| undead | 12 | 5 |

| | 内容 |
|---|---|
| 客户端 | 无需参考（脚本即规格） |
| Sim 现状 | `OrcCodes`、`MeleeAiStep`、四族 `*OpeningFlat`（无条件序列，仅参考） |
| 缺口 | 四族各一个 `*AiScript.cs`，只写序列与阈值，不含基础设施 |
| 验收 | `--ai-race=1..4 --housekeeping` 各族推进到二本；建筑/单位数与脚本序列一致 |

## 2 48 个引擎调用的分组

| 组 | 调用 | 客户端实现 | 层 |
|---|---|---|---|
| 线程 | `StartThread`、`Sleep` | `Ai.cs` | L2 |
| 采集 | `HarvestGold`、`HarvestWood`、`ClearHarvestAI`、`StopGathering`、`SetPeonsRepair`、`SetSlowChopping` | `Ai.cs`、`Harvest.cs`、`Ai.HarvestAndExpansion.cs` | L2 |
| 生产 | `SetReplacementCount` | `Ai.cs` | L1/L3 |
| 扩张 | `PurchaseZeppelin`、`LoadZepWave` | `Ai.cs` | L3 |
| 进攻 | `InitAssault`、`AddAssault`、`AttackMoveXY`、`AttackMoveKill`、`CreateCaptains`、`ConvertUnits`、`MergeUnits`、`GroupTimedLife`、`PopLastCommand` | `Ai.Captain.cs`、`AiActions.cs`、`TimedLifeGrouping.cs` | L4 |
| 防守 | `FillGuardPosts`、`ReturnGuardPosts`、`SetDefendPlayer`、`AddDefenders`、`SetGroupsFlee`、`SetUnitsFlee`、`SetHeroesFlee`、`SetIgnoreInjured`、`SetSmartArtillery` | `Ai.Captain.cs`、`Ai.BaseDefense.cs` | L4 |
| 目标 | `SetTargetHeroes`、`SetAllianceTarget`、`SetWatchMegaTargets`、`StartGetEnemyBase` | `Ai.Captain.cs`、`Ai.Queries.cs` | L4 |
| 英雄 | `SetNewHeroes`、`SetHeroLevels`、`SetHeroesBuyItems`、`SetHeroesTakeItems` | `Ai.HeroesAndSpells.cs`、`Ai.HeroItems.cs` | L5 |
| 杂项 | `RemoveInjuries`、`RemoveSiege`、`SuicideUnit`、`SuicideUnitEx`、`Unsummon`、`SetMeleeAI`、`SetCampaignAI`、`DisplayText*` | `Ai.cs` | L4/L5 |

## 3 任务清单

按依赖顺序，每条都要保持 `dotnet build` 绿、`--housekeeping` 可用。

| 编号 | 任务 | 前置 | 判据 |
|---|---|---|---|
| J-E0 | `IMeleeAiWorld` 查询层（按玩家聚合）**已完成** | — | 与 `--dump-units` 对齐 |
| J-E1 | `MeleeAiBuildBook` 账本（`InitBuildArray`/`SetBuildUnit`/`SetBuildNext`/`SetBuildUpgr`）**已完成** | J-E0 | `[book]` 五项用例通过 |
| J-E2 | 采集状态机下沉 Sim（`Harvest.cs` 语义） | J-E0、J-E1 | `--gather-test`、`--lumber-test` |
| J-E3 | 生产执行端（按目标数补单 + 选址） | J-E1 | 兽族 10 苦工/祭坛/2 地洞；人族 8 农民/民房/祭坛 |
| J-E4 | 扩张与城镇索引（`TownHasMine`/`SecondaryTown`） | J-E3 | 二矿落地且第二大厅开始采矿 |
| J-E5 | 编队与波次（`Captain`/`Assault`） | J-E3 | 60 秒出第一波并接战 |
| J-E6 | `common.ai` 辅助函数转写（首批 30 个） | J-E1–J-E5 | 每函数一条对照用例 |
| J-E7 | 四族序列转写（human 已有基线，补 orc/elf/undead） | J-E6 | `--ai-race=1..4` 各族到二本 |
| J-E8 | 英雄与物品决策 | J-E5、J-E6 | `--hire-probe`、`--revive-probe` |

## 4 与现有代码的关系

- `OrcCodes`、`MeleeAiStep`、`MeleeAiState`/`MeleeAiCounters`、`--ai-race`、以及"原版 AI 整局接管"的调度修正（`bc6b89c`）保留复用。
- `HumanAi.cs` 的 `AppendHumanBasicOpening`、`OrcAi.cs` 的 `AppendOrcBasicOpening` 是手写近似，J-E6 完成后由 `common.ai` 转写版取代，届时删除重复实现。
- 四族 `*OpeningFlat` 保持"无条件序列"的参考定位，不进运行时。
- `Ai.cs`（1527 行通用 AI）在四族接管后退化为非对战模式的备用逻辑，不在本规划范围内。

## 5 风险

| 风险 | 说明 | 应对 |
|---|---|---|
| 两套账本并存 | 现有 `MeleeAiState.Plan` 与 J-E1 的 `BuildBook` 语义不同，混用会出现"计划被打断" | J-E3 切换时只留一套，另一套删除 |
| 客户端实现是"AMAI 运行时" | 它的语义面向 AMAI 脚本，与官方 melee 脚本有细微差别（如 `SetBuildNext` 的追加规则） | 以 `common.ai` 原文为准，客户端实现只作交叉验证 |
| 采集状态机在 Godot | `SimEconomy.cs` 持有表现层 `Unit`，下沉需要拆 job 结构 | 先搬状态机与计时，表现层只留读状态 |
| 四族工作量 | 每族 10–12 个函数，但 `attack_sequence` 与 `set_vars` 分支多 | 按 J-E7 逐族推进，一族一验收 |
