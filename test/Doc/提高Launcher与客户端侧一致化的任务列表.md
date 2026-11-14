# 提高 Launcher 与客户端侧一致化的任务列表 V2

这份清单只记真正会造成流程、画面或行为差异的地方。判定标准沿用 AGENTS.md：

1. 客户端和 Launcher 做同一件事，但执行流程不一样。
2. 客户端和 Launcher 看到的效果不一样，尤其是动画、特效、加载和镜头。
3. 客户端已经有现成实现，Launcher 却另写了一套，导致后续很难保持同步。
4. Launcher 可以共用代码的情况下没有共用。
5. Launcher 的 C# 文件在有对应客户端侧 C# 文件时，相对路径、文件名、类名不一致。

**本次按模块分组、模块内从底层到表层重排**：V2 编号 1..35，已落地任务删除后编号留空、不复用；标题后的 `（V1 #xx）` 是上一份文档的原编号，便于追溯；本文之后的新任务从 **36** 起，做完一条删一条，序号不复用。

## 范围

- 起点是地图选择界面。客户端首屏（公告页、主菜单、菜单音乐）、地图文件夹浏览、地图导入、战役列表不在本轮范围内，Launcher 固定从 `test/map_gmap` 读地图。
- Launcher 不跑 JASS 与触发器，只有脚本能触发的效果不进清单。
- 默认环境：macOS，`RunLauncher.command` 启动；地图用 `(4)Turtle Rock.gmap`、`(2)BootyBay.gmap`。

---

## 模块一：数据、资源与导出

> L1 格式、L3 物编/地图与 Extract 导出。这一层不改，上层表现无从对齐。
> 任务 1（V1 #51）、任务 2（V1 #53）、任务 3（V1 #49）、任务 4（V1 #50）已落地并合并到 Launcher，编号不复用。


## 模块二：地形与环境

> 地形、悬崖、水面、天气、战争迷雾、装饰物与光照。
> 任务 6（地形/悬崖动态光）、任务 7（装饰物死亡姿态）、任务 8（天气实例化）、任务 9（迷雾参数即时下发）、任务 10（雾中建筑定格记忆）已落地并合并到 Launcher，编号不复用。

## 模块三：单位、技能与物品

> 模拟层的单位行为、Buff 技能与地面物品。
> 任务 11（转身速率）、任务 12（咆哮/嗜血物编字段与 Buff 美术）、任务 13（地面物品拾取订单）、任务 14（Powerup 拾取即用）、任务 15（地面物品影子/光圈/离场）、任务 33（烈焰风暴/云雾/季风/哨兵/火山美术循环）已落地并合并到 Launcher，编号不复用。

## 模块四：特效与声音

> MDX 粒子、丝带、弹道与音乐、事件音。
> 任务 16（粒子参数按动画片段驱动）已落地并合并（Extract 导出侧），任务 17（MDX 粒子吃大气雾）、任务 18（飞弹材质与丝带）、任务 19（音乐/循环音/声部）、任务 20（城镇被攻击/英雄阵亡事件音）已落地并合并；音量设置仍随任务 21 的选项界面做，编号不复用。

## 模块五：输入与镜头

> 键鼠语义、Esc/菜单与快捷键。
> 任务 21（Esc/F10/顶栏/选项设置）已落地并合并到 Launcher，编号不复用。

## 模块六：界面

> 命令卡、提示、小地图、头像与 HUD 布局。
> 任务 22（命令卡槽位/热键/可用性）、任务 23（法术书页面）、任务 24（命令提示/悬停/Tooltip）、任务 25（聊天/编队按钮）、任务 26（小地图营地图标/Alt/阵型）、任务 27（Buff 图标栏）、任务 28（头像 Portrait/Talk）、任务 34（设置页暂停）已落地并合并到 Launcher，编号不复用。

### 29. 界面拉满整窗，超宽屏不居中（V1 #43）

- **客户端**：把 `PixelOffsetX` 和 `ActivePixelWidth` 传给渲染设备，`War3UiViewport.FitPixelWidth` 把 HUD 限制在居中 16:9（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Rendering/War3UiViewport.cs:24`、`Scenes/War3GameScene/Rendering.cs:46`）。`UiFontScale` 读用户设置并在倍率变化时 `InvalidateLayout()`。
- **Launcher**：`GameHudScreen._Draw` 用 `viewport.X / DesignWidth`、`viewport.Y / DesignHeight` 直接拉伸（`Launcher/GameCore/L4_Composition/Game/UI/GameHudScreen.cs:515-535`），没有 `FitPixelWidth`/`PixelOffsetX`；`UiFontScale` 默认 1.2，但全仓没有一处 `Set`（`Launcher/GameCore/L2_Core/UI/UiFontScale.cs:18`），依赖它的选项界面也不存在。
- **表现差异**：2560×1080 下 HUD 被横向拉宽三成，小地图变扁、命令卡格变宽、圆形按钮变椭圆；字号无法调。
- **要做**：HUD 改用 `War3UiViewport`（选图页、加载屏已经在用），删掉重复常量；字号接选项设置。

### 30. 界面渲染能力缺几项，FDF 生成的绑定没人用（V1 #44）

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/UiRenderDevice.cs:367` 的 `DrawCooldownSweep` 用 `cooldown.blp` 画扇形，`DrawCooldownCompletion` / `DrawAutoCast` / `DrawTexturedQuadAdd` 都有真实实现（`:382`、`:389`、`:496`，含 ADD 混合）。`War3FdfUi` 真的用 `ConsoleUiBinding`、`ResourceBarBinding`、`UpperButtonBarBinding`、`EscMenuMainPanelBinding` 切换 `_console.Visible` 和 `SetChromeVisible`（`War3FdfUi.cs:11-56`）。
- **Launcher**：`DrawCooldownSweep` 是纯色扇形（`GodotUiRenderDevice.cs:132`），`DrawCooldownCompletion` / `DrawAutoCast` 返回 false（`:155`、`:159`），`DrawTexturedQuadAdd` 退化成普通 alpha（`:98`）；`FdfBindGen` 生成了 5 个绑定（`Launcher/GameCore/L4_Composition/Game/Diagnostics/FdfBindGen.cs:17-44`），但只有 `LoadingScreenBinding` 有调用者（`Launcher/GameCore/L4_Composition/Game/Rendering/War3LoadingScreenRenderer.cs:95`），HUD 走手写 `TopBarFrame + ConsoleSkinFrame`。
- **表现差异**：冷却遮罩没有原版质感、冷却结束不闪、自动施法没有旋转光环、ADD 高亮/光晕偏暗；FDF 皮肤改了也不会反映到 HUD。
- **要做**：先补三个渲染能力和 ADD 混合，再决定 FDF 绑定是接上还是删掉。

### 35. 命令卡悬停说明没有结构化成本图标（新增）

- **客户端**：`TooltipFrame` 接收结构化 `costs`，按 `ToolTipMana/Gold/Lumber/SupplyIcon` 画四个成本图标槽并显示数字（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/TooltipFrame.cs`）。
- **Launcher**：`GameHudScreen.OnHoverChanged` 已把 `button.TooltipCosts` 传给 `TooltipFrame`（`GameHudScreen.cs:286`），但命令卡的 `CommandEntry.Cost` 仍是拼好的字符串（`MeleeHudController.cs:12`），构建命令卡时从未设置 `TooltipCosts`；目前只有 `InventoryFrame` 显式置空（`InventoryFrame.cs:77`）。
- **表现差异**：悬停建造/训练/研究按钮时只有一行文字成本，没有客户端的金/木/人口/法力图标。
- **要做**：给 `CommandEntry`/命令卡绑定补结构化成本字段，训练/建造/研究/升级/技能分别从物编填金、木、人口、法力，再写入 `TooltipCosts`。

## 模块七：工程结构（横切）

> 命名、归属与目录对齐；放最后，避免和功能改动互相踩。
> 任务 31（坡面姿态归属）、任务 32（战争迷雾/大气雾拆分）已落地并合并到 Launcher，编号不复用。

---

## 核对结果（V1 剩余 3 条）

| V1 # | 状态 | 关键证据 |
|---|---|---|
| 43 | 仍成立 | HUD 直接拉伸（`GameHudScreen.cs:515-535`），`UiFontScale` 无 `Set` |
| 44 | 仍成立 | `DrawCooldownCompletion/DrawAutoCast` 返回 false（`GodotUiRenderDevice.cs:155-159`），FDF 绑定只有加载屏在用 |
| 60 | 仍成立 | 动画轨道固定 `interp = 1`（`ModelBundle.cs:2403`） |

> **复核评价**：这一表的 21 行里，20 行能在正文找到对应任务，只有 #60 是孤行：标着「仍成立」，正文却没有它的条目，等于永远排不上。而且证据也失效了——`ModelBundle.cs:2403` 现在落在 `CameraNodes` 函数里，`interp` 的真正写入在 `:2644`、`:2656`、`:2666`，并且按 `Interpolation == 0` 分别写 0 和 1，「固定 `interp = 1`」这个描述本身就不准了。要么按规则 12 补成一条新任务，要么把这一行删掉。

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

> **复核评价**：这张表当「防重复立项的对照表」用是有价值的，但 AGENTS.md 规则 7 写的是「平台差异和『仅对战』的已知取舍不再列出」，「AI 的战术选择」这一行就是纯粹的仅对战取舍。要么在表头点明这是对照表而不是清单内容，要么把这一行去掉。

---

## 推进顺序

模块顺序是从底层到表层，模块内从上往下做即可。

现在最容易被玩家一眼看出的是 **第 18 条（飞弹）**、**第 33 条（五个技能特效）**；界面上 **第 26、27、28、34、35 条** 最直观。

## 收尾规则

- 一条做完就从本文删掉，序号不复用，后面的不重排；下一条新任务从 **36** 开始。
- 新发现的差异先判断是不是平台差异或已知取舍：Godot 与原生 GL 的差别、Launcher 固定 gmap 路径、不跑 JASS/触发器/战役/存档/录像/联机，这些不写进来。
- 写进来时必须带三个东西：客户端在哪、Launcher 在哪、玩家看得到什么。只有「不一样」没有证据的，等复核清楚再写。

> **复核评价**：「序号不复用」在多副本并行的工作流里已经落空。工作区有 `Launcher` 到 `Launcher5` 六份副本，`RunLauncher.command` 指向 `Launcher/`，而 `Launcher5` 的 git 历史里已经提交了「任务36」「任务37」，本清单最高才到 33、还写着新任务从 34 起。更麻烦的是 `Launcher5` 的「任务21/22/24」内容分别是模型丝带、闪电抖动、应答音效，与本清单第 21/22/24 条（Esc 语义、命令卡、提示语）不是一回事——同一个编号在两处指不同任务，编号就失去了追溯能力。
>
> 建议改成用标题做锚点（本清单的标题本来就够唯一），提交信息里带标题而不是只带序号；清单内部沿用序号只作阅读顺序。
>
> 另外「标题后的 `（V1 #xx）`」和「核对结果」这张 21 行的表属于历史变更记录，与 AGENTS.md 协作约定里的「不记录历史变更」相抵。清单只需要回答「还剩什么」，删完落地条目后这些追溯信息一并清掉更省事。