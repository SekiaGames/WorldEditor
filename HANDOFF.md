# 交接：城镇大厅 Stand Work 特效

## 目标
Wiki 还原「城镇大厅训练兵种」特效。用户反馈：游戏里很明显，Wiki 里看不到。

## 已确认（不必重查）
- 资源是对的：`TownHall` 21 个发射器全导出，`Stand Work` 期间 `fx_node68/69` = visible+emitting，scale 10、box 2.5×0.15..15、tex `CloudSingle.blp`
- 客户端同渲染器出图（预热 90 帧）能看到：屋顶右侧一小团浅色雾
- Wiki 45° 视角：两帧差分仅 17px，等于看不见（判断为过小 + 被屋顶遮挡）

## 待做（下一步就干这个）
让客户端真的进入训练态，再出参照图，确定"很明显"的到底是哪一层表现：
1. 在 `War3GameScene` 加 env 钩子 `OPENWC3_AUTO_TRAIN=hpea`：开局 6 秒后对玩家 0 的第一个建筑调
   `_jassRuntime.IssuePlayerTrain(localPlayerId, building, rawcode, out error)`
   （UI 的 `ActivateTrain` 走的就是这一条，见 `War3Game 输入/War3CommandInteractionComponent.Production.cs:22`）
2. 用 `OPENWC3_SHOT_SEQ` 连拍，与不开钩子的同机位做像素差分

## 已知失败路线（别再试）
- 地图 JASS：`GroupEnumUnitsOfPlayer` + `IssueImmediateOrderById(htow,'hpea')` → 脚本执行了但不产生训练，
  日志里没有任何 `hpea`。大概率是 `IssueImmediateOrder` 里 `_objects.Get(typeId).GetString("Trains")` 匹配不上

## 常用命令
```
# 客户端编译（macOS）
bash Mac/build_osx.command 1 < /dev/null
# 客户端单帧出图（第9参=粒子预热帧数，默认90）
OpenWC3.Game --model-shot 'Buildings\Human\TownHall\TownHall.mdx' 'Stand Work' 2000 1000 800 out.png 0 90
# 客户端连拍
OPENWC3_SHOT_SEQ=<绝对前缀> OPENWC3_SHOT_WAIT=12 OPENWC3_SHOT_INTERVAL=3 OPENWC3_SHOT_COUNT=8 OpenWC3.Game <map>
# Wiki 出图 + 粒子状态
WIKI_DEBUG_PARTICLES=1 Godot --path Wiki -- --shot=<abs>.png@units:htow|Stand\ Work@2
# Extract 探针（读 res://，不用打 pck）
PROBE_CLIP="Stand Work" PROBE_TIME=2 PROBE_FORCE=1 PROBE_DIR="0.83,0.24,0.5" PROBE_OUT=<abs>.png \
  Godot --path Extract res://Demo/ParticleProbe.tscn
```

## 环境注意
- Godot 出图必须真实窗口，`--headless` 拿不到画面
- `user://` 在工作区外，跑 Godot 需要非沙箱执行，否则卡在 `Failed to open log file`
- 沙箱里跑完 `--export-pack` 后立刻跑截图容易拿不到 GPU 上下文，先 `pkill -f Godot_mono` 再跑

## 产物状态
- `extract.pck` = 971MB，已含 `assets/common/mesh/particle_quad.tres`、`assets/common/lighting/dnc.json`
- 代码全部未提交
- Wiki 根目录有 2 个未跟踪探针脚本 `_particle_probe.gd`、`_view_probe.gd`，建议删