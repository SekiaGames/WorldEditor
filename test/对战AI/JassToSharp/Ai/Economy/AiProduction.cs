//重构情况：从 Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Jass/JassGameNatives.Ai.Production.cs 转换（骨架，方法体待补）
namespace OpenWC3.JassToSharp.Ai.Economy;

public static class AiProduction
{
    public const int DefaultQueueDepth = 2;

    public static int Room(int queueLength, int depth = DefaultQueueDepth) =>
        Math.Max(0, depth - Math.Max(0, queueLength));

    public static int PendingFood(IReadOnlyList<int> queueFood)
    {
        int total = 0;
        for (int index = 0; index < queueFood.Count; index++) total += Math.Max(0, queueFood[index]);
        return total;
    }

    public static bool FitsFood(int used, int pending, int add, int cap) =>
        used + pending + Math.Max(1, add) <= cap;
}

/*
原文（JassGameNatives.Ai.Production.cs，668 行）：

using System.Text;
using OpenWC3.Pathing;
using OpenWC3.War3Formats;
using OpenWC3.War3Formats.Jass;

namespace OpenWC3.Game;

public sealed partial class JassGameNatives
{
    sealed partial class MeleeAiRuntime
    {
        bool SetProduce(int quantity,int typeId,int town)
        {
            if(quantity==0)return true;
            int remaining=quantity;
            while(remaining!=0)
            {
                if(!ProduceOne(typeId,town))return false;
                remaining=unchecked(remaining-1);
            }
            return true;
        }

        bool ProduceOne(int typeId,int town)
        {
            const int quantity=1;
            string rawcode=TypeRawcode(typeId);
            if(quantity>0&&rawcode=="uaco"&&CombatBehaviorLog.Enabled)
                CombatBehaviorLog.Write($"[acolyte-produce] t={_owner._world.Tick} P{_player.Id} "
                    +$"quantity={quantity} town={town} done={Count(typeId,true,false)} "
                    +$"includingQueued={Count(typeId,false,true)} mines={MinesOwned} "
                    +$"pendingExpansion={AiInteger("pendingExpansionPeons")} "
                    +$"minTarget={AiInteger("min_peon_number_single_mine")}/"
                    +$"{AiInteger("min_peon_number_double_mine")}/"
                    +$"{AiInteger("min_peon_number_multi_mine")} "
                    +$"gold={_player.States.GetValueOrDefault(1)} wood={_player.States.GetValueOrDefault(2)}");
            // Stock orc.ai intentionally asks for BERSERKER before Robk. Game.dll
            // resolves the rtma family to HEAD_HUNTER until the upgrade finishes;
            // common.ai likewise counts both rawcodes as the same requested unit.
            rawcode=_owner.ResolveAvailableUnitAlias(_player.Id,rawcode);
            UnitTypeData data=_owner._objects?.Get(rawcode);
            if(data==null)return false;
            if(data.GetInt("isbldg",0)!=0)
            {
                // Town tiers are trained by the existing town hall, not placed as
                // a second building.  Blizzard AI requests them through SetProduce
                // just like ordinary structures.
                Unit upgradeSource=Owned().FirstOrDefault(unit=>unit.IsBuilding
                    &&Split(_owner._objects?.Get(unit.TypeId)?.GetString("Upgrade","")??"")
                        .Contains(rawcode,StringComparer.OrdinalIgnoreCase));
                if(upgradeSource!=null)
                {
                    bool upgraded=_owner.IssuePlayerUpgrade(_player.Id,upgradeSource,
                        rawcode,out string error);
                    return upgraded;
                }
                bool built=TryBuild(Builder(rawcode),typeId,town);
                return built;
            }
            if(IsHeroType(rawcode))
            {
                Unit deadHero=_owner._world.Units.FirstOrDefault(unit=>
                    !unit.Removed&&!unit.Alive&&unit.PlayerId==_player.Id
                    &&unit.TypeId.Equals(rawcode,StringComparison.OrdinalIgnoreCase)
                    &&unit.IsHero&&!unit.IsIllusion);
                if(deadHero!=null)
                {
                    Hero revive=deadHero.Abilities.Get<Hero>();
                    if(revive?.Reviving==true)return true;
                    if(revive?.AwaitingRevive==true)
                    {
                        // 8a0430 -> 89fef0 uses Arev and the existing hero
                        // registry for revival. Unlike first-time training, it
                        // does not require the hero rawcode in ATrn/Trains.
                        Unit producer=SelectIdleProducer(Owned().Where(unit=>
                            unit.IsBuilding&&unit.Abilities.Get<ConstructionProgress>()==null
                            &&CanReviveHeroes(unit,_owner._objects)));
                        if(producer!=null&&_owner.IssuePlayerReviveHero(_player.Id,
                            producer,deadHero.SimulationId,out _))return true;
                    }
                    // 89fef0 tail: an existing hero-registry entry satisfies
                    // SetProduce even when no revive order can currently be
                    // submitted. common.ai must continue with later priorities.
                    return true;
                }
            }
            Unit[] producers=EligibleProducers(rawcode).ToArray();
            if(producers.Length==0)return false;
            int accepted=0;
            string failure=null;
            for(int index=0;index<quantity;index++)
            {
                Unit producer=SelectIdleProducer(producers);
                if(producer==null){failure="all-production-queues-full";break;}
                if(producer.RallyPoint==null&&_hasStagePoint)producer.RallyPoint=_stagePoint;
                if(!_owner.IssuePlayerTrain(_player.Id,producer,rawcode,out failure))break;
                accepted++;
            }
            return accepted>0;
        }

        public bool SpendingExpansionResources=>_spendingExpansionResources;

        public bool TryGetExpansionResourceReserve(out int gold,out int lumber)
        {
            gold=0;
            lumber=0;
            if(_spendingExpansionResources||AiBoolean("take_exp")
                ||!_vm.TryGetScalarGlobal("current_expansion",out JassValue value)
                ||!_vm.TryGetHandleObject(value,out Unit mine)||mine?.Alive!=true)
                return false;
            int mines=MinesOwned;
            long elapsed=AiInteger("ai_time")-AiInteger("exp_time_count");
            bool expansionTimeReady=(mines<2&&elapsed>AiInteger("exp_first_time"))
                ||(mines<3&&elapsed>AiInteger("exp_second_time"));
            if(!expansionTimeReady||ExpansionFoe(mine)!=null)return false;
            var units=_owner._world.Units;
            for(int i=0;i<units.Count;i++)
            {
                Unit unit=units[i];
                if(unit.Removed||!unit.Alive||unit.PlayerId!=_player.Id)continue;
                if(unit.IsBuilding&&unit.Identity.Is(Classification.TownHall)
                    &&unit.Abilities.Get<ConstructionProgress>()!=null
                    &&Vec2.Dist(unit.Pos,mine.Pos)<=1200f)return false;
            }
            int expansion=(int)AiInteger("racial_expansion");
            int typeId=(int)AiInteger("old_id",expansion);
            UnitTypeData data=_owner._objects?.Get(TypeRawcode(typeId));
            if(data==null)return false;
            gold=Math.Max(0,data.GetInt("goldcost",0));
            lumber=Math.Max(0,data.GetInt("lumbercost",0));
            return gold>0||lumber>0;
        }

        public void RecordExpansionResourceBlock()=>_expansionResourceBlocks++;

        string ExpansionReserveDebug()=>(TryGetExpansionResourceReserve(
            out int gold,out int lumber)?$"{gold}/{lumber}":"0/0")
            +$":blocks={_expansionResourceBlocks}";

        public bool IsExpansionResourceOrder(int orderId,Vec2 target)
        {
            if(!_vm.TryGetScalarGlobal("current_expansion",out JassValue value)
                ||!_vm.TryGetHandleObject(value,out Unit mine)||mine?.Alive!=true)
                return false;
            int expansion=(int)AiInteger("racial_expansion");
            int expansionTypeId=(int)AiInteger("old_id",expansion);
            return orderId==expansionTypeId&&Vec2.Dist(target,mine.Pos)<=1600f;
        }

        public T SpendExpansionResources<T>(Func<T> action)
        {
            bool previous=_spendingExpansionResources;
            _spendingExpansionResources=true;
            try{return action();}
            finally{_spendingExpansionResources=previous;}
        }

        long AiInteger(string name,int? index=null)
        {
            if(index.HasValue)
            {
                try{return _vm.GetGlobal(name,index) is JassInt indexed?indexed.Val:0;}
                catch(JassVmException){return 0;}
            }
            return _vm.TryGetScalarGlobal(name,out JassValue value)&&value is JassInt scalar
                ?scalar.Val:0;
        }

        bool AiBoolean(string name,int? index=null)
        {
            if(index.HasValue)
            {
                try{return _vm.GetGlobal(name,index) is JassBool indexed&&indexed.Val;}
                catch(JassVmException){return false;}
            }
            return _vm.TryGetScalarGlobal(name,out JassValue value)&&value is JassBool scalar
                &&scalar.Val;
        }

        double AiReal(string name,int? index=null)
        {
            if(index.HasValue)
            {
                try{return _vm.GetGlobal(name,index) is JassReal indexed?indexed.Val:0;}
                catch(JassVmException){return 0;}
            }
            return _vm.TryGetScalarGlobal(name,out JassValue value)&&value is JassReal scalar
                ?scalar.Val:0;
        }

        string AiUnit(string name,int? index=null)
        {
            JassValue value=null;
            if(index.HasValue)
            {
                try{value=_vm.GetGlobal(name,index);}
                catch(JassVmException){return "null";}
            }
            else _vm.TryGetScalarGlobal(name,out value);
            return _vm.TryGetHandleObject(value,out Unit unit)&&unit!=null
                ?$"{unit.SimulationId}/{unit.TypeId}@{unit.Pos.X:0},{unit.Pos.Y:0}"
                :"null";
        }

        string BuildQueueDebug()
        {
            int length=(int)Math.Clamp(AiInteger("build_length"),0,8192);
            return $"{length}["+string.Join(",",Enumerable.Range(0,Math.Min(length,12))
                .Select(index=>
                {
                    int internalId=(int)AiInteger("build_item",index);
                    int rawcode=(int)AiInteger("old_id",internalId);
                    // u0(内部id 0)入队时标出内部id, 便于定位策略层零单位来源
                    string code=rawcode==0?$"u0#{internalId}":TypeRawcode(rawcode);
                    return $"{code}:{AiInteger("build_qty",index)}"
                        +$"/t{AiInteger("build_type",index)}"
                        +$"/p{AiInteger("build_prio",index)}";
                }))+"]";
        }

        bool CanTrain(Unit producer,string rawcode)
        {
            UnitTypeData producerData=_owner._objects?.Get(producer.TypeId);
            return Split(producerData?.GetString("Trains",""))
                .Contains(rawcode,StringComparer.OrdinalIgnoreCase);
        }

        IEnumerable<Unit> EligibleProducers(string rawcode)=>Owned().Where(unit=>
            unit.IsBuilding&&unit.Abilities.Get<ConstructionProgress>()==null
            &&CanTrain(unit,rawcode));

        // 8a1690/8a0430 skip active Aque/ARes/ATrn work. This AI
        // selector does not limit the player's manual production queue.
        static Unit SelectIdleProducer(IEnumerable<Unit> producers)=>producers
            .FirstOrDefault(unit=>(unit.Abilities.Get<RuntimeProductionQueueAbility>()?.Count??0)==0);

        IReadOnlyList<string> Split(string data)
        {
            data??="";
            if(_splitCache.TryGetValue(data,out string[] cached))return cached;
            cached=data.Split(',',StringSplitOptions.RemoveEmptyEntries
                |StringSplitOptions.TrimEntries);
            _splitCache[data]=cached;
            return cached;
        }

        string TypeRawcode(int typeId)
        {
            if(_typeRawcodeCache.TryGetValue(typeId,out string cached))return cached;
            cached=_owner.Rawcode(typeId);
            _typeRawcodeCache[typeId]=cached;
            return cached;
        }

        string Rawcode(JassValue value)=>_owner.Rawcode(value);

        bool TryBuild(Unit worker,int typeId,int town=0)
        {
            string rawcode=TypeRawcode(typeId);
            if(rawcode.Equals(GoldMineIdentity.HauntedType,StringComparison.OrdinalIgnoreCase))
            {
                // Native 8a0130 requires a builder and dispatches construction;
                // an AI production request is not CreateBlightedGoldmine.
                if(worker?.Alive!=true||worker.Removed||worker.PlayerId!=_player.Id
                    ||!CanBuild(worker,rawcode))return false;
                Vec2 center=TownBuildCenter(worker,town);
                Unit mine=_owner._world.Units.Where(unit=>IsAvailableGoldMine(unit)
                        &&GoldMineIdentity.IsOrdinary(unit))
                    .OrderBy(unit=>Vec2.Dist(unit.Pos,center))
                    .ThenBy(unit=>unit.SimulationId).FirstOrDefault();
                return mine!=null&&_owner.IssuePlayerBuild(worker,rawcode,mine.Pos,
                    null,out _,out _lastBuildError);
            }
            if(rawcode.Equals(GoldMineIdentity.EntangledType,StringComparison.OrdinalIgnoreCase))
            {
                Unit neutral=_owner._world.Units.Where(unit=>unit.Alive&&!unit.Removed
                        &&unit.TypeId.Equals(GoldMineIdentity.NeutralType,StringComparison.OrdinalIgnoreCase)
                        &&unit.ResourceAmount>0)
                    .OrderBy(unit=>Vec2.Dist(unit.Pos,Home())).FirstOrDefault();
                return neutral!=null&&_owner.ReplaceNeutralGoldMineInstant(
                    neutral,rawcode,_player)!=null;
            }
            UnitTypeData data=_owner._objects?.Get(rawcode);
            if(data==null||data.GetInt("isbldg",0)==0)return false;
            // Changing builder/reference cannot make an unaffordable request
            // payable. In particular, expansion reserves apply before search.
            if(!CanAffordBuild(data))return false;
            // FUN_6f8a0130 iterates candidate builders in creation order
            // until one search succeeds; a busy builder no longer fails the
            // whole request, and with SetProduce(-1) each candidate's own
            // position is the search reference in turn.
            bool BuilderEligible(Unit candidate)=>candidate?.Alive==true
                &&CanBuild(candidate,rawcode)
                &&!IsAmaiBuyingUnit(candidate)&&!IsAmaiTowerBuilder(candidate)
                &&candidate.Current is not BuildOrder
                &&candidate.Current is not ConstructionWorkOrder;
            Unit[] candidates=Owned().Where(BuilderEligible)
                .OrderBy(candidate=>candidate.SimulationId).ToArray();
            if(candidates.Length==0)return false;
            if(town>=0)
            {
                // an explicit town resolves to one reference point for every
                // candidate, so the search runs once
                Unit chosen=BuilderEligible(worker)?worker:candidates[0];
                return TryBuildFromReference(chosen,rawcode,town,data);
            }
            foreach(Unit candidate in candidates)
                if(TryBuildFromReference(candidate,rawcode,town,data))
                    return true;
            return false;
        }

        bool CanAffordBuild(UnitTypeData data)=>_owner.CanSpendResources(_player.Id,
            Math.Max(0,data.GetInt("goldcost",0)),
            Math.Max(0,data.GetInt("lumbercost",0)),out _lastBuildError);

        bool TryBuildFromReference(Unit worker,string rawcode,int town,
            UnitTypeData data,Vec2? centerOverride=null)
        {
            SimPhaseProbe.Begin("ai.tryBuild");
            try
            {
            return TryBuildFromReferenceCore(worker,rawcode,town,data,centerOverride);
            }
            finally{SimPhaseProbe.End("ai.tryBuild");}
        }

        bool TryBuildFromReferenceCore(Unit worker,string rawcode,int town,
            UnitTypeData data,Vec2? centerOverride=null)
        {
            if(!_owner.TryGetPlayerBuildPrerequisites(worker,rawcode,data,
                    out string prerequisiteError))
            {
                _lastBuildError=prerequisiteError??"build prerequisites rejected";
                return false;
            }
            // Expansion orders also enter here directly. Their scoped payment
            // override is honored by the shared check; no resources are spent.
            if(!CanAffordBuild(data))return false;
            Vec2 center=centerOverride??TownBuildCenter(worker,town);
            PathingTextureMask constructionMask=_owner.ResolveConstructionMask(rawcode);
            // FUN_6f88f2f0 chain: FUN_6f88bbd0 builds a temporary 101x101,
            // 32-world-unit grid around the reference.  A cell stays free (5)
            // only where the building's placement rules accept the terrain.
            // Values follow the native signed-char states: 5=free,
            // -100/-99=terrain/footprint blocked, 0..3=dilated zone class.
            const int GridExtent=BuildGridExtent,GridSize=BuildGridSize;
            const float GridCell=32f;
            const sbyte Free=5,TerrainBlocked=-100;
            IReadOnlyList<string> requiredTokens=_owner.PlacementTokens(
                data.GetString("requirePlace",""));
            IReadOnlyList<string> preventedTokens=_owner.PlacementTokens(
                data.GetString("preventPlace","unbuildable"));
            bool placedNeedsBlight=ContainsToken(requiredTokens,"blighted");
            bool placedNeedsWater=ContainsToken(requiredTokens,"water");
            bool placedNeedsLand=ContainsToken(requiredTokens,"land");
            sbyte[] cellState=_buildCellState;
#if OPENWC3_PERFORMANCE_PROBES
            long probeMark=JassPhaseProbe.Now();
#endif
            for(int y=0;y<GridSize;y++)
            for(int x=0;x<GridSize;x++)
                cellState[y*GridSize+x]=_owner.CellAllowsPlacement(center
                    +new Vec2((x-GridExtent)*GridCell,(y-GridExtent)*GridCell),
                    requiredTokens,preventedTokens,placedNeedsBlight,
                    placedNeedsWater,placedNeedsLand)?Free:TerrainBlocked;
#if OPENWC3_PERFORMANCE_PROBES
            JassPhaseProbe.Add("place:grid",probeMark);
#endif
            // FUN_6f891b70 classification: UnitData.slk buffType
            // (townhall=1/resource=2/factory=4/buffer=8, else 0) and
            // buffRadius supply the projection margin N; grid blight flags
            // (placing hwtw/uzig) override buffer margins with 15/5.
            bool placingTowerBase=rawcode.Equals("hwtw",
                    StringComparison.OrdinalIgnoreCase)
                ||rawcode.Equals("uzig",StringComparison.OrdinalIgnoreCase);
            (sbyte Zone, int Margin) RepulseParams(UnitTypeData objectData)
            {
                string buffType=(objectData?.GetString("buffType","")??"")
                    .Trim().ToLowerInvariant();
                int radius=(int)(objectData?.GetFloat("buffRadius",0f)??0f);
                // FUN_6f88bbd0 grid flags: placing uzig (blight tower base)
                // widens buffer zones to 15; placing hwtw narrows them to 5.
                int bufferMargin=placingTowerBase&&placedNeedsBlight?15
                    :placingTowerBase&&!placedNeedsBlight?5:radius;
                return buffType switch
                {
                    "townhall"=>(0,radius),
                    "resource"=>(1,radius),
                    "factory"=>(2,radius),
                    "buffer"=>(3,bufferMargin),
                    _=>(3,6),
                };
            }
            // FUN_6f891980 -> FUN_6f891b70 -> FUN_6f891b00 -> FUN_6f8918e0:
            // every nearby object stamps its footprint cells and a
            // (2N+1)x(2N+1) zone of its class value around each footprint
            // cell; a zone only overwrites cells holding a weaker value.
            void StampObject(Vec2 pos,UnitTypeData objectData,
                int widthCells,int heightCells)
            {
                (sbyte zone,int margin)=RepulseParams(objectData);
                if(zone<0||zone>3)return;
                int baseX=(int)((pos.X-center.X)/GridCell)+GridExtent;
                int baseY=(int)((pos.Y-center.Y)/GridCell)+GridExtent;
                StampBuildProjection(cellState,GridSize,baseX,baseY,
                    widthCells,heightCells,zone,margin);
            }
            // FUN_6f661060 filter bit 2 (FUN_6f66ea60): the projection covers
            // buildings only - mobile units never stamp the placement grid.
#if OPENWC3_PERFORMANCE_PROBES
            probeMark=JassPhaseProbe.Now();
#endif
            foreach(Unit unit in _owner._world.Units)
            {
                if(!unit.IsBuilding||unit.Hidden||unit.Removed||!unit.Alive
                    ||!unit.PathingEnabled)continue;
                UnitTypeData objectData=_owner._objects?.Get(unit.TypeId);
                if(unit.PathingTexture is{ }texture)
                    StampObject(unit.Pos,objectData,texture.Width,texture.Height);
                else
                {
                    int cells=Footprint.PathingCells(unit.CollisionSize);
                    StampObject(unit.Pos,objectData,cells,cells);
                }
            }
            // A paid BuildOrder owns its foundation cell before the worker
            // arrives; the native projection sees the foundation because
            // Game.dll materialises it immediately.
            foreach(Unit builder in _owner._world.Units)
            {
                if(builder.Hidden||builder.Removed
                    ||builder.Current is not BuildOrder pending)continue;
                string targetCode=RawcodeFromFourcc(pending.JassOrderId);
                UnitTypeData targetData=string.IsNullOrEmpty(targetCode)
                    ?null:_owner._objects?.Get(targetCode);
                CPathingFootprint target=pending.PathingTarget;
                if(target?.Texture is{ }targetTexture)
                    StampObject(pending.Point,targetData,targetTexture.Width,
                        targetTexture.Height);
                else
                {
                    int cells=target?.WidthCells
                        ??Footprint.PathingCells(builder.CollisionSize);
                    StampObject(pending.Point,targetData,cells,cells);
                }
            }
            // FUN_6f88f2f0: the first search pass requires the site to be
            // walkable from the builder (FUN_6f03c3b0 reachability gate);
            // a drained pass retries once without the gate.
#if OPENWC3_PERFORMANCE_PROBES
            JassPhaseProbe.Add("place:stamp",probeMark);
#endif
            // flood-fill 逐格探查前拍一次占用快照 (失败无副作用, 成功即返回):
            // CanPlaceBuilding 的两次全表单位扫描从每格一次降为每次选址一次。
            _owner.BuildPlacementOccupancySnapshot(worker);
            // FUN_6f03c3b0(1,0xf,worker,预算=1,豁免建造者) 的对标: 先做
            // FUN_6f111380 同叶直查(静态开放区内任意两格零搜索直通), 跨叶跑
            // 预算 1 的叶级查询——几乎必拒, 拒绝即置失败标志, 由 param_4=0 的
            // 无门二遍兜底。旧实现用 400 预算全量 A* 逐候选格验证, 拥挤基地
            // 单次选址可达 1.5s (游戏内 5 秒卡顿根因)。
            bool SiteReachableCore(Vec2 site)
            {
                CProximityMap map=_owner._world.PathingMap;
                if(_owner._world.CLrPathingAcc.SameUniformLeaf(
                        map.CellX(worker.Pos.X),map.CellY(worker.Pos.Y),
                        map.CellX(site.X),map.CellY(site.Y),
                        worker.MovementType))
                    return true;
                return _owner._world.CLrPathingAcc.CanReach(
                    worker.Pos,site,worker.MovementType,worker.CollisionSize,
                    worker,budget:1);
            }
            bool SiteReachable(Vec2 site)
            {
#if OPENWC3_PERFORMANCE_PROBES
                if(!JassPhaseProbe.Enabled)return SiteReachableCore(site);
                long reachMark=JassPhaseProbe.Now();
                bool reachable=SiteReachableCore(site);
                JassPhaseProbe.Add("place:reach",reachMark);
                return reachable;
#else
                return SiteReachableCore(site);
#endif
            }
            int placementRejected=0;
            bool pathabilityRejected=false;
            for(int pass=0;;pass++)
            {
                bool requireReachability=pass==0;
                // FUN_6f88d020: flood fill from the centre, native neighbour
                // order; the first free cell that passes full placement wins.
                // Zone cells (0..3) remember one validated fallback position
                // per class; if the queue drains without a free cell the
                // strongest zone (3 -> 0) supplies the site.
                if(++_buildVisitGeneration==0)
                {
                    Array.Clear(_buildVisitStamp);
                    _buildVisitGeneration=1;
                }
                int visitGeneration=_buildVisitGeneration;
                int queueRead=0,queueWrite=0;
                int startIndex=GridExtent*GridSize+GridExtent;
                _buildQueue[queueWrite++]=startIndex;
                _buildVisitStamp[startIndex]=visitGeneration;
                Vec2?[] zoneFallbacks=_buildZoneFallbacks;
                Array.Clear(zoneFallbacks);
                void Enqueue(int x,int y)
                {
                    if(x<0||x>=GridSize||y<0||y>=GridSize)return;
                    int index=y*GridSize+x;
                    if(_buildVisitStamp[index]==visitGeneration)return;
                    _buildVisitStamp[index]=visitGeneration;
                    _buildQueue[queueWrite++]=index;
                }
                while(queueRead<queueWrite)
                {
                    int cellIndex=_buildQueue[queueRead++];
                    int cellY=cellIndex/GridSize,cellX=cellIndex-cellY*GridSize;
                    sbyte state=cellState[cellIndex];
                    Vec2 point=center+new Vec2((cellX-GridExtent)*GridCell,
                        (cellY-GridExtent)*GridCell);
                    if(state==Free)
                    {
                        if(requireReachability&&!SiteReachable(point))
                        {
                            placementRejected++;
                            pathabilityRejected=true;
                            _lastBuildError="unreachable site";
                        }
                        else if(_owner.IssuePlayerBuild(worker,rawcode,point,
                            constructionMask,out _,out string error,
                            useOccupancySnapshot:true))
                        {
                            _lastBuildError="";
                            float anchorDistance=Vec2.Dist(
                                JassGameNatives.SnapBuildPoint(point,
                                    constructionMask),center);
                            _aiBuildPlacements++;
                            _aiBuildAnchorDistanceTotal+=anchorDistance;
                            _aiBuildAnchorDistanceMax=Math.Max(
                                _aiBuildAnchorDistanceMax,anchorDistance);
                            RemoveHarvesterAssignment(worker);
                            return true;
                        }
                        else
                        {
                            placementRejected++;
                            _lastBuildError=error??"placement rejected";
                        }
                    }
                    else if(state>=0&&state<4&&zoneFallbacks[state]==null
                        &&(!requireReachability||SiteReachable(point))
                        &&_owner.TryGetPlayerBuildPlacement(worker,rawcode,point,
                            constructionMask,out _,out _,useOccupancySnapshot:true))
                        zoneFallbacks[state]=point;
                    Enqueue(cellX-1,cellY);
                    Enqueue(cellX-1,cellY-1);
                    Enqueue(cellX-1,cellY+1);
                    Enqueue(cellX+1,cellY);
                    Enqueue(cellX+1,cellY-1);
                    Enqueue(cellX+1,cellY+1);
                    Enqueue(cellX,cellY-1);
                    Enqueue(cellX,cellY+1);
                }
                // FUN_6f88d020 tail: no free cell left, fall back to a
                // validated zone position, preferring the weakest class.
                for(int zone=3;zone>=0;zone--)
                {
                    if(zoneFallbacks[zone] is not{ } fallback)continue;
                    if(_owner.IssuePlayerBuild(worker,rawcode,fallback,
                        constructionMask,out _,out string fallbackError,
                        useOccupancySnapshot:true))
                    {
                        _lastBuildError="";
                        float anchorDistance=Vec2.Dist(
                            JassGameNatives.SnapBuildPoint(fallback,
                                constructionMask),center);
                        _aiBuildPlacements++;
                        _aiBuildAnchorDistanceTotal+=anchorDistance;
                        _aiBuildAnchorDistanceMax=Math.Max(
                            _aiBuildAnchorDistanceMax,anchorDistance);
                        RemoveHarvesterAssignment(worker);
                        return true;
                    }
                    placementRejected++;
                    _lastBuildError=fallbackError??"zone placement rejected";
                }
                // FUN_6f88f2f0 retries on the grid copy only when the first
                // pass rejected candidates through the reachability gate
                // (grid+0x20 failure flag); a plain dry search stops here.
                if(pass>0||!pathabilityRejected)break;
            }
            _lastBuildError+=$" [placement={placementRejected}]";
            return false;
        }

        static string RawcodeFromFourcc(int fourcc)=>new(
        [
            (char)((fourcc>>24)&255),(char)((fourcc>>16)&255),
            (char)((fourcc>>8)&255),(char)(fourcc&255),
        ]);

        Vec2 TownBuildCenter(Unit worker,int town)
        {
            if(town<0)return worker?.Pos??Home();
            Unit mine=TownMine(town);
            if(mine==null)return Home();
            Unit hall=Owned().Where(unit=>unit.IsBuilding
                    &&unit.Identity.Is(Classification.TownHall))
                .OrderBy(unit=>Vec2.Dist(unit.Pos,mine.Pos))
                .FirstOrDefault(unit=>Vec2.Dist(unit.Pos,mine.Pos)<=TownMineRadius);
            return hall?.Pos??mine.Pos;
        }

        bool SetUpgrade(int typeId)
        {
            string rawcode=TypeRawcode(typeId);
            UpgradeTypeData data=_owner._objects?.GetUpgrade(rawcode);
            if(data==null)return false;
            int level=_owner.GetPlayerTechLevel(_player.Id,typeId);
            if(level>=Math.Max(1,data.GetInt("maxlevel",0,0,1)))
                return true;

            Unit researcher=null;
            foreach(Unit building in Owned().Where(unit=>unit.IsBuilding)
                .OrderBy(unit=>unit.SimulationId))
            {
                if(_owner.CanPlayerResearch(_player.Id,building,rawcode,
                        out string candidateError))
                {
                    researcher=building;
                    break;
                }
            }
            if(researcher==null)return false;
            bool started=_owner.IssuePlayerResearch(_player.Id,researcher,rawcode,
                out string error);
            _lastResearch=started
                ?$"{rawcode}:{level+1}@{researcher.TypeId}"
                :$"{rawcode}:fail/{error}";
            if(started)_researchStarted++;
            return started;
        }

        bool OpeningPriorityResearchComplete()=>OpeningPriorityRequirements()
            .All(requirement=>_owner.GetPlayerTechLevel(_player.Id,
                FourCC(requirement.Rawcode))>=requirement.Level);

        // Research remains an opening priority, but unavailable/slow tier-three
        // infrastructure must never hold the first attack forever.
        bool OpeningAssaultGateSatisfied()=>OpeningPriorityResearchComplete()
            ||_owner._world.Tick-_runtimeStartTick>=800;

        (string Rawcode,int Level)[] OpeningPriorityRequirements()=>
            _player.MeleeAiScript.ToLowerInvariant() switch
            {
                "human.ai"=>[("Rhcd",1),("Rhpt",2),("Rhss",1)],
                "orc.ai"=>[("Rows",1),("Rost",2)],
                "undead.ai"=>[("Rufb",1),("Ruba",2)],
                "elf.ai"=>[("Resi",1),("Recb",1)],
                _=>[],
            };

    }

}

*/
