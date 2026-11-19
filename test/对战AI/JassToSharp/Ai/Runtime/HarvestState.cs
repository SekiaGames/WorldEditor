//重构情况：从 Open-Warcraft3_Wiki JassGameNatives.Harvest 的 PlayerHarvestOrder 状态与 Ahar 物编参数转换
namespace OpenWC3.JassToSharp.Ai.Runtime;

public enum HarvestPhase
{
    Idle,
    ToResource,
    Harvesting,
    ToBase,
}

public sealed class HarvestState
{
    public HarvestPhase Phase;
    public bool Gold;
    public int SimulationId;
    public float Timer;
    public float Cargo;
    public float Capacity;

    public static string AnimationFor(HarvestPhase phase, bool gold) => phase switch
    {
        HarvestPhase.ToResource
            or HarvestPhase.ToBase => "Walk",
        HarvestPhase.Harvesting => gold ? "Gold" : "Lumber",
        _ => "Stand",
    };
}

public sealed class HarvestStateStore
{
    readonly Dictionary<int, HarvestState> _states = new();

    public HarvestState For(int simulationId)
    {
        if (_states.TryGetValue(simulationId, out HarvestState state)) return state;
        state = new HarvestState { SimulationId = simulationId };
        _states[simulationId] = state;
        return state;
    }

    public void Remove(int simulationId) => _states.Remove(simulationId);

    public IEnumerable<HarvestState> All => _states.Values;
}

/*
原文（JassGameNatives.HarvestTiming.cs 节选，快照结构）：

    }

    public readonly record struct HarvestMineTiming(int Waiting,int Servicing,int Channeling,double RemainingProgress);
    public HarvestMineTiming GetHarvestMineTiming(Unit mine)
        =>mine?.GetAbility<HarvestMineClock>()?.Snapshot??default;

    // Aenc's five native occupant slots are backed by the wisp harvest channels
    // in this host. Project the live channel order without copying it every HUD frame.
    public IReadOnlyList<Unit> GetEntangledMineOccupants(Unit mine)
    {
        if(!IsEntangledMine(mine))return Array.Empty<Unit>();
        return mine.GetAbility<HarvestMineClock>()?.ChannelWorkers??Array.Empty<Unit>();
    }

    // Game.dll: Agld timers live in service slots (5d1660/5d1740);
    // Abgm/Aegm share a Float progress property (4cbe10/10e250).
    sealed class HarvestMineClock(JassGameNatives natives,Unit mine):Ability
    {
        sealed class Slot { public PlayerHarvestOrder Order; public double Due; }
        readonly List<Slot> _slots=new();
*/
