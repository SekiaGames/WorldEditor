# 备忘录


Wiki方向
网页版化，先做分析

模型里的灯光问题


自定义光照收集系统，模型上Node3D节点表示光源，metadata表示参数。
  运行时脚本收集光源并传递参数给shader，不走Godot的光照体系。



# 待办

> 2026-11-09 从 Wiki(Godot) 实机验收整理。顺序：Extract 侧 → Wiki 侧。
> 已完成：`extract.pck` 的 `include_filter` 补上 `assets/*`，shader 与 fallback 贴图已进包；4 个页面 0 报错，随机抽 25 个模型 25/25 加载通过。

## P0 Extract：geoset 可见性（挡住建筑/技能模型预览）

- [ ] `ModelBundle` 导出时把 GEOA 的 `KGAO` alpha 轨道带进 `.wc3.json`（现在 `geosetAnimations` 只有 `staticAlpha`，`tracks` 为空）。
- [ ] 或按 `Stand` 首帧预计算每个 geoset 的 alpha，`alpha < 0.01` 时给节点写 `visible = false`。
- 现状：`TownHall` 36 个 geoset、`AltarOfKings` 13 个、`BlizzardTarget` 3 个，`visible=false` 全是 0；全库 4041 个 `.tscn` 只有 648 个带 `visible=false`。
- 影响：施工架、升级形态（Keep/Castle）、地面 decal 叠在一起；技能模型被超大面片拉远取景，实际只剩一个小环（见 `BlizzardTarget`）。

## P1 Extract：其他

- [ ] 表/文本文件名大小写不统一：`Tables/UnitsunitUI.json`、`UnitsUnitUI` 混用，建议统一（Wiki 已改为后缀匹配，其他消费方仍会踩）。
- [ ] `--table-export` / `--text-export` 漏传 `--game-dir` 时退出码仍是 0、输出 0 张表，建议直接报错退出。
- [ ] （暂缓）765 个 `*.fx*.tres` 指向 `assets/fallback/missing_texture.png`，对应 blp 其实都已转成 png，粒子贴图解析待查。

## P2 Wiki：可看性

- [ ] geoset 面板：逐个显隐 + 顶点数/尺寸/贴图名；默认自动隐藏「顶点 ≤ 8 且最大边 > 中位面片 2.2 倍」的面片；取景只算合理面片。
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