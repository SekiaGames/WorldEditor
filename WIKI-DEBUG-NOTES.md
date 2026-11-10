# Open-Warcraft3_Wiki 数据加载调试心得

给后来的 Agent：这份文档讲**流程和判定语义**，不讲具体某次改动。客户端工程是纯测试用的，随时可能被重置，所以下面的开关如果不存在了，按「需要时怎么加回来」那节插桩即可。

## 1. 三个工程在一条链上的角色

```
war3.mdx  ──(Extract: ModelBundle/GlbWriter)──>  extract/*.tscn|*.tres  ──(Godot 导出)──>  extract.pck
     │                                                                                        │
     └──(客户端: MdxLoader/MdxParticle2Renderer)── 运行时基准 ────────────────────────────────┴──> Wiki 预览
```

- 基准永远取**客户端**（`/Open-Warcraft3_Wiki`，权威参考在 `/Open-Warcraft3_Wiki` 与 `/test/Open-Warcraft3`）。
- 出问题时不要先看截图，先比**数据**：同一份 MDX、同一个 clip，比「谁开、朝哪、在哪、多大」。

## 2. 客户端的数据通路（按执行顺序）

1. `MdxLoader.Load(byte[])`：按文件里的 chunk 顺序解析，实测 TownHall 的顺序是
   `VERS → MODL → SEQS → GLBS → MTLS → TEXS → GEOS → GEOA → BONE → LITE → ATCH → PIVT → PRE2 → CAMS → EVTS → CLID`。
   **所有 chunk 解析完之后**才 `ResolvePivots(m)`：节点的世界位置来自 `PIVT[objectId]`，`BONE/HELP/PRE2/ATCH/RIBB` 各自的 generic object 里带自己的 TRS 轨迹。
2. `CharacterRenderer`：每帧为每个节点构建世界矩阵
   `BuildLocal = T(-pivot) * S * R * T(pivot + translation)`，再 `local * parentWorld`。
   含义：**旋转/缩放围绕 pivot 发生，节点位置仍等于 pivot + 动画平移**。
3. `MdxParticle2Renderer.UpdateInstance`：每帧对每个发射器算
   `visibility = Evaluate(KP2V, …, fallback=1)`、`rate = Evaluate(KP2E, …, fallback=静态 EmissionRate)`；
   只有 `visibility > 0.01 && rate > 0` 才发射（squirt 发射器用 KP2E 的键值当突发次数）。
4. `MdxParticle2Renderer.Emit`：
   - 位置 = `pivot + (rand(±width/2), rand(±length/2), 0)`（MDX 空间，XY 平面矩形）
   - 方向 = 局部 `+Z` 加 latitude 随机锥；`ModelSpace=false` 时**再过节点矩阵法线变换**
   - 速度/重力在 `ModelSpace=false` 时乘节点缩放
5. 时间轴取值统一走 `MdxTimelineSampling.FindKeys/Evaluate`，语义要点见第 3 节。

## 3. 判定语义（最容易对错的地方）

- **时间轴窗口语义**：`FindKeys` 只承认落在 `[sequenceStart, sequenceEnd]` 内的键；**窗口内一个键都没有时返回 fallback**，不是沿用窗口前最后一个键。
  → 例：`fx_node68` 的 KP2V 键在 61667 和 68333，Stand Work 窗口是 62667..68000，窗口内无键 → 可见性按 fallback=1 处理（即可见）。
- **可见性缺轨迹** = 一直可见（fallback=1），不是不可见。
- **发射率为 0** = 可见但不发射；两者要分开判。
- **坐标系**：客户端是 **Z-up**（相机 `CreateLookAt(..., Vector3.UnitZ)`，重力减在 `.Z`）；Godot 是 Y-up。
  导出换算 `ConvertPosition(x,y,z) = (x, z, -y)`，四元数用 `R * q * R⁻¹`（R 为绕 X 轴 -90°），向量反向换算 `(gx, gy, gz) → (gx, -gz, gy)`。
- **squirt vs 连续发射**：`Squirt != 0` 时 Godot 侧必须写 `one_shot = true` + 默认 `emitting = false`，否则加载瞬间会喷一发。
- **全局序列（GLBS）**：带 `GlobalSequenceId` 的轨道（锯齿、风车、锯末这类循环动画）在客户端是 `frame = globalFrame % period`，**不是**把整段 clip 均分成一个周期。导出时要按 `(clipStart + t) % period` 重采样，否则看着就像“完全不动”。

## 4. 需要时怎么加回调试开关

工程被重置后，按这三处插桩即可恢复（都是 env 开关，默认关闭、零成本）：

1. `MdxLoader.Load` 的 chunk 循环里：打印 `tag/size/文件偏移`；循环结束后打印 summary（sequences/nodes/pivots/emitters/…）与序列表。→ `OPENWC3_TRACE_LOAD=1`
2. `MdxParticle2Renderer.UpdateInstance` 里 visibility 算完之后：按 `(序列名, 帧/步长)` 打印每个发射器的 `vis / rate / on / 节点世界位置 / 存活粒子数`。→ `OPENWC3_TRACE_RENDER=<clip>` + `OPENWC3_TRACE_EMITTERS=<id,id>` + `OPENWC3_TRACE_STEP=<ms>`
3. `MdxParticle2Renderer.Emit` 末尾：打印前 N 次的 `pos / dir / speed / gravity / modelSpace`。→ `OPENWC3_TRACE_EMIT=1` + `OPENWC3_TRACE_EMIT_LIMIT=<n>`

另外保留一个纯数据转储入口（建议以 `--mdx-particles <mdx|目录>` 形式挂在 `Program.Main` 上）：

- `<mdx>`：打印 sequence 表 + 每个 PRE2 的 pivot/flags/width/length/speed/latitude/gravity/life/rate/颜色/alpha/纹理，以及各条轨迹的键值与节点 TRS 轨。
- `<目录>`：递归扫描，只输出「粒子节点带平移或缩放轨」的发射器（`transKeys/scaleKeys/rotKeys/visibleClips`），用来找导出端还缺哪些通道。

## 5. 一轮对齐验收怎么做

1. 客户端取真相：
   ```
   OPENWC3_TRACE_RENDER="Stand Work" OPENWC3_TRACE_EMITTERS=68,69 OPENWC3_TRACE_EMIT=1 \
     OpenWC3.Game --model-shot 'Buildings\Human\TownHall\TownHall.mdx' 'Stand Work' 64000 1000 800 out.png 0 24
   ```
   注意第 3 个参数是**绝对帧（毫秒）**，必须落在该 clip 的区间内，否则时间轴会走 fallback，结论会错。
2. 导出端取产物：读 `Extract/extract/<模型>/*.tres` 里的 `fx_nodeNN:visible|emitting|rotation` 轨与 `*.tscn` 的 `transform`，按第 3 节语义换算回 MDX 空间。
3. 比三样东西：**on/off 集合**（逐 clip × 逐发射器）、**方向矢量**（客户端 `dir` vs Godot `basis * (0,1,0)` 换算回 MDX）、**位置**（pivot + 动画平移）。
4. 三者都对上，才算 Wiki 侧数据一致；对不上就回第 2 节看是哪一层丢的。

## 6. Wiki 侧怎么出图核对

```
WIKI_DEBUG_PARTICLES=1 Godot --path Wiki -- --shot=/abs/out.png@units:htow|Stand\ Work@2
```

- `@` 前面是导航（`units:htow` / 模型库路径），`|` 后是 clip 名，再 `@` 是秒数。
- `WIKI_DEBUG_PARTICLES=1` 会打印每个 `fx_nodeNN` 的 `visible/emitting/amount/ratio/scale/shape`，直接和客户端第 2 步的数值对照。
- Wiki 只读根目录 `extract.pck`（`ProjectSettings.LoadResourcePack("../extract.pck")`），**不读 `Extract/` 目录**；改完导出必须重新打包才能看到。

## 7. 已知的导出通道覆盖

| MDX 通道 | 导出到 Godot | 说明 |
|---|---|---|
| KP2V 可见性 | `fx_nodeNN:visible` 轨 | 每个 clip 一条，窗口内无键时用 fallback=可见 |
| KP2E 发射率 | `fx_nodeNN:emitting` 轨 | 值 0 时关闭 |
| KGRT 节点旋转 | `.tscn` 静态基 + `fx_nodeNN:rotation` 轨 | 影响发射方向与发射面朝向 |
| KGTR 节点平移 | `.tscn` 静态基 + `fx_nodeNN:position` 轨 | 值 = pivot + translation（转换到 Y-up 后） |
| KGSC 节点缩放 | `.tscn` 静态基 + `fx_nodeNN:scale` 轨 | 注意粒子着色器只取矩阵平移，尺寸不会跟缩放走 |
| KP2S/R/L/G/N/W 等 | 逐 clip 采样后写入材质 | 速度/变化/纬度/重力/长宽 |
| TXAN 贴图动画 | 材质 `uv_offset/uv_scale/uv_rotation`（第二层用 `detail_uv_*`）+ 每 clip 一条 value 轨 | 路径 `geosetN:surface_material_override/0:shader_parameter/uv_offset`；材质需 `resource_local_to_scene = true` 才能逐实例动画 |
| GEOA 颜色（KGAC） | 材质 `geoset_tint` + 每 clip 一条 value 轨 | 仅 `flags & 2` 时才动；窗口内无键按客户端语义取 0（黑），有静态色则取静态色 |
| EVTS 事件 | 写进 `.wc3.json` 的 `eventObjects[].frames` | 格式是 `KEVT, count, globalSeqId, frame[count]`，**没有名字字段**；Wiki 目前不消费 |

两个与节点动画相关的开关必须成对：

- `local_coords = (emitter.ModelSpace)`：非 modelSpace 发射器在客户端是“发射瞬间就固定在世界空间”，粒子不跟随节点；写成 `true` 会让整团粒子被移动的节点拖着走。
- 节点 transform 的权威定义是客户端的 `T(-pivot)*S*R*T(pivot+translation)`：它的**矩阵平移列**等于 `pivot + translation - pivot*R`，而**粒子发射中心**等于 `pivot + translation`（发射点先减 pivot 再加回来）。核对位置时要用发射中心，不要用矩阵平移列。
## 8. 全量覆盖审计（先跑这个，别一个个撞问题）

不要靠“打开某个模型发现不对”来推进。用两条命令把 3264 个 MDX 的**数据结构**和**导出产物**对一遍：

```
# 1) 客户端读出每个模型的通道清单（seq/gseq/骨骼轨/发射器/ribbon/灯光/挂点/事件/相机/材质…）
OpenWC3.Game --mdx-inventory test/war3-unpack > inventory.txt

# 2) 和 Extract 产物逐模型比对，只输出不一致项
python3 audit_mdx_coverage.py 5
```

审计项与判定口径：

| 审计项 | 口径 |
|---|---|
| `clip-count-mismatch` | 该序列没有任何轨道时不写 clip，属正常；若模型有轨道却缺 clip 才是问题 |
| `bone-track-missing` | 只统计“关键帧落在某个 clip 窗口内、或全局序列 id 合法”的骨骼；全局序列周期为 0 的算静止，不算缺失 |
| `global-bone-not-resampled` | 有合法全局序列的骨骼必须在某个 clip 里出现密集采样键（否则就是循环没导出） |
| `uv-animation-missing` | TXAN 动画的材质必须带 `uv_offset` 参数 |
| `geoset-tint-missing` | GEOA 颜色（flags&2）必须至少有一条 `geoset_tint` 轨 |
| `camera-not-exported` | CAMS 未导出；客户端用它做「肖像/电影镜头」，预览取景可对齐 |
| `event-frames-missing` | EVTS 的关键帧必须写进 `.wc3.json` 的 `frames` |

改完导出后先跑审计，再重打 `extract.pck`，最后才去 Wiki 里看效果。
