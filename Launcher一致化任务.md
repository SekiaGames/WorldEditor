# 一致化任务：持续循环规程

> 本文自包含。只读这一份就能继续推进，不需要翻历史记录。
> 历史日志在 `Launcher字节一致化路线.md`（按轮次记录，可查不必读）。

---

## 1. 目标

把 `Launcher/` 的对战逻辑与 `test/Open-Warcraft3/` 对齐。

`Launcher` 是客户端的「Godot + 对战」删减版：只保留对战需要的模拟、数值、技能、指令，砍掉编辑器、地图工具、多人联机、完整渲染管线。因此对齐的方向是**单向**的——客户端有而 Launcher 缺的逻辑要补全，Launcher 自己的表现层实现保留。

可度量的两个指标：

| 指标 | 含义 | 当前 |
|---|---|---|
| 字节一致文件数 | 两边同路径 `.cs` 内容完全相同 | 78 / 227 |
| 扫描零错误文件数 | 单独替换进 Launcher 后能编译通过的文件 | 38 / 约 190 |

字节一致是主指标。它是「逻辑同源」最硬的证据，也让后续改动可以直接对照客户端。

---

## 2. 硬约束

每轮结束必须同时满足，否则该轮作废：

1. `dotnet build -t:Rebuild` 零错误、零警告。
2. `--state-probe` 与 `--combat-probe` 全部通过。
3. 工作区 clean（已提交）。
4. 行为优先于字节一致：一个文件字节一致但功能被削弱，作废；反过来可以接受。
5. 回退要彻底：文件、工厂注册、探针改动、新增垫片全部还原，`git status` 干净。

不得为了通过探针而改探针。允许改探针的情形只有一种：探针编码的是 Launcher 旧实现的语义，而客户端实现与数据提供了反证。此时必须同时写清依据（数据列值、客户端注释、实测分布）。

---

## 3. 工具

### 3.1 构建与探针

```bash
cd /Volumes/Data/Work/WorldEditor/Launcher
dotnet build -t:Rebuild -v q --nologo          # 末行是耗时，有错会打印 CSxxxx
"$HOME/Applications/Godot_mono.app/Contents/MacOS/Godot" --path . --headless -- --state-probe
"$HOME/Applications/Godot_mono.app/Contents/MacOS/Godot" --path . --headless -- --combat-probe
```

探针输出 `[自检] 全部通过` 即绿；有 `[校验] FAIL` 行会带期望值和实际值。

### 3.2 扫描：找出「单独替换能编译」的文件

```python
# 保存为 /tmp/scan.py，在 Launcher 目录下运行
import subprocess, shutil, re, json, os
B = '/Volumes/Data/Work/WorldEditor/test/Open-Warcraft3/'
out = {}
for dp, _, fs in os.walk('.'):
    if '/.godot' in dp or '/.git' in dp: continue
    for f in fs:
        if not f.endswith('.cs'): continue
        a = os.path.join(dp, f)[2:]
        b = B + a
        if not os.path.exists(b): continue
        orig = open(a, 'rb').read()
        shutil.copyfile(b, a)
        r = subprocess.run(['dotnet','build','-t:Rebuild','-v','q','--nologo'],
                           capture_output=True, text=True)
        open(a, 'wb').write(orig)
        out[a] = sorted({m.strip()[:100] for _, m in
                         re.findall(r'error (CS\d+): ([^\n]{0,100})', r.stdout)})
json.dump(out, open('/tmp/scan.json','w'), ensure_ascii=False, indent=1)
print('零错误', sum(1 for v in out.values() if not v),
      '单错', sum(1 for v in out.values() if len(v) == 1))
```

约 4~5 分钟。**跑之前必须 `git status` 干净**：未跟踪的坏文件会让每个候选都报同一批错，整轮数据作废（踩过一次）。

### 3.3 批处理：把能过的文件一次收掉

```python
# 保存为 /tmp/batch.py。依赖 3.2 产出的 /tmp/scan.json
import json, shutil, subprocess, os
B = '/Volumes/Data/Work/WorldEditor/test/Open-Warcraft3/'
GODOT = os.path.expanduser('~/Applications/Godot_mono.app/Contents/MacOS/Godot')
d = json.load(open('/tmp/scan.json'))
kept, failed = [], []
for f, v in d.items():
    if v: continue
    b = B + f
    if not os.path.exists(b): continue
    if open(f,'rb').read() == open(b,'rb').read(): continue
    orig = open(f,'rb').read(); shutil.copyfile(b, f)
    r = subprocess.run(['dotnet','build','-t:Rebuild','-v','q','--nologo'],
                       capture_output=True, text=True)
    if 'error CS' in r.stdout:
        open(f,'wb').write(orig); failed.append((f,'build')); continue
    ok = True
    for flag in ('--state-probe','--combat-probe'):
        p = subprocess.run([GODOT,'--path','.','--headless','--',flag],
                           capture_output=True, text=True)
        o = p.stdout + p.stderr
        if 'FAIL' in o or 'Exception' in o:
            ok = False
            line = [l for l in o.splitlines() if 'FAIL' in l][:1]
            failed.append((f, line[0][:70] if line else 'crash')); break
    if ok: kept.append(f)
    else: open(f,'wb').write(orig)
print('KEPT:', kept)
for k, v in failed: print('FAIL', k.split('/')[-1], v)
```

约 0.7 秒/文件。**每修完一处基础设施就重跑一次**，常有碎片被顺手捡回。

### 3.4 定位一个文件的差距

```bash
cd /Volumes/Data/Work/WorldEditor
python3 -c "
import os, difflib
for dp,_,fs in os.walk('Launcher'):
    if '/.godot' in dp or '/.git' in dp: continue
    for f in fs:
        if not f.endswith('.cs'): continue
        p=os.path.join(dp,f); c='test/Open-Warcraft3/'+os.path.relpath(p,'Launcher')
        if not os.path.exists(c): continue
        a=open(p,errors='ignore').read().splitlines(); b=open(c,errors='ignore').read().splitlines()
        if a==b: continue
        sm=difflib.SequenceMatcher(None,a,b)
        ch=sum(max(i2-i1,j2-j1) for tag,i1,i2,j1,j2 in sm.get_opcodes() if tag!='equal')
        if ch<=40: print(ch,p)
"
```

---

## 4. 每轮规程

```
1. 取候选
   a. 若上一轮修过基础设施 → 先跑 3.3 批处理，取 KEPT 结果
   b. 否则从 3.4 取改动行数最少的文件；没有则从 backlog（第 7 节）挑一个子系统的最小切片
2. 尝试采纳
   - 复制客户端文件
   - 跑工厂修正：把 `["Axxx"] = () => new Xxx(),` 改成 `["Axxx"] = WithObjects<RuntimeXxx>(),`
   - 构建。报错 → 判断是缺成员还是缺类型
3. 失败分支
   - 缺成员：补在 Launcher 侧。优先补成通用能力（例如把 `OnAttackHit` 派发移到正确位置），
     拒绝为单个技能加特例
   - 缺类型：看该类型是否是独立文件。若是，照搬；若与别的类型同处一个文件，按整文件评估
   - 探针红：先确认探针走的路径和生产路径一致（见 6.1），再查语义
4. 收尾
   - 通过：保留文件，删除临时调试输出，两探针复跑
   - 不通过：全部回退，记录失败点在本文第 6 节对应条目下
5. 提交
   git commit -m "<中文短句>"，正文写：改了什么、为什么、指标变化
6. 更新指标（第 8 节）与本轮收益
```

### 退出判据

**一轮收益**指满足以下任一条：

- 至少一个文件从「不一致」转入「字节一致」且探针全绿；
- 至少一处被探针验证的行为修复（修复前某断言红、修复后绿），且无回归；
- 新增一条有依据的失效模式（第 6 节），能解释此前无法解释的红断言。

**连续两轮既无上述收益**，停止循环，输出：当前指标、两轮的尝试与失败原因、建议的下一步立项。

单轮内允许反复试错；计入「无收益」的是整轮结果，不是单次尝试。

---

## 5. 当前状态（2026-11-13）

| 项 | 值 |
|---|---|
| 字节一致 | 78 / 227 |
| 扫描零错误 | 78（最近一次全量扫描；每采纳一批会下降，重扫即更新） |
| 扫描单错 | 15 |
| 客户端配置目录类总数 / Launcher 缺失 | 405 / 206 |
| 构建 | 0 错 0 警告 |
| 探针 | 两条全绿 |

差异规模分布（按改动行数）:

| 区间 | 文件数 |
|---|---|
| 1–10 | 1 |
| 11–30 | 5 |
| 31–80 | 30 |
| 81–200 | 77 |
| 200+ | 42 |

含义：**「差一两行」的池子已经空了**。剩下的分歧不是修补，是 Launcher 自己写了一套等价实现，或者能力整块缺失。

---

## 6. 失效模式清单

四十多轮里真正花时间的，全部是这一类：代码在、编译过、探针不红，但功能悄悄不生效。每条都附了识别方法。

### 6.1 探针走的路径 ≠ 生产路径

| 现象 | 根因 | 处理 |
|---|---|---|
| 分裂攻击永不溅射 | 探针直接 `DealDamage`，跳过武器系统的 `ApplyDamagePacketModifiers`，而 `_pendingCleave` 挂在出伤修正里 | 探针按生产顺序：先修正再结算 |
| 命中回调不触发 | `OnAttackHit` 整块被写在 `PassesDefensiveHooks` 的**失败**分支内：未命中会通知命中 | 移到伤害结算之后，按快照遍历 |
| 导弹技能无效果 | 客户端技能发 `AbilityMissileEffect`，命中在若干帧后 | 探针 `Advance()` 后再断言 |

识别：断言的期望值与实际值「差一个量」，且技能参数都读对了。

### 6.2 同名不同义的成员

| 成员 | Launcher 旧语义 | 客户端语义 | 后果 |
|---|---|---|---|
| `World.Tick` | 单帧步长 `0.05` | 单调递增帧号 | `Tick*TickSeconds` 恒为 0.0025，所有按它计时的效果冻结 |
| `Learned` | `Level >= 1` | 技能是否生效 | 挂在技能表里的 Buff 没有等级，被所有遍历跳过 |
| `Classifications` | 只读类型静态 flag | 含运行时状态 | 召唤物不被识别为召唤物 |

识别：某个数值恒为 0 或恒定不变。

### 6.3 生命周期钩子缺失或错位

| 位置 | 问题 |
|---|---|
| `TickAbilities` | 只调单参 `OnTick(float)`，Buff 的倒计时在双参 `OnTick(World, float)`，于是 Buff 永不过期 |
| `AddAbility` | 客户端 Buff 只写 `Duration`，不初始化 `Remaining`，挂上即过期 |
| `OnRemove(World)` | 空实现，`OnBuffRemove` 不触发，状态残留（睡眠的无敌期不过期） |
| `ApplyIncomingModifiers` | `foreach` 遍历中 Buff 移除自己，抛 `Collection was modified` |

处理原则：**Buff 挂在技能表里，所有按技能表遍历的路径都要把 Buff 当一等公民**。目前 `Learned` 已统一口径，遍历改快照，移除延迟到遍历后。

### 6.4 数据层缺列导致「读 0 当合法值」

- `RefreshXxxData()` 只在 `OnLevelUp` 调用：等级设置没走那条路径时全零。
- 适配器 `GetString/GetFloat` 返回 `""` 或 `0`，技能把它当真实值。
- `Units\AbilityBuffData.slk` 只有 9 个结构列（`alias/code/comments/isEffect/version/useInEditor/sort/race/InBeta`），**没有任何玩法字段**。Buff 的 `Magic`/`AutoDispel`/`Positive` 是 `virtual => true` 加子类 override，不要指望从表里读。

识别：技能参数打印出来是 0，而物编里该列有值。

### 6.5 类型耦合：采纳 A 会打断 B

- 火焰吐息点火条件写的是 `victim.FindBuff<HazeBuff>()`（Launcher 自己的类型）。换成客户端 `RuntimeDrunkenHazeBuff` 后，火伤加成与点燃都不再触发。
- 掘尸要 `RuntimeMeatCargoAbility`，Launcher 侧同类概念叫 `RuntimeMeatLoadAbility`。`OfType<T>` 找不到，静默返回。

处理：涉及「被别的技能读取的 debuff」，要么成组采纳，要么先统一交互层（按接口或 Buff id 匹配）。

### 6.7 Buff 在两张表里，谁看得见谁

`World.ApplyBuff` 把 Buff 放进 `unit.Buffs`，客户端技能扫的是 `target.Abilities.OfType<Buff>()`。

根因比「两张表」更具体：`Unit.Abilities` 是字段初始化 `new()`，容器自己的 `Owner` **从来没被赋值**（现成的 `BindAbilityOwner()` 一次都没调用过）。所以任何依赖 `Abilities.Owner` 的逻辑都拿不到主人。

已处理（第 51 轮）：
- `Abilities` 改成惰性构造并绑定 `Owner`；
- 容器的**枚举**在技能之后并入 `Buffs`，`OfType<Buff>()` 因此能看到 A 施加的 Buff；
- tick 走索引器与 `Count`（只覆盖技能），Buff 仍由 `Buffs` 循环计时，不会重复扣时长。

仍未解决：净化能看见 Buff 了（实测 `OfType<Buff>()` 返回 2 个），但驱散后 `BonusDamagePct` 没回零。问题落在「移除时是否按施加路径回退属性」，与可见性无关。

### 6.6 「缺一个类型」往往是缺一整个文件

`RuntimeIllusionModifierBuff` 不在独立文件里，它在 `RuntimeItemIllusionAbility.cs` 内。`RuntimePurgeBuff` 在 `RuntimePurgeAbility.cs` 内。评估成本时按文件算，不按类型算。

---

### 6.8 同名类被覆盖的三种形态

换文件时最容易踩的不是缺成员，是「A 的实现和客户端的实现在同一条路径上」，覆盖后连锁失效。

| 形态 | 例子 | 症状 | 处理 |
|---|---|---|---|
| A 的简化类和客户端类写在同一个文件 | `Mine`、`Silence`、`Purge` | 工厂里 `new Mine()` 找不到类型 | 从 git 取回旧文件，把 A 的类单独落一个文件，再采纳 |
| 两边同名同路径，各写一套 | `RuntimeMirrorImageAbility` | 删掉文件后工厂才报错 | 判断哪边语义更全；不采纳就整文件恢复 |
| A 只定义类型/枚举，客户端写在别的文件里 | `EventTargetKind` | CS0101 重复定义 | 删 A 侧那份，保留客户端所在文件 |

识别方法：复制前先 `grep "class " 目标文件`，看它是否顺带定义了别的东西；再看工厂里 `new X()` 引用的类是否就在该文件中。

## 7. 剩余工作地图

单错文件（18 个）按阻塞原因归类：

| 阻塞 | 文件 | 规模判断 |
|---|---|---|
| 指令队列 `OrderQueue`/`COrder`/`CastOrder` | 游荡、运输、生产队列 | 13 个文件约 2100 行。Launcher 无同名类，纯新增，但要接进单位 tick |
| 事件总线 `World.Events`/`EventContext` | 物品幻象、镜像 | 未评估，幻象已推到只剩这一项 |
| 科技系统 `RefreshUnitTechEffects` | 夜视、升级类被动 | 未评估。客户端 `Ault` 只是标记，实际效果来自科技刷新 |
| 地形 | 荒芜、震荡波 | `World.Terrain` 在 Launcher 侧是空壳 |
| 库存/尸体 | 掘尸 | 依赖 6.5 的类名对齐 |
| 配置装载 | 献祭、季风 | `ObjectDataBuff`、`AbilityTypeData` |

已判断不可行的方向：

- `Weapon`：缺 `StandalonePathing.Combat`，那是 `NativeMirror/Combat/` 整个目录。
- `ChainLightning`：`LastChain` 是 Launcher 自己代码在用（`Lightning.cs`、`Cheats.cs`、探针），替换会打断这些调用点。
- `RallyPointDisplay`：Launcher 版是 124 行静态工具类，客户端是 86 行实例类。
- L4 的 31 个文件（地形/渲染/UI/音频）：走不同的渲染抽象，无对齐可能。

---

## 8. 指标记录

每轮结束时在本节追加一行。只记事实（提交号、指标、收益/无收益）。

| 轮次 | 提交 | 字节一致 | 收益 |
|---|---|---:|---|
| 59 | `605eb36` | 78 | `War3Pathing`（wpm 解码 + `PathingOverlay` + `PathingTextureBits`） |
| 58 | `e5cea01` | 77 | 无收益（寻路可行性调查：不能当切片做） |
| 57 | `e860b4e` | 77 | 无收益（荒芜要真 `Terrain`；`COrder` 依赖 `OpenWC3.Pathing`） |
| 56 | `80c8b18` | 77 | 无一致收益（镜像探针要 `objects`，环境里为 null） |
| 55 | `eac4677` | 77 | 事件系统 5 文件 + 物品幻象 |
| 54 | — | 71 | 无收益（震荡波要 `TerrainGrid`，地形子系统） |
| 53 | `97361ba` | 71 | 魔法免疫（净化带出的 `RuntimePurgeBuff`） |
| 52 | `61d3a85` | 70 | 净化；`RemoveAbility` 两端钩子配对 |
| 51 | `d08ec32` | 69 | 无一致收益（修 `Abilities.Owner` 从未绑定；枚举并入 Buff） |
| 49 | — | 69 | 无收益（净化仍未过：减速读数 0、驱散不生效） |
| 48 | `ef1daa7` | 69 | 沉默；A 的 `Silence` 独立成文件 |
| 47 | `89c1db6` | 68 | 神圣护盾 |
| 46 | `0d050ea` | 67 | `ObjectDataBuff`、`RuntimeObjectNumbers`；`GetBuff`/`CreateBuff` 接通 |
| 45 | `62aa4fc` | 65 | 布雷（AIpm）；`SpawnSummoned`；`Mine` 独立成文件 |
| 44 | — | 64 | 无收益（掘尸组缺 `Amtc` 容量数据，技能表里没有这一行） |
| 43 | `e40294f` | 64 | 破坏；PRD 阈值表；脚本随机流 `Stream(n)` |
| 42 | `138ce2b` | 63 | 分裂攻击；`Unit` 补幻象字段 |
| 41 | `7b0391d` | 62 | 抗性皮肤；`OnAttackHit` 派发位置 |
| 40 | `c9a9d17` | 61 | 批处理器收 4 个；`Get<Hero>()` 常驻视图 |
| 39 | — | 57 | 无收益（撤回「Buff 物编数据是总线」判断） |
| 38 | `37de543` | 57 | `Classifications` 合并运行时分类 |
| 37 | — | 57 | 无收益（三个候选都要拆子系统） |
| 36 | — | 57 | 无收益（扫描被污染文件作废） |
| 35 | `991195c` | 57 | 腐尸群、防御姿态、线型导弹 |
| 34 | — | 54 | 无收益（醉拳打断火焰吐息） |
| 33 | `3be1da6` | 54 | 隐身（出手破隐） |
| 32 | `d3f29ae` | 53 | 火焰雨；`World.Tick` 语义 |
| 31 | `f550b1d` | 52 | 睡眠；Buff 计时/过期/移除三处 |
| 30 | `cc48606` | 51 | `Learned` 统一口径 |
| 29 | `b9d92b5` | 51 | 引导自动开启；Buff 参与 tick |
| 28 | `b3e9a3b` | 50 | 弹道特效管线（4 文件） |
| 27 | `2986412` | 46 | 永久隐身对 |
| 26 | `aa37990` | 44 | 疲惫、龙卷风、反魔法外壳 |

早期轮次见 `Launcher字节一致化路线.md`。

---

## 8.5 第 57 轮结束时的退出判定

第 56、57 两轮连续无收益，按第 4 节的判据**停止循环**。

两轮的尝试与结论：

| 轮 | 尝试 | 卡点 | 性质 |
|---|---|---|---|
| 56 | 镜像分身 | 客户端版本靠 `objects` 创建单位，探针环境为 null | 测试环境未装配数据源，非逻辑差异 |
| 57 | 荒芜蔓延 | `world.Terrain` 在 Launcher 是 `object` | 地形子系统 |
| 57 | `COrder` 家族（330 行） | `using OpenWC3.Pathing`、`StandalonePathing.Unit` | 寻路子系统 |

**当前池子的真实形状**：15 个单错文件全部指向四个子系统，没有一个是补成员能解决的。

| 子系统 | 阻塞的文件 | 依赖链 |
|---|---|---|
| 寻路 `Pathing` | `Weapon`、`War3Pathing`、`COrder` 全家、`CargoTransport` | 最深，`StandalonePathing` 是客户端自己的导航库 |
| 地形 `Terrain` | `BlightGrowth`、`Shockwave` | `TerrainGrid` + 荒芜状态 |
| 指令 `OrderQueue` | `Wander`、`Immolation`（切换）、`CargoTransport` | 与寻路耦合 |
| 配置 `War3TypeData` | `Monsoon` | 276 行的自定义物编模型 |

**建议立项**（按投入产出排序）：

1. **寻路**——它同时卡住指令、武器、搬运三类，是唯一能一次解开多个方向的。客户端那套在 `StandalonePathing` 里，需要先确认是否可以整体移植而不改动 Launcher 的移动循环。
2. **地形**——只卡两个技能，但 `TerrainGrid` 是荒芜、震荡波、地形变形的公共底座。
3. **指令队列**——2100 行，且依赖寻路。寻路不通之前不宜动。

## 8.6 寻路可行性调查（第 58 轮）

结论：**寻路不能当切片做，它是原生镜像的移植**。

| 项 | 数据 |
|---|---|
| 寻路库规模 | `Src/Core/Pathing` 199 个文件、33,279 行 |
| `COrder` 直接引用 | 只有 3 个符号：`NativeOrderConstruction.Configure`、`NativePointOrderConstruction.Configure`、`CAbilityMove.IsFollowRequest` |
| 补完这 3 个之后的连锁 | `NativeListHead`、`CAgentWar3`、`OrderPointData` |
| 再往后 | `CAgent`/`CUnit` 的代理身份模型 |

`COrder` 的形状是「薄数据 + 多态」，订单本身不做事，行为挂在能力上，订单与代理之间靠 `+0x38` 的链表头关联。那个链表头就是 `NativeListHead`，属于 `NativeMirror/Unit`。所以补三个符号只是把依赖往后推一层。

**判断**：要么整体移植 `NativeMirror`（Orders + Unit），要么放弃这批文件。没有中间路径。

**地形同样收尾在原生镜像**：`TerrainGrid.cs`（1004 行）前 940 行能直接用，尾部 60 行要 `StandalonePathing.Core.NativeWpmGrid`、`Unit.NativePathingQueryMask`、`Footprint`。所以地形也不是纯数据模块，它的查询路径挂在原生网格上。第 59 轮只落地了它前面的 `War3Pathing.cs`。

**对剩余 15 个单错的影响**：`Weapon`、`War3Pathing`、`COrder` 全家、`CargoTransport` 全部挂在这条链上，占三分之一。其余三个子系统（地形、指令队列、配置模型）与寻路无直接耦合，但指令队列的执行体依赖 `COrder`。

## 9. 交接检查表

接手（人或 agent）第一件事：

```bash
cd /Volumes/Data/Work/WorldEditor/Launcher
git status --short          # 必须为空
dotnet build -t:Rebuild -v q --nologo | tail -1
dotnet build -t:Rebuild --nologo 2>&1 | grep -c "warning CS"   # 必须 0
"$HOME/Applications/Godot_mono.app/Contents/MacOS/Godot" --path . --headless -- --state-probe | grep -E "全部通过|FAIL"
"$HOME/Applications/Godot_mono.app/Contents/MacOS/Godot" --path . --headless -- --combat-probe | grep -E "全部通过|FAIL"
```

四项都符合预期再开工；有一项不符，先修到符合，别在坏地基上做实验。
