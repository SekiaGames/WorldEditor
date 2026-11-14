# 提高 Launcher 与客户端侧一致化的任务列表 V2

这份清单只记真正会造成流程、画面或行为差异的地方。判定标准沿用 AGENTS.md：

1. 客户端和 Launcher 做同一件事，但执行流程不一样。
2. 客户端和 Launcher 看到的效果不一样，尤其是动画、特效、加载和镜头。
3. 客户端已经有现成实现，Launcher 却另写了一套，导致后续很难保持同步。
4. Launcher 可以共用代码的情况下没有共用。
5. Launcher 的 C# 文件在有对应客户端侧 C# 文件时，相对路径、文件名、类名不一致。

**本次按模块分组、模块内从底层到表层重排**：V2 编号 1..34，已落地任务删除后编号留空、不复用；标题后的 `（V1 #xx）` 是上一份文档的原编号，便于追溯；本文之后的新任务从 **35** 起，做完一条删一条，序号不复用。

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
> 任务 16（粒子参数按动画片段驱动）已落地并合并（Extract 导出侧），任务 17（MDX 粒子吃大气雾）、任务 19（音乐/循环音/声部）、任务 20（城镇被攻击/英雄阵亡事件音）已落地并合并；音量设置仍随任务 21 的选项界面做，编号不复用。

### 18. 飞弹模型几乎不可见（V1 #63）

- **客户端**：弹道用 `CharacterRenderer` 画模型，箭、火球、投石在游戏里都看得很清楚。
- **Launcher**：`OnMissileFired` 实例化模型、设位置、加进 `_projectiles`（`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/Presentation.cs:33-79`）。Turtle Rock 实测弓箭手的 `earc` 弹道节点存在、位置在相机视野中心、`Visible=true`，模型放大 4 倍和 12 倍后截图里仍没有箭；场景结构 `Node3D → Model/Skeleton3D → geoset0`，材质 `war3_model_opaque`（`alpha_cutoff 0.75`、贴图 `ArrowMissile.png`）完整，问题在渲染侧而不是导出。
- **表现差异**：远程单位对射时看不到箭/火球/投石，只有伤害数字和命中音。
- **要做**：先确认是骨骼/Stand 动画把网格挪走，还是投射物节点/材质在运行时被改写；修好后用弓箭手对射复验箭、火球、投石三类飞弹。

> **复核评价**：表现差异有证据，修法还没有，这类条目容易被当成能直接下手的任务。建议标题标 `（待定位）`，定位结论写回本条再动手。定位可以先量 `_projectiles` 节点的运行时 AABB：放大 12 倍时 AABB 一起变大，说明网格在、只是被裁或全透明；AABB 塌成一点，说明骨骼/动画把网格移出了包围盒。

### 33. 烈焰风暴/云雾/季风/哨兵/火山的美术没写，陨石雨技能缺失（新增）

- **客户端**：这五个技能都把 `EfctID` 解析出的模型接进生成循环：烈焰风暴余烬取 Buff 的 `EffectArt`（`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Configuration/RuntimeFlameStrikeAbility.cs:51`、`:73-80`），云雾取 `Effectart` 常驻雾团（`RuntimeCloudOfFogAbility.cs:46`、`:69-76`），季风取雨幕与闪电落点（`RuntimeMonsoonAbility.cs:32`、`:85-87`），哨兵在树上生成猫头鹰（`RuntimeSentinelAbility.cs:29-31`、`:50-52`），火山生成喷发视觉与熔岩弹（`RuntimeVolcanoAbility.cs:127`、`:151`、`:226`）；另外客户端有独立的 `RuntimeMeteorRainAbility.cs`。
- **Launcher**：上述五个类（`Launcher/GameCore/L3_Simulation/Simulation/Configuration/RuntimeFlameStrikeAbility.cs`、`RuntimeCloudOfFogAbility.cs`、`RuntimeMonsoonAbility.cs`、`RuntimeSentinelAbility.cs`、`RuntimeVolcanoAbility.cs`）只结算伤害/状态，没有 `Art`、`Effect`、`Spawn` 相关的美术字段与生成循环；全仓也没有 `RuntimeMeteorRainAbility` 对应类。
- **表现差异**：烈焰风暴只有伤害数字没有柱状余烬，云雾没有雾团遮挡，季风没有雨幕和落点闪电，哨兵没有停在树上的猫头鹰，火山没有喷发和熔岩弹；陨石雨技能整个缺失。
- **要做**：逐个按客户端补美术字段、`RuntimePreloadArts` 与生成/销毁循环；火山和陨石雨还要补对应的 `Effect`/`Missile` 类。

> **复核评价**：五个技能那半成立，陨石雨这半越界。`ANme` 在 `test/war3-unpack/Units/AbilityData.slk` 里没有行（大小写都查过），`UnitAbilities.slk` 里没有任何单位携带，全仓只出现在战役脚本和 `Blizzard.j`；客户端 `RuntimeAbilityFactory.Constructors.cs:167` 的注册在 1.27 数据下是死代码。按本清单范围「只有脚本能触发的效果不进清单」，陨石雨应从本条删除，`RuntimeMeteorRainAbility` 不必补。另外编号 33 夹在 20 和 21 之间，破坏了「模块内从上往下」的读法，技能美术应排到模块三末尾。

## 模块五：输入与镜头

> 键鼠语义、Esc/菜单与快捷键。
> 任务 21（Esc/F10/顶栏/选项设置）已落地并合并到 Launcher，编号不复用。

## 模块六：界面

> 命令卡、提示、小地图、头像与 HUD 布局。

### 22. 命令卡的槽位、热键和可用性判定（V1 #38）

- **客户端**：`FindNativeSlot` 是「向 11 找空位、再往 0 回退、满卡就丢」（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/CommandCardBuilder.cs:426`）；热键按按钮真正显示的 `Hotkey` 分发（`:90` 的 `CommandAtHotkey`），训练/研究/建造/升级都能用键盘；可用性走 `NativeManaAdmission` / `NativeSpellOrderAdmission` / `NativeSelectionCommandPolicy`。
- **Launcher**：`TrySelectionHotkey` 完全不看命令卡，只认 `ability.Hotkey` 和硬编码的 M/S/H/A/P（`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/QuickCast.cs:411-417`、`:449-457`），遇到建筑直接返回 false；槽位算法是「从请求位往后找第一个空位，找不到就 break」；可用性判定写死在 `QuickCast.cs` 里。
- **表现差异**：同一份物编，两边第 3/8/11 格可能放不同技能；命令卡上的热键字母是装饰，建筑训练/研究不能用键盘；带法力修正符的单位会出现「亮着却放不出」。
- **要做**：先把热键改成读按钮 `Hotkey`，再改槽位算法和可用性判定；冷却闪光、自动施法高亮、禁用变暗在第 30 条。

> **执行结论（Launcher2，待合并）**：热键、槽位与可用性已按客户端对齐。
> - 热键分发：新增 `GameHudScreen.CommandAtHotkey`，按 `NativeCommandCard.HotkeySlot` 的列优先次序扫描已落格的 12 个按钮，跳过不可见与禁用项，匹配按钮真正显示的 `Hotkey`（物编热键或 QWER/ASDF/ZXCV 兜底）；`War3GameScene.TrySelectionHotkey` 改为直接执行命中按钮的 `Action`，移动/停止/保持/攻击/巡逻、英雄与单位技能、训练、建造、研究、升级都能用键盘，并保留 A/S 等键的镜头平移屏蔽。删掉原来硬编码 M/S/H/A/P 的 `TryStandardCommandHotkey`，小键盘 1~6 物品热键保留。
> - 槽位：训练与建筑升级改用单位物编 `Buttonpos`（`TryGetUnitConfiguredSlot`），研究改用升级物编 `Buttonpos`（`UpgradeType` 从 `*UpgradeFunc.txt` 读入，新增 `TryGetUpgradeConfiguredSlot`）；技能没有 `Buttonpos` 时不再顺序补格（客户端同样不把无效位置塞进空格）。落位算法本身已是 `FindNativeSlot`（向 11 找空、再向 0 回退、满卡丢弃），本次未改。
> - 热键数据：训练/升级取 `UnitStrings` 的 `Hotkey`，研究取 `UpgradeStrings` 的逐级 `Hotkey`，因此按钮字母不再是装饰。
> - 可用性：技能按钮在“已学/冷却/法力/开关例外”之外，再要求 `!ability.IsDisabled` 与 `Owner.State.CanCast`（沉默、眩晕等），与 `TryCast` 的实际门禁一致；英雄与非英雄技能卡同一口径。Launcher 没有客户端那套法力消耗修正符系统，所以“法力修正符导致亮着放不出”在 Launcher 不会出现，本次让可用性与真实施法门禁一致。
> - 验证：`dotnet build Launcher.csproj` 0 错误；`Turtle Rock --card-probe` 输出：英雄卡 `8:神圣之光 9:神圣护甲 10:虔诚光环`，热键 `T→AHhb`、`D→AHds`、`V→无`（被动已禁用）；兵营卡 `0/1/2` 训练 + `8/9/10` 研究，热键 `F/R/K → hfoo/hrif/hkni`；铁匠铺卡 `0/1/4/5`，热键 `S/G/P/A → Rhme/Rhra/Rhar/Rhla`，与物编 `Buttonpos` 一致。`--state-probe` 仍只剩基线已知失败 `utod 研究项`。
> - 合并注意：改动落在 `QuickCast.cs`、`Commands.cs`、`Ui.cs`、`GameHudScreen.cs`、`MeleeHudController.cs`、`CommandCardBuilder*.cs`、`TechTree.cs`；命令卡探针新增在 `Cheats.cs` 的 `RunCardProbe` 里，和其它 Launcher 副本易冲突。未改 Extract，无需重建 `extract.pck`。

### 23. 法术书没有页面，子技能进不了命令卡（V1 #64）

- **客户端**：法术书是命令卡的一个页面：`OpenSpellBook(selected, book)` / `BackFromSpellBook()`（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/CommandCardBuilder.cs:59-64`），`CollectAbilityCommands` 在 `spellBook != null` 时只收 `ContainerAbility` 与这本书同源的子技能（`CommandCardBuilder.Abilities.cs:25-26`），子技能序号当格号落在 0..10、第 12 格放「返回」；书的主人死亡、书被移除或子技能不可用时自动 `_menus.Back()`。
- **Launcher**：`Ability` 只有 `ContainerRawcode` / `ContainerAbility` 两个字段，全仓没有一处赋值（`Launcher/Src/Abilities/Ability.cs:714-715`），也没有 `RuntimeSpellBookAbility`；物编里的法术书物品（`sbok` / `Aspb`）用出去什么都不发生。
- **表现差异**：拿到法术书点使用没有任何页面打开，书里的子技能永远进不了命令卡，物品等于失效。
- **要做**：补 `RuntimeSpellBookAbility`（物编 MenuOrderId + 子技能 `Requires` 链），在 `CommandCardBuilder` 的菜单栈里加 `MenuKind.Book` 与 `OpenSpellBook` / `BackFromSpellBook`，子技能按容器过滤并把序号当格号，点开/关页条件与客户端一致。

### 24. 提示语和悬停说明石沉大海（V1 #39）

- **客户端**：`CommandErrorFrame` 黄字、在 console 上方、3 秒 + 每字符 0.1 秒；`SetHint` 等提示都有真实读者；信息面板悬停按帧名分派（金币/木材/人口/维修费、攻击力/护甲、三围、经验条、8 个 Buff 图标）；`TooltipFrame` 有 `ShowEx(ext/costs)`、四个成本图标槽、分段富文本、跟随锚点、屏幕边缘内缩、单位血蓝条（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/TooltipFrame.cs` 1227 行）。
- **Launcher**：`MeleeHudController.SetHint` 只做 `_hint = text`（`MeleeHudController.cs:150`），`_hint` 没有读者；`SetQueue(string)`、`SetClock(string)` 是空方法（`:72`、`:139`），`SetPlayerInfo`、`SetBuffs`、`SetUnitSummary` 只写字段没人读（`:74`、`:141`、`:147`）。全仓没有 `CommandErrorFrame`；`TooltipFrame.cs` 只有 112 行，固定 280 宽纯色底 + 按 34 字符硬折行。
- **表现差异**：资源不足点建造、修理由非农民点残血建筑、Ctrl+1 存编队、子组切换、放置失败等 70 多处调用，一句提示都看不到；悬停资源/数值/Buff 没有说明；长文本用固定宽度硬折行。
- **要做**：先补客户端的 `CommandErrorFrame`（不要造等价物，就用这个名字和这套行为），再补信息面板悬停，最后升级 `TooltipFrame`；Buff 图标本身缺失另见第 27 条。

> **执行结论（Launcher4，已实现，待合并）**：命令提示、资源/信息面板悬停、TooltipFrame 三项都已接上。
> - `CommandErrorFrame`：新增 `GameCore/L4_Composition/Game/UI/CommandErrorFrame.cs`，与客户端同名同行为——黄字 `(1,.8,0)`、几何 `0.21/0.23/0.38/0.05 × 1280`、字号 `0.015 × 1280`、底边压在 console 上方、寿命 `3 + 0.1×字数` 秒后再淡出 3 秒。`GameHudScreen` 把它挂到根帧并每帧 `Update`；`MeleeHudController.SetHint` 改为调 `ShowCommandError`，现有 83 处提示调用立刻有读者。
> - 悬停分派：`GameHudScreen.OnHoverChanged` 按帧名分派——`ResIcon0/1/2`、`ResVal0/1/2` → 金币/木材/人口，`ResVal3` → 维修费用；`UnitAttackIcon/Label`、`UnitArmorIcon/Label`、`UnitStr/Agi/Int Label`、`UnitStatusLabel`、`UnitXpBar` → 攻击/护甲（含 `DAMAGE_*` / `ARMOR_*` 种类行）、三围（`STRENGTH_HILIGHT` + `BONUS_*`）、状态、经验。文案优先读 FDF：用 `FdfUiFactory` + `FdfSource` 加载 `GlobalStrings.fdf` / `InfoPanelStrings.fdf`，缺键时回落到原版中文兜底。Buff 图标悬停留给第 27 条（控件还没建）。
> - `TooltipFrame`：按客户端结构重写为名称 / 成本行 / 描述 / Ex 四段；宽度用渲染端 `MeasureTextLineWidth` 实测、`UiText.WrapToWidth` 整词折行（去掉固定 280 宽与 34 字符硬切）；宽度 = `max(名称, min(0.2, 描述), 成本行)`，Ex 版 = `max(名称, 0.21)`；四个成本图标槽按 `ToolTipMana/Gold/Lumber/SupplyIcon` 取皮肤纹理并压紧排布；Ex 段白字；面板跟随悬停控件并钳在屏幕内缩 `0.01×1280`，无锚点时仍回右下 console 上方；背景改九宫 `ToolTipBackground/ToolTipEdge`，底层保留不透明 `#101824`。
> - 富文本：`GodotUiRenderDevice.DrawTextLine` 增加 `|cAARRGGBB…|r` 分段着色（`||` 字面量），度量仍按纯文本、绘制按分段颜色连续推进；FDF 的 `|Cffffcc00…|R` 现在真正着色。
> - 回归：新增 `--ui-hud-probe`，10 条断言全过（命令提示显示、`SetHint` 进提示、金币/木材/维修费、攻击/护甲含种类行、力量、经验）；`--combat-probe` 全部通过；`--state-probe` 仍只剩改动前就有的 `utod 研究项` 一项失败。
> - 视觉验证：窗口模式跑 `Godot --path Launcher4 -- --ui-hud --shot=/tmp/hud.png@3`，可看到 console 上方的黄字提示与 tooltip 的九宫边框、实测折行、黄色高亮；headless 的 dummy 渲染器截图会 NRE，不能用。
> - 未在本轮：成本图标槽已就绪，但命令卡数据仍是纯文本 `Cost`（没有结构化四槽，图标暂时不会出现）；Buff 图标槽与单位血蓝条随第 27 条/世界单位悬停另做；`SetQueue(string)`、`SetPlayerInfo`、`SetClock(string)` 目前没有可显示的构件，不在本条三步范围内。

### 25. 聊天和控制组各写了一套（V1 #41）

- **客户端**：`ChatFrame` 是 HUD 里的帧，行会淡出、自动换行、解析 `|c` 颜色、有 `AppendHistory`（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/ChatFrame.cs`）；屏幕左下有 1–9 编队按钮 `ControlGroupFrame`（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/ControlGroupFrame.cs`），支持双击居中。
- **Launcher**：`ChatFrame` 同名不同物，是独立 `CanvasLayer` 里的 `Label + LineEdit`，固定 `224,560 / 620×110`，不随 UI 缩放、不淡出、不换行（`Launcher/GameCore/L4_Composition/Game/UI/ChatFrame.cs:23-28`）；没有 `ControlGroupFrame`，只有键盘 0–9。
- **表现差异**：聊天窗口和 HUD 不同步缩放、超长消息被裁；看不到编队按钮，也不能双击居中。
- **要做**：聊天挪回 HUD 帧体系并补换行/淡出；补 1–9 编队按钮。

### 26. 小地图少了图标，滤镜和快捷键没接全（V1 #42，部分修复）

- **客户端**：`MinimapUnitView.Classify` 认金矿、缠绕金矿、闹鬼金矿、中立建筑、圈地、物品、野怪营地、出生点（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/MinimapUnitView.cs:58`）；野怪营地还有 `MinimapCampTracker`（按迷雾和等级画两种图标，`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Scenes/War3GameScene/Ui.cs:1338`）；`MinimapFilter` 和 `Alt+G/T/A` 都接到真实状态；腐地用 `MinimapBlightTexture` 缓存（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Rendering/MinimapBlightTexture.cs:10`）。
- **Launcher**：`Classify` 只认 4 个金矿 id（`Launcher/GameCore/L4_Composition/Game/UI/MinimapUnitView.cs:43-54`），`SimulationId` 恒为 0，`ColorIndex = unit.Player`；`_minimapIcons` 声明后从未 `Add`（`Scenes/War3GameScene/Ui.cs:199`、`Lifecycle.cs:520-521`），`DrawMinimapCamp` 直接 `return false`（`GodotUiRenderDevice.cs:157`）。`HandleAltShortcut` 没有调用者（`MinimapFrame.cs:190`），`MinimapFilter` 只赋值没人读（`Ui.cs:203`）；阵型按钮只改自己的开合状态，不改变移动阵型。腐地已经改用 `MinimapBlightTexture` 缓存（`Ui.cs:186-242`），这部分已修复。
- **表现差异**：小地图没有野怪营地图标（野怪只按单位点显示）、没有物品和出生点图标，中立建筑只按玩家色画方块而没有中立图标；Alt+G/T/A 没反应；因为没有营地图标，野怪过滤没有实际对象；阵型按钮不改变移动阵型。
- **要做**：补全 `Classify` 与营地图标；把 `HandleAltShortcut` 接到输入、把阵型开关接到移动阵型；配色/野怪按钮现有的颜色与显隐逻辑保留；腐地保持现状。

> **复核评价**：「只认 4 个金矿 id」与代码不符：`MinimapUnitView.cs:43-54` 现在是 3 个金矿（`ugol`/`egol`/`ngol`）加 3 个圈地（`ncop`/`ncp2`/`ncp3`），圈地图标已经在用。补 `Classify` 的时候别把圈地这几个分支一起重写成新表。

### 27. 单位信息面板没有 Buff 图标栏

- **客户端**：`UnitInfoPanelFrame` 有 8 个 `BuffIcon0..7`（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/UnitInfoPanelFrame.cs:41-43`、`:126-128`），`UpdateBuffIcons` 按选中单位的 Buff 填图标、剩余时间和光环/增益标记（`:534-573`），悬停显示 `Bufftip/BuffUbertip`。
- **Launcher**：`UnitInfoPanelFrame` 只有名字/经验/攻击/护甲/三围/状态标签（`Launcher/GameCore/L4_Composition/Game/UI/UnitInfoPanelFrame.cs:24-39`），没有 Buff 控件；`MeleeHudController.SetBuffs` 只把列表存进 `_buffs`（`MeleeHudController.cs:141-145`），`GameHudScreen` 没有任何读取方。
- **表现差异**：选中单位后看不到身上的增益/减益图标和剩余时间，也看不到悬停说明；玩家无法判断减速、嗜血、光环还剩多久。
- **要做**：在 `UnitInfoPanelFrame` 补 8 个 Buff 图标槽与剩余时间/光环标记，数据从 `SetBuffs` 接入；悬停说明与第 24 条一起做。

### 28. 头像只循环 Stand，没有 Portrait/Talk 动作

- **客户端**：`PortraitAnimator` 待机时每段 `Portrait` 序列播完重摇下一段（不循环同一段、不定格），收到单位应答时切 `Portrait+TALK`，播到语音时长结束再回待机（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/PortraitAnimator.cs:3-7`、`:127-138`）；换单位/换类型会重建头像并重摇种子（`:61-79`）。
- **Launcher**：`PortraitViewport.Show` 只加载选中单位模型并 `Play("Stand")`（`Launcher/GameCore/L4_Composition/Game/Rendering/PortraitViewport.cs:51-70`），没有 Portrait 序列选择、没有待机轮换、没有应答时说话动画；`GameHudScreen` 选中变化时只调 `Show(selected)`（`Launcher/GameCore/L4_Composition/Game/UI/GameHudScreen.cs:205-214`）。
- **表现差异**：头像永远循环同一个 Stand，站立动作整齐、缺少原版的手势/说话表现；点单位听到应答时头像嘴不动。
- **要做**：按客户端补 `PortraitAnimator`（Portrait/Portrait+TALK、待机重摇、按身份重播种），在 ack 时触发 Talk，时长取语音实际长度。

> **复核评价**：本条顺手欠着一笔规则 5 的账。`PortraitViewport.cs` 客户端在 `L4_Composition/Game/UI/`，Launcher 在 `L4_Composition/Game/Rendering/`。既然本来就要重写这个类，一起搬回 `Game/UI/`。全仓 273 个同名 C# 文件里，相对路径对不上的只有这一处。

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

### 34. 打开设置页时没有暂停模拟（新增）

- **客户端**：`MeleeHudController.AutoPauseRequested` 在 `_aboutDialog.Visible` 等对话框可见时为 true（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/MeleeHudController.cs:551`），`War3GameScene/Ui.cs:34` 把它作为 `MenuPauseRequested` 交给游戏循环暂停模拟。
- **Launcher**：`GameHudScreen` 只负责 `AboutDialog` 的显示/隐藏（`Launcher/GameCore/L4_Composition/Game/UI/GameHudScreen.cs:247-253`），没有把可见性接到 `Paused`；`Paused` 只由 Esc 菜单的暂停按钮切换（`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/Lifecycle.cs:70-74`），而模拟只在 `Paused` 时停（`Lifecycle.cs:794`）。
- **表现差异**：打开选项/设置页时单位仍在移动、AI 仍在出兵、计时继续；客户端会自动暂停模拟。
- **要做**：让设置页的显示/关闭驱动 `Paused`（或实现 `AutoPauseRequested`），关闭时恢复原暂停状态；Esc 菜单本身不算，按客户端口径只认对话框。

## 模块七：工程结构（横切）

> 命名、归属与目录对齐；放最后，避免和功能改动互相踩。

### 31. 坡面姿态在客户端是场景 partial 方法，Launcher 抽成了独立类（V1 #59）

- **客户端**：`ComputeUnitTerrainTilt` 挂在 `War3GameScene` 的 partial class 上（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Scenes/War3GameScene/Presentation.cs:489`），同目录诊断也直接调它。
- **Launcher**：写成 `Launcher/GameCore/L4_Composition/Game/Rendering/UnitTerrainTilt.cs` 的独立静态类，算法逐行一致但归属与目录都不对。
- **表现差异**：玩家看不到差别，但任何按客户端路径找坡面姿态的改动都会落空，后续场景层改动容易漏改这份独立实现。
- **要做**：搬回 `War3GameScene` 的 partial（可放 `Scenes/War3GameScene/Presentation.cs` 同目录新 partial），调用点经场景转调；如果拿场景引用不方便，至少放到 `Scenes/War3GameScene/` 下并在类名上体现归属。

### 32. `SceneFog` 该叫 `FogOfWarUniforms`，而且不该管大气雾（V1 #58）

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Rendering/FogOfWarUniforms.cs` 只管战争迷雾的 `Texture/Uvp/Enabled` 加 `Publish/Reset/Upload`；大气雾由各渲染器按 `FogSettings` 自己传 `uFogColor/uFogParams`。
- **Launcher**：`Launcher/GameCore/L4_Composition/Game/Environment/SceneFog.cs` 同时装大气雾 `fog_color/fog_params` 和战争迷雾 `fog_of_war_*`，`SceneLightRig.Apply` 一次写两样（`Launcher/GameCore/L4_Composition/Game/Environment/SceneLighting.cs:297`）；路径、类名都和客户端对不上。
- **表现差异**：玩家看到的雾暂时一致，但两类雾的生命周期被绑在一起（第 9 条的延迟就是它造成的），后续任一渲染器改雾参数都会牵动全局。
- **要做**：拆开。战争迷雾改叫 `FogOfWarUniforms` 并挪到 `Game/Rendering/`，只留雾纹理/UV/开关；大气雾留在 `War3FogConfig`，由地形、悬崖、水面、模型各自在材质创建处传参，`SceneLightRig.Apply` 只负责光照。

> **复核评价**：命名与归属那一半属规则 5，成立。「各渲染器自己在材质创建处传参」属于行为重构，落在规则 1/3 上，超出了规则 5 的题面，方向是对的，保留。做的时候和第 9 条一起收，顺序是先拆后修。

---

## 核对结果（V1 剩余 11 条）

| V1 # | 状态 | 关键证据 |
|---|---|---|
| 38 | 仍成立 | 无 `CommandAtHotkey`/`FindNativeSlot`；`QuickCast.cs:411-417` 只认物编热键 |
| 39 | 仍成立 | `SetHint` 只赋值（`MeleeHudController.cs:150`），无 `CommandErrorFrame`；Buff 图标另立第 27 条 |
| 41 | 仍成立 | 聊天独立 CanvasLayer（`ChatFrame.cs:23-28`），无 `ControlGroupFrame` |
| 42 | 部分修复 | 腐地已用缓存；`Classify` 只认金矿（`MinimapUnitView.cs:43`），Alt 快捷键无调用者，阵型滤镜未接 |
| 43 | 仍成立 | HUD 直接拉伸（`GameHudScreen.cs:515-535`），`UiFontScale` 无 `Set` |
| 44 | 仍成立 | `DrawCooldownCompletion/DrawAutoCast` 返回 false（`GodotUiRenderDevice.cs:155-159`），FDF 绑定只有加载屏在用 |
| 58 | 仍成立 | `SceneFog` 同时管两类雾（`SceneFog.cs:10-41`） |
| 59 | 仍成立 | 独立 `Rendering/UnitTerrainTilt.cs`，客户端是场景 partial（`Presentation.cs:489`） |
| 60 | 仍成立 | 动画轨道固定 `interp = 1`（`ModelBundle.cs:2403`） |
| 63 | 仍成立 | 弹道仍是实例化后直线/弧线飞行（`Presentation.cs:33-79`），根因未定位 |
| 64 | 仍成立 | `ContainerAbility` 无赋值（`Ability.cs:714-715`），无 `RuntimeSpellBookAbility`/`OpenSpellBook` |

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

模块顺序是从底层到表层，模块内从上往下做即可。两个例外：

1. **第 22、24 条**是「按了没反应/看不到提示」的功能性缺陷，可以提前插队。
现在最容易被玩家一眼看出的是 **第 18 条（飞弹）**、**第 33 条（五个技能特效）**；界面上 **第 22、24、26、27、28 条** 最直观。

## 收尾规则

- 一条做完就从本文删掉，序号不复用，后面的不重排；下一条新任务从 **35** 开始。
- 新发现的差异先判断是不是平台差异或已知取舍：Godot 与原生 GL 的差别、Launcher 固定 gmap 路径、不跑 JASS/触发器/战役/存档/录像/联机，这些不写进来。
- 写进来时必须带三个东西：客户端在哪、Launcher 在哪、玩家看得到什么。只有「不一样」没有证据的，等复核清楚再写。

> **复核评价**：「序号不复用」在多副本并行的工作流里已经落空。工作区有 `Launcher` 到 `Launcher5` 六份副本，`RunLauncher.command` 指向 `Launcher/`，而 `Launcher5` 的 git 历史里已经提交了「任务36」「任务37」，本清单最高才到 33、还写着新任务从 34 起。更麻烦的是 `Launcher5` 的「任务21/22/24」内容分别是模型丝带、闪电抖动、应答音效，与本清单第 21/22/24 条（Esc 语义、命令卡、提示语）不是一回事——同一个编号在两处指不同任务，编号就失去了追溯能力。
>
> 建议改成用标题做锚点（本清单的标题本来就够唯一），提交信息里带标题而不是只带序号；清单内部沿用序号只作阅读顺序。
>
> 另外「标题后的 `（V1 #xx）`」和「核对结果」这张 21 行的表属于历史变更记录，与 AGENTS.md 协作约定里的「不记录历史变更」相抵。清单只需要回答「还剩什么」，删完落地条目后这些追溯信息一并清掉更省事。