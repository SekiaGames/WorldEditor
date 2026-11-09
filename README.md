# 备忘录


Wiki方向
网页版化，先做分析

模型里的灯光问题


自定义光照收集系统，模型上Node3D节点表示光源，metadata表示参数。
  运行时脚本收集光源并传递参数给shader，不走Godot的光照体系。



# 待办

> 2026-11-09 从 Wiki(Godot) 实机验收整理。顺序：Extract 侧 → Wiki 侧。

**已完成**

- `extract.pck` 的 `include_filter` 补上 `assets/*`，shader 与 fallback 贴图已进包；4 个页面 0 报错，随机抽 25 个模型 25/25 加载通过。
- **P0 geoset 可见性**：改为「tscn 静态默认值 + 动画 `:visible` 轨道」承载（`wc3.json` 不塞 KGAO）。
  `TownHall` 36 个 geoset / 隐藏 20、`AltarOfKings` 13 / 10、`BlizzardTarget` 3 / 2；全库 787 个 tscn 带 `visible=false`。
  动画文件里有 `tracks/N/path = NodePath("Model/Skeleton3D/geoset0:visible")`。实测城镇大厅、国王祭坛的施工架/升级形态消失。

## P1 Extract：其他

- [ ] 表/文本文件名大小写不统一：`Tables/UnitsunitUI.json`、`UnitsUnitUI` 混用，建议统一（Wiki 已改为后缀匹配，其他消费方仍会踩）。
- [ ] `--table-export` / `--text-export` 漏传 `--game-dir` 时退出码仍是 0、输出 0 张表，建议直接报错退出。
- [ ] （暂缓）765 个 `*.fx*.tres` 指向 `assets/fallback/missing_texture.png`，对应 blp 其实都已转成 png，粒子贴图解析待查。

## P2 Wiki：可看性

- [ ] 取景改用「主体网格」：`BlizzardTarget` 的 `Birth` 里 geoset 从 y=26 铺到 y=991，按整体包围盒算距离导致主体只剩一个小环；建筑也偏小（距离公式里 `+ size.Z * 0.5` 太大）。
- [ ] geoset 面板：逐个显隐 + 顶点数/尺寸/贴图名，方便对照动画轨道带来的变化。
- [ ] 无动画的模型隐藏动画工具栏（现在显示「0 条动画」+ 空下拉）。
- [ ] 音效单实例播放，切条目/切页即停。
- [ ] 键盘 ← → 切换条目、Esc 返回列表。
- [ ] 深链 `#/units/Hmkg/skills` + 记住上次分类与条目。

## P3 Wiki：数据与性能

- [ ] 可破坏物 / 物品 / 升级 / 表格 / 文本 五个占位分类接入。
- [ ] 技能详情接 `Abilities/` 模型 + `Animnames` 施法动画联动。
- [ ] 启动 3.5s → 1.5s 内：文本表按分类懒加载（单位页只读 `UnitStrings/UnitFunc`）。
- [ ] 贴图页缩略图分批加载 + LRU 上限；模型库卡片 3D 缩略图队列。
- [ ] `--audit`：遍历 3264 个模型输出 json（加载失败 / 材质缺失 / 动画数 / 可见 geoset 数），当回归基线。

## 备忘

- 自定义光照：Node3D + metadata，运行时收集后传数据纹理给 shader，不走 Godot 光照体系。Wiki 侧尚未实现光源收集与上传。