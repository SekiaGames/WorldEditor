extern alias Sim;

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

using SimAbility = Sim::OpenWC3.Ability;
using SimCargoTransport = Sim::OpenWC3.CargoTransport;
using SimCOrder = Sim::OpenWC3.COrder;
using SimCOrderTarget = Sim::OpenWC3.COrderTarget;
using SimHero = Sim::OpenWC3.Hero;
using SimUnit = Sim::OpenWC3.Unit;
using SimVec2 = Sim::OpenWC3.Vec2;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    private sealed class SimAiOrderState
    {
        public SimUnit Target;
        public SimVec2? Point;
    }

    private readonly Dictionary<Unit, SimAiOrderState> _simAiOrders = new();
    private int _simWaveLogBudget = 8;
    private int _simAiOrderLogBudget = 8;
    private float _simTransportTimer;
    private readonly Dictionary<Unit, Unit> _simTransportPassenger = new();

    private static bool IsNeutralHostile(int player) => player == MeleeRules.PlayerNeutralHostile;

    private void TickSimAi(float delta)
    {
        if ((!AiEnabled && !CreepAiEnabled) || _simHost == null) return;
        if (AiEnabled && Aggression) TickAiWaves(delta);

        foreach (PlayerSlot slot in Slots())
        {
            if (!IsComputerPlayer(slot.Id)) continue;
            bool hostile = IsNeutralHostile(slot.Id);
            if (hostile ? !CreepAiEnabled : !AiEnabled) continue;
            TickAi(slot, delta);
        }
        TickSimAiTransport(delta);
        SyncSimAiOrders();
    }

    private void SyncSimAiOrders()
    {
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.CargoCapacity > 0
                || !IsActivePlayer(unit.Player) || !IsComputerPlayer(unit.Player)) continue;
            if (!_simUnitByPresentation.TryGetValue(unit, out SimUnit sim)) continue;
            if (!_simAiOrders.TryGetValue(unit, out SimAiOrderState state))
            {
                state = new SimAiOrderState();
                _simAiOrders.Add(unit, state);
            }

            Unit target = unit.Target;
            if (target != null && target.Alive)
            {
                if (!_simUnitByPresentation.TryGetValue(target, out SimUnit simTarget)) continue;
                if (ReferenceEquals(state.Target, simTarget)) continue;
                sim.Issue(new SimCOrderTarget(simTarget));
                state.Target = simTarget;
                state.Point = null;
                if (_simAiOrderLogBudget > 0)
                {
                    _simAiOrderLogBudget--;
                    GD.Print($"[SIM-AI] P{unit.Player} {unit.Type.Id} 攻击 {target.Type.Id}");
                }
                continue;
            }

            state.Target = null;
            if (!unit.MoveTarget.HasValue) continue;
            Vector2 map = ToMap(unit.MoveTarget.Value);
            var point = new SimVec2(map.X, map.Y);
            if (state.Point.HasValue && SimVec2.Dist(state.Point.Value, point) <= 4f) continue;
            sim.Issue(new SimCOrderTarget(point, 851986));
            state.Point = point;
        }
    }

    private void TickSimAiTransport(float delta)
    {
        _simTransportTimer += delta;
        if (_simTransportTimer < 2f) return;
        _simTransportTimer = 0f;

        foreach (PlayerSlot slot in Slots())
        {
            if (!IsComputerPlayer(slot.Id)) continue;
            foreach (Unit carrier in _allUnits)
            {
                if (carrier == null || !carrier.Alive || carrier.Player != slot.Id) continue;
                if (!_simUnitByPresentation.TryGetValue(carrier, out SimUnit simCarrier)
                    || simCarrier.CargoHold == null || simCarrier.CargoHold.Capacity <= 0) continue;

                if (simCarrier.CargoHold.Passengers.Count > 0)
                {
                    _simTransportPassenger.Remove(carrier);
                    Vector2 target = SimEnemyTargetMap(slot.Id);
                    simCarrier.Issue(new SimCOrderTarget(new SimVec2(target.X, target.Y), 851986));
                    if (SimVec2.Dist(simCarrier.Pos, new SimVec2(target.X, target.Y)) < 360f)
                    {
                        simCarrier.CargoHold.QueueUnload(null, new SimVec2(target.X, target.Y), all: true);
                        GD.Print($"[SIM-AI] P{slot.Id} 运输卸载 {simCarrier.CargoHold.Passengers.Count} 个单位");
                    }
                    continue;
                }

                if (_simTransportPassenger.ContainsKey(carrier)) continue;
                Unit passenger = _allUnits.FirstOrDefault(unit => unit != null && unit.Alive
                    && !ReferenceEquals(unit, carrier) && unit.Player == slot.Id
                    && !unit.IsBuilding && !unit.IsWorker
                    && _simUnitByPresentation.TryGetValue(unit, out SimUnit candidate)
                    && candidate.Cargo == null && candidate.CargoHold == null
                    && SimVec2.Dist(candidate.Pos, simCarrier.Pos) < 1200f);
                if (passenger == null) continue;
                if (!SimCargoTransport.TrySmartOrder(simCarrier,
                    _simUnitByPresentation[passenger], out SimCOrder order)) continue;
                simCarrier.Issue(order);
                _simTransportPassenger[carrier] = passenger;
                GD.Print($"[SIM-AI] P{slot.Id} 派 {passenger.Type.Id} 上 {carrier.Type.Id}");
            }
        }
    }

    private bool AssignSimBuild(Unit worker, Unit site, bool queue)
    {
        if (!SimDriven || worker == null || site == null) return false;
        if (queue && worker.BuildSite != null && !ReferenceEquals(worker.BuildSite, site))
        {
            if (!worker.BuildQueue.Contains(site)) worker.BuildQueue.Add(site);
            return true;
        }
        SimBuildJob job = _simBuildJobs.FirstOrDefault(candidate => ReferenceEquals(candidate.Site, site));
        if (job != null)
        {
            AttachSimBuilder(job, worker);
            return true;
        }
        worker.BuildSite = site;
        worker.BuildingNow = false;
        worker.GoldMine = null;
        worker.Tree = null;
        worker.RepairTarget = null;
        worker.Repairing = false;
        worker.Target = null;
        worker.MoveTarget = null;
        IssueSimMoveUnit(worker, site.Position);
        return true;
    }

    private void AssignSimRepair(Unit worker, Unit building)
    {
        if (!SimDriven || worker == null || building == null) return;
        StartSimRepair(building, new[] { worker });
    }

    private void AssignSimGather(Unit worker, Unit mine)
    {
        if (!SimDriven || worker == null || mine == null) return;
        StartSimGather(mine, null, true, new[] { worker });
    }

    private void AssignSimChop(Unit worker, TreeState tree)
    {
        if (!SimDriven || worker == null || tree == null) return;
        StartSimGather(null, tree, false, new[] { worker });
    }

    private void TickSimHeroSkills(PlayerSlot slot)
    {
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Player != slot.Id || !unit.IsHero) continue;
            if (unit.SkillPoints <= 0) continue;
            if (!_simUnitByPresentation.TryGetValue(unit, out SimUnit sim)) continue;
            SimHero hero = sim.Abilities.Get<SimHero>();
            if (hero == null || hero.SkillPoints <= 0) continue;

            string planned = HeroSkillBuild.Pick(unit.Type?.Id, hero.HeroLevel);
            SimAbility ability = planned == null ? null : sim.GetAbility(planned);
            if (ability == null || !hero.CanLearnSkill(ability))
                ability = PickSimHeroAbility(sim, hero);
            if (ability == null || !hero.LearnSkill(ability)) continue;

            SyncSimAbilityState(unit, sim);
            if (_heroSkillLogBudget <= 0) continue;
            _heroSkillLogBudget--;
            GD.Print($"[SIM-AI] P{slot.Id} {unit.Type.Name} 加点 {ability.Name} L{ability.Level}"
                + $"（等级 {hero.HeroLevel}，剩 {unit.SkillPoints} 点）");
        }
    }

    private static SimAbility PickSimHeroAbility(SimUnit sim, SimHero hero)
    {
        SimAbility best = null;
        foreach (SimAbility ability in sim.Abilities.Items)
        {
            if (ability is SimHero || ability is Sim::OpenWC3.Buff) continue;
            if (!ability.Learnable || ability.Level >= ability.MaxLevel) continue;
            if (!hero.CanLearnSkill(ability)) continue;
            if (best == null || ability.Level < best.Level) best = ability;
        }
        return best;
    }

    private void SpawnSimWave()
    {
        if (_map?.Players == null) return;
        foreach (MapPlayerInfo player in _map.Players)
        {
            if (!IsComputerPlayer(player.Id)) continue;
            Unit hall = FindHall(player.Id);
            if (hall == null) continue;

            int race = Mathf.Clamp(player.Race, 1, 4);
            List<string> wave = WaveComposition(player.Id, race);
            int repeat = AiDifficulty >= 2 ? 2 : 1;
            Vector2 hallMap = ToMap(hall.Position);
            Vector2 target = SimEnemyTargetMap(player.Id);
            int spawned = 0;
            for (int copy = 0; copy < repeat; copy++)
            {
                int slot = 0;
                foreach (string unitId in wave)
                {
                    float angle = Mathf.Pi * 0.5f + slot * 0.7f;
                    float ring = 180f + (slot / 3) * 60f;
                    SimUnit sim = _simHost.Spawn(unitId, player.Id,
                        hallMap.X + Mathf.Cos(angle) * ring,
                        hallMap.Y + Mathf.Sin(angle) * ring, angle);
                    slot++;
                    if (sim == null) continue;
                    sim.Issue(new SimCOrderTarget(new SimVec2(target.X, target.Y), 851986));
                    spawned++;
                }
            }

            if (spawned == 0 || _simWaveLogBudget <= 0) continue;
            _simWaveLogBudget--;
            GD.Print($"[SIM-WAVE] P{player.Id} 出兵 {spawned} 个 → ({target.X:0},{target.Y:0})");
        }
    }

    private Vector2 SimEnemyTargetMap(int player)
    {
        foreach (MapPlayerInfo info in _map.Players)
        {
            if (info.Id == player || !IsActivePlayer(info.Id) || AreAllied(info.Id, player)) continue;
            if (info.StartX == 0f && info.StartY == 0f) continue;
            return new Vector2(info.StartX, info.StartY);
        }
        return new Vector2(0f, 0f);
    }
}