# Launcher 与 Open-Warcraft3 差异清单与同步计划

> 对象：`/Volumes/Data/Work/WorldEditor/Launcher`（Godot 4 + C#，46.8k 行，HEAD `4d76270` 2026-11-12）
> 基准：`/Volumes/Data/Work/WorldEditor/test/Open-Warcraft3`（295.9k 行，HEAD `14b62fd5` 2026-11-11，**按安卓版运行路径取基准**）
> 生成时间：2026-11-12
> 说明：`Open-Warcraft3_Wiki` 是同一仓库的较早快照（少若干次推送），不作为基准；本文全部结论以 `test/Open-Warcraft3` 当前工作树为准。

---

## 0. 本文的基准口径（先看这一节，否则映射表会被误读）

### 0.1 为什么是"安卓版"

客户端 L4 用 `GameCore/L0_Foundation/RHI/IRenderDevice.cs` 做渲染抽象：

| 层 | 桌面版 | 安卓版 | 是否共用 |
|---|---|---|---|
| 模拟 | `GameCore/L3_Simulation/` + `Src/` | 同 | **共用** |
| 场景/表现/音频/UI | `GameCore/L4_Composition/Game/` | 同（`new War3GameScene(host)`，见 `Android/AndroidGlesRenderer.cs:1240`） | **共用** |
| 渲染后端 | `L1_Adapter/RHI.OpenGL/OpenGLRenderDevice.cs` | `Android/AndroidGlesRenderer.cs` | 分别实现 `IRenderDevice` |
| 输入物理层 | `Game/Input/War3InputComponent.cs`（键鼠） | `Android/AndroidPhysicalInputRouter.cs` + `TouchGestureRecognizer.cs` | 不同 |
| UI 控件 | `Game/UI/*`（FDF 帧树） | 同，另加 `Android/Ui/*` 触屏部件 + `Scenes/War3GameScene/AndroidUiBridge.cs` | 部分 |
| 启动/资源 | `Program.cs`、`Game/Hosting/`、`Game/Imgui/`、`Game/Shaders/` | `Android/MainActivity*`、`Android/AndroidResourcePack.cs` | 不同 |

`Game/` 下 **没有一处 `#if ANDROID`**，说明 L4 Game 层是平台无关的；安卓的差异全部收敛在 `Android/`。
所以本文的基准 = **L3 + Game/ 共用部分**，加上 **Android/ 的输入与 UI 接入点**。

### 0.2 明确不采用的桌面专属代码

以下桌面专属文件**不作为**对齐目标，映射表中标注「桌面专属」：

- `GameCore/L4_Composition/Game/Program.cs`（命令行入口）
- `GameCore/L4_Composition/Game/Hosting/`（桌面启动器、Lua/生产模式）
- `GameCore/L4_Composition/Game/Imgui/`（ImGui 调试面板）
- `GameCore/L4_Composition/Game/Shaders/`（桌面 GLSL，Launcher 用 `.gdshader`）
- `Game/Input/War3InputComponent.cs` 的键鼠绑定部分（安卓走 `AndroidPhysicalInputRouter`）
- `Game/UI/MainMenuScreen.cs`、`MapSelectionScreen.cs`、`MenuScreenHost.cs`（桌面菜单，安卓走 `NativeMapLobbyView.cs` + `Android/Ui/`）

### 0.3 Launcher 的定位

Launcher 不是客户端的移植，而是**按功能重写的删减版**（16% 代码量）。文件命名大量直接沿用客户端（69 个文件同名），但内部逻辑是按"能跑对战"重写的。

### 0.4 路径镜像原则（本文的核心建议）

**现状的问题**：目录结构不同，同步时无法直接定位。

| Launcher 现有位置 | 客户端实际位置 | 同步时的代价 |
|---|---|---|
| `src/World/World.WeaponSystem.cs` | `Src/Components/WeaponSystem.cs` | 要先查表，才知道改哪个文件 |
| `src/Units/Unit.Combat.cs` | `Src/Units/Unit.Combat.cs` | 同名，唯一不用查的一类 |
| `src/Simulation/Configuration/UnitCatalog.cs` | `GameCore/L3_Simulation/Simulation/Configuration/UnitFactory.cs` | 名字都不一样 |
| `src/Environment/TerrainGridFactory.cs` | `GameCore/L4_Composition/Game/Environment/TerrainGridFactory.cs` | 同名但差了 4 层目录 |

**结论**：Launcher 应改用与客户端**完全相同的相对路径与文件名**。这样"映射表"退化成"同路径即同源"，同步成本从"查表 + 猜"降到"同路径直接 diff"。

#### 目标目录结构

```text
Launcher/
├── Src/                                  # 镜像客户端 Src/
│   ├── Abilities/                        # Ability.cs、Buff.cs、CAbility*.cs
│   ├── Components/                       # WeaponSystem.cs、Health*.cs、OrderQueue.cs
│   ├── Configuration/                    # War3GameplayConfig.cs
│   ├── Core/                             # World.cs、DamageData.cs、FogOfWar.cs、Pathing/
│   ├── Items/                            # ItemBonus.cs、NativeItemCharges.cs
│   ├── Skills/                           # 每个技能一个文件，与客户端同名
│   └── Units/                            # Unit*.cs（含 Unit.Combat.cs、Unit.Items.cs）
├── GameCore/                             # 镜像客户端 GameCore/
│   ├── L1_Adapter/Formats/               # War3Pathing.cs、Text/
│   ├── L2_Core/MapBrowser/               # MapInfoParser.cs、TerrainParser.cs
│   ├── L3_Simulation/Simulation/
│   │   ├── Configuration/                # Runtime*Ability.cs、War3ObjectData.cs
│   │   ├── Jass/                         # 仅当搬运 melee native 时才需要
│   │   └── Maps/                         # War3MapUnits.cs、War3MapDoodads.cs
│   └── L4_Composition/Game/
│       ├── Environment/ Presentation/ Audio/ Camera/ Rendering/ UI/
│       └── Scenes/War3GameScene/
└── Platform/Godot/                       # 客户端无对应，Godot 壳
    ├── App/                              # LauncherApp.cs
    ├── Nodes/                            # Node3D / Control 派生类（渲染实现）
    ├── Assets/                           # 资源装配（ModelLibrary、DoodadMeshLibrary…）
    └── Extensions/                       # Gmap 导入、MapBrowser、GameOverPanel 等原创件
```

#### 五条配套规则

1. **namespace 跟客户端**：领域代码用 `OpenWC3` / `OpenWC3.Game`，Godot 壳用 `OpenWC3.Launcher`。这样从客户端复制代码时不用改 `using`，也不会因为命名空间不同而看不出是同源文件。
2. **1:1 对齐**：一个客户端文件对应一个 Launcher 文件。既不把客户端的多个文件合并成一个，也不把一个客户端文件拆成多个。唯一例外是现存聚合文件（见规则 4），需要先拆开才能对齐。
3. **取消 `partial class World` 的横向拆分**：现在 `World.WeaponSystem.cs`、`World.Orders.cs`、`World.Targeting.cs` 都是同一个类的 partial 分册，客户端对应的是独立类（`WeaponSystem`、`OrderQueue`、`World.Targeting`）。这类要按客户端拆成独立类，否则"同路径"只是表面一致、diff 依旧对不上。
4. **聚合文件按客户端拆分**：`src/Skills/` 现在是 103 个文件装 220 个类（如 `HumanUnits.cs` 装 7 个技能），客户端是 190 个 `Runtime*Ability.cs`。在 P-1g 阶段按技能拆开，拆完与客户端 1:1。
5. **Godot 特有能力不进镜像目录**：`Node3D` 子类、`_Process`、资源加载这些放 `Platform/Godot/`，用继承或 partial 挂到领域类上，保证镜像目录里的文件"内容可逐行对照"。

#### 执行方式与收益

- 全部用 `git mv` 移动，保留 blame 历史。
- 每移一批：`dotnet build` + `--combat-probe` + `--state-probe` 必须仍全过。
- 移完自检：`diff -rq Launcher/Src test/Open-Warcraft3/Src` 应只剩"Launcher 尚未实现"的文件，不应有"同路径不同内容"的意外。
- 收益：客户端每次改动后，`diff -rq` 一遍就知道哪几个 Launcher 文件要跟；不用再维护本文第 2 章那张表（那张表会退化成"迁移进度清单"）。

> 这一步不改变任何行为，纯结构搬迁，但它是后续所有同步的地基，所以排在 P0 之前（见 4.2 的 P-1）。

---

## 1. 结论摘要

### 1.1 一句话

**不是一一对应**：模拟主干（伤害、订单、生产、技能）与客户端结构同构，可逐文件对照；表现层是"重写简化版"，有明确缺口；数值层存在**整表取错档**的系统性错误。

### 1.2 三类问题（按危害排序）

| 类型 | 说明 | 代表 |
|---|---|---|
| **A. 数值取错源** | 读错表/读错列/字段错位，导致所有相关数值偏离 | Misc 规则表读了 RoC 版；物品三围字段错位 |
| **B. 规则缺分支** | 客户端有判定分支，Launcher 没有，表现为"少一条规则" | 虚无物理免疫、在建建筑护甲类型 |
| **C. 表现缺实现** | 客户端有渲染/音频实现，Launcher 整类缺失 | 岸边浪、昼夜光照、距离雾、贴地阴影 |

### 1.3 量化

| 指标 | Launcher | 客户端 | 说明 |
|---|---|---|---|
| 代码行数 | 46.8k | 295.9k | 16% |
| cs 文件数 | 286 | 1518 | — |
| 同名文件 | 69（24%） | — | 命名已尽量照搬 |
| 技能注册码 | 513 | 407（工厂显式注册） | 共同码 295 |
| 技能类覆盖 | 220 个类 | 253 个类 | 按码对齐 210 类 |
| 映射覆盖 | 286 / 286 | — | 同名 69 + 技能码级 103 + 逐条核对 114 |

---

## 2. 全量文件映射

> 说明：一行一个 Launcher 文件。「客户端对应」为**安卓版实际运行路径**。
> 「客户端对应」列同时就是**迁移目标路径**（按 0.4 的镜像原则）：单值即目标路径；多值取第一项为主落点，其余是拆分后的落点；「—」表示迁入 `Platform/Godot/`。
> 证据等级：**A** = 两侧代码亲读确认；**B** = 同名同构 + 抽样确认；**C** = 按职责推断，待复核。

### 2.1 模拟核心：World / Units / Components

| Launcher | 客户端对应（安卓版路径） | 等级 | 备注 |
|---|---|---|---|
| `World/World.cs` | `Src/Core/World.cs` | A | tick 入口、单位集合、相位调度 |
| `World/World.Abilities.cs` | `Src/Components/AbilityContainer*.cs` | A | 能力容器/光环/法球汇总 |
| `World/World.Blight.cs` | `RuntimeBlightGrowthAbility.cs` + 地形 SetBlightArea | C | 腐地铺设 |
| `World/World.BuffMembers.cs` | `Src/Abilities/Buff.cs` | A | Buff 挂载/刷新 |
| `World/World.Cargo.cs` | `Src/Units/CargoTransport.cs` | B | Launcher 只有建筑驻军 |
| `World/World.Channels.cs` | `Src/Abilities/*Channel*` + `Unit.Step.cs` | A | 引导中断条件不同 |
| `World/World.CorpseRevive.cs` | `RuntimeResurrectionAbility.cs`、`RuntimeAnimateDeadAbility.cs` | A | 两者被合并成一个函数（差异见 3.6） |
| `World/World.CreepSleep.cs` | `CAbilitySleepAlways.cs` + 昼夜判定 | C | 野怪昼夜睡眠 |
| `World/World.DamageMembers.cs` | `Src/Components/Health.cs` + `Health.DamageScaling.cs` | A | 唯一伤害入口，缺虚无/吸收层 |
| `World/World.DeathTriggers.cs` | `Src/Components/Death.cs` | A | 死亡触发/重生 |
| `World/World.FogRevealMembers.cs` | `Src/Core/FogOfWar.cs`（Reveal 部分） | C | 揭雾 |
| `World/World.GroundSpells.cs` | `Src/Abilities/*GroundSpell*` | C | 地面法术 |
| `World/World.GroupMovement.cs` | `Src/Core/Pathing/Group/MovementGroup.cs` | A | 编队移动，语义差异大 |
| `World/World.HeroRevive.cs` | `JassGameNatives.Production.cs`（祭坛单） + `GameplayConstants.cs` | A | 自动复活 vs 下单复活 |
| `World/World.HeroSelectionOrder.cs` | `Src/Core/World.cs:502` + `Src/Skills/Hero.cs` | A | 同名不同物：客户端该文件是"英雄序号" |
| `World/World.Illusions.cs` | `Src/Units/Unit.cs`（`IsIllusion` 字段）+ `Src/Core/World.cs` | B | 客户端无 `Unit.Illusion.cs`，幻象字段挂在 Unit 主体 |
| `World/World.Items.cs` | `JassGameNatives.OrdersAndItems.cs`（真实文件名带前缀） | A | 拾取/出售/掉落 |
| `World/World.MechanismMembers.cs` | `JassGameNatives.Harvest.cs` + `Construction.cs` | A | 采集/砍木/建造 |
| `World/World.Morph.cs` | `Src/Units/Unit.TypeChange.cs` | A | 变形 |
| `World/World.MovementEvents.cs` | `Src/Core/Pathing/Move/MovementExecution.cs` | A | 单步移动，无阻挡校验 |
| `World/World.Orders.cs` | `Src/Components/OrderQueue.cs` + `Orders/COrder*.cs` | A | 队列深度 12 vs 500 |
| `World/World.PhoenixEgg.cs` | `RuntimePhoenixAbility.cs` | B | 凤凰蛋 |
| `World/World.Rally.cs` | `Src/Abilities/CAbilityRally.cs` + `CRallyPoint.cs` | A | 集结点 |
| `World/World.RelationMembers.cs` | `Src/Core/TargetRules.cs`（RelationOf） | B | 归属关系 |
| `World/World.Repair.cs` | `RuntimeRepairAbility.cs` + `Construction.cs` | B | 修理 |
| `World/World.RepulseMembers.cs` | `Src/Core/Pathing/Contact/CPoSeparate.cs` | A | 软分离 + Launcher 额外硬解重叠 |
| `World/World.Targeting.cs` | `Src/Core/World.Targeting.cs` + `Components/Ai.cs` | A | 索敌节奏/评分不同 |
| `World/World.UnitLookup.cs` | `Src/Core/World.Targeting.cs`（空间查询） | C | 邻域查询 |
| `World/World.WeaponSystem.cs` | `Src/Components/WeaponSystem.cs` + `Src/Core/Projectile.cs` | A | 攻击时序/投射物/溅射 |
| `World/TreeState.cs` | `GameCore/L3_Simulation/Simulation/Jass/JassGameNatives.DestructableCombat.cs` + `War3CustomObjectData.cs`（`DestructableTypeData`） | B | 客户端无独立树状态类 |
| `Units/Unit.cs` | `Src/Units/Unit.cs` + `Unit.TypeFields.cs` | A | 单位主体 |
| `Units/Unit.Abilities.cs` | `Src/Units/Unit.Facade.cs` + `AbilityContainer` | B | 能力访问 |
| `Units/Unit.Channel.cs` | `Src/Units/Unit.Step.cs`（引导段） | B | 引导 |
| `Units/Unit.Combat.cs` | `Src/Units/Unit.Combat.cs` | A | 同名，起手/后摇 |
| `Units/Unit.Illusion.cs` | `Src/Units/Unit.cs`（幻象字段段） | B | 同名不同物：客户端幻象逻辑在 `Src/Core/World.cs` |
| `Units/Unit.Items.cs` | `Src/Units/Unit.Items.cs` | A | 同名，容量来源不同 |
| `Units/Unit.Lifecycle.cs` | `Src/Units/Unit.Lifecycle.cs` | A | 尸体/复活 |
| `Units/Unit.Morph.cs` | `Src/Units/Unit.TypeChange.cs` | B | 变形 |
| `Units/Unit.Ownership.cs` | `Src/Units/Unit.Facade.cs`（归属） | C | 归属/控制权 |
| `Units/Unit.Slows.cs` | `Src/Abilities/Buff.cs`（减速源） | B | 多来源减速取 max |
| `Components/State.cs` | `Src/Components/State.cs` | A | 状态位 |

### 2.2 数据与配置：Simulation/

| Launcher | 客户端对应 | 等级 | 备注 |
|---|---|---|---|
| `Simulation/Configuration/War3Tables.cs` | `GameCore/L3_Simulation/Simulation/Configuration/War3SlkReader.cs` | B | 表缓存，客户端 17 张 SLK，Launcher 12 张 |
| `Simulation/Configuration/UnitCatalog.cs` | `UnitFactory.cs` + `UnitFactory.TypeData.cs` + `War3ObjectData.cs` | A | 单位静态数据 |
| `Simulation/Configuration/AbilityCatalog.cs` | `RuntimeObjectData`/`War3ObjectData`（Ability 段） | B | 技能静态数据 |
| `Simulation/Configuration/ItemCatalog.cs` | `War3CustomObjectData.cs:250`（ItemTypeData） | B | 物品静态数据 |
| `Simulation/Configuration/TechTree.cs` | `War3ObjectData.cs:291-330` + `JassGameNatives.Production.cs` | A | 生产/科技 |
| `Simulation/Configuration/HeroRules.cs` | `Src/Skills/Hero.cs` + `Src/Skills/HeroStat.cs` + `War3GameplayConfig.Hero*` | A | 英雄成长 |
| `Simulation/Configuration/MiscRules.cs` | `Src/Configuration/War3GameplayConfig.cs` | A | **表源错档，见 3.1** |
| `Simulation/Configuration/War3GameplayConfig.cs` | `Src/Configuration/War3GameplayConfig.cs` | A | 同名，矩阵来源不同 |
| `Simulation/Configuration/MeleeRules.cs` | `Scripts/Blizzard.j` 的 `Melee*`（JASS） | A | 引擎内重写 |
| `Simulation/Configuration/War3Text.cs` | `L1_Adapter/Formats/Text/*` + `War3TextMacroResolver.cs` | B | 文本占位符 |
| `Simulation/Configuration/LightningTable.cs` | `RuntimeAbilityFactory` 的 Lightning 映射 | C | 闪电码表 |
| `Simulation/Configuration/FogRules.cs` | `Src/Core/FogOfWar.cs`（常量） | C | 迷雾参数 |
| `Simulation/GroupFormation.cs` | `Src/Core/Pathing/Group/MovementFormationOffsets.cs` | A | 落点算法不同 |
| `Simulation/AiTactics.cs` | — | — | Launcher 原创 |

### 2.3 技能：Skills/（103 文件，逐文件映射见 2.7）

技能层的映射不按文件名，而按**技能码**：Launcher 的 `SkillRegistry` 注册 513 个码，客户端 `RuntimeAbilityFactory` 显式注册 407 个码，两者共同码 295。逐文件映射见 2.7 表。

### 2.4 表现：Environment / Presentation / Audio / Assets

| Launcher | 客户端对应 | 等级 | 备注 |
|---|---|---|---|
| `Environment/MapWorldBuilder.cs` | `Game/Scenes/War3GameScene/Terrain.cs:32-73` | B | Launcher 原创组装器 |
| `Environment/TerrainGridFactory.cs` | `Game/Environment/TerrainGridFactory.cs` | A | 同名，层级混合参数不同 |
| `Environment/TerrainTextureSet.cs` | `Game/Environment/TerrainTextureSet.cs` | A | 同名 |
| `Environment/TerrainTextureLoader.cs` | `Game/Environment/TerrainTextureLoader.cs` | A | 同名 |
| `Environment/LayerAtlas.cs` | `Game/Environment/TerrainTextureSet.cs`（图集） | C | 层图集 |
| `Environment/CliffRenderer.cs` | `Game/Environment/CliffRenderer.cs` | A | 同名 |
| `Environment/CliffSurface.cs` | `Game/Environment/CliffSurface.cs` | A | 同名 |
| `Environment/CliffTextureSet.cs` | `Game/Environment/War3TerrainConfig.cs`（CliffType） | B | 悬崖贴图配置 |
| `Environment/WaterRenderer.cs` | `Game/Environment/WaterRenderer.cs` | A | 同名 |
| `Environment/War3WaterConfig.cs` | `Game/Environment/War3TerrainConfig.cs` | B | 水色/水深配置 |
| `Environment/WeatherRenderer.cs` | `Game/Environment/WeatherRenderer.cs` + `WeatherDefinition.cs` | A | 粒子数取列错误 |
| `Environment/DoodadRenderer.cs` | `Game/Environment/DoodadRenderer.cs` | A | 同名，无动画/无记忆 |
| `Environment/BlightMask.cs` | `Game/Environment/ScoutedBlight.cs` + `TerrainRenderer.cs:220-275` | B | 机制不同 |
| `Environment/SkyboxRenderer.cs` | `Game/Rendering/SkyboxRenderer.cs` | A | 同名 |
| `Environment/SkyDomeRenderer.cs` | `Game/Rendering/SkyboxRenderer.cs`（穹顶） | C | 模型穹顶 |
| `Environment/War3Paths.cs` | `Game/Environment/War3Paths.cs` | A | 同名 |
| `Presentation/UnitSceneComponent.cs` | `Game/Presentation/UnitSceneComponent.cs` + `CharacterRenderer.cs` + `RenderUnitPresentation.cs` + `UnitAnimationController.cs` + `UnitStateColor.cs` | A | 差距最大的一处：一个类顶五处 |
| `Presentation/AttachmentPoints.cs` | `Game/Rendering/FrameAttachmentCache.cs` + `CharacterRenderer.cs:1616-1674` | B | 挂点 |
| `Presentation/EffectArtTable.cs` | `Game/Scenes/War3GameScene/Presentation.cs:1057-1082`（AbilityData arts） | B | 自造硬编码表 |
| `Presentation/RallyFlagView.cs` | `Game/Rendering/RallyPointDisplay.cs` | B | 集结点旗帜 |
| `Audio/War3AudioSystem.cs` | `Game/Audio/War3AudioSystem.cs`（75 行 vs 1022 行） | A | 同名，声部/优先级缺失 |
| `Audio/War3SoundTables.cs` | `Game/Audio/War3SoundTables.cs`（4 张 vs 12 张） | A | 同名 |
| `Audio/War3MusicPlayer.cs` | `Game/Audio/War3MusicPlayer.cs` + `War3MusicCatalog.cs` | A | 同名，无曲间间隔 |
| `Assets/Assets.cs` | `Game/Assets/`（Branding/Music/UI）+ `Android/AndroidResourcePack.cs` | C | 资源入口 |
| `Assets/War3Assets.cs` | `GameCore/L4_Composition/Game/Assets/`（资源注册） | C | 资源装配 |
| `Assets/PathResolve.cs` | `Game/Environment/War3Paths.cs` | C | 路径解析 |
| `Assets/ModelLibrary.cs` | `Game/Rendering/MdxLoader.cs` | B | 模型库 |
| `Assets/UnitMeshLibrary.cs` | `Game/Rendering/CharacterRenderer*.cs` | C | 单位网格 |
| `Assets/DoodadMeshLibrary.cs` | `Game/Environment/DoodadRenderer.cs`（网格缓存） | C | 装饰物网格 |

### 2.5 场景与交互：Scenes / Camera / UI / Input

| Launcher | 客户端对应（安卓版路径） | 等级 | 备注 |
|---|---|---|---|
| `Scenes/War3GameScene/Lifecycle.cs` | `Game/Scenes/War3GameScene/Lifecycle.cs` | A | 同名，`IsReignOfChaos` 判定错 |
| `Scenes/War3GameScene/Units.cs` | `Game/Scenes/War3GameScene/Units.cs` | A | 同名，开局清场实现一致 |
| `Scenes/War3GameScene/Ui.cs` | `Game/Scenes/War3GameScene/Ui.cs` + `Android/Ui/*` | A | 同名，命令卡/面板大幅简化 |
| `Scenes/War3GameScene/Input.cs` | `Game/Scenes/War3GameScene/Input.cs` + `Android/AndroidPhysicalInputRouter.cs` + `TouchGestureRecognizer.cs` | A | 安卓触屏为主，键鼠为辅 |
| `Scenes/War3GameScene/Commands.cs` | `Game/Input/PlayerCommandProcessor.cs` + `Game/UI/CommandCardBuilder*.cs` | A | 命令下发 |
| `Scenes/War3GameScene/QuickCast.cs` | `Game/Scenes/War3GameScene/QuickCast.cs`（同名不同物）+ `Game/Input/War3CommandInteractionComponent.Abilities.cs` | B | 客户端同名文件是安卓自动选目标 |
| `Scenes/War3GameScene/Melee.cs` | `Scripts/Blizzard.j`（`Melee*`）+ `JassGameNatives.Production.cs` | A | JASS → C# 重写 |
| `Scenes/War3GameScene/Lobby.cs` | `JassGameNatives.ApplyLobbyConfiguration` + `InitializePlayerAlliances` | B | 座位/同盟 |
| `Scenes/War3GameScene/Production.cs` | `GameCore/L3_Simulation/Simulation/Jass/JassGameNatives.Production.cs` + `JassGameNatives.StockAndShops.cs` + `Game/UI/CommandCardBuilder.Production.cs` | A | 客户端无同名场景文件；训练/研究/商店在 JASS native 层 |
| `Scenes/War3GameScene/Presentation.cs` | `Game/Scenes/War3GameScene/Presentation.cs`（2311 行） | A | 同名，表现总装 |
| `Scenes/War3GameScene/Effects.cs` | `Game/Rendering/MdxParticle2Renderer.cs` + `Src/Core/SpecialEffect.cs` | B | 特效 |
| `Scenes/War3GameScene/Floats.cs` | `Src/Core/FloatingText.cs` | A | 飘字 |
| `Scenes/War3GameScene/Lightning.cs` | `Game/Rendering/BattleOverlayRenderer.cs`（闪电） | B | 闪电噪声 |
| `Scenes/War3GameScene/Terrain.cs` | `Game/Scenes/War3GameScene/Terrain.cs` | A | 同名 |
| `Scenes/War3GameScene/Environment.cs` | `Game/Scenes/War3GameScene/Rendering.cs` + `FogOfWarRenderer.cs` | B | 可见性/迷雾刷新 |
| `Scenes/War3GameScene/Rally.cs` | `Game/Scenes/War3GameScene/Rally.cs` | A | 同名 |
| `Scenes/War3GameScene/Ai.cs` | `JassGameNatives.Ai*.cs` + `.ai` 脚本（AMAI） | A | 机制完全不同 |
| `Scenes/War3GameScene/AiCounters.cs` | — | — | Launcher 原创 |
| `Scenes/War3GameScene/Cheats.cs` | `Game/Scenes/War3GameScene/Cheats.cs` + `NativeDiagnostics.cs` | B | 调试命令 |
| `Scenes/War3GameScene/CameraFollowTrack.cs` | `Game/Scenes/War3GameScene/CameraFollowTrack.cs`（同名不同物） | C | 客户端是安卓跟随开关 |
| `Camera/War3Camera.cs` | `Game/Camera/War3Camera.cs` + `War3CameraComponent.cs` | A | 同名，缺边缘滚屏 |
| `UI/MeleeHudController.cs` | `Game/UI/MeleeHudController.cs` + `TopBarFrame/UnitInfoPanelFrame/InventoryFrame/...` | A | 一个类顶整棵 FDF 帧树 |
| `UI/MinimapFrame.cs` | `Game/UI/MinimapFrame.cs` + `MinimapUnitView.cs` | A | 同名，无按钮列/图标体系 |
| `UI/HeroBarController.cs` | `Game/UI/HeroBarFrame.cs` | B | 英雄栏 |
| `UI/EscMenu.cs` | `Game/UI/EscMenuFrame.cs` | B | 菜单 |
| `UI/GameLogPanel.cs` | `Game/UI/ChatFrame.cs` | B | 消息 |
| `UI/GameOverPanel.cs` | — | — | Launcher 原创 |
| `UI/LoadingScreen.cs` | `Game/Rendering/War3LoadingScreenRenderer.cs` | B | 载入画面 |
| `MapBrowser/MapBrowser.cs` | `GameCore/L2_Core/MapBrowser/*` | B | 选图 |

### 2.6 平台与格式：Core / App / Maps / Diagnostics

| Launcher | 客户端对应 | 等级 | 备注 |
|---|---|---|---|
| `App/LauncherApp.cs` | `Program.cs` / `Android/MainActivity.cs` | — | 桌面专属对照，**不作对齐目标** |
| `Core/Map/MapDocument.cs` | — | — | Launcher 自有地图抽象 |
| `Maps/GmapImporter.cs` | — | — | Launcher 自有 `.gmap` 格式 |
| `Maps/GmapPackage.cs` | — | — | 同上 |
| `Maps/MapSource.cs` | `GameCore/L3_Simulation/Simulation/Maps/` | C | 地图来源 |
| `Maps/PathGrid.cs` | `Src/Core/Pathing/Search/CLrPathingSys.cs` + `CLrPathingAcc.cs` | A | A* 代价/预算不同 |
| `Maps/War3Strings.cs` | `GameCore/L1_Adapter/Formats/Text/War3TextEncoding.cs` | C | WTS 字符串表 |
| `Core/Formats/Mpq/*.cs`（8 个） | `GameCore/L0_Foundation/ThirdParty/War3Net/War3Net.IO.Mpq/*` | B | 第三方 MPQ，等价 |
| `Core/Formats/Slk/SlkTable.cs` | `War3SlkReader.cs` | B | SLK 表 |
| `Core/Formats/W3x/W3eTerrain.cs` | `GameCore/L3_Simulation/Simulation/Maps/`（地形解析） | C | w3e |
| `Core/Formats/W3x/W3iInfo.cs` | 同上（w3i） | C | 地图信息 |
| `Core/Formats/W3x/W3mPathing.cs` | `L1_Adapter/Formats/War3Pathing.cs` | B | wpm |
| `Core/Formats/W3x/W3oDoodads.cs` | 同上（doo） | C | 装饰物 |
| `Core/Formats/W3x/W3oUnits.cs` | 同上（units.doo） | C | 预置单位 |
| `Core/Formats/W3x/W3xObjectData.cs` | `War3ObjectData.cs` / `War3CustomObjectData.cs` | C | 物编 |
| `Core/Formats/W3x/WtsDocument.cs` | `L1_Adapter/Formats/Text/` | C | WTS |
| `Core/Formats/W3x/W3xBinaryReader.cs` | — | — | Launcher 自有 IO |
| `Core/Formats/W3x/W3xBinaryWriter.cs` | — | — | 仅写 `.gmap` 用 |
| `Core/Formats/W3x/W3xImports.cs` | 同上（导入表） | C | 导入 |
| `Core/Combat/DamageInfo.cs` | `Src/Core/DamageData.cs` | A | 伤害上下文 |
| `Core/Combat/WeaponProfile.cs` | `Src/Core/Weapon.cs` + `Weapon.TypeFields.cs` | A | 武器参数 |
| `Core/Combat/AttackType.cs` | `Src/Core/AttackType.cs` | A | 同名 |
| `Core/Combat/DefenseType.cs` | `Src/Core/DefenseType.cs` | A | 同名 |
| `Core/Combat/DamageType.cs` | `Src/Core/DamageType.cs` | A | 客户端 22 种，Launcher 3 种 |
| `Core/Combat/TargetFlags.cs` | `Src/Core/TargetFlags.cs` + `NativeTargetMask.cs` | A | 位定义不同 |
| `Core/Combat/OrbEffect.cs` | `RuntimeItemOrbAbility.cs` / `RuntimeOrbAttackAbility.cs` | B | 法球 |
| `Core/Combat/CombatCodes.cs` | 分散（`AttackType` 解析） | C | 码解析工具 |
| `Core/Abilities/SpellTargets.cs` | `Src/Core/NativeSpellTargetRules.cs` + `TargetFlags.cs` | A | 目标位 |
| `Core/Abilities/AbilityTargetMode.cs` | `Src/Abilities/AbilityTargetMode.cs` | A | 同名 |
| `Diagnostics/*.cs`（22 个） | `Game/Diagnostics/*`（部分） | C | 探针，客户端无等价 1:1 |

### 2.7 技能文件映射（Skills/ 全部 103 个文件）

> 依据：`SkillRegistry` 注册码 → 客户端 `RuntimeAbilityFactory` 同码类 → 该类所在文件。
> 「客户端类」列列出 Launcher 文件中主要类对应的客户端类；一个文件含多个类时列前 3 个。

| Launcher 文件 | 客户端类 | 客户端文件 |
|---|---|---|
| `Skills/AncestralSpirit.cs` | `RuntimeAncestralSpiritAbility` | `RuntimeAncestralSpiritAbility.cs` |
| `Skills/AnimateDead.cs` | `RuntimeAnimateDeadAbility` | `RuntimeAnimateDeadAbility.cs` |
| `Skills/Avatar.cs` | `RuntimeMetamorphosisAbility` | `RuntimeMetamorphosisAbility.cs` |
| `Skills/Banish.cs` | `RuntimeBanishAbility` | `RuntimeBanishAbility.cs` |
| `Skills/Bash.cs` | `RuntimeBashAbility` | `RuntimeBashAbility.cs` |
| `Skills/BigBadVoodoo.cs` | `RuntimeBigBadVoodooAbility` | `RuntimeHumanOrcUltimates.cs` |
| `Skills/BlackArrow.cs` | `RuntimeBlackArrowAbility` | `RuntimeBlackArrowAbility.cs` |
| `Skills/Bladestorm.cs` | （辅助类，随主类） | — |
| `Skills/BlightAbilities.cs` | `CAbilityBlightedGoldMine`、`RuntimeBlightGrowthAbility` | `CAbilityGoldMineBase.cs`、`RuntimeBlightGrowthAbility.cs` |
| `Skills/Blink.cs` | `Blink` | `Blink.cs` |
| `Skills/Blizzard.cs` | `RuntimeBlizzardAbility` | `RuntimeAbilityFactory.cs` |
| `Skills/BrillianceAura.cs` | `RuntimeBrillianceAuraAbility` | `RuntimeBrillianceAuraAbility.cs` |
| `Skills/BurrowGarrison.cs` | `RuntimeBattleStationsAbility`、`RuntimeBunkerAbility`、`RuntimeStandDownAbility` | `RuntimeCargoHoldAbility.cs`、`RuntimeStandDownAbility.cs` |
| `Skills/Cannibalize.cs` | `RuntimeCannibalizeAbility`、`RuntimeRaiseDeadAbility` | `RuntimeCannibalizeAbility.cs`、`RuntimeRaiseDeadAbility.cs` |
| `Skills/CarrionScarabs.cs` | `RuntimeCarrionBeetlesAbility` | `RuntimeCarrionBeetlesAbility.cs` |
| `Skills/CarrionSwarm.cs` | `RuntimeCarrionSwarmAbility` | `RuntimeCarrionSwarmAbility.cs` |
| `Skills/ChainLightning.cs` | `ChainLightning` | `ChainLightning.cs` |
| `Skills/Charm.cs` | `RuntimeCharmAbility` | `RuntimeCharmAbility.cs` |
| `Skills/ColdArrows.cs` | `RuntimeColdArrowsAbility` | `RuntimeColdArrowsAbility.cs` |
| `Skills/Couple.cs` | `RuntimeCoupleAbility`、`RuntimeDecoupleAbility` | `RuntimeCoupleAbility.cs`、`RuntimeDecoupleAbility.cs` |
| `Skills/CreepAbilities.cs` | `RuntimeEatTreeAbility`、`RuntimeFrenzyAbility`、`RuntimePermanentImmolationAbility` 等 5 个 | 同名文件等 5 个 |
| `Skills/CriticalStrike.cs` | `CriticalStrike` | `CriticalStrike.cs` |
| `Skills/DarkConversion.cs` | `DarkConversion`、`DarkConversionBuff` | `CampaignAndAuraSkills.cs` |
| `Skills/DarkRitual.cs` | `RuntimeDarkRitualAbility` | `RuntimeUndeadHeroAbilities.cs` |
| `Skills/DeathAndDecay.cs` | `RuntimeDeathAndDecayAbility` | `RuntimeClassicHeroAbilities.cs` |
| `Skills/DeathCoil.cs` | `RuntimeDeathCoilAbility` | `RuntimeUndeadHeroAbilities.cs` |
| `Skills/DeathPact.cs` | `RuntimeDeathPactAbility` | `RuntimeUndeadHeroAbilities.cs` |
| `Skills/DevotionAura.cs` | `DevotionAura` | `DevotionAura.cs` |
| `Skills/DivineShield.cs` | `RuntimeDivineShieldAbility` | `RuntimeDivineShieldAbility.cs` |
| `Skills/DruidForm.cs` | `RuntimeDruidFormAbility` | `RuntimeDruidFormAbility.cs` |
| `Skills/Earthquake.cs` | `RuntimeEarthquakeAbility` | `RuntimeHumanOrcUltimates.cs` |
| `Skills/EnduranceAura.cs` | `EnduranceAura` | `EnduranceAura.cs` |
| `Skills/EntanglingRoots.cs` | `RuntimeEntanglingRootsAbility` | `RuntimeEntanglingRootsAbility.cs` |
| `Skills/Evasion.cs` | `Evasion` | `Evasion.cs` |
| `Skills/FanOfKnives.cs` | `RuntimeFanOfKnivesAbility` | `RuntimeAbilityFactory.cs` |
| `Skills/FarSight.cs` | `FarSight` | `CampaignAndAuraSkills.cs` |
| `Skills/FingerOfDeath.cs` | `FingerOfDeath` | `CampaignAndAuraSkills.cs` |
| `Skills/FlameStrike.cs` | `RuntimeFlameStrikeAbility` | `RuntimeFlameStrikeAbility.cs` |
| `Skills/ForkedLightning.cs` | `RuntimeForkedLightningAbility` | `RuntimeForkedLightningAbility.cs` |
| `Skills/FrostArmor.cs` | `RuntimeFrostArmorAbility` | `RuntimeClassicSummonAndArmorAbilities.cs` |
| `Skills/FrostNova.cs` | `RuntimeFrostNovaAbility` | `RuntimeFrostNovaAbility.cs` |
| `Skills/GrabTree.cs` | `RuntimeGrabTreeAbility`、`RuntimeGrabTreeBuff` | `RuntimeGrabTreeAbility.cs` |
| `Skills/Heal.cs` | `RuntimeHealAbility` | `RuntimeHealAbility.cs` |
| `Skills/HealingWave.cs` | `RuntimeHealingWaveAbility` | `RuntimeClassicHeroAbilities.cs` |
| `Skills/Hex.cs` | `RuntimeHexAbility` | `RuntimeHexAbility.cs` |
| `Skills/HolyLight.cs` | `HolyLight` | `HolyLight.cs` |
| `Skills/HumanUnits.cs` | `RuntimeBurningOilAbility`、`RuntimeDetectAbility`、`RuntimeFeedbackAbility` 等 6 个 | 同名文件等 6 个 |
| `Skills/HumanUnits2.cs` | `RuntimeBarrageAbility`、`RuntimeDefendAbility`、`RuntimeDispelMagicAbility` 等 10 个 | 同名文件等 9 个 |
| `Skills/Immolation.cs` | `RuntimeImmolationAbility` | `RuntimeImmolationAbility.cs` |
| `Skills/Impale.cs` | `RuntimeImpaleAbility` | `RuntimeUndeadHeroAbilities.cs` |
| `Skills/Inferno.cs` | `RuntimeInfernoAbility` | `RuntimeInfernoAbility.cs` |
| `Skills/LifeDrain.cs` | `RuntimeDrainAbility` | `RuntimeDrainAbility.cs` |
| `Skills/LocustSwarm.cs` | `RuntimeLocustSwarmAbility` | `RuntimeLocustSwarmAbility.cs` |
| `Skills/ManaBurn.cs` | `RuntimeManaBurnAbility` | `RuntimeNightElfHeroAbilities.cs` |
| `Skills/MassTeleport.cs` | `RuntimeMassTeleportAbility` | `RuntimeHumanOrcUltimates.cs` |
| `Skills/MeatWagon.cs` | `RuntimeExhumeAbility`、`RuntimeMeatDropAbility`、`RuntimeMeatLoadAbility` | `RuntimeExhumeAbility.cs`、`RuntimeMeatCargoAbilities.cs` |
| `Skills/MeleeRemaining.cs` | `RuntimeAttributeModSkillAbility`、`RuntimeCallToArmsAbility`、`RuntimeCloudOfFogAbility` 等 11 个 | 同名文件等 10 个 |
| `Skills/Metamorphosis.cs` | `RuntimeMetamorphosisAbility` | `RuntimeMetamorphosisAbility.cs` |
| `Skills/Mines.cs` | `RuntimeItemPlaceMineAbility` | `RuntimeItemPlaceMineAbility.cs` |
| `Skills/MirrorImage.cs` | `RuntimeMirrorImageAbility` | `RuntimeMirrorImageAbility.cs` |
| `Skills/NightElfPassives.cs` | `RuntimeLifeRegenerationAuraAbility`、`RuntimeShadowMeldAbility` | `RuntimeLifeRegenerationAuraAbility.cs`、`RuntimeWarlockStateSkills.cs` |
| `Skills/NightElfUnits.cs` | `RuntimeCorrosiveBreathAbility`、`RuntimeCycloneAbility`、`RuntimeDispelMagicAbility` 等 14 个 | 同名文件等 13 个 |
| `Skills/NightElfUnits2.cs` | `RuntimeCorporealFormAbility`、`RuntimeImpalingBoltAbility`、`RuntimeMagicImmunityAbility` | 同名文件等 3 个 |
| `Skills/OrcUnits.cs` | `RuntimeBloodlustAbility`、`RuntimeCommandAuraAbility`、`RuntimeFrenzyAbility` 等 11 个 | 同名文件等 11 个 |
| `Skills/OrcUnits2.cs` | `RuntimeEnsnareAbility`、`RuntimeFreezingBreathAbility`、`RuntimeHealingWardAbility` 等 6 个 | 同名文件等 5 个 |
| `Skills/Phoenix.cs` | `RuntimePhoenixEggBuff`、`RuntimeSummonAbility` | `RuntimeAbilityFactory.cs`、`RuntimePhoenixAbility.cs` |
| `Skills/Polymorph.cs` | `RuntimePolymorphAbility` | `RuntimePolymorphAbility.cs` |
| `Skills/RainOfFire.cs` | `RuntimeRainOfFireAbility` | `RuntimeRainOfFireAbility.cs` |
| `Skills/Reincarnation.cs` | `RuntimeReincarnationAbility` | `RuntimeReincarnationAbility.cs` |
| `Skills/RemainingCodes.cs` | `RuntimeDarkPortalAbility`、`RuntimeHarvestAbility`、`RuntimeParasiteAbility` 等 7 个 | 同名文件等 5 个 |
| `Skills/Root.cs` | `RuntimeRootAbility` | `RuntimeRootAbility.cs` |
| `Skills/SearingArrows.cs` | `RuntimeFlamingArrowsAbility` | `RuntimeFlamingArrowsAbility.cs` |
| `Skills/SerpentWard.cs` | `RuntimeSummonAbility` | `RuntimeAbilityFactory.cs` |
| `Skills/ShadowStrike.cs` | `RuntimeShadowStrikeAbility` | `RuntimeShadowStrikeAbility.cs` |
| `Skills/ShockWave.cs` | `RuntimeShockwaveAbility` | `RuntimeShockwaveAbility.cs` |
| `Skills/Silence.cs` | `RuntimeSilenceAbility` | `RuntimeSilenceAbility.cs` |
| `Skills/SiphonMana.cs` | `RuntimeDrainAbility` | `RuntimeDrainAbility.cs` |
| `Skills/Sleep.cs` | `RuntimeSleepAbility` | `RuntimeSleepAbility.cs` |
| `Skills/SoulPreservation.cs` | `SoulPreservation`、`SoulPreservationBuff` | `CampaignAndAuraSkills.cs` |
| `Skills/SpikedCarapace.cs` | `RuntimeSpikedCarapaceAbility` | `RuntimeSpikedCarapaceAbility.cs` |
| `Skills/SpiritOfVengeance.cs` | `RuntimeVengeanceAbility` | `RuntimeClassicSummonAndArmorAbilities.cs` |
| `Skills/Starfall.cs` | `RuntimeStarfallAbility` | `RuntimeStarfallAbility.cs` |
| `Skills/Submerge.cs` | `RuntimeSubmergeAbility` | `RuntimeWarlockStateSkills.cs` |
| `Skills/SummonUnit.cs` | `RuntimeResurrectionAbility`、`RuntimeSummonAbility` | `RuntimeAbilityFactory.cs`、`RuntimeResurrectionAbility.cs` |
| `Skills/TavernHeroes.cs` | `RuntimeBreathOfFireAbility`、`RuntimeCleavingAttackAbility`、`RuntimeDoomAbility` 等 9 个 | 同名文件等 9 个 |
| `Skills/TavernHeroes2.cs` | `RuntimeAcidBombAbility`、`RuntimeClusterRocketsAbility`、`RuntimeHealingSprayAbility` 等 6 个 | 同名文件等 6 个 |
| `Skills/ThornsAura.cs` | `RuntimeThornsAuraAbility` | `RuntimeThornsAuraAbility.cs` |
| `Skills/ThunderBolt.cs` | `RuntimeStormBoltAbility` | `RuntimeAbilityFactory.cs` |
| `Skills/ThunderClap.cs` | `RuntimeThunderClapAbility` | `RuntimeThunderClapAbility.cs` |
| `Skills/Tinker.cs` | `RuntimeDemolishAbility`、`RuntimeEngineeringUpgradeAbility`、`RuntimePocketFactoryAbility` 等 4 个 | 同名文件等 4 个 |
| `Skills/Tornado.cs` | `RuntimeTornadoAbility` | `RuntimeTornadoAbility.cs` |
| `Skills/Tranquility.cs` | `RuntimeTranquilityAbility` | `RuntimeTranquilityAbility.cs` |
| `Skills/TransportCargo.cs` | `RuntimeCargoDropAbility`、`RuntimeCargoLoadAbility`、`RuntimeDevourAbility` | `RuntimeCargoHoldAbility.cs`、`RuntimeDevourAbility.cs` |
| `Skills/TrueshotAura.cs` | `RuntimeTrueshotAuraAbility` | `RuntimeCommandAuraAbility.cs` |
| `Skills/UndeadUnits.cs` | `RuntimeAbsorbManaAbility`、`RuntimeDevourMagicAbility`、`RuntimeEnsnareAbility` 等 8 个 | 同名文件等 6 个 |
| `Skills/UndeadUnits2.cs` | `RuntimeAlternateFormAbility`、`RuntimeAntiMagicShellAbility`、`RuntimeAntiMagicShellBuff` 等 12 个 | 同名文件等 10 个 |
| `Skills/UnholyAura.cs` | `RuntimeUnholyAuraAbility` | `RuntimeUnholyAuraAbility.cs` |
| `Skills/UnitTypeUpgrade.cs` | （辅助类，随主类） | — |
| `Skills/UniversalPassives.cs` | `RuntimeInvulnerableAbility`、`RuntimeResistantSkinAbility`、`RuntimeUltravisionAbility` | 同名文件等 3 个 |
| `Skills/VampiricAura.cs` | `VampiricAura` | `CampaignAndAuraSkills.cs` |
| `Skills/WarStomp.cs` | `RuntimeWarStompAbility` | `RuntimeWarStompAbility.cs` |
| `Skills/WindWalk.cs` | `RuntimeWindWalkAbility` | `RuntimeWindWalkAbility.cs` |
| `Skills/WorkerSkills.cs` | `RuntimeRepairAbility` | `RuntimeRepairAbility.cs` |

**技能层小结**：103 个文件中，88 个能定位到客户端同名/同码实现；15 个是聚合文件（一个文件装多个技能，客户端散在多个 `Runtime*Ability.cs`）或辅助 Buff 类。**没有一个是"客户端没有对应"的技能文件**——这与缺口的分布一致：缺的是注册码，不是文件。

### 2.8 其余文件：Abilities / Items / FogOfWar / MPQ / Diagnostics

| Launcher | 客户端对应 | 等级 | 备注 |
|---|---|---|---|
| `Abilities/Ability.cs` | `Src/Abilities/Ability.cs` | A | 技能基类 |
| `Abilities/Buff.cs` | `Src/Abilities/Buff.cs` | A | Buff 基类与状态挂载 |
| `Abilities/AuraBonus.cs` | `Src/Abilities/Buff.cs`（光环段） | C | 光环加成聚合，客户端分散在各 `Runtime*AuraAbility` |
| `Abilities/UnitBuffs.cs` | `Src/Abilities/Buff.cs` | C | 具体 Buff 实现集中处 |
| `Abilities/SkillRegistry.cs` | `RuntimeAbilityFactory*.cs`（5 个分册） | A | 码→类注册表 |
| `Items/Item.cs` | `JassGameNatives.Models.cs:270`（`ItemRef`） | B | 物品实例；客户端无独立 `Item` 类 |
| `Items/ItemBonus.cs` | `Configuration/ItemBonusReader.cs` + `Src/Items/ItemBonus.cs` | A | 属性加成，字段顺序错位见 3.1-5 |
| `Items/ItemConsumable.cs` | `RuntimeItemRestoreAbility.cs` | B | 消耗品回复 |
| `Items/ItemUseEffect.cs` | `RuntimeItem*Ability.cs`（约 15 个）+ `JassGameNatives.ItemUseRuntime.cs` | A | 一次性效果表 |
| `Items/ItemUseRules.cs` | `RuntimeItemAbilityResolution.cs` | B | 主动能力可用性判定 |
| `World/FogOfWar.cs` | `Src/Core/FogOfWar.cs` | A | 战争迷雾；格宽/LOS 差异见 3.7-49 |
| `Core/Formats/Mpq/MpqArchive.cs` | `L0_Foundation/ThirdParty/War3Net/War3Net.IO.Mpq/MpqArchive.cs` | A | 归档读取 |
| `Core/Formats/Mpq/MpqCrypto.cs` | `War3Net.IO.Mpq/MpqEncryptionUtils.cs` | B | 加密/哈希 |
| `Core/Formats/Mpq/MpqWriter.cs` | `War3Net.IO.Mpq/MpqArchiveBuilder.cs` | B | 仅 `.gmap` 导出用 |
| `Core/Formats/Mpq/AdpcmDecode.cs` | War3Net 无独立实现 | — | 音频压缩解码，客户端走第三方 |
| `Core/Formats/Mpq/HuffmanDecode.cs` | War3Net 无独立实现 | — | 同上 |
| `Core/Formats/Mpq/HuffmanTables.cs` | War3Net 无独立实现 | — | 同上 |
| `Core/Formats/Mpq/LsbBitReader.cs` | War3Net 无独立实现 | — | 位读取工具 |
| `Core/Formats/Mpq/PkwareExplode.cs` | War3Net 无独立实现 | — | PKWARE 解压 |
| `Diagnostics/CombatProbe.cs` | `Game/Diagnostics/DeterminismCheck.cs` + `AbilityCoverageReview.cs` | C | 总探针，客户端是 104 个独立回归文件 |
| `Diagnostics/CombatProbe.Skills.cs` | `Game/Diagnostics/AbilityCoverageReview.cs` | C | 技能覆盖 |
| `Diagnostics/CombatProbe.UnitSkills.cs` | `Game/Diagnostics/AbilityShowcase.cs` | C | 单位技能 |
| `Diagnostics/CombatProbe.RacialSkills.cs` | `Game/Diagnostics/CombatUpgradeRegression.cs` | C | 种族技能 |
| `Diagnostics/CombatProbe.ClientSkills.cs` | `Game/Diagnostics/AbilityAnimationRegression.cs` | C | 客户端技能 |
| `Diagnostics/CombatProbe.MeleeUltimates.cs` | `Game/Diagnostics/*Ultimate*`（无同名） | C | 大招 |
| `Diagnostics/CombatProbe.Tavern.cs` | `Game/Diagnostics/*Tavern*`（无同名） | C | 酒馆英雄 |
| `Diagnostics/CombatProbe.TavernItems.cs` | `Game/Diagnostics/*Item*`（无同名） | C | 酒馆物品 |
| `Diagnostics/CombatProbe.Tinker.cs` | `Game/Diagnostics/*Tinker*`（无同名） | C | 修补匠 |
| `Diagnostics/CombatProbe.UndeadSkills.cs` | `Game/Diagnostics/*Undead*`（无同名） | C | 亡灵技能 |
| `Diagnostics/CombatProbe.MorphSkills.cs` | `Game/Diagnostics/*Morph*`（无同名） | C | 变形 |
| `Diagnostics/CombatProbe.Forms.cs` | `Game/Diagnostics/*Form*`（无同名） | C | 形态 |
| `Diagnostics/CombatProbe.Illusions.cs` | `Game/Diagnostics/*Illusion*`（无同名） | C | 幻象 |
| `Diagnostics/CombatProbe.Channels.cs` | `Game/Diagnostics/AutocastIntervalRegression.cs` | C | 引导/自动施法 |
| `Diagnostics/CombatProbe.States.cs` | `Game/Diagnostics/BypassAttackerRegression.cs` | C | 状态位 |
| `Diagnostics/CombatProbe.Orders.cs` | `Game/Diagnostics/AttackFrontRowBlockRegression.cs` | C | 订单 |
| `Diagnostics/CombatProbe.Formation.cs` | `Game/Diagnostics/AttackFrontRowBlockRegression.cs` | C | 编队 |
| `Diagnostics/CombatProbe.ItemUse.cs` | `Game/Diagnostics/*Item*`（无同名） | C | 物品使用 |
| `Diagnostics/CombatProbe.Ai.cs` | `Game/Diagnostics/CreepRetargetRegression.cs` | C | AI/野怪 |
| `Diagnostics/CombatProbe.CreepAbilities.cs` | `Game/Diagnostics/CreepRetargetRegression.cs` | C | 野怪技能 |
| `Diagnostics/CombatProbe.BloodMage.cs` | `Game/Diagnostics/*BloodMage*`（无同名） | C | 血法师 |
| `Diagnostics/AttachmentProbe.cs` | `Game/Diagnostics/CursorProvenance.cs`（近似） | C | 挂点探针 |
| `Diagnostics/WorldTest.cs` | `Game/Diagnostics/DecalSceneRegression.cs`（近似） | C | 世界冒烟测试 |

> Diagnostics 与客户端**不是 1:1**：客户端有 104 个按主题拆分的回归文件，Launcher 用 22 个 `CombatProbe.*` 分册覆盖。搬运时不需要对齐文件名，但**断言值必须来自客户端**（见 4.2 P0-7）。


---

## 3. 差异清单

> 每条都给两侧可查证的位置。「搬运难度」按改动行数估：★ ≤20 行，★★ 20-100 行，★★★ >100 行。

### 3.1 数值取错源（最高优先，影响全部对战数值）

| # | 项 | Launcher | 客户端 | 影响 | 难度 |
|---|---|---|---|---|---|
| 1 | Misc 规则表版本 | `MiscRules.cs:9` → `MeleeV0UnitsMiscGame.json`（RoC） | `Src/Configuration/War3GameplayConfig.cs:511-514` → `Units/MiscGame.txt`（TFT） | 实测 21 个键不同 | ★ |
| 2 | 攻击/护甲矩阵 | 同上继承 RoC 值 | `DamageBonusNormal={1,1.5,1,.7,...}` | 普通打中甲少 33%；穿刺打轻甲 0.75 vs 2.0 | ★（随 1） |
| 3 | 移速下限 | `World.Abilities.cs:401-408` 无夹取 | `Stats.cs:155` → `NativeMovementSpeed.Limit`，下限 150 / 上限 522 | 减速可压到 110 以下 | ★ |
| 4 | 伤害骰子 | `World.WeaponSystem.cs:90` `Lerp(min,max)` | `Src/Core/Weapon.cs:114-117` `base + N×d(sides)` | 每击期望偏低 `dice/2` | ★ |
| 5 | 物品三围字段 | `Items/ItemBonus.cs:148-153` → `(dataA, dataB, DataC)` | `ItemBonusReader.cs:66-67` → `(Str=c, Agi=a, Int=b)` | 力/敏/智全部串位 | ★ |
| 6 | 复活时间字段 | `UnitCatalog.cs:278` 用 `reptm`，下限 5s | `JassGameNatives.Ai.HeroesAndSpells.cs:11-20` 用 `bldtm`，下限 1s | 复活快慢不同 | ★ |
| 7 | 库存缺省 | `ItemCatalog.cs:98-99`：stockMax 缺省 0、regen 缺省 120 | `StockAndShops.cs:244,268-284`：缺省 1 / 缺省 0（0=不消耗） | 无该列的商品永久缺货 | ★ |
| 8 | 起始资源判定 | `Lifecycle.cs:252` `FormatVersion < 25` | `Blizzard.j:7903` `VersionGet()`（运行版本） | RoC 格式图在 TFT 下多给 250 金 | ★ |

**这一组的关键认知**：`--combat-probe` 的断言是按 Launcher 当前系数写的，改完 1/2 后断言会成片失败——那不是回归，是断言本身锁定了错值，必须同步改成客户端值。

### 3.2 战斗规则缺分支

| # | 项 | Launcher | 客户端 | 影响 | 难度 |
|---|---|---|---|---|---|
| 9 | 虚无不免疫物理 | `World.DamageMembers.cs:190-264` 无该分支 | `Health.cs:196` 直接 0 伤；`Weapon.cs:138` 连目标都选不上 | 放逐只剩减速+增伤，失去物理免疫 | ★ |
| 10 | 在建建筑护甲类型 | `DamageMembers.cs:176` 保留原 `DefenseType` | `Health.DamageScaling.cs:16-22` 换 `Unarmored` | 攻城打在建建筑差 50% | ★ |
| 11 | 免伤分类口径 | `DamageMembers.cs:226` 只按 `DamageType==Normal` 判物理 | `Health.cs:183-192` 按位掩码 + `AttackType` 分类 | 攻城/英雄攻击配 Universal 时魔免判定错 | ★★ |
| 12 | 死亡缠绕 | `Skills/DeathCoil.cs:49` 全额 + `Magic` | `RuntimeUndeadHeroAbilities.cs:42-44` `HealAmount/2` + `Death` | 伤害翻倍 | ★ |
| 13 | 圣光术对敌方亡灵 | `Skills/HolyLight.cs:23` 只接受友军 | `Src/Skills/HolyLight.cs:34-52` 可打敌方亡灵 `DataA/2` | 不能净化敌方亡灵 | ★ |
| 14 | 霜冻新星主目标 | `Skills/FrostNova.cs:31-42` 主目标只吃 `DataB` | `RuntimeFrostNovaAbility.cs:88-118` 主目标先吃范围 `DataA` 再吃 `DataB` | 主目标少一次伤害 | ★ |
| 15 | 暴风雪槽位 | `Skills/Blizzard.cs:19-21` `WaveInterval=>Data('D')` | `RuntimeAbilityFactory.cs:533-541` 波间隔取 `Cast`，`DataD`=建筑减伤 | 数值巧合相等，但缺每波总量上限 | ★★ |
| 16 | 引导中断条件 | `World.Channels.cs:96-101` `Stunned || Silenced` | `Src/Units/Unit.Step.cs:32-35` `Stunned || Sleeping` | 被沉默会误打断引导 | ★ |
| 17 | 嗜血类 Buff 叠加 | `Skills/OrcUnits.cs:21-31` 每次 new 叠加 | `RuntimeBloodlustAbility` 同源刷新 | 重复施法线性叠加攻速 | ★★ |
| 18 | 光环离场余韵 | `World.Abilities.cs:298-325` 每 tick 重算 | `RuntimeUnholyAuraAbility.cs:105-118` Buff 4 秒滞留 | 离开光环立即失效 | ★★ |

### 3.3 命令与操作

| # | 项 | Launcher | 客户端 | 影响 | 难度 |
|---|---|---|---|---|---|
| 19 | 攻击移动 | `World.Orders.cs:7-20` 只有 Move/Attack/Patrol，`Attack` 必须带 `Target` | `GameSession.cs:79` `AttackMoveCommand` → `COrderTarget(Point)` | 点地面无法边走边打 | ★★ |
| 20 | 订单队列深度 | `World.Orders.cs:27` 12 | `OrderQueue.cs:58` 500 | Shift 连点被静默丢弃 | ★ |
| 21 | 受击反击 | 无 | `Components/Ai.cs:136 ReturnFire` | 被打不还手 | ★★ |
| 22 | 移动步进阻挡校验 | `World.MovementEvents.cs:80-102` 只算位移 | `MovementExecution.cs:264-270` 每步查静态阻挡 | 新建建筑可被穿行 | ★★ |
| 23 | 编队限速/等齐 | `Commands.cs:135-143` ≥2 单位就取 min | `CPrCluster.cs:1426` 编队时跳过共享限速 | 编队被拖到最慢兵速 | ★★ |

### 3.4 技能

| # | 项 | 说明 | 难度 |
|---|---|---|---|
| 24 | 8 个「类有码无」 | ANmo/AOwd/Aroo/Aarm/ANbr/Abli/Acoi/Ansu 退化成 `PassiveAbility`（`SkillRegistry.cs:591`），单位带技能完全无效果且不报错 | ★（一行一个） |
| 25 | 7 个真缺口实现 | Acha（换型）、AUds（黑暗召唤）、AEpa（毒箭）、Aspb（技能书）、Aetl（常驻虚无）、ANme（流星雨）、Artn（返还资源） | ★★★ |
| 26 | Btlf 通用限时生命 | 只有召唤物走 `SummonRemaining`，图腾/守卫类若不走 `SummonUnit` 就永不消失 | ★★ |
| 27 | 自动施法节奏 | `World.Abilities.cs:248-271` 全局 0.5s 一轮最多一个；客户端每技能独立 deadline（`UnitAutoCastScheduler.cs:36-41`） | ★★ |
| 28 | 攻速加性桶 | `World.Targeting.cs:163-170` 连乘且无 [0.2,5] 夹取；客户端 `NativeAttackSpeedFactor.cs:8-14` 单桶 + 夹取 | ★ |

### 3.5 物品与商店

| # | 项 | Launcher | 客户端 | 难度 |
|---|---|---|---|---|
| 29 | 买物品前置/木材价 | `Production.cs:425-488` 不查前置、只扣金 | `StockAndShops.cs:621-645,421-424` | ★★ |
| 30 | 回收价 | `World.Items.cs:209` `Max(1, gold/2)` | 客户端按 `charges/uses × 0.5` | ★ |
| 31 | 售兵排除建筑 | `Production.cs:602` `if (unit.IsBuilding) continue` | `StockAndShops.cs:771-784` 不过滤 | ★ |
| 32 | 死亡掉背包 | 从不掉 | `OrdersAndItems.cs:740-751`（`DropOnDeath` / `AInv DataB`） | ★★ |
| 33 | 物品冷却组 | 每件独立 | `cooldownID` 共享（`OrdersAndItems.cs:799-841`） | ★★ |
| 34 | 随机掉落哨兵码 | `World.Items.cs:288` 直接查表→null 静默丢失 | `OrdersAndItems.cs:913-936` 按 class/level 抽 | ★★ |

### 3.6 生产与英雄

| # | 项 | Launcher | 客户端 | 难度 |
|---|---|---|---|---|
| 35 | 维修费/upkeep | 无，全额入账（`Ui.cs:240-246`） | `JassGameNatives.Harvest.cs:414-421` 三档税率 | ★★ |
| 36 | 侍僧入矿槽位 | 即采即入账（`MechanismMembers.cs:185-191`） | `HarvestTiming.cs:76-86` 容量/批量 | ★★ |
| 37 | 生产队列 7 格上限 | 无上限 | `RuntimeProductionQueueAbility.cs:50` | ★ |
| 38 | 施工起始血量 | `Production.cs:98` `site.Hp = 1f` | `ConstructionProgress.cs:62` `Max(1, MaxLife×0.1)` | ★ |
| 39 | 建筑升级费用 | 全额扣（`Production.cs:326-330`） | 差额扣（`GameplayConstants.cs:52-53`） | ★ |
| 40 | 经验分配 | 只做范围内均分 | 另需 critter 排除、建筑击杀门控、`GlobalExperience` 兜底、满级吸经验开关（`World.cs:502-552`） | ★★ |
| 41 | 英雄复活流程 | 每 tick 自动复活、只扣金 | 下单进祭坛队列、金+木、可取消退款 | ★★ |
| 42 | 敏→攻速 | `HeroRules.cs:25` 定义未消费 | `Src/Skills/Hero.cs:206` | ★ |
| 43 | 科技效果 | 16 种（`TechTree.cs:243-309`） | 20+ 种，缺 rasd/renw/rman/rmap/rauv/rart/raud | ★★ |

### 3.7 表现层（此前被文档误判为"客户端也没有"）

| # | 项 | 客户端 | Launcher | 难度 |
|---|---|---|---|---|
| 44 | 岸边浪 | `Environment/ShorelineWaveRenderer.cs`（11-06 加入，`Lifecycle.cs:250` 实例化） | 无 | ★★ |
| 45 | 昼夜光照 | `Environment/SceneLighting.cs`（DNC 插值，地形+单位两套光） | 无（`SkyboxRenderer.cs:37 AmbientSource.Disabled`） | ★★★ |
| 46 | 动态光源 | `Environment/DynamicLight.cs`（最多 8 盏） | 无 | ★★★ |
| 47 | 距离雾 | `Environment/FogSettings.cs` + `FogStyle.cs`，全渲染层接入 | 只解析字段（`W3iInfo.cs:45-47`）从不使用 | ★★ |
| 48 | 贴地阴影/树影 | `Rendering/ShadowRenderer.cs` 逐顶点采样地形；`TreeShadowBuffer.cs` 合成进 shd | 固定 y=1.5 平 quad，alpha 常量 | ★★ |
| 49 | 迷雾 LOS | `Src/Core/FogOfWar.cs` 128 单位格、三态、遮挡传播、视野封顶 1800 | 64 单位格、纯圆形盖章、无遮挡 | ★★★ |
| 50 | 迷雾淡变 | `FogOfWarRenderer.cs:66-99` 30ms 步进 | 整张贴图直写 | ★★ |
| 51 | 装饰物动画 | `DoodadRenderer.cs:38-62,235-280` Stand/Death + 逐实例 | 静态实例，砍掉即隐藏 | ★★ |
| 52 | 建筑灰雾残影 | `CharacterRenderer.FogMemory.cs` 冻结最后可见姿态 | `Visible=false` | ★★ |
| 53 | 幻象染色 | `IllusionPresentation.cs:20-24` 蓝色 | 无 | ★ |
| 54 | 单位染色/死亡淡出 | `UnitStateColor.cs:9-29` Ethereal/隐身/淡出 | 只有隐身 ghost | ★★ |
| 55 | 动画状态机 | `Scenes/War3GameScene/Animation.cs:26-130` tag 状态机 + 变体 + 混合 | 名字前缀匹配（`UnitSceneComponent.cs:281-308`） | ★★★ |
| 56 | 天气粒子数 | `emrate×lifespan`（暴雨 90/格） | `particles` 列再 `Clamp(16,400)` | ★ |
| 57 | 音频事件/循环音 | `War3AudioSystem.cs:265-282` SND/SPN、`War3LoopingSoundService`、`War3StackedSoundManager` | 无 | ★★★ |
| 58 | 声部/优先级 | 24 声 + 同文件≤3 + 优先级抢占 | 16 声 + 0.08s 冷却 | ★★ |
| 59 | 队伍色 | `TeamColor{n}.blp` 贴图 | 12 个硬编码 hex | ★★ |

### 3.8 结构性替换（不是缺口，但要写入计划）

| 项 | 客户端 | Launcher | 处理方式 |
|---|---|---|---|
| melee 开局 | `Blizzard.j` 的 `Melee*`（JASS 执行） | `MeleeRules.cs` + `Melee.cs` C# 重写 | 按 Blizzard.j 逐函数搬运，不发明 |
| 电脑 AI | AMAI `.ai` 脚本 + JIT/AOT 预编译 | `Ai.cs` 波次刷兵 + `AiCounters.cs` 打分 | 保持原创，但难度参数对齐 `AiDifficulty` 语义 |
| UI 框架 | FDF 帧树（`Game/UI/*`） | Godot Control 自绘 | 保留自绘，**但对齐交互语义**（槽位、分页、热键、可取消） |
| 渲染后端 | `IRenderDevice` + GL/GLES | Godot 材质/网格 | 保留 Godot，不追求像素级一致 |
| 调试工具 | `Game/Imgui/*`（桌面专属） | `Cheats.cs` | 不参与对齐 |

---

## 4. 同步计划（搬运优先）

### 4.1 搬运原则

1. **数据同源**：任何常量都必须来自客户端同一张表、同一字段。禁止在代码里二次折算或另写默认值。
2. **判定照搬**：客户端的 `if` 分支顺序、边界值、提前返回，整段搬；不"理解后重写"。
3. **命名照搬**：类名/方法名/字段名优先用客户端原名（`DealDamage`、`ScaleIncomingDamage`、`ApplySplash`、`MeleeClearExcessUnit`），文件按客户端的拆分粒度对齐。新增文件命名 `客户端类名.cs`。
4. **不建中间层**：不为"以后可能换实现"抽接口。Launcher 是客户端行为的执行者，不是框架。
5. **不引用桌面专属**：`Program.cs`、`Hosting/`、`Imgui/`、`Game/Shaders/`、`War3InputComponent` 的键鼠段一律不作依据。
6. **一处一验证**：每个搬运项完成后必须能用探针或并排截图证明，否则不算完成。

### 4.2 阶段计划

#### P-1：结构镜像（1-2 天，纯搬迁，不改行为）

| 批次 | 内容 | 文件数 | 校验 |
|---|---|---|---|
| P-1a | 建 `Src/` 镜像，移 `Abilities/`、`Components/`、`Units/`、`Items/` | 约 21 | 构建 + 两个探针 |
| P-1b | 移 `Core/`：`Combat/`、`Abilities/` → `Src/Core/`；`Formats/` → `GameCore/L1_Adapter/Formats/` | 约 24 | 同上 |
| P-1c | 移 `Simulation/Configuration/` → `GameCore/L3_Simulation/Simulation/Configuration/`；`Maps/` → `.../Simulation/Maps/` | 17 | 同上 |
| P-1d | 移 `Environment/`、`Presentation/`、`Audio/`、`UI/`、`Camera/`、`Scenes/` → `GameCore/L4_Composition/Game/` 对应目录 | 约 55 | 同上 |
| P-1e | 拆 `World.*.cs` partial 为客户端独立类（`WeaponSystem`、`OrderQueue`、`CargoTransport`、`CRallyPoint` 等） | 30 | 探针逐项 |
| P-1f | namespace 统一为 `OpenWC3` / `OpenWC3.Game`，Godot 壳用 `OpenWC3.Launcher` | 全量 | 构建 |
| P-1g | `Skills/` 按客户端拆成一技能一文件（220 类 → 对齐客户端 190 个文件） | 103 | 码表覆盖率不变 |
| P-1h | 入库 `sync-check.command`：`diff -rq` 两侧同名文件，输出待跟进清单 | 1 | 人工确认输出 |

> **P-1 与 P0 的穿插顺序**：先 P-1a~P-1d（纯移动，不改内容），再 P0（改数值），最后 P-1e/P-1f（拆类与改 namespace）。理由：P0 改的是内容，如果同时移动文件，diff 会呈现成"整文件重写"，review 时看不出真正的逻辑改动。
> P-1 全部完成后，第 2 章的映射表即可废弃——同路径即同源。

#### P0：数值基准修正（半天，改完立刻可验证）

| 步骤 | 改动 | 文件 | 验收 |
|---|---|---|---|
| P0-1 | 表源切到 `Units/MiscGame.txt`，解析支持逗号数组 | `MiscRules.cs:9,15-40` | 打印矩阵，与 `Units/MiscGame.json` 逐格相同 |
| P0-2 | 物品三围改 `(DataC, DataA, DataB)` | `ItemBonus.cs:148-153` | 拿"力量手套"验证 +3 力量 |
| P0-3 | 虚无物理免疫分支 | `DamageMembers.cs:226` 附近，按 `Health.cs:183-197` 顺序 | 放逐后普攻伤害为 0 |
| P0-4 | 在建建筑换 `Unarmored` | `DamageMembers.cs:176` + `War3GameplayConfig.Resolve` | 攻城打在建塔，伤害按无甲列 |
| P0-5 | 移速夹取 150/522 | `World.Abilities.cs:401-408` | 减速到 110 的单位显示 150 |
| P0-6 | 伤害骰子 | `UnitCatalog.cs:367-368` 读 `dice1/sides1`，`WeaponSystem.cs:90` 改 `Roll` | 同种子下与客户端逐击相同 |
| P0-7 | 修正探针断言 | `Diagnostics/CombatProbe*.cs` 里所有硬编码期望值 | 探针全绿，且数值与客户端一致 |

> P0-1 与 P0-7 必须同批改，否则构建后探针会大面积报红，容易被误判成"改坏了"。

#### P1：对战规则对齐（2-3 天）

| 批次 | 内容 | 主文件 |
|---|---|---|
| P1-1 | 命令层：攻击移动、队列 500、受击反击 | `World.Orders.cs`、`Commands.cs`、`World.Targeting.cs` |
| P1-2 | 伤害口径：死亡缠绕、圣光术、霜冻新星、暴风雪、攻速桶 | `Skills/DeathCoil.cs`、`HolyLight.cs`、`FrostNova.cs`、`Blizzard.cs`、`World.Targeting.cs` |
| P1-3 | 经验与复活：四项经验规则、祭坛队列、`bldtm` | `World.HeroSelectionOrder.cs`、`World.HeroRevive.cs`、`Production.cs` |
| P1-4 | 物品：前置检查、木材价、回收价按次数、掉背包、冷却组、随机掉落码 | `Production.cs`、`World.Items.cs`、`Items/ItemBonus.cs` |
| P1-5 | 生产：upkeep、入矿槽位、7 格队列、施工初始血量、升级差额 | `Ui.cs`、`World.MechanismMembers.cs`、`Production.cs` |
| P1-6 | 技能登记：8 个码 + Btlf + 自动施法节奏 | `SkillRegistry.cs`、`World.Abilities.cs` |
| P1-7 | RoC 判定改运行版本 | `Lifecycle.cs:252` |

#### P2：表现层补齐（分批，每批可独立出图对比）

| 批次 | 内容 | 客户端参考 |
|---|---|---|
| P2-1 | 天气粒子数改 `emrate×lifespan`；天气三档差异化 | `WeatherDefinition.cs:59-60` |
| P2-2 | 距离雾接入地形/悬崖/水/单位 | `FogSettings.cs`、`FogStyle.cs` |
| P2-3 | 昼夜光照（方向光 + 环境光 + 两套光） | `SceneLighting.cs:41-51,142-161` |
| P2-4 | 阴影改贴地 splat + 树影进 shd | `ShadowRenderer.cs`、`TerrainShadowMap.cs`、`TreeShadowBuffer.cs` |
| P2-5 | 岸边浪 | `ShorelineWaveRenderer.cs:8-24` |
| P2-6 | 迷雾 LOS 与遮挡（工作量最大，单独立项） | `Src/Core/FogOfWar.cs:514-619` |
| P2-7 | 动画状态机 tag 化 + 变体 + 混合 | `Animation.cs:26-130`、`UnitAnimationController.cs:275-303` |
| P2-8 | 幻象染色、单位死亡淡出、建筑灰雾残影、队伍色贴图 | `IllusionPresentation.cs`、`UnitStateColor.cs`、`CharacterRenderer.FogMemory.cs` |

#### P3：体验层（可选，按需）

| 批次 | 内容 |
|---|---|
| P3-1 | 音频事件音 SND/SPN、循环音、环境音、24 声部与优先级 |
| P3-2 | 命令卡分页（建造/学习独立页 + 返回槽 11）、热键回退表 QWER/ASDF/ZXCV、队列图标可点取消 |
| P3-3 | 小地图按钮列 + 图标体系 + 4 档雾 alpha + Alt 组合键 |
| P3-4 | AI 对齐 AMAI 的难度语义（建造速度/经济/进攻波次），保留 Launcher 的打分原创 |

### 4.3 不建议搬运的部分

| 项 | 原因 |
|---|---|
| FDF 帧树与 `.fdf` 解析 | Godot Control 已经能表达同等布局；搬 FDF 会把 Godot 拖进客户端的 UI 框架 |
| JASS 运行时 | Launcher 定位是无脚本对战；melee 规则用 C# 复刻即可 |
| `IRenderDevice` 抽象与 GL/GLES 后端 | Godot 已提供；只需对齐"什么条件下画什么"，不需要对齐"怎么调 GL" |
| `Game/Imgui/*`、`Program.cs`、`Hosting/*` | 桌面专属工具链 |
| AMAI `.ai` 解释器 | 工作量远大于收益；AI 保持 C# 原创 |

### 4.4 每阶段验收方式

1. **数值类**：用 `--combat-probe` / `--state-probe`，并新增"与客户端对拍"脚本：同一地图、同一单位、同一随机种子，比较伤害/经验/时间三个可观测值。
2. **规则类**：在探针里加最小复现场景（如"放逐步兵后测普攻伤害"），断言值直接引客户端代码注释里的推导。
3. **表现类**：同一地图同一镜头位并排截图（`--shot`），按项对比：岸边是否有浪、远处是否有雾、树下是否有影、雾边是否渐变。
4. **性能**：P2-6（迷雾 LOS）与 P2-3（光照）完成后必须复测帧率，不得低于当前基线。

---

## 5. 一句话总结

Launcher 与客户端的对应关系是：**模拟主干同构可搬、表现层重写有缺、数值层取错了表**。

落地顺序：**先把目录结构搬成与客户端一致（P-1），再修 P0 那八处读错源，然后按 P1 逐条搬规则，表现层放最后**。

路径先对齐的理由很简单——它决定了以后每一次同步是"打开同名文件直接改"，还是"先查表再猜该改哪"。而 P0 决定数值对不对，P1/P2 决定规则和观感像不像。顺序错了，改动会互相覆盖，review 时也分不清差异来自逻辑还是来自挪文件。

