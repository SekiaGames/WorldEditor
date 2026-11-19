//重构情况：从 Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Jass/JassGameNatives.Harvest.cs 的 PlayerHarvestOrder 接单与推进逻辑转换（金矿，852018 执行器）
using OpenWC3;
using OpenWC3.JassToSharp.Ai.Economy;
using System.Linq;

namespace OpenWC3.JassToSharp.Ai.Runtime;

public sealed class HarvestOrderDriver
{
    public const int OrderHarvest = 852018;
    public const int OrderMove = 851986;
    public const float ReachRange = 120f;
    public const float HallStandOff = 160f;
    public const float MineStandOff = 280f;

    static readonly HashSet<string> HallIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "htow", "hkee", "hcas", "ogre", "ostr", "ofrt", "etol", "etoa", "etoe", "unpl", "unp1", "unp2",
    };

    readonly World _world;
    readonly IResourceSink _sink;
    readonly Dictionary<int, PlayerHarvestOrder> _orders = new();
    readonly Dictionary<int, HarvestPhase> _phases = new();
    readonly Dictionary<int, MineServiceQueue> _mineQueues = new();

    public HarvestOrderDriver(World world, IResourceSink sink)
    {
        _world = world;
        _sink = sink;
    }

    public int Count => _orders.Count;

    public bool TryPhase(int simulationId, out HarvestPhase phase) =>
        _phases.TryGetValue(simulationId, out phase);

    public void SubmitLumber(Unit worker, int treeId, int capacity = 10)
    {
        if (worker == null || treeId < 0) return;
        PlayerHarvestOrder order = OrderFor(worker);
        order.StartHarvest(treeId, gold: false, capacity: capacity);
        worker.HarvestCargo = 0;
    }

    public void Submit(Unit worker, Unit mine, float interval)
    {
        if (worker == null || mine == null) return;
        PlayerHarvestOrder order = OrderFor(worker);
        order.StartHarvest(mine.SimulationId, gold: true, capacity: 10f);
        order.Capacity = 10f;
        worker.HarvestCargoGold = true;
    }

    public void Cancel(Unit worker)
    {
        if (worker == null) return;
        _orders.Remove(worker.SimulationId);
    }

    PlayerHarvestOrder OrderFor(Unit worker)
    {
        if (_orders.TryGetValue(worker.SimulationId, out PlayerHarvestOrder order)) return order;
        order = new PlayerHarvestOrder();
        _orders[worker.SimulationId] = order;
        return order;
    }

    public void Tick(float delta, float interval, IWoodSource wood = null)
    {
        if (_world == null) return;
        var stale = new List<int>();
        foreach (KeyValuePair<int, PlayerHarvestOrder> pair in _orders)
        {
            PlayerHarvestOrder order = pair.Value;
            Unit worker = FindUnit(pair.Key);
            if (worker == null || !worker.Alive)
            {
                stale.Add(pair.Key);
                continue;
            }

            if (!order.Gold)
            {
                TickLumber(pair.Key, worker, order, delta, interval, wood);
                continue;
            }

            if (Interrupted(worker, order))
            {
                ReleaseSlot(worker, order);
                continue;
            }
            if (order.Interrupted) order.Resume();

            Unit mine = FindUnit(order.TargetId);
            if (mine == null || mine.ResourceAmount <= 0)
            {
                mine = NearestMine(worker);
                if (mine == null)
                {
                    stale.Add(pair.Key);
                    continue;
                }
                order.StartHarvest(mine.SimulationId, true, order.Capacity);
            }

            float capacity = Math.Max(1f, order.Capacity);
            bool full = worker.HarvestCargo >= capacity;
            Unit hall = NearestHall(worker);
            Vec2 goal = full
                ? Towards(worker.Pos, hall?.Pos ?? worker.Pos, HallStandOff)
                : Towards(worker.Pos, mine.Pos, MineStandOff);
            bool atGoal = Distance(worker.Pos, goal) <= ReachRange;

            var facts = new HarvestFacts
            {
                CarryingFull = full,
                AtBase = full && atGoal,
                AtResource = !full && atGoal,
                Interval = interval,
                SlotGranted = true,
            };
            HarvestAction action = order.Tick(delta, facts);
            _phases[pair.Key] = action switch
            {
                HarvestAction.MoveToBase => HarvestPhase.ToBase,
                HarvestAction.MoveToResource => HarvestPhase.ToResource,
                HarvestAction.TakeCargo => HarvestPhase.Harvesting,
                _ => HarvestPhase.Idle,
            };

            if (action == HarvestAction.Deliver)
            {
                _sink?.Credit(worker.PlayerId, (int)worker.HarvestCargo, 0);
                worker.HarvestCargo = 0;
                order.Reset();
                continue;
            }

            if (action == HarvestAction.TakeCargo)
            {
                int amount = Math.Min((int)(capacity - worker.HarvestCargo), mine.ResourceAmount);
                if (amount > 0)
                {
                    mine.ResourceAmount -= amount;
                    worker.HarvestCargo += amount;
                }
                order.Reset();
                continue;
            }

            if (action == HarvestAction.MoveToBase || action == HarvestAction.MoveToResource)
            {
                bool moved = order.ShouldIssue(action, true, delta);
                if (moved) worker.Issue(new COrderTarget(goal, OrderMove));
            }
        }
        foreach (int id in stale) _orders.Remove(id);
    }


    void TickLumber(int workerId, Unit worker, PlayerHarvestOrder order, float delta,
        float interval, IWoodSource wood)
    {
        if (wood == null) return;
        if (Interrupted(worker, order))
        {
            if (!order.Interrupted) order.Interrupt();
            return;
        }
        if (order.Interrupted) order.Resume();
        if (!wood.IsAlive(order.TargetId))
        {
            if (!wood.TryNearestTree(worker.Pos, out int nextId, out _))
            {
                _orders.Remove(workerId);
                return;
            }
            order.StartHarvest(nextId, false, order.Capacity);
        }

        float capacity = Math.Max(1f, order.Capacity);
        bool full = order.Cargo >= capacity;
        Unit hall = NearestHall(worker);
        Vec2 goal = full
            ? Towards(worker.Pos, hall?.Pos ?? worker.Pos, HallStandOff)
            : Towards(worker.Pos, wood.PositionOf(order.TargetId), wood.RadiusOf(order.TargetId) + 32f);
        bool atGoal = Distance(worker.Pos, goal) <= ReachRange;

        var facts = new HarvestFacts
        {
            CarryingFull = full,
            AtBase = full && atGoal,
            AtResource = !full && atGoal,
            Interval = interval,
            SlotGranted = true,
        };
        HarvestAction action = order.Tick(delta, facts);
        _phases[workerId] = action switch
        {
            HarvestAction.MoveToBase => HarvestPhase.ToBase,
            HarvestAction.MoveToResource => HarvestPhase.ToResource,
            HarvestAction.TakeCargo => HarvestPhase.Harvesting,
            _ => HarvestPhase.Idle,
        };

        if (action == HarvestAction.Deliver)
        {
            _sink?.Credit(worker.PlayerId, 0, (int)order.Cargo);
            order.Cargo = 0;
            order.Reset();
            return;
        }
        if (action == HarvestAction.TakeCargo)
        {
            int per = Math.Max(1, (int)(capacity / 10f));
            int choppable = Math.Min(per, (int)(capacity - order.Cargo));
            order.Cargo += wood.Chop(order.TargetId, choppable);
            order.Reset();
            return;
        }
        if (action == HarvestAction.MoveToBase || action == HarvestAction.MoveToResource)
        {
            if (order.ShouldIssue(action, true, delta))
                worker.Issue(new COrderTarget(goal, OrderMove));
        }
    }


    bool Interrupted(Unit worker, PlayerHarvestOrder order)
    {
        COrder current = worker.Current;
        if (current == null) return false;
        return current.JassOrderId != OrderMove;
    }

    bool ClaimSlot(int mineId, int workerId)
    {
        if (!_mineQueues.TryGetValue(mineId, out MineServiceQueue queue))
        {
            queue = new MineServiceQueue(HarvestTiming.MineSlots);
            _mineQueues[mineId] = queue;
        }
        return queue.TryClaim(workerId);
    }

    void ReleaseSlot(Unit worker, PlayerHarvestOrder order)
    {
        if (_mineQueues.TryGetValue(order.TargetId, out MineServiceQueue queue))
            queue.Release(worker.SimulationId);
    }

    Unit FindUnit(int simulationId)
    {
        IReadOnlyList<Unit> units = _world.Units;
        for (int index = 0; index < units.Count; index++)
        {
            Unit unit = units[index];
            if (unit != null && unit.SimulationId == simulationId) return unit;
        }
        return null;
    }

    Unit NearestMine(Unit worker)
    {
        Unit best = null;
        float bestDistance = float.MaxValue;
        IReadOnlyList<Unit> units = _world.Units;
        for (int index = 0; index < units.Count; index++)
        {
            Unit unit = units[index];
            if (unit == null || !unit.Alive || unit.ResourceAmount <= 0) continue;
            float distance = Distance(worker.Pos, unit.Pos);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = unit;
        }
        return best;
    }

    Unit NearestHall(Unit worker)
    {
        Unit best = null;
        float bestDistance = float.MaxValue;
        IReadOnlyList<Unit> units = _world.Units;
        for (int index = 0; index < units.Count; index++)
        {
            Unit unit = units[index];
            if (unit == null || !unit.Alive || !unit.IsBuilding) continue;
            if (unit.PlayerId != worker.PlayerId) continue;
            if (!HallIds.Contains(unit.TypeId)) continue;
            float distance = Distance(worker.Pos, unit.Pos);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = unit;
        }
        return best;
    }

    static float Distance(Vec2 left, Vec2 right)
    {
        float dx = left.X - right.X;
        float dy = left.Y - right.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    static Vec2 Towards(Vec2 from, Vec2 target, float standOff)
    {
        float dx = from.X - target.X;
        float dy = from.Y - target.Y;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        if (length < 0.01f) return target;
        return new Vec2(target.X + dx / length * standOff, target.Y + dy / length * standOff);
    }
}
