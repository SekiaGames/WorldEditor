# 提高 Launcher 与客户端侧一致化的任务列表 V3

判定标准沿用 AGENTS.md：做同一件事流程不同、看到的效果不同、客户端有实现而 Launcher 另写一套、能共用没共用、C# 文件的路径/文件名/类名对不上。

**按模块分组，模块从底层到表层，模块内也从上往下做。** 每条都写三件事：客户端在哪、Launcher 在哪、玩家看到什么。编号 1..45 是阅读顺序，做完一条删一条，序号不复用。

> **复核评价**：正文实际到 50 条，收尾规则也写着「新任务从 51 起」，这句「1..45」是上一版留下的。改成 1..50，或者干脆不写区间，只写「编号即阅读顺序」。

## 范围

- 起点是地图选择界面；客户端首屏（公告、主菜单、菜单音乐）、地图文件夹浏览、地图导入、战役列表不在范围内。
- Launcher 不跑 JASS 与触发器，脚本才能触发的表现不进清单。
- 默认环境 macOS，`RunLauncher.command` 启动，地图 `(4)Turtle Rock.gmap`、`(2)BootyBay.gmap`。

---

## 模块一 数据与配置

> 地图数据、物编、文本表的读取口径。这一层读错，上层怎么调都是错的。

### 58. 预摆单位 owner 被钳到 0..15，24 人图里 17–24 号玩家摆的单位判给 16 号玩家

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Maps/War3MapUnits.cs:22` 把 `war3mapUnits.doo` 的 `Player` 原样读进 `MapUnit.Player`（4 字节），不做区间收口，建单位时直接当阵营用；`ApplyInstancePresentation` 再按 `CustomTeamColor` 覆盖队伍色。
- **Launcher**：`GameCore/L4_Composition/Game/Scenes/War3GameScene/Units.cs:44` 是 `(int)Mathf.Clamp(entry.Player, 0, 15)`，owner ≥ 16 全部折到 15；`War3MapUnits.cs:154` 的解析本身没问题（`uint` 原样读出）。
- **玩家可见**：24 人图、或者编辑器里给 17–24 号玩家摆过单位的地图，这些单位开局就变成 16 号玩家（中立被动）的东西：用的是 16 号那支灰、不归任何人指挥、和作者的排布意图对不上；靠 owner 认单位的地图脚本也会跟着错。
- **要做**：owner 按 0..23 保留，只对超出 24 个座位的值收口。

> **执行结论（Launcher4，已修，待合并）**：
>
> - **改法**：`War3GameScene.Players.cs` 新增 `PlacementOwner(uint owner, int seatCount)`，`Math.Min(owner, seatCount - 1)`，`Units.cs` 调用时传 `_players.Length`（场景里玩家表的真实长度 24）——座位数只有这一处来源，不再写死字面量。
> - **为什么不是「去钳制」**：`entry.Player` 是 `uint`，doo 里脏数据能到 4 亿，直接 `(int)` 会溢出成负数玩家号；客户端不钳是因为它的读取层另有边界，这里保留一个 24 座位的上限收口。
> - **波及面核对**：场景里按玩家号索引的容器都是字典（`_allyMasks`、`_playerNames`、`_heroAcquisitionCounts`、`byPlayer`），`PlayerSlot[]` 本来就是 24 长度；16 号以上不会越界。16 号以上玩家不在 `_active` 时，其单位不受内务 AI 管、不可被本地玩家选中、对所有人敌对——与客户端对「没有该座位的玩家」的处理一致。
> - **验证**：新增 `Diagnostics/CombatProbe.PlayerSlots.cs` 7 条断言（owner 15/16/23 原样保留、200 收到 23、17 号玩家单位记在 16 号位、且上色不等于 16 号色）全过；临时插桩确认端到端（把第 8/9 个预摆单位的 owner 强制成 20，日志 `[OWNER] nftb raw=20 -> P20`，对局四族内务正常推进、无异常，插桩已还原）；`--state-probe` 仍只剩既存 `utod 研究项` 1 条，`--combat-probe` 全过，`dotnet build -t:Rebuild` 4 条既存警告 0 错；两张测试图跑动单位数与改前一致（Turtle Rock 111 / BootyBay 59），说明现有地图 owner 全在 0..15、本改动对它们是空操作。
> - **顺带发现（未改，另立条件）**：`War3MapUnits.cs:68,226` 解析了 `CustomTeamColor`，但全仓没有任何读取处 —— 编辑器里给单个单位指定的队伍色被直接丢掉。客户端在 `ApplyInstancePresentation` 里 `unit.TeamColorIndex = CustomTeamColor`。Launcher 侧 `Unit.TeamColorIndex` 目前是 `Player` 的别名（`Src/Units/Unit.Compat.cs:50`），要落地得先给 Unit 一个独立的队伍色字段，改动面比这次大，建议单独立条。

### 59. 地图里给单个单位指定的队伍色被丢掉，预摆单位一律用玩家色

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Maps/War3MapUnits.cs:171` 读 doo 的 `CustomTeamColor`（4 字节，`-1` 表示不覆盖），`:57-60` 的 `ApplyInstancePresentation` 里 `if (CustomTeamColor >= 0) { unit.TeamColorIndex = CustomTeamColor; unit.HasCustomTeamColor = true; }`；`Src/Units/Unit.TypeFields.cs:23` 再让物编自带的队伍色只在「没有地图实例色」时才生效。
- **Launcher**：`GameCore/L3_Simulation/Simulation/Maps/War3MapUnits.cs:68,226` 已经把 `CustomTeamColor` 读进 `UnitPlacement`，但全仓再没有第二处读取（`grep CustomTeamColor` 只命中字段与解析），`SpawnUnits` 建完单位就结束。而且 `Src/Units/Unit.Compat.cs:50-54` 的 `TeamColorIndex` 是 `Player` 的别名，setter 会直接改归属，没有独立字段能承载这个覆盖值。
- **玩家可见**：编辑器里给某个单位单独换色（把敌方首领染黑、给剧情单位一个独特颜色、让召唤物跟敌人同色）在 Launcher 里完全无效，这些单位一律显示所属玩家的颜色。
- **要做**：`Unit` 补独立的队伍色字段（`TeamColorIndex` 不再当 `Player` 别名），出生时套用地图实例的 `CustomTeamColor`，表现层取色改读该字段。

---

## 模块二 寻路与移动

> 走位、碰撞、可达性。玩家感知最强的一层差异在这里。

### 51. 寻路搜索仍是自研 32px A*，没有客户端的 footprint 接触解算与粗/细/出口搜索

- **客户端**：`Open-Warcraft3_Wiki/Src/Core/Pathing/NativeMirror/` 用路径纹理 footprint 做接触解算、放置掩码，搜索分粗搜、细搜与出口搜索；`CProximityMap` 逐格记录对象占用与关系。
- **Launcher**：`Launcher/Src/Core/Pathing/Search/PathGrid.cs` 的 `FindPath` 仍是 `CellWorld = 32f` 布尔网格上的 A*；网格只含 WPM 与可破坏物静态 footprint（`DoodadPathingOverlays.cs`、`PathingTextureMasks.cs`），单位移动不写回网格。
- **玩家可见**：单位能否挤过建筑与树林之间的缝、绕建筑拐角的走法、被围住时能否蹭出去，仍与客户端不同；窄口会出现客户端能过而 Launcher 判不可达，或反向卡位。
- **要做**：接入客户端 footprint 接触解算与粗/细/出口搜索，单位移动写回网格；验收口径：窄口与围堵场景下，Launcher 与客户端逐格可达性一致。

> **进展（Launcher1，第一批已提交 `1666a39`，任务未完成）**
>
> 先按「与 Godot+对战是否冲突」核过 `Src/Core`：446 个顶层类型里 **0 处** 引用 Godot / 渲染 / 音频 / 输入 / 窗口，**0 处** 依赖 JASS 解释器、战役、录像。grep 命中的 4 个文件是 `NativePlacementSearchInput` 之类的名字误匹配，`FromJassId`、`Campaign` 分别是伤害类型映射和 AI 字段名。这一层本身是纯模拟/数据，不在平台冲突面上。
>
> 挡住搬运的是**宿主面**。`Src/Core` 与 Launcher 有 24 个同名类型（`Weapon`、`DamageData`、`FogOfWar`、`TargetRules`、`SpecialEffect`、`Effect`、`State`、`World`、`AbilityContainer`、`MovementType`、`NativeSoftFloat`、`CantPathSearchPoint`…），按「同名留 Launcher、冲突处裁剪」处理完，仍剩 1002 处编译错误，集中在大约 30 个宿主成员：
>
> - `World`：`PathingWork`(96)、`PathingMap`(64)、`MovementTick`(34)、`CLrPathingAcc`(24)、`MovementBatchStub.Enqueue/InstalledMovementVelocity`(18/18)
> - `Unit`：`MoveReqId`(76)、`Transform`(72)、`MovingPresenceFilteredForPlanning`(32)、`PathingEnabled`(30)、`Current`(30)、`PathingQueryMask`(20)、`RequestPresenceFilteredForPlanning`(18)、`MoveReqCellX/Y`(14/14)、`Weapons`(12)
> - 其它：`AbilityContainer.Move`(36)、`CAbilityAttack`(14)、`Weapon.Kind`(12)
>
> 硬卡点是 `Unit.Transform`：客户端寻路长在「组件化 Unit」上（`Transform`/`Current`/`Weapons` 三个分量），Launcher 的 `Unit` 是扁平字段（`Position`/`Facing`，订单在 `World`）。加 shim 会让位置有两份状态互相漂移，裁剪解决不了，属于第 47/49 条的下游。
>
> 已落地的第一批：`Src/Core` 里既不碰平台、又不碰宿主面的 11 个文件（`GameClock`、`DualEffectTargetRules`、`ImageVisual`、`NativeAbilityClassification.Generated`、`NativeDiagnosticClock`、`NativeInfoPanelName`、`NativeInventoryTooltip`、`NativeSpellBlockRules`、`NativeTargetMask`、`ObjectDataDiagnostics`、`SimPhaseProbe`），加 `Src/Configuration/War3MapCompatibility.cs`，逐字复制、无调用方、行为不变；`GlobalUsings` 补 `SeekOrigin` 别名。`--state-probe` 仍只剩既存 `utod 研究项` 1 条。
>
> 试过但裁掉的两处：`War3GameplayConfig`（客户端 1063 行 vs 本地 232 行）的玩家颜色段要 `OpenWC3.Color3`，而 Launcher 的颜色辅助签名是 `Godot.Color?`——这是整个 `Src/Core` 里唯一一处「Godot 渗进来」，换配置类会连锁到渲染层；`Footprint.cs`/`TerrainGrid.cs` 依赖 `PathingOverlay`/`PathingTextureBits` 与镜像层 `NativeWpmGrid`，得等寻路整块换装时一起进。
>
> 下一步二选一：甲、给 `Unit`/`World` 补这 30 个成员、`Unit` 往组件化靠，之后可整块搬 `Pathing/**` 33,279 行 + 客户端 104 条契约用例；乙、只搬 `NativeMirror/{Core,Placement,Search,Cursor,Cluster,Execution}` 这 10,876 行零 `OpenWC3` 引用的算法层，宿主面用一层显式适配器收口。

## 模块三 地形与环境

> 地形、悬崖、水面、迷雾、天气、阴影。整屏可见，改动集中在渲染数据与 shader。

## 模块四 单位、技能与物品

> 模拟层行为。这一层决定对局打得对不对。

### 24. 队伍色索引钳到 0..15，17–24 号玩家全部同色

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Rendering/CharacterRenderer.cs:2245,2294,2306` 全部 `Clamp(teamColorIndex, 0, 27)`，`ReplaceableId 1/2`（TeamColor/TeamGlow）分开解析。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Rendering/TeamColorTextures.cs:10-11` `Clamp(index, 0, 15)` 且只加载 `TeamColor00..15`；`Game/Presentation/UnitSceneComponent.cs:420-423` 的 TeamGlow 同样钳 0..15。
- **玩家可见**：16 人以上对局或自定义地图里，第 17–24 号玩家全部显示成 16 号色，小地图与选中圈跟着错。
- **要做**：上限改 27，补 24 张 TeamColor/TeamGlow。

> **执行结论（Launcher4，已修，待合并）**：
>
> - **定位**：Launcher 的队伍色有三处各自钳 0..15，都是 16 长度的数组：`Rendering/TeamColorTextures.cs`（TeamColor 贴图）、`Presentation/UnitSceneComponent.cs` 的 `TeamGlowTexture`（TeamGlow 贴图）、`Configuration/War3UiSkin.cs:26,31` 的 `UnitTeamColor`/`LoadUnitTeamColors`。最后一处才是主通道——模型队伍色由 `war3_model.gdshaderinc:48` 的 `use_team_color` 用 `team_color` uniform 直接改写 rgb，`UnitSceneComponent.ApplyTeamColor` 喂的就是 `War3UiSkin.UnitTeamColor(player)`，贴图替换只影响显式引用 TeamColor 纹理的发光层、粒子和丝带。所以玩家 17–24 全部落在 `UnitTeamColor(15)`。
> - **数据边界（实测，不是推断）**：`war3skins` 的 `Default.TeamColors` = 16（`Generated/Texts/UIwar3skins.g.cs`）；`war3.mpq` 里 `TeamColor00..15`、`TeamGlow00..15` 各 16 张连续，`War3x.mpq`/`War3xPatch.mpq` 一张都没有（直接读 MPQ 哈希表验证）；这 16 张里 0..11 是经典 12 色，12..15 全是同一支 `(0.157,0.157,0.157)` 深灰占位。**结论：任务里「补 24 张」补不出来，游戏数据里没有第 17 支颜色**，只能靠索引映射解决。
> - **改法**：`TeamColorTextures` 与新增的 `TeamGlowTextures` 都改成 28 槽（对客户端 `Clamp(0,27)`），槽数按实际存在且连续的贴图张数算（当前 16），索引超出可用张数时取模，不再折到最后一格。于是 17 号玩家=红、24 号玩家=粉，和客户端小地图那条 `index % _teamColors.Length`（`MinimapFrame.cs:322`、`War3UiSkin.TeamColor`）是同一套映射，模型色与小地图点终于一致。`War3UiSkin.UnitTeamColor` 改成从 `TeamColorTextures` 同一份槽位取色，两处不会再各算一套。
> - **为什么没照抄客户端的 WhiteTexture 兜底**：客户端 `CharacterRenderer.cs:2245` 夹到 0..27 后直接请求 `TeamColor{16..27}.blp`，文件不存在时 `LoadTexture` 落到 `WhiteTexture()`（整片白）。游戏数据里没有第 17 支色，照抄会让 17–24 号玩家的单位全变白模，比现在的全灰更难认。取模是唯一能让人分辨、又和小地图对得上的映射，已在结论里登记为已知取舍。
> - **改动文件**：`GameCore/L4_Composition/Game/Rendering/TeamColorTextures.cs`（重写）、`Rendering/TeamGlowTextures.cs`（新增）、`Presentation/UnitSceneComponent.cs`（TeamGlow 改走新类，粒子换色比对改用实际槽位）、`Configuration/War3UiSkin.cs`（`UnitTeamColor` 走同一槽位、色板长度取实际张数）、`Diagnostics/CombatProbe.TeamColors.cs`（新增 11 条断言）。
> - **验证**：11 条断言全过（可用张数=16、索引 15→15、16→0、23→7、40→夹 27 后 11、发光层同映射、17 号玩家拿到实体贴图且与 1 号玩家同图、着色器色 17≠16 且 17=1）；`--state-probe` 仍只剩 1 条既存失败（`utod 研究项`，与本次无关），`--combat-probe` 全过，`dotnet build -t:Rebuild` 4 条既存警告 0 错；`--play=(2)BootyBay.gmap --aggro` 跑 30 秒，地图预摆单位 59 个、开局基地单位 12 个正常生成，无异常。
> - **顺带发现（不在本次范围，建议单独立条）**：`GameCore/L4_Composition/Game/Scenes/War3GameScene/Units.cs:44` 把 doo 里的 owner 钳成 `Clamp(entry.Player, 0, 15)`，24 人图里 17–24 号玩家摆放的单位会被直接判给 16 号玩家（连归属都错，不只是颜色）；客户端 `War3MapUnits.cs` 不做这个钳制。改它会牵动 AI 座位、生产队列与同盟判定，风险面比配色大，所以这次没动。

### 53. 进魔免不清减速，因为减速不在 Buff 体系里

- **客户端**：`Open-Warcraft3_Wiki/Src/Skills/SlowBuff.cs:4-19` 减速是 `Buff` 子类（沿用基类 `Magic=true`），`GameCore/L3_Simulation/Simulation/Configuration/RuntimeMagicImmunityAbility.cs:35-38` 进魔免按 `buff.Magic && !buff.Aura && !buff.TimedLife` 摘除，被减速的单位一开魔免当场恢复速度。
- **Launcher**：`Launcher/Src/Units/Unit.Slows.cs:23` 减速存在独立的 `List<SlowSource>`，与 Buff 是两张表，也没有魔法/物理之分；`RemoveImmunityEntryBuffs` 只扫 Buff，进魔免速度照旧。
- **玩家可见**：山丘之王被霜冻减速追着打，开天神下凡后客户端的移速立刻恢复，Launcher 还是慢吞吞走不动。
- **要做**：给减速来源打魔法/物理标记，进魔免只清魔法那一类。

### 54. 物编 animProps 没进 Unit.AnimationProperties，变形形态与升级后的站姿都取不到变体

- **客户端**：物编 `animProps` 是序列副标签的来源（`ResolveUpgradeAnimationProperties`、`UpdateIdleAnimation` 都读它）。新类型自带 `upgrade,first/second` 时站姿落在 `Stand Upgrade First/Second`，熊/恶魔这类形态落在 `Stand Alternate`。
- **Launcher**：`GameCore/L3_Simulation/Simulation/Configuration/TechTree.cs:374` 已经把 `UnitFunc` 的 `Animprops` 读进 `UnitFuncRow.AnimationProperties`（升级动画够用），但 `UnitType` 没有这个字段，`Src/Units/Unit.Compat.cs:204` 的 `Unit.AnimationProperties` 始终为空（只有 UI 的 `PortraitAnimator` 在读），所以 `Play("Stand")` 只能选主标签那套。
- **玩家可见**：主基地升级完，新建出来的城堡站姿是普通 `Stand`；德鲁伊变熊后待机与行走也不是模型给熊准备的 `Alternate` 变体。
- **要做**：`Animprops` 一并读进 `UnitType`，单位创建与变形时写进 `Unit.AnimationProperties`；`AnimationTagCache` 已经在吃这个集合，接上即可。

### 55. 每次变形都补一颗 Polymorph 烟，客户端只有变形术有

- **客户端**：变形术那团烟是技能数据里的目标特效，由技能系统在命中时播；`Open-Warcraft3_Wiki` 全仓没有 `PolyMorphDoneGround` 的硬编码。
- **Launcher**：`GameCore/L4_Composition/Game/Scenes/War3GameScene/Units.cs:552-554` 在 `RebuildMorphModel` 里无条件 `AddSpecialEffect(PolyMorphDoneGround)`，德鲁伊变熊、恶魔变身、凤凰变蛋都会冒，时机还跟着重建走。
- **玩家可见**：变熊、变恶魔这类原版没有烟的变化也爆一团黄烟，而且是变形结束（1.5 秒后）才冒，不是起手那一下。
- **要做**：烟交回技能层按技能数据的目标特效播，重建路径不再硬编码。

---

## 模块五 特效与声音

> MDX 事件音、音频空间、声部管理。

> **复核评价**：模块名叫「特效与声音」，正文 25–28 四条全是音频，没有一条特效。上一版里那条「粒子参数是待机帧快照」并没有修好：`Extract/src/Core/Tools/ModelBundle.cs:1996-2001` 的速度、纬度、重力、宽、长、可见度仍走 `SnapshotValue(..., idle, ...)`，取的是待机帧单值，运行时也不随动画帧更新（Launcher 侧没有任何 `ParticleProcessMaterial` 驱动）。同一批里「512 上限」和「粒子吃雾」已经落地（`amount` 现在夹在 8..20000，`:2296`；`war3_particle.gdshader:4` 已引 `atmosphere.gdshaderinc`、`:91` 调 `war3_apply_fog`），只剩「参数随帧变化」这一半悬着。建议按收尾规则补一条 51 号，否则这一层等于被跳过了。

### 25. 缺 AnimSounds 表，单位死亡喊的是「Yes」

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Audio/War3SoundTables.cs:29-31` 把 `UnitAckSounds / UnitCombatSounds / AnimSounds` 合进同一张 ack 表，死亡与攻击音由 MDX 事件经 `AnimLookups → AnimSounds` 解析。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Audio/War3SoundTables.cs:44-48` 只 Load 5 张表，没有 `UISoundInfoAnimSounds`（生成的表其实存在：`Generated/Tables/UISoundInfoAnimSounds.g.cs`）；`War3UnitSoundset.cs:21-27` 用 `soundName + kind` 拼 label，`Scenes/War3GameScene/Presentation.cs:354-362` 死亡路径带 `fallback = "Yes"`。
- **玩家可见**：每个单位被击杀时喊一声「Yes / 遵命」，全场听不到死亡音，脚步与整理类动画音效一并丢失。
- **要做**：补一行 `LoadTable("UISoundInfoAnimSounds")`，死亡路径去掉 `Yes` 兜底。

> **执行结论（Launcher5，已修复，待合并）**：
> - **定位**：`Generated/Tables/UISoundInfoUnitAckSounds.g.cs` 里 `Footman` 只有 `What / Pissed / Ready / Warcry / Yes / YesAttack` 六行，没有 `FootmanDeath`；这条只存在于 `AnimSounds`（`FootmanDeath\tFootmanDeath.wav\tUnits\Human\Footman\`）。`PlayVoice(unit,"Death")` 拼出 `FootmanDeath` 查不到，再走 `fallback="Yes"` 取到 `FootmanYes`，就是玩家听到的那声「遵命」。
> - **补表安全**：脚本比过三张表的键，ack 1155 / combat 60 / anim 593 条，**无同名**，加载 AnimSounds 只新增条目，不会改动现有 What/Yes/Combat 的解析结果；顺序按客户端 `UnitAckSounds → UnitCombatSounds → AnimSounds`，同名覆盖规则也就一致。
> - **改动**：`War3SoundTables.Load` 补 `LoadTable("UISoundInfoAnimSounds")`；`Presentation.PlayVoice` 删掉 `fallback` 参数与回落分支，`Melee.OnUnitDied` 与 `Production` 两处 Ready 调用同步去掉 `null` 实参（Ready 原本就传 null，行为不变）。净改动 3 文件 5 行。
> - **验证**：`--creep-probe` 无头跑 (4)Turtle Rock，步兵阵亡日志为 `[VOICE] hfoo FootmanDeath FootmanDeath.wav`（修复前同一行会落空并回落 `FootmanYes`）。另把五族死亡标签逐条打印过，`FootmanDeath / PeasantDeath / GruntDeath / AcolyteDeath / ArcherDeath` 都解析到具体 wav 且 `ResourceLoader` 能读到。音效条目 2088（其中 AnimSounds 593 行）。
> - **玩家侧验证**：进任意 melee 图，拉步兵跟野怪对砍，打死一个听死亡音；建筑完工与训练完成仍是原来的 Ready 语音，没有被改动影响。
> - **脚步这半已由 57 收掉**：`SND` 事件链补上后，模型自带的死亡/攻击/工作音都会响；脚步不是 SND 而是 `FPT` 前缀事件，客户端也不解析，保持忽略。详见第 57 条。

### 26. DistanceCutoff 解析了但没用，半个地图外都能听见伐木声

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Audio/War3AudioSystem.cs:225-229,290-294` 用 `DistanceCutoff` 做「超出不播」的硬门，`maxDistance` 只作衰减终点。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Audio/War3SoundTables.cs:26,85` 解析了 `DistanceCutoff`，全仓再无读取；`War3UnitSound.cs:84-87` 只把 `MaxDistance` 传给 `AudioStreamPlayer3D.MaxDistance`。
- **玩家可见**：以伐木音为例（表值 Min 600 / Max 10000 / Cutoff 2100），客户端 2100 外听不到，Launcher 要到 10000 才静音，在自己基地能听到半个地图外的伐木、交战、施法。
- **要做**：`Start` 前按 `DistanceCutoff` 做一次距离门。

> **执行结论（Launcher5，已修复，待合并）**：
> - **定位**：`War3AudioSystem.Start` 整条路径没有任何距离判断，只有 Godot 的 `UnitSize/MaxDistance` 衰减；`War3SoundTables.Entry.DistanceCutoff` 解析出来后确实无人读取，`War3UnitSound` 的 `Play3D / PlayLooping / PlayOneShot` 只传 `MinDistance / MaxDistance`。另有一个前置缺口：Launcher 连「监听点」这个概念都没有，所以无从比较。
> - **改动**：`War3AudioSystem` 补 `UpdateListener(position)` 与 `WithinListenerDistance`（平方距离比较），`Start` / `Play3D` 末尾加 `cutoffDistance` 参数，`Start` 里按客户端口径 `gate = cutoff > 0 ? cutoff : maxDistance`，只对定位播放生效；`War3UnitSound` 三条定位播放把 `Entry.DistanceCutoff` 传下去；`Lifecycle._Process` 每帧喂 `_camera.Position` 给听者。共 3 文件，`War3AudioSystem` +21、`War3UnitSound` +6/-3、`Lifecycle` +1。
> - **验证**（临时探针按 key 去重打印被挡的音，跑完已撤）：`[AGATE] 挡 AxeMediumChopWood 距离 2109 门 2100`——正是本条举的例子（表值 Min 600 / Max 10000 / Cutoff 2100），改前要到 10000 才静音。另两档：`PlaceBuildingDefault 距离 10938 门 10000`（这条行里没有 cutoff，按客户端口径退回 maxDistance）、`Aren / Arst 距离 5202 / 12511 门 900`（法术 fallback 走程序化定义，同样按 maxDistance 兜底）。近处不受影响：`Repair`、`BuildingConstructionLoop`、`PlaceBuildingDefault` 照常播。
> - **监听点位置**：现在用相机位置，与 Godot 自己的 3D 听者一致，门和玩家实际听到的衰减对得上。第 27 条会把听者挪到镜头目标点并挂 `AudioListener3D`，届时这里跟着改成同一个点，两条要一起看。
> - **未做**：门只在 `Start` 时判一次，已经在播的循环音不会因为镜头拉远而中断，客户端同样只在建声部时判，保持现状。
> - **玩家侧验证**：开一局，镜头放在自家基地能听到自己的砍树、建造、交战音；把镜头拖到地图另一角，远处的砍树与交战音不再响（改前 10000 距离内都听得见）。

### 27. 3D 听者绑在相机上，客户端放在镜头目标点

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Scenes/War3GameScene/Rendering.cs:283-285` 每帧 `_audio.UpdateListener(cameraPosition, cameraTarget)`；`War3AudioSystem.cs:181-195` 明确把听者放在地面目标点，理由是高空相机会把地面单位压没。
- **Launcher**：无对应实现（全仓没有 `AudioListener3D`，也没有 `UpdateListener`）；Godot 的 3D 听者就是当前 `Camera3D`（`Game/Camera/War3Camera.cs:201-207`），默认档在目标点上方约 1370 单位。
- **玩家可见**：所有 3D 音都比客户端更远，拉远镜头整体变小、拉近才听得见（原版缩放不改音量）；听者带 34° 俯角，声像偏「来自下方」。
- **要做**：挂一个 `AudioListener3D` 到镜头目标点，每帧同步位置。

### 28. 同帧去重键不同：按标签去重 vs 按文件名去重

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Audio/War3AudioSystem.cs:236-239,300-305` 按解析后的文件名去重。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Audio/War3AudioSystem.cs:178-187` 按 `SoundName` 标签去重。
- **玩家可见**：同一条 ack 的 4 个变体在同帧最多响 3 条，不同标签指向同一文件时又不去重；成群单位同帧砍树时声音厚度和客户端不同。
- **要做**：去重键改成解析后的文件名，声部上限与抢占优先级维持现状。

### 57. MDX 的 SND 事件不解析，模型自带的死亡/攻击/工作音全静音

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Configuration/War3EventObjectData.cs:59-82` 拿事件名末 4 位查 `AnimLookups.SoundLabel`，再用标签查 `AnimSounds` 行；`Game/Scenes/War3GameScene/Presentation.cs:1793-1795` 收到 `War3ModelEventKind.Sound` 就 `_audio.Play(data, position)`（Combat 通道、优先级 60）。
- **Launcher**：`GameCore/L4_Composition/Game/Configuration/War3EventObjectData.cs:68-74` 只有 `SPL / UBR / SPN` 三个分支，`SND` 落到 `default`；`Game/Rendering/ModelEventEmitter.cs:131-136` 拿到 `Unknown` 直接 `continue`。所以 `Footman.mdx` 的 `SNDXDFOO`（死亡）、`Rifleman.mdx` 的 `SNDXKRIF`（攻击）、`peasant.mdx` 的 `SNDXAREP`（工作）从来不发。死亡音只能靠 `PlayVoice(unit,"Death")` 拼 `SoundSet + "Death"` 顶替。
- **玩家可见**：模型自带的声音事件全静音；死亡音是合成出来的，与模型事件指向不同文件时对不上；这条一接上，合成死亡音还会与模型事件各播一次同一条。
- **要做**：补 `SND` 分支（`AnimLookups → AnimSounds`），`HandleModelEvent` 播 `War3ModelEventKind.Sound`；删掉合成的死亡语音，避免与模型事件重复。

> **执行结论（Launcher5，已修复，待合并）**：
> - **事件名形状先量过**：跑 `--headless` 把模型的 `war3_model_events` 元数据打出来，`SND` 事件都是 8 字符（3 位前缀 + 1 位分隔 + 4 位键），如 `SNDXFDFR`、`SNDXDPES`、`SNDXKRIF`。注意脚步不是 SND：`Footman.mdx` / `peasant.mdx` / `Grunt.mdx` 里是 `FPTxFBL1 / FPTxFBR1`（FPT 前缀），客户端同样不解析 FPT，所以 FPT 保持忽略。第 25 条里「脚步一并丢失」这半句是误判，脚步音来自模型的 SND 变体（`SNDXFDFR → DeepFootstep`），不是 FPT。
> - **改动**：`War3EventObjectData` 加 `SoundLabel` 字段与 `ResolveSound`（`AnimLookups.AnimSoundEvent → SoundLabel → AnimSounds.SoundName`，同时拼出 `Resource` 供日志核对），`ResolveDefinition` 加 `"SND"` 分支；`Presentation.HandleModelEvent` 加 `War3ModelEventKind.Sound` 分支走新增的 `PlayModelEventSound`（`War3SoundTables` 取行 → `War3UnitSound.Play3D`，Combat、60，与客户端同通道同优先级）。
> - **重复问题**：模型 Death 序列的 `SNDXDFOO` 与合成路径 `SoundSet+"Death"` 指向同一个 `FootmanDeath.wav`，两条都留着会各播一次。客户端只有模型这一路（`Lifecycle.cs:585` 那一路是英雄阵亡播报 `HeroDiesSound`，不是单位语音），所以删掉 `Melee.OnUnitDied` 里的 `PlayVoice(unit,"Death")`，`PlayVoice` 只剩训练完工的 Ready。
> - **验证**（`--creep-probe --time=0.15`，无头）：`[AUDIO] 播放 DeepFootstep`、`RiflemanAttack1`、`Repair`、`FootmanDeath` 各就位；`FootmanDeath` 全程只播 1 次（改前模型事件接上会播 2 次）。逐条核对：`FDFR→DeepFootstep`、`KRIF→RiflemanAttack1`、`AREP→Repair`、`DFOO→FootmanDeath`、`DPES→PeasantDeath`、`DGLD→GoldMineDeath`，标签都能在 `AnimSounds` 命中实际 wav。
> - **顺带确认**：`SNDXAREP` 在 `Stand Work`、`Stand Work Gold`、`Stand Work Lumber` 三个序列里都有，是通用工作音，采矿/伐木播它不算选错动画。
> - **玩家侧验证**：进 melee 图，让步兵走动听脚步、砍野怪听攻击音、农民敲建筑听工作音、打死一个单位听死亡音；每种只响一次。

---

## 模块六 输入与镜头

> 键鼠语义、技能瞄准、拾取判定、镜头跟手。

### 30. 单位或点地双模技能，落点恒为施法者脚下

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Input/War3CommandInteractionComponent.cs:316-361` 对 `Unit / UnitOrPoint / Any` 先 `PickUnit` 取单位，取不到再用 `ScreenToTerrain` 取落点，技能据此算方向。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/QuickCast.cs:82-101` 只处理 `TargetMode.Unit` 与 `TargetMode.Point`，`point` 初值是 `caster.Position`，`UnitOrPoint`（`Src/Abilities/AbilityTargetMode.cs:10`）两不沾。受影响的有腐臭蜂群、震荡波、沉默，技能侧拿到零向量后回退成 `Owner.Facing`（`GameCore/L3_Simulation/Simulation/Configuration/RuntimeCarrionSwarmAbility.cs:59-63`）。
- **玩家可见**：腐臭蜂群、震荡波点哪里都无效，波形永远朝英雄当前朝向直喷；沉默的落点恒在自己身上。
- **要做**：补 `UnitOrPoint / Any / Destructable` 三条取目标分支。

> **执行结论（Launcher9，已提交，待合并）**
>
> 根因确认：`QuickCast.TryCastAt` 只有 `TargetMode.Unit` 会去 `PickUnit`、只有 `TargetMode.Point` 会去 `TryGroundPoint`，`UnitOrPoint` 两条都不进，`point` 停在初值 `caster.Position`。技能侧 `target == null && point == Owner.Pos`，`direction` 退化成零向量再回退 `Owner.Facing`（`RuntimeCarrionSwarmAbility.cs:59-63`、`RuntimeSilenceAbility.cs:49`）。
>
> 改法按客户端 `War3CommandInteractionComponent.cs:316-361` 的两段式重写（`QuickCast.cs:83-104`）：`Unit / UnitOrPoint / Any` 先 `PickUnit`，拾到就把 `point` 设成 `target.Position`；拾不到且模式是 `Unit` 才提示重选；其余模式走 `TryGroundPoint` 取地面落点。`BeginCast` 的待选提示按三种模式分开（`QuickCast.cs:51-57`），`UnitOrPoint` 不再显示「选择落点」。
>
> `Destructable` 这条没接 `PickDestructable`：Launcher 的可破坏物是 `TreeState`（山岭巨人吃树走 `Point` + `World.TakeNearestTree`），没有「对着可破坏物施法」的指令，全仓也没有技能声明这个模式（`Ability.TargetMode` 默认 `Unit`，只有沉默、腐臭蜂群覆写成 `UnitOrPoint`）。该分支现在退化为取地面落点，等第 31 条把可破坏物目标模型补齐后再接。
>
> 实测（临时在 `Cheats.cs` 挂了一段 AUcs 演示，跑完已撤）：恐惧魔王点地前方 700，靶子步兵再往后 260。
> - 改动前：`点击=True 靶子 420→420`，波朝英雄朝向飞走。
> - 改动后：`[TRYCAST] AUcs mode=UnitOrPoint target=- point=(-4142,12762) caster=(-4142,12141)`，落点与施法者相差 621，`靶子 420→220`（3 级 200 伤害）。
> - 尸体目标链路（`AUcb` 命令卡点尸体）输出与改动前逐字一致，`--skill-probe` 其余项无变化。
>
> 留了一处差异：点到不合法单位时，客户端先 `CheckConfiguredTarget` 报「无法对这个目标施放」，Launcher 仍由 `World.TryCast` 里的 `CheckCanTarget` 兜底报「施放失败」。

### 31. 右键缺三类目标：可破坏物、吃树、传送门

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Input/War3WorldInteractionComponent.cs:809-885` 右键按序尝试 `EnterWaygate`、可破坏物采集、`Aeat` 吃树、`IssuePlayerAttackDestructable`、地面物品拾取，逐支只吃掉能干这活儿的单位。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Input/War3WorldInteractionComponent.cs:301-347` 只到伐木（限能砍木的工人）和地面物品，没有可破坏物攻击、没有吃树，全仓也没有 `EnterWaygate`。
- **玩家可见**：剑士、火枪右键点树或桥不会攻击它，而是绕过走到落点，清树开路和拆桥失效；山岭巨人右键树不会吃树；带传送门的地图右键无效。
- **要做**：按客户端顺序补齐这几支，目标类型不完备是第 30 条的同根问题。

> **执行结论（Launcher9，已提交，待合并）**
>
> **可破坏物攻击：已补。** Launcher 的可破坏物只有树（`TreeState`：Hp + `CreationNumber` + pathTex 撤除），所以「攻击可破坏物」落成「右键点树，走过去按武器伤害砍」。新增 `Unit.AttackTree`、`World.SetAttackTree`、`StepAttackTree`（`Src/World/World.MechanismMembers.cs`，紧挨 `StepChop`）：`Advance` 到 `AttackReach+48` 内，按 `AttackCooldownOf` 的节奏挥一次，`tree.Chop(武器骰 + DamageBonus)`、播 `PlayAttack`，砍倒后 `ClearTreePathing` 撤掉那格阻挡再收单。订单复位挂进 `ApplyOrder` / `ClearOrders`，任何新指令都会撤销。右键分支排在伐木之后，只吃 `CanAttack` 的非建筑单位。
>
> **吃树：已补。** `Aeat` 在 Launcher 原来是 `IsPassive=true` 的自动行为（血低于 70% 就吃最近的树，源码注释自己写着「原版是玩家主动点树；这里退化成…」）。改成客户端形态：`TargetMode = Destructable`、`IsPassive` 取消、`OnEffect` 吃掉点到的树并按 `DataC / Dur` 挂 `HealOverTimeBuff`，`CheckTarget` 用 `TakeNearestTree(point, 120)` 拒掉空点。右键顺序与客户端 `:837 → :856 → :869` 一致：伐木 → 吃树 → 攻击可破坏物 → 地面物品。命令卡的「吃树」按钮走 `QuickCast` 的 `Destructable` 分支（`PickTree` 取落点），补上了第 30 条留下的那个口子。
>
> 顺带一个行为确认：生产型古树（`eaom` 这类）右键树仍然是**设集结点**，因为客户端的集结点分支在 `:746` 就在吃树之前，Launcher 的 `TryIssueRallyAt` 也是第一支，两侧同序。真正吃树的是 `etrp` 这种非生产古树。
>
> **传送门：不做。** 客户端 `WaygateConfigured / WaygateActive` 只有 JASS 写（`Tool/Tests/Game/WaygateEffectTests.cs:151` 的断言原话是「campaign script did not activate/configure gates」，native 侧也只有 `WarpNativesTests` 覆盖 setter）。Launcher 不跑 JASS，melee 图没有传送门，属范围第 10 条排除的脚本表现。
>
> **非树可破坏物（桥、箱子）：不做，另立第 60 条。** `DoodadPlacement` 只有 `Life` 百分比字节，没有运行时 HP、死亡动画和掉落，树之外连状态对象都没有。
>
> 实测（临时在 `Cheats.cs` 挂演示，跑完已撤）：
> - 步兵右键树：派单后 `AttackTree=True`，20 秒后树 `HP 50→0`、`存活=False`，步兵停在距树 136（射程 88+48）。
> - 古树 `etrp` 右键树：`树木存活=False`，血 `300→472`（200 tick ≈ 10 秒 × 500/30）。
> - 命令卡点「吃树」再点树：`点击=True`，点中的树被消耗。
> - 伐木回归：工人 `接单=True`、`携带=10`、树 `HP 50→30`。
> - `--state-probe` 只剩既有的 `utod` 一项；`Aeat` 的自检改成「先 TryCast 再验回血」，三项 OK。

### 32. 左键点敌方或友方单位会清空当前选择

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Input/War3WorldInteractionComponent.cs:654-723` 拾取判据只有 `Alive && !Removed`，敌方与我方都能单选查看，只有控制权不同。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Input/War3WorldInteractionComponent.cs:83-104` 用 `PickUnit(position, ViewPlayer)`，其判据 `MatchesOwner`（`Game/Input/War3PickingComponent.cs:152-161`）只认自己与中立商店，其余返回 null，走到 `_selection?.Clear()`。
- **玩家可见**：想点一下敌方英雄看装备血量，或者点友军看状态，结果自己整队选择被清空，战斗中误点一次就丢操作对象。
- **要做**：把「能否选中查看」与「能否控制」拆开。

### 33. 框选忽略飞行高度，空中单位要框到它的地面投影才中

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Input/War3PickingComponent.cs:186-196` 投影点用 `RenderPosition + RenderFlyHeight`。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Input/War3PickingComponent.cs:130-134` 投影点用 `unit.Position + View.Height * 0.5f`，既没算 `RenderFlyHeight`（模型在 `ground + RenderFlyHeight`，见 `Game/Presentation/UnitSceneComponent.cs:334`）也没用插值坐标。
- **玩家可见**：框选狮鹫、冰龙必须把框拉到正下方地面才选得中，框住模型本身反而漏选；快速移动的单位框选边缘还慢半拍。
- **要做**：加上 `FlyHeight`，优先用渲染插值坐标。

### 34. 中键按住不动超过 0.1 秒，跟随镜头把视角抢回去

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Camera/War3CameraComponent.cs:35-37` 每帧 `if (_dragging) _runtime.ResetCameraPanUnlockTimer();` 续期。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Camera/War3CameraComponent.cs:19-62` 没有这条续期，只在 `BeginDrag`（`:80`）和收到移动事件时（`:106`）重置，窗口由 `Scenes/War3GameScene/CameraFollow.cs:64-79` 倒计时。
- **玩家可见**：开着镜头总跟随时，按住中键稳住视角、手停 0.1 秒以上，镜头被被跟随单位拽走；再拖动时第一段位移被丢掉，表现为回弹。
- **要做**：补每帧续期。

### 35. 镜头居中改瞬移，还顺手关掉了跟随

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Scenes/War3GameScene/Input.cs:1226-1230` 用 `_camera.PanTo(x, y, 0.25f)`；`Game/Camera/War3Camera.cs:345-355` 的 `SetTarget` 只清目标与朝向控制器，保留软跟随，平移结束后跟随自动接回。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/Input.cs:127-131` 走 `_camera.Focus(...)`，`Game/Camera/War3Camera.cs:279-297` 的 `SetTarget` 在 `:290` 直接 `_softFollowController = null`，位置立即跳变。这个入口被 Ctrl+C、双击编队、退格切城镇、小地图点选共用。
- **玩家可见**：开总跟随后按 Ctrl+C 或点一次小地图，跟随被永久关闭；画面硬切而不是 0.25 秒滑过去。
- **要做**：`Focus` 换成平滑 `PanTo`，保留软跟随。

### 36. 开局镜头落在矿边工人锚点，不是出生点

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Scenes/War3GameScene/Lifecycle.cs:727-744` 在 main 之前按 `GetPlayerStartLocationPosition` 把镜头移到出生点。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/CameraFollowTrack.cs:113-135` 取 `_startFocus[ViewPlayer]`，该值来自 `Units.cs:174` 的 `plan.Camera`，而 `GameCore/L3_Simulation/Simulation/Configuration/MeleeRules.cs:133,139` 里人类、兽族把它设成从出生点朝金矿偏 320 的 `workerAnchor`，亡灵是侍僧簇中心。
- **玩家可见**：开局镜头中心比客户端偏 2.5 格朝矿区，主基地被推到画面偏侧；出生点在角落的地图偏移方向更明显。
- **要做**：改 `player.StartX/StartY`；如果当初是为了让工人入画，需要先确认这个取舍。

> **复核评价**：差异成立（`MeleeRules.cs:131,139` 把 `plan.Camera` 设成 `workerAnchor`），但「要做」自带一个未决问题，这种条目执行到一半就会停下来问。开工前先把结论写进这条：客户端出生点相机是 `GetPlayerStartLocationPosition`，没有「工人入画」这层，`MeleeRules` 的偏移就是 Launcher 自加的行为，按规则 6 直接改掉、不留取舍。改完顺手看 `plan.Camera` 这个字段还有没有别的用处，没有就删。

---

## 模块七 界面

> 加载屏、HUD、面板、提示。

### 37. 加载屏背景还是四等分，客户端已经改成上 2/3 下 1/3

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Rendering/War3LoadingScreenRenderer.cs:532-580` 先用加载屏模型（2×2 平面）铺背景，`:613-630` 回退直绘时 `top = Height * 2f/3f`、`bottom = Height - top`，另有 `FULL` 整幅兜底。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Rendering/War3LoadingScreenRenderer.cs:292-312` 四象限按 `DesignHeight * 0.5f` 等分，没有背景模型路径，`:161-168,309` 候选不足 4 张时只画深色底，没有 `FULL` 兜底；`:510-518` 的 `DumpLayout` 却按 `2f/3f` 打印。
- **玩家可见**：1024×768 设计画布下，客户端上带 512 下带 256（天地 2:1），Launcher 上下各 384，地平线被压到正中，四张图上下各被拉压约三成；单张 loading 图的地图在 Launcher 上是近黑底加文字。更麻烦的是调试日志会显示「已对齐」，掩盖真实差异。
- **要做**：绘制改 2/3 分带，补背景模型与 `FULL` 兜底，`DumpLayout` 改为打印实际绘制值。

> **执行结论（Launcher7，已修，待合并）**：
> - **定位**：三件事是同一处代码。`War3LoadingScreenRenderer.cs:292-312` 的 `DrawBackgroundQuads` 取 `mid = DesignHeight * 0.5f` 上下各铺 384；而四张 loading 贴图的原生尺寸是 `Loading-TopLeft/TopRight` 512×512、`Loading-BotLeft/BotRight` 512×256（`Extract/extract/UI/Glues/Loading/Multiplayer/` 实测），1024×768 设计画布下本应上 512、下 256。上带被压 0.75 倍、下带被拉 1.5 倍，接缝从 2/3 掉到正中。`CollectBackground` 只认 TL/TR/BL/BR 四个后缀，模型只给一张整图时 `_background.Count < 4` 直接落深色底，客户端 `:628-631` 的 `FULL` 那一支没有对应实现。`DumpLayout:512` 又按 `2f/3f` 打印，日志写的和实际画的对不上。
> - **改法一（背景模型）**：`CollectModelTiles:195` 实例化 extract 导出的加载屏场景（`Load-Multiplayer-{Race}.tscn`），逐 `MeshInstance3D` 取 AABB 与材质 `base_texture`，用全表面并集把 x 轴、z 轴各自归一化到设计画布。这就是客户端 `DrawBackgroundModel:532-580` 的做法——取几何包围盒归一化整屏、每个 geoset 贴自己的图——只是 Launcher 的 UI 是立即模式绘制，把三角形换成每片 geoset 的矩形，省掉一层 3D 渲染与雾/光照依赖。对战图实测四片落在 `0,0,512,512 / 512,0,512,512 / 0,512,512,256 / 512,512,512,256`，模型自带的 2/3 分带自然还原，比例不再写死。
> - **改法二（贴图回退）**：几何拿不到时走 `CollectTextureTiles:244`：四张齐 → `top = DesignHeight * 2f/3f`、`bottom = DesignHeight - top`（与客户端 `:621-626` 同一套算式）；否则有 `FULL` 就整幅铺（`:628-631`）；都没有才画深色底。`CollectBackground:149` 里非 TL/TR/BL/BR 的贴图归到 `FULL`（对应客户端 `:137`），四张缺位时才用硬编码文件名补位。
> - **改法三（日志）**：`_backgroundMode` 记 `model/quads/full/solid`，`DumpLayout:563` 打印 `bg mode=` 和每片真实矩形的 `bg tile=x,y,w,h tex=WxH path=...`，删掉原来按 2/3 硬算的 `bg rects` 假日志。底部 92px 压暗条与客户端 `:725` 一致，未动。
> - **验证**：(2)BootyBay 全流程 `OPENWC3_LOADING_DEBUG=1` 打印的四片矩形就是上面那组值；同一张图改动前后各截一次（`--shot=/tmp/loading.png@0.8`），接缝从画面正中移到 2/3，插图不再上下拉压。临时关掉几何路径跑出 `mode=quads`，打印的仍是同一组 2/3 矩形；再临时只留一张整图，`mode=full` 打印 `0,0,1024,768`。三个分支都实测过，`dotnet build` 0 错误。
> - **复现**：`cd Launcher7 && OPENWC3_LOADING_DEBUG=1 Godot --headless --path . -- --play="../test/map_gmap/(2)BootyBay.gmap" | grep "bg "`；看画面用 `Godot --path . -- --play=../test/map_gmap/\(2\)BootyBay.gmap --shot=/tmp/loading.png@0.8`。
> - **顺带发现（不在本条范围）**：`ResolveBackgroundModel:118` 只按 campaignBackground 查 `WorldEditData` 的 `LoadingScreens` 表；战役背景模型（`Backgrounds/Campaigns/*.glb`，14–15 片 geoset，除四张底图外还有立绘摆件）会落进几何路径，把摆件按 AABB 画成方块。Launcher 不跑战役，暂时看不到，接战役或自定义模型时再处理。
> - **提交**：`任务37：加载屏背景按模型几何分带，补 FULL 兜底与真实布局日志`。

### 38. 加载到 100% 后多出 0.55 秒不可操作窗口

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Hosting/GameApplication.cs:354-380` 用 `using` 管理加载屏，场景 `OnLoad` 同步跑完 13 个阶段，`Report(1f)` 后方法结束即 `Dispose`，下一帧就是可操作的游戏。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/Lifecycle.cs:146-161` 先 `await ProcessFrame` 让加载屏画出来，`War3LoadingScreenRenderer.cs:569-585` 再等 3 帧 settle 并做 0.5 秒 `Modulate` 淡出，期间 `LoadingBlocked` 为 true（`:521,713-715,780-794`），音乐也要等 `StartPendingMusic`。
- **玩家可见**：进度条满了还有半秒不能操作、没有音乐；这三帧 settle 本身说明世界刚建好的头几帧是黑的。
- **要做**：按客户端改成同步组装、硬切进游戏；如果保留淡出，需要在文档里登记为平台差异。

> **执行结论（Launcher7，已修，待合并）**：
> - **定位**：收尾是三段串联。`War3LoadingScreenRenderer.Finish()` 原来只置 `_finishing`；`FrameSeconds` 先空转 3 帧 settle，再用 `FadeSeconds = 0.5` 把 `Modulate` 从 1 拉到 0；`Finished` 到淡出结束才为真。而 `Lifecycle.LoadingBlocked:536` 就是 `!_loadingScreen.Finished`，于是 `_Process:728` 与 `_PhysicsProcess` 在进度 100% 之后还要空转 3 帧 + 500ms，`StartPendingMusic:729` 排在后面。玩家看到的就是「条满了、半秒不动、也没音乐」。
> - **改动**：`Finish:321` 改成硬切——进度置 1、`_finished` 立刻置真、`QueueFree` 掉 `CanvasLayer` 与自己；删掉 `FrameSeconds`、`FadeSeconds`、`_settleFrames`、`_fade`、`_remaining`（构造参数 `seconds` 只服务淡出，一并删除）；`Lifecycle._Process` 不再驱动加载屏。
> - **为什么没做「13 个阶段全同步」**：试过，实测更差。`RenderingServer.ForceDraw()` 不派发 CanvasItem 的 `_Draw`——整段加载里 `[loading-draw]` 只出现 1 次，且是在全部阶段跑完之后；ForceDraw 只是把半成品世界画了 12 次，白费约 500ms，而 3.85 秒的加载期间窗口一帧不出、进度条不动。客户端能同步是因为 `Report` 里 `RenderCurrent()` 之后直接 `SwapBuffers()`（客户端 `War3LoadingScreenRenderer.cs:267-274`）内联出图；Godot 的 canvas 更新由主循环驱动，阻塞主线程就出不了图。因此保留「阶段之间让出一帧」（`Loading:529`，发生在加载过程中，不在 100% 之后），按平台差异处理，不计为淡出遗留。
> - **验证**：改前从 `Finish` 到首个可操作帧是 3 帧 + 500ms；改后实测 `finish 5157ms → first frame 5170ms`，一帧 13ms。加载中截图与改动前逐字节相同（进度条、2/3 背景无回归）；加载完成后 6s 截图是正常游戏画面（HUD、小地图、单位齐全）；`(2)BootyBay` 无头跑 11 条 `[INIT]` 全出、无异常日志。
> - **复现**：`cd Launcher7 && Godot --path . -- --play="../test/map_gmap/(2)BootyBay.gmap"`，看进度条到顶那一刻是否立刻能操作、音乐是否同时起来。
> - **提交**：`4fce638 任务38：加载完成即硬切进游戏，去掉 3 帧 settle 与 0.5 秒淡出`。

### 39. HUD 不走 FDF 帧树，坐标是手抄常量，4 个 FDF 绑定没有生成

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/War3FdfUi.cs:38-49` 从 `ConsoleUI.fdf / ResourceBar.fdf / UpperButtonBar.fdf / InventoryCover.fdf` 装载帧树，帧引用全部走 `Generated/*.g.cs` 强类型绑定；`MeleeHudController` 在帧树上挂内容。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/UI/GameHudScreen.cs:37,140-142` 只用 `FdfUiFactory` 读 `GlobalStrings.fdf` 的文案，帧体全部手搭；`Generated/` 下只有 `LoadingScreenBinding.g.cs`，没有 `ResourceBarBinding / ConsoleUiBinding / UpperButtonBarBinding / InventoryCoverBinding`。`FdfScreen` 这套 FDF 装载只被 `Platform/Godot/App/LauncherApp.cs:465-474` 的调试开关 `--ui-screen` 用到。
- **玩家可见**：资源条的图标尺寸、数字基线、四组间距由 `TopBarFrame.cs:147-167` 的常量决定，和 FDF 的右对齐贴边不同，越靠右越明显；FDF 皮肤下的按钮条与空背包遮罩不存在；维护费文案是中文「无/低/高维修费用」，客户端 FDF 路径是 `No/Low/Heavy Upkeep`。
- **要做**：补 `ResourceBarBinding` 等生成，HUD 改成「FDF 优先、手写兜底」，两边同一套锚点。

> **复核评价**：方向成立，证据有两处要修。一是落盘位置：绑定生成到 `GameCore/L4_Composition/Game/UI/Generated/`（`FdfBindGen.cs:56-58`），不是顶层 `Generated/`，那个目录里只有表数据。二是目标数量：`FdfBindGen.Targets` 现在是 6 个（`ConsoleUiBinding`、`ResourceBarBinding`、`UpperButtonBarBinding`、`LoadingScreenBinding`、`MainMenuBinding`、`EscMenuMainPanelBinding`），成品只有 `LoadingScreenBinding.g.cs` 一个。清单只点了 4 个，漏了 `EscMenuMainPanelBinding` 和 `MainMenuBinding`；而第 41 条要把 Esc 菜单换成 FDF 面板，正需要 `EscMenuMainPanelBinding`。这条把「补哪些绑定」写全，41 才有得依赖。

> **执行结论（Launcher7，已修，待合并）**：
> - **绑定生成**：`--fdf-bindgen` 本来就能出 6 个，只是产物一直没落盘。这次把 `ConsoleUiBinding`、`ResourceBarBinding`、`UpperButtonBarBinding`、`MainMenuBinding`、`EscMenuMainPanelBinding` 一起生成进 `GameCore/L4_Composition/Game/UI/Generated/`（`LoadingScreenBinding` 重跑后与原文件逐字节相同）。数量按 `FdfBindGen.Targets:15-50` 的 6 个走，`EscMenuMainPanelBinding` 第 41 条直接可用。
> - **`InventoryCoverBinding` 生不出来**：客户端那份的来源是 `UI\FrameDef\SmashUI\InventoryCover.fdf`（client `Assets/` 内置覆盖），extract 导出的 `Generated/FdfTables.g.cs` 里没有这张文件，也没有 `FrameDef.toc`/`SmashFrameDef.toc`。所以空背包遮罩继续走手写 `ConsoleSkinFrame` 的 `HumanUITile-InventoryCover.blp`，位置与 UV 与 FDF 同源（0.3125..1 的底条），改前改后逐像素相同。要真的走 FDF，得先让 Extract 把 Smash 那套 FDF 导进表里。
> - **HUD 改成 FDF 优先**：新增 `UI/HudFdfUi.cs`，照客户端 `War3FdfUi` 的顺序装配：`ConsoleUI` 作根、`ResourceBarFrame` 贴 `TopRight`、`UpperButtonBarFrame` 贴 `TopLeft`、资源文字宽度借 `ResourceBarUpkeepText` 并右对齐、竖直居中。`GameHudScreen.BuildFdfChrome:211` 把它插到 `_root` 第 0 位并 `SetAllPoints`（chrome 画在动态内容之下，也才收得到鼠标），`MenuButton` 接 `MenuClicked`。资源值走 `HudFdfUi.SetResources:79`，维护费改成 `No/Low/Heavy Upkeep`（颜色同客户端 `(1,.25,.25)/(1,.78,.25)/(.38,1,.38)`）。
> - **兜底**：`OPENWC3_FDF_UI=0` 或不满足条件时仍用 `ConsoleSkinFrame`/`TopBarFrame`；FDF 生效时 `_console.SetTilesVisible(false)` 只关底板、保留背包遮罩（否则 FDF 底板的背包格会顶替遮罩，非英雄选中时会露出背包美术），`_topBar.SetChromeVisible(false)` 关掉手写的按钮与资源组，`FPS` 读数改挂到 `UpkeepText.RenderBounds` 下沿。
> - **验证**：`--ui-hud` 探针对比，改前/改后整图差异只有 `(255,6)-(1872,55)` 这条顶栏：菜单按钮变成 FDF 的「菜单 (F10)」，三个资源组按 FDF 锚点右移贴边，维护费变绿色 `No Upkeep`；底部控制台与背包遮罩像素级无差异。`OPENWC3_FDF_UI=0` 跑同一探针，整图与改前 baseline 完全一致（diff 无差异）。实机 `(2)BootyBay` 加载完成后 HUD 正常、无异常日志。
> - **仍缺**：FDF `UpperButtonBar` 四个按钮都已生成，但任务/盟友/聊天三个面板还没做（第 40 条），现在先把这三颗按钮 `Visible=false`、`Disabled=true`，视觉上与改前一致；等 40 落地后打开并接 `OnQuestClicked/OnAlliesClicked`。
> - **复现**：`cd Launcher7 && Godot --path . -- --ui-hud --shot=/tmp/hud.png@1.0`；回退对照加 `OPENWC3_FDF_UI=0`。
> - **提交**：`65b9821 任务39：补 FDF 强类型绑定，HUD 底板与资源条走 FDF 优先、手写兜底`。

### 40. 顶栏只剩一个菜单按钮，任务日志与盟友面板整族缺失

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/TopBarFrame.cs:39-40` 四个按钮全部可见，`MeleeHudController.cs:544-545` 绑 `OnQuestClicked/OnAlliesClicked`，`UI/QuestDialogFrame.cs:108-112` 由 F9 开关。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/UI/TopBarFrame.cs:17` `MenuInScope = {false,true,false,false}`，`:40-41` 只显示索引 1；`GameHudScreen.cs:167-170` 只接菜单按钮。`QuestDialogFrame`、`AllianceDialogFrame`、`LeaderboardFrame`、`MultiboardFrame`、`TimerDialogFrame`、`CinematicSceneFrame` 都没有对应文件。
- **玩家可见**：左上角四连按钮少三个，按 F9 没有任务日志，看不到盟友资源与状态（原版 F11）；过场时 HUD 不整层隐藏。
- **要做**：补任务与盟友两个原生面板（与 JASS 无关），其余计分类面板另立任务。

> **复核评价**：盟友面板成立，任务日志这半越界。「与 JASS 无关」这个判断只在盟友面板上成立；`QuestDialogFrame` 的数据模型就是 JASS 对象——`_objectives` 的类型是 `JassGameNatives.QuestItemView`（`QuestDialogFrame.cs:25`），`SyncDetails` 全部从 `_selected.Items` 取（`:156-164`），任务列表本身由 `JassGameNatives` 维护（`JassGameNatives.cs:547-567`）。Launcher 不跑 JASS，就没有 quest 对象，面板补出来是空壳，只有全局失败条件的追加那一路有内容。按范围第 10 条「脚本才能触发的表现不进清单」，任务日志应删掉，只留盟友面板；如果要保留，至少要写明「内容来自地图脚本，melee 图下会显示空列表」并确定这算不算要修。

### 41. Esc 菜单是自建 1600×900 遮罩，打开也不暂停

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/EscMenuFrame.cs:36,114-129` 实例化 `EscMenuMainPanel.fdf` 帧树，主面板 368×340 锚在屏幕中心；`MeleeHudController.cs:551-552,1092` 只要 Esc 菜单或任一对齐面板可见就请求暂停。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/UI/EscMenuFrame.cs:16-33` 自建 `CanvasLayer + ColorRect`，遮罩 `1600×900`、面板写死 `(600,270) 400×360`，`:48-62` 用 Godot 原生 `Button`；`GameHudScreen.cs:478` `AutoPauseRequested => AboutDialogVisible`，Esc 菜单不计入；`Scenes/War3GameScene/Lifecycle.cs:60` 同样只认设置页。
- **玩家可见**：菜单是深灰方块加系统默认按钮，和旁边石质九宫格的选项页两套风格；1920×1080 下黑幕只盖到 1600×900 右下留亮边，面板偏左 160；多出一个原版没有的「暂停游戏」项；最要紧的是开着菜单游戏还在跑，玩家在菜单里会被打死。
- **要做**：Esc 菜单并入 `MENU` 暂停请求，外壳改 FDF 面板并按 UI 视口居中。

### 42. Tooltip 跟着控件跳，客户端固定在右下信息栏

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/TooltipFrame.cs:774-787` 默认固定为「右缘内缩、底边压在 console 上沿」，跟锚点只是对照用的环境变量；`:397-405` 有 `ShowUnitTip`（单位名、Tip、血蓝条），`MeleeHudController.cs:381-435` 覆盖多选头像、单位信息面板、顶栏三个悬停源；`:213,584` 支持 `|T路径:尺寸|t` 内联图标。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/UI/TooltipFrame.cs:304-321` 的 `Place` 贴在控件上方或下方；`GameHudScreen.cs:316-324` 只处理 `ButtonFrame`；`UI/Views/CommandCardView.cs:6-17` 没有 `Ext` 字段，`ShowEx` 的扩展块走不到；`UI/GodotUiRenderDevice.cs:370-410` 不识别 `|T`。
- **玩家可见**：悬停金币图标提示框贴屏幕顶，悬停命令格又压在 console 上，位置每次都变；悬停单位信息面板与多选头像完全不弹提示；带内联图标的文案原样露出 `|T...|t`；建筑升级提示缺「需要: …」段。
- **要做**：定位改固定右下，补三个悬停源与 `|T` 解析。

### 43. 命令卡 5Hz 刷新，蓝不足不变红，待选目标不高亮

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Scenes/War3GameScene/Ui.cs:777-785` 命令卡与当前选择逐帧同步；`UI/CommandCardBuilder.cs:280-295` 同时写 `IconColor`（蓝不足 `0xff4040ff`）、`DimWhenDisabled`、`NumberText`、`TooltipExt`，`:208-219` 的 `UpdatePending` 按 `Kind + Payload + Slot` 高亮。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/Lifecycle.cs:764-772` HUD 与命令卡只在 `_hudTimer >= 0.2f` 时刷新；`UI/CommandCardBuilder.cs:167-178` 只设 `Icon/Disabled/Cooldown/Overlay`，从不写 `IconColor`；`GameHudScreen.cs:626-632` 只比对 `Kind`；`Scenes/War3GameScene/Commands.cs:35-45` 只有移动、攻击、巡逻会设 pending。
- **玩家可见**：冷却扇形每 0.2 秒跳一格；法力不足的技能只变暗不转红；点技能进入选目标状态时命令卡那格不高亮（只有移动攻击巡逻会亮）。
- **要做**：HUD 改逐帧刷新，补 `IconColor` 与 `Kind+Payload+Slot` 的 pending 比对。

### 44. 信息面板的攻击/护甲图标恒为空白，等级徽标显示的是英雄等级

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/UnitInfoPanelFrame.cs:299-339` 按武器攻击类型与护甲类型贴图标，徽标显示攻防升级等级，伤害包含主属性、物品与科技并以绿色显示加成；`:268-270` 金矿显示剩余黄金。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/Ui.cs:1308-1327` 攻击取 `type.Weapon.MinDamage-MaxDamage`（不含加成），`AttackLevel` 传的是 `unit.Level`，不传图标；`Game/UI/UnitInfoPanelFrame.cs:197-216` 对应三个 `TextureFrame.Texture` 从未赋值。
- **玩家可见**：面板左侧攻击护甲图标与右侧主属性图标都是空白只剩文字；黄色小徽标显示英雄等级而不是科技等级；攻击数值普遍偏小；选中金矿不显示剩余黄金。
- **要做**：补图标解析与科技等级，伤害改走客户端的加成口径。

### 45. 每次成功下令都弹一行黄字

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Scenes/War3GameScene/Ui.cs:667-776` 这条通道只用于「资源不足 / 无法丢弃 / 物品栏已满」等失败原因；`MeleeHudController.cs:798-799` 转发到 `CommandErrorFrame`。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/Commands.cs:92-139,240` 把「移动到…按阵型展开」「巡逻」「攻击移动」「停止/据守」「自动施法开关」全部走 `SetHint`；`GameHudScreen.cs` 的 `SetHint` 直接 `ShowCommandError`。
- **玩家可见**：每点一次地面就刷一行，连续操作时黄字不断叠现，真正的错误提示被淹没。
- **要做**：成功路径不再走错误通道，需要的话只在调试开关下显示。

### 46. HUD 细节：队列底板是黑块、框选是绿色、多选血条没有底

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/QueueIconRowFrame.cs:36-43` 用 `human-unitqueue-border.blp` 皮肤；`MeleeHudController.cs:942-951` 框选是蓝色填充 `(0.15,0.4,1,0.15)` 加 1.5px 蓝边；`UI/SelectionGroupFrame.cs:77,84` 血蓝条黑底 `(0,0,0,1)`；`UI/PortraitFrame.cs:50-58` 永久无敌时清空生命魔法文字。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/UI/QueueIconRowFrame.cs:30-31` 换成 `SolidColorFrame(0,0,0,.55)`；`GameHudScreen.cs:890-892` 框选是绿色 `(0.35,0.85,0.35,0.16)` 且无描边；`UI/SelectionGroupFrame.cs:73,80` `BackgroundColor = Vector4.Zero`；`UI/PortraitFrame.cs:59-68` 没有无敌判断。
- **玩家可见**：建筑生产时出现一块纯黑矩形；框选是绿色半透明，和客户端的蓝色带亮边一眼可分；多选头像下的血条没有黑底，亮地形上看不清；无敌单位仍显示「100/100」。
- **要做**：四处按客户端取值，队列底板走皮肤。

### 52. 英雄上飞艇或钻进地洞后，左上角英雄栏少一个头像

- **客户端**：`Open-Warcraft3_Wiki/Src/Units/CargoTransport.cs:115` 装载只把乘客加进 `_passengers`，仍留在世界单位表里；`Src/Core/World.UiMembers.cs:24-41` 的英雄候选按这张表重建，上船或进洞时头像、选中面板都还在。
- **Launcher**：`Launcher/Src/Units/CargoTransport.cs:79` 装载时 `_world.Units.Remove(passenger)`，`Src/World/World.UiMembers.cs:24` 的英雄候选按同一张表重建，头像当场消失，卸载才回来。
- **玩家可见**：把英雄装上飞艇偷矿或塞进地洞，左上角英雄栏空一格、编队里也点不到这个英雄，看着像丢了单位。
- **要做**：装载不从公共单位表摘人（照客户端只摘空间索引），或者让英雄候选把货舱里的英雄算回来。

---

## 模块八 工程结构（横切）

> 命名、归属与共用。放最后，避免和功能改动互相踩。

### 48. 技能工厂与技能实现文件的结构和客户端对不上

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Configuration/` 下工厂拆成五个文件：`RuntimeAbilityFactory.cs`（1044 行，主分派）、`.Constructors.cs`（384 行，按 rawcode 造实例）、`.Initializers.cs`（194 行）、`.Specialization.cs`（62 行）、`.Initialization.cs`（60 行），不是把一张字典切成几段；技能实现按族分文件，该目录下 194 个 `Runtime*Ability*.cs`。
- **Launcher**：`Launcher/Src/Abilities/RuntimeAbilityFactory.cs` 一个文件、一张 523 条的 `["xxxx"] = () => new …` 字典；实现文件 `Src/Skills/` 47 个 + `GameCore/L3_Simulation/Simulation/Configuration/` 131 个 = 178 个，和客户端 194 差 16，不是「都塞在少数聚合类里」。
- **玩家可见**：目前无直接表现。真正的代价是新增或修技能时两边的登记口不在同一处，容易只改一边；`AbilityCatalog` 里物编字段读不到时技能会静默退化成无行为被动。
- **要做**：先照客户端把 `Map` 按「构造 / 初始化 / 特化 / 依赖准备」拆成五个 partial 文件（纯搬运，不改行为），再逐个技能把实现挪到与客户端同名的文件里。**不要**把 `AbilityType`/`AbilityCatalog` 改名叫 `ObjectDataAbility`：客户端那个是 `Ability` 子类（`Src/Abilities/ObjectDataAbility.cs`），Launcher 这个是纯数据行（`AbilityCatalog.cs:12`），同名会误导；对齐方向应该是让 GameCore 的技能走 `ObjectData` 那条路。

> **复核评价（2026-11-15）**：数字已按两棵树重数（523 登记、178 实现文件、客户端 194）。本条与已清掉的第 47 条（重复实现）是同一件事的两半：去重已完成，剩下的是「结构」。拆 partial 是零行为改动的搬运，可以先做；「实现文件按族对齐」要 178 次改名，必须分批并每批跑 `--state-probe`。

### 49. 核心类的目录归属和客户端不一致

- **客户端**：`World` 在 `Src/Core/`（`World.cs` + 12 个 partial，另有 `WorldUnitCollection.cs` 承载单位表），`AbilityContainer` 在 `Src/Components/`。
- **Launcher**：`World` 及其 29 个 partial 全在 `Src/World/`，`AbilityContainer` 在 `Src/Abilities/`，单位表是 `Src/World/UnitList.cs`、树状态是 `Src/World/TreeState.cs`（客户端叫 `WorldUnitCollection.cs`，没有 TreeState）。
- **玩家可见**：无直接表现，代价是跨仓库对照检索时同名类落在不同目录。
- **要做**：分两步。**第一步只搬 10 个两侧同名的 partial**：`DamageMembers / HeroSelectionOrder / MechanismMembers / MovementEvents / Rally / RelationMembers / RepulseMembers / Targeting / UiMembers / UnitLookup` → `Src/Core/`，并把 `UnitList.cs` 改名 `WorldUnitCollection.cs`、`AbilityContainer` 挪到 `Src/Components/`。**第二步要先有归属决定**，涉及 19 个客户端没有同文件的 partial：`Abilities / Blight / BuffMembers / Channels / CorpseRevive / CreepSleep / DeathTriggers / FogRevealMembers / GroundSpells / GroupMovement / HeroRevive / Illusions / InjuryFleeMembers / Items / Morph / Orders / PhoenixEgg / Repair / WeaponSystem`——客户端的职责散在 `Src/Units/Unit.Items.cs`、`Src/Components/WeaponSystem.cs`、`OrderQueue.cs`、`AbilityContainer*.cs` 里，逐个决定「并回 Unit / 并回 Component / 留在 World」，落成映射表后再搬。`TreeState.cs` 客户端没有，属 Launcher 自有结构，不搬。

> **复核评价（2026-11-15）**：整目录搬迁执行不下去的原因已写进「要做」。第一步是纯文件搬运（namespace 不变），可以一次做完；第二步是本条的主体工作量，需要先出映射表。客户端还有 `World.Spatial.cs`（空间索引）与 `World.Recording.cs`（录像）两个 Launcher 没有的 partial：前者 Launcher 用 `UnitLookup` 里的自建索引顶替，是否要按客户端拆出来随第二步一起定；后者属不做范围。

### 56. 寻路纹理掩码有两套模型，一套在 L1 解析、一套在 Core

- **客户端**：`PathingTextureMask`/`PathingOverlay` 在 `Src/Core/` 只有一份，WPM/TGA 解析与 footprint 解算共用同一个类型。
- **Launcher**：`GameCore/L1_Adapter/Formats/War3Pathing.cs:54-73` 定义 `PathingTextureBits`（`byte` 枚举）、`PathingTextureMask`（`record`，`PathingTextureBits[] Cells`）、`PathingOverlay`（`record`，9 个参数，带 `UseBuildingRotation`/`SourceIsDead`）；`Src/Core/Pathing/Search/PathingTextureMasks.cs:8-34` 又定义了一遍 `PathingTextureBits`（成员完全相同）、`PathingTextureMask`（`class`，`byte[] Cells`）、`PathingOverlay`（`record`，6 个参数）。可破坏物 footprint（任务3）走后者，WPM 解析走前者，同名不同物，`dots`/`doo` 的快照字段只在 L1 那份上有。
- **玩家可见**：暂时看不出来，但两边字段不一致（`UseBuildingRotation`、`SourceIsDead`、`OcclusionHeight` 的有无），一处加了字段另一处不知道，摆放旋转与死亡后 footprint 的行为会分叉。
- **要做**：留一份、以 L1 那份为准（字段全），`Src/Core` 的消费方改成引用它；搬的时候注意 `Cells` 从 `byte[]` 变 `PathingTextureBits[]`，读到的地方要做转换。

### 60. 树以外的可破坏物没有运行时，桥和箱子点不动也打不掉

- **客户端**：可破坏物是统一对象（`JassGameNatives.DestructableRef`：`Life`、`Invulnerable`、`Removed`、`DeathProcessed`、`TargType`），`DoodadRenderer` 与 `IssuePlayerAttackDestructable` 都按这个模型走，树只是 `targType` 里带 `tree` 的一个子集。
- **Launcher**：只有树有状态（`Src/World/TreeState.cs`：Hp/Alive/CreationNumber/Node），由 `MapWorldBuilder.BuildDoodads` 单独建；其余 doodad 是纯静态 `Node3D`，`DoodadPlacement` 只留了 `Life` 百分比字节（`GameCore/L3_Simulation/Simulation/Maps/War3MapDoodads.cs:14-24`），没有 HP、死亡序列和掉落集，`_picking` 里也只有 `PickTree`，没有通用 `PickDestructable`。
- **玩家可见**：任务 31 补齐后树能砍了，但桥、木箱、木桶这些东西右键还是走到跟前站住，打不掉也挡路；依赖拆桥开门的地图过不去。
- **要做**：把 `TreeState` 提升成通用 `DestructableState`（HP/死亡动画/掉落/`targType`），`PickTree` 扩成 `PickDestructable`，`StepAttackTree` 与 `World.AttackTree` 换成可破坏物版本；树保留 lumber 专用的额外通道（`Tree`/`BuildingNow` 那条）。

---

## 推进顺序

模块顺序是从底层到表层，模块内从上往下做。

现在最容易被玩家一眼看出的是 **第 29/31/32 条（键鼠语义）**、**第 37 条（加载屏分带）**、**第 41 条（Esc 菜单在跑）**。改动最小的几条是 9、25、29、34、45。

## 收尾规则

- 一条做完就从本文删掉，序号不复用，后面的不重排；新任务从 51 起。
- 写进来必须带三样东西：客户端在哪、Launcher 在哪、玩家看得到什么。只有「不一样」没有证据的，等复核清楚再写。
- 平台差异与「仅对战」的已知取舍不写：Godot 渲染与 OpenAL/GL 的差别、Launcher 自有调试工具（`Cheats.cs`、`CombatProbe*`、`Diagnostics/`）、Android 触控、JASS/战役/存档/录像、固定 gmap 路径。
