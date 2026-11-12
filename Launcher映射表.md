# Launcher ↔ Open-Warcraft3 映射表

> 两侧 HEAD：Launcher `0271d76` · 客户端 `14b62fd5` · 生成于 2026-11-12
> 数据由脚本扫描全部 `*.cs` 得出，重跑命令见 §8。手写说明只在 §0、§1、§3、§4 开头。

---

## 0. 三个用途

**定位**：改一条行为前，查这里知道客户端对应哪个文件、哪一层。
**状态追踪**：每对标注当前关系，改完刷新一遍就知道漂移有没有扩大。
**收敛入口**：标「字节一致」的 4 个文件与客户端逐字节相同，改动会直接出现在 `git diff`；其余文件只能靠断言对表。

---

## 1. 逐字节一致的四个条件

四条全满足才允许 `cp` 覆盖：

| # | 条件 | 不满足的后果 |
|---|---|---|
| 1 | 不引用客户端 `Ability` / `Unit` / `World` 的成员 | 同名类的成员语义不同（`ObjectData`、`Rawcode`、`Owner.Health` 在 Launcher 不存在），照搬即编译失败 |
| 2 | 不引用客户端专有类型 | `Vec2`/`Vec3`/`War3ObjectData`/`PlayerRef`/`UnitTypeData` 在 Launcher 无同名或语义不同 |
| 3 | 自带 `using`，或只依赖 BCL | 客户端开了 ImplicitUsings，文件普遍省略 using；Launcher 实测开启后有 15 处歧义（`Godot.Timer` vs `System.Threading.Timer`、`Godot.FileAccess` vs `System.IO.FileAccess`） |
| 4 | 语义等价，或是 Launcher 的超集 | 照搬会改掉已实现的行为 |

191 对里只有 4 个满足四条。其余 187 个卡在条件 1 或 2：职责相同，实现是重写的。

命名空间已按客户端分层统一（`OpenWC3` / `OpenWC3.Game` / `OpenWC3.Game.UI` / `OpenWC3.War3Formats`），照搬不再需要额外的 using 过渡层，覆盖即可用。

---

## 2. 总览

| 项 | 数量 |
|---|---|
| 同路径 | 191 |
| ├ 字节一致 | 4 |
| ├ 重写·同职责 | 22 |
| ├ 同名·实现不同 | 105 |
| ├ 同名·能力差一个量级 | 57 |
| └ 客户端薄壳（≤10 行） | 3 |
| 仅 Launcher 有，能找到同类名文件 | 75 |
| 仅 Launcher 有，客户端同名类散在多文件 | 34 |
| 仅 Launcher 有，客户端无同类名 | 119 |

成员级重合度（同名 public 成员的交集 ÷ 并集）：

| 区间 | 文件数 |
|---|---|
| ≥0.6 | 4 |
| 0.3–0.6 | 1 |
| 0.1–0.3 | 32 |
| <0.1 | 154 |

154 个文件成员重合度低于 0.1。路径镜像只做到文件名对齐，类内部结构各自长各自的。

---

## 3. 字节一致清单

| 文件 | 行数 | 说明 |
|---|---|---|
| `Src/Core/AttackType.cs` | 5 | 7 种攻击类型，枚举值与顺序一致 |
| `Src/Core/DefenseType.cs` | 5 | 8 种护甲类型 |
| `Src/Core/DamageType.cs` | 38 | 客户端 22 种 JASS 伤害类型 + `DamageTypeCodec`。Launcher 原为 3 值，照搬后是超集，旧代码只用 `Normal/Magic/Universal`，行为不变 |
| `Src/Abilities/AbilityTargetMode.cs` | 13 | 客户端 6 种目标形态（含 `Destructable`）。Launcher 原为 3 值，数值 0/1/2 与原实现一致 |
| `Src/Core/Vec2.cs` | 21 | 新增文件，客户端没有同路径对应（Launcher 原先只有 Godot `Vector3`） |
| `Src/Abilities/IRuntimeObjectData.cs` | 15 | 新增文件，能力层对象数据的读取接口 |
| `Src/Core/TargetFlags.cs` | 26 | 客户端的 31 位目标掩码。Launcher 原有的 `TargetKind`/`TargetRelation` 拆到 `Src/Core/TargetKind.cs` |

前 5 个是同路径覆盖，后 2 个是新增镜像文件。全部 `cmp` 返回 0；`--state-probe` 与 `--combat-probe` 均通过。

> 191 对里编译可通过的现在是 9 个（多 `RuntimeResistantSkin`）（多 `RuntimeImpalingBolt`、`RuntimeStormHammers`、`RuntimeUltravision`），但这 3 个换过去会丢行为，没有采用，原因见路线文档 §0.5。

---

## 4. 同路径 191 对

**成员重合** = public 成员名交集 ÷ 并集；**代码重合** = 去注释、去 using 后的字符级相似度。
状态里的「同名」指文件名与职责对应，不代表实现口径一致；行为差异查差异文档 §3。


### GameCore/L1_Adapter

| # | 文件 | 行数 A/B | 成员重合 | 代码重合 | 状态 |
|---|---|---|---|---|---|
| 1 | `GameCore/L1_Adapter/Formats/War3Pathing.cs` | 74/199 | 0.18 | 0.10 | 重写·同职责 |

### GameCore/L3_Simulation/Configuration

| # | 文件 | 行数 A/B | 成员重合 | 代码重合 | 状态 |
|---|---|---|---|---|---|
| 2 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeUltravisionAbility.cs` | 11/14 | 0.20 | 0.59 | 重写·同职责 |
| 3 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeResistantSkinAbility.cs` | 10/11 | 0.20 | 0.50 | 重写·同职责 |
| 4 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeHardenedSkinAbility.cs` | 26/40 | 0.20 | 0.17 | 重写·同职责 |
| 5 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeMirrorImageAbility.cs` | 85/170 | 0.20 | 0.11 | 重写·同职责 |
| 6 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeStormHammersAbility.cs` | 32/10 | 0.17 | 0.27 | 客户端薄壳 |
| 7 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeForkedLightningAbility.cs` | 77/79 | 0.16 | 0.09 | 重写·同职责 |
| 8 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeCoupleAbility.cs` | 114/107 | 0.16 | 0.08 | 重写·同职责 |
| 9 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeImpalingBoltAbility.cs` | 37/10 | 0.14 | 0.18 | 客户端薄壳 |
| 10 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeBarrageAbility.cs` | 26/42 | 0.14 | 0.16 | 同名·实现不同 |
| 11 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeUnsummonAbility.cs` | 25/37 | 0.12 | 0.29 | 重写·同职责 |
| 12 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeFlareAbility.cs` | 20/48 | 0.12 | 0.20 | 重写·同职责 |
| 13 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeFlameStrikeAbility.cs` | 61/191 | 0.12 | 0.05 | 同名·能力差一个量级 |
| 14 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeDrunkenHazeAbility.cs` | 48/123 | 0.12 | 0.07 | 同名·实现不同 |
| 15 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimePillageAbility.cs` | 20/47 | 0.11 | 0.07 | 同名·实现不同 |
| 16 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeFireBoltAbility.cs` | 39/88 | 0.11 | 0.07 | 同名·实现不同 |
| 17 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeParasiteAbility.cs` | 50/128 | 0.11 | 0.07 | 同名·实现不同 |
| 18 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeSentinelAbility.cs` | 23/76 | 0.11 | 0.10 | 同名·实现不同 |
| 19 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeLightningAttackAbility.cs` | 25/25 | 0.10 | 0.25 | 重写·同职责 |
| 20 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeInvisibleGhostAbility.cs` | 16/39 | 0.10 | 0.16 | 同名·实现不同 |
| 21 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeAncestralSpiritAbility.cs` | 80/95 | 0.10 | 0.08 | 同名·实现不同 |
| 22 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeDoomAbility.cs` | 49/151 | 0.10 | 0.07 | 同名·实现不同 |
| 23 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeLiquidFireAbility.cs` | 32/101 | 0.10 | 0.07 | 同名·实现不同 |
| 24 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeWindWalkAbility.cs` | 53/197 | 0.09 | 0.07 | 同名·实现不同 |
| 25 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeRootAbility.cs` | 87/116 | 0.09 | 0.05 | 同名·实现不同 |
| 26 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeMagicImmunityAbility.cs` | 16/50 | 0.09 | 0.18 | 同名·实现不同 |
| 27 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeSpikedBarricadesAbility.cs` | 10/41 | 0.09 | 0.17 | 同名·实现不同 |
| 28 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeCannibalizeAbility.cs` | 55/70 | 0.09 | 0.11 | 同名·实现不同 |
| 29 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeRoboGoblinAbility.cs` | 84/84 | 0.09 | 0.05 | 同名·能力差一个量级 |
| 30 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeCurseAbility.cs` | 37/101 | 0.09 | 0.08 | 同名·实现不同 |
| 31 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeCloudOfFogAbility.cs` | 43/232 | 0.09 | 0.08 | 同名·实现不同 |
| 32 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeVolcanoAbility.cs` | 48/228 | 0.08 | 0.06 | 同名·实现不同 |
| 33 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeStandDownAbility.cs` | 19/54 | 0.08 | 0.13 | 同名·实现不同 |
| 34 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeDrunkenBrawlerAbility.cs` | 28/108 | 0.08 | 0.05 | 同名·能力差一个量级 |
| 35 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeCrippleAbility.cs` | 44/93 | 0.08 | 0.13 | 同名·实现不同 |
| 36 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeHowlOfTerrorAbility.cs` | 38/129 | 0.08 | 0.06 | 同名·实现不同 |
| 37 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeDecoupleAbility.cs` | 50/33 | 0.08 | 0.14 | 同名·实现不同 |
| 38 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeCleavingAttackAbility.cs` | 27/68 | 0.08 | 0.08 | 同名·实现不同 |
| 39 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeThunderClapAbility.cs` | 58/157 | 0.08 | 0.08 | 同名·实现不同 |
| 40 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeCarrionBeetlesAbility.cs` | 110/139 | 0.07 | 0.04 | 同名·能力差一个量级 |
| 41 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeAntiMagicShellAbility.cs` | 66/111 | 0.07 | 0.07 | 同名·实现不同 |
| 42 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeInnerFireAbility.cs` | 51/127 | 0.07 | 0.07 | 同名·实现不同 |
| 43 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeCharmAbility.cs` | 41/73 | 0.07 | 0.18 | 同名·实现不同 |
| 44 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeAttributeModSkillAbility.cs` | 22/80 | 0.07 | 0.15 | 同名·实现不同 |
| 45 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeSlowAbility.cs` | 38/136 | 0.07 | 0.08 | 同名·实现不同 |
| 46 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimePocketFactoryAbility.cs` | 51/141 | 0.07 | 0.08 | 同名·实现不同 |
| 47 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimePhaseShiftAbility.cs` | 20/57 | 0.07 | 0.16 | 同名·实现不同 |
| 48 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeExhumeAbility.cs` | 38/34 | 0.07 | 0.09 | 同名·实现不同 |
| 49 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeResurrectionAbility.cs` | 53/111 | 0.07 | 0.08 | 同名·实现不同 |
| 50 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeLightningShieldAbility.cs` | 39/67 | 0.06 | 0.14 | 同名·实现不同 |
| 51 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeRepairAbility.cs` | 63/71 | 0.06 | 0.09 | 同名·实现不同 |
| 52 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeSpikedCarapaceAbility.cs` | 18/80 | 0.06 | 0.09 | 同名·实现不同 |
| 53 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeVenomSpearAbility.cs` | 27/188 | 0.06 | 0.04 | 同名·能力差一个量级 |
| 54 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeLocustSwarmAbility.cs` | 45/288 | 0.06 | 0.04 | 同名·能力差一个量级 |
| 55 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeKaboomAbility.cs` | 38/101 | 0.06 | 0.15 | 同名·实现不同 |
| 56 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimePlagueTossAbility.cs` | 47/62 | 0.06 | 0.10 | 同名·实现不同 |
| 57 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeWarStompAbility.cs` | 41/95 | 0.06 | 0.08 | 同名·实现不同 |
| 58 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeWanderAbility.cs` | 33/110 | 0.06 | 0.07 | 同名·实现不同 |
| 59 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeMagicLeashAbility.cs` | 44/198 | 0.06 | 0.06 | 同名·实现不同 |
| 60 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeMonsoonAbility.cs` | 44/202 | 0.05 | 0.06 | 同名·实现不同 |
| 61 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeTransmuteAbility.cs` | 38/59 | 0.05 | 0.14 | 同名·实现不同 |
| 62 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeHealingWardAbility.cs` | 28/70 | 0.05 | 0.11 | 同名·实现不同 |
| 63 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeAnimateDeadAbility.cs` | 58/130 | 0.05 | 0.07 | 同名·实现不同 |
| 64 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeNeutralRegenAbility.cs` | 26/119 | 0.05 | 0.09 | 同名·实现不同 |
| 65 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeStasisTrapAbility.cs` | 29/131 | 0.05 | 0.08 | 同名·实现不同 |
| 66 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeHealAbility.cs` | 51/124 | 0.05 | 0.06 | 同名·实现不同 |
| 67 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeEatTreeAbility.cs` | 45/71 | 0.05 | 0.05 | 同名·能力差一个量级 |
| 68 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeCycloneAbility.cs` | 36/97 | 0.04 | 0.10 | 同名·实现不同 |
| 69 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeInvisibilityAbility.cs` | 47/143 | 0.04 | 0.09 | 同名·实现不同 |
| 70 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeFrostAttackAbility.cs` | 29/73 | 0.04 | 0.07 | 同名·实现不同 |
| 71 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeFlamingArrowsAbility.cs` | 33/84 | 0.04 | 0.06 | 同名·实现不同 |
| 72 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimePhoenixFireAbility.cs` | 25/153 | 0.04 | 0.05 | 同名·实现不同 |
| 73 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeEntanglingRootsAbility.cs` | 53/103 | 0.04 | 0.09 | 同名·实现不同 |
| 74 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeCarrionSwarmAbility.cs` | 49/189 | 0.04 | 0.08 | 同名·实现不同 |
| 75 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeDivineShieldAbility.cs` | 30/110 | 0.04 | 0.07 | 同名·实现不同 |
| 76 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeLifeRegenerationAuraAbility.cs` | 15/132 | 0.04 | 0.07 | 同名·实现不同 |
| 77 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeTornadoAbility.cs` | 64/60 | 0.04 | 0.16 | 同名·实现不同 |
| 78 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeHealingSprayAbility.cs` | 44/101 | 0.04 | 0.10 | 同名·实现不同 |
| 79 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeStarfallAbility.cs` | 42/107 | 0.04 | 0.08 | 同名·实现不同 |
| 80 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeFrostNovaAbility.cs` | 56/176 | 0.04 | 0.08 | 同名·实现不同 |
| 81 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeFaerieFireAbility.cs` | 36/159 | 0.04 | 0.07 | 同名·实现不同 |
| 82 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimePolymorphAbility.cs` | 41/86 | 0.04 | 0.06 | 同名·实现不同 |
| 83 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeBashAbility.cs` | 27/154 | 0.04 | 0.03 | 同名·能力差一个量级 |
| 84 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeBanishAbility.cs` | 55/137 | 0.04 | 0.08 | 同名·实现不同 |
| 85 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeRainOfFireAbility.cs` | 86/234 | 0.04 | 0.07 | 同名·实现不同 |
| 86 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeImmolationAbility.cs` | 92/207 | 0.04 | 0.06 | 同名·实现不同 |
| 87 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeSilenceAbility.cs` | 53/121 | 0.04 | 0.06 | 同名·实现不同 |
| 88 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeInfernoAbility.cs` | 37/187 | 0.04 | 0.05 | 同名·实现不同 |
| 89 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeShadowStrikeAbility.cs` | 56/176 | 0.04 | 0.06 | 同名·实现不同 |
| 90 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeManaShieldAbility.cs` | 28/135 | 0.03 | 0.08 | 同名·实现不同 |
| 91 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeSleepAbility.cs` | 42/200 | 0.03 | 0.06 | 同名·实现不同 |
| 92 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeSoulBurnAbility.cs` | 44/117 | 0.03 | 0.05 | 同名·能力差一个量级 |
| 93 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeDefendAbility.cs` | 36/104 | 0.03 | 0.05 | 同名·能力差一个量级 |
| 94 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeBloodlustAbility.cs` | 54/166 | 0.03 | 0.05 | 同名·能力差一个量级 |
| 95 | `GameCore/L3_Simulation/Simulation/Configuration/War3SlkReader.cs` | 135/132 | 0.03 | 0.05 | 同名·实现不同 |
| 96 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeAcidBombAbility.cs` | 55/124 | 0.03 | 0.09 | 同名·实现不同 |
| 97 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimePermanentImmolationAbility.cs` | 33/161 | 0.03 | 0.06 | 同名·实现不同 |
| 98 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeManaFlareAbility.cs` | 33/156 | 0.03 | 0.06 | 同名·实现不同 |
| 99 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeTranquilityAbility.cs` | 64/119 | 0.03 | 0.07 | 同名·实现不同 |
| 100 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeBlackArrowAbility.cs` | 56/114 | 0.03 | 0.05 | 同名·能力差一个量级 |
| 101 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeRoarAbility.cs` | 31/235 | 0.03 | 0.03 | 同名·能力差一个量级 |
| 102 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeStampedeAbility.cs` | 56/137 | 0.03 | 0.09 | 同名·实现不同 |
| 103 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimePurgeAbility.cs` | 49/190 | 0.03 | 0.08 | 同名·实现不同 |
| 104 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeColdArrowsAbility.cs` | 42/127 | 0.03 | 0.08 | 同名·实现不同 |
| 105 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeBurningOilAbility.cs` | 35/146 | 0.03 | 0.04 | 同名·能力差一个量级 |
| 106 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeClusterRocketsAbility.cs` | 39/198 | 0.03 | 0.09 | 同名·实现不同 |
| 107 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeGrabTreeAbility.cs` | 56/136 | 0.03 | 0.06 | 同名·实现不同 |
| 108 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeHexAbility.cs` | 63/237 | 0.03 | 0.06 | 同名·实现不同 |
| 109 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeIncinerateAbility.cs` | 31/175 | 0.03 | 0.03 | 同名·能力差一个量级 |
| 110 | `GameCore/L3_Simulation/Simulation/Configuration/War3ObjectData.cs` | 215/754 | 0.01 | 0.02 | 同名·能力差一个量级 |
| 111 | `GameCore/L3_Simulation/Simulation/Configuration/UnitFactory.cs` | 451/466 | 0.01 | 0.02 | 同名·能力差一个量级 |
| 112 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeCorporealFormAbility.cs` | 22/19 | 0.00 | 0.29 | 重写·同职责 |
| 113 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeBurrowFormAbility.cs` | 24/17 | 0.00 | 0.27 | 重写·同职责 |
| 114 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeInvulnerableAbility.cs` | 16/34 | 0.00 | 0.16 | 同名·实现不同 |
| 115 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeBlightGrowthAbility.cs` | 23/82 | 0.00 | 0.13 | 同名·实现不同 |
| 116 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeDemolishAbility.cs` | 19/43 | 0.00 | 0.13 | 同名·实现不同 |
| 117 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeShockwaveAbility.cs` | 48/34 | 0.00 | 0.13 | 同名·实现不同 |
| 118 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeDestroyerFormAbility.cs` | 33/17 | 0.00 | 0.12 | 同名·实现不同 |
| 119 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeEngineeringUpgradeAbility.cs` | 66/115 | 0.00 | 0.10 | 同名·实现不同 |
| 120 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeTornadoDamageAbility.cs` | 56/140 | 0.00 | 0.09 | 同名·实现不同 |
| 121 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeDruidFormAbility.cs` | 49/21 | 0.00 | 0.09 | 同名·实现不同 |
| 122 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeMoonGlaiveAbility.cs` | 41/36 | 0.00 | 0.08 | 同名·实现不同 |
| 123 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeItemPlaceMineAbility.cs` | 34/31 | 0.00 | 0.07 | 同名·实现不同 |
| 124 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeFeedbackAbility.cs` | 25/98 | 0.00 | 0.07 | 同名·实现不同 |
| 125 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeReincarnationAbility.cs` | 16/126 | 0.00 | 0.06 | 同名·实现不同 |
| 126 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeSlowAuraAbility.cs` | 33/86 | 0.00 | 0.05 | 同名·实现不同 |
| 127 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeThornsAuraAbility.cs` | 20/81 | 0.00 | 0.04 | 同名·能力差一个量级 |
| 128 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeBreathOfFireAbility.cs` | 61/128 | 0.00 | 0.04 | 同名·能力差一个量级 |
| 129 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeBrillianceAuraAbility.cs` | 19/145 | 0.00 | 0.04 | 同名·能力差一个量级 |
| 130 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeUnholyAuraAbility.cs` | 21/133 | 0.00 | 0.04 | 同名·能力差一个量级 |
| 131 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeCommandAuraAbility.cs` | 15/129 | 0.00 | 0.04 | 同名·能力差一个量级 |
| 132 | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeAlternateFormAbility.cs` | 28/283 | 0.00 | 0.02 | 同名·能力差一个量级 |

### GameCore/L3_Simulation/Maps

| # | 文件 | 行数 A/B | 成员重合 | 代码重合 | 状态 |
|---|---|---|---|---|---|
| 133 | `GameCore/L3_Simulation/Simulation/Maps/War3MapUnits.cs` | 242/209 | 0.31 | 0.04 | 重写·同职责 |
| 134 | `GameCore/L3_Simulation/Simulation/Maps/War3MapDoodads.cs` | 134/95 | 0.03 | 0.10 | 同名·实现不同 |

### GameCore/L4_Composition/Game/Audio

| # | 文件 | 行数 A/B | 成员重合 | 代码重合 | 状态 |
|---|---|---|---|---|---|
| 135 | `GameCore/L4_Composition/Game/Audio/War3MusicPlayer.cs` | 177/261 | 0.05 | 0.04 | 同名·能力差一个量级 |
| 136 | `GameCore/L4_Composition/Game/Audio/War3SoundTables.cs` | 222/142 | 0.04 | 0.01 | 同名·能力差一个量级 |
| 137 | `GameCore/L4_Composition/Game/Audio/War3AudioSystem.cs` | 76/1023 | 0.02 | 0.02 | 同名·能力差一个量级 |

### GameCore/L4_Composition/Game/Camera

| # | 文件 | 行数 A/B | 成员重合 | 代码重合 | 状态 |
|---|---|---|---|---|---|
| 138 | `GameCore/L4_Composition/Game/Camera/War3Camera.cs` | 76/1249 | 0.08 | 0.02 | 同名·能力差一个量级 |

### GameCore/L4_Composition/Game/Environment

| # | 文件 | 行数 A/B | 成员重合 | 代码重合 | 状态 |
|---|---|---|---|---|---|
| 139 | `GameCore/L4_Composition/Game/Environment/TerrainTextureSet.cs` | 119/105 | 0.00 | 0.06 | 同名·实现不同 |
| 140 | `GameCore/L4_Composition/Game/Environment/TerrainTextureLoader.cs` | 100/95 | 0.00 | 0.04 | 同名·能力差一个量级 |
| 141 | `GameCore/L4_Composition/Game/Environment/WaterRenderer.cs` | 142/232 | 0.00 | 0.03 | 同名·能力差一个量级 |
| 142 | `GameCore/L4_Composition/Game/Environment/War3Paths.cs` | 33/68 | 0.00 | 0.02 | 同名·能力差一个量级 |
| 143 | `GameCore/L4_Composition/Game/Environment/WeatherRenderer.cs` | 104/556 | 0.00 | 0.02 | 同名·能力差一个量级 |
| 144 | `GameCore/L4_Composition/Game/Environment/DoodadRenderer.cs` | 127/1131 | 0.00 | 0.02 | 同名·能力差一个量级 |
| 145 | `GameCore/L4_Composition/Game/Environment/TerrainGridFactory.cs` | 457/133 | 0.00 | 0.01 | 同名·能力差一个量级 |
| 146 | `GameCore/L4_Composition/Game/Environment/CliffSurface.cs` | 205/47 | 0.00 | 0.01 | 同名·能力差一个量级 |
| 147 | `GameCore/L4_Composition/Game/Environment/CliffRenderer.cs` | 137/281 | 0.00 | 0.01 | 同名·能力差一个量级 |

### GameCore/L4_Composition/Game/Presentation

| # | 文件 | 行数 A/B | 成员重合 | 代码重合 | 状态 |
|---|---|---|---|---|---|
| 148 | `GameCore/L4_Composition/Game/Presentation/UnitSceneComponent.cs` | 419/95 | 0.00 | 0.01 | 同名·能力差一个量级 |

### GameCore/L4_Composition/Game/Rendering

| # | 文件 | 行数 A/B | 成员重合 | 代码重合 | 状态 |
|---|---|---|---|---|---|
| 149 | `GameCore/L4_Composition/Game/Rendering/RallyPointDisplay.cs` | 125/87 | 0.07 | 0.05 | 同名·能力差一个量级 |
| 150 | `GameCore/L4_Composition/Game/Rendering/SkyboxRenderer.cs` | 74/162 | 0.00 | 0.04 | 同名·能力差一个量级 |

### GameCore/L4_Composition/Game/Scenes

| # | 文件 | 行数 A/B | 成员重合 | 代码重合 | 状态 |
|---|---|---|---|---|---|
| 151 | `GameCore/L4_Composition/Game/Scenes/War3GameScene/Terrain.cs` | 23/145 | 0.25 | 0.05 | 重写·同职责 |
| 152 | `GameCore/L4_Composition/Game/Scenes/War3GameScene/Rally.cs` | 247/124 | 0.09 | 0.03 | 同名·能力差一个量级 |
| 153 | `GameCore/L4_Composition/Game/Scenes/War3GameScene/CameraFollowTrack.cs` | 79/352 | 0.06 | 0.04 | 同名·能力差一个量级 |
| 154 | `GameCore/L4_Composition/Game/Scenes/War3GameScene/Units.cs` | 488/9 | 0.05 | 0.01 | 客户端薄壳 |
| 155 | `GameCore/L4_Composition/Game/Scenes/War3GameScene/Cheats.cs` | 1882/190 | 0.02 | 0.00 | 同名·能力差一个量级 |
| 156 | `GameCore/L4_Composition/Game/Scenes/War3GameScene/QuickCast.cs` | 637/555 | 0.02 | 0.02 | 同名·能力差一个量级 |
| 157 | `GameCore/L4_Composition/Game/Scenes/War3GameScene/Ui.cs` | 878/1520 | 0.01 | 0.01 | 同名·能力差一个量级 |
| 158 | `GameCore/L4_Composition/Game/Scenes/War3GameScene/Presentation.cs` | 418/2312 | 0.01 | 0.01 | 同名·能力差一个量级 |
| 159 | `GameCore/L4_Composition/Game/Scenes/War3GameScene/Input.cs` | 375/1385 | 0.01 | 0.02 | 同名·能力差一个量级 |
| 160 | `GameCore/L4_Composition/Game/Scenes/War3GameScene/Lifecycle.cs` | 382/1391 | 0.00 | 0.01 | 同名·能力差一个量级 |

### GameCore/L4_Composition/Game/UI

| # | 文件 | 行数 A/B | 成员重合 | 代码重合 | 状态 |
|---|---|---|---|---|---|
| 161 | `GameCore/L4_Composition/Game/UI/EscMenuFrame.cs` | 87/161 | 0.18 | 0.04 | 重写·同职责 |
| 162 | `GameCore/L4_Composition/Game/UI/HeroBarFrame.cs` | 143/140 | 0.04 | 0.09 | 同名·实现不同 |
| 163 | `GameCore/L4_Composition/Game/UI/ChatFrame.cs` | 86/281 | 0.00 | 0.03 | 同名·能力差一个量级 |
| 164 | `GameCore/L4_Composition/Game/UI/MinimapFrame.cs` | 267/636 | 0.00 | 0.02 | 同名·能力差一个量级 |
| 165 | `GameCore/L4_Composition/Game/UI/MeleeHudController.cs` | 946/1097 | 0.00 | 0.01 | 同名·能力差一个量级 |

### Src/Abilities

| # | 文件 | 行数 A/B | 成员重合 | 代码重合 | 状态 |
|---|---|---|---|---|---|
| 166 | `Src/Abilities/AbilityTargetMode.cs` | 13/13 | 1.00 | 1.00 | 字节一致 |
| 167 | `Src/Abilities/Ability.cs` | 295/1153 | 0.05 | 0.02 | 同名·能力差一个量级 |
| 168 | `Src/Abilities/Buff.cs` | 236/179 | 0.04 | 0.03 | 同名·能力差一个量级 |

### Src/Components

| # | 文件 | 行数 A/B | 成员重合 | 代码重合 | 状态 |
|---|---|---|---|---|---|
| 169 | `Src/Components/State.cs` | 129/136 | 0.27 | 0.11 | 重写·同职责 |

### Src/Configuration

| # | 文件 | 行数 A/B | 成员重合 | 代码重合 | 状态 |
|---|---|---|---|---|---|
| 170 | `Src/Configuration/War3GameplayConfig.cs` | 153/1064 | 0.06 | 0.02 | 同名·能力差一个量级 |

### Src/Core

| # | 文件 | 行数 A/B | 成员重合 | 代码重合 | 状态 |
|---|---|---|---|---|---|
| 171 | `Src/Core/AttackType.cs` | 5/5 | 1.00 | 1.00 | 字节一致 |
| 172 | `Src/Core/DamageType.cs` | 38/38 | 1.00 | 1.00 | 字节一致 |
| 173 | `Src/Core/DefenseType.cs` | 5/5 | 1.00 | 1.00 | 字节一致 |
| 174 | `Src/Core/Weapon.cs` | 61/150 | 0.20 | 0.08 | 重写·同职责 |
| 175 | `Src/Core/DamageData.cs` | 33/44 | 0.17 | 0.11 | 重写·同职责 |
| 176 | `Src/Core/TargetFlags.cs` | 33/19 | 0.00 | 0.18 | 同名·实现不同 |

### Src/Items

| # | 文件 | 行数 A/B | 成员重合 | 代码重合 | 状态 |
|---|---|---|---|---|---|
| 177 | `Src/Items/ItemBonus.cs` | 216/35 | 0.04 | 0.07 | 同名·实现不同 |

### Src/Skills

| # | 文件 | 行数 A/B | 成员重合 | 代码重合 | 状态 |
|---|---|---|---|---|---|
| 178 | `Src/Skills/Avatar.cs` | 46/50 | 0.27 | 0.15 | 重写·同职责 |
| 179 | `Src/Skills/Blink.cs` | 75/71 | 0.23 | 0.18 | 重写·同职责 |
| 180 | `Src/Skills/HolyLight.cs` | 66/62 | 0.18 | 0.10 | 重写·同职责 |
| 181 | `Src/Skills/ChainLightning.cs` | 93/69 | 0.17 | 0.03 | 重写·同职责 |
| 182 | `Src/Skills/DevotionAura.cs` | 22/52 | 0.11 | 0.14 | 同名·实现不同 |
| 183 | `Src/Skills/Evasion.cs` | 30/30 | 0.10 | 0.18 | 同名·实现不同 |
| 184 | `Src/Skills/CriticalStrike.cs` | 24/99 | 0.08 | 0.08 | 同名·实现不同 |
| 185 | `Src/Skills/EnduranceAura.cs` | 22/68 | 0.00 | 0.16 | 同名·实现不同 |
| 186 | `Src/Skills/Berserk.cs` | 34/22 | 0.00 | 0.08 | 同名·实现不同 |

### Src/Units

| # | 文件 | 行数 A/B | 成员重合 | 代码重合 | 状态 |
|---|---|---|---|---|---|
| 187 | `Src/Units/Unit.Lifecycle.cs` | 143/79 | 0.04 | 0.05 | 同名·能力差一个量级 |
| 188 | `Src/Units/Unit.Items.cs` | 305/19 | 0.04 | 0.02 | 同名·能力差一个量级 |
| 189 | `Src/Units/Unit.Combat.cs` | 47/206 | 0.03 | 0.02 | 同名·能力差一个量级 |
| 190 | `Src/Units/CargoTransport.cs` | 126/241 | 0.02 | 0.04 | 同名·能力差一个量级 |
| 191 | `Src/Units/Unit.cs` | 133/325 | 0.02 | 0.02 | 同名·能力差一个量级 |

---

## 5. 移位映射（Launcher 有，客户端在别的路径）

改名、搬目录、拆文件造成，共 75 对。

| # | Launcher | 客户端 | 类型 |
|---|---|---|---|
| 1 | `GameCore/L1_Adapter/Formats/Mpq/MpqArchive.cs` | `GameCore/L0_Foundation/ThirdParty/War3Net/War3Net.IO.Mpq/MpqArchive.cs` | `MpqArchive` |
| 2 | `GameCore/L3_Simulation/Simulation/Maps/W3eTerrain.cs` | `GameCore/L2_Core/MapBrowser/TerrainParser.cs` | `Corner` |
| 3 | `Src/Abilities/RuntimeAbilityFactory.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeAbilityFactory.cs` | `RuntimeAbilityFactory` |
| 4 | `Src/Skills/CAbilityBlightedGoldMine.cs` | `Src/Abilities/CAbilityGoldMineBase.cs` | `CAbilityBlightedGoldMine` |
| 5 | `Src/Skills/DarkConversion.cs` | `Src/Skills/CampaignAndAuraSkills.cs` | `DarkConversion` |
| 6 | `Src/Skills/DarkConversionBuff.cs` | `Src/Skills/CampaignAndAuraSkills.cs` | `DarkConversionBuff` |
| 7 | `Src/Skills/FarSight.cs` | `Src/Skills/CampaignAndAuraSkills.cs` | `FarSight` |
| 8 | `Src/Skills/FingerOfDeath.cs` | `Src/Skills/CampaignAndAuraSkills.cs` | `FingerOfDeath` |
| 9 | `Src/Skills/RuntimeAbsorbManaAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeDestroyerAbilities.cs` | `RuntimeAbsorbManaAbility` |
| 10 | `Src/Skills/RuntimeAntiMagicShellBuff.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeAntiMagicShellAbility.cs` | `RuntimeAntiMagicShellBuff` |
| 11 | `Src/Skills/RuntimeBattleStationsAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeStandDownAbility.cs` | `RuntimeBattleStationsAbility` |
| 12 | `Src/Skills/RuntimeBigBadVoodooAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeHumanOrcUltimates.cs` | `RuntimeBigBadVoodooAbility` |
| 13 | `Src/Skills/RuntimeBlizzardAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeAbilityFactory.cs` | `RuntimeBlizzardAbility` |
| 14 | `Src/Skills/RuntimeBunkerAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeCargoHoldAbility.cs` | `RuntimeBunkerAbility` |
| 15 | `Src/Skills/RuntimeCallToArmsAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeMilitiaAbility.cs` | `RuntimeCallToArmsAbility` |
| 16 | `Src/Skills/RuntimeCargoDropAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeCargoHoldAbility.cs` | `RuntimeCargoDropAbility` |
| 17 | `Src/Skills/RuntimeCargoLoadAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeCargoHoldAbility.cs` | `RuntimeCargoLoadAbility` |
| 18 | `Src/Skills/RuntimeCorrosiveBreathAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeTierThreeUnitAbilities.cs` | `RuntimeCorrosiveBreathAbility` |
| 19 | `Src/Skills/RuntimeDarkPortalAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeClassicSummonAndArmorAbilities.cs` | `RuntimeDarkPortalAbility` |
| 20 | `Src/Skills/RuntimeDarkRitualAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeUndeadHeroAbilities.cs` | `RuntimeDarkRitualAbility` |
| 21 | `Src/Skills/RuntimeDeathAndDecayAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeClassicHeroAbilities.cs` | `RuntimeDeathAndDecayAbility` |
| 22 | `Src/Skills/RuntimeDeathCoilAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeUndeadHeroAbilities.cs` | `RuntimeDeathCoilAbility` |
| 23 | `Src/Skills/RuntimeDeathPactAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeUndeadHeroAbilities.cs` | `RuntimeDeathPactAbility` |
| 24 | `Src/Skills/RuntimeDetonateAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeSystemSkills.cs` | `RuntimeDetonateAbility` |
| 25 | `Src/Skills/RuntimeDevourMagicAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeDestroyerAbilities.cs` | `RuntimeDevourMagicAbility` |
| 26 | `Src/Skills/RuntimeEarthquakeAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeHumanOrcUltimates.cs` | `RuntimeEarthquakeAbility` |
| 27 | `Src/Skills/RuntimeFanOfKnivesAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeAbilityFactory.cs` | `RuntimeFanOfKnivesAbility` |
| 28 | `Src/Skills/RuntimeFreezingBreathAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeTierThreeUnitAbilities.cs` | `RuntimeFreezingBreathAbility` |
| 29 | `Src/Skills/RuntimeFrostArmorAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeClassicSummonAndArmorAbilities.cs` | `RuntimeFrostArmorAbility` |
| 30 | `Src/Skills/RuntimeGrabTreeBuff.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeGrabTreeAbility.cs` | `RuntimeGrabTreeBuff` |
| 31 | `Src/Skills/RuntimeHarvestAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeSystemSkills.cs` | `RuntimeHarvestAbility` |
| 32 | `Src/Skills/RuntimeHealingWaveAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeClassicHeroAbilities.cs` | `RuntimeHealingWaveAbility` |
| 33 | `Src/Skills/RuntimeImpaleAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeUndeadHeroAbilities.cs` | `RuntimeImpaleAbility` |
| 34 | `Src/Skills/RuntimeManaBurnAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeNightElfHeroAbilities.cs` | `RuntimeManaBurnAbility` |
| 35 | `Src/Skills/RuntimeManaShieldBuff.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeManaShieldAbility.cs` | `RuntimeManaShieldBuff` |
| 36 | `Src/Skills/RuntimeMassTeleportAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeHumanOrcUltimates.cs` | `RuntimeMassTeleportAbility` |
| 37 | `Src/Skills/RuntimeMeatDropAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeMeatCargoAbilities.cs` | `RuntimeMeatDropAbility` |
| 38 | `Src/Skills/RuntimeMeatLoadAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeMeatCargoAbilities.cs` | `RuntimeMeatLoadAbility` |
| 39 | `Src/Skills/RuntimeMoonWellReplenishAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeRestorationAbilities.cs` | `RuntimeMoonWellReplenishAbility` |
| 40 | `Src/Skills/RuntimeOrbOfAnnihilationAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeDestroyerAbilities.cs` | `RuntimeOrbOfAnnihilationAbility` |
| 41 | `Src/Skills/RuntimePhoenixEggBuff.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimePhoenixAbility.cs` | `RuntimePhoenixEggBuff` |
| 42 | `Src/Skills/RuntimePlagueAuraAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeSystemSkills.cs` | `RuntimePlagueAuraAbility` |
| 43 | `Src/Skills/RuntimePossessionAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeTierThreeUnitAbilities.cs` | `RuntimePossessionAbility` |
| 44 | `Src/Skills/RuntimePulverizeAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeTierThreeUnitAbilities.cs` | `RuntimePulverizeAbility` |
| 45 | `Src/Skills/RuntimeRainOfChaosAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeClassicSummonAndArmorAbilities.cs` | `RuntimeRainOfChaosAbility` |
| 46 | `Src/Skills/RuntimeRejuvenationAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeRestorationAbilities.cs` | `RuntimeRejuvenationAbility` |
| 47 | `Src/Skills/RuntimeShadowMeldAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeWarlockStateSkills.cs` | `RuntimeShadowMeldAbility` |
| 48 | `Src/Skills/RuntimeSlowPoisonAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeWarlockStateSkills.cs` | `RuntimeSlowPoisonAbility` |
| 49 | `Src/Skills/RuntimeSpellStealAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeTierThreeUnitAbilities.cs` | `RuntimeSpellStealAbility` |
| 50 | `Src/Skills/RuntimeSpiritLinkAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeWarlockStateSkills.cs` | `RuntimeSpiritLinkAbility` |
| 51 | `Src/Skills/RuntimeSpiritLinkBuff.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeWarlockStateSkills.cs` | `RuntimeSpiritLinkBuff` |
| 52 | `Src/Skills/RuntimeSpiritOfVengeanceAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeClassicSummonAndArmorAbilities.cs` | `RuntimeSpiritOfVengeanceAbility` |
| 53 | `Src/Skills/RuntimeStatueReplenishAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeRestorationAbilities.cs` | `RuntimeStatueReplenishAbility` |
| 54 | `Src/Skills/RuntimeStormBoltAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeAbilityFactory.cs` | `RuntimeStormBoltAbility` |
| 55 | `Src/Skills/RuntimeSubmergeAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeWarlockStateSkills.cs` | `RuntimeSubmergeAbility` |
| 56 | `Src/Skills/RuntimeTauntAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeSystemSkills.cs` | `RuntimeTauntAbility` |
| 57 | `Src/Skills/RuntimeTrueshotAuraAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeCommandAuraAbility.cs` | `RuntimeTrueshotAuraAbility` |
| 58 | `Src/Skills/RuntimeUnholyFrenzyAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeArmyTemplateAbilities.cs` | `RuntimeUnholyFrenzyAbility` |
| 59 | `Src/Skills/RuntimeUnholyFrenzyBuff.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeArmyTemplateAbilities.cs` | `RuntimeUnholyFrenzyBuff` |
| 60 | `Src/Skills/RuntimeUnstableConcoctionAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeArmyTemplateAbilities.cs` | `RuntimeUnstableConcoctionAbility` |
| 61 | `Src/Skills/RuntimeVengeanceAbility.cs` | `GameCore/L3_Simulation/Simulation/Configuration/RuntimeClassicSummonAndArmorAbilities.cs` | `RuntimeVengeanceAbility` |
| 62 | `Src/Skills/SoulPreservation.cs` | `Src/Skills/CampaignAndAuraSkills.cs` | `SoulPreservation` |
| 63 | `Src/Skills/SoulPreservationBuff.cs` | `Src/Skills/CampaignAndAuraSkills.cs` | `SoulPreservationBuff` |
| 64 | `Src/Skills/VampiricAura.cs` | `Src/Skills/CampaignAndAuraSkills.cs` | `VampiricAura` |
| 65 | `Src/World/FogOfWar.cs` | `Src/Core/FogOfWar.cs` | `FogOfWar` |
| 66 | `Src/World/World.DamageMembers.cs` | `Src/Core/World.DamageMembers.cs` | `World` |
| 67 | `Src/World/World.HeroSelectionOrder.cs` | `Src/Core/World.HeroSelectionOrder.cs` | `World` |
| 68 | `Src/World/World.MechanismMembers.cs` | `Src/Core/World.MechanismMembers.cs` | `World` |
| 69 | `Src/World/World.MovementEvents.cs` | `Src/Core/World.MovementEvents.cs` | `World` |
| 70 | `Src/World/World.Rally.cs` | `Src/Core/World.Rally.cs` | `World` |
| 71 | `Src/World/World.RelationMembers.cs` | `Src/Core/World.RelationMembers.cs` | `World` |
| 72 | `Src/World/World.RepulseMembers.cs` | `Src/Core/World.RepulseMembers.cs` | `World` |
| 73 | `Src/World/World.Targeting.cs` | `Src/Core/World.Targeting.cs` | `World` |
| 74 | `Src/World/World.UnitLookup.cs` | `Src/Core/World.UnitLookup.cs` | `World` |
| 75 | `Src/World/World.cs` | `Src/Core/World.cs` | `World` |

### 客户端同名类散在多文件（34）

这些是客户端用 partial 拆开的场景类，Launcher 合成一个文件。按目录找。

| # | Launcher | 客户端类型 | 客户端分布 |
|---|---|---|---|
| 1 | `GameCore/L4_Composition/Game/Scenes/War3GameScene/Ai.cs` | `War3GameScene` | `GameCore/L4_Composition/Game/Scenes/War3GameScene/` 下 38 个文件 |
| 2 | `GameCore/L4_Composition/Game/Scenes/War3GameScene/Commands.cs` | `War3GameScene` | `GameCore/L4_Composition/Game/Scenes/War3GameScene/` 下 38 个文件 |
| 3 | `GameCore/L4_Composition/Game/Scenes/War3GameScene/Effects.cs` | `War3GameScene` | `GameCore/L4_Composition/Game/Scenes/War3GameScene/` 下 38 个文件 |
| 4 | `GameCore/L4_Composition/Game/Scenes/War3GameScene/Environment.cs` | `War3GameScene` | `GameCore/L4_Composition/Game/Scenes/War3GameScene/` 下 38 个文件 |
| 5 | `GameCore/L4_Composition/Game/Scenes/War3GameScene/Floats.cs` | `War3GameScene` | `GameCore/L4_Composition/Game/Scenes/War3GameScene/` 下 38 个文件 |
| 6 | `GameCore/L4_Composition/Game/Scenes/War3GameScene/Lightning.cs` | `War3GameScene` | `GameCore/L4_Composition/Game/Scenes/War3GameScene/` 下 38 个文件 |
| 7 | `GameCore/L4_Composition/Game/Scenes/War3GameScene/Lobby.cs` | `War3GameScene` | `GameCore/L4_Composition/Game/Scenes/War3GameScene/` 下 38 个文件 |
| 8 | `GameCore/L4_Composition/Game/Scenes/War3GameScene/Melee.cs` | `War3GameScene` | `GameCore/L4_Composition/Game/Scenes/War3GameScene/` 下 38 个文件 |
| 9 | `GameCore/L4_Composition/Game/Scenes/War3GameScene/Production.cs` | `War3GameScene` | `GameCore/L4_Composition/Game/Scenes/War3GameScene/` 下 38 个文件 |
| 10 | `Platform/Godot/Extensions/MapDocument.cs` | `Entry` | `GameCore/L4_Composition/Game/Rendering/` 下 16 个文件 |
| 11 | `Src/Units/Unit.Abilities.cs` | `Unit` | `Src/Units/` 下 15 个文件 |
| 12 | `Src/Units/Unit.Channel.cs` | `Unit` | `Src/Units/` 下 15 个文件 |
| 13 | `Src/Units/Unit.Illusion.cs` | `Unit` | `Src/Units/` 下 15 个文件 |
| 14 | `Src/Units/Unit.Morph.cs` | `Unit` | `Src/Units/` 下 15 个文件 |
| 15 | `Src/Units/Unit.Ownership.cs` | `Unit` | `Src/Units/` 下 15 个文件 |
| 16 | `Src/Units/Unit.Slows.cs` | `Unit` | `Src/Units/` 下 15 个文件 |
| 17 | `Src/World/World.Abilities.cs` | `World` | `Src/Core/` 下 14 个文件 |
| 18 | `Src/World/World.Blight.cs` | `World` | `Src/Core/` 下 14 个文件 |
| 19 | `Src/World/World.BuffMembers.cs` | `World` | `Src/Core/` 下 14 个文件 |
| 20 | `Src/World/World.Channels.cs` | `World` | `Src/Core/` 下 14 个文件 |
| 21 | `Src/World/World.CorpseRevive.cs` | `World` | `Src/Core/` 下 14 个文件 |
| 22 | `Src/World/World.CreepSleep.cs` | `World` | `Src/Core/` 下 14 个文件 |
| 23 | `Src/World/World.DeathTriggers.cs` | `World` | `Src/Core/` 下 14 个文件 |
| 24 | `Src/World/World.FogRevealMembers.cs` | `World` | `Src/Core/` 下 14 个文件 |
| 25 | `Src/World/World.GroundSpells.cs` | `World` | `Src/Core/` 下 14 个文件 |
| 26 | `Src/World/World.GroupMovement.cs` | `World` | `Src/Core/` 下 14 个文件 |
| 27 | `Src/World/World.HeroRevive.cs` | `World` | `Src/Core/` 下 14 个文件 |
| 28 | `Src/World/World.Illusions.cs` | `World` | `Src/Core/` 下 14 个文件 |
| 29 | `Src/World/World.Items.cs` | `World` | `Src/Core/` 下 14 个文件 |
| 30 | `Src/World/World.Morph.cs` | `World` | `Src/Core/` 下 14 个文件 |
| 31 | `Src/World/World.Orders.cs` | `World` | `Src/Core/` 下 14 个文件 |
| 32 | `Src/World/World.PhoenixEgg.cs` | `World` | `Src/Core/` 下 14 个文件 |
| 33 | `Src/World/World.Repair.cs` | `World` | `Src/Core/` 下 14 个文件 |
| 34 | `Src/World/World.WeaponSystem.cs` | `World` | `Src/Core/` 下 14 个文件 |

---

## 6. 仅 Launcher 有（客户端无同类名）

共 119 个，按目录收拢。主要是探针、AI、地图导入、格式解码这类对战外围能力。


**GameCore/L1_Adapter**（10）

`AdpcmDecode.cs`、`HuffmanDecode.cs`、`HuffmanTables.cs`、`LsbBitReader.cs`、`MpqCrypto.cs`、`MpqWriter.cs`、`PkwareExplode.cs`、`SlkTable.cs`、`War3Strings.cs`、`WtsDocument.cs`

**GameCore/L2_Core**（1）

`MapBrowser.cs`

**GameCore/L3_Simulation/Configuration**（9）

`AbilityCatalog.cs`、`FogRules.cs`、`HeroRules.cs`、`ItemCatalog.cs`、`LightningTable.cs`、`MeleeRules.cs`、`MiscRules.cs`、`TechTree.cs`、`War3Text.cs`

**GameCore/L3_Simulation/Maps**（5）

`MapSource.cs`、`W3iInfo.cs`、`W3xBinaryReader.cs`、`W3xBinaryWriter.cs`、`W3xImports.cs`

**GameCore/L4_Composition/Game/Diagnostics**（23）

`AttachmentProbe.cs`、`CombatProbe.Ai.cs`、`CombatProbe.BloodMage.cs`、`CombatProbe.Channels.cs`、`CombatProbe.ClientSkills.cs`、`CombatProbe.CreepAbilities.cs`、`CombatProbe.Formation.cs`、`CombatProbe.Forms.cs`、`CombatProbe.Illusions.cs`、`CombatProbe.ItemUse.cs`、`CombatProbe.MeleeUltimates.cs`、`CombatProbe.MorphSkills.cs`、`CombatProbe.Orders.cs`、`CombatProbe.RacialSkills.cs`、`CombatProbe.Skills.cs`、`CombatProbe.States.cs`、`CombatProbe.Tavern.cs`、`CombatProbe.TavernItems.cs`、`CombatProbe.Tinker.cs`、`CombatProbe.UndeadSkills.cs`、`CombatProbe.UnitSkills.cs`、`CombatProbe.cs`、`WorldTest.cs`

**GameCore/L4_Composition/Game/Environment**（6）

`BlightMask.cs`、`CliffTextureSet.cs`、`LayerAtlas.cs`、`MapWorldBuilder.cs`、`SkyDomeRenderer.cs`、`War3WaterConfig.cs`

**GameCore/L4_Composition/Game/Presentation**（2）

`AttachmentPoints.cs`、`EffectArtTable.cs`

**GameCore/L4_Composition/Game/Scenes**（1）

`AiCounters.cs`

**GameCore/L4_Composition/Game/UI**（2）

`GameOverPanel.cs`、`LoadingScreen.cs`

**Platform/Godot**（10）

`LauncherApp.cs`、`Assets.cs`、`DoodadMeshLibrary.cs`、`ModelLibrary.cs`、`PathResolve.cs`、`UnitMeshLibrary.cs`、`War3Assets.cs`、`AiTactics.cs`、`GmapImporter.cs`、`GmapPackage.cs`

**Src/Abilities**（2）

`AuraBonus.cs`、`UnitBuffs.cs`

**Src/Core**（5）

`CombatCodes.cs`、`OrbEffect.cs`、`GroupFormation.cs`、`PathGrid.cs`、`SpellTargets.cs`

**Src/Items**（4）

`Item.cs`、`ItemConsumable.cs`、`ItemUseEffect.cs`、`ItemUseRules.cs`

**Src/Skills**（38）

`AbolishMagic.cs`、`AntiMagicShield.cs`、`AntiMagicShieldBuff.cs`、`Bladestorm.cs`、`BlightDispel.cs`、`CannibalizeBuff.cs`、`CloudOfFogSpell.cs`、`DetectPassive.cs`、`Devour.cs`、`ElunesGrace.cs`、`Ensnare.cs`、`FactorySpawnSpell.cs`、`FlakCannon.cs`、`FlameStrikeBurn.cs`、`FragShards.cs`、`Frenzy.cs`、`FrostBolt.cs`、`Graveyard.cs`、`InventoryPassive.cs`、`ItemAura.cs`、`LifeDrain.cs`、`LocustAgent.cs`、`LocustSwarmRelease.cs`、`MassDispel.cs`、`Metamorphosis.cs`、`MineBlast.cs`、`Phoenix.cs`、`RaiseDead.cs`、`RevealArea.cs`、`RuntimeParasiteBuff.cs`、`SerpentWard.cs`、`SiphonMana.cs`、`SpiritLinkGroup.cs`、`StasisTrapSpell.cs`、`SummonUnit.cs`、`TransportHold.cs`、`UnitTypeUpgrade.cs`、`Web.cs`

**Src/World**（1）

`TreeState.cs`

---

## 7. 仅客户端有

客户端运行时代码 1505 个文件，与 Launcher 有明确对应的 266 个（191 同路径 + 75 移位）。其余按目录计数前 15：

| 目录 | 文件数 |
|---|---|
| `GameCore/L4_Composition/Game` | 390 |
| `GameCore/L3_Simulation/Simulation` | 376 |
| `Src/Core/Pathing` | 245 |
| `GameCore/L1_Adapter/Formats` | 76 |
| `GameCore/L0_Foundation/ThirdParty` | 67 |
| `GameCore/L4_Composition/Android` | 58 |
| `GameCore/L2_Core/UI` | 30 |
| `GameCore/L1_Adapter/RHI.OpenGL` | 14 |
| `GameCore/L4_Composition/Headless` | 8 |
| `GameCore/L0_Foundation/RHI` | 7 |
| `GameCore/L2_Core/MapBrowser` | 6 |
| `GameCore/L0_Foundation/Input` | 3 |
| `Src/Abilities/Ability.cs` | 1 |
| `Src/Abilities/AbilityDisableType.cs` | 1 |
| `Src/Abilities/AbilityTargetMode.cs` | 1 |

对战侧的整块缺口（`Input/` 20、`UI/` 86、`Environment/` 34、`Rendering/` 60）按能力条目列在差异文档 §3，这里不重复。

---

## 8. 重跑与新增照搬

```bash
cd Launcher && find . -name '*.cs' -not -path './.godot/*' | sed 's|^\./||' | sort > /tmp/launcher_files.txt
cd test/Open-Warcraft3 && find Src GameCore -name '*.cs' -not -path '*/obj/*' | sed 's|^\./||' | sort > /tmp/client_files.txt
comm -12 /tmp/launcher_files.txt /tmp/client_files.txt > /tmp/common.txt   # 191 对

# 检查 4 个镜像文件有没有漂移（返回 0 = 仍是逐字节一致）
for f in Src/Core/AttackType.cs Src/Core/DefenseType.cs Src/Core/DamageType.cs Src/Abilities/AbilityTargetMode.cs; do
  cmp -s "Launcher/$f" "test/Open-Warcraft3/$f" || echo "漂移: $f"
done

# 判定某文件能否照搬：覆盖 + 全量重编，看退出码
cp test/Open-Warcraft3/<path> Launcher/<path>
cd Launcher && dotnet build -t:Rebuild -v q --nologo; echo "exit=$?"
```
三条硬要求：
1. 用 `-t:Rebuild`：增量构建改文件后可能直接跳过，报「已成功生成」而没编译。
2. 看退出码：终端是中文 locale，`grep "error "` 抓不到「错误 CS0246」，本轮因此误判过 4 个文件。
3. 覆盖后跑 `--state-probe` 与 `--combat-probe`，两者全过才算成功。

---

## 9. 把 4 提到 150 的路线

单独一篇：`Launcher字节一致化路线.md`。

结论摘要：187 个不一致文件共 8,652 条编译错误，49% 是缺类型（`Vec2` 131 个文件、`TargetFlags` 71 个），33% 是缺虚成员（`OnLevelUp/OnLevelDown` 各 96、`Ability.Name` 非虚卡住 120 个）。天花板 159 个，L4 的 32 个走不同渲染抽象，不做。
