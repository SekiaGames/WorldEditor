# Launcher 界面一致化任务

> 只做界面：启动流程、加载界面、游戏内界面。
> 配置层（表、文本、fdf、光照）已是 C#，见附录 A。

---

## 0 范围

- 目标：三块界面与客户端一致，代码结构与客户端同形。
- 做：地图选择 → 种族 → 难度 → 加载 → 游戏内 HUD。
- 不做：模拟层同源、世界画面、音频、地图容器格式切换。
- 不变：单程序集；配置只有 C#；帧层不写 `using Godot`。

---

## 1 现状

| 项 | 客户端 | Launcher |
|---|---|---|
| `L2_Core/UI` 底座 | Frame、Fdf、Frames、Events、UiRoot | 逐字节相同 |
| `Game/UI` | 86 文件，17,269 行 | 7 文件，1,691 行，Godot Control 手绘 |
| 引擎渲染 | `UiRenderDevice` 1,167 行（OpenGL） | `GodotUiRenderDevice`，按接口实现 |
| 纹理 | `War3UiTextureResolver`（GL） | `GodotUiTextureResolver`，接皮肤表 |
| 字体 | `War3FontFace`（System.Drawing） | `GodotWar3FontFace`，pck → `FontFile` |
| 皮肤 | `War3UiSkin` 110 行 | 同形，读生成的 `UIwar3skins` |
| FDF 绑定 | `Generated/*Binding.g.cs` 8 个 | 6 个，`--fdf-bindgen` 生成 |

客户端分层：

```
Game/UI/         帧层：Skin、纹理、各 Frame、CommandCardBuilder、MeleeHudController
  Generated/     fdf 生成的类型化绑定
Game/Rendering/  引擎层：UiRenderDevice、War3FontFace、War3UiViewport、LoadingScreenRenderer
Game/Hosting/    流程层：GameApplication、MenuScreenHost、GameHostContext
Game/Scenes/     场景胶水：War3GameScene/Ui.cs 把状态推给 MeleeHudController
```

---

## 2 目标结构

```
Launcher/
├── GameCore/L2_Core/UI/                 与客户端一致（保持）
└── GameCore/L4_Composition/Game/
     ├── Configuration/War3UiSkin.cs
     ├── Rendering/
     │     War3UiViewport.cs
     │     GodotWar3FontFace.cs
     │     LoadingScreenRenderer.cs
     ├── UI/
     │     GodotUiRenderDevice.cs
     │     GodotUiTextureResolver.cs
     │     MeleeHudController.cs
     │     ConsoleSkinFrame / TopBarFrame / ResourceBarFrame / UpperButtonBar
     │     CommandCardBuilder* / NativeCommandCard / Native*Command*
     │     UnitInfoPanelFrame / HeroBarFrame / PortraitFrame
     │     MinimapFrame / IdlePeonFrame / SelectionGroupFrame
     │     TooltipFrame / ItemDetailFrame / InventoryFrame / QueueIconRowFrame
     │     EscMenuFrame / QuestDialogFrame / ChatFrame / MultiboardFrame
     │     MapSelectionScreen / RaceSelectionDialogFrame / AiDifficultyDialogFrame
     │     MenuScreenHost + Generated/*Binding.g.cs + Views/
     └── Hosting/
           LauncherApplication.cs
           MeleeSessionConfig.cs
```

依赖方向：`Views → UI → Rendering → Godot`。界面只读 `Views` 记录，不碰模拟类型。

---

## 3 处置规则

### 3.1 三类文件

| 类别 | 文件数 | 行数 | 处置 |
|---|---:|---:|---|
| 干净 | 51 | 6,300 | 原样搬 |
| 只碰模拟类型 | 24 | 5,900 | 换签名为视图记录 |
| 碰引擎 | 11 | 4,061 | 重写引擎件，两个只换输入 |

引擎耦合的 11 个：`UiRenderDevice`（1,167）、`MeleeHudController`（1,096）、`MapSelectionScreen`（573）、`QuestDialogFrame`（385）、`GlueModelLayer`（217）、`War3CursorController`（186）、`MainMenuScreen`/`PublicServiceScreen`（278，暂不做）、`War3UiTextureResolver`（112）、`UiAutoCastModel`（47）。

### 3.2 补丁点

| # | 位置 | 改动 |
|---|---|---|
| B1 | `MeleeHudController`、`Frame.HandleKey` | `Silk.NET.Input.Key` → `Godot.Key` |
| B2 | 帧层的 `Unit`/`War3ObjectData` 参数 | 换视图记录 |
| B3 | 纹理解析 | `ITexture` 用 Godot 实现 |
| B4 | `MapSelectionScreen` | 渲染设备换 Godot |

`tools/check-ui-parity.sh` 比对搬运文件与客户端的行差异，未登记的差异即失败。

### 3.3 视图记录

客户端帧层收 `JassGameNatives.*View`。Launcher 在 `Game/UI/Views/` 定义同名字段的记录，帧层引用 `UiViews.X`；接 JASS 后加适配器，帧层不动。清单见附录 C。

### 3.4 渲染能力对照

| 能力 | 状态 |
|---|---|
| 四边形、uv、裁剪栈、平铺、冷却扇形、文本与阴影、行高换算 | 已实现 |
| `ADD` 混合 | 按普通绘制，等命令卡高亮用到再补批次 |
| 逐字裁剪 | 只做整块可见性判断，小地图与提示框落地时补 |

---

## 4 启动流程

```
LauncherApplication
 ├─ MenuScreenHost.Show(MapSelectionScreen)
 │    ├─ 地图列表（按文件夹分组，每页 8 行）
 │    ├─ 预览图 + 名称/尺寸/地形/说明/玩家数
 │    └─「开始」→ RaceSelectionDialogFrame（人族/兽族/不死族/暗夜精灵/返回）
 │                  → AiDifficultyDialogFrame（普通/疯狂/彻底疯狂/返回）
 │                  → 开局
 ├─ MeleeSessionConfig 组装
 ├─ LoadingScreenRenderer（0 → 1）
 └─ War3GameScene
```

对应客户端 `GameApplication`：`ShowMapSelection` → `_localMeleeRacePreference` / `_meleeAiPreset` → `LoadMap` → `GameHostContext`。

### 4.1 1v1 槽位

对战地图都按 1v1：第一个玩家槽归本地，第一个不同队的槽归电脑，其余关闭。队伍用槽位号，互为敌对。固定玩家设置的地图按地图配置走。

### 4.2 种族与难度

| 项 | 取值 | 客户端落点 |
|---|---|---|
| 种族 | 人族、兽族、不死族、暗夜精灵 | `LobbyRace` 1 / 2 / 8 / 4；无随机 |
| 难度 | 普通、疯狂、彻底疯狂 | `MeleeAiPreset` |

| 预设 | Difficulty | ConstructionSpeed |
|---|---:|---:|
| 普通 | 1 | 1.0 |
| 疯狂 | 2 | 1.0 |
| 彻底疯狂 | 2 | 2.0 |

Launcher 的 `AiDifficulty` 0..2 映射：普通 1；疯狂 2；彻底疯狂 2 且建造间隔乘 0.5。

### 4.3 与客户端的差异

| 项 | 客户端 | Launcher |
|---|---|---|
| 地图容器 | w3x | gmap（本阶段不改） |
| 开局单位 | `MeleeStartingUnits` | `MeleeRules.Plan` |
| 玩家关系 | `InitializePlayerAlliances` | 场景 `AreAllied` |
| 胜负 | 引擎 + 触发器 | `TickVictoryCheck` |

开局验收：同一张 1v1 图，建筑与工人清单逐项相同（主基地、祭坛、兵营、农场、5 工人、起始金木）。

---

## 5 加载界面

逐条对齐客户端 `War3LoadingScreenRenderer`：

| 步 | 客户端做法 |
|---|---|
| 1 | 种族取 `racePreference`，没有就用地图 P0 的 race；据此 `War3UiSkin.Load(race)` |
| 2 | 标题 = `LoadingScreenTitle` ?? `Name`；副标题 = `LoadingScreenSubtitle`；正文 = `LoadingScreenText` ?? `Description`；都过 WTS |
| 3 | 帧树用 `LoadingScreenBinding`（`GlobalStrings` + `Glue\StandardTemplates` + `Glue\Loading`），根帧 `SetAllPoints` |
| 4 | `LoadingMeleePanel.Visible = false`，`LoadingCustomPanel.Visible = true` |
| 5 | 背景取 `LoadingMeleeBackground` 模型，按顶点包围盒归一化到全屏 |
| 6 | 进度条宽 `min(640, W-96)`，高为宽的 1/8，居中；纵向 = FDF `LoadingBar.Y - 高 - 52` |
| 7 | 条内：`Loading-BarBackground` 平铺、`Loading-BarFill` 按进度平铺、`Textures\Loading-BarGlass` 0.72、`Loading-BarBorder` 描边；两端按高度 0.5/0.2 内缩 |
| 8 | 标题/副标题/正文 26/20/16，从 1.0 按 0.05 缩到总高 ≤ 480；宽 460 折行；右缘 `W-32`，从 `LoadingBar.Y-80-总高` 起（夹在 80..365） |
| 9 | 阶段文字写进 `LoadingBarText`，18 号，超长逐级降到 10，居中于进度条 |
| 10 | `MinimapImage` 里放地图缩略图 `war3mapMap.blp` |

**背景几何**：`Load-Multiplayer-*.mdx` 是 4 个面，但不是四个等分象限。按顶点坐标归一化后是 2 列 × 2 行不等高：上带 `y 0.2..0.6` 占 2/3 屏高（两张 512×512），下带 `y 0..0.2` 占 1/3（两张 512×256）。四张贴图在设计单位下正好 1:1。Launcher 直绘这四张贴图，不用模型。

**进度条平铺**：客户端不是按纹理像素 1:1 重复，而是以条高为段宽切段，每段铺满整张贴图（u 0..1）。所以 `Loading-BarBackground`（32×32）在 560 宽的槽里重复 12 段多，不是 17 段。

**字号**：FDF 的 `FontHeight` 就是 em 尺寸，客户端直接 `fontSize * ScaleY` 做光栅字号，行高按 `fontSize * 1.25`。Launcher 不按字体的 ascent+descent 回缩，中英文用同一套尺寸。

**调试**：两侧都读 `OPENWC3_LOADING_DEBUG`，设为 `1` 时每次布局变化输出一段 `[loading-layout]`，设为 `2` 每帧输出。同一份日志逐行 diff 即可定位到具体元件：

```
[loading-layout] fb=1920x1080 active=1920 offset=0 scale=1.875x1.4063 design=1024x768
[loading-layout] bg mode=quads
[loading-layout] bg TL tex=512x512 path=UI\Glues\Loading\Multiplayer\Loading-TopLeft.png
[loading-layout] bg rects TL=0,0,512,512 TR=512,0,512,512 BL=0,512,512,256 BR=512,512,512,256
[loading-layout] bar rect=192,632.8,640,80 fdf=0,764.8,0,0
[loading-layout] channel rect=232,648.8,560,44 progress=0
[loading-layout] label size=18 px=25 rect=192,632.8,640,80 lines=1 w=40 text='装载中'
[loading-layout] text title size=26 px=37 rect=532,365,460,47 lines=1 h=47 w=111.47 text='Turtle Rock'
[loading-layout] text body size=16 px=22 rect=532,422,460,32 lines=1 h=32 w=352 text='…'
```

`px` 是实际光栅字号，`w` 是首行宽度（设计单位），`h` 是含 8 单位内边距的整块高度。三者任一不同都说明字体或折行有偏差。

**未接**：加载阶段的 `Report(progress, stage)`，目前进度按时间推进、阶段文字固定；地图缩略图读到了字节但 Godot 的 JPEG 解码器不支持这种 4 分量 BLP1，帧内暂时留空。

## 6 游戏内界面

### 6.1 HUD 组成

| 帧 | 客户端行数 | 数据 | 阶段 |
|---|---:|---|---|
| `ConsoleSkinFrame` | 87 | skin | U3 |
| `ResourceBarFrame` + `TopBarFrame` + `UpperButtonBar` | 454 | `ResourceView` | U3 |
| `CommandCardBuilder` + 三分部 + `Native*Command*` | 2,100 | `CommandCardView` | U3 |
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

### 6.2 输入

`UiEventRouter` 命中帧 → 帧事件 → `MeleeHudController` → `IUiCommandListener` → 场景。指针来自 `_Input` 的 `InputEventMouse*`，坐标经 `War3UiViewport` 转设计单位。

### 6.3 旧 HUD

`Game/UI/MeleeHudController.cs`（945 行，Godot Control 手绘）按块替换：先资源栏/命令卡/单位面板，再小地图/英雄栏/提示，U4 结束删除。切换开关 `--hud=new|old`。

---

## 7 阶段

| 阶段 | 内容 | 验收 |
|---|---|---|
| U0 底座 | 视口、皮肤、字体、渲染设备、纹理解析、`--fdf-bindgen` | 帧层无 `using Godot`；`--ui-screen` 出画面 |
| U1 启动流程 | `MenuScreenHost`、地图选择、两个弹窗、`LauncherApplication`、`MeleeSessionConfig` | 选图 → 种族 → 难度 → 进图；4 人图只起两槽 |
| U2 加载界面 | 绑定、背景 sprite、进度条、槽位、小地图预览 | 与客户端截屏对齐，进度条位置误差 ≤ 2px |
| U3 核心 HUD | 控制台、资源栏、命令卡、单位面板、英雄栏、小地图、提示 | 截图对齐；造兵/建造/施法可用 |
| U4 扩展 HUD | 队列、物品、选择组、编队、聊天、飘字、ESC、任务、面板 | 截图对齐；拖拽/编队/F9/F11 可用 |
| U5 收尾 | 联盟/作弊/关于、脚本对话框、光标、水印 | 回归；手绘 HUD 删除 |

每阶段结束：`Launcher.csproj` 0 错误，`check-ui-parity.sh` 无未登记差异。

---

## 8 验收

- 帧名：生成期导出 `Generated/fdf-frames.txt`，与客户端绑定字段名 diff 为空。
- 截图：1920×1080、同一张 1v1 图、时间 0:30。列表/弹窗/加载/HUD/提示逐项比对，HUD 矩形误差 ≤ 2px。
- 交互：指向、点击、悬停、滚轮、拖拽、Esc、F1..F12、Ctrl/Shift 编队、物品右键。
- 分层：`Game/UI/**` 不出现 `using Godot`；渲染路径不新增临时分配。

---

## 9 风险

| 风险 | 应对 |
|---|---|
| 渲染设备重写量大 | 先按客户端调用面补齐，内部先简单实现，U3 再优化 |
| 字体度量差异 | 用行高比换算；每个屏加「文本不越界」自检 |
| 视图转换样板多 | `record` + 主构造，转换集中在场景胶水 |
| 3D 头像 | SubViewport，先静态后动画 |
| 新旧 HUD 并存 | `--hud=new\|old` 二选一 |
| 视图记录与 JASS 漂移 | 字段名逐字对齐，附录 C 核对 |

---

## 附录 A 配置层

| 数据 | 位置 | 规模 |
|---|---|---|
| slk 表 | `Launcher/Generated/Tables/` | 63 文件，4.6 MB |
| 文本表 | `Launcher/Generated/Texts/` | 159 文件，4.8 MB |
| fdf 原文 | `Launcher/Generated/Fdf/` | 86 文件，884 KB |
| 光照曲线 | `Launcher/Generated/Lighting/` | 6 组，84 KB |

`extract.pck` 只留美术：模型、材质、贴图、音频、字体、shader。
界面要用的表：`UIwar3skins`、`UIMiscData`、`UISkinMetaData`、`UISoundInfo*`。

---

## 附录 B 文件清单

原样搬（51）：`AboutDialogFrame`、`AllianceDialogFrame`、`ChatFrame`、`CheatDialogFrame`、`CinematicSkipFrame`、`CommandCardBuilder.Abilities`、`CommandErrorFrame`、`ConsoleSkinFrame`、`ControlGroupFrame`、`EscMenuFrame`、`FdfButtonStates`、`GameUiCommand`、`IMenuScreen`、`MapCompatibilityNotice`、`MapPreviewImage`、`MenuScreenHost`、`NativeAbility*`、`NativeCommand*`、`NativeGeneric*`、`NativeHudProbe`、`NativeItemSourceBinding`、`NativeManaAdmission`、`NativeSelection*`、`NativeSpell*`、`NativeStandardAdmissionRecipe`、`NativeTeleport*`、`NativeType*`、`PortraitViewport`、`UiCooldownModel`、`War3FdfUi`、`War3TouchControlGate`、`War3UnitPanelGate`、`RaceSelectionDialogFrame`、`AiDifficultyDialogFrame`、`TopBarFrame`、`TextTagFrame`

换签名后搬（24）：`TooltipFrame`、`UnitInfoPanelFrame`、`HeroBarFrame`、`HeroPresentation`、`PortraitFrame`、`PortraitAnimator`、`QueueIconRowFrame`、`InventoryFrame`、`ItemDetailFrame`、`MinimapFrame`、`MinimapUnitView`、`IdlePeonFrame`、`SelectionGroupFrame`、`CommandCardBuilder` + 三分部、`NativeCommandCard`、`MeleeHudController`

重写（11）：见 §3.1。

---

## 附录 C 视图记录

| 记录 | 客户端来源 | 用途 |
|---|---|---|
| `ResourceView` | `World` 玩家资源 | 顶栏 |
| `UnitInfoView` | `Unit` + `War3ObjectData` | 单位面板 |
| `HeroView` | `Unit` 英雄字段 | 英雄栏、头像 |
| `CommandCardView` | 命令表 + 能力 | 命令卡 |
| `MinimapView` | 坐标 + 迷雾 + 装饰物 | 小地图 |
| `SelectionView` | 选中集合 | 选择组 |
| `QueueView` | 建造/训练队列 | 队列图标 |
| `InventoryView` / `ItemRefView` | `JassGameNatives.*` | 物品栏、物品提示 |
| `TooltipView` | 单位/技能/物品数值 | 悬停提示 |
| `QuestView` / `QuestItemView` | 同名 | 任务 |
| `DialogView` / `DialogButtonView` | 同名 | 脚本对话框 |
| `TimerDialogView` / `TimedTextView` | 同名 | 倒计时、定时文字 |
| `LeaderboardRef` / `MultiboardRef` | 同名 | 排行、多人面板 |
| `CinematicSceneView` / `CineFilterView` | 同名 | 过场 |

字段名与客户端同名同序，由 `Scenes/War3GameScene/Ui.cs` 每帧填。

---

## 附录 D 差异登记

| # | 差异 | 原因 | 期限 |
|---|---|---|---|
| D1 | 地图列表列 gmap | 容器未切换 | 格式切换时 |
| D2 | 主菜单与服务条款页不做 | 本轮范围 | 后续 |
| D3 | 开局单位由 `MeleeRules.Plan` 生成 | 无 JASS | §4.3 清单须一致 |
| D4 | 界面读 `Views` | 与模拟解耦 | 长期 |
| D5 | 战役与自定义地图房间配置不做 | 只做 1v1 | 后续 |
| D6 | `ADD` 混合按普通绘制 | Godot 单 CanvasItem 无逐命令混合 | U3 命令卡落地时 |
| D7 | 文本裁剪只做整块判断 | 同上 | U3 小地图时 |
