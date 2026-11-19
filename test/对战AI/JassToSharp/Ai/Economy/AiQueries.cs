//重构情况：从 Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Jass/JassGameNatives.Ai.Queries.cs 转换（Owned/TownUnitCount/CountFor，改为对 Sim 单位直接查询）
using OpenWC3;

namespace OpenWC3.JassToSharp.Ai.Economy;

public static class AiQueries
{
    public static bool IsPrimaryWorker(string typeId) => typeId switch
    {
        "hpea" or "opeo" or "ewsp" or "uaco" => true,
        _ => false,
    };

    public static bool IsDone(Unit unit) =>
        unit != null && (!unit.IsBuilding || unit.GetAbility<ConstructionProgress>() == null);

    public static bool Owned(Unit unit, int player) =>
        unit != null && unit.Alive && unit.PlayerId == player;

    public static int TownUnitCount(IReadOnlyList<Unit> all, int player, string typeId,
        int town, bool doneOnly, Func<Unit, int> townOf = null)
    {
        int count = 0;
        for (int index = 0; index < all.Count; index++)
        {
            Unit unit = all[index];
            if (!Owned(unit, player)) continue;
            if (!string.Equals(unit.TypeId, typeId, StringComparison.OrdinalIgnoreCase)) continue;
            if (doneOnly && !IsDone(unit)) continue;
            if (town >= 0 && (townOf?.Invoke(unit) ?? -1) != town) continue;
            count++;
        }
        return count;
    }

    public static int CountDone(IReadOnlyList<Unit> all, int player, string typeId) =>
        TownUnitCount(all, player, typeId, -1, doneOnly: true);

    public static int CountAlive(IReadOnlyList<Unit> all, int player, string typeId) =>
        TownUnitCount(all, player, typeId, -1, doneOnly: false);

    public static int PrimaryWorkerCount(IReadOnlyList<Unit> all, int player)
    {
        int count = 0;
        for (int index = 0; index < all.Count; index++)
        {
            Unit unit = all[index];
            if (!Owned(unit, player)) continue;
            if (unit.IsBuilding || !IsPrimaryWorker(unit.TypeId)) continue;
            count++;
        }
        return count;
    }

    public static int BuildingCount(IReadOnlyList<Unit> all, int player, bool doneOnly)
    {
        int count = 0;
        for (int index = 0; index < all.Count; index++)
        {
            Unit unit = all[index];
            if (!Owned(unit, player) || !unit.IsBuilding) continue;
            if (doneOnly && !IsDone(unit)) continue;
            count++;
        }
        return count;
    }

    public static int NearestIndex(IReadOnlyList<Unit> all, int player, Vec2 from,
        Func<Unit, bool> filter)
    {
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int index = 0; index < all.Count; index++)
        {
            Unit unit = all[index];
            if (!Owned(unit, player) || !filter(unit)) continue;
            float dx = unit.Pos.X - from.X;
            float dy = unit.Pos.Y - from.Y;
            float distance = dx * dx + dy * dy;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = index;
        }
        return best;
    }

    public static int TownIndexOf(Unit unit, IReadOnlyList<Unit> halls, float radius)
    {
        if (unit == null || halls == null) return -1;
        int best = -1;
        float bestDistance = radius * radius;
        for (int index = 0; index < halls.Count; index++)
        {
            Unit hall = halls[index];
            if (hall == null || !hall.Alive) continue;
            float dx = hall.Pos.X - unit.Pos.X;
            float dy = hall.Pos.Y - unit.Pos.Y;
            float distance = dx * dx + dy * dy;
            if (distance > bestDistance) continue;
            bestDistance = distance;
            best = index;
        }
        return best;
    }
}

/*
原文（JassGameNatives.Ai.Queries.cs，754 行）：

using System.Text;
using OpenWC3.Pathing;
using OpenWC3.War3Formats;
using OpenWC3.War3Formats.Jass;

namespace OpenWC3.Game;

public sealed partial class JassGameNatives
{
    sealed partial class MeleeAiRuntime
    {
        string DescribeAcolyteTownStock()
        {
            var workers=new List<Unit>();int queued=0;
            foreach(Unit unit in Owned())
            {
                if(unit.TypeId=="uaco")workers.Add(unit);
                var jobs=unit.Abilities.Get<RuntimeProductionQueueAbility>()?.Jobs;
                if(jobs!=null)foreach(var job in jobs)if(job.Rawcode=="uaco")queued++;
            }
            var text=new StringBuilder($"live={workers.Count};queued={queued};towns=");
            for(int town=0;town<4;town++)
            {if(town>0)text.Append(',');text.Append(TownUnitCount(FourCC("uaco"),town,true));}
            text.Append(";units=");
            for(int i=0;i<workers.Count;i++)
            {
                Unit unit=workers[i];if(i>0)text.Append(',');
                text.Append($"{unit.SimulationId}:town={(_unitTowns.TryGetValue(unit,out int town)?town:-1)}"
                    +$":order={unit.Current?.GetType().Name??"idle"}");
            }
            return text.ToString();
        }
        DestructableRef FindTree(Unit worker,Vec2 point)
        {
            // 原版多工可共树(后排与前排重叠后被分离系统拉开, 期间采集不停),
            // 不做"已被他人认领"排除——排除会让工人舍近求远跑去找空树。
            return _lumberTargets.Next()??_owner.FindNearestAiHarvestTree(worker,point);
        }

        bool IsLiveTree(DestructableRef tree)
        {
            if(tree==null||tree.Removed||tree.Life<=0)return false;
            DestructableTypeData data=_owner._objects?.GetDestructable(
                TypeRawcode(tree.TypeId));
            string target=data?.GetString("targType","")??"";
            string category=data?.GetString("category","")??"";
            return target.Contains("tree",StringComparison.OrdinalIgnoreCase)
                ||category.Equals("T",StringComparison.OrdinalIgnoreCase);
        }

        static bool IsHarvester(Unit unit,bool gold)
            =>IsMeleeAiHarvesterCandidate(unit,gold);

        int Count(int typeId,bool done,bool includeQueued)
        {
            string rawcode=TypeRawcode(typeId);
            IReadOnlySet<string> family=_owner.UnitAvailabilityFamily(rawcode);
            int count=CachedCountFor(_player.Id,rawcode,family,done);
            if(!includeQueued)return count;
            return count+ProducingUnitCount(family);
        }

        int NativeUnitCount(int typeId,bool done,bool includeQueued)
        {
            string rawcode=TypeRawcode(typeId);
            int count=ExactUnitCountFor(_player.Id,rawcode,done);
            return includeQueued?count+ProducingUnitCountForRawcode(rawcode):count;
        }

        // 89e070 -> 66b1c0: one contribution per producer if any queued
        // order matches; duplicate orders and later queue positions are allowed.
        // The single-rawcode form is the common GetUnitCount/GetTownUnitCount
        // path. It is exactly the old one-element ordinal HashSet lookup without
        // allocating a set per native call.
        int ProducingUnitCountForRawcode(string rawcode)
        {
            int count=0;
            foreach(Unit unit in _owner._world.UnitsForPlayer(_player.Id))
            {
                if(unit.PlayerId!=_player.Id||!unit.Alive||unit.Removed)continue;
                if(unit.Current is BuildOrder {AwaitingFoundation:true} build
                    &&string.Equals(TypeRawcode(build.JassOrderId),rawcode,
                        StringComparison.Ordinal))
                {count++;continue;}
                IReadOnlyList<RuntimeProductionQueueAbility.Job> jobs=
                    unit.Abilities.Get<RuntimeProductionQueueAbility>()?.Jobs;
                if(jobs==null)continue;
                for(int index=0;index<jobs.Count;index++)
                    if(string.Equals(jobs[index].Rawcode,rawcode,
                            StringComparison.Ordinal))
                    {
                        count++;
                        break;
                    }
            }
            return count;
        }

        int ProducingUnitCount(IReadOnlySet<string> family)
        {
            int count=0;
            foreach(Unit unit in _owner._world.UnitsForPlayer(_player.Id))
            {
                if(unit.PlayerId!=_player.Id||!unit.Alive||unit.Removed)continue;
                // The build is already paid for while its worker travels to
                // the site, before a foundation exists in the world list.
                if(unit.Current is BuildOrder {AwaitingFoundation:true} build
                    &&family.Contains(TypeRawcode(build.JassOrderId)))
                {count++;continue;}
                IReadOnlyList<RuntimeProductionQueueAbility.Job> jobs=
                    unit.Abilities.Get<RuntimeProductionQueueAbility>()?.Jobs;
                if(jobs==null)continue;
                // Count producers with a matching job, not matching jobs.
                for(int index=0;index<jobs.Count;index++)
                    if(family.Contains(jobs[index].Rawcode))
                    {
                        count++;
                        break;
                    }
            }
            return count;
        }
        int CountFor(int playerId,IReadOnlySet<string> family,bool done)
        {
            int count=0;
            foreach(Unit unit in _owner._world.UnitsForPlayer(playerId))
                if(!unit.Removed&&unit.Alive&&unit.PlayerId==playerId
                    &&family.Contains(unit.TypeId)
                    &&(!done||unit.Abilities.Get<ConstructionProgress>()==null))count++;
            return count;
        }
        int CountFor(int playerId,int typeId,bool done)
        {
            string rawcode=TypeRawcode(typeId);
            return CachedCountFor(playerId,rawcode,
                _owner.UnitAvailabilityFamily(rawcode),done);
        }

        // typed-count 缓存: 按 (玩家, 族键, 含在建) 键 + 世界变异版本失效。
        // GetPlayerUnitTypeCount 1.79M 次/局的调用量下免去每次全表扫描;
        // 任何漏掉的变异点会被同 seed 逐位一致 A/B 直接抓出。
        readonly Dictionary<(int Player,string Rawcode,bool Done),(long Version,int Count)>
            _countCache=new();
        readonly Dictionary<(int Player,string Rawcode,bool Done),(long Version,int Count)>
            _exactCountCache=new();
        long _countCacheCollisions;

        int CachedCountFor(int playerId,string rawcode,IReadOnlySet<string> family,
            bool done)
        {
            var key=(playerId,rawcode,done);
            long version=_owner._world.UnitVersion;
            if(_countCache.TryGetValue(key,out var entry)
                &&entry.Version==version)
                return entry.Count;
            _countCacheCollisions++;
            int count=CountForUncached(playerId,family,done);
            _countCache[key]=(version,count);
            return count;
        }

        int CountForUncached(int playerId,IReadOnlySet<string> family,bool done)
        {
            int count=0;
            foreach(Unit unit in _owner._world.UnitsForPlayer(playerId))
                if(!unit.Removed&&unit.Alive&&unit.PlayerId==playerId
                    &&family.Contains(unit.TypeId)
                    &&(!done||unit.Abilities.Get<ConstructionProgress>()==null))count++;
            return count;
        }

        // Game.dll FUN_6f890680 = FUN_6f89e070(在产生产者数) + FUN_6f89c090
        // (城镇域存量)。FUN_6f89e070: 数"活着且生产队列包含该族"
        // 的生产者, 每生产者计一次(匹配任一队列项, 队列深度不重复计); FUN_6f89c090: 只数
        // 归属于该城镇记录的同族单位, dn=true 排除在建; townid 越界(<0 或
        // >3)时城镇域为 0, 只剩在产部分。
        // 原版归属两条来源: 采集编入时 FUN_6f8a0e70 写单位 AI 对象
        // +0x48/+0x4c (_unitTowns); 单位创建时(FUN_6f898560)按寻路就近
        // 城镇记录归属(FUN_6f899920), 市政厅类建筑出现还会自动建镇记录
        // (FUN_6f8950f0)。宿主已补单城镇初始 peon 归属并保留任务切换
        // 时的归属; 多城镇创建尚缺原版路径成本选择。建筑下方仍使用
        // 矿点直线近似, 不能视为跨悬崖场景的原版等价实现。
        int TownUnitCount(int typeId,int townId,bool doneOnly)
        {
            string rawcode=TypeRawcode(typeId);
            int producing=ProducingUnitCountForRawcode(rawcode);
            if(townId is <0 or >3)return producing;
            int assigned=0;
            foreach(KeyValuePair<Unit,int> entry in _unitTowns)
            {
                Unit unit=entry.Key;
                if(entry.Value!=townId||unit.Removed||!unit.Alive
                    ||unit.PlayerId!=_player.Id
                    ||!string.Equals(unit.TypeId,rawcode,StringComparison.Ordinal))
                    continue;
                if(doneOnly&&unit.Abilities.Get<ConstructionProgress>()!=null)
                    continue;
                assigned++;
            }
            List<Unit> townMines=TownMines();
            Unit ownMine=townId<townMines.Count?townMines[townId]:null;
            if(ownMine!=null)
            {
                foreach(Unit unit in Owned())
                {
                    if(!unit.IsBuilding||_unitTowns.ContainsKey(unit)
                        ||!string.Equals(unit.TypeId,rawcode,StringComparison.Ordinal))
                        continue;
                    if(doneOnly&&unit.Abilities.Get<ConstructionProgress>()!=null)
                        continue;
                    int nearest=0;
                    float nearestDistance=float.PositiveInfinity;
                    for(int index=0;index<townMines.Count;index++)
                    {
                        float distance=Vec2.Dist(unit.Pos,townMines[index].Pos);
                        if(distance<nearestDistance)
                        {
                            nearest=index;
                            nearestDistance=distance;
                        }
                    }
                    if(nearest==townId)assigned++;
                }
            }
            return producing+assigned;
        }
        int PlayerUnitTypeCount(JassVm vm,JassValue playerValue,int typeId)
        {
            SimPhaseProbe.Begin("native.unitTypeCount");
            try
            {
            return PlayerUnitTypeCountCore(vm,playerValue,typeId);
            }
            finally{SimPhaseProbe.End("native.unitTypeCount");}
        }
        int PlayerUnitTypeCountCore(JassVm vm,JassValue playerValue,int typeId)
        {
            if(vm.TryGetHandleObject(playerValue,out PlayerRef player))
            {
                // This native is a raw type-id query.  Do not use the production
                // availability family here: AMAI deliberately asks for uzg1/uzg2
                // when estimating tower strength, and folding the rtma predecessor
                // uzig into that result makes an unupgraded ziggurat count as an
                // attacking tower.
                string rawcode=TypeRawcode(typeId);
                return ExactUnitCountFor(player.Id,rawcode,false);
            }
            _invalidPlayerUnitTypeQueries++;
            return 0;
        }

        int ExactUnitCountFor(int playerId,string rawcode,bool done)
        {
            var key=(playerId,rawcode,done);
            long version=_owner._world.UnitVersion;
            if(_exactCountCache.TryGetValue(key,out var entry)
                &&entry.Version==version)return entry.Count;
            int count=0;
            foreach(Unit unit in _owner._world.UnitsForPlayer(playerId))
                if(!unit.Removed&&unit.Alive&&unit.PlayerId==playerId
                    &&unit.TypeId==rawcode
                    &&(!done||unit.Abilities.Get<ConstructionProgress>()==null))count++;
            _exactCountCache[key]=(version,count);
            return count;
        }
        int UnitInt(int typeId,string field)=>Math.Max(0,UnitData(typeId)?.GetInt(field,0)??0);
        UnitTypeData UnitData(long typeId)=>_owner._objects?.Get(TypeRawcode((int)typeId));
        OwnedUnits Owned()=>new(_owner._world.UnitsForPlayer(_player.Id),_player.Id);

        // Value enumeration preserves List's mutation/version checks and live filters.
        readonly struct OwnedUnits : IEnumerable<Unit>
        {
            readonly List<Unit> _units;
            readonly int _player;
            readonly Func<Unit,bool> _filter;
            internal OwnedUnits(List<Unit> units,int player,Func<Unit,bool> filter=null)
            { _units=units;_player=player;_filter=filter; }
            public Enumerator GetEnumerator()=>new(_units.GetEnumerator(),_player,_filter);
            IEnumerator<Unit> IEnumerable<Unit>.GetEnumerator()=>GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
            public OwnedUnits Where(Func<Unit,bool> predicate)
            {
                ArgumentNullException.ThrowIfNull(predicate);
                if(_filter==null)return new(_units,_player,predicate);
                return new(_units,_player,CombineFilters(_filter,predicate));
            }
            static Func<Unit,bool> CombineFilters(Func<Unit,bool> first,Func<Unit,bool> second)
                =>unit=>first(unit)&&second(unit);
            public int Count(){int count=0;foreach(Unit unit in this)count++;return count;}
            public int Count(Func<Unit,bool> predicate)
            { ArgumentNullException.ThrowIfNull(predicate);int count=0;foreach(Unit unit in this)if(predicate(unit))count++;return count; }
            public bool Any(){foreach(Unit unit in this)return true;return false;}
            public bool Any(Func<Unit,bool> predicate)
            { ArgumentNullException.ThrowIfNull(predicate);foreach(Unit unit in this)if(predicate(unit))return true;return false; }
            public Unit FirstOrDefault(){foreach(Unit unit in this)return unit;return null;}
            public Unit FirstOrDefault(Func<Unit,bool> predicate)
            { ArgumentNullException.ThrowIfNull(predicate);foreach(Unit unit in this)if(predicate(unit))return unit;return null; }
            public struct Enumerator : IEnumerator<Unit>
            {
                List<Unit>.Enumerator _source;
                readonly int _player;
                readonly Func<Unit,bool> _filter;
                internal Enumerator(List<Unit>.Enumerator source,int player,Func<Unit,bool> filter)
                { _source=source;_player=player;_filter=filter; }
                public Unit Current=>_source.Current;
                object System.Collections.IEnumerator.Current=>Current;
                public bool MoveNext()
                {
                    while(_source.MoveNext())
                    {
                        Unit unit=_source.Current;
                        if(!unit.Removed&&unit.Alive&&unit.PlayerId==_player
                            &&(_filter==null||_filter(unit)))return true;
                    }
                    return false;
                }
                public void Dispose()=>_source.Dispose();
                public void Reset()=>throw new NotSupportedException();
            }
        }

        OwnedUnits CombatUnits()=>new(_owner._world.UnitsForPlayer(_player.Id),_player.Id,IsCombatUnit);
        bool IsCombatUnit(Unit unit)=>!unit.IsBuilding&&unit.Attack!=null
            &&!IsPrimaryWorker(unit)&&!_harvesters.ContainsKey(unit);
        static readonly HashSet<string> PrimaryWorkerTypes=new(
            StringComparer.OrdinalIgnoreCase){"hpea","opeo","uaco","ewsp"};
        static bool IsPrimaryWorker(Unit unit)
            =>unit?.TypeId!=null&&PrimaryWorkerTypes.Contains(unit.TypeId);
        Unit FirstCombatUnit(){foreach(Unit unit in CombatUnits())return unit;return null;}
        Unit Hero()
        {
            if(_skillHero!=null)return _skillHero;
            foreach(Unit unit in Owned())if(unit.IsHero)return unit;
            return null;
        }
        Unit Worker()
        {
            foreach(Unit unit in Owned())if(!unit.IsBuilding&&unit.CanMove&&!unit.IsHero)return unit;
            return null;
        }
        bool ScriptGroupContains(string name,Unit unit)=>unit!=null
            &&_vm.TryGetScalarGlobal(name,out JassValue value)
            &&value is JassHandle handle
            &&_vm.GetHandleObject<GroupRef>(handle)?.Contains(unit)==true;
        bool IsAmaiBuyingUnit(Unit unit)=>
            ScriptGroupContains("unit_buying_item",unit)
            ||ScriptGroupContains("unit_buying_merc",unit)
            ||ScriptGroupContains("unit_buying_tavern",unit);
        bool IsAmaiTowerBuilder(Unit unit)
        {
            if(unit==null||!AiBoolean("towerrush"))return false;
            int count=(int)Math.Clamp(AiInteger("race_tower_peon"),0,9);
            try
            {
                for(int i=1;i<=count;i++)
                    if(_vm.GetGlobal("builder",i) is JassHandle handle
                        &&_vm.TryGetHandleObject(handle,out Unit builder)
                        &&ReferenceEquals(builder,unit))return true;
            }
            catch(JassVmException){ }
            return false;
        }
        Unit Builder()=>SelectBuilder(null);
        Unit Builder(string rawcode)=>SelectBuilder(rawcode);
        Unit SelectBuilder(string rawcode)
        {
            Unit best=null;int bestRank=int.MaxValue;float bestDistance=float.PositiveInfinity;
            Vec2 home=Home();
            foreach(Unit unit in Owned())
            {
                if(unit.IsBuilding||!unit.CanMove||unit.IsHero||IsAmaiBuyingUnit(unit)||IsAmaiTowerBuilder(unit)
                    ||unit.Current is BuildOrder or ConstructionWorkOrder
                    ||rawcode!=null&&!CanBuild(unit,rawcode))continue;
                int rank=_harvesters.ContainsKey(unit)?1:0;
                float distance=Vec2.Dist(unit.Pos,home);
                if(best==null||rank<bestRank||rank==bestRank&&distance.CompareTo(bestDistance)<0)
                {best=unit;bestRank=rank;bestDistance=distance;}
            }
            return best;
        }
        bool CanBuild(Unit unit,string rawcode)
        {
            if(unit?.GetAbility<RuntimeMilitiaAbility>()?.IsMilitia==true)return false;
            foreach(string id in Split(_owner._objects?.Get(unit?.TypeId)?.GetString("Builds","")??""))
                if(string.Equals(id,rawcode,StringComparison.OrdinalIgnoreCase))return true;
            return false;
        }
        Vec2 Home()
        {
            Vec2? configured=_owner.GetPlayerStartLocationPosition(_player.Id);
            if(configured.HasValue)return configured.Value;
            Unit first=null;
            foreach(Unit unit in _owner._world.UnitsForPlayer(_player.Id))
            {
                if(unit.PlayerId!=_player.Id||!unit.Alive||unit.Removed)continue;
                first??=unit;
                if(unit.IsBuilding)return unit.Pos;
            }
            return first?.Pos??default;
        }
        Vec2 CaptainHome()=>_hasCaptainHome?_captainHome:Home();
        Vec2 DefenseCaptainHome()=>_hasDefenseCaptainHome?_defenseCaptainHome:Home();
        void SetCaptainHome(int mask,Vec2 home)
        {
            // FUN_6f89fcb0: attack bit 1, defense bit 2, in that order.
            if((mask&1)!=0){_captainHome=home;_hasCaptainHome=true;}
            if((mask&2)!=0){_defenseCaptainHome=home;_hasDefenseCaptainHome=true;}
        }
        // Game.dll 1.27 AI_GetEnemyPower (0x6f88fb90) is a literal stub.
        // Keep parity instead of inventing a unit-count scale that callers could
        // incorrectly compare with the AI script's weighted strength values.
        int EnemyPower()=>0;
        Unit FindEnemy(bool building=false)
        {
            Vec2 home=Home();
            Unit nearest=null;
            float nearestDistance=float.PositiveInfinity;
            foreach(Unit unit in _owner._world.Units)
            {
                if(!unit.Alive||unit.Removed||unit.Hidden
                    ||unit.PlayerId is <0 or >=12
                    ||unit.PlayerId==_player.Id
                    ||_owner.PlayerAllied(_player.Id,unit.PlayerId)
                    ||building&&!unit.IsBuilding)continue;
                float distance=Vec2.Dist(unit.Pos,home);
                if(!(distance<nearestDistance))continue;
                nearest=unit;
                nearestDistance=distance;
            }
            return nearest;
        }
        Unit PlayerBuilding(int playerId)
        {
            foreach(Unit unit in _owner._world.UnitsForPlayer(playerId))
                if(!unit.Removed&&unit.Alive&&unit.IsBuilding
                    &&unit.Abilities.Get<ConstructionProgress>()==null
                    &&unit.Abilities.Get<RuntimeGoldMineDepletion>()==null)return unit;
            return null;
        }

        // StartGetEnemyBase in Game.dll builds per-player candidate slots and
        // resolves two best handles asynchronously; it is not a nearest-arbitrary-
        // structure query. The current candidate policy retains its stable base
        // preference: completed town hall at the player's main, then another
        // completed structure, and only then a surviving unit after base loss.
        Unit FindEnemyBase()
        {
            Vec2 origin=CaptainAnchor()?.Pos??CaptainHome();
            Unit best=null;
            float bestDistance=float.PositiveInfinity;
            for(int playerId=0;playerId<12;playerId++)
            {
                if(playerId==_player.Id||_owner.PlayerAllied(_player.Id,playerId))
                    continue;
                Vec2? start=_owner.GetPlayerStartLocationPosition(playerId);
                Unit candidate=null;int bestRank=int.MaxValue;float candidateDistance=float.PositiveInfinity;
                foreach(Unit unit in _owner._world.UnitsForPlayer(playerId))
                {
                    if(unit.Removed||!unit.Alive||unit.Hidden)continue;
                    bool complete=unit.IsBuilding&&unit.Abilities.Get<ConstructionProgress>()==null;
                    int rank=complete?(unit.Identity.Is(Classification.TownHall)?0:1):2;
                    float baseDistance=rank<2&&start.HasValue?Vec2.Dist(unit.Pos,start.Value):0;
                    int comparison=baseDistance.CompareTo(candidateDistance);
                    if(candidate==null||rank<bestRank||rank==bestRank
                        &&(comparison<0||comparison==0&&unit.SimulationId<candidate.SimulationId))
                    {candidate=unit;bestRank=rank;candidateDistance=baseDistance;}
                }
                if(candidate==null)continue;
                float distance=Vec2.Dist(origin,candidate.Pos);
                if(distance>=bestDistance)continue;
                best=candidate;
                bestDistance=distance;
            }
            return best;
        }
        void StepEnemyBaseSearch(float deltaTime)
        {
            if(!_enemyBaseSearchPending)return;
            _enemyBaseSearchRemaining-=deltaTime;
            if(_enemyBaseSearchRemaining>0)return;
            // 89f5f0 publishes results during scheduled work, and only clears
            // pending when its candidate list is drained. Native candidate and
            // incremental path-budget selection still need full restoration.
            _enemyBase=FindEnemyBase();
            _enemyBaseSearchPending=false;
        }
        Unit _megaTarget;
        double _megaTargetUpdateRemaining=10d;
        void StepMegaTargetReference(float deltaTime)
        {
            // CCompAI reset 89ca20 arms periodic event D01C3 on timer +94
            // with DAT_6fbb81fc = 10 seconds. The getter remains a weak read.
            _megaTargetUpdateRemaining-=Math.Max(0d,deltaTime);
            while(_megaTargetUpdateRemaining<=0d)
            {
                _megaTargetUpdateRemaining+=10d;
                UpdateMegaTargetReference();
            }
        }
        Unit MegaTargetReference()=>_megaTarget is {Removed:false} target?target:null;
        // 89ad00 clears/rebuilds the weak reference during AI maintenance;
        // GetMegaTarget itself never runs the power census.
        void UpdateMegaTargetReference()=>_megaTarget=FindMegaTarget();

        Unit FindMegaTarget()
        {
            if(!_flags.GetValueOrDefault("SetWatchMegaTargets"))return null;
            // Game.dll 1.27 FUN_6f89ad00 rebuilds the mega target from a
            // whole-map power census. It requires own power > 19, allied power
            // greater than total enemy power, and own power greater than twice
            // the selected enemy player's power. GetMegaTarget itself only
            // reads the maintained agent reference at CCompAI +0x2c4.
            int ownPower=MegaPlayerPower(_player.Id);
            // Game.dll's alliance census includes the AI player's own force.
            // Starting at zero makes GetMegaTarget impossible in a 1v1 because
            // there are no other allied players whose power could beat the
            // enemy total.
            int alliedPower=ownPower,enemyPower=0;
            Unit selected=null;
            for(int playerId=0;playerId<12;playerId++)
            {
                if(playerId==_player.Id)continue;
                bool allied=_owner.PlayerAllied(_player.Id,playerId);
                int power=MegaPlayerCensus(playerId,!allied&&selected==null,out Unit target);
                if(allied)
                {
                    alliedPower+=power;
                    continue;
                }
                enemyPower+=power;
                if(selected==null&&target!=null&&ownPower>power*2)selected=target;
            }
            if(ownPower<=19||alliedPower<=enemyPower)selected=null;
            if(CombatBehaviorLog.Enabled)
            {
                string decision=$"own={ownPower} allied={alliedPower} enemy={enemyPower} "
                    +$"target={(selected==null?"none":selected.PlayerId+"/"+selected.TypeId)}";
                if(!decision.Equals(_lastMegaDecision,StringComparison.Ordinal))
                {
                    _lastMegaDecision=decision;
                    CombatBehaviorLog.Write($"[ai-order] t={_owner._world.Tick} "
                        +$"P{_player.Id} site=mega-target {decision}");
                }
            }
            return selected;
        }
        int MegaPlayerPower(int playerId)
        {
            int power=0;
            foreach(Unit unit in _owner._world.UnitsForPlayer(playerId))
                if(unit.Alive&&!unit.Removed&&!unit.Hidden)power=checked(power+MegaUnitPower(unit));
            return power;
        }

        // Read the live force at each upkeep call. AI scripts can change units
        // between players' updates, so a tick-wide census cache would be stale.
        int MegaPlayerCensus(int playerId,bool findTarget,out Unit target)
        {
            target=null;
            if(!findTarget)return MegaPlayerPower(playerId);
            int power=0,bestRank=int.MaxValue;
            foreach(Unit unit in _owner._world.UnitsForPlayer(playerId))
            {
                if(!unit.Alive||unit.Removed)continue;
                if(!unit.Hidden)power=checked(power+MegaUnitPower(unit));
                if(!unit.IsBuilding)continue;
                bool hall=unit.Identity.Is(Classification.TownHall);
                if(!hall&&!unit.TypeId.Equals(GoldMineIdentity.HauntedType,StringComparison.OrdinalIgnoreCase))continue;
                int rank=hall?0:1;
                if(target==null||rank<bestRank||rank==bestRank&&unit.SimulationId<target.SimulationId)
                {target=unit;bestRank=rank;}
            }
            return power;
        }
        int MegaUnitPower(Unit unit)
        {
            if(unit.IsBuilding)
            {
                string type=(unit.TypeId??"").ToLowerInvariant();
                return type switch
                {
                    "owtw" or "hgtw" or "etrp" or "hctw"=>3,
                    "otrb"=>1,
                    "unp1" or "unp2"=>4,
                    "uzg1"=>3,
                    _=>(Math.Max(1,unit.TypeLevel)+1)/2+4,
                };
            }
            UnitTypeData data=_owner._objects?.Get(unit.TypeId);
            return Math.Max(0,data?.GetInt("fused",0)??0);
        }
        string _creepQueryDebug="none";
        Unit FindCreep(int min,int max,bool flyersOk)
        {
            // FUN_6f88f940 probes distance anew on every GetCreepCamp call.
            // Scripted teleports and obstacle changes can occur in one tick.
            _creepPathDistances.Clear();
            List<Unit> creeps=_creepCandidates;
            creeps.Clear();
            foreach(Unit unit in _owner._world.Units)
                if(IsAttackableCreep(unit))creeps.Add(unit);
            creeps.Sort(static(left,right)=>
                left.SimulationId.CompareTo(right.SimulationId));
            HashSet<Unit> assigned=_creepAssigned;
            assigned.Clear();
            List<Unit> members=_creepCampMembers;
            var selection=new NativeCreepQueryPolicy<Unit>(min,max,flyersOk);
            int camps=0,rejectLevel=0,rejectAir=0,rejectPath=0,deferred=0;
            // Searches below do not advance simulation or mutate units. Share
            // one query-local army snapshot; never cache it across native calls.
            Unit pathProbe=CaptainAnchor()??Hero()??FirstCombatUnit()??Worker();
            HashSet<Unit> pathIgnore=_creepPathIgnore;
            pathIgnore.Clear();
            foreach(Unit unit in CombatUnits())
                if(unit.Alive&&!unit.Removed)pathIgnore.Add(unit);
            foreach(Unit seed in creeps)
            {
                if(assigned.Contains(seed))continue;
                if(IsDeferredCreepCamp(seed)){deferred++;continue;}
                camps++;
                Vec2 center=CreepGuardPoint(seed);
                members.Clear();
                int strength=0;
                bool hasFlyer=false;
                foreach(Unit unit in creeps)
                {
                    if(Vec2.Dist(CreepGuardPoint(unit),center)
                        >GameplayConstants.CreepCampRadius)continue;
                    members.Add(unit);
                    assigned.Add(unit);
                    // 88ff40 sums unit levels, doubling active flyers. 667a20
                    // reads the current hero level rather than its type level.
                    bool flying=IsFlyingCreep(unit);
                    int level=unit.GetAbility<Hero>()?.HeroLevel??Math.Max(0,unit.TypeLevel);
                    strength=unchecked(strength+(flying?unchecked(level*2):level));
                    hasFlyer|=flying;
                }
                // Native distance probing precedes strength/air filtering, so even
                // rejected camps participate in query-local budget tightening.
                Unit target=members.Count>0?members[0]:null;
                if(target==null){rejectPath++;continue;}
                bool added=pathIgnore.Add(target);
                uint targetDistance;
                int expanded;
                try { targetDistance=CreepPathDistance(target,pathProbe,pathIgnore,selection.Budget,out expanded); }
                finally { if(added)pathIgnore.Remove(target); }
                if(targetDistance>=999999)rejectPath++;
                if(strength<min||strength>max)rejectLevel++;
                if(!flyersOk&&hasFlyer)rejectAir++;
                selection.Consider(target,targetDistance,expanded,strength,hasFlyer);
            }
            if(CombatBehaviorLog.Enabled)
            {
                int infinite=0;foreach(var pair in _creepPathDistances)if(float.IsPositiveInfinity(pair.Value))infinite++;
                _creepQueryDebug=$"range={min}..{max},air={flyersOk},units={creeps.Count},camps={camps},level={rejectLevel},airReject={rejectAir},pathReject={rejectPath},deferred={deferred},cacheInf={infinite},target={selection.Target?.SimulationId},budget={selection.Budget}";
            }
            return selection.Target;
        }

        static Vec2 CreepGuardPoint(Unit unit)=>unit.Ai.IsCreepGuard
            ?unit.Ai.GuardPosition:unit.Pos;
        static bool IsFlyingCreep(Unit unit)=>unit.MovementType==MovementType.Fly
            ||(unit.TargetType&TargetFlags.Air)!=0;

        uint CreepPathDistance(Unit creep,Unit probe,HashSet<Unit> ignore,int budget,out int expanded)
        {
            expanded=0;
            if(creep==null)return 999999;
            Vec2 origin=probe?.Pos??CaptainHome();
            if(probe==null||probe.World?.PathingMap==null)
                return (uint)(Vec2.Dist(origin,creep.Pos)/32f);
            // 88f940 -> 03c310 -> 10ef30: direct fine-distance query, initially
            // 5000 iterations, using captain SmartPosition. No coarse-path veto.
            uint distance=probe.World.CLrPathingAcc.MeasureLeafDistance(origin,creep.Pos,
                probe.MovementType,probe.CollisionSize,probe,budget,out _,ignore);
            expanded=probe.World.CLrPathingAcc.LastDistanceExpandedNodes;
            bool reachable=distance!=CLrPathingAcc.LeafDistanceUnreachable;
            _creepPathDistances[creep]=reachable?distance:float.PositiveInfinity;
            return reachable?distance:999999;
        }


        bool AddCount(Dictionary<int,int> target,int id,int count)
        {target[id]=target.GetValueOrDefault(id)+Math.Max(0,count);return true;}

        bool AddAssaultUnits(int typeId,int count)
        {
            if(count<=0)return true;
            AllowExplicitGuardRecruitment(typeId);
            // AMAI uses exactly 60 as an "all existing units of this family"
            // recycle sentinel (ResetGuardPositionOnlyJob and desperation
            // assault). It is not a persistent formation quota: treating it as
            // one makes CaptainIsFull permanently false for impossible armies
            // such as 60 Riflemen.
            const int AmaiExistingFamilySentinel=60;
            bool addExistingFamily=count==AmaiExistingFamilySentinel;
            bool hadDesired=_assault.TryGetValue(typeId,out int previousDesired);
            // FUN_6f898450 receives one mutable remaining quantity for this
            // call. Units already in the captain satisfy it; repeated calls
            // for the same rawcode do not accumulate a second requirement.
            _assault[typeId]=Math.Max(_assault.GetValueOrDefault(typeId),count);

            // Game.dll FUN_6f897c80 -> FUN_6f898450 selects matching units at
            // AddAssault time.  If the captain is already in state 2, the
            // selected unit receives the captain's current task immediately.
            // Merely storing the desired count leaves combat reinforcements
            // idle at home until some unrelated readiness query refreshes them.
            var existing=new HashSet<Unit>(_captainMembers);
            RefreshCaptainFormation();
            if(addExistingFamily)
            {
                if(hadDesired)_assault[typeId]=previousDesired;
                else _assault.Remove(typeId);
            }
            IReadOnlySet<string> requestedFamily=_owner.UnitAvailabilityFamily(
                TypeRawcode(typeId));
            int available=0;foreach(Unit unit in _captainMembers)if(requestedFamily.Contains(unit.TypeId))available++;
            // 8981d0's second pass consumes a requested slot for an injured
            // candidate under IgnoreInjured, without attaching that unit.
            if(_flags.GetValueOrDefault("SetIgnoreInjured"))
                available+=Owned().Count(unit=>IsNewCaptainFormationCandidate(unit,requestedFamily)
                    &&IgnoreInjuredForFormation(unit));
            bool satisfied=addExistingFamily||available>=count;
            if(!satisfied)
            {
                _captainIsFull=false;
                if(CombatBehaviorLog.Enabled)
                    CombatBehaviorLog.Write($"[ai-order] t={_owner._world.Tick} "
                        +$"P{_player.Id} site=add-assault short={TypeRawcode(typeId)}"
                        +$"/{count} have={available} "
                        +$"food={_owner.CurrentFoodUsed(_player.Id)}");
            }
            if(!_captainDispatched||_captainState!=2||_captainRetreating
                ||_captainReturningHome)return satisfied;
            var recruits=new List<Unit>();
            foreach(Unit unit in _captainMembers)if(!existing.Contains(unit))recruits.Add(unit);
            if(recruits.Count==0)return satisfied;
            Unit target=ValidCaptainAttackTarget();
            if(target!=null)
                IssueSmartCaptainAttackMoveKill(recruits,target);
            else if(_captainPointTargetSet)
                _owner._world.Movement.IssueCaptainAttackMove(recruits,
                    _captainPointTarget);
            if(CombatBehaviorLog.Enabled)
                foreach(Unit recruit in recruits)
                    CombatBehaviorLog.Write($"[captain-recruit-order] "
                        +$"t={_owner._world.Tick} P{_player.Id} "
                        +$"unit={recruit.SimulationId}/{recruit.TypeId} "
                        +$"target={(target==null?"point":$"{target.SimulationId}/{target.TypeId}")} "
                        +$"order={recruit.Current?.GetType().Name??"idle"}");
            return satisfied;
        }
    }
}

*/
