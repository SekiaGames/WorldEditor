# Launcher 与 Open-Warcraft3 差异对比 v2

> 对象：`/Volumes/Data/Work/WorldEditor/Launcher`（Godot 4 + C#，419 个 cs，47,565 行，HEAD `0271d76`）
> 基准：`/Volumes/Data/Work/WorldEditor/test/Open-Warcraft3`（HEAD `14b62fd5`）
> 生成时间：2026-11-12
> 基准范围：`Src/` + `GameCore/`（1,505 个 cs，295,772 行）。`Tool/`、`MobileMapEditor/`、`VisualStudio/`、`GameCore/L4_Composition/Android` 不计入基准。

---

## 0. 这版和 v1 的口径不同

v1 做的是三件事：建全量文件映射表、给出目录镜像方案、排 P0/P1/P2 计划。三件事都已落地：结构镜像完成（191 个文件与客户端同路径）、P0 数值修正完成、P1 的大部分规则已搬。映射表也补做了，见 `Launcher映射表.md`。

v2 只回答一个问题：**现在还剩哪些差异，各自什么性质**。

口径上有一处必须纠正：

> v1 的 0.4 节写「P-1 全部完成后第 2 章的映射表即可废弃——同路径即同源」。
> 实测不成立。191 个同路径文件，用 difflib 逐行比对，**平均相似度 14.5%（中位数 13.3%）**。只有 4 个文件字节一致（两个枚举、伤害类型表、目标形态枚举），其余是重写关系。`Audio/War3AudioSystem.cs`（75 行 vs 1022 行）相似度 0.01，`Camera/War3Camera.cs`（75 vs 1248）0.01。

结论：**路径镜像解决的是「改哪个文件」，不解决「改了什么」**。除那 4 个文件外，`diff` 会把每个文件都报成不同，筛不出真正的漂移。行为一致性只能按语义对表（本文第 3 章），或者靠探针断言。

那 4 个文件按四条硬条件筛出来的：不引用客户端 `Ability/Unit/World` 成员、不引用客户端专有类型、自带 using、语义等价或是超集。命名空间已按客户端分层统一，照搬不再需要过渡层。完整判据和 191 对的状态表在 `Launcher映射表.md`。

还有一个反向陷阱：同路径文件里有 16 个不足 25 行。`Scenes/War3GameScene/Units.cs` 客户端 8 行（`public sealed partial class War3GameScene {}`），Launcher 487 行。同路径、同类名，但客户端的单位逻辑已经拆到别处。同路径在这种情况下反而会误导。

### 证据等级

| 等级 | 含义 |
|---|---|
| **甲** | 本次逐行读两侧代码，并亲自核对行号与数值（本文大部分结论） |
| **乙** | 两侧代码抽样比对，行号可查，未逐分支穷举 |
| **丙** | 按类名与调用点推断，未完整读实现 |

标注「复核纠正」的条目，是 v1 或本轮初查判错、经实测推翻的。

---

## 1. 量化现状

| 指标 | Launcher | 客户端 | 变化（相对 v1） |
|---|---|---|---|
| 代码行数 | 47,565 | 295,772 | 46.8k → 47.6k |
| cs 文件数 | 419 | 1,505（运行时代码） | 286 → 419 |
| 同路径同文件 | 191（占 Launcher 45.6%） | — | 0 → 191 |
| 同路径文件行重合均值 | 14.5% | — | 13.5% → 14.5%（4 个文件改逐字节一致后） |
| `Runtime*Ability` 文件 | 128 | 188 | 103 → 128（P-1g 拆分后） |
| Buff 子类 | 24 | 104（+15 AuraBuff） | — |
| 技能注册码 | 513 | 413（构造表） | 513 不变 |
| 双方共同码 | 299 | 299 | 295 → 299 |
| 订单类 | 4（Move/Attack/Patrol/AttackMove） | 11 个 `COrder` 子类 | — |
| UI 文件 | 7 | 86 | — |
| 输入文件 | 0（全在 `Input.cs` 374 行） | 20 | — |
| 寻路代码 | 406 行 | 14,690 行 | — |

### 1.1 差异的性质变了

v1 的问题分布是「客户端有、Launcher 整类没有」（岸边浪、昼夜光照、距离雾）。

现在这类整类缺失还剩一批（主要集中在表现层与数据面），但**主要问题已经换成另一种**：两侧都有实现，实现深度差一到两个量级。表现是「功能能跑、数值和分支对不上」。

典型三处：

- 音频：两侧都有 `War3AudioSystem`，75 行对 1022 行。Launcher 16 声道、无优先级；客户端 24 声道、8 个通道配额、优先级抢占、循环音、3D 锥体。
- 寻路：两侧都有 A*，406 行对 14,690 行。Launcher 有网格、代价、阻挡；客户端还有单位入网、接触式移动请求、分帧调度、共享路径等待。
- 命令卡：两侧都生成 4×3，Launcher 用顺序填格，客户端读物编 `Buttonpos` 并按 Game.dll 的抢格规则求解。

这类差异的搬运成本高于 v1 的估价：要重做整条管线。第 4 章单独处理。

---

## 2. v1 清单的现状

v1 列了 59 条编号差异 + 8 条结构性替换。逐条回验结果：

### 2.1 已修（v1 判对，且已落地）

| v1 编号 | 项 | 验证位置 |
|---|---|---|
| 1、2 | Misc 表源切 TFT、攻击护甲矩阵回到 1.0/1.5/1.0/0.7 | `MiscRules.cs:10` 读 `UnitsMiscGame`；提取数据源为 `Units/MiscGame.txt` |
| 3 | 移速夹取 | `World.Abilities.cs:417-420`，150~400，硬上限 522 |
| 4 | 伤害骰子 | `World.WeaponSystem.cs:90` 改 `weapon.Roll` |
| 5 | 物品三围字段 | `ItemBonus.cs:148-154`，力=C/敏=A/智=B |
| 6 | 复活时间用 `bldtm` | `World.HeroRevive.cs:55`，下限 1s |
| 7 | 商店库存缺省 | `ItemCatalog.cs:99-100`，缺省 1 / 缺省 0 |
| 8 | RoC 判定 | `Lifecycle.cs:255` `IsReignOfChaos => false` |
| 9 | 虚无免疫物理 | `World.DamageMembers.cs:227-232` |
| 11 | 免伤分类 | 部分修：仍按 `DamageType==Normal` 判物理，见 3.1-5 |
| 12、13、14 | 死亡缠绕 / 圣光 / 霜冻新星 | `RuntimeDeathCoilAbility.cs`、`HolyLight.cs`、`RuntimeFrostNovaAbility.cs` |
| 15 | 暴风雪波间隔读 `Cast` | `RuntimeBlizzardAbility.cs` |
| 19、20 | 攻击移动、队列 500 | `World.Orders.cs:31`、AttackMove 分支 |
| 28 | 攻速单桶 | `World.Targeting.cs:170` |
| 35 | 维修费 upkeep | `War3GameplayConfig.cs:33-50` + `Ui.cs:243-247`，三档 0/30/60 与客户端同值 |

### 2.2 复核纠正（v1 或本轮初查判错，客户端本来就没有）

| 项 | 客户端的实际情况 | 结论 |
|---|---|---|
| 在建建筑换 Unarmored | `World.DamageMembers.cs:256` 已有 `UnderConstruction` 分支 | 无差异 |
| 修缮/建造起始血量 | Launcher 每 tick 按进度重算 `Hp = MaxHp × progress`（`World.MechanismMembers.cs:98-99`） | 起始 0% vs 客户端 10%，量级差异，非「只有 1 点血」 |
| 敏捷护甲系数 0.30 vs 0.15 | **本轮初查误判**。客户端 `War3GameplayConfig.cs:186` 的 `0.15f` 只是字段缺省；`RegFloat("AgiDefenseBonus")` 会从 Misc 覆盖成 **0.30**，`AgiDefenseBase` 覆盖成 **-2**。提取数据 `Misc.AgiDefenseBonus = 0.30`、`Misc.AgiDefenseBase = -2` | 两侧同源同值，不是差异 |
| w3i 随机掉落表 | 客户端 `MapInfoParser` 不解析 | Launcher 反而更全 |

### 2.3 仍存在

第 3 章按危害重排，不复述 v1 编号。v1 中仍成立的条目约 30 条，其中 10 条性质变了（v1 判为「缺实现」，实际是「实现口径不同」）。

---

## 3. 当前差异清单

编号 `P0` = 改变对战胜负与数值；`P1` = 改变操作手感与战术可行；`P2` = 观感与数据面。

### 3.1 P0：数值与结算

| # | 项 | Launcher | 客户端 | 差异实质 | 等级 |
|---|---|---|---|---|---|
| 1 | **英雄经验曲线** | `HeroRules.cs:31-35` `XpToNext(L)=200L+100L(L-1)/2` 得 200/500/900/1400…；`World.HeroSelectionOrder.cs:63-66` 每级 `Xp -= XpToNext(L)` | `Hero.cs:344` `Xp >= XpToReach(Level+1)`，`XpToReach` 是**累计**阈值（`War3GameplayConfig.cs:92-112`，由 `NeedHeroXP=200` + 公式 A=1/B=100/C=0 递推） | Launcher 把累计表当每级增量扣。升到 4 级需 200+500+900=1600 点，客户端需 900 点。**前 3 级慢约 1.8 倍，越往后差距越大** | 甲 |
| 2 | 训练时长读错列 | `UnitFactory.cs:278` `TrainSeconds = 表列 reptm` | `JassGameNatives.Production.cs:107` `duration = data.GetFloat("bldtm")` | `reptm` 是修理时间、`bldtm` 是建造/训练时间。`UnitBalance.slk` 里 30 个单位两列不等，极端如 `ubsp`（bldtm 1 / reptm 60）、`hkni`（40/45）、`edoc`（35/30） | 甲 |
| 3 | 建筑升级收全额 | `Production.cs:326-330` `slot.Gold -= target.GoldCost` | `JassGameNatives.Production.cs:443-446` `BuildingUpgradeCost(source, target)`（差价） | 升二级主基地多付一整座基地的钱 | 甲 |
| 4 | 首个英雄不免费 | `MeleeRules.cs:25` `StartingHeroTokens=1` 定义后**零引用**（`grep` 全仓 1 处命中） | `JassGameNatives.Production.cs:84-93` 英雄令牌：首个英雄免单 | 四族首发英雄（425 金档）本该免单，Launcher 照价收费 | 甲 |
| 5 | 免伤分类只用三值 | `Src/Core/DamageType.cs` 三种（Normal/Magic/Universal）；`World.DamageMembers.cs:226` `physical = DamageType==Normal` | `Src/Components/Health.cs:186-193` 21 种 JASS 伤害类型 + 位掩码，Universal 既非物理也非魔法 | 穿刺(Universal)伤害被魔法免疫吞掉；攻城/英雄攻击配 Universal 时判定错 | 乙 |
| 6 | 负回复不致死 | `World.DamageMembers.cs:150` `if (hpPerSecond > 0f)` 才结算 | `Health.cs:70-74` 负回复同样扣血、可致死 | 凤凰、毁灭者这类靠负回复死掉的单位永远活着 | 甲 |
| 7 | 吸血/反伤基数 | `World.DamageMembers.cs:246` `info.ThornsBase = info.Amount`（护甲前） | `Health.DamageEvents.cs:53-56` 以「扣血前剩余生命」封顶 | 残血单位吸血虚高、反伤溢出 | 乙 |
| 8 | 采集常量硬编码 | `World.cs:18-19` + `MechanismMembers.cs:46-58`：每 2.2s 得 10 金 / 10 木 | `Harvest.cs:1119-1122` 金读 `Ahar.DataC`，木读 `DataA`/容量 `DataB`，时长读 `Dur` | 非标准种族、自定义工人产量全错 | 甲 |
| 9 | 无矿槽上限 | `MechanismMembers.cs:184-191` 每个工人独立采集，无排队 | `HarvestTiming.cs:139-161` 金矿容量 FIFO + 服务时间 | 5 农民同矿收益约 5 倍，客户端受容量限制 | 甲 |
| 10 | 侍僧免返程 | 同上，`EntersMine` 当帧入账 | `Harvest.cs:1131-1147` 出矿仍带 `HarvestCargo` 走回程 | 亡灵经济显著偏强 | 乙 |
| 11 | 物品自动拾取 | `World.Items.cs:14` `PickupRange=150`，`TickItemPickup` 任何有格子的单位路过即捡 | `OrdersAndItems.cs:1131-1153` 需拾取指令，半径 250（`War3GameplayConfig.cs:274`） | 敌方英雄路过能把掉落物捡走；商店旁落地的物品自动消失 | 甲 |
| 12 | 卖物回补库存 | `World.Items.cs:209-218` 半价 + `shop.AddStock` | `OrdersAndItems.cs:178` 直接 `RemoveItem`，不回库存 | 买→卖可以刷库存、刷差价 | 甲 |
| 13 | 卖物不折残量、不退木 | `World.Items.cs:209` `max(1, goldcost/2)` | `OrdersAndItems.cs:186-194` `cost × (charges/uses) × 0.5`，金木都退 | 用剩 1 次的权杖仍卖半价 | 乙 |
| 14 | 合建线性加速 | `MechanismMembers.cs:88-91` `rate = min(builders, 3)`，不加价 | `ConstructionProgress.cs:85-87` `1+0.6(n-1)`，且额外工人按进度补扣 15% 造价 | 3 人合建 3 倍速且免费 | 甲 |
| 15 | 修理近乎免费 | `RuntimeRepairAbility.cs:10-11,26` 0.15 金/HP，无木材 | `Construction.cs:1030-1037` `goldRep×0.35` + `lumberRep×0.35`，按血量比例扣 | 攻城收益被瞬间修回 | 乙 |

### 3.2 P0：订单与指令（结构性最大缺口）

Launcher 没有 `Src/Orders/` 目录。订单只有 4 种，放在 `World.Orders.cs` 的 `UnitOrderKind` 枚举里。客户端的订单是 11 个 `COrder` 子类，带状态机与打断门。

| # | 项 | Launcher | 客户端 | 影响 | 等级 |
|---|---|---|---|---|---|
| 16 | 施法不占订单 | `World.Abilities.cs:124` `TryCast` 立即执行 | `OrderQueue.cs:513-518` `Cast()` 生成 `CastOrder` 入队 | 无法「施法→移动」连续排队；施法不参与打断规则 | 乙 |
| 17 | 建造/采集不占订单 | `World.Orders.cs:100-105` 直接把 `GoldMine/Tree/BuildSite` 清空 | `BuildOrder`、`ConstructionWorkOrder`、`ResumeConstructionOrder` 入队 | 无法排队「造完再采」 | 乙 |
| 18 | 无订单打断门 | `World.Orders.cs:45` 新命令无条件 `PendingOrders.Clear()` | `OrderQueue.cs:149-157` 当前订单不可打断时改为延后 | 新命令吞掉整条队列 | 乙 |
| 19 | 无前摇保护 | `ApplyOrder` 不调 `CancelAttack` | `OrderQueue.cs:250-264` → `CAbilityAttack.Reset()` | 走位躲不掉抬手、做不到走砍 | 乙 |
| 20 | 队列推进靠特判 | `World.cs:178-187` 只在「目标死/走到点」调 `AdvanceOrder` | `OrderQueue.cs:23-34` 统一 `OnEnd` 状态机 | 巡逻与攻击交叉时队列不推进 | 丙 |
| 21 | 施工取消无退款 | `World.Orders.cs:102-104` 直接 `BuildSite = null` | `Src/Orders/BuildOrder.cs:76-101` `_refunded` 保证退一次 | 取消在建建筑，资源凭空消失 | 乙 |
| 22 | 无订单 ID 体系 | 无 | `OrderIdTable.cs`（366 行）编码全部 native order id | 触发器/脚本下发订单无法映射 | 丙 |

### 3.3 P1：攻击与移动

| # | 项 | Launcher | 客户端 | 影响 | 等级 |
|---|---|---|---|---|---|
| 23 | 前摇后摇不随攻速 | `World.WeaponSystem.cs:70,80` 用原值 `DamagePoint`/`Backswing` | `Src/Core/Weapon.cs:64-75` 三者同除 speed 并夹到 cooldown | 高攻速英雄动作拖沓，DPS 手感与数值脱节 | 甲 |
| 24 | 无射程缓冲 | `World.WeaponSystem.cs:82` 出手只查 `Alive` | `CAbilityAttack.cs:250` `EffectiveRange + RangeMotionBuffer`（250） | 目标跑出射程仍必中 | 乙 |
| 25 | 无最小射程 | 全仓无 `MinimumRange`（仅 `Blink.cs` 有同名属性） | `CAbilityAttack.cs:210-219`、`Weapon.cs:61` | 迫击炮/投石车可贴脸开火 | 甲 |
| 26 | 无投射物轨迹 | `World.WeaponSystem.cs:12-18` `PendingHit` 只有倒计时 | `Src/Core/Projectile.cs:36-73` 位置/发射偏移/弧高/落点 | 无抛物线，命中点固定目标中心 | 乙 |
| 27 | 无弹道种类 | 只有圆形三档溅射 | `Weapon.cs:97` `MissileKind`：Bounce/Line/Spill | 女猎手月刃、狮鹫连锁、冰龙直线全缺 | 乙 |
| 28 | 溅射人数上限未接线 | `Src/Core/Weapon.cs:44` 读入 `SplashTargets` 但 `ApplySplash` 不用 | `Weapon.cs:101-103` `EffectiveBounceCount` | `targCount` 配置无效 | 甲 |
| 29 | 无受击还击 | 全仓 `ReturnFire` 0 命中 | `Src/Components/Ai.cs:136-166` | 被打不还手（野怪呼救已有，见 `World.Targeting.cs:146-160`） | 甲 |
| 30 | 单位不入寻路网 | `PathGrid.cs:21-25` 只有地形/动态/清除三层 | `CLrPathingSys.cs:60-64` 单位接触目标与强制占位 | 围杀、堵口不成立，只靠软分离推 | 乙 |
| 31 | 寻路预算大 28 倍 | `PathGrid.cs:207` `guard > 20000` | `CLrPathingSys.cs:41` `DefaultBudget=700` | 大图寻路卡顿，且无「最近可达点」回退 | 乙 |
| 32 | 软分离档位少两档 | `World.RepulseMembers.cs:78-102` 3 档 5/7/10 | `CPoSeparate.cs:44-52` 5 档 5/7/8/9/10 | 中型单位推挤力度错档 | 乙 |
| 33 | 编队落点不看体积 | `GroupFormation.cs:48-61` `ceil(sqrt(n))` 列 + 固定 2 格 | `MovementFormationOffsets.cs:28-36,76-86` 按层最大半径定距 | 大体积单位重叠、阵型偏后 | 乙 |
| 34 | 无窄口让行 | `World.GroupMovement.cs:55-74` 只用最小 `PathIndex` 卡点 | `MovementGroup.cs:26-40` 等待计数 + 抑制冷却 | 窄口堵死 | 乙 |

### 3.4 P1：技能与 Buff

| # | 项 | Launcher | 客户端 | 影响 | 等级 |
|---|---|---|---|---|---|
| 35 | 工厂无特化流水线 | `RuntimeAbilityFactory.cs:587-591` 码表 → `new` 一个类 | `RuntimeAbilityFactory.cs:68-80` 五段：依赖准备 → 特化 → 绑数据 → 绑依赖 → 刷新 | 技能拿不到 BuffData、拿不到等级数据，见 #36 | 甲 |
| 36 | 技能数值只读 SLK | `AbilityCatalog.cs:98` 固定读 `UnitsAbilityData` | `RuntimeAbilityFactory.cs:298` `ReadFloat(ObjectData,"DataA",level,0)` | **地图 `war3map.w3a` 对技能的覆盖全部不生效** | 甲 |
| 37 | 无物编字段覆盖 | `War3ObjectData.cs:190` 只存 4cc，不读 MetaData 映射 | `War3ObjectData.cs:558-568` 4cc → 列名映射 | 即使接上消费方，字段名也解析不出来 | 甲 |
| 38 | 无 BuffData 表 | 不读 `AbilityBuffData.slk` | `War3ObjectData.cs:304` | Buff 无图标、无名字、无物编字段 | 乙 |
| 39 | Buff 不在技能容器 | `Unit.Abilities.cs:14` `List<Buff> Buffs` 独立于能力列表 | `Src/Abilities/Buff.cs:5` `Buff : Ability` | 无等级、无禁用、无统一 `OnRemove` | 乙 |
| 40 | Buff 叠加不查重 | `World.BuffMembers.cs:13-22` 直接 `Add` | `RuntimeSlowAbility.cs:92-97` 同 Rawcode 先 `RemoveAbility` 再挂 | 重复施法属性线性叠加 | 甲 |
| 41 | Buff 只有正负一维 | `Src/Abilities/Buff.cs:25` `IsHarmful => true` 缺省为真 | `Buff.cs:8-20` `Positive/Magic/Physical/AutoDispel/SpellStealable` | 物理/魔法驱散不分；增益被当负面处理 | 乙 |
| 42 | 驱散副作用过大 | `World.BuffMembers.cs:40` `if (removed>0) target.ClearSlows()` | 减速本身就是 Buff，无需旁路 | 驱散一个治疗 HOT 会顺手清掉全部减速 | 甲 |
| 43 | 死亡不清 Buff | `World.DamageMembers.cs:442-448` 只调 `OnDeath()`；死亡单位不再推进计时（`World.cs:81-85`） | `Buff.cs:176` `OnDeath() => Owner.RemoveAbility(this)` | 尸体带着常驻加成，复活后旧 Buff 继续生效 | 乙 |
| 44 | 光环每 tick 重算 | `World.Abilities.cs:298-325` 清空后全量求和 | `Src/Skills/AuraAbility.cs:86-142` 每 2s 扫描授 Buff | 加成不可驱散、无图标、无余韵 | 乙 |
| 45 | 光环叠加取 max | `AuraBonus.cs:29-41` 逐字段 `Max` | 每个光环独立 Buff 相加 | 邪恶光环 + 耐久光环的移速只取较大者 | 甲 |
| 46 | 光环只判阵营 | `World.Abilities.cs:319` 仅 `AreAllied` | `AuraAbility.cs:55-84` 目标掩码 + 幻象/隐身/变形门 | 幻象也能吃到光环、隐身单位照样施加 | 乙 |

### 3.5 P1：英雄流程

| 项 | Launcher | 客户端 | 影响 | 等级 |
|---|---|---|---|---|
| 复活靠自动倒计时 | `World.HeroRevive.cs:68-99` 死亡 1 秒后自动找祭坛扣钱开倒计时 | `JassGameNatives.Production.cs:355-410` 玩家在祭坛下单进生产队列，金+木，可取消退款 | 玩家无法选择时机/祭坛，也无法取消 | 甲 |
| 复活只扣金 | `World.HeroRevive.cs:46-49` | 同处 `HeroReviveLumberCost` 金+木 | 木材价英雄复活免费 | 乙 |
| 祭坛硬编码 | `World.HeroRevive.cs:12-15` 4 个固定 id | `Production.cs` 读物编 `Revive` 列 | 自定义祭坛的英雄永不复活 | 乙 |
| 复活必满蓝 | `Unit.Lifecycle.cs:125-126` `Hp=Max`、`Mana=Max(Mana, MaxMana)` | `Hero.cs:171-178` 法力回 `InitialMana` | 复活即满蓝 | 甲 |
| 击杀经验固定 25 | `HeroRules.cs:37-46` 非英雄恒 `GrantNormalXP` | `War3GameplayConfig.cs:165-180` 按等级查表递推 | 高级野怪经验不涨 | 乙 |
| 经验分配无 handicap | `World.HeroSelectionOrder.cs:28-46` 只做均分 + `GlobalExperience` 兜底 | `World.cs:517-552` 另有 handicap 与加经验科技 | 经验数值偏低 | 丙 |

### 3.6 P1：界面与操作

| # | 项 | Launcher | 客户端 | 影响 | 等级 |
|---|---|---|---|---|---|
| 47 | 命令卡只有 12 格、顺序填 | `Ui.cs:227-288` 基础命令占 0~4；`MeleeHudController.cs:736` 填满 12 即 `break` | `CommandCardBuilder.cs:631-639` 读物编 `Buttonpos`，`NativeCommandPosition.cs:57` 求解槽位 | 人族农民 `Builds` 有 11 项（`hpea.Builds = htow,hhou,hbar,hbla,hwtw,halt,harm,hars,hlum,hgra,hvlt`），5 格被基础命令占掉，**只能点到 7 个建筑，剩 4 个静默丢弃** | 甲 |
| 48 | 无建造/学习子页 | 平铺 | `CommandCardBuilder.Menus.cs:33-91` 子页 + slot 11 返回 + B/O 热键 | 同上，且技能学习与施法混在同一格 | 乙 |
| 49 | 队列格不可点 | `MeleeHudController.cs:341-365` `MouseFilter=Ignore` | `QueueIconRowFrame.cs:153` 每格可取消 | 排队错了只能右键退最后一项 | 乙 |
| 50 | 无边缘滚屏 | 全仓 `EdgeScroll` 0 命中 | `War3CameraComponent.cs:105-116` 3px 边缘 | 桌面玩家鼠标到边上镜头不动 | 甲 |
| 51 | 相机字段少 | `War3Camera.cs` 75 行：距离 + 仰角数组 | `War3Camera.cs` 1248 行：FOV/旋转/平滑/跟随/边界余量 | 无旋转、无跟随、缩放生硬 | 甲 |
| 52 | 编队无 Shift 并入 | `Input.cs:100-105` 只有 Ctrl 覆盖 | `Input.cs:1237-1250` set/add/recall 三态 | `Shift+2` 并入失效 | 乙 |
| 53 | Esc 语义相反 | `Input.cs:64-71` Esc 开菜单 | `War3InputComponent.cs:128-159` F10 开菜单、Esc 只取消 | 老玩家按 Esc 想取消施法却弹菜单 | 乙 |
| 54 | 智能右键缺项 | `Rally.cs:30-`（集结点/修理/采金/攻击/移动） | `War3WorldInteractionComponent.cs:257-438` 另有拾取/传送门/上载具/跟随 | 右键点地面物品、点友军无反应 | 乙 |
| 55 | 无任务/排行榜/多面板 | 全仓无 | `QuestDialogFrame.cs`(385)、`LeaderboardFrame.cs`(124)、`MultiboardFrame.cs`(123) | 地图脚本类 UI 全部不显示 | 甲 |

### 3.7 P1：AI

Launcher 是手写 C# AI（`Ai.cs` 1440 + `AiCounters.cs` 149）；客户端是 JASS AI 宿主（`JassGameNatives.Ai.cs:58-84` 载入 `Scripts\common.ai`，仓库内带 AMAI 版 61,578 行）。

| # | 项 | Launcher | 客户端 | 影响 | 等级 |
|---|---|---|---|---|---|
| 56 | 内务跑完就停 | `Ai.cs:1003-1006` 任务表全达成后 `slot.Done = true`；`Ai.cs:918` 之后 `return` | AMAI 主循环常驻 | **出兵不停（`TickAiWaves` 独立），但建造/训练/研究/采集永久停止**，中后期电脑只靠免费波次出兵 | 甲 |
| 57 | 无分矿 | `Ai.cs` 无 expansion 分支 | `JassGameNatives.Ai.HarvestAndExpansion.cs`（1205 行） | 长局电脑经济必崩 | 乙 |
| 58 | 加点策略固定 | `World.Abilities.cs:278-297` `AutoLearnHeroSkills` 每点只选「当前等级最低、已解锁」的技能，等于平均加点 | `Ai.HeroesAndSpells.cs`（1218 行）按对局局势选技能 | 电脑英雄不会主升大招，战力偏低 | 甲 |
| 59 | 无基地回防 | 只有修建筑（`Ai.cs:373`）和保英雄（`Ai.cs:271`） | `Ai.BaseDefense.cs:6-40` | 拆家时电脑部队不回头 | 乙 |
| 60 | 波次是凭空刷兵 | `Ai.cs:35-60` 到点 `Spawn`，不扣钱、不走生产 | AMAI 走生产队列 + 队长集结 | 电脑兵力与经济脱钩 | 甲 |

> 「AI 永久停机」这个说法要修正。出兵走独立的 `TickAiWaves`（`Ai.cs:20-33`），`slot.Done` 只停内务。实际表现是：电脑打完内务计划后不再发展，但仍按固定间隔免费刷兵。

### 3.8 P2：语音与音乐

| 项 | Launcher | 客户端 | 等级 |
|---|---|---|---|
| 声道 | `War3AudioSystem.cs:9` 16 个，单池 | `:38` 24 个 + `:48` 8 个通道配额 | 甲 |
| 抢占 | `:60-74` 取最旧 | `:232-256` 按优先级抢占 | 甲 |
| 循环音 | 无 | `War3LoopingSoundService.cs:126-149` | 乙 |
| 3D 锥体 | `:24-28` 固定半径 4500 | `:605-628` 锥体 + listener | 乙 |
| 音乐间隔 | `War3MusicPlayer.cs:68-73` 播完立刻下一首 | `:163` `_nextTrackDelay = 20` | 甲 |
| 音乐双通道 | 单通道 | `:8` 背景 + 主题曲，`:108-114` 主题曲压背景 | 乙 |
| 音效表 | `War3SoundTables.cs` 4 张 | `:33-47` 11 张（含环境音/EAX） | 甲 |

### 3.9 P2：表现层（客户端有、Launcher 无）

按玩家可见程度排序，均有客户端文件为证。

| 项 | 客户端位置 | Launcher | 等级 |
|---|---|---|---|
| 昼夜光照 | `Environment/SceneLighting.cs:47-52` | `Environment.cs:19-22` 只有 `IsNight` 布尔，无光源 | 甲 |
| 战争迷雾 LOS 遮挡 | `Src/Core/FogOfWar.cs:566-597` 阴影传播 + 悬崖 | `Src/World/FogOfWar.cs:193-219` 纯圆形 | 甲 |
| 迷雾格宽 | `FogOfWar.cs:13-14` 128 单位 | `FogOfWar.cs:16,58` 256 单位（`CellsPerTexel=2` × 128） | 甲 |
| 地面贴花 / UberSplat | `Rendering/War3SplatSystem.cs:49-110` | 无 | 乙 |
| 地形烘焙阴影分辨率 | `TerrainShadowMap.cs:35-36` 每格 4 像素 | `TerrainGridFactory.cs:150` 每格 1 像素，强度写死 0.5 | 乙 |
| 树影合成 | `TreeShadowBuffer.cs` 合成进 shd 纹理 | `DoodadRenderer.cs:80-117` 独立浮动 Quad | 乙 |
| 装饰物动画/破坏 | `DoodadRenderer.cs:229-294,958-978` | 静态实例，死亡直接隐藏 | 甲 |
| 建筑灰雾残影 | `CharacterRenderer.FogMemory.cs:20-60` | `Environment.cs:73` `Visible=false` | 乙 |
| 幻象染色 | `IllusionPresentation.cs:21-23` `(20,96,255)` | 无 | 乙 |
| 顶点染色/虚无色调 | `UnitStateColor.cs:16-30` | 只有整体 alpha | 乙 |
| 动画状态机 | `UnitAnimationController.cs:54-120,212-265` | `UnitSceneComponent.cs:281-308` 名称前缀匹配 | 甲 |
| 选择圈贴图 | `Presentation.cs:1844` `SelectionCircleMed.blp` | `UnitSceneComponent.cs:235-253` 自绘 32 段纯色环 | 乙 |
| 天空盒日月/云 | `Rendering/SkyboxRenderer.cs:14-61` | 全景 PNG，`AmbientSource.Disabled` | 乙 |
| 天气类型 | `WeatherType.cs:10-34` 21 种 + `WeatherDefinition.cs:83-87` 环境音 | `WeatherRenderer.cs:20-83` 单一 Box 发射器 | 乙 |
| 岸边浪 | `Environment/ShorelineWaveRenderer.cs:14-71`，`Lifecycle.cs:250` 实例化 | 无 | 甲 |
| MDX 粒子/丝带 | `MdxParticle2Renderer.cs`、`MdxRibbonRenderer.cs` | 依赖外部转换产物，代码层无粒子系统 | 丙 |
| 特效码表 | 无码表，读物编 Art 字段（`Ability.cs:436-457`） | `EffectArtTable.cs` 硬编码 148 码 | 甲 |

> `EffectArtTable` 那 148 码是 Launcher 侧唯一的表现来源。物编里改技能美术、或者用客户端支持而表里没有的码，Launcher 一律没有特效（`Effects.cs:21-27` 只打日志）。

### 3.10 P2：数据面

| 项 | Launcher | 客户端 | 等级 |
|---|---|---|---|
| 物编结果进运行时 | `MapDocument.cs:224-233` 只在 `.gmap` 转换期解析，唯一消费点是 `ResolveObjectBaseId`（`:272-289`） | `War3ObjectData.cs:55-77` 载入即合并进对象图 | 甲 |
| 段集合 | `War3ObjectData.cs:70-80`：`BuffFile = "war3map.w3b"`，`AllFiles` 无 `w3h` | `War3ObjectData.cs:387-397`：w3u/w3a/w3h/w3t/w3b/w3q + w3d | 甲 |
| Meta 表 | 不读 `*MetaData.slk` | `War3ObjectData.cs:67-69,389-397` | 甲 |
| Misc 地图层 | `MiscRules.cs:10` 只读全局 `UnitsMiscGame.json` | `War3GameplayConfig.cs:514` 追加 `war3mapMisc.txt` | 甲 |
| `*AbilityFunc.txt` | `AbilityCatalog.cs:101-111` 只读 Strings | `War3ObjectData.cs:326-331` Func + Strings | 乙 |
| 单位战斗字段 | `UnitFactory.cs` 无 `RngBuff`/`prio`/`threat`/`weapsOn`/`minRange` | `UnitFactory.cs:294-297`、`UnitFactory.TypeData.cs:82-84` | 乙 |
| wpm 标志位 | `War3Pathing.cs:8-10` 3 位 | `War3Pathing.cs:5-15` 8 位 | 甲 |
| 建筑 footprint | `UnitFactory.cs:427-442` 按文件名猜尺寸 | `War3Pathing.cs:110-190` 解 TGA 逐像素 | 乙 |
| 装饰物记录切分 | `War3MapDoodads.cs:62` `(len-16)/count` 平均步长 | `War3MapDoodads.cs:40-71` 顺序读 | 乙 |
| 文本宏 | `War3Text.cs:88-127` 只处理技能引用、单数字等级 | `War3TextMacroResolver.cs:49-176` 五表引用 + 百分比 + 偏移 | 乙 |
| 科技效果码 | `TechTree.cs:243-309` 少 `rasd/renw/rman/rmap/rauv/rart/raud/rtma` | `Production.cs:549-911` 全实现 | 甲 |
| 能力 `Requires` 门控 | 不读技能的 `Requires`/`checkDep`，`abilList` 里有就直接可用 | `UnitFactory.Abilities.cs:21-25` `Requires` 非空且 `checkDep!=0` → `SetTechTreeLocked(true)`，研究完成由 `SetPlayerTechLevel` 解锁 | 甲 |
| 科技同码异义 | `ratx` → 固定附加伤害（`DamageMembers.cs:68`）；`rlev` → 召唤物等级；`rrai` → 召唤物存活 | `ratx` → 百分比乘算（`Production.cs:863`）；`rlev` → 技能等级；`rrai` → 仅 `RaiseDead` 时长 | 甲 |

---

## 4. 结构性差异（搬运成本最高，需单独立项）

这四条要重做管线，靠补文件解决不了。列在这里是因为它们决定了上层差异能不能修干净。

### 4.1 订单体系

现状：4 个枚举值 + `UnitOrderKind`，施法/建造/采集是 `Unit` 上的字段。

目标：`COrder` 基类 + 11 个子类 + `OrderQueue` 组件 + `OrderIdTable`。

代价：`IssueOrder` 的生产调用点只有 5 处（`Commands.cs` 4、`Rally.cs` 1），但 AI 与采集/建造是直接写 `Unit` 字段：`MoveTarget =` 65 处、`Tree =` 26 处、`GoldMine =` 17 处、`BuildSite =` 13 处、`RepairTarget =` 11 处。这些路径都要改成「生成订单入队」，否则订单体系只覆盖玩家指令，AI 与内务仍是旁路。

### 4.2 伤害管线

现状：`DealDamage` 一个函数，`ApplyIncomingModifiers` 一次遍历 Buff。

目标：`Health` 组件 + 三段观测者（priority 1 / 2-8 / final absorber）+ 双事件（伤害前可改类型、命中后可改扣血）。

代价：所有减伤/免疫/吸收/反射的实现都要挂到带优先级的钩子上。当前 12 种「改 Unit 字段」的写法（`BonusDamageTakenPct`、`MagicDamageReductionPct` 等）要重做成 Buff 回调。

### 4.3 Buff 载体

现状：`Buff` 是独立于 `Ability` 的轻类，24 个子类。

目标：`Buff : Ability`，104 个子类 + 15 个 AuraBuff。

代价：`Ability` 与 `Buff` 的合并会牵动 `Unit.Abilities` 的容器结构、驱散、图标、时长。

### 4.4 表现资产管线

现状：MDX 已在 Extract 阶段转成 `.tscn`，Launcher 只 `Load`。

目标：客户端在运行时解析 MDX（材质层、geoset 动画、粒子、丝带、事件、骨骼）。

代价：这条不能照搬，只能让 Extract 在转换期把上述特性烘进 `.tscn`。当前缺口（粒子发射器、丝带、动画事件、replaceableId 2/11/31）的根子在 **Extract 的转换覆盖面**。

---

## 5. 已确认不是缺口

> 这一节是 `施工进度.md` 3.1 的延续。其中三条经本轮复核**站不住**：「能力 `Requires` 门控」「剩余 8 条科技效果码」「英雄手动复活」。前两条已移到 3.10，第三条移到 3.5。其余条目实测成立。

| 项 | 依据 |
|---|---|
| 弹道遮挡 | `Src/Core/Projectile.cs` 无遮挡逻辑 |
| 多段巡逻 | `COrderPoint2` 只存两组坐标 |
| 召唤物随施法者死亡消失 | 只有凤凰蛋有 `DismissSummonsOf` |
| 单位让位 | 靠 `CPoSeparate` 软分离，无绕行 |
| 高低地命中惩罚 | 客户端只把 `ChanceToMiss` 读进配置（`War3GameplayConfig.cs:253`），全仓无消费方 |
| 非英雄背包 | 客户端无物品栏实现 |
| 脚步循环音 | `umsl` 在本地 SLK 无值 |
| 地面血渍 | 客户端无死亡贴花 |
| 区域天气 | 客户端只有 w3i 全局天气 + JASS |
| 迷雾内淡入淡出 | 客户端渲染层也是二值 |
| 云层时变 | 天空模型无动画轨道 |
| UberSplat 建筑光晕 | 1.27 物编无该字段 |
| 昼夜光照色表 | 客户端无逐时段颜色表（`[Light] Direction` 单值） |
| 未完工工地拆除 | 只有 `CancelConstruction` |
| 物品合成 | 对战无合成 |
| 市场出售面板 | `nmrk` 的 `Asid`/`Asud` 客户端无运行时实现 |
| w3i 随机掉落表 | 5 张测试图 w3i 无该段 |
| headless 材质告警 | 报错栈在 dummy 渲染器（`material_storage.cpp:264`） |

---

## 6. 建议顺序

按「可验证性 × 影响面」排，不按工作量。

**第一批（数值，当天可验）**：#1 经验曲线、#2 训练时长、#3 升级费用、#4 首英雄令牌、#12 卖物回补库存、#11 拾取需指令、#8 采集读表。这批全部是「改一行读表位置」，且都能用 `--state-probe` 加断言锁死。

**第二批（规则，每条一个探针）**：#6 负回复致死、#14 合建系数、#15 修理费用、#37/#36 物编字段与技能覆盖、#44/#45 光环改 Buff 化。

**第三批（管线，需立项）**：订单体系（#16-22）、伤害观测者（#5）、表现资产。

**第四批（体验）**：命令卡子页与物编格位（#47-48）、相机（#50-51）、音频（3.8）。

**关于 AI**：内务停机（#56）是设计选择。`slot.Done` 的本意是「计划做完」。要让电脑持续发展，得把内务改成循环计划，这属于新功能，不在对齐范围内。

---

## 7. 复现方法

每条结论都可单独验证。几类常用手段：

```bash
# 1. 同路径文件集合与相似度
comm -12 <(cd Launcher && find . -name '*.cs' | sed 's|^\./||' | sort) \
         <(cd test/Open-Warcraft3 && find . -name '*.cs' -not -path '*/obj/*' | sed 's|^\./||' | sort)

# 2. 技能注册码集合差（注意大小写：Launcher 字典是 OrdinalIgnoreCase）
cd Launcher && grep -o '\["[A-Za-z0-9]\{4\}"\]' Src/Abilities/RuntimeAbilityFactory.cs | tr -d '[]"' | tr 'A-Z' 'a-z' | sort -u

# 3. 属性是否存在
grep -rn "ReturnFire\|RangeMotionBuffer\|MissileKind\|EdgeScroll" Launcher --include='*.cs'

# 4. 数据源真值（Misc 类数值不是看代码缺省值，要看提取出的 json）
python3 -c "import json;d=json.load(open('Extract/extract/Texts/UnitsMiscGame.json'))['keys'];print(d['Misc.AgiDefenseBonus'],d['Misc.NeedHeroXP'])"

# 5. 行为验收
cd Launcher && dotnet build && ./launcher.command -- --play=test.gmap --state-probe
```

第 4 条最容易被漏：**代码里的 `0.15f`/`0.30f` 只是缺省，真值在 `Extract/extract/Texts/*.json`**。本轮就有一条结论（敏捷护甲）因为只看代码缺省而判错，后被 json 推翻。

---

## 8. 一句话

结构镜像和 P0 数值都已完成，剩下的差异分三种：**读错列/算错公式的数值错误（能当天修）**、**实现深度差一到两个量级的管线（要立项）**、**客户端本来就没有的功能（不要修）**。

最容易踩的坑是把后两种当成第一种去估工，或者拿 `diff -rq` 当差异检测工具——同路径文件平均只有 13.5% 的重合行，它只会告诉你「都不一样」。
