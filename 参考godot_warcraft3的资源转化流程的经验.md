# 参考 godot_warcraft3 的资源转化流程的经验

分析对象：`godot_warcraft3-master`（Node 工具链 + Godot 运行时）。
本文只记录对 Extract 工程有用的部分：流程、决策、踩过的坑，以及可以直接采纳和不该照搬的地方。

---

## 一、整体路线

```
MPQ ──extract──> BLP ──decode──> PNG ─────────────┐
             └─> MDX ──parse──> 中间表示(IR) ─┬─> X.gltf + X.bin     网格/骨骼/动画
                                            ├─> X.pe2.json          粒子发射器
                                            ├─> X.geosetvis.json    网格可见性
                                            ├─> X.attachments.json  挂点
                                            ├─> X.cameras.json      相机
                                            ├─> X.collision.json    碰撞
                                            └─> X.animkeys.json     补 glTF 装不下的键
                                                        │
                     Godot 原生导入 glTF ────────────────┤
                     GDScript (bake:scn) 读 sidecar ─────┘──> .tscn（场景）
```

关键点：**glTF 只负责它擅长的部分**（几何、骨骼、动画、贴图引用），WC3 特有的东西（粒子、可见性、挂点、相机）全部落到同名 sidecar JSON，由 Godot 侧的 GDScript 在烘焙或加载时组装。转换器因此不需要“什么都能表达”的格式，产物体积小、可 diff、可单独重生成。

Extract 目前是直接从 MDX 生成 `.tscn` + `.tres`，两条路线的差别在第 8 节展开。

---

## 二、产物与路径约定

`tools/asset-convert/src/paths.js` 里一个 `X.mdx` 展开成：

| 产物 | 内容 |
|---|---|
| `X.gltf` / `X.bin` | 网格、骨骼、动画、材质（贴图用外部 URI） |
| `X.pe2.json` | ParticleEmitter2 全参数 |
| `X.geosetvis.json` | geoset 可见性轨 |
| `X.attachments.json` | 挂点名称与位置 |
| `X.cameras.json` | 相机位置与目标轨 |
| `X.collision.json` | 碰撞顶点与 AABB |
| `X.bone_rest.json` | 骨骼静止矩阵 |
| `X.animkeys.json` | glTF 表达不了的采样键 |
| `X.model_ir.json` | 解析后的中间表示 |

命名规则是「主文件改名 + 后缀」，天然可以按模型定位、单独比较、单独重生成。Extract 里对应的信息现在都塞在 `.tscn` 的节点和 AnimationPlayer 轨道里，想单独看一份粒子的参数只能去翻场景文件。

---

## 三、几何

- 每个 geoset 独立成一个 mesh 节点 `Geoset_*`，顶点**保持 model space**（不做 bind 空间变换）。
- UV **不翻 V**：`// Do NOT flip V — war3-model uploads raw TVertices to WebGL/glTF-like V=0 bottom space.`
- 绕序保留原样，配 `(x, z, -y)` 的坐标变换，注释说这在多数单位上是对的。
- `MODEL_SCALE = 0.01`，WC3 单位到米。
- 材质语义编码进**名字**：`_fmN`（filter mode）、`_repN`（replaceable id）。Godot 侧读名字决定加法混合、队伍色、光晕。
- 贴图方案 B（外链）：`// 必须同时 setImage + setURI：仅 URI 会被 writer 丢弃`。理由写在注释里——GLB 内嵌无法跨模型共享同一张 PNG。

---

## 四、骨骼：全部归零 + IBM = 单位阵

`mdx-gltf.js` 里这部分注释密度最高，三条结论值得直接抄：

1. **PivotPoint 是旋转原点**，不是骨骼平移。每个 joint 在父坐标系里的偏移 = 父 Pivot − 子 Pivot。
2. **不要给 joint setTranslation**：`// Godot 若把它收成 bone rest，而顶点已是 model space、IBM=I，蒙皮会再加一遍局部平移，骑士叠进马身。`
3. **IBM 必须是单位阵**：`// pose=I、IBM=inv(bind) 会把绑定姿态顶点减回原点（网格堆成一团）。`

合起来就是：顶点存 model space，joint 放原点，IBM=I，于是“绑定姿态即 MDX 外形”，骨骼动画靠 joint 的 TRS 叠加。

---

## 五、可见性与序列

- `GLTFDocument` 会丢掉 skinned mesh 节点上的 TRS，于是 geoset 显隐写进 `X.geosetvis.json`，烘焙时注入成 `:visible` 轨。glTF 里仍然保留 `Geoset_*` 的 scale 通道，给非 Godot 的消费者用。
- 静止时就该隐藏的 geoset 在导出阶段过滤掉（`// Hide geosets that WC3 keeps invisible at rest (sequence-scoped)`）。
- 运行时按序列名切换：`Wc3Pe2Particles.apply_sequence(root, "Stand")`。

---

## 六、粒子（最有参考价值的一段）

### 6.1 sidecar 字段

`mdx-particles.js` 每个发射器写出：`name / object_id / parent / bone / flags / speed / variation / latitude / gravity / life_span / emission_rate / emission_rate_interpolation / width / length / filter_mode / rows / columns / frame_flags / tail_length / squirt / time_middle / segment_color / alpha / particle_scaling / life_span_uv / decay_uv / tail_uv / tail_decay_uv`。

额外两条：

- `active_sequences`：`null` = 全程发射（火盆），数组 = 只在列出的序列名下发射。判定规则是“区间内任一关键帧 `vis >= 0.5` 且 `rate > 0`”，**采关键帧而不采中点**，注释举例：`// 火枪 Flame 等脉冲只亮几十帧，采中点会漏（active=[]）`。
- `positions_by_sequence`：发射器节点常带 Translation/Rotation 轨（兵营门光在 Stand Work 才挪到门口），按序列把 `W × pivot` 烘成位置表，切动画时改粒子节点位置。绑了骨的发射器直接跟 `BoneAttachment3D` 走。

### 6.2 运行时映射

`wc3_pe2_particles.gd`（896 行）做 PE2 → `GPUParticles3D`：

| WC3 字段 | Godot 实现 |
|---|---|
| `speed`（可负） | `direction = (0, ±1, 0)` + `initial_velocity_min/max` |
| `latitude` | `proc.spread`，另设 `flatness = 0.15` |
| `variation` | 速度上下限取 ±50% |
| `gravity` | `Vector3(0, -g, 0)`（WC3 −Z → glTF −Y） |
| `width/length` 都 < 0.02 | `EMISSION_SHAPE_POINT` |
| 线形发射 | `emission_box_extents = (length/2, 0.01, width/2)` |
| `tail_length` | QuadMesh 宽高比 = `tail_len × s1 / s0`，**夹在 1.8~5.0**，配 `particle_flag_align_y` |
| `frame_flags` 含 Tail | 两个 `draw_pass`，头片/尾片各自一份材质 |
| 三段缩放 | `scale_curve`：`scale_min = max(s0,s1,s2)`，曲线乘 `s1/s0`、`s2/s0` |
| 图集帧 | shader 里按 `age/middle` 算帧号，`UV = (UV + cell) / grid` |
| `filter_mode` | 1 → `blend_add`，4 → `blend_mul`，其余 `blend_mix` |

### 6.3 三个明确的工程结论

1. **材质挂 QuadMesh，不挂 `material_override`**：`// GPUParticles3D 的 material_override 对粒子 billboard 不可靠`。
2. **发射开关交给动画**：`// 有 Visibility/Rate 脉冲：emitting 交给 AnimationPlayer 轨，勿整段强开。` 配套 `_apply_sequence_to_particle()` 处理“序列里本该开、但 t=0 才开闸”的情况。
3. **速率取峰值，时段单独控制**：`emissionRateForAmount()` 对速率轨取 `max`，避免脉冲发射器在某些时刻池子被打空；何时发射交给 `active_sequences` 与 `:emitting` 轨。

---

## 七、增量与可校验

- 逐文件比对：产物齐全 + `mtime >= 源文件` + glTF 头部校验（`isValidGltfOnDisk`，检查是不是含 `"asset"` 的 JSON），全部满足才跳过。注释里还专门处理“上一次写出损坏文件”的情况——删掉重转。
- 原子写：`.tmp → rename`，先写 sidecar，再写 gltf/bin，中途失败清掉本模型的全部产物。
- 有 `bake-task.schema.json`，烘焙任务本身是带 schema 的数据。
- 有独立的 `asset-audit` / `audit-cli`，可以对产物做批量体检。

Extract 现在每次启动都全量重导，中途失败会留下半成品 `.tscn`；这一节的三条都能直接借。

---

## 八、对 Extract 的借鉴清单（按收益排序）

1. **把“发射多大”和“何时发射”拆成两个正交输入**。参考项目用 `emission_rate`（峰值）+ `active_sequences`；我们用 `amount = round(rate × life)` + `KP2V × KP2E` 合并成一条 emitting 轨。后者会把“脉冲期间该喷多少”和“脉冲之外还该不该继续”耦合在一起，调一个就会动另一个。
2. **尾巴长度要设上界**。参考项目夹 `1.8~5.0`，我们的 `tail_extent = speed × TailLength` 无上限，长尾特效（`lightrays` 2340 单位）在近景会铺满屏幕。
3. **`FrameFlags` 按位解释**（bit0 = Head、bit1 = Tail）。参考项目的注释：`// 仅 2/3 或 bit1 当 Tail，避免把 Head-only 的 1 拧成拖尾`。我们目前用 `HeadOrTail != 1 / != 0` 判断，遇到写成 3 的文件会多画一片。
4. **静止隐藏的 geoset 在导出期就过滤掉**，不要留给运行时每帧判。
5. **原子写 + 产物校验**。至少保证一次导出失败不会留下半个 `.tscn`。
6. **按模型的增量转换**。以 `mtime` + 产物齐全度为判据，二次运行能跳过 99% 的文件。
7. **sidecar 化的思路可以只借一部分**：把每个发射器的参数单独落一份 `.pe2.json`，`.tscn` 只引用它。调特效时改数据文件，不用碰场景。

---

## 九、不建议照搬的地方

- **尾巴用拉伸的 QuadMesh + `align_y`**：只能表达“朝一个方向拉长”，同一次发射里不同速度方向的粒子会一起被拉成同向。我们的顶点着色器按 `INSTANCE_CUSTOM` 里的速度方向逐粒子拉，这一条保持现状。
- **运动交给 `ParticleProcessMaterial`**：它的参数是全局均匀的，逐粒子的脉动、重力差异、脉冲节奏表达不了。我们在 process shader 里算速度，自由度更高。
- **用材质名传递语义**（`_fmN` / `_repN`）：改名即失效，也比显式字段更难校验。我们走 `ShaderMaterial` 参数，语义在参数名上，更直白。
- **`gltf` + 外链 PNG**：Godot 导入后会生成 `.import` 与压缩纹理，链路比直接生成 `.tres` 长，且引用了外部 PNG 的场景不方便单文件分发。我们的 `.tres` 直接引用 `Textures/*.png`，够用。

---

## 十、可以直接拿去验证的两条对照实验

1. **脉冲发射器**：拿 `UI\Glues\MainMenu\MainMenu3d_exp` 的 `BlizParticle04`（rate 0.1、15 秒寿命、左上角白烟），按参考项目的“峰值速率 + active_sequences”口径重算池量，再和现在 `amount = round(rate × life)` 的结果比存活曲线。
2. **长尾特效**：对 `BlizParticle01lightrays`（`speed 400 × tail 5.85 = 2340`）套用 1.8~5 倍的宽高比夹取，看近景是否还铺屏。

两条都能用 `TestMdx --dump-particles` 出数值曲线，不需要靠截图判断。
