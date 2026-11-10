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




# 关于动画
城镇大厅（htow/hkee/hcas 共用同一个 TownHall.mdx）的表现可以按生命周期拆成 5 段，Wiki 逐段对照如下。
一、游戏里它到底演什么
模型自带 14 条序列（同一份 mdx 装了三档形态）：
序列	区间	对应游戏行为
Birth	0–60s	农民建造过程，从脚手架逐层长出
Stand	0.33s	一级待机（钟摆/旗帜靠全局序列 gsid 0–5 驱动）
Stand Work	5.33s	训练兵种时循环播放
Birth Upgrade First/Second	各 60s	升级到要塞 / 城堡的过程
Stand Upgrade First/Second	0.17 / 0.33s	二级 / 三级待机
Stand Work Upgrade First/Second	2 / 2s	二级 / 三级训练中
Death / Decay	3.17s / 60s	被摧毁 → 腐烂消散
运行时衔接（客户端 War3GameScene/Animation.cs）：
ConstructionProgress != null → SeekBirth(percent/100)：按建造百分比 seek Birth，不是按时间播。htow 实际建造 180 秒（bldtm=180），模型 Birth 只有 60 秒，所以是 3 倍慢放
RuntimeProductionQueueAbility.Count > 0 → Stand + Work 标签，即 Stand Work 循环
升级：JobKind.BuildingUpgrade → 完成后 FinishBuildingUpgrade 换模板（HP 1500→2000→2500、多一个 Amic 民兵动员、pathing mask 换档）
死亡：Death → Decay，并在 frame 96 触发事件对象 SNDxDHLB
除动画以外还有 7 样东西：
项	数据
脚手架 geoset	36 个 geoset 里 Doodads0 那组 8 个是施工架
附着点	8 个：Overhead Ref(48)、Origin Ref(49)、Sprite First~Fifth(50–54)、Sprite RallyPoint(53)
灯	Omni01 静态强度 20、衰减 80–200，KLAV 只在死亡区段两帧亮
建筑阴影	三档不同贴图：ShadowTownhall / ShadowKeep / ShadowCastle
地面底座	GroundSplat（常驻贴地底座 + Start→Middle→End 三键颜色）
队伍色	屋顶/门帘
材质透明度动画	mat2(geoset3) 3 键、mat7(geoset34) 10 键 KMTA
二、Wiki 现在的对照结果
能演的（我实拍确认）：
表现	Wiki 做法	证据
建造过程	下拉选 Birth，拖进度条到 30s → 脚手架 + 半截木梁 + 半截墙，和游戏里一致	birth50.png
待机 / 训练中 / 死亡	14 条序列全在动画列表里，随便切	Stand、Death@2s 截图
三档形态	一级 / 二级 / 三级按钮 + 升级动画按钮	已验过可见 geoset 集合 {11,13,14}/{15,16,17}/{0,32,33}
队伍色 / 粒子	工具栏切换	阶段 1 补齐的粒子已生效
缺的（按缺口大小排）：
#	表现	现状	缺在哪个工程
1	地面底座 GroundSplat	完全没画，建筑"浮"在纯黑背景上	Wiki（Extract 侧不需要，读渲染配置即可）
2	建筑阴影	ModelPreview 里写死 ShadowEnabled = false，三档 Shadow* 贴图一张没用	Wiki
3	建筑的技能/生产面板	点「技能」显示"没有英雄技能 —— 建筑没有登记 heroAbilList"，而 htow 的 abilList=Abdl,Argl、hkee/hcas 还多 Amic	Wiki（表里已有数据）
4	建造进度绑定	只能手动拖进度条；没有"进度% → Birth 帧"的输入，也没有 180s/140s 的时长映射	Wiki
5	材质透明度动画 KMTA	.matN.tres 只写静态 layer_alpha，geoset3/geoset34 的淡入淡出丢了	Extract（要写轨道）
6	事件音效 KEVT	Wiki 有音效列表但不在动画帧触发；客户端 MdxEventObject 只存帧号不存 soundId，wc3.json 里 soundId 还是被 float 化的整数（1.7e34）	Extract + Wiki + 客户端三方
7	附着点 8 个	Wiki 完全不显示，无法核对头顶特效/施法点/集结旗位置	Wiki
8	选中圈	无	Wiki
三、两个反直觉的结论
Wiki 在三档形态上比客户端还全。客户端全项目只有一条注释提到 Birth Upgrade First/Second，没有任何地方切到 Stand Upgrade First/Second —— 也就是说客户端里把城镇大厅升到城堡，外观仍是市政厅（因为永远播 Stand，而 Stand 的 visible 轨道只显示一级 geoset）。这块不能拿客户端当基准，要以 mdx + 表为准。
"训练兵种"在模型层面只有一个开关：Stand Work 序列。Wiki 缺的不是模型能力，是触发条件（队列非空）。同理"建造"缺的只是"百分比 → Birth 帧"的映射，两者都是十几行的绑定工作，不是资产工作。
四、建议顺序
P0（半天内能补完，观感提升最大）：地基 GroundSplat → 建筑阴影 → 建筑技能/生产面板
P1：建造进度滑杆（0–100% → Birth seek）、训练中开关、KMTA 材质透明度（Extract 侧）
P2：附着点可视化、选中圈、KEVT 帧事件音效没有改任何代码。需要我先把 P0 里的哪一项做出来，或者先补哪个工程的？

