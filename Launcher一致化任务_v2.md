# Launcher 一致化任务 v2：把 Launcher 做成客户端的 Godot 宿主

> 自包含文档。只读这一份就能接手并继续推进。
> 上一版（逐文件适配循环）见 `Launcher一致化任务.md`，其历史轮次见 `Launcher字节一致化路线.md`，差异清单见 `Launcher与Open-Warcraft3的差异_v2.md`。
> 基准：Launcher `HEAD`（511 个 cs / 5.9 万行），客户端 `test/Open-Warcraft3`（1,505 个 cs / 29.6 万行）。
> 写于 2026-11-13。

---

## 0. 这份文档要解决什么

v1 的做法是「拿客户端的单个 .cs 覆盖进 Launcher，补齐缺的虚成员，跑探针，过的留下」。六十余轮之后它撞到了天花板：剩下的差异全部是子系统，单个文件补不进去。

v2 换方向。判定标准从「Launcher 的实现能否被客户端文件替换」改成「Launcher 能否原样运行客户端代码」。

具体交换的东西：

| | v1 | v2 |
|---|---|---|
| 改动主体 | 客户端文件（适配进 Launcher 的基类） | Launcher 平台层（适配客户端已有的端口抽象） |
| 客户端源码改动 | 每轮都在改 | 只保留 2 处 net8 补丁 |
| 达标文件 | 逐个挑，112 个 | 整目录搬运，一次 1000+ 个 |
| 失败模式 | 每个文件各缺一个零件 | 后端接口对不上，一次暴露全部 |
| 编译状态 | 始终绿 | P1 允许红，分支上进行 |

读完 §1、§2 就能判断方向是否成立，§3 到 §5 是架构与接缝，§6 起是执行。接手的人从 §6 的 P0 开工，按 §9 的规程一轮一轮推。

---

## 1. 目标

Launcher 与客户端在**地图加载界面**和**游戏运行全程**的画面、逻辑完全一致。Launcher 是客户端的「Godot + 对战」形态：同一套模拟、同一套表现代码，换掉窗口、图形、音频、输入四层平台实现。

### 1.1 四条判据

| 判据 | 定义 | 达标线 |
|---|---|---|
| 同源度 | Launcher 内与客户端逐字节相同的 .cs 数量 | P6 结束时 ≥ 客户端核心文件的 95% |
| 逻辑等价 | 同地图、同种子、同输入脚本下逐 tick 状态摘要 | 3000 tick 内逐 tick 完全一致 |
| 画面等价 | 同机位、同 tick 截图对比 | SSIM ≥ 0.99，均值通道差 ≤ 2/255，无结构性缺失 |
| 加载等价 | 加载阶段序列、加载界面元素、文本、进度 | 阶段序列一致，界面截图达画面等价 |

画面判据要写清楚一点：**像素完全一致在不同图形后端之间不做承诺**。客户端走 OpenGL，Launcher 走 Godot 的 RenderingDevice（macOS 上是 Metal），光栅化与抗锯齿的末位差异不可消除。判据是「差异只出现在边缘与高频区，人眼不可辨」，并配一张逐像素差异图存档。

---

## 2. 现状实测

### 2.1 规模

| 区域 | 客户端 | Launcher |
|---|---:|---:|
| 模拟层（`Src/` + `L3_Simulation`） | 141,749 | 30,741（自写） |
| 数据层（`L1_Adapter/Formats` + `L2_Core`） | 17,880 | 5,691（自写） |
| 表现层（`L4_Composition/Game`） | 96,507 | 20,693（自写） |
| 平台层（宿主 + 渲染后端） | 9,226 | 2,086 |
| 合计（不含 Android、Tool、MobileMapEditor） | 265,362 | 59,211 |

客户端另有 `L4_Composition/Android` 29,227 行，Launcher 不需要。

客户端 L4 的构成（行数）：Diagnostics 33,603、UI 18,045、Scenes 11,334、Rendering 10,353、Environment 5,299、Input 4,269、Audio 3,087、Hosting 2,758、Camera 1,541、SceneGraph 598、Presentation 728。

### 2.2 逐文件适配走到了哪

| 指标 | 值 |
|---|---:|
| 同路径 .cs（两侧都存在） | 262 |
| 其中字节一致 | 112 |
| 客户端配置目录类总数 / Launcher 缺失 | 405 / 206 |
| 剩余「差 1 到 10 行」的文件 | 1 |

112 这个数字已经是逐个文件路线的极限。剩下的差距分布是 81 到 200 行（77 个文件）与 200 行以上（42 个），每一处都是「Launcher 自己写了一套等价实现」或「能力整块缺失」。继续逐文件推进，单轮收益接近零。

### 2.3 三个决定性的实测结论

这三条把 v2 从设想变成了可执行计划。

**结论一：客户端核心源码在 net8.0 下能编译，只有 2 处需要补丁。**

Godot 4.7.2 的 `GodotSharp.dll` 只有 `lib/net8.0`，随包 `GodotPlugins.runtimeconfig.json` 声明 `net8.0`。本机装了 8.0.20 时进程落在 net8 运行时，net11 的程序集载不进去。所以 Launcher 必须留在 net8.0，而客户端跑 net11.0-rc。靠卸掉 .NET 8 让它滚动到 11 的做法依赖用户机器状态，不能当交付方案。

把客户端的 Formats + Core + Simulation（15.7 万行，含 Pathing 3.3 万行）单独抽出来按 net8.0 编译，结果是：

| 阶段 | 错误数 | 性质 |
|---|---:|---|
| 缺隐式 using | 6,444 | 工程配置，非源码问题 |
| 补 ImplicitUsings 后 | 158 | 全部是缺 NuGet 包（MoonSharp、SharpCompress、War3Net.Drawing.Blp） |
| 补包引用后 | 2 | 真的 net9+ API：`Dictionary.Capacity`（1 处）、`PersistedAssemblyBuilder`（1 处） |

两处都在 JASS 相关代码里：`JassGameNatives.Models.cs:222` 用了 net9 才有的 `Dictionary<,>.Capacity`，`JassAotCompiler.cs:126` 用了 net9 的 `PersistedAssemblyBuilder`。前者改成 `EnsureCapacity`，后者在 net8 下关掉 JASS AOT 预编译（退回解释执行，只影响加载耗时）。另外 War3Net 三个 csproj 的 `TargetFramework` 要从 net9.0 改成 net8.0，并给 `War3Net.IO.Mpq` 补一条到 `War3Net.Common` 的直接引用。

**结论二：客户端的平台边界已经切好了，宿主只提供 6 个端口。**

客户端本身已经切成多层：它有自己的场景图（`SceneGraph/Scene.cs`：`BeginLoad` / `ContinueLoad` / `Tick(dt)` / `RenderFrame`）、渲染接口（`IRenderDevice`，43 行、20 个方法）、输入源（`IInputSource`）、音频端口（`IAudioPort`）、相机端口（`ICameraPort`）、文本光栅（`IGameTextRasterizer`）、音乐后端（`IGameMusicBackend`）。

而且它已经有一份非 OpenGL 的宿主实现可以照抄结构：`L4_Composition/Headless/HeadlessHost.cs` 用 `NoopCameraPort` + `NoopAudioPort` 把整套模拟和 JASS 跑起来，不碰图形。

**结论三：Godot 4.7.2 暴露了自绘所需的全部低层 API。**

在 GodotSharp 4.7.2 的 API 文档里逐条确认过：

| 需要的能力 | Godot API | 存在 |
|---|---|---|
| 取渲染设备 | `RenderingServer.GetRenderingDevice()` | 是 |
| 自己的渲染通道 | `RenderingDevice.DrawListBegin/BindRenderPipeline/BindUniformSet/DrawListDraw` | 是 |
| SPIR-V 着色器 | `RenderingDevice.ShaderCreateFromSpirV` | 是 |
| 纹理/采样器 | `RenderingDevice.TextureCreate`、`UniformType.SamplerWithTexture` | 是 |
| 把自己的纹理显示到窗口 | `RenderingServer.TextureRdCreate` + `Texture2Drd.TextureRdRid` | 是 |
| 渲染线程内执行 | `RenderingServer.CallOnRenderThread` | 是 |

这意味着客户端的渲染器可以整块搬过来，用同一套绘制代码、同一套着色器、同一批资源出画，省掉在 Godot 节点体系里重写 10 万行的活。

---

## 3. 方向：Launcher 变成客户端的 Godot 宿主

### 3.1 两条路线

| | A：宿主化（推荐） | B：表现层重写 |
|---|---|---|
| 模拟 | 客户端源码原样 | 客户端源码原样 |
| 渲染 | 客户端渲染器 + Godot RD 后端 | 用 Godot 节点重写地形/模型/特效/UI |
| 画面一致 | 同一套绘制代码，差异只在后端光栅化末位 | 永远差一层，逐项对不齐 |
| 主要风险 | RD 后端与引擎帧共存 | 10 万行表现层重写，工期不可控 |
| 失败判定 | P0 探针失败 | — |

先做 A。P0 的探针如果过不去，退回 B，并在本文 §8 记录判定依据。

### 3.2 目标架构

```
┌──────────────────────────── Launcher.csproj（Godot.NET.Sdk, net8.0）──┐
│  Platform/Godot/                                （新写，约 2 千行）    │
│    GodotHost        主循环：固定 20Hz 模拟 + 可变帧渲染                │
│    GodotRenderDevice  IRenderDevice 的 RD 实现（含 pipeline 缓存）      │
│    GodotInputSource   IInputSource / IInputContext 的 Godot 实现        │
│    GodotAudioPort     IAudioPort / IGameMusicBackend 的 Godot 实现      │
│    GodotViewport      IGameViewport                                    │
│    GodotArchiveSource IWar3ArchiveSource（pck / 目录）                 │
├────────────────────────────────────────────────────────────────────────┤
│  Client/                客户端源码 verbatim 拷贝（约 26 万行）          │
│    GameCore/L0_Foundation  L1_Adapter/Formats  L2_Core                 │
│    GameCore/L3_Simulation  L4_Composition/Game                         │
│    Src/Core Abilities Components Skills Units Orders ...               │
├────────────────────────────────────────────────────────────────────────┤
│  依赖方向：Client 不引用 Godot。Godot 后端引用 Client。                  │
└────────────────────────────────────────────────────────────────────────┘
```

编译期用三个工程把这条方向钉死：

| 工程 | 内容 | 允许引用 |
|---|---|---|
| `Client/Client.csproj` | 客户端源码 verbatim | 只有 NuGet 包与 War3Net，无 Godot |
| `Backend/Backend.Godot.csproj` | RD/输入/音频/资源端口实现 | Client + GodotSharp |
| `Launcher.csproj` | `Platform/Godot/*`、`project.godot`、场景入口 | Backend + Client |

`Client` 里一旦有人 `using Godot`，编译立刻失败。这是「客户端代码不依赖引擎」的硬保证。

### 3.3 客户端源码的三类处理

搬进来的文件分三类，全部登记，同步脚本据此校验。

| 类别 | 处理 | 当前数量 |
|---|---|---:|
| verbatim | 逐字节复制，不许改 | 除下面两类之外的全部 |
| patch | 允许最小区分，登记在附录 A | 2 处源码（A1、A2）+ 3 个 csproj 的 TFM 标签（A3） |
| exclude | 不搬 | Android、MobileMapEditor、Tool、VisualStudio、Imgui、`L1_Adapter/RHI.OpenGL`、`L4/Diagnostics` 中的平台探针 |

同步脚本 `tools/sync-client.sh`：

```bash
# 从客户端仓库拉取 verbatim 源码到 Client/，随后跑三类校验
rsync -a --delete \
  --include='*/' --include='*.cs' --exclude='*' \
  --exclude='Android/' --exclude='MobileMapEditor/' --exclude='Imgui/' \
  "$CLIENT/GameCore/" Launcher/Client/GameCore/
rsync -a --delete --include='*/' --include='*.cs' --exclude='*' \
  "$CLIENT/Src/" Launcher/Client/Src/
# 校验 1：patch 清单之外的文件必须逐字节一致
# 校验 2：Client/ 下不许出现 using Godot
# 校验 3：Client/ 不许引用 Backend/
```

客户端更新时重跑一次，差异会精确落在 Appendix A 登记的那几处。

### 3.4 范围：搬什么、不搬什么

| 客户端模块 | 处理 | 理由 |
|---|---|---|
| `Src/**`（模拟、能力、指令、寻路、配置、资源栈） | 搬 | 对战核心 |
| `L1_Adapter/Formats`、`L2_Core` | 搬 | 地图与物编解析 |
| `L3_Simulation/**`（含 Jass 3.93 万行） | 搬 | 触发器、AI、脚本 UI |
| `L4/Game` 的 `Scenes` `Rendering` `Environment` `UI` `Camera` `Presentation` `SceneGraph` `Audio` `Configuration` | 搬 | 画面与界面 |
| `L4/Game/Hosting` | 部分搬：`GameHostContext`、`MapLibrary`、`CrashLog`、`FrameRateCap`；`GameApplication` 由 `GodotHost` 重写 | 窗口与图形上下文在 Godot 侧 |
| `L4/Game/Input` | 搬交互逻辑，`Silk.NET.Input` 的类型换成端口 | 设备层换 Godot |
| `L4/Game/Diagnostics` | 挑不依赖窗口的搬：`DeterminismCheck` 与各类 `*Regression` | 回归资产 |
| 录像与回放（`World.Recording`） | 随模拟层一起搬 | 与 tick 绑定 |
| `L4/Game/Imgui` | 不搬 | 调试面板，与对战无关 |
| `L4/Android`、`MobileMapEditor`、`Tool`、`VisualStudio` | 不搬 | 平台与工具 |
| Glue 层（`MainMenuScreen`、`MapSelectionScreen`、`War3CampaignSession`、`CustomCampaignLibrary`） | P6 再评估 | Launcher 走 `--play=` 直接进图，不提供主菜单与战役 |

这条边界决定了工作量：对战进图到结算的全部逻辑与画面都在范围内，主菜单、战役、联机、编辑器辅助在范围外。

---

## 4. 宿主接缝清单

Launcher 要提供的六样东西。每一项都写明客户端契约、实现要点、风险。

### S1 渲染设备

| 项 | 内容 |
|---|---|
| 客户端契约 | `IRenderDevice`（20 个方法）+ `IBuffer` / `ITexture` / `IShaderProgram` / `IVertexArray` / `IFramebuffer` |
| 现有实现 | `L1_Adapter/RHI.OpenGL/` 共 14 个文件 1,611 行，其中资源包装 8 个；`OpenGLRenderDevice.cs` 本身只有 30 处 `_gl.` 调用，GL 细节全在包装类里 |
| Launcher 实现 | 同名命名空间 `OpenWC3.RHI.OpenGL`、同名类 `OpenGLRenderDevice`，内部换成 RD。保留类名是为了让客户端 88 处 `OpenGLRenderDevice` 引用零改动 |
| 关键难点 | 状态映射（GL 是全局状态，RD 是 pipeline 对象，需按 `(shader, blend, depth, cull, 目标格式)` 建缓存）；GLSL 330 → SPIR-V 450 离线编译；GL 深度范围 [-1,1] 与 Vulkan [0,1] 的裁剪空间差异 |
| 风险 | 高。P0 先做一个能出画的最小闭环 |

保留 `OpenGLRenderDevice` 这个类名是刻意取舍：名字带着历史包袱，但它换来 88 处引用不动、23 处 `.Shaders`、7 处 `MaxUniformBoneMatrices` / `MaxTextureArrayLayers` 也不动。命名洁癖不值这个价。

### S2 输入

| 项 | 内容 |
|---|---|
| 客户端契约 | `IInputSource`（`L0_Foundation/Input`，48 行）+ 场景内 `IInputContext` 与 `Silk.NET.Input.Key` 枚举 |
| 现有实现 | 5 个文件用 `IInputContext`，22 个文件引用 `Silk.NET.Input`（多数只用到 `Key` 枚举） |
| Launcher 实现 | 用 Godot 的 `InputEvent` 填充客户端输入事件结构；`Key` 枚举值直接照抄常量表，不依赖 Silk 包 |
| 难点 | 键位映射表要与客户端一致（尤其 F10 菜单、Esc 取消、Ctrl/Shift 组合）；文本输入（聊天、玩家名）要走 Godot 的 IME |

### S3 音频

| 项 | 内容 |
|---|---|
| 客户端契约 | `IAudioPort`（176 行：`Start/Stop/SetVolume/SetPitch/SetPosition`）+ `IGameMusicBackend` |
| 现有实现 | OpenAL（`War3AudioSystem` 24 声道 + 8 通道配额 + 优先级抢占 + 3D 锥体，全部是客户端自己的逻辑） |
| Launcher 实现 | 声道调度、优先级、循环音全部沿用客户端代码；`IAudioPort.Start` 里创建一个 `AudioStreamPlayer3D`，距离衰减在 C# 侧按 OpenAL 的 inverse-clamped 公式算成 `volume_db` 每帧写入 |
| 难点 | Godot 内建衰减曲线与 OpenAL 不同，必须自己算，不能开 `attenuation_model` |

### S4 视口与主循环

| 项 | 内容 |
|---|---|
| 客户端契约 | `SceneManager.Update(dt)` / `Render()`；场景内 `FixedDeltaTime = World.TickSeconds`（0.05，20Hz）自行累加 |
| Launcher 实现 | `_Process(delta)` 里调 `Update`，`_Process` 末尾调 `Render`；RD 绘制在 `CallOnRenderThread` 内录制，结束后把纹理贴到 `Texture2Drd` 上呈现 |
| 难点 | 帧率抖动时的累加上限要设（避免一次补 20 帧）；窗口尺寸变化要同步 `IGameViewport` |
| 分辨率 | 客户端按像素坐标布局 UI，Launcher 要保持一致的虚拟分辨率与缩放策略，否则 UI 位置会偏 |

### S5 资源来源

见第 5 节。

### S6 文本光栅与字体

| 项 | 内容 |
|---|---|
| 客户端契约 | `IGameTextRasterizer`（`Rasterize` + `MeasureAdvance`）；桌面版用 `System.Drawing`，macOS 走 `MacTextRasterizer`（SkiaSharp） |
| Launcher 实现 | 沿用 `MacTextRasterizer` 的 SkiaSharp 路径，三平台统一 |
| 注意 | `System.Drawing.Common` 在 net8 上仅 Windows 可用，任何 `using System.Drawing` 的代码路径不能在 macOS 被执行 |

---

## 5. 数据源：两条腿走路

这是全文档唯一必须先做决定、后做取舍的地方。

客户端读 War3 安装目录的 MPQ，运行期从 MPQ 里解 MDX/BLP/SLK。Launcher 的前提是 `extract.pck`，里面是转换后的 Godot 资源。

实测 `extract.pck`（988 MB）的构成：

| 类型 | 数量 |
|---|---:|
| .tres（材质/配置） | 25,980 |
| .tscn（模型场景） | 6,946 |
| .png（已解码贴图） | 6,380 |
| .wav / .mp3（音频） | 6,357 |
| .json（表格） | 3,464 |
| .glb（网格） | 3,264 |
| .fdf（界面描述） | 84 |
| .mdx / .blp | 0 |

没有 `.mdx` / `.blp`。客户端的渲染器读不了 pck 里的 `.tscn` 和 `.png`。

两条路径：

| | D1 安装目录 | D2 extract 原始镜像 |
|---|---|---|
| 做法 | Launcher 直接读 War3 目录的 MPQ，与客户端同一份资源栈 | Extract 增加一个导出模式，额外产出 `.mdx` / `.blp` / `.slk`/`.txt` 原字节 + 清单；Launcher 用目录式 `IWar3ArchiveSource` 挂载 |
| 优点 | 零转换损失，P1 立刻能跑，用来验证画面一致 | 不依赖安装目录，符合 Launcher 的定位 |
| 缺点 | 违背「Launcher 依赖 extract.pck」的前提 | pck 体积增加（原始资源约 1.5~2 GB），需要改 Extract |
| 用在哪 | P1 至 P4 的开发与验收基准 | P5 的交付形态 |

顺序：先用 D1 把引擎跑通、把画面调一致，再把资源挂载层换成 D2。切换只改 `War3ArchiveSourceRegistry` 的挂载实现，模拟与渲染代码一行不动。

`IWar3ArchiveSource` 是客户端现成的接口（`Src/Resources/War3ArchiveSourceRegistry.cs`，108 行），本来就是为「非文件系统宿主」准备的，Android 版就在用。Launcher 实现一个读 pck 的版本即可。

`.gmap` 的定位也要一起定下来：它是 Godot 资源格式的地图包，client 那套解析链读不了。P1 起 Launcher 直接吃 `.w3x`，`.gmap` 的导入导出继续由 Map 工程负责。要让 Launcher 直接开 `.gmap`，得在 P5 之后做一个 gmap → w3x 的内存转换层，属于可选增量。

Extract 的 raw 导出只增加一个模式，不改现有产物，Editor 与 Wiki 的取用方式不受影响。

---

## 6. 阶段计划

每个阶段单独可验收、单独可回退。阶段之间不重叠。

| 阶段 | 目标 | 主要交付 | 关键风险 |
|---|---|---|---|
| P0 | 证明路线成立 | RD 后端最小闭环、net8 兼容、三工程骨架 | RD 与 Godot 帧时序 |
| P1 | 逻辑同源 | 模拟层整块搬迁、JASS、w3x 直读、逐 tick digest | 旧引擎并存期的双维护 |
| P2 | 世界画面 | RD 后端补全、着色器管线、地形/模型/特效 | 后端差异导致画面对不齐 |
| P3 | 界面与加载 | FDF 界面、输入完整、加载界面 | 分辨率与布局口径 |
| P4 | 音频 | 音频端口、衰减模型、音乐调度 | 混音模型差异 |
| P5 | 资源自立 | Extract raw 导出、pck 资源源 | 体积与导出覆盖面 |
| P6 | 收尾 | 删旧实现、性能、打包 | 性能不及 GL |

### P0 立项探针

目标：证明 A 路线成立。这一阶段不追求能玩。

| 任务 | 内容 |
|---|---|
| P0.1 | 建 `Client/` `Backend/` 三工程骨架，接同步脚本 |
| P0.2 | net8 兼容：2 处补丁 + War3Net TFM + 包引用 |
| P0.3 | `GodotRenderDevice` 最小闭环：清屏 + 一个全屏四边形 + 纹理 |
| P0.4 | GLSL 330 → SPIR-V 离线管线：拿真实的 `Terrain.vert.glsl` / `Terrain.frag.glsl` 编译并在 RD 里跑通 |
| P0.5 | `Texture2Drd` 呈现 + `CallOnRenderThread` 时序 |
| P0.6 | 输入端口最小实现（键鼠 → `IInputSource`） |

验收：

- Godot 窗口里出现由 RD 绘制的地形贴图四边形，连续 600 帧无 `RenderingDevice` 错误
- `--rhi-spike` 参数启动，退出码 0，帧时间 ≤ 8ms（1080p）
- macOS（Metal）与 Windows（Vulkan）各跑一次

回退：P0.3 或 P0.4 失败 → 切 B 路线，重写本文 §3.1 与 §6。

### P1 模拟同源

目标：把客户端模拟层整块搬进来，逻辑与客户端逐 tick 相同，画面可以先没有。

| 任务 | 内容 |
|---|---|
| P1.1 | 搬 L0 / L1 / L2 / L3 与 `Src/`；删 Launcher `Src/` 与 `GameCore/L3` 里的自写模拟（3.0 万行） |
| P1.2 | `GameSession` 驱动：`world.Step()` + `JassRuntimeHost.Step()` 的 20Hz 循环 |
| P1.3 | JASS 运行时接通：`common.j` / `blizzard.j` 从资源栈读，`war3map.j` 执行 |
| P1.4 | 地图装载：w3x 直读（走客户端 `War3Map*` 解析链），替换 Launcher 的 `.gmap` 路径 |
| P1.5 | `--state-digest`：逐 tick 规范化状态摘要 |
| P1.6 | 保留旧引擎开关：`--engine=launcher` 仍走旧路径，保证主干可运行 |

digest 内容（缺一项就会漏掉一类回归）：

| 分组 | 字段 |
|---|---|
| 单位 | id、类型码、所属玩家、位置（定点化到 1e-3）、朝向、HP、MP、当前订单 id、订单目标 |
| 玩家 | 金、木、人口、人口上限、科技等级表 |
| 世界 | tick、随机数流状态、单位总数、尸体数、特效数 |
| 脚本 | JASS 全局变量哈希、触发器队列长度 |

验收：

- 5 张对战图 × 3000 tick，与客户端 `--headless --digest` 逐 tick 完全一致
- 同一输入脚本（建造、训练、交战、升本、放技能）在两侧产生同一份命令序列
- 旧 `--state-probe` 的 764 条断言中，与模拟相关的部分在 `--engine=client` 下同样通过

回退：整阶段在分支 `client-host` 上进行，主干继续用旧引擎。P1 期间允许构建红。

### P2 世界画面

目标：地形、装饰物、单位、阴影、迷雾、天空、水、天气与客户端同源出画。

| 任务 | 内容 |
|---|---|
| P2.1 | RD 后端补全：`IBuffer` / `IVertexArray` / `ITexture`（含 2DArray）/ `IFramebuffer` / `IShaderProgram` |
| P2.2 | 着色器离线编译：31 个 GLSL 文件（29 个带 `#version 330 core`，含 `#include` 片段），展开 include、改 450、编译 SPIR-V，产物随包 |
| P2.3 | 裁剪空间与深度：处理 GL 与 Vulkan 的差异，在一个已知场景上校验深度精度 |
| P2.4 | 搬 `Environment/`（地形、悬崖、水、岸浪、天空、天气、光照）与 `Rendering/`（角色、MDX、粒子、丝带、阴影、迷雾、贴花、加载画面） |
| P2.5 | 搬 `Camera/`（`War3Camera` 1,248 行）与 `Scenes/War3GameScene/Rendering.cs`、`Presentation.cs` |
| P2.6 | 资源读取切到客户端 `War3ResourceManager`（D1） |

验收：

- 10 组机位（俯视、贴地、水边、夜间、迷雾内、大规模交战）截图对比：SSIM ≥ 0.99，差异图集中在中高频边缘
- draw call 计数与客户端同量级（±15%）
- 单位动画（站立、行走、攻击、死亡、施法）逐帧抽检 20 组，骨骼姿态一致

### P3 界面、交互与加载

| 任务 | 内容 |
|---|---|
| P3.1 | 搬 `UI/`（18,045 行，FDF 帧树）与 `Scenes/War3GameScene/Ui.cs` |
| P3.2 | 命令卡、队列格、英雄栏、小地图、聊天、任务/排行榜/多面板 |
| P3.3 | 输入完整化：选择、编队（含 Shift 并入）、右键智能指令、热键、滚轮、边缘滚屏、Esc/F10 |
| P3.4 | 加载界面：`War3LoadingScreenRenderer` + 进度上报（`IWar3LoadingProgress`）+ 场景切换 |
| P3.5 | 光标、选择圈、浮动文字、伤害数字 |

验收：

- 输入回放脚本 200 条（选择、移动、建造、训练、施法、编队、小地图点击）→ 命令日志与客户端逐条一致
- 加载界面、主 HUD、命令卡、任务面板、结算界面逐屏截图达画面等价
- 加载阶段序列（资源装载 → 地形生成 → 单位创建 → 脚本启动）与客户端一致

### P4 音频

| 任务 | 内容 |
|---|---|
| P4.1 | `IAudioPort` 的 Godot 实现，3D 衰减在 C# 侧计算 |
| P4.2 | 音乐双通道、间隔、主题曲压背景 |
| P4.3 | 单位语音、环境音、天气音、UI 音效 |

验收：

- 音频事件日志（资源名、通道、优先级、起始 tick、音量）与客户端一致
- 5 个距离点（100 / 500 / 1500 / 3000 / 5000）的音量差 ≤ 0.5 dB

### P5 资源自立

| 任务 | 内容 |
|---|---|
| P5.1 | Extract 增加 raw 导出模式（mdx / blp / slk / txt / fdf 原字节 + 清单） |
| P5.2 | `IWar3ArchiveSource` 的 pck 实现 + 目录式资源 |
| P5.3 | 去掉对 War3 安装目录的依赖 |

验收：在没装 War3 的机器上，P1 到 P3 的全部门槛复跑通过。

### P6 收尾

| 任务 | 内容 |
|---|---|
| P6.1 | 删除 Launcher 旧实现（`Src/` 剩余部分、`GameCore/L4` 自写表现层） |
| P6.2 | 删除旧 `--state-probe`，改用客户端 `Diagnostics/` 的 104 个诊断入口 |
| P6.3 | 性能基线：draw call、CPU 帧时间、内存、加载耗时 |
| P6.4 | 打包（macOS / Windows），启动参数收敛 |

验收：1v1 中后期（80 单位 + 60 建筑 + 特效）帧时间 ≤ 客户端 × 1.1；包体与启动时间记录进指标表。

---

## 7. 验证体系

四条独立的验证线，互不替代。

### 7.1 逻辑：逐 tick 摘要

```bash
# 客户端
OpenWC3.Headless <map.w3x> --war3=<dir> --ticks=3000 --digest
# Launcher
Godot --path Launcher --headless -- --play=<map.w3x> --ticks=3000 --digest
```

比较方式：两边输出 `tick<TAB>digest` 行，`diff` 必须为空。首个不一致的 tick 打印完整状态差异（单位逐个对比），这是定位回归最快的入口。

### 7.2 画面：机位快照

```bash
# 已有脚本 side_by_side.sh 的扩展版
tools/shot-compare.sh <map.w3x> <镜头预设> <tick>
#   → out/launcher.png、out/client.png、out/diff.png、out/metrics.txt
```

指标：SSIM、均值绝对差、差异像素占比，外加一份掩码图（标出差异位置）。判定看三件事：指标达线、差异不含整块缺失、差异位置在边缘/高频区。

批量：10 个机位 × 3 个时间点（0s、30s、180s），一次跑完约 5 分钟。

### 7.3 交互：输入回放

脚本格式（纯文本，每行一个动作，按 tick 执行）：

```
0    select 0 12 34        # 世界坐标
0    move 1500 2400
40   train hfoo
120  cast AHea 3
```

两侧执行同一脚本，比对命令日志（tick、玩家、命令类型、目标）与最终 digest。

### 7.4 音频与性能

| 项 | 手段 |
|---|---|
| 音频 | 在 `IAudioPort.Start/Stop/SetVolume` 打事件日志，两侧比对 |
| draw call | `OpenGLRenderDevice.DrawCallCount` 已是现成计数，RD 后端同样实现 |
| 帧时间 | `CpuFrameProfiler` + `GpuFrameProfiler` 两侧同一套 |
| 内存 | 加载完成后与 180s 时的 managed heap + 纹理字节数 |

---

## 8. 风险与预案

| 风险 | 影响 | 触发条件 | 预案 |
|---|---|---|---|
| RD 与引擎帧时序冲突 | 致命 | P0.3 无法在 `CallOnRenderThread` 内完成一次完整绘制 | 改为渲染到自有 framebuffer 后 blit 到 `Texture2Drd`；仍不行则切 B 路线 |
| Metal/Vulkan 差异导致画面不达线 | 高 | SSIM < 0.99 且差距来自着色器 | 逐着色器替换为等价实现，保留一份「已知差异」清单，按结构相似度放行 |
| GLSL→SPIR-V 语义差异 | 中 | 编译失败或运行结果不同（纹理采样、精度） | glslang 固定版本；对 `sampler2DArray`、`uniform` 数组逐条验证 |
| 深度范围差异 | 中 | 地形与模型 z-fighting、阴影偏移 | 投影矩阵统一改写；专门做一个深度精度场景 |
| 文本光栅不一致 | 中 | 字体边缘、字宽不同 | 统一走 SkiaSharp；`MeasureAdvance` 与客户端同源 |
| 音频衰减模型差异 | 低 | 距离音量曲线不一致 | 在 C# 侧算 OpenAL 公式，不依赖 Godot 的衰减模型 |
| 性能不及客户端 | 高 | RD 每 draw 的 CPU 开销高于 GL | 沿用客户端的 instancing 与批次；必要时把状态切换合并成 pipeline 变体表 |
| 客户端持续演进 | 中 | 客户端改了模拟层 | 同步脚本 + 三类登记；每轮先跑一次同步再跑 gate |
| net8 兼容被新代码破坏 | 中 | 客户端用了 net9+ API | 同步脚本里带一条 net8 编译检查，失败即报出文件与行号 |
| 一次性搬迁过大 | 中 | P1 一次删掉 3.0 万行 | 新旧引擎并存，`--engine=` 开关保留到 P6 |

---

## 9. 迭代规程

### 9.1 每轮

```
1. 选切片
   从当前阶段的任务表取一个「能被 gate 验证」的最小单元。
   禁止跨两个阶段同时开工。

2. 写预期
   开工前写清：这轮要动哪些文件、预期哪条 gate 从红转绿、指标怎么变。
   没有预期的一轮不计入收益。

3. 实现
   优先让客户端代码保持原样，改动落在 Client 之外的层。
   确实要碰客户端源码时，先确认能不能在 Backend 侧解决。

4. 跑 gate
   构建（0 错 0 警告）→ digest（P1 起）→ 截图（P2 起）→ 输入回放（P3 起）。

5. 记账与提交
   提交信息格式：<阶段><序号> <一句话结论>
   正文写：改了什么、为什么、gate 前后数值。
   同时更新 §10 指标表。

6. 不达标就回退
   回退要彻底：文件、工程引用、登记表、探针改动全部还原，git status 干净。
```

### 9.2 停止判据

连续两轮 gate 无任何指标推进，停下来改计划，写清：卡在哪条 gate、已排除哪些原因、建议改哪一段计划。继续加轮次只会烧时间。

### 9.3 不许做的事

| 禁令 | 原因 |
|---|---|
| 为了过 gate 改 gate | 判据一旦松动，后面所有结论都不可信 |
| 为单个技能/单个界面加特例 | v1 的六十余轮已经证明这条路不收敛 |
| 在 `Client/` 里写 `using Godot` | 破坏分层，后面无法判断谁依赖了引擎 |
| 让 Backend 反向依赖 Launcher | 同上 |
| 用 `diff -rq` 判断差异 | 同路径文件行重合度很低，它只会说「都不一样」 |
| 把 `.gmap` 转换塞进 P1 | 先把 w3x 跑通，gmap 是 P5 之后的可选容器 |

---

## 10. 指标表

每轮结束追加一行。只记事实。

| 阶段 | 同源文件数 | digest 一致 tick | 截图 SSIM 均值 | draw call 比 | 备注 |
|---|---:|---:|---:|---:|---|
| 起点（v1 结束） | 112 / 262 同路径 | — | — | — | 逐文件路线终止 |
| | | | | | |

辅助指标（不进主表）：

| 指标 | 起点 | 目标 |
|---|---:|---:|
| Launcher 自写代码行数 | 59,211 | ≤ 6,000 |
| 客户端源码补丁处数 | — | ≤ 2 |
| 构建时间 | — | ≤ 90s |

---

## 附录 A：net8 补丁清单

客户端源码在 Launcher 内只允许两处偏离，另有一处工程标签调整。

| # | 文件 | 改动 | 原因 | 影响 |
|---|---|---|---|---|
| A1 | `GameCore/L3_Simulation/Simulation/Jass/JassGameNatives.Models.cs:222` | `dict.Capacity` → `dict.EnsureCapacity(n)` | `Dictionary.Capacity` 是 net9 API | 无 |
| A2 | `GameCore/L1_Adapter/Formats/Jass/JassAotCompiler.cs:126` | net8 下条件编译掉 `PersistedAssemblyBuilder` 分支 | net9 API，且 JASS AOT 是可选优化 | 加载期 JASS 预编译不可用，退回解释执行 |
| A3 | `GameCore/L0_Foundation/ThirdParty/War3Net/*.csproj` | `net9.0` → `net8.0`，`War3Net.IO.Mpq` 补 `War3Net.Common` 直接引用 | 工程标签，非源码 | 无 |

同步脚本对 A1/A2 做定点还原检查：每次同步后跑一遍 net8 编译，报错即定位。

## 附录 B：模块对照

| 客户端 | Launcher 对应 | 处理 |
|---|---|---|
| `Src/Core`（含 Pathing） | `Launcher/Src/Core` | 整体替换 |
| `Src/Abilities` `Skills` `Units` `Orders` `Items` `Components` | `Launcher/Src/*` | 整体替换 |
| `GameCore/L1_Adapter/Formats` | `Launcher/GameCore/L1_Adapter/Formats` | 整体替换 |
| `GameCore/L3_Simulation` | `Launcher/GameCore/L3_Simulation` | 整体替换 |
| `GameCore/L4_Composition/Game` | `Launcher/GameCore/L4_Composition/Game` | 整体替换（Diagnostics 部分剔除） |
| `GameCore/L1_Adapter/RHI.OpenGL` | `Launcher/Backend/RHI.Godot` | 重写，保留类型名 |
| `GameCore/L4_Composition/Game/Hosting/GameApplication.cs` | `Launcher/Platform/Godot/GodotHost.cs` | 重写 |
| `GameCore/L4_Composition/Headless` | `--headless` 参数路径 | 参考其结构 |
| `GameCore/L4_Composition/Android` | 不搬 | 剔除 |
| `GameCore/L0_Foundation/Input` | `Launcher/Backend/Input.Godot` | 实现端口 |
| `Src/Ports/IAudioPort` | `Launcher/Backend/Audio.Godot` | 实现端口 |
| `Launcher/Platform/Godot/Extensions`（gmap 解析） | 保留 | P5 之后再决定去留 |

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

# 2. net8 兼容检查（把核心源码单独编一次）
#    tools/net8-probe/probe.csproj 的配置：
#      TargetFramework=net8.0, LangVersion=preview, ImplicitUsings=enable,
#      Nullable=disable, EnableDefaultCompileItems=false
#      Compile: L1_Adapter/Formats/**、L2_Core/MapBrowser/MapInfoParser.cs、
#               Src/**（排除 Src/Diagnostics/FdfParserTest.cs）、L3_Simulation/**
#      包: MoonSharp 2.0.0 / SharpCompress 0.50.3 / War3Net.Drawing.Blp 6.0.2 /
#          System.Drawing.Common 9.0.0 / System.Formats.Nrbf 9.0.0 /
#          System.Text.Encoding.CodePages 9.0.0
#      引用: War3Net.IO.Mpq.csproj（其 TFM 已按附录 A3 改为 net8.0）
dotnet build Launcher/tools/net8-probe/probe.csproj -v q --nologo 2>&1 | grep -c "error"

# 3. 两侧截图（P0 阶段先用现有 side_by_side.sh，P2 起换 shot-compare.sh）
./side_by_side.sh test.gmap test.w3x 12 /tmp/sbs

# 4. 客户端无头跑图
cd test/Open-Warcraft3 && dotnet run --project VisualStudio/OpenWC3.Headless.csproj -- \
  <map.w3x> --war3=<dir> --ticks=3000
```

## 附录 D：v1 留下的可复用资产

| 资产 | 用途 |
|---|---|
| `--combat-probe`（攻击矩阵 89.3 / 115.4 / 150.0） | P1 的数值回归，与客户端同一套公式对照 |
| `--state-probe` 764 条断言 | P1 期间双引擎对照，P6 后由客户端 Diagnostics 取代 |
| `判据「行为优先于字节一致」` | 继续有效：文件一致但功能削弱的一律作废 |
| 六十余轮记录的失效模式清单（`Launcher一致化任务.md` 第 6 节） | P2/P3 调试时的第一手排查表：`World.Tick` 语义、`Learned` 过滤、Buff 生命周期、探针走非生产路径 |
