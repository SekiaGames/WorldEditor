# War3 WorldEdit（Godot + C#）路线图

> 目标：在 `D:\WorldEditor\Godot` 从空工程开始，用纯 C# 重新实现一个可用的 War3 地图编辑器。
> 参照物：`D:\WorldEditor`（HiveWE 0.6 C++ 实现）与 `D:\HiveWeGodot`（失败的先例）。
> 前置阅读：`Doc/00-现状与经验.md`（资产盘点、环境实测、上一代诊断）。
>
> **2026-11-05 修订**：界面层已先行完成（14 个界面 + 打桩路由），里程碑的验收单位已从「格式层完成」
> 改为「面板从桩变真」，顺序与工作量有调整，见 `Doc/02-界面先行与路线修订.md`。本文的架构与决策仍然有效，
> 第 4 节的里程碑表以 02 为准。

---

## 1. 目标与边界

### 1.1 要做

1. 打开 `.w3m` / `.w3x`（MPQ）与解包目录，**无损**读写 `war3map.*` 系列。
2. 地形可视化与编辑：高度、纹理、悬崖、水面、cell、blight。
3. 对象放置与编辑：装饰物、单位、区域、相机、开始点。
4. 地图属性、玩家、对象编辑器（SLK 元数据驱动）。
5. 路径图（walkable/flyable/buildable）显示与编辑。
6. 存回的地图能直接在 War3 客户端跑起来。

### 1.2 不做（第一版）

- 游戏运行、战役编辑、多人协作、自定义 UI 布局。
- 重制版（Reforged）模型/高清资源。
- 触发器图形化**生成**（第一版只做浏览 + 原样回存，见 D8）。
- 粒子/ribbon/事件物体等 MDX 特效（只渲染几何 + 骨骼 + 材质层）。

### 1.3 成功判据

| 判据 | 度量 |
|---|---|
| 打得开 | 全部 `Data\Maps` 样本 + 1.27a 自带官方图（≥ 50 张）不崩、字段不丢 |
| 存得回 | 未编辑时保存，9 个被写文件 md5 与原始一致 |
| 看得对 | 与 HiveWE 并排截图，地形/悬崖/水面/装饰物无明显差异 |
| 改得动 | 地形笔刷命中偏差 < 0.1 格；每类编辑可撤销 |
| 跑得动 | BootyBay（193×97、2011 doodad、61 unit）1080p ≥ 60 fps |
| 用得上 | 改完的地图在 1.27a 客户端里能正常载入并开局 |

---

## 2. 架构

### 2.1 三条总原则

1. **单一运行时**：所有状态、解析、算法在 C#。Godot 只负责「把数据画出来」和「把输入翻译成命令」。
2. **核心可脱离引擎**：`Core` 不 `using Godot`，能在 xunit 与纯 CLI 里跑。这样任何格式/算法问题都能在无窗口环境下复现。
3. **字节优先**：格式层以「原始字节 → 结构 → 原始字节」为闭环，任何未识别的区块都不许丢。

### 2.2 分层

```
┌──────────────────────────────────────────────┐
│ Godot 层（src/Game）                          │
│  场景 / Control 面板 / 输入 / 相机 / 材质      │
│  只做：读 Core 数据 → 建节点；输入 → 发命令    │
└───────────────┬──────────────────────────────┘
                │ 单向依赖（Game → Core）
┌───────────────▼──────────────────────────────┐
│ 应用层（src/Core/Editing）                    │
│  EditorSession：打开/保存/命令/撤销/选中       │
│  TerrainBrush / DoodadBrush / PathingBrush    │
└───────────────┬──────────────────────────────┘
┌───────────────▼──────────────────────────────┐
│ 领域层（src/Core/Map）                        │
│  Map 聚合：Terrain / Doodads / Units /         │
│  Regions / Cameras / MapInfo / Pathing /      │
│  Triggers / Imports                           │
└───────────────┬──────────────────────────────┘
┌───────────────▼──────────────────────────────┐
│ 基础设施（src/Core/Formats、Core/GameData）    │
│  MPQ / CASC / BLP / SLK / INI / MDX / w3*     │
│  Hierarchy（map > client > 内置 的查找优先级） │
└──────────────────────────────────────────────┘
```

依赖只能向下。`Core/Map` 不认识 Godot，`Core/Editing` 不发信号，返回「变更矩形 / 脏对象集合」，由 Game 层决定重绘什么。

### 2.3 目录（`D:\WorldEditor\Godot`）

```
Godot/                        Godot 工程根（project.godot 所在）
├─ Godot.csproj               assembly_name 必须与 project.godot 一致
├─ project.godot
├─ src/
│  ├─ Game/                   ← 引用 Godot
│  │   ├─ Bootstrap/          启动、加载进度、设置
│  │   ├─ TerrainView/        地形网格、悬崖、水面、pathing 叠加
│  │   ├─ Objects/            doodad/unit 的 MultiMesh 与选中框
│  │   ├─ Panels/             调色板、属性、小地图
│  │   └─ Camera/             轨道相机 + 游戏相机预览
│  └─ Core/                   ← 禁止 using Godot（用 analyzer 或 CI 检查）
│      ├─ Formats/            Mpq、Blp、Slk、Ini、Mdx、W3e、Wpm、Doo…
│      ├─ GameData/           Hierarchy、SLK 合并、MetaData、TriggerData
│      ├─ Map/                聚合根与各子模块
│      ├─ Editing/            命令、撤销栈、笔刷算子
│      └─ Rendering/          与引擎无关的几何生成（顶点/索引/UV/权重）
├─ tests/Core.Tests/          xunit，直接跑，不需要 Godot
└─ tools/HeadlessCli/         dump / roundtrip / bench，无窗口
```

**双工程 vs 单工程**：建议 `Core` 独立成 `Core.csproj`（`Godot.csproj` 用 `ProjectReference` 引用）。这样 CLI 与测试不必拖 GodotSharp。代价是首次接线需要验证 Godot 4.7 对子工程的支持（约 30 分钟，见 M0-S0）。

### 2.4 数据流

```
输入事件 → Game 层翻译成世界上的一点/一条射线
        → Core：命中测试（数学求交，非物理引擎）
        → Core：编辑命令（可撤销）改数据
        → Core：返回 ChangeSet（脏矩形 / 脏对象 id）
        → Game 层：只重建脏区域
```

**热路径不许全量重建。** 上一代拖动 2011 个装饰物时整组重建要几十毫秒，只能靠「幽灵节点」绕过。我们从一开始就按 ChangeSet 增量更新。

### 2.5 关键不变量

| 不变量 | 说明 |
|---|---|
| 一份高度定义 | 地形高度统一为 `height`（corners 原始值）；需要可视高度时只允许调 `FinalHeight()` 一个函数，禁止各处自行 `+ layer - 2`。这条是 `4ba7d79` 笔刷偏移 bug 的直接教训 |
| 一份坐标换算 | 世界单位 ↔ 格（1 格 = 128.0）只在一个静态类里，禁止散落魔数 |
| 保存即校验 | 任何 writer 写完后，回读并与内存结构比对，不一致立即抛异常而不是写出坏文件 |
| 不静默丢字段 | 未知/未识别一律进 `UnknownBlocks`，保存时原样回写 |

---

## 3. 关键技术决策

每条给出选项与建议；标 ★ 的是需要你拍板的（见第 8 节）。

### D1 ★ 数据源优先级：MPQ（1.27a）先，CASC（1.31）后

| 选项 | 优点 | 代价 |
|---|---|---|
| A. 只支持 MPQ（1.27a 及更早） | 纯 C# 可完整实现；本机有 `D:\War3_1.27a`；地图文件本身也是 MPQ，读写复用一套代码 | 打不开 1.31 数据（但地图仍可打开，只是缺游戏数据） |
| B. 只支持 CASC（1.31） | 匹配最新客户端 | CASC 解析复杂（TVFS/BLTE 索引），纯 C# 工作量大；地图仍是 MPQ，两套并存 |
| C. 抽象成 `IGameStorage`，先 A 后 B | 上层无感；二期加 CASC 不动业务 | 多一层接口，几乎无成本 |

**建议 C**：接口 + 先落地 MPQ。CASC 二期用 P/Invoke `CascLib.dll`（5 个函数）而不是自写，避免重复上一代「替身返回失败」的坑。

### D2 ★ 格式层：全部自写，还是借用 War3Net

| 格式 | 复杂度 | 建议 |
|---|---|---|
| MPQ 读 | 中（哈希表、块表、压缩、加解密） | 自写或评估 `War3Net.IO.Mpq`（纯 C#）。spike 里 30 分钟能测出来 |
| MPQ 写 | 中高（`(listfile)`、属性、对齐） | 自写，必须能控制 listfile 与块顺序 |
| BLP | 中（DXT1/3/5、调色板、JPEG 内容、alpha 位深） | 自写，总量 < 500 行 |
| SLK / INI | 低 | 自写，总量 < 300 行 |
| w3e / wpm / doo / w3i / w3r / w3c / w3s / w3u… | 低到中（字段顺序 + 版本分支） | 自写。字段顺序直接对照 HiveWE 源码，这是它最可靠的部分 |
| MDX | 高（版本分支 v800→v1800、骨骼、材质层、Geoset 动画） | 自写，只做编辑器需要的子集；先只读 GEOS/MTLS/BONE/SEQS |

**原则：凡是需要「无损回写」的格式，必须自己控制字节，不交给第三方序列化。** 第三方库可以用于读（例如 MPQ 解包），但写回路径必须是自己的 writer。

### D3 地形渲染：自写网格 + 自写 shader

不用 Terrain3D 或 HeightMapTerrain 插件。War3 地形不是高度场网格：每个 corner 带 4 个纹理 id + 4 个 variation、`layer_height`、`cliff/ramp` 标志，悬崖是独立 MDX 网格拼接。通用插件表达不了这些语义。

实现要点（来自 `Data/Shaders/terrain.fs`，可直接复用公式）：

- 顶点：每格 4 个 corner，高度取 `corners.height`；法线由相邻高度计算（与官方光照一致）。
- 片元：`color = mix(color, next, next.a)`，从 `a` 到 `r` 叠四次；纹理走 `Texture2DArray`，每字节 `index | variation<<5`。
- 悬崖：从 `cliff_slk` / `TerrainArt\Cliffs\*.mdx` 取网格，按 corner 的 `cliff_variation` / `cliff_texture` 实例化。
- 水面：固定高度平面 + 参数（`min_depth=10/128`、`deeplevel=64/128`、`maxdepth=72/128`），先不做波浪。

### D4 拾取与命中：纯数学，不用物理引擎

- 相机射线与高度场求交：先按最大/最小高度求包围盒交点，再沿射线步进 + 二分，落在 `InterpolatedHeight(x,y)` 上。
- 对象拾取：射线 vs 轴对齐包围盒（单位/装饰物），再按屏幕距离排序。
- **不引入 Jolt/Bullet。** 上一代用 `btHeightfieldTerrainShape` + 刚体原点偏移，导致渲染面与碰撞面相差 1-2 单位，是最难查的 bug。物理引擎会制造第二套高度真相。

### D5 模型与动画：运行时解析 + GPU 蒙皮，装饰物静态

| 方案 | 说明 | 取舍 |
|---|---|---|
| A. 运行时解析 MDX → ArrayMesh | 无中间产物，改文件即生效 | 首次加载慢（需缓存） |
| B. 预转换成 glTF/.res | 加载快 | 丢失非标准材质与队伍色，转换器本身是负担 |
| C. 运行时 + 磁盘缓存（建议） | 第一次解析后写 `.res` 缓存，按 mtime 失效 | 略复杂，但两头好处都有 |

动画：单位用单个共享 `AnimationPlayer` 驱动，蒙皮在顶点着色器做（Godot `MeshInstance3D` 自定义 shader + 骨骼 uniform），**不走逐实例 CPU 重蒙皮**。装饰物默认静态（War3 装饰物本来就多静止），例外是带 Stand 动画的少数类型，二期再说。

### D6 无损往返：解析留原始字节，写入前做自检

三档策略：

| 档 | 做法 | 风险 |
|---|---|---|
| A. 严格字节保留 | 只重写被编辑的区块，其余原样拷贝 | 编辑跨区块时容易错位 |
| B. 完整重写 | 解析成结构，全量序列化 | 任何未识别字段丢失，版本号被改 |
| C. A + 自检（建议） | 结构里带「原始片段」；保存后回读比对 | 实现成本略高，但可控 |

必须做到：**未编辑的文件不写入**。上一代的 wtg 自毁就是「打开地图顺手重写 wtg」造成的。

### D7 UI：Godot Control + Theme，模仿 WE 布局

- 顶部：菜单 + ribbon 式工具条（Terrain / Doodad / Unit / Region / Pathing / Tools）。
- 左侧：调色板（纹理墙、悬崖类型、装饰物分类树、单位分类树）。
- 底部：小地图 + 状态栏（坐标、高度、当前工具、FPS）。
- 右侧：属性面板（选中对象 / 笔刷参数）。
- 全部用 `Theme` 资源统一字号与配色，不用 `_Draw` 自绘（除非小地图和纹理墙的缩略图）。

### D8 ★ 触发器：第一版只读 + 原样回存

HiveWE 0.6 的触发器模块是全工程缺陷最集中的地方：`build_trigger_data()` 有写盘副作用、分类计数往返不一致会自毁文件、脚本生成器系统性残缺（可破坏物/物品创建函数为空、`.doo` 字段错位、`SetTeams` 与官方不一致）。

建议分三步：

1. **只读 + 原样回存**（M9a）：解析 `wtg`/`wct`/`wts` 展示树与脚本，保存时**不重写**，除非用户改过。保证「打开即安全」。
2. **属性编辑**（M9b）：改名称/描述/启用/初始开启/ECA 参数，改完只写 `wtg`，且写后自检。
3. **JASS 生成**（M9c，可选）：以官方 WE 的输出为基准做差分，同一份 wtg 生成的 `.j` 必须与官方逐字节一致，否则不上线。

如果目标用户主要是做地形/布局，M9 整体可以延后到 M10 之后。

---

## 4. 里程碑

> 「量级」是粗估，含调试时间，假设单人 + AI 协作：S ≤ 3 天，M 3–8 天，L 8–15 天，XL > 15 天。

### M0 · 风险清除（5 个 spike，S+M+M+M+S）

不写产品代码，每个 spike 只回答一个问题，产出结论写回 `Doc/`。

| # | Spike | 通过标准 | 量级 |
|---|---|---|---|
| S0 | 工程接线 | `Core.csproj` + `Godot.csproj` + `Core.Tests` + `HeadlessCli` 四个工程能一起 build；Godot 里能跑一个 C# 节点；CLI 能跑一个测试 | S |
| S1 | MPQ 读 | 打开 `(2)BootyBay.w3m`，列出条目，读出 `war3map.w3e` 头部 = 193×97、tileset `L` | S |
| S2 | BLP 解码 | 解出 `TerrainArt\Ashenvale\Ashen_Dirt.blp` 存 PNG，颜色与原图一致 | M |
| S3 | MDX 解析 | 读一个单位模型，输出顶点数/包围盒/序列名；Godot 里用 `ArrayMesh` 静态显示，朝向正确 | M |
| S4 | 地形渲染 | 64×64 手工高度场 + 4 层混合 shader + 一层水面，出一屏 | M |
| S5 | 往返 | 读 BootyBay 的 `w3e` 原样写回，md5 与原始一致 | S |

**通过全部 5 个才进入 M1。** 任何一个失败，先解决，不带着不确定性往下走。

### M1 · 地基与只读加载（M）

- 字节读写工具（小端、带 position/limit、越界即抛；不做静默读 0）。
- `IGameStorage` + MPQ 实现 + `Hierarchy`（查找优先级：地图内 > 客户端 > 内置测试数据）。
- 解析：`w3e`、`wpm`、`w3i`、`w3s`、`w3r`、`w3c`、`wts`、`doo`、`war3mapUnits.doo`。
- `HeadlessCli dump`：输出 JSON（尺寸、纹理列表、单位/装饰物计数、玩家、区域）。

**验收**：`HeadlessCli dump "(2)BootyBay.w3m"` 输出 193×97 / 7 纹理 / 2011 doodad / 61 unit / 18721 corner，与 HiveWeGodot 实测值一致；50 张官方图全部能解析。

### M2 · 地形渲染（只读画面）（L）

- 地形网格生成（含 corner 纹理打包、variation、blight 权重）。
- 四层混合 shader + `Texture2DArray`。
- 水面（固定高度 + 参数化颜色）。
- 悬崖网格拼接（最难的一块，单独留时间）。
- 轨道相机 + 固定视角预设（War3 Landscape）。

**验收**：与 HiveWE 并排截图（同图同视角）目视一致；断崖、坡道、水陆交界无裂缝；BootyBay 1080p ≥ 60 fps。

### M3 · 地形编辑（L）

- 笔刷框架：圆形/方形/菱形、衰减曲线、按格遍历、`ChangeSet`。
- 高度：raise / lower / level / smooth / noise；水面高度独立。
- 纹理：涂抹 + 自动 variation + blight。
- 悬崖：放置 / 升降 / 坡道 / 类型选择。
- Cell：水 / 陆 / blight 直接刷。
- 撤销：按「高度 / 纹理 / 悬崖 / 水面」四类分栈，矩形区域快照，上限可配。

**验收**：笔刷中心命中点与渲染面偏差 < 0.1 格（用上一代的采样日志法，把命中点反查 `w3e` 高度统计）；连续 200 步撤销后内存增长 < 200 MB；撤销后画面与初始截图逐像素一致。

### M4 · 保存与无损往返（M）

- 各格式 writer（`w3e/wpm/doo/w3i/wts/w3r/w3c/w3s`）。
- 未编辑文件不落盘；编辑过的做「写完回读自检」。
- MPQ 打包（保留 `(listfile)`、跳过 `(attributes)`）。

**验收**：打开→保存（不做任何编辑）→ 被写文件 md5 与原始一致；改一笔高度→保存→重开→高度与内存一致；生成的地图能被 1.27a 客户端载入。

### M5 · 装饰物（L）

- `DoodadData.slk` + 分类树 + 缩略图（BLP→ImageTexture，做缓存）。
- 放置 / 随机变体 / 随机旋转 / 随机缩放 / 高度锁定。
- MDX 静态渲染（`MultiMesh`），选中描边，拾取。
- pathing 贴图联动（War3 的路径图是装饰物自带 BLP 贴图 blit 上去的）。

**验收**：2011 个装饰物全部摆上且帧率 ≥ 60；删改后 pathing 立即刷新；拾取误差在包围盒内。

### M6 · 单位（M）

- `UnitData.slk` + 玩家颜色 / 队伍色（shader 里按玩家 id 取色）。
- 放置 / 选中 / 拖动 / 旋转（按 `fixedrot` 吸附）。
- Stand 动画（共享 `AnimationPlayer` + GPU 蒙皮）。

**验收**：61 个单位全部显示，四名玩家颜色正确，动画不抖动，100 个单位实例帧率 ≥ 60。

### M7 · 区域 / 相机 / 开始点 / 地图属性 / 玩家（M）

- `w3r`（矩形/圆形区域 + 颜色 + 天气 + 环境音）、`w3c`（相机）、`w3s`（声音）。
- `w3i`：地图名/作者/描述/建议人数/选项/载入画面/地图尺寸/可玩区域。
- 玩家槽位：控制器、种族、名字、固定出生点。

**验收**：每项改完保存，与官方 WE 对同一字段的写盘结果一致（逐字段比对，不要求整文件一致）。

### M8 · 对象编辑器（XL）

- SLK 元数据合并（`UnitMetaData.slk` 等七类）+ 字段表单自动生成。
- 自定义对象：`w3u/w3t/w3d/w3b/w3a/w3h/w3q/w3o`。
- 搜索、分类树、图标、跳转。

**验收**：新建一个自定义单位，改 5 个字段，保存后游戏内生效；官方 WE 打开同一地图能看到该对象。

### M9 · 触发器（分三步，合计 L~XL）

见 D8。M9a 只读 + 原样回存（M）；M9b 属性编辑（L）；M9c JASS 生成（XL，可选）。

**验收（M9a/M9b）**：打开含 17 个触发器的样本图，往返 md5 一致；改一个触发器名，只有 `wtg` 变化且能被客户端识别。

### M10 · 资产与收尾（L）

- 资源管理器（浏览 MPQ / 地图内文件，预览 BLP/MDX，导出）。
- 实时小地图（含 `war3mapMap.blp` 与 `mmp` 图标）。
- 地图保护 / 去保护、导出、测试地图（拉起客户端）。
- 导入管理器（`war3map.imp`）——**上一代完全没做，这是实际做图的刚需**。
- 模型预览窗口、设置、快捷键、最近打开。

**验收**：导入一张自定义贴图 → 地图内引用 → 导出 → 客户端生效，全链路走通。

### 阶段依赖

```
M0 ─→ M1 ─→ M2 ─→ M3 ─→ M4
              └─→ M5 ─→ M6
                        └─→ M7
M4 ─→ M8 ─→ M9
M5 ─→ M10
```

M8/M9 可以与 M6/M7 并行（一个人做格式+UI，一个人做渲染）。

---

## 5. 依赖与格式清单

### 5.1 游戏数据（客户端读，1.27a）

| 用途 | 文件 |
|---|---|
| 地形 | `TerrainArt\Terrain.slk`、`CliffTypes.slk`、`*\*.blp`、`Cliffs\*.mdx` |
| 装饰物 | `Doodads\DoodadData.slk`、`DoodadSkins.txt`、`*.mdx` |
| 单位 | `Units\UnitData.slk`、`UnitUI/UnitBalance/UnitWeapons/UnitAbilData.slk`、`UnitMetaData.slk` |
| 物品 / 技能 / 升级 / buff | `Units\ItemData.slk`、`Units\AbilityData.slk`、`Units\UpgradeData.slk`、`Units\AbilityBuffData.slk` + 各自 MetaData |
| 可破坏物 | `Units\DestructableData.slk`、`DestructableMetaData.slk` |
| 玩家颜色 | `UI\PlayerColor*.txt`、`Units\UnitUI.slk` 的 team color 列 |
| 触发器 | `UI\TriggerData.txt`、`TriggerStrings.txt`、`Scripts\common.j` / `blizzard.j` |
| 杂项 | `UI\MiscData.txt`、`MiscUI.txt`、`War3Skin.txt` |

### 5.2 地图文件（按里程碑）

| 文件 | 内容 | 里程碑 |
|---|---|---|
| `war3map.w3e` | 地形（corner、纹理、悬崖、水） | M1 读 / M4 写 |
| `war3map.wpm` | 寻路（每 tile 4×4 子格） | M1 / M4 |
| `war3map.doo`、`war3mapUnits.doo` | 装饰物、单位 | M1 / M5、M6 |
| `war3map.w3i` | 地图信息、玩家、队伍、载入画面 | M1 / M7 |
| `war3map.w3r`、`.w3c`、`.w3s` | 区域、相机、声音 | M1 / M7 |
| `war3map.wts` | 触发器字符串表 | M1 / M4 |
| `war3map.w3u/.w3t/.w3d/.w3b/.w3a/.w3h/.w3q/.w3o` | 自定义对象 | M8 |
| `war3map.wtg`、`.wct`、`.j` | 触发器、脚本 | M9 |
| `war3map.imp` | 导入文件清单 | M10（**上一代缺失**） |
| `war3map.mmp`、`war3map.shd`、`war3mapMap.blp` | 小地图图标、静态阴影、缩略图 | M10（**上一代缺失**） |

---

## 6. 测试与验证

四层，全部可命令行执行：

| 层 | 做法 | 频率 |
|---|---|---|
| 单元 | `dotnet test`：格式读写、笔刷算子、撤销、字节工具 | 每次提交 |
| 黄金样本 | 50 张官方图的 `dump` 快照存 JSON，改动后 diff | 每次提交 |
| 差分 | 同一张图，用 HiveWE 0.6（现成 exe）与新编辑器各 dump 一次，逐字段比对 | 每个里程碑 |
| 视觉 | 固定相机参数截图，与 HiveWE 截图并排；关键区域（悬崖/水边）单独截 | 每个里程碑 |
| 实机 | 保存的地图丢进 `D:\War3_1.27a` 开局 | M4 之后每阶段 |

**禁止的验证方式**：「肉眼看差不多」。上一代的教训是——笔刷偏移 1-4 格，肉眼在地形上完全看不出来，只能靠采样日志统计。

---

## 7. 风险登记

| # | 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|---|
| 1 | 悬崖网格拼接做不对（坡道、跨层、边缘） | 高 | 高 | M2 单独留 3-5 天；先用 HiveWE 的 cliff 数据模型对齐；做「每格悬崖类型可视化」调试视图 |
| 2 | MPQ 写回破坏原图 | 中 | 高 | M0-S5 先验证往返；writer 写完必自检；保存前自动备份 |
| 3 | MDX 版本分支多（v800–v1800） | 中 | 中 | 只做编辑器需要的子集；遇到不认识的版本明确报错，不猜 |
| 4 | 大量实例性能（2000 doodad + 动画单位） | 中 | 中 | `MultiMesh` + GPU 蒙皮；从一开始按 ChangeSet 增量更新；M5 做基准 |
| 5 | 触发器格式自毁 | 中 | 高 | M9a 只读不写；写入前 diff 校验；不进 M9b 前不碰 `wtg` 写路径 |
| 6 | 1.31 CASC 支持 | 中 | 中 | 二期做，P/Invoke CascLib；地图文件本身是 MPQ，不受影响 |
| 7 | ~~Godot 编辑器内 build 失效~~ | — | — | **已排除**：误判，原因是沙箱 shell 缺 `dotnet`。真问题只有「导出需要 `.sln` + 纯数字版本号」，已修 |
| 8 | 单人开发周期拉长导致范围膨胀 | 高 | 中 | 严格按 M0–M4 先出「能看能改能存」的最小闭环，其余按需 |

---

## 8. 需要你拍板的四件事

| # | 决策 | 我的建议 | 备选 |
|---|---|---|---|
| D1 | 首发客户端 | 1.27a（MPQ），CASC 二期 | 直接刚 1.31（成本高很多） |
| D2 | MPQ 实现 | 先 spike 评估 `War3Net.IO.Mpq`，不行就自写 | 直接 P/Invoke StormLib（上一代证明可行但引入原生依赖） |
| D8 | 触发器 | 第一版只读 + 原样回存 | 直接做完整编辑器（风险最高） |
| D-范围 | 第一版要不要「对象编辑器」 | 要，但排在 M8；先保证地形链路 | 砍掉，只做地形+对象摆放 |

---

## 9. 下一步（等你确认后我就动手）

1. 按 D1–D8 定稿，把 M0 的 5 个 spike 拆成可执行任务。
2. 先做 S0（工程接线）+ S1（MPQ 读），这两个通过后再谈渲染。
3. 每个 spike 结束写一页结论进 `Doc/`，包括失败原因与替代方案。
