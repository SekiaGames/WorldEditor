# Launcher 界面一致化任务

> 自包含文档。只做界面这一块，逻辑层与画面其余部分不在本轮。
> 数据层（表、文本、fdf、光照全转 C#）已完成，见附录 A。
> 写于 2026-11-13。

---

## 0. 范围

目标是启动流程、加载界面、游戏内界面三块与客户端一致，代码结构与客户端保持同一套形状。

**做**

- 启动流程：地图列表 → 种族弹窗 → 电脑难度弹窗 → 进图
- 加载界面
- 游戏内界面：资源栏、命令卡、单位面板、小地图、英雄栏、提示、聊天、ESC 菜单
- 1v1 对战房间配置与开局流程

**不做**

| 方向 | 状态 |
|---|---|
| 模拟层换成客户端源码、逐 tick 对账 | 推迟到界面完成之后 |
| 世界画面（地形、单位、特效、迷雾） | 维持现有实现，界面画在它上面 |
| 音频 | 维持现有实现 |
| 地图格式（直接读 w3x） | 本阶段继续读 gmap |

**三条不变量**

1. Launcher 一个工程、Extract 一个工程，都不拆子工程。
2. 配置只有 C#。表、文本、fdf、光照曲线已在 `Launcher/Generated/`。
3. 引擎相关代码集中在 `Rendering/`、`Platform/`。帧层文件不写 `using Godot`。

---

## 1. 现状

### 1.1 两侧的家底

| 项 | 客户端 | Launcher |
|---|---|---|
| 帧层底座 `L2_Core/UI` | Frame、Fdf、Frames、Events、UiRoot | **逐字节相同**（`FdfUiFactory`、`UiFrame`、`ButtonFrame`、`UiEventRouter`、`UiRoot` 已逐个比对） |
| 具体界面 `Game/UI` | 86 个文件，17,269 行 | 7 个文件，1,691 行，Godot Control 手绘 |
| 引擎侧渲染 | `UiRenderDevice` 1,050 行 + `MultiTexture` 117 行（OpenGL） | `GodotUiDrawDevice` 103 行，只够画一个界面屏 |
| 纹理解析 | `War3UiTextureResolver` 112 行（GL 纹理） | `GodotUiTextureResolver` 55 行 |
| 字体 | `War3FontFace` 186 行（System.Drawing） | 无，用 Godot 默认字体 |
| 皮肤 | `War3UiSkin` 110 行（`war3skins.txt` + FDF） | 无 |
| FDF 绑定 | `Generated/*Binding.g.cs` 8 个类，`--fdf-bindgen` 生成 | 无，运行时按名字查（本轮已补上 6 个，见 §7） |

底座是同一套，差的是 `Game/UI` 这一层和它的引擎配套。对齐方式是搬这一层。

### 1.2 客户端的界面分层

```
Game/UI/            帧层：Skin、纹理、各 Frame、CommandCardBuilder、MeleeHudController
  Generated/        fdf 生成的类型化绑定
Game/Rendering/     引擎层：UiRenderDevice、War3FontFace、War3UiViewport、LoadingScreenRenderer
Game/Hosting/       流程层：GameApplication、MenuScreenHost、GameHostContext
Game/Scenes/        场景胶水：War3GameScene/Ui.cs 把模拟状态推给 MeleeHudController
```

Launcher 按这个形状摆。

---

## 2. 目标结构

```
Launcher/
├── GameCore/L2_Core/UI/                 与客户端逐字节一致（保持）
├── GameCore/L4_Composition/Game/
│    ├── Configuration/War3UiSkin.cs          搬，110 行
│    ├── Rendering/
│    │     War3UiViewport.cs                   搬，63 行
│    │     GodotUiRenderDevice.cs              新，替代 UiRenderDevice
│    │     GodotWar3FontFace.cs                新，替代 War3FontFace
│    │     GodotUiTextureResolver.cs           扩写现有 55 行
│    │     GodotCursorRenderer.cs              新，替代 War3CursorController
│    │     LoadingScreenRenderer.cs            新，替代 War3LoadingScreenRenderer
│    ├── UI/                                  帧层，结构对齐客户端
│    │     MeleeHudController.cs               搬 + 换输入枚举
│    │     ResourceBarFrame.cs / TopBarFrame.cs / ConsoleSkinFrame.cs
│    │     CommandCardBuilder*.cs / NativeCommandCard.cs / Native*Command*.cs
│    │     UnitInfoPanelFrame.cs / HeroBarFrame.cs / PortraitFrame.cs / PortraitAnimator.cs
│    │     MinimapFrame.cs / MinimapUnitView.cs / IdlePeonFrame.cs / SelectionGroupFrame.cs
│    │     TooltipFrame.cs / ItemDetailFrame.cs / InventoryFrame.cs / QueueIconRowFrame.cs
│    │     EscMenuFrame.cs / QuestDialogFrame.cs / ChatFrame.cs / MultiboardFrame.cs ...
│    │     MapSelectionScreen.cs / RaceSelectionDialogFrame.cs / AiDifficultyDialogFrame.cs
│    │     MenuScreenHost.cs
│    │     Generated/*Binding.g.cs             fdf 绑定，6 个
│    │     Views/                              视图记录，见 §3.3
│    └── Hosting/
│          LauncherApplication.cs              对应 GameApplication，只保留对战这条线
│          MeleeSessionConfig.cs               对应 GameHostContext 的对战字段
└── Platform/Godot/App/LauncherApp.cs          参数解析与 pck 挂载，保留
```

### 2.1 分层与依赖

`Views → UI → Rendering → Godot`。界面层只读 `Views` 里的记录，不直接摸模拟类型。

这一条比客户端严：客户端的 `MeleeHudController.UpdateHeroes(Unit[])` 直接收模拟对象，Launcher 收 `HeroView[]`。好处是将来换模拟层时界面一行不用改，代价是多一层转换，转换集中在场景胶水 `Scenes/War3GameScene/Ui.cs`。

---

## 3. 对齐规则

### 3.1 三类处置

按客户端 86 个文件的依赖分三堆：

| 类别 | 文件数 | 行数 | 处置 |
|---|---:|---:|---|
| 干净（不碰引擎、Jass、模拟） | 51 | 6,300 | 原样搬 |
| 只碰模拟类型 | 24 | 5,900 | 签名里的 `Unit` / `War3ObjectData` 换成视图记录后搬 |
| 碰引擎 | 11 | 4,061 | 写 Godot 版，两个只换输入层 |

引擎耦合的 11 个：

| 文件 | 行数 | Launcher 做法 |
|---|---:|---|
| `UiRenderDevice.cs` + `MultiTexture` | 1,167 | 重写成 `GodotUiRenderDevice`，实现同一个 `IUiRenderDevice` |
| `MeleeHudController.cs` | 1,096 | 搬，输入签名从 `Silk.NET.Input.Key` 改 `Godot.Key` |
| `MapSelectionScreen.cs` | 573 | 搬，渲染设备与输入换 Godot |
| `QuestDialogFrame.cs` | 385 | 搬，去掉 `OpenGLRenderDevice` 依赖 |
| `GlueModelLayer.cs` | 217 | 重写成 SubViewport 版，给 3D 头像用 |
| `War3CursorController.cs` | 186 | 重写，用 `Input.SetCustomMouseCursor` |
| `MainMenuScreen.cs` / `PublicServiceScreen.cs` | 278 | 本阶段不做，入口直接进地图选择 |
| `War3UiTextureResolver.cs` | 112 | 重写，pck 里的 png 直接加载成 `Texture2D` |
| `UiAutoCastModel.cs` | 47 | 搬，去掉纹理依赖 |

### 3.2 需要新写的引擎件

| 件 | 要求 |
|---|---|
| `GodotUiRenderDevice` | 实现 `IUiRenderDevice` 全部方法：纹理四边形、ADD 混合、平铺、九宫格、冷却扇形、小地图阵营图标、带阴影的文本、裁剪。现有 103 行只做了四边形与文本，其余走接口默认实现，必须补齐 |
| `GodotWar3FontFace` | 从 pck 读 `Fonts/*.ttf`，按 skin 的 `MasterFont` 选中，用 `FontFile` 加载；行高比例照 `War3FontFace` 的 `nativeLineRatio` 算法 |
| `GodotUiTextureResolver` | skin 名 → 路径 → `res://extract/**.png`，带缓存，支持队伍色与幻象染色 |
| `GodotCursorRenderer` | 从 `UI\Cursor\*` 取图，按当前命令态切换 |
| `LoadingScreenRenderer` | 帧树 + 背景 MDX sprite + 进度条 + 槽位面板 |

### 3.3 视图记录

客户端的帧层收 `JassGameNatives.*View` 这类记录（`QuestView`、`TimerDialogView`、`LeaderboardRef`……）。Launcher 现在没有 JASS，写不出这些类型。

做法：`Game/UI/Views/` 里定义字段形状相同的记录，帧层引用 `UiViews.X`。字段名与客户端一致。将来接 JASS 时写一个适配器把 `JassGameNatives.*View` 映射过来，帧层不动。清单见附录 C。

### 3.4 补丁点

| # | 位置 | 改动 | 原因 |
|---|---|---|---|
| B1 | `MeleeHudController`、各 `Frame.HandleKey` | `Silk.NET.Input.Key` → `Godot.Key` | 引擎换 Godot |
| B2 | 帧层的 `Unit` / `War3ObjectData` 参数 | 改成视图记录 | 界面与模拟解耦 |
| B3 | `War3UiTextureResolver` | `ITexture` 换成 Godot 版 | 同上 |
| B4 | `MapSelectionScreen` | 渲染设备换 `GodotUiRenderDevice` | 同上 |

搬运用脚本核对：除 B1/B2 涉及的行，搬过来的文件正文应与客户端一致。做成 `tools/check-ui-parity.sh`，列出每个文件的差异行号，未登记的差异即失败。

---

## 4. 启动流程与 1v1 房间配置

### 4.1 流程

```
LauncherApplication
   │
   ├─ MenuScreenHost.Show(MapSelectionScreen)
   │      ├─ 地图列表（按文件夹分组，每页 8 行）
   │      ├─ 右侧预览图 + 名称/尺寸/地形/说明/玩家数
   │      └─ 「开始」→ RaceSelectionDialogFrame
   │                        ├─ 人族 / 兽族 / 不死族 / 暗夜精灵 / 返回
   │                        └─ 选中 → AiDifficultyDialogFrame
   │                                     ├─ 普通 / 疯狂 / 彻底疯狂 / 返回
   │                                     └─ 选中 → 开局
   │
   ├─ MeleeSessionConfig 组装
   ├─ LoadingScreenRenderer（进度 0 → 1）
   └─ War3GameScene
```

对应客户端 `GameApplication`：`ShowMapSelection` → `_localMeleeRacePreference` / `_meleeAiPreset` → `LoadMap` → `GameHostContext` → `War3GameScene`。

### 4.2 1v1 槽位规则

对战地图都按 1v1 打。规则对齐客户端 `MapLobbySettings.DefaultSlots` 的对战分支：

| 槽位 | 归属 | 说明 |
|---|---|---|
| 地图里第一个 `Controller == 1` 的槽 | 本地玩家 | 种族取弹窗选择 |
| 第一个不同队的槽 | 电脑 | 种族取地图 w3i，难度取弹窗选择 |
| 其余可玩槽 | 关闭 | 4 人图也只开两个 |

队伍用槽位号（`Team = PlayerId`），互为敌对。固定玩家设置的地图按地图配置走，不适用这条。

### 4.3 种族与难度

| 项 | 取值 | 客户端的落点 |
|---|---|---|
| 种族 | 人族、兽族、不死族、暗夜精灵 | `LobbyRace` 1 / 2 / 8 / 4。客户端的弹窗只有四个，没有随机 |
| 难度 | 普通、疯狂、彻底疯狂 | `MeleeAiPreset.Normal / Crazy / TotallyCrazy` |

预设换算（客户端 `MeleeAiPresetRules`）：

| 预设 | Difficulty | ConstructionSpeed |
|---|---:|---:|
| 普通 | 1 | 1.0 |
| 疯狂 | 2 | 1.0 |
| 彻底疯狂 | 2 | 2.0 |

Launcher 的 `AiDifficulty` 现在是 0..2 三档自建 AI。映射为：普通 → 1；疯狂 → 2；彻底疯狂 → 2，并把建造间隔乘 0.5。界面上只出现客户端的三个名字。

### 4.4 与客户端对战逻辑的差异

| 项 | 客户端 | Launcher |
|---|---|---|
| 地图容器 | 直接读 w3x | 读 gmap（转换产物），本阶段不改 |
| 开局单位 | Blizzard.j 的 `MeleeStartingUnits` | `MeleeRules.Plan` 自建开局表，246 行 |
| 玩家关系 | `InitializePlayerAlliances` | 场景内 `AreAllied` |
| 胜负判定 | 引擎 + 触发器 | `Melee.cs` 的 `TickVictoryCheck` |

前两项在界面上看不出来，本阶段保持现状。后两项行为一致即可。

**开局验收**：同一张 1v1 地图，Launcher 与客户端开局后的建筑与工人清单逐项相同（主基地、祭坛、兵营、农场、5 个工人、起始金币与木材）。不一致就改 `MeleeRules`。

---

## 5. 加载界面

客户端 `War3LoadingScreenRenderer` 863 行，其中约 500 行是 GL 顶点直绘，Launcher 不复用。

Launcher 的做法：

1. `--fdf-bindgen` 生成 `LoadingScreenBinding`（根帧 `Loading`，三个 fdf：`GlobalStrings`、`Glue\StandardTemplates`、`Glue\Loading`）。已生成，14 个字段，与客户端同名同数
2. 帧树按 FDF 布局摆放：标题、副标题、正文、进度条、槽位面板、小地图预览
3. 背景取 `LoadingScreen.mdx` 的 sprite 材质贴图，按 16:9 拉伸；缺模型时退回纯色
4. 进度条按 FDF 给的纵向位置横置居中，保持 8:1 比例
5. 槽位面板两行：本地玩家与电脑，头像、名字、种族图标、难度文字
6. 文本取自 w3i 的 `LoadingScreenTitle` / `Subtitle` / `Text`，经 WTS

验收：加载一张 1v1 地图，标题、副标题、正文、两个槽位、进度条位置与客户端截屏一致。

---

## 6. 游戏内界面

### 6.1 HUD 组成

按客户端 `MeleeHudController` 的成员逐项落地：

| 帧 | 客户端行数 | 数据来源 | 阶段 |
|---|---:|---|---|
| `ConsoleSkinFrame` | 87 | skin | U3 |
| `ResourceBarFrame`（金币/木材/人口 + 时间 + 编队） | 227 | `ResourceView` | U3 |
| `CommandCardBuilder` + 三个分部 + `Native*Command*` | 2,100 | `CommandCardView` | U3 |
| `UnitInfoPanelFrame` | 664 | `UnitInfoView` | U3 |
| `HeroBarFrame` / `PortraitFrame` / `PortraitAnimator` | 356 | `HeroView` | U3 |
| `MinimapFrame` / `MinimapUnitView` / `IdlePeonFrame` | 1,077 | `MinimapView` | U3 |
| `TooltipFrame` | 1,227 | `TooltipView` | U3 |
| `QueueIconRowFrame` / `InventoryFrame` / `ItemDetailFrame` | 463 | `QueueView` / `InventoryView` | U4 |
| `SelectionGroupFrame` / `ControlGroupFrame` | 401 | `SelectionView` | U4 |
| `ChatFrame` / `TextTagFrame` / `CommandErrorFrame` | 500 | 文本与飘字 | U4 |
| `EscMenuFrame` / `QuestDialogFrame` / `MultiboardFrame` / `LeaderboardFrame` | 1,060 | `MenuView` / `QuestView` | U4 |
| `AllianceDialogFrame` / `CheatDialogFrame` / `AboutDialogFrame` | 1,253 | 对话框 | U5 |
| `DialogFrame` / `TimerDialogFrame` / `TimedTextFrame` / `CinematicSceneFrame` | 921 | `DialogView` 等 | U5 |

### 6.2 输入与事件

客户端路径：`UiEventRouter` 命中帧 → 帧抛事件 → `MeleeHudController` → `IUiCommandListener` → 场景执行。

Launcher 照抄，只换指针来源：`_Input` 里的 `InputEventMouse*` 转成设计单位坐标后交给 `UiEventRouter`；Esc、F1..F12、字母热键同理。坐标换算用搬过来的 `War3UiViewport`。

### 6.3 现有 HUD 的处置

Launcher 现在的 `Game/UI/MeleeHudController.cs`（945 行）是 Godot Control 手绘，和客户端不是一套。替换顺序：

1. U3 先接新 HUD 的资源栏、命令卡、单位面板，新旧并存，用 `--hud=new|old` 切换
2. 小地图、英雄栏、提示跟上，旧的逐块删
3. U4 结束时删掉整个手绘 HUD

---

## 7. 阶段

| 阶段 | 内容 | 验收 |
|---|---|---|
| U0 底座 | 搬 `War3UiSkin`、`War3UiViewport`；写 `GodotUiRenderDevice` 全量方法、`GodotWar3FontFace`、`GodotUiTextureResolver`；`--fdf-bindgen` 产出 6 个绑定（已完成，见 §10） | `--ui-screen` 打开 `ConsoleUI` 与 `ResourceBar` 两屏，帧数与客户端一致；帧层文件无 `using Godot` |
| U1 启动流程 | 搬 `MenuScreenHost`、`MapSelectionScreen`、`RaceSelectionDialogFrame`、`AiDifficultyDialogFrame`；`LauncherApplication` 串起选择 → 弹窗 → 开局；`MeleeSessionConfig` | 选图 → 选种族 → 选难度 → 进图；日志打印本地种族与电脑预设；4 人图只起两个槽 |
| U2 加载界面 | `LoadingScreenBinding`、`LoadingScreenRenderer`、背景 sprite、进度条、槽位面板、小地图预览 | 与客户端截屏对齐；进度条在 0% / 50% / 100% 三处的矩形误差 ≤ 2px |
| U3 核心 HUD | 资源栏、命令卡、单位面板、英雄栏、小地图、提示、空闲工人条 | 截图对齐；鼠标命中与悬停正常；造兵、造建筑、施法三条命令链路可用 |
| U4 扩展 HUD | 队列图标、物品栏、选择组、编队、聊天、飘字、ESC 菜单、任务、多人面板、排行榜 | 各项截图对齐；物品拖拽、编队切换、F9 / F11 可用 |
| U5 收尾 | 联盟 / 作弊 / 关于、脚本对话框、光标、版本水印 | 截图与交互全量回归；手绘 HUD 删除完毕 |

每阶段结束：`Launcher.csproj` 0 错误，`tools/check-ui-parity.sh` 无未登记差异。

---

## 8. 验收

### 8.1 帧名清单

生成期从 fdf 抽取全部帧名与字段，输出 `Generated/fdf-frames.txt`；客户端 `--fdf-bindgen` 的字段名同样导出。两份排序后 diff，必须为空。缺帧在生成期暴露，运行期不再查字符串。

### 8.2 截图

固定条件：1920×1080、同一张 1v1 地图、游戏时间 0:30、固定相机。

| 画面 | 判据 |
|---|---|
| 地图选择页 | 列表行高、预览图位置、按钮位置目测一致 |
| 种族 / 难度弹窗 | 文字、按钮尺寸、遮罩一致 |
| 加载界面 | 标题与正文位置、进度条位置与长度一致 |
| HUD 全图 | 顶栏、命令卡、单位面板、小地图的矩形坐标误差 ≤ 2px |
| 悬停提示 | 出现位置与换行一致 |

### 8.3 交互回归

指向、点击、悬停、滚轮、拖拽、Esc、F1..F12、Ctrl/Shift 编队、物品右键。每项在两侧各做一次，行为一致。

### 8.4 编译与分层

- `Launcher.csproj` 0 错误
- `GameCore/L4_Composition/Game/UI/**` 不出现 `using Godot`
- 渲染路径不新增临时集合分配，客户端已有的池化写法保留

---

## 9. 风险

| 风险 | 影响 | 应对 |
|---|---|---|
| 渲染设备重写量大 | U0 拖长 | 先按客户端调用面补齐方法，内部先简单实现，U3 再优化 |
| 字体度量差异 | 文本重叠、换行不同 | 用 `nativeLineRatio` 算法；每个界面屏加一条「文本不越界」自检 |
| 视图转换样板多 | 文件数量上升 | 用 `record` + 主构造，转换函数集中在场景胶水，单向 |
| 3D 头像 | 单位面板画面不一致 | SubViewport，先静态模型，动画后补 |
| 新旧 HUD 并存 | 输入冲突 | `--hud=new\|old` 二选一，不做混合 |
| 视图记录与将来 JASS 不一致 | 返工 | 字段名逐字对齐，附录 C 做对照，接 JASS 时按表核对 |

---

## 10. 进度

### U0.1 已完成（2026-11-13）

| 项 | 结果 |
|---|---|
| `War3UiViewport` | 从客户端原样搬入，1024×768 设计单位与 16:9 裁切换算 |
| `War3UiSkin` | 改写为从 `Generated.Texts.UIwar3skins` 读，保留 Default → 种族 → CustomSkin → 地图皮肤的顺序；队伍色从 pck 的 `TeamColor00.png` 取首像素 |
| `FdfBindGen` | 搬入并改从 `Generated.FdfTables` 读 fdf，`--fdf-bindgen[=目录]` 触发 |
| 绑定产物 | 6 个 `.g.cs`，字段数与客户端逐一相同：ResourceBar 5、UpperButtonBar 9、LoadingScreen 14、MainMenu 45、EscMenuMainPanel 65 |

`ConsoleUiBinding` 目前只有 1 个字段。客户端的 9 个字段有 8 个来自 `SmashUI\SmashConsoleUI.fdf`，那是客户端自带的界面扩展，不在游戏资源里。Launcher 走原版 ConsoleUI，后续 U3 再决定是否补一个自己的 console 扩展。

### 下一步

`GodotUiRenderDevice` 补齐接口方法，`GodotWar3FontFace` 从 pck 加载字体，然后把 `ConsoleSkinFrame`、`TopBarFrame` 接到 `--ui-screen` 上做第一张对照截图。

---

## 附录 A：已完成的配置层

界面要的配置已经是 C#，本轮不再动：

| 数据 | 位置 | 规模 |
|---|---|---|
| slk 表 | `Launcher/Generated/Tables/` | 63 个文件，4.6 MB |
| 文本表 | `Launcher/Generated/Texts/` | 159 个文件，4.8 MB |
| fdf 原文 | `Launcher/Generated/Fdf/` | 86 个文件，884 KB |
| 光照曲线 | `Launcher/Generated/Lighting/` | 6 组，84 KB |

`extract.pck` 只留美术：模型、材质、贴图、音频、字体、shader。

界面要用的表：`UIwar3skins`（skin 路径）、`UIMiscData`（界面常量）、`UISkinMetaData`（skin 元数据）、`UISoundInfo*`（按钮音效）。

---

## 附录 B：文件对照

### B1 原样搬（51 个）

`AboutDialogFrame`、`AllianceDialogFrame`、`ChatFrame`、`CheatDialogFrame`、`CinematicSkipFrame`、`CommandCardBuilder.Abilities`、`CommandErrorFrame`、`ConsoleSkinFrame`、`ControlGroupFrame`、`EscMenuFrame`、`FdfButtonStates`、`GameUiCommand`、`IMenuScreen`、`MapCompatibilityNotice`、`MapPreviewImage`、`MenuScreenHost`、`NativeAbilityAvailability`、`NativeAbilityCounters`、`NativeAbilityDependencies.Generated`、`NativeAbilityLevelMetadata`、`NativeCommandButtonBinding`、`NativeCommandMenu`、`NativeCommandPosition`、`NativeEmptyCommandProviders.Generated`、`NativeGenericCommandProfiles.Generated`、`NativeGenericCommandProvider`、`NativeHudProbe`、`NativeItemSourceBinding`、`NativeManaAdmission`、`NativeSelectionCommandPolicy`、`NativeSelectionCommandProfiles.Generated`、`NativeSpellCleanup`、`NativeSpellOrderAdmission`、`NativeStandardAdmissionRecipe`、`NativeTeleportCleanup`、`NativeTeleportOutcome`、`NativeTypeInheritance`、`NativeTypeParents.Generated`、`NativeUnitAbilityLookup`、`PortraitViewport`、`UiCooldownModel`、`War3FdfUi`、`War3TouchControlGate`、`War3UnitPanelGate`、`RaceSelectionDialogFrame`、`AiDifficultyDialogFrame`、`TopBarFrame`、`TextTagFrame`、`TooltipFrame` 的排版部分、`SelectionGroupFrame` 的纯逻辑部分

### B2 换签名后搬（24 个）

`TooltipFrame`、`UnitInfoPanelFrame`、`HeroBarFrame`、`HeroPresentation`、`PortraitFrame`、`PortraitAnimator`、`QueueIconRowFrame`、`InventoryFrame`、`ItemDetailFrame`、`MinimapFrame`、`MinimapUnitView`、`IdlePeonFrame`、`SelectionGroupFrame`、`CommandCardBuilder` 与三个分部、`NativeCommandCard`、`MeleeHudController`

### B3 重写（11 个）

见 §3.1。

---

## 附录 C：视图记录清单

| 记录 | 客户端来源 | 界面用途 |
|---|---|---|
| `ResourceView` | `World` 玩家资源 | 顶栏金币、木材、人口 |
| `UnitInfoView` | `Unit` + `War3ObjectData` | 单位面板（名称、护甲、攻击、属性） |
| `HeroView` | `Unit` 英雄字段 | 英雄栏、头像、等级、经验 |
| `CommandCardView` | 命令表 + 单位能力 | 命令卡按钮 |
| `MinimapView` | 单位坐标 + 迷雾 + 装饰物 | 小地图 |
| `SelectionView` | 选中集合 | 选择组、编队 |
| `QueueView` | 建造/训练队列 | 队列图标 |
| `InventoryView` | `JassGameNatives.InventoryItemView` | 物品栏六格 |
| `ItemRefView` | `JassGameNatives.ItemRef` | 物品提示 |
| `TooltipView` | 单位/技能/物品数值 | 悬停提示 |
| `QuestView` / `QuestItemView` | `JassGameNatives.QuestView` | 任务面板 |
| `DialogView` / `DialogButtonView` | `JassGameNatives.DialogView` | 脚本对话框 |
| `TimerDialogView` | `JassGameNatives.TimerDialogView` | 倒计时窗 |
| `TimedTextView` | `JassGameNatives.TimedTextView` | 定时文字 |
| `LeaderboardRef` / `LeaderboardItemRef` | 同名 | 排行榜 |
| `MultiboardRef` | 同名 | 多人面板 |
| `CinematicSceneView` / `CineFilterView` | 同名 | 过场字幕与滤镜 |

字段名与客户端同名同序，由 `Scenes/War3GameScene/Ui.cs` 每帧填充。

---

## 附录 D：与客户端的差异登记

| # | 差异 | 原因 | 期限 |
|---|---|---|---|
| D1 | 地图列表列 gmap，客户端列 w3x | 容器格式未切换 | 格式切换时解决 |
| D2 | 主菜单与服务条款页不做，直接进地图选择 | 本轮范围 | 后续版本 |
| D3 | 开局单位由 `MeleeRules.Plan` 生成 | 无 JASS 运行时 | §4.4 的清单比对必须一致 |
| D4 | 界面读 `Views` 记录，客户端读模拟对象 | 界面与模拟解耦 | 长期保留 |
| D5 | 战役与自定义地图的房间配置不做 | 只做 1v1 对战 | 后续版本 |
