# Launcher 一致化任务 v2：客户端模拟 + Godot 表现

> 自包含文档。只读这一份就能接手并继续推进。
> 上一版（逐文件适配循环）见 `Launcher一致化任务.md`，其历史轮次见 `Launcher字节一致化路线.md`，差异清单见 `Launcher与Open-Warcraft3的差异_v2.md`。
> 基准：Launcher 511 个 cs / 5.9 万行，客户端 `test/Open-Warcraft3` 1,505 个 cs / 29.6 万行（不含 Android 与工具）。
> 写于 2026-11-13。

---

## 0. 方向

两件事同时定下来：

1. **逻辑用客户端源码**。模拟、数据解析、JASS 全部原样搬进来，逐字节一致，`diff` 能直接对照。
2. **画面用 Godot 实现**。地形、模型、特效、界面、音频都由 Godot 的节点与资源体系画，资源只从 `extract.pck` 取。Launcher 不携带任何 War3 资源，也不读 War3 安装目录。

交换的东西：

| | v1（逐文件适配） | v2（本文件） |
|---|---|---|
| 逻辑 | Launcher 自写等价实现，逐个文件对齐 | 客户端源码整目录搬入 |
| 画面 | Launcher 自写，或把客户端文件补进 Launcher | Godot 重写，按客户端逐项对齐参数 |
| 资源 | 混用 | 只有 extract.pck |
| 达标单位 | 单个 .cs | 整个模拟层 |
| 主要成本 | 每个技能各修一遍 | 表现层一次重写 + 数据镜像 |

画面这块要说清楚代价：渲染代码不共享，所以「画面一致」靠资源同源、参数对齐、逐项截图验收，不靠代码同源。这条判据写在第 1 节，§5.4 给出 20 项对齐清单，避免后面反复争论「为什么和客户端不一样」。

---

## 1. 目标与判据

Launcher 在**地图加载界面**和**游戏运行全程**的画面与行为与客户端一致。四条判据：

| 判据 | 定义 | 达标线 | 性质 |
|---|---|---|---|
| 逻辑同源 | Launcher 内与客户端逐字节相同的模拟层 .cs | ≥ 99%（只允许附录 A 的补丁） | 硬指标，机器可校验 |
| 逻辑等价 | 同图、同种子、同输入下逐 tick 状态摘要 | 3000 tick 逐 tick 一致 | 硬指标 |
| 画面等价 | 同机位、同 tick 截图 | SSIM ≥ 0.99，均值通道差 ≤ 2/255，无结构性缺失 | 需人工看图确认 |
| 加载等价 | 加载阶段序列、界面元素、文本、进度 | 阶段序列一致，界面截图达画面等价 | 硬指标 + 截图 |

补一条纪律：**画面差异按 §5.4 的清单逐项关掉，不按「整体看起来像不像」判断**。清单没关完之前，任何「差不多了」都不算达标。

---

## 2. 现状

### 2.1 规模

| 区域 | 客户端 | Launcher |
|---|---:|---:|
| 数据层（`L1_Adapter/Formats` + `L2_Core`） | 17,880 | 5,691 |
| 模拟层（`Src/` + `L3_Simulation`） | 141,749 | 30,741 |
| 表现层（`L4_Composition/Game`） | 96,507 | 20,693 |
| 平台层 | 9,226 | 2,086 |
| 合计 | 265,362 | 59,211 |

Launcher 表现层分布：`Scenes/War3GameScene` 9,276、`Diagnostics` 6,261、`Environment` 1,927、`UI` 1,691、`Presentation` 795、`Audio` 472、`Rendering` 196、`Camera` 75。

### 2.2 逐文件适配走到了哪

| 指标 | 值 |
|---|---:|
| 同路径 .cs | 262 |
| 其中字节一致 | 112 |
| 差 1 到 10 行的文件 | 1 |
| 差 81 行以上的文件 | 119 |

112 是那条路线的上限。剩下的差距都是子系统级，单个文件补不进去。

### 2.3 三个实测结论（决定这份计划能不能做）

**一、客户端核心源码在 net8.0 下能编译，只有 2 处需要补丁。**

Godot 4.7.2 的 `GodotSharp.dll` 只有 `lib/net8.0`，随包 `GodotPlugins.runtimeconfig.json` 声明 net8.0；本机装了 8.0.20，进程落在 net8 运行时，net11 程序集载不进去。所以 Launcher 锁 net8，客户端跑 net11.0-rc。

把客户端 Formats + Core + Simulation（15.96 万行，含 3.3 万行寻路）单独抽出来按 net8.0 编：

| 阶段 | 错误数 | 性质 |
|---|---:|---|
| 原始 | 6,444 | 缺 ImplicitUsings 与包引用 |
| 补隐式 using | 158 | 全部是缺 NuGet 包 |
| 补包引用 | 2 | net9 API：`Dictionary.Capacity`、`PersistedAssemblyBuilder` |

两处都在 JASS 代码里，改法见附录 A。War3Net 三个 csproj 的 `TargetFramework` 从 net9.0 改 net8.0，并给 `War3Net.IO.Mpq` 补一条到 `War3Net.Common` 的直接引用。

**二、客户端的数据入口只有一个。**

`War3ObjectData.Load`、`War3GameplayConfig.Load`、`JassRuntimeHost` 全部经过 `War3ResourceManager`，只用三个方法：`Read(path)` 取字节、`Exists(path)` 判存在、`ReadDatabaseText(path)` 读表文本。JASS 启动读的文件固定三个：`Scripts\common.j`、`Scripts\Blizzard.j`、`Scripts\war3map.j`（后者在地图包里）。

这意味着「把数据源换成 pck」只需要让这个类读到 pck 里的内容，解析代码一行不用改。做法见第 4 节。

**三、模拟层已经为外部表现层留了契约。**

模拟对象自带可视字段，Godot 视图不用猜：

| 契约 | 位置 | 内容 |
|---|---|---|
| 特效 | `Src/Core/Effect.cs` | `VisualModelPath`、`VisualTarget`、`VisualAttachmentPoint`、`VisualHeight`、`VisualScale`、`VisualAgeSeconds`、`VKind`、`VRadius` |
| 弹道 | `Src/Core/MissileEffect.cs` | `TravelProgress`、`ArcOffset`、`SegmentLaunchPos`、`SegmentTargetPos`、`LaunchHeight`、`ImpactHeight`、`Direction`、`Team`、`MissileArt` |
| 单位缩放 | `Src/Units/Unit.cs:172-181` | `VisualScale`、`VisualScaleX/Y/Z`、脚本缩放 |
| 目标形态 | `Src/Core/TargetFlags.cs`、`DefenseType.cs` | 纯数据 |

Godot 视图按这些字段画，模拟层不用为渲染改一行。

### 2.4 为什么不用客户端渲染器

客户端渲染器走 `IRenderDevice`（OpenGL）。要在 Godot 里跑它，得用 `RenderingDevice` 重写整个后端，还要把 29 个 GLSL 330 着色器离线编成 SPIR-V，处理 GL 与 Vulkan 的深度范围与裁剪空间差异。技术上可行（Godot 4.7.2 有 `DrawList*`、`ShaderCreateFromSpirV`、`Texture2Drd`），但那样 Launcher 就成了一个自带渲染器的黑盒窗口，Godot 只剩画布，编辑器的选中、拖拽、叠加层全用不上。

按本文件的方向，这条路不走。渲染归 Godot，代价是表现层重写，收益是 Launcher 是个正常的 Godot 工程。

---

## 3. 架构

### 3.1 分层

```
Launcher（Godot 4.7.2 / net8.0）
├── Client/    客户端模拟与数据，verbatim，禁止 using Godot
│      L1_Adapter/Formats      w3x / slk / txt / 物编解析
│      L2_Core                 配置模型
│      L3_Simulation           World / Unit / Ability / Buff / Order / Pathing / JASS
│      Src/                    Core Abilities Components Skills Units Orders Items Events
├── Bridge/    客户端状态 → 视图可读的快照，表现层不直接摸 Client
│      UnitView / EffectView / PlayerView   每帧快照
│      CommandBridge                        Godot 输入 → GameSession.IssueCommand
│      Coords                               WC3 坐标 ↔ Godot 坐标（唯一换算入口）
│      AssetPath                            WC3 资源路径 → res://extract 路径
├── View/      Godot 表现层
│      Scene / Terrain / Doodads / Units / Effects / Fog / Camera / Hud / Audio
└── Data/      只读 extract.pck
       PckMount       挂载 pck
       War3DataMount  把 pck 里的数据镜像交给 War3ResourceManager
```

依赖方向：`View → Bridge → Client`。`Client` 不引用 Godot，`View` 不直接引用 `Client` 的模拟类型。两条用工程引用钉死。

为什么要 Bridge 这一层：Launcher 的视图现在直接读 `Unit.Position`、`Unit.Alive`、`Unit.Type`。换成客户端的 `Unit` 后，同名属性有一半语义不同（v1 的失效模式清单里记过 `World.Tick`、`Learned`、`Classifications` 三例）。中间隔一层快照，语义差异在一个文件里对齐，出问题只改一处。

### 3.2 工程与目录

| 工程 | 内容 | 允许引用 |
|---|---|---|
| `Client/Client.csproj` | 客户端源码 verbatim | NuGet + War3Net，无 Godot |
| `View/View.csproj` | Godot 表现层与 Bridge | GodotSharp + Client |
| `Launcher.csproj` | `Platform/`、`project.godot`、入口 | View + Client |

`Client` 里出现 `using Godot` 编译即失败，这是「模拟层不依赖引擎」的硬保证。

### 3.3 客户端源码的三类处理

| 类别 | 处理 | 数量 |
|---|---|---:|
| verbatim | 逐字节复制 | 除下面两类之外全部 |
| patch | 最小区分，登记附录 A | 2 处源码 + 3 个 csproj 标签 |
| exclude | 不搬 | `L4_Android`、`MobileMapEditor`、`Tool`、`VisualStudio`、`L4/Game/Imgui`、`L1_Adapter/RHI.OpenGL`、`L4/Game` 的渲染与界面 |

最后一项要留意：`L4_Composition/Game` 里 `Rendering`、`Environment`、`UI`、`Scenes`、`Input`、`Audio` 都不搬，那是 Godot 版的活。搬的是 `L3_Simulation` 与 `Src`。

`Src/Diagnostics` 里极少数引用界面类型的测试（如 `FdfParserTest.cs`）单独排除，登记在附录 A5。

同步脚本 `tools/sync-client.sh`：从客户端仓库 rsync 到 `Client/`，随后跑三条校验（补丁清单外逐字节一致、无 `using Godot`、不引用 View 与 Launcher）。

### 3.4 范围

| 客户端模块 | 处理 | 理由 |
|---|---|---|
| `Src/**`（模拟、能力、指令、寻路、配置、资源栈） | 搬 | 对战核心 |
| `L1_Adapter/Formats`、`L2_Core` | 搬 | 地图与物编解析 |
| `L3_Simulation/**`（含 Jass 3.93 万行） | 搬 | 触发器、AI、脚本 UI |
| 录像回放 `World.Recording` | 随模拟层搬 | 与 tick 绑定 |
| `L4/Game/Rendering`、`Environment`、`UI`、`Scenes`、`Input`、`Audio` | 不搬，Godot 重写 | 本文件方向 |
| `L4/Android`、`Tool`、`MobileMapEditor`、`Imgui` | 不搬 | 平台与工具 |
| Glue 层（主菜单、地图选择、战役） | 不搬 | Launcher 走 `--play=` 直接进图 |

范围内：进图、加载、对战、结算的全部逻辑与画面。范围外：主菜单、战役、联机、编辑器辅助。

---

## 4. 数据：只读 extract.pck

### 4.1 pck 里现在有什么

`extract.pck` 988 MB：

| 类型 | 数量 | 用途 |
|---|---:|---|
| .tres | 25,980 | 材质与配置 |
| .tscn | 6,946 | 模型（MDX 转换结果） |
| .png | 6,380 | 贴图（BLP 转换结果） |
| .wav / .mp3 | 6,357 | 音频 |
| .json | 3,464 | 表格（slk/txt 的转换结果） |
| .glb | 3,264 | 网格 |
| .fdf | 84 | 界面描述 |
| .wc3.json | 每模型一份 | 动画序列、包围盒、`boundsRadius` |

### 4.2 缺什么

客户端模拟层要读的东西里，有三类 pck 现在没有：

| 缺口 | 读取位置 | 规模 |
|---|---|---|
| 原始 slk / txt 表文本 | `War3ResourceManager.ReadDatabaseText` | 约 10 MB |
| `common.j` / `Blizzard.j` 与 `.ai` 脚本 | `JassRuntimeHost:184-187`、AI 载入 | 约 2 MB |
| 建筑 footprint 的 `*.tga` | `War3Pathing.cs:110-190` 逐像素解 | 约 3 MB |

`.json` 表格替代不了 slk/txt：客户端的解析器读 slk/txt 原文，换成 json 等于重写配置层，那部分同源度就没了。

处理方式：**Extract 增加一个 raw 镜像导出**，把这三类原字节按 WC3 路径写进 pck。总量约 15 MB，相对 988 MB 可以忽略。模型与贴图继续用转换后的 Godot 资源，两套并行，各取所需。

### 4.3 怎么喂给客户端

客户端唯一的数据入口是 `War3ResourceManager`，它按固定名字打开游戏档案（`War3.mpq`、`War3x.mpq`、`War3Local.mpq`、`War3xLocal.mpq`、`War3Patch.mpq`），从里面读文件。

最省事的做法：**Extract 把 raw 镜像打成一个真正的 MPQ，命名 `War3x.mpq`，放进 pck**。

```
Extract 侧：
  extract/war3data/War3x.mpq        ← slk / txt / j / ai / pathing tga，约 15 MB

Launcher 启动：
  1. 挂载 extract.pck
  2. FileAccess 读 res://extract/war3data/War3x.mpq → 写 user://cache/war3/War3x.mpq
  3. new War3ResourceManager("user://cache/war3", mapPath)
  4. 后面全部走客户端原有代码
```

这条路让客户端源码零改动：`War3ResourceManager` 以为自己在读一个装了冰封王座的目录。代价是首次运行落盘 15 MB，需要缓存版本号（mpq 里塞 `war3data.version`，与 pck 版本比对，不匹配就重写）。

如果受不了这次落盘，P5 补 1 处 seam：给 `War3ResourceManager` 加流提供者，直接读 pck 内的 mpq 字节。归入附录 A4。

### 4.4 资源路径映射

Godot 侧按 WC3 路径取资源。pck 里的目录是镜像的，映射规则机械：

| WC3 | pck |
|---|---|
| `Units\Human\Footman\Footman.mdx` | `res://extract/Units/Human/Footman/Footman.tscn` |
| `Units\Human\Footman\Footman.blp` | `res://extract/Units/Human/Footman/Footman.png` |
| `Sound\Units\...wav` | `res://extract/Sound/...wav` |

即替换分隔符、按扩展名换后缀。例外（`SharedModels`、`ReplaceableTextures`、`UI`）写进 `Bridge/AssetPath.cs` 的例外表，逐个确认。找不到的资源不能静默失败，必须打日志并由 `--asset-probe` 列出，否则会退化成「模型不显示但没人知道为什么」。

---

## 5. 表现层

### 5.1 三层职责

| 层 | 职责 | 不该做的事 |
|---|---|---|
| 模拟（Client） | 判定、结算、状态推进、JASS | 不碰节点、不碰贴图 |
| 桥（Bridge） | 每帧把模拟状态压成视图快照；把 Godot 输入翻成 `PlayerCommand` | 不做插值、不持有资源 |
| 视图（View） | 实例化节点、动画、材质、界面、相机、音频 | 不读 `Unit` 字段、不改模拟状态 |

快照内容（每帧重建，不复用旧对象）：

| 视图 | 字段 |
|---|---|
| UnitView | id、类型码、玩家、队伍色、位置、朝向、缩放、HP/MP 比例、建筑/英雄/幻象标记、隐身态、动画状态与当前序列 |
| EffectView | 模型路径、位置、附着单位、附着点、高度、缩放、存活比 |
| MissileView | 起点、终点、进度、弧度、美术路径、队伍 |
| PlayerView | 金、木、人口、人口上限、是否本地玩家 |

### 5.2 视觉参数集中登记

画面能不能对上，八成取决于参数。所有画面常量集中在 `View/ViewTuning.cs`，每个值注明客户端出处（文件:行），代码里禁止裸数字。

| 参数 | 客户端出处 |
|---|---|
| 相机距离、俯角、FOV、边缘滚屏宽度 | `Camera/War3Camera.cs`、`War3CameraComponent.cs:105-116` |
| 光照方向、颜色、环境光、昼夜曲线 | `Environment/SceneLighting.cs:47-52` |
| 雾颜色、起止距离 | 地形着色器 `FogOfWarCommon.glsl` |
| 迷雾格宽、边缘柔化 | `Src/Core/FogOfWar.cs:13-14` |
| 地形贴图混合权重、阴影强度 | `Rendering/TerrainMaterialBatches.cs`、`TerrainShadowMap.cs:35-36` |
| 天空盒日月与云 | `Rendering/SkyboxRenderer.cs:14-61` |
| 水面参数 | `Environment/WaterRenderer.cs` |
| 队伍色、幻象染色 `(20,96,255)` | `Presentation/UnitStateColor.cs:16-30`、`IllusionPresentation.cs:21-23` |
| 选择圈贴图与尺寸 | `Presentation.cs:1844` |
| 动画过渡时长、行走速率系数 | `Presentation/UnitAnimationController.cs` |
| 血条、浮动文字字号与颜色 | `Scenes/War3GameScene/Floats.cs` |

### 5.3 坐标与单位

WC3 世界单位直接当 Godot 世界单位用（1:1），不缩放。轴向沿用 Launcher 现有约定，集中在 `Bridge/Coords.cs`：

```
worldX = -(mapX - terrain.OffsetX)
worldZ =  mapY - terrain.OffsetY
```

高度用 `Y`。所有进出模拟的坐标都过这个文件，禁止在视图里手算。

理由：单位位置、粒子尺寸、相机距离、阴影偏移全依赖这个口径。口径一乱，截图逐项对比永远对不上。

### 5.4 逐项对齐清单

每项都要有客户端出处、Godot 实现位置、验收截图。清单没关完，画面判据不算达标。

| # | 项 | 客户端 | Godot 实现 | 阶段 |
|---|---|---|---|---|
| 1 | 地形网格与贴图混合 | `TerrainRenderer`、`Terrain.frag.glsl` | `View/Terrain` | P2 |
| 2 | 悬崖与断崖接缝 | `CliffRenderer.cs` | `View/Terrain/Cliff` | P2 |
| 3 | 地形烘焙阴影 | `TerrainShadowMap.cs:35-36` | 预烘焙贴图 | P2 |
| 4 | 装饰物实例与动画 | `DoodadRenderer.cs:229-294` | `View/Doodads` | P2 |
| 5 | 装饰物破坏与消失 | `DoodadRenderer.cs:958-978` | 动画 + 淡出 | P2 |
| 6 | 昼夜光照 | `SceneLighting.cs` | `WorldEnvironment` + 方向光 | P2 |
| 7 | 天空盒日月云 | `SkyboxRenderer.cs` | `View/Sky` | P2 |
| 8 | 水与岸边浪 | `WaterRenderer.cs`、`ShorelineWaveRenderer.cs` | `View/Water` | P2 |
| 9 | 战争迷雾（含 LOS 遮挡） | `FogOfWar.cs:566-597` | shader + 贴图 | P2 |
| 10 | 单位模型与骨骼动画 | `CharacterRenderer.cs` | `View/Units` + AnimationPlayer | P3 |
| 11 | 动画选择规则 | `UnitAnimationController.cs` | 决策表移植 | P3 |
| 12 | 附着点与武器挂点 | MDX 事件、`AttachmentPoints` | 从 `wc3.json` 读 | P3 |
| 13 | 队伍色、幻象染色、虚无色调 | `UnitStateColor.cs` | 材质参数 | P3 |
| 14 | 选择圈、血条、施法条 | `Presentation.cs:1844` | `View/Hud/Overlay` | P3 |
| 15 | 技能特效与粒子 | `Effect.Visual*` 契约 | `View/Effects` | P3 |
| 16 | 弹道（弧形、直线、反弹） | `MissileEffect` 参数 | 同上 | P3 |
| 17 | 地面贴花、建筑残影 | `War3SplatSystem.cs`、`FogMemory.cs` | `View/Decals` | P3 |
| 18 | 天气 | `WeatherRenderer.cs`、`WeatherType.cs` | `View/Weather` | P3 |
| 19 | HUD 与命令卡 | `UI/` 18,045 行 | `View/Hud` | P4 |
| 20 | 加载界面 | `War3LoadingScreenRenderer.cs` | `View/Loading` | P4 |

### 5.5 Launcher 现有表现层怎么处理

Launcher 已有 20,693 行 Godot 表现代码，能复用的复用，省下的时间投到清单上。

| 现有内容 | 处理 |
|---|---|
| `Environment/`（地形、悬崖、水、天空、装饰物、天气、光照、迷雾遮罩） | 保留，按清单补参数 |
| `Presentation/UnitSceneComponent`、`EffectArtTable` | 保留骨架，数据源换成模拟契约；`EffectArtTable` 的 148 条硬编码改为读物编 `Art` 字段 |
| `Camera/War3Camera` | 保留，按客户端补齐旋转、跟随、平滑、边界 |
| `UI/`（HUD、小地图、英雄栏、聊天、菜单、加载） | 保留骨架，按客户端 FDF 重排 |
| `Audio/` | 保留，声道策略按客户端重写 |
| `Scenes/War3GameScene` 的 `Ai`、`AiCounters`、`Melee`、`Production`、`Commands`、`Rally`、`QuickCast`、`Units`、`Lobby`、`Cheats` | 删除，6,531 行。这些是模拟职责，客户端已有 |
| `Diagnostics/`（CombatProbe、WorldTest、FdfProbe 等） | 删除，6,261 行。全部针对旧模拟 API，P6 换成客户端诊断 |
| `Src/`（World、Skills、Abilities、Units、Items、Core、Components、Events、UI） | 删除，18,475 行 |
| `GameCore/L1_Adapter`、`L2_Core`、`L3_Simulation`（Launcher 现有副本） | 删除，17,957 行，由 `Client/` 取代 |

合计删除 49,224 行，剩约 10,000 行（视图 + 桥 + 平台），P6 目标压到 9,000。

---

## 6. 阶段计划

每个阶段单独可验收、单独可回退。

| 阶段 | 目标 | 关键交付 | 主要风险 |
|---|---|---|---|
| P0 | 数据通路打通 | 三工程骨架、net8 兼容、raw 镜像、`--data-probe` | mpq 镜像与路径 |
| P1 | 逻辑同源 | 模拟层整块搬迁、w3x 直读、JASS、digest | 双引擎并存期 |
| P2 | 世界画面 | 地形、悬崖、水、天空、光照、装饰物、迷雾 | 参数对不齐 |
| P3 | 单位与特效 | 模型、动画规则、附着点、染色、特效、贴花、天气 | 动画决策移植 |
| P4 | 界面与交互 | FDF 界面、命令卡、输入、加载界面 | 布局口径 |
| P5 | 音频与 gmap | 音频端口、gmap 容器 | 混音模型 |
| P6 | 收尾 | 删旧实现、性能、打包 | 性能不及客户端 |

### P0 数据通路

| 任务 | 内容 |
|---|---|
| P0.1 | 建 `Client/` `View/` 三工程，接同步脚本 |
| P0.2 | net8 兼容：2 处补丁 + War3Net TFM + 包引用 |
| P0.3 | Extract 增 raw 镜像导出，打 `War3x.mpq`，放进 pck |
| P0.4 | `Data/War3DataMount`：pck → 缓存 → `War3ResourceManager` |
| P0.5 | `--data-probe`：打印单位/技能/Buff/物品/科技条数与 JASS 脚本字节数 |
| P0.6 | `--asset-probe`：按当前地图清点模型/贴图/音频/界面缺件 |

验收：`--data-probe` 打印的六组计数与客户端 Debug 构建加载时打印的 `全量物编: unit=… ability=… buff=… item=… destructable=… upgrade=…`（`War3ObjectData.cs:79-81`）逐项相等。这条过了，说明数据层已经从 pck 读全了。

回退：Extract 的导出是增量，不影响现有产物与 Editor/Wiki。

### P1 逻辑同源

| 任务 | 内容 |
|---|---|
| P1.1 | 搬 `L1` `L2` `L3` `Src`；删 Launcher 的 `Src/` 与 `GameCore/L1` `L2` `L3`（合计 36,432 行） |
| P1.2 | 地图：w3x 直读，走客户端 `War3Map*` 解析链，替换 `.gmap` 路径 |
| P1.3 | `GameSession` 驱动：20Hz 固定步，`World.Step` + `JassRuntimeHost.Step` |
| P1.4 | `Bridge/CommandBridge` 最小集：选择、移动、攻击、停止、建造、训练 |
| P1.5 | 视图降级：地形 + 单位方块，保证有画面可看 |
| P1.6 | `--state-digest`：逐 tick 规范化摘要 |

digest 字段：

| 分组 | 字段 |
|---|---|
| 单位 | id、类型码、玩家、位置（定点到 1e-3）、朝向、HP、MP、当前订单 id、目标 |
| 玩家 | 金、木、人口、人口上限、科技等级表 |
| 世界 | tick、随机流状态、单位数、尸体数、特效数 |
| 脚本 | JASS 全局变量哈希、触发器队列长度 |

验收：

- 5 张对战图 × 3000 tick，与客户端 `--headless --digest` 逐 tick 一致
- 同一输入脚本下命令日志逐条一致
- `--engine=launcher` 开关保留，旧引擎仍可跑，主干不红

### P2 世界画面

| 任务 | 内容 |
|---|---|
| P2.1 | `Bridge/Coords` 落地，清掉视图里的手算坐标 |
| P2.2 | `View/Terrain`：网格、图层混合、悬崖、烘焙阴影 |
| P2.3 | `View/Doodads`：实例化、动画、破坏 |
| P2.4 | 光照与天空：`WorldEnvironment`、方向光、昼夜曲线 |
| P2.5 | 水、岸边浪 |
| P2.6 | 迷雾：读模拟的可见性数据，shader 出图 |
| P2.7 | 相机：距离、俯角、滚轮、边缘滚屏、跟随 |

验收：清单 1 至 9 项逐项截图为绿；8 组机位（俯视、贴地、水边、夜间、迷雾内、悬崖边、密林、大战场）SSIM ≥ 0.99。

### P3 单位与特效

| 任务 | 内容 |
|---|---|
| P3.1 | `Bridge/UnitView` 快照 + 实例池；单位增删走事件 |
| P3.2 | 模型实例化：`res://extract/**/*.tscn`，`wc3.json` 提供序列与包围盒 |
| P3.3 | 动画规则移植：状态 → 序列标签、过渡时长、行走速率 |
| P3.4 | 附着点、武器挂点、缩放（含脚本缩放） |
| P3.5 | 队伍色、幻象染色、虚无色调、隐身透明度 |
| P3.6 | 选择圈、血条、施法条、浮动文字 |
| P3.7 | 特效：按 `Effect.Visual*` 实例化，粒子走 `GPUParticles3D` 或 pck 内粒子场景 |
| P3.8 | 弹道：按 `MissileEffect` 的进度与弧度插值 |
| P3.9 | 贴花、建筑残影、天气 |

验收：清单 10 至 18 项逐项截图为绿；抽 20 个单位逐帧比对动画序列名与播放速率；一次 3v3 交战录像逐帧看一遍，无穿模、无残留、无闪烁。

### P4 界面与交互

| 任务 | 内容 |
|---|---|
| P4.1 | FDF → Godot Control：锚点、层级、九宫格、字体 |
| P4.2 | 命令卡：用客户端算好的格位（`Buttonpos` 与抢格结果），不再顺序填 |
| P4.3 | 队列格（可点取消）、英雄栏、小地图、聊天、任务/排行榜/多面板 |
| P4.4 | 输入全套：选择、编队（Shift 并入）、右键智能指令、热键、滚轮、边缘滚屏、Esc/F10 |
| P4.5 | 加载界面：阶段文本、进度条、地图预览、玩家列表 |

验收：清单 19、20 项为绿；输入回放 200 条命令日志一致；逐屏截图（加载、主 HUD、命令卡、建造子页、技能学习、任务、结算）。

### P5 音频与 gmap

| 任务 | 内容 |
|---|---|
| P5.1 | 音频：24 声道、8 组配额、优先级抢占、循环音；决策用客户端，播放用 `AudioStreamPlayer3D` |
| P5.2 | 3D 衰减在 C# 侧按 OpenAL 公式算成 `volume_db`，不用 Godot 内建衰减 |
| P5.3 | 音乐双通道与间隔 |
| P5.4 | gmap 容器：把 w3x 段原样装进 gmap，读出来仍是同一批字节 |

验收：音频事件日志（资源、通道、优先级、起始 tick、音量）与客户端一致；5 个距离点音量差 ≤ 0.5 dB。

### P6 收尾

| 任务 | 内容 |
|---|---|
| P6.1 | 删除旧引擎与 `--engine=launcher` |
| P6.2 | 旧 `--state-probe` 退役，改用客户端 `Diagnostics/` 里不依赖窗口的诊断 |
| P6.3 | 性能：draw call、CPU 帧时间、内存、加载耗时 |
| P6.4 | 打包 macOS / Windows |

验收：1v1 中后期（80 单位 + 60 建筑 + 特效）帧时间 ≤ 客户端 × 1.1。

---

## 7. 验证体系

### 7.1 逻辑：逐 tick 摘要

```bash
# 客户端
OpenWC3.Headless <map.w3x> --war3=<dir> --ticks=3000 --digest
# Launcher
Godot --path Launcher --headless -- --play=<map.w3x> --ticks=3000 --digest
```

两边输出 `tick<TAB>digest`，`diff` 必须为空。首个不一致的 tick 打印逐单位差异。

### 7.2 画面：机位快照

```bash
tools/shot-compare.sh <map.w3x> <镜头预设> <tick>
# → out/launcher.png、out/client.png、out/diff.png、out/metrics.txt
```

指标：SSIM、均值绝对差、差异像素占比、差异掩码图。三条同时满足才算过：指标达线、无整块缺失、差异集中在边缘与高频区。

批量：8 机位 × 3 时间点（0s、30s、180s）。

### 7.3 界面：逐屏

UI 靠机位盖不住，按屏截：加载、主 HUD、命令卡、建造子页、技能学习、商店、任务、排行榜、结算、Esc 菜单。每屏一张对比图进 `out/ui/`。

### 7.4 交互：输入回放

```
0    select 0 12 34
0    move 1500 2400
40   train hfoo
120  cast AHea 3
```

两侧执行同一脚本，比对命令日志与最终 digest。

### 7.5 资源：缺件检查

`--asset-probe`：遍历当前地图实际用到的模型、贴图、音频、界面贴图，逐个走 `Bridge/AssetPath` 解析，输出缺失清单。缺件必须是零，否则「模型不显示」这类问题会一直拖到人工发现。

---

## 8. 风险与预案

| 风险 | 影响 | 触发条件 | 预案 |
|---|---|---|---|
| 表现层双实现导致长期漂移 | 高 | 客户端改了画面参数，Launcher 没跟 | §5.2 参数表集中登记；每轮同步后跑截图回归，超阈值报警 |
| 门面语义错位 | 高 | 视图读到的字段含义与模拟不一致 | `UnitView` 单文件对齐；`--view-probe` 抽 50 个单位逐字段比对 |
| pck 缺数据 | 中 | 某类资源只有转换产物、没有原文 | P0.6 的 `--asset-probe` 先把缺件跑成零，再进 P2 |
| mpq 镜像落盘 | 低 | 首次启动写 15 MB | 缓存 + 版本号；P5 改流式 |
| 动画规则移植不全 | 中 | 采集、建造、引导施法等特殊状态序列不对 | 按 `UnitAnimationController` 的决策表逐状态列对照表 |
| 坐标口径被破坏 | 中 | 某处手算坐标 | `Coords` 唯一入口 + review 检查 |
| 地图自定义导入资源 | 中 | 带自定义模型/贴图的地图 | 先支持对战图；自定义导入由 Map 工程在 gmap 转换期处理，P5 之后 |
| 客户端持续演进 | 中 | 客户端改了模拟层 | 同步脚本 + 补丁清单；每轮先同步再跑 gate |
| net8 兼容被破坏 | 中 | 客户端用了 net9+ API | 同步脚本带 net8 编译检查，报文件与行号 |
| 性能不及客户端 | 中 | 节点数过多、draw call 高 | 实例池、MultiMesh、可见性剔除；用客户端 `DrawCallCount` 作对照 |

---

## 9. 迭代规程

### 9.1 每轮

```
1. 选切片
   从当前阶段任务表取一个能被 gate 验证的最小单元，不跨阶段开工。

2. 写预期
   开工前写清：动哪些文件、哪条 gate 从红转绿、指标怎么变。

3. 实现
   模拟层保持原样，改动落在 View / Bridge。
   要碰 Client 时先确认能不能在 Bridge 侧解决。

4. 跑 gate
   构建（0 错 0 警告）→ digest（P1 起）→ 截图（P2 起）→ 界面（P4 起）。

5. 记账与提交
   提交信息：<阶段><序号> <一句话结论>
   正文写：改了什么、为什么、gate 前后数值。

6. 不达标就回退
   文件、工程引用、参数表、登记表全部还原，git status 干净。
```

### 9.2 停止判据

连续两轮 gate 无推进，停下来改计划，写清：卡在哪条 gate、已排除哪些原因、建议改哪一段。

### 9.3 不许做的事

| 禁令 | 原因 |
|---|---|
| 为了过 gate 改 gate | 判据一松，后面所有结论都不可信 |
| 在 `View/` 里直接读 `Client` 的 `Unit` | 破坏分层，语义漂移没人管得住 |
| 在 `Client/` 里写 `using Godot` | 模拟层必须能无头运行 |
| 视图里出现裸数字 | 参数必须进 `ViewTuning` 并注明出处 |
| 用 `diff -rq` 判差异 | 同路径文件行重合度低，只会说「都不一样」 |
| 把 `.gmap` 塞进 P1 | 先把 w3x 跑通 |

---

## 10. 指标表

| 阶段 | 同源文件数 | digest 一致 tick | 截图 SSIM 均值 | 清单关闭项 | 备注 |
|---|---:|---:|---:|---:|---|
| 起点 | 112 / 262 同路径 | — | — | 0 / 20 | v1 结束 |
| | | | | | |

辅助指标：

| 指标 | 起点 | 目标 |
|---|---:|---:|
| Launcher 自有代码行数 | 59,211 | ≤ 9,000 |
| 客户端源码补丁处数 | — | ≤ 2 |
| 画面参数表条目 | 0 | ≥ 40，全部带出处 |
| 构建时间 | — | ≤ 90s |

---

## 附录 A：客户端源码补丁清单

| # | 文件 | 改动 | 原因 | 影响 |
|---|---|---|---|---|
| A1 | `L3_Simulation/Simulation/Jass/JassGameNatives.Models.cs:222` | `dict.Capacity` → `dict.EnsureCapacity(n)` | `Dictionary.Capacity` 是 net9 API | 无 |
| A2 | `L1_Adapter/Formats/Jass/JassAotCompiler.cs:126` | net8 下条件编译掉 `PersistedAssemblyBuilder` 分支 | net9 API，JASS AOT 属可选优化 | 加载期不预编译，退回解释执行 |
| A3 | `L0_Foundation/ThirdParty/War3Net/*.csproj` | TFM net9.0 → net8.0；`War3Net.IO.Mpq` 补 `War3Net.Common` 直接引用 | 工程标签 | 无 |
| A4（可选，P5） | `Src/Resources/War3ResourceManager.cs` | 增加流提供者，直接读 pck 内的 mpq 字节 | 免去首次落盘 | 无 |
| A5 | `Src/Diagnostics/FdfParserTest.cs` | 不搬 | 引用 `UiFrame` / `IUiTextureResolver`，属界面层测试 | 无 |

同步脚本每次同步后跑一遍 net8 编译，报错即定位。

## 附录 B：模块对照

| 客户端 | Launcher | 处理 |
|---|---|---|
| `Src/Core`、`Abilities`、`Components`、`Skills`、`Units`、`Orders`、`Items`、`Events` | `Client/Src` | 整体搬入 |
| `L1_Adapter/Formats`、`L2_Core` | `Client/` | 整体搬入 |
| `L3_Simulation` | `Client/` | 整体搬入 |
| `L4/Game/Rendering`、`Environment` | `View/` | Godot 重写，参考参数与算法 |
| `L4/Game/UI` | `View/Hud` | Godot 重写 |
| `L4/Game/Scenes` | `View/Scene` | 只保留视图部分 |
| `L4/Game/Input` | `Bridge/Input` | 逻辑搬，设备换 Godot |
| `L4/Game/Audio` | `View/Audio` | 决策搬，播放换 Godot |
| `L4/Game/Presentation/UnitAnimationController` | `View/Units/AnimationRules` | 决策表移植 |
| `L4_Composition/Headless` | `--headless` | 参考其结构 |
| Launcher 现有 `Environment/`、`Presentation/`、`Camera/`、`UI/` | `View/` | 保留骨架，补参数 |
| Launcher 现有 `Src/`、`GameCore/L1` `L2` `L3` | 删除 | 由 Client 取代 |

## 附录 C：复现命令

```bash
# 1. 当前同源度
python3 - <<'EOF'
import os
L='Launcher'; C='test/Open-Warcraft3'
same=tot=0
for dp,_,fs in os.walk(L):
    if '/.godot' in dp or '/.git' in dp: continue
    for f in fs:
        if not f.endswith('.cs'): continue
        p=os.path.join(dp,f); rel=os.path.relpath(p,L); c=os.path.join(C,rel)
        if not os.path.exists(c): continue
        tot+=1
        same += open(p,'rb').read()==open(c,'rb').read()
print('同路径',tot,'字节一致',same)
EOF

# 2. net8 兼容检查（tools/net8-probe/probe.csproj）
#    net8.0 / LangVersion=preview / ImplicitUsings=enable / Nullable=disable
#    Compile: L1_Adapter/Formats/**、L2_Core/MapBrowser/MapInfoParser.cs、
#             Src/**（排除 Src/Diagnostics/FdfParserTest.cs）、L3_Simulation/**
#    包: MoonSharp 2.0.0 / SharpCompress 0.50.3 / War3Net.Drawing.Blp 6.0.2 /
#        System.Drawing.Common 9.0.0 / System.Formats.Nrbf 9.0.0 /
#        System.Text.Encoding.CodePages 9.0.0
dotnet build Launcher/tools/net8-probe/probe.csproj -v q --nologo 2>&1 | grep -c "error"

# 3. 两侧截图
./side_by_side.sh <gmap> <w3x> 12 /tmp/sbs

# 4. 客户端无头跑图
cd test/Open-Warcraft3 && dotnet run --project VisualStudio/OpenWC3.Headless.csproj -- \
  <map.w3x> --war3=<dir> --ticks=3000
```

## 附录 D：可复用资产

| 资产 | 用途 |
|---|---|
| `--combat-probe`（攻击矩阵 89.3 / 115.4 / 150.0） | P1 数值回归 |
| `--state-probe` 764 条断言 | P1 双引擎对照，P6 退役 |
| 判据「行为优先于字节一致」 | 继续有效 |
| 失效模式清单（`Launcher一致化任务.md` 第 6 节） | 调试排查表：`World.Tick` 语义、`Learned` 过滤、Buff 生命周期、探针走非生产路径 |
| 客户端 `Diagnostics/` 104 个诊断入口 | P6 之后替代 Launcher 自有探针 |
