# 提高 Launcher 与客户端侧一致化的任务列表 V2

这份清单只记真正会造成流程、画面或行为差异的地方。判定标准沿用 AGENTS.md：

1. 客户端和 Launcher 做同一件事，但执行流程不一样。
2. 客户端和 Launcher 看到的效果不一样，尤其是动画、特效、加载和镜头。
3. 客户端已经有现成实现，Launcher 却另写了一套，导致后续很难保持同步。
4. Launcher 可以共用代码的情况下没有共用。
5. Launcher 的 C# 文件在有对应客户端侧 C# 文件时，相对路径、文件名、类名不一致。

**本次按模块分组、模块内从底层到表层重排**：V2 编号 1..33，已落地任务删除后编号留空、不复用；标题后的 `（V1 #xx）` 是上一份文档的原编号，便于追溯；本文之后的新任务从 **34** 起，做完一条删一条，序号不复用。

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
> 任务 6（地形/悬崖动态光）、任务 7（装饰物死亡姿态）、任务 9（迷雾参数即时下发）、任务 10（雾中建筑定格记忆）已落地并合并到 Launcher，编号不复用。

### 8. 天气粒子还是每格一个 CpuParticles3D，跨格重启整窗雨（V1 #56）

- **客户端**：一次 `DrawElementsInstanced` 画整个窗格（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Environment/WeatherRenderer.cs:328`），相位由 `CellSeed(weather,col,row)` 确定性生成（`:350`），跨格只 `RebuildVisibleWindow` 重传实例缓冲（`:368`），并让散布平面随 `angx/angy` 倾斜。
- **Launcher**：`_cellsPerSide × _cellsPerSide` 个 `CpuParticles3D`（`Launcher/GameCore/L4_Composition/Game/Environment/WeatherRenderer.cs:69-94`），`AcquireWindow` 跨格时 `emitter.Restart()`（`:199`），`Preprocess = LifeSpan` 再跑一遍；发射域是水平 `Box`（`:85-87`），不随风向倾斜。
- **表现差异**：镜头跨格滑动时整窗雨/雪会跳一次相位；斜风雨的落点分布和客户端不同。
- **要做**：改成实例化 billboard 走 `weather.gdshaderinc`，相位按 cell 索引确定性生成，跨格只重建实例缓冲；散布按 `angx/angy` 倾斜；几何继续用方片，不按 `taillen` 拉伸。

> **执行结论（Launcher8，待合并）**：天气改成单个 `MultiMesh` 实例缓冲。
> - 着色器：`Extract/assets/shaders/weather.gdshader`、`weather_add.gdshader` 从 `INSTANCE_CUSTOM.xyz` 读速度、`.w` 读相位，按 `TIME` 求年龄、位移与 accel，用相机基向量把 2×2 方片做成 billboard；颜色/UV/大气雾继续走 `weather.gdshaderinc`。该改动已随 Extract 本地提交 `e84fa08fb` 进入版本库（与任务 6 同一提交）。
> - 渲染：`Launcher8/GameCore/L4_Composition/Game/Environment/WeatherRenderer.cs` 去掉每格 `CpuParticles3D` 与跨格 `Restart()`，改成单个 `MultiMeshInstance3D`；容量 = 窗口格数 × 每格粒子数，`CellSeed(weather,col,row)` 生成每格固定随机种子，实例变换存出生点、`INSTANCE_CUSTOM` 存速度与相位；只在相机跨 512 格时重建实例缓冲。
> - 散布：`ScatterOffset` 按 SLK `angx/angy` 旋转散布平面并换算到 Launcher 世界轴；几何保持 2×2 方片，不读 `taillen`。
> - 验证：`dotnet build Launcher.csproj` 0 错误；强制 RAhr 时日志为 `cells=5x5 perCell=90 capacity=2250`，跨格日志 `重建实例 2250，容量 2250`；用独立 `SceneTree` 场景加载 pck 内 `weather_add.gdshader` 与 rainTail 贴图，2000 实例正常出雨。全图截图仍受并行任务占满 GPU 的 Metal fence 超时影响，未作为画面证据。
> - 合并注意：`extract.pck` 已在本地重打包；合并 Launcher8 提交 `d428e56` 时需一起带入 Extract 提交 `e84fa08fb` 里的两个天气着色器。

### 11. 单位转身比客户端慢五倍多（V1 #55）

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Configuration/UnitFactory.TypeData.cs:139-140` 把 SLK 的 `turnRate` 当弧度/秒，`TurnRate = TurnSpeed(turnRate) / Period`，`Period = 0.03`（`Open-Warcraft3_Wiki/Src/Core/Pathing/NativeMirror/Combat/NativeTimedFacing.cs:7`），步兵约 20 rad/s。
- **Launcher**：`Launcher/GameCore/L3_Simulation/Simulation/Configuration/UnitFactory.cs:357-358` 是 `Mathf.Clamp(turnRate * 6f, 1.5f, 14f)`，`Launcher/Src/World/World.MovementEvents.cs:155` 每仿真步最多转 `Mathf.Max(1.5f, TurnSpeed) * 0.05`；同一条 `turnRate` 只走出客户端五分之一到六分之一，1.5 的下限还会把慢速单位一起抬快。
- **表现差异**：点攻击后单位跑到贴脸才开始转身，侧身追人/绕圈像滑行；攻击前摇被拉长，编队齐转像整体平移。
- **要做**：`TurnSpeed` 按 `turnRate / 0.03` 换算并去掉 1.5 下限，上限对齐客户端量级；改完回归 `--combat-probe`、追击贴脸、编队转向、攻击前摇四项。

> **执行结论（Launcher1，待合并）**：
> - 换算改为客户端口径：`UnitFactory` 去掉 `Clamp(turnRate * 6f, 1.5f, 14f)`，改为 `turnRate / 0.03f`；`UnitType.TurnSpeed` 默认值从 3.6 改为 20（对应客户端 `Transform.TurnRate = 20` 的默认 0.6 rad / 0.03s）。不设上限，SLK 实际 `turnRate` 为 0.1–0.6，折合 3.33–20 rad/s；若沿用 14 会把 0.5/0.6 档的单位重新削慢。
> - `World.MovementEvents.ApplyFacing` 去掉 `Mathf.Max(1.5f, ...)` 下限，每 tick 最大转角改为 `TurnSpeed * FrameSeconds`。
> - 数值验收（临时探针打印，已还原）：`hfoo`/`hrif`/`ogru` 20 rad/s，`nftr`/`hgyr` 16.667 rad/s；半圈耗时 0.157s / 0.188s，修复前步兵为 3.6 rad/s、半圈 0.873s。
> - 回归：`dotnet build Launcher.csproj` 0 错误；`--combat-probe` 全部通过；`--state-probe` 仅剩基线已知失败 `utod 研究项 4 vs 2`，阵型/命令/追击相关校验均通过。攻击前摇不经过朝向门控（`BeginAttack` 直接用同一 tick 的 `FaceTarget` 结果开火），本次只改视觉转角速度，前摇时序不变。
> - 合并注意：无新增资源或探针代码，只改 `Launcher1` 的两个 C# 文件；文档结论随根仓库单独提交。

### 12. Buff 类技能只消耗部分物编字段，护甲/回复/缩放/Buff 美术都没生效

- **客户端**：以 Roar 为例读取 `DataA`（攻击）、`DataB`（护甲）、`DataC`（回血）、`DataD`（回蓝）、`DataG`（目标上限）、`BuffID`（Buff 美术/名称）和 `HeroDur`（`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Configuration/RuntimeRoarAbility.cs:27-48`）；Bloodlust 还读 `DataC`（模型缩放）与 `BuffID`（`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Configuration/RuntimeBloodlustAbility.cs:24-47`），并生成对应 Buff 对象数据。
- **Launcher**：`RuntimeRoarAbility` 只有 `DamagePct => Data('A')`，施法时给一个只带攻击力的 `StatBuff`（`Launcher/GameCore/L3_Simulation/Simulation/Configuration/RuntimeRoarAbility.cs:10`、`:20`）；`RuntimeBloodlustAbility` 只有 `DataA/DataB`，同样用 `StatBuff`（`Launcher/GameCore/L3_Simulation/Simulation/Configuration/RuntimeBloodlustAbility.cs:10-11`、`:25-29`）。没有读 `BuffID`，也不绑 Buff 物编，所以没有 Buff 美术、没有护甲/回复、没有模型放大、英雄持续时间也与普通单位共用。
- **表现差异**：咆哮只加攻击力，不给护甲和回血回蓝；嗜血不会把模型放大；两种增益在单位身上都没有对应的 Buff 图标/特效；英雄吃到的持续时间与客户端不同。
- **要做**：按客户端补 `DataB/C/D/G`、`HeroDur`、`BuffID` 与目标规则；Buff 实例绑到 `BuffData`，让 `Buff` 的美术与名称走物编。

> **复核评价**：证据成立，「要做」漏了两处客户端规则：`RefreshRoarData` 还读 `DataF`（`PreferSharedSpellRecipients`），并用「护甲或回血非零」推出 `AllowsRecipientsWithoutAttack`，让没有攻击能力的接收者也能吃到咆哮。这两条不补，改完仍会出现「一队里有些单位吃不到增益」的差异。建议在「目标规则」后面点名 `DataF` 和 `AllowsRecipientsWithoutAttack`。

> **执行结论（Launcher2，待合并）**：Roar/Bloodlust 已按客户端搬运，补齐物编字段、Buff 物编绑定与目标规则。
> - Roar：`GameCore/L3_Simulation/Simulation/Configuration/RuntimeRoarAbility.cs` 改为客户端结构，读 `DataA/B/C/D/F/G`、`Dur/HeroDur`、`BuffID`、`targs`；攻击加成用 `BonusDamagePct`，护甲/回血/回蓝写入 `BonusArmor`/`AlwaysLifeRegenBonus`/`ManaRegen`，幻象按 `IllusionsGetDefenseBonus`/`IllusionsCanRestoreLife`/`IllusionsCanRestoreMana` 放行；`AllowsRecipientsWithoutAttack` 由「护甲或回血非零」推出；`DataF` 参与 `TargetLimit` 排序，取数上限与排序按客户端。新增 `RuntimeRoarBuff` 增量施加并精确回退，绑定 `Broa` 物编后名称与美术（`RoarTarget.mdl` → `overhead`）走物编。
> - Bloodlust：`RuntimeBloodlustAbility.cs` 读 `DataA/B/C`、`Dur/HeroDur`、`BuffID`、`targs`；新增 `RuntimeBloodlustBuff` 负责攻速/移速/模型缩放，缩放同时写 `VisualScale` 与场景根节点，刷新时按客户端只换攻速/移速、不重复叠加缩放；绑定 `Bblo` 后美术取 `BloodlustTarget.mdl`（客户端同样只取第一个 TargetArt），`RuntimePreloadArts` 同步预载。英雄与普通单位时长按 `UsesHeroSpellRules` 分开。
> - 物品路径：`AIrr`（野兽卷轴）随工厂改用带物编构造后，`World.UseItemAbility` 只调 `OnCast`，因此在 `RuntimeRoarAbility.OnCast` 补了「按 `Type` 绑定数据后转 `OnEffect`」的入口，避免物品技能失效。
> - 配置：`War3GameplayConfig` 补 `IllusionsCanRestoreLife`（`Misc.IllusionsCanRestoreLife`），供咆哮回血判定使用。
> - 验证：`dotnet build Launcher.csproj` 0 错误；`-- --state-probe` 新增断言全部通过——嗜血缩放 1.3、`Bblo` 绑定、`BloodlustTarget` 美术与预载；咆哮 `Broa` 绑定、`RoarTarget` 美术、护甲/回血/回蓝增量与移除回退；野兽卷轴恢复可用。整轮仅剩基线已知失败 `utod 研究项`。
> - 说明：Launcher 侧 AI 自动施法仍是简化的友军扫描，未移植 `TryAcquireAutoCastTarget` 里依赖 `NativeAutoCastScanRadius`/`PredictedObjectPoint` 的选目标细节；本次对齐的是增益数值、目标规则、时长与表现。
> - 合并注意：`Src/Abilities/RuntimeAbilityFactory.cs` 把 `Aroa/Ara2/ACro/AIrr/Ablo/ACbl/ACbb` 改为 `WithObjects<>`，与其它 Launcher 副本同名行易冲突；探针断言加在 `CombatProbe.UnitSkills.cs` 的 `CheckBloodlust`/`CheckRoar`。本次未改 Extract，无需重建 `extract.pck`。

### 13. 地面物品靠走近自动吸走，客户端要求右键下单

- **客户端**：拾取是显式订单：`IssuePlayerPickupItem`（`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Jass/JassGameNatives.cs:800-819`）由右键/交互层下发（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Input/War3WorldInteractionComponent.cs:268`）；接受半径读 `Misc.PickupItemRange`（默认 250，`Open-Warcraft3_Wiki/Src/Configuration/War3GameplayConfig.cs:274`），单位走到物品旁再执行拾取。
- **Launcher**：每 tick 由 `TickItemPickup` 扫描所有地面物品，任何 `CanCarryItems` 且有空格、距离小于硬编码 `PickupRange = 150f` 的单位都会被直接塞进背包（`Launcher/Src/World/World.Items.cs:14`、`:282-303`、`:351-367`）；右键物品只是下一条「走到物品位置」的移动单（`Launcher/GameCore/L4_Composition/Game/Input/War3WorldInteractionComponent.cs:323-333`），真正的拾取仍由走近触发。
- **表现差异**：单位路过就自动吸走物品，甚至可以抢走敌方单位附近的掉落；客户端不右键就不会捡，且拾取距离由 `PickupItemRange` 决定。玩家会看到掉落物莫名其妙进背包。
- **要做**：去掉按距离自动拾取，改成明确的拾取订单；半径读 `Misc.PickupItemRange`，通过寻路走到物品旁再结算。

> **执行结论（Launcher3，待合并）**：拾取已改为显式订单，走近不再自动吸入。
> - 订单：`Launcher3/Src/World/World.Orders.cs` 新增 `UnitOrderKind.PickupItem` 与 `UnitOrder.Pickup(item)`，`Unit` 增加 `PickupTarget`；`ApplyOrder`/`ClearOrders`/`HasActiveOrder` 一并纳入，新指令、停止都会打断拾取。
> - 结算：`Launcher3/Src/World/World.Items.cs` 新增 `IssuePickupItem`（对齐客户端 `IssuePlayerPickupItem`，返回 `None/Issued/InventoryFull`）与 `StepPickupItem`（走到半径内再 `AddItem` 并入包发事件）；删除 `TickItemPickup`/`NearestPicker` 全量扫描，`World.Advance` 不再自动捡物。
> - 半径：`Launcher3/Src/Configuration/War3GameplayConfig.cs` 读取 `Misc.PickupItemRange`（缺省 250）；当前表值 150，与客户端默认 250 的差异来自数据本身。
> - 输入：`Launcher3/GameCore/L4_Composition/Game/Input/War3WorldInteractionComponent.cs` 右键物品改为主位单位下发拾取订单，满包提示「物品栏已满。」且不移动；接单后其余选中单位仍走向物品落点（客户端 `MoveUnhandled`）。
> - 联动：商店买满包落地、作弊与探针的旧 `TickItemPickup` 调用改为显式下单；`--state-probe` 新增「未下令不会自动拾取 / 下发拾取订单 / 英雄走到物品旁拾取 / 非英雄拾取被拒」四项。
> - 验证：`dotnet build -t:Rebuild` 0 错误；`--state-probe` 上述四项全 OK（唯一失败仍是基线的 `utod 研究项`），`--combat-probe` 全部通过。
> - 操作：用 `Godot --path Launcher3 -- --play="(4)Turtle Rock.gmap"` 进入游戏，英雄丢下一件物品后另一名英雄从旁边走过不会拾取；右键物品才会寻路走到 `Misc.PickupItemRange` 内入包，背包满时只出提示。
> - 范围说明：Launcher 的 AI 没有物品拾取逻辑，改动后 AI 英雄也不会再顺路吸走掉落物；若要 AI 主动拾取，属于客户端 melee AI 系统的迁移，另立任务。

### 14. Powerup 物品（经验书/属性书/符文）不会自动使用

- **客户端**：`powerup` 字段为 1 的物品，拾取时走自动使用管线（`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Jass/JassGameNatives.OrdersAndItems.cs:409-411`、`:517-561`），执行子技能后消耗（`:993-1020`；`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Jass/JassGameNatives.ItemUseRuntime.cs:47-57`）。
- **Launcher**：`ItemType.PowerUp` 只在 UI 里显示「一次性」（`Launcher/GameCore/L3_Simulation/Simulation/Configuration/ItemCatalog.cs:28`、`:104`；`Launcher/GameCore/L4_Composition/Game/UI/MeleeHudController.cs:171`），没有运行时消费方；拾取后作为普通物品进背包（`Launcher/Src/Units/Unit.Items.cs:57` 的 `AddItem`），`ItemUseRules` 也没有 powerup 分支。
- **表现差异**：捡到经验书/属性书会占一个背包格且无法使用，符文也不会在踩上去时立即生效；客户端是拾取即用、不占格。
- **要做**：读取物编 `powerup`，在拾取路径上先执行物品技能再消耗，不进入背包；补齐 `PowerupSound`/拾取事件。

### 15. 地面物品没有影子，Powerup 没有金色光圈，离场也没有缩小

- **客户端**：地面物品统一用 `ReplaceableTextures\Shadows\Shadow.blp` 投影，尺寸取模型包围盒，普通物品 alpha 0.45、Powerup 用金色 0.75（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Scenes/War3GameScene/Presentation.cs:2121-2160`）；`Departing` 的物品（离场缩小）不投影。
- **Launcher**：`CreateItemNode` 只加载模型、设缩放（`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/Units.cs:281-294`），没有物品投影，也没有 Powerup 光圈/离场状态。
- **表现差异**：地上物品“浮”在地表，没有影子；Powerup 与普通物品视觉上无法区分；物品被拾取/消失是瞬间消失，没有缩小过渡。
- **要做**：补地面物品阴影与 Powerup 金色光圈；给物品加离场缩小状态并在离场期间停止投影。

## 模块四：特效与声音

> MDX 粒子、丝带、弹道与音乐、事件音。

### 16. 粒子参数还是待机帧快照，数量夹在 512（V1 #61）

- **客户端**：`ParticleEmitter2` 每帧按当前动画帧求 `KP2S`、`KP2L`、`KP2G`、`KP2E`、`KP2W/KP2N`、`KP2V`，运行时逐实例推进（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Rendering/MdxParticle2Renderer.cs:249-277`）。
- **Launcher**：Extract 侧把发射器烘成 `GPUParticles3D`，速度/纬度/重力/宽/长/可见度取待机帧快照（`Extract/src/Core/Tools/ModelBundle.cs:1559-1566`）；`amount` 已用整段最大发射率计算（`:1562`、`:1855`），但仍硬夹到 8..512，运行时不再随动画帧更新。
- **表现差异**：发射率、速度、重力随动画变化的特效（呼吸、爆发、蓄力）全程按待机参数出粒子；粒子多的特效被削到 512。
- **要做**：把上述轨道按片段导出，运行时按 `AnimationPlayer` 当前位置驱动 `ParticleProcessMaterial` 参数；`amount` 按整段最大发射率给，去掉 512 上限或按客户端上限。

### 17. MDX 粒子不吃大气雾（V1 #52，部分修复）

- **客户端**：`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Shaders/MdxParticle2.frag.glsl` 按 `useFog` 把 `uFogColor/uFogParams` 传进粒子，丝带同样。
- **Launcher**：天气已经改用 `weather.gdshaderinc` 并调 `war3_apply_fog`（`Extract/assets/shaders/weather.gdshaderinc:42`），丝带也已接上（`war3_ribbon.gdshader:23`、`war3_ribbon_add.gdshader:23`）；但 `war3_particle.gdshader` 没有包含 `atmosphere.gdshaderinc`，`fragment()` 里没有雾项（`Extract/assets/shaders/war3_particle.gdshader:69-91`）。
- **表现差异**：远处的技能粒子/环境粒子在浓雾图里保持原色，和地形、模型、丝带的雾感不一致。
- **要做**：`war3_particle.gdshader` 引入 `atmosphere.gdshaderinc`，片元里按视深调 `war3_apply_fog`。

### 18. 飞弹模型几乎不可见（V1 #63）

- **客户端**：弹道用 `CharacterRenderer` 画模型，箭、火球、投石在游戏里都看得很清楚。
- **Launcher**：`OnMissileFired` 实例化模型、设位置、加进 `_projectiles`（`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/Presentation.cs:33-79`）。Turtle Rock 实测弓箭手的 `earc` 弹道节点存在、位置在相机视野中心、`Visible=true`，模型放大 4 倍和 12 倍后截图里仍没有箭；场景结构 `Node3D → Model/Skeleton3D → geoset0`，材质 `war3_model_opaque`（`alpha_cutoff 0.75`、贴图 `ArrowMissile.png`）完整，问题在渲染侧而不是导出。
- **表现差异**：远程单位对射时看不到箭/火球/投石，只有伤害数字和命中音。
- **要做**：先确认是骨骼/Stand 动画把网格挪走，还是投射物节点/材质在运行时被改写；修好后用弓箭手对射复验箭、火球、投石三类飞弹。

> **复核评价**：表现差异有证据，修法还没有，这类条目容易被当成能直接下手的任务。建议标题标 `（待定位）`，定位结论写回本条再动手。定位可以先量 `_projectiles` 节点的运行时 AABB：放大 12 倍时 AABB 一起变大，说明网格在、只是被裁或全透明；AABB 塌成一点，说明骨骼/动画把网格移出了包围盒。

### 19. 音乐无缝连播，也没有脚步和建筑循环音（V1 #25）

- **客户端**：曲间有 20 秒间隔（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Audio/War3MusicPlayer.cs:3`），支持主题音乐叠加、淡入淡出、`SetMapMusic/PlayMusic/StopMusic`、三层音量；`War3LoopingSoundService` 按 `MovementSoundLabel` 播脚步并跟随位置，建筑建造时播 `BuildingSoundLabel`（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Audio/War3LoopingSoundService.cs:7`）；`War3AudioSystem` 24 个系统声部、同文件最多 3 个、按 channel 配额/优先级抢占，每条声音用自己的 Min/MaxDistance。
- **Launcher**：`War3MusicPlayer` 播完立刻下一首，固定 `-10dB`，没有间隔、淡入淡出和脚本接口（`Launcher/GameCore/L4_Composition/Game/Audio/War3MusicPlayer.cs:14`、`:26`、`:69-74`）；没有循环音服务；`War3AudioSystem` 只有 16 个 3D + 8 个 2D 固定播放器，`UnitSize = 900`、`MaxDistance = 4500` 写死（`War3AudioSystem.cs:12-42`），表里的 Min/MaxDistance 没有使用；没有音量设置；客户端加载 11 张声音表（ack/combat/anim/ui/ambience/ability/dialog/midi/environment/EAX/portrait，`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Audio/War3SoundTables.cs:26-44`），Launcher 只有 5 张（`Launcher/GameCore/L4_Composition/Game/Audio/War3SoundTables.cs:44-48`）。
- **表现差异**：音乐一直接一首、没有呼吸感；行军完全没有脚步声，建筑建造没有循环施工音；远处/近处声音衰减一致，声部抢占规则不同。
- **要做**：曲间间隔与淡入淡出、脚步/建造循环音；声部按 channel 配额和优先级；音量设置与第 21 条的选项界面一起做。

> **复核评价**：本条依赖第 21 条的选项界面，而 21 在模块五，「模块内从上往下」会先把 19 做到一半卡住。建议把 19 一并放进「提前插队」名单，或者把「音量设置」整段挪进 21 条。

### 20. 城镇被攻击、英雄阵亡这几类事件音还没接（V1 #62）

- **客户端**：`War3UiSoundService` 有 `TownAttack`、`UnderAttack`、`HeroDies`、`AllyHeroDies`，由模拟层事件源驱动（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Audio/War3UiSoundService.cs:65-67`；`MeleeHudController` 里按事件派发）。
- **Launcher**：冷却表和种族前缀已经建好（`Launcher/GameCore/L4_Composition/Game/Audio/War3UiSoundService.cs:10-41`），但上述四个键没有任何调用点（全仓只有定义和生成文本）；建筑被攻击不报警，英雄阵亡也不出声。
- **表现差异**：混战里少了「基地被打」和「英雄阵亡」的听觉提示。
- **要做**：在 `World` 的建筑受伤路径上区分城镇与其他建筑，派 `TownAttack/UnderAttack`；在死亡路径上区分英雄、自己与盟友，派 `HeroDies/AllyHeroDies`。

### 33. 烈焰风暴/云雾/季风/哨兵/火山的美术没写，陨石雨技能缺失（新增）

- **客户端**：这五个技能都把 `EfctID` 解析出的模型接进生成循环：烈焰风暴余烬取 Buff 的 `EffectArt`（`Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Configuration/RuntimeFlameStrikeAbility.cs:51`、`:73-80`），云雾取 `Effectart` 常驻雾团（`RuntimeCloudOfFogAbility.cs:46`、`:69-76`），季风取雨幕与闪电落点（`RuntimeMonsoonAbility.cs:32`、`:85-87`），哨兵在树上生成猫头鹰（`RuntimeSentinelAbility.cs:29-31`、`:50-52`），火山生成喷发视觉与熔岩弹（`RuntimeVolcanoAbility.cs:127`、`:151`、`:226`）；另外客户端有独立的 `RuntimeMeteorRainAbility.cs`。
- **Launcher**：上述五个类（`Launcher/GameCore/L3_Simulation/Simulation/Configuration/RuntimeFlameStrikeAbility.cs`、`RuntimeCloudOfFogAbility.cs`、`RuntimeMonsoonAbility.cs`、`RuntimeSentinelAbility.cs`、`RuntimeVolcanoAbility.cs`）只结算伤害/状态，没有 `Art`、`Effect`、`Spawn` 相关的美术字段与生成循环；全仓也没有 `RuntimeMeteorRainAbility` 对应类。
- **表现差异**：烈焰风暴只有伤害数字没有柱状余烬，云雾没有雾团遮挡，季风没有雨幕和落点闪电，哨兵没有停在树上的猫头鹰，火山没有喷发和熔岩弹；陨石雨技能整个缺失。
- **要做**：逐个按客户端补美术字段、`RuntimePreloadArts` 与生成/销毁循环；火山和陨石雨还要补对应的 `Effect`/`Missile` 类。

> **复核评价**：五个技能那半成立，陨石雨这半越界。`ANme` 在 `test/war3-unpack/Units/AbilityData.slk` 里没有行（大小写都查过），`UnitAbilities.slk` 里没有任何单位携带，全仓只出现在战役脚本和 `Blizzard.j`；客户端 `RuntimeAbilityFactory.Constructors.cs:167` 的注册在 1.27 数据下是死代码。按本清单范围「只有脚本能触发的效果不进清单」，陨石雨应从本条删除，`RuntimeMeteorRainAbility` 不必补。另外编号 33 夹在 20 和 21 之间，破坏了「模块内从上往下」的读法，技能美术应排到模块三末尾。

## 模块五：输入与镜头

> 键鼠语义、Esc/菜单与快捷键。

### 21. Esc 语义、顶部按钮和选项界面（V1 #40，部分修复）

- **客户端**：Esc 分层取消（取消待定命令 → 关菜单；2026-10-22 起不再开菜单），菜单开关是 F10（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/Scenes/War3GameScene/Input.cs:1349-1351`）；Backspace 循环主城（`:1306`）、PageUp/Down 缩放（`:1340`）、Ctrl+C/Alt+C 居中、Alt+F 阵型、Alt+G/T/A 小地图；Esc 菜单用 FDF `EscMenuMainPanel`；`AboutDialogFrame` 有镜头距离、平移灵敏度、音乐/音效音量、帧率上限、渲染倍率（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/AboutDialogFrame.cs`）。
- **Launcher**：Esc 现在会先取消施法、再关菜单、最后开菜单（`Launcher/GameCore/L4_Composition/Game/Input/War3InputComponent.cs:41-47`），但放置建筑时 `_placingId` 不在取消分支里，Esc 会弹菜单且幽灵建筑还在；没有 F10/Backspace/PageUp/PageDown/Ctrl+C/Alt+F，Alt+G/T/A 也没有调用者。`TopBarFrame` 暴露了 `MenuButton/QuestButton/AlliesButton`（`TopBarFrame.cs:31`），但没有任何 `.Clicked +=`，顶部按钮可点没反应；Esc 菜单和胜负面板是 `ColorRect + Label + Button` 手搓，坐标写死在 1600×900；没有选项界面。
- **表现差异**：放置时按 Esc 关不掉幽灵建筑；想开菜单只能按 Esc；编队/镜头快捷键缺失；顶部菜单/任务/盟友按钮点了没反应；没有任何画质/音量/镜头设置。
- **要做**：把放置建筑也纳入「取消优先」，补 F10；顶部菜单按钮接 Esc 菜单（任务/盟友按范围隐藏）；选项界面和音量一起做。

## 模块六：界面

> 命令卡、提示、小地图、头像与 HUD 布局。

### 22. 命令卡的槽位、热键和可用性判定（V1 #38）

- **客户端**：`FindNativeSlot` 是「向 11 找空位、再往 0 回退、满卡就丢」（`Open-Warcraft3_Wiki/GameCore/L4_Composition/Game/UI/CommandCardBuilder.cs:426`）；热键按按钮真正显示的 `Hotkey` 分发（`:90` 的 `CommandAtHotkey`），训练/研究/建造/升级都能用键盘；可用性走 `NativeManaAdmission` / `NativeSpellOrderAdmission` / `NativeSelectionCommandPolicy`。
- **Launcher**：`TrySelectionHotkey` 完全不看命令卡，只认 `ability.Hotkey` 和硬编码的 M/S/H/A/P（`Launcher/GameCore/L4_Composition/Game/Scenes/War3GameScene/QuickCast.cs:411-417`、`:449-457`），遇到建筑直接返回 false；槽位算法是「从请求位往后找第一个空位，找不到就 break」；可用性判定写死在 `QuickCast.cs` 里。
- **表现差异**：同一份物编，两边第 3/8/11 格可能放不同技能；命令卡上的热键字母是装饰，建筑训练/研究不能用键盘；带法力修正符的单位会出现「亮着却放不出」。
- **要做**：先把热键改成读按钮 `Hotkey`，再改槽位算法和可用性判定；冷却闪光、自动施法高亮、禁用变暗在第 30 条。

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

## 核对结果（V1 剩余 18 条）

| V1 # | 状态 | 关键证据 |
|---|---|---|
| 25 | 仍成立 | 音乐无间隔/淡变（`War3MusicPlayer.cs:26`），无循环音服务，16+8 固定播放器（`War3AudioSystem.cs:12-42`） |
| 38 | 仍成立 | 无 `CommandAtHotkey`/`FindNativeSlot`；`QuickCast.cs:411-417` 只认物编热键 |
| 39 | 仍成立 | `SetHint` 只赋值（`MeleeHudController.cs:150`），无 `CommandErrorFrame`；Buff 图标另立第 27 条 |
| 40 | 部分修复 | Esc 已先取消施法/关菜单（`War3InputComponent.cs:41-47`）；放置取消、F10、快捷键、顶栏、选项仍缺 |
| 41 | 仍成立 | 聊天独立 CanvasLayer（`ChatFrame.cs:23-28`），无 `ControlGroupFrame` |
| 42 | 部分修复 | 腐地已用缓存；`Classify` 只认金矿（`MinimapUnitView.cs:43`），Alt 快捷键无调用者，阵型滤镜未接 |
| 43 | 仍成立 | HUD 直接拉伸（`GameHudScreen.cs:515-535`），`UiFontScale` 无 `Set` |
| 44 | 仍成立 | `DrawCooldownCompletion/DrawAutoCast` 返回 false（`GodotUiRenderDevice.cs:155-159`），FDF 绑定只有加载屏在用 |
| 52 | 部分修复 | 天气/丝带已吃雾；`war3_particle.gdshader` 仍无雾 |
| 55 | 仍成立 | `turnRate * 6` 且 1.5–14 夹紧（`UnitFactory.cs:357-358`） |
| 56 | 仍成立 | 每格 `CpuParticles3D` + 跨格 `Restart`（`WeatherRenderer.cs:69-94`、`:199`） |
| 58 | 仍成立 | `SceneFog` 同时管两类雾（`SceneFog.cs:10-41`） |
| 59 | 仍成立 | 独立 `Rendering/UnitTerrainTilt.cs`，客户端是场景 partial（`Presentation.cs:489`） |
| 60 | 仍成立 | 动画轨道固定 `interp = 1`（`ModelBundle.cs:2403`） |
| 61 | 仍成立（修正） | 参数取待机帧快照；`amount` 已用最大发射率但仍夹 8..512（`ModelBundle.cs:1559-1566`、`:1855`） |
| 62 | 仍成立 | 四个事件音键无调用点（`War3UiSoundService.cs:10-41`） |
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

1. **第 21、22、24 条**是「按了没反应/看不到提示」的功能性缺陷，可以提前插队。
2. **第 6、8、9 条与第 17 条**共用环境/雾/天气代码，建议连着做，避免来回改同一批着色器与加载时序。

> **复核评价**：这条串法只对一半。17 是 `war3_particle.gdshader` 引入雾项，和 9 确实同批；6 是地形/悬崖顶点光照接动态光，8 是天气粒子改实例化，各自改的是不同着色器文件，跟 17 没有共用面。把 17 单独和 9 绑在一起，6 和 8 留在模块二按序做，改动面更小。

底层里最容易被玩家一眼看出的是 **第 11 条（转身）**、**第 12 条（Buff 技能）**、**第 13–15 条（物品）**；特效与声音里 **第 18 条（飞弹）** 和 **第 19 条（音乐/脚步）** 最直观。

## 收尾规则

- 一条做完就从本文删掉，序号不复用，后面的不重排；下一条新任务从 **34** 开始。
- 新发现的差异先判断是不是平台差异或已知取舍：Godot 与原生 GL 的差别、Launcher 固定 gmap 路径、不跑 JASS/触发器/战役/存档/录像/联机，这些不写进来。
- 写进来时必须带三个东西：客户端在哪、Launcher 在哪、玩家看得到什么。只有「不一样」没有证据的，等复核清楚再写。

> **复核评价**：「序号不复用」在多副本并行的工作流里已经落空。工作区有 `Launcher` 到 `Launcher5` 六份副本，`RunLauncher.command` 指向 `Launcher/`，而 `Launcher5` 的 git 历史里已经提交了「任务36」「任务37」，本清单最高才到 33、还写着新任务从 34 起。更麻烦的是 `Launcher5` 的「任务21/22/24」内容分别是模型丝带、闪电抖动、应答音效，与本清单第 21/22/24 条（Esc 语义、命令卡、提示语）不是一回事——同一个编号在两处指不同任务，编号就失去了追溯能力。
>
> 建议改成用标题做锚点（本清单的标题本来就够唯一），提交信息里带标题而不是只带序号；清单内部沿用序号只作阅读顺序。
>
> 另外「标题后的 `（V1 #xx）`」和「核对结果」这张 21 行的表属于历史变更记录，与 AGENTS.md 协作约定里的「不记录历史变更」相抵。清单只需要回答「还剩什么」，删完落地条目后这些追溯信息一并清掉更省事。