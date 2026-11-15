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

### 23. 地面物品凭空出现，没有 Birth/Open 入场

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Scenes/War3GameScene/Presentation.cs:1545-1579` 物品首次出现按 `SpawnAge < birthSeconds` 播 `Birth/Open`，之后转 `Stand`；离场按 `DepartAge` 缩小。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/Units.cs:281-297` `CreateItemNode` 只实例化、设位置缩放，没有任何 `Play`；离场在 `Presentation.cs:564-608`。
- **玩家可见**：宝箱、符文、掉落装备直接出现（依赖模型 autoplay，多数是 Stand 首帧）；击杀野怪掉宝的瞬间观感差别明显。
- **要做**：物品节点创建后按 `SpawnAge` 播一次 `Birth/Open`。

- **执行结论（Launcher3，已修，待合并）**：
    - 入场序列按客户端顺序取第一个存在的：`Birth` → `Open` → `Birth Sequence`（`Presentation.cs:505`）。实查条目模型动画库：`PotofGold`/`Tome`/`TreasureChest`/`Rune` 都只有 `Birth`，宝箱类没有 `Open`；长度 1.00–1.07 秒。
    - 播放方式与单位侧统一：先关 `AnimationTree`，用 `AnimationPlayer` 播 `Birth`，按 `GetAnimation().Length` 计时；到点切 `Stand Alternate` → `Stand` → `Stand - 1` 并把 `LoopMode` 设成 `Linear`（`Presentation.cs:537`）。这条和 `UnitSceneComponent.Create` 里 `tree.Active = false` + 手动 `Player.Play` 的口径一致，不再让状态机决定物品播什么。
    - 接线：`Units.cs:294` 的 `CreateItemNode` 建完节点即调 `BeginGroundItemBirth`；`Lifecycle.cs:755` 每帧 `TickGroundItemBirths`。
    - 打断：`BeginGroundItemDeparture` 先 `CancelGroundItemBirth`（`Presentation.cs:633`），避免物品刚落地就被捡走时，入场计时把已经交给 `Death` 的节点再切回 Stand。
    - 模型里没有入场序列时直接进 Stand（`Presentation.cs:512`），与客户端 `FirstSequence` 取不到就退 `standSequence` 一致。
    - 验证：临时在地图加载完放 3 件物品（Crown of Kings / Spell Book / Tome of Power）跑 headless，日志为 `出生 序列=Birth 时长=1.00 / 1.00 / 1.07`，约 1 秒后 `序列=Stand - 1 循环=Linear`，无脚本报错；探针代码已移除。注意 `(4)Turtle Rock.gmap` 本身没有预摆物品，玩家能看到的是野怪掉宝与宝箱开出那一下。
    - 提交：`43e2990 任务23：地面物品按 Birth/Open 入场，播完转 Stand`。

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

> **执行结论（Launcher9，已提交，待合并）**
>
> `War3CameraComponent.Update` 的平移输入现在只剩方向键与小键盘：`Game/Camera/War3CameraComponent.cs:34-37` 是 `Up/Down/Left/Right` 加 `Kp8/Kp2/Kp4/Kp6`，与客户端 `War3CameraComponent.cs:81-91` 的取键集合一致。`W/A/S/D` 四个字母分支全部删除，字母键只走命令卡热键（`War3InputComponent.cs:99-102` 的 `HandleCameraFollowHotkey → TrySelectionHotkey → TryInventoryHotkey`）。
>
> 只为字母键存在的屏蔽机制一并清掉：`_panBlockedKey` 字段、`ReleaseCommandKey`、`PanKeyAvailable`（原 `Commands.cs:20,253-261`），以及 `QuickCast.cs:509-513` 在按下 Move/Stop/Hold/AttackMove/Patrol 时给 `_panBlockedKey` 赋值的那段；`War3InputComponent.HandleKey` 的按键抬起分支简化成 `if (!keyEvent.Pressed) return;`。全仓已无这三处引用，编译 0 错误。
>
> 验证口径：`Key.W / Key.A / Key.S / Key.D` 在 `Game/Camera/`、`Game/Input/` 下只剩 `War3InputComponent.cs:124` 的 Alt+A 小地图快捷键，镜头侧不再读字母键。屏幕边缘滚屏、中键拖动、Insert/Delete 旋转、四条跟随驱动的代码未动。

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

---

## 推进顺序

模块顺序是从底层到表层，模块内从上往下做。

现在最容易被玩家一眼看出的是 **第 29/31/32 条（键鼠语义）**、**第 37 条（加载屏分带）**、**第 41 条（Esc 菜单在跑）**。改动最小的几条是 9、25、29、34、45。

## 收尾规则

- 一条做完就从本文删掉，序号不复用，后面的不重排；新任务从 51 起。
- 写进来必须带三样东西：客户端在哪、Launcher 在哪、玩家看得到什么。只有「不一样」没有证据的，等复核清楚再写。
- 平台差异与「仅对战」的已知取舍不写：Godot 渲染与 OpenAL/GL 的差别、Launcher 自有调试工具（`Cheats.cs`、`CombatProbe*`、`Diagnostics/`）、Android 触控、JASS/战役/存档/录像、固定 gmap 路径。
