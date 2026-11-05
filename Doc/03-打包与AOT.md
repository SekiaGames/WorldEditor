# 03 · 打包与 AOT（2026-11-05）

目标：**一个 exe 拷走就能跑**，并尽量用 AOT 提前编译。

## 结论先说

| 需求 | 能不能 | 做法 |
|---|---|---|
| 单文件（无 `data_*/` 目录） | **能** | `binary_format/embed_pck=true` + `dotnet/embed_build_outputs=true` |
| AOT：ReadyToRun（IL 预编译成原生码） | **能** | csproj 里 `<PublishReadyToRun>true</PublishReadyToRun>` |
| AOT：NativeAOT（`PublishAot`，完全去掉 JIT） | **不能** | 本机缺 MSVC 链接器；Godot 的 .NET 导出也没有桌面 NativeAOT 通道 |

## 单文件怎么做

`export_presets.cfg` 两处（Windows 与 macOS 两个预设都有）：

```
binary_format/embed_pck = true          # 把项目资源 PCK 塞进 exe
dotnet/embed_build_outputs = true       # 把 dotnet publish 的产物（含 .NET 运行时）塞进 exe
```

第二个是 Godot 4.4+ 的选项，含义就是「不要把发布产物平铺到 `data_<Assembly>_<platform>_<arch>/`」。
两个都开后，导出目录里只剩一个 exe。

## 实测数据

| 组合 | 产物 | 体积 |
|---|---|---|
| 双文件（`embed_build_outputs=false`） | `WorldEditor.exe` + `data_WorldEdit_windows_x86_64/`（238 个文件） | 109 MB + 159 MB |
| 单文件 | `WorldEditor.exe` | **189.7 MB** |
| 单文件 + ReadyToRun | `WorldEditor.exe` | **199.9 MB** |

R2R 多出来的约 10 MB，是框架程序集也被预编译成原生代码的结果（我们自己那份 `WorldEdit.dll` 单独 publish 时从 105 KB 涨到 335 KB）。
体积账：模板 + 项目 PCK 约 109 MB，内嵌 .NET 8 运行时约 80 MB。

## AOT 细节

**ReadyToRun（已启用）**

```xml
<PublishReadyToRun>true</PublishReadyToRun>
```

- 不需要额外工具链，`dotnet publish` 自己调 crossgen2。
- 效果：方法体在构建期编译成原生代码，运行时不 JIT，冷启动更快。
- 验证：同一份源码 `dotnet publish -r win-x64 --self-contained false` 加不加这个属性，
  `WorldEdit.dll` 是 105,472 → 335,872 字节；导出 exe 从 189,653,576 → 199,884,360 字节。

**NativeAOT（不可用）**

直接用 `-p:PublishAot=true` 试过，构建期就断：

```
error : Platform linker not found. Ensure you have all the required prerequisites documented at
https://aka.ms/nativeaot-prerequisites, in particular the Desktop Development for C++ workload
in Visual Studio.
```

本机只装了 .NET SDK（`C:\Program Files\dotnet`），没有 Visual Studio / MSVC，所以链接器缺失。

即使补上 MSVC 也未必能跑：Godot 加载 C# 是靠 hostfxr 加载托管程序集，NativeAOT 产出的是原生库，没有托管元数据。
在 `GodotTools.dll` 里搜到的 AOT 相关字符串只有 `_aot.xcframework` / `godot-aot-`（iOS/Apple 平台的 AOT 打包），
桌面端没有对应通道。**所以「AOT」在本项目里的落地形态就是 ReadyToRun。**

## 验证

1. 把 `WorldEditor.exe` 单独拷到空目录（`/tmp/iso2`），目录里只有一个 exe。
2. 直接运行，`[SHOT] ... 1600x900` 正常出图；`--smoke` 35/35 通过。
3. 运行后目录里没有新文件，`%APPDATA%\Godot\app_userdata\WorldEditor` 只有 logs / shader_cache / vulkan 缓存，
   **没有把运行时解包到磁盘**。
4. 本机只装了 .NET 10 运行时（`Microsoft.NETCore.App 10.0.9`），没有 .NET 8；程序是 `net8.0` 却能跑，
   说明运行时确实来自 exe 内部，不依赖机器上装没装 .NET。

## 代价与注意

- **单文件不能热更新资源**：改一张图就要重新导出 190 MB。开发期建议把 `dotnet/embed_build_outputs` 关掉，发布时再打开。
- **导出时不能有实例在跑**：exe 被占用会报「重命名临时文件失败」。`export.bat` 支持传输出名绕开（见脚本注释）。
- **`dotnet/include_debug_symbols` 目前是 true**，pdb 一起进了包。要再瘦一点可以关掉。
- 每次启动引擎仍要初始化渲染管线，实测这个环境下从进程启动到出图约 6.9 秒（含 `--smoke` 全流程），与是否单文件无关的证据还没做，先按「无差异」处理。
