//重构情况：从 Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Jass/JassGameNatives.Production.cs 转换（骨架，方法体待补）
namespace OpenWC3.JassToSharp.Ai.Economy;

public static class Production
{
    public static int Needed(int target, int have, int pending) =>
        Math.Max(0, target - have - pending);

    public static int Affordable(int gold, int lumber, int costGold, int costLumber, int wanted)
    {
        if (wanted <= 0) return 0;
        int count = wanted;
        if (costGold > 0) count = Math.Min(count, gold / costGold);
        if (costLumber > 0) count = Math.Min(count, lumber / costLumber);
        return Math.Max(0, count);
    }

    public static bool CanStart(int needed, int affordable, bool requirementsMet, bool hasBuilder) =>
        needed > 0 && affordable > 0 && requirementsMet && hasBuilder;

    public static int Spend(int budget, int cost, int count) =>
        cost <= 0 ? budget : Math.Max(0, budget - cost * count);
}

/*
原文（JassGameNatives.Production.cs，1107 行）：

using OpenWC3.War3Formats.Jass;

namespace OpenWC3.Game;

public sealed partial class JassGameNatives
{
    float ConstructionDuration(int playerId,float duration)
    {
        float cheat=CheatConstructionSpeedMultiplier(playerId);
        return Math.Max(World.TickSeconds,duration/Math.Max(1f,cheat));
    }

    float UnitTrainingDuration(int playerId,float duration)
    {
        float cheat=CheatConstructionSpeedMultiplier(playerId);
        return Math.Max(World.TickSeconds,duration/Math.Max(1f,cheat));
    }

    static bool IsMeleeTownHallTier(string rawcode)=>rawcode?.ToLowerInvariant()
        is "hkee" or "hcas" or "ostr" or "ofrt" or "unp1" or "unp2"
            or "etoa" or "etoe";

    public bool IssuePlayerTrain(Unit producer,string rawcode,out string error)
        =>IssuePlayerTrain(producer?.PlayerId??-1,producer,rawcode,out error);

    public int IssuePlayerTrain(int requestingPlayerId,IReadOnlyList<Unit> producers,
        string rawcode,out string error)
    {
        error=null;
        int started=0;
        foreach(Unit producer in producers??Array.Empty<Unit>())
        {
            if(IssuePlayerTrain(requestingPlayerId,producer,rawcode,out string currentError))
            {
                started++;
                continue;
            }
            error=currentError;
            if(currentError is "金币不足。" or "木材不足。" or "食物不足。")break;
        }
        return started;
    }

    public bool IssuePlayerTrain(int requestingPlayerId,Unit producer,string rawcode,
        out string error)
    {
        error=null;
        UnitTypeData data=_objects?.Get(rawcode);
        PlayerRef player=Player(producer?.PlayerId??-1);
        if(producer?.Alive!=true||producer.Removed
            ||!CanPlayerControl(requestingPlayerId,producer,true)||!producer.IsBuilding
            ||producer.Abilities.Get<ConstructionProgress>()!=null||data==null
            ||!SplitRawcodes(_objects?.Get(producer.TypeId)?.GetString("Trains","")??"")
                .Contains(rawcode,StringComparer.OrdinalIgnoreCase))
        {
            error="该建筑不能训练这个单位。";
            return false;
        }
        RuntimeProductionQueueAbility queue=
            producer.Abilities.Get<RuntimeProductionQueueAbility>();
        if(queue?.Count>=7)
        {
            error="训练队列已满。";
            return false;
        }
        if(!MeetsStockRequirements(player,data)||!IsUnitAvailableForPlayer(player.Id,rawcode))
        {
            error="尚未满足训练条件。";
            return false;
        }
        int typeId=FourCC(rawcode);
        if(!CanTrainUnitWithinLimit(player,rawcode,out error))return false;
        int food=Math.Max(0,data.GetInt("fused",0));
        int foodUsed=CurrentFoodUsed(player.Id);
        int foodCap=CurrentFoodCap(player.Id);
        const bool ignoresFood=false;
        if(!ignoresFood&&food>0&&(foodUsed+food>foodCap
                ||WouldConsumeReservedMeleeHeroFood(player,typeId,food,
                    foodUsed,foodCap)))
        {
            error="食物不足。";
            return false;
        }
        bool heroToken=IsHeroUnitId(typeId)&&player.States.GetValueOrDefault(3)>0;
        int gold=heroToken?0:Math.Max(0,data.GetInt("goldcost",0));
        int lumber=heroToken?0:Math.Max(0,data.GetInt("lumbercost",0));
        if(!TrySpendResources(player.Id,gold,lumber))
        {
            error=player.States.GetValueOrDefault(1)<gold?"金币不足。":"木材不足。";
            return false;
        }
        queue??=AddProductionQueue(producer);
        if(heroToken)AdjustHeroTrainingTokens(player,-1);
        bool resolved=false;
        Unit finishedUnit=null;
        void Resolve(bool completed)
        {
            if(resolved)return;
            resolved=true;
            if(completed&&finishedUnit!=null)
                return;
            RefundResources(player.Id,gold,lumber);
            if(heroToken)AdjustHeroTrainingTokens(player,1);
            FireTrainingEvent(GameEvent.UnitTrainCancel,producer,null,typeId);
        }
        var job=new RuntimeProductionQueueAbility.Job(rawcode,
            UnitTrainingDuration(producer.PlayerId,data.GetFloat("bldtm",1f)),
            ()=>FireTrainingEvent(GameEvent.UnitTrainStart,producer,null,typeId),Resolve,
            ()=>ignoresFood||food==0||CurrentFoodUsed(player.Id)+food
                <=CurrentFoodCap(player.Id)
                &&!WouldConsumeReservedMeleeHeroFood(player,typeId,food,
                    CurrentFoodUsed(player.Id),CurrentFoodCap(player.Id)),food,
            tryComplete:()=> (finishedUnit=TryFinishTraining(producer,player,rawcode,typeId))!=null);
        if(queue.Enqueue(job))return true;
        Resolve(false);
        error="训练队列已满。";
        return false;
    }

    void AdjustHeroTrainingTokens(PlayerRef player,int delta)
    {
        player.States[3]=unchecked(player.States.GetValueOrDefault(3)+delta);
        FirePlayerStateEvents(player,3);
        _world.Events.Fire(new EventContext { Event=GameEvent.PlayerStateChanged,
            TriggerPlayerId=player.Id,PlayerStateId=3 });
    }

    bool CanTrainUnitWithinLimit(PlayerRef player,string rawcode,out string error)
    {
        error=null;
        int typeId=FourCC(rawcode);
        bool hero=IsHeroUnitId(typeId);
        if(hero&&TryGetHeroLimit(player,out int heroLimit)
            &&MeleeHeroCount(player.Id)>=heroLimit)
        {
            error=$"最多允许 {heroLimit} 名英雄。";
            return false;
        }
        // Campaign InitTechTree uses this for every producible type, not just
        // heroes: zero disables a command and positive values are a hard cap.
        if(!player.TechMaxAllowed.TryGetValue(typeId,out int maximum))return true;
        int owned=0,queued=0;
        foreach(Unit unit in _world.UnitsForPlayerInWorldOrder(player.Id))
        {
            if(unit.PlayerId!=player.Id)continue;
            if(!unit.Removed&&FourCC(unit.TypeId)==typeId)owned++;
            // The previous query included a removed producer's pending jobs
            // until world cleanup. Preserve that distinction from owned units.
            var jobs=unit.Abilities.Get<RuntimeProductionQueueAbility>()?.Jobs;
            if(jobs==null)continue;
            for(int index=0;index<jobs.Count;index++)
                if(jobs[index].Rawcode.Equals(rawcode,StringComparison.OrdinalIgnoreCase))queued++;
        }
        if((uint)(owned+queued)<(uint)maximum)return true;
        error=maximum==0?"战役中不能生产该单位。":"已达到该单位的数量上限。";
        return false;
    }

    /// <summary>Command visibility; requirements remain a separate disabled-state check.</summary>
    public bool CanPlayerTrainCommand(int playerId,Unit producer,string rawcode)
    {
        UnitTypeData data=_objects?.Get(rawcode);
        PlayerRef player=Player(producer?.PlayerId??playerId);
        if(producer?.Alive!=true||producer.Removed||!CanPlayerControl(playerId,producer,true)
            ||producer.Abilities.Get<ConstructionProgress>()!=null||data==null
            ||!SplitRawcodes(_objects?.Get(producer.TypeId)?.GetString("Trains","")??"")
                .Contains(rawcode,StringComparer.OrdinalIgnoreCase)
            ||!IsUnitAvailableForPlayer(player.Id,rawcode))return false;
        return CanTrainUnitWithinLimit(player,rawcode,out _);
    }

    // Native CAbilityQueue +0x308 controls visibility; +0x304 separately
    // controls requirements/disabled state. A reservation hides every copy.
    public bool IsPlayerResearchCommandVisible(int playerId,string rawcode)
    {
        UpgradeTypeData data=_objects?.GetUpgrade(rawcode);
        PlayerRef player=Player(playerId);
        int typeId=FourCC(rawcode);
        int maximum=Math.Max(1,data?.GetInt("maxlevel",0,0,1)??1);
        if(player.TechMaxAllowed.TryGetValue(typeId,out int allowed)&&allowed>=0)
            maximum=Math.Min(maximum,allowed);
        return data!=null&&player.Tech.GetValueOrDefault(typeId)<maximum
            &&!HasPlayerResearchReservation(playerId,rawcode);
    }

    bool HasPlayerResearchReservation(int playerId,string rawcode)
    {
        foreach(Unit unit in _world.UnitsForPlayerInWorldOrder(playerId))
        {
            if(unit.Removed||unit.PlayerId!=playerId)continue;
            var jobs=unit.Abilities.Get<RuntimeProductionQueueAbility>()?.Jobs;
            if(jobs==null)continue;
            for(int index=0;index<jobs.Count;index++)
            {
                var job=jobs[index];
                if(job.IsResearch&&job.Rawcode.Equals(rawcode,StringComparison.OrdinalIgnoreCase))return true;
            }
        }
        return false;
    }

    bool WouldConsumeReservedMeleeHeroFood(PlayerRef player,int requestedTypeId,
        int requestedFood,int foodUsed,int foodCap)
    {
        if(player?.Controller!=1||requestedFood<=0||IsHeroUnitId(requestedTypeId))
            return false;
        int tierLimit=TryGetHeroLimit(player,out int configuredLimit)
            ?configuredLimit:int.MaxValue;
        int heroSlotsCommitted=0;
        IReadOnlySet<string> heroTypes=TechDependencyTypes("HERO");
        foreach(Unit unit in _world.UnitsForPlayerInWorldOrder(player.Id))
            if(!unit.Removed&&unit.PlayerId==player.Id&&heroTypes.Contains(unit.TypeId)&&!unit.IsIllusion)
                heroSlotsCommitted++;
        foreach(Unit unit in _world.UnitsForPlayerInWorldOrder(player.Id))
        {
            if(unit.Removed||unit.PlayerId!=player.Id)continue;
            var jobs=unit.Abilities.Get<RuntimeProductionQueueAbility>()?.Jobs;
            if(jobs==null)continue;
            for(int index=0;index<jobs.Count;index++)
                if(jobs[index].HasStarted&&heroTypes.Contains(jobs[index].Rawcode))heroSlotsCommitted++;
        }
        if(heroSlotsCommitted>=tierLimit)return false;
        Unit altar=null;
        foreach(Unit unit in _world.UnitsForPlayerInWorldOrder(player.Id))
            if(!unit.Removed&&unit.Alive&&unit.PlayerId==player.Id&&unit.IsBuilding
                &&unit.Abilities.Get<ConstructionProgress>()==null
                &&(unit.TypeId.Equals("halt",StringComparison.OrdinalIgnoreCase)
                    ||unit.TypeId.Equals("oalt",StringComparison.OrdinalIgnoreCase)
                    ||unit.TypeId.Equals("uaod",StringComparison.OrdinalIgnoreCase)
                    ||unit.TypeId.Equals("eate",StringComparison.OrdinalIgnoreCase)))
            { altar=unit;break; }
        if(altar==null)return false;
        int heroFood=int.MaxValue;
        foreach(string rawcode in SplitRawcodes(_objects?.Get(altar.TypeId)?.GetString("Trains","")??""))
            if(IsHeroUnitId(FourCC(rawcode)))
            {
                int value=Math.Max(0,_objects?.Get(rawcode)?.GetInt("fused",0)??0);
                if(value>0)heroFood=Math.Min(heroFood,value);
            }
        if(heroFood==int.MaxValue)heroFood=5;
        return ShouldReserveMeleeHeroFood(foodUsed,foodCap,requestedFood,heroFood,
            computer:true,completedAltar:true,heroSlotsCommitted,tierLimit,
            requestedHero:false);
    }

    internal static bool ShouldReserveMeleeHeroFood(int foodUsed,int foodCap,
        int requestedFood,int heroFood,bool computer,bool completedAltar,
        int heroSlotsCommitted,int heroTierLimit,bool requestedHero)
        =>computer&&completedAltar&&heroSlotsCommitted<heroTierLimit
            &&!requestedHero&&requestedFood>0&&heroFood>0
            &&foodUsed+requestedFood>foodCap-heroFood;

    int MeleeHeroCount(int playerId)
    {
        // Game.dll 215f40 with flags 0xb: completed + training counts,
        // recursively including HERO.DependencyOr. Hero classification and
        // hideHeroBar do not determine membership in this technology group.
        IReadOnlySet<string> heroTypes=TechDependencyTypes("HERO");
        int existing=0;
        foreach(Unit unit in _world.UnitsForPlayerInWorldOrder(playerId))
            if(!unit.Removed&&unit.PlayerId==playerId
                &&heroTypes.Contains(unit.TypeId)&&!unit.IsIllusion)existing++;
        int queued=0;
        foreach(Unit producer in _world.UnitsForPlayerInWorldOrder(playerId))
        {
            if(producer.Removed||producer.PlayerId!=playerId)continue;
            var jobs=producer.Abilities.Get<RuntimeProductionQueueAbility>()?.Jobs;
            if(jobs==null)continue;
            for(int index=0;index<jobs.Count;index++)
            {
                var job=jobs[index];
                if(!heroTypes.Contains(job.Rawcode))continue;
                bool reviveJob=false;
                foreach(Unit hero in _world.UnitsForPlayerInWorldOrder(playerId))
                    if(!hero.Removed&&hero.PlayerId==playerId
                        &&hero.TypeId.Equals(job.Rawcode,StringComparison.OrdinalIgnoreCase)
                        &&hero.Abilities.Get<Hero>() is { Reviving:true } revive
                        &&ReferenceEquals(revive.Altar,producer))
                    { reviveJob=true;break; }
                if(!reviveJob)queued++;
            }
        }
        return existing+queued;
    }

    sealed class TechDependencyTypeState
    {
        internal readonly HashSet<string> Types=new(StringComparer.Ordinal);
        internal readonly List<(string Rawcode,string Dependencies)> Sources=new();
        internal bool Matches(War3ObjectData objects)
        {
            // Fields is publicly mutable; validate every reachable alias text,
            // not just the root or a simulation-tick version.
            foreach(var source in Sources)
                if((objects?.Get(source.Rawcode)?.GetString("DependencyOr","")??"")!=source.Dependencies)
                    return false;
            return true;
        }
    }
    readonly Dictionary<string,TechDependencyTypeState> _techDependencyTypes=new(StringComparer.Ordinal);

    IReadOnlySet<string> TechDependencyTypes(string rawcode)
    {
        if(_techDependencyTypes.TryGetValue(rawcode,out var cached)&&cached.Matches(_objects))
            return cached.Types;
        var state=new TechDependencyTypeState();
        void Add(string id)
        {
            if(!state.Types.Add(id))return;
            string dependencies=_objects?.Get(id)?.GetString("DependencyOr","")??"";
            state.Sources.Add((id,dependencies));
            foreach(string dependency in SplitRawcodes(dependencies))
                Add(dependency);
        }
        Add(rawcode);
        // Results already returned to callers remain independently valid.
        if(_techDependencyTypes.Count>=256)_techDependencyTypes.Clear();
        _techDependencyTypes[rawcode]=state;
        return state.Types;
    }

    bool TryGetHeroLimit(PlayerRef player,out int limit)
    {
        bool configured=player.TechMaxAllowed.TryGetValue(FourCC("HERO"),out limit);
        if(limit<0)limit=int.MaxValue;
        return configured;
    }

    int MeleeHeroTierLimit(int playerId)
    {
        int tier=1;
        foreach(Unit hall in _world.UnitsForPlayerInWorldOrder(playerId))
        {
            if(hall.Removed||!hall.Alive||hall.PlayerId!=playerId
                ||!hall.Identity.Is(Classification.TownHall))continue;
            string code=hall.TypeId;
            int level=code.Equals("hcas",StringComparison.OrdinalIgnoreCase)
                ||code.Equals("ofrt",StringComparison.OrdinalIgnoreCase)
                ||code.Equals("unp2",StringComparison.OrdinalIgnoreCase)
                ||code.Equals("etoe",StringComparison.OrdinalIgnoreCase)?3
                :code.Equals("hkee",StringComparison.OrdinalIgnoreCase)
                ||code.Equals("ostr",StringComparison.OrdinalIgnoreCase)
                ||code.Equals("unp1",StringComparison.OrdinalIgnoreCase)
                ||code.Equals("etoa",StringComparison.OrdinalIgnoreCase)?2:1;
            tier=Math.Max(tier,level);
        }
        return tier;
    }

    public bool IssuePlayerReviveHero(int playerId,Unit altar,int heroSimulationId,
        out string error)
    {
        if(CanAwakenHeroes(altar,playerId))
            return IssuePlayerAwakenHero(playerId,altar,heroSimulationId,out error);
        error=null;
        if(altar?.Alive!=true||altar.Removed||!CanPlayerControl(playerId,altar,true)
            ||altar.Abilities.Get<ConstructionProgress>()!=null
            ||!CanReviveHeroes(altar,_objects))
        {
            error="必须在自己的祭坛复活英雄。";
            return false;
        }
        playerId=altar.PlayerId;
        Unit deadHero=_world.Units.FirstOrDefault(unit=>unit.SimulationId==heroSimulationId);
        Hero hero=deadHero?.Abilities.Get<Hero>();
        if(deadHero==null||deadHero.IsIllusion||deadHero.PlayerId!=playerId
            ||hero?.AwaitingRevive!=true
            ||hero.Reviving)
        {
            error="该英雄当前不能复活。";
            return false;
        }
        UnitTypeData data=_objects?.Get(deadHero.TypeId);
        int gold=GameplayConstants.HeroReviveGoldCost(
            Math.Max(0,data?.GetInt("goldcost",0)??0),hero.HeroLevel,_world.GameplayConfig);
        int lumber=GameplayConstants.HeroReviveLumberCost(
            Math.Max(0,data?.GetInt("lumbercost",0)??0),hero.HeroLevel,_world.GameplayConfig);
        if(!TrySpendResources(playerId,gold,lumber))
        {
            PlayerRef player=Player(playerId);
            error=player.States.GetValueOrDefault(1)<gold?"金币不足。":"木材不足。";
            return false;
        }
        RuntimeProductionQueueAbility queue=
            altar.Abilities.Get<RuntimeProductionQueueAbility>()??AddProductionQueue(altar);
        if(queue.Count>=7||!hero.StartRevive(altar,notifyStarted:false))
        {
            RefundResources(playerId,gold,lumber);
            error="复活队列已满。";
            return false;
        }
        bool resolved=false;
        void Resolve(bool completed)
        {
            if(resolved)return;
            resolved=true;
            if(completed&&altar.Alive&&!altar.Removed&&hero.CompleteRevive())return;
            hero.CancelRevive();
            RefundResources(playerId,gold,lumber);
        }
        // Revival reads the fallen hero type build time, then applies native
        // factor, five-second quantization and duration clamps. Missing data
        // supplies zero before those clamps; no altar-time fallback is used.
        // 40f5f0: native revive duration uses integer type build time and
        // five-second quantization before its minimum/maximum clamps.
        float duration=NativeHeroBuildTime(data?.GetInt("bldtm",0)??0,hero.HeroLevel,_world.GameplayConfig);
        var job=new RuntimeProductionQueueAbility.Job(deadHero.TypeId,duration,hero.NotifyReviveStarted,Resolve,
            kind:RuntimeProductionQueueAbility.JobKind.Revival);
        if(queue.Enqueue(job))return true;
        Resolve(false);
        error="复活队列已满。";
        return false;
    }

    public bool IssuePlayerUpgrade(int playerId,Unit building,string targetRawcode,
        out string error)
    {
        error=null;
        UnitTypeData sourceData=_objects?.Get(building?.TypeId);
        UnitTypeData targetData=_objects?.Get(targetRawcode);
        if(building?.Alive!=true||building.Removed||!CanPlayerControl(playerId,building,true)
            ||!building.IsBuilding||building.Abilities.Get<ConstructionProgress>()!=null
            ||targetData==null||!SplitRawcodes(sourceData?.GetString("Upgrade","")??"")
                .Contains(targetRawcode,StringComparer.OrdinalIgnoreCase))
        {
            error="该建筑不能进行这项升级。";
            return false;
        }
        playerId=building.PlayerId;
        RuntimeProductionQueueAbility queue=
            building.Abilities.Get<RuntimeProductionQueueAbility>();
        if(queue?.Count>0)
        {
            error="生产队列不为空。";
            return false;
        }
        PlayerRef player=Player(playerId);
        if(!MeetsStockRequirements(player,targetData))
        {
            error="尚未满足升级条件。";
            return false;
        }
        int gold=GameplayConstants.BuildingUpgradeCost(
            sourceData?.GetInt("goldcost",0)??0,targetData.GetInt("goldcost",0));
        int lumber=GameplayConstants.BuildingUpgradeCost(
            sourceData?.GetInt("lumbercost",0)??0,targetData.GetInt("lumbercost",0));
        if(!TrySpendResources(playerId,gold,lumber))
        {
            error=player.States.GetValueOrDefault(1)<gold?"金币不足。":"木材不足。";
            return false;
        }
        queue??=AddProductionQueue(building);
        bool resolved=false;
        void Resolve(bool completed)
        {
            if(resolved)return;
            resolved=true;
            if(completed&&FinishBuildingUpgrade(building,targetRawcode))return;
            RefundResources(playerId,gold,lumber);
            FireUpgradeEvent(GameEvent.UnitUpgradeCancel,building);
        }
        float duration=ConstructionDuration(playerId,targetData.GetFloat("bldtm",1f));
        if(IsMeleeTownHallTier(targetRawcode))
            duration=ConstructionDuration(playerId,duration);
        var job=new RuntimeProductionQueueAbility.Job(targetRawcode,duration,
            ()=>FireUpgradeEvent(GameEvent.UnitUpgradeStart,building),Resolve,
            kind:RuntimeProductionQueueAbility.JobKind.BuildingUpgrade);
        if(queue.Enqueue(job))return true;
        Resolve(false);
        error="无法开始升级。";
        return false;
    }

    bool FinishBuildingUpgrade(Unit building,string targetRawcode)
    {
        if(building?.Alive!=true||building.Removed)return false;
        Unit template=UnitFactory.Create(_objects,targetRawcode,building.Team,
            building.Pos,building.Facing);
        building.ApplyUnitTypeTemplate(template);
        foreach(Ability ability in template.Abilities.Snapshot())
        {
            if(string.IsNullOrWhiteSpace(ability.Rawcode)
                ||building.Abilities.Get(ability.Rawcode)!=null)continue;
            template.RemoveAbility(ability);
            building.AddAbilityAndInitialize(ability);
        }
        var upgradedMask=ResolveConstructionMask(targetRawcode);
        if(upgradedMask!=null)
        {
            bool pathingEnabled=building.PathingEnabled;
            if(pathingEnabled)_world.SetUnitPathing(building,false);
            building.PathingTexture=upgradedMask;
            if(pathingEnabled)_world.SetUnitPathing(building,true);
        }
        FireUpgradeEvent(GameEvent.UnitUpgradeFinish,building);
        return true;
    }

    public int GetPlayerTechLevel(int playerId,int techId)=>
        Player(playerId).Tech.GetValueOrDefault(techId);

    // GetPlayerTechCount is polymorphic in game.dll: upgrade ids return their
    // researched level, while unit ids return the number of completed matching
    // units.  Keeping only the research dictionary made an in-place town-hall
    // upgrade invisible to scripts until some unrelated bookkeeping caught up.
    public int GetPlayerTechCount(int playerId,int techId,bool specificOnly)
    {
        PlayerRef player=Player(playerId);
        string rawcode=Rawcode(JassVm.IntegerValue(techId));
        // Both GetPlayerTechCount modes count completed units (flags 0x10/0x18),
        // unlike the production limit's completed + queued flags 0xb.
        // HERO is a virtual dependency group defined in NeutralUnitFunc.txt.
        if(rawcode=="HERO")
        {
            IReadOnlySet<string> heroTypes=TechDependencyTypes(rawcode);
            int heroes=0;
            foreach(Unit unit in _world.UnitsForPlayerInWorldOrder(playerId))
                if(!unit.Removed&&!unit.IsIllusion&&unit.PlayerId==playerId
                    &&heroTypes.Contains(unit.TypeId)&&unit.Abilities.Get<ConstructionProgress>()==null)heroes++;
            return heroes;
        }
        if(_objects?.GetUpgrade(rawcode)!=null)
            return player.Tech.GetValueOrDefault(techId);
        if(_objects?.Get(rawcode)==null)
            return player.Tech.GetValueOrDefault(techId);
        IReadOnlySet<string> family=specificOnly?null:UnitAvailabilityFamily(rawcode);
        int count=0;
        foreach(Unit unit in _world.UnitsForPlayerInWorldOrder(playerId))
            if(!unit.Removed&&unit.Alive&&unit.PlayerId==playerId
                &&(specificOnly?string.Equals(unit.TypeId,rawcode,StringComparison.OrdinalIgnoreCase):family.Contains(unit.TypeId))
                &&unit.Abilities.Get<ConstructionProgress>()==null)count++;
        return count;
    }

    public bool MeetsPlayerRequirements(int playerId,War3TypeData data)=>
        MeetsStockRequirements(Player(playerId),data);

    // rtma(单位替换)效果只依赖静态升级表,缓存后避免每次全表扫描+插值字段名。
    List<(int TechId,(string Code,int Direction)[] Effects)> RtmaUpgrades()
    {
        if(_rtmaUpgrades!=null)return _rtmaUpgrades;
        var result=new List<(int,(string,int)[])>();
        foreach(UpgradeTypeData upgrade in _objects?.Upgrades
            ??Array.Empty<UpgradeTypeData>())
        {
            var effects=new List<(string,int)>();
            for(int index=1;index<=4;index++)
            {
                if(!upgrade.GetString($"effect{index}","").Equals("rtma",
                        StringComparison.OrdinalIgnoreCase))continue;
                effects.Add((upgrade.GetString($"code{index}",""),
                    upgrade.GetInt($"base{index}",0,0,0)));
            }
            if(effects.Count>0)
                result.Add((FourCC(upgrade.Id),effects.ToArray()));
        }
        _rtmaUpgrades=result;
        return result;
    }

    public bool IsUnitAvailableForPlayer(int playerId,string rawcode)
    {
        if(string.IsNullOrWhiteSpace(rawcode))return false;
        PlayerRef player=Player(playerId);
        int typeId=FourCC(rawcode);
        if(player.TechMaxAllowed.TryGetValue(typeId,out int maximum)&&maximum==0)
            return false;
        var tech=player.Tech;
        foreach((int techId,var effects) in RtmaUpgrades())
        {
            bool researched=tech.GetValueOrDefault(techId)>0;
            foreach((string code,int direction) in effects)
            {
                if(!code.Equals(rawcode,StringComparison.OrdinalIgnoreCase))continue;
                if(direction>0&&researched)return false;
                if(direction<0&&!researched)return false;
            }
        }
        return true;
    }

    public string ResolveAvailableUnitAlias(int playerId,string requestedRawcode)
    {
        if(IsUnitAvailableForPlayer(playerId,requestedRawcode))return requestedRawcode;
        foreach((_,var effects) in RtmaUpgrades())
        {
            bool containsRequested=false;
            string predecessor=null;
            foreach((string code,int direction) in effects)
            {
                if(direction<0&&code.Equals(requestedRawcode,
                        StringComparison.OrdinalIgnoreCase))containsRequested=true;
                if(direction>0)predecessor=code;
            }
            if(containsRequested&&predecessor?.Length>=4
                &&IsUnitAvailableForPlayer(playerId,predecessor[..4]))
                return predecessor[..4];
        }
        return requestedRawcode;
    }

    // 全名→4 字族键缓存, 避免热路径每调用切片分配
    readonly Dictionary<string,string> _availabilityKeyCache=new();

    public IReadOnlySet<string> UnitAvailabilityFamily(string rawcode)
    {
        string normalized=rawcode??"";
        if(!_availabilityKeyCache.TryGetValue(normalized,out string key))
            _availabilityKeyCache[normalized]=key=
                normalized.Length>=4?normalized[..4]:normalized;
        if(_unitAvailabilityFamilies.TryGetValue(key,out IReadOnlySet<string> cached))
            return cached;
        var result=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(key.Length>=4)result.Add(key);
        foreach(UpgradeTypeData upgrade in _objects?.Upgrades
            ??Array.Empty<UpgradeTypeData>())
        {
            string[] members=new string[4];
            int memberCount=0;
            bool intersects=false;
            for(int index=1;index<=4;index++)
            {
                if(!upgrade.GetString($"effect{index}","").Equals("rtma",
                        StringComparison.OrdinalIgnoreCase))continue;
                string code=upgrade.GetString($"code{index}","");
                if(code.Length<4)continue;
                string member=code[..4];
                members[memberCount++]=member;
                if(result.Contains(member))intersects=true;
            }
            if(!intersects)continue;
            for(int index=0;index<memberCount;index++)result.Add(members[index]);
        }
        _unitAvailabilityFamilies[key]=result;
        return result;
    }

    public int GetUpgradeGoldCost(int playerId,string rawcode)
    {
        UpgradeTypeData data=_objects?.GetUpgrade(rawcode);
        int level=GetPlayerTechLevel(playerId,FourCC(rawcode));
        return data==null?0:Math.Max(0,data.GetInt("goldbase",0,0,0)
            +data.GetInt("goldmod",0,0,0)*level);
    }

    public int GetUpgradeWoodCost(int playerId,string rawcode)
    {
        UpgradeTypeData data=_objects?.GetUpgrade(rawcode);
        int level=GetPlayerTechLevel(playerId,FourCC(rawcode));
        return data==null?0:Math.Max(0,data.GetInt("lumberbase",0,0,0)
            +data.GetInt("lumbermod",0,0,0)*level);
    }

    public bool CanPlayerResearch(int playerId,Unit building,string rawcode,
        out string error)
    {
        error=null;
        UpgradeTypeData data=_objects?.GetUpgrade(rawcode);
        UnitTypeData buildingData=_objects?.Get(building?.TypeId);
        if(building?.Alive!=true||building.Removed||!CanPlayerControl(playerId,building,true)
            ||!building.IsBuilding||building.Abilities.Get<ConstructionProgress>()!=null
            ||data==null||!SplitRawcodes(buildingData?.GetString("Researches","")??"")
                .Contains(rawcode,StringComparer.OrdinalIgnoreCase))
        {
            error="该建筑不能研究这项科技。";
            return false;
        }
        playerId=building.PlayerId;
        int level=GetPlayerTechLevel(playerId,FourCC(rawcode));
        if(level>=Math.Max(1,data.GetInt("maxlevel",0,0,1)))
        {
            error="该科技已研究完成。";
            return false;
        }
        RuntimeProductionQueueAbility queue=building.Abilities
            .Get<RuntimeProductionQueueAbility>();
        // CAbilityQueue 6f3dffd0 checks the last of seven shared slots, not
        // whether training is active. Research and units share the FIFO.
        if(queue?.Count>=7)
        {
            error="生产队列已满。";
            return false;
        }
        if(queue?.Jobs.Any(job=>job.Kind==RuntimeProductionQueueAbility.JobKind.BuildingUpgrade)==true)
        {
            error="该建筑正在升级。";
            return false;
        }
        if(HasPlayerResearchReservation(playerId,rawcode))
        {
            error="该科技正在研究。";
            return false;
        }
        if(!MeetsUpgradeRequirements(Player(playerId),data,level))
        {
            error="尚未满足研究条件。";
            return false;
        }
        return true;
    }

    public bool IssuePlayerResearch(int playerId,Unit building,string rawcode,
        out string error)
    {
        if(!CanPlayerResearch(playerId,building,rawcode,out error))return false;
        playerId=building.PlayerId;
        UpgradeTypeData data=_objects.GetUpgrade(rawcode);
        int techId=FourCC(rawcode);
        int level=GetPlayerTechLevel(playerId,techId);
        int gold=GetUpgradeGoldCost(playerId,rawcode);
        int lumber=GetUpgradeWoodCost(playerId,rawcode);
        if(!TrySpendResources(playerId,gold,lumber))
        {
            PlayerRef player=Player(playerId);
            error=player.States.GetValueOrDefault(1)<gold?"金币不足。":"木材不足。";
            return false;
        }
        RuntimeProductionQueueAbility queue=building.Abilities
            .Get<RuntimeProductionQueueAbility>()??AddProductionQueue(building);
        bool resolved=false;
        void Resolve(bool completed)
        {
            if(resolved)return;
            resolved=true;
            if(completed&&building.Alive&&!building.Removed)
            {
                SetPlayerTechLevel(Player(playerId),techId,level+1);
                FireResearchEvent(GameEvent.UnitResearchFinish,building,techId);
                return;
            }
            RefundResources(playerId,gold,lumber);
            FireResearchEvent(GameEvent.UnitResearchCancel,building,techId);
        }
        float duration=Math.Max(World.TickSeconds,data.GetInt("timebase",0,0,1)
            +data.GetInt("timemod",0,0,0)*level);
        var job=new RuntimeProductionQueueAbility.Job(rawcode,duration,
            ()=>FireResearchEvent(GameEvent.UnitResearchStart,building,techId),
            Resolve,isResearch:true,researchLevel:level+1);
        if(queue.Enqueue(job))return true;
        Resolve(false);
        error="无法开始研究。";
        return false;
    }

    bool MeetsUpgradeRequirements(PlayerRef player,UpgradeTypeData data,int currentLevel)
    {
        // W3Q greq/grqc use one-based levels; TXT profiles use Requires,
        // Requires1, Requires2. An explicit empty W3Q value clears the stock gate.
        string requirements=UpgradeRequirement("Requires");
        string amounts=UpgradeRequirement("Requiresamount");
        string UpgradeRequirement(string field)=>
            data.LevelFields.TryGetValue(new(field,currentLevel+1),out string value)
                ?value:data.GetString(currentLevel==0?field:$"{field}{currentLevel}","");
        string[] ids=SplitObjectList(requirements);
        string[] counts=SplitObjectList(amounts);
        for(int index=0;index<ids.Length;index++)
        {
            int amount=index<counts.Length&&int.TryParse(counts[index],out int parsed)
                ?Math.Max(1,parsed):1;
            if(!MeetsRequirement(player,ids[index],amount))return false;
        }
        return true;
    }

    internal void SetPlayerTechLevel(PlayerRef player,int techId,int level)
    {
        int previous=player.Tech.GetValueOrDefault(techId);
        level=Math.Max(0,level);
        player.Tech[techId]=level;
        if(previous<=0&&level>0)ApplyUnitAvailabilityTransforms(
            player,Rawcode(JassVm.IntegerValue(techId)));
        foreach(Unit unit in _world.Units.Where(unit=>!unit.Removed
            &&unit.PlayerId==player.Id))
        {
            RefreshUnitTechAbilities(unit,player);
            RefreshUnitTechEffects(unit,player);
        }
    }

    void RefreshAbilityAvailability(Ability ability,PlayerRef player)
    {
        // Buffs already applied to a recipient follow their own expiry/decay.
        if(ability is Buff||ability.Rawcode.Length!=4)return;
        if(IsPlayerAbilityAvailable(player.Id,FourCC(ability.Rawcode)))
            ability.Enable(AbilityDisableType.PlayerAvailability);
        else ability.Disable(AbilityDisableType.PlayerAvailability);
    }

    void RefreshUnitTechAbilities(Unit unit,PlayerRef player)
    {
        if(unit==null||player==null)return;
        foreach(Ability ability in unit.Abilities.Snapshot())
        {
            RefreshAbilityAvailability(ability,player);
            if(ability.ObjectData is not War3TypeData data
                ||string.IsNullOrWhiteSpace(data.GetString("Requires","")))continue;
            // checkDep 是 AbilData 单值列(不参与分级), 按 level=0 读原始 Fields 槽。
            ability.SetTechTreeLocked(data.GetInt("checkDep",0,0,1)!=0
                &&!MeetsStockRequirements(player,data));
        }
    }

    void RefreshUnitTechEffects(Unit unit,PlayerRef player)
    {
        UnitTypeData unitData=_objects?.Get(unit?.TypeId);
        if(unitData==null||player==null)return;
        RuntimeUltravisionAbility ultravision=unit.GetAbility<RuntimeUltravisionAbility>();
        if(ultravision!=null)
            unit.Stats.SightRadiusNight=unitData.GetFloat("nsight",unit.Stats.SightRadiusNight);
        float oldMaxLife=unit.MaxLife,oldMaxMana=unit.MaxMana;
        unit.Stats.TechMaxLifeBonus=0;
        unit.Stats.TechArmorBonus=0;
        unit.Stats.TechSpeedBonus=0;
        unit.Stats.TechDamageBonus=0;
        unit.Stats.TechDamageMulBonus=0;
        unit.Stats.TechAttackRangeBonus=0;
        unit.Stats.TechAttackSpeedBonusPct=0;
        unit.Health.TechMaxManaBonus=0;
        unit.Health.TechMaxManaMul=0;
        unit.Health.TechLifeRegenBonus=0;
        unit.Health.TechManaRegenBonus=0;
        foreach(RuntimeRaiseDeadAbility raise in unit.Abilities.OfType<RuntimeRaiseDeadAbility>())
            raise.TechDurationBonus=0;
        foreach(RuntimeSpikedBarricadesAbility spikes in unit.Abilities.OfType<RuntimeSpikedBarricadesAbility>())
            spikes.TechReturnedDamageBonus=0;
        foreach(RuntimeReinforcedArmorAbility armor in unit.Abilities.OfType<RuntimeReinforcedArmorAbility>())
            armor.TechArmorTypeActive=false;
        foreach(Weapon weapon in unit.WeaponList)
        {
            weapon.TechRangeBonus=0;
            weapon.TechSpillDistanceBonus=0;
            weapon.TechBounceCountBonus=0;
            weapon.Enabled=(unitData.GetInt("weapsOn",0)&(1<<(weapon.Slot-1)))!=0;
        }
        foreach(RuntimeCommandAuraAbility aura in unit.Abilities
                    .OfType<RuntimeCommandAuraAbility>())
            aura.SetTechDamageFactorBonus(0);
        foreach(string rawcode in SplitRawcodes(unitData.GetString("upgrades","")))
        {
            UpgradeTypeData upgrade=_objects.GetUpgrade(rawcode);
            int level=player.Tech.GetValueOrDefault(FourCC(rawcode));
            if(upgrade==null)continue;
            for(int index=1;index<=4;index++)
            {
                string effect=upgrade.GetString($"effect{index}","").ToLowerInvariant();
                if(level<=0&&effect!="rlev")continue;
                float value=level>0?UpgradeEffectValue(upgrade,index,level):0;
                switch(effect)
                {
                    case "ratc":
                        foreach(Weapon weapon in unit.WeaponList)
                            weapon.TechBounceCountBonus+=(int)value;
                        break;
                    case "rasd":
                        foreach(Weapon weapon in unit.WeaponList)
                            weapon.TechSpillDistanceBonus+=value;
                        break;
                    case "renw":
                        foreach(Weapon weapon in unit.WeaponList)
                            weapon.Enabled=((int)value&(1<<(weapon.Slot-1)))!=0;
                        break;
                    case "ratd":unit.Stats.TechDamageBonus+=value;break;
                    case "ratx":unit.Stats.TechDamageMulBonus+=Pct(value);break;
                    case "rarm":
                        unit.Stats.TechArmorBonus+=upgrade.GetString($"base{index}","")=="-"
                            ?unitData.GetFloat("defUp",0)*level:value;
                        break;
                    // rhpx adds hit points (Rhan=150, caster training=75/level);
                    // rhpo scales the type's base maximum life.
                    case "rhpx":unit.Stats.TechMaxLifeBonus+=value;break;
                    case "rhpo":unit.Stats.TechMaxLifeBonus+=unit.MaxLifeBase*Pct(value);break;
                    case "rmvx":unit.Stats.TechSpeedBonus+=value;break;
                    case "rmnx":unit.Health.TechMaxManaBonus+=value;break;
                    case "rman":unit.Health.TechMaxManaBonus+=value;break;
                    case "rmap":unit.Health.TechMaxManaMul+=Pct(value);break;
                    case "rmnr":unit.Health.TechManaRegenBonus+=value;break;
                    case "rhpr":unit.Health.TechLifeRegenBonus+=value;break;
                    case "ratr":
                        unit.Stats.TechAttackRangeBonus+=value;
                        foreach(Weapon weapon in unit.WeaponList)weapon.TechRangeBonus+=value;
                        break;
                    case "rats":unit.Stats.TechAttackSpeedBonusPct+=Pct(value);break;
                    case "rauv":unit.Stats.SightRadiusNight=unit.Stats.SightRadius;break;
                    case "rart":
                        if(value>0&&unit.IsBuilding)unit.DefenseType=DefenseType.Fort;
                        foreach(RuntimeReinforcedArmorAbility armor in unit.Abilities.OfType<RuntimeReinforcedArmorAbility>())
                            armor.TechArmorTypeActive=value>0;
                        break;
                    case "rlev":
                        string abilityRawcode=upgrade.GetString($"code{index}","");
                        Ability ability=unit.GetAbility(abilityRawcode);
                        // rlev is a bonus above the base ability level, not an
                        // absolute level. Recompute from research to stay idempotent.
                        if(ability!=null)ability.SetLevel(Math.Max(1,1+(int)value),
                            Math.Max(1,ability.ObjectData?.GetInt("levels",1,0,1)??1));
                        break;
                    case "rspi":
                        foreach(RuntimeSpikedBarricadesAbility spikes in unit.Abilities.OfType<RuntimeSpikedBarricadesAbility>())
                            spikes.TechReturnedDamageBonus+=value;
                        break;
                    case "rrai":
                        foreach(RuntimeRaiseDeadAbility raise in unit.Abilities
                            .OfType<RuntimeRaiseDeadAbility>().Where(raise=>raise.SupportsAutoCast))
                            raise.TechDurationBonus+=value;
                        break;
                    case "raud":
                        string auraRawcode=upgrade.GetString($"code{index}","");
                        if(unit.GetAbility(auraRawcode) is RuntimeCommandAuraAbility aura)
                            aura.SetTechDamageFactorBonus(
                                aura.TechDamageFactorBonus+value);
                        break;
                }
            }
        }
        foreach(RuntimeGrabTreeBuff grabTree in unit.Abilities.OfType<RuntimeGrabTreeBuff>())
            grabTree.ReapplyAfterTechRefresh();
        foreach(RuntimeRoarBuff roar in unit.Abilities.OfType<RuntimeRoarBuff>())
            roar.ReapplyDamageBonuses();
        // Stock Reuv has no scalar upgrade effects. It unlocks Ault, which
        // makes night sight equal day sight without casting a reveal spell.
        if(ultravision is { IsTechTreeLocked:false })
            unit.Stats.SightRadiusNight=unit.Stats.SightRadius;
        if(unit.MaxLife>oldMaxLife)unit.Life=Math.Min(unit.MaxLife,
            unit.Life+unit.MaxLife-oldMaxLife);
        else unit.Life=Math.Min(unit.Life,unit.MaxLife);
        if(unit.MaxMana>oldMaxMana)unit.Mana=Math.Min(unit.MaxMana,
            unit.Mana+unit.MaxMana-oldMaxMana);
        else unit.Mana=Math.Min(unit.Mana,unit.MaxMana);
    }

    static readonly string[] UpgradeBaseFields={"base1","base2","base3","base4"};
    static readonly string[] UpgradeModifierFields={"mod1","mod2","mod3","mod4"};

    static float UpgradeEffectValue(UpgradeTypeData data,int index,int level)
    {
        float baseValue=data.GetFloat(UpgradeBaseFields[index-1],0,0,0);
        float modifier=data.GetFloat(UpgradeModifierFields[index-1],0,0,0);
        return baseValue+modifier*Math.Max(0,level-1);
    }

    // 百分比型升级效果统一规整: 数据以分数(0.25)或百分数(25)存储均可。
    static float Pct(float value)=>value>1f?value/100f:value;

    void ApplyUnitAvailabilityTransforms(PlayerRef player,string upgradeRawcode)
    {
        UpgradeTypeData data=_objects?.GetUpgrade(upgradeRawcode);
        if(data==null)return;
        var oldCodes=new List<string>();
        var newCodes=new List<string>();
        for(int index=1;index<=4;index++)
        {
            if(!data.GetString($"effect{index}","").Equals("rtma",
                    StringComparison.OrdinalIgnoreCase))continue;
            string code=data.GetString($"code{index}","");
            int direction=data.GetInt($"base{index}",0,0,0);
            if(code.Length<4)continue;
            int typeId=FourCC(code[..4]);
            int maximum=player.TechMaxAllowed.GetValueOrDefault(typeId,-1)+direction;
            if(maximum<0)player.TechMaxAllowed.Remove(typeId);
            else player.TechMaxAllowed[typeId]=maximum;
            if(direction>0)oldCodes.Add(code[..4]);
            else if(direction<0)newCodes.Add(code[..4]);
        }
        for(int index=0;index<Math.Min(oldCodes.Count,newCodes.Count);index++)
        foreach(Unit unit in _world.Units.Where(unit=>!unit.Removed&&unit.PlayerId==player.Id
            &&unit.TypeId.Equals(oldCodes[index],StringComparison.OrdinalIgnoreCase)).ToArray())
            ApplyUnitTypeReplacement(unit,newCodes[index]);
    }

    void ApplyUnitTypeReplacement(Unit unit,string targetRawcode)
    {
        Unit template=UnitFactory.Create(_objects,targetRawcode,unit.Team,unit.Pos,unit.Facing);
        float lifeRatio=unit.MaxLife>0?unit.Life/unit.MaxLife:1;
        float manaRatio=unit.MaxMana>0?unit.Mana/unit.MaxMana:1;
        // Game.dll 变身(如 uobs->ubsp 毁灭者形态)以新类型的 abilList 为准:
        // 旧类型的物编技能(雕像 Arpl/Arpm)必须随之移除, 只保留引擎机制类
        // (攻击/生产队列/英雄/建造等)与英雄已学技能。
        var templateRawcodes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(Ability templateAbility in template.Abilities.Snapshot())
            if(!string.IsNullOrWhiteSpace(templateAbility.Rawcode))
                templateRawcodes.Add(templateAbility.Rawcode);
        foreach(Ability existing in unit.Abilities.Snapshot())
        {
            if(string.IsNullOrWhiteSpace(existing.Rawcode)
                ||templateRawcodes.Contains(existing.Rawcode))continue;
            if(existing is CAbilityAttack or RuntimeProductionQueueAbility
                or Hero or ConstructionProgress)continue;
            if(existing is Buff)continue;
            unit.RemoveAbility(existing);
        }
        unit.ApplyUnitTypeTemplate(template);
        foreach(Ability ability in template.Abilities.Snapshot())
        {
            if(string.IsNullOrWhiteSpace(ability.Rawcode)
                ||unit.Abilities.Get(ability.Rawcode)!=null)continue;
            template.RemoveAbility(ability);
            unit.AddAbilityAndInitialize(ability);
        }
        unit.Life=Math.Clamp(unit.MaxLife*lifeRatio,0,unit.MaxLife);
        unit.Mana=Math.Clamp(unit.MaxMana*manaRatio,0,unit.MaxMana);
    }

    void FireResearchEvent(GameEvent gameEvent,Unit building,int techId)
    {
        if(building?.World==null)return;
        _world.Events.Fire(new EventContext{Event=gameEvent,Source=building,
            Target=building,TriggerUnit=building,ResearchingUnit=building,
            ResearchedId=techId,TriggerPlayerId=building.PlayerId});
    }

    void FireUpgradeEvent(GameEvent gameEvent,Unit building)
    {
        if(building?.World==null)return;
        _world.Events.Fire(new EventContext{Event=gameEvent,Source=building,
            Target=building,TriggerUnit=building,TriggerPlayerId=building.PlayerId});
    }

    // Native 413760 -> 31f800 reads the producer type's Revive flag.
    // Campaign/custom altars need not have a melee rawcode or a training list.
    public static bool CanReviveHeroes(Unit producer,War3ObjectData objects)
        =>producer!=null&&(objects?.Get(producer.TypeId)?.GetInt("Revive",0)??0)!=0;

    public bool CancelPlayerTraining(Unit producer)
        =>producer?.Abilities.Get<RuntimeProductionQueueAbility>()?.CancelActive()==true;

    public bool CancelPlayerTrainingAt(Unit producer,int index)
        =>producer?.Abilities.Get<RuntimeProductionQueueAbility>()?.CancelAt(index)==true;

    RuntimeProductionQueueAbility AddProductionQueue(Unit producer)
    {
        var queue=new RuntimeProductionQueueAbility();
        producer.AddAbility(queue);
        return queue;
    }

    Unit TryFinishTraining(Unit producer,PlayerRef player,string rawcode,
        int typeId)
    {
        if(producer?.Alive!=true||producer.Removed)return null;
        Unit trained=UnitFactory.Create(_objects,rawcode,player.Team,producer.Pos,producer.Facing);
        ApplyPlayerIdentity(trained,player,true);
        // 3f0653 submits the producer, trained type and rally to 650cb0.
        // Search failure retains the completed paid job; it must not switch
        // to an unrelated point search or register the temporary unit.
        Vec2 spawn=producer.Pos;
        if(_world.PathingMap!=null
            &&!_world.PathingMap.TryResolveProducedUnitPlacement(trained,producer,out spawn))return null;
        trained.Pos=spawn;
        _world.Spawn(trained);
        if(trained.IsHero)
        {
            trained.Life=trained.MaxLife;
            trained.Mana=trained.MaxMana;
        }
        // Blizzard.j's MeleeGrantHeroItems owns the first-hero grant via this event.
        FireTrainingEvent(GameEvent.UnitTrainFinish,producer,trained,typeId);
        if(producer.RallyPoint is Vec2 rally&&trained.CanMove)
        {
            if(!TryIssueRallyHarvest(trained,rally))
                trained.ForceIssue(new COrderTarget(rally,851986){IsRallyMove = true});
        }
        return trained;
    }

    internal Unit TryFinishTrainingForReview(Unit producer,int playerId,
        string rawcode)=>TryFinishTraining(producer,Player(playerId),rawcode,0);

    // 原版: 集结点落在金矿/树上时, 训练出的工人直接转为采集而非走到点。
    bool TryIssueRallyHarvest(Unit worker,Vec2 rally)
    {
        if(!CanHarvest(worker))return false;
        Unit mine=null;
        float mineDistance=256f;
        foreach(Unit unit in _world.Units)
        {
            if(!IsAvailableGoldMine(unit)||!IsGoldMineUnit(unit))continue;
            float distance=Vec2.Dist(unit.Pos,rally);
            if(distance<mineDistance)
            {
                mine=unit;
                mineDistance=distance;
            }
        }
        if(mine!=null)
            return CanHarvestGold(worker,mine)
                &&IssuePlayerHarvest(new[]{worker},mine,null)>0;
        if(CanHarvestLumber(worker))
        {
            DestructableRef tree=FindHarvestTree(rally,256f);
            if(tree!=null)return IssuePlayerHarvest(worker,tree);
        }
        return false;
    }

    void FireTrainingEvent(GameEvent gameEvent,Unit producer,Unit trained,int typeId)
    {
        if(producer?.World==null)return;
        _world.Events.Fire(new EventContext
        {
            Event=gameEvent,Source=producer,Target=trained??producer,
            // 原版训练事件的 GetTriggerUnit() 是新训练出的单位; train start/cancel 阶段
            // 单位可能尚未生成, 此时回退为生产建筑。
            TriggerUnit=trained??producer,TriggerPlayerId=producer.PlayerId,
            TrainedUnit=trained,TrainedUnitTypeId=typeId,
        });
    }
}

*/
