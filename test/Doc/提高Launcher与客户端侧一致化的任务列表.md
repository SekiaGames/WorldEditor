# 提高 Launcher 与客户端侧一致化的任务列表

这份清单只记真正会造成流程、画面或行为差异的地方。判定标准就是 AGENTS.md 里那五条，末尾再加上两条执行规则：

**判定标准**

1. 客户端和 Launcher 做同一件事，但执行流程不一样。
2. 客户端和 Launcher 看到的效果不一样，尤其是动画、特效、加载和镜头。
3. 客户端已经有现成实现，Launcher 却另写了一套，导致后续很难保持同步。
4. Launcher 可以共用代码的情况下没有共用。
5. Launcher 的 C# 文件在有对应客户端侧 C# 文件时，相对路径、文件名、类名不一致。

**执行规则**

6. 做完一条就删掉它，序号不复用，新任务往后续。
7. 一切以客户端行为为准，不写「我们这样更好」的要求。

读完一条就动手，不要先全面铺开。每条都标了客户端在哪、Launcher 在哪，照着路径能直接翻到。

**排序方式：从底层往表层。** 先数据读取，再地形与地表，再水与天气，再光照大气与迷雾，再单位实体，再特效与声音，最后镜头输入、加载流程、界面，结构问题放在最后作为横切项。

**本次是整体重排，编号全部重排过一次；从那以后恢复「删掉不复用、新任务往后续」的规则。当前排到 61，下一条新任务从 62 起。**

## 范围

- 起点是地图选择界面。客户端首屏（公告页、主菜单、菜单音乐）、地图文件夹浏览、地图导入、战役列表都不在本轮范围内，Launcher 保持固定地图路径，地图只从 `test/map_gmap` 读。
- Launcher 不跑 JASS 与触发器，所以「只有脚本能触发」的效果不进清单，这一条在文末「已排除的候选」里有对照。

---

## 五、战争迷雾

---

## 七、特效与声音

## 22. 闪电是 20Hz 随机折线，弹道不俯仰、不裁剪

- **闪电**：客户端每帧按 `id/index/time` 确定性抖动、UV 滚动、段数上限 64、颜色乘实例 RGBA、端点支持 `StartHeight/EndHeight`；Launcher 每 50ms 用 `GD.Randf()` 重建，最多 24 条，UV 不滚动，端点固定在「单位位置 + 0.55 身高」。
- **弹道**：客户端用模拟层的分段路径加 `RenderArcOffset`、`VisualPitch`、`BulletDeathTime` 和迷雾可见性；Launcher 自己算抛物线、只设 Yaw，技能弹道弧强制 0，弹道在黑雾里照样可见。
- **占位与引导**：客户端模型缺失只记日志；Launcher 丢一颗金色 `SphereMesh`。客户端引导技能用技能自己的 `AreaEffectArt`；Launcher 自己画一个半透明蓝圆盘。

要做：闪电改成确定性抖动 + UV 滚动；弹道补俯仰和迷雾裁剪；把黄球和蓝盘去掉。

## 23. 飘字是贴在 3D 世界里的字，而且是「Miss」

客户端把伤害/资源数字和 `CreateTextTag` 都交给 `TextTagFrame` 在屏幕空间投影画，字号按 framebuffer 高度换算，速度/寿命/淡出点来自 `UI\MiscData` 可覆盖的样式表，之后在屏幕上飘、不跟单位。

Launcher 用 `Label3D`（世界单位、Billboard），拉远镜头字就变小；落空文本是 **Miss**（灰、0.9s），客户端是 **未命中**（红、3s）；普通伤害寿命 0.95s 对 0.4s，暴击 1.6s 对 2s；木材赏金客户端默认不显示，Launcher 会飘一个金色 `+N`；暗影突袭的绿色 `N/N!` 完全没有；可见性只判迷雾不判隐身。

要做：改成屏幕空间文字并接样式表；文本、颜色、时长逐项对齐。

**已完成**（2026-11-14）：

1. **数据搬到模拟层**：新增 `Src/Core/FloatingText.cs`（客户端同名同路径）——`World.Floats` 在 `Advance()` 里按 `TickSeconds` 老化，文本、颜色、寿命、淡出点、`WorldHeight` 都在出字那一刻定住，之后不跟单位。`ShowCriticalStrike / ShowOrdinaryDamage / ShowMiss / ShowManaBurn / ShowShadowStrike / ShowResource / ShowBounty` 七个入口与客户端一一对应，`CanShow` 管迷雾与隐身/反隐（原来只判迷雾）。
2. **样式表**：新增 `Src/Configuration/War3FloatingTextStyle.cs`（客户端同名同路径），`War3GameplayConfig` 挂九套样式。颜色/速度/寿命/淡出点读 `UI\MiscData.txt` 的 `Misc.{前缀}Color|Velocity|Lifetime|FadeStart`，字号读 `UI\MiscUI.txt` 的 `Misc.{前缀}Height`（都是 0.024 归一化帧缓冲高度），缺键回落经典 TFT 默认值。实测值：暴击红 `255,0,0` 5s/淡出 2s、未命中红 3s/淡出 1s、金币 `255,220,0`、木材 `0,200,80`、法力燃烧 `82,82,255`、暗影突袭 `160,255,0`。
3. **屏幕空间渲染**：新增 `GameCore/L4_Composition/Game/UI/TextTagFrame.cs`（客户端同名同路径）——池化 `StringFrame`，一条飘字一个帧，`FontLineHeight` 按帧缓冲高度换算并四舍五入（1080p 下 18.49 设计单位），速度按 `velocity × age × 屏幕高/0.6` 换算，超出视口 48px 剔除。HUD 在 `_Draw` 里、所有界面之前绘制（`RenderWorldText`），所以数字永远不会盖在 HUD 上。
4. **逐项对齐**：落空文本改成「未命中」；普通伤害数字只对非魔法伤害出、受「伤害数字显示」开关与迷雾双门禁；暴击出在攻击者头上、用减伤前数值；木材赏金按样式表 `alpha 0 / height 0` 保持不显示；暗影突袭补上 `75!`（首段，用实际扣血）与每跳的绿色 `10`（`PeriodicDamageBuff` 的 `DamageType.ShadowStrike` 分流）。

验证（`(4)Turtle Rock.gmap`，`--float-probe --fog-off`，终端打 `[FLT]`）：

| 用例 | 结果 |
|---|---|
| 九种反馈的样式 | 颜色/寿命/透明度与 `UI\MiscData` + `UI\MiscUI` 逐项相同（见上） |
| 未命中 | 文本「未命中」、红、3s，不再有 `Miss` |
| 木材赏金 | `+40` 寿命 0、透明 0，也就是不显示 |
| 镜头拉远 2.4 倍 | 字号 `18.49 → 18.49`，投影高度 `241 → 159`（字号不再随镜头缩放） |
| 「伤害数字显示」关/开 | 同一条普通伤害数字进视图 0 条 / 1 条 |

未做：`CreateTextTag` 这类脚本接口（Launcher 没有 JASS 运行时）。

## 24. 单位应答变成 3D 音，命中音只认木头和肉

- **应答**：客户端 `War3AckSoundService` 有完整节流——连续选择计数、Pissed 索引、每单位「语音时长 +250ms」冷却、同名去重，并返回时长给头像做说话动作；应答是 `Play2D`。Launcher 只有「12 秒内点 4 次切 Pissed」，没有节流，语音会叠在一起；而且走 `AudioStreamPlayer3D` 放在单位坐标 +80 高，点远处单位时又小又偏声道。
- **命中音**：客户端按 `weaponSound + 目标 ArmorType` 选 Flesh/Metal/Wood/Ethereal/Stone；Launcher 只有 `target.IsBuilding ? "Wood" : "Flesh"`。
- **施法音**：客户端按 `AbilitySounds.slk` 的 `Aas/Asp/Ahe/Acs/Acst` 查；Launcher 没有实现，反而每次施法播一遍 What 选择语音。
- **界面音效**：客户端有 InterfaceClick、MapPing、SubGroupSelectionChange、InterfaceError、ItemGet/ItemDrop、JobDone、UpgradeComplete、NoGold/NoLumber/NoFood/NoMana、CantPlace、InventoryFull、TownAttack；Launcher 只剩 `InterfaceError`、`ResearchComplete{Race}`、`UpgradeComplete{Race}`。
- **冷却**：Launcher 的 `_clock += 0.016f` 只在调用 `Play` 时推进，0.08s 冷却实际是「每 5 次调用响一次」。
- **音高**：客户端 `RANDOMPITCH` + `PitchVariance` 有自然变化，Launcher 解析了 `RandomPitch` 却没用，种子还写死。

要做：应答先改回 2D 并补节流，再按材质补命中音、补界面音效。

**已完成**（2026-11-14）：

1. 播放器：`War3AudioSystem` 拆 2D/3D 两条通道（16 个 `AudioStreamPlayer3D` + 8 个 `AudioStreamPlayer`），`Play2D`/`Play3D` 都返回实际时长；`Now` 改用 `Time.GetTicksMsec()`，冷却不再是「只在调用时往前走」的假时钟。`War3SoundTables` 解出 `RandomPitch`/`PitchVariance`/`MinDistance`/`MaxDistance`/`Flags`，`War3UnitSound` 按音高换算时长、在 `FileNames` 里随机挑变体。
2. 应答：新增 `NativeUnitAckState`（连续选择计数、Pissed 索引、每单位「语音时长 + 250ms」冷却、`NODUPEUSERNAMES` 同名去重）与 `War3AckSoundService.PlaySelection/PlayOrderAck/PlayReady`，全部改 `Play2D`，并把时长回传给头像 `Talk`。
3. 命中音：`UnitFactory` 读物编 `armor`/`unitSound`，`CombatCodes.ParseWeaponSound` 解析 `weaponSound`；`PlayCombatHit` 按 `WeaponSound + ArmorSound` 拼键（如 `MetalMediumSliceMetal`），Flesh/Metal/Wood/Ethereal/Stone 全覆盖。
4. 施法音：`PlaySpellCast` 先查 `Aas/Asp/Ahe/Acs/Acst/Sounds/Ubz`，查不到落 `SpellSoundFallback`（rawcode → wav），不再把施法当成选择语音播 What。
5. 界面音效：新增 `War3UiSoundService`（冷却表 + 种族前缀 + Generic 回退），挂上 InterfaceClick、SubGroupSelectionChange、InterfaceError、ItemGet/ItemDrop、JobDone、UpgradeComplete、ResearchComplete、NoGold/NoLumber/NoFood/NoMana、CantPlace、InventoryFull；`World` 补 `ItemDropped` 事件（丢格、丢地、死亡掉落三条路）与 `ItemPickedUp` 接进场景层，买物/转交背包满时播 InventoryFull。

实测（BootyBay，`--skill-probe` / `--item-probe` / `--attack-probe` 的 `[AUDIO]` 日志）：

| 场景 | 日志 | 结论 |
|---|---|---|
| 连点同一英雄 5 次 | 只 1 条 `HeroPaladinWhat 2D 时长1.30s`，应答计数 1 | 节流生效，不再叠音 |
| 点敌方单位 | `InterfaceClick 2D` | 不可控走点击音，不念应答 |
| 步兵打步兵 | `MetalMediumSliceMetal` | 材质不再是固定 Flesh/Wood |
| 圣骑士/山丘施法 | `AHtb`、`AHtc`、`AHbz`、`AEbl` 各一条 | 施法音按技能走 |
| 拾取/丢弃物品 | `ItemGet 2D 时长0.20s`、`ItemDrop 2D 时长0.49s` | 两个界面音接通 |
| 建造完成 | `JobDoneSoundHuman` | 种族前缀拼接生效 |

**还差**：MapPing（等任务 42 的地图标记交互）、TownAttack/UnderAttack（城镇被攻击还没有事件源）、HeroDies/AllyHeroDies（要在死亡事件里区分英雄与同盟）。

## 25. 音乐无缝连播，也没有脚步和建筑循环音

- **音乐**：客户端曲间有 20 秒间隔，支持主题音乐叠加、淡入淡出、`SetMapMusic/PlayMusic/StopMusic`、三层音量；Launcher 播完立刻下一首，固定 `-10dB`，没有淡入淡出、没有脚本接口。
- **循环音**：客户端的 `War3LoopingSoundService` 按 `MovementSoundLabel` 播脚步并跟随位置，建筑建造时播 `BuildingSoundLabel`；Launcher 没有循环音服务，行军完全没有脚步声。
- **声部**：客户端 24 个系统声部、同文件最多 3 个、按 channel 配额和优先级抢占、每条声音有自己的 Min/MaxDistance；Launcher 16 个固定播放器，参数写死，表里读出来的 Min/MaxDistance 从来没用。
- **音量**：客户端有 `SoundVolume` / `MusicVolume` 并按通道分；Launcher 没有任何音量设置。
- **表覆盖**：客户端加载 12 类，Launcher 合并成 5 张，`AnimSounds` 和 `Dialog` 没有源。

要做：曲间间隔和淡入淡出，脚步循环音，然后音量设置（和 40 的选项界面一起做）。

---

## 九、加载流程

## 十、界面

## 37. 命令卡少了四个功能格，混选时只剩通用命令

客户端 `CommandCardBuilder` 是 5 个 partial 加一整套原生状态机：技能卡第 (3,1) 是「学习技能」（`BTNSkillz`、热键 `O`、角标显示剩余点数），点开后按 `Researchbuttonpos` 重排、第 12 格是「返回」；第 9 格是「建造」（`B`）；法术书有 `OpenSpellBook` / `BackFromSpellBook`；目标选择或放置模式下第 12 格换成 `BTNCancel`；混选时按成员 `CollectCommands` 聚合成一格并显示数量。

Launcher 的 `CommandCardBuilder` 只有 103 行，把 12 个槽刷成按钮就完了：没有学习页（学习直接点技能图标上的「+」，格位按 `HeroAbilList` 顺序硬排，和物编 `Buttonpos` 无关）；没有建造页（建筑直铺 12 格，多族建筑放不下，也没有「返回」）；没有法术书；没有取消格；混选只走 `_selected[0]`，加 5 个通用命令就 `break`。

要做：按客户端拆 partial，先把「学习 / 建造 / 取消 / 聚合」四件做了，法术书随后。

## 38. 命令卡的槽位、热键和可用性判定

- **槽位**：客户端 `FindNativeSlot` 是「向 11 找空位、再往 0 回退、满卡就丢」；Launcher 是「从请求位往后找第一个空位，找不到就 break」。同一份物编配置，两边第 3/8/11 格放的可能是不同技能。
- **热键**：客户端按按钮上真正显示的 `Hotkey` 分发（`CommandAtHotkey`），训练、研究、建造、升级都能用键盘；Launcher 的 `TrySelectionHotkey` 完全不看命令卡，只认 `ability.Hotkey` 和硬编码的 M/S/H/A/P，遇到建筑直接 `return false`。命令卡上的字母是纯装饰。
- **可用性**：客户端有 `NativeManaAdmission`、`NativeSpellOrderAdmission`、`NativeSelectionCommandPolicy`；Launcher 的判断写死在 `QuickCast.cs` 里，带法力修正符的单位会出现「亮着却放不出」。

要做：先把热键改成读按钮 `Hotkey`，再改槽位算法和可用性判定。冷却闪光、自动施法高亮、禁用变暗属于渲染设备能力，在 44 条。

## 39. 所有提示语都石沉大海

`MeleeHudController.SetHint` 只做 `_hint = text`，而 `_hint` 全工程除了声明和这一行赋值没有任何读者；`SetQueue(string)`、`SetClock(string)` 是空方法体，`SetPlayerInfo`、`SetBuffs`、`SetUnitSummary` 只写字段没人读。

结果是：资源不足点建造、修理由非农民点残血建筑、Ctrl+1 存编队、子组切换、放置失败——73 处调用，一句提示都看不到。客户端对应位置是 `CommandErrorFrame`（黄字、在 console 上方、3 秒 + 每字符 0.1 秒）。

信息面板的悬停说明也缺：客户端按帧名分派（金币/木材/人口/维修费、攻击力/护甲、三围、经验条、8 个 Buff 图标），Launcher 的悬停处理只认 `ButtonFrame`，而资源图标、数值标签、Buff 图标都不是 `ButtonFrame`。`TooltipFrame` 本身也退化了：客户端有 `ShowEx(title, body, ext, costs)`、四个成本图标槽、分段富文本、跟随锚点、屏幕边缘内缩、单位血蓝条，Launcher 是固定 280 宽纯色底 + 按 34 字符硬折行。

要做：先把 `CommandErrorFrame` 补上（不要造等价物，就用客户端这个名字和这套行为），再补信息面板悬停，最后升级 `TooltipFrame`。

## 40. Esc 语义相反，顶部按钮点了没反应，也没有选项界面

- **Esc**：客户端 Esc 是分层取消（取消待定命令 → 退出学习页），菜单开关是 F10；Launcher 里 Esc 第一件事就是 `_escMenu.Open()`，而且没有 F10。正在放置建筑时按 Esc，客户端取消放置，Launcher 弹菜单、关掉菜单后幽灵建筑还在。Backspace（循环主城）、PageUp/Down（缩放）、Ctrl+C/Alt+C（居中）、Alt+F（阵型）、Alt+G/T/A（小地图）也都没有。
- **Esc 菜单本体**：客户端是 FDF 的 `EscMenuMainPanel` 强类型绑定，隐藏存档/读档/帮助/重开，保留选项/结束游戏/返回游戏；Launcher 用 `ColorRect + Label + Button` 手搓，只有「继续/暂停/返回列表」，坐标还写死在 1600×900。
- **顶部按钮**：`TopBarFrame` 暴露了 `MenuButton` / `QuestButton` / `AlliesButton`，全仓没有一处 `.Clicked +=`，所以顶部按钮可点但完全没反应。任务和盟友面板不在本轮范围（见文末），这两个按钮按范围取舍隐藏；菜单按钮接 Esc 菜单。
- **选项**：客户端的 `AboutDialogFrame` 有镜头距离、平移灵敏度、音乐/音效音量、帧率上限、渲染倍率滑块，关掉时 `UserSettings.Save`；Launcher 没有任何设置界面。

要做：Esc 改回取消优先并补 F10；菜单按钮接上；选项界面和 25 的音量一起做。

## 41. 聊天和控制组各写了一套

- **聊天**：客户端 `ChatFrame` 是 HUD 里的帧，行会淡出、自动换行、解析 `|c` 颜色、有 `AppendHistory`；Launcher 的 `ChatFrame` 同名不同物，是独立 `CanvasLayer` 里的 `Label` + `LineEdit`，固定 `224,560 / 620×110`，不随 UI 缩放、不淡出、不换行，`Send` 只 `Add("[玩家] ...")` 加 `GD.Print`。多人频道（全体/盟友）不做，这里只对齐显示与排版。
- **控制组**：客户端屏幕左下有一排 1–9 编队按钮，支持双击居中；Launcher 只有键盘 0–9。

要做：聊天挪回 HUD 帧体系并补换行/淡出；编队按钮补上。

## 42. 小地图少了图标，滤镜点了没效果

- **图标**：客户端 `MinimapUnitView.Classify` 认金矿、缠绕金矿、闹鬼金矿、中立建筑、圈地、物品、野怪营地、出生点；野怪营地还有 `MinimapCampTracker`（按迷雾和等级画两张图标，位置走 MDX 模型）。Launcher 的 `MinimapUnitView` 只认几个硬编码 id，`SimulationId` 恒为 0，`ColorIndex` 直接等于 `unit.Player`；营地图标列表 `_minimapIcons` 从来没 `Add` 过，`DrawMinimapCamp` 直接 `return false`。小地图上没有野怪营地、没有物品、没有出生点，中立建筑也不显示中立色。
- **滤镜**：`MinimapFilter` 只被赋值没人读，`SetFilterState` / `HandleAltShortcut` 都没有调用者。点「野怪/配色/阵型」按钮图标会变，游戏里没有任何效果；Alt+G/T/A 没反应；阵型按钮不改变移动阵型。
- **腐地**：客户端有 `MinimapBlightTexture` 带缓存；Launcher 在 `Ui.cs` 里内联重算，腐地一变就整图重传并新建贴图。

要做：`Classify` 补全 + 营地图标，滤镜接上，腐地改用缓存那块。

## 43. 界面拉满整窗，超宽屏不居中

客户端把 `PixelOffsetX` 和 `ActivePixelWidth` 传给渲染设备，`War3UiViewport.FitPixelWidth` 把 HUD 限制在居中 16:9；Launcher 的 `GameHudScreen._Draw` 用 `viewport / 1024` 直接铺满，`_offsetX` 传 0。2560×1080 下 HUD 被横向拉宽三成，小地图变扁、命令卡格变宽、圆形按钮变椭圆。`GameHudScreen` 还自己又定义了一份 `DesignWidth/DesignHeight`。

`UiFontScale` 也没接：共用 `UiFontScale` 默认 1.2，Launcher 全仓没有一处 `Set`；客户端读用户设置并在倍率变化时 `InvalidateLayout()`（依赖 40 的选项界面）。

要做：`War3UiViewport` 用起来（选图页、加载屏已经在用），删掉重复常量，字号接设置。

## 44. 界面渲染能力缺几项，FDF 生成的绑定没人用

`GodotUiRenderDevice` 里：`DrawCooldownSweep` 是纯色扇形（客户端用 `cooldown.blp` 画），`DrawCooldownCompletion` / `DrawAutoCast` / `DrawTexturedQuadAdd` 分别返回 false 或退化成普通 alpha（所有 ADD 混合的贴图偏暗）。所以冷却遮罩没有原版质感、冷却结束不闪、自动施法没有旋转光环、高亮和光晕偏暗。

另一件事：Launcher 生成了 `ConsoleUiBinding`、`ResourceBarBinding`、`UpperButtonBarBinding`、`EscMenuMainPanelBinding`、`MainMenuBinding`，但除了 `LoadingScreenBinding` 之外没有任何调用者，HUD 走的是手写 `TopBarFrame` + `ConsoleSkinFrame`。客户端 `War3FdfUi` 是真的用这些绑定切换 `_console.Visible` 和 `SetChromeVisible`。

要做：先补三个渲染能力和 ADD 混合（都是小改动），再决定 FDF 绑定是接上还是删掉。

---

## 核对结果

按 AGENTS.md 的 1–5 条逐条对照，依据列里的数字就是条款号。

| # | 依据 | 结论 |
|---|---|---|
| 2 | 2 | 保留：尺寸算错，烘焙阴影等于全废 |
| 3 | 2 | 保留：地表花纹、斜坡、破面 |
| 4 | 2、3 | 保留：客户端并进阴影图，Launcher 另做黑片 |
| 8 | 2 | 保留：偶发大浪 |
| 10 | 2、3 | 保留：客户端有 SceneLighting/DynamicLight，Launcher 没有 |
| 12 | 2、3 | 保留：日月、选天空依据 |
| 15 | 2 | 三块已完成，等合并后删 |
| 16 | 2 | 已完成，等合并后删 |
| 17 | 2 | 已完成，等合并后删 |
| 18 | 2 | 已完成，等合并后删 |
| 19 | 2 | 三块已完成，等合并后删 |
| 20 | 2、3 | 保留：手写表 vs 数据驱动 |
| 22 | 2 | 保留：闪电与弹道 |
| 23 | 2 | 保留：飘字样式 |
| 24 | 2、3 | 保留：应答、命中音、界面音效 |
| 25 | 2、3 | 保留：音乐、循环音、声部 |
| 26 | 3 | 保留：客户端有 CAMS，Launcher 空接口 |
| 27 | 2、4 | 保留：跟随、设置、取景 |
| 28 | 1、3 | 保留：两套子组、点头像清队 |
| 29 | 1 | 保留：右键分支 |
| 30 | 1、3 | 保留：拖拽、拾取、地面物品详情 |
| 37 | 2、3 | 保留：命令卡结构 |
| 38 | 2、3 | 保留：槽位、热键、可用性 |
| 39 | 2、3 | 保留：提示语与悬停说明 |
| 40 | 1、3 | 保留：Esc 语义、顶部按钮、选项 |
| 41 | 2、3 | 保留：聊天框、编队按钮 |
| 42 | 2、3 | 保留：小地图图标、滤镜 |
| 43 | 2、4 | 保留：超宽屏、字号设置 |
| 44 | 3、4 | 保留：渲染能力、FDF 绑定 |
| 45 | 5 | 保留：命名与职责错位 |

## 已排除的候选（不再写进清单）

| 原编号 | 内容 | 排除理由 |
|---|---|---|
| 12 | 地形高度变形 | 客户端只有脚本会调 `TerrainDeformCrater`（`TerrainDeformationTrigger` 注释写明「没有任何技能调用它」），Launcher 不跑 JASS，这条永远不会触发；等接 JASS 再一起做 |
| 37 | 任务、盟友、排行榜、多面板、脚本对话框、过场遮罩、秘籍面板 | 全部来自 `_jassRuntime?.GetQuests()` 这类触发器数据，属于 Launcher 明确不做的 JASS/触发器范围；其中「地面物品详情」已并入 30 |
| 46 | 没有调用者的接口清单 | 不属于 1–5 任何一条，是代码卫生；其中会影响玩家的（`SetHint`、顶栏按钮、营地图标、`MinimapFilter`）已分别写进 39、40、42。排查时先 grep 调用者仍是个好习惯，但不当任务列 |
| 36 子项 | F4–F7 选英雄 | 单人最多 3 个英雄，永远用不到 |
| 38 子项 | 全体/盟友聊天频道 | 多人频道不做 |
| 43 子项 | 电影模式隐藏 HUD | 过场本身来自脚本，同 37 的理由 |
| 19 子项 | 动画时间缩放 `AnimationTimeScale` | 客户端只由 JASS `SetUnitTimeScale` 改这一个字段，Launcher 不跑 JASS，永远不会被设成非 1 |

---

## 推进顺序

编号已经是自底向上的顺序，从上往下做即可，但有两个例外：

1. **第 39 条**（提示语）和第 38 条（热键）现在是「按了没反应」，属于功能性缺陷，可以提前插队。

底层那一批里，**第 2、3、10** 是「一改就能并排看出差别」的，建议先啃这三个；**第 12** 跟在后面，因为它们和前面共用同一套地形/环境代码，隔开做会来回改同一个文件。

单位实体那一批（**15–19**）互不依赖，可以并行；特效与声音（**21–25**）里 22 的闪电和弹道最容易被玩家看出来。

## 收尾规则

- 一条做完就从本文删掉，序号不复用，后面的不重排；下一条新任务从 **62** 开始。
- 新发现的差异先判断是不是「平台差异」或「已知取舍」：Godot 与原生 GL 的差别、Launcher 固定 gmap 路径、不跑 JASS/触发器/战役/存档/录像/联机，这些不写进来。
- 写进来的时候要带三个东西：客户端在哪、Launcher 在哪、玩家看得到什么。只有「不一样」没有证据的，等复核清楚再写。

## 47. 地形与悬崖不接收 MDX 动态光

客户端 `Terrain.vert.glsl` 和 `Cliff.vert.glsl` 里各有一段 `dynamicLightFactor`，把单位的动态光（火把、法术特效）按距离/衰减累加进顶点光照；Launcher 的地形、悬崖、水面着色器只吃方向光和环境光。

要做：把 `war3_light_data` 也传进三个世界着色器，按客户端公式累加。

## 49. `EfctID` 那条艺术链没接

客户端 `War3EffectArtResolver` 把 `EfctID`（`X***`）解析成 `Effects\*.mdl`，挂到单位身上做常驻表现；Launcher 只读了 `CasterArt` / `TargetArt` / `SpecialArt` / `EffectArt` / `AreaEffectArt` 五个字段，`EfctID` 完全没读，Extract 也没导出过 `Effects/` 目录。

要做：Extract 先导出 `Effects/` 目录，再在 `AbilityArt` 里补 `EfctID` 这条解析。

## 50. 两条技能美术资源 Extract 没导出

`--effect-probe` 实测物编 976 条、美术引用 656、可加载 654，缺两条：`Arsp` 的 `Abilities\Spells\Other\Stampede\MissileDeath.mdl`、`AIga` 的 `Abilities\Spells\NightElf\NatureTouch\NatureTouchTarget.mdl`。

要做：Extract 补导出这两个模型，跑 `--effect-probe` 应变成「缺 0」。

## 51. `Ability.Arts` 没并进物编合并

客户端在 `LoadStandardTypedData` 里把 `Units*AbilityFunc` 这些 txt 并进同一份 `War3TypeData`，技能和 Buff 共用一份数据；Launcher 的 `AbilityArt` 单独读一张合并过的文本表，等于多了一层旁路。

要做：把技能美术字段并进 `War3ObjectData` 的物编合并，之后 `AbilityArt` 可以退化成按字段名取值。

## 52. 天气和特效粒子不吃雾

客户端 `Weather.frag` 按 `useFog` 把 `uFogColor/uFogParams` 传进天气粒子，MDX 粒子/丝带也一样（`MdxParticle2.frag`、`MdxRibbon.frag`）。

Launcher 的天气用 `StandardMaterial3D` + `DisableFog`，而全场景没有开 Godot 的 `Environment.FogEnabled`，`useFog` 等于空转；`war3_particle.gdshader`、`war3_ribbon*.gdshader` 也没有雾 uniform。地形、悬崖、水面、模型已接上 `atmosphere.gdshaderinc`。

要做：天气材质换成自定义着色器并吃 `atmosphere.gdshaderinc` 的雾；粒子和丝带在片元里调 `war3_apply_fog`。

## 53. w3i 子记录名和客户端还不一致

客户端 `MapInfoParser.cs` 里玩家/势力叫 `MapPlayerInfo` / `MapForceInfo`，雾、天气、水色、光照 tileset 这些环境字段直接平铺在 `MapInfo` 上；Launcher 现在叫 `W3iPlayer` / `W3iForce`，环境单独一层 `W3iEnvironment` 子对象，`War3WaterConfig` / `SkyboxRenderer` / `SceneLighting` 都按 `map.Info.Environment.X` 取值。

要做：两个记录改成客户端名字；环境字段平铺进 `MapInfo`，去掉 `W3iEnvironment` 这层，调用点同步。

## 54. 建筑在雾里被拆掉后仍然立刻消失

客户端把「已探索但看不见」的建筑冻成 CPU 快照，被拆掉时快照还在，直到 `ScoutingMemory` 把这一格重新看见才遗忘。

Launcher 现在会冻结序列和粒子（`UnitSceneComponent.SetFogFrozen`），模型也吃迷雾压暗，但建筑阵亡后没有「冻结快照」：模型停止推进、继续留在地上，重新看见时也不会按客户端逻辑遗忘。

要做：建筑在不可见时阵亡要保留最后一次看到的定格模型，重新可见时再按 `ScoutingMemory` 口径清掉。

## 55. 单位转身比客户端慢五倍多

客户端 `UnitFactory.TypeData.cs:139-140` 把 SLK 的 `turnRate` 直接当弧度/秒用：`DefaultTurnSpeed = turnRate`，`TurnRate = NativeTimedFacing.TurnSpeed(turnRate) / Period`（`Period = 0.03`），即每 0.03 秒转过 `turnRate` 弧度；`NativeTimedFacing.TurnSpeed` 只做 `max(0.001, …)`，没有额外下限或上限。步兵这一档换算出来约 20 rad/s，也就是 0.3 秒转一整圈。

Launcher `UnitFactory.cs:353` 是 `TurnSpeed = Mathf.Clamp(turnRate * 6f, 1.5f, 14f)`，`World.MovementEvents.ApplyFacing` 每仿真步最多转 `Mathf.Max(1.5f, TurnSpeed) * 0.05` 弧度。同一条 `turnRate` 只走出客户端五分之一到六分之一，而且 `1.5` 的下限会把慢速单位（攻城器械、建筑类转向）一起抬快。

玩家看到：点攻击后单位往往跑到贴脸才开始转身，侧身追人、绕圈时像在滑行；`NeedsAttackFacing` 的朝向窗口满足得晚，攻击前摇整体被拉长；编队齐转时更像整体平移而不是各自转身。

要做：`TurnSpeed` 按 `turnRate / 0.03` 换算并去掉 1.5 下限，上限对齐客户端量级；改完必须回归 `--combat-probe`、追击贴脸、编队转向、攻击前摇四项——这条动的是战斗节奏，不只是表现。

## 56. 天气粒子还在用 CpuParticles3D，跨格重启会把整窗雨打回起点

先把结论放前面：**tail 几何不用补。** 客户端 `docs/weather-atmosphere-rendering.md:54` 记着几何是「面向相机的 ±1 方片 × `scale` 轨道」，按 `|veloc|*taillen` 把方片拉成雨丝试过并撤回——`rainTail.blp` 实测 32×16（2:1），长条形态由纹理本体提供，拉伸只会让 2:1 贴图变形。客户端 `WeatherDefinition` 因此只把 `head/tail/taillen` 当诊断字段，`Weather.vert.glsl` 里没有任何拉长指令。

Launcher 的几何口径也已经对齐：`WeatherRenderer` 用 2×2 `QuadMesh`，`weather.gdshader` 里 `VERTEX.xy *= weather_timeline(weather_scale, life)`，与客户端 `uCameraRight*aCorner.x + uCameraUp*aCorner.y` 同一个 ±1 方片 × scale。

差的是提交与相位：客户端 `WeatherRenderer` 一次 `DrawElementsInstanced` 画整个 7×7 窗格，相位由 `CellSeed(weather, col, row)` 确定性生成，跨格只在 `RebuildVisibleWindow` 里重传实例缓冲；Launcher 是 7×7 个 `CpuParticles3D`，`AcquireWindow` 每跨一格就 `emitter.Restart()`，`Preprocess = LifeSpan` 再跑一遍，整窗雨的相位被重置，窗口滑动时雨会整片跳一次。发射域也偏了：Launcher 用 `EmissionShapeEnum.Box`（`512×1×512` 的水平盒），客户端 `ScatterOffset` 让散布平面随 `angx/angy` 一起倾斜。

要做：天气粒子改成实例化 billboard 走 `weather.gdshaderinc`，相位按 cell 索引确定性生成，跨格只重建实例缓冲、不重启发射器；散布按 `angx/angy` 倾斜平面；几何继续方片，不按 `taillen` 拉伸。验证：`--path . --rendering-driver opengl3 -- --play="(4)Turtle Rock.gmap" --shot=...@N`，看跨格滑动时雨有没有整片断档。

## 57. 雾纹理参数搭着光照刷新才下发，进图后有一段时间没雾

客户端 `FogOfWarRenderer.Draw` 每帧算完淡变就 `FogOfWarUniforms.Publish(_texture, FogOfWarUvp, true)` 写进共享状态，六个接收者（地形/悬崖/水/岸浪/单位/公告板）在各自的 draw 里 `FogOfWarUniforms.Upload(shader)`，纹理一更新同一帧就生效。

Launcher 的 `FogOfWar.Upload()` 只调 `SceneFog.SetFogOfWar(...)` 存静态字段，真正写进材质是在 `SceneLightRig.Apply` 里的 `SceneFog.Write`——而 `Apply` 只有 `LoadTerrainPresentation` 调一次，之后靠 `TickClock` 的「时辰变化 ≥0.05 小时」触发。`_fog.Build()` 在 `LoadPathing`（加载末尾）才跑，那一刻 `_fogOfWar` 还是 null，材质里根本没有 `fog_of_war_texture/uvp/enable`。

玩家看到：进图后地图先全亮，等下一次时辰跳变（约 1 秒）雾才突然铺上去。实测 5.6 秒截图已经有雾，窗口短，但违反「纹理更新即生效」。

要做：`FogOfWar.Upload()` 写完 `SceneFog.SetFogOfWar` 之后立刻把三项参数重写进已创建的材质，别再搭光照刷新的车。

## 58. `SceneFog` 该叫 `FogOfWarUniforms`，而且不该管大气雾

客户端 `GameCore/L4_Composition/Game/Rendering/FogOfWarUniforms.cs` 是「战争迷雾 uniform 的统一上传入口」，只管雾：`Texture` / `Uvp` / `Enabled` 三个状态加 `Publish` / `Reset` / `Upload(IShaderProgram)`。大气雾不在这里，客户端是各渲染器自己按 `FogSettings` 传 `uFogColor` / `uFogParams`。

Launcher 把两件事塞进了同一个类：`GameCore/L4_Composition/Game/Environment/SceneFog.cs`——名字、路径都和客户端对不上，类里同时有 `fog_color` / `fog_params`（大气雾）和 `fog_of_war_*`（战争迷雾），`SceneLightRig.Apply` 里一次调两样。

要做：拆开。战争迷雾那半改叫 `FogOfWarUniforms` 并挪到 `Game/Rendering/`，只留雾纹理/UV/开关三项；大气雾留在 `War3FogConfig`，由地形、悬崖、水面、模型各自在材质创建处传参，`SceneLightRig.Apply` 只负责光照。

## 59. 坡面姿态在客户端是场景 partial 方法，Launcher 抽成了独立类

客户端 `GameCore/L4_Composition/Game/Scenes/War3GameScene/Presentation.cs:489` 是 `internal static (float Pitch, float Roll) ComputeUnitTerrainTilt(Vec2 position, float facing, float sampleRadius, float maxPitchDegrees, float maxRollDegrees, Func<float,float,float> heightAt)`——挂在 `War3GameScene` 这个 partial class 上，同目录的 `Diagnostics/UnitPresentationDataRegression.cs:299` 也直接调它。

Launcher 写成 `GameCore/L4_Composition/Game/Rendering/UnitTerrainTilt.cs` 的 `internal static class UnitTerrainTilt.Compute(...)`。算法逐行一致（四点采样、`atan2(前-后, 2r)`、`clamp` 到 `maxPitch/maxRoll`、负角度当固定倾角），差的是归属与位置：客户端在场景 partial 里，Launcher 单开一个类还放进了 `Rendering/`。

要做：按客户端归位——方法搬进 `War3GameScene` 的 partial（可以放在 `Scenes/War3GameScene/Presentation.cs` 同目录的新 partial 文件里，类名仍是 `War3GameScene`），调用点从 `UnitSceneComponent` 改成经场景转调；如果确认 Godot 侧 `UnitSceneComponent` 拿场景引用不方便，退一步也要放到 `GameCore/L4_Composition/Game/Scenes/War3GameScene/` 下而不是 `Rendering/`，并在类名上体现它是场景的一部分。

## 60. 转成 Godot 动画时丢掉了关键帧切线

客户端 `MdxTimelineSampling` 按序列区间求值，关键帧插值支持 step / linear / Hermite / Bezier 四种（`MdxAnimation` 的 `InterpType`），贝塞尔还会取 inTan/outTan 做缓入缓出。

Extract 的 MDX 转换只写 TRS 轨道关键帧的位置/旋转/缩放数值，切线信息整条丢掉，Godot 一律按线性插值。玩家看到：所有带缓入缓出的动作（起手、收招、建筑生长、死亡倒地）都变成匀速，动作发飘、缺少原版的重量感。

要做：Extract 导出时把切线按 Godot `Animation` 支持的曲线表达出来（`Transform3D` 轨道只能是线性，得改成可承载自定义插值的轨道形式或按切线重采样成更密的线性关键帧），Launcher 侧不需要改。

## 61. 粒子参数还是待机帧快照，数量也夹在 512

客户端的 `ParticleEmitter2` 每帧按当前动画帧求 `KP2S`（速度）、`KP2L`（纬度）、`KP2G`（重力）、`KP2E`（发射率）、`KP2W`/`KP2N`（宽/长）、`KP2V`（可见度），粒子的位置/速度/重力和寿命都在运行时逐实例推进。

Launcher 在 Extract 侧把发射器烘成 `GPUParticles3D`：参数取待机帧那一帧的快照，`amount = ceil(emissionRate * lifeSpan) + 8` 再硬夹到 8..512，所以发射率随动画变化的特效（呼吸、爆发、蓄力）全程按待机速率出粒子，粒子多的特效被削到一半。

要做：把上述轨道按片段导出，运行时按 `AnimationPlayer` 当前位置驱动 `ParticleProcessMaterial` 的对应参数，并按整段最大发射率给 `amount`。

