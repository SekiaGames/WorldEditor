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

---

## 模块二 寻路与移动

> 走位、碰撞、可达性。玩家感知最强的一层差异在这里。

### 51. 寻路搜索仍是自研 32px A*，没有客户端的 footprint 接触解算与粗/细/出口搜索

- **客户端**：`Open-Warcraft3_Wiki/Src/Core/Pathing/NativeMirror/` 用路径纹理 footprint 做接触解算、放置掩码，搜索分粗搜、细搜与出口搜索；`CProximityMap` 逐格记录对象占用与关系。
- **Launcher**：`Launcher/Src/Core/Pathing/Search/PathGrid.cs` 的 `FindPath` 仍是 `CellWorld = 32f` 布尔网格上的 A*；网格只含 WPM 与可破坏物静态 footprint（`DoodadPathingOverlays.cs`、`PathingTextureMasks.cs`），单位移动不写回网格。
- **玩家可见**：单位能否挤过建筑与树林之间的缝、绕建筑拐角的走法、被围住时能否蹭出去，仍与客户端不同；窄口会出现客户端能过而 Launcher 判不可达，或反向卡位。
- **要做**：接入客户端 footprint 接触解算与粗/细/出口搜索，单位移动写回网格；验收口径：窄口与围堵场景下，Launcher 与客户端逐格可达性一致。

## 模块三 地形与环境

> 地形、悬崖、水面、迷雾、天气、阴影。整屏可见，改动集中在渲染数据与 shader。

## 模块四 单位、技能与物品

> 模拟层行为。这一层决定对局打得对不对。

### 15. 工地从 1 点血起造，任何一次攻击都能一击拆掉

- **客户端**：`Open-Warcraft3_Wiki/Src/Abilities/ConstructionProgress.cs:37,45,62` 工地初始血量是 `MaxLife × startLifeFraction`（默认 0.1），随进度从 10% 涨到 100%。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/Production.cs:102` `site.Hp = 1f`；`Src/World/World.MechanismMembers.cs:175` 每帧用 `MaxHp * progress` 覆盖，进度 0 时是 1 点。
- **玩家可见**：刚放下的农场、兵营只有 1 点血，一个农民或一只野怪摸一下就塌；血量条是空的，客户端是一小截。
- **要做**：起始血量改 `MaxHp * 0.1f`，进度换算同 `ConstructionProgress`。

> **执行结论（Launcher5，已修复，待合并）**：
> - **改了三处**：`Src/Units/Unit.cs:113-114` 补 `BuildLifeRatio` / `BuildLifeLoss` 两个字段承载工地生命进度；`Src/World/World.MechanismMembers.cs:176` 的 `site.Hp = MaxHp * progress` 换成 `AdvanceConstructionLife`，完工分支不再 `Hp = MaxHp` 硬回满；`GameCore/L4_Composition/Game/Scenes/War3GameScene/Production.cs:99-103` 的 `site.Hp = 1f` 改成 `MaxHp × 0.1`。
> - **顺带删掉 `site.MaxHp = type.Hp`**：`Spawn` 里 `RefreshUpgradeStats` 已经按科技算好上限，这行把它覆盖回基础值；删掉后工地上限与客户端 `MaxLife` 同口径，非英雄建筑无科技时数值不变。
> - **换算口径**：上限 `MaxHp × (0.1 + 0.9 × 进度)`，血量 = 上限 − 累计损失，进度推进时保住挨过的伤害；进度回落（`SetProgress` 反向）按上限下压，取 `Clamp(…, 1, MaxHp)`，与 `AdvanceConstructionLife` 的 `Min/Max` 处理一致。
> - **为什么不用累加**：照抄客户端 `Life += MaxLife×(1-f)×Δratio` 跑出来完工是 `899.998/900`，浮点残留让 `Hp >= MaxHp` 永远不成立，修理与 AI 自动修理会一直把这栋建筑当可修目标。改成「上限 − 损失」后完工上限精确等于 `MaxHp`，实测 8 种建筑全是 `.000`。
> - **验证**：`--headless --play=test/map_gmap/(4)Turtle Rock.gmap --build-test --time=0.15`，开工日志 农场 50/500、兽族地洞 60/600、国王祭坛 90/900、兵营 150/1500（全为 10%）；完工日志 500.000/500、900.000/900、1500.000/1500，无一残留。`Production.cs` 开工日志与 `TickConstruction` 完工日志各补一行血量，方便下一次对着日志核对。
> - **玩家侧验证**：`RunLauncher` 进任意 melee 图，让农民放一个农场，血条应为十分之一截且空着九成；派一个步兵砍它，不再一击即塌，血量随工期一格格涨。
> - **未覆盖**：修理工地只加血不加进度（客户端修理工地是推进建造进度），属于修理通道的问题，本条没动。

### 16. 合建按人数线性 1/2/3 倍，兽族与暗夜建造者不会进建筑

- **客户端**：`Open-Warcraft3_Wiki/Src/Abilities/ConstructionProgress.cs:85-88` 多人速度 `1 + (人数-1) × 0.6`（1 / 1.6 / 2.2 倍），`:217` 每多一人追加 15% 造价；`:66,233-250` 兽族、暗夜把建造者 `StoreWorkerInside`（隐藏、暂停、无敌、不可选中），完工或取消再从出口放出。
- **Launcher**：`Launcher/Src/World/World.MechanismMembers.cs:166-170` `rate = Min(builders, 3)` 直接乘到剩余工期上，不追加费用；建造者全程站在工地边上播 `Stand Work`，不进入建筑。
- **玩家可见**：三个人族农民一起敲，工期是单人 1/3（客户端 1/2.2）且少付两次 15% 资源；兽族地洞、暗夜古树开工后苦工精灵应当消失进建筑，Launcher 里它们站在外面挨打，能被直接点掉。
- **要做**：速度改 `1 + (n-1)*0.6`，补追加造价与 `StoreWorkerInside`。

### 17. 运输船拒载英雄，还要求乘客与船同 Player

- **客户端**：`Open-Warcraft3_Wiki/Src/Units/CargoTransport.cs:44-57` `CanAccept` 只排除建筑、已在别的货舱、满 10 格、成本超容量、非同盟，不排除英雄。
- **Launcher**：`Launcher/Src/Units/CargoTransport.cs:27` `passenger.IsBuilding || passenger.IsHero` 直接拒载，`:29` 还额外要求 `passenger.Player == holder.Player`。
- **玩家可见**：右键点自己的飞艇装英雄，什么都不会发生，飞艇空着飞走；英雄坐飞艇偷矿、逃命这套标准打法不存在。
- **要做**：去掉 `IsHero` 判定，同队判定改成 `AreAllied`。

### 18. 进入魔免不清洗身上的魔法 Buff，驱散也只看正负极性

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Configuration/RuntimeMagicImmunityAbility.cs:21,28-39` 进入魔免时 `RemoveImmunityEntryBuffs` 移除正负两性的魔法 Buff（光环与限时生命除外）；`Src/Skills/AvatarBuff.cs:12-14` 天神下凡声明 `Magic=false, AutoDispel=false`；驱散侧按 `buff.Magic && buff.AutoDispel` 过滤。
- **Launcher**：`Launcher/Src/Abilities/Buff.cs:220-233` 天神下凡只加属性并 `State.Add(MagicImmune)`，没有 Buff 清洗，全仓也没有这个函数；`Src/World/World.BuffMembers.cs:33` 驱散只按 `IsHarmful` 一刀切；`AvatarBuff` 用的是基类默认 `Magic=true/AutoDispel=true`。
- **玩家可见**：中了妖术、减速、诅咒再开天神下凡，山丘之王带着一身 debuff 变魔免（客户端当场洗掉）；牧师的驱散魔法、毁灭者的吞噬魔法能把敌方天神下凡整个扒掉（客户端驱不动）。
- **要做**：补 `RemoveImmunityEntryBuffs`，Buff 加 `Magic/AutoDispel` 标记，驱散改按标记判定。

### 19. 暴击不用递进阈值，闪避按来源各掷一次

- **客户端**：`Open-Warcraft3_Wiki/Src/Skills/CriticalStrike.cs:56,71` 用 `NativeProcThreshold(percent, _procAttempts)`，不暴击递增、暴击后重置；`Src/Components/Health.cs:42-50` 闪避取所有来源的最大值只掷一次（`Src/Skills/Evasion.cs` 注释写明「取最大可用来源，只掷一次，无 PRD」）。
- **Launcher**：`Launcher/Src/Skills/CriticalStrike.cs:31` 每次独立 `RollPercent(Chance)`，抄过来的 `NativeProcThreshold` 与系数表从未被调用；`Src/Skills/Evasion.cs:22` 每个实例各掷一次，另有独立的 `BonusEvasionPct` 一路（`Src/World/World.DamageMembers.cs:226`）。
- **玩家可见**：客户端几乎不出现「连砍 12 刀不暴击」，Launcher 会出现；闪避护符 +15% 配 3 级闪避 30%，Launcher 实际闪避率约 40.5%，客户端恒为 30%，带两个闪避来源的单位明显更肉。
- **要做**：暴击接 `NativeProcThreshold`；闪避合成「取最大、掷一次」。

### 20. 攻击后不接 Stand Ready，每次挥砍都从第 0 帧重来

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Scenes/War3GameScene/Animation.cs:113-126` 起手 `animation.Set("Attack", NoAnimationTags, true, preservePrimary: true)` 并 `Queue("Stand", ReadyTag, false)`，攻击结束后队列回到 `Stand Ready`。
- **Launcher**：`Launcher/Src/Units/Unit.Combat.cs:23-31` `View?.Play("Attack", ..., restart: true)`，每次挥砍重置；`Src/World/World.cs:288-306` 射程内直接 `BeginAttack(...); return;`，起手与后摇期间没有任何 `Play`，也没有队列。
- **玩家可见**：攻击间隔大于片段长度时单位停在末帧举着武器定格，片段长于冷却时被硬切成第 0 帧；近战成排站桩时这一帧差很明显。
- **要做**：攻击指令改为「不重启 + 排队 Stand Ready」，结束后按是否仍有目标在 `Stand` 与 `Stand Ready` 之间切换。

### 21. 变形不播 Morph 序列，直接销毁重建模型

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Scenes/War3GameScene/Animation.cs:210-251` 对变形技能置 `FinishMorphAnimation`，用 `Advance(dt, holdAtEnd: true)` 把 `Morph` 播到末帧再切形态。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/Units.cs:478-515` `OnUnitMorphed` 直接 `unit.View.Root.QueueFree()` 后按新类型重建节点，再补一个 `PolyMorphDoneGround` 特效。
- **玩家可见**：德鲁伊变熊、石像鬼变毁灭者、剑圣分身都是模型瞬间替换加一团烟，没有原版的 Morph 过渡帧，过渡期间高度、光环、队伍色会跳。
- **要做**：`UnitSceneComponent.SelectSequence` 已支持 `MORPH` 主标签，变形路径改为先播完再重建。

### 22. 主基地升级播的是 Birth，不是 Birth Upgrade First/Second

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Scenes/War3GameScene/Animation.cs:306-320` 用 `production.Percent / 100f` 加 `ResolveUpgradeAnimationProperties(目标类型)` 读 `animProps`（`htow→hkee` 得 `upgrade,first`），命中 `Birth Upgrade First`。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/Animation.cs:54-66` 只按进度取 `SetUpgradeProgress(progress)`；`Game/Presentation/UnitSceneComponent.cs:798-802` 的 `UpgradeBirth()` 传 `Array.Empty<string>()`，主标签只有 `Birth`。
- **玩家可见**：主基地、主堡升级时播基础 Birth，观感是「重新建造一次」；部分模型的基础 Birth 与 Upgrade 变体长度不同，进度条和模型生长对不齐。
- **要做**：把目标类型的 `animProps` 传到 `SetUpgradeProgress`，`Seek` 前合并副标签。

### 23. 地面物品凭空出现，没有 Birth/Open 入场

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Scenes/War3GameScene/Presentation.cs:1545-1579` 物品首次出现按 `SpawnAge < birthSeconds` 播 `Birth/Open`，之后转 `Stand`；离场按 `DepartAge` 缩小。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/Units.cs:281-297` `CreateItemNode` 只实例化、设位置缩放，没有任何 `Play`；离场在 `Presentation.cs:564-608`。
- **玩家可见**：宝箱、符文、掉落装备直接出现（依赖模型 autoplay，多数是 Stand 首帧）；击杀野怪掉宝的瞬间观感差别明显。
- **要做**：物品节点创建后按 `SpawnAge` 播一次 `Birth/Open`。

### 24. 队伍色索引钳到 0..15，17–24 号玩家全部同色

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Rendering/CharacterRenderer.cs:2245,2294,2306` 全部 `Clamp(teamColorIndex, 0, 27)`，`ReplaceableId 1/2`（TeamColor/TeamGlow）分开解析。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Rendering/TeamColorTextures.cs:10-11` `Clamp(index, 0, 15)` 且只加载 `TeamColor00..15`；`Game/Presentation/UnitSceneComponent.cs:420-423` 的 TeamGlow 同样钳 0..15。
- **玩家可见**：16 人以上对局或自定义地图里，第 17–24 号玩家全部显示成 16 号色，小地图与选中圈跟着错。
- **要做**：上限改 27，补 24 张 TeamColor/TeamGlow。

---

## 模块五 特效与声音

> MDX 事件音、音频空间、声部管理。

> **复核评价**：模块名叫「特效与声音」，正文 25–28 四条全是音频，没有一条特效。上一版里那条「粒子参数是待机帧快照」并没有修好：`Extract/src/Core/Tools/ModelBundle.cs:1996-2001` 的速度、纬度、重力、宽、长、可见度仍走 `SnapshotValue(..., idle, ...)`，取的是待机帧单值，运行时也不随动画帧更新（Launcher 侧没有任何 `ParticleProcessMaterial` 驱动）。同一批里「512 上限」和「粒子吃雾」已经落地（`amount` 现在夹在 8..20000，`:2296`；`war3_particle.gdshader:4` 已引 `atmosphere.gdshaderinc`、`:91` 调 `war3_apply_fog`），只剩「参数随帧变化」这一半悬着。建议按收尾规则补一条 51 号，否则这一层等于被跳过了。

### 25. 缺 AnimSounds 表，单位死亡喊的是「Yes」

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Audio/War3SoundTables.cs:29-31` 把 `UnitAckSounds / UnitCombatSounds / AnimSounds` 合进同一张 ack 表，死亡与攻击音由 MDX 事件经 `AnimLookups → AnimSounds` 解析。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Audio/War3SoundTables.cs:44-48` 只 Load 5 张表，没有 `UISoundInfoAnimSounds`（生成的表其实存在：`Generated/Tables/UISoundInfoAnimSounds.g.cs`）；`War3UnitSoundset.cs:21-27` 用 `soundName + kind` 拼 label，`Scenes/War3GameScene/Presentation.cs:354-362` 死亡路径带 `fallback = "Yes"`。
- **玩家可见**：每个单位被击杀时喊一声「Yes / 遵命」，全场听不到死亡音，脚步与整理类动画音效一并丢失。
- **要做**：补一行 `LoadTable("UISoundInfoAnimSounds")`，死亡路径去掉 `Yes` 兜底。

### 26. DistanceCutoff 解析了但没用，半个地图外都能听见伐木声

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Audio/War3AudioSystem.cs:225-229,290-294` 用 `DistanceCutoff` 做「超出不播」的硬门，`maxDistance` 只作衰减终点。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Audio/War3SoundTables.cs:26,85` 解析了 `DistanceCutoff`，全仓再无读取；`War3UnitSound.cs:84-87` 只把 `MaxDistance` 传给 `AudioStreamPlayer3D.MaxDistance`。
- **玩家可见**：以伐木音为例（表值 Min 600 / Max 10000 / Cutoff 2100），客户端 2100 外听不到，Launcher 要到 10000 才静音，在自己基地能听到半个地图外的伐木、交战、施法。
- **要做**：`Start` 前按 `DistanceCutoff` 做一次距离门。

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

---

## 模块六 输入与镜头

> 键鼠语义、技能瞄准、拾取判定、镜头跟手。

### 29. W/A/S/D 被当镜头平移键，客户端只认方向键与小键盘

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Camera/War3CameraComponent.cs:81-91` 平移只读 `Left/Right/Up/Down` 与 `Keypad2/4/6/8`，字母键全部留给命令卡热键。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Camera/War3CameraComponent.cs:34-43` 把 `W/A/S/D` 并入平移，只在按下 M/P/H/A/S 进入待定指令时用一次性 `_panBlockedKey` 屏蔽（`Scenes/War3GameScene/Commands.cs:253-261,509-513`），按键抬起即解除。
- **玩家可见**：按住 W 放水元素、疾风步时镜头同时往北漂；用 A/S/D 下指令时镜头往左、下、右滑，同一点永远对不准。
- **要做**：删掉字母键分支。

### 30. 单位或点地双模技能，落点恒为施法者脚下

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Input/War3CommandInteractionComponent.cs:316-361` 对 `Unit / UnitOrPoint / Any` 先 `PickUnit` 取单位，取不到再用 `ScreenToTerrain` 取落点，技能据此算方向。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/QuickCast.cs:82-101` 只处理 `TargetMode.Unit` 与 `TargetMode.Point`，`point` 初值是 `caster.Position`，`UnitOrPoint`（`Src/Abilities/AbilityTargetMode.cs:10`）两不沾。受影响的有腐臭蜂群、震荡波、沉默，技能侧拿到零向量后回退成 `Owner.Facing`（`GameCore/L3_Simulation/Simulation/Configuration/RuntimeCarrionSwarmAbility.cs:59-63`）。
- **玩家可见**：腐臭蜂群、震荡波点哪里都无效，波形永远朝英雄当前朝向直喷；沉默的落点恒在自己身上。
- **要做**：补 `UnitOrPoint / Any / Destructable` 三条取目标分支。

### 31. 右键缺三类目标：可破坏物、吃树、传送门

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Input/War3WorldInteractionComponent.cs:809-885` 右键按序尝试 `EnterWaygate`、可破坏物采集、`Aeat` 吃树、`IssuePlayerAttackDestructable`、地面物品拾取，逐支只吃掉能干这活儿的单位。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Input/War3WorldInteractionComponent.cs:301-347` 只到伐木（限能砍木的工人）和地面物品，没有可破坏物攻击、没有吃树，全仓也没有 `EnterWaygate`。
- **玩家可见**：剑士、火枪右键点树或桥不会攻击它，而是绕过走到落点，清树开路和拆桥失效；山岭巨人右键树不会吃树；带传送门的地图右键无效。
- **要做**：按客户端顺序补齐这几支，目标类型不完备是第 30 条的同根问题。

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

### 38. 加载到 100% 后多出 0.55 秒不可操作窗口

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Hosting/GameApplication.cs:354-380` 用 `using` 管理加载屏，场景 `OnLoad` 同步跑完 13 个阶段，`Report(1f)` 后方法结束即 `Dispose`，下一帧就是可操作的游戏。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/Lifecycle.cs:146-161` 先 `await ProcessFrame` 让加载屏画出来，`War3LoadingScreenRenderer.cs:569-585` 再等 3 帧 settle 并做 0.5 秒 `Modulate` 淡出，期间 `LoadingBlocked` 为 true（`:521,713-715,780-794`），音乐也要等 `StartPendingMusic`。
- **玩家可见**：进度条满了还有半秒不能操作、没有音乐；这三帧 settle 本身说明世界刚建好的头几帧是黑的。
- **要做**：按客户端改成同步组装、硬切进游戏；如果保留淡出，需要在文档里登记为平台差异。

### 39. HUD 不走 FDF 帧树，坐标是手抄常量，4 个 FDF 绑定没有生成

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/War3FdfUi.cs:38-49` 从 `ConsoleUI.fdf / ResourceBar.fdf / UpperButtonBar.fdf / InventoryCover.fdf` 装载帧树，帧引用全部走 `Generated/*.g.cs` 强类型绑定；`MeleeHudController` 在帧树上挂内容。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/UI/GameHudScreen.cs:37,140-142` 只用 `FdfUiFactory` 读 `GlobalStrings.fdf` 的文案，帧体全部手搭；`Generated/` 下只有 `LoadingScreenBinding.g.cs`，没有 `ResourceBarBinding / ConsoleUiBinding / UpperButtonBarBinding / InventoryCoverBinding`。`FdfScreen` 这套 FDF 装载只被 `Platform/Godot/App/LauncherApp.cs:465-474` 的调试开关 `--ui-screen` 用到。
- **玩家可见**：资源条的图标尺寸、数字基线、四组间距由 `TopBarFrame.cs:147-167` 的常量决定，和 FDF 的右对齐贴边不同，越靠右越明显；FDF 皮肤下的按钮条与空背包遮罩不存在；维护费文案是中文「无/低/高维修费用」，客户端 FDF 路径是 `No/Low/Heavy Upkeep`。
- **要做**：补 `ResourceBarBinding` 等生成，HUD 改成「FDF 优先、手写兜底」，两边同一套锚点。

> **复核评价**：方向成立，证据有两处要修。一是落盘位置：绑定生成到 `GameCore/L4_Composition/Game/UI/Generated/`（`FdfBindGen.cs:56-58`），不是顶层 `Generated/`，那个目录里只有表数据。二是目标数量：`FdfBindGen.Targets` 现在是 6 个（`ConsoleUiBinding`、`ResourceBarBinding`、`UpperButtonBarBinding`、`LoadingScreenBinding`、`MainMenuBinding`、`EscMenuMainPanelBinding`），成品只有 `LoadingScreenBinding.g.cs` 一个。清单只点了 4 个，漏了 `EscMenuMainPanelBinding` 和 `MainMenuBinding`；而第 41 条要把 Esc 菜单换成 FDF 面板，正需要 `EscMenuMainPanelBinding`。这条把「补哪些绑定」写全，41 才有得依赖。

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

---

## 模块八 工程结构（横切）

> 命名、归属与共用。放最后，避免和功能改动互相踩。

### 47. 同一个类在 Launcher 有两份，命名空间按层分裂

- **客户端**：`Open-Warcraft3_Wiki/Src/Abilities/Buff.cs`、`Src/Units/Unit.cs`、`Src/Core/World.cs` 等 L2 层文件统一 `namespace OpenWC3`，技能实现统一在 `GameCore/L3_Simulation/Simulation/Configuration/` 下 `namespace OpenWC3.Game`，每个类只有一个定义。
- **Launcher**：`Launcher/Src/` 用 `namespace OpenWC3`，`Launcher/GameCore/L3_Simulation/` 用 `namespace OpenWC3.Game`，于是同名类并存：`RuntimeMeatDropAbility`（`Src/Skills/RuntimeMeatDropAbility.cs:7` 与 `GameCore/L3_Simulation/Simulation/Configuration/RuntimeMeatCargoAbilities.cs:127`）、`RuntimeMeatLoadAbility`、`RuntimeTrueshotAuraAbility`（`Src/Skills/` 与 `RuntimeCommandAuraAbility.cs:127`）、`RuntimeAntiMagicShellBuff`（`Src/Skills/` 与 `RuntimeAntiMagicShellAbility.cs:78`）。工厂只注册 `OpenWC3` 那一份（`Src/Abilities/RuntimeAbilityFactory.cs:60`）。
- **玩家可见**：暂时看不出来，但同一技能两套实现在跑不同的数值与流程，改一处不会同步另一处；接 AI 或事件时容易挂到没被注册的那一份上。
- **要做**：按客户端分层收敛成一层，删掉未注册的重复实现。

> **复核评价**：同名类并存成立（`RuntimeMeatDropAbility`、`RuntimeTrueshotAuraAbility`、`RuntimeAntiMagicShellBuff` 都是一份在 `Src/Skills/`、一份在 `GameCore/L3_Simulation/.../Configuration/`）。和第 48 条是同一件事的两半：本条管「重复的类」，48 管「文件怎么分」。建议合并成一条做，先去重再拆文件，否则拆完发现有一半要删。另外「删掉未注册的重复实现」要先确认工厂真的没注册——`RuntimeMeatCargoAbilities.cs:127` 那两份里有名字带 `s` 的聚合类，删之前按 `RuntimeAbilityFactory.Map` 逐条对一遍。

### 48. 技能工厂与技能实现文件的结构和客户端对不上

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Configuration/` 下 `RuntimeAbilityFactory` 拆成 `Constructors / Initialization / Initializers / Specialization` 四个 partial，实现按族分文件（约 194 个 `Runtime*Ability` 文件）。
- **Launcher**：`Launcher/Src/Abilities/RuntimeAbilityFactory.cs` 一个文件里 523 条 `["xxxx"] = () => new ...` 字典登记，实现只有 47 个 `Runtime*Ability` 文件，其余技能落在少数聚合类里；静态数据一侧叫 `AbilityCatalog/AbilityType`（`GameCore/L3_Simulation/Simulation/Configuration/AbilityCatalog.cs`），客户端叫 `ObjectDataAbility`（`Src/Abilities/ObjectDataAbility.cs`）。
- **玩家可见**：目前体现为部分技能退化成无行为的被动；更实际的影响是新增或修一个技能时，两边的登记口不在同一处，容易只改一边。
- **要做**：工厂按客户端的四个 partial 拆分，技能实现文件按族对齐命名，静态数据的类名改成客户端的名字。

> **复核评价**：523 条登记和客户端 194 个实现文件都对得上（`grep -oE '^\s*\["'` 数出 523；`Configuration/Runtime*Ability*.cs` 数出 194）。但「实现只有 47 个 `Runtime*Ability` 文件」只数了 `Src/Skills/`：`GameCore/L3_Simulation/Simulation/Configuration/` 下另有 131 个，两处合计 178，和客户端的 194 只差 16。所以「其余技能落在少数聚合类里」这个说法站不住，聚合类只占一小部分，真实情况是两棵并行的技能树（也正是第 47 条在说的事）。这条和第 47 条合并后再动手，数字也要按 131 + 47 重写。

### 49. 核心类的目录归属和客户端不一致

- **客户端**：`World` 在 `Src/Core/World.cs`（partial 也在 `Src/Core/`），`AbilityContainer` 在 `Src/Components/`。
- **Launcher**：`World` 及其 29 个文件（`World.cs` 加 28 个 partial）在 `Src/World/`，`AbilityContainer` 在 `Src/Abilities/`。
- **玩家可见**：无直接表现，但 AGENTS.md 第 5 条要求「相对路径、文件名、类名一致」，现在 grep 两侧同名类会落到不同目录，跨仓库对照检索的成本一直存在。
- **要做**：整目录搬迁到客户端的层级，一次做完，不要分批。

> **复核评价**：文件数对（`Src/World/` 共 31 个 .cs：`World.cs` + 28 个 partial + `TreeState.cs`、`UnitList.cs`），但「整目录搬迁」按现在的写法执行不下去。客户端 `Src/Core/` 里只有 13 个 World 文件（`World.cs` + 12 个 partial），Launcher 有 28 个 partial，其中只有 10 个同名（`DamageMembers`、`HeroSelectionOrder`、`MechanismMembers`、`MovementEvents`、`Rally`、`RelationMembers`、`RepulseMembers`、`Targeting`、`UiMembers`、`UnitLookup`）。剩下 18 个（`Abilities`、`Blight`、`BuffMembers`、`Channels`、`CorpseRevive`、`CreepSleep`、`DeathTriggers`、`FogRevealMembers`、`GroundSpells`、`GroupMovement`、`HeroRevive`、`Illusions`、`Items`、`Morph`、`Orders`、`PhoenixEgg`、`Repair`、`WeaponSystem`）在客户端根本没有同名文件，职责散在 `Src/Units/Unit.Items.cs`、`Src/Components/WeaponSystem.cs`、`OrderQueue.cs`、`AbilityContainer*.cs` 里。搬目录只能搬那 10 个，其余 18 个要先决定「并回 Unit/Component 还是留在 World」，这是设计决定，不是搬家。
>
> 建议把本条改成两步：先出一张 28 → 客户端归属的映射表（含「不搬」的理由），确认后再动文件。另外客户端有 `World.Spatial.cs`、`World.Recording.cs` 两个 Launcher 没有的 partial，前者可能是真缺（空间索引），`Recording` 属不做范围，顺带确认一下。

### 50. 缺 WorldFrameTextMetrics，聊天与文本通道各写死一套字号

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/WorldFrameTextMetrics.cs` 统一给出字号、行距、寿命、淡出等口径，聊天、信息通道、计时文本都引用它。
- **Launcher**：全仓没有这个类。`Game/UI/ChatFrame.cs:13-24` 自己写死 `ChatLeft=8 / ChatWidth=990.4 / ChatFont=16.64 / BaseLifetime=3 / PerCharacter=0.1 / Fade=3 / Capacity=30`，其他文本面板再各写一份。
- **玩家可见**：改了 UI 字号设置后，聊天与提示的字号不跟着变，和 HUD 其它文本不同步。
- **要做**：把度量抽成客户端同名类，各帧改引用。

---

## 推进顺序

模块顺序是从底层到表层，模块内从上往下做。

现在最容易被玩家一眼看出的是 **第 29/31/32 条（键鼠语义）**、**第 37 条（加载屏分带）**、**第 41 条（Esc 菜单在跑）**。改动最小的几条是 9、25、29、34、45。

## 收尾规则

- 一条做完就从本文删掉，序号不复用，后面的不重排；新任务从 51 起。
- 写进来必须带三样东西：客户端在哪、Launcher 在哪、玩家看得到什么。只有「不一样」没有证据的，等复核清楚再写。
- 平台差异与「仅对战」的已知取舍不写：Godot 渲染与 OpenAL/GL 的差别、Launcher 自有调试工具（`Cheats.cs`、`CombatProbe*`、`Diagnostics/`）、Android 触控、JASS/战役/存档/录像、固定 gmap 路径。
