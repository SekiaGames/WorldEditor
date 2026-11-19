//重构情况：从 Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Jass/JassGameNatives.HarvestTiming.cs 转换（骨架，方法体待补）
namespace OpenWC3.JassToSharp.Ai.Economy;

public static class HarvestTiming
{
    public const int MineSlots = 5;

    public static float ServicePeriod(float interval, int activeWorkers) =>
        activeWorkers <= 0 ? interval : MathF.Max(0.01f, interval / MathF.Max(1, activeWorkers));

    public static double DueAt(double now, float period) => now + MathF.Max(0.01f, period);

    public static bool IsOverdue(double now, double due) => now >= due;

    public static float TripSeconds(float distance, float speed, float interval)
    {
        float walk = speed <= 0.01f ? 0f : distance / speed;
        return walk * 2f + interval;
    }
}

/*
原文（JassGameNatives.HarvestTiming.cs，270 行）：

namespace OpenWC3.Game;

public sealed partial class JassGameNatives
{
    // Normal 0.05s steps and the 0.01s minimum mine period need at most
    // 32 * 5 slot events. Retain overdue events/progress across an abnormal
    // clock jump rather than letting one advance monopolize the frame.
    const int HarvestMineEventBudget=256;
    static double HarvestNow(Unit unit)=>(double)unit.World.Tick*World.TickSeconds;

    HarvestMineClock MineClock(Unit mine)
    {
        var clock=mine.GetAbility<HarvestMineClock>();
        if(clock==null)mine.AddAbility(clock=new HarvestMineClock(this,mine));
        return clock;
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
        {
            Advance();
            if(!Valid(order)||_retired)return false;
            if(_channels.Contains(order))return true;
            if(_channels.Count>=natives.GoldMineCapacity(mine))return false;
            if(_channels.Count==0)_remaining=1;
            _channels.Add(order);
            ScheduleRateChange();
            return true;
        }

        public int NearestFreeAcolyteSlot(Unit worker)
        {
            Advance();
            int capacity=natives.GoldMineCapacity(mine);
            Span<bool> used=stackalloc bool[capacity];
            used.Clear();
            foreach(var order in _channels)
                if(order._channelSlot>=0&&order._channelSlot<capacity)used[order._channelSlot]=true;
            int best=-1;
            float distance=float.PositiveInfinity;
            float radius=natives.GoldMineChannelRadius(mine);
            for(int i=0;i<capacity;i++)
            {
                if(used[i])continue;
                float angle=-MathF.PI*.5f+i*MathF.Tau/capacity;
                Vec2 point=mine.Pos+new Vec2(MathF.Cos(angle),MathF.Sin(angle))*radius;
                float candidate=Vec2.Dist(worker.Pos,point);
                if(candidate<distance){distance=candidate;best=i;}
            }
            return best;
        }

        public void Leave(PlayerHarvestOrder order)
        {
            Advance();
            if(order.MineQueueNode?.List==_waiting)_waiting.Remove(order.MineQueueNode);
            order.MineRegistered=false;
            if(_channels.Remove(order))ScheduleRateChange();
            foreach(var slot in _slots)if(slot.Order==order)ReleaseSlot(slot,HarvestNow(mine));
            if(!_advancing)FillSlots(HarvestNow(mine));
        }

        public bool CanEnter(int player,double time)
            =>_claimPlayer==15||player==15||time>=_claimReleaseDue
                ||(natives._world.PlayerAdvancedControlQuery(_claimPlayer,player)
                    &&natives._world.PlayerAdvancedControlQuery(player,_claimPlayer));

        void ReleaseSlot(Slot slot,double time)
        {
            slot.Order=null;
            if(--_servicingCount==0)
                _claimReleaseDue=time+natives._world.GameplayConfig.GoldMineOwnDuration;
        }

        bool Valid(PlayerHarvestOrder order)=>order!=null&&order._worker?.Alive==true
            &&!order._worker.Removed&&ReferenceEquals(order._worker.Current,order)
            &&natives.CanHarvestGold(order._worker,mine,checkOwnership:false);

        void FillSlots(double time)
        {
            if(_retired||_waiting.Count==0||IsHauntedMine(mine)||IsEntangledMine(mine)||!mine.Alive||mine.Removed||mine.ResourceAmount<=0)return;
            int capacity=natives.GoldMineCapacity(mine);
            while(_slots.Count<capacity)_slots.Add(new());
            for(int i=0;i<capacity;i++)
            {
                Slot slot=_slots[i];
                if(slot.Order!=null)continue;
                while(_waiting.First!=null)
                {
                    var order=_waiting.First.Value;_waiting.RemoveFirst();
                    if(!Valid(order)||!CanEnter(order._worker.PlayerId,time))
                    {order.MineRegistered=false;continue;}
                    if(_servicingCount++==0)
                    {
                        _claimPlayer=order._worker.PlayerId;
                        _claimReleaseDue=double.PositiveInfinity;
                    }
                    slot.Order=order;
                    slot.Due=time+natives.GoldMineDuration(mine);
                    order.StartMineService();
                    break;
                }
            }
        }

        public override void OnTick(float dt)=>Advance();
        public override void OnRemove()
        {
            _retired=true;
            foreach(var order in _channels.Concat(_waiting).Concat(_slots.Select(s=>s.Order)).Where(o=>o!=null).ToArray())
            {
                order.MineRegistered=false;
                if(ReferenceEquals(order._worker.Current,order))order._worker.CancelOrders();
            }
            _waiting.Clear();_channels.Clear();_slots.Clear();
        }

        void ScheduleRateChange()
        {
            if(_channels.Count==0)
            {_remaining=1;_rateCount=0;_pendingRateCount=0;_rateChangeDue=double.PositiveInfinity;return;}
            _pendingRateCount=_channels.Count;
            // 4cbe90 defers 4cbe10 by 0.01 seconds. Split the next simulation
            // step at that deadline rather than rounding it to a whole tick.
            _rateChangeDue=HarvestNow(mine)+.01;
        }

        public void Advance()
        {
            if(_advancing||_retired)return;
            double now=HarvestNow(mine);
            if(now<=_lastTime+1e-9)return;
            _advancing=true;
            try
            {
                double start=_lastTime,elapsed=Math.Max(0,now-start);
                _lastTime=now;
                if(!mine.Alive||mine.Removed||mine.ResourceAmount<=0)return;
                if(mine.State.Paused)
                {
                    foreach(var slot in _slots)if(slot.Order!=null)slot.Due+=elapsed;
                    return;
                }
                for(int index=_channels.Count-1;index>=0;index--)
                {
                    var order=_channels[index];
                    if(Valid(order))continue;
                    _channels.RemoveAt(index);
                    if(ReferenceEquals(order._worker.Current,order))order._worker.CancelOrders();
                    ScheduleRateChange();
                }
                for(var node=_waiting.First;node!=null;)
                {
                    var next=node.Next;
                    if(!Valid(node.Value))
                    {
                        var order=node.Value;_waiting.Remove(node);order.MineRegistered=false;
                        if(ReferenceEquals(order._worker.Current,order))order._worker.CancelOrders();
                    }
                    node=next;
                }
                foreach(var slot in _slots)
                    if(slot.Order!=null&&!Valid(slot.Order))
                    {
                        var order=slot.Order;ReleaseSlot(slot,now);order.MineRegistered=false;
                        if(ReferenceEquals(order._worker.Current,order))order._worker.CancelOrders();
                    }
                if(IsHauntedMine(mine)||IsEntangledMine(mine))
                {
                    if(_channels.Count==0){_remaining=1;return;}
                    double beforeChange=Math.Max(0,Math.Min(now,_rateChangeDue)-start);
                    double progress=beforeChange*_rateCount;
                    if(_rateChangeDue<=now)
                    {
                        _rateCount=_pendingRateCount;
                        progress+=Math.Max(0,now-Math.Max(start,_rateChangeDue))*_rateCount;
                        _rateChangeDue=double.PositiveInfinity;
                    }
                    progress/=natives.GoldMineCapacity(mine)*natives.GoldMineDuration(mine);
                    _remaining-=progress;
                    int events=0;
                    while(_remaining<=1e-7&&mine.ResourceAmount>0&&_channels.Count>0
                        &&events++<HarvestMineEventBudget)
                    {
                        _remaining+=1;
                        int amount=Math.Min(natives.GoldMineAmountPerChannel(mine),mine.ResourceAmount);
                        mine.ResourceAmount-=amount;
                        natives.DepositHarvest(mine,true,amount);
                        if(mine.ResourceAmount==0)natives.FinishGoldExtraction(mine,null);
                    }
                }
                else
                {
                    // Complete events in due-time order, preserving residual time
                    // when a slot immediately serves the next FIFO waiter.
                    for(int events=0;events<HarvestMineEventBudget;events++)
                    {
                        Slot next=null;
                        foreach(var slot in _slots)
                            if(slot.Order!=null&&slot.Due<=now+1e-7&&(next==null||slot.Due<next.Due))next=slot;
                        if(next==null||mine.ResourceAmount<=0)break;
                        var order=next.Order;double due=next.Due;
                        ReleaseSlot(next,due);order.MineRegistered=false;
                        order.CompleteMineService();
                        FillSlots(due);
                    }
                    FillSlots(now);
                }
            }
            finally {_advancing=false;}
        }
    }
}

*/
