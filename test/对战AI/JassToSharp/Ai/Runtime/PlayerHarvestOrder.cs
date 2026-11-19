//重构情况：从 Open-Warcraft3_Wiki JassGameNatives.Harvest 的 PlayerHarvestOrder 转换（目标跟踪 + 打断/恢复 + 阶段推进）
namespace OpenWC3.JassToSharp.Ai.Runtime;

public enum HarvestAction
{
    Idle,
    MoveToResource,
    MoveToBase,
    TakeCargo,
    Deliver,
}

public struct HarvestFacts
{
    public bool CarryingFull;
    public bool AtResource;
    public bool AtBase;
    public bool SlotGranted;
    public float Interval;
}

public sealed class PlayerHarvestOrder
{
    public HarvestAction Issued;
    public int TargetId = -1;
    public bool Gold;
    public float Cargo;
    public float Capacity = 1f;
    public float HarvestTimer;
    public float StuckTimer;
    public bool Interrupted;

    public void StartHarvest(int targetId, bool gold, float capacity)
    {
        TargetId = targetId;
        Gold = gold;
        Capacity = Math.Max(1f, capacity);
        HarvestTimer = 0f;
        StuckTimer = 0f;
        Interrupted = false;
        Issued = HarvestAction.Idle;
    }

    public void Interrupt()
    {
        Interrupted = true;
        HarvestTimer = 0f;
    }

    public void Resume()
    {
        Interrupted = false;
        StuckTimer = 0f;
        Issued = HarvestAction.Idle;
    }

    public bool IsFull => Cargo >= Capacity;

    public HarvestAction Tick(float delta, in HarvestFacts facts)
    {
        if (Interrupted) return HarvestAction.Idle;

        if (facts.CarryingFull)
        {
            HarvestTimer = 0f;
            return facts.AtBase ? HarvestAction.Deliver : HarvestAction.MoveToBase;
        }

        if (!facts.AtResource) return HarvestAction.MoveToResource;
        if (!facts.SlotGranted) return HarvestAction.Idle;

        HarvestTimer += delta;
        if (HarvestTimer < facts.Interval) return HarvestAction.Idle;
        HarvestTimer = 0f;
        return HarvestAction.TakeCargo;
    }

    public bool ShouldIssue(HarvestAction action, bool moved, float delta)
    {
        if (action != Issued)
        {
            Issued = action;
            StuckTimer = 0f;
            return true;
        }
        if (moved)
        {
            StuckTimer = 0f;
            return false;
        }
        StuckTimer += delta;
        if (StuckTimer < 1.5f) return false;
        StuckTimer = 0f;
        return true;
    }

    public void ClearCargo() => Cargo = 0f;

    public void Reset()
    {
        HarvestTimer = 0f;
        StuckTimer = 0f;
        Issued = HarvestAction.Idle;
    }
}

/*
原文（JassGameNatives.Harvest.cs 节选，起始于 sealed class PlayerHarvestOrder）：

    sealed class PlayerHarvestOrder:JassGameNativesOrder
    {
        enum Phase { ToResource,RetryLumber,WaitingForMine,MiningInside,FacingResource,Harvesting,ToHome }

        readonly Unit _mine;
        readonly MovementGroup _movementGroup;
        bool _movementGroupConsumed;
        DestructableRef _tree;
        Phase _phase;
        MovementExecution _move;
        CLrPath _retiredCursor;

        Vec2 _moveGoal;
        CPathingFootprint _movePathingTarget;
        float _moveCompletionRadius;
        Unit _moveContact;
        Unit _home;
        float _harvestRemaining;
        internal Unit _worker;
        internal bool MineRegistered;
        internal LinkedListNode<PlayerHarvestOrder> MineQueueNode;
        HarvestMineClock _mineClock;
        enum LumberStage { Windup,Backswing,Recovery }
        LumberStage _lumberStage;
        double _lumberDue,_lumberRecoveryDue;
        bool _insideMine;
        bool _wasHidden;
        bool _wasInvulnerable;
        bool _wasUntargetable;
        bool _pathingWasEnabled;
        bool _harvestCollisionDisabled;
        bool _moveFailed;
        int _arrivalRetries;
        double _lumberRetryDue;
        int _homeRetries;
        Unit _returnHome;
        Unit SelectReturnHome(Unit worker,bool gold,Vec2 center)
            // FUN_6f3f9000 searches the receivers on each return cycle.
            // Retaining the last receiver hides a newly completed expansion.
            =>Owner.FindHarvestDropOff(worker,gold,center);
        float _phaseStartHomeDist;
        bool _escapeActive;
        int _escapeLegsUsed;
        const int EscapeProbeBudget=5600;   // 规划探针专用, 非移动搜索预算
        const float EscapeLegRange=512f;
        const int EscapeLegLimit=8;
        readonly List<Vec2> _escapeCandidates=new();
        int _escapeCandidateIdx;
        bool _acolyteChannel;
        bool _wispLumberChannel;
        bool _wispGoldChannel;
        bool _mountedTree;
        Vec2 _wispDismountPosition;
        Vec2 _mineDismountPosition;
        SpecialEffect _wispTreeEffect;
        internal int _channelSlot=-1;
        int _lumberAnimationVersion;

        public PlayerHarvestOrder(JassGameNatives owner,Unit mine,DestructableRef tree,
            MovementGroup movementGroup=null)
*/
