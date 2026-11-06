# WorldEditor（Godot + C#）

HiveWE 0.6 的界面在 Godot 4.7 / .NET 上的复刻，加上从 0 写的 War3 格式层。

**已经能用的**：打开 `.w3m/.w3x`（MPQ 解析 + 表解密 + 多扇区解压）→ 地形笔刷能刷出来的东西都在视口里按真实资源渲染：
高度、四层地表叠加、悬崖模型、斜坡过渡、逐格水面（深浅配色）；保存 / 另存为写回 MPQ。
其余按钮仍然打桩，点下去只打印 `[STUB] <id>`。

资源取自 `assets/`（已从 1.27 客户端提取），**不依赖 War3 安装目录**。

```
D:/WorldEditor/
  HiveWE/           C++ 参考实现（HiveWE 0.6 源码 + 分析文档）
  Godot/            Godot 工程本体（工程文件、C# 源码、资源）
  tools/            截图核对用的小工具
  export.bat        Windows 一键导出
  export.command    macOS 一键导出
```

## 运行

```bash
# 编译
dotnet build Godot/WorldEdit.csproj

# 启动
"D:/Ghost/GameDev/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64.exe" --path Godot
```

> Godot 编辑器里的 C# 构建依赖 PATH 中的 `dotnet`。系统 PATH 里如果没有 `C:\Program Files\dotnet`，
> 编辑器会报 `Microsoft.Build 15.1.0.0 找不到`，但命令行 `dotnet build` 仍然正常。本项目要求两个文件：
> `WorldEdit.csproj`（工程）与 `WorldEdit.sln`（解决方案，**导出 .NET 项目时必需**）。

## 导出 Windows 版

```bash
cmd //c export.bat          # 或双击 export.bat
```

产物是**单个 exe，没有任何附带文件**，输出到仓库根目录（`D:/WorldEditor/WorldEditor.exe`）（`export_presets.cfg` 里开了 `binary_format/embed_pck` 与 `dotnet/embed_build_outputs`）：

| 文件 | 说明 |
|---|---|
| `WorldEditor.exe`（仓库根） | 190 MB，导出模板 + 内嵌 PCK + 内嵌 .NET 运行时与程序集，拷到任何空白目录都能直接跑 |

体积构成（实测）：模板 + 项目 PCK 约 109 MB，加上内嵌的 .NET 8 运行时约 80 MB；开启 ReadyToRun 后多约 10 MB。
想看双文件形态（exe + `data_*/`）把 `dotnet/embed_build_outputs` 关掉即可，两者功能一致。

**AOT**：`WorldEdit.csproj` 里开了 `<PublishReadyToRun>true</PublishReadyToRun>`，IL 预先编译成原生代码，运行时不 JIT。
NativeAOT（`PublishAot`）本机走不通——缺 MSVC 链接器，且 Godot 的 .NET 导出也没有桌面 NativeAOT 通道，详见 `HiveWE/Doc/03-打包与AOT.md`。

三个容易踩的点：

1. `project.godot` 的 `config/version` 只能填纯数字（`0.1.0`），写 `0.1.0-stub` 会触发导出警告。
2. 没有 `.sln` 时，导出会在每个 `.cs` 上报 `no solution file exists`，最终产物能生成但**不含 C# 程序集**，一运行就退。
3. 导出时 Godot 会调用 `dotnet publish`；它从 PATH 找 `dotnet`，找不到就报 `Cannot find the dotnet executable.`（`export.bat` 已兜底补 PATH）。


## 相机操作（对齐官方 WE）

| 操作 | 作用 |
|---|---|
| 右键拖动 | 平移地图（上下方向与左右相反，按官方 WE；位移按当前视野换算成世界距离） |
| Ctrl + 右键拖动 | 绕视觉中心旋转（目标点不动，只改水平角/俯角） |
| Shift + 右键拖动 | 沿当前观察方向缓慢升降相机（俯角、朝向都不变） |
| 中键拖动 | 平移（备用） |
| 滚轮 | 缩放 |
| Ctrl+Shift+C / 视图按钮 | 重置相机 |

`--camera-test` 用数值自检这几条：平移 120 像素应等于 `120 × 每像素世界单位` 且方向与相机右向量相反；
向下拖 120 像素沿 `-forward`；旋转后目标点位移必须是 0；Shift 上拖 100 像素后目标点与相机同时升高。

## 截图自检

界面改动不能靠肉眼，全部走命令行截图：

```bash
# 首屏（只有主窗口，与 HiveWE 启动状态一致）
Godot.exe --path Godot -- --shot=C:/tmp/hero.png --frames=30

# 指定面板 / 窗口
Godot.exe --path Godot -- --shot=C:/tmp/a.png --palette=doodads
Godot.exe --path Godot -- --shot=C:/tmp/b.png --window=mapinfo
Godot.exe --path Godot -- --shot=C:/tmp/c.png --palette=none      # 关掉调色板看 Ribbon
```

**窗口是独立的系统窗口**（`display/window/subwindows/embed_subwindows=false`），主窗口截图拍不到它们。
所以有窗口打开时会额外存两张：

- `xxx-palette.png`：当前调色板窗口
- `xxx-window.png`：当前对话框

```
[SHOT] C:/tmp/b.png 1600x900
[SHOT] C:/tmp/b-palette.png 284x592 at (1786, 391)
[SHOT] C:/tmp/b-window.png 720x560 at (920, 416)
```

放大看细节（1px 边框这类东西在整图里看不出来）：

```bash
python tools/crop_png.py shot.png corner.png 6 126 40 30 10   # 左上角 10 倍
```

参数：

| 参数 | 作用 |
|---|---|
| `--shot=<path>` | 第 N 帧后保存 PNG 并退出 |
| `--frames=<n>` | N 默认为 24 |
| `--palette=terrain\|doodads\|pathing\|units\|regions\|none` | 启动时打开/关闭调色板 |
| `--window=mapinfo\|trigger\|import\|settings\|tilesetter\|tilepather\|tilepicker\|objecteditor\|players\|model` | 启动时打开窗口 |
| `--tab=<n>` | 启动时切到第 n 个 Ribbon 页签 |
| `--check-tabs` | 逐页签切一遍并打印 Ribbon 高度，验证切页签不跳 |
| `--camera-test` | 平移/旋转/升降的数值自检 |
| `--map=<path>` | 启动时直接打开地图 |
| `--map-dialog` | 启动时弹出打开地图对话框 |
| `--tile-dump=<tileID>\|<dir>` | 按瓦片 ID 查 Terrain.slk 并解码成 PNG |
| `--blp-dump=<相对路径>\|<png>` | 直接解码单张 BLP 到 PNG |
| `--tooltip-probe` | 按 Godot 生成 Tooltip 的同一结构建一个探针，打印解析出的底色/字色/字体，并画一份在视口上供截图 |
| `--dist=<世界单位>` | 启动时把相机距离设成指定值（近景核对用） |
| `--focus=<x>,<z>` | 启动时把相机目标移到某格（格坐标），配合 `--dist` 看局部 |
| `--no-water` | 不生成水面，排查地形/悬崖破洞用 |
| `--shot-view` | 只存 3D 视口（不含 UI），和 HiveWE 的视口截图逐像素比 |
| `--view-size=<W>x<H>` | 固定 3D 视口像素尺寸 |
| `--fov=<度>` | 改垂直 FOV（默认已经是 HiveWE 的 50.705°） |
| `--mpq-unpack=<mpq>|<dir>` | 把地图解包成目录，供 HiveWE 的目录模式打开 |
| `--cam=<x,y,z>` | 把相机放到指定世界坐标（标题栏里的坐标可以原样贴回来复现） |
| `--yaw=` / `--pitch=` | 覆盖水平角 / 俯角 |
| `--minimap-dump=<w3m>\|<png>` | 导出小地图 PNG（校验方向用） |
| `--no-minimap` | 启动时不显示小地图（截图对比用，等价于 View → 小地图 取消勾选） |

## 结构

路径相对 `Godot/`：

```
src/Game/App/
  Main.cs        主窗口装配、打桩路由、快捷键、截图入口
  UiTheme.cs     配色与 Theme（取自 HiveWE Dark.qss）
  W.cs           控件工厂（Ribbon 大按钮、分组卡片、色块等）
  Stub.cs        打桩路由：Stub.Fire("file.open_mpq")
src/Game/Ui/
  Ribbon.cs      File / View / Tools / Window 四个页签 + 动态调色板页签
  ViewportPanel.cs  SubViewport 占位地形（48×48 tile、128 单位/tile）、水面、笔刷环
  MinimapPanel.cs   由同一高度函数生成的小地图
  Palettes.cs    地形 / 装饰物 / 寻路 / 单位 / 区域
  Windows.cs     地图信息、触发器、导入、设置、瓦片集、瓦片寻路、替换瓦片、对象编辑器、玩家、模型预览
  Defs.cs        假数据（纹理名、装饰物分类、单位列表）
assets/icons/    从 `HiveWE/Data/Icons` 复制（ICO 已转 PNG）
assets/shaders/  占位地形与水面着色器
```

## 地图显示

`文件 → 打开地图` 选 `.w3m/.w3x`，视口会显示真实地形：高度来自 `war3map.w3e` 的角点，
地表贴图按 `Terrain.slk` 映射到 BLP 并叠成 `Texture2DArray`，水面按 `Water.slk` 的 tileset 偏移定位。

技术细节与踩过的坑见 `HiveWE/Doc/04-打开地图与地形渲染.md`，这里只列关键的三条：

- BLP1 头 156 字节后有 **1024 字节共享调色板**，`mipOffsets` 多算了它
- 地形贴图是 **4 分量 JPEG（BGRA）**，Godot 的 stb_image 不支持，所以自带一个基线 JPEG 解码器
- 世界高度必须加 **`layer_height`**：`(raw-8192)/512 + layer - 2`，漏了整张图会沉到水下

**已知限制**：悬崖尚未渲染，相邻角点层高差会显示成斜坡（看起来像锯齿）；水面是单平面；
没有阴影与雾效。

## 与 HiveWE 0.6 的差异

| 项 | HiveWE 0.6 | 这里 |
|---|---|---|
| 调色板 | 浮动 QDialog，贴视口右上 | 同：独立 `Window`，尺寸取 .ui 原值（284×592 / 244×560 / 242×265） |
| 启动状态 | 只有主窗口，调色板要靠 Window 页签打开 | 同（不再自动打开地形调色板） |
| 小地图 | 图像 256×256，外加 1px 等宽细边（面板 258），方角、无标题 | 同 |
| 状态栏 | 没有 | 没有（`Main.ShowStatusBar` 默认 false，需要时打开） |
| 地形调色板 | 三个 CheckBox 分区 + 48×48 图标按钮 | 同（默认只勾「纹理」「形变」，「悬崖」整段灰） |
| 弹窗（地图信息/触发器/…） | QDialog，可拖出主窗口 | `Window` 节点，同样是系统窗口；表单类模态、工具类非模态 |
| Ribbon 页签 | File / View / Tools / Window + 调色板页签 | 同 |
| 主题 | Dark.qss / Light.qss 二选一 | 固定亮色，配色取自 Light.qss |
| Ribbon 高度 | 116，切页签不跳 | 锁死 120（页签内容统一 89 + 31 页签条）；图标 32px、文字 10px、**文字不换行**，按钮宽度随文字撑开 |
| 中文 | 无 | 全中文，靠 `SystemFont`（不打包字库） |
| 工具提示 | Qt 原生，浅色 | 白底 + `#b9babb` 边 + 深色字。**必须自定义 `TooltipPanel` / `TooltipLabel`**：引擎默认是深底，而主题里 `Label` 的字色是深色，不定义就是深底深字（全黑） |
| 未实现的 0.6 界面 | — | 单位、区域、对象编辑器、玩家、模型预览补为占位 |

`Defs.cs` 里全是假数据。接真实数据时**必须整份替换**，不要让它活到能用为止——否则会出现「界面有内容、其实是假的」这种最难查的状态。
