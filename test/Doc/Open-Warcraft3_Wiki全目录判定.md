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

| 子目录 | 文件 | 行数 | 后端 | 判定 |
|---|---|---|---|---|
| `Input/` | 3 | 48 | 0 | **已搬**：`Key`/`KeyModifiers`/`PointerEvent`/`IInputSource`，纯抽象 |
| `RHI/` | 7 | 203 | 7 | 不搬：`ITexture`/`IBuffer`/`IShaderProgram` 等自绘渲染接口，Launcher 保留自己的 `ITexture`/`Enums` 两件 |
| `ThirdParty/War3Net/` | 67 | 7065 | 67 | 不搬：vendored 的 MPQ/压缩库，需要 `System.IO.Hashing`、`ICSharpCode.SharpZipLib` |

---

## 四、`GameCore/L1_Adapter`：格式解析

| 子目录 | 文件 | 行数 | 后端 | JASS | 判定 |
|---|---|---|---|---|---|
| `Formats/` | 1 | 198 | 0 | 0 | 已搬（`War3Pathing`） |
| `Formats/Text` | 1 | 316 | 0 | 0 | 已搬（文本解码） |
| `Formats/Jass` | 39 | 10366 | 1 | 26 | **不搬**：JASS 词法/语法/AOT/虚拟机。只留 `JassNativeReal`（32 位浮点仿真，`War3GameplayConfig` 依赖） |
| `Formats/SaveGame` | 29 | 3330 | 0 | 0 | **不搬**：`NetGameStore` 网络同步、zlib 仿真、能力状态序列化 |
| `Formats/Trigger` | 6 | 703 | 1 | 0 | **不搬**：`war3map.wtg`/`wct` 触发树解析 |
| `RHI.OpenGL` | 14 | 1611 | 14 | 0 | 不搬：自绘 OpenGL 后端 |

---

## 五、`GameCore/L2_Core`：客户端 UI 与地图浏览

Launcher 早就移植了这一层的 FDF 部分（`GameCore/L2_Core/UI/*` 在 Launcher 里基本齐全），所以这一层主要看 `MapBrowser`。

| 子目录 | 文件 | 行数 | 后端 | 已搬 | 判定 |
|---|---|---|---|---|---|
| `UI/Fdf` | 7 | 1013 | 7 | 7 | 已搬：FDF 解析器/词法器/膨胀器 |
| `UI/Frame` | 7 | 448 | 7 | 7 | 已搬：帧模型 |
| `UI/Frames` | 9 | 1141 | 9 | 9 | 已搬：具体帧类型 |
| `UI/Events` | 4 | 262 | 4 | 4 | 已搬：UI 事件 |
| `UI/Render` | 1 | 57 | 1 | 1 | 已搬：纹理解析器接口 |
| `UI`（根） | 2 | 46 | 2 | 2 | 已搬 |
| `MapBrowser/` | 6 | 1073 | 5 | 2 | **部分搬**：`MapInfoParser`（纯 `System.IO`，Launcher 已有但落后 619 行，值得对齐）搬；`BlpDecoder`/`TgaDecoder`/`MapArchive`/`MainForm` 不搬（`System.Drawing` GDI+、WinForms、War3Net） |

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

## 十、执行顺序

自底向上，每批单独编译、单独报错误数，能收敛才进下一批。

| 批次 | 内容 | 规模 | 验收 |
|---|---|---|---|
| **B1** | `L4/Game/Audio` 15 个逻辑件，驱动换 Godot | 15 文件 / ~2500 行 | 音效/音乐/语音宏行为一致 |
| **B2** | `L4/Game/Camera` 3 件（去 War3Net、把 Silk 输入换 Godot） | 3 / 1541 | 相机跟随、边界、震动与原版一致 |
| **B3** | `L4/Game/Presentation` 6 件 | 6 / ~600 | 朝向、状态色、动画控制一致 |
| **B4** | `L3/Simulation/Configuration` 230 件（技能层整体替换） | 230 / 29307 | 1222 条 `CS0115` 收敛，技能行为对齐 |
| **B5** | `L4/Game/{Rendering,Environment,Input,Hosting}` 转换搬 | ~106 / 22783 | 渲染与输入表现一致 |
| **B6** | `L4/Game/UI`（FDF）与现有 `HudFdfUi` 合并 | 86 / 17267 | HUD 帧与客户端逐项一致 |

B4 是分水岭：它决定 Launcher 的模拟层是「客户端那套」还是「两套并存」。在 B4 之前，L4 的改动都会反复被推翻。
