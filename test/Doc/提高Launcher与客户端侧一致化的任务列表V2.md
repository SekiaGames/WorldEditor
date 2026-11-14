# 提高 Launcher 与客户端侧一致化的任务列表 V2

这份清单只记真正会造成流程、画面或行为差异的地方。判定标准沿用 AGENTS.md：

1. 客户端和 Launcher 做同一件事，但执行流程不一样。
2. 客户端和 Launcher 看到的效果不一样，尤其是动画、特效、加载和镜头。
3. 客户端已经有现成实现，Launcher 却另写了一套，导致后续很难保持同步。
4. Launcher 可以共用代码的情况下没有共用。
5. Launcher 的 C# 文件在有对应客户端侧 C# 文件时，相对路径、文件名、类名不一致。

**本次是整体重排**：V2 编号 1..N 按「底层 → 表层」重新编排，标题后的 `（V1 #xx）` 是上一份文档的原编号，便于追溯；本文之后的新任务从 **33** 起，做完一条删一条，序号不复用。

## 范围

- 起点是地图选择界面。客户端首屏（公告页、主菜单、菜单音乐）、地图文件夹浏览、地图导入、战役列表不在本轮范围内，Launcher 固定从 `test/map_gmap` 读地图。
- Launcher 不跑 JASS 与触发器，只有脚本能触发的效果不进清单。
- 默认环境：macOS，`RunLauncher.command` 启动；地图用 `(4)Turtle Rock.gmap`、`(2)BootyBay.gmap`。

---

## 1. 技能美术仍走旁路文本表，没并进物编合并（V1 #51）

- **客户端**：`War3ObjectData.LoadStandardTypedData` 把 `Units\*AbilityFunc.txt` / `AbilityStrings.txt` 与 `AbilityData.slk` 一起并进同一份 `War3TypeData`（`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Configuration/War3ObjectData.cs:291`、`:327`、`:403`），技能和 Buff 共用一套字段查询。
- **Launcher**：`AbilityArt` 自己把 8 张 `Units*AbilityFunc` 生成表合并成一层旁路字典（`Launcher/GameCore/L4_Composition/Game/Presentation/AbilityArt.cs:9`、`:28`），`Ability` 再通过 `AbilityArt.Arts(...)` 取值（`Launcher/Src/Abilities/Ability.cs:438`）。
- **表现差异**：地图物编对技能美术字段的覆盖不进入统一物编，自定义/地图技能的美术字段与客户端可能对不上；同一字段在 Launcher 有两套来源，后续改一个技能要在两处同步。
- **要做**：把技能美术字段并进 `War3TypeData` 的物编合并，`AbilityArt` 退化成按字段名取值。

## 2. w3i 玩家/势力/环境记录的名字和结构与客户端不一致（V1 #53）

- **客户端**：`MapInfoParser` 里叫 `MapPlayerInfo` / `MapForceInfo`（`Open-Warcraft3_Wiki/GameCore/L2_Core/MapBrowser/MapInfoParser.cs:208`、`:218`），雾、天气、水色、光照 tileset 等环境字段直接平铺在 `MapInfo` 上（`MapInfoParser.cs:39-42`、`:88`）。
- **Launcher**：叫 `W3iPlayer` / `W3iForce`，环境另包一层 `W3iEnvironment`，`MapInfo.Environment` 暴露给所有消费方（`Launcher/GameCore/L2_Core/MapBrowser/MapInfoParser.cs:41`、`:63`、`:105`），`War3FogConfig` 和 `WeatherSystem` 都按 `map.Info.Environment.X` 取值。
- **表现差异**：玩家看不到直接差异，但任何按客户端路径移植的 w3i 字段都要多绕一层，字段默认值/缺失时容易取错。
- **要做**：两个记录改成客户端名字；环境字段平铺进 `MapInfo`，去掉 `W3iEnvironment`，调用点同步。

## 3. `EfctID` 这条艺术链没接（V1 #49）

- **客户端**：`War3EffectArtResolver` 把物编 `EfctID`（`X***`）解析成 `Effects\*.mdl` 并挂到单位/技能上（`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Configuration/War3EffectArtResolver.cs`；`RuntimeClassicSummonAndArmorAbilities.cs` 也沿这条链取）。
- **Launcher**：通用美术解析只读 `CasterArt` / `TargetArt` / `SpecialArt` / `EffectArt` / `AreaEffectArt` 五个字段（`Launcher/Src/Abilities/Ability.cs:438-446`）；`EfctID` 只有火雨一处特判读取（`Launcher/GameCore/L3_Simulation/Simulation/Configuration/RuntimeRainOfFireAbility.cs:48`）。Extract 也没有导出 `Effects/` 目录（`Extract/assets` 下只有 `mesh` / `fallback` / `shaders`）。
- **表现差异**：物编里挂 `EfctID` 的常驻/施法特效不会出现，玩家只能看到其它五个字段的美术。
- **要做**：Extract 先导出 `Effects/` 目录，再在 `AbilityArt` 补 `EfctID` 的通用解析。

## 4. 两条技能美术资源 Extract 没导出（V1 #50）

- **客户端**：技能美术由资源管理器按 `Abilities\...` 路径直接解析，这两个模型原版存在。
- **Launcher**：本轮实跑 `Godot --headless --path Launcher -- --effect-probe`：物编 976 条、美术引用 656、可加载 654、缺 2，缺的仍是：
  - `Arsp` 的 `Abilities\Spells\Other\Stampede\MissileDeath.mdl`
  - `AIga` 的 `Abilities\Spells\NightElf\NatureTouch\NatureTouchTarget.mdl`
- **表现差异**：践踏的落地死亡特效和自然之触的目标特效缺失。
- **要做**：Extract 补导出这两个模型，复跑 `--effect-probe` 应变成「缺 0」。

## 5. 地形与悬崖不接收 MDX 动态光（V1 #47）

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Shaders/Terrain.vert.glsl:25-35`、`Cliff.vert.glsl:13` 都有 `uDynamicLights` / `dynamicLightFactor`，把火把、法术特效的动态光按距离衰减累加进顶点光照。
- **Launcher**：`Extract/assets/shaders/terrain_map.gdshader:9-11`、`cliff_map.gdshader` 只吃 `war3_scene_direction/color/ambient`，没有 `war3_light_data`；动态光只进了模型着色器（`Extract/assets/shaders/war3_model.gdshaderinc:30`）。
- **表现差异**：单位脚下的地面和悬崖不会被火光/技能光照亮，火把、献祭、火焰雨只有模型亮、地面不亮。
- **要做**：把 `war3_light_data` 也传进地形、悬崖、水面三个世界着色器，按客户端公式累加。

## 6. MDX 粒子不吃大气雾（V1 #52，部分修复）

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Shaders/MdxParticle2.frag.glsl` 按 `useFog` 把 `uFogColor/uFogParams` 传进粒子，丝带同样。
- **Launcher**：天气已经改用 `weather.gdshaderinc` 并调 `war3_apply_fog`（`Extract/assets/shaders/weather.gdshaderinc:42`），丝带也已接上（`war3_ribbon.gdshader:23`、`war3_ribbon_add.gdshader:23`）；但 `war3_particle.gdshader` 没有包含 `atmosphere.gdshaderinc`，`fragment()` 里没有雾项（`Extract/assets/shaders/war3_particle.gdshader:69-91`）。
- **表现差异**：远处的技能粒子/环境粒子在浓雾图里保持原色，和地形、模型、丝带的雾感不一致。
- **要做**：`war3_particle.gdshader` 引入 `atmosphere.gdshaderinc`，片元里按视深调 `war3_apply_fog`。

## 7. 天气粒子还是每格一个 CpuParticles3D，跨格重启整窗雨（V1 #56）

- **客户端**：一次 `DrawElementsInstanced` 画整个窗格（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Environment/WeatherRenderer.cs:328`），相位由 `CellSeed(weather,col,row)` 确定性生成（`:350`），跨格只 `RebuildVisibleWindow` 重传实例缓冲（`:368`），并让散布平面随 `angx/angy` 倾斜。
- **Launcher**：`_cellsPerSide × _cellsPerSide` 个 `CpuParticles3D`（`Launcher/GameCore/L4_Composition/Game/Environment/WeatherRenderer.cs:69-94`），`AcquireWindow` 跨格时 `emitter.Restart()`（`:199`），`Preprocess = LifeSpan` 再跑一遍；发射域是水平 `Box`（`:85-87`），不随风向倾斜。
- **表现差异**：镜头跨格滑动时整窗雨/雪会跳一次相位；斜风雨的落点分布和客户端不同。
- **要做**：改成实例化 billboard 走 `weather.gdshaderinc`，相位按 cell 索引确定性生成，跨格只重建实例缓冲；散布按 `angx/angy` 倾斜；几何继续用方片，不按 `taillen` 拉伸。

## 8. 雾纹理参数搭着光照刷新才下发，进图后有一段时间没雾（V1 #57）

- **客户端**：`FogOfWarRenderer.Draw` 每帧算完淡变就 `FogOfWarUniforms.Publish(...)`；六个接收者在各自 draw 里 `Upload(shader)`，纹理更新同一帧生效。
- **Launcher**：`FogOfWar.Upload()` 只调 `SceneFog.SetFogOfWar(...)` 存静态字段（`Launcher/Src/Core/FogOfWar.cs:185`），真正写材质在 `SceneLightRig.Apply` 里的 `SceneFog.Write`（`SceneLighting.cs:297`），而 `Apply` 只在 `LoadTerrainPresentation` 调一次（`Lifecycle.cs:294`），之后靠时辰跳变触发；`_fog.Build()` 要到 `LoadPathing` 才跑（`Lifecycle.cs:381-386`）。
- **表现差异**：进图后地图先全亮，等下一次时辰跳变雾才突然铺上去。
- **要做**：`FogOfWar.Upload()` 写完静态字段后立刻把 `fog_of_war_texture/uvp/enable` 重写进已创建的材质，不搭光照刷新的车。

## 9. 转成 Godot 动画时丢掉了关键帧切线（V1 #60）

- **客户端**：`MdxTimelineSampling` 按序列区间求值，支持 step / linear / Hermite / Bezier，贝塞尔取 inTan/outTan 做缓入缓出。
- **Launcher**：Extract 写动画库时把所有 transform/value 轨道固定成 `interp = 1`（线性）（`Extract/src/Core/Tools/ModelBundle.cs:2403`），切线信息在 `MdxModel` 里解析出来后没有进入动画资源。
- **表现差异**：起手、收招、建筑生长、死亡倒地这些带缓入缓出的动作全部变成匀速，动作发飘、缺少重量感。
- **要做**：Extract 导出时把切线表达成 Godot 能承载的曲线（`Transform3D` 轨道只能是线性，需要改成可自定义插值的轨道形式，或按切线重采样成更密的线性关键帧）。

## 10. 单位转身比客户端慢五倍多（V1 #55）

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Configuration/UnitFactory.TypeData.cs:139-140` 把 SLK 的 `turnRate` 当弧度/秒，`TurnRate = TurnSpeed(turnRate) / Period`，`Period = 0.03`（`Open-Warcraft3_Wiki/Src/Core/Pathing/NativeMirror/Combat/NativeTimedFacing.cs:7`），步兵约 20 rad/s。
- **Launcher**：`Launcher/GameCore/L3_Simulation/Simulation/Configuration/UnitFactory.cs:357-358` 是 `Mathf.Clamp(turnRate * 6f, 1.5f, 14f)`，`Launcher/Src/World/World.MovementEvents.cs:155` 每仿真步最多转 `Mathf.Max(1.5f, TurnSpeed) * 0.05`；同一条 `turnRate` 只走出客户端五分之一到六分之一，1.5 的下限还会把慢速单位一起抬快。
- **表现差异**：点攻击后单位跑到贴脸才开始转身，侧身追人/绕圈像滑行；攻击前摇被拉长，编队齐转像整体平移。
- **要做**：`TurnSpeed` 按 `turnRate / 0.03` 换算并去掉 1.5 下限，上限对齐客户端量级；改完回归 `--combat-probe`、追击贴脸、编队转向、攻击前摇四项。

## 11. 坡面姿态在客户端是场景 partial 方法，Launcher 抽成了独立类（V1 #59）

- **客户端**：`ComputeUnitTerrainTilt` 挂在 `War3GameScene` 的 partial class 上（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Scenes/War3GameScene/Presentation.cs:489`），同目录诊断也直接调它。
- **Launcher**：写成 `Launcher/GameCore/L4_Composition/Game/Rendering/UnitTerrainTilt.cs` 的独立静态类，算法逐行一致但归属与目录都不对。
- **表现差异**：玩家看不到差别，但任何按客户端路径找坡面姿态的改动都会落空，后续场景层改动容易漏改这份独立实现。
- **要做**：搬回 `War3GameScene` 的 partial（可放 `Scenes/War3GameScene/Presentation.cs` 同目录新 partial），调用点经场景转调；如果拿场景引用不方便，至少放到 `Scenes/War3GameScene/` 下并在类名上体现归属。

## 12. 建筑在雾里被拆掉后仍然立刻消失（V1 #54）

- **客户端**：把「已探索但看不见」的建筑冻成 CPU 快照，被拆掉时快照还在，直到 `ScoutingMemory` 把这一格重新看见才遗忘（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Rendering/ScoutingMemory.cs`）。
- **Launcher**：只冻结可见性（`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/Environment.cs:91-95` 对建筑调 `SetFogFrozen(true)`，`Launcher/GameCore/L4_Composition/Game/Presentation/UnitSceneComponent.cs:121-137` 暂停序列、关粒子），建筑阵亡后仍按单位移除流程释放模型，没有任何快照/遗忘层（全仓无 `ScoutingMemory`）。
- **表现差异**：玩家离开后基地被拆，再探开那一格时建筑直接消失，看不到「最后看到的废墟」；客户端会保留定格直到重新看见。
- **要做**：建筑在不可见时阵亡保留最后一次看到的定格模型，重新可见时再按 `ScoutingMemory` 口径清掉。

## 13. 树和装饰物死亡瞬间隐藏，没有倒地/死亡姿态

- **客户端**：`DoodadRenderer` 启动时保留 `life <= 0` 的实例并记录死亡计数（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Environment/DoodadRenderer.cs:342-355`），死亡/被砍时解析 `death` 序列（`:106-108`、`:684-692`），非循环序列播到末帧；看不到时按 `TreeKnowledge` 记忆保留。
- **Launcher**：加载时直接跳过 `Dead` 和 `Hidden` 的装饰物（`Launcher/GameCore/L4_Composition/Game/Environment/DoodadRenderer.cs:30-39`）；树被砍倒时 `TreeState.Chop` 只做 `Node.Visible = false`（`Launcher/Src/World/TreeState.cs:17-21`），采伐 `ConsumeTree` 同样瞬间隐藏（`Launcher/Src/World/World.UnitLookup.cs:46-52`）。
- **表现差异**：树被砍倒没有倒地/枯木过渡，模型一帧消失；地图开局就已毁坏的装饰物完全不显示，而客户端保留死亡姿态。
- **要做**：装饰物实例补 `death` 序列播放与定格，死亡后按可见性记忆保留；初始已毁的实例也要建出来并播死亡姿态。

## 14. Buff 类技能只消耗部分物编字段，护甲/回复/缩放/Buff 美术都没生效

- **客户端**：以 Roar 为例读取 `DataA`（攻击）、`DataB`（护甲）、`DataC`（回血）、`DataD`（回蓝）、`DataG`（目标上限）、`BuffID`（Buff 美术/名称）和 `HeroDur`（`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Configuration/RuntimeRoarAbility.cs:27-48`）；Bloodlust 还读 `DataC`（模型缩放）与 `BuffID`（`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Configuration/RuntimeBloodlustAbility.cs:24-47`），并生成对应 Buff 对象数据。
- **Launcher**：`RuntimeRoarAbility` 只有 `DamagePct => Data('A')`，施法时给一个只带攻击力的 `StatBuff`（`Launcher/GameCore/L3_Simulation/Simulation/Configuration/RuntimeRoarAbility.cs:10`、`:20`）；`RuntimeBloodlustAbility` 只有 `DataA/DataB`，同样用 `StatBuff`（`Launcher/GameCore/L3_Simulation/Simulation/Configuration/RuntimeBloodlustAbility.cs:10-11`、`:25-29`）。没有读 `BuffID`，也不绑 Buff 物编，所以没有 Buff 美术、没有护甲/回复、没有模型放大、英雄持续时间也与普通单位共用。
- **表现差异**：咆哮只加攻击力，不给护甲和回血回蓝；嗜血不会把模型放大；两种增益在单位身上都没有对应的 Buff 图标/特效；英雄吃到的持续时间与客户端不同。
- **要做**：按客户端补 `DataB/C/D/G`、`HeroDur`、`BuffID` 与目标规则；Buff 实例绑到 `BuffData`，让 `Buff` 的美术与名称走物编。

## 15. 地面物品靠走近自动吸走，客户端要求右键下单

- **客户端**：拾取是显式订单：`IssuePlayerPickupItem`（`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Jass/JassGameNatives.cs:800-819`）由右键/交互层下发（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Input/War3WorldInteractionComponent.cs:268`）；接受半径读 `Misc.PickupItemRange`（默认 250，`Open-Warcraft3_Wiki/Src/Configuration/War3GameplayConfig.cs:274`），单位走到物品旁再执行拾取。
- **Launcher**：每 tick 由 `TickItemPickup` 扫描所有地面物品，任何 `CanCarryItems` 且有空格、距离小于硬编码 `PickupRange = 150f` 的单位都会被直接塞进背包（`Launcher/Src/World/World.Items.cs:14`、`:282-303`、`:351-367`）；右键物品只是下一条「走到物品位置」的移动单（`Launcher/GameCore/L4_Composition/Game/Input/War3WorldInteractionComponent.cs:323-333`），真正的拾取仍由走近触发。
- **表现差异**：单位路过就自动吸走物品，甚至可以抢走敌方单位附近的掉落；客户端不右键就不会捡，且拾取距离由 `PickupItemRange` 决定。玩家会看到掉落物莫名其妙进背包。
- **要做**：去掉按距离自动拾取，改成明确的拾取订单；半径读 `Misc.PickupItemRange`，通过寻路走到物品旁再结算。

## 16. Powerup 物品（经验书/属性书/符文）不会自动使用

- **客户端**：`powerup` 字段为 1 的物品，拾取时走自动使用管线（`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Jass/JassGameNatives.OrdersAndItems.cs:409-411`、`:517-561`），执行子技能后消耗（`:993-1020`；`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Jass/JassGameNatives.ItemUseRuntime.cs:47-57`）。
- **Launcher**：`ItemType.PowerUp` 只在 UI 里显示「一次性」（`Launcher/GameCore/L3_Simulation/Simulation/Configuration/ItemCatalog.cs:28`、`:104`；`Launcher/GameCore/L4_Composition/Game/UI/MeleeHudController.cs:171`），没有运行时消费方；拾取后作为普通物品进背包（`Launcher/Src/Units/Unit.Items.cs:57` 的 `AddItem`），`ItemUseRules` 也没有 powerup 分支。
- **表现差异**：捡到经验书/属性书会占一个背包格且无法使用，符文也不会在踩上去时立即生效；客户端是拾取即用、不占格。
- **要做**：读取物编 `powerup`，在拾取路径上先执行物品技能再消耗，不进入背包；补齐 `PowerupSound`/拾取事件。

## 17. 地面物品没有影子，Powerup 没有金色光圈，离场也没有缩小

- **客户端**：地面物品统一用 `ReplaceableTextures\Shadows\Shadow.blp` 投影，尺寸取模型包围盒，普通物品 alpha 0.45、Powerup 用金色 0.75（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Scenes/War3GameScene/Presentation.cs:2121-2160`）；`Departing` 的物品（离场缩小）不投影。
- **Launcher**：`CreateItemNode` 只加载模型、设缩放（`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/Units.cs:281-294`），没有物品投影，也没有 Powerup 光圈/离场状态。
- **表现差异**：地上物品“浮”在地表，没有影子；Powerup 与普通物品视觉上无法区分；物品被拾取/消失是瞬间消失，没有缩小过渡。
- **要做**：补地面物品阴影与 Powerup 金色光圈；给物品加离场缩小状态并在离场期间停止投影。

## 18. 粒子参数还是待机帧快照，数量夹在 512（V1 #61）

- **客户端**：`ParticleEmitter2` 每帧按当前动画帧求 `KP2S`、`KP2L`、`KP2G`、`KP2E`、`KP2W/KP2N`、`KP2V`，运行时逐实例推进（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Rendering/MdxParticle2Renderer.cs:249-277`）。
- **Launcher**：Extract 侧把发射器烘成 `GPUParticles3D`，速度/纬度/重力/宽/长/可见度取待机帧快照（`Extract/src/Core/Tools/ModelBundle.cs:1559-1566`）；`amount` 已用整段最大发射率计算（`:1562`、`:1855`），但仍硬夹到 8..512，运行时不再随动画帧更新。
- **表现差异**：发射率、速度、重力随动画变化的特效（呼吸、爆发、蓄力）全程按待机参数出粒子；粒子多的特效被削到 512。
- **要做**：把上述轨道按片段导出，运行时按 `AnimationPlayer` 当前位置驱动 `ParticleProcessMaterial` 参数；`amount` 按整段最大发射率给，去掉 512 上限或按客户端上限。

## 19. 飞弹模型几乎不可见（V1 #63）

- **客户端**：弹道用 `CharacterRenderer` 画模型，箭、火球、投石在游戏里都看得很清楚。
- **Launcher**：`OnMissileFired` 实例化模型、设位置、加进 `_projectiles`（`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/Presentation.cs:33-79`）。Turtle Rock 实测弓箭手的 `earc` 弹道节点存在、位置在相机视野中心、`Visible=true`，模型放大 4 倍和 12 倍后截图里仍没有箭；场景结构 `Node3D → Model/Skeleton3D → geoset0`，材质 `war3_model_opaque`（`alpha_cutoff 0.75`、贴图 `ArrowMissile.png`）完整，问题在渲染侧而不是导出。
- **表现差异**：远程单位对射时看不到箭/火球/投石，只有伤害数字和命中音。
- **要做**：先确认是骨骼/Stand 动画把网格挪走，还是投射物节点/材质在运行时被改写；修好后用弓箭手对射复验箭、火球、投石三类飞弹。

## 20. 音乐无缝连播，也没有脚步和建筑循环音（V1 #25）

- **客户端**：曲间有 20 秒间隔（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Audio/War3MusicPlayer.cs:3`），支持主题音乐叠加、淡入淡出、`SetMapMusic/PlayMusic/StopMusic`、三层音量；`War3LoopingSoundService` 按 `MovementSoundLabel` 播脚步并跟随位置，建筑建造时播 `BuildingSoundLabel`（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Audio/War3LoopingSoundService.cs:7`）；`War3AudioSystem` 24 个系统声部、同文件最多 3 个、按 channel 配额/优先级抢占，每条声音用自己的 Min/MaxDistance。
- **Launcher**：`War3MusicPlayer` 播完立刻下一首，固定 `-10dB`，没有间隔、淡入淡出和脚本接口（`Launcher/GameCore/L4_Composition/Game/Audio/War3MusicPlayer.cs:14`、`:26`、`:69-74`）；没有循环音服务；`War3AudioSystem` 只有 16 个 3D + 8 个 2D 固定播放器，`UnitSize = 900`、`MaxDistance = 4500` 写死（`War3AudioSystem.cs:12-42`），表里的 Min/MaxDistance 没有使用；没有音量设置；客户端加载 11 张声音表（ack/combat/anim/ui/ambience/ability/dialog/midi/environment/EAX/portrait，`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Audio/War3SoundTables.cs:26-44`），Launcher 只有 5 张（`Launcher/GameCore/L4_Composition/Game/Audio/War3SoundTables.cs:44-48`）。
- **表现差异**：音乐一直接一首、没有呼吸感；行军完全没有脚步声，建筑建造没有循环施工音；远处/近处声音衰减一致，声部抢占规则不同。
- **要做**：曲间间隔与淡入淡出、脚步/建造循环音；声部按 channel 配额和优先级；音量设置与第 22 条的选项界面一起做。

## 21. 城镇被攻击、英雄阵亡这几类事件音还没接（V1 #62）

- **客户端**：`War3UiSoundService` 有 `TownAttack`、`UnderAttack`、`HeroDies`、`AllyHeroDies`，由模拟层事件源驱动（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Audio/War3UiSoundService.cs:65-67`；`MeleeHudController` 里按事件派发）。
- **Launcher**：冷却表和种族前缀已经建好（`Launcher/GameCore/L4_Composition/Game/Audio/War3UiSoundService.cs:10-41`），但上述四个键没有任何调用点（全仓只有定义和生成文本）；建筑被攻击不报警，英雄阵亡也不出声。
- **表现差异**：混战里少了「基地被打」和「英雄阵亡」的听觉提示。
- **要做**：在 `World` 的建筑受伤路径上区分城镇与其他建筑，派 `TownAttack/UnderAttack`；在死亡路径上区分英雄、自己与盟友，派 `HeroDies/AllyHeroDies`。

## 22. Esc 语义、顶部按钮和选项界面（V1 #40，部分修复）

- **客户端**：Esc 分层取消（取消待定命令 → 关菜单；2026-10-22 起不再开菜单），菜单开关是 F10（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Scenes/War3GameScene/Input.cs:1349-1351`）；Backspace 循环主城（`:1306`）、PageUp/Down 缩放（`:1340`）、Ctrl+C/Alt+C 居中、Alt+F 阵型、Alt+G/T/A 小地图；Esc 菜单用 FDF `EscMenuMainPanel`；`AboutDialogFrame` 有镜头距离、平移灵敏度、音乐/音效音量、帧率上限、渲染倍率（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/AboutDialogFrame.cs`）。
- **Launcher**：Esc 现在会先取消施法、再关菜单、最后开菜单（`Launcher/GameCore/L4_Composition/Game/Input/War3InputComponent.cs:41-47`），但放置建筑时 `_placingId` 不在取消分支里，Esc 会弹菜单且幽灵建筑还在；没有 F10/Backspace/PageUp/PageDown/Ctrl+C/Alt+F，Alt+G/T/A 也没有调用者。`TopBarFrame` 暴露了 `MenuButton/QuestButton/AlliesButton`（`TopBarFrame.cs:31`），但没有任何 `.Clicked +=`，顶部按钮可点没反应；Esc 菜单和胜负面板是 `ColorRect + Label + Button` 手搓，坐标写死在 1600×900；没有选项界面。
- **表现差异**：放置时按 Esc 关不掉幽灵建筑；想开菜单只能按 Esc；编队/镜头快捷键缺失；顶部菜单/任务/盟友按钮点了没反应；没有任何画质/音量/镜头设置。
- **要做**：把放置建筑也纳入「取消优先」，补 F10；顶部菜单按钮接 Esc 菜单（任务/盟友按范围隐藏）；选项界面和音量一起做。

## 23. 命令卡的槽位、热键和可用性判定（V1 #38）

- **客户端**：`FindNativeSlot` 是「向 11 找空位、再往 0 回退、满卡就丢」（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/CommandCardBuilder.cs:426`）；热键按按钮真正显示的 `Hotkey` 分发（`:90` 的 `CommandAtHotkey`），训练/研究/建造/升级都能用键盘；可用性走 `NativeManaAdmission` / `NativeSpellOrderAdmission` / `NativeSelectionCommandPolicy`。
- **Launcher**：`TrySelectionHotkey` 完全不看命令卡，只认 `ability.Hotkey` 和硬编码的 M/S/H/A/P（`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/QuickCast.cs:411-417`、`:449-457`），遇到建筑直接返回 false；槽位算法是「从请求位往后找第一个空位，找不到就 break」；可用性判定写死在 `QuickCast.cs` 里。
- **表现差异**：同一份物编，两边第 3/8/11 格可能放不同技能；命令卡上的热键字母是装饰，建筑训练/研究不能用键盘；带法力修正符的单位会出现「亮着却放不出」。
- **要做**：先把热键改成读按钮 `Hotkey`，再改槽位算法和可用性判定；冷却闪光、自动施法高亮、禁用变暗在第 31 条。

## 24. 法术书没有页面，子技能进不了命令卡（V1 #64）

- **客户端**：法术书是命令卡的一个页面：`OpenSpellBook(selected, book)` / `BackFromSpellBook()`（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/CommandCardBuilder.cs:59-64`），`CollectAbilityCommands` 在 `spellBook != null` 时只收 `ContainerAbility` 与这本书同源的子技能（`CommandCardBuilder.Abilities.cs:25-26`），子技能序号当格号落在 0..10、第 12 格放「返回」；书的主人死亡、书被移除或子技能不可用时自动 `_menus.Back()`。
- **Launcher**：`Ability` 只有 `ContainerRawcode` / `ContainerAbility` 两个字段，全仓没有一处赋值（`Launcher/Src/Abilities/Ability.cs:714-715`），也没有 `RuntimeSpellBookAbility`；物编里的法术书物品（`sbok` / `Aspb`）用出去什么都不发生。
- **表现差异**：拿到法术书点使用没有任何页面打开，书里的子技能永远进不了命令卡，物品等于失效。
- **要做**：补 `RuntimeSpellBookAbility`（物编 MenuOrderId + 子技能 `Requires` 链），在 `CommandCardBuilder` 的菜单栈里加 `MenuKind.Book` 与 `OpenSpellBook` / `BackFromSpellBook`，子技能按容器过滤并把序号当格号，点开/关页条件与客户端一致。

## 25. 提示语和悬停说明石沉大海（V1 #39）

- **客户端**：`CommandErrorFrame` 黄字、在 console 上方、3 秒 + 每字符 0.1 秒；`SetHint` 等提示都有真实读者；信息面板悬停按帧名分派（金币/木材/人口/维修费、攻击力/护甲、三围、经验条、8 个 Buff 图标）；`TooltipFrame` 有 `ShowEx(ext/costs)`、四个成本图标槽、分段富文本、跟随锚点、屏幕边缘内缩、单位血蓝条（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/TooltipFrame.cs` 1227 行）。
- **Launcher**：`MeleeHudController.SetHint` 只做 `_hint = text`（`MeleeHudController.cs:150`），`_hint` 没有读者；`SetQueue(string)`、`SetClock(string)` 是空方法（`:72`、`:139`），`SetPlayerInfo`、`SetBuffs`、`SetUnitSummary` 只写字段没人读（`:74`、`:141`、`:147`）。全仓没有 `CommandErrorFrame`；`TooltipFrame.cs` 只有 112 行，固定 280 宽纯色底 + 按 34 字符硬折行。
- **表现差异**：资源不足点建造、修理由非农民点残血建筑、Ctrl+1 存编队、子组切换、放置失败等 70 多处调用，一句提示都看不到；悬停资源/数值/Buff 没有说明；长文本用固定宽度硬折行。
- **要做**：先补客户端的 `CommandErrorFrame`（不要造等价物，就用这个名字和这套行为），再补信息面板悬停，最后升级 `TooltipFrame`；Buff 图标本身缺失另见第 28 条。

## 26. 聊天和控制组各写了一套（V1 #41）

- **客户端**：`ChatFrame` 是 HUD 里的帧，行会淡出、自动换行、解析 `|c` 颜色、有 `AppendHistory`（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/ChatFrame.cs`）；屏幕左下有 1–9 编队按钮 `ControlGroupFrame`（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/ControlGroupFrame.cs`），支持双击居中。
- **Launcher**：`ChatFrame` 同名不同物，是独立 `CanvasLayer` 里的 `Label + LineEdit`，固定 `224,560 / 620×110`，不随 UI 缩放、不淡出、不换行（`Launcher/GameCore/L4_Composition/Game/UI/ChatFrame.cs:23-28`）；没有 `ControlGroupFrame`，只有键盘 0–9。
- **表现差异**：聊天窗口和 HUD 不同步缩放、超长消息被裁；看不到编队按钮，也不能双击居中。
- **要做**：聊天挪回 HUD 帧体系并补换行/淡出；补 1–9 编队按钮。

## 27. 小地图少了图标，滤镜和快捷键没接全（V1 #42，部分修复）

- **客户端**：`MinimapUnitView.Classify` 认金矿、缠绕金矿、闹鬼金矿、中立建筑、圈地、物品、野怪营地、出生点（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/MinimapUnitView.cs:58`）；野怪营地还有 `MinimapCampTracker`（按迷雾和等级画两种图标，`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Scenes/War3GameScene/Ui.cs:1338`）；`MinimapFilter` 和 `Alt+G/T/A` 都接到真实状态；腐地用 `MinimapBlightTexture` 缓存（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Rendering/MinimapBlightTexture.cs:10`）。
- **Launcher**：`Classify` 只认 4 个金矿 id（`Launcher/GameCore/L4_Composition/Game/UI/MinimapUnitView.cs:43-54`），`SimulationId` 恒为 0，`ColorIndex = unit.Player`；`_minimapIcons` 声明后从未 `Add`（`Scenes/War3GameScene/Ui.cs:199`、`Lifecycle.cs:520-521`），`DrawMinimapCamp` 直接 `return false`（`GodotUiRenderDevice.cs:157`）。`HandleAltShortcut` 没有调用者（`MinimapFrame.cs:190`），`MinimapFilter` 只赋值没人读（`Ui.cs:203`）；阵型按钮只改自己的开合状态，不改变移动阵型。腐地已经改用 `MinimapBlightTexture` 缓存（`Ui.cs:186-242`），这部分已修复。
- **表现差异**：小地图没有野怪营地图标（野怪只按单位点显示）、没有物品和出生点图标，中立建筑只按玩家色画方块而没有中立图标；Alt+G/T/A 没反应；因为没有营地图标，野怪过滤没有实际对象；阵型按钮不改变移动阵型。
- **要做**：补全 `Classify` 与营地图标；把 `HandleAltShortcut` 接到输入、把阵型开关接到移动阵型；配色/野怪按钮现有的颜色与显隐逻辑保留；腐地保持现状。

## 28. 单位信息面板没有 Buff 图标栏

- **客户端**：`UnitInfoPanelFrame` 有 8 个 `BuffIcon0..7`（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/UnitInfoPanelFrame.cs:41-43`、`:126-128`），`UpdateBuffIcons` 按选中单位的 Buff 填图标、剩余时间和光环/增益标记（`:534-573`），悬停显示 `Bufftip/BuffUbertip`。
- **Launcher**：`UnitInfoPanelFrame` 只有名字/经验/攻击/护甲/三围/状态标签（`Launcher/GameCore/L4_Composition/Game/UI/UnitInfoPanelFrame.cs:24-39`），没有 Buff 控件；`MeleeHudController.SetBuffs` 只把列表存进 `_buffs`（`MeleeHudController.cs:141-145`），`GameHudScreen` 没有任何读取方。
- **表现差异**：选中单位后看不到身上的增益/减益图标和剩余时间，也看不到悬停说明；玩家无法判断减速、嗜血、光环还剩多久。
- **要做**：在 `UnitInfoPanelFrame` 补 8 个 Buff 图标槽与剩余时间/光环标记，数据从 `SetBuffs` 接入；悬停说明与第 25 条一起做。

## 29. 头像只循环 Stand，没有 Portrait/Talk 动作

- **客户端**：`PortraitAnimator` 待机时每段 `Portrait` 序列播完重摇下一段（不循环同一段、不定格），收到单位应答时切 `Portrait+TALK`，播到语音时长结束再回待机（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/PortraitAnimator.cs:3-7`、`:127-138`）；换单位/换类型会重建头像并重摇种子（`:61-79`）。
- **Launcher**：`PortraitViewport.Show` 只加载选中单位模型并 `Play("Stand")`（`Launcher/GameCore/L4_Composition/Game/Rendering/PortraitViewport.cs:51-70`），没有 Portrait 序列选择、没有待机轮换、没有应答时说话动画；`GameHudScreen` 选中变化时只调 `Show(selected)`（`Launcher/GameCore/L4_Composition/Game/UI/GameHudScreen.cs:205-214`）。
- **表现差异**：头像永远循环同一个 Stand，站立动作整齐、缺少原版的手势/说话表现；点单位听到应答时头像嘴不动。
- **要做**：按客户端补 `PortraitAnimator`（Portrait/Portrait+TALK、待机重摇、按身份重播种），在 ack 时触发 Talk，时长取语音实际长度。

## 30. 界面拉满整窗，超宽屏不居中（V1 #43）

- **客户端**：把 `PixelOffsetX` 和 `ActivePixelWidth` 传给渲染设备，`War3UiViewport.FitPixelWidth` 把 HUD 限制在居中 16:9（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Rendering/War3UiViewport.cs:24`、`Scenes/War3GameScene/Rendering.cs:46`）。`UiFontScale` 读用户设置并在倍率变化时 `InvalidateLayout()`。
- **Launcher**：`GameHudScreen._Draw` 用 `viewport.X / DesignWidth`、`viewport.Y / DesignHeight` 直接拉伸（`Launcher/GameCore/L4_Composition/Game/UI/GameHudScreen.cs:515-535`），没有 `FitPixelWidth`/`PixelOffsetX`；`UiFontScale` 默认 1.2，但全仓没有一处 `Set`（`Launcher/GameCore/L2_Core/UI/UiFontScale.cs:18`），依赖它的选项界面也不存在。
- **表现差异**：2560×1080 下 HUD 被横向拉宽三成，小地图变扁、命令卡格变宽、圆形按钮变椭圆；字号无法调。
- **要做**：HUD 改用 `War3UiViewport`（选图页、加载屏已经在用），删掉重复常量；字号接选项设置。

## 31. 界面渲染能力缺几项，FDF 生成的绑定没人用（V1 #44）

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/UiRenderDevice.cs:367` 的 `DrawCooldownSweep` 用 `cooldown.blp` 画扇形，`DrawCooldownCompletion` / `DrawAutoCast` / `DrawTexturedQuadAdd` 都有真实实现（`:382`、`:389`、`:496`，含 ADD 混合）。`War3FdfUi` 真的用 `ConsoleUiBinding`、`ResourceBarBinding`、`UpperButtonBarBinding`、`EscMenuMainPanelBinding` 切换 `_console.Visible` 和 `SetChromeVisible`（`War3FdfUi.cs:11-56`）。
- **Launcher**：`DrawCooldownSweep` 是纯色扇形（`GodotUiRenderDevice.cs:132`），`DrawCooldownCompletion` / `DrawAutoCast` 返回 false（`:155`、`:159`），`DrawTexturedQuadAdd` 退化成普通 alpha（`:98`）；`FdfBindGen` 生成了 5 个绑定（`Launcher/GameCore/L4_Composition/Game/Diagnostics/FdfBindGen.cs:17-44`），但只有 `LoadingScreenBinding` 有调用者（`Launcher/GameCore/L4_Composition/Game/Rendering/War3LoadingScreenRenderer.cs:95`），HUD 走手写 `TopBarFrame + ConsoleSkinFrame`。
- **表现差异**：冷却遮罩没有原版质感、冷却结束不闪、自动施法没有旋转光环、ADD 高亮/光晕偏暗；FDF 皮肤改了也不会反映到 HUD。
- **要做**：先补三个渲染能力和 ADD 混合，再决定 FDF 绑定是接上还是删掉。

## 32. `SceneFog` 该叫 `FogOfWarUniforms`，而且不该管大气雾（V1 #58）

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Rendering/FogOfWarUniforms.cs` 只管战争迷雾的 `Texture/Uvp/Enabled` 加 `Publish/Reset/Upload`；大气雾由各渲染器按 `FogSettings` 自己传 `uFogColor/uFogParams`。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Environment/SceneFog.cs` 同时装大气雾 `fog_color/fog_params` 和战争迷雾 `fog_of_war_*`，`SceneLightRig.Apply` 一次写两样（`Launcher/GameCore/L4_Composition/Game/Environment/SceneLighting.cs:297`）；路径、类名都和客户端对不上。
- **表现差异**：玩家看到的雾暂时一致，但两类雾的生命周期被绑在一起（第 8 条的延迟就是它造成的），后续任一渲染器改雾参数都会牵动全局。
- **要做**：拆开。战争迷雾改叫 `FogOfWarUniforms` 并挪到 `Game/Rendering/`，只留雾纹理/UV/开关；大气雾留在 `War3FogConfig`，由地形、悬崖、水面、模型各自在材质创建处传参，`SceneLightRig.Apply` 只负责光照。

---

## 核对结果（V1 剩余 25 条）

| V1 # | 状态 | 关键证据 |
|---|---|---|
| 25 | 仍成立 | 音乐无间隔/淡变（`War3MusicPlayer.cs:26`），无循环音服务，16+8 固定播放器（`War3AudioSystem.cs:12-42`） |
| 38 | 仍成立 | 无 `CommandAtHotkey`/`FindNativeSlot`；`QuickCast.cs:411-417` 只认物编热键 |
| 39 | 仍成立 | `SetHint` 只赋值（`MeleeHudController.cs:150`），无 `CommandErrorFrame`；Buff 图标另立第 28 条 |
| 40 | 部分修复 | Esc 已先取消施法/关菜单（`War3InputComponent.cs:41-47`）；放置取消、F10、快捷键、顶栏、选项仍缺 |
| 41 | 仍成立 | 聊天独立 CanvasLayer（`ChatFrame.cs:23-28`），无 `ControlGroupFrame` |
| 42 | 部分修复 | 腐地已用缓存；`Classify` 只认金矿（`MinimapUnitView.cs:43`），Alt 快捷键无调用者，阵型滤镜未接 |
| 43 | 仍成立 | HUD 直接拉伸（`GameHudScreen.cs:515-535`），`UiFontScale` 无 `Set` |
| 44 | 仍成立 | `DrawCooldownCompletion/DrawAutoCast` 返回 false（`GodotUiRenderDevice.cs:155-159`），FDF 绑定只有加载屏在用 |
| 47 | 仍成立 | 地形/悬崖着色器无 `uDynamicLights`（`terrain_map.gdshader:9-11`） |
| 49 | 仍成立 | 只有火雨特判读 `EfctID`（`RuntimeRainOfFireAbility.cs:48`），Extract 无 `Effects/` 导出 |
| 50 | 仍成立 | 实跑 `--effect-probe`：976 条、656 引用、654 可加载、缺 2 |
| 51 | 仍成立 | `AbilityArt.cs:9-56` 单独合并 8 张文本表 |
| 52 | 部分修复 | 天气/丝带已吃雾；`war3_particle.gdshader` 仍无雾 |
| 53 | 仍成立 | `W3iEnvironment` 独立一层（`MapInfoParser.cs:41/105`） |
| 54 | 仍成立 | 只有可见性冻结（`Environment.cs:91-95`），无 `ScoutingMemory` |
| 55 | 仍成立 | `turnRate * 6` 且 1.5–14 夹紧（`UnitFactory.cs:357-358`） |
| 56 | 仍成立 | 每格 `CpuParticles3D` + 跨格 `Restart`（`WeatherRenderer.cs:69-94`、`:199`） |
| 57 | 仍成立 | `Upload` 只存静态字段（`FogOfWar.cs:185`），材质写入在 `SceneLightRig.Apply` |
| 58 | 仍成立 | `SceneFog` 同时管两类雾（`SceneFog.cs:10-41`） |
| 59 | 仍成立 | 独立 `Rendering/UnitTerrainTilt.cs`，客户端是场景 partial（`Presentation.cs:489`） |
| 60 | 仍成立 | 动画轨道固定 `interp = 1`（`ModelBundle.cs:2403`） |
| 61 | 仍成立（修正） | 参数取待机帧快照；`amount` 已用最大发射率但仍夹 8..512（`ModelBundle.cs:1559-1566`、`:1855`） |
| 62 | 仍成立 | 四个事件音键无调用点（`War3UiSoundService.cs:10-41`） |
| 63 | 仍成立 | 弹道仍是实例化后直线/弧线飞行（`Presentation.cs:33-79`），根因未定位 |
| 64 | 仍成立 | `ContainerAbility` 无赋值（`Ability.cs:714-715`），无 `RuntimeSpellBookAbility`/`OpenSpellBook` |

---

## 已排除的候选（不写进清单）

| 内容 | 排除理由 |
|---|---|
| 任务/盟友面板、排行榜、多面板、脚本对话框、过场遮罩、秘籍面板 | 全部来自触发器/JASS 数据，Launcher 不跑 JASS |
| 电影模式隐藏 HUD、`AnimationTimeScale` | 只由 JASS 触发，Launcher 永远不会遇到 |
| 地形高度变形 `TerrainDeformCrater` | 只有脚本会调，等接 JASS 再一起做 |
| Android 自动拾取指令、返回上一个选择、镜头锁定/遥杆 | 客户端安卓触控套件专属，Launcher 没有触控套件 |
| 多人聊天频道、共享单位控制、联机大厅 | Launcher 固定单人+AI，不做多人 |
| F4–F7 选英雄 | 单人最多 3 个英雄，永远用不到 |
| 非英雄背包 / 物品合成 | 客户端 C# 没有对应实现，不在对齐范围 |
| AI 的战术选择 | “仅对战”的已知取舍；Launcher 的 AI 不是客户端 melee AI 的移植对象 |
| CAMS 模型自带镜头、UberSplat 建筑光晕 | 1.27 数据/客户端运行时没有可对齐的实现 |

---

## 推进顺序

编号已经是自底向上的顺序，从上往下做即可。两个例外：

1. **第 22、23、25 条**是「按了没反应/看不到提示」的功能性缺陷，可以提前插队。
2. **第 5、6、7、8 条**共用环境/雾/天气代码，建议连着做，避免来回改同一批着色器与加载时序。

底层里最容易被玩家一眼看出的是 **第 10 条（转身）**、**第 14 条（Buff 技能）**、**第 15–17 条（物品）**；特效与声音里 **第 19 条（飞弹）** 和 **第 20 条（音乐/脚步）** 最直观。

## 收尾规则

- 一条做完就从本文删掉，序号不复用，后面的不重排；下一条新任务从 **33** 开始。
- 新发现的差异先判断是不是平台差异或已知取舍：Godot 与原生 GL 的差别、Launcher 固定 gmap 路径、不跑 JASS/触发器/战役/存档/录像/联机，这些不写进来。
- 写进来时必须带三个东西：客户端在哪、Launcher 在哪、玩家看得到什么。只有「不一样」没有证据的，等复核清楚再写。