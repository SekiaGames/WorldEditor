extern alias Sim;

using System;
using System.Collections.Generic;
using Godot;
using Sim::OpenWC3.JassToSharp.Ai;

namespace OpenWC3.Game;

public sealed partial class War3GameScene : IMeleeAiWorld,
    Sim::OpenWC3.JassToSharp.Ai.Economy.IResourceSink,
    Sim::OpenWC3.JassToSharp.Ai.Runtime.IWoodSource
{
    int IMeleeAiWorld.UnitCount(int player, string typeId)
    {
        PlayerSlot slot = Slot(player);
        return slot == null ? 0 : MeleeTownCount(slot, typeId, false);
    }

    int IMeleeAiWorld.UnitCountDone(int player, string typeId)
    {
        PlayerSlot slot = Slot(player);
        return slot == null ? 0 : MeleeTownCount(slot, typeId, true);
    }

    int IMeleeAiWorld.BuildingCount(int player, string typeId, bool doneOnly)
    {
        PlayerSlot slot = Slot(player);
        return slot == null ? 0 : HumanCountBuildings(slot, typeId, doneOnly);
    }

    void Sim::OpenWC3.JassToSharp.Ai.Economy.IResourceSink.Credit(int player, int gold, int lumber)
    {
        PlayerSlot slot = Slot(player);
        if (slot == null) return;
        slot.Gold += gold;
        slot.Lumber += lumber;
    }

    int IMeleeAiWorld.Gold(int player) => Slot(player)?.Gold ?? 0;

    int IMeleeAiWorld.Lumber(int player) => Slot(player)?.Lumber ?? 0;

    int IMeleeAiWorld.FoodUsed(int player) => FoodUsed(player);

    int IMeleeAiWorld.FoodMade(int player) => FoodCap(player);

    int IMeleeAiWorld.OwnedMineCount(int player) => OwnedMines(player).Count;

    int IMeleeAiWorld.OwnedMineGold(int player)
    {
        int total = 0;
        foreach (Unit mine in OwnedMines(player)) total += Math.Max(0, mine.Gold);
        return total;
    }


    bool Sim::OpenWC3.JassToSharp.Ai.Runtime.IWoodSource.TryNearestTree(Sim::OpenWC3.Vec2 from,
        out int treeId, out Sim::OpenWC3.Vec2 position)
    {
        treeId = -1;
        position = from;
        TreeState best = null;
        float bestDistance = float.MaxValue;
        foreach (TreeState tree in _trees)
        {
            if (tree == null || !tree.Alive) continue;
            Vector2 simPos = ToMap(tree.Position);
            float dx = simPos.X - from.X;
            float dy = simPos.Y - from.Y;
            float distance = dx * dx + dy * dy;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = tree;
        }
        if (best == null) return false;
        treeId = best.CreationNumber;
        Vector2 bestSim = ToMap(best.Position);
        position = new Sim::OpenWC3.Vec2(bestSim.X, bestSim.Y);
        return true;
    }

    Sim::OpenWC3.Vec2 Sim::OpenWC3.JassToSharp.Ai.Runtime.IWoodSource.PositionOf(int treeId)
    {
        foreach (TreeState tree in _trees)
            if (tree != null && tree.CreationNumber == treeId)
            {
                Vector2 simPos = ToMap(tree.Position);
                return new Sim::OpenWC3.Vec2(simPos.X, simPos.Y);
            }
        return default;
    }

    bool Sim::OpenWC3.JassToSharp.Ai.Runtime.IWoodSource.IsAlive(int treeId)
    {
        foreach (TreeState tree in _trees)
            if (tree != null && tree.CreationNumber == treeId) return tree.Alive;
        return false;
    }

    float Sim::OpenWC3.JassToSharp.Ai.Runtime.IWoodSource.RadiusOf(int treeId) =>
        _doodadRadius.TryGetValue(treeId, out float radius) ? radius : 48f;

    int Sim::OpenWC3.JassToSharp.Ai.Runtime.IWoodSource.Chop(int treeId, int amount)
    {
        if (amount <= 0) return 0;
        foreach (TreeState tree in _trees)
        {
            if (tree == null || tree.CreationNumber != treeId || !tree.Alive) continue;
            float before = tree.Hp;
            tree.Chop(amount);
            return (int)Math.Min(amount, before);
        }
        return 0;
    }

    public void RunAiWorldProbe()
    {
        IMeleeAiWorld world = this;
        int alive = 0, buildings = 0, mines = 0;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive) continue;
            alive++;
            if (unit.IsBuilding) buildings++;
            if (unit.Gold > 0) mines++;
        }
        GD.Print($"[AIW] 校验 存活={alive} 建筑={buildings} 金矿={mines} 总数={_allUnits.Count}");
        foreach (PlayerSlot slot in _players)
        {
            if (slot == null) continue;
            string worker = PlayerRace(slot.Id) switch
            {
                1 => "hpea",
                2 => "opeo",
                3 => "ewsp",
                4 => "uaco",
                _ => "hpea",
            };
            GD.Print($"[AIW] P{slot.Id} 工人({worker}) count={world.UnitCount(slot.Id, worker)}"
                + $" done={world.UnitCountDone(slot.Id, worker)}"
                + $" 金={world.Gold(slot.Id)} 木={world.Lumber(slot.Id)}"
                + $" 人口={world.FoodUsed(slot.Id)}/{world.FoodMade(slot.Id)}"
                + $" 矿={world.OwnedMineCount(slot.Id)} 矿储量={world.OwnedMineGold(slot.Id)}");
            int halls = OwnCompletedTownHalls(slot.Id).Count;
            GD.Print($"[AIW] P{slot.Id} 大厅={halls}"
                + $" town0={HallForTown(slot, 0)?.Type?.Id ?? "无"}"
                + $" town1={HallForTown(slot, 1)?.Type?.Id ?? "无"}");
        }
    }

    public void ScheduleHarvestProbe(float seconds)
    {
        SceneTreeTimer timer = GetTree().CreateTimer(seconds);
        timer.Timeout += () => { RunHarvestProbe(); RunAiWorldProbe(); };
    }

    public void RunHarvestProbe()
    {
        foreach (KeyValuePair<Unit, Sim::OpenWC3.JassToSharp.Ai.Runtime.MineServiceQueue> slotPair in _mineQueues)
            if (slotPair.Value.ActiveCount > 0 || slotPair.Value.WaitingCount > 0)
                GD.Print($"[HARV] 矿位 ({slotPair.Key.Position.X:0},{slotPair.Key.Position.Z:0})"
                    + $" 槽={slotPair.Value.ActiveCount}/{SimMineSlots} 等待={slotPair.Value.WaitingCount}");
        foreach (PlayerSlot slot in _players)
        {
            if (slot == null) continue;
            int shown = 0;
            int stuck = 0;
            int gold = 0, lumber = 0, idle = 0, carrying = 0;
            foreach (Unit worker in _allUnits)
            {
                if (worker == null || !worker.Alive || worker.Player != slot.Id || !worker.IsWorker) continue;
                bool isGold = worker.GoldMine != null;
                bool isLumber = worker.Tree != null;
                if (isGold) gold++;
                else if (isLumber) lumber++;
                else idle++;
                int carry = worker.Carry + worker.CarryLumber;
                if (carry > 0) carrying++;
                bool blocked = false;
                if (_simUnitByPresentation.TryGetValue(worker, out Sim::OpenWC3.Unit simWorker)
                    && simWorker?.World?.PathingMap != null)
                    blocked = simWorker.World.PathingMap.IsFootprintBlockedInclusive(
                        simWorker.Pos, simWorker.MovementType, simWorker.CollisionSize, simWorker);
                if (blocked) stuck++;
                if (shown < 6)
                {
                    GD.Print($"[HARV] P{slot.Id} {(isGold ? "采金" : isLumber ? "伐木" : "空闲")}"
                        + $" 携带={carry} 位置=({worker.Pos.X:0},{worker.Pos.Y:0})"
                        + $" 被足迹覆盖={blocked}");
                    shown++;
                }
            }
            GD.Print($"[HARV] P{slot.Id} 工人={gold + lumber + idle} 采金={gold} 伐木={lumber}"
                + $" 空闲={idle} 携带中={carrying} 被足迹覆盖={stuck}");
        }
    }

    public void RunHarvestDataProbe()
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || !unit.IsWorker) continue;
            string id = unit.Type?.Id ?? "";
            if (!seen.Add(id)) continue;
            if (!_simUnitByPresentation.TryGetValue(unit, out Sim::OpenWC3.Unit sim)) continue;
            var harvest = sim.GetAbility<Sim::OpenWC3.Game.RuntimeHarvestAbility>();
            if (harvest == null)
            {
                GD.Print($"[HARVD] {id} 无采集能力");
                continue;
            }
            GD.Print($"[HARVD] {id} 金/趟={harvest.GoldPerTrip} 木/趟={harvest.LumberPerTrip}"
                + $" 木容量={harvest.CarryCapacity} 周期={harvest.Interval:0.00}s"
                + $" 半径={harvest.Radius:0} 采金={harvest.HarvestsGold}");
        }
    }
}
