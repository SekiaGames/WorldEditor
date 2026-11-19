extern alias Sim;
using static Sim::OpenWC3.JassToSharp.Ai.Data.HumanCodes;
using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    private const float AttackTickSeconds = 1f;
    private const float AttackFormTimeout = 25f;
    private const float AttackMarchTimeout = 150f;
    private const float AttackCreepCampRadius = 600f;

    private static readonly string[] HumanAttackUnitOrder =
    {
        "hfoo", "hkni", "hrif", "hmpr", "hsor", "hgry", "hgyr", "hspt", "hdhw",
    };

    private sealed class HumanAttackWave
    {
        public bool GateOpen;
        public bool Forming;
        public bool Marching;
        public bool TakeExp;
        public int Waves;
        public int ExpSeen;
        public float Sleep;
        public float Tick;
        public float PhaseTimer;
        public Vector3 Staging;
        public Vector3 Goal;
        public Unit Target;
        public readonly HashSet<int> Members = new();
    }

    private sealed class HumanCreepCamp
    {
        public Vector3 Center;
        public Unit Anchor;
        public int Strength;
        public bool Flying;
        public readonly List<Unit> Units = new();
    }

    private int _humanAttackLogBudget = 40;

    private bool IsHumanAttackGroupMember(int player, Unit unit)
    {
        if (unit == null) return false;
        if (!_meleeAiStates.TryGetValue(player, out MeleeAiState state)) return false;
        return state.Attack.Members.Contains(unit.SimulationId);
    }

    private void LogHumanWave(PlayerSlot slot, string message)
    {
        if (_humanAttackLogBudget <= 0) return;
        _humanAttackLogBudget--;
        GD.Print($"[HUMAN-AI] P{slot.Id} {message}");
    }

    private void TickHumanAttack(PlayerSlot slot, MeleeAiState state, float delta)
    {
        HumanAttackWave wave = state.Attack;
        wave.Tick += delta;
        if (wave.Tick < AttackTickSeconds) return;

        float step = wave.Tick;
        wave.Tick = 0f;

        if (_world == null || _gameOver) return;

        if (wave.Sleep > 0f)
        {
            wave.Sleep = Mathf.Max(0f, wave.Sleep - step);
            return;
        }

        if (!wave.GateOpen)
        {
            if (!HumanAttackGateMet(slot, state)) return;
            wave.GateOpen = true;
            wave.Sleep = (IsHumanNewbie ? 240f : 0f) + HumanAttackStagger(slot.Id);
            LogHumanWave(slot, $"进攻序列启动，首波 {wave.Sleep:0} 秒后");
            return;
        }

        if (HumanTownThreatened(slot))
        {
            wave.Sleep = 2f;
            return;
        }

        if (wave.Marching)
        {
            UpdateHumanMarch(slot, state);
            return;
        }

        if (wave.Forming)
        {
            UpdateHumanForm(slot, state);
            return;
        }

        BeginHumanWave(slot, state);
    }

    private bool HumanAttackGateMet(PlayerSlot slot, MeleeAiState state)
    {
        if (state.Hero1 == null || !state.Counters.Hero1Done) return false;
        return CountUnits(slot, HumanFootman) >= 2;
    }

    private float HumanAttackStagger(int player)
    {
        var ids = new List<int>(_active);
        ids.Sort();
        int index = ids.IndexOf(player);
        if (index <= 0 || ids.Count <= 1) return 0f;
        return 2f * index / ids.Count;
    }

    private void BeginHumanWave(PlayerSlot slot, MeleeAiState state)
    {
        HumanAttackWave wave = state.Attack;

        if (HumanArmyReadiness(slot) < 0.55f)
        {
            wave.Sleep = 15f;
            return;
        }

        Unit hall = FindHall(slot.Id);
        if (hall == null)
        {
            wave.Sleep = 10f;
            return;
        }

        if (!TryChooseHumanAttackTarget(slot, state, out Unit target, out Vector3 goal))
        {
            wave.Sleep = 15f;
            return;
        }

        List<Unit> members = CollectHumanForce(slot, state);
        if (members.Count == 0)
        {
            wave.Sleep = 12f;
            return;
        }

        Vector3 forward = goal - hall.Position;
        forward.Y = 0f;
        if (forward.LengthSquared() < 1f) forward = new Vector3(0f, 0f, 1f);
        forward = forward.Normalized();

        wave.Staging = hall.Position + forward * 320f;
        wave.Goal = goal;
        wave.Target = target;
        wave.PhaseTimer = 0f;
        wave.Members.Clear();
        foreach (Unit unit in members) wave.Members.Add(unit.SimulationId);
        wave.Forming = true;

        Dictionary<Unit, Vector3> slots = GroupFormation.Layout(members, wave.Staging, forward);
        foreach (Unit unit in members)
        {
            Vector3 spot = slots.TryGetValue(unit, out Vector3 placed) ? placed : wave.Staging;
            _world.IssueOrder(unit, UnitOrder.Move(spot), false);
        }

        LogHumanWave(slot, $"第 {wave.Waves + 1} 波集结 {members.Count} 个，目标 {HumanAttackTargetName(target)}"
            + $"({goal.X:0},{goal.Z:0})");
    }

    private List<Unit> CollectHumanForce(PlayerSlot slot, MeleeAiState state)
    {
        var members = new List<Unit>();
        var taken = new HashSet<int>();

        AddHumanGroupType(slot, state.Hero1, 1, members, taken);
        AddHumanGroupType(slot, state.Hero2, 1, members, taken);
        AddHumanGroupType(slot, state.Hero3, 1, members, taken);

        foreach (string id in HumanAttackUnitOrder)
        {
            int owned = CountUnits(slot, id);
            int want = Mathf.Min(owned * 3 / 4, 20);
            AddHumanGroupType(slot, id, want, members, taken);
        }
        return members;
    }

    private void AddHumanGroupType(PlayerSlot slot, string unitId, int want,
        List<Unit> members, HashSet<int> taken)
    {
        if (string.IsNullOrEmpty(unitId) || want <= 0) return;

        var candidates = new List<Unit>();
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Player != slot.Id) continue;
            if (unit.IsBuilding || unit.IsWorker) continue;
            if (!string.Equals(unit.Type?.Id, unitId, StringComparison.OrdinalIgnoreCase)) continue;
            if (taken.Contains(unit.SimulationId)) continue;
            candidates.Add(unit);
        }

        candidates.Sort((left, right) => right.SimulationId.CompareTo(left.SimulationId));
        int added = 0;
        foreach (Unit unit in candidates)
        {
            if (added >= want) break;
            members.Add(unit);
            taken.Add(unit.SimulationId);
            added++;
        }
    }

    private void UpdateHumanForm(PlayerSlot slot, MeleeAiState state)
    {
        HumanAttackWave wave = state.Attack;
        wave.PhaseTimer += AttackTickSeconds;

        int alive = 0;
        int gathered = 0;
        float health = 0f;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || !wave.Members.Contains(unit.SimulationId)) continue;
            alive++;
            health += unit.MaxHp > 0f ? unit.Hp / unit.MaxHp : 1f;
            if (PlanarDistance(unit.Position, wave.Staging) <= 700f) gathered++;
        }

        if (alive == 0)
        {
            LogHumanWave(slot, "集结阶段部队全灭，重新招集");
            EndHumanWave(slot, state, 12f);
            return;
        }

        float readiness = health / alive;
        bool ready = gathered >= alive && readiness >= 0.5f;
        if (!ready && wave.PhaseTimer < AttackFormTimeout) return;

        int ordered = 0;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || !wave.Members.Contains(unit.SimulationId)) continue;
            _world.IssueOrder(unit, UnitOrder.AttackMove(wave.Goal), false);
            ordered++;
        }

        wave.Forming = false;
        wave.Marching = true;
        wave.PhaseTimer = 0f;
        LogHumanWave(slot, $"出击 {ordered} 个，健康 {readiness * 100f:0}%"
            + $"（{(ready ? "全员到位" : "超时出发")}）");
    }

    private void UpdateHumanMarch(PlayerSlot slot, MeleeAiState state)
    {
        HumanAttackWave wave = state.Attack;
        wave.PhaseTimer += AttackTickSeconds;

        int alive = 0;
        int arrived = 0;
        float health = 0f;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || !wave.Members.Contains(unit.SimulationId)) continue;
            alive++;
            health += unit.MaxHp > 0f ? unit.Hp / unit.MaxHp : 1f;
            if (PlanarDistance(unit.Position, wave.Goal) <= 640f) arrived++;
        }

        if (alive == 0)
        {
            LogHumanWave(slot, $"第 {wave.Waves + 1} 波全灭");
            EndHumanWave(slot, state, 20f);
            return;
        }

        float readiness = health / alive;
        if (readiness <= 0.45f)
        {
            ReturnHumanWave(slot, state, $"残血 {readiness * 100f:0}%");
            return;
        }

        bool enemyNear = HumanHostileNear(wave.Goal, 700f, slot.Id);
        if (!enemyNear && arrived >= Mathf.CeilToInt(alive * 0.6f))
        {
            LogHumanWave(slot, $"第 {wave.Waves + 1} 波目标清空，收兵（剩 {alive} 个）");
            ReturnHumanWave(slot, state, "目标已清");
            return;
        }

        if (wave.PhaseTimer >= AttackMarchTimeout)
        {
            ReturnHumanWave(slot, state, "超时");
            return;
        }

        if ((int)wave.PhaseTimer % 5 == 0) AssignHumanSiegeTargets(wave, slot);
    }

    private void AssignHumanSiegeTargets(HumanAttackWave wave, PlayerSlot slot)
    {
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || !wave.Members.Contains(unit.SimulationId)) continue;
            if (unit.Target != null || unit.MoveTarget.HasValue) continue;

            Unit building = null;
            float best = 900f;
            foreach (Unit other in _allUnits)
            {
                if (other == null || !other.Alive || !other.IsBuilding) continue;
                if (other.Player == slot.Id || AreAllied(other.Player, slot.Id)) continue;
                float distance = unit.DistanceTo(other);
                if (distance >= best) continue;
                best = distance;
                building = other;
            }
            if (building == null) continue;
            _world.IssueOrder(unit, UnitOrder.Attack(building), false);
        }
    }

    private void ReturnHumanWave(PlayerSlot slot, MeleeAiState state, string reason)
    {
        HumanAttackWave wave = state.Attack;
        Unit hall = FindHall(slot.Id);
        int sent = 0;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || !wave.Members.Contains(unit.SimulationId)) continue;
            if (hall != null) _world.IssueOrder(unit, UnitOrder.Move(hall.Position), false);
            sent++;
        }
        LogHumanWave(slot, $"第 {wave.Waves + 1} 波收队 {sent} 个（{reason}）");
        EndHumanWave(slot, state, 25f);
    }

    private void EndHumanWave(PlayerSlot slot, MeleeAiState state, float sleep)
    {
        HumanAttackWave wave = state.Attack;
        wave.Waves++;
        wave.Forming = false;
        wave.Marching = false;
        wave.PhaseTimer = 0f;
        wave.Target = null;
        wave.Members.Clear();
        wave.Sleep = sleep + (IsHumanNewbie ? 60f : 0f);
    }

    private float HumanArmyReadiness(PlayerSlot slot)
    {
        int count = 0;
        float health = 0f;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Player != slot.Id) continue;
            if (unit.IsBuilding || unit.IsWorker) continue;
            count++;
            health += unit.MaxHp > 0f ? unit.Hp / unit.MaxHp : 1f;
        }
        return count == 0 ? 0f : health / count;
    }

    private bool HumanTownThreatened(PlayerSlot slot)
    {
        foreach (Unit intruder in _allUnits)
        {
            if (intruder == null || !intruder.Alive || intruder.IsBuilding) continue;
            if (intruder.Player == slot.Id || AreAllied(intruder.Player, slot.Id)) continue;
            if (intruder.Player == MeleeRules.PlayerNeutralHostile) continue;
            if (intruder.Player == MeleeRules.PlayerNeutralPassive) continue;
            foreach (Unit own in _allUnits)
            {
                if (own == null || !own.Alive || own.Player != slot.Id || !own.IsBuilding) continue;
                if (own.DistanceTo(intruder) <= 900f) return true;
            }
        }
        return false;
    }

    private bool HumanHostileNear(Vector3 point, float radius, int player)
    {
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive) continue;
            if (unit.Player == player || AreAllied(unit.Player, player)) continue;
            if (unit.Player == MeleeRules.PlayerNeutralPassive) continue;
            if (PlanarDistance(unit.Position, point) <= radius) return true;
        }
        return false;
    }

    private bool TryChooseHumanAttackTarget(PlayerSlot slot, MeleeAiState state,
        out Unit target, out Vector3 goal)
    {
        target = null;
        goal = Vector3.Zero;

        MeleeAiCounters counters = state.Counters;
        int forceLevel = HumanForceLevel(counters);
        int maxCreeps = forceLevel * 4 / 5;
        int minCreeps = Mathf.Max(0, maxCreeps - 10);
        bool needsExp = state.Attack.TakeExp && (forceLevel >= 9 || counters.GoldOwned < 2000);
        bool hasSiege = forceLevel >= 40 || counters.MortarDone > 0 || counters.TankDone > 0;
        bool allowAirCreeps = counters.RiflemanDone + counters.HawkDone
            + 2 * counters.GyroDone + counters.GryphonDone >= 3;

        if (needsExp)
        {
            Unit mine = FindNextExpansionMine(slot.Id);
            if (mine != null)
            {
                HumanCreepCamp guard = NearestHumanCreepCamp(mine.Position, allowAirCreeps);
                if (guard != null && guard.Strength > 0 && guard.Center.DistanceTo(mine.Position) <= 1200f)
                {
                    state.Attack.TakeExp = false;
                    target = guard.Anchor;
                    goal = guard.Center;
                    LogHumanWave(slot, $"抢矿：先清金矿({mine.Position.X:0},{mine.Position.Z:0})"
                        + $" 旁野怪 强度 {guard.Strength}");
                    return true;
                }
            }
        }

        if (!IsHumanNewbie)
        {
            int megaPlayer = FindHumanMegaTargetPlayer(slot);
            if (megaPlayer >= 0)
            {
                Unit building = NearestHumanEnemyBuilding(slot, megaPlayer);
                if (building != null)
                {
                    target = building;
                    goal = building.Position;
                    LogHumanWave(slot, $"对手 P{megaPlayer} 兵力薄弱，全力压上主基地");
                    return true;
                }
            }
        }

        Unit expansion = FindHumanEnemyExpansion(slot);
        if (expansion != null && (hasSiege || !HumanIsTowered(expansion)))
        {
            int minimum = IsHumanNewbie ? 3 : 0;
            if (state.Attack.ExpSeen >= minimum)
            {
                state.Attack.ExpSeen = 0;
                target = expansion;
                goal = expansion.Position;
                LogHumanWave(slot, $"拆对手扩张({expansion.Position.X:0},{expansion.Position.Z:0})");
                return true;
            }
            state.Attack.ExpSeen++;
        }

        if (hasSiege)
        {
            Unit town = FindHumanEnemyTown(slot);
            if (town != null)
            {
                target = town;
                goal = town.Position;
                LogHumanWave(slot, $"攻城 {town.Type?.Name}，force_level {forceLevel}");
                return true;
            }
        }

        HumanCreepCamp camp = PickHumanCreepCamp(slot, minCreeps, maxCreeps, allowAirCreeps);
        if (camp == null) camp = PickHumanCreepCamp(slot, 0, int.MaxValue, allowAirCreeps);
        if (camp != null)
        {
            target = camp.Anchor;
            goal = camp.Center;
            LogHumanWave(slot, $"打野 强度 {camp.Strength}（区间 {minCreeps}..{maxCreeps}，force_level {forceLevel}）");
            return true;
        }

        Unit fallback = FindHumanEnemyTown(slot);
        if (fallback == null) return false;
        target = fallback;
        goal = fallback.Position;
        LogHumanWave(slot, "没有野怪可打，直接进攻对手基地");
        return true;
    }

    private int HumanForceLevel(MeleeAiCounters counters)
    {
        int level = 4;
        level += 2 * (counters.FootmanDone + counters.PriestDone + counters.SorceressDone + counters.BreakerDone);
        level += 3 * (counters.RiflemanDone + counters.GryphonDone + counters.HawkDone);
        level += 5 * counters.KnightDone + (counters.Hero3Done ? 5 : 0);
        level += counters.Hero2Done ? 6 : 0;
        return level;
    }

    private string HumanAttackTargetName(Unit target)
    {
        if (target == null) return "空点";
        if (target.Player == MeleeRules.PlayerNeutralHostile) return $"野怪营地 {target.Type?.Name}";
        return $"{PlayerName(target.Player)} 的 {target.Type?.Name}";
    }

    private List<HumanCreepCamp> HumanCreepCamps()
    {
        var camps = new List<HumanCreepCamp>();
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.IsBuilding) continue;
            if (unit.Player != MeleeRules.PlayerNeutralHostile) continue;

            HumanCreepCamp camp = null;
            Vector3 anchor = unit.Home.LengthSquared() > 1f ? unit.Home : unit.Position;
            foreach (HumanCreepCamp candidate in camps)
            {
                if ((candidate.Center - anchor).Length() <= AttackCreepCampRadius)
                {
                    camp = candidate;
                    break;
                }
            }
            if (camp == null)
            {
                camp = new HumanCreepCamp { Center = anchor };
                camps.Add(camp);
            }
            camp.Units.Add(unit);
            camp.Strength += unit.TypeLevel;
            if (unit.Type?.Flying == true) camp.Flying = true;
            if (camp.Anchor == null) camp.Anchor = unit;
        }
        return camps;
    }

    private HumanCreepCamp PickHumanCreepCamp(PlayerSlot slot, int minStrength, int maxStrength, bool flyersOk)
    {
        Unit hall = FindHall(slot.Id);
        Vector3 from = hall != null ? hall.Position : Vector3.Zero;

        HumanCreepCamp pick = null;
        float best = float.MaxValue;
        foreach (HumanCreepCamp camp in HumanCreepCamps())
        {
            if (camp.Strength < minStrength || camp.Strength > maxStrength) continue;
            if (camp.Flying && !flyersOk) continue;
            float distance = camp.Center.DistanceTo(from);
            if (distance >= best) continue;
            best = distance;
            pick = camp;
        }
        return pick;
    }

    private HumanCreepCamp NearestHumanCreepCamp(Vector3 point, bool flyersOk)
    {
        HumanCreepCamp pick = null;
        float best = float.MaxValue;
        foreach (HumanCreepCamp camp in HumanCreepCamps())
        {
            if (camp.Flying && !flyersOk) continue;
            float distance = camp.Center.DistanceTo(point);
            if (distance >= best) continue;
            best = distance;
            pick = camp;
        }
        return pick;
    }

    private IEnumerable<int> HumanHostilePlayers(PlayerSlot slot)
    {
        foreach (int id in _active)
        {
            if (id == slot.Id) continue;
            if (AreAllied(id, slot.Id)) continue;
            yield return id;
        }
    }

    private int CountMilitary(int player)
    {
        int count = 0;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Player != player) continue;
            if (unit.IsBuilding || unit.IsWorker) continue;
            count++;
        }
        return count;
    }

    private int CountPlayerBuildings(int player)
    {
        int count = 0;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Player != player || !unit.IsBuilding) continue;
            count++;
        }
        return count;
    }

    private int FindHumanMegaTargetPlayer(PlayerSlot slot)
    {
        int own = CountMilitary(slot.Id);
        if (own < 10) return -1;

        int weakest = int.MaxValue;
        int pick = -1;
        foreach (int enemy in HumanHostilePlayers(slot))
        {
            if (CountPlayerBuildings(enemy) == 0) continue;
            int theirs = CountMilitary(enemy);
            if (theirs * 3 > own) continue;
            if (theirs >= weakest) continue;
            weakest = theirs;
            pick = enemy;
        }
        return pick;
    }

    private Unit NearestHumanEnemyBuilding(PlayerSlot slot, int enemy)
    {
        Unit hall = FindHall(slot.Id);
        Vector3 from = hall != null ? hall.Position : Vector3.Zero;

        Unit pick = null;
        float best = float.MaxValue;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || !unit.IsBuilding || unit.Player != enemy) continue;
            float distance = unit.Position.DistanceTo(from);
            if (distance >= best) continue;
            best = distance;
            pick = unit;
        }
        return pick;
    }

    private Unit FindHumanEnemyTown(PlayerSlot slot)
    {
        Vector3 from = FindHall(slot.Id)?.Position ?? Vector3.Zero;

        Unit pick = null;
        float best = float.MaxValue;
        foreach (int enemy in HumanHostilePlayers(slot))
        {
            foreach (Unit unit in _allUnits)
            {
                if (unit == null || !unit.Alive || !unit.IsBuilding || unit.Player != enemy) continue;
                float distance = unit.Position.DistanceTo(from);
                if (IsTownHall(unit)) distance -= 600f;
                if (distance >= best) continue;
                best = distance;
                pick = unit;
            }
        }
        return pick;
    }

    private bool IsTownHall(Unit building)
    {
        if (building?.Type?.Trains == null) return false;
        foreach (string id in building.Type.Trains)
        {
            if (UnitFactory.Get(id)?.IsWorkerType == true) return true;
        }
        return false;
    }

    private Unit FindHumanEnemyExpansion(PlayerSlot slot)
    {
        Vector3 from = FindHall(slot.Id)?.Position ?? Vector3.Zero;

        Unit pick = null;
        float best = float.MaxValue;
        foreach (int enemy in HumanHostilePlayers(slot))
        {
            var halls = new List<Unit>();
            foreach (Unit unit in _allUnits)
            {
                if (unit == null || !unit.Alive || !unit.IsBuilding || unit.Player != enemy) continue;
                if (!IsTownHall(unit)) continue;
                halls.Add(unit);
            }
            if (halls.Count < 2) continue;

            halls.Sort((left, right) => right.SimulationId.CompareTo(left.SimulationId));
            for (int index = 1; index < halls.Count; index++)
            {
                float distance = halls[index].Position.DistanceTo(from);
                if (distance >= best) continue;
                best = distance;
                pick = halls[index];
            }
        }
        return pick;
    }

    private bool HumanIsTowered(Unit building)
    {
        if (building == null) return false;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || !unit.IsBuilding || unit.Player != building.Player) continue;
            if (unit.DistanceTo(building) > 800f) continue;
            if (unit.Type?.Weapon == null) continue;
            if (unit.Type.Trains is { Length: > 0 }) continue;
            if (unit.Type.Researches is { Length: > 0 }) continue;
            return true;
        }
        return false;
    }
}
