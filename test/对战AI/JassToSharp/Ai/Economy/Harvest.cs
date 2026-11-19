//重构情况：从 Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Jass/JassGameNatives.Harvest.cs 转换（骨架，方法体待补）
namespace OpenWC3.JassToSharp.Ai.Economy;

public readonly record struct HarvestSpec(int PerSwing, int Capacity, int GoldPerTrip, float Interval, float Radius)
{
    public int SwingsPerTrip => PerSwing <= 0 ? 0 : Math.Max(1, (Capacity + PerSwing - 1) / PerSwing);
    public float TripSeconds => SwingsPerTrip * Interval;
}

public static class Harvest
{
    public static HarvestSpec Spec(int dataA, int dataB, int dataC, float dur, float area) =>
        new(Math.Max(0, dataA), Math.Max(1, dataB), Math.Max(0, dataC),
            MathF.Max(0.01f, dur), MathF.Max(0f, area));

    public static int Take(int capacity, int carried, int available)
    {
        int room = Math.Max(0, capacity - carried);
        return Math.Min(room, Math.Max(0, available));
    }

    public static int ChopAmount(int perSwing, int carried, int capacity) =>
        Math.Max(0, Math.Min(perSwing, capacity - carried));

    public static bool CanHarvestGold(bool typeCanGather, bool typeIsWorker) => typeCanGather && typeIsWorker;

    public static int RemainingTrips(int mineGold, int goldPerTrip) =>
        goldPerTrip <= 0 ? 0 : (mineGold + goldPerTrip - 1) / goldPerTrip;
}

/*
原文（JassGameNatives.Harvest.cs，1692 行）：

using OpenWC3.Pathing;
using OpenWC3.War3Formats;
using OpenWC3.War3Formats.Jass;

namespace OpenWC3.Game;

public sealed partial class JassGameNatives
{
    const int HarvestOrderId=852018;

    public bool IssuePlayerResumeHarvest(Unit worker)
    {
        if(!CanHarvest(worker)||HarvestAbility(worker) is not RuntimeHarvestAbility harvest)return false;
        if(worker.HarvestCargo<=0&&harvest.RememberedMine==null
            &&harvest.RememberedTreeCreationNumber<0)return false;
        if(worker.HarvestCargo>0)
        {
            var receiver=FindHarvestDropOff(worker,worker.HarvestCargoGold,worker.Pos);
            if(receiver==null)return false;
            worker.ForceIssue(new PlayerHarvestOrder(this,null,null)
            {
                ReturnOnly=true,ResumeAfterReturn=true,ReturnReceiver=receiver,ReturnCargoOnStart=true,
                JassOrderId=852017,EventTargetKind=EventTargetKind.None,
            });
        }
        else
        {
            var order=CreateResumeHarvestOrder(worker);
            if(order==null)worker.CancelOrders();
            else worker.ForceIssue(order);
        }
        return true;
    }

    PlayerHarvestOrder CreateResumeHarvestOrder(Unit worker)
    {
        if(HarvestAbility(worker) is not RuntimeHarvestAbility harvest)return null;
        var mine=harvest.RememberedMine;
        if(mine!=null&&CanHarvestGold(worker,mine)&&IsAvailableGoldMine(mine)&&IsGoldMineUnit(mine))
            return new PlayerHarvestOrder(this,mine,null){JassOrderId=852017,EventTargetKind=EventTargetKind.None};
        var tree=_destructables.FirstOrDefault(value=>value.CreationNumber==harvest.RememberedTreeCreationNumber);
        if(!IsLiveHarvestTree(tree))
        {
            var exhausted=tree;
            tree=FindNearestHarvestTree(worker,worker.Pos,exhausted);
            if(tree==null&&harvest.RememberedTreeCreationNumber>=0)
                tree=FindNearestHarvestTree(worker,harvest.RememberedTreePosition,exhausted);
        }
        return CanHarvestLumber(worker)&&IsLiveHarvestTree(tree)
            ?new PlayerHarvestOrder(this,null,tree){JassOrderId=852017,EventTargetKind=EventTargetKind.None}:null;
    }

    public bool IssuePlayerReturnResources(Unit worker,Unit receiver=null)
    {
        if(!CanHarvest(worker)||worker.HarvestCargo<=0)return false;
        receiver??=FindHarvestDropOff(worker,worker.HarvestCargoGold,worker.Pos);
        if(receiver==null||receiver.PlayerId!=worker.PlayerId
            ||!AcceptsHarvestReturn(receiver,worker.HarvestCargoGold))return false;
        worker.ForceIssue(new PlayerHarvestOrder(this,null,null)
        {
            ReturnOnly=true,ReturnReceiver=receiver,ReturnCargoOnStart=true,
            JassOrderId=852020,
            EventTargetKind=EventTargetKind.None,
        });
        return true;
    }

    public bool IssuePlayerHarvest(Unit worker,Unit target,Vec2 point,bool queued=false)
    {
        // 57aa50 redirects an Awha tree-channel target to its remembered tree.
        if(target?.Current is PlayerHarvestOrder mounted&&mounted.MountedWispTree is DestructableRef mountedTree)
            return IssuePlayerHarvest(worker,mountedTree,queued);
        if(target?.ResourceAmount>0)
        {
            if(!CanHarvestGold(worker,target)||!IsAvailableGoldMine(target)
                ||!IsGoldMineUnit(target))return false;
            var order=new PlayerHarvestOrder(this,target,null)
            {
                ReturnCargoOnStart=IsReturningHarvest(worker),
                JassOrderId=HarvestOrderId,
                EventTargetKind=EventTargetKind.Unit,
            };
            if(queued)worker.Issue(order,true);else worker.ForceIssue(order);
            return true;
        }
        // A unit-target gather order is valid only for an actual mine. Do not
        // reinterpret an enemy, tavern, or other unit as a point near a tree.
        if(target!=null)return false;

        // ScreenToTerrain projects through the visible tree crown onto the ground,
        // which can be several cells away from the destructable origin.  Warcraft's
        // widget picking still selects that tree, so keep a canopy-sized tolerance.
        DestructableRef tree=FindHarvestTree(point,320f);
        if(tree==null)return false;
        return IssuePlayerHarvest(worker,tree,queued);
    }

    public bool IssuePlayerHarvest(Unit worker,int destructableCreationNumber,bool queued=false)
    {
        DestructableRef tree=_destructables.FirstOrDefault(value=>
            value.CreationNumber==destructableCreationNumber);
        if(tree==null)return false;
        // 指定树木不可达时, 在工人附近自动找最近可伐木
        if(!CanHarvestLumber(worker)||!IsLiveHarvestTree(tree))
        {
            var alt=FindHarvestTree(worker.Pos,400f);
            if(alt!=null)tree=alt;
        }
        return IssuePlayerHarvest(worker,tree,queued);
    }

    public int IssuePlayerHarvest(IReadOnlyList<Unit> workers,
        int destructableCreationNumber,bool queued=false)
    {
        DestructableRef tree=_destructables.FirstOrDefault(value=>
            value.CreationNumber==destructableCreationNumber);
        return IssuePlayerHarvest(workers,null,tree,queued);
    }

    public int IssuePlayerHarvest(IReadOnlyList<Unit> workers,Unit target,Vec2 point,
        bool queued=false)
    {
        if(target?.Current is PlayerHarvestOrder mounted&&mounted.MountedWispTree is DestructableRef mountedTree)
            return IssuePlayerHarvest(workers,null,mountedTree,queued);
        if(target?.ResourceAmount>0)
            return IssuePlayerHarvest(workers,target,null,queued);
        if(target!=null)return 0;
        DestructableRef tree=FindHarvestTree(point,320f);
        return IssuePlayerHarvest(workers,null,tree,queued);
    }

    public bool CanPlayerHarvest(Unit worker)=>CanHarvest(worker);

    internal bool IsAcolyteChannelingGold(Unit worker)=>
        worker?.Current is PlayerHarvestOrder order&&order.IsAcolyteChanneling;

    internal bool IsWispMountedOnTree(Unit worker)=>
        worker?.Current is PlayerHarvestOrder order&&order.IsWispMounted;

    internal bool IsWispInsideEntangledMine(Unit worker)=>
        worker?.Current is PlayerHarvestOrder order&&order.IsWispGoldChanneling;

    // 1e8a70 -> 4708c0 -> 46ffc0 resolves the passenger's Acrg carrier.
    // Mine occupants use the harvest host instead of CargoTransport: exposing that
    // identity must not suspend their harvest order as TransportUnit would.
    Unit LoadedCarrier(Unit unit)=>unit?.TransportUnit
        ??(unit?.Current is PlayerHarvestOrder order&&order.InsideMine?order.Mine:null);

    internal float HarvestTreeLife(int creationNumber)=>_destructables
        .FirstOrDefault(tree=>tree.CreationNumber==creationNumber)?.Life??0;

    internal int CarriedHarvestAmount(Unit worker)=>worker?.HarvestCargo??0;

    internal string HarvestAnimationTag(Unit worker)=>
        worker?.Alive==true&&!worker.Removed
            &&worker.HarvestCargo>0
            &&CarriesVisibleHarvestBundle(worker)
                ?worker.HarvestCargoGold?"Gold":"Lumber"
                :null;

    internal bool IsActivelyCuttingLumber(Unit worker)=>
        worker?.Current is PlayerHarvestOrder order&&order.IsCuttingLumber;

    internal bool IsReturningHarvest(Unit worker)=>
        worker?.Current is PlayerHarvestOrder order&&order.IsReturning;
    internal string HarvestMovementTimingDebug(Unit worker)=>
        worker?.Current is PlayerHarvestOrder order?order.MovementTimingDebug:"<no-harvest-order>";

    internal bool TryGetHarvestMoveDebug(Unit worker,out Vec2 navigationGoal,
        out int treeCreationNumber,out int movementGroupId,out int requestCount)
    {
        if(worker?.Current is PlayerHarvestOrder order)
        {
            navigationGoal=order.MoveGoal;
            treeCreationNumber=order.TreeCreationNumber;
            movementGroupId=order.MovementGroupId;
            requestCount=order.MovementRequestCount;
            return true;
        }
        navigationGoal=default;
        treeCreationNumber=-1;
        movementGroupId=-1;
        requestCount=0;
        return false;
    }

    internal bool TryGetHarvestReturnDebug(Unit worker,out Vec2 navigationGoal,
        out Vec2 movementTarget,out int dropOffSimulationId)
    {
        // 逃逸腿阶段目标是中途路点而非家, 返程诊断/震荡检测不评估。
        if(worker?.Current is PlayerHarvestOrder order&&order.IsReturning
            &&!order.IsEscapeLegActive)
        {
            navigationGoal=order.MoveGoal;
            movementTarget=order.MoveTarget;
            dropOffSimulationId=order.HomeSimulationId;
            return true;
        }
        navigationGoal=default;
        movementTarget=default;
        dropOffSimulationId=-1;
        return false;
    }

    // 供回归检查: 当前订单是否采集单及其种类 (gold/lumber)。
    internal static string CurrentHarvestKind(Unit unit)=>unit?.Current
        is PlayerHarvestOrder order?order.Gold?"gold":"lumber":null;

    bool IssuePlayerHarvest(Unit worker,DestructableRef tree,bool queued=false)
    {
        if(!CanHarvestLumber(worker)||!IsLiveHarvestTree(tree))return false;
        // 同目标重复采集是 no-op (对标原版重复订单语义): AMAI 每秒的
        // "Correcting Harvest" 会重发采集单, 直接替换会清零砍伐计时,
        // 工人永远完不成一次砍伐。
        // Shift 排队是玩家的显式追加指令, 不能被这条去重吞掉。
        if(!queued&&worker.Current is PlayerHarvestOrder current
            &&!current.Gold&&ReferenceEquals(current.Tree,tree))
            return true;
        var order=new PlayerHarvestOrder(this,null,tree)
        {
            ReturnCargoOnStart=IsReturningHarvest(worker),
            JassOrderId=HarvestOrderId,
            EventTargetKind=EventTargetKind.Destructable,
        };
        if(queued)worker.Issue(order,true);else worker.ForceIssue(order);
        return true;
    }

    int IssuePlayerHarvest(IReadOnlyList<Unit> workers,Unit mine,
        DestructableRef tree,bool queued=false)
    {
        Unit[] accepted=(workers??Array.Empty<Unit>())
            .Where(worker=>mine!=null
                ?CanHarvestGold(worker,mine):CanHarvestLumber(worker))
            .Distinct().ToArray();
        if(accepted.Length==0
            ||mine!=null&&(!IsAvailableGoldMine(mine)||!IsGoldMineUnit(mine))
            ||mine==null&&!IsLiveHarvestTree(tree))return 0;
        Vec2 commandTarget=mine?.Pos??tree.Position;
        MovementGroup movementGroup=_world.Movement.CreateGroup(accepted,
            commandTarget,formation:false,targetObject:mine);
        foreach(Unit worker in accepted)
        {
            // 同目标重复采集 no-op (与单工入口同语义): 替换会清零砍伐计时
            // 与已载货物。Shift 排队是玩家的显式追加指令, 不走这条去重。
            if(!queued&&worker.Current is PlayerHarvestOrder current
                &&(mine!=null?ReferenceEquals(current.Mine,mine)
                    :ReferenceEquals(current.Tree,tree)))
            {
                continue;
            }
            var order=new PlayerHarvestOrder(this,mine,tree,movementGroup)
            {
                ReturnCargoOnStart=IsReturningHarvest(worker),
                JassOrderId=HarvestOrderId,
                EventTargetKind=mine==null
                    ?EventTargetKind.Destructable:EventTargetKind.Unit,
            };
            if(queued)worker.Issue(order,true);else worker.ForceIssue(order);
        }
        // Keeping an existing harvest order is still a successful command.
        return accepted.Length;
    }

    public static bool IsHarvestAbility(Ability ability)
    {
        if(ability==null)return false;
        // Native harvest dispatch belongs to the ability class. Custom Order
        // text must not hide an already specialized Aaha/Awha instance.
        if(ability is RuntimeHarvestAbility)return true;
        string order=ability.OrderId??"";
        if(order.Equals("harvest",StringComparison.OrdinalIgnoreCase)
            ||order.Equals("autoharvestgold",StringComparison.OrdinalIgnoreCase)
            ||order.Equals("autoharvestlumber",StringComparison.OrdinalIgnoreCase)
            ||order.Equals("resumeharvesting",StringComparison.OrdinalIgnoreCase)
            ||order.Equals("wispharvest",StringComparison.OrdinalIgnoreCase)
            ||order.Equals("acolyteharvest",StringComparison.OrdinalIgnoreCase))return true;
        string baseId=AbilityBase(ability);
        return baseId.Equals("Ahar",StringComparison.OrdinalIgnoreCase)
            ||baseId.Equals("Ahrl",StringComparison.OrdinalIgnoreCase)
            ||baseId.Equals("Aaha",StringComparison.OrdinalIgnoreCase)
            ||baseId.Equals("Awha",StringComparison.OrdinalIgnoreCase);
    }

    bool CanHarvest(Unit worker)=>worker?.Alive==true&&!worker.Removed
        &&worker.CanMove&&HarvestAbility(worker)!=null;

    // 能力类分发 (原生语义): uaco=Aaha/AcolyteHarvest, ewsp=Awha/WispHarvest,
    // ugho=Ahrl/HarvestLumber; 矿类 = 矿产能能力 Abgm(闹鬼)/Aegm(缠绕)/Agld。
    internal static string AbilityBase(Ability ability)
        // Standard aliases Ahr2/Ahr3 (and map-derived abilities) retain their
        // object ID in BaseId. Native harvest behavior follows the class code.
        =>RuntimeAbilityFactory.ResolveBaseCode(ability?.ObjectData);

    internal static bool HasAcolyteHarvest(Unit worker)=>worker?.Abilities.Items
        .Any(ability=>AbilityBase(ability).Equals("Aaha",
            StringComparison.OrdinalIgnoreCase))==true;

    internal static bool HasWispHarvest(Unit worker)=>worker?.Abilities.Items
        .Any(ability=>AbilityBase(ability).Equals("Awha",
            StringComparison.OrdinalIgnoreCase))==true;

    internal static bool HasGhoulLumber(Unit worker)=>worker?.Abilities.Items
        .Any(ability=>AbilityBase(ability).Equals("Ahrl",
            StringComparison.OrdinalIgnoreCase))==true;

    internal static bool IsHauntedMine(Unit mine)=>GoldMineIdentity.Kind(mine)==GoldMineKind.Haunted;

    internal static bool IsEntangledMine(Unit mine)=>GoldMineIdentity.Kind(mine)==GoldMineKind.Entangled;

    // OrdinalIgnoreCase 直比, 避免 ToLowerInvariant 每矿每能力分配
    static Ability GoldMineAbilityOf(Unit mine)=>GoldMineIdentity.ResourceAbility(mine);

    // 1e47b0 -> 668f00: query the resource ability payload, including depleted
    // values; visibility and unit life are not query gates. The host represents
    // the native resource identity bit through its gold-mine ability family.
    static int ScriptResourceAmount(Unit unit)=>unit!=null&&!unit.Removed
        &&GoldMineAbilityOf(unit)!=null?unit.ResourceAmount:0;

    bool CanHarvestGold(Unit worker,Unit mine,bool checkOwnership=true)
    {
        if(!CanHarvest(worker)||mine==null)return false;
        if(HasAcolyteHarvest(worker))
            return IsHauntedMine(mine)&&mine.PlayerId==worker.PlayerId;
        if(HasWispHarvest(worker))
            return IsEntangledMine(mine)&&mine.PlayerId==worker.PlayerId;
        if(HasGhoulLumber(worker))return false;
        return !IsHauntedMine(mine)&&!IsEntangledMine(mine)
            &&(!checkOwnership||mine.GetAbility<HarvestMineClock>() is not {} clock
                ||clock.CanEnter(worker.PlayerId,HarvestNow(mine)));
    }

    bool CanHarvestLumber(Unit worker)=>CanHarvest(worker)
        &&!HasAcolyteHarvest(worker);

    static bool IsAvailableGoldMine(Unit mine)=>mine?.Alive==true&&!mine.Removed&&!mine.Hidden
        &&mine.ResourceAmount>0
        &&mine.Abilities.Get<ConstructionProgress>()==null;

    static bool IsGoldMineUnit(Unit unit)=>GoldMineIdentity.IsMine(unit);

    static bool CarriesVisibleHarvestBundle(Unit worker)=>
        HasGhoulLumber(worker)
        ||worker?.Abilities.Items.Any(ability=>AbilityBase(ability)
            .Equals("Ahar",StringComparison.OrdinalIgnoreCase))==true;

    DestructableRef FindHarvestTree(Vec2 point,float radius)
    {
        radius=Math.Max(0,radius);if(float.IsNaN(radius))return null;
        _treeQueryScratch.Clear();
        if(float.IsPositiveInfinity(radius))
            foreach(var bucket in _treeMemberCells.Values)_treeQueryScratch.AddRange(bucket);
        else CollectNearbyTrees(point,radius,_treeQueryScratch);
        DestructableRef best=null;float distance=float.PositiveInfinity;long ordinal=long.MaxValue;
        foreach(var tree in _treeQueryScratch)
        {
            if(!IsLiveHarvestTree(tree))continue;
            float candidateDistance=Vec2.Dist(tree.Position,point);if(!(candidateDistance<=radius))continue;
            long candidateOrdinal=_registeredTreeMembers[tree].Order;
            if(best!=null&&(candidateDistance>distance||candidateDistance==distance&&candidateOrdinal>=ordinal))continue;
            best=tree;distance=candidateDistance;ordinal=candidateOrdinal;
        }
        return best;
    }

    // 树类型判定按物编类型缓存: 原实现每树每次都查物编表+两次字符串比较,
    // 是找树扫描的隐藏大头。类型集合运行期不变, 缓存命中后 O(1)。
    readonly Dictionary<string,bool> _harvestTreeTypeCache=new();
    bool IsHarvestTreeType(string typeId)
    {
        if(typeId==null)return false;
        if(_harvestTreeTypeCache.TryGetValue(typeId,out bool cached))return cached;
        DestructableTypeData data=_objects?.GetDestructable(typeId);
        string target=data?.GetString("targType","")??"";
        string category=data?.GetString("category","")??"";
        bool isTree=target.Contains("tree",StringComparison.OrdinalIgnoreCase)
            ||category.Equals("T",StringComparison.OrdinalIgnoreCase);
        _harvestTreeTypeCache[typeId]=isTree;
        return isTree;
    }

    bool IsLiveHarvestTree(DestructableRef tree)
    {
        if(tree==null||tree.Removed||tree.Invulnerable||tree.Life<=0)return false;
        return IsHarvestTreeType(Rawcode(tree.TypeId));
    }

    int TakeLumber(Unit worker,DestructableRef tree,int requested)
    {
        if(!IsLiveHarvestTree(tree)||requested<=0)return 0;
        // 6F3E7A40: native GetUnitRace == 3 corrupts the harvested tree even
        // away from blighted terrain (the ordinary ghoul lumber path).
        if(LookupUnitTypeRace(worker)==3)tree.Blighted=true;
        // 3e79d0 damages the tree, then credits configured +1d8 independently
        // of remaining tree life. Only the worker's cargo capacity clamps credit.
        if(tree.Life<=requested)KillDestructable(_vm,tree);
        else
        {
            tree.Life-=requested;
            // 0xd01b1 requests the tree's own clip (CDestructable vtable+0x120)
            // alongside the wood deduction; without it a chopped tree never reacts.
            RequestDestructableHit(tree);
        }
        return requested;
    }

    void DepositHarvest(Unit worker,bool gold,int amount,float feedbackHeightOffset=0)
    {
        if(worker==null||amount<=0)return;
        PlayerRef player=Player(worker.PlayerId);
        int creditedAmount=ApplyMeleeAiResourceCredit(amount,
            player.Controller==1,player.AiDifficulty);
        int grossAmount=creditedAmount;
        // 维修费用: 只扣金币, 木头不扣. 税率从地图平衡常数读取.
        if(gold)
        {
            var cfg=_world?.GameplayConfig??War3GameplayConfig.Default;
            int foodUsed=CurrentFoodUsed(player.Id);
            float factor=cfg.GoldIncomeFactor(foodUsed);
            creditedAmount=(int)MathF.Floor(creditedAmount*factor);
        }
        int stateId=gold?1:2;
        player.States[stateId]=SaturatingAdd(player.States.GetValueOrDefault(stateId),creditedAmount);
        RecordHarvestScore(player,gold,grossAmount,creditedAmount);
        if(_meleeAiRuntimes.ContainsKey(player.Id))
        {
            if(gold)player.MeleeAiHarvestedGold=SaturatingAdd(
                player.MeleeAiHarvestedGold,creditedAmount);
            else player.MeleeAiHarvestedLumber=SaturatingAdd(
                player.MeleeAiHarvestedLumber,creditedAmount);
        }
        FirePlayerStateEvents(player,stateId);
        EventContext stateChanged=EventContext.Rent();
        stateChanged.Event=GameEvent.PlayerStateChanged;
        stateChanged.TriggerPlayerId=player.Id;stateChanged.PlayerStateId=stateId;
        _world.Events.Fire(stateChanged);EventContext.Recycle(stateChanged);
        if(creditedAmount>0&&CanPlayerControl(LocalPlayerId,worker,advanced:true))
            FloatingText.ShowResource(worker,creditedAmount,lumber:!gold,feedbackHeightOffset);
    }

    // Warcraft III's Insane computer receives twice the returned resource,
    // while the mine/tree still loses only the worker's normal gathered amount.
    // This is a lobby AI difficulty rule, not an AMAI strategy-side bonus.
    public static int ApplyMeleeAiResourceCredit(int amount,bool computer,
        int aiDifficulty)
        =>computer&&aiDifficulty>=2?SaturatingAdd(amount,amount):amount;

    // FUN_6f3f9000: 以资源目标为中心找最近接受回收的建筑。
    // 原生无路由可达性预检、无缓存 (搜索谓词即目标校验+距离序)。
    Unit FindHarvestDropOff(Unit worker,bool gold,Vec2 center,Unit excluded=null)
    {
        Unit best=null;
        float bestDistance=float.PositiveInfinity;
        foreach(Unit unit in _world.BuildingUnitsForPlayer(worker.PlayerId))
        {
            if(ReferenceEquals(unit,excluded)||unit.PlayerId!=worker.PlayerId
                ||!AcceptsHarvestReturn(unit,gold))continue;
            float distance=Vec2.Dist(unit.Pos,center);
            if(distance<bestDistance)
            {
                best=unit;
                bestDistance=distance;
            }
        }
        return best;
    }

    // The navigation fringe retains the worker's footprint scale.  Return
    // completion is checked separately by FUN_6f03e3a0 / FUN_6f03bad0 below.
    internal static float ReturnReach(Unit worker)=>worker.CollisionSize;

    internal Unit FindHarvestDropOffForReview(Unit worker,bool gold,Vec2 center)
        =>FindHarvestDropOff(worker,gold,center);

    internal Unit FindHarvestRetryDropOff(Unit worker,bool gold,Unit previous)
        =>FindHarvestDropOff(worker,gold,worker.Pos,previous)
            ??FindHarvestDropOff(worker,gold,worker.Pos);

    internal static bool AcceptsHarvestReturn(Unit unit,bool gold)
    {
        if(unit?.Alive!=true||unit.Removed||!unit.IsBuilding
            ||unit.Abilities.Get<ConstructionProgress>()!=null)return false;
        var abilities=unit.Abilities.Items;
        for(int i=0;i<abilities.Count;i++)
        {
            Ability ability=abilities[i];
            // Argl / Argd / Arlm (and custom copies) share code=Artn,
            // but retain their own BaseId. Test the implementation, not the alias.
            string code=RuntimeAbilityFactory.ResolveBaseCode(ability.ObjectData);
            if(ability is not RuntimeReturnResourceAbility
                &&!code.Equals("Artn",StringComparison.OrdinalIgnoreCase))continue;
            return ability.GetLevelInt(gold?"DataA":"DataB",fallback:0)!=0;
        }
        // Flattened/custom object data can omit the hidden Artn instance. Keep the
        // classic melee rawcodes as a compatibility fallback, never every building.
        return CanDropOff(unit.TypeId,gold);
    }

    static bool CanDropOff(string rawcode,bool gold)
    {
        rawcode=(rawcode??"").ToLowerInvariant();
        if(rawcode is "htow" or "hkee" or "hcas" or "ogre" or "ostr" or "ofrt"
            or "unpl" or "unp1" or "unp2" or "etol" or "etoa" or "etoe")return true;
        // 原版木材回收: 人族伐木场(hlum)/兽族磨坊(owar)/不死墓地(ugrv)
        return !gold&&rawcode is "hlum" or "owar" or "ugrv";
    }

    Ability HarvestAbility(Unit worker)
    {
        var items=worker?.Abilities.Items;
        for(int i=0;items!=null&&i<items.Count;i++)
        {
            Ability ability=items[i];
            if(IsHarvestAbility(ability)&&ability.Level>0&&!ability.IsDisabled)return ability;
        }
        return null;
    }

    static int HarvestValue(Ability ability,string field,int fallback)
        =>Math.Max(1,ability?.GetLevelInt(field,fallback:fallback)??fallback);

    static float HarvestDuration(Ability ability,float fallback)
        =>Math.Max(World.TickSeconds,ability?.GetLevelFloat("Dur",fallback:fallback)??fallback);

    static readonly string[] UpgradeEffectFields={"effect1","effect2","effect3","effect4"};

    int LumberCapacity(Unit worker,Ability ability)
    {
        // 原生装载 (FUN_6f3fdf00 共用基础装载器): DataA→+0x1d8 每刀量,
        // DataB→+0x198 容量; FUN_6f3fde40 (Ahar) 另读 DataC→+0x194 金量。
        int baseCapacity=HarvestValue(ability,"DataB",10);
        UnitTypeData unitData=_objects?.Get(worker?.TypeId);
        PlayerRef player=worker==null?null:Player(worker.PlayerId);
        float bonus=0;
        if(unitData!=null&&player!=null)
        foreach(string rawcode in SplitRawcodes(unitData.GetString("upgrades","")))
        {
            UpgradeTypeData upgrade=_objects.GetUpgrade(rawcode);
            int level=player.Tech.GetValueOrDefault(FourCC(rawcode));
            if(upgrade==null||level<=0)continue;
            for(int index=1;index<=4;index++)
                if(upgrade.GetString(UpgradeEffectFields[index-1],"").Equals("rlum",
                    StringComparison.OrdinalIgnoreCase))
                    bonus+=UpgradeEffectValue(upgrade,index,level);
        }
        return Math.Max(1,baseCapacity+(int)MathF.Round(bonus));
    }

    Ability GoldMineAbility(Unit mine)=>GoldMineAbilityOf(mine);

    int GoldMineCapacity(Unit mine)
    {
        Ability production=GoldMineAbility(mine);
        int capacity=production?.GetLevelInt("DataC",fallback:0)??0;
        if(capacity<=0)
            capacity=mine?.Abilities.Items.FirstOrDefault(ability=>
                    (ability.ObjectData?.BaseId??"").Equals("Aenc",
                        StringComparison.OrdinalIgnoreCase))
                ?.GetLevelInt("DataA",fallback:0)??0;
        return Math.Clamp(capacity>0?capacity:IsHauntedMine(mine)||IsEntangledMine(mine)?5:1,
            1,IsHauntedMine(mine)?8:32);
    }

    // 5c9c60: Agld.DataB is slot service time; 4bcb40: Abgm/Aegm.DataB
    // is the full-capacity shared period. Aenc supplies capacity, not duration.
    float GoldMineDuration(Unit mine)
    {
        float duration=GoldMineAbility(mine)?.GetLevelFloat("DataB",fallback:1f)??1f;
        return duration>0?Math.Max(0.01f,duration):1f;
    }

    int GoldMineAmount(Unit mine)
        // Abgm/Aegm.DataA is one shared batch; Agld.DataA is storage and
        // ordinary extraction instead uses the worker's Ahar.DataC.
        =>Math.Max(1,GoldMineAbility(mine)?.GetLevelInt("DataA",fallback:10)??10);

    int GoldMineAmountPerChannel(Unit mine)=>GoldMineAmount(mine);

    public float GoldMineChannelRadius(Unit mine)
        // 物编 Aenc DataD 为空(见上), 保留 200。
        =>Math.Max(64f,GoldMineAbility(mine)?.GetLevelFloat("DataD",fallback:200f)??200f);

    void FinishGoldExtraction(Unit mine,Unit extractingWorker)
    {
        if(mine?.Alive!=true||mine.Removed||mine.ResourceAmount>0
            ||mine.GetAbility<RuntimeGoldMineDepletion>()!=null)return;
        // Release every remaining mine slot now, including workers whose world
        // tick preceded the last extractor. The final carrier already exits and
        // enters ToHome before this call, so its last load survives the death.
        foreach(Unit worker in _world.Units.ToArray())
            if(worker!=extractingWorker&&worker.Current is PlayerHarvestOrder order
                &&ReferenceEquals(order.Mine,mine)&&!order.IsReturning)
                worker.CancelOrders();
        if(IsHauntedMine(mine))
        {
            mine.AddAbility(new RuntimeGoldMineDepletion(_objects,mine,(gold,lumber)=>
            {
                PlayerRef player=Player(mine.PlayerId);
                player.States[1]=SaturatingAdd(player.States.GetValueOrDefault(1),gold);
                player.States[2]=SaturatingAdd(player.States.GetValueOrDefault(2),lumber);
            }));
        }
        else mine.Death.Die(null,allowBounty:false);
    }

    DestructableRef FindNearestHarvestTree(Unit worker,Vec2 clusterPoint,
        HashSet<DestructableRef> excluded)
        =>FindNearestHarvestTree(worker,clusterPoint,null,excluded);

    internal DestructableRef FindNearestHarvestTree(Unit worker,Vec2 clusterPoint,
        DestructableRef except=null)
        =>FindNearestHarvestTree(worker,clusterPoint,except,null);

    // FUN_6f3f7c60 uses the configured resource search radius.
    // 41c6c0 rejects the eight timed records and failed path queries, and
    // ranks remaining candidates by native leaf distance, starting at budget 32.
    // 小精灵占树 (一树一精灵) 是可观察的原生行为, 保留专属过滤。
    readonly List<DestructableRef> _treeQueryScratch=new(64);
    readonly Dictionary<DestructableRef,PlayerHarvestOrder> _mountedWispHarvestTrees=new();

    // 89bbf0/89e5c0 pass &bf18b4 to 3f7c60. Initializer 019500
    // writes 1200; this AI query is independent of the ability's Area/Rng.
    DestructableRef FindNearestAiHarvestTree(Unit worker,Vec2 clusterPoint)
        =>FindNearestHarvestTree(worker,clusterPoint,null,null,1200f);

    DestructableRef FindNearestHarvestTree(Unit worker,Vec2 clusterPoint,
        DestructableRef except,HashSet<DestructableRef> excluded,float? radiusOverride=null)
    {
        bool wisp=HasWispHarvest(worker);
        Ability harvest=HarvestAbility(worker);
        float searchRadius=wisp
            ?harvest?.GetLevelFloat("Rng",fallback:900f)??900f
            :harvest?.ConfiguredAreaOfEffect??0f;
        // Awha uses Rng (900), not its empty Area column.
        if(searchRadius<=0f)searchRadius=900f;
        if(radiusOverride.HasValue)searchRadius=radiusOverride.Value;
        DestructableRef best=null;
        uint bestDistance=uint.MaxValue;
        int budget=32;
        var finder=worker.World?.CLrPathingAcc;
        double now=HarvestNow(worker);
        _treeQueryScratch.Clear();
        CollectNearbyTrees(clusterPoint,searchRadius,_treeQueryScratch);
        for(int i=0;i<_treeQueryScratch.Count;i++)
        {
            DestructableRef tree=_treeQueryScratch[i];
            if(ReferenceEquals(tree,except)
                ||(excluded!=null&&excluded.Contains(tree))
                ||wisp&&_mountedWispHarvestTrees.ContainsKey(tree)
                ||harvest is RuntimeHarvestAbility runtime&&runtime.RejectsTree(tree,now)
                ||!IsLiveHarvestTree(tree))
            {
                continue;
            }
            float distance=Vec2.Dist(tree.Position,clusterPoint);
            if(distance>searchRadius)continue;
            Vec2 goal=tree.Position;
            // 41c6c0 ranks the actual widget position; 03c3b0 temporarily
            // clears its accumulator channels instead of preselecting a side.
            uint score=finder==null?(uint)MathF.Floor(Vec2.Dist(worker.Pos,goal)/32f)
                :finder.MeasureWidgetDistance(worker.Pos,goal,worker.MovementType,
                    worker.CollisionSize,worker,budget,tree.PathingOverlayHandle,out _);
            if(score<bestDistance)
            {
                best=tree;
                bestDistance=score;
                if(finder!=null)budget=Math.Min(budget,finder.LastDistanceExpandedNodes+16);
            }
        }
        return best;
    }

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
            :base(owner)
        {
            _mine=mine;
            _tree=tree;
            _movementGroup=movementGroup;
        }

        bool _returnGold;
        public bool ReturnOnly { get; init; }
        public bool ResumeAfterReturn { get; init; }
        public Unit ReturnReceiver { get; init; }
        public bool Gold=>ReturnOnly?_returnGold:_mine!=null;
        public bool ReturnCargoOnStart { get; init; }

        // 原版采集单位的当前订单: 资源段=harvest(852018), 满载返程=
        // returnresources(852020)。AMAI 用 GetUnitCurrentOrder 判定采集状态。
        // 返程报告 resumeharvesting(852017), 采集自动循环的回家腿即"恢复采集"
        // 中途态; common.ai 依赖它保护返程不被 Correcting Harvest 打断/计数。
        public int CurrentJassOrderId=>ReturnOnly?(ResumeAfterReturn?852017:852020):_phase==Phase.ToHome?852017:852018;
        Vec2 ResourcePoint=>_mine?.Pos??_tree?.Position??default;

        public override void OnStart(Unit worker)
        {
            _worker=worker;
            if(!ReturnOnly&&Owner.HarvestAbility(worker) is RuntimeHarvestAbility harvest)
            {
                harvest.RememberedMine=_mine;
                harvest.RememberedTreeCreationNumber=_tree?.CreationNumber??-1;
                if(_tree!=null)harvest.RememberedTreePosition=_tree.Position;
            }
            if(ReturnOnly)_returnGold=worker.HarvestCargoGold;
            _acolyteChannel=Gold&&HasAcolyteHarvest(worker)
                &&IsHauntedMine(_mine);
            _wispLumberChannel=!Gold&&HasWispHarvest(worker);
            _wispGoldChannel=Gold&&HasWispHarvest(worker)
                &&IsEntangledMine(_mine);
            // 金矿通道(侍僧/小精灵)关闭单位碰撞; 伐木恢复开启碰撞——
            // 实测关闭后共树工人直接永久重叠, 轻推也不散开。
            SetHarvestCollision(worker,Gold);
            // 同类型满载工人被重下采集单时直接进返程交付, 不再空走回资源点折返;
            // 类型不符仍走资源点 (入账时混载清零, FUN_6f3ddd60 语义)。
            if(worker.HarvestCargo>0&&worker.HarvestCargoGold==Gold
                &&(Gold||ReturnCargoOnStart
                    ||worker.HarvestCargo>=Owner.LumberCapacity(worker,Owner.HarvestAbility(worker)))
                &&!_wispLumberChannel&&!_wispGoldChannel)
            {
                _home=ReturnReceiver??SelectReturnHome(worker,worker.HarvestCargoGold,worker.Pos);
                if(_home!=null)
                {
                    _phase=Phase.ToHome;
                    BeginMove(worker,_home.Pos,ReturnReach(worker),_home);
                    return;
                }
            }
            BeginMove(worker,ResourcePoint,
                Gold?HarvestReach(worker):LumberReach(worker),
                Gold?_mine:null,Gold?null:_tree);
        }

        int _phaseEnteredTick;

        public override COrder Execute(Unit worker,float dt)
        {
            SimPhaseProbe.Begin("harvest.execute");
            try
            {
            return ExecuteCore(worker,dt);
            }
            finally{SimPhaseProbe.End("harvest.execute");}
        }

        COrder ExecuteCore(Unit worker,float dt)
        {
            if(Owner.HarvestAbility(worker)==null)return null;
            if(!ReturnOnly&&Gold&&_phase!=Phase.ToHome
                &&!Owner.CanHarvestGold(worker,_mine,checkOwnership:_phase!=Phase.MiningInside))return null;
            // A final lumber hit may kill the tree.  The worker must still carry
            // that batch home; only resource-facing phases require a live node.
            if(_phase!=Phase.ToHome&&!ResourceAvailable()
                &&!(!Gold&&!_wispLumberChannel&&_phase==Phase.Harvesting
                    &&_lumberStage==LumberStage.Backswing))
            {
                if(Gold||!RetargetLumber(worker))
                return null;
                return this;
            }
            switch(_phase)
            {
                case Phase.RetryLumber:
                    // 41a090 registers the rejected tree before d0167(500).
                    // 41a260 converts that task parameter to 0.5 seconds; its
                    // refresh then re-enumerates trees. This is a failure wait,
                    // not a timeout inferred from a stationary worker.
                    if(HarvestNow(worker)<_lumberRetryDue)return this;
                    return RetargetLumber(worker,rejectExisting:false)?this:null;
                case Phase.ToResource:
                    if(!AdvanceMove(worker,dt))
                        return this;
                    if(_moveFailed)
                    {
                        EndMove(worker,false);
                        // FUN_6f41a3a0: 金矿到位失败原生重试上限 10 次
                        // (0xd0171 重挂 + 0xd0167 500 超时等待), 耗尽掉单。
                        if(Gold&&++_arrivalRetries<=10)
                        {
                            BeginMove(worker,ResourcePoint,
                                HarvestReach(worker),_mine,null);
                            return this;
                        }
                        if(!Gold&&!_wispLumberChannel)
                        {
                            if(Owner.HarvestAbility(worker) is RuntimeHarvestAbility rejected)
                                rejected.RejectTree(_tree,HarvestNow(worker));
                            _lumberRetryDue=HarvestNow(worker)+.5;
                            _phase=Phase.RetryLumber;
                            return this;
                        }
                        if(!Gold&&RetargetLumber(worker))return this;
                        return null;
                    }
                    EndMove(worker,false);
                    if(Gold)
                    {
                        if(_acolyteChannel)
                        {
                            _phase=Phase.WaitingForMine;
                            TryBeginAcolyteChannelMove(worker);
                            return this;
                        }
                        _phase=Phase.WaitingForMine;
                        return this;
                    }
                    // FUN_6f41a090: 到树时已满载 → 直接转返程, 不再开砍。
                    Ability earlyHarvest=Owner.HarvestAbility(worker);
                    if(earlyHarvest!=null&&worker.HarvestCargo>=Owner.LumberCapacity(
                            worker,earlyHarvest))
                    {
                        _home=SelectReturnHome(worker,false,ResourcePoint);
                        // 满载到树却选不到接收厅: 同 ToHome 复检点保留订单原地等,
                        // 厅造好下一 tick 复检续走。保留订单期间按 2026-10-19 用户
                        // 口径仍是"正在去收集点"(在忙); 真掉单也只是"抬着资源"(算空闲),
                        // 派单侧会触发自动返回而不是派矿/伐木(见 DispatchIdleWorkers 分流)。
                        if(_home==null)
                        {
                            _phase=Phase.ToHome;
                            return this;
                        }
                        _phase=Phase.ToHome;
                        BeginMove(worker,_home.Pos,ReturnReach(worker),_home);
                        return this;
                    }
                    if(_wispLumberChannel)
                    {
                        if(TreeClaimedByOtherWisp(worker))
                        {
                            if(RetargetLumber(worker))return this;
                            return null;
                        }
                        MountWispOnTree(worker);
                    }
                    _phase=_wispLumberChannel?Phase.Harvesting:Phase.FacingResource;
                    _harvestRemaining=HarvestDuration(
                        Owner.HarvestAbility(worker),1f);
                    return this;
                case Phase.WaitingForMine:
                    if(_acolyteChannel)
                    {
                        TryBeginAcolyteChannelMove(worker);
                        return this;
                    }
                    _mineClock=Owner.MineClock(_mine);
                    if(_wispGoldChannel)
                    {
                        if(!_mineClock.JoinChannel(this))return this;
                        EnterMine(worker);
                        _phase=Phase.Harvesting;
                    }
                    else _mineClock.Enqueue(this);
                    return this;
                case Phase.MiningInside:
                    _mineClock.Advance();
                    if(!ReferenceEquals(worker.Current,this))return null;
                    return this;
                case Phase.FacingResource:
                    // CBehaviorHarvest finishes contact movement first, then uses
                    // the worker's normal turn rate to enter the lumber work state.
                    // Keeping this separate from Harvesting prevents the Attack
                    // animation and chop timer from starting while facing away.
                    if(!worker.TurnToward(_tree.Position,dt))return this;
                    _phase=Phase.Harvesting;
                    BeginLumberChop(worker,HarvestNow(worker));
                    return this;
                case Phase.Harvesting:
                    if(Gold)
                    {
                        _mineClock.Advance();
                        return ResourceAvailable()?this:null;
                    }
                    // A scripted displacement can change the required bearing while
                    // the tree remains selected. Never advance a chop while the
                    // worker is outside its configured facing window.
                    if(!Gold&&!_wispLumberChannel
                        &&!worker.TurnToward(_tree.Position,dt))return this;
                    Ability harvest=Owner.HarvestAbility(worker);
                    if(_wispLumberChannel)
                    {
                        _harvestRemaining-=dt;
                        if(_harvestRemaining>0)return this;
                        Owner.DepositHarvest(worker,false,
                            HarvestValue(harvest,"DataA",5),feedbackHeightOffset:100f);
                        _harvestRemaining=HarvestDuration(harvest,8f);
                        return this;
                    }
                    return AdvanceLumberChop(worker,harvest);
                case Phase.ToHome:
                    // Native 41b140 rechecks 3e0f80 before completing delivery;
                    // stale receivers must not accept cargo after an owner or
                    // receiver-type change. Keep the cargo when no receiver exists.
                    if(_home?.PlayerId!=worker.PlayerId
                        ||!AcceptsHarvestReturn(_home,worker.HarvestCargoGold))
                    {
                        EndMove(worker,true);
                        _home=SelectReturnHome(worker,worker.HarvestCargoGold,worker.Pos);
                        _escapeActive=false;
                        _escapeLegsUsed=0;
                        _homeRetries=0;
                        ResetEscapeCandidates();
                        // 无接收厅(名下大厅全在建造/刚被拆)时保留订单与货物原地等,
                        // 对标原版 41b140 "Keep the cargo when no receiver exists":
                        // 厅造好下一 tick 的复检就会重新选中并续走返程。保留订单期间
                        // 按 2026-10-19 用户口径仍是"正在去收集点"(在忙); 若掉单成
                        // "无当前订单", 虽按同口径只是"抬着资源"(算空闲), 派单侧也会
                        // 触发自动返回而不是派矿/伐木(见 DispatchIdleWorkers 分流)。
                        if(_home==null)return this;
                        BeginMove(worker,_home.Pos,ReturnReach(worker),_home);
                        return this;
                    }
                    // 原生无逐 tick 阻塞计数掉单: 返程受阻由移动栈终到链
                    // (GivenUp/重定位) 或 0xd0167 500 超时的订单刷新收尾。
                    // 对标 0xd0167(500 tick)+FUN_6f3f9000: 交付超时先换最近
                    // 交付厅重试, 换厅仍失败才掉单——防止厅边被围死时
                    // 工人无限绕圈 (玩家"送不到"反馈的兜底)。
                    if(worker.World.Tick-_phaseEnteredTick>=500)
                    {
                        // 500t 内有净接近只顺延计时, 不中途换厅 (交付在即时会被拉回)。
                        if(_home!=null
                            &&Vec2.Dist(worker.Pos,_home.Pos)<=_phaseStartHomeDist-128f)
                        {
                            _phaseEnteredTick=worker.World.Tick;
                            _phaseStartHomeDist=Vec2.Dist(worker.Pos,_home.Pos);
                            return this;
                        }
                        // An oscillating local route need not report GivenUp.
                        // Give the same bounded detour used by move failures a
                        // chance before switching receivers.
                        if(!_escapeActive&&_home!=null&&_escapeLegsUsed<EscapeLegLimit
                            &&TryPlanEscapeLeg(worker,out Vec2 timeoutLeg))
                        {
                            ++_escapeLegsUsed;
                            _escapeActive=true;
                            _phaseEnteredTick=worker.World.Tick;
                            EndMove(worker,false);
                            BeginMove(worker,timeoutLeg,64f,null);
                            return this;
                        }
                        if(++_homeRetries>3)
                        {
                            return null;
                        }
                        EndMove(worker,false);
                        // Retrying the same nearest blocked hall forever is
                        // not a fallback. Prefer another valid receiver first.
                        _home=Owner.FindHarvestRetryDropOff(worker,
                            worker.HarvestCargoGold,_home);
                        if(_home==null)return null;
                        _phaseEnteredTick=worker.World.Tick;
                        _escapeActive=false;
                        ResetEscapeCandidates();
                        BeginMove(worker,_home.Pos,ReturnReach(worker),_home);
                        return this;
                    }
                    if(!AdvanceMove(worker,dt))
                        return this;
                    if(_escapeActive&&!_moveFailed)
                    {
                        // Reaching a waypoint is not necessarily progress toward
                        // home. Keep the trip's retry budget and progress deadline.
                        _escapeActive=false;
                        if(_home==null||!_home.Alive||_home.Removed)
                            // 掉单后农民带货站立: 按 2026-10-19 用户口径"仅仅是抬着
                            // 资源"不算在忙, 空闲农民条(IdlePeonFrame.IsStillIdle)会收它;
                            // 但「空闲农民干活」派单仍对它触发一次自动返回而不是派矿/伐木
                            // (DispatchIdleWorkers 的订单×载货分流, 2026-10-17), 免得派单把货清掉。
                            return null;
                        EndMove(worker,false);
                        if(_escapeCandidateIdx<_escapeCandidates.Count)
                        {
                            // Finish the finite detour before retrying the direct
                            // approach, which can lead straight back into the pocket.
                            _escapeActive=true;
                            BeginMove(worker,_escapeCandidates[_escapeCandidateIdx++],64f,null);
                            return this;
                        }
                        ResetEscapeCandidates();
                        BeginMove(worker,_home.Pos,ReturnReach(worker),_home);
                        return this;
                    }
                    if(_moveFailed)
                    {
                        // 二级: 分段逃逸——深探针规划绕行路线, 裸点腿逐段出袋。
                        if(_home!=null&&_escapeLegsUsed<EscapeLegLimit
                            &&TryPlanEscapeLeg(worker,out Vec2 leg))
                        {
                            ++_escapeLegsUsed;
                            _escapeActive=true;
                            EndMove(worker,false);
                            BeginMove(worker,leg,64f,null);
                            return this;
                        }
                        // 三级: 换厅 (与 500t retimeout 共用 _homeRetries 预算),
                        // 无替代厅或预算耗尽才掉单 (重下后 OnStart 满载直送)。
                        Unit retry=_homeRetries<3
                            ?Owner.FindHarvestRetryDropOff(worker,worker.HarvestCargoGold,_home)
                            :null;
                        if(retry!=null&&!ReferenceEquals(retry,_home))
                        {
                            ++_homeRetries;
                            EndMove(worker,false);
                            _home=retry;
                            _escapeActive=false;
                            ResetEscapeCandidates();
                            BeginMove(worker,_home.Pos,ReturnReach(worker),_home);
                            return this;
                        }
                        // 厅存在但不可达时掉单是既定防兜圈设计 (见上方 500t 注释):
                        // 改派返程会每 ~25-30s 重试同一不可达厅。带货站立按 2026-10-19
                        // 口径算空闲(仅仅是抬着资源), 空闲农民条会显示它; 但派单侧
                        // (DispatchIdleWorkers) 对它触发自动返回, 不会被派去矿/伐木。
                        return null;
                    }
                    EndMove(worker,false);
                    _homeRetries=0;
                    _returnHome=null;
                    _escapeActive=false;
                    _escapeLegsUsed=0;
                    ResetEscapeCandidates();
                    // FUN_6f3ddd60: 交付类型按货物自身 (非订单类型)。
                    Owner.DepositHarvest(worker,worker.HarvestCargoGold,
                        worker.HarvestCargo);
                    worker.HarvestCargo=0;
                    if(ReturnOnly)return ResumeAfterReturn?Owner.CreateResumeHarvestOrder(worker):null;
                    // Lumber workers only ignore unit collision while carrying
                    // resources home. Restore normal collision before starting
                    // the next trip to the trees; otherwise every later cycle
                    // leaves them permanently non-colliding.
                    SetHarvestCollision(worker,Gold);
                    if(!ResourceAvailable()&&!Gold)
                    {
                        DestructableRef exhausted=_tree;
                        _tree=Owner.FindNearestHarvestTree(worker,worker.Pos,exhausted);
                        if(_tree==null&&exhausted!=null)
                            _tree=Owner.FindNearestHarvestTree(worker,exhausted.Position,exhausted);
                        if(_tree!=null&&Owner.HarvestAbility(worker) is RuntimeHarvestAbility remembered)
                        {
                            remembered.RememberedTreeCreationNumber=_tree.CreationNumber;
                            remembered.RememberedTreePosition=_tree.Position;
                        }
                    }
                    if(!ResourceAvailable())return null;
                    _phase=Phase.ToResource;
                    BeginMove(worker,ResourcePoint,
                        Gold?HarvestReach(worker):LumberReach(worker),
                        Gold?_mine:null,Gold?null:_tree);
                    return this;
                default:return null;
            }
        }

        int TakeGold(Unit worker)
        {
            if(_mine?.ResourceAmount<=0)return 0;
            // FUN_6f3ddd60: 金木不可混载——切入金前清掉木材。
            if(worker.HarvestCargo>0&&!worker.HarvestCargoGold)
                worker.HarvestCargo=0;
            worker.HarvestCargoGold=true;
            // DataC = 金量 (原生 FUN_6f3fde40: DataC→+0x194)。
            int amount=Math.Min(HarvestValue(
                Owner.HarvestAbility(worker),"DataC",10),_mine.ResourceAmount);
            _mine.ResourceAmount-=amount;
            return amount;
        }

        internal void StartMineService()
        {
            EnterMine(_worker);
            _phase=Phase.MiningInside;
        }

        internal void CompleteMineService()
        {
            Unit worker=_worker;
            worker.HarvestCargo=TakeGold(worker);
            ExitMine(worker);
            _home=SelectReturnHome(worker,true,_mine.Pos);
            _phase=Phase.ToHome;
            if(worker.HarvestCargo>0&&_home!=null)
                BeginMove(worker,_home.Pos,ReturnReach(worker),_home);
            // 载货但选不到接收厅(唯一大厅在建造/刚被拆): 保留订单与货物原地等,
            // 对标 ToHome 复检点的 "Keep the cargo when no receiver exists" ——
            // 厅造好下一 tick 复检即重新选中并续走返程。订单保留期间按 2026-10-19
            // 用户口径仍是"正在去收集点"(在忙), 不会掉进空闲名单; 真掉单了也不算
            // 灾难: 按同口径它只是"抬着资源"(算空闲), 而派单侧会触发自动返回
            // 而不是派矿/伐木(见 DispatchIdleWorkers 的订单×载货分流)。
            else if(worker.HarvestCargo>0)
            {
                // 停在矿出口等接收厅; 订单保持 Current, 不算空闲。
            }
            else worker.CancelOrders();
            Owner.FinishGoldExtraction(_mine,worker);
        }

        void BeginLumberChop(Unit worker,double start)
        {
            var ability=Owner.HarvestAbility(worker) as RuntimeHarvestAbility;
            if(ability?.LumberRecoveryUntil>start+1e-7)
            {
                _lumberStage=LumberStage.Recovery;
                _lumberDue=ability.LumberRecoveryUntil;
                return;
            }
            _lumberStage=LumberStage.Windup;
            _lumberAnimationVersion++;
            var weapons=worker.WeaponList;
            _lumberDue=start+Math.Max(0,(weapons.Count>0?weapons[0].DamagePoint:0.5f));
        }

        COrder AdvanceLumberChop(Unit worker,Ability harvest)
        {
            double now=HarvestNow(worker);
            // Normal steps cross only a few phases. Keep any older deadline
            // pending if a displaced/stalled worker has accumulated a long
            // backlog, instead of processing the whole backlog in one frame.
            int transitions=0;
            while(_lumberDue<=now+1e-7&&transitions++<64)
            {
                double due=_lumberDue;
                if(_lumberStage==LumberStage.Recovery)
                {BeginLumberChop(worker,due);continue;}
                if(_lumberStage==LumberStage.Backswing)
                {
                    if(worker.HarvestCargo>=Owner.LumberCapacity(worker,harvest))
                    {
                        _home=SelectReturnHome(worker,false,ResourcePoint);
                        if(_home==null)return null;
                        _phase=Phase.ToHome;
                        SetHarvestCollision(worker,true);
                        BeginMove(worker,_home.Pos,ReturnReach(worker),_home);
                        return this;
                    }
                    if(!Owner.IsLiveHarvestTree(_tree))return RetargetLumber(worker)?this:null;
                    _lumberStage=LumberStage.Recovery;
                    _lumberDue=_lumberRecoveryDue;
                    continue;
                }
                int cut=Owner.TakeLumber(worker,_tree,HarvestValue(harvest,"DataA",1));
                if(cut<=0)return null;
                if(worker.HarvestCargo>0&&worker.HarvestCargoGold)worker.HarvestCargo=0;
                worker.HarvestCargoGold=false;
                worker.HarvestCargo=Math.Min(Owner.LumberCapacity(worker,harvest),SaturatingAdd(worker.HarvestCargo,cut));
                var weapons=worker.WeaponList;
                Weapon weapon=weapons.Count>0?weapons[0]:null;
                double front=Math.Max(0,weapon?.DamagePoint??0.5f),back=Math.Max(0.01,weapon?.Backswing??0.5f);
                _lumberStage=LumberStage.Backswing;
                _lumberDue=due+back;
                _lumberRecoveryDue=_lumberDue+Math.Max(0.01,HarvestDuration(harvest,1f)-front-back);
                if(harvest is RuntimeHarvestAbility runtime)runtime.LumberRecoveryUntil=_lumberRecoveryDue;
                EventContext lumberEvent=EventContext.Rent();
                lumberEvent.Event=GameEvent.UnitHarvestLumber;
                lumberEvent.Source=worker;lumberEvent.Target=worker;lumberEvent.TriggerUnit=worker;
                lumberEvent.Point=_tree.Position;
                worker.World.Events.Fire(lumberEvent);
                EventContext.Recycle(lumberEvent);
                if(!ReferenceEquals(worker.Current,this))return null;
            }
            return this;
        }

        public bool InsideMine=>_insideMine;
        public bool IsInsideMine(Unit mine)=>_insideMine&&ReferenceEquals(_mine,mine);
        public bool IsAcolyteChanneling=>_acolyteChannel
            &&_phase==Phase.Harvesting&&_channelSlot>=0;
        public bool IsWispMounted=>_wispLumberChannel&&_mountedTree
            &&_phase==Phase.Harvesting;
        public DestructableRef MountedWispTree=>IsWispMounted?_tree:null;
        public bool IsWispGoldChanneling=>_wispGoldChannel&&_insideMine
            &&_phase==Phase.Harvesting;
        public bool IsCuttingLumber=>!Gold&&!_wispLumberChannel
            &&_phase==Phase.Harvesting;
        public bool IsReturning=>_phase==Phase.ToHome;
        public bool IsEscapeLegActive=>_escapeActive;
        public DestructableRef Tree=>_tree;
        public Unit Mine=>_mine;
        public Vec2 MoveGoal=>_moveGoal;
        public Vec2 MoveTarget=>_move?.MovementTarget??_moveGoal;
        public int HomeSimulationId=>_home?.SimulationId??-1;
        public int TreeCreationNumber=>_tree?.CreationNumber??-1;
        public int MovementGroupId=>_movementGroup?.Id??-1;
        public int MovementRequestCount=>_movementGroup?.RequestCount??0;
        internal string MovementTimingDebug=>$"phase={_phase} phaseTicks={_worker.World.Tick-_phaseEnteredTick} "
            +$"moveState={_move?.LastCursorState} initialReached={_move?.InitialCoarseReachedGoal} "
            +$"initialExhausted={_move?.InitialCoarseBudgetExhausted} initialPoints={_move?.InitialCoarsePointCount} "
            +$"retries={_arrivalRetries} tree={_tree?.CreationNumber}@{_tree?.Position} "
            +$"goal={_moveGoal} cursor=({_move?.CursorDebug})";

        bool TryBeginAcolyteChannelMove(Unit worker)
        {
            // 41aa50 checks mine range before 49c1d0 chooses the nearest free
            // slot. Travelling workers do not reserve ring positions.
            var clock=Owner.MineClock(_mine);
            _mineClock=clock;
            int slot=clock.NearestFreeAcolyteSlot(worker);
            // The host clock may extract the last gold and synchronously end
            // this order while selecting a slot. Do not resume that continuation.
            if(!ReferenceEquals(worker.Current,this)||slot<0)return false;
            _channelSlot=slot;
            if(!clock.JoinChannel(this)||!ReferenceEquals(worker.Current,this))
            {_channelSlot=-1;return false;}
            float angle=-MathF.PI*.5f+slot*MathF.Tau/Owner.GoldMineCapacity(_mine);
            float radius=Owner.GoldMineChannelRadius(_mine);
            Vec2 point=_mine.Pos+new Vec2(MathF.Cos(angle),MathF.Sin(angle))*radius;
            worker.World.SetUnitPosition(worker,point);
            if(!ReferenceEquals(worker.Current,this))return false;
            FaceMine(worker);
            _phase=Phase.Harvesting;
            return true;
        }

        void FaceMine(Unit worker)
        {
            Vec2 toward=_mine.Pos-worker.Pos;
            if(toward.Length>.001f)
                worker.Transform.SetFacing(MathF.Atan2(toward.Y,toward.X));
        }

        void EnterMine(Unit worker)
        {
            if(_insideMine)return;
            _insideMine=true;
            _wasHidden=worker.Hidden;
            _wasInvulnerable=worker.Invulnerable;
            _wasUntargetable=worker.Untargetable;
            _pathingWasEnabled=worker.PathingEnabled;
            worker.Hidden=true;
            worker.Invulnerable=true;
            worker.Untargetable=true;
            if(_pathingWasEnabled)worker.World.SetUnitPathing(worker,false);
            // 普通 Agld: 41a3a0/5a9d80 只隐藏工人，不搬动入矿接触位置。
            // Awha: 5917b0 才保存接近点并把工人送入矿心。
            if(_wispGoldChannel)
            {
                _mineDismountPosition=worker.Pos;
                worker.Pos=_mine.Pos;
            }
        }

        void ExitMine(Unit worker)
        {
            if(!_insideMine)return;
            _insideMine=false;
            // Agld 经 67eea0→67c4d0 从当前位置执行 CUnit+0x180；
            // Awha 经 5911f0 对保存的接近点执行同一位置提交。两者都不是
            // “矿心朝主基地方向×200”：后者可能越过唯一入口或落入阻挡区。
            Vec2 exitPosition=_wispGoldChannel?_mineDismountPosition:worker.Pos;
            worker.Pos=exitPosition;
            worker.Hidden=_wasHidden;
            worker.Invulnerable=_wasInvulnerable;
            worker.Untargetable=_wasUntargetable;
            if(_pathingWasEnabled)worker.World.SetUnitPathing(worker,true);
            // 67c4d0→67a920→633ec0：原地先测，失败才按正常路径掩码扩环。
            worker.World.SetUnitPosition(worker,exitPosition);
        }

        bool TreeClaimedByOtherWisp(Unit worker)=>
            Owner._mountedWispHarvestTrees.TryGetValue(_tree,out var order)
            &&!ReferenceEquals(order,this);

        void MountWispOnTree(Unit worker)
        {
            if(_mountedTree)return;
            _mountedTree=true;
            Owner._mountedWispHarvestTrees[_tree]=this;
            _wispDismountPosition=worker.Pos;
            _pathingWasEnabled=worker.PathingEnabled;
            SetHarvestCollision(worker,true);
            if(_pathingWasEnabled)worker.World.SetUnitPathing(worker,false);
            // 5917b0 saves the approach position, then submits the tree center.
            worker.World.SetUnitPosition(worker,_tree.Position);
            Ability harvest=Owner.HarvestAbility(worker);
            string art=harvest?.TargetArts.FirstOrDefault(value=>
                !string.IsNullOrWhiteSpace(value));
            if(!string.IsNullOrWhiteSpace(art))
            {
                // 物编 Awha(小精灵伐木) 实测 DataC=150(树木特效高度, 非零)
                // -> 中性化行为不变; 缺字段时特效落在地面(height 0)而不是
                // 被静默抬到 150, 并伴随 [物编缺字段 Awha.DataC] 日志。
                _wispTreeEffect=worker.World.AddSpecialEffect(art,_tree.Position,
                    height:Math.Max(0,harvest.GetLevelFloat("DataC",fallback:0f)));
                if(_wispTreeEffect!=null)_wispTreeEffect.TeamColorIndex=worker.TeamColorIndex;
            }

        }

        void UnmountWispFromTree(Unit worker)
        {
            if(!_mountedTree)return;
            _mountedTree=false;
            Owner._mountedWispHarvestTrees.Remove(_tree);
            _wispTreeEffect?.Destroy();
            _wispTreeEffect=null;
            // 5911f0 restores saved x/y, including when the tree handle is gone.
            worker.Pos=_wispDismountPosition;
            if(_pathingWasEnabled)worker.World.SetUnitPathing(worker,true);
            SetHarvestCollision(worker,false);
            worker.World.SetUnitPosition(worker,_wispDismountPosition);

        }

        bool ResourceAvailable()=>Gold
            ?_mine?.Alive==true&&!_mine.Removed&&_mine.ResourceAmount>0
            :Owner.IsLiveHarvestTree(_tree);

        bool RetargetLumber(Unit worker,bool rejectExisting=true)
        {
            DestructableRef exhausted=_tree;
            UnmountWispFromTree(worker);
            if(rejectExisting&&Owner.HarvestAbility(worker) is RuntimeHarvestAbility ability)
                ability.RejectTree(exhausted,HarvestNow(worker));
            // Re-enumerate around the worker on every transition. The old eight
            // approach-watch slots were treated as a persistent candidate pool,
            // hiding closer lateral trees until all remembered trees were gone.
            DestructableRef replacement=Owner.FindNearestHarvestTree(
                worker,worker.Pos,exhausted);
            if(replacement==null&&exhausted!=null)
                replacement=Owner.FindNearestHarvestTree(worker,exhausted.Position,exhausted);
            if(replacement==null)
            {
                if(!_wispLumberChannel&&worker.HarvestCargo>0)
                {
                    _home=SelectReturnHome(worker,false,worker.Pos);
                    if(_home!=null)
                    {
                        EndMove(worker,false);
                        _phase=Phase.ToHome;
                        SetHarvestCollision(worker,true);
                        BeginMove(worker,_home.Pos,ReturnReach(worker),_home);
                        return true;
                    }
                }
                return false;
            }
            EndMove(worker,false);
            _tree=replacement;
            if(Owner.HarvestAbility(worker) is RuntimeHarvestAbility remembered)
            {
                remembered.RememberedTreeCreationNumber=replacement.CreationNumber;
                remembered.RememberedTreePosition=replacement.Position;
            }
            _phase=Phase.ToResource;
            _harvestRemaining=0;
            BeginMove(worker,ResourcePoint,LumberReach(worker),null,_tree);
            return true;
        }

        float HarvestReach(Unit worker)=>_acolyteChannel
            ?Math.Max(0f,Owner.HarvestAbility(worker)?.GetLevelFloat("Rng",fallback:200f)??200f)
            :_wispGoldChannel?Math.Max(32f,worker.AttackRange):0f;

        // PathTex trees navigate to their contact cells. Without a PathTex,
        // use the same ability range + object extent as the arrival predicate.
        float LumberReach(Unit worker)=>HasWispHarvest(worker)
            ?Math.Max(32f,worker.AttackRange)
            :MathF.Max(0f,Owner.HarvestAbility(worker)
                ?.GetLevelFloat("Rng",fallback:0f)??0f)+1f;

        void SetHarvestCollision(Unit worker,bool disabled)
        {
            if(worker==null||_harvestCollisionDisabled==disabled)return;
            _harvestCollisionDisabled=disabled;
            if(disabled)worker.AddState(StateModType.DisableUnitCollision);
            else
            {
                worker.RemoveState(StateModType.DisableUnitCollision);
                // 原生重注册路径 FUN_6f11d830 不环搜, 即无此事件 (EP28 §6.1 #5)。
            }

        }

        void BeginMove(Unit worker,Vec2 point,float range,Unit contact,
            DestructableRef destructable=null)
        {
            if(_phase!=Phase.ToHome||!ReferenceEquals(_returnHome,_home))
            {
                _phaseEnteredTick=worker.World.Tick;
                _phaseStartHomeDist=_phase==Phase.ToHome
                    ?Vec2.Dist(worker.Pos,_home.Pos):0f;
                _returnHome=_phase==Phase.ToHome?_home:null;
            }
            // Harvest movement keeps normal unit collision.  Only the resource or
            // drop-off itself is ignored/contact-tested below.  Disabling the
            // worker's entire unit-collision channel makes a multi-worker smart
            // order collapse into one moving coordinate, unlike Warcraft's visible
            // queue around trees and halls.
            // Native 5325f0 -> 53ccd0 offsets from the target along the approach
            // vector. 10c3c0 rescales that vector; it is NOT a terrain/legal-point
            // clamp. This port still navigates via PathTex contact cells, while
            // arrival separately checks object range or the target record.
            PathingTextureMask mask=contact?.PathingTexture
                ??destructable?.PathingOverlay?.Texture;
            int rotation=contact!=null
                ?(int)MathF.Round(contact.Facing*180f/MathF.PI)
                :destructable?.PathingOverlay?.RotationDegrees??-90;
            float contactRange=destructable!=null?0f:range;
            // Native cell-chain contact compares the target's object record,
            // including its overlay identity (FUN_6f0f4fb0).  The bare mask
            // is ambiguous where two buildings' PathTex pixels overlap.
            int overlayHandle=contact?.PathingOverlayHandle
                ??destructable?.PathingOverlayHandle??0;
            _movePathingTarget=mask==null?null:new CPathingFootprint(
                point,mask,contactRange,rotation,overlayHandle,
                destructable?.PathingOverlay?.UseBuildingRotation??true);
            Vec2 navigationGoal=_movePathingTarget?.NearestReachablePoint(
                worker.World?.PathingMap,worker.Pos,worker.MovementType,
                worker.CollisionSize,
                preferUnobstructedApproach:_phase==Phase.ToHome)??point;
            _moveCompletionRadius=_movePathingTarget==null
                ?CollisionSystem.PhysicalRadius(worker.CollisionSize)+range
                    +(contact==null?0f:CollisionSystem.PhysicalRadius(contact.CollisionSize))
                :0f;
            // A custom receiver without a PathTex must navigate to the same
            // extent radius used by 41b140, not stop at the larger return range.
            if(_phase==Phase.ToHome&&_movePathingTarget==null&&contact!=null)
                _moveCompletionRadius=Math.Max(.49f*32f,worker.CollisionSize+contact.CollisionSize);
            _moveContact=contact;
            _moveGoal=navigationGoal;
            _moveFailed=false;
            if(MoveReached(worker))return;
            // The command group only coordinates the initial common resource
            // approach.  After a member reaches/fails that leg, workers enter
            // different behavior phases and may retarget different trees or
            // return to the hall at different times.  Reusing the old request
            // makes those unrelated goals overwrite each other's shared route.
            MovementGroup moveGroup=_movementGroupConsumed?null:_movementGroup;
            _move=worker.World.MovementBatch.RentMovementForReuse(navigationGoal,moveGroup,
                completionRadius:_moveCompletionRadius,contactTarget:contact,
                pathingTarget:_movePathingTarget);
            if(_retiredCursor!=null)
            {
                _move.SetRecycledCursor(_retiredCursor);
                _retiredCursor=null;
            }
            _move.OnStart(worker);
        }

        // 订单层规划探针 (深预算仅用于分段逃逸规划, 移动搜索仍是原生 700):
        // 绕行路线按约 512 路程分段, 每条腿交给真实移动栈裁决,
        // 腿失败 (_moveFailed) 顺延下一候选, 不重复钉死同一腿点。
        bool TryPlanEscapeLeg(Unit worker,out Vec2 leg)
        {
            leg=default;
            if(_escapeCandidateIdx>=_escapeCandidates.Count
                &&!TryBuildEscapeCandidates(worker))
                return false;
            if(_escapeCandidateIdx>=_escapeCandidates.Count)return false;
            leg=_escapeCandidates[_escapeCandidateIdx++];
            return true;
        }

        bool TryBuildEscapeCandidates(Unit worker)
        {
            _escapeCandidates.Clear();
            _escapeCandidateIdx=0;
            var coarse=worker.World?.CLrPathingSys;
            if(coarse==null||_home==null)return false;
            var target=_home.PathingTexture==null?null:new CPathingFootprint(
                _home.Pos,_home.PathingTexture,ReturnReach(worker),
                (int)MathF.Round(_home.Facing*180f/MathF.PI));
            PathResult probe=coarse.FindPath(worker.Pos,_home.Pos,
                worker.MovementType,worker.CollisionSize,worker,
                EscapeProbeBudget,contactTarget:_home,pathingTarget:target);
            if(probe.Points==null||probe.Points.Count<2)return false;
            float distance=0f;
            Vec2 previous=worker.Pos;
            foreach(Vec2 p in probe.Points)
            {
                distance+=Vec2.Dist(previous,p);
                previous=p;
                if(distance<EscapeLegRange)continue;
                _escapeCandidates.Add(p);
                distance=0f;
            }
            if(distance>=128f)_escapeCandidates.Add(probe.Points[^1]);
            // Preserve route order: Euclidean distance can decrease after a
            // hairpin turn, so sorting by distance may skip the reachable prefix.
            return _escapeCandidates.Count>0;
        }

        void ResetEscapeCandidates()
        {
            _escapeCandidates.Clear();
            _escapeCandidateIdx=0;
        }

        bool AdvanceMove(Unit worker,float dt)
        {
            // In-place upgrades replace the building's PathTex record. The
            // old contact ring refers to a removed overlay even when both
            // tiers use the same mask. Rebind before arrival/path execution.
            if(_moveContact?.IsBuilding==true
                &&((_movePathingTarget?.OverlayHandle??0)!=_moveContact.PathingOverlayHandle
                    ||!ReferenceEquals(_movePathingTarget?.Texture,_moveContact.PathingTexture)))
            {
                Unit contact=_moveContact;
                float range=_phase==Phase.ToHome?ReturnReach(worker):HarvestReach(worker);
                EndMove(worker,false);
                _escapeActive=false;
                ResetEscapeCandidates();
                BeginMove(worker,contact.Pos,range,contact);
            }
            if(MoveReached(worker))return true;
            // BeginMove 发现接触环不可达时直接置位, 无移动订单可执行。
            if(_moveFailed)return true;
            if(_move==null)return false;
            // A registered event may already have reported failure. Do not
            // spend another search budget before the behavior consumes it.
            bool consumesLumberFailure=_phase==Phase.ToResource&&!Gold;
            if(consumesLumberFailure&&_move.CursorFailureObserved)
            {
                _moveFailed=true;
                return true;
            }
            COrder next=_move?.Execute(worker,dt);
            // PathTex movement intentionally retains a distant request after
            // cursor code 4 (11c320/118ae0). The harvest adapter must consume
            // its exhausted approach: ordinary cutters enter 41a090's wait,
            // while Awha rejects and refreshes through its separate behavior.
            // Other object orders keep their request lifecycle.
            bool lumberFailure=consumesLumberFailure&&_move.CursorFailureObserved;
            if(next!=null&&!lumberFailure)return false;
            if(MoveReached(worker))return true;
            // CLrPath has reached its native finite terminal. Propagate that
            // result to the harvesting behavior; rebuilding the identical
            // failed movement here bypasses the 7/8-attempt terminal forever.
            _moveFailed=true;
            return true;
        }

        bool MoveReached(Unit worker)
        {
            // 41a3a0 uses the zero range at be7270 (initialized by 00adf0),
            // then 03e3a0 OR 03bad0. Attack range and a 48-unit waypoint
            // shortcut both let ordinary miners enter before touching the mine.
            if(_phase==Phase.ToResource&&Gold&&!_acolyteChannel&&!_wispGoldChannel)
                return NativeObjectRangeReached(worker)||NativeTargetRecordReached(worker);
            // FUN_6f41a090 passes the per-level ability range (3fa330) to
            // 03e3a0, which adds it to both object extents. Testing only the
            // extents can leave a worker stopped outside a tree's contact cells.
            if(TreeExtentReached(worker))return true;
            // 41a127 calls 03bad0 for lumber as well as for return resources.
            // A terrain sweep can stop outside the radial/point predicate
            // while the tree record is already in the native mover ring.
            if(_phase==Phase.ToResource&&!Gold)
            {
                if(NativeTargetRecordReached(worker))return true;
                if(!_wispLumberChannel)return false;
            }
            // FUN_6f41b140: return arrival is A-type object range OR
            // FUN_6f03bad0.  The latter reaches FUN_6f1126f0 and scans the
            // mover-size ring with FUN_6f0f4ef0 for the target's blocking
            // record.  The EI hall's northwest contact stops just outside
            // CanReach, yet its PathTex record is already in that native ring.
            if(_phase==Phase.ToHome)
                return NativeObjectRangeReached(worker)
                    ||NativeTargetRecordReached(worker);
            return _movePathingTarget!=null
                ?_movePathingTarget.CanReach(worker.Pos,CLrPath.GoalTolerance)
                    ||Vec2.Dist(worker.Pos,_moveGoal)<=48f
                :Vec2.Dist(worker.Pos,_moveGoal)
                    <=Math.Max(CLrPath.GoalTolerance,_moveCompletionRadius);
        }

        // FUN_6f03e3a0: both +0x90 extents are added in native base cells,
        // then clamped from below to 0.49 cell (EP35 E4.3).
        bool NativeObjectRangeReached(Unit worker)
            =>ObjectInteractionRangeReached(worker,_moveContact,0f);

        bool NativeTargetRecordReached(Unit worker)
            =>InteractionTargetRecordReached(worker,_movePathingTarget,_moveContact);

        bool TreeExtentReached(Unit worker)
        {
            // Awha mounts a tree through its separate contact movement; its
            // cast range is not the ordinary Ahar lumber interaction range.
            if(Gold||_wispLumberChannel||_phase!=Phase.ToResource||_tree==null)
                return false;
            // CDestructable vtable+168 -> 6333a0 returns 1 world unit.
            // 63d170 -> 03f100 installs that extent in mover+90 (divided by
            // 32). PathTex dimensions belong only to the contact-record test;
            // adding their full width here permits chopping far from the tree.
            const float treeExtent=1f;
            float range=Math.Max(0,Owner.HarvestAbility(worker)
                ?.GetLevelFloat("Rng",fallback:0f)??0f);
            float reach=MathF.Max(.49f*32f,
                range+treeExtent+MathF.Max(1f,worker.CollisionSize));
            return Vec2.Dist(worker.Pos,_tree.Position)<=reach;
        }

        void EndMove(Unit worker,bool interrupted)
        {
            if(_move?.DebugCursor is { } retiredCursor)_retiredCursor=retiredCursor;
            _move?.OnEnd(worker,interrupted);
            if(_move!=null)
            {
                worker.World?.MovementBatch.RetireMovementForReuse(_move);
                _movementGroupConsumed=true;
            }
            _move=null;
            _movePathingTarget=null;
            _moveCompletionRadius=0f;
            _moveContact=null;
        }

        public override void OnEnd(Unit worker,bool interrupted)
        {
            var clock=_mineClock;
            _mineClock=null;
            clock?.Leave(this);
            ExitMine(worker);
            UnmountWispFromTree(worker);
            EndMove(worker,interrupted);
            SetHarvestCollision(worker,false);
        }
        static readonly string[] AcolyteGoldAnimationTags={"Work","Gold"};
        public override string WorkAnimation=>_phase==Phase.Harvesting
            &&_acolyteChannel?"Stand"
            :_phase==Phase.Harvesting&&!_wispLumberChannel&&!_wispGoldChannel
                ?"Attack":null;
        public override int WorkAnimationRestartVersion=>_lumberAnimationVersion;
        // Game.dll 419350: play Attack (6), then queue Stand (4).
        public override string WorkAnimationFollowup=>WorkAnimation=="Attack"?"Stand":null;
        public override IReadOnlyCollection<string> WorkAnimationSecondaryTags=>
            _phase==Phase.Harvesting&&_acolyteChannel
                ?AcolyteGoldAnimationTags:Array.Empty<string>();
        public override string ToString()=>Gold?"采集黄金":"采集木材";
    }

    abstract class JassGameNativesOrder(JassGameNatives owner):COrder
    {
        protected JassGameNatives Owner { get; }=owner;
    }
}

*/
