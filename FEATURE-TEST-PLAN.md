# HiveWE 0.6 功能摸底测试清单

用途：逐项验证 HiveWE 0.6 的功能完整度，记录「有问题 / 不可用」。

---

## 1. 文件与地图生命周期

| # | 功能 | 结果 |
|---|---|---|---|---|---|
| 1.1 | New Map（新建地图）| 未实现
| 1.2 | Open Map (Folder) |
| 1.3 | Open Map (MPQ) |
| 1.4 | Save Map 写回当前目录|
   编辑器内的改动默认只改内存，通过Save Map或快捷键`Ctrl+S`存盘，Export MPQ也会存盘。
| 1.5 | Save Map as 另存为新目录|
| 1.6 | Export MPQ 导出为.w3x|
| 1.7 | Test Map 调 `x86_64\Warcraft III.exe -loadfile`|



File/Game/Test Map | 运行地图 |
File/Game/MapInfo | 打开 MapInfoEditor 界面 |
Flle/Editor/Settings/ 编辑器设置 |
Flle/Editor/Exit/ 退出编辑器 |

---

## 2. 视图与渲染（View 选项卡）

| # | 功能 | 结果 |
|---|---|---|---|---|---|
| 2.1 | Units 显示 |
| 2.2 | Doodads 显示 |
| 2.3 | Pathing 显示 |
| 2.4 | Brush 显示 |
| 2.5 | Lighting 显示 |
| 2.6 | Wireframe 线框 |
| 2.7 | Debug 叠加 | 
| 2.8 | Minimap 小地图 |
| 2.9 | Switch Camera 相机切换FPS第一人称模式| 感觉FPS操作不了
| 2.10 | Reset Camera 重置相机位置|

---

## 3. 地形编辑（Terrain Palette）

| # | 功能 | 风险 | 备注 | 结果 |
|---|---|---|---|---|
| 3.1 | 笔刷形状（方/圆/菱形） | P2 | | |
| 3.2 | 笔刷大小 | P2 | | |
| 3.3 | Deformation：升高/降低 | P1 | 高度被 clamp 在 `[-16, 15.98]`（`TerrainBrush.cpp:200`） | |
| 3.4 | Deformation：平滑/压平 | P1 | | |
| 3.5 | 纹理（Texture）涂抹 | P1 | | |
| 3.6 | Cliff 悬崖编辑 | P1 | 悬崖拼接是最容易出错的部分 | |
| 3.7 | 水面高度与边界 | P1 | `enforce_water_height_limits` 默认 true | |
| 3.8 | 撤销 / 重做 | P1 | **只覆盖地形**，快捷键 Z / Y | |
| 3.9 | 撤销分组粒度 | P2 | `new_undo_group()` 按笔画分组，检查是否一次撤销过多/过少 | |

---

## 4. 装饰物（Doodad Palette）

| # | 功能 | 风险 | 备注 | 结果 |
|---|---|---|---|---|
| 4.1 | 列表加载与搜索 | P1 | 依赖 SLK 数据解析 | |
| 4.2 | 放置装饰物 | P1 | 写入 `.doo` | |
| 4.3 | 删除/擦除 | P1 | | |
| 4.4 | 笔刷形状与大小 | P2 | | |
| 4.5 | 选择与属性编辑（朝向/缩放/生命） | P1 | 界面上没有属性面板，需确认是否只能放置 | |
| 4.6 | 装饰物的掉落物表 | P1 | 生成脚本时会引用 `UnitItemDrops_*` | |

---

## 5. 寻路（Pathing Palette）

| # | 功能 | 风险 | 备注 | 结果 |
|---|---|---|---|---|
| 5.1 | 添加 / 删除 / 替换 寻路 | P1 | 直接改 `war3map.wpm` | |
| 5.2 | 笔刷大小 | P2 | | |
| 5.3 | 「应用不可建造」选项 | P2 | | |
| 5.4 | 寻路与地形/装饰物的一致性 | P1 | 导出后进游戏看是否真的不可通行 | |

---

## 6. Tools 选项卡

| # | 功能 | 风险 | 备注 | 结果 |
|---|---|---|---|---|
| 6.1 | Import Heightmap | P1 | 读取图片写高度 | |
| 6.2 | Change Tileset | P0 | 换 tileset 会重写地形纹理索引，最容易整图损坏 | |
| 6.3 | Change Tile Pathing | P1 | `TilePather`，含「追溯应用」选项 | |
| 6.4 | Change Game Folder | P1 | 切换魔兽安装目录，影响 CASC/MPQ 读取 | |
| 6.5 | Map Size / Camera Bounds | — | **缺失**：Ribbon 里相关按钮被注释掉，README 也把它列为「可贡献功能」 | |

---

## 7. 编辑器窗口（Window 选项卡）

| # | 功能 | 风险 | 备注 | 结果 |
|---|---|---|---|---|
| 7.1 | Import Manager：导入文件 | P1 | 写入 `war3map.imp` + 实际文件 | |
| 7.2 | Import Manager：Export All | P1 | | |
| 7.3 | Import Manager：新建文件夹 / 资源管理器打开 | P2 | | |
| 7.4 | Import Manager：编辑条目（自定义路径） | P1 | `ImportManagerEdit` | |
| 7.5 | Trigger Editor：新建 GUI 触发器 | P0 | **已知缺陷 1**，生成器有系统性残缺 | |
| 7.6 | Trigger Editor：新建 JASS 触发器 / 注释 | P1 | 纯文本，风险低 | |
| 7.7 | Trigger Editor：JASS 编辑器（高亮/补全/括号匹配） | P2 | QScintilla | |
| 7.8 | Trigger Editor：GenerateScript（编译检查） | P0 | 依赖 `Data/Tools/clijasshelper.exe`，会弹编译错误框 | |
| 7.9 | Trigger Editor：变量编辑器 | P1 | `VariableEditor` | |
| 7.10 | Trigger Editor：搜索窗口 | P2 | `SearchWindow` | |
| 7.11 | Trigger Editor：触发器树拖拽排序 | P1 | `TriggerExplorer` | |
| 7.12 | 区域（Regions）编辑 | — | **缺失**：只有渲染，无编辑 UI | |
| 7.13 | 相机（Cameras）编辑 | — | **缺失**：只有运行时相机，无编辑 UI | |
| 7.14 | 声音（Sounds）编辑 | — | **缺失**：只有环境音播放 | |
| 7.15 | 单位（Units）放置 | — | **缺失**：只有 `Units.cpp` 渲染，Ribbon 无单位面板 | |
| 7.16 | 复制 / 粘贴 / 剪切 | — | **已随 Home 页签移除**：原先有 Copy/Paste 按钮但 `HiveWE.cpp` 没有任何连接，剪切按钮连 `addWidget` 都被注释掉了，整理页签时一并删除 | |

---


# 浮窗

## MapInfoEditor
1. 多语言设计
    1.31支持多语言，方式是：
        客户端先找 _Locales\<自己语言>.w3mod\war3map.wts，找不到才回退到根表。
    本工具暂时不支持多语言，方案是导出.w3x文件时忽略_Locales目录。

2. Description 页
    Name | 地图名
    Suggested Players | 建议人数
    Description | 地图简介
    Author | 作者署名，客户端侧未显示。

    Map Version | 官方WE每次保持递增的版本号，本工具不使用。
    Editor Version | 官方WE的内部build号，本工具不使用。

    地图对应的客户端版本号（未显示）| 写入28，对应客户端版本1.31。
    
3. Loading Screen 页

    Loading Screen Graphic
        Use Default Screen | 用当前 tileset 的默认载入图
        Use Campaign Screen | 用战役里内置的载入图
        Use Imported File | 用导入文件 不可用

    Loading Screen Title | 载入画面标题
    Loading Screen Subtitle | 载入画面副标题
    Loading Screen Text | 载入画面正文

### 8.3 Options 页

开关类（写 w3i 头部 flags 位，`Base/MapInfo.cpp:204-220`）：

| # | 选项 | 中文含义 | flag | 注意 |
|---|---|---|---|---|
| 8.3.1 | Melee Map | 对战地图标记？。影响 `Game Data Set` 的默认取值，客户端按对战规则处理玩家/队伍与比赛设置 | `0x0004` | 勾上后官方编辑器会接管玩家/队伍页，本工具没有对应 UI，只能改标记本身 |

Hide minimap in preview screens | 在预览/房间界面不显示小地图缩略图


| 8.3.3 | Masked areas are partially visible | 未探索（黑幕遮罩）区域半透明可见，能看出地形轮廓，而不是纯黑 | `0x0010` | 客户端 fog 行为，进游戏按 `M`（小地图）/移镜头对比 |
| 8.3.4 | Show water waves on cliff shores | 悬崖岸边显示水波 | `0x0800` | 需要图里有悬崖贴水的地形才看得到 |
| 8.3.5 | Show water waves on rolling shores | 缓坡（非悬崖）岸边显示水波 | `0x1000` | 同上，两者互相独立 |
| 8.3.6 | Use item classification system | 启用物品分类系统（1.30+ 的物品类别：永久/充能/神器…），影响商店分类与物品提示 | `0x8000` | 老图默认关闭；打开后物品栏分类行为会变，属行为变更，测前备份 |

雾（GroupBox，可勾选，勾选状态本身不落盘）：

| # | 控件 | 中文含义 | 字段 | 注意 |
|---|---|---|---|---|
| 8.3.7 | Use Terrain Fog（组框标题） | 「启用地形雾」。**这个勾选框不参与保存** | — | `setChecked(fog_style != 0)`（`:59`）但 `save()` 无条件写 `fogStyle` 索引（`:154`）→ Linear(0) 的图显示为"未勾选"；取消勾选也关不掉雾（w3i 的 `fog_style` 只有 3 种雾型，没有"关闭"值，要关只能把 Density 设 0） |
| 8.3.8 | Style | 雾的衰减方式：`Linear` 线性、`Exponential 1` 指数雾 1、`Exponential 2` 指数雾 2（指数型随距离衰减更快、远处更快糊成一片） | `fog_style` 0/1/2 | 索引直接写回，0 与"未启用"在数据上无法区分 |
| 8.3.9 | Z Start | 雾开始出现的**高度**（世界 Z 轴，不是与相机的距离） | `fog_start_z_height` | 单位≈地形高度；相机贴地时调这个最直观 |
| 8.3.10 | Z End | 雾完全遮蔽的高度 | `fog_end_z_height` | 注意是"高度区间"，不是距离区间，跟大多数引擎的 fog near/far 语义不同 |
| 8.3.11 | Density | 雾浓度 | `fog_density` | 指数雾下作用更明显；Linear 雾下几乎只受 Z Start/End 控制 |
| 8.3.12 | Color | 雾颜色（色板按钮）；w3i 里是 4 字节 RGBA，UI 只给 RGB | `fog_color` | alpha 由 `ColorButton::get_glm_color()` 决定，客户端基本不用它 |

环境类：

| # | 控件 | 中文含义 | 字段 | 注意 |
|---|---|---|---|---|
| 8.3.13 | Use Global Weather + 下拉 | 全局天气（雨、雪等）。列表来自 `TerrainArt/Weather.slk`，数据是 4 字符 `effectID`；`0` = 无天气 | `weather_id` | 勾选时取下拉的 `currentData()`，未勾选写 0；取消勾选再勾选会丢原来的天气 |
| 8.3.14 | Custom Sound Environment + 下拉 | 自定义环境音（如 Dungeon/Forest 那套背景声）。列表来自 `UI/SoundInfo/EnvironmentSounds.slk` 的 `EnvironmentType` | `custom_sound_environment`（字符串，空=默认） | 要戴耳机在游戏里听；改了不一定立刻听得出，建议换极端值（如 Water/Underwater）对比 |
| 8.3.15 | Custom Light Environment + 下拉 | 自定义光照环境：借用**别的 tileset** 的光照参数（列表来自 `WorldEditData.txt` 的 `TileSets` 段，存的是 tileset 首字母） | `custom_light_tileset`（1 字节） | 这是"用 A 地形的光打 B 地形"，不改地形纹理，风险低 |
| 8.3.16 | Use Water Tinting Color + 色板 | 全局水面染色 | `water_tinting`（`0x10000`）+ `water_color` | 关掉复选框 `water_color` 仍会被写入，只是 flag 不生效 |
| 8.3.17 | Game Data Set | 用哪套平衡数据：`Default (Map melee status)`=按 Melee Map 标记决定 / `Custom (TFT 1.02, RoC 1.01)`=固定老版数据 / `Melee (Latest Patch)`=最新对战平衡 | `game_data_set` 0/1/2 | **最容易被忽略但影响最大**：同一张图换这里，单位数值会整片变化 |

- 页面顺序：六个开关 → 雾组框 → 天气/音效/光照/水面/Game Data Set。
- 本页所有控件都写 w3i，保存即生效；不生效只可能是没保存或客户端读旧包。

### 8.4 Preferences 页

- 实测只有一个居中的 `?` 占位 QLabel（`MapInfoEditor.ui:476-493`），**没有任何输入控件**，属未实现。
- 对应官方编辑器的过场/玩家与队伍一类设置，HiveWE 把这些数据读进了结构体但不给 UI（见 8.5）。

| # | 功能 | 结果 |
|---|---|---|
| 8.4.1 | Preferences 页存在但为空（无控件、无保存逻辑） | 未实现 |

### 8.5 有数据但没开放 UI 的 w3i 字段

这些字段**读入后原样写回**，用本工具保存不会丢；只是改不了。

| 字段 | 含义 |
|---|---|
| `prologue_screen_model` / `prologue_text` / `prologue_title` / `prologue_subtitle` | 过场（Prologue）画面与字幕 |
| `fixed_player_settings`（`0x0020`） | 固定玩家设置（进游戏不能改玩家槽位） |
| `custom_forces`（`0x0040`） | 自定义队伍（Forces） |
| `custom_techtree`（`0x0080`）/ `custom_abilities`（`0x0100`）/ `custom_upgrades`（`0x0200`） | 自定义科技树 / 技能 / 升级 |
| `modif_ally_priorities`（`0x0002`） | 允许修改结盟优先级 |
| `unknown` `0x0008`、`unknown2` `0x0400`、`unknown3` `0x2000`、`unknown4` `0x4000` | 用途未知的位，读写都保留 |
| `players[]` / `forces[]` | 玩家（名称/种族/类型/起始点）与队伍（结盟、共享视野、共享控制） |
| `available_upgrades` / `available_tech` | 科技可用性 |
| `random_unit_tables` / `random_item_tables` | 随机单位表 / 随机物品表 |
| `camera_left_bottom` … `camera_complements`、`playable_width/height` | 相机边界与可玩区域（README 把 Map Size / Camera Bounds 列为缺失功能，见 6.5） |

### 8.6 本页疑似缺陷汇总（待确认，均未修改）

| # | 现象 | 依据 |
|---|---|---|
| A | 战役载入图下拉每项重复两次；从后半段选中会写出越界索引 | `MapInfoEditor.cpp:20-25` 与 `31-36` 重复 |
| B | 雾的勾选框只是装饰：Linear 雾显示"未勾选"，取消勾选也关不掉雾 | `:59` vs `:154` |
| C | w3i 存字面量时文本改动被静默丢弃，只在 stdout 打印 `Invalid TRIGSTR set` | `TriggerStrings.cpp:101-113` |
| D | 新建字符串可能撞号：`next_id` 取的是"行内空格偏移"而不是序号（`STRING 003` → 7），`Test Map` 已用到 `TRIGSTR_046` | `TriggerStrings.cpp:44-50` |
| E | "去前导零"循环判断 `final_key.front() == '0'`，首字符是 `S`，永远不执行，写出的仍是 `STRING 001`（与 `_Locales` 里 `STRING 0` 风格不一致） | `TriggerStrings.cpp:66-69` |
| F | 导入载入图下拉恒为空（`Imports::load()` 无调用点，`war3map.imp` 从不加载）；选中该项会把 `loading_screen_model` 写成空串 | `MapInfoEditor.cpp:27-29, 131`；`Base/Map.cpp` 无 `imports.load` |
| G | 打开 RoC/TFT（v18/v25）老图时 `game_version_*` 与 `lua` 不会被赋值，保存按 v28 写出这 5 个字段 → 可能写出垃圾值（1.31 图是 v28，不受影响） | `MapInfo.cpp:19-24, 87-89, 182-188, 249` |

---

## 9. 设置（Settings）

| # | 功能 | 风险 | 备注 | 结果 |
|---|---|---|---|---|
| 9.1 | 主题（Dark/Light） | P2 | 读 `Data/Themes/<name>.qss` | |
| 9.2 | 主题热重载 | P2 | F5 | |
| 9.3 | Testing 页（测试地图参数） | P2 | 传给 `-loadfile` 的附加参数 | |
| 9.4 | 设置持久化 | P2 | QSettings（注册表 `HKCU`） | |

---

## 10. 快捷键一览

| 快捷键 | 功能 | 代码 |
|---|---|---|
| `Ctrl+O` | 打开地图（文件夹） | `HiveWE.cpp:121` |
| `Ctrl+I` | 打开地图（MPQ） | `:122` |
| `Ctrl+S` | 保存 | `:123` |
| `Ctrl+Shift+S` | 另存为 | `:124` |
| `Ctrl+Z` / `Ctrl+Y` | 撤销 / 重做（仅地形） | `:83-84` |
| `U` / `D` / `P` / `L` / `T` | 单位 / 装饰物 / 寻路 / 光照 / 线框 显示开关 | `:95-99` |
| `F1` | 切换相机模式 | `:116` |
| `Ctrl+Shift+C` | 重置相机 | `:117` |
| `F3` | Debug 叠加 | `:100` |
| `F5` | 重载主题 | `:103` |

注意：`F3` 没有帮助文档入口，`F1` 也不是帮助而是切相机——和常见习惯相反，容易误按。

---

## 11. 已知缺陷（本次排查已确认）

| # | 缺陷 | 影响 | 状态 |
|---|---|---|---|
| 1 | **脚本生成器系统性残缺**：`CreateDestructables`/`CreateItems` 生成为空函数、`.doo` 字段读取错位（生成出 `SetHeroAgi(u, 1690254504)`）、`SetTeams` 与官方编辑器不一致 | 任何走「重新生成脚本」的导出，地图进游戏立即判负 | 已改为「触发器没改过就保留原始 `war3map.j`」（`f5e09aa`）；**改过触发器后的导出仍未验证** |
| 2 | `.doo` 解析字段错位 | 单位属性、掉落表可能被读成垃圾值 | 未修，只做了规避 |
| 3 | 物理碰撞线框默认渲染，画面一层绿线 | 视觉干扰 | 已挂到 Debug 开关，默认关闭 |
| 4 | Export MPQ 未检查 `SFileCreateArchive` 返回值，句柄未初始化 | 导出直接闪退 | 已修（`efecc98`） |
| 5 | 1.31 经典客户端 `.build.info` 在安装根目录 | 打开游戏数据失败退出 | 已加回退（`f5e09aa` 之前的提交） |
| 6 | 官方 0.6 用 Qt 5.12.3，本仓库用 5.15.2 | 控件样式可能有细微差异 | 已知差异，不修 |

---

## 12. 建议测试顺序

1. **闭环**（第 0 节）——如果 0.4 不通过，后面的模块测试都没有意义
2. **地形**（第 3 节）——最容易验证，且是所有编辑的基础
3. **装饰物 / 寻路**（4、5 节）——都写二进制，出错会体现在导出物上
4. **地图信息 / 导入管理器**（8、7.1-7.4 节）——同样是写二进制
5. **触发器**（7.5-7.11 节）——**放最后**，因为生成器已知不可靠，先用「只读」方式验证能不能正确加载/显示，再决定要不要动
6. **工具类**（6 节）——`Change Tileset` 会整图重写，务必在副本上做

每测完一组，把异常记到「结果」列，附上具体的输入与现象。二进制格式类问题往往表现为「导出后进游戏某处异常」，而不是编辑器里报错，所以**每组测完都要导出一份进客户端验证**。

---

# 附录 A：地图目录里每个文件是什么（样本 `Data/Maps/(2)BootyBay`）

样本概况：地形 193×97 格、格式版本 28（1.31）、tileset `L`（Lordaeron 夏）、6 种地面纹理 + 2 种悬崖纹理；顶层 14 个文件 + `_Locales` 下 9 份语言表。

## A.1 逐个文件

| 文件 | 大小 | 是什么 | 本工具读写 | 注意 |
|---|---|---|---|---|
| `war3map.w3e` | 131,116 | **地形本体**：地图尺寸（193×97）、tileset、每格地面高度、水面高度与边界、地面/悬崖纹理索引、层高 | **读写**（`Terrain.cpp:329` / `Map.cpp:108`） | 每格 7 字节；改地形必写它，写坏=整图废 |
| `war3map.w3i` | 319 | **地图信息**：名称/作者/描述/建议人数（TRIGSTR 键）、相机边界、可玩区域、flags 位、载入图、Game Data Set、雾、天气、玩家/队伍/科技 | **读写**（`MapInfo.cpp:318` / `Map.cpp:105`） | 第 8 节整节都作用于它 |
| `war3map.wpm` | 294,928 | **寻路图**：格数 192×96、每格 4 字节的通行/建造位（不可走、不可建、空中/地面/两栖等） | **读写**（`PathingMap.cpp:59` / `Map.cpp:117`） | 编辑器会按装饰物再叠加一次 pathing（`Map.cpp:141-146`） |
| `war3map.doo` | 100,574 | **装饰物/可破坏物实例**：模型、坐标、朝向、缩放、生命、掉落表、可见性 | **读写**（`Doodads.cpp:123` / `Map.cpp:124`） | 已知字段错位缺陷（第 11 节 #2） |
| `war3mapUnits.doo` | 6,935 | **单位/物品实例**：类型、所属玩家、坐标、朝向、生命/魔法、掉落、自定义属性 | **读写**（`Units.cpp:197` / `Map.cpp:159`） | 同上；本工具没有单位放置 UI，只能读进来渲染 |
| `war3map.wtg` | 597 | **GUI 触发器树**：触发器/事件/条件/动作，用变量 ID 与分类 ID 编码，不含可读代码 | **读写**（`Triggers.cpp:513` / `Map.cpp:89`） | 也是"地图是否加密"的判据：缺它即视为 protected（`Map.cpp:101`） |
| `war3map.wct` | 17 | **触发器自定义文本**：每个触发器附带的 JASS 片段/注释 | **读写**（`Triggers.cpp:558` / `Map.cpp:94`） | 本样本几乎为空 |
| `war3map.j` | 30,538 | **编译后的 JASS 脚本**，客户端真正执行的就是它 | **只读 + 条件写回**（`Triggers.cpp:125` / `:1384`） | 触发器没改过就原样保留（`f5e09aa`）；改过会重新生成 → 第 11 节 #1 的风险区 |
| `war3map.wts` | 390 | **触发字符串表**：`STRING nnn { ... }`，w3i 与触发器里只存 `TRIGSTR_nnn` 键 | **读写**（`TriggerStrings.cpp:88` / `Map.cpp:83`） | 地图名/描述/载入文本都在这里，见 8.0 |
| `war3map.imp` | 338 | **导入文件清单**：版本 1 + 10 条记录，每条 = 1 字节（是否自定义路径）+ 路径；本样本 10 条全是 `_Locales/*/war3map.wts` | **不读不写**（`Imports::load()` 无调用点，`Map::save()` 也不调 `Imports::save()`） | 见 A.3：`_Locales` 被导出丢弃后，清单里还剩 10 条指向不存在文件的记录 |
| `war3map.mmp` | 168 | **小地图图标**：版本 0 + 10 个图标（商店、金矿、传送门等的位置与类型） | **不读不写**（全工程 0 引用） | 图标数据不会被本工具改动，也不会被用来渲染 |
| `war3map.shd` | 294,912 | **静态阴影图**：逐格记录是否被静态阴影覆盖，WE 保存时重算 | **不读不写**（`ShadowMap` 类只有 `load`/`save`，**没有任何调用点**，是死代码） | 改地形后阴影不会跟着更新 |
| `war3mapMap.blp` | 15,083 | **小地图缩略图**（BLP 图片），房间预览与大/小地图用它 | **不读不写**（全工程 0 引用；编辑器的小地图是运行时用 w3e 现画的） | 改地形后缩略图仍是旧的 → 和 8.3.2「Hide minimap in preview screens」一起测容易误判 |
| `war3map.w3r` | 8 | **区域（Regions）**：版本 5 + 数量 0，本样本为空 | **只读**（`Map.cpp:169`，`Regions` 无编辑 UI，见 7.12） | 空文件也要保留，删了客户端不一定报错但 WE 会当新图处理 |
| `war3map.w3c` | 8 | **相机（Cameras）**：版本 0 + 数量 0，本样本为空 | **只读**（`Map.cpp:175`，`GameCameras` 无编辑 UI，见 7.13） | 同上 |
| `_Locales/<lang>.w3mod/war3map.wts` ×9 | 374–469 | **分语言的字符串表**：deDE/esES/frFR/itIT/koKR/plPL/ruRU/zhCN/zhTW | **不读**；导出时整个 `_Locales` 目录被丢弃（`HiveWE.cpp:360-367`） | 见 8.0.1；`Save Map` 会原样保留它们 |

## A.2 文件时间能说明什么

本目录的时间戳正好对应"谁被重写过"：

| 时间 | 文件 | 对应步骤 |
|---|---|---|
| 14:07 | `.imp` `.mmp` `.shd` `.w3c` `.w3r` `war3mapMap.blp` | 目录副本带来的原始时间，**保存流程从不写它们** |
| 14:25 | `war3map.doo` `.wtg` `.wct` `.j` | `Doodads::save` / `Triggers::save` / `save_jass` |
| 14:30 | `.w3e` `.w3i` `.wpm` `.wts` `war3mapUnits.doo` | `Map::save()` 的 terrain / pathing_map / info / trigger_strings / units 步骤（`Map.cpp:221-227`） |

**注意**：`Map::save()` 每次都会无条件重写上面那 5 个文件，所以时间戳只能证明"保存跑过"，不能说明"内容被改过"——只改地图名，`war3map.w3e` 也会被重写一遍。反过来，`.imp`/`.shd`/`Map.blp` 的时间永远不变，这正好是判断"某个现象是编辑器没更新，还是我改错了"的快速手段。

## A.3 不被本工具碰的文件带来的四个具体问题

1. **小地图缩略图不会更新**（`war3mapMap.blp`）：改完地形导出，房间预览/小地图还是旧图。要更新只能用官方 WE 重存一次。
2. **静态阴影不会更新**（`war3map.shd`）：地形抬高/降低后，阴影贴图仍是旧地形算出来的，可能出现"阴影浮在坡上"。
3. **导入清单与导出内容不一致**（`war3map.imp`）：本样本 10 条记录全是 `_Locales/*/war3map.wts`（其中 `enUS` 那份目录里并不存在，实际只有 9 份）；导出时 `_Locales` 被整个丢弃，但 `.imp` 不重写 → 包内"清单说有、文件没有"。客户端会不会因此报缺文件/警告，需要在 1.31 上实测一条。
4. 另外，`Import Manager` 现在只是一个 `QFileSystemModel` 文件浏览器（`ImportManager.cpp:41-46`），导入/导出/新建目录的按钮连接全被注释掉，唯一可用的是「打开资源管理器」；即使往目录里拷了文件，也不会写进 `.imp`。

## A.4 本目录没有、但格式里存在的文件

| 文件 | 是什么 | 本工具 |
|---|---|---|
| `war3map.w3u` / `w3t` | 单位 / 物品的自定义数据（改过的对象） | 只读（`Map.cpp:149, 154`），不写 |
| `war3map.w3b` / `w3d` | 可破坏物 / 装饰物的自定义数据 | 只读（`Map.cpp:127, 132`），不写 |
| `war3map.w3s` | 声音（环境音、单位语音的引用） | 只读（`Map.cpp:181`），无编辑 UI（见 7.14） |
| `war3map.w3a` / `w3q` / `w3h` / `w3o` | 技能 / 升级 / buff / 科技 | 全工程 **0 引用**，原样保留 |
| `war3map.lua` | 1.31+ 的 Lua 脚本地图（替代 `war3map.j`） | 不读不写；w3i 里的 lua 标志位见 8.6-G |
| `war3map.w3h` 之外的 `.w3*` 变体 | 视 WE 版本而定 | 未知的一律原样保留，不做校验 |

## A.5 一句话总结

本工具真正"拥有写权限"的只有 9 个文件：`w3e` / `wpm` / `doo` / `Units.doo` / `w3i` / `wts` / `wtg` / `wct` / `j`；其余全部原样透传。所以"导出后进游戏某处不对"时，先按这张表判断该怀疑**被重写的那 9 个**，还是**被原样带着走的那些**（比如缩略图、阴影、导入清单）。
