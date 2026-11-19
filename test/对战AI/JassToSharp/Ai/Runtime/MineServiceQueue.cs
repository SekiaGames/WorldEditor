//重构情况：从 Open-Warcraft3_Wiki JassGameNatives.HarvestTiming 的 HarvestMineClock 服务槽/等待队列转换
namespace OpenWC3.JassToSharp.Ai.Runtime;

public sealed class MineServiceQueue
{
    readonly int _slots;
    readonly List<int> _active = new();
    readonly List<int> _waiting = new();

    public MineServiceQueue(int slots) => _slots = Math.Max(1, slots);

    public int ActiveCount => _active.Count;

    public int WaitingCount => _waiting.Count;

    public bool TryClaim(int workerId)
    {
        if (_active.Contains(workerId)) return true;
        if (_active.Count < _slots)
        {
            _active.Add(workerId);
            _waiting.Remove(workerId);
            return true;
        }
        if (!_waiting.Contains(workerId)) _waiting.Add(workerId);
        return false;
    }

    public void Release(int workerId)
    {
        if (!_active.Remove(workerId))
        {
            _waiting.Remove(workerId);
            return;
        }
        if (_waiting.Count == 0) return;
        _active.Add(_waiting[0]);
        _waiting.RemoveAt(0);
    }

    public bool IsActive(int workerId) => _active.Contains(workerId);

    public bool IsWaiting(int workerId) => _waiting.Contains(workerId);
}

/*
原文（JassGameNatives.HarvestTiming.cs 节选，起始于 sealed class HarvestMineClock）：

    sealed class HarvestMineClock(JassGameNatives natives,Unit mine):Ability
    {
        sealed class Slot { public PlayerHarvestOrder Order; public double Due; }
        readonly List<Slot> _slots=new();
        readonly LinkedList<PlayerHarvestOrder> _waiting=new();
        readonly List<PlayerHarvestOrder> _channels=new();
        double _lastTime=HarvestNow(mine),_remaining=1;
        int _rateCount,_pendingRateCount;
        double _rateChangeDue=double.PositiveInfinity;
        int _claimPlayer=15,_servicingCount;
        double _claimReleaseDue=double.PositiveInfinity;
        bool _advancing,_retired;
        public override string Name=>"采矿调度";
        public override bool IsPassive=>true;
        public override bool TickRunsWhilePaused=>true;
        public override void OnAdd()=>AdjustScriptVisibility(true);
        public HarvestMineTiming Snapshot=>new(_waiting.Count,_slots.Count(s=>s.Order!=null),_channels.Count,_remaining);
        ChannelWorkerView _channelWorkerView;
        public IReadOnlyList<Unit> ChannelWorkers=>_channelWorkerView??=new(this);

        sealed class ChannelWorkerView(HarvestMineClock owner):IReadOnlyList<Unit>
        {
            public int Count=>owner._channels.Count;
            public Unit this[int index]=>owner._channels[index]._worker;
            public IEnumerator<Unit> GetEnumerator()
            {
                for(int i=0;i<Count;i++)yield return this[i];
            }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
        }

        public void Enqueue(PlayerHarvestOrder order)
        {
            Advance();
            // Waiters call this each tick. The order belongs to one immutable
            // mine, so membership doesn't require rescanning its entire queue.
            if(order.MineRegistered)return;
            if(!Valid(order)||_retired)return;
            order.MineRegistered=true;
            order.MineQueueNode??=new LinkedListNode<PlayerHarvestOrder>(order);
            _waiting.AddLast(order.MineQueueNode);
            FillSlots(HarvestNow(mine));
        }

        public bool JoinChannel(PlayerHarvestOrder order)
*/
