# HiveWE 0.6 功能摸底测试清单

用途：逐项验证 HiveWE 0.6 的功能完整度，记录「有问题 / 不可用」。
没有实现的功能就不能修改就行了，原样读原样保存。

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
| 3.7 | 水面高度与边界 | P1 | `enforce_water_height_limits` 默认 true，语义见 3.10.1 | |
| 3.8 | 撤销 / 重做 | P1 | **只覆盖地形**，快捷键 Z / Y | |
| 3.9 | 撤销分组粒度 | P2 | `new_undo_group()` 按笔画分组，检查是否一次撤销过多/过少 | |

### 3.10 顶部 Ribbon：General / Cliff / Pathing（一排 6 个开关）

入口：`Window → Palette → Terrain`。浮窗激活时 `TerrainPalette::event` 发 `ribbon_tab_requested`，
`HiveWE::set_current_custom_tab`（`HiveWE.cpp:557`）在 Ribbon 里插入一个名为 `Terrain Palette` 的页签。

三个分区都是 `QRibbonSection`，`QRibbonTab::addSection`（`QRibbon.cpp:125`）把它们插进**同一个横向 layout**，
所以这一排从左到右是 2 + 1 + 3 共 6 个按钮，分区之间只有一条竖线分隔：

| 顺序 | 按钮文字 | 分区 | 默认 | 对应 `TerrainBrush` 字段 |
|---|---|---|---|---|
| 1 | Enforce Water Height Limit | General | 开 | `enforce_water_height_limits` |
| 2 | Update Doodad Z | General | 开 | `change_doodad_heights` |
| 3 | Relative Height | Cliff | **灰（点不动）** | `relative_cliff_heights`（无人读） |
| 4 | Cliff Pathing | Pathing | 开 | `apply_cliff_pathing` |
| 5 | Tile Pathing | Pathing | 开 | `apply_tile_pathing` |
| 6 | Water Pathing | Pathing | 开 | `apply_water_pathing` |

代码位置：按钮构造与连接 `TerrainPalette.cpp:66-131`；生效点全在 `TerrainBrush::apply()` / `apply_end()`。

#### 3.10.1 Enforce Water Height Limit —— 强制水面高度上限

- 生效位置：`TerrainBrush.cpp:258-262`，只在**悬崖操作**分支里（Cliff 面板的 Lower 2 / Lower 1 / Level / Raise 1 / Raise 2）。
- 判定：该格本来有水（`corner.water`），且按新层高算出来的地面已经**高过**水面
  （`final_water_height() < final_ground_height()`，前者 = `water_height + water_offset`，后者 = `height + layer_height - 2`）
  → 把 `corner.water` 置 false，这格的水被删掉。
- 关掉的后果：水记录留着、水面沉到地面以下（渲染上看不见水），但该格仍然算水 → 寻路照旧写「水面」相关位（见 3.10.6），
  `war3map.w3e` 的 0x40 位也照旧保存。表现就是「看着是干地，实际走不进去」。
- 它**不**调整水位（`water_height` 不动），也不碰 `water_offset`；浅水/深水两个操作和 Ramp 完全不受它影响。
- 反方向不会自动补水：地面压低到水面以下时，水位仍来自 w3e 里原有的 `water_height`。

#### 3.10.2 Update Doodad Z —— 把可破坏物贴回地面

- 生效位置：`TerrainBrush.cpp:352-366`，前置条件是 `apply_height || apply_cliff`（Deformation 或 Cliff 勾选着）。
- 行为：遍历 `map->doodads.doodads`，凡是 (x, y) 落在笔刷方块内（`area.contains`，单位是格子/128，不是世界坐标）
  → `position.z = terrain.interpolated_height(x, y)`（对 `final_ground_height()` 做双线性插值）+ `update()` 重建矩阵；
  旧值/新值分别记进 `pre_change_doodads` / `post_change_doodads`，`apply_end()` 包成 `DoodadStateAction` 供撤销。
- 关掉的后果：地形抬升后树木石头原地不动 → 悬空；压低后 → 埋进地里。
- **单位不受这个开关控制**：`map->units.update_area(updated_area)`（`TerrainBrush.cpp:365`、`Units.cpp:220`）无条件执行，
  单位始终跟着地面走，这个开关只管可破坏物。
- 只处理笔刷方块内：`check_nearby()`（`TerrainBrush.cpp:15-38`）会把周围层高差 >2 的格子逐级顺平，
  这些修正在 `updated_area` 里但常在 `area` 之外，落在上面的可破坏物不会被重新贴地 → **悬崖边缘容易出现悬空的树**。

#### 3.10.3 Relative Height —— 相对高度（死按钮）

- `setEnabled(false)`（`TerrainPalette.cpp:92`），永远灰；`relative_cliff_heights` 除 `TerrainPalette.cpp:125` 赋值外，
  `TerrainBrush` 里**没有任何读取点** → 纯占位，点了也不会有反应。原意应是「抬降悬崖时按相对高度而非绝对值」。

#### 3.10.4 Cliff Pathing —— 悬崖寻路

- 生效位置：`TerrainBrush.cpp:324-326`（在每笔都会跑的寻路重算里）。
- 行为：该角的 4×4 子格先清 `0x02|0x04|0x08|0x40`；若该角是悬崖（`cliff` 或 `romp`）且不是坡道入口
  （`is_corner_ramp_entrance`）→ 写 `0b1010` = 不可行走(0x02) + 不可建造(0x08)。
- 关掉的后果：悬崖格不再写不可行走，悬崖两侧在 `war3map.wpm` 上连通（进游戏单位会往悬崖里挤）；
  坡道入口本就被排除，关掉后坡道行为不变。

#### 3.10.5 Tile Pathing —— 地表寻路

- 生效位置：`TerrainBrush.cpp:330-333`，`mask |= pathing_options[tileset_ids[ground_texture]].mask()`。
- 每个 tile 的「不可行走 / 不可飞行 / 不可建造」来自 `Tools → Change Tile Pathing`（`TilePather`），
  存在内存 `Terrain::pathing_options`（`Terrain.h:90`）。
- **注意**：这张表全工程没有任何加载/保存代码，只在 `TilePather::save_tiles()` 里写回内存（`TilePather.cpp:82-90`），
  重开地图就全丢；默认全 false → **没先在 Change Tile Pathing 里勾过任何 tile，这个开关等于无效**。
- 笔刷只取 `mask()`，不认 `Operation`（replace / add / remove）→ 在 Change Tile Pathing 里选 remove 也不会让笔刷去清位。

#### 3.10.6 Water Pathing —— 水面寻路

- 生效位置：`TerrainBrush.cpp:335-342`。
- 行为：非悬崖（或坡道）格且有水 → `mask |= 0x40`；再按水深叠加：
  - 水深 > 地面 + 0.40 → 追加 `0b1010`（不可行走 + 不可建造，深水）
  - 水深 ∈ (地面, 地面+0.40] → 追加 `0b1000`（只不可建造，可涉水）
- 关掉的后果：水面格不写 0x40、也不写深水禁行 → 单位可以直接走进深水区。
- 0x40 是写进 `war3map.wpm` 的一个位，HiveWE 自己的寻路叠加显示只画 0x02/0x04/0x08 三通道
  （`Data/Shaders/terrain.fs:98`），所以看不到「水」的颜色；该位在客户端里的实际作用**待验证（P2）**。

#### 3.10.7 三个 pathing 开关的共同坑（重要）

`apply()` 里的寻路重算**不管三个开关怎么设**，都会先把 `updated_area` 内的 `0x4E`（即 0x02|0x04|0x08|0x40）清成 0
（`TerrainBrush.cpp:321`），再按开关把位加回去。于是：

- 开关 = 关，不是「保持原样」，而是「这片区域这些位被擦掉」；
- 只刷纹理（Texture 勾上、Deformation/Cliff 都关）**也会**触发这段重算（`updated_area` = 笔刷方块外扩 1 格）
  → 用 Pathing Palette 手绘的不可行走 / 不可建造会被地形笔刷擦掉。这是最容易踩的坑。

#### 3.10.8 其他已知小问题

- 信号重复连接：`enforce_water_height_limit` 连了两次（`TerrainPalette.cpp:123` / `127`），`cliff_group` 也连了两次（`151` / `164`）；
  lambda 幂等，暂无可见影响，属冗余。
- `apply_end()` 里只要 `change_doodad_heights` 为真就一定会 `add_undo_action`（`TerrainBrush.cpp:383-392`），
  即使一个可破坏物都没动 → 撤销栈里会多出空步。
- 开关状态不落盘：存在 `TerrainPalette` 自己的 `TerrainBrush brush` 成员里，浮窗是 `WA_DeleteOnClose`，
  关掉再打开就回到默认（三个 pathing + 两个 General 全开）。

#### 3.10.9 建议的验证动作

1. 造一格水 → 用 Cliff / Level 抬到水面以上：开关开 → 水消失且该格可走；开关关 → 看不见水但 View→Pathing 下仍是红色禁行。
2. 悬崖边缘放几个树/石头 → 抬悬崖：检查边缘顺平区域的可破坏物是否悬空（预期会，见 3.10.2 最后一条）。
3. 用 Pathing Palette 手绘一片不可行走 → 再用纹理笔刷扫过：检查手绘是否被擦掉（预期会，见 3.10.7）。

---

## 4. 装饰物（Doodad Palette）

| # | 功能 | 风险 | 备注 | 结果 |
|---|---|---|---|---|
| 4.1 | 列表加载与搜索 | P1 | 依赖 SLK 数据解析；搜索框 `search` 在 `.ui` 里就是 `enabled=false`，**搜索不可用** | |
| 4.2 | 放置装饰物 | P1 | 写入 `.doo` | |
| 4.3 | 删除/擦除 | P1 | 走选择模式 + Delete，见 4.7.1 | |
| 4.4 | 笔刷形状与大小 | P2 | **不可用**：`brushGroupBox` 的 `maximumSize.height=0`（整组被压成 0 高），且 `DoodadPalette.cpp` 里没有任何 `brushSize*` 的 connect。占地由模型的 pathing 贴图尺寸决定，见 4.7.6 | |
| 4.5 | 选择与属性编辑（朝向/缩放/生命） | P1 | 界面上没有属性面板，需确认是否只能放置。选择模式本身可用，见 4.7.1 | |
| 4.6 | 装饰物的掉落物表 | P1 | 生成脚本时会引用 `UnitItemDrops_*` | |

### 4.7 顶部 Ribbon：Selection Mode / Placement / Variations / Flags / Pathing

入口：`Window → Palette → Doodads`。浮窗激活时 `DoodadPalette::event` 发
`ribbon_tab_requested(ribbon_tab, "Doodad Palette")`（`DoodadPalette.cpp:168`），Ribbon 里出现同名页签。

5 个 `QRibbonSection` 依次 `addSection`（`DoodadPalette.cpp:133-137`），横向排成一行，顺序即从左到右：

| 顺序 | 分区标题 | 内容 | 默认 |
|---|---|---|---|
| 1 | **（无标题）** | `Selection Mode` 开关按钮 | 关 |
| 2 | Placement | `Random Rotation`、`Random Scale` | 都开 |
| 3 | Variations | `Random Variation` + 动态生成的变体按钮（每行 3 个） | 开，变体全选 |
| 4 | Flags | 三个单选：Invisible non solid / Visible non solid / Visible solid | Visible solid |
| 5 | Pathing | 选中模型自带 pathing 贴图的预览图（没有则整段隐藏） | — |

#### 4.7.1 Selection Mode（选择模式）

- checkable 按钮，快捷键 **Space**（`setShortCut(Qt::Key_Space, { this, parent })`，同时挂在浮窗和主窗口上，
  浮窗激活时启用、切到别的面板时禁用）。
- 点击 → `brush.switch_mode()`（`Brush.cpp:104`），在 `placement` ↔ `selection` 之间翻转。进入选择模式后：
  - 左键拖框 → `map->doodads.query_area` 框选一片（`DoodadBrush.cpp:184-187`）
  - 拖框时按住 Ctrl → 已选对象朝鼠标方向原地旋转（`atan2`，181 行）
  - 小键盘 1-9 → 已选对象按 0.25 格平移（97-112 行）
  - PageUp/PageDown → 改 `scale.z` 0.1；Ctrl+PageUp/PageDown → 改 `position.z` 0.1（114-151 行）
  - Delete / 复制 / 剪切 / 粘贴全部走 Brush 基类的 clipboard（`delete_selection` / `copy_selection` / `place_clipboard`）
- 在列表里点任何一个模型会强制 `selection_mode->setChecked(false)`（`DoodadPalette.cpp:219`）→ 自动退回放置模式。
- **疑似缺陷（状态脱钩）**：`deactivate()` 只 `clear_selection()`、不复位 `mode`（`DoodadPalette.cpp:252-257`），
  所以「选择模式 → 切到 Terrain → 切回来」按钮显示未按下、实际仍在 selection 模式。
  其它改 `mode` 的路径同样不回写按钮：`Ctrl+V` 会临时切到 `pasting`（`Brush.cpp:135-138`），Esc 只清选择不改模式（117-119 行）。

#### 4.7.2 Placement（放置）

- `Random Rotation`（`random_rotation`）：每次放置后把 `rotation` 随机到 `[0, 2π)`（`DoodadBrush.cpp:300-303`）。
  **只在 `free_rotation` 为真时生效**：`free_rotation` = 「有 pathing 贴图且贴图是正方形」或「没有 pathing 贴图」
  且 slk 的 `fixedRot < 0`（431-437 行）。所以带非方形 pathing 贴图的树/岩石，这个开关等于无效。
- `Random Scale`（`random_scale`）：每次放置后在 slk 的 `minScale`~`maxScale` 之间随机（309-312 行）。
- 关掉 Random Rotation 时，`rotation` 取 slk 的 `fixedRot`（-1 视为 270°，419-423 行）。

#### 4.7.3 Variations（变体）

- `Random Variation`（`random_variation`，默认开）：每次放置从「勾选着的变体集合」里随机取一个
  （`set_random_variation` → `get_random_variation`）。
- 右边那组数字按钮是**动态生成**的：选中模型时按 slk 的 `numVar` 重建（`selection_changed`，224-240 行），
  每行 3 个，默认全勾；取消勾选 = 从 `possible_variations` 里移除。
- 踩点：全部不勾时 `possible_variations` 为空 → `get_random_variation()` 直接返回 0（`DoodadBrush.cpp:25-27`）
  → 仍然会放 0 号变体，不报错也不会「不放」。

#### 4.7.4 Flags（可见性/碰撞标记）

- 三个 `QRadioButton` 直接改 `brush.state`，只影响**之后新放的**可破坏物；放置时写进 `doodad.state`
  （`DoodadBrush::apply`，286 行），最终落在 `.doo` 每条记录的那个 flags 字节（`Doodads.cpp:98`）。
- 枚举顺序与字节值一致：`invisible_non_solid=0` / `visible_non_solid=1` / `visible_solid=2`（`Doodads.h:22-25`）。
  HiveWE 自己**不解释**这个字节（渲染完全不看它），效果要到游戏里才看得到。
- 它们没放进 `QButtonGroup`，靠同父控件的 autoExclusive 互斥；选中已有对象时也不会同步显示该对象的 state。

#### 4.7.5 Pathing（寻路贴图预览）

- 只是一个 `AspectRatioPixmapLabel`（`DoodadPalette.cpp:98`），把选中模型的 `pathTex` 贴图转成 QImage 显示（244-246 行）。
- 没有 pathing 贴图的模型 → `pathing_section->setHidden(true)`，整段消失（248 行）。
- 初始还没选中任何模型时这一格是**空框**（构造时没有 setHidden(true)）。

#### 4.7.6 其他观测

- 这一排**没有笔刷大小/形状**：放置占地由 `pathTex` 贴图尺寸决定（`set_size(max(w, h))`，431 行）；
  没有 pathing 贴图时 `free_placement = true`，直接按鼠标世界坐标放（277-281 行）。
- 分区 1 没有 `setText()`，所以它那个 caption 是空的（`QRibbonSection` 把 caption 放在按钮下方，`QRibbon.cpp:89`），
  和另外四个带标题的分区并排时看起来缺一块。
- 快捷键是 `QShortcut(sequence, attach_to)` 挂在两个 widget 上，`activatedAmbiguously` 只 `printf("kut2\n")`
  （`QRibbon.cpp:20-24`），焦点候选冲突时控制台会出现这行垃圾输出。

#### 4.7.7 建议验证动作

1. 选一棵树（有 pathing 贴图）→ 看 Pathing 分区是否出现贴图；换成没有 pathTex 的石块 → 分区应消失。
2. 勾掉所有变体 → 连放 3 个 → 检查是否都落在 0 号变体。
3. Space 切到选择模式并框选 → 切到 Terrain Palette 再切回来 → 检查按钮显示与实际模式是否一致（预期不一致）。
4. 三个 Flags 各放一个 → 保存后用二进制查看 `.doo` 的 state 字节是否为 0/1/2。

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
    是否可行走、是否可飞行、是否可建筑，这应该是3个需要独立绘制的遮罩。
    这里可能是指的是用地表笔刷额外绘制的逻辑区域。
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

4. Options 页

    Melee Map | 对战地图标记，影响客户端对AI、数据是否采用对战版本。勾选后不能有自定义触发器、自定义单位。

    缩略地图
    Hide minimap in preview screens | 在预览/房间界面不显示小地图缩略图
    Masked areas are partially visible | 未探索区域不是纯黑

    水波
    Show water waves on cliff shores | 悬崖岸边显示水波 | `0x0800` | 需要图里有悬崖贴水的地形才看得到 |
    Show water waves on rolling shores | 缓坡（非悬崖）岸边显示水波 | `0x1000` | 同上，两者互相独立 |

    商店
    Use item classification system | 启用物品分类系统（1.30+ 的物品类别：永久/充能/神器…），影响商店分类与物品提示。

    雾
    Use Terrain Fog（组框标题） | 激活雾设置
    Style | 雾的衰减方式：`Linear` 线性、`Exponential 1` 指数雾 1、`Exponential 2` 指数雾 2（衰减更快） | 
    Z Start | 雾开始出现的距离地面的高度
    Z End | 雾完全遮蔽的距离地面的高度
    Density | 雾浓度，默认0。
    Color | 雾颜色（色板按钮）

    环境类
    Use Global Weather + 下拉 | 全局天气
    Custom Sound Environment + 下拉 | 自定义环境音
    Custom Light Environment + 下拉 | 自定义光照环境
    Use Water Tinting Color + 色板 | 全局水面染色

    Game Data Set | 用哪套平衡数据：
        `Default (Map melee status)`=按 Melee Map 标记决定 / 
        `Custom (TFT 1.02, RoC 1.01)`=固定老版数据 / 
        `Melee (Latest Patch)`=最新对战平衡

5. Preferences 页

- 未实现。
- 对应官方编辑器的过场/玩家与队伍一类设置。

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

# 附录 A：地图目录里每个文件是什么（样本 `Data/Maps/(2)BootyBay`）

样本概况：地形 193×97 格、格式版本 28（1.31）、tileset `L`（Lordaeron 夏）、6 种地面纹理 + 2 种悬崖纹理；顶层 14 个文件 + `_Locales` 下 9 份语言表。

## A.1 逐个文件

| 文件 | 大小 | 是什么 | 本工具读写 | 注意 |
|---|---|---|---|---|
| `war3map.w3e` | **地形本体**：地图尺寸、tileset、每格地面高度、水面高度与边界、地面/悬崖纹理索引、层高 | 每格 7 字节

| `war3map.w3i` | **地图信息**：名称/作者/描述/建议人数、相机边界、可玩区域、flags 位、载入图、Game Data Set、雾、天气、玩家/队伍/科技

| `war3map.wpm` | **寻路图**：格数、每格 4 字节的通行/建造位（不可走、不可建、空中/地面/两栖等） | 编辑器会按装饰物再叠加一次 pathing

| `war3map.doo` | **装饰物/可破坏物实例**：模型、坐标、朝向、缩放、生命、掉落表、可见性

| `war3mapUnits.doo` | **单位/物品实例**：类型、所属玩家、坐标、朝向、生命/魔法、掉落、自定义属性 | **读写**（`Units.cpp:197` / `Map.cpp:159`） | 同上；本工具没有单位放置 UI，只能读进来渲染 |

| `war3map.wtg` | 597 | **GUI 触发器树**：触发器/事件/条件/动作，用变量 ID 与分类 ID 编码，不含可读代码 | 也是"地图是否加密"的判据：缺它即视为 protected

| `war3map.wct` | 17 | **触发器自定义文本**：每个触发器附带的 JASS 片段/注释

| `war3map.j` | 30,538 | **编译后的 JASS 脚本**，客户端真正执行的就是它 | **只读 + 条件写回**（`Triggers.cpp:125` / `:1384`） | 触发器没改过就原样保留（`f5e09aa`）；改过会重新生成 → 第 11 节 #1 的风险区 |

| `war3map.wts` | 390 | **触发字符串表**：`STRING nnn { ... }`，w3i 与触发器里只存 `TRIGSTR_nnn` 键 | 地图名/描述/载入文本都在这里

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
