//重构情况：从 Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Jass/JassGameNatives.Ai.HarvestAndExpansion.cs 转换（骨架，方法体待补）
namespace OpenWC3.JassToSharp.Ai.Economy;

public static class HarvestAndExpansion
{
    public static bool ShouldBasicExpand(int mines, int threshold) => mines < threshold;

    public static int PeonTarget(int lumber, int mines, int halls, int min, int max)
    {
        int peons = 6 - lumber / 200;
        if (peons < min) peons = min;
        peons += mines < 2 || halls < 2 ? 5 : 10;
        return Math.Min(max, peons);
    }

    public static bool ShouldBuildBurrow(int foodUsed, int foodMade, int margin) =>
        foodUsed + margin >= foodMade;

    public static bool ShouldGuard(bool newbie, bool hasHall, bool hasMine) =>
        !newbie && hasHall && hasMine;

    public static bool ShouldTech(int gold, int threshold) => gold > threshold;
}

/*
原文（JassGameNatives.Ai.HarvestAndExpansion.cs，1205 行）：

using System.Text;
using OpenWC3.Pathing;
using OpenWC3.War3Formats;
using OpenWC3.War3Formats.Jass;

namespace OpenWC3.Game;

public sealed partial class JassGameNatives
{
    sealed partial class MeleeAiRuntime
    {
        void SetHarvestBudget(bool gold,int townId,int count)
        {
            if(unchecked((uint)townId)>=4u||count<=0)return;
            if(gold) { SetGoldHarvestBudget(townId,count); return; }
            _woodPeonsByTown[townId]=Math.Clamp(
                _woodPeonsByTown.GetValueOrDefault(townId)+count,0,128);
            _woodPeons=Math.Clamp(_woodPeons+count,0,128);
            // 89c310's nonzero-town pass restores previous assignments without
            // replacing their orders. Town zero has an additional captain pass.
            if(townId!=0)
            {
                int remaining=count;
                foreach(Unit unit in Owned())
                    if(_harvesters.TryGetValue(unit,out HarvesterState state)&&!state.Gold
                        &&unit.AiOrNull is Ai ai)
                        ai.TryRenewNativeWoodAssignment(state.TownId,townId,unit.RepulseGroup,ref remaining);
            }
            RefreshAiLumberTargets();
            AddWoodHarvesters(DesiredWoodHarvesterCount(),townId);
        }

        void SetGoldHarvestBudget(int townId,int count)
        {
            count=Math.Max(0,count);
            if(townId is <0 or >3||count==0)return;
            _goldPeonsByTown[townId]=Math.Clamp(
                _goldPeonsByTown.GetValueOrDefault(townId)+count,0,128);
            _goldPeons=Math.Clamp(_goldPeons+count,0,128);
            // 同 SetHarvestBudget: Game.dll 在 native 调用当场加派。
            AddGoldHarvesters(DesiredGoldHarvesterCount());
        }

        void StepHarvest(float dt)
        {
            TrackReleasedBuilders();
            _harvestRefreshClock+=dt;
            if(_harvestRefreshClock>=1)
            {
                _harvestRefreshClock-=1;
                ReconcileHarvesters();
            }
            if(_harvestersDirty)
            {
                _harvestersDirty=false;
                _harvesterStepScratch.Clear();
                _harvesterStepScratch.AddRange(_harvesters.Values);
            }
            foreach(HarvesterState state in _harvesterStepScratch)
            {
                if(state.Worker?.Alive!=true||state.Worker.Removed)continue;
                if(state.Worker.Current is BuildOrder or ConstructionWorkOrder)
                {
                    RemoveHarvesterAssignment(state.Worker);
                    continue;
                }
                StepSharedHarvester(state);
            }
        }

        readonly HashSet<Unit> _buildersToResume=new(ReferenceEqualityComparer.Instance);

        void TrackReleasedBuilders()
        {
            // 只扫本玩家主工人列表 (World 按生成序维护); 谓词与全表扫描同序同义。
            foreach(Unit worker in _owner._world.PrimaryWorkersOf(_player.Id))
                if(worker.Alive&&!worker.Removed
                    &&worker.Current is BuildOrder or ConstructionWorkOrder
                    &&IsPrimaryWorker(worker))
                    _buildersToResume.Add(worker);
            List<Unit> snapshot=_owner._world.RentUnitList();
            try
            {
                snapshot.AddRange(_buildersToResume);
                foreach(Unit worker in snapshot)
                {
                    if(!worker.Alive||worker.Removed||worker.PlayerId!=_player.Id)
                    {
                        _buildersToResume.Remove(worker);
                        continue;
                    }
                    if(worker.Current is BuildOrder or ConstructionWorkOrder)continue;
                    // A new explicit task wins. Tower-rush builders retain their
                    // reservation until AMAI releases them, even between buildings.
                    if(worker.Current is not null and not COrder { Field24: 851972 })
                    {
                        _buildersToResume.Remove(worker);
                        continue;
                    }
                    if(IsAmaiTowerBuilder(worker)||IsAmaiBuyingUnit(worker))continue;
                    bool issued=false;
                    if(_owner.CanHarvestLumber(worker))
                    {
                        DestructableRef tree=FindTree(worker,worker.Pos);
                        if(tree!=null)issued=_owner.IssuePlayerHarvest(worker,tree);
                    }
                    else if(HasAcolyteHarvest(worker))
                    {
                        // Do not invent another gold assignment at a full haunted
                        // mine. A spare summoner can resume when a slot opens.
                        Unit mine=FindResumeHauntedMine(worker);
                        if(mine!=null)issued=_owner.IssuePlayerHarvest(worker,mine,mine.Pos);
                    }
                    if(issued)_buildersToResume.Remove(worker);
                }
            }
            finally { _owner._world.ReturnUnitList(snapshot); }
        }

        Unit FindResumeHauntedMine(Unit worker)
        {
            Unit best=null;
            float bestDistance=0;
            foreach(Unit mine in Owned())
            {
                if(!IsAvailableGoldMine(mine)||!IsHauntedMine(mine))continue;
                int assigned=0;
                foreach(Unit peon in Owned())
                    if(peon.Current is PlayerHarvestOrder harvest
                        &&ReferenceEquals(harvest.Mine,mine))assigned++;
                if(assigned>=5)continue;
                float distance=Vec2.Dist(worker.Pos,mine.Pos);
                // CompareTo matches LINQ's floating-point ordering (including
                // NaN); equal distances retain the first owned-unit entry.
                if(best==null||distance.CompareTo(bestDistance)<0)
                {
                    best=mine;
                    bestDistance=distance;
                }
            }
            return best;
        }

        void StepPeonRepair(float deltaTime)
        {
            _repairRefreshClock+=Math.Max(0,deltaTime);
            if(_repairRefreshClock<1f)return;
            _repairRefreshClock-=MathF.Floor(_repairRefreshClock);
            RefreshPeonRepair();
        }

        readonly record struct AiRankedUnit(Unit Unit,float Primary,int Secondary,int Ordinal);
        sealed class AiRankedUnitComparer:IComparer<AiRankedUnit>
        {
            internal static readonly AiRankedUnitComparer Instance=new();
            public int Compare(AiRankedUnit left,AiRankedUnit right)
            {
                int result=left.Primary.CompareTo(right.Primary);
                if(result==0)result=left.Secondary.CompareTo(right.Secondary);
                return result!=0?result:left.Ordinal.CompareTo(right.Ordinal);
            }
        }
        // Independent leases retain candidate snapshots across order callbacks
        // and nested AI refreshes. Clear references when returning a lease.
        readonly Stack<List<AiRankedUnit>> _rankedAiUnitLists=new();
        readonly Stack<HashSet<int>> _repairAssignedSets=new();
        List<AiRankedUnit> RentRankedAiUnits()=>_rankedAiUnitLists.Count>0
            ?_rankedAiUnitLists.Pop():new();
        void ReturnRankedAiUnits(List<AiRankedUnit> units)
        {
            units.Clear();
            _rankedAiUnitLists.Push(units);
        }

        void RefreshPeonRepair()
        {
            HashSet<int> assignedStructures=_repairAssignedSets.Count>0
                ?_repairAssignedSets.Pop():new();
            List<AiRankedUnit> targets=RentRankedAiUnits();
            try
            {
                foreach(Unit worker in Owned())
                    if(IsPrimaryWorker(worker)
                        &&_owner.TryGetConstructionContactDebug(worker,out string kind,
                            out _,out _,out int structureId)&&kind=="repair")
                        assignedStructures.Add(structureId);

                // Snapshot every eligible target before issuing repair orders.
                // Life keys and ordinal preserve stable LINQ ordering.
                bool buildingsEnabled=_flags.GetValueOrDefault("SetPeonsRepair");
                foreach(Unit target in Owned())
                    if((target.IsBuilding?buildingsEnabled:target.Is(Classification.Mechanical))
                        &&target.Abilities.Get<ConstructionProgress>()==null
                        &&target.Life<target.MaxLife
                        &&!assignedStructures.Contains(target.SimulationId))
                        targets.Add(new(target,target.Life,0,targets.Count));
                targets.Sort(AiRankedUnitComparer.Instance);
                foreach(AiRankedUnit entry in targets)
                {
                    Unit target=entry.Unit;
                    Unit worker=null;
                    (int Idle,float Distance,int Id) bestRank=default;
                    foreach(Unit candidate in Owned())
                    {
                        if(!IsPrimaryWorker(candidate)||!candidate.CanMove
                            ||candidate.GetAbility<RuntimeRepairAbility>()?.CheckCanTarget(target,target.Pos)!=true
                            ||candidate.Current is not (null or COrder { Field24: 851972 } or PlayerHarvestOrder)
                            ||_harvesters.TryGetValue(candidate,out HarvesterState state)
                                &&state.Gold&&GoldHarvesterCount<=5)continue;
                        var rank=(candidate.Current is null or COrder { Field24: 851972 }?0:1,
                            Vec2.Dist(candidate.Pos,target.Pos),candidate.SimulationId);
                        if(worker==null||rank.CompareTo(bestRank)<0)
                        {
                            worker=candidate;
                            bestRank=rank;
                        }
                    }
                    if(worker==null)continue;
                    if(!_owner.IssuePlayerRepair(worker,target))continue;
                    RemoveHarvesterAssignment(worker);
                    assignedStructures.Add(target.SimulationId);
                    _repairOrders++;
                    if(_owner._world.LoggingEnabled&&GameLogger.DetailedWorldEnabled)
                        _owner._world.Log($"[combat-decision] kind=peon-repair "
                            +$"player={_player.Id} worker={worker.SimulationId}/{worker.TypeId} "
                            +$"target={target.SimulationId}/{target.TypeId} "
                            +$"life={target.Life:0}/{target.MaxLife:0}");
                }
            }
            finally
            {
                ReturnRankedAiUnits(targets);
                assignedStructures.Clear();
                _repairAssignedSets.Push(assignedStructures);
            }
        }

        // AMAI 脚本对已被采集状态机追踪的工人下达非采集订单 (如买英雄的
        // move) 时, 引擎采金循环会每 tick 重发采集移动并覆盖脚本命令。
        // 脚本显式指挥优先: 摘除采集追踪, 由脚本任务全权控制该单位。
        public void ReleaseFromHarvest(Unit unit)
        {
            if(unit!=null)RemoveHarvesterAssignment(unit);
        }

        // Harvest reservation bits and town ownership are independent. Native
        // 89c090 counts +0x48/+0x4c regardless of the worker's order state;
        // 89c600 rewrites town ownership only when assigning another town.
        void RemoveHarvesterAssignment(Unit worker)
        {
            _harvesters.Remove(worker);
            _harvestersDirty=true;
            if(worker.Removed||!worker.Alive||worker.PlayerId!=_player.Id)
                _unitTowns.Remove(worker);
        }

        void StepConstructionResume(float deltaTime)
        {
            _resumeRefreshClock+=Math.Max(0,deltaTime);
            if(_resumeRefreshClock<2f)return;
            _resumeRefreshClock-=MathF.Floor(_resumeRefreshClock);
            // 原版: 农民死亡后地基保留, 任意同族农民可继续建造; AI 侧周期性
            // 给停工地基派最近的空闲工人 (对标人族 resume 交互)。
            foreach(Unit target in Owned().Where(unit=>unit.IsBuilding&&unit.Alive
                    &&!unit.Removed))
            {
                ConstructionProgress progress=target.Abilities
                    .Get<ConstructionProgress>();
                if(progress==null||!progress.IsPaused)continue;
                if(Owned().Any(worker=>worker.Current is ConstructionWorkOrder work
                        &&ReferenceEquals(work.Structure,target)))continue;
                Unit worker=Owned().Where(unit=>IsPrimaryWorker(unit)
                        &&unit.Alive&&!unit.Removed&&unit.CanMove&&!IsAmaiTowerBuilder(unit)
                        &&unit.Current is null or COrder { Field24: 851972 } or PlayerHarvestOrder)
                    .OrderBy(unit=>Vec2.Dist(unit.Pos,target.Pos))
                    .ThenBy(unit=>unit.SimulationId).FirstOrDefault();
                if(worker==null)continue;
                if(!_owner.IssuePlayerResumeConstruction(worker,target))continue;
                RemoveHarvesterAssignment(worker);
            }
        }
        float _resumeRefreshClock;

        void ReconcileHarvesters()
        {
            foreach(Unit worker in _harvesters.Keys.Where(worker=>
                        worker?.Alive!=true||worker.Removed).ToArray())
                RemoveHarvesterAssignment(worker);
            // Game.dll keeps assault-Captain ownership above the harvest budget.
            // This matters for Undead because ghouls are both lumber workers and
            // combat units: a later HarvestWood refresh must not pull a dispatched
            // (or returning) ghoul out of the Captain and strand the return state.
            foreach(Unit worker in _harvesters.Keys.Where(worker=>
                        IsCaptainCommittedForHarvest(worker)
                        ||ScriptGroupContains("unit_harassing",worker)||IsAmaiTowerBuilder(worker)).ToArray())
            {
                RemoveHarvesterAssignment(worker);
                if(worker.Current is PlayerHarvestOrder)
                    worker.ForceIssue(new COrder(851972));
            }
            int goldDesired=DesiredGoldHarvesterCount();
            int woodDesired=DesiredWoodHarvesterCount();
            TrimGoldHarvesters(goldDesired);
            TrimWoodHarvesters(woodDesired);
            AddGoldHarvesters(goldDesired);
            AddWoodHarvesters(woodDesired);
        }

        int DesiredGoldHarvesterCount()=>EffectiveGoldHarvesterCount();

        int DesiredWoodHarvesterCount()
        {
            // HarvestWood is the script's exact assignment request. Adding a
            // private minimum or resource-ratio override here changes AMAI's
            // strategy and, for Undead, steals attack ghouls back to lumber.
            return Math.Max(0,_woodPeons);
        }

        static bool IsDefenseTowerRawcode(string rawcode)=>(rawcode??"")
            .ToLowerInvariant() is "hgtw" or "hctw" or "hatw" or "owtw"
                or "uzg1" or "uzg2" or "etrp";

        bool IsArmyProductionBuilding(string rawcode)
        {
            if(string.IsNullOrEmpty(rawcode))return false;
            if(_armyProductionBuildingCache.TryGetValue(rawcode,out bool cached))
                return cached;
            cached=false;
            foreach(string candidate in Split(
                _owner._objects?.Get(rawcode)?.GetString("Trains","")??""))
            {
                string available=_owner.ResolveAvailableUnitAlias(_player.Id,candidate);
                if(!IsHeroType(available)&&!IsPrimaryWorkerRawcode(available)
                    &&UnitResourceValue(available)>=100)
                {
                    cached=true;
                    break;
                }
            }
            _armyProductionBuildingCache[rawcode]=cached;
            return cached;
        }

        static bool IsPrimaryWorkerRawcode(string rawcode)=>(rawcode??"").ToLowerInvariant()
            is "hpea" or "opeo" or "uaco" or "ugho" or "ewsp";

        int UnitResourceValue(string rawcode)
        {
            UnitTypeData data=_owner._objects?.Get(rawcode);
            if(data==null||data.GetInt("isbldg",0)!=0||data.GetInt("weapsOn",0)==0)
                return int.MinValue;
            return data.GetInt("goldcost",0)*2+data.GetInt("lumbercost",0)*3;
        }

        int SacrificeResourceValue(string rawcode)
        {
            UnitTypeData data=_owner._objects?.Get(rawcode);
            if(data==null)return int.MaxValue;
            return Math.Max(0,data.GetInt("goldcost",0))*2
                +Math.Max(0,data.GetInt("lumbercost",0))*3;
        }

        void TrimGoldHarvesters(int desired)
        {
            // 先把金矿采集者快照到实例缓冲: 循环体里的 RemoveHarvesterAssignment
            // 会改动 _harvesters, 直接原地枚举会破坏枚举器版本。排序/计数用
            // 复用缓冲替代每次 new 的 Dictionary 与 Where/ToArray 数组。
            var goldStates=_trimGoldScratch;
            goldStates.Clear();
            foreach(HarvesterState state in _harvesters.Values) if(state.Gold) goldStates.Add(state);
            var retained=_trimRetainedScratch;
            retained.Clear();
            int retainedTotal=0;
            for(int stateIndex=0;stateIndex<goldStates.Count;stateIndex++)
            {
                HarvesterState state=goldStates[stateIndex];
                int limit=_goldPeonsByTown.Count>0
                    ?_goldPeonsByTown.GetValueOrDefault(state.TownId):desired;
                int count=retained.GetValueOrDefault(state.TownId);
                if(retainedTotal<desired&&count<limit&&TownMine(state.TownId)!=null)
                {
                    retained[state.TownId]=count+1;
                    retainedTotal++;
                    continue;
                }
                RemoveHarvesterAssignment(state.Worker);
                if(state.Worker.Current is PlayerHarvestOrder)
                    state.Worker.ForceIssue(new COrder(851972));
            }
        }

        void AddGoldHarvesters(int desired)
        {
            if(_goldPeonsByTown.Count==0)
            {
                AddHarvesters(true,desired);
                return;
            }
            int goldCount=0;
            foreach(HarvesterState state in _harvesters.Values)
                if(state.Gold)goldCount++;
            int remaining=Math.Max(0,desired-goldCount);
            // SetGoldHarvestBudget restricts keys to 0..3, so direct ascending
            // ids are exactly the former OrderBy(key) enumeration.
            for(int townId=0;townId<4;townId++)
            {
                if(!_goldPeonsByTown.TryGetValue(townId,out int count))continue;
                if(remaining==0)break;
                int current=0;
                foreach(HarvesterState state in _harvesters.Values)
                    if(state.Gold&&state.TownId==townId)current++;
                int missing=Math.Min(remaining,Math.Max(0,count-current));
                Unit mine=TownMine(townId);
                if(mine==null)continue;
                // FUN_6f89c600 的状态位过滤不吸收已在采集的 peon;
                // 脚本直发 harvest 的工人保持其订单, 否则与脚本纠偏互相拉扯。
                int unassigned=Owned().Count(unit=>IsHarvester(unit,true)
                    &&!_harvesters.ContainsKey(unit)
                    &&CanRecruitCaptainForHarvest(unit)
                    &&!ScriptGroupContains("unit_harassing",unit)&&!IsAmaiTowerBuilder(unit)
                    &&unit.Current is not BuildOrder and not ConstructionWorkOrder
                    and not PlayerHarvestOrder);
                // FUN_6f89c600 dispatches gold-capable idle peons only. Stopping a
                // lumber worker that can never mine gold (undead ghouls) drains
                // the wood line without ever filling the gold slot.
                int woodTransfers=Math.Max(0,missing-unassigned);
                if(woodTransfers>0)
                {
                    List<AiRankedUnit> states=RentRankedAiUnits();
                    try
                    {
                        foreach(HarvesterState state in _harvesters.Values)
                            if(!state.Gold&&IsHarvester(state.Worker,true))
                                states.Add(new(state.Worker,
                                    Vec2.Dist(state.Worker.Pos,mine.Pos),0,states.Count));
                        states.Sort(AiRankedUnitComparer.Instance);
                        int countToTransfer=Math.Min(woodTransfers,states.Count);
                        for(int index=0;index<countToTransfer;index++)
                        {
                            Unit worker=states[index].Unit;
                            RemoveHarvesterAssignment(worker);
                            if(worker.Current is PlayerHarvestOrder)
                                worker.ForceIssue(new COrder(851972));
                        }
                    }
                    finally { ReturnRankedAiUnits(states); }
                }
                if(missing>0)
                {
                    List<AiRankedUnit> workers=RentRankedAiUnits();
                    try
                    {
                        foreach(Unit worker in Owned())
                            if(IsHarvester(worker,true)&&!_harvesters.ContainsKey(worker)
                                &&CanRecruitCaptainForHarvest(worker)
                                &&!ScriptGroupContains("unit_harassing",worker)
                                &&!IsAmaiTowerBuilder(worker)
                                &&worker.Current is not BuildOrder and not ConstructionWorkOrder
                                and not PlayerHarvestOrder)
                                workers.Add(new(worker,Vec2.Dist(worker.Pos,mine.Pos),0,workers.Count));
                        workers.Sort(AiRankedUnitComparer.Instance);
                        int countToAdd=Math.Min(missing,workers.Count);
                        for(int index=0;index<countToAdd;index++)
                        {
                            Unit worker=workers[index].Unit;
                            _captainMembers.Remove(worker);
                            _defenderMembers.Remove(worker);
                            HarvesterState state=CreateHarvester(worker,true,townId);
                            if(state!=null)
                            {
                                _harvesters[worker]=state;
                                _harvestersDirty=true;
                                _unitTowns[worker]=townId;
                                remaining--;
                            }
                        }
                    }
                    finally { ReturnRankedAiUnits(workers); }
                }
            }
        }

        int EffectiveGoldHarvesterCount()
        {
            if(_goldPeons>0)return _goldPeons;
            // Blizzard's undead melee script hands the economy to the haunted-mine
            // controller after conversion instead of continuing to call HarvestGold.
            // Fill the mine's five acolyte slots from idle workers in that state.
            if(!Owned().Any(unit=>unit.TypeId.Equals(GoldMineIdentity.HauntedType,
                    StringComparison.OrdinalIgnoreCase)))return 0;
            return Math.Min(5,Owned().Count(unit=>unit.TypeId.Equals("uaco",
                StringComparison.OrdinalIgnoreCase)));
        }

        void TrimHarvesters(bool gold,int desired)
        {
            // 与原先 Skip().Select().ToArray() 等价: 跳过前 desired 个同类型采集者,
            // 其余逐个解除; 先快照到实例缓冲, 避免循环体改动 _harvesters 破坏枚举。
            int skip=Math.Max(0,desired);
            var trimStates=_trimAnyScratch;
            trimStates.Clear();
            foreach(HarvesterState state in _harvesters.Values) if(state.Gold==gold) trimStates.Add(state);
            for(int stateIndex=skip;stateIndex<trimStates.Count;stateIndex++)
            {
                Unit worker=trimStates[stateIndex].Worker;
                RemoveHarvesterAssignment(worker);
                if(worker.Current is PlayerHarvestOrder)
                    worker.ForceIssue(new COrder(851972));
            }
        }

        void TrimWoodHarvesters(int desired)
        {
            if(_woodPeonsByTown.Count==0) { TrimHarvesters(false,desired); return; }
            var retained=_trimRetainedScratch;
            retained.Clear();
            int total=0;
            var woodStates=_trimWoodScratch;
            woodStates.Clear();
            foreach(HarvesterState state in _harvesters.Values) if(!state.Gold) woodStates.Add(state);
            for(int woodIndex=0;woodIndex<woodStates.Count;woodIndex++)
            {
                HarvesterState state=woodStates[woodIndex];
                int count=retained.GetValueOrDefault(state.TownId);
                if(total<desired&&count<_woodPeonsByTown.GetValueOrDefault(state.TownId))
                {
                    retained[state.TownId]=count+1;
                    total++;
                    continue;
                }
                RemoveHarvesterAssignment(state.Worker);
                if(state.Worker.Current is PlayerHarvestOrder) state.Worker.ForceIssue(new COrder(851972));
            }
        }

        void AddWoodHarvesters(int desired,int? requestedTown=null)
        {
            if(_woodPeonsByTown.Count==0) { AddHarvesters(false,desired); return; }
            IEnumerable<KeyValuePair<int,int>> requests=requestedTown is int selected
                ?new[]{new KeyValuePair<int,int>(selected,_woodPeonsByTown.GetValueOrDefault(selected))}
                :_woodPeonsByTown.OrderBy(pair=>pair.Key);
            foreach((int townId,int count) in requests)
            {
                int total=_harvesters.Values.Count(state=>!state.Gold);
                int current=_harvesters.Values.Count(state=>!state.Gold&&state.TownId==townId);
                int missing=Math.Min(Math.Max(0,desired-total),Math.Max(0,count-current));
                if(missing>0) AddHarvesters(false,total+missing,townId);
            }
        }

        bool CanRecruitCaptainForHarvest(Unit unit)=>
            (unit.Ai.ComputerHarvestFlags&3)==0
            &&(!_flags.GetValueOrDefault("SetSlowChopping")
                ||!_captainMembers.Contains(unit)&&!_defenderMembers.Contains(unit))
            &&(unit.RepulseGroup is 0 or 5 or 6
                ||unit.RepulseGroup==7&&unit.TypeId.Equals("ugho",StringComparison.OrdinalIgnoreCase));

        void AddHarvesters(bool gold,int desired,int townId=0)
        {
            int current=0;
            foreach(HarvesterState state in _harvesters.Values) if(state.Gold==gold) current++;
            int missing=Math.Max(0,desired-current);
            if(missing==0)return;
            // 直接遍历玩家单位集合 (Owned 是零分配结构化枚举) 挑选候选, 取代
            // Where/OrderBy/Take/ToArray 的迭代器链。顺序与原先逐项等价:
            // 金矿候选保持集合顺序; 木头候选仍把 ugho 排在普通工人之前 (稳定排序)。
            var workers=_harvesterCandidateScratch;
            workers.Clear();
            foreach(Unit unit in Owned())
                if(IsHarvester(unit,gold)&&!_harvesters.ContainsKey(unit)
                    &&CanRecruitCaptainForHarvest(unit)
                    &&!ScriptGroupContains("unit_harassing",unit)&&!IsAmaiTowerBuilder(unit)
                    &&unit.Current is not BuildOrder and not ConstructionWorkOrder
                    // FUN_6f89c600 状态位过滤: 已在采集的 peon 不被预算重派,
                    // 否则脚本纠偏(直发 harvest)与预算再平衡互相打断。
                    and not PlayerHarvestOrder)
                    workers.Add(unit);
            if(!gold)
            {
                var ordered=_harvesterOrderedScratch;
                ordered.Clear();
                foreach(Unit unit in workers)
                    if(unit.TypeId.Equals("ugho",StringComparison.OrdinalIgnoreCase)) ordered.Add(unit);
                foreach(Unit unit in workers)
                    if(!unit.TypeId.Equals("ugho",StringComparison.OrdinalIgnoreCase)) ordered.Add(unit);
                (workers,ordered)=(_harvesterOrderedScratch,_harvesterCandidateScratch);
            }
            // Creating the first haunted/entangled mine replaces the neutral mine
            // in World.Units. Snapshot candidates before issuing orders so that
            // race-specific mine conversion cannot invalidate this enumeration.
            int candidateCount=workers.Count;
            if(candidateCount>missing)candidateCount=missing;
            for(int candidateIndex=0;candidateIndex<candidateCount;candidateIndex++)
            {
                Unit worker=workers[candidateIndex];
                // 89c600 detaches an eligible captain member before assigning
                // town/harvest state when SlowChopping is disabled.
                _captainMembers.Remove(worker);
                _defenderMembers.Remove(worker);
                HarvesterState state=CreateHarvester(worker,gold,townId);
                if(state!=null)
                {
                    _harvesters[worker]=state;
                    _harvestersDirty=true;
                    _unitTowns[worker]=townId;
                }
            }
        }

        HarvesterState CreateHarvester(Unit worker,bool gold,int townId)
        {
            var state=new HarvesterState { Worker=worker,Gold=gold,TownId=townId };
            if(gold)
            {
                state.Mine=FindRaceGoldMine(worker,townId);
                if(state.Mine==null)return null;
                state.ResourcePoint=state.Mine.Pos;
            }
            else
            {
                state.Tree=FindTree(worker,worker.Pos);
                if(state.Tree==null)return null;
                state.ResourcePoint=state.Tree.Position;
            }
            IssueSharedHarvest(state);
            worker.Ai.ComputerHarvestFlags|=gold?1u:2u;
            return state;
        }

        void StepSharedHarvester(HarvesterState state)
        {
            if(state.Worker.Current is PlayerHarvestOrder order)
            {
                if(order.Gold==state.Gold)return;
                // A direct AMAI PeonMineCheck order is authoritative. Do not
                // keep counting that worker against the stale opposite budget
                // or overwrite the script-issued assignment on the next tick.
                RemoveHarvesterAssignment(state.Worker);
                return;
            }
            if(state.Gold)
            {
                if(state.Mine?.Alive!=true||state.Mine.Removed||state.Mine.ResourceAmount<=0)
                    state.Mine=FindRaceGoldMine(state.Worker,state.TownId);
                if(state.Mine==null)
                {
                    // No mine with gold left for this town: release the worker
                    // so the reconcile budget can re-task it (e.g. lumber)
                    // instead of leaving it registered-and-idle forever.
                    RemoveHarvesterAssignment(state.Worker);
                    return;
                }
                state.ResourcePoint=state.Mine.Pos;
            }
            else
            {
                // 89e510/89bbf0 recover only outside the Captain and select
                // from the player's target ring again on each idle dispatch.
                if(IsCaptainCommittedForHarvest(state.Worker)
                    ||state.Worker.Current is not (null or COrder {Field24:851972}))return;
                state.Tree=FindTree(state.Worker,state.Worker.Pos);
                if(state.Tree==null)
                {
                    return;
                }
                state.ResourcePoint=state.Tree.Position;
            }
            IssueSharedHarvest(state);
        }

        void IssueSharedHarvest(HarvesterState state)
        {
            if(state.Gold)_owner.IssuePlayerHarvest(
                state.Worker,state.Mine,state.Mine?.Pos??state.Worker.Pos);
            else _owner.IssuePlayerHarvest(state.Worker,state.Tree.CreationNumber);
        }

        // 矿类按能力判定 (原生语义): Aaha 工人闹鬼矿, Awha 工人缠绕矿。
        Unit FindRaceGoldMine(Unit worker,int townId)
        {
            Unit townMine=TownMine(townId);
            // Aaha can only channel an existing owned Abgm. Assigning an AI
            // harvester must not haunt a neutral mine (89c600 only reserves
            // the worker/town); rebuilding belongs to the build order path.
            if(JassGameNatives.HasAcolyteHarvest(worker))
            {
                bool Available(Unit mine)=>IsAvailableGoldMine(mine)
                    &&_owner.CanHarvestGold(worker,mine);
                return Available(townMine)?townMine:_owner._world.Units
                    .Where(Available).OrderBy(mine=>Vec2.Dist(mine.Pos,Home()))
                    .ThenBy(mine=>mine.SimulationId).FirstOrDefault();
            }
            // A depleted town mine is not an assignment target: workers sent
            // there oscillate walk/idle forever. Fall through to the nearest
            // mine that still has gold (own expansion, then any).
            if(townMine is{ResourceAmount:<=0})townMine=null;
            string special=JassGameNatives.HasWispHarvest(worker)?GoldMineIdentity.EntangledType:"";
            if(special.Length>0)
            {
                Unit owned=townMine?.TypeId.Equals(special,
                    StringComparison.OrdinalIgnoreCase)==true?townMine:null;
                if(owned!=null)return owned;
                Unit neutral=townMine??FindGoldMine();
                if(neutral!=null)return ConvertGoldMine(neutral,special);
            }
            return townMine??FindGoldMine();
        }

        Unit FindGoldMine()
        {
            Vec2 home=Home();
            Unit best=null;float bestDistance=0;
            foreach(Unit unit in _owner._world.Units)
            {
                if(!unit.Alive||unit.Removed||unit.ResourceAmount<=0)continue;
                float distance=Vec2.Dist(unit.Pos,home);
                // Match OrderBy's float comparer, including NaN and stable ties.
                if(best==null||distance.CompareTo(bestDistance)<0)
                { best=unit;bestDistance=distance; }
            }
            return best;
        }

        const float TownMineRadius=1400f;

        static bool IsGoldMine(Unit unit)=>unit?.Alive==true&&!unit.Removed
            &&(unit.TypeId.Equals(GoldMineIdentity.NeutralType,StringComparison.OrdinalIgnoreCase)
                ||unit.TypeId.Equals(GoldMineIdentity.HauntedType,StringComparison.OrdinalIgnoreCase)
                ||unit.TypeId.Equals(GoldMineIdentity.EntangledType,StringComparison.OrdinalIgnoreCase));

        bool IsOwnedSpecialMine(Unit mine)=>mine?.PlayerId==_player.Id
            &&(mine.TypeId.Equals(GoldMineIdentity.HauntedType,StringComparison.OrdinalIgnoreCase)
                ||mine.TypeId.Equals(GoldMineIdentity.EntangledType,StringComparison.OrdinalIgnoreCase));

        bool HasTownHallNear(Unit mine,int? playerId=null)
        {
            if(mine==null)return false;
            if(playerId.HasValue)
            {
                foreach(Unit unit in _owner._world.UnitsForPlayerInWorldOrder(playerId.Value))
                    if(unit.PlayerId==playerId.Value&&IsTownHallNear(unit,mine))return true;
            }
            else
                foreach(Unit unit in _owner._world.Units)
                    if(IsTownHallNear(unit,mine))return true;
            return false;
        }

        static bool IsTownHallNear(Unit hall,Unit mine)=>hall.Alive&&!hall.Removed&&hall.IsBuilding
            &&hall.Identity.Is(Classification.TownHall)
            &&Vec2.Dist(hall.Pos,mine.Pos)<=TownMineRadius;

        List<Unit> TownMines()
        {
            // Game.dll stores at most four explicit town records and counts the
            // mine handles attached to those records (FUN_6f89bee0). A radius
            // query is not equivalent: on Echo Isles it can attach both the
            // main and a nearby neutral mine to the starting hall, making
            // common.ai believe it already owns two mines and skip expansion.
            if(_townMinesCacheTick==_owner._world.Tick
                &&_townMinesCacheUnitCount==_owner._world.Units.Count)
                return _townMinesScratch;
            _townMinesCacheTick=_owner._world.Tick;
            _townMinesCacheUnitCount=_owner._world.Units.Count;
            List<Unit> available=_availableMinesScratch;
            List<Unit> halls=_townHallsScratch;
            List<Unit> result=_townMinesScratch;
            available.Clear();
            halls.Clear();
            result.Clear();
            foreach(Unit unit in _owner._world.Units)
            {
                if(IsGoldMine(unit))available.Add(unit);
                if(unit.PlayerId==_player.Id&&unit.Alive&&!unit.Removed&&unit.IsBuilding
                    &&unit.Identity.Is(Classification.TownHall))halls.Add(unit);
            }
            Vec2 home=Home();
            while(halls.Count>0)
            {
                int hallIndex=0;
                float hallDistance=Vec2.Dist(halls[0].Pos,home);
                for(int index=1;index<halls.Count;index++)
                {
                    float distance=Vec2.Dist(halls[index].Pos,home);
                    if(distance<hallDistance||distance==hallDistance
                        &&halls[index].SimulationId<halls[hallIndex].SimulationId)
                    {
                        hallIndex=index;
                        hallDistance=distance;
                    }
                }
                Unit hall=halls[hallIndex];
                halls.RemoveAt(hallIndex);
                Unit mine=null;
                int mineIndex=-1;
                float mineDistance=float.PositiveInfinity;
                for(int index=0;index<available.Count;index++)
                {
                    float distance=Vec2.Dist(available[index].Pos,hall.Pos);
                    if(distance>TownMineRadius||distance>mineDistance
                        ||distance==mineDistance&&mine!=null
                            &&available[index].SimulationId>=mine.SimulationId)continue;
                    mine=available[index];
                    mineIndex=index;
                    mineDistance=distance;
                }
                if(mine==null)continue;
                result.Add(mine);
                available.RemoveAt(mineIndex);
                if(result.Count==4)return result;
            }
            while(result.Count<4)
            {
                Unit mine=null;
                int mineIndex=-1;
                float distance=float.PositiveInfinity;
                for(int index=0;index<available.Count;index++)
                {
                    Unit candidate=available[index];
                    if(!IsOwnedSpecialMine(candidate))continue;
                    float candidateDistance=Vec2.Dist(candidate.Pos,home);
                    if(candidateDistance>distance||candidateDistance==distance&&mine!=null
                        &&candidate.SimulationId>=mine.SimulationId)continue;
                    mine=candidate;
                    mineIndex=index;
                    distance=candidateDistance;
                }
                if(mine==null)break;
                result.Add(mine);
                available.RemoveAt(mineIndex);
            }
            return result;
        }

        Unit TownMine(int townId)
        {
            if(townId is <0 or >3)return null;
            List<Unit> mines=TownMines();
            return townId<mines.Count?mines[townId]:null;
        }

        bool TownHasHall(int townId)
        {
            Unit mine=TownMine(townId);
            return mine!=null&&HasTownHallNear(mine,_player.Id);
        }

        int TownWithMine()
        {
            List<Unit> mines=TownMines();
            int race=RaceFromPreference(_player.RacePreference);
            for(int index=0;index<mines.Count;index++)
            {
                Unit mine=mines[index];
                if(mine==null)continue;
                if(race is 1 or 2)
                {
                    Unit hall=null;float hallDistance=0;
                    foreach(Unit candidate in Owned())
                    {
                        if(!candidate.IsBuilding||!candidate.Identity.Is(Classification.TownHall))continue;
                        float distance=Vec2.Dist(candidate.Pos,mine.Pos);
                        if(!(distance<=TownMineRadius))continue;
                        int comparison=distance.CompareTo(hallDistance);
                        if(hall==null||comparison<0||comparison==0&&candidate.SimulationId<hall.SimulationId)
                        { hall=candidate;hallDistance=distance; }
                    }
                    if(hall==null||hall.Abilities.Get<ConstructionProgress>()!=null)continue;
                }
                else if(mine.Abilities.Get<ConstructionProgress>()!=null
                    ||race==4&&mine.TypeId!=GoldMineIdentity.EntangledType)continue;
                return index;
            }
            return -1;
        }

        int ExpansionMineIndex(Unit mine)
        {
            if(mine==null)return -1;
            int lower=0,equalBefore=0;bool found=false;
            foreach(Unit candidate in _owner._world.Units)
            {
                if(!IsGoldMine(candidate))continue;
                if(ReferenceEquals(candidate,mine)){found=true;continue;}
                if(candidate.SimulationId<mine.SimulationId)lower++;
                else if(!found&&candidate.SimulationId==mine.SimulationId)equalBefore++;
            }
            // Stable OrderBy + IndexOf selects the first identical reference.
            return found?lower+equalBefore:-1;
        }

        float _expansionSearchRemaining;
        Unit SelectExpansionMine()
        {
            // 890110 preserves even a cached -1 while its 10-second timer is
            // active. A cached mine is checked only for a valid resource ref.
            if(_expansionSearchRemaining>0)
            {
                if(_expansionMine==null)return null;
                if(!_expansionMine.Removed&&(uint)_expansionMine.ResourceAmount>600)return _expansionMine;
            }
            _expansionSearchRemaining=10;
            _expansionMine=null;
            Unit probe=ExpansionPeon(selectMine:false)
                ??Hero()??CombatUnits().FirstOrDefault();
            if(probe==null)return null;
            var ignored=Owned().Where(unit=>!unit.IsBuilding).ToHashSet();
            Unit clear=null,guarded=null,unreachable=null;
            uint clearCost=99999,guardedCost=99999;
            float unreachableDistance=float.PositiveInfinity;
            int budget=5000;
            var candidates=_owner._world.RentUnitList();
            try
            {
                CollectExpansionCandidates(candidates);
                foreach(Unit mine in candidates)
                {
                    Vec2 closest;
                    uint cost=_owner._world.PathingMap==null
                        ?(uint)(Vec2.Dist(probe.Pos,mine.Pos)/32f)
                        :_owner._world.CLrPathingAcc.MeasureNativeDistance(probe.Pos,mine.Pos,
                            probe.MovementType,probe.CollisionSize,probe,budget,out closest,
                            ignored,reconstructPath:false);
                    if(cost==uint.MaxValue)
                    {
                        // 890110 uses actor/mine physical positions here, not the
                        // partial route endpoint's remaining gap.
                        float distance=Vec2.Dist(probe.Pos,mine.Pos);
                        if(distance<unreachableDistance){unreachable=mine;unreachableDistance=distance;}
                        continue;
                    }
                    bool playerPresent=_owner._world.Units.Any(unit=>unit.Alive&&!unit.Removed
                        &&!unit.Hidden&&unit.PlayerId<12&&unit.PlayerId!=_player.Id
                        &&Vec2.Dist(unit.Pos,mine.Pos)<=1500);
                    if(playerPresent)
                    {
                        if(cost<guardedCost){guarded=mine;guardedCost=cost;}
                    }
                    else
                    {
                        if(_owner._world.PathingMap!=null)
                            budget=Math.Min(budget,_owner._world.CLrPathingAcc.LastDistanceExpandedNodes+100);
                        if(cost<clearCost){clear=mine;clearCost=cost;}
                    }
                }
                _expansionMine=clear??guarded??unreachable;
                return _expansionMine;
            }
            finally { _owner._world.ReturnUnitList(candidates); }
        }

        int CountExpansionCandidates()
        {
            var candidates=_owner._world.RentUnitList();
            try { CollectExpansionCandidates(candidates);return candidates.Count; }
            finally { _owner._world.ReturnUnitList(candidates); }
        }

        // Build current candidates once per query, never cache them across ticks
        // or native calls. 890110's resource threshold, mine order and occupied
        // town rules stay unchanged; distance search still uses its 5000 budget.
        void CollectExpansionCandidates(List<Unit> results)
        {
            results.Clear();
            _expansionMinesScratch.Clear();
            _expansionHallsScratch.Clear();
            _expansionOccupantsScratch.Clear();
            _expansionOccupiedMinesScratch.Clear();
            try
            {
                bool hasCandidates=false;
                foreach(Unit unit in _owner._world.Units)
                {
                    if(!unit.Alive||unit.Removed)continue;
                    if(IsGoldMine(unit))
                    {
                        _expansionMinesScratch.Add(unit);
                        if((uint)unit.ResourceAmount>600
                            &&unit.TypeId.Equals(GoldMineIdentity.NeutralType,StringComparison.OrdinalIgnoreCase))
                            hasCandidates=true;
                    }
                    bool townHall=unit.Identity.Is(Classification.TownHall);
                    if(unit.PlayerId==_player.Id&&(townHall||unit.TypeId==GoldMineIdentity.HauntedType))
                        _expansionOccupantsScratch.Add(unit);
                    if(unit.IsBuilding&&townHall
                        &&(unit.PlayerId==_player.Id||_owner.PlayerAllied(_player.Id,unit.PlayerId)))
                        _expansionHallsScratch.Add(unit);
                }
                if(!hasCandidates)return;
                // Each hall's nearest mine is independent of the candidate being
                // checked. Compute that relation once, keeping the original tie rule.
                foreach(Unit hall in _expansionHallsScratch)
                {
                    Unit nearest=null;
                    float nearestDistance=float.PositiveInfinity;
                    for(int index=0;index<_expansionMinesScratch.Count;index++)
                    {
                        Unit candidate=_expansionMinesScratch[index];
                        float distance=Vec2.Dist(candidate.Pos,hall.Pos);
                        if(distance>TownMineRadius||distance>nearestDistance
                            ||distance==nearestDistance&&nearest!=null
                                &&candidate.SimulationId>=nearest.SimulationId)continue;
                        nearest=candidate;
                        nearestDistance=distance;
                    }
                    if(nearest!=null)_expansionOccupiedMinesScratch.Add(nearest);
                }
                foreach(Unit mine in _expansionMinesScratch)
                {
                    if((uint)mine.ResourceAmount<=600
                        ||!mine.TypeId.Equals(GoldMineIdentity.NeutralType,StringComparison.OrdinalIgnoreCase)
                        ||_expansionOccupiedMinesScratch.Contains(mine))continue;
                    bool occupied=false;
                    // Includes unfinished own halls and haunted mines, exactly as
                    // the original per-candidate census did (no IsBuilding gate).
                    foreach(Unit unit in _expansionOccupantsScratch)
                        if(Vec2.Dist(unit.Pos,mine.Pos)<=1200){occupied=true;break;}
                    if(!occupied)results.Add(mine);
                }
            }
            finally
            {
                _expansionMinesScratch.Clear();
                _expansionHallsScratch.Clear();
                _expansionOccupantsScratch.Clear();
                _expansionOccupiedMinesScratch.Clear();
            }
        }

        readonly List<Unit> _expansionMinesScratch=new(32);
        readonly List<Unit> _expansionHallsScratch=new(16);
        readonly List<Unit> _expansionOccupantsScratch=new(16);
        readonly HashSet<Unit> _expansionOccupiedMinesScratch=new();

        Unit ExpansionPeon()=>ExpansionPeon(selectMine:true);

        Unit ExpansionPeon(bool selectMine)
        {
            Unit mine=selectMine?SelectExpansionMine():_expansionMine;
            if(mine==null)
            {
                // The public native needs a valid expansion record. This private
                // bootstrap supplies the host's mine search with an origin only.
                if(selectMine)return null;
                return Owned().Where(unit=>unit.CanMove&&!unit.IsBuilding
                        &&IsMeleeAiHarvesterCandidate(unit,gold:true))
                    .OrderBy(unit=>Vec2.Dist(unit.Pos,Home())).FirstOrDefault();
            }
            Unit idle=null,wood=null,other=null;
            uint idleCost=9999,woodCost=9999,otherCost=9999;
            foreach(Unit unit in Owned())
            {
                if(!unit.CanMove||unit.IsBuilding||unit.Hidden||!unit.UsesFood
                    ||string.IsNullOrWhiteSpace(_owner._objects?.Get(unit.TypeId)?.GetString("Builds",""))
                    ||unit.AiOrNull is not Ai ai
                    ||(ai.ComputerHarvestFlags&0x30000000)==0x30000000)continue;
                bool isIdle=unit.RepulseGroup is 0 or 7;
                if(!isIdle&&(unit.RepulseGroup is not (5 or 6)||idle!=null))continue;
                uint cost=_owner._world.PathingMap==null
                    ?(uint)(Vec2.Dist(unit.Pos,mine.Pos)/32f)
                    :_owner._world.CLrPathingAcc.MeasureNativeDistance(unit.Pos,mine.Pos,
                        unit.MovementType,unit.CollisionSize,unit,700,out _,reconstructPath:false);
                if(isIdle)
                {
                    if(cost<idleCost){idle=unit;idleCost=cost;}
                }
                else if((ai.ComputerHarvestFlags&2)!=0)
                {
                    if(cost<woodCost){wood=unit;woodCost=cost;}
                }
                else if(cost<otherCost){other=unit;otherCost=cost;}
            }
            return idle??wood??other;
        }

        bool ConvertUnits(int quantity,int sourceTypeId)
        {
            string source=TypeRawcode(sourceTypeId);
            // 899ab0 decrements the number of matching source units examined,
            // even when dispatch fails. Existing output units do not count.
            // It tests quantity at the next owned unit, returning false at end
            // of enumeration even if the final matching unit received an order.
            foreach(Unit unit in Owned().OrderBy(unit=>unit.SimulationId).ToArray())
            {
                if(quantity<=0)return true;
                if(!unit.TypeId.Equals(source,StringComparison.OrdinalIgnoreCase)
                    ||unit.Abilities.Get<ConstructionProgress>()!=null)continue;
                RuntimeAlternateFormAbility form=null;
                var formItems=unit.Abilities.Items;
                for(int f=0;f<formItems.Count;f++)
                    // Original 4f78b0 selector returns Aave, not all morphs.
                    if(formItems[f] is RuntimeAlternateFormAbility a
                        &&a.BaseCode.Equals("Aave",StringComparison.OrdinalIgnoreCase)&&a.NormalFormId
                        .Equals(source,StringComparison.OrdinalIgnoreCase)){form=a;break;}
                if(form==null)continue;
                if(unit.Current is not CastOrder cast||!ReferenceEquals(cast.Ability,form)
                    ||cast.JassOrderId!=form.ForwardOrderId)
                    _owner.IssueOrder(unit,form.ForwardOrderId,form.OrderId,null,null);
                quantity--;
            }
            return false;
        }

        Unit ExpansionFoe()
        {
            Unit mine=SelectExpansionMine();
            return ExpansionFoe(mine);
        }

        Unit ExpansionFoe(Unit mine)
        {
            if(mine==null)return null;
            // 等价于 Where.Where.OrderBy(dist).ThenBy(simId).First：取 (dist,simId) 最小
            Unit best=null;float bestDist=float.MaxValue;int bestSim=0;
            var units=_owner._world.Units;
            for(int i=0;i<units.Count;i++)
            {
                Unit unit=units[i];
                if(!IsAttackableCreep(unit))continue;
                if(Vec2.Dist(unit.Pos,mine.Pos)>1500f)continue;
                float dist=Vec2.Dist(unit.Pos,mine.Pos);
                if(best==null||dist<bestDist||(dist==bestDist&&unit.SimulationId<bestSim))
                {best=unit;bestDist=dist;bestSim=unit.SimulationId;}
            }
            return best;
        }

        bool TryBuildExpansion(Unit worker,int typeId)
        {
            _expansionAttempts++;
            string rawcode=TypeRawcode(typeId);
            if(worker!=null)
            {
                // 8a08f0 removes the supplied worker from assault membership,
                // then checks its own build ability. Failure does not choose
                // a different worker through the null-worker branch.
                _captainMembers.Remove(worker);
                if(!worker.Alive||worker.Removed||!CanBuild(worker,rawcode))return false;
            }
            Unit mine=SelectExpansionMine();
            if(mine==null)
            {
                _owner._world.Log($"[AI P{_player.Id}] expansion rejected no reachable mine "
                    +$"building={rawcode} owned={TownMines().Count}");
                return false;
            }
            Unit guard=ExpansionFoe(mine);
            if(guard!=null)
            {
                _expansionBuildBlocks++;
                _lastBuildError=$"expansion guarded by {guard.TypeId}";
                if(_expansionBuildBlocks==1||_expansionBuildBlocks%30==0)
                    _owner._world.Log($"[AI P{_player.Id}] expansion blocked by creep "
                        +$"mine={mine.SimulationId}@{mine.Pos} "
                        +$"guard={guard.SimulationId}/{guard.TypeId}@{guard.Pos}");
                return false;
            }
            if(worker==null)worker=Builder(rawcode);
            if(worker?.Alive!=true||worker.Current is BuildOrder or ConstructionWorkOrder)
                return false;

            if(rawcode.Equals(GoldMineIdentity.HauntedType,StringComparison.OrdinalIgnoreCase))
            {
                if(!SpendExpansionResources(()=>_owner.IssuePlayerBuild(worker,rawcode,
                    mine.Pos,null,out _,out _lastBuildError)))return false;
                _expansionMine=null;
                _expansionsStarted++;
                _owner._world.Log($"[AI P{_player.Id}] expansion started "
                    +$"building={rawcode} mine={mine.SimulationId}@{mine.Pos}");
                return true;
            }

            UnitTypeData hallData=_owner._objects?.Get(rawcode);
            if(hallData!=null&&SpendExpansionResources(()=>TryBuildFromReference(
                worker,rawcode,-1,hallData,mine.Pos)))
            {
                _lastBuildError="";
                _expansionsStarted++;
                string specialMine=_player.MeleeAiScript.ToLowerInvariant() switch
                {
                    "elf.ai"=>GoldMineIdentity.EntangledType,
                    _=>"",
                };
                if(specialMine.Length>0&&mine.TypeId.Equals(GoldMineIdentity.NeutralType,
                        StringComparison.OrdinalIgnoreCase))
                    _owner.ReplaceNeutralGoldMineInstant(mine,specialMine,_player);
                _expansionMine=null;
                _owner._world.Log($"[AI P{_player.Id}] expansion started "
                    +$"building={rawcode} worker={worker.SimulationId}/{worker.TypeId} "
                    +$"mine={mine.SimulationId}@{mine.Pos}");
                return true;
            }
            _owner._world.Log($"[AI P{_player.Id}] expansion placement failed "
                +$"building={rawcode} mine={mine.SimulationId}@{mine.Pos} "
                +$"error={_lastBuildError}");
            return false;
        }

        Unit ConvertGoldMine(Unit source,string rawcode)
        {
            return _owner.ReplaceNeutralGoldMineInstant(source,rawcode,_player)??source;
        }

    }
}

*/
