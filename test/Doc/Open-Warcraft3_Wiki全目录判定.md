# Open-Warcraft3_Wiki 全目录判定

这份表用来回答一个问题：客户端仓库里哪些目录的代码，在「Godot + 只打对战」这个前提下能拿来用，哪些要改造，哪些该丢。后面所有搬运按这里的分层顺序推进。

判定不看好坏，只看三条硬指标：

1. **后端绑定**：代码里出现 `Godot`、`OpenWC3.UI`、`OpenWC3.RHI`、`System.Drawing`、`System.Windows.Forms`、`Silk`（客户端窗口/输入后端）、`War3Net`（MPQ 库）、`OpenAL`、`MonoGame`、`Veldrid`、`Imgui`、`SharpCompress`、`NAudio` 的，都要改造或丢弃。
2. **JASS 驱动**：出现 `JassGameNatives`、`JassVm`、`IJassEngine` 的，属于「不做 JASS」范围。
3. **功能归属**：存档、录像、网络同步、战役、平台专用（Android/Mac）、客户端调试工具，都不在对战范围内。

> 口径：文件数按 `.cs` 计，行数为物理行；「后端」「JASS」是该目录里**命中**这两个模式的**文件数**；「已搬」是与 Launcher 相对路径同名且已落地的文件数。统计时排除 `obj/`、`bin/`。

---

## 一、总览

| 顶层目录 | 文件 | 行数 | 后端 | JASS | 判定 |
|---|---|---|---|---|---|
| `Src/` | 470 | 72094 | 8 | 1 | **主体已搬**（460/470），只剩 `Src/Resources` |
| `GameCore/L0_Foundation` | 77 | 7316 | 74 | 0 | 只有 `Input` 可用 |
| `GameCore/L1_Adapter` | 90 | 16524 | 16 | 26 | 只有 `Formats`/`Text` 两件可用 |
| `GameCore/L2_Core` | 36 | 4040 | 35 | 0 | 客户端 IMGUI/GDI+，基本不搬 |
| `GameCore/L3_Simulation` | 376 | 69655 | 7 | 130 | `Configuration` 是技能层主力，`Jass` 不做 |
| `GameCore/L4_Composition` | 456 | 126257 | 179 | 72 | 客户端呈现层，按子系统转换搬 |
| `GameCore/L5_Consumer` | — | — | — | — | 无源码（只剩 `obj/` 构建产物） |
| `Tool/` | 1104 | 139205 | 283 | 464 | 客户端工具/测试，不搬 |
| `MobileMapEditor/` | 34 | 4298 | 10 | 0 | 移动端地图编辑器，不搬 |
| `Recomp/`、`Static/`、`VisualStudio/`、`Mac/`、`WineFex/`、`[1]Reference/`、`docs/`、`logs/` | — | — | — | — | 文档、脚本、IDE 配置、逆向参考，无运行时代码 |

---

## 二、`Src/`：模拟层主体（已搬）

`Src/` 是客户端的模拟层，与宿主解耦得最干净：470 个文件里只有 8 个命中后端模式、1 个命中 JASS，已落地 460 个。**除了 `Src/Resources`，其余全部已搬进 Launcher。**

### 2.1 目录分工

| 子目录 | 文件 | 行数 | 已搬 | 职责 |
|---|---|---|---|---|
| `Core/` | 63 | 8252 | 62 | 值类型与核心模型：`Vec2`、`DamageData`、`Weapon`、`AttackType`/`DefenseType`、`TargetFlags`/`TargetRules`、`Effect`、`Projectile`、`Footprint`、`CollisionSystem`、`FogOfWar`、`TerrainGrid`、`World.*` partial、`Ability*Effect` |
| `Core/Pathing/` | 245 | 21000+ | 245 | 寻路全栈，见 2.2 |
| `Units/` | 18 | 1719 | 18 | `Unit` 主体与各 partial（属性、生命、物品、战斗、状态） |
| `Components/` | 25 | 3215 | 25 | 单位组件：`Transform`、`Death`、`AbilityContainer`、`State`、`Buff` 等 |
| `Skills/` | 42 | 2662 | 42 | 被动技能实现（光环、暴击、闪避、法术护盾等） |
| `Abilities/` | 34 | 4613 | 34 | 主动能力基类与指令形态（`CAbilityAttack`、`CRallyPoint`、`RuntimeAbilityFactory`） |
| `Orders/` | 11 | 1509 | 11 | 订单模型 `COrder`/`COrderTarget`/`COrderPoint`、建造、施法、运输 |
| `Events/` | 5 | 474 | 5 | 事件总线、触发器（条件/动作模型） |
| `Ports/` | 5 | 253 | 5 | 宿主端口：`IAudioPort`、`ICameraPort`、`IPickingQuery`、`IGameSession`、`PlayerCommand` |
| `Items/` | 3 | 182 | 3 | 物品加成、充能、使用事件 |
| `Configuration/` | 3 | 1172 | 3 | `War3GameplayConfig`、`War3MapCompatibility`、浮动文字样式 |
| `Diagnostics/` | 9 | 13498 | 7 | 客户端自检（路径契约、FDF/JASS 解析器测试、`GameLogger`） |
| `Resources/` | 7 | 1266 | **0** | MPQ 归档、资源管理、Mod 库 —— **不搬**：依赖 `SharpCompress`，Launcher 运行时不读 MPQ（走 extract.pck） |

### 2.2 `Src/Core/Pathing/` 细分

客户端把寻路库内置在这个目录里（命名空间 `StandalonePathing.*`，不是外部工程），一共 245 个文件，全部零后端、零 JASS，已整体搬完。

| 子目录 | 文件 | 职责 |
|---|---|---|
| `NativeMirror/Combat` | 43 | 目标评分、攻击目标选择 |
| `NativeMirror/Execution` | 31 | 移动执行、单位运动、速度授予 |
| `NativeMirror/Orders` | 24 | 订单上下文、任务归属、空订单延续 |
| `NativeMirror/Abilities` | 23 | 能力驱动的移动/攻击接入 |
| `NativeMirror/Placement` | 22 | 放置解算、出生点占位 |
| `NativeMirror/Sim` | 22 | 仿真骨架、可寻路判定 |
| `NativeMirror/Core` | 14 | 软浮点、空间查询、WPM/W3E 网格读取 |
| `NativeMirror/Cluster` | 6 | 编队簇、形心 |
| `NativeMirror/Commands` | 6 | 命令派发 |
| `NativeMirror/Unit` | 7 | 代理事件与引用 |
| `NativeMirror/Search` | 3 | 搜索算子 |
| `NativeMirror/Cursor` | 2 | 路径游标 |
| `Move` | 13 | 移动执行、成员角度/速度提交 |
| `Map` | 8 | 邻近图、可走性 |
| `Group` | 7 | 编队、移动请求分配 |
| `Search` | 6 | 粗/细/出口搜索 |
| `Contact` | 5 | 碰撞分离、footprint 接触 |
| 根 | 3 | 导航轨迹、静态金字塔桥接 |

---

## 三、`GameCore/L0_Foundation`：地基

**逐个核对结论**：77 个文件，已落地 5 个（`Input` 3 + Launcher 自己的 `RHI/ITexture`、`RHI/Enums`）；剩下 72 个全部判定不搬，且都有替代（见下表）。此层已结清，无待搬项。

| 子目录 | 文件 | 已搬 | 判定 | Godot/Launcher 替代 |
|---|---|---|---|---|
| `Input/` | 3 | 3 | **已搬** | `Key`/`KeyModifiers`/`PointerEvent`/`IInputSource` 是纯抽象，由 Godot `InputEvent` 侧喂 |
| `RHI/` | 7 | 2 | 5 个不搬 | 渲染抽象由 Godot 直接提供：`ITexture`→`Texture2D`、`IShaderProgram`→`ShaderMaterial`、`IVertexArray`/`IBuffer`→`ArrayMesh`/`ImmediateMesh`、`IFramebuffer`→`SubViewport`、`IRenderDevice`→`RenderingServer`。Launcher 只保留自己那份 `ITexture`/`Enums`，给 `L2_Core/UI/Fdf` 的纹理解析用 |
| `ThirdParty/War3Net/` | 67 | 0 | 不搬 | MPQ 读取移到**构建期**：Extract 把 MPQ 解成 `extract.pck` 与 `.gmap`，运行时零 MPQ 代码 |

> 注意 `RHI` 这 5 个接口没有等价的抽象层：Launcher 直接写 Godot 类型。这意味着**任何依赖 RHI 抽象的上层代码搬到 Launcher 都要改写**（`L4/Game/Rendering` 的 43 个文件首当其冲），搬之前要先决定是「直接换 Godot 调用」还是「在 Godot 之上再造一层 RHI 壳」。

---

## 四、`GameCore/L1_Adapter`：格式解析

**逐个核对结论**：90 个文件，已落地 3 个（`Formats/War3Pathing`、`Formats/Text`、`Formats/Jass/JassNativeReal`）；剩下 87 个分属 JASS 38 / SaveGame 29 / Trigger 6 / OpenGL 14，全部判定不搬，替代与缺口见下表。此层已结清，无待搬项。

| 子目录 | 文件 | 已搬 | 判定 | Godot/Launcher 替代 |
|---|---|---|---|---|
| `Formats/`（根） | 1 | 1 | 已搬 | 可走性/路径纹理 |
| `Formats/Text` | 1 | 1 | 已搬 | 文本解码 |
| `Formats/Jass` | 39 | 1 | 38 不搬 | JASS 运行时的功能由 C# 规则顶替：八步 melee 初始化在 `MeleeRules`、掉落表在 `War3MapUnits.UnitItemDropSet`、VictoryDefeat 在 `Environment`。**地图自定义脚本不执行**（范围排除）。`JassNativeReal` 例外保留：它是 32 位浮点仿真，`War3GameplayConfig` 的伤害/经验公式依赖它 |
| `Formats/SaveGame` | 29 | 0 | 不搬 | 无替代：Launcher 不做存档与网络同步。`NetGameStore`/zlib 仿真/能力状态序列化整体不复现 |
| `Formats/Trigger` | 6 | 0 | 不搬 | 无替代：`war3map.wtg`/`wct` 是编辑器触发树。对战图实测只有 1 个触发器（`Trig_Melee_Initialization_Actions`），其语义已由 `MeleeRules` 覆盖 |
| `RHI.OpenGL` | 14 | 0 | 不搬 | Godot 渲染器（`RenderingServer`/`ShaderMaterial`） |

---

## 五、`GameCore/L2_Core`：客户端 UI 与地图浏览

**逐个核对结论**：36 个文件，已落地 32 个；剩下 4 个（`MapBrowser` 的 `BlpDecoder`/`TgaDecoder`/`MapArchive`/`MainForm`）全部有构建期或 Godot 侧替代，**无缺口**。此层已结清，无待搬项。

| 子目录 | 文件 | 已搬 | 判定 | 替代 |
|---|---|---|---|---|
| `UI/Fdf` | 7 | 7 | 已搬 | — |
| `UI/Frame` | 7 | 7 | 已搬 | — |
| `UI/Frames` | 9 | 9 | 已搬 | — |
| `UI/Events` | 4 | 4 | 已搬 | — |
| `UI/Render` | 1 | 1 | 已搬 | — |
| `UI`（根） | 2 | 2 | 已搬 | — |
| `MapBrowser/MapInfoParser` | 1 | 1 | 已搬（Launcher 自写版） | w3i 解析在构建期完成：Launcher 版里 0 个 `Read*` 调用，它读的是 gmap 里的模型 |
| `MapBrowser/TerrainParser` | 1 | 1 | 已搬（Launcher 自写版） | w3e 解析同上，构建期完成 |
| `MapBrowser/BlpDecoder` | 1 | 0 | 不搬 | 构建期把 `war3mapMap.blp` 转成 TGA；运行时 Godot `Image.LoadTgaFromBuffer`（`MapThumbnailCache` 已在用） |
| `MapBrowser/TgaDecoder` | 1 | 0 | 不搬 | 同上，Godot `Image.LoadTgaFromBuffer` |
| `MapBrowser/MapArchive` | 1 | 0 | 不搬 | `.gmap` 容器替代，`Assets` 直接扫 `.gmap` |
| `MapBrowser/MainForm` | 1 | 0 | 不搬 | Launcher 主菜单的地图列表（`Assets.cs` 扫 `.gmap`）与现有地图选择界面 |

---

## 六、`GameCore/L3_Simulation`：技能与规则（业务层主力）

| 子目录 | 文件 | 行数 | 后端 | JASS | 已搬 | 判定 |
|---|---|---|---|---|---|---|
| `Simulation/Configuration` | 230 | 29307 | 2 | 6 | 137 | **搬**：物编规则与全部 `Runtime*Ability` 技能实现。这是「两侧表现一致」的核心，也是 Launcher 现有 153 个技能实现的替换对象 |
| `Simulation/Jass` | 138 | 39272 | 2 | 124 | 0 | **不搬**：JASS 运行时、原生函数表、VM 执行 |
| `Simulation/Maps` | 3 | 644 | 3 | 0 | 2 | **搬**：`war3map.doo`/`war3mapUnits.doo`/脚本单位解析。用 War3Net 读档的部分换成 Launcher 的 gmap 读取 |
| `Simulation/AI` | 3 | 312 | 0 | 0 | 0 | 搬：对战 AI 决策策略 |
| `Simulation`（根） | 2 | 120 | 0 | 0 | 0 | 搬 |

---

## 七、`GameCore/L4_Composition`：客户端呈现层

456 个文件、12.6 万行，是最大的一个父目录。179 个文件命中后端、72 个命中 JASS。这一层不能整搬，要按子系统拆，逐个把 MonoGame/OpenAL/Silk 换成 Godot。

| 子目录 | 文件 | 行数 | 后端 | JASS | 已搬 | 判定 |
|---|---|---|---|---|---|---|
| `Game/Audio` | 18 | 3087 | 3 | 0 | 10 | **搬 15 件**：音表、音效叠放、语音宏、音量分组、音乐目录/播放器、Ack/UI/循环音服务。`War3AudioSystem`(1022 行)、`War3OpenAlConfiguration`、`War3AudioContext` 三件是 OpenAL 驱动 → 换 Godot 音频 |
| `Game/Camera` | 3 | 1541 | 2 | 0 | 3 | **转换搬**：`CinematicModelCamera` 纯搬；`War3Camera`(1248 行) 去掉 `War3Net` 依赖；`War3CameraComponent` 的 `Silk.NET.Input` 换 Godot 输入 |
| `Game/Presentation` | 8 | 728 | 2 | 0 | 3 | **搬 6 件**：`UnitAnimationController`、`UnitStateColor`、`NativeFacingPresentation` 等；两个渲染件换 Godot |
| `Game/Environment` | 24 | 5299 | 10 | 2 | 13 | 转换搬：地形、光照、天气、迷雾呈现 |
| `Game/Rendering` | 43 | 10467 | 25 | 3 | 16 | 转换搬：模型/材质/贴图加载改 Godot 节点与 `ShaderMaterial` |
| `Game/Input` | 20 | 4269 | 8 | 7 | 6 | 转换搬：拾取、选择、命令交互逻辑可留，取键来源换 Godot `InputEvent` |
| `Game/Hosting` | 19 | 2758 | 6 | 0 | 1 | 转换搬：应用生命周期、加载阶段编排 |
| `Game/UI` | 86 | 17267 | 54 | 21 | 35 | **与现有 FDF 合并**：客户端 HUD 各帧；Launcher 已有 `HudFdfUi`/`FdfBindGen`，按帧逐个对齐 |
| `Game/UI/Generated` | 8 | 778 | 8 | 0 | 6 | 生成物，随 `Game/UI` 处理 |
| `Game/Scenes/War3GameScene` | 23 | 11294 | 11 | 8 | 10 | 场景编排，按功能拆开搬（战区、天气、过场） |
| `Game/Scenes`（根） | 1 | 40 | 0 | 0 | 0 | 参考 |
| `Game/Diagnostics` | 103 | 29221 | 24 | 20 | 1 | **不搬**：客户端调试工具（对照截图、JASS 审阅、路径校验） |
| `Game/Diagnostics/JassReviews` | 12 | 4382 | 1 | 8 | 0 | 不搬：JASS 审阅脚本 |
| `Game/Imgui` | 3 | 2037 | 1 | 0 | 0 | 不搬：调试 IMGUI |
| `Game/Jass` | 2 | 267 | 0 | 0 | 0 | 不搬：JASS 端口实现（`AudioPort`/`CameraPort`） |
| `Game/SceneGraph` | 7 | 598 | 0 | 0 | 0 | 不搬：自研场景图，Godot 有场景树 |
| `Headless` | 8 | 409 | 0 | 0 | 0 | 参考：无头宿主与 noop 端口，可对照 Launcher 的探针体系 |
| `Android` | 36 | 9528 | 8 | 2 | 0 | 不搬：安卓宿主 |
| `Android/Ui` | 22 | 19699 | 11 | 1 | 0 | 不搬：安卓触屏 UI |
| `Game/Configuration` | 6 | 925 | 3 | 0 | 3 | 部分已搬：输入/视频等配置项 |
| `Game`（根） | 4 | 1663 | 2 | 0 | 1 | 按文件挑 |

---

## 八、`Tool/` 与其它

| 目录 | 文件 | 判定 |
|---|---|---|
| `Tool/Tests/Game` | 982 | 不搬：客户端测试集（含技能模板、法力/伤害断言），只作对照 |
| `Tool/Tests/*`（其余） | 51 | 不搬 |
| `Tool/Research` | 23 | 不搬：针对具体 RPG 图的逆向分析 |
| `Tool/Scripts` | 19 | 不搬：地图审计脚本 |
| `Tool/Performance` | 15 | 不搬：性能剖析 |
| `Tool/Re` | 5 | 不搬：JASS 语义探针 |
| `Tool/NativeAotTypeNames` | 7 | 不搬：AOT 类型名导出 |
| `Tool/MpqExtract`、`Tool/Packaging`、`Tool/ShaderPack`、`Tool/ActivationAdmin`、`Tool/ActivationBot` | 各 0–1 | 不搬：打包/发布/授权工具 |
| `MobileMapEditor/` | 34 | 不搬：移动端地图编辑器 |
| `Recomp/`、`docs/`、`Static/` | — | 不搬：逆向阶段文档与静态资源 |

---

## 九、搬运时需要的平台替换

| 客户端依赖 | 替换目标 | 涉及目录 |
|---|---|---|
| OpenAL（`War3AudioSystem`、`War3OpenAlConfiguration`、`War3AudioContext`） | Godot `AudioStreamPlayer` + `AudioServer` 总线，保留客户端的音量分组/距离门语义 | `L4/Game/Audio` |
| Silk.NET（窗口与输入） | Godot `InputEvent`，接到现有 `War3InputComponent` | `L4/Game/Camera`、`L4/Game/Input` |
| MonoGame 渲染（顶点/纹理/材质） | Godot 节点 + `ShaderMaterial` + `Texture2D` | `L4/Game/Rendering`、`Environment`、`Presentation` |
| `War3Net.IO.Mpq` | Launcher 现有 gmap 读取 | `L3/Simulation/Maps`、`L4/Game/Camera` |
| `System.Drawing`（BLP/TGA/GDI+） | Godot `Image`，必要时自写 BLP 解码 | `L2_Core/MapBrowser` |
| 自研 `SceneGraph` | Godot 场景树与 `Node` 组件 | `L4/Game/SceneGraph` |

---

## 十、层间依赖

按特征类型做的跨层引用统计（行=被检查层，列=它引用了谁，单位是文件数）：

| | L0 | L1 | L2 | L3 | L4 |
|---|---|---|---|---|---|
| **L0** | — | 20 | 0 | 6 | **0** |
| **L1** | 16 | — | 0 | 16 | **0** |
| **L2** | 10 | 5 | — | 5 | **0** |
| **L3** | 19 | 92 | 1 | — | **4** |
| **L4** | 77 | 81 | 37 | 264 | — |

- **L4 依赖下面全部**：引用 L3 264 个文件、L1 81、L0 77、L2 37。L4 只能靠 L3 及以下支撑。
- **L0/L1/L2 没有反向引用**：L1 只引用 L0，L2 只引用 L0/L1（另有 5 处引用 L3 的地图类型）。
- **唯一的反向引用是 L3→L4 的 4 个文件**，全在 `Simulation/Jass/`（`JassGameNatives.cs`、`JassRuntimeHost.cs`、`JassGameNatives.Environment.cs`、`JassGameNatives.Sounds.cs`，直接用 `War3AudioSystem`）。JASS 整层不搬，删掉这 4 个，反向依赖归零。

> 层次顺序只解决「能不能编过」，不解决「要不要替换」。L0–L2 在 Launcher 里没有同名实现，搬进来是新增；从 L3 起两边同名（`World`/`Unit`/`Ability`/技能层），必须二选一。目前 `World`、`Unit`、`Ability` 基类已换成客户端版，所以真正的前沿是 L3。

---

## 十一、执行顺序

按依赖顺序推进，每批单独编译、单独报错误数，能收敛才进下一批。

| 批次 | 内容 | 规模 | 依赖已就位 | 验收 |
|---|---|---|---|---|
| **B1** | `L3/Simulation/Maps` 3 件（`war3map.doo`/`war3mapUnits.doo`/脚本单位） | 3 / 644 | L1 解析层已搬 | 预摆单位、掉落表解析与客户端逐字段一致 |
| **B2** | `L3/Simulation/Configuration` 230 件（技能层整体替换） | 230 / 29307 | `Unit`/`World`/`Ability` 基类已是客户端版 | 1222 条 `CS0115` 收敛，技能行为对齐 |
| **B3** | `L4/Game/Audio` 15 个逻辑件，驱动换 Godot | 15 / ~2500 | L3 的 `World`/`Unit` 已就位 | 音效/音乐/语音宏行为一致 |
| **B4** | `L4/Game/Presentation` 6 件 + `Camera` 3 件 | 9 / ~2100 | 同上 | 朝向、状态色、动画、相机跟随与震动一致 |
| **B5** | `L4/Game/{Rendering,Environment,Input,Hosting}` 转换搬 | ~106 / 22783 | 需先定 RHI 策略（直接换 Godot 调用，或在 Godot 上再造一层壳） | 渲染与输入表现一致 |
| **B6** | `L4/Game/UI`（FDF）与现有 `HudFdfUi` 合并 | 86 / 17267 | B5 的交互层 | HUD 帧与客户端逐项一致 |

顺序依据的是第十节的依赖矩阵，不是目录编号：L0–L2 已结清，前沿在 L3；B2 是分水岭，它决定 Launcher 的模拟层是「客户端那套」还是「两套并存」，在它之前动 L4 会被反复推翻。

---

## 十二、Launcher 自有文件合理性

搬客户端代码时，Launcher 侧有一批同名或同职责的自有文件。判定它们留不留，用三条：

1. **纯逻辑、客户端有 C# 等价实现** → 用客户端的，删 Launcher 的；
2. **数据/资源加载**（Launcher 走 C# 生成的表、pck、gmap，客户端读 SLK/MPQ）→ 保留 Launcher 的加载方式，但**语义与字段必须跟客户端对齐**，不能各算各的；
3. **表现/渲染**（颜色、光效、材质、相机）→ 保留 Launcher 版，Godot 平台自行判断；
4. **客户端靠 JASS 实现、无 C# 等价** → 必须自研，保留。

### 12.1 `L3/Simulation/Configuration` 里 10 个自有文件

| 文件 | 行数 | 做什么 | 客户端对应 | 判定 |
|---|---|---|---|---|
| `MeleeRules.cs` | 256 | 八步 melee 开局模板（`Melee*` 函数的 C# 版） | **无**，客户端靠 `Blizzard.j` | 保留自研（属第 4 条） |
| `ItemCatalog.cs` | 190 | `ItemType` 模型 + 读 ItemData 表 | `ItemTypeData`（`War3CustomObjectData.cs`） | **模型重复** → 应改用客户端的 `ItemTypeData`，Launcher 只保留 C# 读表这一段 |
| `AbilityCatalog.cs` | 321 | `AbilityType` 模型 | `AbilityTypeData` | 同上 |
| `TechTree.cs` | 537 | `UnitFuncRow`：建造/训练前置要求 | `UnitTypeData` 读 `Requires`/`Requiresamount`/`areq`/`arqa`（`War3ObjectData.cs:701`） | **模型重复** → 同上 |
| `MiscRules.cs` | 73 | 生成表取值器（`Misc.DamageBonusNormal` 之类） | 无直接等价（这是 Launcher 的表加载入口） | 保留（第 2 条），但语义交给 `War3GameplayConfig` |
| `HeroRules.cs` | 87 | `MaxLevel`/`NeedHeroXpBase`/`ExpRange`/`GrantNormalXp` 等 | `War3GameplayConfig.MaxHeroLevel`/`HeroExpRange`/`NeedHeroXPFormula*` | **纯重复** → 删，统一读 config |
| `FogRules.cs` | 59 | 迷雾底色/暗角颜色 | 无（表现参数） | 保留（第 3 条），供 Godot 材质 |
| `GeneratedTexts.cs` | 75 | 文本表访问 | 客户端读 `War3Formats.Text` | 保留（第 2 条） |
| `LightningTable.cs` | 108 | 闪电束参数（`LightningData`） | `LightningData` 用法散在 `Runtime*Lightning` 与 `Src/Skills/ChainLightning` | 保留，需核对字段顺序与客户端一致 |
| `War3Text.cs` | 128 | `|cffRRGGBB` 色码、`|n` 换行、`<AEmb,DataA1>` 占位替换 | 客户端 `War3TextMacroResolver`（`L4/Game/Audio`，已搬） | 保留，需确认两者是否重复实现同一套宏替换 |

### 12.2 `Src/Units` 与 `Src/Abilities` 的 Launcher 独有文件

| 文件 | 判定 | 依据 |
|---|---|---|
| `Unit.Inventory.cs` | **保留** | 客户端物品/背包在 JASS 层（`World.OccupiedInventorySlotsQuery` 是委托），Launcher 不跑 JASS 必须自研 |
| `Unit.Compat.cs` / `Unit.Facade.cs` | **删** | 旧 `Unit` API 的兼容壳；客户端 `Unit` 已自带 `HasUsableInventory`、`IsHero` 等成员 |
| `Unit.Abilities/Channel/Illusion/Morph/Ownership/Slows.cs` | **需逐个看** | 客户端把这些拆进了 `Src/Components/*` 与 `L3/Configuration/Runtime*Ability`，不是同一种切法，删之前要确认功能有落点 |
| `Src/Abilities/UnitBuffs.cs` | **看功能落点** | 客户端 buff 模型在 `Src/Abilities/Buff.cs` + `ObjectDataBuff.cs` + `Src/Components/`，`UnitBuffs` 是 Launcher 自己的挂载容器 |
| `Src/Abilities/{AbilityContainer,AbilityTypeObjectData,AuraBonus,RuntimeAbilityFactory}.cs` | **重复** | 客户端同职责在 `Src/Components/AbilityContainer.*`（25 个）与 `L3/Configuration/RuntimeAbilityFactory.*`，路径不同但类型同名，是 CS0101/CS0102 的主要来源 |

### 12.3 `Src/Skills` 里 99 个 Launcher 独有文件

客户端 `Src/Skills` 只有 42 个，且是**机制层**（暴击、闪避、光环这些通用规则），具体技能实现放在 `L3/Configuration` 的 194 个 `Runtime*Ability` 与 `SkillRegistry`/`CampaignAndAuraSkills`。Launcher 的 99 个是**按技能逐个写**的旧路径。

判定不能一刀切，按三条分组：

| 组 | 特征 | 处置 |
|---|---|---|
| A 客户端有等价实现 | 例如 `FarSight`、`DarkConversion` 在客户端 `Src/Skills/SkillRegistry.cs`、`CampaignAndAuraSkills.cs` 里 | 删 Launcher 版 |
| B 客户端没有、Launcher 独有 | 例如 `SiphonMana`、`FingerOfDeath`，客户端全仓搜不到同名实现（可能走通用能力路径） | 先确认客户端是否用通用能力覆盖；没有就把 Launcher 版**改到新 `Ability` 契约**，不能删 |
| C 数据/表现相关 | 例如涉及贴图、音效路径、光效的技能文件 | 保留 Launcher 版，按 Godot 处理 |

> 这一组的判定要逐个做，方法和 12.1 一样：先查客户端有没有等价实现，再判它是纯逻辑还是表现。
