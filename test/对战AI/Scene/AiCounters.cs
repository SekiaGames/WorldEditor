using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>
/// 对手阵容快照：按护甲类型累计权重，另外记空中与英雄数量。Ai.cs 每 2 秒刷一次。
/// 中立野怪（12/15）不算对手军队，否则前期会被地图野点带偏。
/// </summary>
public sealed class EnemyComposition
{
    public readonly float[] ArmorWeight = new float[8];
    public int Air;
    public int Ground;
    public int Heroes;
    public float Casters;
    public float Auras;
    public float Total;

    public bool IsEmpty => Total <= 0f;

    public DefenseType DominantDefense
    {
        get
        {
            int best = 0;
            for (int index = 1; index < ArmorWeight.Length; index++)
            {
                if (ArmorWeight[index] > ArmorWeight[best]) best = index;
            }
            return (DefenseType)best;
        }
    }
}

/// <summary>
/// AI 换兵种的打分器：候选兵种的攻击类型打对手护甲构成的平均倍率，
/// 对手有空中而它打不到空中要扣分，带溅射的再加一点（适合打密集地面）。
/// 只给 Ai.cs 用，模拟层不依赖这里。
/// </summary>
public static class AiCounters
{
    public static EnemyComposition Analyze(IReadOnlyList<Unit> units, int player, Func<int, int, bool> allied)
    {
        var composition = new EnemyComposition();
        foreach (Unit unit in units)
        {
            if (unit == null || !unit.Alive || unit.Type == null) continue;
            if (unit.IsBuilding) continue;
            if (unit.Player == player) continue;
            if (unit.Player is MeleeRules.PlayerNeutralHostile or MeleeRules.PlayerNeutralPassive) continue;
            if (allied != null && allied(player, unit.Player)) continue;

            float weight = unit.IsWorker ? 0.5f : 1f;
            composition.ArmorWeight[(int)unit.Type.DefenseType] += weight;
            composition.Total += weight;
            if (unit.Type.Flying) composition.Air++;
            else composition.Ground++;
            if (unit.IsHero) composition.Heroes++;
            foreach (Ability ability in unit.Abilities)
            {
                if (ability == null || !ability.Learned) continue;
                if (ability.IsAura) composition.Auras += weight;
                else if (!ability.IsPassive && ability.ManaCost > 0f) composition.Casters += weight;
            }
        }
        return composition;
    }

    public static float Score(UnitType candidate, EnemyComposition enemy)
    {
        if (candidate == null || enemy == null || enemy.IsEmpty) return 0f;
        if (!candidate.CanAttack) return 0f;

        float weighted = 0f;
        for (int column = 0; column < enemy.ArmorWeight.Length; column++)
        {
            float weight = enemy.ArmorWeight[column];
            if (weight <= 0f) continue;

            DefenseType defense = (DefenseType)column;
            float best = 0f;
            if (candidate.Weapon.Enabled)
            {
                best = Math.Max(best, War3GameplayConfig.Default.ClassRatio(candidate.Weapon.AttackType, defense));
            }
            if (candidate.Weapon2 != null && candidate.Weapon2.Enabled)
            {
                best = Math.Max(best, War3GameplayConfig.Default.ClassRatio(candidate.Weapon2.AttackType, defense));
            }
            weighted += best * weight;
        }

        float score = weighted / Math.Max(1f, enemy.Total);
        if (enemy.Air > 0 && !candidate.CanTarget(TargetKind.Air))
        {
            score -= 0.6f * enemy.Air / Math.Max(1f, enemy.Total) + 0.2f;
        }
        if (candidate.Weapon.SplashFull > 0f && enemy.Ground >= 3) score += 0.1f;

        if (enemy.Casters > 0f && enemy.Total > 0f)
        {
            float share = Mathf.Clamp(enemy.Casters / enemy.Total, 0f, 1f);
            score += 0.08f * share * Mathf.Clamp((candidate.Hp - 300f) / 900f, 0f, 1f);
        }
        if (enemy.Auras > 0f) score += 0.02f;

        float dps = (candidate.Weapon.MinDamage + candidate.Weapon.MaxDamage) * 0.5f
            / Mathf.Max(0.5f, candidate.Weapon.Cooldown);
        float cost = Mathf.Max(1f, candidate.GoldCost + candidate.LumberCost * 2f);
        float efficiency = (candidate.Hp + dps * 60f) / cost;
        score += 0.2f * Mathf.Clamp(efficiency / 8f, 0f, 1f);
        return score;
    }

    public static string Describe(EnemyComposition enemy)
    {
        if (enemy == null || enemy.IsEmpty) return "对手无军队";

        var parts = new List<string>();
        for (int column = 0; column < enemy.ArmorWeight.Length; column++)
        {
            if (enemy.ArmorWeight[column] <= 0f) continue;
            parts.Add($"{DefenseName((DefenseType)column)}x{enemy.ArmorWeight[column]:0.#}");
        }

        string text = $"对手 {string.Join(" ", parts)}";
        if (enemy.Air > 0) text += $" 空中{enemy.Air}";
        if (enemy.Heroes > 0) text += $" 英雄{enemy.Heroes}";
        if (enemy.Casters > 0f) text += $" 法师{enemy.Casters:0.#}";
        if (enemy.Auras > 0f) text += $" 光环{enemy.Auras:0.#}";
        return text;
    }

    public static string DefenseName(DefenseType defense)
    {
        return defense switch
        {
            DefenseType.Small => "轻甲",
            DefenseType.Medium => "中甲",
            DefenseType.Large => "重甲",
            DefenseType.Fort => "城甲",
            DefenseType.Unarmored => "无甲",
            DefenseType.Hero => "英雄甲",
            DefenseType.Divine => "神圣甲",
            _ => "无类型",
        };
    }
}