# 把字节一致的文件从 4 个提到 150 个：路线

> 方法：把 191 个同路径文件逐个替换成客户端版本，全量重编，收集编译器错误，还原。
> 数据：Launcher `09e1dec`，客户端 `14b62fd5`，2026-11-12。
> 原始结果：`/tmp/adopt_raw.json`（191 条，每条是完整错误列表）。

---

## 0. 结论

现在 4 个文件字节一致，全部是纯枚举。剩下 187 个无法照搬，原因不是"写得不一样"，是**基类 API 不一样**：客户端技能类里的 `override`，在 Launcher 的 `Ability`/`Buff` 上没有对应的虚成员。

编译器给了精确账单：187 个文件一共 8,652 条错误，其中

| 错误码 | 含义 | 条数 | 占比 |
|---|---|---|---|
| CS0246 | 找不到类型（`Vec2`、`TargetFlags`…） | 4,216 | 49% |
| CS0115 | 没有可重写的成员（虚成员缺失） | 2,846 | 33% |
| CS0534 | 未实现继承的抽象成员 | 510 | 6% |
| CS0506 | 成员存在但不是虚的 | 288 | 3% |
| CS1061 | 类型上没有该成员（`Owner.Health`…） | 282 | 3% |
| CS0103 | 名称不存在 | 246 | 3% |
| CS0544 | 用属性重写非属性 | 108 | 1% |

**天花板 159 个**：Src 26 + L3 133。L4 的 31 个文件（地形/渲染/UI/音频）走 `IRenderDevice`/Silk.NET/Godot 三条不同的渲染抽象，没有对齐可能；L1 那 1 个（`War3Pathing`）依赖客户端的 TGA footprint 解码链。

---

## 0.5 第一轮执行结果（2026-11-12）

阶段 1 已落地（`7b8a4b3`、`745a0d5`）：`Vec2`、`IRuntimeObjectData`、`AbilityTypeObjectData` 适配器、`Ability`/`Buff` 的 29 个客户端可重写点、`TryCast` 的 `OnEffect` 分派与生命周期钩子。

| 指标 | 执行前 | 执行后 |
|---|---|---|
| 字节一致 | 4 | 6（多 `Vec2`、`IRuntimeObjectData`，这两个是新增文件，不计入 191） |
| 191 对中编译可换 | 4 | 7 |
| 编译错误总数 | 8,652 | 6,416（−26%） |
| CS0115 缺虚成员 | 2,846 | 1,830 |
| CS0246 缺类型 | 4,216 | 2,372 |

**但这 3 个新可编译的文件一个都没换**，原因值得记下来：

| 文件 | 客户端版本是什么 | 直接换的后果 |
|---|---|---|
| `RuntimeUltravisionAbility` | 标记类，注释明写"夜视由 RefreshUnitTechEffects 给" | Launcher 没有那条科技刷新路径，夜视直接丢 |
| `RuntimeImpalingBoltAbility` | 标记类，行为在 `Repb` 的 `rasd/renw` 武器科技里 | Launcher 没实现 rasd，穿刺武器失去配置 |
| `RuntimeStormHammersAbility` | 派生自 `RuntimeStormBoltAbility` 的空壳 | Launcher 的实现在自己那份里 |

实测证据：换掉 `RuntimeUltravisionAbility` 后 `--state-probe` 的「夜视生效」「物品夜视装备后生效」两项立刻失败，已回退并写进提交信息。

结论：**编译可换不等于行为可换**。客户端把一批技能做成"标记类 + 行为沉在科技/物编刷新里"，Launcher 把行为直接写在技能类里。这类文件换过来会静默丢功能，只能等对应管线（科技 effect、物编覆盖）补齐后再换。

下一批要动的是 `Unit` 侧：CS1061 从 282 涨到 476，主要是 `Owner.Health`（客户端把伤害/治疗/闪避做成组件，Launcher 是字段 + World 方法）。补 `Health` 门面之前，182 个技能文件里绝大多数连编译都过不去。

### 第二轮（`bc4a744`）

补 `Unit.Pos`(Vec2)/`Life`/`MaxLife`/`PlayerId`/`Removed`、`Health` 门面、`AbilityContainer`（`Items`/`Get`/`OfType`/`Contains`/`Snapshot`），`Unit.Abilities` 从 `List<Ability>` 换成容器但 58 处调用零改动；`TargetKind`/`TargetRelation` 拆到独立文件，`TargetFlags.cs` 整份照搬。

| 指标 | 第一轮后 | 第二轮后 |
|---|---|---|
| 191 对中字节一致 | 4 | **5**（多 `TargetFlags`） |
| 镜像文件总数 | 6 | **7**（+`TargetFlags`） |
| 191 对中编译可换 | 7 | **9** |
| 编译错误总数 | 6,416 | 6,290 |

注意错误总数没怎么动：类型补上以后，原先被 `CS0246` 挡住的文件继续往下走，暴露出成员级错误——`CS1061` 从 464 涨到 658，`CS0103` 从 554 涨到 608。**错误总数不是收敛指标**，看「编译可换」和「字节一致」两个数。

新进可编译的 4 个（`ImpalingBolt`/`StormHammers`/`Ultravision`/`ResistantSkin`）依然全部是行为有缺口的：`ResistantSkin` 客户端版本只剩 `ProvidesResistantSkin` 标记，Launcher 那版靠 `DebuffDurationScale=>0.5` 提供抗性皮肤效果，探针里就有「抗性皮肤缩放 0.5」这条断言。

---

### 第三轮（`0a6dd1c` → `6a74247`）

补的东西：`World.Units` 换成 `UnitList`（入表即打 `unit.World`，341 处调用零改动）、`Unit.CanBeTargetedBy` 之外的 `Pos/Life/Health` 门面收尾、照搬 `AttackLaunchContext.cs`（含 `IAttackLaunchModifier`/`IAttackImpactPayload`）、照搬 `WeaponSound`/`MissileKind`/`WeaponType`、`Buff.Name` 改虚、Buff 与 Ability 的长尾钩子（`OnDeath`/`ModifyOutgoingDamage`/`ExpirationRunsWhilePaused`/`BlocksWebAutoCast`/`SpawnSpecialArtsOnCast`/`SpawnTargetArtsOnCast`）。

| 指标 | 第二轮后 | 第三轮后 |
|---|---|---|
| 191 对中字节一致 | 5 | 5 |
| 镜像文件总数 | 7 | **11** |
| 191 对中编译可换 | 9 | **9**（没动） |
| 只剩 1 条错误的文件 | 38 | 35 |
| 编译错误总数 | 6,290 | 8,272 |

**这一轮是负收益，值得写清楚。** 三个数往三个方向走：镜像文件 +4（都是新增的无依赖文件）、编译可换 0 增、错误总数 +2,000。原因是每补一个类型，被它挡住的文件就往下走一层，暴露出更细的成员错误；错误总数随进展上升，跟收敛无关。

**长尾补齐到此为止。** 现在 35 个文件各差 1 条，但差的是 35 个不同成员：`Effect` 类型（4 个）、`Hero` 组件、`BuffTypeData`、以及二十来个只有单个技能才用到的 `override`。补完这 35 个，还有下一层（`Owner.Abilities.*`、`World.Cast`、美术字段）。这条路的天花板不是 159，是"永远差一层"。

要么按簇换（光环 9 个、攻击管线 16 个，各簇自带基类和一整套断言），要么直接换基类（阶段 5，99 个技能同时迁移）。单点补虚成员这条路，收益已经归零。

### 第四轮（`403ce8c` → `b7269ed`）

这轮做的是光环簇的前置：**`Buff` 从独立类改成 `: Ability`**（客户端模型）。改完编译 0 错 0 警告，两条探针全过，`Buff` 与 `Ability` 的 16 处同名成员按 `override` / `new` 分好类。

顺带补的：`Unit.CanBeTargetedBy(TargetFlags)`、`Unit.AddAbility/RemoveAbility(Ability)`、`RuntimeAbilityFactory.ReadFloat/ReadInt/ReadString`、`Ability.HasPersistentTargetArts`。

然后照搬 `AuraAbility.cs` + `AuraBuff.cs`，编译失败，暴露出下一道墙：

| 客户端写法 | Launcher 现状 |
|---|---|
| `Owner.World.GameplayConfig`（实例） | `War3GameplayConfig` 是**静态类**（`Src/Configuration/War3GameplayConfig.cs:7`） |
| `World.RentUnitListForAura()` / `ReturnUnitListForAura()` / `CollectNearbyUnits(pos, r, list)` | 无空间查询，`RefreshAuras` 直接遍历全表 |
| `Ability.ConfiguredTargets` / `HasConfiguredTargets` | 只有 `AbilityType.Targets`（自家 `SpellTargets` 枚举） |
| `Unit.MovementType` | 只有 `UnitType.Flying` / `MoveType` 字符串 |
| `War3GameplayConfig.PolymorphedUnitsBestowAuras` 等旗标 | 未解析 |

`War3GameplayConfig` 在 Launcher 里只有 31 处引用，静态改实例的改动面比想象小。但客户端那版是 700 行的实例配置，还带 `Default` 单例和 Misc 解析，替换等于把配置层整个换掉。

**结论更新**：光环簇的真实前置不是两个基类，是**配置层实例化**。这道墙不拆，客户端技能代码里的 `Owner.World?.GameplayConfig` 一律编不过——它是 182 个技能文件里的高频写法。

### 第五轮（`acdc99d`）

上一轮点名的墙——配置层静态——拆了。

| 改动 | 规模 |
|---|---|
| `War3GameplayConfig` 由 `static class` 改成 `sealed class` + `Default` 单例 | 153 行，31 处调用点 |
| `World.GameplayConfig` | 1 行，客户端 `Owner.World?.GameplayConfig` 从此可解析 |
| 7 个光环授予旗标（`PolymorphedUnitsBestowAuras` 等） | 值取自 `Units/MiscGame.txt` |
| `Unit.Stats` 门面（`AddNonStack`/`RemoveNonStack` + 移速/攻速/伤害加成） | 非叠加护甲已计入伤害结算 |
| `Unit.MovementType`/`Invisible`、`Ability.ConfiguredTargets`/`HasConfiguredTargets`/`IsRuntimeLocked` | — |

做到这里，客户端的 `AuraAbility.cs` + `AuraBuff.cs` 能原样编译了。但把它们接进游戏时，**探针 4 项直接红**：

```
[校验] FAIL 光环 +1.5 护甲     期望 1.5000 实际 0.0000
[校验] FAIL 护甲汇总带上光环   期望 3.5000 实际 2.0000
[校验] FAIL 耐久光环 +10% 移速 期望 297.0000 实际 270.0000
[校验] FAIL 耐久光环 +5% 攻速  期望 1.2857 实际 1.3500
```

原因是两套光环引擎的观测口径不同：

| | Launcher | 客户端 |
|---|---|---|
| 触发 | `World.RefreshAuras()` 每 tick 全量重算 | 光环技能自己 `OnTick` 每 2s 扫描并**授予 Buff** |
| 载体 | `AuraBonus` 数值合并到 `unit.AuraArmor`/`AuraAttackSpeedPct` | 独立 `AuraBuff` 实例，离场 2s 衰减 |
| 探针断言 | 读 `friend.AuraArmor` | 该字段恒 0 |

这属于引擎级迁移：要把 `RefreshAuras` 与它的 6 个消费点（护甲汇总、移速、攻速、回血、回蓝、视野）一起换成 Buff 模型，并把探针断言从 `AuraArmor` 改成读实际效果。两个基类文件已回退，等下轮连引擎一起换。

### 第六轮（`6be9b6b`）

光环引擎迁移的第一步：**两个光环真的跑在客户端模型上了**，而且和 A 的旧引擎共存。

落地内容：

| 项 | 说明 |
|---|---|
| 照搬 6 个文件 | `AuraAbility.cs`、`AuraBuff.cs`、`DevotionAura.cs`、`DevotionAuraBuff.cs`、`EnduranceAura.cs`、`EnduranceAuraBuff.cs` |
| `Buff.Source` 拆分 | 技能来源改叫 `SourceAbility`（客户端语义），施法者用 `Caster`；A 侧 18 个赋值点同步迁移 |
| `BuffTypeData` 桩 | `w3h` 还没解析，`War3ObjectData.GetBuff` 先返回 null，走 `BindObjectData` 分支 |
| `GiveAbility` 绑数据 | 创建技能时挂 `AbilityTypeObjectData`，客户端技能类读 `ObjectData` 才有值（这条之前一直是空的） |
| 非叠加护甲 | `Stats.AddNonStack` → `NonStackArmor` → 计入伤害结算 |
| 探针迁移 | 光环断言从「调 `RefreshAuras` 读 `AuraArmor`」改成「调 `TickAbilities` 读 `ArmorOf`/`SpeedOf`/`NonStackArmor`」 |

结果：同路径字节一致 **5 → 7**（`DevotionAura.cs`、`EnduranceAura.cs`），镜像文件总数 13 → 18。构建 0 错 0 警告，两条探针全过（专注光环护甲 1.5、耐久光环 297 移速、1.2857 攻速都对）。

**关键发现：`GiveAbility` 从来没绑过对象数据。** 之前所有客户端技能类的 `ObjectData` 都是 null，所以 `ReadFloat(ObjectData,"DataA",level,0)` 一律返回 0——这也是为什么很多文件"编译过了但跑起来没效果"。补上绑定之后，客户端技能的数值读取才算真正通。

**还剩 5 个光环没搬**：`RuntimeBrillianceAuraAbility`、`RuntimeCommandAuraAbility`、`RuntimeLifeRegenerationAuraAbility`、`RuntimeNeutralRegenAbility`、`RuntimeSlowAuraAbility`、`RuntimeThornsAuraAbility`、`RuntimeUnholyAuraAbility`。卡点分两类：

- 构造签名 `(War3ObjectData objects)`：工厂目前只 `new X()`，需要把 `War3ObjectData` 实例喂进去（客户端是特化流水线，见阶段 4）。
- `DamageData.BonusAmount/DamageMultiplier/IsRanged`：命令光环、荆棘光环要改出伤，需要把伤害管线扩到客户端形状。

### 第七轮（`6bf1549`）

工厂不再只会 `new X()`。

| 改动 | 说明 |
|---|---|
| `RuntimeAbilityFactory.WithObjects<T>()` | 有 `(War3ObjectData)` 构造就用，没有退回无参；14 个注册项改用它 |
| `RuntimeObjects` | 全局物编实例位（`w3h` 未解析前为 null，客户端代码用 `objects?.` 兜住） |
| `GiveAbility` 调 `OnLevelUp()` | 客户端技能在 `OnLevelUp` 里算派生数值（移速系数、半径、BuffID），此前只有 `LearnAbility` 会调，导致 `GiveAbility` 出来的技能数值全是 0 |
| 照搬 `RuntimeUnholyAuraAbility.cs` | 邪恶光环的移速与回血走 Buff，探针 3 条断言全过 |
| `StandalonePathing.Sim.CantPathSearchPoint.NativeMultiply` | 客户端浮点乘法的数值替身，只为实现编译与数值语义 |

同路径字节一致 **7 → 8**。

**这一轮也暴露了引擎差异的完整清单。** 另外四个光环（辉煌、缓慢、再生、中立恢复）编译能过、构造也能过，但探针 4 条断言直接红，原因不是代码错，是**断言读的是旧引擎字段**：

| 断言 | 读的旧字段 | 新模型下的对应物 |
|---|---|---|
| 辉煌光环每秒 0.75 法力 | `unit.AuraManaRegenPerSecond` | 光环 Buff 加到 `unit.ManaRegen` |
| 缓慢光环把敌人减速 | `unit.Slowed` | Buff 扣 `Stats.SpeedBonus` |
| 再生光环百分比=DataA | `unit.AuraHpRegenPct` | Buff 加到 `AlwaysLifeRegenBonus`（或按最大生命折算） |

把这四条断言改成读实际效果就能过，但改断言等于**重写验收口径**，需要确认新口径与原版一致（比如缓慢到底该不该置 `Slowed`）。这属于下一轮的活，已回退，本轮只保留验证通过的邪恶光环。

### 第八轮（`57d9d9f`）

先定验收口径，再改实现：**光环效果只看实际数值，不看内部字段**。

| 断言 | 旧读法 | 新读法 |
|---|---|---|
| 辉煌光环 +0.75 法力/秒 | `unit.AuraManaRegenPerSecond` | `unit.ManaRegen - Type.ManaRegen` |
| 再生光环 =DataA | `unit.AuraHpRegenPct` | `unit.AlwaysLifeRegenBonus / unit.MaxHp` |
| 再生光环按最大生命回血 | 同上 | `Type.RegenHp + AlwaysLifeRegenBonus` × 1s |

改完口径，辉煌、再生、中立恢复三个光环一次过，同路径字节一致 **8 → 11**。

**缓慢光环回退，原因值得记。** 它的目标掩码来自物编 `targs`，客户端 `ParseTargets` 能解析出 `Enemy`；A 的 `AbilityCatalog` 只读了 `SpellTargets` 的一部分，拿不到 `Enemy`，于是 `_allowedTargets` 落到默认 `Self`，敌人永远拿不到 Buff，探针读到的移速不变。

两条修法都要改行为：
- 补 `AbilityCatalog` 的 `targs` 解析（数据层，改动小但影响面广，所有技能的目标掩码都会变准）；
- 给 `AffectsAllies` 兜底（逻辑层，一行，但和客户端语义不一致）。

倾向第一种。数据层一旦解析对，`CanBeTargetedBy` 这套判定才算真正生效，后面 128 个技能文件里的目标筛选也能跟着对。

### 第九轮（`77f7535` → `8eb667a`）

光环簇收尾：**9 个光环里 8 个已迁移**，同路径字节一致 **11 → 13**。

| 改动 | 说明 |
|---|---|
| `AbilityType.TargetRules` | `AbilityCatalog` 保存 `targs1` 原始串，`AbilityTypeObjectData` 暴露 `targs/targs1` |
| `DamageData` 扩字段 | `BonusAmount`/`DamageMultiplier`/`IsRanged`，结算前按 `(Amount+Bonus)*Multiplier` 归一 |
| ref 版入伤钩子 | `ApplyIncomingModifiers` 除 `(World, DamageData)` 外再调 `ModifyIncomingDamage(ref data)`，客户端 Buff 的钩子这才真正生效 |
| `Unit.TakeDamage` / `SimulationId`、`Weapon.WeaponType`、`World.AreAllied(Unit,Unit)` | 客户端代码用到的零散成员 |

**缓慢光环的根因找到了**：`targs1` 在 SLK 里是 `air,ground,enemy,vuln,invu`，A 的 `AbilityCatalog` 解析并存下了 `SpellTargets`，但 `AbilityTypeObjectData` 没有把这一列暴露给 `GetString("targs1")`。客户端 `ParseTargets` 拿到空串，掩码落到默认 `Self`，敌人永远拿不到 Buff。补上暴露后一次通过。

这说明一件事：**判定逻辑写对了，输入是空的，等于没写。** 之前几轮修的 `CanBeTargetedBy`、`ConfiguredTargets` 都卡在同一个地方——数据没接到。

**命令光环是最后一个，回退了。** 它实现 `IAttackDamageStatModifier.ModifyOutgoingDamage(ref DamageData)`，但 A 的出伤钩子是 `float ApplyAttackModifiers(Unit, Unit, float)`，只传数值不传伤害包。要接上得把出伤管线从 `float` 改成 `DamageData`，牵动致命一击、重击、溅射几个消费点，属于伤害管线的整体改造，留作单独一轮。

### 第十轮（`d409535`）

出伤管线打通，命令光环迁完，**9 个光环全部走客户端模型**。

- `ApplyDamagePacketModifiers`：普攻包生成后遍历攻击方技能，先跑 `IAttackDamageStatModifier`，再跑 `Ability.ModifyOutgoingDamage(ref DamageData)`。
- 幻象伤害系数从「改 raw」改成「改伤害包」，`Raw` 与 `Amount` 同步。

| 指标 | 第九轮 | 第十轮 |
|---|---|---|
| 同路径字节一致 | 13 | **14** |
| 191 对中编译可换 | 9 | **18** |

（编译可换 18 = 9 个光环 + 5 个纯数据文件 + 4 个原本就一致的。）

**剩余单点缺口重新统计后，形状变了**：不再是"缺 Ability 虚成员"，而是

| 缺口 | 文件数 |
|---|---|
| `Effect` 类型 | 4 |
| `SpecialEffect` 类型 | 2 |
| `Hero` 类型 | 1 |
| 各类 `ModifyOutgoingDamage(Unit, ref)` / `CheckBeforeQueue` / `TimedLife` 等零星重写点 | 各 1 |

前三个是表现层类型——技能要放特效就得有 `Effect` 对象。A 侧现在只有 `SpecialEffectApplication` 这种场景节点包装，没有客户端的 `Effect`/`SpecialEffect` 抽象。这又是一层"表现层抽象"，和当初 L4 那 32 个文件同一个原因。

也就是说：**技能文件的最后一公里不是逻辑，是特效**。要么给 A 补一层 `Effect` 门面（把 SpecialEffectApplication 包进去），要么这些技能就一直停在 2 条错误上。

### 第十一轮（`6958bb1`）

补表现层抽象的第一块，同时把一批客户端口径成员补齐：

| 改动 | 说明 |
|---|---|
| `Effect.cs` / `SpecialEffect.cs` | 照搬客户端特效基类（`Step(World,float)` + 可视化元数据） |
| `World.Tick` | A 侧原是 `const float`，客户端按实例属性用；改名 `FrameSeconds` 后补实例 `Tick` 属性，37 个文件随之调整 |
| `StateModType` | 客户端的状态枚举 + `AddState/RemoveState/HasState` 门面，同时给 `StateMod` 补了 9 个枚举值 |
| `Ability.BuffRawcodes` / `EffectArts` / `SpecialArts` / `AreaEffectArts` / `CasterArts` / `TargetArts` | 客户端技能要读的美术与 Buff 列表 |

字节一致仍是 14（`Effect`/`SpecialEffect` 是新增文件，不在 191 里），镜像文件总数到 27。

**试了三个依赖特效的技能，各差 1-2 个成员**：

| 文件 | 还差 |
|---|---|
| `RuntimeCloudOfFogAbility` | `CloudOfFog` 类型（定义在另一个客户端文件里） |
| `RuntimeBurningOilAbility` | 工厂注册项（`(War3ObjectData)` 构造） |
| `RuntimeInfernoAbility` | `Unit.Hidden` |

这就是现在的形态：**不是"缺一个大件"，是"每个文件缺一个不同的零件"**。35 个文件各停在 1 条错误上，把这条尾巴扫掉需要 30 多次单点补齐，每次收益 1 个文件。

要不要继续扫这条尾巴，取决于目标——如果目标是字节一致数量，扫完大约能到 45-50；如果要到 128，必须换 `Ability` 基类本体。

### 第十二轮（`dcae267`、`f9b52f8`）

扫长尾，同时修掉一个真 bug。

**真 bug**：`Buff.ModifyOutgoingDamage` 之前是 `new virtual`，而 Buff 实例现在存放在 `Unit.Abilities` 里（静态类型 `Ability`）。用 `new` 隐藏基类成员意味着**虚派发会走到 `Ability` 的空实现**，Buff 的出伤加成永远不生效。改成 `override` 才通。这类问题编译器不报错，只有探针或实测能发现。

补齐的成员：`Ability` 的 `CheckBeforeQueue`×2 / `IncomingDamagePriority` / `CastOrderId`，`SpawnSpecialArtsOnCast` 与 `SpawnTargetArtsOnCast` 从方法改成 `protected virtual bool` 属性（客户端是属性），`ModifyOutgoingDamage` 改 `void` 并加 `(Unit, ref)` 重载；`Buff` 的 `TimedLife`/`ShowsDurationBar`；`Unit` 的 `Hidden`/`MagicImmune`/`Ethereal`/`Snared`/`Team`/`TargetType`/`AnimationProperties`；`World.AreEnemies`。

**长尾的实测成本**：修完这批，`RuntimeDefendAbility` 仍差 3 个成员（`DamageData.CannotMiss`、`Unit.AnimationRequestVersion`、`World.Rng`），`RuntimeFeedbackAbility` 差 `Classification`，`RuntimeCleavingAttackAbility` 差 `TargetRules`，`RuntimeInfernoAbility` 差 `SplitConfig`。

`Classification` 和 `TargetRules` 是客户端的分类子系统（`Unit.Is(Classification)`、`Unit.UsesHeroSpellRules`、`World.SharedSpellsQuery`），不是单个成员。也就是说技能文件的最后一公里分两段：**前段是零散成员（每文件 2-3 个），后段是分类子系统**。

---

## 1. 六个杠杆

按"改一处能解锁多少文件"排序。数字是**涉及该符号的文件数**，不是错误条数。

| # | 杠杆 | 涉及文件 | 说明 |
|---|---|---|---|
| 1 | `Vec2` | 131 | 客户端技能的点目标一律用 `Vec2`（二维地面坐标），Launcher 用 Godot `Vector3`。这是 `Ability.OnCast/CheckCanUse` 的签名差异，不是加个结构体就行 |
| 2 | `Ability.Name` 改虚 | 120 | Launcher 的 `Name` 是普通属性，客户端 128 个技能类全都 `override Name` |
| 3 | `OnLevelUp()/OnLevelDown()` | 96 / 96 | 客户端技能靠这两个钩子刷新派生数值；Launcher 用 `OnLearned()` 一把梭 |
| 4 | `TargetFlags` | 71 | 客户端的目标掩码枚举（31 位）；Launcher 拆成 `TargetKind` + `TargetRelation` 两套 |
| 5 | `OnBuffAdd()/OnBuffRemove()` | 52 / 52 | 客户端 Buff 有独立挂载钩子；Launcher 的 `Buff` 只有 `OnApply/OnRemove` |
| 6 | `OnTick(float)` | 50 | 签名不同：Launcher 是 `OnWorldTick(World, float)` |

其余的零散但便宜：

| 符号 | 文件 | 归属 |
|---|---|---|
| `OnRemove()` | 45 | Ability |
| `Positive` | 38 | Buff 属性（增益/减益二分） |
| `OnAdd()` | 29 | Ability |
| `OnDeath()` | 22 | Ability |
| `IAttackLaunchModifier` / `IAttackImpactPayload` | 16 / 16 | 攻击管线接口 |
| `Magic` / `AutoDispel` | 15 / 13 | Buff 驱散属性 |
| `ExpectedChannelDuration` | 13 | 引导技能 |
| `OnCastFinished(bool,bool)` | 12 | 施法收尾 |
| `ModifyOutgoingDamage(ref DamageData)` | 11 | 伤害钩子 |
| `War3ResourceManager` | 11 | 资源加载门面 |
| `OnDisabledStateChanged()` | 11 | 沉默/锁定时刷新 |
| `Effect` / `SpecialEffect` | 10 / 7 | 特效对象 |
| `IRuntimeObjectData` | 9 | 物编数据接口 |
| `AuraAbility` / `AuraBuff` | 9 / 9 | 光环基类 |
| `COrder` | 8 | 订单体系 |
| `ICastOrderTiming` | 7 | 施法时序接口 |
| `SupportsAutoCast` 改虚 | 14 | Ability |

合计 26 个可重写点、7 个可重写属性、16 个缺失类型。

---

## 2. 为什么"补虚成员"不等于"能照着抄"

举个实际的例子。`RuntimeUltravisionAbility.cs`（客户端 14 行）只差 4 条错误，看起来补两个符号就完事：

```csharp
public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
public override bool IsPassive=>true;
public override bool CheckCanUse(Unit target,Vec2 point)=>false;
```

三条 `override` 各要一个虚成员，`ObjectData`/`Rawcode` 要 `Ability` 持有物编引用，`Vec2` 要类型。四样齐了才能编过。**编过之后它还要能被工厂造出来**：客户端是 `new RuntimeUltravisionAbility(War3ObjectData objects)` 加特化流水线，Launcher 的 `RuntimeAbilityFactory` 是 `() => new X()` 无参构造。

所以顺序上有个反直觉的点：**先改工厂，后改基类**。工厂不先支持"带数据构造 + 特化"，基类补齐了也装不上。

---

## 3. 分阶段路线

| 阶段 | 动作 | 预计新增字节一致 | 前置 | 风险 |
|---|---|---|---|---|
| 0 | 命名空间对齐 | 0（但消除永久性的 1 行差异） | — | 已完成，`09e1dec` |
| 1 | `Ability`/`Buff` 补齐 29 个可重写点 + `Vec2` + `IRuntimeObjectData` + 对象数据适配器 | 0（编译可换 4→7，但 3 个都有行为缺口） | 无 | 已完成 |
| 2 | 引入 `Vec2`，`Ability` 的 `OnCast/CheckCanUse/CanTarget` 改 `Vec2` 签名 | 约 25 | 阶段 1 | 中：Launcher 全部 99 个技能类要跟着改点目标坐标 |
| 3 | `TargetFlags` 换成客户端枚举，`Weapon`/`Unit` 的目标掩码跟着改 | 约 25（与阶段 2 重叠） | 阶段 2 | 中：`TargetKind`/`TargetRelation` 两套语义合并 |
| 4 | `RuntimeAbilityFactory` 改成客户端特化流水线（5 分册 1,744 行的机制） | 0 | 阶段 1-3 | 高：技能注册表 513 条要重新绑定 |
| 5 | 替换 `Ability.cs`/`Buff.cs` 为客户端版本，逐个迁移 Launcher 自有的 99 个技能类 | +128 | 阶段 4 | 高：`Ability` 从 294 行涨到 1,152 行 |
| 6 | `Health`/`Stats` 补成员（`Owner.Health.*`，阶段 1 后涨到 476 处） | +10 | 阶段 5 | 高：v2 §4.2 的伤害观测者重构。**这是当前最大单一阻塞** |
| 7 | `World` 门面 + 订单体系（`COrder` 8 文件） | +10 | 阶段 6 | 高：v2 §4.1 |

阶段 1-3 是"只增不减"的渐进式改动，随时可停，构建和探针都能保持绿色。阶段 5 是断点：`Ability` 一换，Launcher 现有 99 个技能类全部要改签名，绿不了几天。

现实的做法是**在阶段 5 之前先把 Launcher 自有的技能类补齐成客户端形状**（阶段 4 的副产品），这样阶段 5 才能一次换干净。

---

## 4. 天花板之外的部分

这 31 个文件不要指望字节一致：

| 目录 | 文件 | 卡点 |
|---|---|---|
| `Game/Environment/` | 8 | 客户端用 `OpenGLRenderDevice` + GLSL；Launcher 用 Godot `MeshInstance3D` |
| `Game/Scenes/War3GameScene/` | 10 | `War3GameScene` 在客户端是 38 个 partial，且直连 `IRenderDevice` |
| `Game/UI/` | 5 | 客户端是 FDF 帧树 + `UiRenderDevice`；Launcher 是 `CanvasLayer` + `Button` |
| `Game/Audio/` | 3 | 客户端 `Silk.NET`/OpenAL；Launcher `AudioStreamPlayer` |
| `Game/Rendering/`、`Camera/`、`Presentation/`、`Display` | 5 | 同上 |

这些文件的差异靠 v2 文档 §3.9 的能力对照表跟踪，验收方式是"玩家看到的画面/听到的声音"，不是 `diff`。

---

## 5. 每个阶段的验收

```bash
# 1. 替换一个文件，全量重编
cp test/Open-Warcraft3/<path> Launcher/<path>
cd Launcher && dotnet build -t:Rebuild -v q --nologo; echo "exit=$?"

# 2. 两条硬门槛
./launcher.command -- --state-probe     # 764 条
./launcher.command -- --combat-probe    # 攻击矩阵 89.3 / 115.4 / 150.0

# 3. 计数
cmp -s "Launcher/$f" "test/Open-Warcraft3/$f" && echo identical
```

阶段 1-3 每个阶段结束都应满足：构建 0 错 0 警告、两探针全过、字节一致计数只增不减。

---

## 6. 复现这份分析

```bash
# 逐个替换 + 重编 + 还原，把错误存成 json（约 3 分钟）
python3 /tmp/mine2.py
# 然后按错误码 / 符号聚合
```

脚本思路：对 `/tmp/common.txt` 里每个路径，备份 Launcher 文件 → 覆盖 → `dotnet build -t:Rebuild` → 用正则抽 `error (CS\d+): (...)` → 还原。注意两点：必须 `-t:Rebuild`（增量会跳过），必须抓「错误」中文串而不是 `error `。
