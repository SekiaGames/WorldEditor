extern alias Sim;
using Sim::OpenWC3.JassToSharp.Ai;
using static Sim::OpenWC3.JassToSharp.Ai.Data.HumanCodes;
using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{

    private sealed class MeleeAiCounters
    {
        public int GreatHall;
        public int GreatHallDone;
        public int StrongholdDone;
        public int FortressDone;
        public int Burrow;
        public int BurrowDone;
        public int BarracksDone;
        public int ForgeDone;
        public int BestiaryDone;
        public int TotemDone;
        public int VoodooDone;
        public int LodgeDone;
        public int PeonDone;
        public int GruntDone;
        public int HeadHunterDone;
        public int CatapultDone;
        public int TaurenDone;
        public int RaiderDone;
        public int WyvernDone;
        public int BatriderDone;
        public int ShamanDone;
        public int WitchDoctorDone;
        public int SpiritWalkerDone;
        public int KodoDone;
        public int Zep;
        public int Hall;
        public int HallDone;
        public int KeepDone;
        public int CastleDone;
        public int AltarDone;
        public int Arcane;
        public int AviaryDone;
        public int BreakerDone;
        public int FoodMade;
        public int FoodUsed;
        public int FootmanDone;
        public int GryphonDone;
        public int GyroDone;
        public int HawkDone;
        public int House;
        public int HouseDone;
        public int KnightDone;
        public int MillDone;
        public int Mines;
        public int MortarDone;
        public int PeasantDone;
        public int PriestDone;
        public int RiflemanDone;
        public int SanctumDone;
        public int SmithDone;
        public int SorceressDone;
        public int TankDone;
        public int Watch;
        public int WatchDone;
        public int WorkshopDone;
        public int Gold;
        public int GoldOwned;
        public bool Hero1Done;
        public bool Hero2Done;
        public bool Hero3Done;
        public bool KnightsOk;
    }

    private sealed class MeleeAiState
    {
        public bool BasicOpening = true;
        public string Hero1;
        public string Hero2;
        public string Hero3;
        public float CounterTimer;
        public float AssignTimer;
        public float BuildTimer;
        public float StatusTimer;
        public int TotalGold;
        public int TotalWood;
        public MeleeAiCounters Counters = new();
        public readonly Dictionary<string, int> Planned =
            new(StringComparer.OrdinalIgnoreCase);
        public MeleeAiBuildBook Book;
        public List<MeleeAiBuildEntry> Plan => Book.Entries;
        public readonly HumanAttackWave Attack = new();
    }

    private readonly Dictionary<int, MeleeAiState> _meleeAiStates = new();
    private int _humanAiLogBudget = 60;

    private static readonly string[] HumanHeroPool =
    {
        "Hamg", "Hmkg", "Hpal",
    };

    private MeleeAiState MeleeAiStateFor(int player)
    {
        if (_meleeAiStates.TryGetValue(player, out MeleeAiState state)) return state;
        state = new MeleeAiState { Book = new MeleeAiBuildBook(this, player) };
        PickHumanHeroes(state, player);
        _meleeAiStates[player] = state;
        return state;
    }

    private void PickHumanHeroes(MeleeAiState state, int player)
    {
        if (state.Hero1 != null) return;

        var heroes = new string[HumanHeroPool.Length];
        Array.Copy(HumanHeroPool, heroes, heroes.Length);
        var random = new Random(20261118 + player);
        for (int index = heroes.Length - 1; index > 0; index--)
        {
            int swap = random.Next(index + 1);
            (heroes[index], heroes[swap]) = (heroes[swap], heroes[index]);
        }
        state.Hero1 = heroes[0];
        state.Hero2 = heroes[1];
        state.Hero3 = IsReignOfChaos ? heroes[2] : "Hblm";
    }

    private int PlayerRace(int player)
    {
        if (_map?.Players == null) return 0;
        foreach (MapPlayerInfo info in _map.Players)
        {
            if (info.Id == player) return info.Race;
        }
        return 0;
    }

    private static readonly HashSet<string> TownHallIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "htow", "hkee", "hcas", "ogre", "ostr", "ofrt",
    };

    private bool IsHumanOriginalAi(PlayerSlot slot) =>
        slot != null && IsComputerPlayer(slot.Id) && PlayerRace(slot.Id) == 1;

    private int HumanCountBuildings(PlayerSlot slot, string id, bool done)
    {
        int count = CountBuildings(slot, id);
        if (done) count -= CountBuildingsUnderConstruction(slot, id);
        return Math.Max(0, count);
    }

    private int MeleeTownCount(PlayerSlot slot, string id, bool done) => id switch
    {
        HumanTownHall => HumanCountBuildings(slot, HumanTownHall, done)
            + HumanCountBuildings(slot, HumanKeep, done)
            + HumanCountBuildings(slot, HumanCastle, done),
        HumanKeep => HumanCountBuildings(slot, HumanKeep, done)
            + HumanCountBuildings(slot, HumanCastle, done),
        HumanCastle => HumanCountBuildings(slot, HumanCastle, done),
        HumanWatchTower => HumanCountBuildings(slot, HumanWatchTower, done)
            + HumanCountBuildings(slot, HumanGuardTower, done)
            + HumanCountBuildings(slot, HumanArcaneTower, done),
        HumanPeasant => CountUnits(slot, HumanPeasant) + CountUnits(slot, HumanMilitia),
        _ => HumanCountBuildings(slot, id, done) + CountUnits(slot, id),
    };

    private int HumanCountHeroAnyState(int player, string id)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        int count = 0;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || unit.Player != player || unit.Removed) continue;
            if (!string.Equals(unit.Type?.Id, id, StringComparison.OrdinalIgnoreCase)) continue;
            count++;
        }
        return count;
    }

    private List<Unit> OwnTownHalls(int player, bool includeUnderConstruction)
    {
        var halls = new List<Unit>();
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Player != player) continue;
            if (!unit.IsBuilding) continue;
            if (!includeUnderConstruction && unit.IsUnderConstruction) continue;
            string id = unit.Type?.Id;
            if (TownHallIds.Contains(id)) halls.Add(unit);
        }
        halls.Sort((left, right) => left.SimulationId.CompareTo(right.SimulationId));
        return halls;
    }

    private Unit HallForTown(PlayerSlot slot, int town)
    {
        List<Unit> halls = OwnCompletedTownHalls(slot.Id);
        if (town < 0) return FindHall(slot.Id) ?? FirstOrNull(halls);
        return town < halls.Count ? halls[town] : null;
    }

    private List<Unit> OwnCompletedTownHalls(int player)
        => OwnTownHalls(player, includeUnderConstruction: false);

    private List<Unit> ClaimedMines(int player)
    {
        var mines = new List<Unit>();
        foreach (Unit hall in OwnTownHalls(player, includeUnderConstruction: true))
        {
            Unit mine = NearestMineTo(hall.Position, null, 1200f);
            if (mine == null || mines.Contains(mine)) continue;
            mines.Add(mine);
        }
        return mines;
    }

    private Unit NearestMineTo(Vector3 position, Unit exclude, float radius)
    {
        Unit best = null;
        float bestDistance = radius;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Gold <= 0) continue;
            if (ReferenceEquals(unit, exclude)) continue;
            float distance = PlanarDistance(position, unit.Position);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = unit;
        }
        return best;
    }

    private static float PlanarDistance(Vector3 a, Vector3 b)
        => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    private List<Unit> OwnedMines(int player)
    {
        var mines = new List<Unit>();
        foreach (Unit hall in OwnCompletedTownHalls(player))
        {
            Unit mine = NearestMineTo(hall.Position, null, 1200f);
            if (mine == null || mines.Contains(mine)) continue;
            mines.Add(mine);
        }
        return mines;
    }

    private Unit FindNextExpansionMine(int player)
    {
        List<Unit> halls = OwnCompletedTownHalls(player);
        if (halls.Count == 0) return null;
        List<Unit> claimed = ClaimedMines(player);

        Unit best = null;
        float bestDistance = float.MaxValue;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Gold <= 0) continue;
            if (claimed.Contains(unit)) continue;
            float distance = PlanarDistance(halls[0].Position, unit.Position);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = unit;
        }
        return best;
    }

    private void RefreshHumanCounters(PlayerSlot slot, MeleeAiState state)
    {
        MeleeAiCounters counters = state.Counters;
        counters.Hall = MeleeTownCount(slot, HumanTownHall, false);
        counters.HallDone = MeleeTownCount(slot, HumanTownHall, true);
        counters.KeepDone = MeleeTownCount(slot, HumanKeep, true);
        counters.CastleDone = MeleeTownCount(slot, HumanCastle, true);
        counters.AltarDone = HumanCountBuildings(slot, HumanAltar, true);
        counters.Arcane = HumanCountBuildings(slot, HumanArcaneTower, false);
        counters.AviaryDone = HumanCountBuildings(slot, HumanAviary, true);
        counters.BreakerDone = CountUnits(slot, HumanSpellBreaker);
        counters.FoodMade = FoodCap(slot.Id);
        counters.FoodUsed = FoodUsed(slot.Id);
        counters.FootmanDone = CountUnits(slot, HumanFootman);
        counters.GryphonDone = CountUnits(slot, HumanGryphon);
        counters.GyroDone = CountUnits(slot, HumanCopter);
        counters.HawkDone = CountUnits(slot, HumanDragonHawk);
        counters.House = HumanCountBuildings(slot, HumanHouse, false);
        counters.HouseDone = HumanCountBuildings(slot, HumanHouse, true);
        counters.KnightDone = CountUnits(slot, HumanKnight);
        counters.MillDone = HumanCountBuildings(slot, HumanLumberMill, true);
        counters.Mines = OwnedMines(slot.Id).Count;
        counters.MortarDone = CountUnits(slot, HumanMortar);
        counters.PeasantDone = MeleeTownCount(slot, HumanPeasant, false);
        counters.PriestDone = CountUnits(slot, HumanPriest);
        counters.RiflemanDone = CountUnits(slot, HumanRifleman);
        counters.SanctumDone = HumanCountBuildings(slot, HumanSanctum, true);
        counters.SmithDone = HumanCountBuildings(slot, HumanBlacksmith, true);
        counters.SorceressDone = CountUnits(slot, HumanSorceress);
        counters.TankDone = CountUnits(slot, HumanTank);
        counters.Watch = MeleeTownCount(slot, HumanWatchTower, false);
        counters.WatchDone = MeleeTownCount(slot, HumanWatchTower, true);
        counters.WorkshopDone = HumanCountBuildings(slot, HumanWorkshop, true);
        counters.Gold = slot.Gold;
        counters.GoldOwned = 0;
        foreach (Unit mine in OwnedMines(slot.Id)) counters.GoldOwned += Math.Max(0, mine.Gold);
        counters.Hero1Done = HumanCountHeroAnyState(slot.Id, state.Hero1) >= 1;
        counters.Hero2Done = HumanCountHeroAnyState(slot.Id, state.Hero2) >= 1;
        counters.Hero3Done = HumanCountHeroAnyState(slot.Id, state.Hero3) >= 1;
        counters.KnightsOk = counters.CastleDone >= 1
            && counters.MillDone >= 1 && counters.SmithDone >= 1;

        if (state.BasicOpening
            && (counters.Hero2Done || (IsHumanNewbie && counters.CastleDone >= 1)))
            state.BasicOpening = false;
    }

    private bool IsHumanNewbie => AiDifficulty <= 0;

    private void InitializeHumanAi(PlayerSlot slot)
    {
        MeleeAiState state = MeleeAiStateFor(slot.Id);
        slot.Tasks.Clear();
        slot.Done = false;
        slot.Announced = false;
        RefreshHumanCounters(slot, state);
        BuildHumanPlan(slot, state);
        LogHuman(slot, $"原版人族 AI 启动 英雄 {UnitFactory.Get(state.Hero1)?.Name}"
            + $"/{UnitFactory.Get(state.Hero2)?.Name}/{UnitFactory.Get(state.Hero3)?.Name}");
    }

    private void TickHumanAi(PlayerSlot slot, float delta)
    {
        MeleeAiState state = MeleeAiStateFor(slot.Id);

        state.CounterTimer += delta;
        if (state.CounterTimer >= 1f)
        {
            state.CounterTimer = 0f;
            RefreshHumanCounters(slot, state);
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
            RefreshHumanCounters(slot, state);
            BuildHumanPlan(slot, state);
            ExecuteHumanBuildLoop(slot, state);
        }

        state.StatusTimer += delta;
        if (state.StatusTimer >= 15f)
        {
            state.StatusTimer = 0f;
            MeleeAiCounters counters = state.Counters;
            int golders = 0;
            int choppers = 0;
            int carrying = 0;
            foreach (Unit worker in _allUnits)
            {
                if (worker == null || !worker.Alive || worker.Player != slot.Id || !worker.IsWorker) continue;
                if (worker.GoldMine != null) golders++;
                else if (worker.Tree != null) choppers++;
                if (worker.Carry > 0 || worker.CarryLumber > 0) carrying++;
            }
            LogHuman(slot, $"人口 {counters.FoodUsed}/{counters.FoodMade} 金 {counters.Gold}"
                + $" 木 {slot.Lumber} 大厅 {counters.HallDone} 农民 {counters.PeasantDone}"
                + $" 采金工 {golders} 伐木工 {choppers} 携带 {carrying}"
                + $" 步兵 {counters.FootmanDone} 火枪 {counters.RiflemanDone}"
                + $" 计划 {state.Plan.Count} 条");
        }

        TickHumanAttack(slot, state, delta);
    }

    private void BuildHumanPlan(PlayerSlot slot, MeleeAiState state)
    {
        List<MeleeAiBuildEntry> plan = state.Plan;
        state.Book.InitBuildArray();

        if (state.BasicOpening) AppendHumanBasicOpening(slot, state, plan);
        else AppendHumanDynamicPlan(slot, state, plan);
    }

    private void AddUnit(List<MeleeAiBuildEntry> plan, int quantity, string id)
    {
        if (quantity > 0) plan.Add(MeleeAiBuildEntry.Unit(quantity, id));
    }

    private void AddUpgrade(List<MeleeAiBuildEntry> plan, int quantity, string id)
    {
        if (IsHumanNewbie && quantity != 1) return;
        if (quantity > 0) plan.Add(MeleeAiBuildEntry.Upgrade(quantity, id));
    }

    private void AddNext(PlayerSlot slot, List<MeleeAiBuildEntry> plan, int quantity, string id)
    {
        if (MeleeTownCount(slot, id, false) >= quantity) return;
        AddUnit(plan, MeleeTownCount(slot, id, true) + 1, id);
    }

    private void AddMeleeTownHall(PlayerSlot slot, List<MeleeAiBuildEntry> plan, int town, string id)
    {
        if (town != 0) return;
        int halls = MeleeTownCount(slot, HumanTownHall, false);
        if (halls == 0) plan.Add(MeleeAiBuildEntry.Expand(1, id));
    }

    private void AddBasicExpansion(PlayerSlot slot, List<MeleeAiBuildEntry> plan,
        bool buildIt, string id)
    {
        if (!buildIt) return;
        if (MeleeTownCount(slot, id, false) != MeleeTownCount(slot, id, true)) return;
        plan.Add(MeleeAiBuildEntry.Expand(MeleeTownCount(slot, id, false) + 1, id));
    }

    private void AddGuardSecondary(PlayerSlot slot, MeleeAiCounters counters,
        List<MeleeAiBuildEntry> plan, int town, int quantity, string id)
    {
        if (IsHumanNewbie) return;
        if (counters.Hall <= town || counters.Mines <= town) return;
        AddUnit(plan, quantity, id);
    }

    private void AddBuildFactory(PlayerSlot slot, List<MeleeAiBuildEntry> plan, string id)
    {
        AddUnit(plan, slot.Gold > 1000 && slot.Lumber > 500 ? 2 : 1, id);
    }

    private void AppendHumanBasicOpening(PlayerSlot slot, MeleeAiState state,
        List<MeleeAiBuildEntry> plan)
    {
        MeleeAiCounters counters = state.Counters;
        AddMeleeTownHall(slot, plan, 0, HumanTownHall);
        AddMeleeTownHall(slot, plan, 1, HumanTownHall);

        AddUnit(plan, 6, HumanPeasant);
        AddUnit(plan, 1, HumanAltar);
        AddUnit(plan, 7, HumanPeasant);
        AddUnit(plan, 1, HumanHouse);
        AddUnit(plan, 1, HumanBarracks);
        AddUnit(plan, 9, HumanPeasant);
        AddUnit(plan, 2, HumanHouse);
        AddUnit(plan, 1, state.Hero1);
        AddUnit(plan, 11, HumanPeasant);
        AddUnit(plan, 1, HumanFootman);
        AddUnit(plan, 3, HumanHouse);
        AddUnit(plan, 12, HumanPeasant);
        AddUnit(plan, 2, HumanFootman);
        AddUnit(plan, 13, HumanPeasant);
        AddUnit(plan, 3, HumanFootman);
        AddUnit(plan, 14, HumanPeasant);
        AddUnit(plan, 4, HumanHouse);
        AddUnit(plan, 15, HumanPeasant);
        AddUnit(plan, 4, HumanFootman);
        AddUnit(plan, 16, HumanPeasant);
        AddUnit(plan, 5, HumanFootman);
        AddUnit(plan, 1, HumanBlacksmith);
        AddUnit(plan, 6, HumanFootman);
        AddUnit(plan, 5, HumanHouse);
        AddUnit(plan, 8, HumanFootman);
        AddUnit(plan, 6, HumanHouse);
        AddUnit(plan, 2, HumanRifleman);
        AddUnit(plan, 7, HumanHouse);
        AddUnit(plan, 3, HumanRifleman);
        AddUnit(plan, 1, HumanArcaneVault);

        AddBasicExpansion(slot, plan, counters.Mines < 2, HumanTownHall);

        AddUnit(plan, 4, HumanRifleman);
        AddUnit(plan, 1, HumanLumberMill);

        if (!IsHumanNewbie)
        {
            AddGuardSecondary(slot, counters, plan, 1, 2, HumanWatchTower);
            if (counters.MillDone >= 1)
                AddGuardSecondary(slot, counters, plan, 1, 1, HumanGuardTower);
            if (counters.WatchDone >= 1)
                AddGuardSecondary(slot, counters, plan, 1, 1, HumanArcaneTower);
        }

        AddUnit(plan, 1, HumanKeep);
        AddUpgrade(plan, 1, HumanUpgradeDefend);
        AddUpgrade(plan, 1, HumanUpgradeArmor);
        AddUnit(plan, 7, HumanHouse);
        AddUpgrade(plan, 1, HumanUpgradeMelee);

        if (counters.KeepDone < 1) return;

        AddUnit(plan, 1, HumanWorkshop);
        if (!IsHumanNewbie) AddUnit(plan, 1, state.Hero2);
        AddUnit(plan, 1, HumanCastle);

        AddGuardSecondary(slot, counters, plan, 0, 2, HumanWatchTower);
        if (counters.WatchDone >= 3)
        {
            AddGuardSecondary(slot, counters, plan, 0, 1, HumanGuardTower);
            if (counters.WatchDone >= 4)
                AddGuardSecondary(slot, counters, plan, 0, 1, HumanArcaneTower);
        }

        AddUpgrade(plan, 1, HumanUpgradeRanged);
        AddUnit(plan, 1, HumanSanctum);
        AddUnit(plan, 2, HumanMortar);
        AddUpgrade(plan, 1, HumanUpgradeWood);
        AddUpgrade(plan, 2, HumanUpgradeArmor);
        AddUpgrade(plan, 2, HumanUpgradeWood);
    }

    private void AppendHumanDynamicPlan(PlayerSlot slot, MeleeAiState state,
        List<MeleeAiBuildEntry> plan)
    {
        MeleeAiCounters counters = state.Counters;

        if (counters.HallDone >= 1)
        {
            int peasants = 6 - slot.Lumber / 200;
            if (peasants < 3) peasants = 3;
            peasants += counters.Mines < 2 ? 5 : 10;
            if (peasants > 15) peasants = 15;
            AddNext(slot, plan, peasants, HumanPeasant);
        }

        if (counters.Hall < 1 && counters.PeasantDone >= 1)
        {
            AddMeleeTownHall(slot, plan, 0, HumanTownHall);
            AddMeleeTownHall(slot, plan, 1, HumanTownHall);
            AddMeleeTownHall(slot, plan, 2, HumanTownHall);
        }

        if (slot.Gold > 500 && slot.Lumber < 100)
            AddNext(slot, plan, 20, HumanPeasant);

        if (counters.GoldOwned < 2000)
        {
            AddBasicExpansion(slot, plan, counters.Mines < 2, HumanTownHall);
            if (!IsHumanNewbie)
            {
                AddGuardSecondary(slot, counters, plan, 1, 2, HumanWatchTower);
                if (counters.Watch >= 2)
                {
                    AddGuardSecondary(slot, counters, plan, 1, 1, HumanGuardTower);
                    AddGuardSecondary(slot, counters, plan, 1, 1, HumanArcaneTower);
                }
            }
        }

        if (counters.FoodUsed + 5 >= counters.FoodMade)
            AddUnit(plan, counters.HouseDone + 1, HumanHouse);

        if (counters.AltarDone >= 1)
        {
            if (counters.Hero1Done && !IsHumanNewbie)
            {
                if (counters.Hero2Done)
                {
                    if (counters.Hero3Done || counters.CastleDone >= 1)
                        AddUnit(plan, 1, state.Hero3);
                }
                else AddUnit(plan, 1, state.Hero2);
            }
            else AddUnit(plan, 1, state.Hero1);
        }
        else AddUnit(plan, 1, HumanAltar);

        AddUnit(plan, 1, HumanBarracks);
        if (counters.KnightsOk) AddUnit(plan, 2, HumanKnight);
        else AddUnit(plan, 4, HumanFootman);

        if (counters.AviaryDone >= 1)
        {
            AddUnit(plan, 3, HumanGryphon);
        }
        else
        {
            AddUnit(plan, 1, HumanBlacksmith);
            if (counters.SmithDone >= 1 || counters.Gold < 1000)
                AddUnit(plan, 4, HumanRifleman);
        }

        AddUnit(plan, 1, HumanKeep);
        AddUnit(plan, 1, HumanWorkshop);
        AddUnit(plan, 2, HumanMortar);
        AddUnit(plan, counters.WatchDone - counters.Arcane, HumanGuardTower);

        if (counters.Gold > 1000)
        {
            AddUnit(plan, 1, HumanArcaneVault);
            AddUnit(plan, 1, HumanBlacksmith);
            AddUnit(plan, 1, HumanLumberMill);
            AddUnit(plan, 1, HumanSanctum);
            AddUnit(plan, 1, HumanCastle);
            AddUnit(plan, 1, HumanAviary);

            AppendHumanUpgrades(slot, counters, plan);

            if (counters.Gold > 2000)
            {
                AddBuildFactory(slot, plan, HumanBarracks);
                AddBuildFactory(slot, plan, HumanSanctum);
                AddBuildFactory(slot, plan, HumanAviary);
            }
            AddUpgrade(plan, 1, HumanUpgradeSentinel);
        }
        else if (counters.FoodUsed >= 50)
        {
            AppendHumanUpgrades(slot, counters, plan);
        }

        AddBasicExpansion(slot, plan, counters.Mines < 2, HumanTownHall);

        if (counters.FoodUsed >= 70 && counters.Gold < 2000) return;

        if (counters.KnightsOk) AddNext(slot, plan, 4, HumanKnight);

        if (counters.SanctumDone >= 1)
        {
            AddNext(slot, plan, 2, HumanPriest);
            AddNext(slot, plan, 1, HumanSorceress);
            AddNext(slot, plan, 1, HumanSpellBreaker);

            if (counters.AviaryDone >= 1)
            {
                AddNext(slot, plan, 4, HumanGryphon);
                AddNext(slot, plan, 2, HumanDragonHawk);
            }
            if (counters.WorkshopDone >= 1)
                AddNext(slot, plan, 2, HumanCopter);

            AddNext(slot, plan, 2, HumanSorceress);
            AddNext(slot, plan, 1, HumanSpellBreaker);
        }

        if (counters.GoldOwned < 10000)
            AddBasicExpansion(slot, plan, counters.Mines < 3, HumanTownHall);
    }

    private void AppendHumanUpgrades(PlayerSlot slot, MeleeAiCounters counters,
        List<MeleeAiBuildEntry> plan)
    {
        if (counters.KeepDone >= 1)
        {
            AddUpgrade(plan, 1, HumanUpgradeWood);
            if (counters.CastleDone >= 1) AddUpgrade(plan, 2, HumanUpgradeWood);
            if (counters.KnightsOk) AddUpgrade(plan, 1, HumanUpgradeBreeding);

            if (counters.SanctumDone >= 1)
            {
                if (counters.PriestDone >= 1) AddUpgrade(plan, 1, HumanUpgradePraying);
                if (counters.SorceressDone >= 1) AddUpgrade(plan, 1, HumanUpgradeSorcery);
            }

            if (counters.CastleDone >= 1)
            {
                AddUpgrade(plan, 2, HumanUpgradeWood);
                if (counters.SanctumDone >= 1)
                {
                    if (counters.PriestDone >= 1) AddUpgrade(plan, 2, HumanUpgradePraying);
                    if (counters.SorceressDone >= 1) AddUpgrade(plan, 2, HumanUpgradeSorcery);
                }
            }
        }

        AddUpgrade(plan, 1, HumanUpgradeDefend);

        if (counters.SmithDone >= 1)
        {
            AddUpgrade(plan, 1, HumanUpgradeArmor);
            AddUpgrade(plan, 1, HumanUpgradeMelee);
            AddUpgrade(plan, 1, HumanUpgradeRanged);
            AddUpgrade(plan, 1, HumanUpgradeLeather);

            if (counters.KeepDone >= 1)
            {
                AddUpgrade(plan, 2, HumanUpgradeArmor);
                AddUpgrade(plan, 2, HumanUpgradeMelee);
                AddUpgrade(plan, 2, HumanUpgradeRanged);
                AddUpgrade(plan, 2, HumanUpgradeLeather);

                if (counters.CastleDone >= 1)
                {
                    AddUpgrade(plan, 3, HumanUpgradeArmor);
                    AddUpgrade(plan, 3, HumanUpgradeMelee);
                    AddUpgrade(plan, 3, HumanUpgradeRanged);
                    AddUpgrade(plan, 3, HumanUpgradeLeather);
                }
            }
        }

        if (counters.RiflemanDone >= 1 && counters.CastleDone >= 1)
            AddUpgrade(plan, 1, HumanUpgradeGunRange);
        if (counters.WorkshopDone >= 1 && counters.MortarDone >= 1)
            AddUpgrade(plan, 1, HumanUpgradeFrags);

        if (counters.MillDone >= 1)
        {
            AddUpgrade(plan, 1, HumanUpgradeMasonry);
            if (counters.KeepDone >= 1) AddUpgrade(plan, 2, HumanUpgradeMasonry);
        }

        if (counters.WorkshopDone >= 1 && counters.GyroDone >= 1)
        {
            if (counters.CastleDone >= 1) AddUpgrade(plan, 1, HumanUpgradeFlak);
            AddUpgrade(plan, 1, HumanUpgradeBombs);
        }

        if (counters.AviaryDone >= 1 && counters.GryphonDone >= 1)
            AddUpgrade(plan, 1, HumanUpgradeHammers);
        if (counters.SanctumDone >= 1 && counters.BreakerDone >= 1)
            AddUpgrade(plan, 1, HumanUpgradeControlMagic);
        if (counters.AviaryDone >= 1 && counters.HawkDone >= 1)
            AddUpgrade(plan, 1, HumanUpgradeCloud);
        if (counters.WorkshopDone >= 1 && counters.TankDone >= 1)
            AddUpgrade(plan, 1, HumanUpgradeTank);
    }

    private void ExecuteHumanBuildLoop(PlayerSlot slot, MeleeAiState state)
    {
        state.TotalGold = slot.Gold;
        state.TotalWood = slot.Lumber;
        state.Planned.Clear();

        for (int index = 0; index < state.Plan.Count; index++)
        {
            MeleeAiBuildEntry entry = state.Plan[index];
            switch (entry.Kind)
            {
                case MeleeAiBuildKind.Unit:
                    if (!StartHumanUnit(slot, state, entry)) return;
                    break;
                case MeleeAiBuildKind.Upgrade:
                    StartHumanUpgrade(slot, entry);
                    break;
                case MeleeAiBuildKind.Expand:
                    if (!StartHumanExpansion(slot, state, entry)) return;
                    break;
            }
        }
    }

    private bool StartHumanUnit(PlayerSlot slot, MeleeAiState state, MeleeAiBuildEntry entry)
    {
        UnitType type = UnitFactory.Get(entry.Id);
        if (type == null) return true;

        int planned = state.Planned.TryGetValue(entry.Id, out int queuedPlanned) ? queuedPlanned : 0;
        int pending = PendingTrainCount(slot.Id, entry.Id);
        int have = type.IsHero
            ? HumanCountHeroAnyState(slot.Id, entry.Id) + pending
            : Math.Max(MeleeTownCount(slot, entry.Id, false) + pending, planned);
        if (have >= entry.Quantity) return true;

        int need = entry.Quantity - have;
        int afford = need;
        if (type.GoldCost > 0) afford = Math.Min(afford, state.TotalGold / type.GoldCost);
        if (type.LumberCost > 0) afford = Math.Min(afford, state.TotalWood / type.LumberCost);
        if (afford < 1) return false;

        state.TotalGold = Math.Max(0, state.TotalGold - type.GoldCost * need);
        state.TotalWood = Math.Max(0, state.TotalWood - type.LumberCost * need);

        if (type.IsBuilding)
        {
            if (string.Equals(entry.Id, HumanKeep, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Id, HumanCastle, StringComparison.OrdinalIgnoreCase))
                return StartHumanTownHallUpgrade(slot, state, entry.Id);

            if (!RequirementsMet(slot, type.Requires)) return false;
            Unit hall = HallForTown(slot, entry.Town);
            if (hall == null) return false;
            if (!BuildHumanBuilding(slot, hall, entry.Id, type)) return false;
            state.Planned[entry.Id] = have + 1;
            LogHuman(slot, $"开工 {type.Name}");
            return true;
        }

        if (!type.IsHero && !RequirementsMet(slot, type.Requires)) return false;
        if (FoodUsed(slot.Id) + PendingTrainFood(slot.Id) + Mathf.Max(1, type.FoodUsed)
            > FoodCap(slot.Id)) return false;

        int queued = TrainHumanUnits(slot, entry.Id, afford, HallForTown(slot, entry.Town));
        if (queued <= 0) return false;
        state.Planned[entry.Id] = have + queued;
        LogHuman(slot, $"训练 {type.Name} x{queued}");
        return true;
    }

    private static Unit FirstOrNull(List<Unit> units) => units.Count > 0 ? units[0] : null;

    private bool BuildHumanBuilding(PlayerSlot slot, Unit hall, string buildingId, UnitType type)
    {
        if (CountActiveSites(slot) >= 3)
        {
            LogHuman(slot, $"施工位满 {type.Name}");
            return false;
        }
        if (!HasFreeHumanWorker(slot.Id))
        {
            LogHuman(slot, $"没有空闲农民 {type.Name}");
            return false;
        }

        float footprint = Mathf.Max(32f, type.FootprintRadius);
        var halls = OwnCompletedTownHalls(slot.Id);
        if (halls.Count == 0) halls.Add(hall);
        foreach (Unit origin in halls)
        {
            Unit mine = NearestMineTo(origin.Position, null, 1200f);
            PathingTextureMask spotMask = PathingTextureLibrary.Get(type.PathingTexturePath);
            if (!TryFindHumanBuildSpot(origin, mine, footprint, spotMask, out Vector3 spot)) continue;
            Unit site = PlaceBuildingAt(slot, buildingId, spot, false);
            if (site != null) return true;
        }

        LogHuman(slot, $"找不到 {type.Name} 的建造点");
        return false;
    }

    private bool TryFindHumanBuildSpot(Unit hall, Unit mine, float footprint,
        PathingTextureMask mask, out Vector3 spot)
    {
        spot = Vector3.Zero;
        bool pathAlreadyBlocked = mine != null && _pathGrid != null
            && _pathGrid.FindPath(hall.Position, mine.Position, 32f, PathLayer.Ground) == null;

        float start = Mathf.Max(260f, footprint * 2.2f);
        for (float radius = start; radius <= 4200f; radius += 100f)
        {
            int steps = radius < 900f ? 20 : 36;
            for (int step = 0; step < steps; step++)
            {
                float angle = Mathf.Tau * step / steps;
                var candidate = new Vector3(
                    Mathf.Round((hall.Position.X + Mathf.Cos(angle) * radius) / 32f) * 32f,
                    0f,
                    Mathf.Round((hall.Position.Z + Mathf.Sin(angle) * radius) / 32f) * 32f);
                candidate.Y = MapWorldBuilder.HeightAt(_map.Terrain, candidate.X, candidate.Z);

                if (!IsSpotUsable(candidate, footprint)) continue;
                if (!pathAlreadyBlocked && mine != null && _pathGrid != null)
                {
                    int probe = -1;
                    if (mask != null) probe = _pathGrid.AddFootprint(candidate, mask);
                    else _pathGrid.AddBlocker(candidate, footprint);
                    bool mineReachable = _pathGrid.FindPath(
                        hall.Position, mine.Position, 32f, PathLayer.Ground) != null;
                    if (mask != null) _pathGrid.RemoveFootprint(probe);
                    else _pathGrid.RemoveBlocker(candidate, footprint);
                    if (!mineReachable) continue;
                }

                spot = candidate;
                return true;
            }
        }
        return false;
    }


    private bool HasFreeHumanWorker(int player)
    {
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Player != player || !unit.IsWorker) continue;
            if (unit.BuildSite == null) return true;
        }
        return false;
    }

    private bool StartHumanTownHallUpgrade(PlayerSlot slot, MeleeAiState state, string targetId)
    {
        foreach (Unit hall in OwnCompletedTownHalls(slot.Id))
        {
            if (hall.Upgrading) return true;
        }
        foreach (Unit hall in OwnCompletedTownHalls(slot.Id))
        {
            if (Array.IndexOf(hall.Type.UpgradeTo, targetId) < 0) continue;
            if (!StartBuildingUpgrade(hall, targetId)) return false;
            state.Planned[targetId] = state.Planned.TryGetValue(targetId, out int planned)
                ? planned + 1 : 1;
            LogHuman(slot, $"升级 {UnitFactory.Get(targetId)?.Name}");
            return true;
        }
        return false;
    }

    private int PendingTrainCount(int player, string unitId)
    {
        int count = 0;
        foreach (BuildingOrder order in _orders)
        {
            if (order.Player != player || order.Kind != OrderKind.Train) continue;
            if (string.Equals(order.Id, unitId, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private int TrainHumanUnits(PlayerSlot slot, string unitId, int wanted, Unit near = null)
    {
        UnitType type = UnitFactory.Get(unitId);
        if (type == null) return 0;

        List<Unit> candidates = new();
        foreach (Unit building in _allUnits)
        {
            if (building == null || !building.Alive || building.Player != slot.Id) continue;
            if (!building.IsBuilding || building.IsUnderConstruction) continue;
            if (Array.IndexOf(building.Type.Trains, unitId) < 0) continue;
            candidates.Add(building);
        }
        if (near != null)
            candidates.Sort((left, right) =>
                left.Position.DistanceSquaredTo(near.Position)
                    .CompareTo(right.Position.DistanceSquaredTo(near.Position)));

        int queued = 0;
        foreach (Unit building in candidates)
        {
            if (queued >= wanted) break;
            if (building == null || !building.Alive || building.Player != slot.Id) continue;
            if (!building.IsBuilding || building.IsUnderConstruction) continue;
            if (Array.IndexOf(building.Type.Trains, unitId) < 0) continue;

            while (queued < wanted && QueueLength(building) < 2)
            {
                if (!StartTraining(building, unitId)) break;
                queued++;
            }
        }
        return queued;
    }

    private void StartHumanUpgrade(PlayerSlot slot, MeleeAiBuildEntry entry)
    {
        UpgradeType upgrade = TechTree.Upgrade(entry.Id);
        if (upgrade == null) return;

        int have = slot.ResearchLevel(entry.Id)
            + CountPlayerResearchOrders(slot.Id, entry.Id);
        if (have >= entry.Quantity) return;
        if (!RequirementsMet(slot, upgrade.Requires)) return;

        int next = have + 1;
        if (next > upgrade.MaxLevel) return;
        if (slot.Gold < upgrade.GoldFor(next) || slot.Lumber < upgrade.LumberFor(next)) return;

        foreach (Unit building in _allUnits)
        {
            if (building == null || !building.Alive || building.Player != slot.Id) continue;
            if (!building.IsBuilding || building.IsUnderConstruction) continue;
            if (Array.IndexOf(building.Type.Researches, entry.Id) < 0) continue;
            if (QueueLength(building) >= 2) continue;
            if (!StartResearch(building, entry.Id)) continue;
            LogHuman(slot, $"研究 {upgrade.Name} L{next}");
            return;
        }
    }

    private bool StartHumanExpansion(PlayerSlot slot, MeleeAiState state, MeleeAiBuildEntry entry)
    {
        int have = MeleeTownCount(slot, HumanTownHall, false);
        if (have >= entry.Quantity) return true;

        UnitType type = UnitFactory.Get(entry.Id);
        if (type == null) return true;
        if (slot.Gold < type.GoldCost || slot.Lumber < type.LumberCost) return false;

        Unit mine = FindNextExpansionMine(slot.Id);
        if (mine == null)
        {
            LogHuman(slot, "没有可扩张金矿");
            return true;
        }
        if (!HasFreeHumanWorker(slot.Id))
        {
            LogHuman(slot, "扩张没有空闲农民");
            return false;
        }
        if (!TryFindBuildSpot(mine.Position, Mathf.Max(32f, type.FootprintRadius), out Vector3 spot))
        {
            LogHuman(slot, $"扩张找不到位置 金矿({mine.Position.X:0},{mine.Position.Z:0})");
            return false;
        }

        Unit site = PlaceBuildingAt(slot, entry.Id, spot, false);
        if (site == null)
        {
            LogHuman(slot, "扩张落位失败");
            return false;
        }
        state.Attack.TakeExp = true;
        LogHuman(slot, $"扩张 {type.Name} 金矿({mine.Position.X:0},{mine.Position.Z:0})");
        return true;
    }

    private void AssignHumanPeons(PlayerSlot slot)
    {
        List<Unit> halls = OwnCompletedTownHalls(slot.Id);
        if (halls.Count == 0) return;

        Unit mainHall = halls[0];
        Unit mainMine = NearestMineTo(mainHall.Position, null, 1200f);
        Unit secondHall = halls.Count > 1 ? halls[1] : null;
        Unit secondMine = secondHall != null
            ? NearestMineTo(secondHall.Position, mainMine, 1200f)
            : null;

        var workers = new List<Unit>();
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Player != slot.Id) continue;
            if (!unit.IsWorker) continue;
            if (unit.BuildSite != null || unit.RepairTarget != null) continue;
            workers.Add(unit);
        }
        workers.Sort((left, right) => left.SimulationId.CompareTo(right.SimulationId));

        int mainAssigned = 0;
        int secondAssigned = 0;
        int woodAssigned = 0;
        var idle = new List<Unit>();

        foreach (Unit worker in workers)
        {
            if (ReferenceEquals(worker.GoldMine, mainMine) && mainMine != null)
            {
                mainAssigned++;
                continue;
            }
            if (ReferenceEquals(worker.GoldMine, secondMine) && secondMine != null)
            {
                secondAssigned++;
                continue;
            }
            if (worker.Tree != null)
            {
                if (worker.Carry > 0) { idle.Add(worker); continue; }
                woodAssigned++;
                continue;
            }
            idle.Add(worker);
        }

        foreach (Unit worker in idle)
        {
            if (worker.Carry > 0 && mainMine != null)
            {
                if (!ReferenceEquals(worker.GoldMine, mainMine)) _world.SetGather(worker, mainMine);
                mainAssigned++;
                continue;
            }
            if (worker.CarryLumber > 0)
            {
                if (worker.Tree == null)
                {
                    TreeState tree = NearestTreeTo(worker.Position);
                    if (tree != null) _world.SetChop(worker, tree);
                }
                woodAssigned++;
                continue;
            }
            if (mainAssigned < 5 && mainMine != null)
            {
                _world.SetGather(worker, mainMine);
                mainAssigned++;
                continue;
            }
            if (secondAssigned < 5 && secondMine != null)
            {
                _world.SetGather(worker, secondMine);
                secondAssigned++;
                continue;
            }
            if (woodAssigned < 15)
            {
                TreeState tree = NearestTreeTo(worker.Position);
                if (tree != null) _world.SetChop(worker, tree);
                woodAssigned++;
            }
        }

    }

    private void LogHuman(PlayerSlot slot, string message)
    {
        if (_humanAiLogBudget <= 0) return;
        _humanAiLogBudget--;
        GD.Print($"[HUMAN-AI] P{slot.Id} {message}");
    }
}