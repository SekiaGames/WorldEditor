# 提高 Launcher 与客户端侧一致化的任务列表

> 排序原则：**系统越底层，优先级越高**。
> L0 基座 → L1 适配 → L2 核心 → L3 模拟 → L4 表现（配置 → 环境 → 相机 → 渲染/表现 → 场景 → UI）。
> 同层内：数据/规则 > 运行时行为 > 画面 > 工具。
> 平台差异（Godot 渲染、无 JASS/Android、gmap 由 Extract 生成）单列，不计入一致率。

---

## 0. 一致率基线

| 层 | 客户端 | Launcher | 现状 |
|---|---:|---:|---|
| L0 基座 | 86 文件 / 7,409 行 | 2 文件 / 83 行 | 大部分是 GL RHI/Input/ThirdParty，属平台差异 |
| L1 适配 | 90 文件 / 16,524 行 | 5 文件 / 894 行 | Jass/Trigger/SaveGame/OpenGL 未搬 |
| L2 核心 | 36 文件 / 4,040 行 | 31 文件 / 3,046 行 | `L2_Core/UI` 2,967 行逐字节一致；差 MapBrowser |
| L3 模拟 | 376 文件 / 69,655 行 | 154 文件 / 12,236 行（另有 `Src/` 约 1.7 万行） | 差距最大，JASS 占 39,272 行 |
| L4 表现 | 456 文件 / 126,143 行 | 100 文件 / 24,629 行 | 渲染平台差异大；UI 差 67 个文件 |

已完成、不再立任务：

- `L2_Core/UI` 与客户端逐字节一致。
- 加载阶段顺序与数值、加载期间音频/模拟冻结。
- gmap 生成迁到 Extract：Launcher 已删除 `GmapImporter/MapDocument/--import-map` 与 MPQ 读写。
- 小地图底图改读 gmap 的 `war3mapMap.png`；小地图/英雄栏几何与输入接口已对齐。
- `TopBarFrame`、`ConsoleSkinFrame` 与客户端逐行同源。

---

## 1. L0 基座（最先核对，避免把平台差异当任务）

| ID | 任务 | 客户端参照 | Launcher 落点 | 验收 |
|---|---|---|---|---|
| L0-1 | 平台差异白名单：RHI/Input/ThirdParty 逐项确认“不追” | `L0_Foundation/RHI`、`Input`、`ThirdParty/War3Net` | `Launcher/GameCore/L0_Foundation/RHI`（仅 ITexture/Enums） | 产出差异清单；清单外不得再出现 GL/Input 依赖 |
| L0-2 | 确定性数值基座核对：Vec2、随机、软浮点、数学垫片 | `Src/Core/Vec2.cs` 等 | `Launcher/Src/Core` 同名文件 | 固定种子/固定 tick 序列输出一致 |
| L0-3 | 时间/暂停语义统一：20Hz、单帧上限、插值 alpha | `World.TickSeconds` 及场景循环 | `Src/World/World.cs`、`War3GameScene` | 同一场景 1000 tick 状态快照一致 |

L0 判定：客户端 GL RHI/Input 由 Godot 替代，属架构差异；除此之外基座文件不得有行为差异。

---

## 2. L1 适配层

| ID | 任务 | 客户端参照 | Launcher 落点 | 验收 |
|---|---|---|---|---|
| L1-1 | 文本/WTS 读写边界对齐 | `L1_Adapter/Formats/Text` | `War3TextEncoding`（已同源）、`WtsDocument`、`War3Strings` | 同一 wts/w3i 名称解析结果一致 |
| L1-2 | SLK/物编读取口径固定：运行期只读生成 C# | `L1_Adapter/Formats` 与物编适配 | `SlkTable` + Extract 生成表 | 同 id 同字段值一致；不得回退 json/解包目录 |
| L1-3 | 地图数据格式对齐：w3e/w3i/doo/wpm | `L3_Simulation/Simulation/Maps` | `Launcher/GameCore/L3_Simulation/Simulation/Maps` | 同一 gmap 解析出的地形/单位/寻路一致 |
| L1-4 | 存档格式：先解析后接 UI | `L1_Adapter/Formats/SaveGame`（29 文件） | Launcher 暂无 | 能读出客户端存档中的玩家/单位/物品核心字段 |
| L1-5 | JASS/Trigger 暂缓登记 | `L1_Adapter/Formats/Jass`、`Trigger` | Launcher 无 | 明确“对战地图不含 JASS”范围；超出即立项 |

---

## 3. L2 核心

| ID | 任务 | 客户端参照 | Launcher 落点 | 验收 |
|---|---|---|---|---|
| L2-1 | `L2_Core/UI` 维持逐字节一致，禁止本地改 | `L2_Core/UI` | 同路径 | 全量 diff 为空 |
| L2-2 | 输入路由回归：控制台整层吞输入、空白区放行 | `MeleeHudController.HandlePointer*` | `GameHudScreen.HandlePointer*` | 点上/下控制台不落到世界；命令卡命中消费 |
| L2-3 | MapBrowser 收口：删除 w3x 浏览器职责 | `MapBrowser` 6 文件 | `L2_Core/MapBrowser/MapBrowser.cs`（79 行原创） | 只列 gmap；w3x 浏览归 Extract |

---

## 4. L3 模拟（工作量最大，优先于所有画面）

| ID | 任务 | 客户端参照 | Launcher 落点 | 验收 |
|---|---|---|---|---|
| L3-1 | World tick/时间/暂停/插值 | `L3_Simulation` World | `Src/World/World.cs` | 1000 tick 快照 diff 为空 |
| L3-2 | 单位移动/路径/碰撞/阵型 | `World.GroupMovement`、`Pathing` | `Src/World`、`Src/Core/Pathing` | 同起点同指令，逐 tick 位置误差 ≤ 1 |
| L3-3 | 战斗公式：攻击/护甲/减伤/弹道/暴击/法球 | `World.WeaponSystem`、`Damage*` | `Src/World/World.WeaponSystem.cs` | 同一组攻防参数伤害逐次一致 |
| L3-4 | Buff/Aura/状态与驱散 | `UnitBuffs`、`Aura*` | `Src/Abilities/UnitBuffs.cs`、`Src/Skills` | 同 buff 序列剩余时间/层数一致 |
| L3-5 | 生产/建造/升级/研究/人口/维修费 | `Production`、物编规则 | `War3GameScene.Production.cs` | 同一经济序列金木人口逐 tick 一致 |
| L3-6 | 物品/库存/商店/掉落 | `World.Items`、`Item*` | `Src/Items`、`Src/World/World.Items.cs` | 同掉落种子同结果 |
| L3-7 | 技能全量对照 | `L3_Simulation/Configuration/Runtime*Ability` | `Src/Skills`（117 文件） | 先产出缺失清单，再按技能分批对齐 |
| L3-8 | AI 归属与口径 | `Simulation/AI`（3 文件） | `War3GameScene/Ai*.cs` | 若 AI 属对战范围，迁入 L3 并同名 |
| L3-9 | 地图对象数据（w3u/w3a/w3q/w3t/w3b/w3d/w3h/w3r） | 物编适配 | `Launcher/GameCore/L3_Simulation` | 同 id 同字段值一致 |
| L3-10 | JASS/Trigger 支持 | `Simulation/Jass`（138 文件） | 无 | 单独立项；未立项前只允许对战地图 |

---

## 5. L4 表现层

层内顺序：**配置 → 环境 → 相机 → 渲染/表现 → 场景/加载 → UI**。

### 5.1 配置

| ID | 任务 | 客户端参照 | 验收 |
|---|---|---|---|
| L4-1 | 补 `War3RenderConfig`（321 行） | `L4_Composition/Game/Configuration` | 单位渲染参数同 id 一致 |
| L4-2 | 补 `War3CombatTooltipStrings`（95 行） | 同上 | 战斗提示文案一致 |
| L4-3 | 补 `War3EventObjectData`/`War3ModelEventSpawns`（162 行） | 同上 | 模型事件/挂点一致 |
| L4-4 | `War3MiscData` 补齐（客户端 237 行，当前 114） | 同上 | 迷雾/小地图/选择色字段齐全 |

### 5.2 环境

| ID | 任务 | 客户端参照 | 验收 |
|---|---|---|---|
| L4-5 | 地形渲染状态/表面/阴影 | `TerrainRenderState/TerrainSurface/TerrainShadowMap` | 同机位地形明暗/阴影一致 |
| L4-6 | 地形配置与贴图集 | `War3TerrainConfig`（367 行） | 同 tileset 贴图选择一致 |
| L4-7 | 雾设置/风格、场景光照、动态光 | `FogSettings/FogStyle/SceneLighting/DynamicLight` | 同时间点雾色/光照一致 |
| L4-8 | 天气定义与天气渲染 | `WeatherDefinition/WeatherRenderer`（696 行） | 同天气类型粒子/音效一致 |
| L4-9 | 装饰物渲染与目标高亮 | `DoodadRenderer`（1,130 行）、`DoodadTargetHighlights` | 同地图装饰物数量/位置/高亮一致 |
| L4-10 | 腐地/浪花/树影 | `ScoutedBlight/ShorelineWaveRenderer/TreeShadowBuffer` | 腐地扩张与浪花边缘一致 |

### 5.3 相机

| ID | 任务 | 客户端参照 | 验收 |
|---|---|---|---|
| L4-11 | 相机全套行为：滚动/边缘平移/旋转/缩放插值/边界 | `War3Camera.cs`（1,248 行）、`War3CameraComponent`（244 行） | 同一输入序列相机位姿误差 ≤ 1% |
| L4-12 | 电影相机 | `CinematicModelCamera.cs` | 电影镜头时序一致 |

### 5.4 渲染/表现

| ID | 任务 | 客户端参照 | 验收 |
|---|---|---|---|
| L4-13 | 单位动画状态机/队伍色/血条/选择圈 | `CharacterRenderer*`、`Presentation` | 同帧截图对齐 |
| L4-14 | 附着特效/投射物/闪电/漂浮文字 | `MdxParticle2Renderer`、`War3MissileAnimation` | 同帧特效位置/数量一致 |
| L4-15 | 天空盒/水面/悬崖 | `Sky*`、`WaterRenderer`、`CliffRenderer` | 同机位画面一致 |
| L4-16 | UI 渲染设备能力补齐：ADD 混合、逐字裁剪、冷却完成、自动施法、营地动画 | `UiRenderDevice*` | 对应控件截图对齐 |

### 5.5 场景/加载

| ID | 任务 | 客户端参照 | 验收 |
|---|---|---|---|
| L4-17 | 加载界面改为客户端同形 `War3LoadingScreenRenderer` | `Rendering/War3LoadingScreenRenderer` | 类名/流程一致；截图矩形误差 ≤ 2px |
| L4-18 | 场景初始化顺序与 `Lifecycle` 对照 | `Scenes/War3GameScene/Lifecycle.cs` | 阶段日志逐条一致 |

### 5.6 UI

| ID | 任务 | 客户端参照 | 验收 |
|---|---|---|---|
| L4-19 | U1 启动：`MapSelectionScreen`、`RaceSelectionDialogFrame`、`AiDifficultyDialogFrame`、`MenuScreenHost`；删除 `PreGameDialog` | 同名文件 | 选图→种族→难度→进图；4 人图起两槽 |
| L4-20 | U3 控制器：HUD 收敛到 `MeleeHudController`，`GameHudScreen` 只留 Godot 胶水 | `MeleeHudController`（1,096 行） | 文件/类/方法一一对应 |
| L4-21 | U3 命令卡：数据驱动 + `UiCommand`/`IUiCommandListener` | `CommandCardBuilder*`（721 行）、`Native*Command*` | 造兵/建造/施法/右键取消/长按自动施法可用 |
| L4-22 | U3 单位面板/英雄栏/小地图/提示框 | `UnitInfoPanelFrame`、`HeroPresentation`、`MinimapVisibilityTracker`、`MinimapBlightTexture`、`TooltipFrame` | 截图对齐；迷雾记忆/腐地/提示正确 |
| L4-23 | U3 头像/空闲工人/选择组 | `PortraitFrame*`、`IdlePeonFrame`、`SelectionGroupFrame` | 3D 头像、F8、编队点击可用 |
| L4-24 | U4/U5：队列/物品/编队/聊天/ESC/任务/多人面板/对话框/光标 | 对应 40+ 文件 | 逐项截图与交互回归 |

---

## 6. 工具与验收口径

| ID | 任务 | 说明 |
|---|---|---|
| T-1 | gmap 唯一入口 | 官方地图放 `test/map_w3x`；Extract `--gmap-export` 输出到 `test/map_gmap`；Launcher 只列/加载 `test/map_gmap`，不读 w3x、不生成 gmap |
| T-2 | 差异登记表 | 平台差异、JASS、AI、Legacy HUD、ADD 混合、逐字裁剪必须登记 |
| T-3 | 一致率统计 | 文件对应数、同名文件行差、截图矩形误差（≤ 2px）、tick 快照 diff |
| T-4 | 提交前检查 | 改动是否符合工程定义、是否有客户端原创、文件是否一一对应 |

---

## 7. 优先执行队列（Top 10）

1. L0-1/L0-2：平台差异白名单 + 数值基座核对。
2. L0-3：时间/暂停/tick 语义。
3. L1-1/L1-3：文本与地图数据格式。
4. L3-1/L3-2：World tick、移动、路径、碰撞。
5. L3-3/L3-4：战斗公式与 Buff/Aura。
6. L3-5/L3-6：生产经济与物品。
7. L3-7：技能缺失清单与分批对齐。
8. L4-1～L4-4：表现配置层。
9. L4-5～L4-10：地形/光照/雾/天气/装饰物。
10. L4-11：相机行为。

UI（L4-19～L4-24）排在同层最后；等底层数据和模拟稳定后再推进，避免界面反复返工。