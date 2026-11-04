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
| 1.5 | Save Map as 另存为新目录|
| 1.6 | Export MPQ 导出为.w3x|
| 1.7 | Test Map 调 `x86_64\Warcraft III.exe -loadfile`|
| 1.8 | 设置窗口 | 
| 1.9 | 退出 |


File/Game/MapInfo | 打开 MapInfoEditor 界面 |

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

2. Description页





| # | 功能 | 结果 |
|---|---|---|---|---|
| 8.1 | Description 页（名称/作者/描述/建议人数） | 改动后未生效
    Name
    Suggested Players
    Description
    Map Version和EditorVersion是什么意思
| 8.2 | Loading Screen 页（图片/标题/副标题/文本） | 改动后未生效
    Loadding Screen Graphic
        三个选项
    Loading Screen Tittle
    Loading Screen Subtitle
    Loading Screen Text
| 8.3 | Options 页（迷雾/天气/水面/光照/声音环境/小地图预览） | 得先翻译下每个选项是什么意思
    Hide minimap in preview creens 开关|
| 8.3 | Preferences 页 无内容|

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
