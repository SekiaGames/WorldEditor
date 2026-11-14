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

**本次是整体重排，编号全部重排过一次；从那以后恢复「删掉不复用、新任务往后续」的规则。当前排到 54，下一条新任务从 55 起。**

## 范围

- 起点是地图选择界面。客户端首屏（公告页、主菜单、菜单音乐）、地图文件夹浏览、地图导入、战役列表都不在本轮范围内，Launcher 保持固定地图路径，地图只从 `test/map_gmap` 读。
- Launcher 不跑 JASS 与触发器，所以「只有脚本能触发」的效果不进清单，这一条在文末「已排除的候选」里有对照。

---

## 一、资源与数据

## 二、地形与地表

## 三、水与天气

## 四、光照与大气

## 12. 天空没有太阳月亮，选天空的依据也不对

客户端单独加载 `Environment\Sky\Sky\skyLight.mdx`（日月就在这个模型里），按 `SunMoonAngle(timeOfDay)` 每 24 小时转一圈跟着相机走；天空用 `GameplayConfig.SkyboxType`（war3mapMisc 的 0..14 索引）选，`SetSkyModel` 可以覆盖。

Launcher 只加载天空壳（`SkyDomeRenderer`），不加载 `skyLight`（Extract 里其实有 `skyLight.tscn`）；天空按地形 tileset 字符猜，自定义天空和 war3mapMisc 都不生效。另外多了一层 `PanoramaSkyMaterial` 全景底，天空壳半透明处会露出亮带。

要做：加载 `skyLight` 并按时辰旋转；天空改按 `SkyboxType` 选；全景底只在完全没有天空壳时兜底。

已完成（Launcher5 `任务12` 提交）：

- **顶栏 24 小时时间指示器补上了**：新增 `Game/Rendering/TimeOfDayViewport`，用 `SubViewport` + 正交相机把皮肤里 `TimeOfDayIndicator`（`UI\Console\<race>\<race>UI-TimeIndicator.mdl`）画到设计空间。位置不是手写坐标，而是取模型自身包围盒乘 `FdfScale.ToDesignUnits(1280)`：`x 431.36 / y -9.79 / 163.84×91.71`，正好落在顶栏中段 `439~587` 的空缺上，与客户端 `RenderTimeOfDayIndicator` 的投影口径一致。绘制顺序放在 `_root.Render` 之前，和客户端「先画指示器再画 FDF 帧」一致。
- **anim 由时辰驱动**：`Stand` 序列长 60，`SetClock(时)` 直接 `Seek(时/24×60)`，日月走模型自带的 `GeoSphere01` 旋转轨道与 geoset 显隐轨道，不再自己算角度。
- **数字时间显示出来了**：`TopBarFrame._timeText` 一直存在但从来没显示——`SetChromeVisible` 全仓无人调用，`Visible` 初始 false 就再没翻过来。现在建 HUD 时按客户端 `SetChromeVisible(!UseFdfResourceBar)` 的口径调 `SetChromeVisible(true)`。
- **UI 模型材质换了一套**（Extract 那份 `war3_model_opaque` 在这只模型上取不到 frame 贴图 alpha，石框整个被 discard 掉）：不透明层改 `StandardMaterial3D`（unshaded、cull off、nearest、alpha scissor 0.5）；ADD 层改用本工程自带的加色 shader。ADD 在透明离屏里会把 alpha 也累加成不透明，导致「黑方块」，所以按亮度写 alpha：`ALBEDO = rgb·a·gain`、`ALPHA = a·max(rgb)·gain`。代价是这三层 `geoset_tint` 的动画轨道不再驱动颜色（材质已不是原 ShaderMaterial），显隐轨道和旋转轨道照常生效。
- **天空改按 `SkyboxType` 选**：`MapSource` 从 gmap 的 `war3mapMisc.txt` 读 `SkyboxType`；`SkyDomeRenderer` 用客户端 `SkyboxRenderer.SkyModels` 那张 0..14 表选壳，`SkyboxRenderer` 的全景底只在壳模型缺失时创建（原来两级都建，全景底从半透明处漏出亮带）。
- **日月载入 `Environment\Sky\Sky\skyLight`**：跟相机同心，`Rotation.Y = 2π·时/24`（客户端是 `CreateRotationZ`，MDX 的 Z-up 映射到 Godot 就是绕 Y）。skyLight 自带的天空壳（3911）与 8 片云条（5600~6500）会把地形盖住——这些层在客户端是 `BackgroundPass` 不写深度、先画后被地形覆盖，Godot 里没法这么排，所以按包围盒尺寸 >3000 剔除，只留日月两块（716/450）。
- 验证：`--play="(4)Turtle Rock.gmap" --rendering-driver opengl3 --shot=...` 看顶栏；小时可以用 `--day-seconds=` 快进。`[SKY] 穹顶 ... 日月 已载入`、`[TOD] 时间指示器 ... 序列 Stand 长 60 秒 区域 ...` 是日志取证口。日月是否在天上肉眼不好验（顶视角基本看不到天空），按 8 点/12 点打印过日月中心随时刻转动、高度恒为 1057。

未做：`skyLight` 的云层（上面剔了）；`geoset_tint` 动画；`SetSkyModel`（Launcher 没有 JASS 运行时，`SkyModelOverride` 这条没有对应物）。

---

## 五、战争迷雾

---

## 六、单位实体

## 16. 队伍色回绕，幻象不变色，虚无没反应

- 客户端队伍色用 `TeamColorXX.blp`（0–27 号），`TeamGlowXX` 跟着玩家色变；Launcher 只内置 12 个纯色，`ColorOf(player)` 取 `player % 12`，于是 12–23 号（中立、野怪）会回绕成玩家亮色——野怪变成鲜红或亮蓝。队伍色区域在 shader 里被平涂成纯色，没有原版明暗纹理；光晕固定一种颜色。
- 客户端幻象按观察者关系染成 `(20,96,255)` 蓝；Launcher 的 `CreateIllusion` 只复制单位，幻象和真身一模一样。
- 客户端 `UnitStateColor` 处理虚无（淡绿半透明）、`SetUnitVertexColor`、死亡 alpha；Launcher 模拟层有 `StateMod.Ethereal` 和 `VertexColorRgba` 字段，渲染从不消费，被放逐的单位外观完全不变。
- 隐身：两边都压到 127/255，但客户端只作用于模型，Launcher 用 `GeometryInstance3D.Transparency` 把血条、选中圈、影子一起弄半透明。

已完成（2026-11-14），条目等合并后再删。

**已完成**（2026-11-14）：

1. 队伍色改成读 `ReplaceableTextures/TeamColor/TeamColor00..15.png` 的首个像素（12–15 号是中立灰 `#282828`），`ColorOf` 由 `% 12` 改成 `Clamp(0,15)`，野怪/中立不再回绕成玩家的鲜红/亮蓝。
2. `war3_model_add` 且 base_texture 落在 `TeamGlow/` 的模型光晕层，按单位色号换成对应的 `TeamGlowXX.png`（原来恒为 `TeamGlow00` 的红色）。
3. 新增 `UnitStateColor`（与客户端同路径同名）：`VertexColorRgba` × 虚无 `(.75,1,.5,.5)`，隐身且存活时 alpha 取 `min(127/255)`。
4. 新增 `IllusionPresentation`（与客户端同路径同名）：己方/盟友的幻象染 `(20,96,255)`，敌方幻象与真身同色；观者关系走场景 `AreAllied`。
5. 外观色落到模型：RGB 乘进 `geoset_tint`、alpha 走 `GeometryInstance3D.Transparency`，每帧只在值变化时写；血条/选中圈/影子已不在 `Root` 下，隐身不再把它们一起弄半透明。
6. 删掉 `UnitSceneComponent.SetGhost` 与 `Environment` 里的调用，隐身改由上面的外观色统一处理。
7. `UnitSceneComponent.Create` 的入参从 `Color` 改成色号索引，光晕换贴图需要索引。

## 17. 飞行单位贴地站着，转身是 20Hz 一跳，炮塔不会转

客户端模型 Z = 地形高 + `RenderFlyHeight`，飞行单位还有 MoveFloor 下限，建筑 +12 防 z-fighting；按 `ElevationSampleRadius/MaxPitch/MaxRoll` 采四点做俯仰横滚；朝向由 `FacingInterpolation` 逐渲染帧追；带 `LookAtBone` 的单位每帧把炮塔/龙头转向目标。

Launcher 的 `Spawn` 把模型 Y 设成 `SurfaceY(...)`，`FlyHeight` 只给弹道用过，所以狮鹫、双足飞龙、冰龙全都贴地；没有俯仰横滚；朝向在 20Hz 仿真步里直接写 `Root.Rotation`，角速度上限还是 14 rad/s（客户端 20）；完全没有 look-at。

三块都已完成（飞行高度、坡面姿态、朝向插值、look-at），条目等合并后再删。

**已完成**（2026-11-14，飞行高度）：

1. `UnitType` 补 `FlyHeight = UnitData.moveHeight`、`MoveFloor = max(0, moveFloor)`，此前 Launcher 把飞行高度写死成 `Flying ? 100 : 0`。
2. `Unit.FlyHeight` 改成可写字段，未赋值时回落到类型的 `FlyHeight`，这样 `Type` 为空/后设的召唤、幻象、变形路径都不用单独初始化。
3. 模型摆位统一收到 `UnitSceneComponent.SyncPlacement`：`y = 地面高 + RenderFlyHeight`，建筑再 +12 防 z-fighting，飞行单位按 `moveFloor` 兜底取大；每帧在 `_Process` 里跟着 `unit.Position` 走。
4. `RenderFlyHeight` 按客户端 `UpdateRenderFlyHeight` 的方式平滑逼近 `unit.FlyHeight`（速度 = 本次变化量 / 仿真步长），起飞/落地不会瞬移。
5. 影子与选中圈仍读 `unit.Position`（地面），飞行单位不会把影子带上天；血条读 `Root.GlobalPosition`，跟着模型升空。

**已完成**（2026-11-14，坡面姿态）：

1. `UnitType` 补 `TerrainTiltRadius = unitUI.elevRad`、`MaxPitchDegrees = maxPitch`、`MaxRollDegrees = maxRoll`（步兵 r=20/p=10、树 r=30、狮鹫 r=50/p=33/roll=25、建筑多为 0）。
2. 新增 `UnitTerrainTilt.Compute`：沿朝向按半径采前/后/左/右四点，`pitch = atan2(前后, 2r)`、`roll = atan2(左右, 2r)`，各自 clamp 到上限；上限为负值时按客户端语义当成固定倾角。
3. 姿态在 `SyncPlacement` 里写 `Root.Rotation = (pitch, yaw, -roll)`，Godot 默认 YXZ 欧拉正好是「先本地 roll、再本地 pitch、最后 yaw」；模型前向是 -Z（由 `Facing = π + atan2(-dx, dz)` 反推），所以 pitch 取正、roll 取负。
4. `ApplyFacing` 转向时保留 X/Z 分量，不会每 20Hz 把倾角清一次。
5. 平地实测不误倾斜（营地四个朝向的步兵都站正）；营地/岸边等有落差处 pitch 打满上限，与客户端同样被 `maxPitch` 限制。

**已完成**（2026-11-14，朝向插值）：

1. 仿真侧 `ApplyFacing` 只维护逻辑 `unit.Facing`，不再直接写 `Root.Rotation`；渲染侧由 `UnitSceneComponent.SyncFacing` 用指数平滑（时间常数 0.04s）逐帧逼近，20Hz 的台阶被抹平。
2. `SyncPlacement` 用平滑后的 `RenderFacing` 写欧拉 Y 分量，倾角仍取当帧计算值。
3. 首次出现、暂停恢复（`delta >= 1`）直接吸附，不会从 0 转过去；变形重建的 `View` 也是新组件，同样吸附。

**已完成**（2026-11-14，look-at）：

1. `Unit` 补 `LookAtBone` / `LookAtTarget`（与客户端同名字段）。
2. `Step` 开头对建筑跑 `UpdateTurretLookAt`：有存活攻击目标且自身能攻击时置 `Bone_Turret`，目标消失或不再攻击时清掉（对应客户端 `Unit.Combat.UpdateTurretLookAt`）。
3. `UnitSceneComponent.SyncLookAt` 在渲染帧把该骨骼指向目标：目标坐标转到 `Skeleton3D` 空间，构造「X=前向、Z=上」的基，用 `SetBoneGlobalPoseOverride` 覆盖；取消瞄准时以 weight 0 发一次恢复。
4. 模型骨骼名与客户端一致（`HumanTower.glb` 里确有 `Bone_Turret`，第 15 根，且被蒙皮引用）；`FindBone` 失败或无骨骼的模型（地形装饰等）直接跳过。
5. 验证：新增 `--turret-probe` 在亡灵基地旁摆一座人族塔 + 一个敌方步兵，塔的 `CanAttack/Target/aiming` 均为真；把 `Bone_Turret` 换成 `FlagPole` 强转 90° 时整座塔几何跟着变，确认 override 通路真的作用到渲染。

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

## 21. 粒子、丝带和模型事件都缺

- **粒子**：客户端逐实例模拟位置/速度/重力/寿命/模型空间，支持替换贴图、实例染色、队伍色粒子；Launcher 转换时烘成 `GPUParticles3D`，数量硬夹 512，参数取待机帧快照，没有队伍色/实例色。
- **丝带**：客户端有完整运行时丝带（发射时钟、历史点、上下高度、重力、颜色、纹理行列）；Launcher 只给 `gravity>0` 的丝带烘一张静态扫掠网格，其余只留 `metadata/war3_ribbon_*`，而且没有代码消费这些元数据——剑刃拖尾、魔法轨迹基本不出现。
- **模型事件**：`SPN` 生成子特效、`FPT` 脚步特效仍未派发，出生帧特效和脚印全没有（对应的声音在 24 条）；地面溅射 `SPL`/`UBR` 已接。

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

## 27. 镜头不跟随、不吃设置、取景也和客户端不同

- **跟随**：客户端每帧驱动肖像跟随、锁定跟随、总跟随和标记跟随，总跟随按选择队列挑目标、过滤建筑/敌方/中立，喂的是渲染插值坐标。Launcher 只有键盘、边缘滚屏和拖拽，长按头像时临时跟随，拿到的是 20Hz 仿真坐标，高帧率下一格一格跳；松手立刻停，客户端要滑到单位附近才解绑。
- **设置**：客户端有 `DistanceScale` 和 `PanSensitivity`；Launcher 没有这两个概念，只有 6 档写死的距离。
- **相机档位来源**：客户端按 `UI\MiscData.txt` 逐层合并（基础库→War3x→War3Patch→当前地图），地图可以覆盖 `[Camera]`；Launcher 只读构建期生成的固定表。
- **取景**：客户端 `CameraBounds` 直接用 w3i，边界外靠 `CameraMargins` 放宽；Launcher 把 w3i 边界和地形网格矩形求了交集，带外围装饰的地图会被裁掉一截。开局客户端放在边界中心由脚本接管，Launcher 直接 `Focus` 出生点。世界投影客户端恒定 4:3 视锥，Launcher 走 Godot 相机默认的 `KeepHeight`。地形高度客户端按渲染网格同一条 TL–BR 三角面插值并查桥面，Launcher 是四角双线性、不查桥。

要做：补跟随和两个设置项；相机档位改成读地图覆盖层；边界不要取交集；投影和高度采样向客户端看齐。

执行情况（Launcher4，待合并）：

1. **跟随**：新增 `GameCore/L4_Composition/Game/Scenes/War3GameScene/CameraFollow.cs`，把客户端 `War3CameraComponent.Update` 里那四条驱动补齐（肖像 / 锁定 / 总跟随 / 标记），调用顺序与客户端一致，位置也在 `camera.Update` 之前。喂镜头的是**渲染率样本**：`AdvanceFollowSample` 按单位速度每渲染帧追赶仿真坐标（客户端 `RenderPosition` + `SnapRenderTransform` 同一套，差超过 max(64, 3 步) 直接贴合），不再把 20Hz 仿真坐标直接喂镜头。肖像跟随改用客户端的「目标控制器 + 表现坐标」通道，且**松手不立刻停**：先立 `releasePending`，等可见镜头进到 32 单位内才解绑。锁定/总跟随/标记走 soft-follow（换目标带 0.3~0.75s 滑翔）。玩家中键拖镜头打开 0.1 秒失锁定窗口，窗口内解绑让位、归零后自动滑回。
2. **跟随入口**：桌面端用热键替代安卓面板 —— `Ctrl+F` 总跟随、`Ctrl+Shift+F` 锁定跟随、`Ctrl+Alt+F` 标记并跟随；必须排在物编热键**之前**（`TrySelectionHotkey` 不判修饰键，放后面 Ctrl+F 会被当成 F 技能）。总跟随的四个过滤开关按客户端默认（建筑参与，敌人/中立/友军不参与，自己的单位恒允许），留了 `SetCameraFollowTrackFilter` 供将来接面板。
3. **两个设置项**：`War3Camera.DistanceScale`（量程 [0.5,4]，与客户端 `GameUserSettings.MinScale/MaxScale` 同源）盖住三处距离消费点（`SnapToPreset` / `RestorePresetFields` / 每帧 `ResolveField(0)`），`SetPlayerDistanceScale` 走「玩家命令」立即生效；`Update(..., panSensitivity)` 把灵敏度乘在方向键/边缘滚屏的**速度**上（=1 与旧行为逐字一致）。
4. **档位来源**：`LoadSettings` 先读地图自己那层 MiscData（`war3mapMisc.txt` → `UI/MiscData.txt`，低→高，与客户端「地图层最高」一致），再落构建期生成表；`[Camera]` / `[CameraRates]` / `[CameraMargins]` 都可被覆盖，命中时打 `[CAMERA] 地图覆盖 [Camera]`。
5. **边界**：`SetCameraBounds` 直接取 w3i 那 8 个角点的包围盒（原来只取了前两个点当矩形，还和地形网格求交集），世界坐标 X 取负后左右翻转；只有 w3i 缺失/写坏才退回地形矩形。放宽仍由 `CameraMargins` 负责。
6. **投影**：世界相机改成客户端口径 —— 恒定竖直 70° 视锥（`KeepAspect=Height`，宽屏只扩水平视野），删掉把游戏内竖直视锥压到 54.43° 的魔数；电影镜头 FOV 改成 `modelFov*0.5` 直接存半角量纲，不再绕 `PlatformGodotFov` 往返。
7. **高度采样**：`CameraTargetHeightAt` = max(地形渲染网格表面（同一条 TL–BR 三角面 + 斜坡抬升，`SurfaceHeightAt`）, 双线性水面 `WaterPlaneHeightAt`, 可通行面)；原来走四角双线性 `HeightAt`，水面还是取最近角点。
8. **桥面**：新增 `GameCore/L4_Composition/Game/Environment/WalkableSurfaceProbe.cs` —— 把 SLK `walkable=1` 的装饰物/破坏物（共 117 条，全是 `Doodads\Terrain\*Bridge*`）的模型三角面在进树后烘成世界坐标，垂直射线取最高命中点，与客户端 `DoodadRenderer.TryGetWalkableHeight` 同口径。两张测试图都没有桥，路径按数据实现、待有桥地图复核。
9. **顺带修**：天空球壳（半径 1957）原来挂在目标点上，镜头距离倍率一拉远眼点就钻出壳外、整屏只剩壳外侧渐变色；改成按眼距等比放大（正常档位 scale=1，取景与之前逐像素一致）。
10. **探针**：`--camera-follow=track|lock|marked|portrait`、`--camera-distance=`、`--pan-sensitivity=`，用户参数要放在 `--` 之后，截图要加 `--rendering-driver opengl3`。

验证数据（`(4)Turtle Rock.gmap`）：相机边界从「地形∩w3i」变成 w3i 原值 `(-14336,1664)-(-2048,14464)`；预设仍是 1650/1600/1500/1400/1275/1100、AOA 304..339（说明没误读覆盖层）；距离倍率 2 → 第 1 档 3300，取景正常。跟随探针：单位每 0.4s 前进 72 单位时镜头目标点同步推进（本机 2.5FPS，60FPS 下滞后仅一帧）；肖像跟随松手后继续跟到单位停步、进到 32 单位内才解绑（`following=True → False`）。

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

## 核对结果

按 AGENTS.md 的 1–5 条逐条对照，依据列里的数字就是条款号。

| # | 依据 | 结论 |
|---|---|---|
| 2 | 2 | 保留：尺寸算错，烘焙阴影等于全废 |
| 3 | 2 | 保留：地表花纹、斜坡、破面 |
| 4 | 2、3 | 保留：客户端并进阴影图，Launcher 另做黑片 |
| 8 | 2 | 保留：偶发大浪 |
| 10 | 2、3 | 保留：客户端有 SceneLighting/DynamicLight，Launcher 没有 |
| 12 | 2、3 | 保留：日月、选天空依据 |
| 15 | 2 | 三块已完成，等合并后删 |
| 16 | 2 | 已完成，等合并后删 |
| 17 | 2 | 已完成，等合并后删 |
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

底层那一批里，**第 2、3、10** 是「一改就能并排看出差别」的，建议先啃这三个；**第 12** 跟在后面，因为它们和前面共用同一套地形/环境代码，隔开做会来回改同一个文件。

单位实体那一批（**15–19**）互不依赖，可以并行；特效与声音（**21–25**）里 22 的闪电和弹道最容易被玩家看出来。

## 收尾规则

- 一条做完就从本文删掉，序号不复用，后面的不重排；下一条新任务从 **55** 开始。
- 新发现的差异先判断是不是「平台差异」或「已知取舍」：Godot 与原生 GL 的差别、Launcher 固定 gmap 路径、不跑 JASS/触发器/战役/存档/录像/联机，这些不写进来。
- 写进来的时候要带三个东西：客户端在哪、Launcher 在哪、玩家看得到什么。只有「不一样」没有证据的，等复核清楚再写。

## 47. 地形与悬崖不接收 MDX 动态光

客户端 `Terrain.vert.glsl` 和 `Cliff.vert.glsl` 里各有一段 `dynamicLightFactor`，把单位的动态光（火把、法术特效）按距离/衰减累加进顶点光照；Launcher 的地形、悬崖、水面着色器只吃方向光和环境光。

要做：把 `war3_light_data` 也传进三个世界着色器，按客户端公式累加。

## 49. `EfctID` 那条艺术链没接

客户端 `War3EffectArtResolver` 把 `EfctID`（`X***`）解析成 `Effects\*.mdl`，挂到单位身上做常驻表现；Launcher 只读了 `CasterArt` / `TargetArt` / `SpecialArt` / `EffectArt` / `AreaEffectArt` 五个字段，`EfctID` 完全没读，Extract 也没导出过 `Effects/` 目录。

要做：Extract 先导出 `Effects/` 目录，再在 `AbilityArt` 里补 `EfctID` 这条解析。

## 50. 两条技能美术资源 Extract 没导出

`--effect-probe` 实测物编 976 条、美术引用 656、可加载 654，缺两条：`Arsp` 的 `Abilities\Spells\Other\Stampede\MissileDeath.mdl`、`AIga` 的 `Abilities\Spells\NightElf\NatureTouch\NatureTouchTarget.mdl`。

要做：Extract 补导出这两个模型，跑 `--effect-probe` 应变成「缺 0」。

## 51. `Ability.Arts` 没并进物编合并

客户端在 `LoadStandardTypedData` 里把 `Units*AbilityFunc` 这些 txt 并进同一份 `War3TypeData`，技能和 Buff 共用一份数据；Launcher 的 `AbilityArt` 单独读一张合并过的文本表，等于多了一层旁路。

要做：把技能美术字段并进 `War3ObjectData` 的物编合并，之后 `AbilityArt` 可以退化成按字段名取值。

## 52. 天气和特效粒子不吃雾

客户端 `Weather.frag` 按 `useFog` 把 `uFogColor/uFogParams` 传进天气粒子，MDX 粒子/丝带也一样（`MdxParticle2.frag`、`MdxRibbon.frag`）。

Launcher 的天气用 `StandardMaterial3D` + `DisableFog`，而全场景没有开 Godot 的 `Environment.FogEnabled`，`useFog` 等于空转；`war3_particle.gdshader`、`war3_ribbon*.gdshader` 也没有雾 uniform。地形、悬崖、水面、模型已接上 `atmosphere.gdshaderinc`。

要做：天气材质换成自定义着色器并吃 `atmosphere.gdshaderinc` 的雾；粒子和丝带在片元里调 `war3_apply_fog`。

## 53. w3i 子记录名和客户端还不一致

客户端 `MapInfoParser.cs` 里玩家/势力叫 `MapPlayerInfo` / `MapForceInfo`，雾、天气、水色、光照 tileset 这些环境字段直接平铺在 `MapInfo` 上；Launcher 现在叫 `W3iPlayer` / `W3iForce`，环境单独一层 `W3iEnvironment` 子对象，`War3WaterConfig` / `SkyboxRenderer` / `SceneLighting` 都按 `map.Info.Environment.X` 取值。

要做：两个记录改成客户端名字；环境字段平铺进 `MapInfo`，去掉 `W3iEnvironment` 这层，调用点同步。

## 54. 建筑在雾里被拆掉后仍然立刻消失

客户端把「已探索但看不见」的建筑冻成 CPU 快照，被拆掉时快照还在，直到 `ScoutingMemory` 把这一格重新看见才遗忘。

Launcher 现在会冻结序列和粒子（`UnitSceneComponent.SetFogFrozen`），模型也吃迷雾压暗，但建筑阵亡后没有「冻结快照」：模型停止推进、继续留在地上，重新看见时也不会按客户端逻辑遗忘。

要做：建筑在不可见时阵亡要保留最后一次看到的定格模型，重新可见时再按 `ScoutingMemory` 口径清掉。
