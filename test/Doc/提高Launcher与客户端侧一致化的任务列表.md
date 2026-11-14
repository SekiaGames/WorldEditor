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
> 任务 11（转身速率）、任务 12（咆哮/嗜血物编字段与 Buff 美术）、任务 13（地面物品拾取订单）、任务 14（Powerup 拾取即用）、任务 15（地面物品影子/光圈/离场）已落地并合并到 Launcher，编号不复用。

## 模块四：特效与声音

> MDX 粒子、丝带、弹道与音乐、事件音。
> 任务 16（粒子参数按动画片段驱动）已落地并合并（Extract 导出侧），任务 17（MDX 粒子吃大气雾）、任务 18（飞弹材质与丝带）、任务 19（音乐/循环音/声部）、任务 20（城镇被攻击/英雄阵亡事件音）已落地并合并；音量设置仍随任务 21 的选项界面做，编号不复用。

### 33. 烈焰风暴/云雾/季风/哨兵/火山的美术没写，陨石雨技能缺失（新增）

- **客户端**：这五个技能都把 `EfctID` 解析出的模型接进生成循环：烈焰风暴余烬取 Buff 的 `EffectArt`（`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Configuration/RuntimeFlameStrikeAbility.cs:51`、`:73-80`），云雾取 `Effectart` 常驻雾团（`RuntimeCloudOfFogAbility.cs:46`、`:69-76`），季风取雨幕与闪电落点（`RuntimeMonsoonAbility.cs:32`、`:85-87`），哨兵在树上生成猫头鹰（`RuntimeSentinelAbility.cs:29-31`、`:50-52`），火山生成喷发视觉与熔岩弹（`RuntimeVolcanoAbility.cs:127`、`:151`、`:226`）；另外客户端有独立的 `RuntimeMeteorRainAbility.cs`。
- **Launcher**：上述五个类（`Launcher/GameCore/L3_Simulation/Simulation/Configuration/RuntimeFlameStrikeAbility.cs`、`RuntimeCloudOfFogAbility.cs`、`RuntimeMonsoonAbility.cs`、`RuntimeSentinelAbility.cs`、`RuntimeVolcanoAbility.cs`）只结算伤害/状态，没有 `Art`、`Effect`、`Spawn` 相关的美术字段与生成循环；全仓也没有 `RuntimeMeteorRainAbility` 对应类。
- **表现差异**：烈焰风暴只有伤害数字没有柱状余烬，云雾没有雾团遮挡，季风没有雨幕和落点闪电，哨兵没有停在树上的猫头鹰，火山没有喷发和熔岩弹；陨石雨技能整个缺失。
- **要做**：逐个按客户端补美术字段、`RuntimePreloadArts` 与生成/销毁循环；火山和陨石雨还要补对应的 `Effect`/`Missile` 类。

> **复核评价**：五个技能那半成立，陨石雨这半越界。`ANme` 在 `test/war3-unpack/Units/AbilityData.slk` 里没有行（大小写都查过），`UnitAbilities.slk` 里没有任何单位携带，全仓只出现在战役脚本和 `Blizzard.j`；客户端 `RuntimeAbilityFactory.Constructors.cs:167` 的注册在 1.27 数据下是死代码。按本清单范围「只有脚本能触发的效果不进清单」，陨石雨应从本条删除，`RuntimeMeteorRainAbility` 不必补。另外编号 33 夹在 20 和 21 之间，破坏了「模块内从上往下」的读法，技能美术应排到模块三末尾。

> **执行结论（Launcher3，待合并）**：五个技能的美术循环已按客户端补齐；陨石雨按复核意见不做，没有新增 `RuntimeMeteorRainAbility`。
> - 美术来源：全部走物编合并层 `War3ObjectData` + `AbilityArt`，与客户端同口径——烈焰风暴 `XHfs.Effectart=FlameStrikeEmbers.mdl`、喷发取 `AHfs.Specialart`；云雾 `Xclf.Effectart=CloudOfFog.mdl`；季风 `XNmo.Effectart=MonsoonRain.mdl`、落雷 `ANmd.Targetart=MonsoonBoltTarget.mdl`；哨兵 `XEsn.Targetart=Owl.mdl`；火山 `Volcan` 破坏物模型 `Volcano.mdl`、`XNvc.Missileart=VolcanoMissile.mdl`（速度 400、弧 0.8）。
> - 烈焰风暴：`RuntimeFlameStrikeAbility` 施法时随机播一个喷发模型（`Dur` 时长），`FlameStrikeBurn` 每 0.33s 在半径内撒 10 个余烬、存活 1s（`Launcher3/Src/Skills/FlameStrikeBurn.cs`）。
> - 云雾：施法时在落点挂常驻 `CloudOfFog.mdl`（`PlayStandAfterBirth`、队伍色），云区提前结束或主人死亡时播 Death（`RuntimeCloudOfFogAbility.cs`、`Src/Skills/CloudOfFogSpell.cs`）。
> - 季风：引导开始挂 `MonsoonRain.mdl` 雨幕，每波对命中的单位挂 `MonsoonBoltTarget.mdl` 落雷；断引导/正常结束销毁雨幕（`RuntimeMonsoonAbility.cs`）。同时补了 `ANmo` 注册（此前只有 `ACmo`，两者同实现）。
> - 哨兵：保留 Launcher 现有的点地面施法与 `AddFogReveal`，在落点按 `DataC`（275）高度生成 `Owl.mdl` 猫头鹰，持续 `DataA`。Launcher 没有可破坏物目标管线，未改成客户端的树目标模式。
> - 火山：引导时在落点生成 `Volcano.mdl` 喷发体；每波保留现有 `DataE` 范围伤害，并新增 `RuntimeVolcanoMissileEffect` 按 `RuntimeVolcanoWavePattern` 从火山口抛出一批熔岩弹（复用 `MissileEffect` 渲染路径，不参与伤害结算，避免改动已对齐的逐波伤害与探针）。断引导时喷发体播 Death。
> - 预载：五个类都实现 `RuntimePreloadArts`，加载流程的 `PreloadAbility` 会把喷发/雾团/雨幕/落雷/猫头鹰/熔岩弹模型一起预载。
> - 验证：`dotnet build -t:Rebuild` 0 错误；`--state-probe` 新增 15 项美术断言全 OK（预载数量、喷发/余烬/雾团/雨幕/落雷/猫头鹰/喷发体/熔岩弹生成、断引导销毁），仅剩基线 `utod` 一项；`--combat-probe` 全部通过（火山第一波伤害仍为 `DataE`）。
> - 操作：`"$HOME/Applications/Godot_mono.app/Contents/MacOS/Godot" --path Launcher3 -- --state-probe` 可直接看断言；画面验证在 `(4)Turtle Rock.gmap` 用血法师/火焰领主放烈焰风暴/火山、龙鹰放云雾、女猎手放哨兵，季风可用风暴术士（`ACmo`）观察雨幕与落雷。

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
> 任务 31（坡面姿态归属）已落地并合并到 Launcher，编号不复用。

### 32. `SceneFog` 该叫 `FogOfWarUniforms`，而且不该管大气雾（V1 #58）

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Rendering/FogOfWarUniforms.cs` 只管战争迷雾的 `Texture/Uvp/Enabled` 加 `Publish/Reset/Upload`；大气雾由各渲染器按 `FogSettings` 自己传 `uFogColor/uFogParams`。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Environment/SceneFog.cs` 同时装大气雾 `fog_color/fog_params` 和战争迷雾 `fog_of_war_*`，`SceneLightRig.Apply` 一次写两样（`Launcher/GameCore/L4_Composition/Game/Environment/SceneLighting.cs:297`）；路径、类名都和客户端对不上。
- **表现差异**：玩家看到的雾暂时一致，但两类雾的生命周期被绑在一起（第 9 条的延迟就是它造成的），后续任一渲染器改雾参数都会牵动全局。
- **要做**：拆开。战争迷雾改叫 `FogOfWarUniforms` 并挪到 `Game/Rendering/`，只留雾纹理/UV/开关；大气雾留在 `War3FogConfig`，由地形、悬崖、水面、模型各自在材质创建处传参，`SceneLightRig.Apply` 只负责光照。

> **复核评价**：命名与归属那一半属规则 5，成立。「各渲染器自己在材质创建处传参」属于行为重构，落在规则 1/3 上，超出了规则 5 的题面，方向是对的，保留。做的时候和第 9 条一起收，顺序是先拆后修。

> **执行结论（Launcher2，待合并）**：两类雾已拆开，命名与路径对齐客户端。
> - 战争迷雾：删除 `Game/Environment/SceneFog.cs`，新增 `Game/Rendering/FogOfWarUniforms.cs`，只保留 `Texture/Uvp/Enabled` 与 `Publish/Reset/Register/Upload`，只写 `fog_of_war_texture/uvp/enable`；`FogOfWar` 改为调用 `Publish`/`Register`，水面在材质创建时注册为接收者，删掉 `BindWaterFogOfWar` 与每 0.2 秒的单独下发。
> - 大气雾：留在 `War3FogConfig`，新增 `Current/SetCurrent/Apply/ApplyTo`；`LoadTextures` 从 w3i 取值后 `SetCurrent` 并 `Reset` 战争迷雾状态，地形、悬崖、水面、装饰物、单位、变形、技能特效、弹道、丝带、集结点旗帜、岸浪、天空都在材质创建处写 `fog_color`/`fog_params`（`SceneLightRig.Apply` 不再碰雾）。
> - `SceneLightRig.Apply` 现在只写 `war3_scene_direction/color/ambient`，不再顺带写两种雾。
> - 验证：`dotnet build Launcher.csproj` 0 错误；`--card-probe` 新增 `[FOG]` 材质检查，Turtle Rock/BootyBay 上地形、悬崖、单位、水面四类材质均为 `fog_of_war=1` 且 `fog_params` 与当前配置一致；`--state-probe` 仍只剩基线已知失败 `utod 研究项`。两张自带地图的 w3i 都是 `useTerrainFog=0`，非零大气雾数值无法在这两张图上肉眼验收，代码路径与材质检查已覆盖。运行时截图因 Metal fence 超时全黑（并行 Godot 占用 GPU），不作为画面证据。
> - 合并注意：改动面覆盖 `SceneLighting.cs`、`War3FogConfig.cs`、`MapWorldBuilder*`/`TerrainGridFactory.cs`、`WaterRenderer.cs`、`DoodadRenderer.cs`、`UnitSceneComponent.cs`、`Units.cs`、`Presentation.cs`、`MdxRibbonRenderer.cs`、`ShorelineWaveRenderer.cs`、`SkyDomeRenderer.cs`、`RallyPointDisplay.cs`、`FogOfWar.cs`、`Lifecycle.cs`/`Environment.cs`，与其它 Launcher 副本冲突概率高；未改 Extract，无需重建 `extract.pck`。

---

## 核对结果（V1 剩余 4 条）

| V1 # | 状态 | 关键证据 |
|---|---|---|
| 43 | 仍成立 | HUD 直接拉伸（`GameHudScreen.cs:515-535`），`UiFontScale` 无 `Set` |
| 44 | 仍成立 | `DrawCooldownCompletion/DrawAutoCast` 返回 false（`GodotUiRenderDevice.cs:155-159`），FDF 绑定只有加载屏在用 |
| 58 | 仍成立 | `SceneFog` 同时管两类雾（`SceneFog.cs:10-41`） |
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