# HiveWE 0.6 在 Windows 上用 VS 2022 构建

对应源码提交 `48940be`（`HiveWE0.6`）。目标：得到能双击运行的 `HiveWE.exe`，且可断点调试。

## 快速构建：build.bat

仓库根目录的 `build.bat` 已经把整套流程固化了：

```cmd
build.bat          编译 HiveWE 本体（默认）
build.bat deps     编译全部第三方依赖
build.bat all      先依赖后本体
build.bat clean    删除构建目录
```

脚本开头的「路径配置」段集中了 Qt 路径、依赖前缀、ADS 路径，换机器只改那里。产物在 `build\Release\HiveWE.exe`，Qt 运行时由脚本自动部署。

两个前提：`build.bat` 靠 `vcvars64.bat` 初始化 MSVC 环境，所以必须在**普通命令行窗口**里运行（被安全策略拦掉 `reg.exe` 的环境会让 vcvars 失效，报 `No CMAKE_CXX_COMPILER could be found`）；`deps` 模式还需要 `git` 来克隆依赖源码。

> 脚本正文刻意只用 ASCII 英文提示。批处理文件里写中文会牵扯编码问题（GBK 存的文件在 UTF-8 编辑器里是乱码，UTF-8 存的文件在 cmd 里会被拆错命令行），换成 ASCII 后两边的坑都没了。中文说明留在本文档里。

下面的 1-9 步是同一流程的手工版本，用于排查问题或想弄清每一步在做什么时参考。

## 0. 总览

整条链路分四块，任何一块出问题都会卡住后面的步骤：

| 步骤 | 装什么 | 大概耗时 | 体积 |
|---|---|---|---|
| 1 | VS 2022 + 桌面 C++ 工作负荷 | 30-60 分钟（下载为主） | ~8 GB |
| 2 | Qt 5.15.2 msvc2019_64 | 2 分钟 | ~600 MB |
| 3 | 源码编译 6 个依赖（glm/SOIL2/StormLib/CascLib/jpeg/Bullet） | 10-20 分钟（Bullet 最慢） | ~1 GB |
| 4 | 自编 QScintilla + ADS | 10 分钟 | ~50 MB |
| 5 | 配置 + 编译 HiveWE | 5-10 分钟 | ~1 GB |

约定几个目录，后面命令都按这个来，你可以改但记得全套一起改：

- Qt：`C:\Qt\5.15.2\msvc2019_64`
- 依赖源码：`C:\deps\src`
- 依赖安装前缀：`C:\deps\prefix`
- ADS 安装前缀：`C:\deps\qads`
- 源码：`D:\WorldEditor`

**全程用英文路径，不要带空格和中文**，MSVC 的 rc/nmake 对这些很敏感。

## 1. 安装 Visual Studio 2022

1. 下载 [Visual Studio 2022 Community](https://visualstudio.microsoft.com/zh-hans/downloads/) 并运行安装器。
2. 在「工作负荷」页勾选 **使用 C++ 的桌面开发**。右侧「安装详细信息」确认这几项是勾上的：
   - MSVC v143 - VS 2022 C++ x64/x86 生成工具
   - Windows 11 SDK（或 Windows 10 SDK，随便哪个版本，rc.exe 在这里面）
   - C++ CMake tools for Windows
   - 用于 Windows 的 C++ AddressSanitizer（可选，调试内存问题有用）
3. 「单个组件」里搜一下 **vcpkg**，如果有「vcpkg 包管理器」就一起勾上——没有也没关系，第 3 步会手动 clone。
4. 安装位置保持默认（`C:\Program Files\Microsoft Visual Studio\2022\Community`）。
5. 装完后打开一次「开始菜单 → Visual Studio 2022 → **x64 Native Tools Command Prompt for VS 2022**」。

**后面所有命令行操作都在这个「x64 Native Tools」窗口里做**，不要用普通 cmd——它会把 `cl.exe`、`nmake.exe`、`rc.exe` 的路径都配好。

验证：

```cmd
cl
```

只要看到 `用于 x64 的 Microsoft (R) C/C++ 优化编译器` 的版本信息（哪怕后面跟着"没有源文件"的报错）就说明环境正常。

## 2. 安装 Qt 5.15.2

Qt 5.15.2 是最后一个提供开源版 MSVC 预编译包的版本，正好也是 HiveWE 0.6 那会儿的版本，别用 5.15.3+（那是商业版）也别用 Qt6（代码是按 Qt5 写的）。

### 方式 A：aqtinstall（推荐，无需注册账号）

需要 Python 3。在同一个命令行窗口里：

```cmd
pip install aqtinstall
aqt install-qt windows desktop 5.15.2 win64_msvc2019_64 -O C:\Qt
```

两个实测注意点：

- **不要加清华镜像**（`-b https://mirrors.tuna.tsinghua.edu.cn/qt`）。清华镜像只同步了 `qt/` 下的发行包，没有镜像 aqt 需要的 `online/qtsdkrepository`，会直接报 `Failed to locate XML data for Qt version '5.15.2'`。官方源实测 68 秒装完。
- aqt 会在**当前工作目录**写 `aqtinstall.log`。如果你从 `C:\` 根目录启动，可能因为权限被拒（`PermissionError: C:\aqtinstall.log`），换个目录执行即可。

### 方式 B：官方在线安装器

到 [qt.io](https://www.qt.io/download-qt-installer) 下 `qt-unified-windows-x64-online.exe`，登录账号后勾 **Qt 5.15.2 → MSVC 2019 64-bit**。慢，但省心。

验证：

```cmd
C:\Qt\5.15.2\msvc2019_64\bin\qmake.exe -v
```

应输出 `QMake version 3.1 / Using Qt version 5.15.2`。

## 3. 编译六个依赖库

**为什么放弃 vcpkg**：实测这台机器访问 GitHub 的 release/raw 下载只有 ~20KB/s，vcpkg 光是拉它自带的 cmake 就卡了十几分钟，六个 port 串行下来不可接受（顺带一提，`bootstrap-vcpkg.bat` 拉 exe 直接失败）。但 **git clone 很快**，所以改成拉源码本地编译，统一装到 `C:\deps\prefix`。

拉源码：

```cmd
mkdir C:\deps\src
cd C:\deps\src
git clone --depth 1 https://github.com/g-truc/glm glm
git clone --depth 1 https://github.com/SpartanJ/SOIL2 soil2
git clone --depth 1 https://github.com/ladislav-zezula/StormLib stormlib
git clone --depth 1 https://github.com/ladislav-zezula/CascLib casclib
git clone --depth 1 https://github.com/libjpeg-turbo/libjpeg-turbo jpeg
git clone --depth 1 https://github.com/madler/zlib zlib
git clone --depth 1 https://github.com/bulletphysics/bullet3 bullet3
```

每个库的编译参数都踩过坑，别省：

| 库 | 必须带的参数 | 不带会怎样 |
|---|---|---|
| glm | `-DGLM_BUILD_TESTS=OFF` | 会 FetchContent 去拉 googletest，卡在下载 |
| SOIL2 | 无 | — |
| StormLib | `-DSTORM_USE_BUNDLED_LIBRARIES=ON -DSTORM_UNICODE=ON` | 不带 bundled 会报 `Could NOT find ZLIB`；不带 `STORM_UNICODE` 会让所有 MPQ 地图打不开（见下方 ANSI/Unicode 说明） |
| CascLib | `-DCASC_BUILD_SHARED_LIB=OFF -DCASC_BUILD_STATIC_LIB=ON -DCASC_UNICODE=ON`，且**必须先装好 zlib** | 除了 config 里的 `find_dependency(ZLIB)`，漏掉 `CASC_UNICODE` 会埋一个极难查的运行时坑（见下方说明） |
| libjpeg-turbo | `-DWITH_SIMD=OFF -DENABLE_SHARED=OFF -DENABLE_STATIC=ON` | 没装 nasm 时开 SIMD 会失败 |
| bullet3 | `-DUSE_MSVC_RUNTIME_LIBRARY_DLL=ON -DBUILD_EXTRAS=OFF -DBUILD_BULLET3=OFF -DBUILD_CPU_DEMOS=OFF` | 默认用 `/MT` 静态 CRT，和 Qt 的 `/MD` 冲突，链接时一片 `LNK2038` |

统一套路（zlib 先来，CascLib 要用）：

```cmd
cmake -S C:\deps\src\<名字> -B C:\deps\build\<名字> -G Ninja -DCMAKE_BUILD_TYPE=Release <该库的参数> -DCMAKE_INSTALL_PREFIX=C:/deps/prefix
cmake --build C:\deps\build\<名字>
cmake --install C:\deps\build\<名字>
```

> 用 Ninja 而不是 VS 生成器，是因为 Ninja 不依赖 MSBuild 去找 SDK，行为更可控（正常终端里两种都行）。

**Bullet 只编三个库，别编全部**：`BUILD_EXTRAS` 里有个编译单元会挂住（`cl` 进程 CPU 0 卡十几分钟不动）。所以单独指定 target：

```cmd
cmake --build C:\deps\build\bullet3 --target BulletCollision BulletDynamics LinearMath
cmake --install C:\deps\build\bullet3
```

装完检查：

```cmd
dir C:\deps\prefix\include\turbojpeg.h
dir C:\deps\prefix\include\bullet\btBulletCollisionCommon.h
dir C:\deps\prefix\lib\StormLib.lib
```

### CascLib / StormLib 的 ANSI / Unicode 陷阱（重点）

这两个库都用 `TCHAR`，而 `TCHAR` 由**编译期**的 `UNICODE` 宏决定，可它们导出的函数名**不带 A/W 后缀**（`CascOpenStorage`、`SFileOpenArchive` 都是同一个符号）。

结果就是：库按 ANSI 编译（`TCHAR=char`），调用方按 Unicode 编译（HiveWE 的 CMake 里有 `UNICODE _UNICODE`），链接阶段**完全正常**。但运行时库会把你传的 `wchar_t*` 当成 `char*` 去解析路径，得到一个乱码路径，于是失败。

两个库的症状：

| 库 | 症状 |
|---|---|
| CascLib 编成 ANSI | 启动时 `Error opening "...\Data" with error:2`，然后退出 |
| StormLib 编成 ANSI | 界面能进，但打开任何 MPQ 地图都弹 `Opening the map archive failed. It might be opened in another program.` |

两个都极具误导性：路径肉眼看着是对的，`fs::exists()` 也确认文件确实在那儿。修法是给库加 `-DCASC_UNICODE=ON` / `-DSTORM_UNICODE=ON` 重新编译，和调用方保持一致。**这两个选项默认都是 OFF。**

（想快速判断是不是这个坑：用同一个库写个 `main()`，传 `char*` 路径调用。能成功就说明库是 ANSI 版、和应用不匹配。）

有个容易踩的点：**HiveWE 用的是 turbojpeg 的 API**（`tjInitDecompress`、`tjDecompress2`），不是 libjpeg 的 `jpeg_*`。所以 `find_package(JPEG)` 找到的 `jpeg.lib` 是没用的，必须显式找 `turbojpeg.h` + `turbojpeg.lib`——改好的 CMakeLists 已经这么做了。

## 4. 编译 QScintilla

HiveWE 的 `JassEditor.h` 里有一行 `#define QSCINTILLA_DLL`，意思是**从 DLL 导入符号**，所以 QScintilla 必须编成动态库，不能是静态库。

1. 下载 [QScintilla 2.13.4 源码包](https://www.riverbankcomputing.com/static/Downloads/QScintilla/2.13.4/QScintilla_src-2.13.4.zip)，解压到 `C:\deps\QScintilla_src-2.13.4`。
2. 在 x64 Native Tools 窗口里：

```cmd
cd C:\deps\QScintilla_src-2.13.4\src
C:\Qt\5.15.2\msvc2019_64\bin\qmake.exe qscintilla.pro
nmake
nmake install
```

3. `nmake install` 会把头文件装到 `C:\Qt\5.15.2\msvc2019_64\include\Qsci`，库装到 `lib\qscintilla2_qt5.lib`。

**注意它把 DLL 也装进了 `lib\` 而不是 `bin\`**，手动挪一份：

```cmd
copy C:\Qt\5.15.2\msvc2019_64\lib\qscintilla2_qt5.dll C:\Qt\5.15.2\msvc2019_64\bin\
```

（这一步别省，CMake 里按 `bin\` 找这个 DLL 做自动拷贝，找不到只会给你一条 WARNING，等到运行才崩。）

验证：

```cmd
dir C:\Qt\5.15.2\msvc2019_64\include\Qsci\qsciscintilla.h
dir C:\Qt\5.15.2\msvc2019_64\lib\qscintilla2_qt5.lib
dir C:\Qt\5.15.2\msvc2019_64\bin\qscintilla2_qt5.dll
```

## 5. 编译 Qt Advanced Docking System

HiveWE 的整套可停靠面板都靠它。用 **4.3.1**（5.x 改了导出名；而且 HiveWE 的源码是按 4.x 之前的 API 调的）。

1. 克隆指定 tag：

```cmd
git clone --depth 1 --branch 4.3.1 https://github.com/githubuser0xFFFF/Qt-Advanced-Docking-System C:\deps\qads-src
```

> 注意 tag 是 `4.3.1`，**不带 `v` 前缀**（`v4.3.1` 会 404）。仓库里另有 `v5.0.0`/`v5.1.1` 之类的 tag，别混。

2. 配置并安装：

```cmd
cmake -S C:\deps\qads-src -B C:\deps\qads-build -G "Visual Studio 17 2022" -A x64 -DCMAKE_PREFIX_PATH=C:/Qt/5.15.2/msvc2019_64 -DADS_VERSION=4.3.1 -DBUILD_EXAMPLES=OFF -DBUILD_STATIC=OFF -DCMAKE_INSTALL_PREFIX=C:/deps/qads
cmake --build C:\deps\qads-build --config Release --target install
```

`-DADS_VERSION=4.3.1` 这个参数不能省。它的 `CMakeLists.txt` 默认用 `git describe` 从 git 索引里取版本号，而 `--depth 1` 的浅克隆拿不到 tag，会报：

```
CMake Error at CMakeLists.txt:30 (project):
  VERSION "GIT-NOTFOUND.GIT-NOTFOUND.GIT-NOTFOUND" format invalid.
```

> `cmake` 用 VS 自带的那份就行（`C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe`，在 x64 Native Tools 窗口里已经在 PATH 上）。

3. 验证——**它装出来的包名是 `qt5advanceddocking`，不是 vcpkg 那个 `qt-advanced-docking-system`**：

```cmd
dir C:\deps\qads\lib\cmake\qt5advanceddocking\qt5advanceddockingConfig.cmake
dir C:\deps\qads\bin\qt5advanceddocking.dll
```

导出的 target 叫 `ads::qt5advanceddocking`，头文件在 `include\qt5advanceddocking`。改好的 CMakeLists 两种命名都兼容，所以 `_DIR` 指对就行。

## 6. 配置并编译 HiveWE

我已经把仓库里的 `CMakeLists.txt` 重写过了（可以用 `git diff CMakeLists.txt` 看改动）。原来的版本有这些致命问题：`${JPEG_INCLUDE_DIR}})` 多打一个大括号、`${BULLET3_INCLUDE_DIR}})` 同病而且变量从未定义、`${QSCINTILLA_INCLUDE_DIRS}` 未定义、链接路径全硬编码成 `C:/vcpkg/packages/...`（那是 vcpkg 的构建中间目录，安装完根本不存在）。

现在这样配置：

```cmd
cd D:\WorldEditor
cmake -S . -B build -G "Visual Studio 17 2022" -A x64 -DQt5_DIR=C:/Qt/5.15.2/msvc2019_64/lib/cmake/Qt5 -DQSCINTILLA_ROOT=C:/Qt/5.15.2/msvc2019_64 -Dqt5advanceddocking_DIR=C:/deps/qads/lib/cmake/qt5advanceddocking -DHIVE_DEPS_PREFIX=C:/deps/prefix
```

`HIVE_DEPS_PREFIX` 就是第 3 步的安装前缀，CMakeLists 会优先按 `find_package(CONFIG)` 找依赖，找不到就退回到这个前缀下的 `include/` + `lib/`。

配置成功的标志是最后输出 `-- Configuring done` 和 `-- Generating done`。若中途 `FATAL_ERROR`，错误信息会直接告诉你缺哪个变量该往哪指。

**另外源码有两处必须改**，否则编译不过（改动很小，已在仓库里改好，`git diff` 可查）：

1. `Utilities.h` 里自定义的 `std::hash<QString>` 特化被 `#if QT_VERSION < QT_VERSION_CHECK(5, 15, 0)` 包住了。Qt 5.15 起 `qhashfunctions.h` 自带这个特化，重复定义会报 `error C2766: 显式专用化已定义"std::hash<QString>"`。
2. `Base/Map.cpp:240` 的 `glm::normalize(pos - cpos)` 里 `cpos` 是个从未声明的笔误（全仓库仅此一处），改成 `pos - origin`（`origin` 就是视点）。

然后编译：

```cmd
cmake --build build --config RelWithDebInfo --parallel
```

**不要用 `--config Debug`**：QScintilla 和所有第三方库编的都是 Release，Debug 配置下 MSVC 的 `_ITERATOR_DEBUG_LEVEL` 和运行库不匹配，会报一堆 `LNK2038` / `LNK2019`。要调试就用 RelWithDebInfo（有 `/Zi` 调试信息，能设断点）。真要 Debug，得把 QScintilla 和第三方库全部重编一份 debug 版。

产物：

```
D:\WorldEditor\build\RelWithDebInfo\HiveWE.exe
D:\WorldEditor\build\RelWithDebInfo\Data\      (CMake 构建后自动复制)
```

## 7. 打包运行环境

`windeployqt` 会把 Qt 的 DLL 和 `platforms\qwindows.dll` 插件拷到 exe 旁边。

**注意把它作用到三个二进制上**，不能只给 exe：

```cmd
cd D:\WorldEditor\build\RelWithDebInfo
C:\Qt\5.15.2\msvc2019_64\bin\windeployqt.exe --release --no-translations HiveWE.exe qscintilla2_qt5.dll qt5advanceddocking.dll
```

只分析 `HiveWE.exe` 的话，`qscintilla2_qt5.dll` 自身依赖的 `Qt5PrintSupport.dll` 不会被带出来（QScintilla 链接了 printsupport 模块），启动时会弹「由于找不到 Qt5PrintSupport.dll，无法继续执行代码」。手动补也行：

```cmd
copy C:\Qt\5.15.2\msvc2019_64\bin\Qt5PrintSupport.dll .
```

**跑之前先把 ADS 的 DLL 复制一份到 Qt 的 bin 目录**。`windeployqt` 会假定所有依赖都在 Qt 的 `bin` 里，找不到就直接报错退出、一个 DLL 都不拷：

```cmd
copy C:\deps\qads\bin\qt5advanceddocking.dll C:\Qt\5.15.2\msvc2019_64\bin\
```

`qscintilla2_qt5.dll` 构建时已经自动拷过去了（CMake 里有这条 POST_BUILD），确认一下：

```cmd
dir qscintilla2_qt5.dll
dir qt5advanceddocking.dll
```

最终 exe 目录里应该只有这些外部 DLL：`Qt5Core / Qt5Gui / Qt5Widgets / Qt5Svg / qscintilla2_qt5 / qt5advanceddocking`，加上 `platforms\`、`styles\` 两个插件目录和 `Data\`。StormLib、CascLib、Bullet、SOIL2、turbojpeg 都是静态链进去的，不需要额外 DLL。

## 8. 运行

双击 `HiveWE.exe`。第一次启动有两个前提：

1. **显卡和驱动要支持 OpenGL 4.5 Core**。`main.cpp` 里硬写了 `format.setVersion(4, 5)` + `CoreProfile`，不满足会直接黑屏或秒退。
2. **要指定魔兽争霸 III 的安装目录**（HiveWE 通过 CASC 读游戏数据）。在设置里填 `C:\Program Files (x86)\Warcraft III` 之类的路径，或者打开 `Data\Test Map` 里的测试地图验证。

## 9. 排错速查

| 现象 | 原因 | 处理 |
|---|---|---|
| `找不到 QScintilla` | 没跑 `nmake install`，或 `QSCINTILLA_ROOT` 没传 | 补跑 install，确认 `include\Qsci` 存在 |
| `找不到 Qt Advanced Docking System` | `_DIR` 指错或包名不匹配 | 上游源码版是 `-Dqt5advanceddocking_DIR=.../lib/cmake/qt5advanceddocking`；vcpkg 版才是 `qt-advanced-docking-system_DIR` |
| QADS 配置报 `VERSION "GIT-NOTFOUND..."` | 浅克隆拿不到 tag | 加 `-DADS_VERSION=4.3.1` |
| `bootstrap-vcpkg.bat` 报下载失败 | 连不上 GitHub Releases | 用 VS 自带的 `vcpkg.exe`，并把仓库 checkout 到 2025-11-19 的提交 |
| vcpkg 报 `document schema version 2 is not supported` | 仓库比工具新 | 同上，checkout 匹配的旧提交 |
| aqt 报 `Failed to locate XML data` | 用了清华镜像 | 去掉 `-b`，走官方源 |
| `LNK2019: QsciScintilla::...` | QScintilla 编成了静态库 | 用默认 `qscintilla.pro`（动态），重新 `nmake install` |
| `error C2766: 显式专用化已定义"std::hash<QString>"` | Qt 5.15 自带该特化，源码又定义了一份 | `Utilities.h` 里的特化用 `QT_VERSION` 判断包起来（仓库里已改） |
| `error C2065: "cpos": 未声明的标识符` | 0.6 源码 `Map.cpp:240` 的笔误 | 改成 `pos - origin`（仓库里已改） |
| 依赖 configure 报 `Could NOT find ZLIB` | StormLib/CascLib 要 zlib | StormLib 加 `-DSTORM_USE_BUNDLED_LIBRARIES=ON`；CascLib 先编 zlib |
| `LNK2038: RuntimeLibrary "MT_StaticRelease" 不匹配 "MD_DynamicRelease"` | Bullet 用 `/MT`，Qt 用 `/MD` | Bullet 重编加 `-DUSE_MSVC_RUNTIME_LIBRARY_DLL=ON` |
| Bullet 编译卡住十几分钟、`cl` 进程 CPU 为 0 | `BUILD_EXTRAS` 里某个编译单元挂了 | 只编 `BulletCollision BulletDynamics LinearMath` 三个 target |
| glm configure 卡在下载 | `GLM_BUILD_TESTS` 默认开，要拉 googletest | 加 `-DGLM_BUILD_TESTS=OFF` |
| `windeployqt` 报 `Unable to find dependent libraries ... qt5advanceddocking.dll` | 它只在 Qt 的 `bin` 里找 | 把 ADS 的 DLL 复制到 `C:\Qt\5.15.2\msvc2019_64\bin\` |
| `LNK2019: tjInitDecompress` | turbojpeg 没链上 | 检查 `C:\deps\prefix\lib\turbojpeg-static.lib` 是否存在 |
| `LNK2019: btVector3 / btCollisionWorld` | Bullet 没找到 | 检查 `C:\deps\prefix\include\bullet` 和 `lib\BulletCollision.lib` |
| `rc.exe not found` | VS 安装时没勾 Windows SDK | 打开 VS Installer 补装 Windows SDK |
| `C4819: 该文件包含不能在当前代码页中表示的字符` | 源码含非 ASCII | 新版 CMakeLists 已加 `/utf-8` |
| 启动即崩 / 黑屏 | 显卡不支持 OpenGL 4.5 | 换独显运行，或更新驱动 |
| `找不到 Qt5PrintSupport.dll` | windeployqt 只扫了 exe，没扫 `qscintilla2_qt5.dll` | 从 Qt 的 `bin` 拷过去，或把 windeployqt 作用到三个二进制上 |
| `找不到 Qt5Core.dll` | 没跑 `windeployqt` | 回到第 7 步 |
| `找不到 qscintilla2_qt5.dll` | DLL 没拷到 exe 旁 | 手动从 Qt 的 `bin` 拷过去 |
| 打开地图报错 | 游戏目录没设对 | 设置里重新指定魔兽 III 安装路径 |
| `Opening the map archive failed. It might be opened in another program.` | StormLib 编成了 ANSI 版（`TCHAR` 与应用不一致），也可能是文件真被占用 | 用 `-DSTORM_UNICODE=ON` 重编 StormLib |
| `Error opening "...\Data" with error:2` | ① 1.31 的 `.build.info` 在安装根目录，不在 `Data\` 里；② CascLib 编成了 ANSI 版而应用是 UNICODE | ① 已加回退逻辑；② 用 `-DCASC_UNICODE=ON` 重编 CascLib（这个原因更常见，且链接不会报错） |

## 附 A：想用 VS 的「打开文件夹」方式

命令行配置是确定性的，推荐先用命令行跑通一次。跑通后如果更习惯在 VS 里按 F5 调试，可以打开 `D:\WorldEditor\.vs` 或直接在 VS 里「打开文件夹」，然后在 `CMakeSettings.json` 的每个 configuration 里补上缓存变量：

```json
"cacheVariables": {
  "Qt5_DIR": "C:/Qt/5.15.2/msvc2019_64/lib/cmake/Qt5",
  "QSCINTILLA_ROOT": "C:/Qt/5.15.2/msvc2019_64",
  "qt5advanceddocking_DIR": "C:/deps/qads/lib/cmake/qt5advanceddocking",
  "HIVE_DEPS_PREFIX": "C:/deps/prefix"
}
```

如果照做后仍然报错，把这几个变量用 `-D` 传给命令行版，两边行为就一致了。

## 附 B：改动清单

供审查，也可用 `git checkout -- <文件>` 逐个回退。**本文所有步骤都已在本机实际跑通**，下面这些改动是编译/链接过程中必须的。

### CMakeLists.txt（重写）

1. `cmake_minimum_required` 从 3.13 提到 3.20。
2. 删掉 `find_package(JPEG)`，改成显式找 `turbojpeg.h` / `turbojpeg.lib`——代码调的是 turbojpeg API，`jpeg.lib` 里没有那些符号。
3. 新增 `HIVE_DEPS_PREFIX`（默认 `C:/deps/prefix`）和 `hivewe_import_target()`，每个依赖都先试 `find_package(CONFIG)`，失败再退回前缀下的 `include/` + `lib/`。
4. Bullet 不走 `find_package`：它导出的 `BULLET_INCLUDE_DIRS` 是相对路径 `include/bullet`，会解析到源码目录下，而且导出 target 不带 `INTERFACE_INCLUDE_DIRECTORIES`。改成自己 `find_path` + `find_library`，只链 `BulletCollision + BulletDynamics + LinearMath`（原来链六个，`BulletSoftBody`、`Bullet3Common`、`BulletInverseDynamics` 代码根本没用）。
5. 补上 `QSCINTILLA_INCLUDE_DIR` / `QSCINTILLA_LIBRARY` 的查找（原脚本用的 `QSCINTILLA_INCLUDE_DIRS` 从未被定义）。
6. ADS 同时兼容两套包名：vcpkg 的 `qt-advanced-docking-system` 和上游源码的 `qt5advanceddocking`。
7. 删掉全部硬编码的 `C:/vcpkg/packages/...` 路径。
8. `create_symlink` 改成 `copy_directory`，目的地用 `$<TARGET_FILE_DIR:HiveWE>`。
9. 加 `/utf-8 /bigobj`，POST_BUILD 自动复制 `qscintilla2_qt5.dll` 和 `qt5advanceddocking.dll`。

### Utilities.h（一行改动）

`std::hash<QString>` 特化用 `#if QT_VERSION < QT_VERSION_CHECK(5, 15, 0)` 包起来——Qt 5.15 起 `qhashfunctions.h` 自带这个特化。

### Base/Map.cpp:240（修 bug）

`glm::normalize(pos - cpos)` → `glm::normalize(pos - origin)`。`cpos` 是上游的笔误，全仓库只出现这一次，从未声明过；`origin` 是 `camera->position - camera->direction * camera->distance`，也就是视点位置。

### Base/Map.cpp:277（关掉默认的物理调试图）

`physics.dynamicsWorld->debugDrawWorld()` 和 `physics.draw->render()` 原本是**无条件调用**的，把 Bullet 碰撞体（地形 heightfield、碰撞盒）画成线框，颜色写死在 `Data/Shaders/physics_debug.fs` 里：

```glsl
color = vec4(0, 1, 0, 1);   // 纯绿
```

所以画面上会出现一层绿色网格线。官方 0.6 的发布包里**没有带这两个 shader**，加载失败自然也就画不出来，看起来就没这层线。

现在包进了 `if (render_debug)` 里，默认关闭，和已有的 F3「Debug」开关联动。

### Base/File Formats/CASC.cpp（适配 1.31 及更早的客户端）

`Hierarchy::open_casc()` 固定打开 `<游戏目录>/Data` 作为 CASC 存储根，这是**重制版**的布局（`.build.info` 在 `Data\` 里面）。而 1.31 经典客户端的 `.build.info` 在**安装根目录**，`Data\` 下只有 `config/ data/ indices/`，于是会报：

```
Loading CASC data from: "D:/War3_1.31"
Error opening "D:/War3_1.31\Data" with error:2
```

`CASC::open()` 现在会在打开失败、且上一级目录存在 `.build.info` 时，退到上一级再试一次。CascLib 的文档本来就写的是「storage directory（`.build.info` 所在处）或其子路径」，所以对 1.31 传安装根目录才是正解。

## 附 C：与官方 0.6 发布版的差异

截至当前改动，和官方 `HiveWE.0.6\x64-Release` 对比只剩以下几点，**没有功能性差异**：

| 项 | 官方 0.6 | 本仓库构建 | 影响 |
|---|---|---|---|
| Qt 版本 | 5.12.3 | 5.15.2 | 控件样式、字体度量、高 DPI 行为可能有极细微差别；功能一致 |
| 依赖链接方式 | 动态链 `CascLib.dll` | 全部静态链接（StormLib / CascLib / Bullet / SOIL2 / turbojpeg） | 运行目录更干净，只需 Qt + qscintilla + ADS 两组 DLL |
| 关掉的物理调试图 | 包内缺 `physics_debug` shader，画不出来 | 有 shader，但已挂到 Debug 开关下默认关闭 | 默认画面一致 |
| `Data/Tools/backups/*` | 带了一堆地图备份 | 仓库里没有（`.gitignore` 也忽略了 `backups`） | 无影响 |

如果想要**像素级完全一致**，得把 Qt 降到 5.12.3（`aqt install-qt windows desktop 5.12.3 win64_msvc2017_64`），并用它重编 QScintilla 和 ADS。注意此时 `Utilities.h` 里的 `std::hash<QString>` 保护条件仍然成立（`QT_VERSION < 5.15`），不需要再改代码。

源码层面的改动只有 4 处，见附 B。
