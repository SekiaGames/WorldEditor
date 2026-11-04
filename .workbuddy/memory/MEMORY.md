# 项目长期约定（HiveWE 0.6 修复与改造）

## 协作方式（用户明确要求，务必遵守）

- **先说明问题与证据，不主动改代码、不主动提交。**
- 只在用户明确要求时才改代码；改完也不要 commit，交给用户决定。
- 大范围行为变更（例如多语言字符串同步策略）先给 A/B/C 方案和代价，由用户选。
- 用可验证的数据定位问题（读文件、加日志、跑工具），不要靠猜。曾经因为连续猜错被明确批评过。

## 已确认的环境事实

- `build.bat` 走 Ninja，产物在 `build\HiveWE.exe`；不调用 `vcvars64.bat`（受限环境里 reg.exe 被拦）。
- 安全策略禁用 `reg.exe`、`Add-Type`、`Assembly.LoadWithPartialName` → 助手**无法截图验证 UI**，UI 改动必须由用户确认。
- 验证 exe 是否含新代码：用 Python 按字节搜索字符串，比 grep 可靠。

## 已修复的问题（按时间）

| 提交 | 内容 |
|---|---|
| `efecc98` | Export MPQ 闪退（未初始化 HANDLE + save 把目标变成目录） |
| `d2aa99f` | Map::save 全程 try/catch + 步骤日志（%TEMP%\hivewe_save.log） |
| `f5e09aa` | 保存时保留原始 war3map.j，只有触发器改过才重新生成 |
| `4ba7d79` | 地形碰撞面与渲染面不一致（笔刷偏离光标），改用 ground_corner_heights |
| `3e14eda` | build.bat 改 Ninja，绕开 vcvars |
| `5dbcbf0` | File 下拉菜单改为常规页签 |
| `6a60b27` | 页签重排为 File / View / Tools / Window |
| `77b78eb` | 地图名同步到 _Locales 语言文件（等待用户确认方案，可能需要回退） |

## 已知但未处理

- 脚本生成器系统性残缺：可破坏物/物品创建函数生成为空、.doo 字段错位、SetTeams 与官方不一致。
- Save Map 曾以 0xc0000409 崩溃，已加兜底与日志，尚未复现定位。
- `_Locales\<lang>.w3mod\war3map.wts` 优先于根 war3map.wts，而编辑器只写根表（当前 `77b78eb` 是同步全部语言，策略待定）。
