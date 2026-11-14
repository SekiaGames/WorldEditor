# 提高 Launcher 与客户端侧一致化的任务列表

这份清单只记真正会造成流程、画面或行为差异的地方。判断一条值不值得写进来，用 AGENTS.md 里的五把尺子：

1. 两边做同一件事，但执行流程不一样。
2. 两边做同一件事，玩家看到（或听到）的效果不一样，重点是动画、特效、加载和镜头。
3. 客户端已经有现成实现，Launcher 却另写了一套。
4. 明明可以共用代码，Launcher 没有共用。
5. Launcher 的 C# 文件在有对应客户端侧文件时，相对路径、文件名、类名对不上。

读完一条就动手，不要先全面铺开。每条都标了客户端在哪、Launcher 在哪，照着路径能直接翻到。

**完成一条就把它从清单里删掉，序列号不复用；下一条新任务接着往后续。当前清单排到 47。**

## 范围

- 起点是**地图选择界面**。客户端首屏（公告页、主菜单、菜单音乐）、地图文件夹浏览、地图导入、战役列表都不在本轮范围内，Launcher 保持固定地图路径，地图只从 `test/map_gmap` 读。
- 因此「客户端双击 `.w3x` 就能进、Launcher 要先转 gmap」属于既定差异，不记进清单；但**选图界面本身的版式、预览、版本提示、点确定之后的流程**要按客户端对齐。

---

## 一、从选图到进图

## 1. 地图选择界面的壳、背景和预览都还差一截

版式坐标两边其实是一样的（列表面板 `Place(listPanel, 396, 520, ..., 84, 132)`、信息面板 `460×440 @ 500,132`、确定按钮 `454×52 @ 503,597` 都是同值），差在壳和内容：

- **宿主**：客户端用 `FdfUiFactory` 解析 `UI\FrameDef\FrameDef.toc`，`Create("Skirmish")` 当全屏宿主，再把它原有子帧全部隐藏，在宿主上挂自己的左右两栏。Launcher 直接在 `_root` 上手搭，`Generated/Fdf` 里那张 `Skirmish.fdf` 一直没人用。
- **背景**：客户端 `GlueModelLayer` 用 `GlueSpriteLayerBackground` / `TopLeft` / `TopRight` 三项皮肤跑模型层动画；Launcher 只有一张静态 `GlueSpriteLayerBackground` 贴图（失败时退纯色），仓库里没有 `GlueModelLayer` 这个文件。
- **预览**：客户端 `MapPreviewImage.Read` 依次试 `war3mapPreview.tga/blp`、`war3mapMap.blp/tga`，再用 w3i 的 `LoadingScreenModel` 兜底。Launcher 只读 `war3mapMap.png`；gmap 里目前确实只有这张 png，作者自带的 `war3mapPreview` 没有，也没有 blp/tga 解码。
- **版本与警告**：客户端有「保存版本」一行，并按 `MapCompatibilityNotice` 出黄色 1.32 兼容警告，警告出现时描述框从 112 收到 60。Launcher 两行都没有，但 `W3iInfo` 里 `FormatVersion` / `GameVersionNumber` 已经解析好了。
- **排序**：客户端普通地图按文件大小排（`MapLibrary.SortMapsBySize`），Launcher 按文件名。
- **超宽屏**：客户端用 `War3UiViewport` 把界面限制在居中 16:9，Launcher 用 `viewport / 1024` 拉满整窗。
- **Esc**：客户端置 `CancelRequested` 交给宿主处理；Launcher 只 `Cancelled?.Invoke()`，而 `LauncherApp.ShowMapList` 只订阅了 `StartRequested`，按 Esc 没有任何反应。
- **文案**：Launcher 副标题是「从 gmap 中选择一个场景开始游戏」，固定路径下应改成和客户端一致的说法。

**已完成**（2026-11-14）：

1. FDF 宿主接上了：`LoadTemplate(Skirmish.fdf)` + `Create("Skirmish")`，原有 7 个子帧全部隐藏，两栏挂在宿主上。启动日志会打印 `[MAP-UI] Skirmish 宿主已创建`。
2. 「保存版本」和兼容警告两行补上了，判定走新加的 `MapCompatibilityNotice`（和客户端同名同路径），警告出现时描述框收到 60。
3. 列表改成按文件大小排（同大小再按文件名）。
4. 界面改成居中 16:9，超宽屏两侧留黑；`_GuiInput` 的坐标换算跟着改成按控件尺寸。

**还要做**：

5. Esc 给个去处，别再发一个没人接的事件（`Cancelled` 至今没有订阅者）。
6. 预览回退顺序对齐：`war3mapPreview` 需要 Extract 在生成 gmap 时一并导出 png，不在 Launcher 里塞一个 blp 解码器（缺数据改 Extract，这条规矩不破）。
7. `GlueModelLayer` 单独评估，工作量不小：要么移植客户端的模型层渲染，要么明确接受静态背景。

## 2. 点「开始」之后有一段空档，屏幕上还停在刚点过的列表

客户端点开始只是置标志位（`MapSelectionScreen.OnPlayRequest` 设 `PlayRequested`），真正的加载发生在主循环里：先 `DetachMenus()`，再把地图名写进标题，然后 `LoadMap()`（`GameApplication.cs` 的 `Render`）。加载屏是在 `LoadMap` 一开始就建好的。

Launcher 是回调里直接干（`LauncherApp` 里 `Clear(); StartGame(mapPath)`），而 `StartGame` 先把 w3e/w3i/units/doodads/wpm 全解析完（`MapSource.Open`），才 `Clear()` 旧菜单、`new War3GameScene`。解析期间画面还停在已经点过的那张列表上。

要做：先开加载屏再解析，和客户端一个顺序。

## 3. 加载屏少了缩略图和背景模型，节奏也不一样

客户端的加载屏由宿主持有，`Report` 里直接画一帧再 SwapBuffers（`War3LoadingScreenRenderer.Report`），`using` 完就释放，时长等于真实加载时长；`MinimapImage` 里塞的是地图缩略图（就是选图页那张），背景优先用 w3i 的 `LoadingScreenModel` 或 `UI\WorldEditData.txt` 的 `[LoadingScreens]`，FDF 绑失败还有一条直绘回退。

Launcher 的加载屏是场景里的一个 `Control`，`Report` 只 `QueueRedraw`；另外硬加了「至少显示 1.2 秒 + 稳定 3 帧 + 0.5 秒淡出」（`Lifecycle.cs` 的 `LoadHoldSeconds`、`War3LoadingScreenRenderer` 的 `_remaining` / `_settleFrames`）。缩略图完全没有，背景是按 skin 里模型路径取目录硬拼四张 `Loading-*.png`；`BuildFrameTree` 在 `_loading == null` 时直接 return，但 `LayoutTexts` 照样解引用，FDF 绑不出来会空引用——客户端这时是回退直绘。

要做：缩略图接上（选图页解码一次就能复用），背景模型按 w3i / LoadingScreens 表来，1.2 秒那条规则去掉或改成「不早于真实加载完成」，补回 FDF 失败时的直绘路径。

## 4. 加载阶段表和建世界的顺序都变了

客户端的阶段是枚举驱动的（`Scenes/War3GameScene/Lifecycle.cs` 的 `Begin → Terrain → Textures → TerrainPresentation → SceneResources → WorldConfiguration → PlacedUnits → MapAot → MapScript → PresentationPreload → Pathing → Selection → Behaviours`），打点在 .10/.22/.34/.45/.56/.66/.73/.76/.86/.93/.98。

Launcher 是 `SetupSteps` 里手写 async 顺序，打点 .10/.22/.34/.45/.66/.80/.90/.98。三处差别：

- 少了 .56「合并地图物编与界面配置」、.73「准备地图脚本」、.76「执行地图脚本与触发器」；
- 「构建寻路与碰撞网格」从客户端的 .93 提前到 .80，跑在预热之前；
- 单位在 .45 就生成，那时 `_world` 还是 null，落点只能退回纯地形高度（`Units.cs` 里 `_world != null ? _world.SurfaceY(...) : MapWorldBuilder.HeightAt(...)`），客户端是 .56 先建 World、.66 再 `_world.Spawn`。

要做：阶段名和打点对齐；World 先于单位；脚本那两格虽然不跑 JASS，也要有对应位置（哪怕是空阶段），否则后面接脚本时又要重排。

## 5. 加载出错时，一边中止一边带病进门

客户端只对 `SceneResources..Selection` 区间吞异常并跳到 `Behaviours`，其余阶段直接抛；`Scene.ContinueLoad` 会先 `Dispose` 再抛；`GameApplication.LoadMap` 没有 try/catch，最终由 `AppDomain.UnhandledException` 写 `CrashLog`。

Launcher 的 `RunSetupAsync` 捕所有异常，只 `GD.PushError`，然后照样 `Finish()` 并发 `Loaded`。

要做：区分「可跳过」和「必须中止」，中止时给玩家一个明确提示，别让人对着残缺场景猜。

---

## 二、镜头

## 6. 电影镜头和脚本镜头是空的

客户端 `War3Camera` 能装 MDX 的 `CAMS` 轨道（`SetCinematicModel` / `CinematicModelCamera.Step`），每帧直写位置、目标、FOV、近远裁剪和滚转，并且跳过地形钳制；`SetField` / `PanTo` / `SetTargetController` / `SetOrientController` / `SetNoise` / `SetShake` / `ResetToGameCamera` 都有实际消费者。

Launcher 这些方法都在，但除 `SetSoftFollowController` 外没有任何调用者。过场里镜头不动，`CameraSetField`、`PanCameraToTimed`、镜头抖动全都不产生画面变化。

要做：`CinematicModelCamera` 补进来并接上轨道求值；脚本镜头接口先接到作弊/探针上验证，等 JASS 进来再接 JASS。

## 7. 镜头不跟随、不吃设置、取景也和客户端不同

四件事：

- **跟随**：客户端 `War3CameraComponent` 每帧驱动肖像跟随、锁定跟随、总跟随和标记跟随，总跟随在 `CameraFollowTrack` 里按选择队列挑目标、过滤建筑/敌方/中立，并且喂的是渲染插值坐标。Launcher 只有键盘、边缘滚屏和拖拽，长按头像时临时 `SetSoftFollowController(unit)`，拿到的是 20Hz 仿真坐标，高帧率下是一格一格跳；松手立刻停止，客户端要滑到单位附近才解绑。
- **设置**：客户端有 `DistanceScale`（整体拉远/拉近）和 `PanSensitivity`；Launcher 全仓没有这两个概念，只有 6 档写死的距离，改灵敏度没反应。
- **相机档位来源**：客户端按 `UI\MiscData.txt` 逐层合并（基础库→War3x→War3Patch→当前地图），地图可以覆盖 `[Camera]`；Launcher 只读构建期生成的 `GeneratedTexts` 固定表，自定义地图改过相机就看不出来。
- **取景**：客户端的 `CameraBounds` 直接用 w3i，边界外靠 `CameraMargins` 放宽；Launcher 把 w3i 边界和地形网格矩形求了交集，带外围装饰的地图会被裁掉一截。开局客户端放在边界中心由脚本接管，Launcher 直接 `Focus` 出生点。世界投影客户端是恒定 4:3 视锥，Launcher 走 Godot 相机默认的 `KeepHeight`，16:9 下横向视野更宽。地形高度采样也反了：客户端按渲染网格同一条 TL–BR 三角面插值并且会查桥面，Launcher 是四角双线性、不查桥。

要做：补跟随和两个设置项；相机档位改成读地图覆盖层；边界不要取交集；投影和高度采样向客户端看齐。

---

## 三、光与环境

## 8. 整局不参与昼夜光照，也没有局部光

客户端 `SceneLighting` 每次 `Update(gameHour)` 从 DNC 模型插值方向光、漫反射和环境光，`Lifecycle` 按 0.05 小时节流刷新，还接受 `SetTerrainLighting` / `SetUnitLighting`；地形和悬崖把光照烘进顶点色，单位和装饰物走 `DoodadRenderer.Draw(..., light, ...)`。`DynamicLight` 再把 MDX 灯光上传成最多 8 盏动态光，在顶点着色器里按衰减累加。

Launcher 场景里没有任何 `DirectionalLight3D`（只有头像视口里有一盏），`SkyboxRenderer` 还显式关掉环境光和反射光；世界着色器全是 `unshaded` + 写死的光方向（`terrain_map.gdshader` 里 `LIGHT_DIRECTION` 加 `+0.45`），模型着色器的 `war3_lighting_enabled` 全仓没有一处置 true，`war3_light_data` 也没人上传。`Lifecycle` 里的 `_todHours` 只用来判断白天黑夜和画时钟。

要做：先把昼夜光照接上（哪怕先只做方向和颜色），再补 MDX 动态光。这条不做，「黄昏」「夜里」「火把照亮地面」这些观感永远对不上。

## 9. 地形雾没人消费

客户端从 w3i 解析雾参数（`FogSettings`），`SetTerrainFogEx` 也能改，地形、悬崖、水、岸浪、天气都把 `uFogColor` / `uFogParams` 传进着色器。

Launcher 的 `W3iInfo` 明明解析了 `FogHeightStart/End/Density/FogColorBgra`，全仓却没有一处读取；三个世界着色器都写死 `fog_disabled`，`SkyboxRenderer` 也没开 `FogEnabled`。

要做：把已经解析出来的字段接到 Godot 的环境雾或着色器上，带雾的图（沼泽、达拉然、剧情图）一眼就能看出来。

## 10. 天空没有太阳月亮，选天空的依据也不对

客户端单独加载 `Environment\Sky\Sky\skyLight.mdx`（日月就在这个模型里），按 `SunMoonAngle(timeOfDay)` 每 24 小时转一圈跟着相机走；天空用 `GameplayConfig.SkyboxType`（war3mapMisc 的 0..14 索引）选，`SetSkyModel` 可以覆盖。

Launcher 只加载天空壳（`SkyDomeRenderer`），不加载 `skyLight`（Extract 里其实有 `skyLight.tscn`）；天空按地形 tileset 字符猜（`'N'→FoggedSky`、`'J'→BlizzardSky`、`'A'/'B'/'O'→Sky`），自定义天空和 war3mapMisc 都不生效。另外 Launcher 多了一层 `PanoramaSkyMaterial` 全景底，天空壳半透明处会露出和模型不一致的亮带。

要做：加载 `skyLight` 并按时辰旋转；天空改按 `SkyboxType` 选；全景底只在完全没有天空壳时兜底。

## 11. 地形贴图和悬崖有四处硬伤

- **变体子块**：客户端普通 4×4 图集只取 `variation & 1`（layer 0 或 15），extended 8×4 才用 `16+variation`；Launcher 无条件 `16+variation`，草地和路缘的接缝花纹比原版更花，混合边界形状也不一样。
- **悬崖模型目录**：客户端读 `CliffTypes.slk` 的 `cliffModelDir` / `rampModelDir`；Launcher 写死 `Doodads/Terrain/Cliffs/` 和 `CliffTrans/`（`CliffRenderer` 里两个常量），城市悬崖这类直接找不到模型，地面留空洞。
- **崖上/崖下贴图**：客户端分两趟并按「是否最低层」在 `GroundTexture` / `UpperTexture` 之间选；Launcher 只读一个 `groundTile`，崖顶崖底用同一张贴图。
- **非法斜坡**：客户端的 `TerrainRenderState` 有 `DisallowHeight3Ramps`、被悬崖挡住的 ramp 判无效、无效时回落成悬崖模型；Launcher 只判断「四角 ramp 且高低不同」，非法斜坡会画成被拉长的斜坡模型。另外过渡格该剔除的地面没剔（`TerrainGridFactory` 的 `_cliffTransitions[k,j]` 仍铺地面），斜面上会出现地面穿出模型。

要做：按 `CliffTypes.slk` 走，补齐上层贴图和非法斜坡修正，过渡格剔除。

## 12. 地形高度改不了

客户端支持运行时改顶点高度和法线（`TerrainRenderer` 的增量变形、`TerrainSurface.SampleHeightDeformation`），`TerrainDeformCrater` 一类会真的在地形上挖坑。

Launcher 没有 deformation，地形网格一次性烘死。

要做：地形网格要能被改写；不然地震、深渊这类技能单位会悬空或陷进去。

## 13. 地图自带的阴影图读错路径，树影也各画各的

客户端的 `TerrainShadowMap` 从当前地图归档读 `war3map.shd`，纹理按 `(W-1)*4 × (H-1)*4` 建（32 世界单位一个 texel），值折半；每棵树的 `ReplaceableTextures\Shadows\*` 也按 alpha 盖进同一张图，取值取 `max(底图, 树影×强度)`，强度随太阳高度在 19 点后归零。

Launcher 读的是固定路径 `res://gmap/map/war3map.shd`，而 gmap 是把 `war3map.shd` 打包在包内的（实测两张图都有），按固定路径永远读不到，所以进图只打印「阴影图缺失」；就算读到了，也按 `W×H` 建纹理并且只拷前 W×H 字节，而实际文件是 `(W-1)*4 × (H-1)*4`，等于取左上角一小块拉伸到全图。树影则是每棵树一个黑色 Quad（alpha 0.65、固定偏移 `(-0.2w, 2, 0.2h)`），密林处多片叠加越来越黑，夜里也还在，斜坡上的影子是悬空平板。

要做：路径改成从 gmap 包内读，尺寸按客户端的算法；树影并进同一张阴影图，并跟着太阳高度走。

## 14. 战争迷雾是一块悬在 320 高的板子

客户端不画雾：`FogOfWarRenderer` 只维护一张 R8 可见性纹理，由地形、悬崖、单位、水、岸浪、天气各自在片元里采样（`FogOfWarCommon.glsl` 里 `rgb=mix(rgb,0,shadow)`），三态 255/128/0，`FogVisualBuffer` 每 30ms 逼近 9 级，所以视野进出是渐变的。

Launcher 把雾做成一张贴地网格（`Src/World/FogOfWar.cs`，`FogHeight=320`），每个角点 `HeightAt+320`，用一张 RGBA 贴图盖上去。后果很明显：低角度看，前面的山脊会把雾挡住、后面反而亮着；悬崖处雾面折成斜面甚至竖幕；高过 320 的树冠和建筑从黑雾里穿出来。另外 `CellsPerTexel=2`（一格 256）、材质 `Linear`，边界比客户端糊一倍；两态直接写像素，没有淡入淡出；已探索区用深蓝 `(16,16,32)` 叠 170/255，客户端是乘 50% 黑；水、岸浪、悬崖着色器里根本没有迷雾 uniform。

要做：把雾改成着色器采样，别用几何体；分辨率回到 128；补灰雾渐变和正确的混色。

## 15. 进雾之后的记忆：一个定格，一个只是暂停

客户端的建筑看不见但已探索时，`CharacterRenderer.FogMemory` 会抓一份 CPU 快照（模型矩阵、骨骼调色板、序列、帧、全局时间、队伍色），把粒子/丝带实例移除，用冻结时间重画，模型仍然吃迷雾所以是灰的；重新看到那块空地时 `ScoutingMemory` 才遗忘。装饰物也一样有可见/腐地/序列/帧的记忆。

Launcher 的 `SetFogFrozen` 只是 `Player.Pause()` + `Root.Visible = true`，模型还活着：升级换模型、队伍色、自带粒子照常变化，而且保持原色全亮，烟囱还在冒烟；建筑一旦被拆掉就 `QueueFree`，灰雾里立刻消失。装饰物完全没有迷雾处理。

要做：建筑和装饰物都要「冻住最后看到的样子」，并吃到迷雾染色。

## 16. 腐地是羽化遮罩，不是换贴图

客户端把整格地表换成 blight 地表贴图，走正常的四角混合，而且只在被观察过的格显示（`ScoutedBlight` 带按玩家的记忆位掩码）。

Launcher 生成一格一字节的 `blight_mask` 加一张贴图，在着色器里按世界 UV `mix`，每 0.5 秒扫全部单位盖章，没有观察者记忆。

要做：改成逐格贴图替换（块状硬边），并区分「世界已经腐化」和「这个玩家看到过」。

## 17. 水面只学了形状，没学参数

- UV 密度：客户端用 `Water.slk` 的 `cells` 列，纹理每 N 格重复一次；Launcher 按格号直接当 UV，等于 `cells=1`，大水面波纹明显更碎。
- 深浅阈值：客户端用 `MinimumDepth` / `DeepLevel` / `MaximumDepth` 分段插值，并乘地图水色 `BaseColor`；Launcher 写死 `5/64`、`27/64`、`0.5`、`0.0625`，也不读地图水色。
- 动画相位：客户端 `(elapsed*rate + texOffset) % count`，Launcher `(1 + elapsed*rate) % count`，起点差一帧以上。
- 光照与雾：客户端水面吃方向光、环境光、距离雾和战争迷雾；Launcher 是 `unshaded, fog_disabled`，还让波纹 alpha 二次调制不透明度，某些帧比原版更透。

要做：`Water.slk` 的字段补齐（`War3WaterConfig` 现在连 `cells` / `alphaMode` / `lighting` 都没有），水面接光照和雾。

## 18. 岸浪逐格实例化，偶发大浪没了

客户端把岸浪模型的 `ParticleEmitter2` 展开成实例化粒子，保留段颜色、透明度、缩放、重力、`TimeMiddle`，还有 7% 概率的 rare `Stand` 脉冲。

Launcher 的 inside/straight/outside 判定和旋转常量跟客户端一致，但每格都 `Instantiate` 一个完整场景，没有 rare 序列/脉冲。

要做：至少把偶发大浪补上；实例化方式可以后面再优化。

## 19. 只有全局天气，跨一格整片雨雪重来

客户端给每个 `AddWeatherEffect` 建一个带 rect 的 `WeatherRenderer`，可以多天气叠加，粒子相位由 cell 索引确定性生成，只在相机跨 cell 时重建实例数据。

Launcher 只按 `map.Info?.Environment?.GlobalWeather` 建一次，`WeatherRenderer` 里的 `_hasArea` 分支永远 false；`baseCol/baseRow` 一变就对每个 cell `emitting Restart()`，镜头每跨 512 单位雨就断一下。另外一个语义错误：`var` 列在客户端是速度变化（`Speed*(1+Variation*rand)`），Launcher 用 `atan(Variance)` 喂给了 `Spread`，于是大雨变成扇形散开。

要做：区域天气接上；跨 cell 不要 Restart；`var` 按速度变化用。

## 20. 地面贴花和地图边缘都是空的

客户端的 `War3SplatSystem` 支持 `SPL`/`UBR` 与 `CreateUberSplat`，带 Birth/Pause/Decay 颜色和 BlendMode，用贴地 Quad 批量画；建筑底座（`UnitUI.uberSplat`）也走这条管线，拆掉时会淡出。地图四边还有 `MapVoidRenderer` 生成贴边裙边和外圈，边缘不会看到天空。

Launcher 全仓没有 splat/decal，也没有 void：建筑脚下没有底座土圈，技能的地面符文、传送门地毯、焦痕、血迹全都不出现；镜头拉到地图边缘会直接看到地形网格外的天空或水面断口。

要做：先补建筑底座和地图边缘裙边（这两项最容易被一眼看出），再补技能 splat。

---

## 四、单位与战斗表现

## 21. 血条、选中圈、影子三块贴片都挂错了地方

- **血条**：客户端按 `SelectionDiameter` 换算成屏幕像素宽、按包围盒决定高度，所有可见存活非无敌单位都画，敌人恒红、有蓝条；Launcher 是世界尺寸 Quad（56 / 建筑 90），只有受伤或被选中才画，没有蓝条，颜色是 0.6/0.3 两档离散色。而且 `BarRoot` 是 `Root` 的子节点、`Root` 上已经设过缩放，模型缩放不是 1 的单位血条离头顶的距离不对；建筑条宽 90，填充偏移却还按 28 算，掉血时填充会往右漂。
- **选中圈**：客户端读 `SelectionRadius/Height`，用原版 `SelectionCircleMed.blp` 铺在地形三角面上，坡地贴合，敌红友绿；Launcher 是固定 32 段程序化圆环（内 40 外 52），颜色恒为 `3ce03c`，`y=6` 固定在 `Root` 下，所有兵种一样大，坡上会插地或悬空。
- **影子**：客户端是世界坐标轴对齐的地面贴花、逐顶点贴地，建筑按 30%/70% 锚点偏移；Launcher 的影子是 `Root` 的子节点，`Root` 每帧按朝向旋转，所以单位一转身影子跟着转，建筑影子以模型原点为中心和墙基错位；SLK 里只写了 `unitShadow` 没写尺寸的单位，Launcher 会回退成 64+ 世界单位的巨大黑影。

要做：三块拆开做，血条先做（最容易看见），再选中圈形状和贴地，再影子脱离 `Root`。

## 22. 队伍色回绕，幻象不变色，虚无没反应

- 客户端队伍色用 `TeamColorXX.blp`（0–27 号），替身 ID 2 的 `TeamGlowXX` 也跟着玩家色变；Launcher 只内置 12 个纯色，`ColorOf(player)` 取 `player % 12`，于是 12–23 号（中立、野怪）会回绕成玩家亮色——**野怪变成鲜红或亮蓝**。队伍色区域在 shader 里被平涂成纯色，没有原版的明暗纹理；光晕固定一种颜色。
- 客户端的幻象按观察者关系染成 `(20,96,255)` 蓝；Launcher 的 `CreateIllusion` 只复制单位，幻象和真身一模一样。
- 客户端 `UnitStateColor` 处理虚无（淡绿半透明）、`SetUnitVertexColor`、死亡 alpha；Launcher 模拟层有 `StateMod.Ethereal` 和 `VertexColorRgba` 字段，渲染从不消费，被放逐的单位外观完全不变。
- 隐身：两边都压到 127/255，但客户端只作用于模型，Launcher 用 `GeometryInstance3D.Transparency` 把血条、选中圈、影子一起弄半透明。

要做：队伍色表补到 28 号并改成贴图替换；幻象、虚无、隐身的作用范围对齐。

## 23. 飞行单位贴地站着，转身是 20Hz 一跳，炮塔不会转

客户端模型 Z = 地形高 + `RenderFlyHeight`，飞行单位还有 MoveFloor 下限，建筑 +12 防 z-fighting；按 `ElevationSampleRadius/MaxPitch/MaxRoll` 采四点做俯仰横滚；朝向由 `FacingInterpolation` 逐渲染帧追；带 `LookAtBone` 的单位每帧把炮塔/龙头转向目标。

Launcher 的 `Spawn` 把模型 Y 设成 `SurfaceY(...)`，`FlyHeight` 只给弹道用过（`Unit.Compat.cs` 里定义、`AbilityMissileEffect` 里读），所以**狮鹫、双足飞龙、冰龙全都贴地**；没有俯仰横滚；朝向在 20Hz 仿真步里直接写 `Root.Rotation`，角速度上限还是 14 rad/s（客户端 20）；完全没有 look-at，箭塔打哪都朝默认方向。

要做：飞行高度、坡面姿态、朝向插值、look-at 四件，先做飞行高度（一眼能看出）。

## 24. 战斗动作：攻击只播一次，攻速不影响动作

- 客户端的动画由 `UpdateUnitAnimation` 统一管优先级，每次出手 `Set("Attack")` + `Queue("Stand", Ready)`，动画速率跟 `AttackSpeedFactor` 走，切序列时还做姿态混合。
- Launcher 的 `Play` 里有 `if (found == CurrentAnimation) return;`，所以站桩连续攻击时第一刀之后一直停在攻击末帧，伤害照常结算；`SpeedScale` 只在 `Walk` 时设置，嗜血/攻速升级后动作节奏不变；没有 `Stand Ready`（进入战斗还没出手时的举刀待机），切换是硬切没有过渡；`SelectSequence` 返回第一个匹配项，所以同兵种待机完全同步、永远是同一个 `Stand` 变体（客户端按 rarity 随机）。

要做：`Play` 允许重播同一序列，攻速接上，补 Ready 和姿态混合，待机变体加随机。

## 25. 死亡、建造、升级的动作断在半路

- **死亡**：客户端 Death 播完按 `DeathAnimTime` 接 `Decay`（Flesh/Bone）或英雄的 `Dissipate`，`Fit` 压缩时长；Launcher 只 `Play("Death")`，非循环停在末帧，88 秒后直接隐藏——尸体不下沉不腐烂，英雄没有消散。
- **建造**：客户端有专门的 `FindConstructionBirthSequence`（避免暗夜主城误选 `stand work ... birth`）；Launcher 是 `PickAnimation("Birth")`，精确匹配不到就取任意以 Birth 开头的，暗夜主城这类可能选错变体。
- **升级**：客户端按目标类型选 `Birth Upgrade First/Second` 并按进度 Seek；Launcher 升级期间外观完全不变，进度条走完「啪」地整栋换掉。
- **金矿**：客户端按缠绕金矿里的采集者数量选 `Stand Work First..Fifth`，显示 1–5 个小精灵；Launcher 没有这段。
- **关键帧插值**：客户端支持 step/linear/Hermite/Bezier，Launcher 转换时只写 TRS 轨道，Godot 走线性，贝塞尔缓入缓出丢了，呼吸/收势这类动作发死。
- **时间缩放**：客户端有 `AnimationTimeScale`（JASS `SetUnitTimeScale`）参与时钟；Launcher 没有。

要做：死亡链、升级生长、金矿小精灵先补；切线信息要不要保留属于 Extract 侧的事，单开一条更合适。

## 26. 特效只认一张手写表，`World.Effects` 根本不画

客户端的特效完全数据驱动：从物编按等级读 `CasterArt/TargetArt/EffectArt/AreaEffectArt` 和挂点，`world.Effects` 里的 `SpecialEffect`、Buff、`ImageVisual` 统一收集绘制，出生/死亡序列、脚本颜色都在里面；Buff 视觉按 `VisualModelPath` 生成，同一挂点的光环按 `AuraKey` 去重，飞行单位按 `VisualAllowedWhileAirborne` 过滤。

Launcher 在手写表 `EffectArtTable` 里维护了 147 条「技能码 → 目录/后缀/挂点/是否持续」，按命名约定拼模型路径；`World` 里的 `Effects` 集合和 `TickEffects` 都在，但 Game 层**没有任何一处渲染 `Effect.VisualModelPath`**——`AddSpecialEffect`、`AddSpecialEffectTarget` 和 `ChainLightningEffect` 的 `TargetArt` 全都不可见。Buff 循环也没有，只有表里标了 `Stick=true` 的少数几个技能挂一个模型。挂点只用三档高度比例（overhead 0.85H / target 0.55H / 其余 0），所以刀光固定在身体中轴、不跟手。特效总数还有 60 个的硬上限，超出直接丢弃。

要做：先把 `world.Effects` 接上渲染（这一步能一次性救回一大片缺失特效），再把 `EffectArtTable` 逐步换成按物编读 Art 字段，挂点改成骨骼。

## 27. 粒子、丝带和模型事件都缺

- **粒子**：客户端逐实例模拟位置/速度/重力/寿命/模型空间，支持替换贴图、实例染色、队伍色粒子、squirt 发射、头/尾；Launcher 转换时烘成 `GPUParticles3D`，数量硬夹 512，参数取待机帧快照，没有队伍色/实例色，重粒子明显更稀。
- **丝带**：客户端有完整运行时丝带（发射时钟、历史点、上下高度、重力、颜色、纹理行列）；Launcher 只给 `gravity>0` 的丝带烘一张静态扫掠网格，其余只留 `metadata/war3_ribbon_*`，而且 Launcher 侧没有代码消费这些元数据——剑刃拖尾、魔法轨迹基本不出现。
- **模型事件**：客户端的 `ModelEventFired` 会解析 `SPN`/`SPL`/`UBR`/`FPT` 生成声音、地面溅射或子特效；Launcher 转换没有事件节点，Game 层也没有派发，武器挥动音、脚步、出生帧特效全没有。

要做：丝带优先（视觉损失最大），然后粒子参数和实例色，最后模型事件（要动 Extract 侧）。

## 28. 闪电是 20Hz 随机折线，弹道不俯仰、不裁剪

- **闪电**：客户端每帧按 `id/index/time` 确定性抖动、UV 滚动、段数上限 64、颜色乘实例 RGBA、端点支持 `StartHeight/EndHeight`；Launcher 每 50ms 用 `GD.Randf()` 重建，最多 24 条，UV 不滚动，端点固定在「单位位置 + 0.55 身高」，JASS 建的闪电完全不显示。
- **弹道**：客户端用模拟层的分段路径加 `RenderArcOffset`、`VisualPitch`（弹体沿抛物线俯仰）、`BulletDeathTime` 和迷雾可见性；Launcher 自己算抛物线、只设 Yaw，技能弹道弧强制 0，弹道在黑雾里照样可见。
- **占位与引导**：客户端模型缺失只记日志；Launcher 丢一颗金色 `SphereMesh`。客户端引导技能用技能自己的 `AreaEffectArt`；Launcher 自己画一个半透明蓝圆盘，每波闪一下再随机落一颗特效。

要做：闪电改成确定性抖动 + UV 滚动；弹道补俯仰和迷雾裁剪；把黄球和蓝盘去掉。

## 29. 飘字是贴在 3D 世界里的字，而且是「Miss」

客户端把伤害/资源数字（`World.Floats`）和 JASS `CreateTextTag` 都交给 `TextTagFrame` 在屏幕空间投影画，字号按 framebuffer 高度换算成像素，速度/寿命/淡出点来自 `UI\MiscData` 可覆盖的样式表，锚点是「地形高 + 飞行高度」，之后在屏幕上飘、不跟单位。

Launcher 用 `Label3D`（世界单位、Billboard），拉远镜头字就变小；落空文本是 **Miss**（灰、0.9s），客户端是 **未命中**（红、3s）；普通伤害寿命 0.95s 对客户端 0.4s，暴击 1.6s 对 2s；木材赏金客户端默认 alpha=0 不显示，Launcher 会飘一个金色 `+N`；暗影突袭的绿色 `N/N!` 完全没有；可见性只判迷雾不判隐身，所以打隐身单位会漏数字。

要做：改成屏幕空间文字并接样式表；文本、颜色、时长逐项对齐。

## 30. 进图不预载，第一次出现都卡一下

客户端的 loading 阶段会预载：地图现有单位、参战玩家的建造/训练/升级树、写死的 timer 兵线和野怪、每个单位的挂件模型、头像、所有武器弹道、技能的 Caster/Target/Special/Effect/Area 艺术、Buff 视觉模型（`Presentation.PreloadPresentationResources` + `PresentationPreloadCatalog`），并由 `PresentationResourceRegistry` 统一释放。

Launcher 在 `.90 预热单位与界面资源` 这一步后面没有预载循环，`Assets.LoadScene` 直接 `ResourceLoader.Load`。所以第一次训练某兵种、第一次放某技能、第一次射某种箭、第一次点开某英雄头像都会明显卡一下。

要做：把客户端的预载名单搬过来，至少覆盖「本局会出的兵」和「本局技能表」。

---

## 五、声音

## 31. 单位应答变成 3D 音，命中音只认木头和肉

- **应答**：客户端的 `War3AckSoundService` 有完整节流——同一单位连续选择计数、Pissed 索引、每单位「语音时长 +250ms」冷却、同名 `ResponseUsername` 去重，并返回时长给头像做说话动作；应答是 `Play2D`，固定在监听者中心。Launcher 只有「12 秒内点 4 次切 Pissed」，没有节流，语音会叠在一起互相打断；而且走 `AudioStreamPlayer3D` 放在单位坐标 +80 高——点远处单位时「Yes sir」又小又偏声道。
- **命中音**：客户端按 `weaponSound + 目标 ArmorType` 选 Flesh/Metal/Wood/Ethereal/Stone；Launcher 只有 `target.IsBuilding ? "Wood" : "Flesh"`，打步兵盾和骑士播的是砍肉声。
- **施法音**：客户端按 `AbilitySounds.slk` 的 `Aas/Asp/Ahe/Acs/Acst` 查；Launcher 没有实现，反而每次施法播一遍 **What** 选择语音。
- **界面音效**：客户端有 InterfaceClick、MapPing、SubGroupSelectionChange、InterfaceError、ItemGet/ItemDrop、JobDone、UpgradeComplete、NoGold/NoLumber/NoFood/NoMana（分种族）、CantPlace、InventoryFull、TownAttack；Launcher 只剩 `InterfaceError`、`ResearchComplete{Race}`、`UpgradeComplete{Race}`——点按钮没有「咔」声，资源不足只会「咚」一声，遇袭没有警报。
- **冷却**：Launcher 的 `_clock += 0.016f` 只在调用 `Play` 时推进，0.08s 冷却实际是「每 5 次调用才响一次」，多个单位同时砍树只有 1/5 有声。
- **音高**：客户端的 `RANDOMPITCH` + `PitchVariance` 有自然变化，Launcher 解析了 `RandomPitch` 却没用，种子还写死 `20261111`。

要做：应答先改回 2D 并补节流（这两步最容易被听出来），再按材质补命中音、补界面音效。

## 32. 音乐无缝连播，也没有脚步和建筑循环音

- **音乐**：客户端曲间有 20 秒间隔，支持主题音乐叠加并压低背景、淡入淡出、`SetMapMusic/PlayMusic/StopMusic`、三层音量；Launcher 播完立刻下一首，`AudioStreamPlayer` 固定 `-10dB`，没有淡入淡出、没有脚本接口。
- **循环音**：客户端的 `War3LoopingSoundService` 按 `MovementSoundLabel` 播脚步并跟随位置，建筑建造时播 `BuildingSoundLabel`；Launcher 没有循环音服务，行军完全没有脚步声。
- **声部**：客户端 24 个系统声部、同文件最多 3 个、按 channel 配额和优先级抢占、每条声音有自己的 Min/MaxDistance；Launcher 16 个固定 `AudioStreamPlayer3D`，全部 `UnitSize=900 / MaxDistance=4500 / PanningStrength=0.6`，表里读出来的 Min/MaxDistance 从来没用，满了就按「播放位置最大」抢占。
- **音量**：客户端有 `SoundVolume` / `MusicVolume` 并按 Unit/Combat/Ui/Effect/Ambient 分通道；Launcher 没有任何音量设置。
- **表覆盖**：客户端加载 12 类（ack/ui/AnimSounds/Dialog/MIDI/Environment/Portrait/EAX 等），Launcher 合并成 5 张，`AnimSounds` 和 `Dialog` 没有源。

要做：曲间间隔和淡入淡出，脚步循环音，然后音量设置（和 36 的选项界面一起做）。

---

## 六、界面

## 33. 所有提示语都石沉大海

`MeleeHudController.SetHint` 只做 `_hint = text`，而 `_hint` 全工程除了声明和这一行赋值没有任何读者；`SetQueue(string)`、`SetClock(string)` 是空方法体，`SetPlayerInfo`、`SetBuffs`、`SetUnitSummary` 只写字段没人读。

结果是：资源不足点建造、修理由非农民点残血建筑、Ctrl+1 存编队、子组切换、放置失败——**73 处调用，一句提示都看不到**。客户端对应位置是 `CommandErrorFrame`（黄字、在 console 上方、3 秒 + 每字符 0.1 秒）。

信息面板的悬停说明也缺：客户端按帧名分派（金币/木材/人口/维修费、攻击力/护甲、三围、经验条、8 个 Buff 图标），Launcher 的悬停处理只认 `ButtonFrame`，而资源图标、数值标签、Buff 图标都不是 `ButtonFrame`，所以悬停什么都没有。`TooltipFrame` 本身也退化了：客户端有 `ShowEx(title, body, ext, costs)`、四个成本图标槽、分段富文本、跟随锚点、屏幕边缘内缩、单位血蓝条，Launcher 是固定 280 宽纯色底 + 按 34 字符硬折行。

要做：先让 `SetHint` 有地方显示（或换成一个 `CommandErrorFrame` 等价物），再补信息面板悬停，最后升级 `TooltipFrame`。

## 34. 命令卡少了四个功能格，混选时只剩通用命令

客户端 `CommandCardBuilder` 是 5 个 partial 加一整套原生状态机：技能卡第 (3,1) 是「学习技能」（`BTNSkillz`、热键 `O`、角标显示剩余点数），点开后按 `Researchbuttonpos` 重排、第 12 格是「返回」；第 9 格是「建造」（`B`）；法术书有 `OpenSpellBook` / `BackFromSpellBook`；目标选择或放置模式下第 12 格换成 `BTNCancel`；混选时按成员 `CollectCommands` 聚合成一格并显示数量。

Launcher 的 `CommandCardBuilder` 只有 103 行，把 12 个槽刷成按钮就完了：

- 没有学习页，学习/升级混在技能图标上点「+」（`QuickCast.cs` 的 `LearnEntry`，`Cost="+"`），技能格位按 `HeroAbilList` 顺序硬排，和物编 `Buttonpos` 无关；
- 没有建造页，农民的建筑直接铺满 12 格，兽族/暗夜/不死建筑一多就放不下，也没有「返回」；
- 没有法术书，带法术书的单位和物品点了没反应；
- 没有取消格，选中技能后只能右键或 Esc 退出；
- 混选时 `BuildCommandEntries` 只走 `_selected[0]`，加 5 个通用命令就 `break`——混选 6 个步兵 + 2 个火枪，命令卡上没有任何技能。

要做：按客户端拆 partial，先把「学习 / 建造 / 取消 / 聚合」四件做了，法术书随后。

## 35. 命令卡的状态基本没画，热键只有一半能用

- **状态**：客户端 `ButtonFrame.SetCooldown(...)` 会触发冷却结束闪光、`AutoCastHighlight` 画自动施法绿框、`IconColor` / `DimWhenDisabled` 画禁用变暗；Launcher 直接 `button.Cooldown = ...`，上面三个都没设，所以冷却结束没有闪白、开自动施法按钮外观不变、禁用按钮不变暗。
- **槽位**：客户端 `FindNativeSlot` 是「向 11 找空位、再往 0 回退、满卡就丢」；Launcher 是「从请求位往后找第一个空位，找不到就 break」。同一份物编配置，两边第 3/8/11 格放的可能是不同技能。
- **热键**：客户端按按钮上真正显示的 `Hotkey` 分发（`CommandAtHotkey`），所以训练、研究、建造、升级都能用键盘；Launcher 的 `TrySelectionHotkey` 完全不看命令卡，只认 `ability.Hotkey` 和硬编码的 M/S/H/A/P，而且遇到建筑直接 `return false`。**命令卡上的字母是纯装饰**。
- **可用性**：客户端有 `NativeManaAdmission`、`NativeSpellOrderAdmission`、`NativeSelectionCommandPolicy`（`SingleRecipient` / `SubgroupOnly`）；Launcher 的判断写死在 `QuickCast.cs` 里，`ability.Ready && (toggledOn || hero.Mana >= ability.ManaCost)`，带法力修正符的单位会出现「亮着却放不出」。

要做：先把热键改成读按钮 `Hotkey`，再补三个状态，最后槽位算法和可用性判定。

## 36. Esc 语义相反，顶部三个按钮点了没反应，也没有选项界面

- **Esc**：客户端 Esc 是分层取消（取消待定命令 → 退出学习页），菜单开关是 F10；Launcher 里 Esc 第一件事就是 `_escMenu.Open()`，而且没有 F10。正在放置建筑时按 Esc，客户端取消放置，Launcher 弹菜单、关掉菜单后幽灵建筑还在。F4–F7（选英雄）、Backspace（循环主城）、PageUp/Down（缩放）、Ctrl+C/Alt+C（居中）、Alt+F（阵型）、Alt+G/T/A（小地图）也都没有。
- **Esc 菜单本体**：客户端是 FDF 的 `EscMenuMainPanel` 强类型绑定，隐藏存档/读档/帮助/重开，保留选项/结束游戏/返回游戏；Launcher 用 `ColorRect + Label + Button` 手搓，只有「继续/暂停/返回列表」，坐标还写死在 1600×900，1920×1080 下遮罩只盖左上角。
- **顶部按钮**：`TopBarFrame` 暴露了 `MenuButton` / `QuestButton` / `AlliesButton`，全仓没有一处 `.Clicked +=`，所以顶部「任务/菜单/盟友」可点但完全没反应。
- **选项**：客户端的 `AboutDialogFrame` 有镜头距离、平移灵敏度、音乐/音效音量、帧率上限、渲染倍率滑块，关掉时 `Closed += _userSettings.Save`；Launcher 没有任何设置界面。

要做：Esc 改回取消优先并补 F10，接上顶部三按钮（接不了就先隐藏），选项界面和 32 的音量一起做。

## 37. 还缺这些面板

客户端有的：`QuestDialogFrame`（任务）、`AllianceDialogFrame`（盟友，含 Shift 批量与资源转赠）、`LeaderboardFrame` / `MultiboardFrame`、`DialogFrame` / `TimerDialogFrame`（脚本对话框）、`TimedTextFrame`（系统消息/字幕）、`CinematicSceneFrame` + `CineFilterFrame` + `CinematicSkipFrame`（过场遮罩、滤镜、跳过提示）、`ItemDetailFrame`（地面物品详情）、`CheatDialogFrame`、`CommandErrorFrame`。

Launcher 一个都没有。玩家看到的：顶部「任务」「盟友」点了没反应，过场不遮幅不过滤也没有跳过提示，点地面物品看不到详情，没有排行榜。

注意：其中多面板、排行榜、任务、脚本对话框在客户端是由触发器驱动的，JASS 进来之前，这几项可以先只做「帧能显示、内容从 C# 侧填」的版本。

要做：按使用频率排——过场遮罩和跳过提示、地面物品详情、任务，然后才是排行榜/多面板。

## 38. 聊天和控制组各写了一套

- **聊天**：客户端 `ChatFrame` 是 HUD 里的帧，行会淡出、自动换行、解析 `|c` 颜色、有 `AppendHistory`，Shift+Enter 全体、Ctrl+Enter 盟友；Launcher 的 `ChatFrame` 同名不同物，是独立 `CanvasLayer` 里的 `Label` + `LineEdit`，固定 224,560 / 620×110，不随 UI 缩放、不淡出、不换行，`Send` 只 `Add("[玩家] ...")` 加 `GD.Print`。
- **控制组**：客户端屏幕左下有一排 1–9 编队按钮，支持双击居中；Launcher 只有键盘 0–9，触屏没法编队。

要做：聊天挪回 HUD 帧体系并补换行/淡出/频道；编队按钮补上（触屏迟早要用）。

## 39. 选择、子组、点头像

- **点头像**：客户端 `SelectFromGroup` 保留整队、只换主位，还处理载具乘客和金矿工人；Launcher 的 `SelectUnitFromHud` 先 `ClearSelection()` 再 `Add(unit)`，**多选 8 个兵时点第二个图标，只剩那一个**，而且绕过了选择控制器，`Primary` 不更新。
- **子组**：客户端 `PlayerSelectionController.CycleSubgroup` 先 `NativeSelectionOrder.Sort` 再只挪主位；Launcher 走场景自己的 `CycleSubgroup`（`Commands.cs`），按 `Type.Id` 重建 `_selected`，不排序、不区分英雄/幻象，控制器那份成了死代码。双击选中一排兵再按 Tab，切出来的顺序和屏幕排列不一致。
- **框选**：客户端判 `CanControl`（含共享控制）、用 `ScreenViewport.TryClipRect` 裁超宽屏、会 `CancelPending`、有选中音；Launcher 只判 `unit.Player == ViewPlayer`，21:9 下框到黑边里也算选中。
- **双击**：客户端双击或 Ctrl+点击都能选屏幕内同类，Launcher 只有双击。

要做：点头像改成 `SelectFromGroup`，子组并回控制器版本，框选补 `CanControl` 和裁切。

## 40. 右键的智能命令少一半分支

客户端 `War3WorldInteractionComponent` 的右键是一串分支，每支「吃掉能干的单位，剩下的继续执行本义」：集结点（逐单位 `CanSetRally`）、交资源回建筑、继续建造、修理、采金/伐木、传送门、运输机装卸、攻击/跟随（`EligibleAttackers` + `MoveUnhandled` 逐单位补全）、拾取地面物品，目标用真实拾取 `PickUnit`。

Launcher 只有四条：全选都是建筑时设集结点、点中残血友方建筑时修理、屏幕距离最近的敌人攻击（金矿转采金）、最近的树伐木，其余移动。目标判定用的是「屏幕像素距离 ≤55px」的启发式。

玩家看得到的：选农民右键正在建造的兵营，Launcher 当成移动；选 5 个农民 + 2 个步兵右键残血主基地，Launcher 让所有单位 `return`，步兵站着不动（客户端步兵继续走）；混选兵营 + 步兵右键不设集结点；重叠单位点到的东西可能和客户端不同。另外放置建筑没有「Shift 连造」，放完一座就退出放置模式。

要做：把分支按客户端补齐，目标判定换成拾取，放置模式支持 Shift 连造。

## 41. 物品栏不能拖，拾取靠中心点

客户端 `InventoryFrame` 有 `BeginDrag / MoveDrag / EndDrag / HitSlot`：拖到另一格换位、拖到友方英雄转交、拖到商店卖出、触屏 45px 矫正，否则丢地面。Launcher 只有 `SlotClicked`，右键是「给 450 范围内最近的友方英雄，否则丢地」——**物品不能在格子之间换位，也不能拖着去商店卖**。

拾取也不同：客户端射线先打 MDX 的 `CLID` 碰撞体，再逐三角形打网格，按 geoset alpha 和 `SelectionFlags` 过滤；Launcher 把单位中心投到屏幕，取 55 像素内最近的一个。高模型、翅膀、武器伸出去很远的单位，点边缘选不中；点旁边空地反而可能选中。

要做：拖拽（`UiEventRouter` 的 `CommandDragMoved/Released` 现成可用），拾取换成几何判定。

## 42. 小地图少了图标，滤镜点了没效果

- **图标**：客户端 `MinimapUnitView.Classify` 认金矿、缠绕金矿、闹鬼金矿、中立建筑、圈地、物品、野怪营地、出生点；野怪营地还有 `MinimapCampTracker`（按迷雾和等级画两张图标，位置走 MDX 模型）。Launcher 的 `MinimapUnitView` 只认几个硬编码 id，`SimulationId` 恒为 0，`ColorIndex` 直接等于 `unit.Player`；营地图标列表 `_minimapIcons` 声明了但从来没 `Add` 过，`GodotUiRenderDevice.DrawMinimapCamp` 直接 `return false`。小地图上**没有野怪营地、没有物品、没有出生点**，中立建筑也不显示中立色。
- **滤镜**：`MinimapFilter` 只被赋值没人读，`SetFilterState` / `HandleAltShortcut` 都没有调用者。点「野怪/配色/阵型」按钮图标会变，游戏里没有任何效果；Alt+G/T/A 没反应；阵型按钮不改变移动阵型。
- **腐地**：客户端有 `MinimapBlightTexture` 带缓存；Launcher 在 `Ui.cs` 里内联重算，`blight.Version` 一变就整图 `GetData()` + 新建 `ImageTexture`，亡灵扩张时会卡。

要做：`Classify` 补全 + 营地图标，滤镜接上，腐地改用缓存那块。

## 43. 界面拉满整窗，超宽屏不居中

客户端把 `PixelOffsetX` 和 `ActivePixelWidth` 传给渲染设备，`War3UiViewport.FitPixelWidth` 把 HUD 限制在居中的 16:9 区域；Launcher 的 `GameHudScreen._Draw` 用 `scaleX = viewport.X/1024`、`scaleY = viewport.Y/768` 直接铺满，`_offsetX` 传 0。2560×1080 下 HUD 被横向拉宽三成，小地图变扁、命令卡格变宽、圆形按钮变椭圆。而且 `GameHudScreen` 自己又定义了一份 `DesignWidth/DesignHeight`，同一份 HUD 里混着两套常量。

`UiFontScale` 也没接：共用的 `UiFontScale` 默认 1.2，Launcher 全仓没有一处 `Set`，客户端读用户设置并在倍率变化时 `InvalidateLayout()`。

电影模式不隐藏 HUD：客户端 `SetInterfaceVisible` 会逐帧隐藏 19 个帧；Launcher 没有这个概念，过场时命令卡、小地图、资源条都还在。

要做：把 `War3UiViewport` 用起来（选图页、加载屏已经在用），删掉重复常量，字号接设置，电影时隐藏界面。

## 44. 界面渲染能力缺几项，FDF 生成的绑定没人用

`GodotUiRenderDevice` 里：`DrawCooldownSweep` 是纯色扇形（客户端用 `cooldown.blp` 画），`DrawCooldownCompletion` / `DrawAutoCast` / `DrawMinimapCamp` 直接 `return false`，`DrawTexturedQuadAdd` 退化成普通的 `DrawTexturedQuad`（所有 ADD 混合的贴图偏暗）。所以冷却遮罩没有原版质感、冷却结束不闪、自动施法没有旋转光环、营地图标画不出来、高亮和光晕偏暗。

另一件事：Launcher 生成了 `ConsoleUiBinding`、`ResourceBarBinding`、`UpperButtonBarBinding`、`EscMenuMainPanelBinding`、`MainMenuBinding`，但除了 `LoadingScreenBinding` 之外**没有任何调用者**，HUD 走的是手写 `TopBarFrame` + `ConsoleSkinFrame`。客户端 `War3FdfUi` 是真的用这些绑定切换 `_console.Visible` 和 `SetChromeVisible`。

要做：先补四个 `return false` 和一个 ADD 混合（都是小改动、收益直接），再决定 FDF 绑定是接上还是删掉。

---

## 七、代码结构

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
| `Game/UI/MapSelectionScreen.cs` | `IMenuScreen` + FDF `Skirmish` 宿主 | `Control` + 手搭帧（第 1 条要改的就是这里） |
| `Game/Audio/War3AudioSystem.cs` | 1022 行，24 声部 + 配额 + 抢占 | 75 行，16 个播放器 |
| `Game/Scenes/War3GameScene/Ui.cs` | HUD 装配 + 文本标签 | 命令卡条目 + 商店 + 放置 + 小地图 |
| `Game/Scenes/War3GameScene/CameraFollowTrack.cs` | 跟随状态机 | 4 个取景函数 |

解析类名也对不上：`W3iInfo` vs `MapInfo`、`W3eTerrain` vs `TerrainData`、`W3mPathing` vs `PathingData`；`TeamColorIndex` 在 Launcher 里是 `Player` 的别名。

要做：改名或改路径，让「同名」等于「同职责」。这一步不产生画面收益，但后面每一条对齐都要靠它定位。

## 46. 没有调用者的接口

这些接口看着像功能，实际没人调，按「已实现」判断会得出错误结论：

- `MeleeHudController`：`SetHint`、`SetQueue(string)`、`SetClock(string)`、`SetPlayerInfo`、`SetBuffs`、`SetUnitSummary`、`DetailProvider`、`SelectionSlotClicked`、`RefreshCooldownNow`、`ShowItemDetailForTest`。
- `TopBarFrame`：`MenuButton` / `QuestButton` / `AlliesButton` 无订阅者。
- `MinimapFrame`：`SetFilterState`、`HandleAltShortcut` 无调用者；`War3GameScene.MinimapFilter` 只写不读；`_minimapIcons` 从不 `Add`。
- `PlayerSelectionController`：`CycleSubgroup`、`SelectInitial`、`ApplyScriptSnapshot`、`RecallSnapshot`。
- `War3Camera`：`SetField`、`GetField`、`PanTo`、`SetTargetController`、`SetOrientController`、`SetNoise`、`SetShake`、`ResetToGameCamera`。
- `MapSelectionScreen`：`Cancelled` 事件（`LauncherApp` 只订阅了 `StartRequested`）。
- 其他：`ConsoleSkinFrame.SetInventoryVisible`、`MenuScreenHost.RegisterCommand/Execute`、`Environment/LayerAtlas.cs`（整个文件无引用）、`GameHudScreen.ClearHover`。
- 生成物：`ConsoleUiBinding`、`ResourceBarBinding`、`UpperButtonBarBinding`、`EscMenuMainPanelBinding`、`MainMenuBinding`。

要做：要么接上，要么删掉。留着会让人误判「这块已经做了」。

## 47. 各自写了一份的东西

明明可以共用却各写一份的：`WeatherDefinition` / `WeatherType`（近乎逐字复制）、`AnimationTagCache` 的 `SplitProperties`（私有复制了一份）、`ShorelineWaveRenderer` 的角落判定、`MinimapBlightTexture`（内联进 `Ui.cs`）、`War3UiViewport` 的设计分辨率常量（`GameHudScreen` 和 `MapSelectionScreen` 里各定义了一遍）。

`GameCore/L2_Core` 除 `MapBrowser` 外两侧逐字节相同，说明共用这条路是通的，上面这些只是没走。

要做：一次清理一份，先清有明确重复代码的（`WeatherDefinition`、`SplitProperties`），再清常量。

---

## 建议的推进顺序

**先做第 1 条**（选图界面）。它是本轮的入口，也是唯一一条会把 FDF 宿主接起来的，后面 HUD 的 FDF 绑定能顺着它复用。

接着做不改变操作习惯、但决定画面一致率的一批：**8、9、10、11、13、14、15**（光照、雾、天空、地形、阴影、迷雾）。这一批做完，同一张图并排看已经能看出「像一个游戏」了。

腐地、水面、岸浪、天气、贴花（**16–20**）跟着 11 一起排，它们和地形用的是同一套环境和着色器代码，分开做会来回改同一个文件；地形高度变形（**12**）要改地形网格的写入方式，单独立一条，别和上面混在一起。

再修一眼就能看出错的单位表现：**21、22、23、24、25**（血条圈影子、颜色、飞行与朝向、战斗动作、死亡升级）。这几条互不依赖，可以并行。

声音（**31、32**）不依赖任何其他改动，可以随时插空做，适合在等编译或等跑图的间隙推进。

然后补表现层的窟窿：**26、27、28、29、30**（特效来源、粒子丝带、闪电弹道、飘字、预载），其中 26 的「接上 `world.Effects`」性价比最高。

进图的加载段（**2–5**）单独成批，因为它要动 `LauncherApp` 和加载编排，和上面几条冲突小，可以安排在不方便跑图的时段做。

界面放最后（**33–44**），不然后面底层行为一改就要返工。界面里也有例外：**33**（提示语）和 **35**（热键）现在的状态是「按了没反应」，属于功能性缺陷，可以提前。

**6、7**（镜头）建议跟着 8 一起做，因为昼夜和镜头都要动 `Lifecycle` 的每帧更新。

**45–47** 是结构清理，什么时候做都可以，但每接一条新功能之前先看一眼 45，能省掉一次找错文件。

## 收尾规则

- 一条做完就从本文删掉，序号不复用，后面的不重排；下一条新任务从 **48** 开始。
- 新发现的差异先判断是不是「平台差异」或「已知取舍」：受 Godot 与原生 GL 的差别限制、Launcher 固定 gmap 路径、或者明确不做的范围（JASS、触发器、战役、存档、录像、联机），不要写进来。
- 写进来的时候要带三个东西：客户端在哪、Launcher 在哪、玩家看得到什么。只有「不一样」没有证据的，等复核清楚再写。
