# HiveWE

一个《魔兽争霸 III》世界编辑器。HiveWE 专注于速度与易用性，尤其是在大型地图上——原版世界编辑器在这些场景里往往又慢又笨重。[Hive 论坛讨论帖](https://www.hiveworkshop.com/threads/introducing-hivewe.303183/)

![HiveWE 截图](http://g2f.nl/0qx1hh2)

## 功能特性

- 直接编辑寻路图（Pathing Map）
![编辑寻路图](http://g2f.nl/0bgv29i)
- 编辑地形
![编辑地形](http://g2f.nl/0nfvw4c)
- 编辑 JASS，支持语法高亮、多标签页、代码补全等功能
![Jass 编辑器](http://g2f.nl/0jb8j8t)
- 编辑全局地块寻路
![编辑全局地块寻路](http://g2f.nl/0202154)

## 下载

二进制版本请见 [releases 页面](https://github.com/stijnherfst/HiveWE/releases)。

## 构建说明

以下内容面向希望从源码自行构建 HiveWE 的用户。如果遇到任何问题，欢迎联系我。

1. 下载 [vcpkg](https://github.com/microsoft/vcpkg)。最省事的做法是把它放在 C 盘根目录：`C:/vcpkg`

2. 运行 `bootstrap-vcpkg.bat`（Linux 下运行 `bootstrap-vcpkg.sh`）

3.
	3.1 把 `vcpkg.exe` 加入 Windows 环境变量 PATH
	或者
	3.2 打开 CMD，切换到包含 `vcpkg.exe` 的目录

4. 运行以下命令安装依赖

```bash
vcpkg install glm:x64-windows
vcpkg install soil2:x64-windows
vcpkg install stormlib:x64-windows
vcpkg install casclib:x64-windows
vcpkg install libjpeg-turbo:x64-windows
vcpkg install qt-advanced-docking-system:x64-windows
vcpkg install qscintilla:x64-windows
```

视机器性能而定，这一步可能需要一段时间（约 20 分钟）。

如果 vcpkg 放在了根目录，那现在就可以开始编译 HiveWE 了。你可以二选一：

- 用 CMake 为你的 IDE 生成工程文件

或者

- 以管理员身份打开 Visual Studio，通过"打开文件夹"按钮打开 HiveWE 目录。（在 Windows 上创建符号链接需要管理员权限）

**完成**

如果你没有把 vcpkg 放在 `C:/` 下，就需要打开 `CMakeLists.txt` 和 `CMakeSettings.json`，把其中所有 `C:/vcpkg` 路径改成你实际的 vcpkg 位置。

**完成**

如果遇到任何问题，随时联系我。

## 可以参与贡献的方向

想参与 HiveWE 的开发吗？下面列了一些你可以实现的功能。你可以从中挑一个，也可以添加任何你认为 HiveWE 应该具备的功能。任何形式的贡献都欢迎！

- 支持修改势力/队伍
- 支持修改地图尺寸/相机边界
- 用地形调色板编辑斜坡
- 或者任何你觉得很酷的其他功能

如有疑问，别客气，可以在这里、HiveWorkshop 论坛（eejin）或 Discord `eejin#4240` 给我留言。
