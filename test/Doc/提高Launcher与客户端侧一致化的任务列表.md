# 提高 Launcher 与客户端侧一致化的任务列表

这份清单只记真正会造成流程、画面或行为差异的地方。判定标准就是 AGENTS.md 里那五条，末尾再加上两条执行规则：

**判定标准**

1. 客户端和 Launcher 做同一件事，但执行流程不一样。
2. 客户端和 Launcher 看到的效果不一样，尤其是动画、特效、加载和镜头。
3. 客户端已经有现成实现，Launcher 却另写了一套，导致后续很难保持同步。
4. Launcher 可以共用代码的情况下没有共用。
5. Launcher 的 C# 文件在有对应客户端侧 C# 文件时，相对路径、文件名、类名不一致。

**执行规则**

6. 做完一条就删掉它，序号不复用，新任务往后续。
7. 一切以客户端行为为准，不写「我们这样更好」的要求。

读完一条就动手，不要先全面铺开。每条都标了客户端在哪、Launcher 在哪，照着路径能直接翻到。

**排序方式：从底层往表层。** 先数据读取，再地形与地表，再水与天气，再光照大气与迷雾，再单位实体，再特效与声音，最后镜头输入、加载流程、界面，结构问题放在最后作为横切项。

**本次是整体重排，编号全部重排过一次；从那以后恢复「删掉不复用、新任务往后续」的规则。当前排到 48，下一条新任务从 49 起。**

## 范围

- 起点是地图选择界面。客户端首屏（公告页、主菜单、菜单音乐）、地图文件夹浏览、地图导入、战役列表都不在本轮范围内，Launcher 保持固定地图路径，地图只从 `test/map_gmap` 读。
- Launcher 不跑 JASS 与触发器，所以「只有脚本能触发」的效果不进清单，这一条在文末「已排除的候选」里有对照。

---

## 一、资源与数据

## 二、地形与地表

## 三、水与天气

## 9. 只有全局天气，跨一格整片雨雪重来

客户端给每个 `AddWeatherEffect` 建一个带 rect 的 `WeatherRenderer`，可以多天气叠加，粒子相位由 cell 索引确定性生成，只在相机跨 cell 时重建实例数据。

Launcher 只按 `map.Info?.Environment?.GlobalWeather` 建一次，`WeatherRenderer` 里的 `_hasArea` 分支永远 false；`baseCol/baseRow` 一变就对每个 cell `emitting Restart()`，镜头每跨 512 单位雨就断一下。另外 `var` 列在客户端是速度变化（`Speed*(1+Variation*rand)`），Launcher 用 `atan(Variance)` 喂给了 `Spread`，大雨变成扇形散开。

要做：区域天气接上（`AddWeatherEffect` 只有脚本会调，先按 C# 侧接口留好）；跨 cell 不要 Restart；`var` 按速度变化用。

---

## 四、光照与大气

## 11. 地形雾没人消费

客户端从 w3i 解析雾参数（`FogSettings`），地形、悬崖、水、岸浪、天气都把 `uFogColor` / `uFogParams` 传进着色器。

Launcher 的 `W3iInfo` 明明解析了 `FogHeightStart/End/Density/FogColorBgra`，全仓却没有一处读取；三个世界着色器都写死 `fog_disabled`，`SkyboxRenderer` 也没开 `FogEnabled`。

要做：把已经解析出来的字段接到环境雾或着色器上。

## 12. 天空没有太阳月亮，选天空的依据也不对

客户端单独加载 `Environment\Sky\Sky\skyLight.mdx`（日月就在这个模型里），按 `SunMoonAngle(timeOfDay)` 每 24 小时转一圈跟着相机走；天空用 `GameplayConfig.SkyboxType`（war3mapMisc 的 0..14 索引）选，`SetSkyModel` 可以覆盖。

Launcher 只加载天空壳（`SkyDomeRenderer`），不加载 `skyLight`（Extract 里其实有 `skyLight.tscn`）；天空按地形 tileset 字符猜，自定义天空和 war3mapMisc 都不生效。另外多了一层 `PanoramaSkyMaterial` 全景底，天空壳半透明处会露出亮带。

要做：加载 `skyLight` 并按时辰旋转；天空改按 `SkyboxType` 选；全景底只在完全没有天空壳时兜底。

---

## 五、战争迷雾

## 13. 战争迷雾是一块悬在 320 高的板子

客户端不画雾：`FogOfWarRenderer` 只维护一张 R8 可见性纹理，由地形、悬崖、单位、水、岸浪、天气各自在片元里采样，三态 255/128/0，`FogVisualBuffer` 每 30ms 逼近 9 级，视野进出是渐变的。

Launcher 把雾做成一张贴地网格（`FogHeight=320`），每个角点 `HeightAt+320`，用一张 RGBA 贴图盖上去：低角度看，前面的山脊会把雾挡住、后面反而亮着；悬崖处雾面折成斜面甚至竖幕；高过 320 的树冠和建筑从黑雾里穿出来。另外 `CellsPerTexel=2`、材质 `Linear`，边界比客户端糊一倍；两态直接写像素，没有淡入淡出；已探索区用深蓝叠 170/255，客户端是乘 50% 黑；水、岸浪、悬崖着色器里根本没有迷雾 uniform。

要做：把雾改成着色器采样，别用几何体；分辨率回到 128；补灰雾渐变和正确的混色。

## 14. 进雾之后的记忆：一个定格，一个只是暂停

客户端的建筑看不见但已探索时，`CharacterRenderer.FogMemory` 会抓一份 CPU 快照（模型矩阵、骨骼调色板、序列、帧、全局时间、队伍色），把粒子/丝带实例移除，用冻结时间重画，模型仍然吃迷雾所以是灰的；重新看到那块空地时 `ScoutingMemory` 才遗忘。装饰物也一样有可见/腐地/序列/帧的记忆。

Launcher 的 `SetFogFrozen` 只是 `Player.Pause()` + `Root.Visible = true`，模型还活着：升级换模型、队伍色、自带粒子照常变化，而且保持原色全亮，烟囱还在冒烟；建筑一旦被拆掉就 `QueueFree`，灰雾里立刻消失。

要做：建筑和装饰物都要「冻住最后看到的样子」，并吃到迷雾染色。

---

## 六、单位实体

## 15. 血条、选中圈、影子三块贴片都挂错了地方

- **血条**：客户端按 `SelectionDiameter` 换算成屏幕像素宽、按包围盒决定高度，所有可见存活非无敌单位都画，敌人恒红、有蓝条；Launcher 是世界尺寸 Quad（56 / 建筑 90），只有受伤或被选中才画，没有蓝条，颜色是两档离散色。而且 `BarRoot` 挂在已经缩放的 `Root` 下，模型缩放不是 1 的单位血条离头顶太远；建筑条宽 90，填充偏移却按 28 算，掉血时填充会往右漂。
- **选中圈**：客户端读 `SelectionRadius/Height`，用原版 `SelectionCircleMed.blp` 铺在地形三角面上，坡地贴合，敌红友绿；Launcher 是固定 32 段程序化圆环，颜色恒为 `3ce03c`，`y=6` 固定在 `Root` 下，所有兵种一样大，坡上会插地或悬空。
- **影子**：客户端是世界坐标轴对齐的地面贴花、逐顶点贴地，建筑按 30%/70% 锚点偏移；Launcher 的影子是 `Root` 的子节点，`Root` 每帧按朝向旋转，单位一转身影子跟着转；SLK 里没写尺寸的单位会回退成 64+ 世界单位的巨大黑影。

要做的三块都已完成（血条、选中圈、影子），条目等合并后再删。

**已完成**（2026-11-14，血条）：

1. `UnitType.SelectionDiameter = unitUI.scale × MiscData [SelectionCircle] ScaleFactor`（缺省 72），血条宽度走 `UnitStatusBarGeometry.Pixels` 换算屏幕像素，不再用 56 / 建筑 90 的世界 Quad。
2. 血条改屏幕空间：`BattleOverlayRenderer`（与客户端同名同路径）挂在 CanvasLayer 20，按模型顶投影画矩形，HUD(45) 仍压在它上面，相机俯仰不再把它拉变形。
3. 全部可见存活单位都画（去掉「受伤或被选中才显示」和 Alt 逻辑），敌方恒红、其余按血量走绿黄红连续色，补了蓝条（厚度系数 0.0028，贴在血条正下方）。
4. 填充用 `human-healthbar-fill.blp` 按血量截 UV + 顶点色，黑底用矩形；不再有两档离散色和填充右漂。
5. 顺带修 `MeasureHeight`：只统计可见 mesh，隐藏的升级形态 geoset 不再把血条顶高（Necropolis 708 → 417）。
6. 删掉 `UnitSceneComponent` 的 `BarRoot/BarFill/SetHp/SetBarVisible`，以及 20 处 `SetHp` 调用。

**已完成**（2026-11-14，选中圈）：

1. 半径改 `SelectionDiameter × 0.5`，不再按「建筑 3.2 / 其它 0.9」固定缩放。
2. 贴图换成 `SelectionCircleMed.blp`，`SelectionCircleRenderer` 按 8×8 细分铺在地形三角面上，逐顶点取高度，坡地跟着起伏。
3. 抬升 = 客户端 `GroundSplatOffset(1) + SelectionCircleBaseZ(16)`。
4. 敌红 `(1,.15,.15)`、友绿 `(.2,1,.25)`，与客户端 `SelectionRingColor` 同色。
5. 新增 `MapWorldBuilder.SurfaceHeightAt`：按地形网格的三角面（含 LayerHeight/RampLift）插值，替掉双线性 `HeightAt` —— 双线性在 tile 内部会落到网格下方。
6. 选中状态留在 `UnitSceneComponent.Selected`，圈由场景按 `_selected` 统一画，`Ring` 与程序化圆环网格删除。

**已完成**（2026-11-14，影子）：

1. 影子脱离 `Root`，改由场景层 `UnitShadowRenderer`（新文件）统一画世界坐标轴对齐的贴地贴花，单位转身不再带影子转。
2. 贴图/尺寸/锚点按客户端 `Presentation.RenderUnitShadows` 两个分支：建筑吃 `buildingShadow` 贴图、边长 = 贴图像素 × 32、锚点 (30%,30%)；普通单位吃 `unitUI` 的 `shadowW/H/X/Y`，宽高为 0 就不画（不再回退成巨大的 64+ 黑影）。
3. 几何按 128 世界单位地形格细分，逐顶点取 `SurfaceHeightAt`，抬升 `UnitShadowOffset(3)`；地图平面 (x 右, y 上) 与 Godot 世界是 `mapX = -worldX`、`mapY = worldZ`，贴图 V 轴跟随客户端 splat 翻转。
4. 复用项目里的 `shadow_map.gdshader`（顶点色 × 贴图 alpha），黑 `(0,0,0)`、alpha 0.5，与客户端 `uOpacity 0.5` 同口径；`Shadow.png` 是纯黑 RGB + alpha 0.75 的柔和椭圆。
5. 删掉 `UnitSceneComponent.ApplyShadow` 与 `Units.ApplyUnitShadow`（原来挂在 `Root` 下的 `QuadMesh` 子节点）。
6. 迷雾外/已死亡的单位不画，贴图组这一帧没人用时收起节点，不会留残影。

## 16. 队伍色回绕，幻象不变色，虚无没反应

- 客户端队伍色用 `TeamColorXX.blp`（0–27 号），`TeamGlowXX` 跟着玩家色变；Launcher 只内置 12 个纯色，`ColorOf(player)` 取 `player % 12`，于是 12–23 号（中立、野怪）会回绕成玩家亮色——野怪变成鲜红或亮蓝。队伍色区域在 shader 里被平涂成纯色，没有原版明暗纹理；光晕固定一种颜色。
- 客户端幻象按观察者关系染成 `(20,96,255)` 蓝；Launcher 的 `CreateIllusion` 只复制单位，幻象和真身一模一样。
- 客户端 `UnitStateColor` 处理虚无（淡绿半透明）、`SetUnitVertexColor`、死亡 alpha；Launcher 模拟层有 `StateMod.Ethereal` 和 `VertexColorRgba` 字段，渲染从不消费，被放逐的单位外观完全不变。
- 隐身：两边都压到 127/255，但客户端只作用于模型，Launcher 用 `GeometryInstance3D.Transparency` 把血条、选中圈、影子一起弄半透明。

要做：队伍色表补到 28 号并改成贴图替换；幻象、虚无、隐身的作用范围对齐。

## 17. 飞行单位贴地站着，转身是 20Hz 一跳，炮塔不会转

客户端模型 Z = 地形高 + `RenderFlyHeight`，飞行单位还有 MoveFloor 下限，建筑 +12 防 z-fighting；按 `ElevationSampleRadius/MaxPitch/MaxRoll` 采四点做俯仰横滚；朝向由 `FacingInterpolation` 逐渲染帧追；带 `LookAtBone` 的单位每帧把炮塔/龙头转向目标。

Launcher 的 `Spawn` 把模型 Y 设成 `SurfaceY(...)`，`FlyHeight` 只给弹道用过，所以狮鹫、双足飞龙、冰龙全都贴地；没有俯仰横滚；朝向在 20Hz 仿真步里直接写 `Root.Rotation`，角速度上限还是 14 rad/s（客户端 20）；完全没有 look-at。

要做：飞行高度、坡面姿态、朝向插值、look-at 四件，先做飞行高度。

## 18. 战斗动作：攻击只播一次，攻速不影响动作

客户端动画由 `UpdateUnitAnimation` 统一管优先级，每次出手 `Set("Attack")` + `Queue("Stand", Ready)`，速率跟 `AttackSpeedFactor` 走，切序列时做姿态混合。

Launcher 的 `Play` 里有 `if (found == CurrentAnimation) return;`，站桩连续攻击时第一刀之后一直停在攻击末帧，伤害照常结算；`SpeedScale` 只在 `Walk` 时设置；没有 `Stand Ready`，切换硬切；`SelectSequence` 返回第一个匹配项，同兵种待机完全同步、永远是同一个 `Stand` 变体（客户端按 rarity 随机）。

要做：`Play` 允许重播同一序列，攻速接上，补 Ready 和姿态混合，待机变体加随机。

## 19. 死亡、建造、升级的动作断在半路

- **死亡**：客户端 Death 播完按 `DeathAnimTime` 接 `Decay`（Flesh/Bone）或英雄的 `Dissipate`；Launcher 只 `Play("Death")`，非循环停在末帧，88 秒后直接隐藏。
- **建造**：客户端有 `FindConstructionBirthSequence`（避免暗夜主城误选 `stand work ... birth`）；Launcher 是 `PickAnimation("Birth")`，精确匹配不到就取任意以 Birth 开头的。
- **升级**：客户端按目标类型选 `Birth Upgrade First/Second` 并按进度 Seek；Launcher 升级期间外观完全不变，进度条走完才整栋换掉。
- **金矿**：客户端按缠绕金矿里的采集者数量选 `Stand Work First..Fifth`，显示 1–5 个小精灵；Launcher 没有这段。
- **关键帧插值**：客户端支持 step/linear/Hermite/Bezier，Launcher 转换只写 TRS 轨道，Godot 走线性，贝塞尔缓入缓出丢了。
- **时间缩放**：客户端有 `AnimationTimeScale` 参与时钟；Launcher 没有。

要做：死亡链、升级生长、金矿小精灵先补；切线信息属于 Extract 导出，单独排一条。

---

## 七、特效与声音

## 20. 特效只认一张手写表，`World.Effects` 根本不画

客户端特效完全数据驱动：从物编按等级读 `CasterArt/TargetArt/EffectArt/AreaEffectArt` 和挂点，`world.Effects` 里的 `SpecialEffect`、Buff、`ImageVisual` 统一收集绘制；Buff 视觉按 `VisualModelPath` 生成，同一挂点光环按 `AuraKey` 去重。

Launcher 在手写表 `EffectArtTable` 里维护 147 条「技能码 → 目录/后缀/挂点/是否持续」，按命名约定拼路径；`World` 里的 `Effects` 集合和 `TickEffects` 都在，但 Game 层没有任何一处渲染 `Effect.VisualModelPath`——`AddSpecialEffect`、`AddSpecialEffectTarget` 和 `ChainLightningEffect` 的 `TargetArt` 全都不可见。Buff 循环也没有，只有表里标了 `Stick=true` 的少数技能挂一个模型。挂点只用三档高度比例，刀光固定在身体中轴、不跟手。特效总数还有 60 个硬上限。

要做：先把 `world.Effects` 接上渲染（这一步能一次性救回一大片缺失特效），再把 `EffectArtTable` 逐步换成按物编读 Art 字段，挂点改成骨骼。

**已完成**（2026-11-14，Launcher5）：数据层 + 渲染层 + 施法入口三件都做了，手写表已删除。

1. `world.Effects` 接上渲染：`Game/Scenes/War3GameScene/Presentation.cs` 新增 `RenderVisualEffects`（与客户端同名职责），逐帧把 `SpecialEffect`、单位身上的 Buff 视觉、持续 Art（Caster/Target/Special 三组 persistent 分支）收进「按实例身份缓存节点」的表里，实例消失即释放。原先完全不可见的链式闪电 `TargetArt`、死亡缠绕、法力燃烧、物品幻象落地、火雨，现在都能看到。
2. 动画按实例年龄推进：序列选择顺序 Death → `AnimationName` → Birth → Stand、`PlayStandAfterBirth` 播完 Birth 接 Stand、循环序列取模、非循环钳住，都照客户端；`LifeSpan<=0` 的非循环特效播完即 `Expire()`，不再靠固定秒数硬删。
3. 挂点改成骨骼：`AttachmentPoints` 找到 `attach_*` 的 `Point` 节点后把特效挂成它的子节点，`overhead` / `chest` / `hand,right` 这类物编写法都能落到骨骼上，只有找不到才退回宿主根部。队伍色、`SpecialEffect.Color`、`VisualScale`、`VisualHeight` 一并接上。
4. 新增 `Game/Presentation/AbilityArt.cs`：按 rawcode 从 `Units{Human,Orc,Undead,NightElf,Neutral,Campaign,Common,Item}AbilityFunc` 文本表读 `Casterart / Targetart / Specialart / Effectart / Areaeffectart` 与 `Casterattach / Targetattach / Specialattach / Effectattach`；`Targetattachcount` 支持多挂点，`_` 表示无，`,`/`;` 分隔多模型，`ObjectData` 里有值时优先。
5. `Ability` 的 `CasterArts / TargetArts / SpecialArts / EffectArts / AreaEffectArts` 与四组 attachments 从空实现改成按物编读（按等级缓存），并补上客户端的 `SpawnArts`：`World.TryCast` 成功后统一往 `world.Effects` 塞 `SpecialEffect`。删掉 `EffectArtTable`（308 行手写表）、`Effects.cs`（171 行节点池）、`MaxSceneEffects=60` 硬上限和 `MountOffset` 三档高度。
6. Buff 视觉补上：`Buff.VisualModelPath` / `VisualAttachmentPoint` 读物编 `Targetart / Targetattach`，`VisualAgeSeconds` 按 tick 差算实例年龄。神圣护盾、闪电护盾、嗜血（两个手部挂点）、野怪的 `GeneralAuraTarget` 光环圈都出现了；同一单位同模型同挂点只画一次。
7. 暴风雪按客户端补冰片：`RuntimeBlizzardAbility` 每波按 `DataC`（夹 1..32）在半径内随机点生成 `BlizzardTarget`、时长 2s。

**还要做**：

8. `EfctID`（`X***` → `Effects\*.mdl`）这条艺术链没接：客户端走 `War3EffectArtResolver`，Extract 也没导出 `Effects/` 目录，等 Extract 补齐后再接。
9. `--effect-probe` 实测物编 976 条、美术引用 656、可加载 654，缺 2 条要 Extract 补导出：`Arsp` 的 `Abilities\Spells\Other\Stampede\MissileDeath.mdl`、`AIga` 的 `Abilities\Spells\NightElf\NatureTouch\NatureTouchTarget.mdl`。
10. `Ability.Arts` 目前单独读 `AbilityFunc` 文本表，没并进 `War3ObjectData` 的物编合并（客户端是在 `LoadStandardTypedData` 里把 txt 并进同一份 War3TypeData，技能和 Buff 共用）。统一后可以去掉这层单独读取。

**怎么验证**：

- `Godot --path . --rendering-driver opengl3 -- --play="(4)Turtle Rock.gmap" --bolt-probe`：控制台出现 `[FX] 载入 Abilities/Weapons/Bolt/BoltImpact.tscn → 森林巨魔 的 origin`，画面能看到闪电链光束和命中特效。
- 同上换 `--skill-probe`：出现 `[FX] 载入 .../DivineShield/DivineShieldTarget.tscn → 圣骑士 的 origin` 和野怪身上的 `GeneralAuraTarget` 光环圈。
- `--effect-probe`（可配 `--headless`）：打印物编美术覆盖率与缺失清单。
- 控制台每 2 秒报一次 `[FX] 在场特效 N`，技能放完会回落到 0（不残留）。

## 21. 粒子、丝带和模型事件都缺

- **粒子**：客户端逐实例模拟位置/速度/重力/寿命/模型空间，支持替换贴图、实例染色、队伍色粒子；Launcher 转换时烘成 `GPUParticles3D`，数量硬夹 512，参数取待机帧快照，没有队伍色/实例色。
- **丝带**：客户端有完整运行时丝带（发射时钟、历史点、上下高度、重力、颜色、纹理行列）；Launcher 只给 `gravity>0` 的丝带烘一张静态扫掠网格，其余只留 `metadata/war3_ribbon_*`，而且没有代码消费这些元数据——剑刃拖尾、魔法轨迹基本不出现。
- **模型事件**：客户端的 `ModelEventFired` 会解析 `SPN`/`SPL`/`UBR`/`FPT` 生成子特效或地面溅射；Launcher 转换没有事件节点，Game 层也没有派发，出生帧特效、溅射全没有（对应的声音在 24 条）。

要做：丝带优先（视觉损失最大），然后粒子参数和实例色，最后模型事件（要动 Extract 侧）。

## 22. 闪电是 20Hz 随机折线，弹道不俯仰、不裁剪

- **闪电**：客户端每帧按 `id/index/time` 确定性抖动、UV 滚动、段数上限 64、颜色乘实例 RGBA、端点支持 `StartHeight/EndHeight`；Launcher 每 50ms 用 `GD.Randf()` 重建，最多 24 条，UV 不滚动，端点固定在「单位位置 + 0.55 身高」。
- **弹道**：客户端用模拟层的分段路径加 `RenderArcOffset`、`VisualPitch`、`BulletDeathTime` 和迷雾可见性；Launcher 自己算抛物线、只设 Yaw，技能弹道弧强制 0，弹道在黑雾里照样可见。
- **占位与引导**：客户端模型缺失只记日志；Launcher 丢一颗金色 `SphereMesh`。客户端引导技能用技能自己的 `AreaEffectArt`；Launcher 自己画一个半透明蓝圆盘。

要做：闪电改成确定性抖动 + UV 滚动；弹道补俯仰和迷雾裁剪；把黄球和蓝盘去掉。

## 23. 飘字是贴在 3D 世界里的字，而且是「Miss」

客户端把伤害/资源数字和 `CreateTextTag` 都交给 `TextTagFrame` 在屏幕空间投影画，字号按 framebuffer 高度换算，速度/寿命/淡出点来自 `UI\MiscData` 可覆盖的样式表，之后在屏幕上飘、不跟单位。

Launcher 用 `Label3D`（世界单位、Billboard），拉远镜头字就变小；落空文本是 **Miss**（灰、0.9s），客户端是 **未命中**（红、3s）；普通伤害寿命 0.95s 对 0.4s，暴击 1.6s 对 2s；木材赏金客户端默认不显示，Launcher 会飘一个金色 `+N`；暗影突袭的绿色 `N/N!` 完全没有；可见性只判迷雾不判隐身。

要做：改成屏幕空间文字并接样式表；文本、颜色、时长逐项对齐。

## 24. 单位应答变成 3D 音，命中音只认木头和肉

- **应答**：客户端 `War3AckSoundService` 有完整节流——连续选择计数、Pissed 索引、每单位「语音时长 +250ms」冷却、同名去重，并返回时长给头像做说话动作；应答是 `Play2D`。Launcher 只有「12 秒内点 4 次切 Pissed」，没有节流，语音会叠在一起；而且走 `AudioStreamPlayer3D` 放在单位坐标 +80 高，点远处单位时又小又偏声道。
- **命中音**：客户端按 `weaponSound + 目标 ArmorType` 选 Flesh/Metal/Wood/Ethereal/Stone；Launcher 只有 `target.IsBuilding ? "Wood" : "Flesh"`。
- **施法音**：客户端按 `AbilitySounds.slk` 的 `Aas/Asp/Ahe/Acs/Acst` 查；Launcher 没有实现，反而每次施法播一遍 What 选择语音。
- **界面音效**：客户端有 InterfaceClick、MapPing、SubGroupSelectionChange、InterfaceError、ItemGet/ItemDrop、JobDone、UpgradeComplete、NoGold/NoLumber/NoFood/NoMana、CantPlace、InventoryFull、TownAttack；Launcher 只剩 `InterfaceError`、`ResearchComplete{Race}`、`UpgradeComplete{Race}`。
- **冷却**：Launcher 的 `_clock += 0.016f` 只在调用 `Play` 时推进，0.08s 冷却实际是「每 5 次调用响一次」。
- **音高**：客户端 `RANDOMPITCH` + `PitchVariance` 有自然变化，Launcher 解析了 `RandomPitch` 却没用，种子还写死。

要做：应答先改回 2D 并补节流，再按材质补命中音、补界面音效。

## 25. 音乐无缝连播，也没有脚步和建筑循环音

- **音乐**：客户端曲间有 20 秒间隔，支持主题音乐叠加、淡入淡出、`SetMapMusic/PlayMusic/StopMusic`、三层音量；Launcher 播完立刻下一首，固定 `-10dB`，没有淡入淡出、没有脚本接口。
- **循环音**：客户端的 `War3LoopingSoundService` 按 `MovementSoundLabel` 播脚步并跟随位置，建筑建造时播 `BuildingSoundLabel`；Launcher 没有循环音服务，行军完全没有脚步声。
- **声部**：客户端 24 个系统声部、同文件最多 3 个、按 channel 配额和优先级抢占、每条声音有自己的 Min/MaxDistance；Launcher 16 个固定播放器，参数写死，表里读出来的 Min/MaxDistance 从来没用。
- **音量**：客户端有 `SoundVolume` / `MusicVolume` 并按通道分；Launcher 没有任何音量设置。
- **表覆盖**：客户端加载 12 类，Launcher 合并成 5 张，`AnimSounds` 和 `Dialog` 没有源。

要做：曲间间隔和淡入淡出，脚步循环音，然后音量设置（和 40 的选项界面一起做）。

---

## 八、镜头与输入

## 26. 电影镜头和脚本镜头是空的

客户端 `War3Camera` 能装 MDX 的 `CAMS` 轨道（`SetCinematicModel` / `CinematicModelCamera.Step`），每帧直写位置、目标、FOV、近远裁剪和滚转，并跳过地形钳制；`SetField` / `PanTo` / `SetTargetController` / `SetOrientController` / `SetNoise` / `SetShake` / `ResetToGameCamera` 都有实际消费者。

Launcher 这些方法都在，但除 `SetSoftFollowController` 外没有任何调用者。

要做：`CinematicModelCamera` 补进来并接上轨道求值；接口先接到作弊/探针上验证。触发源在脚本，验收要等 JASS，但实现可以先行。

**已完成**（2026-11-14，电影镜头）：
1. 新增 `GameCore/L4_Composition/Game/Camera/CinematicModelCamera.cs`（与客户端同名同路径）：每帧按序列时间 `AnimationPlayer.Seek`，读回 `camera0` / `camera0_target` 的世界坐标与 `camera0` 的 `Fov/Near/Far`；时间夹到序列长度，与客户端 `min(elapsed, end-start)` 同口径。序列按惯例取 `Stand`（`GetAnimationList` 是字母序，不能拿下标当序列号）。
2. `War3Camera.SetCinematicModel(Node3D)`：节点由相机接管——加入场景、`Visible=false`、摘掉 `Model` 可视子树（只留 CAMS；否则为了读一只镜头要把整张场景的网格材质一起留在显存里，`Model` 里含相机时不摘）。
3. `War3Camera.Update` 顶部走电影分支：直写 `Position/Target`、跳过地形钳制、`GameplayProjectionScale` 固定 1、FOV 由导出的竖直视锥反算回本类半角量纲交给 `UpdatePose` 原样还原。
4. 释放点与客户端 `_modelCamera=null` 一一对应：`SetTarget` / `PanTo` / `Stop` / `SetTargetController` / `SetOrientController` / `SetField` / `ResetToGameCamera`。
5. 探针：`--cinematic=<War3 模型路径>`，配合已有的 `--shot=` 出图；用户参数必须放在 `--` 之后（`Godot --path . -- --play=… --cinematic=…`）。本机截图要加 `--rendering-driver opengl3`，Metal 后端截出来是全黑。
6. 实测：`UndeadCampaign3D` 的 Birth 序列眼点 2.5s 内从 (-413,-29,2889) 推到 (-2043,-222,2824) 后夹住；`HumanCampaign3D` 落位与导出的 `camera0.transform` 完全一致（-3698.73,-272.996,1223.71）。
7. 未闭环：滚转。客户端取 MDX 的 KCRL，Extract 目前只导出了相机位置轨迹（KCTR/KTTR），需要 Extract 补一条旋转轨迹才能吃进来；触发源仍是 `SetCinematicCamera`，等脚本层接上即可，链路已通。

## 27. 镜头不跟随、不吃设置、取景也和客户端不同

- **跟随**：客户端每帧驱动肖像跟随、锁定跟随、总跟随和标记跟随，总跟随按选择队列挑目标、过滤建筑/敌方/中立，喂的是渲染插值坐标。Launcher 只有键盘、边缘滚屏和拖拽，长按头像时临时跟随，拿到的是 20Hz 仿真坐标，高帧率下一格一格跳；松手立刻停，客户端要滑到单位附近才解绑。
- **设置**：客户端有 `DistanceScale` 和 `PanSensitivity`；Launcher 没有这两个概念，只有 6 档写死的距离。
- **相机档位来源**：客户端按 `UI\MiscData.txt` 逐层合并（基础库→War3x→War3Patch→当前地图），地图可以覆盖 `[Camera]`；Launcher 只读构建期生成的固定表。
- **取景**：客户端 `CameraBounds` 直接用 w3i，边界外靠 `CameraMargins` 放宽；Launcher 把 w3i 边界和地形网格矩形求了交集，带外围装饰的地图会被裁掉一截。开局客户端放在边界中心由脚本接管，Launcher 直接 `Focus` 出生点。世界投影客户端恒定 4:3 视锥，Launcher 走 Godot 相机默认的 `KeepHeight`。地形高度客户端按渲染网格同一条 TL–BR 三角面插值并查桥面，Launcher 是四角双线性、不查桥。

要做：补跟随和两个设置项；相机档位改成读地图覆盖层；边界不要取交集；投影和高度采样向客户端看齐。

## 28. 选择、子组、点头像

- **点头像**：客户端 `SelectFromGroup` 保留整队、只换主位，还处理载具乘客和金矿工人；Launcher 的 `SelectUnitFromHud` 先 `ClearSelection()` 再 `Add(unit)`——多选 8 个兵时点第二个图标只剩那一个，而且绕过选择控制器，`Primary` 不更新。
- **子组**：客户端 `CycleSubgroup` 先 `NativeSelectionOrder.Sort` 再只挪主位；Launcher 走场景自己的 `CycleSubgroup`，按 `Type.Id` 重建 `_selected`，不排序、不区分英雄/幻象，控制器那份成了死代码。双击选一排兵再按 Tab，切出来的顺序和屏幕排列不一致。
- **框选**：客户端判 `CanControl`（含共享控制）、用 `ScreenViewport.TryClipRect` 裁超宽屏、会 `CancelPending`、有选中音；Launcher 只判 `unit.Player == ViewPlayer`。
- **双击**：客户端双击或 Ctrl+点击都能选屏幕内同类，Launcher 只有双击。

要做：点头像改成 `SelectFromGroup`，子组并回控制器版本，框选补 `CanControl` 和裁切。

## 29. 右键的智能命令少一半分支

客户端 `War3WorldInteractionComponent` 的右键是一串分支，每支「吃掉能干的单位，剩下的继续执行本义」：集结点（逐单位 `CanSetRally`）、交资源回建筑、继续建造、修理、采金/伐木、传送门、运输机装卸、攻击/跟随（`EligibleAttackers` + `MoveUnhandled` 逐单位补全）、拾取地面物品，目标用真实拾取 `PickUnit`。

Launcher 只有四条：全选都是建筑时设集结点、点中残血友方建筑时修理、屏幕距离最近的敌人攻击（金矿转采金）、最近的树伐木，其余移动。目标判定用的是「屏幕像素距离 ≤55px」的启发式。

玩家看得到：选农民右键正在建造的兵营，Launcher 当成移动；选 5 个农民 + 2 个步兵右键残血主基地，Launcher 让所有单位 `return`，步兵站着不动；混选兵营 + 步兵右键不设集结点；重叠单位点到的东西可能和客户端不同。另外放置建筑没有「Shift 连造」。

要做：把分支按客户端补齐，目标判定换成拾取，放置模式支持 Shift 连造。

## 30. 物品栏不能拖，拾取靠中心点

客户端 `InventoryFrame` 有 `BeginDrag / MoveDrag / EndDrag / HitSlot`：拖到另一格换位、拖到友方英雄转交、拖到商店卖出、触屏矫正，否则丢地面；点地面物品会开 `ItemDetailFrame` 看详情。Launcher 只有 `SlotClicked`，右键是「给 450 范围内最近的友方英雄，否则丢地」——物品不能在格子之间换位，也不能拖着去商店卖，点地面物品也没有详情。

拾取也不同：客户端射线先打 MDX 的 `CLID` 碰撞体，再逐三角形打网格，按 geoset alpha 和 `SelectionFlags` 过滤；Launcher 把单位中心投到屏幕，取 55 像素内最近的一个。高模型、翅膀、武器伸出去很远的单位，点边缘选不中；点旁边空地反而可能选中。

要做：拖拽（`UiEventRouter` 的 `CommandDragMoved/Released` 现成可用），拾取换成几何判定，补地面物品详情。

---

## 九、加载流程

## 31. 点「开始」之后有一段空档，屏幕上还停在刚点过的列表

客户端点开始只是置标志位，真正的加载发生在主循环里：先 `DetachMenus()`，再把地图名写进标题，然后 `LoadMap()`；加载屏在 `LoadMap` 一开始就建好。

Launcher 是回调里直接干（`LauncherApp` 里 `Clear(); StartGame(mapPath)`），而 `StartGame` 先把 w3e/w3i/units/doodads/wpm 全解析完（`MapSource.Open`），才 `Clear()` 旧菜单、`new War3GameScene`。解析期间画面停在已经点过的列表上。

要做：先开加载屏再解析。

## 32. 加载屏少了缩略图和背景模型，节奏也不一样

客户端加载屏由宿主持有，`Report` 里直接画一帧再 SwapBuffers，`using` 完就释放，时长等于真实加载时长；`MinimapImage` 里塞的是地图缩略图，背景优先用 w3i 的 `LoadingScreenModel` 或 `UI\WorldEditData.txt` 的 `[LoadingScreens]`，FDF 绑失败还有一条直绘回退。

Launcher 的加载屏是场景里的一个 `Control`（这一层是平台差异，不用动），`Report` 只 `QueueRedraw`；但另外硬加了「至少显示 1.2 秒 + 稳定 3 帧 + 0.5 秒淡出」。缩略图完全没有，背景是按 skin 里模型路径取目录硬拼四张 `Loading-*.png`；`BuildFrameTree` 在 `_loading == null` 时直接 return，但 `LayoutTexts` 照样解引用，FDF 绑不出来会空引用。

要做：缩略图接上（选图页解码一次就能复用），背景模型按 w3i / LoadingScreens 表来，1.2 秒那条规则去掉或改成「不早于真实加载完成」，补回 FDF 失败时的直绘路径。

## 33. 加载阶段表和建世界的顺序都变了

客户端阶段是枚举驱动的（`Begin → Terrain → Textures → TerrainPresentation → SceneResources → WorldConfiguration → PlacedUnits → MapAot → MapScript → PresentationPreload → Pathing → Selection → Behaviours`），打点 .10/.22/.34/.45/.56/.66/.73/.76/.86/.93/.98。

Launcher 是 `SetupSteps` 里手写 async 顺序，打点 .10/.22/.34/.45/.66/.80/.90/.98：少了 .56、.73、.76 三格；「构建寻路与碰撞网格」从 .93 提前到 .80，跑在预热之前；单位在 .45 就生成，那时 `_world` 还是 null，落点只能退回纯地形高度。

要做：阶段名和打点对齐；World 先于单位；脚本那两格虽然不跑 JASS，也要留位置。

## 34. 加载出错时，一边中止一边带病进门

客户端只对 `SceneResources..Selection` 区间吞异常并跳到 `Behaviours`，其余阶段直接抛；`Scene.ContinueLoad` 会先 `Dispose` 再抛，最终由 `AppDomain.UnhandledException` 写 `CrashLog`。

Launcher 的 `RunSetupAsync` 捕所有异常，只 `GD.PushError`，然后照样 `Finish()` 并发 `Loaded`。

要做：区分「可跳过」和「必须中止」，中止时给玩家一个明确提示。

## 35. 进图不预载，第一次出现都卡一下

客户端的 loading 阶段会预载：地图现有单位、参战玩家的建造/训练/升级树、写死的 timer 兵线和野怪、每个单位的挂件模型、头像、所有武器弹道、技能的 Caster/Target/Special/Effect/Area 艺术、Buff 视觉模型，并由 `PresentationResourceRegistry` 统一释放。

Launcher 在 `.90 预热单位与界面资源` 之后没有预载循环，`Assets.LoadScene` 直接 `ResourceLoader.Load`。

要做：把客户端的预载名单搬过来，至少覆盖「本局会出的兵」和「本局技能表」。

---

## 十、界面

## 36. 地图选择界面的壳、背景和预览都还差一截

版式坐标两边一样（列表面板 `396×520 @ 84,132`、信息面板 `460×440 @ 500,132`、确定按钮 `454×52 @ 503,597` 都是同值），差在壳和内容：

- **宿主**：客户端用 `FdfUiFactory` 解析 `FrameDef.toc`，`Create("Skirmish")` 当全屏宿主，隐藏原有子帧，在宿主上挂两栏。Launcher 原来直接在 `_root` 上手搭，`Skirmish.fdf` 一直没人用。
- **背景**：客户端 `GlueModelLayer` 用三项皮肤跑模型层动画；Launcher 只有静态贴图。
- **预览**：客户端 `MapPreviewImage.Read` 依次试 `war3mapPreview.tga/blp`、`war3mapMap.blp/tga`，再用 w3i `LoadingScreenModel` 兜底；Launcher 只读 `war3mapMap.png`。
- **版本与警告**：客户端有「保存版本」和黄色 1.32 兼容警告；Launcher 原本两行都没有。
- **排序**：客户端普通地图按文件大小排；Launcher 原本按文件名。
- **超宽屏**：客户端用 `War3UiViewport` 居中 16:9；Launcher 原本拉满整窗。
- **Esc**：客户端暴露 `CancelRequested` 属性由宿主轮询，但全仓没有置位点；Launcher 发的是 `Cancelled` 事件，而 `LauncherApp.ShowMapList` 只订阅 `StartRequested`，按 Esc 等于没按；两个子对话框（种族、难度）开着时按 Esc 也关不掉。

**已完成**（2026-11-14）：

1. FDF 宿主接上了：`LoadTemplate(Skirmish.fdf)` + `Create("Skirmish")`，原有 7 个子帧全部隐藏，两栏挂在宿主上。客户端走的是 `ParseToc(FrameDef.toc)` + `ParseToc(SmashFrameDef.toc)`，但 Extract 还没把这两个 toc 导出成表，暂时只能单文件加载；等 toc 导出后换成和客户端同一套解析。
2. 「保存版本」和兼容警告补上了，判定走新加的 `MapCompatibilityNotice`（与客户端同名同路径），出警告时描述框收到 60。
3. 列表改成按文件大小排。
4. 界面改成居中 16:9，输入坐标跟着改成按控件尺寸换算。
5. Esc 收口：种族/难度对话框开着时先关对话框，其余情况不响应（客户端也没有退出选图页的去处）。顺带删掉没人订阅的 `Cancelled` 和没人读的 `PlayRequested`。
6. 预览顺序改成作者图优先：`war3mapPreview.png` → `war3mapMap.png`，各自解码失败就试下一张。Extract 的 `GmapExportCli` 白名单补了 `war3mapPreview.blp`，否则作者用 blp 存的预览图根本进不了 gmap。
7. 背景换成 `GlueModelLayer`：Extract 在模型 tscn 里导出 `camera0`（`current = false`）和 `camera0_target`，并把相机的 KCTR/KTTR 写成 `position_3d` 轨道；Launcher 用 SubViewport 渲染 `MainMenu3d_exp.tscn`，每帧对 `camera0_target` 做 LookAt，画面能复现客户端的镜头平移和极光动画。注意选图页用的是 TFT 皮肤的 `_V1` 背景（`MainMenu3d_exp`），不是主菜单那个 `_V0`。
8. 背景光照接上自有收集系统：Extract 把每个 `MdxLight` 导成带 `metadata/war3_light_*` 的 `Node3D`（全量 223 盏 omni、25 盏 directional、2 盏 ambient，方向灯多一条 `war3_light_direction`）；Launcher 新增 `Environment/DynamicLight.cs`（与客户端同名同路径），从节点元数据收集光源、打包成 4×8 浮点纹理写进 `war3_light_data`，同时写 `war3_light_count`、`war3_lighting_enabled` 和三个 `war3_scene_*`，配比照客户端 `SceneLight`（方向 +X、0.62 白、环境 0.22）。背景平均亮度从 124 降到 90，客户端 86。

**还要做**：

9. 预览第二级还没接：客户端在没有作者图时会用 w3i `LoadingScreenModel` 指的那张加载画面，Launcher 缺 blp 解码，得让 Extract 在导 gmap 时把这张也算进去。

## 37. 命令卡少了四个功能格，混选时只剩通用命令

客户端 `CommandCardBuilder` 是 5 个 partial 加一整套原生状态机：技能卡第 (3,1) 是「学习技能」（`BTNSkillz`、热键 `O`、角标显示剩余点数），点开后按 `Researchbuttonpos` 重排、第 12 格是「返回」；第 9 格是「建造」（`B`）；法术书有 `OpenSpellBook` / `BackFromSpellBook`；目标选择或放置模式下第 12 格换成 `BTNCancel`；混选时按成员 `CollectCommands` 聚合成一格并显示数量。

Launcher 的 `CommandCardBuilder` 只有 103 行，把 12 个槽刷成按钮就完了：没有学习页（学习直接点技能图标上的「+」，格位按 `HeroAbilList` 顺序硬排，和物编 `Buttonpos` 无关）；没有建造页（建筑直铺 12 格，多族建筑放不下，也没有「返回」）；没有法术书；没有取消格；混选只走 `_selected[0]`，加 5 个通用命令就 `break`。

要做：按客户端拆 partial，先把「学习 / 建造 / 取消 / 聚合」四件做了，法术书随后。

## 38. 命令卡的槽位、热键和可用性判定

- **槽位**：客户端 `FindNativeSlot` 是「向 11 找空位、再往 0 回退、满卡就丢」；Launcher 是「从请求位往后找第一个空位，找不到就 break」。同一份物编配置，两边第 3/8/11 格放的可能是不同技能。
- **热键**：客户端按按钮上真正显示的 `Hotkey` 分发（`CommandAtHotkey`），训练、研究、建造、升级都能用键盘；Launcher 的 `TrySelectionHotkey` 完全不看命令卡，只认 `ability.Hotkey` 和硬编码的 M/S/H/A/P，遇到建筑直接 `return false`。命令卡上的字母是纯装饰。
- **可用性**：客户端有 `NativeManaAdmission`、`NativeSpellOrderAdmission`、`NativeSelectionCommandPolicy`；Launcher 的判断写死在 `QuickCast.cs` 里，带法力修正符的单位会出现「亮着却放不出」。

要做：先把热键改成读按钮 `Hotkey`，再改槽位算法和可用性判定。冷却闪光、自动施法高亮、禁用变暗属于渲染设备能力，在 44 条。

## 39. 所有提示语都石沉大海

`MeleeHudController.SetHint` 只做 `_hint = text`，而 `_hint` 全工程除了声明和这一行赋值没有任何读者；`SetQueue(string)`、`SetClock(string)` 是空方法体，`SetPlayerInfo`、`SetBuffs`、`SetUnitSummary` 只写字段没人读。

结果是：资源不足点建造、修理由非农民点残血建筑、Ctrl+1 存编队、子组切换、放置失败——73 处调用，一句提示都看不到。客户端对应位置是 `CommandErrorFrame`（黄字、在 console 上方、3 秒 + 每字符 0.1 秒）。

信息面板的悬停说明也缺：客户端按帧名分派（金币/木材/人口/维修费、攻击力/护甲、三围、经验条、8 个 Buff 图标），Launcher 的悬停处理只认 `ButtonFrame`，而资源图标、数值标签、Buff 图标都不是 `ButtonFrame`。`TooltipFrame` 本身也退化了：客户端有 `ShowEx(title, body, ext, costs)`、四个成本图标槽、分段富文本、跟随锚点、屏幕边缘内缩、单位血蓝条，Launcher 是固定 280 宽纯色底 + 按 34 字符硬折行。

要做：先把 `CommandErrorFrame` 补上（不要造等价物，就用客户端这个名字和这套行为），再补信息面板悬停，最后升级 `TooltipFrame`。

## 40. Esc 语义相反，顶部按钮点了没反应，也没有选项界面

- **Esc**：客户端 Esc 是分层取消（取消待定命令 → 退出学习页），菜单开关是 F10；Launcher 里 Esc 第一件事就是 `_escMenu.Open()`，而且没有 F10。正在放置建筑时按 Esc，客户端取消放置，Launcher 弹菜单、关掉菜单后幽灵建筑还在。Backspace（循环主城）、PageUp/Down（缩放）、Ctrl+C/Alt+C（居中）、Alt+F（阵型）、Alt+G/T/A（小地图）也都没有。
- **Esc 菜单本体**：客户端是 FDF 的 `EscMenuMainPanel` 强类型绑定，隐藏存档/读档/帮助/重开，保留选项/结束游戏/返回游戏；Launcher 用 `ColorRect + Label + Button` 手搓，只有「继续/暂停/返回列表」，坐标还写死在 1600×900。
- **顶部按钮**：`TopBarFrame` 暴露了 `MenuButton` / `QuestButton` / `AlliesButton`，全仓没有一处 `.Clicked +=`，所以顶部按钮可点但完全没反应。任务和盟友面板不在本轮范围（见文末），这两个按钮按范围取舍隐藏；菜单按钮接 Esc 菜单。
- **选项**：客户端的 `AboutDialogFrame` 有镜头距离、平移灵敏度、音乐/音效音量、帧率上限、渲染倍率滑块，关掉时 `UserSettings.Save`；Launcher 没有任何设置界面。

要做：Esc 改回取消优先并补 F10；菜单按钮接上；选项界面和 25 的音量一起做。

## 41. 聊天和控制组各写了一套

- **聊天**：客户端 `ChatFrame` 是 HUD 里的帧，行会淡出、自动换行、解析 `|c` 颜色、有 `AppendHistory`；Launcher 的 `ChatFrame` 同名不同物，是独立 `CanvasLayer` 里的 `Label` + `LineEdit`，固定 `224,560 / 620×110`，不随 UI 缩放、不淡出、不换行，`Send` 只 `Add("[玩家] ...")` 加 `GD.Print`。多人频道（全体/盟友）不做，这里只对齐显示与排版。
- **控制组**：客户端屏幕左下有一排 1–9 编队按钮，支持双击居中；Launcher 只有键盘 0–9。

要做：聊天挪回 HUD 帧体系并补换行/淡出；编队按钮补上。

## 42. 小地图少了图标，滤镜点了没效果

- **图标**：客户端 `MinimapUnitView.Classify` 认金矿、缠绕金矿、闹鬼金矿、中立建筑、圈地、物品、野怪营地、出生点；野怪营地还有 `MinimapCampTracker`（按迷雾和等级画两张图标，位置走 MDX 模型）。Launcher 的 `MinimapUnitView` 只认几个硬编码 id，`SimulationId` 恒为 0，`ColorIndex` 直接等于 `unit.Player`；营地图标列表 `_minimapIcons` 从来没 `Add` 过，`DrawMinimapCamp` 直接 `return false`。小地图上没有野怪营地、没有物品、没有出生点，中立建筑也不显示中立色。
- **滤镜**：`MinimapFilter` 只被赋值没人读，`SetFilterState` / `HandleAltShortcut` 都没有调用者。点「野怪/配色/阵型」按钮图标会变，游戏里没有任何效果；Alt+G/T/A 没反应；阵型按钮不改变移动阵型。
- **腐地**：客户端有 `MinimapBlightTexture` 带缓存；Launcher 在 `Ui.cs` 里内联重算，腐地一变就整图重传并新建贴图。

要做：`Classify` 补全 + 营地图标，滤镜接上，腐地改用缓存那块。

## 43. 界面拉满整窗，超宽屏不居中

客户端把 `PixelOffsetX` 和 `ActivePixelWidth` 传给渲染设备，`War3UiViewport.FitPixelWidth` 把 HUD 限制在居中 16:9；Launcher 的 `GameHudScreen._Draw` 用 `viewport / 1024` 直接铺满，`_offsetX` 传 0。2560×1080 下 HUD 被横向拉宽三成，小地图变扁、命令卡格变宽、圆形按钮变椭圆。`GameHudScreen` 还自己又定义了一份 `DesignWidth/DesignHeight`。

`UiFontScale` 也没接：共用 `UiFontScale` 默认 1.2，Launcher 全仓没有一处 `Set`；客户端读用户设置并在倍率变化时 `InvalidateLayout()`（依赖 40 的选项界面）。

要做：`War3UiViewport` 用起来（选图页、加载屏已经在用），删掉重复常量，字号接设置。

## 44. 界面渲染能力缺几项，FDF 生成的绑定没人用

`GodotUiRenderDevice` 里：`DrawCooldownSweep` 是纯色扇形（客户端用 `cooldown.blp` 画），`DrawCooldownCompletion` / `DrawAutoCast` / `DrawTexturedQuadAdd` 分别返回 false 或退化成普通 alpha（所有 ADD 混合的贴图偏暗）。所以冷却遮罩没有原版质感、冷却结束不闪、自动施法没有旋转光环、高亮和光晕偏暗。

另一件事：Launcher 生成了 `ConsoleUiBinding`、`ResourceBarBinding`、`UpperButtonBarBinding`、`EscMenuMainPanelBinding`、`MainMenuBinding`，但除了 `LoadingScreenBinding` 之外没有任何调用者，HUD 走的是手写 `TopBarFrame` + `ConsoleSkinFrame`。客户端 `War3FdfUi` 是真的用这些绑定切换 `_console.Visible` 和 `SetChromeVisible`。

要做：先补三个渲染能力和 ADD 混合（都是小改动），再决定 FDF 绑定是接上还是删掉。

---

## 十一、代码结构

## 45. 同名不同物的文件

这些文件路径一样、类名一样，但职责完全不同，按名字对照会看错：

| 文件 | 客户端 | Launcher |
|---|---|---|
| `Game/Presentation/UnitSceneComponent.cs` | 94 行，Unit 与场景图对象的代理 | 548 行，模型实例化 + 血条 + 选中圈 + 影子 + 动画选择 |
| `Game/Scenes/War3GameScene/Presentation.cs` | 2311 行，单位/特效/弹道/影子/雾记忆总成 | 458 行，只剩弹道、语音、攻击音、引导圆盘 |
| `Game/Environment/War3Paths.cs` | 找游戏安装目录 | 装的是 `MapWorldBuilder.TriangleHeightAt` |
| `Game/Environment/CliffRenderer.cs` | GPU pass | 静态模型加载缓存 |
| `Game/Environment/CliffSurface.cs` | 逐顶点高度/法线准备器 | 悬崖网格构建器 |
| `Game/Scenes/War3GameScene/Terrain.cs` | 纹理数组、高度采样、水面高度 | 三个坐标转换函数 |
| `Game/Scenes/War3GameScene/Lobby.cs` | 客户端没有这个文件 | 座位、同盟、`PlayerSlot` |
| `Game/Hosting/GameHostContext.cs` | 完整宿主上下文与 4 个接口 | 只有 `MeleeAiPreset` 枚举 |
| `Src/World/FogOfWar.cs` | 客户端在 `Src/Core/FogOfWar.cs`（纯逻辑） | 逻辑 + 场景节点合体 |
| `Game/UI/MeleeHudController.cs` | 1096 行，持有 28 个帧并实现 `Render` | 181 行，转发壳 + 空实现 |
| `Game/UI/CommandCardBuilder.cs` | 5 个 partial + 15 个 Native/Admission 类 | 103 行刷槽 |
| `Game/UI/ChatFrame.cs` / `EscMenuFrame.cs` | FDF 帧 | Godot Control |
| `Game/UI/MapSelectionScreen.cs` | `IMenuScreen` + FDF `Skirmish` 宿主 | `Control` + FDF 宿主（36 条已对齐这一步） |
| `Game/Audio/War3AudioSystem.cs` | 1022 行，24 声部 + 配额 + 抢占 | 75 行，16 个播放器 |
| `Game/Scenes/War3GameScene/Ui.cs` | HUD 装配 + 文本标签 | 命令卡条目 + 商店 + 放置 + 小地图 |
| `Game/Scenes/War3GameScene/CameraFollowTrack.cs` | 跟随状态机 | 4 个取景函数 |

解析类名也对不上：`W3iInfo` vs `MapInfo`、`W3eTerrain` vs `TerrainData`、`W3mPathing` vs `PathingData`；`TeamColorIndex` 在 Launcher 里是 `Player` 的别名。

要做：改名或改路径，让「同名」等于「同职责」。

## 46. 各自写了一份的东西

明明可以共用却各写一份的：`WeatherDefinition` / `WeatherType`（近乎逐字复制）、`AnimationTagCache` 的 `SplitProperties`（私有复制了一份）、`ShorelineWaveRenderer` 的角落判定、`MinimapBlightTexture`（内联进 `Ui.cs`）。

`GameCore/L2_Core` 除 `MapBrowser` 外两侧逐字节相同，说明共用这条路是通的，上面这些只是没走。

要做：一次清理一份，先清有明确重复代码的（`WeatherDefinition`、`SplitProperties`），再清别的。

---

## 核对结果

按 AGENTS.md 的 1–5 条逐条对照，依据列里的数字就是条款号。

| # | 依据 | 结论 |
|---|---|---|
| 2 | 2 | 保留：尺寸算错，烘焙阴影等于全废 |
| 3 | 2 | 保留：地表花纹、斜坡、破面 |
| 4 | 2、3 | 保留：客户端并进阴影图，Launcher 另做黑片 |
| 8 | 2 | 保留：偶发大浪 |
| 9 | 2、3 | 保留：区域天气分支死 + var 语义错 |
| 10 | 2、3 | 保留：客户端有 SceneLighting/DynamicLight，Launcher 没有 |
| 11 | 2、4 | 保留：字段解析了没有消费者 |
| 12 | 2、3 | 保留：日月、选天空依据 |
| 13 | 2、3 | 保留：悬空雾面 vs 片元采样 |
| 14 | 2 | 保留：雾里建筑状态 |
| 15 | 2 | 三块已完成，等合并后删 |
| 16 | 2 | 保留：颜色与状态色 |
| 17 | 2 | 保留：贴地、姿态、朝向 |
| 18 | 2 | 保留：动画优先级 |
| 19 | 2 | 保留：死亡/升级/金矿 |
| 20 | 2、3 | 保留：手写表 vs 数据驱动 |
| 21 | 2 | 保留：粒子、丝带、事件 |
| 22 | 2 | 保留：闪电与弹道 |
| 23 | 2 | 保留：飘字样式 |
| 24 | 2、3 | 保留：应答、命中音、界面音效 |
| 25 | 2、3 | 保留：音乐、循环音、声部 |
| 26 | 3 | 保留：客户端有 CAMS，Launcher 空接口 |
| 27 | 2、4 | 保留：跟随、设置、取景 |
| 28 | 1、3 | 保留：两套子组、点头像清队 |
| 29 | 1 | 保留：右键分支 |
| 30 | 1、3 | 保留：拖拽、拾取、地面物品详情 |
| 31 | 1 | 保留：点开始到加载屏的空档 |
| 32 | 1、2 | 保留：缩略图、时长、回退 |
| 33 | 1 | 保留：阶段表与建世界顺序 |
| 34 | 1 | 保留：出错处理 |
| 35 | 1 | 保留：预载阶段缺失 |
| 36 | 1、2、5 | 保留：选图页壳与内容 |
| 37 | 2、3 | 保留：命令卡结构 |
| 38 | 2、3 | 保留：槽位、热键、可用性 |
| 39 | 2、3 | 保留：提示语与悬停说明 |
| 40 | 1、3 | 保留：Esc 语义、顶部按钮、选项 |
| 41 | 2、3 | 保留：聊天框、编队按钮 |
| 42 | 2、3 | 保留：小地图图标、滤镜 |
| 43 | 2、4 | 保留：超宽屏、字号设置 |
| 44 | 3、4 | 保留：渲染能力、FDF 绑定 |
| 45 | 5 | 保留：命名与职责错位 |
| 46 | 4 | 保留：重复代码 |

## 已排除的候选（不再写进清单）

| 原编号 | 内容 | 排除理由 |
|---|---|---|
| 12 | 地形高度变形 | 客户端只有脚本会调 `TerrainDeformCrater`（`TerrainDeformationTrigger` 注释写明「没有任何技能调用它」），Launcher 不跑 JASS，这条永远不会触发；等接 JASS 再一起做 |
| 37 | 任务、盟友、排行榜、多面板、脚本对话框、过场遮罩、秘籍面板 | 全部来自 `_jassRuntime?.GetQuests()` 这类触发器数据，属于 Launcher 明确不做的 JASS/触发器范围；其中「地面物品详情」已并入 30 |
| 46 | 没有调用者的接口清单 | 不属于 1–5 任何一条，是代码卫生；其中会影响玩家的（`SetHint`、顶栏按钮、营地图标、`MinimapFilter`）已分别写进 39、40、42。排查时先 grep 调用者仍是个好习惯，但不当任务列 |
| 36 子项 | F4–F7 选英雄 | 单人最多 3 个英雄，永远用不到 |
| 38 子项 | 全体/盟友聊天频道 | 多人频道不做 |
| 43 子项 | 电影模式隐藏 HUD | 过场本身来自脚本，同 37 的理由 |

---

## 推进顺序

编号已经是自底向上的顺序，从上往下做即可，但有三个例外：

1. **第 36 条**（选图界面）已经完成 P0，随时可以收尾剩下的 Esc、预览、Glue 背景，不必等底层。
2. **第 39 条**（提示语）和第 38 条（热键）现在是「按了没反应」，属于功能性缺陷，可以提前插队。
3. **第 26 条**（镜头）的实现可以先做，验收等 JASS。

底层那一批里，**第 2、3、10、11、13** 是「一改就能并排看出差别」的，建议先啃这五个；**第 9、12** 跟在后面，因为它们和前面共用同一套地形/环境代码，隔开做会来回改同一个文件。

单位实体那一批（**15–19**）互不依赖，可以并行；特效与声音（**20–25**）里 20 的「接上 `world.Effects`」性价比最高。

加载流程（**31–35**）要动 `LauncherApp` 和加载编排，和渲染改动冲突小，适合在不方便跑图的时候做。

结构清理（**45、46**）什么时候做都行，但每接一条新功能之前先看一眼 45，能省一次找错文件。

## 收尾规则

- 一条做完就从本文删掉，序号不复用，后面的不重排；下一条新任务从 **49** 开始。
- 新发现的差异先判断是不是「平台差异」或「已知取舍」：Godot 与原生 GL 的差别、Launcher 固定 gmap 路径、不跑 JASS/触发器/战役/存档/录像/联机，这些不写进来。
- 写进来的时候要带三个东西：客户端在哪、Launcher 在哪、玩家看得到什么。只有「不一样」没有证据的，等复核清楚再写。

## 47. 地形与悬崖不接收 MDX 动态光

客户端 `Terrain.vert.glsl` 和 `Cliff.vert.glsl` 里各有一段 `dynamicLightFactor`，把单位的动态光（火把、法术特效）按距离/衰减累加进顶点光照；Launcher 的地形、悬崖、水面着色器只吃方向光和环境光。

要做：把 `war3_light_data` 也传进三个世界着色器，按客户端公式累加。
