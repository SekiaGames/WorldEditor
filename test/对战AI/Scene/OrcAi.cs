extern alias Sim;

using Godot;
using System;
using System.Collections.Generic;
using Sim::OpenWC3.JassToSharp.Ai;
using static Sim::OpenWC3.JassToSharp.Ai.Data.OrcCodes;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    private bool IsOrcOriginalAi(PlayerSlot slot) =>
        slot != null && IsComputerPlayer(slot.Id) && PlayerRace(slot.Id) == 2;

    private void InitializeOrcAi(PlayerSlot slot)
    {
        MeleeAiState state = MeleeAiStateFor(slot.Id);
        state.Hero1 = FarSeer;
        state.Hero2 = Blademaster;
        state.Hero3 = TaurenChieftain;
        slot.Tasks.Clear();
        slot.Done = false;
        slot.Announced = false;
        RefreshOrcCounters(slot, state);
        BuildOrcPlan(slot, state);
        LogHuman(slot, "兽族 AI 启动");
    }

    private void TickOrcAi(PlayerSlot slot, float delta)
    {
        MeleeAiState state = MeleeAiStateFor(slot.Id);

        state.CounterTimer += delta;
        if (state.CounterTimer >= 1f)
        {
            state.CounterTimer = 0f;
            RefreshOrcCounters(slot, state);
        }

        state.AssignTimer += delta;
        if (state.AssignTimer >= 2f)
        {
            state.AssignTimer = 0f;
            AssignHumanPeons(slot);
        }

        state.BuildTimer += delta;
        if (state.BuildTimer >= 2f)
        {
            state.BuildTimer = 0f;
            RefreshOrcCounters(slot, state);
            BuildOrcPlan(slot, state);
            ExecuteHumanBuildLoop(slot, state);
        }

        state.StatusTimer += delta;
        if (state.StatusTimer >= 30f)
        {
            state.StatusTimer = 0f;
            MeleeAiCounters counters = state.Counters;
            LogHuman(slot, $"[兽] 人口 {counters.FoodUsed}/{counters.FoodMade} 金 {counters.Gold}"
                + $" 木 {slot.Lumber} 大厅 {counters.GreatHallDone} 苦工 {counters.PeonDone}"
                + $" 地洞 {counters.BurrowDone} 兵营 {counters.BarracksDone}"
                + $" 步兵 {counters.GruntDone} 猎头 {counters.HeadHunterDone}"
                + $" 计划 {state.Plan.Count} 条");
        }
    }

    private void RefreshOrcCounters(PlayerSlot slot, MeleeAiState state)
    {
        MeleeAiCounters counters = state.Counters;
        counters.GreatHall = MeleeTownCount(slot, GreatHall, false);
        counters.GreatHallDone = MeleeTownCount(slot, GreatHall, true);
        counters.StrongholdDone = MeleeTownCount(slot, Stronghold, true);
        counters.FortressDone = MeleeTownCount(slot, Fortress, true);
        counters.Hall = counters.GreatHall;
        counters.HallDone = counters.GreatHallDone;
        counters.KeepDone = counters.StrongholdDone;
        counters.CastleDone = counters.FortressDone;
        counters.Burrow = HumanCountBuildings(slot, Burrow, false);
        counters.BurrowDone = HumanCountBuildings(slot, Burrow, true);
        counters.BarracksDone = HumanCountBuildings(slot, Barracks, true);
        counters.ForgeDone = HumanCountBuildings(slot, Forge, true);
        counters.BestiaryDone = HumanCountBuildings(slot, Bestiary, true);
        counters.TotemDone = HumanCountBuildings(slot, Totem, true);
        counters.VoodooDone = HumanCountBuildings(slot, VoodooLounge, true);
        counters.LodgeDone = HumanCountBuildings(slot, Lodge, true);
        counters.AltarDone = HumanCountBuildings(slot, Altar, true);
        counters.Watch = MeleeTownCount(slot, WatchTower, false);
        counters.WatchDone = MeleeTownCount(slot, WatchTower, true);
        counters.PeonDone = MeleeTownCount(slot, Peon, false);
        counters.GruntDone = CountUnits(slot, Grunt);
        counters.HeadHunterDone = CountUnits(slot, HeadHunter);
        counters.CatapultDone = CountUnits(slot, Catapult);
        counters.TaurenDone = CountUnits(slot, Tauren);
        counters.RaiderDone = CountUnits(slot, Raider);
        counters.WyvernDone = CountUnits(slot, Wyvern);
        counters.BatriderDone = CountUnits(slot, Batrider);
        counters.ShamanDone = CountUnits(slot, Shaman);
        counters.WitchDoctorDone = CountUnits(slot, WitchDoctor);
        counters.SpiritWalkerDone = CountUnits(slot, SpiritWalker);
        counters.KodoDone = CountUnits(slot, KodoBeast);
        counters.FoodMade = FoodCap(slot.Id);
        counters.FoodUsed = FoodUsed(slot.Id);
        counters.Gold = slot.Gold;
        counters.Mines = OwnedMines(slot.Id).Count;
        counters.GoldOwned = 0;
        foreach (Unit mine in OwnedMines(slot.Id)) counters.GoldOwned += Math.Max(0, mine.Gold);
        counters.Hero1Done = HumanCountHeroAnyState(slot.Id, state.Hero1) >= 1;
        counters.Hero2Done = HumanCountHeroAnyState(slot.Id, state.Hero2) >= 1;
        counters.Hero3Done = HumanCountHeroAnyState(slot.Id, state.Hero3) >= 1;

        if (state.BasicOpening
            && (counters.Hero2Done || (IsHumanNewbie && counters.FortressDone >= 1)))
            state.BasicOpening = false;
    }

    private void BuildOrcPlan(PlayerSlot slot, MeleeAiState state)
    {
        List<MeleeAiBuildEntry> plan = state.Plan;
        plan.Clear();
        if (state.BasicOpening) AppendOrcBasicOpening(slot, state, plan);
        else AppendOrcDynamicPlan(slot, state, plan);
    }

    private void AppendOrcBasicOpening(PlayerSlot slot, MeleeAiState state,
        List<MeleeAiBuildEntry> plan)
    {
        MeleeAiCounters counters = state.Counters;
        AddMeleeTownHall2(slot, plan, GreatHall);
        AddUnit(plan, 6, Peon);
        AddUnit(plan, 1, Altar);
        AddUnit(plan, 7, Peon);
        AddUnit(plan, 1, Burrow);
        AddUnit(plan, 8, Peon);
        AddUnit(plan, 2, Burrow);
        AddUnit(plan, 10, Peon);
        AddUnit(plan, 1, state.Hero1);
        AddUnit(plan, 11, Peon);
        AddUnit(plan, 1, Barracks);
        AddUnit(plan, 1, Forge);
        AddUnit(plan, 14, Peon);
        AddUnit(plan, 1, Grunt);
        AddUpgrade(plan, 1, UpgradeMelee);
        AddUnit(plan, 2, Grunt);
        AddUnit(plan, 3, Burrow);
        AddUnit(plan, 1, HeadHunter);
        AddUnit(plan, 1, VoodooLounge);
        AddUpgrade(plan, 1, UpgradeRanged);
        AddUnit(plan, 3, HeadHunter);
        AddUpgrade(plan, 1, UpgradeArmor);
        AddUnit(plan, 4, Grunt);

        AddBasicExpansion(slot, plan, counters.Mines < 2, GreatHall);

        if (!IsHumanNewbie)
        {
            AddGuardSecondary(slot, counters, plan, 1, 1, Burrow);
            AddGuardSecondary(slot, counters, plan, 1, 2, WatchTower);
        }

        AddUnit(plan, 4, Burrow);
        AddUnit(plan, 6, Grunt);
        AddUnit(plan, 1, Stronghold);
        AddUpgrade(plan, 1, UpgradeSpikes);
        if (!IsHumanNewbie) AddUnit(plan, 1, state.Hero2);
        AddUnit(plan, 5, Burrow);
        AddUnit(plan, 1, Catapult);
        AddUpgrade(plan, 2, UpgradeMelee);
        AddUpgrade(plan, 1, UpgradePillage);
        AddUnit(plan, 2, Catapult);
    }

    private void AppendOrcDynamicPlan(PlayerSlot slot, MeleeAiState state,
        List<MeleeAiBuildEntry> plan)
    {
        MeleeAiCounters counters = state.Counters;

        if (counters.GreatHallDone >= 1)
        {
            int peons = 6 - slot.Lumber / 200;
            if (peons < 3) peons = 3;
            peons += counters.Mines < 2 || counters.GreatHallDone < 2 ? 5 : 10;
            if (peons > 15) peons = 15;
            AddNext(slot, plan, peons, Peon);
        }

        if (counters.GreatHall < 1 && counters.PeonDone >= 1)
            AddMeleeTownHall2(slot, plan, GreatHall);

        if (counters.Gold > 500 && slot.Lumber < 100) AddNext(slot, plan, 15, Peon);

        if (counters.GoldOwned < 2000)
        {
            AddBasicExpansion(slot, plan, counters.Mines < 2, GreatHall);
            if (!IsHumanNewbie) AddGuardSecondary(slot, counters, plan, 1, 1, Burrow);
        }

        if (counters.FoodUsed + 5 >= counters.FoodMade)
            AddUnit(plan, counters.BurrowDone + 1, Burrow);

        if (counters.AltarDone >= 1)
        {
            if (counters.Hero1Done && !IsHumanNewbie) AddUnit(plan, 1, state.Hero2);
            else AddUnit(plan, 1, state.Hero1);
        }
        else AddUnit(plan, 1, Altar);

        if (counters.TotemDone >= 1) AddUnit(plan, 2, Tauren);
        else
        {
            AddUnit(plan, 1, Barracks);
            AddUnit(plan, 3, Grunt);
        }

        if (counters.BestiaryDone >= 1)
        {
            if (counters.FortressDone >= 1) AddUnit(plan, 2, Wyvern);
            else
            {
                AddUnit(plan, 1, Barracks);
                AddUnit(plan, 3, HeadHunter);
            }
            if (counters.VoodooDone >= 1 && counters.FortressDone >= 1) AddUnit(plan, 2, Batrider);
            else AddUnit(plan, 3, Raider);
        }
        else
        {
            AddUnit(plan, 1, Barracks);
            AddUnit(plan, 3, HeadHunter);
            AddUnit(plan, 2, Catapult);
        }

        if (counters.Gold > 1000)
        {
            if (!IsHumanNewbie)
            {
                AddGuardSecondary(slot, counters, plan, 1, 1, Burrow);
                AddGuardSecondary(slot, counters, plan, 1, 1, WatchTower);
                AddGuardSecondary(slot, counters, plan, 1, 2, Burrow);
                AddGuardSecondary(slot, counters, plan, 1, 2, WatchTower);
            }
            AddUnit(plan, 1, Barracks);
            AddUnit(plan, 1, Forge);
            AddUnit(plan, 1, VoodooLounge);
            AddUnit(plan, 1, Stronghold);
            AddUnit(plan, 1, Lodge);
            AddUnit(plan, 1, Bestiary);
            AddUnit(plan, 1, Fortress);
            AddUnit(plan, 1, Totem);
        }

        AddBasicExpansion(slot, plan, counters.Mines < 2, GreatHall);
        if (!IsHumanNewbie)
        {
            AddGuardSecondary(slot, counters, plan, 1, 1, Burrow);
            AddGuardSecondary(slot, counters, plan, 1, 2, WatchTower);
        }

        if (counters.FoodUsed >= 70 && counters.Gold < 2000) return;

        if (counters.TotemDone >= 1) AddNext(slot, plan, 4, Tauren);
        else AddNext(slot, plan, 6, Grunt);

        if (counters.LodgeDone >= 1)
        {
            AddUnit(plan, 1, Shaman);
            AddUnit(plan, 1, WitchDoctor);
            AddUnit(plan, 1, SpiritWalker);
            AddNext(slot, plan, 4, Shaman);
            AddNext(slot, plan, 2, WitchDoctor);
        }
    }

    private void AddMeleeTownHall2(PlayerSlot slot, List<MeleeAiBuildEntry> plan, string id)
    {
        if (MeleeTownCount(slot, id, false) > 0) return;
        plan.Add(MeleeAiBuildEntry.Expand(1, id));
    }
}
