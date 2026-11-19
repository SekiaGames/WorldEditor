//重构情况：从 Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Jass/JassGameNatives.OrdersAndItems.cs 转换（骨架，方法体待补）
namespace OpenWC3.JassToSharp.Ai.Economy;

public interface IResourceSink
{
    void Credit(int player, int gold, int lumber);
}

public static class OrdersAndItems
{
    public static Func<int, int, int> Upkeep;

    public static int AdjustGold(int raw, int foodUsed) =>
        raw <= 0 ? 0 : (Upkeep?.Invoke(raw, foodUsed) ?? raw);

    public static void AddItemResource(IResourceSink sink, int player, int gold, int lumber)
    {
        if (sink == null) return;
        sink.Credit(player, Math.Max(0, gold), Math.Max(0, lumber));
    }

    public static bool HasRoom(int carried, int capacity, int incoming) =>
        capacity - carried >= incoming;

    public static int Overflow(int carried, int capacity, int incoming) =>
        Math.Max(0, carried + incoming - capacity);
}

/*
原文（JassGameNatives.OrdersAndItems.cs，1668 行）：

using OpenWC3.War3Formats.Jass;
using OpenWC3.War3Formats;
using OpenWC3.Pathing;

namespace OpenWC3.Game;

public sealed partial class JassGameNatives
{
    // Mixed melee/ranged contacts make AMAI alternate defend/undefend every
    // decision pulse. Keep an AI-selected stance long enough to be meaningful;
    // player-issued orders are intentionally not throttled.
    const int MeleeAiDefendMinimumStateTicks=100;

    public bool IssuePlayerUnloadPassenger(Unit carrier,Unit passenger)
    {
        if(passenger==null)return false;
        if(carrier?.Alive==true&&!carrier.Removed&&IsEntangledMine(carrier)
            &&passenger.Current is PlayerHarvestOrder mineOrder
            &&mineOrder.IsWispGoldChanneling&&mineOrder.IsInsideMine(carrier))
        {
            // Native 0xd004f releases the selected Aenc occupant. The host keeps
            // that occupant in PlayerHarvestOrder rather than CargoTransport;
            // OnEnd performs the equivalent clock detach and 5911f0 exit restore.
            passenger.CancelOrders();
            return true;
        }
        return carrier?.CargoHold?.Passengers.Contains(passenger)==true
            &&IssueOrder(carrier,852047,"unload",passenger,null);
    }
    bool IssueOrder(Unit unit, int orderId, string orderName, Unit target, Vec2? point)
    {
        if (unit?.Alive != true) return false;
        // 400500 item slot orders precede ordinary ability lookup.
        if (orderId is >= 852008 and <= 852013)
            return IssueInventorySlotOrder(unit, orderId, target, point);
        var request = new JassOrderRequest(unit, orderId, orderName, target, point);
        // null means unhandled; false is a terminal rejection, not fallback.
        if (TryIssueControlOrder(ref request) is bool controlResult) return controlResult;
        if (TryIssueFormOrder(ref request) is bool formResult) return formResult;
        if (target != null) return IssueUnitTargetOrder(ref request);
        if (point.HasValue) return IssuePointTargetOrder(ref request);
        return IssueImmediateOrder(ref request);
    }
    public bool PawnInventorySlot(Unit unit,int slot)
    {
        if(unit?.Alive!=true||unit.PlayerId<0||unit.PlayerId>=12)return false;
        List<ItemRef> items=Inventory(unit);
        if(slot<0||slot>=items.Count||items[slot] is not ItemRef item
            ||!CanDropItem(unit,item)||!item.Pawnable)return false;
        Unit shop=_world.Units.Where(target=>CanReceivePawn(unit,item,target)
                &&InPawnRange(unit,target))
            .OrderBy(target=>Vec2.Dist(unit.Pos,target.Pos)).ThenBy(target=>target.SimulationId).FirstOrDefault();
        return shop!=null&&SellInventorySlotToShop(unit,slot,shop);
    }

    Ability PawnAbility(Unit shop)=>shop?.Abilities.Items.FirstOrDefault(ability=>!ability.IsDisabled
        &&string.Equals(ability.ObjectData?.GetString("code",ability.ObjectData.BaseId)??ability.Rawcode,"Apit",StringComparison.OrdinalIgnoreCase));
    bool CanReceivePawn(Unit source,ItemRef item,Unit shop)=>source!=shop&&shop?.Alive==true&&!shop.Removed
        &&PawnAbility(shop)!=null&&item?.Pawnable==true&&PlayerAllied(shop.PlayerId,source.PlayerId);
    bool InPawnRange(Unit source,Unit shop)=>InItemTargetRange(source,shop,
        (_world?.GameplayConfig??War3GameplayConfig.Default).PawnItemRange);
    bool InItemTargetRange(Unit source,Unit shop,float range)
    {
        if(StandalonePathing.Combat.TargetRange.Contains(
        new StandalonePathing.Core.WorldPt(source.Pos.X/32f,source.Pos.Y/32f),
        new StandalonePathing.Core.WorldPt(shop.Pos.X/32f,shop.Pos.Y/32f),
        source.CollisionSize/32f,shop.CollisionSize/32f,
        range))return true;
        var footprint=shop.PathingTexture==null?null:new OpenWC3.Pathing.CPathingFootprint(
            shop.Pos,shop.PathingTexture,source.CollisionSize,
            (int)MathF.Round(shop.Facing*180f/MathF.PI),shop.PathingOverlayHandle);
        return InteractionTargetRecordReached(source,footprint,shop);
    }
    bool GiveItemToUnit(Unit source,ItemRef item,Unit target)
    {
        if(!CanDropItem(source,item))return false;
        if(PawnAbility(target)!=null)
            return IssuePawnInventorySlotAt(source,Inventory(source).IndexOf(item),target);
        if(!CanAcceptTransferredItem(source,item,target))return false;
        if(InItemTargetRange(source,target,(_world?.GameplayConfig??War3GameplayConfig.Default).GiveItemRange))
            return DeliverTransferredItem(source,item,target);
        if(source.IsBuilding||!source.CanMove)return false;
        source.ForceIssue(new ItemTargetDeliveryOrder(this,item,target,false){JassOrderId=852001,
            EventTargetKind=EventTargetKind.Unit});
        return true;
    }

    bool CanAcceptTransferredItem(Unit source,ItemRef item,Unit target)
    {
        if(!CanReceiveTransferredItem(source,target)||item==null||item.Removed
            ||item.Powerup&&!InventoryGrantsItemAbilities(target))return false;
        if(AutomaticallyUsesItem(target,item))return true;
        var items=Inventory(target);
        // Native transfer uses the strict full-slot gate before any receive/stack dispatch.
        return items.Any(existing=>existing==null);
    }
    bool DeliverTransferredItem(Unit source,ItemRef item,Unit target)
    {
        if(!CanDropItem(source,item)||!CanAcceptTransferredItem(source,item,target))return false;
        if(!DropItem(source,item,force:true))return false;
        // Native receive scans empty slots again after detach callbacks; it cannot merge a now-full inventory.
        return AddItem(target,item,allowFullStackMerge:false);
    }

    public bool IssuePawnInventorySlotAt(Unit unit,int slot,Unit shop)
    {
        if(unit?.Alive!=true||unit.PlayerId<0||unit.PlayerId>=12)return false;
        var items=Inventory(unit);
        if(slot<0||slot>=items.Count||items[slot] is not ItemRef item
            ||!CanDropItem(unit,item)||!CanReceivePawn(unit,item,shop)
            ||shop.Abilities.Get<ConstructionProgress>()!=null
            ||GetItemRuntimeData(item)==null)return false;
        if(InPawnRange(unit,shop))return SellInventorySlotToShop(unit,slot,shop);
        if(unit.IsBuilding||!unit.CanMove)return false;
        unit.ForceIssue(new ItemTargetDeliveryOrder(this,item,shop,true){JassOrderId=852001,
            EventTargetKind=EventTargetKind.Unit});
        return true;
    }

    sealed class ItemTargetDeliveryOrder(JassGameNatives owner,ItemRef item,Unit shop,bool pawn):COrder
    {
        MovementExecution _move;
        Vec2 _point;
        float _range,_extent,_shopExtent,_facing;
        int _overlay;
        OpenWC3.War3Formats.PathingTextureMask _texture;
        public override void OnEnd(Unit unit,bool interrupted)=>_move?.OnEnd(unit,interrupted);
        public override COrder Execute(Unit unit,float dt)
        {
            if(!owner.CanDropItem(unit,item)||(pawn
                ?!owner.CanReceivePawn(unit,item,shop)||shop.Abilities.Get<ConstructionProgress>()!=null
                :!owner.CanAcceptTransferredItem(unit,item,shop)))return null;
            var config=unit.World?.GameplayConfig??War3GameplayConfig.Default;
            float range=pawn?config.PawnItemRange:config.GiveItemRange;
            if(_move!=null&&(!_point.Equals(shop.Pos)||_range!=range||_extent!=unit.CollisionSize
                ||_shopExtent!=shop.CollisionSize||_facing!=shop.Facing
                ||_overlay!=shop.PathingOverlayHandle||!ReferenceEquals(_texture,shop.PathingTexture)))
            { _move.OnEnd(unit,true);_move=null; }
            if(!owner.InItemTargetRange(unit,shop,range))
            {
                if(_move==null)
                {
                    _point=shop.Pos;_range=range;_extent=unit.CollisionSize;_shopExtent=shop.CollisionSize;
                    _facing=shop.Facing;_overlay=shop.PathingOverlayHandle;_texture=shop.PathingTexture;
                    var footprint=_texture==null?null:new OpenWC3.Pathing.CPathingFootprint(
                        _point,_texture,_extent,(int)MathF.Round(_facing*180f/MathF.PI),_overlay);
                    Vec2 goal=footprint?.NearestReachablePoint(unit.World?.PathingMap,
                        unit.Pos,unit.MovementType,_extent)??_point;
                    _move=new MovementExecution(goal,null,new HashSet<Unit>{shop},
                        footprint==null?MathF.Max(.49f*32f,range+_extent+_shopExtent):0f,
                        pathingTarget:footprint,requestTargetObject:shop);
                    _move.OnStart(unit);
                }
                COrder next=_move.Execute(unit,dt);
                if(!owner.InItemTargetRange(unit,shop,range))return next==null?null:this;
            }
            _move?.OnEnd(unit,false);_move=null;
            if(pawn)owner.SellInventorySlotToShop(unit,owner.Inventory(unit).IndexOf(item),shop);
            else owner.DeliverTransferredItem(unit,item,shop);
            return null;
        }
        public override string ToString()=> $"{(pawn?"典当":"转交")}物品 {item.Name}";
    }

    void CompleteItemPawn(Unit seller,ItemRef item,ItemTypeData data,Unit shop)
    {
        // 54d420 notifies while the item is still held, then detaches it before
        // reading the current value, refunding resources and ending its lifetime.
        _world.Events.Fire(new EventContext { Event=GameEvent.ItemPawned,
            Source=seller,TriggerUnit=seller,SellingUnit=shop??seller,
            BuyingUnit=seller,ManipulatedItem=item });
        DropItem(seller,item,force:true);
        int gold=PawnItemValue(item,data,"goldcost");
        int lumber=PawnItemValue(item,data,"lumbercost");
        RefundResources(seller.PlayerId,gold,lumber);
        if(CanPlayerControl(LocalPlayerId,seller,advanced:true))
            FloatingText.ShowBounty(seller,gold!=0?gold:lumber,lumber:gold==0);
        RemoveItem(item);
    }

    int PawnItemValue(ItemRef item,ItemTypeData data,string currency)
        =>CalculateItemPawnValue(data,item.Charges,
            (_world?.GameplayConfig??War3GameplayConfig.Default).PawnItemRate,currency);

    /// <summary>63cb00/63cb60: native real arithmetic, shared by refunds and inventory tooltips.</summary>
    public static int CalculateItemPawnValue(ItemTypeData data,int charges,float rate,string currency)
    {
        if(data==null)return 0;
        int uses=ReadItemUses(data);
        float fraction=uses!=0?JassNativeReal.Divide(JassNativeReal.FromInteger(charges),JassNativeReal.FromInteger(uses)):1f;
        float multiplier=JassNativeReal.Multiply(fraction,rate);
        return JassNativeReal.ToIntegerUnchecked(JassNativeReal.Multiply(
            JassNativeReal.FromInteger(data.GetInt(currency,0,0,0)),multiplier));
    }

    bool IssueTargetOrder(JassVm vm,Unit unit,int orderId,string orderName,JassValue targetValue)
    {
        // Game.dll 1e96c0 returns false for an unresolved ordering unit.
        // Maps may issue orders to an assistant before creating it.
        if(unit==null)return false;
        if(unit!=null&&(orderId==851980||string.Equals(orderName,"setrally",StringComparison.OrdinalIgnoreCase)))
        {
            object widget=ObjOrNull<Unit>(vm,targetValue)??(object)ObjOrNull<DestructableRef>(vm,targetValue);
            Vec2? point=widget is Unit rallyUnit?rallyUnit.Pos:widget is DestructableRef rallyTree?rallyTree.Position:null;
            if(point==null)return false;
            unit.Issue(CAbilityRally.CreateRequest(point.Value,widget,unit.PlayerId));return true;
        }
        if(unit?.Abilities.Get<ConstructionProgress>()!=null)return false;
        if(vm.TryGetHandleObject(targetValue,out Unit target))
        {
            string buildRawcode=orderId!=0?Rawcode(orderId):orderName;
            if(target?.Alive==true&&IsMineOverlayBuilding(buildRawcode))
            {
                // Overlay orders require an ordinary mine ability, including custom mines.
                if(!GoldMineIdentity.IsOrdinary(target))
                    return false;
                return IssuePlayerBuild(unit,buildRawcode,target.Pos,null,
                    out _,out _);
            }
            return IssueOrder(unit,orderId,orderName,target,null);
        }
        if(vm.TryGetHandleObject(targetValue,out DestructableRef destructable))
        {
            if(unit?.Alive!=true||destructable.Removed||destructable.Life<=0)return false;
            string normalized=(orderName??"").Trim().ToLowerInvariant();
            if(orderId==852018||normalized is "harvest")
                return IssuePlayerHarvest(unit,destructable);
            // 3df9c0 accepts smart (0xd0003) for harvest targets too. Map
            // triggers use this widget path rather than the UI right-click path.
            // If harvesting cannot accept it, retain the Eat Tree fallback.
            if((orderId==851971||normalized is "smart")
                &&IssuePlayerHarvest(unit,destructable))return true;
            if(orderId==851983||normalized is "attack")return TryAttackDestructable(unit,destructable);
            Ability ability=FindAbility(unit,orderId,normalized,
                EventTargetKind.Destructable);
            if(orderId==851971||normalized is "smart")
                ability=unit.Abilities.Get<RuntimeEatTreeAbility>();
            if(ability==null)return UnresolvedOrder(unit,orderId,normalized);
            if(ability.IsDisabled||!ability.CheckCanTargetDestructable(destructable,destructable.Position))return false;
            var order=new CastOrder
            {
                Ability=ability,
                DestructableTarget=destructable,
                Point=destructable.Position,
                JassOrderId=orderId!=0?orderId:OrderId(normalized),
                EventTargetKind=EventTargetKind.Destructable,
            };
            unit.Issue(order);
            return true;
        }
        if(orderId is >=852002 and <=852007&&vm.TryGetHandleObject(targetValue,out ItemRef movedItem))
            return MoveItemSlot(unit,movedItem,orderId-852002);
        if(vm.TryGetHandleObject(targetValue,out ItemRef item)
            && (orderId==851971||orderName.Equals("smart",StringComparison.OrdinalIgnoreCase)))
            return IssuePickupItem(unit,item)==PickupItemResult.Issued;
        return false;
    }

    Ability FindAbility(Unit unit,int orderId,string orderName,
        EventTargetKind targetKind)
    {
        int directionId = orderId != 0 ? orderId : OrderId(orderName);
        if(directionId==852173&&targetKind==EventTargetKind.Unit)
            return unit.Abilities.OfType<RuntimeFlamingArrowsAbility>().FirstOrDefault();
        if(directionId==852243&&targetKind==EventTargetKind.Unit)
            return unit.Abilities.OfType<RuntimeColdArrowsAbility>().FirstOrDefault();
        if(directionId is 852589 or 852590)return unit.GetAbility<RuntimeManaShieldAbility>();
        if(directionId is 852090 or 852091)return unit.GetAbility<RuntimeDivineShieldAbility>();
        if(directionId is 852507 or 852508)return unit.GetAbility<RuntimeCoupleAbility>();
        if(directionId is 852512 or 852513)return unit.GetAbility<RuntimeManaFlareAbility>();
        if(directionId is 852165 or 852166)return unit.GetAbility<RuntimeRootAbility>();
        if (RuntimeAlternateFormAbility.IsDirectionalOrder(directionId))
            return unit.Abilities.OfType<RuntimeAlternateFormAbility>()
                .FirstOrDefault(a => a.ForwardOrderId == directionId
                    || a.ReverseOrderId == directionId);
        if(orderName.Length>=4)
        {
            Ability byRawcode=unit.Abilities.Get(orderName[..4]);
            if(byRawcode is CAbilitySleepAlways)return null;
            if(byRawcode!=null)return byRawcode;
        }
        Ability exact=unit.Abilities.FirstOrDefault(a=>a is not CAbilitySleepAlways && (
            a.OrderId?.Equals(orderName,StringComparison.OrdinalIgnoreCase)==true
            ||orderId!=0&&(OrderId(a.OrderId)==orderId
                ||a is not RuntimeChannelAbility
                &&_learnedOrderNames.TryGetValue(orderId,out string learned)
                &&a.OrderId?.Equals(learned,StringComparison.OrdinalIgnoreCase)==true)));
        if(exact!=null)
        {
            ConsumePlayerAbilityHint(unit,exact);
            return RememberOrder(orderId,exact);
        }

        if(_lastAddedAbilityRawcodes.TryGetValue(unit,out string addedRawcode))
        {
            Ability added=unit.Abilities.Get(addedRawcode);
            if(added is not RuntimeChannelAbility
                &&IsOrderCandidate(added,targetKind,requireOrderId:false))
            {
                _lastAddedAbilityRawcodes.Remove(unit);
                return RememberOrder(orderId,added);
            }
        }

        // DotA AI wrappers expose the rawcode immediately before issuing the
        // numeric order through SetPlayerAbilityAvailable.
        int hintedId=Player(unit.PlayerId).LastEnabledAbilityId;
        Ability hinted=hintedId!=0
            ?unit.Abilities.Get(Rawcode(JassVm.IntegerValue(hintedId))):null;
        if(hinted is not RuntimeChannelAbility&&IsOrderCandidate(hinted,targetKind))
        {
            Player(unit.PlayerId).LastEnabledAbilityId=0;
            return RememberOrder(orderId,hinted);
        }

        // Channel's authored DataF is authoritative (native 3df660/4c68a0).
        // A failed AI order must not cast an unrelated Channel or teach that
        // order a global alias: maps may intentionally try several orders.
        // Script dummies normally carry Locust/timed life plus exactly one
        // active payload ability. Resolve that payload without a brittle
        // hand-written table, but refuse ambiguous hero orders.
        Ability[] candidates=unit.Abilities.Where(ability=>
            ability is not RuntimeChannelAbility
            &&IsOrderCandidate(ability,targetKind)).Take(2).ToArray();
        return candidates.Length==1?RememberOrder(orderId,candidates[0]):null;
    }

    static bool IsOrderCandidate(Ability ability,EventTargetKind targetKind,
        bool requireOrderId=true)
    {
        if(ability==null||ability is CAbilitySleepAlways||ability.Level<=0||ability.IsDisabled
            ||requireOrderId&&string.IsNullOrWhiteSpace(ability.OrderId))return false;
        return targetKind switch
        {
            EventTargetKind.Unit=>ability.TargetMode is AbilityTargetMode.Unit
                or AbilityTargetMode.UnitOrPoint or AbilityTargetMode.Any,
            EventTargetKind.Point=>ability.TargetMode is AbilityTargetMode.Point
                or AbilityTargetMode.UnitOrPoint or AbilityTargetMode.Any,
            EventTargetKind.Destructable=>ability.TargetMode is AbilityTargetMode.Unit
                or AbilityTargetMode.UnitOrPoint or AbilityTargetMode.Any or AbilityTargetMode.Destructable,
            _=>ability.TargetMode is AbilityTargetMode.None or AbilityTargetMode.Any,
        };
    }

    Ability RememberOrder(int orderId,Ability ability)
    {
        if(orderId!=0&&!string.IsNullOrWhiteSpace(ability?.OrderId))
            _learnedOrderNames[orderId]=ability.OrderId;
        return ability;
    }

    void ConsumePlayerAbilityHint(Unit unit,Ability ability)
    {
        PlayerRef player=Player(unit.PlayerId);
        if(player.LastEnabledAbilityId!=0
            &&ability?.Rawcode.Equals(
                Rawcode(JassVm.IntegerValue(player.LastEnabledAbilityId)),
                StringComparison.OrdinalIgnoreCase)==true)
            player.LastEnabledAbilityId=0;
    }

    bool UnresolvedOrder(Unit unit,int orderId,string orderName)
    {
        if(orderId!=0)_unresolvedOrderCounts[orderId]=
            _unresolvedOrderCounts.GetValueOrDefault(orderId)+1;
        _world.Log($"[jass-order] unresolved unit={unit?.TypeId}/{unit?.Name} "
            +$"order={(orderId!=0?$"0x{orderId:X}":orderName)} abilities="
            +string.Join(',',unit?.Abilities.Select(ability=>
                $"{ability.Rawcode}:{ability.OrderId}")??Array.Empty<string>()));
        return false;
    }

    static int CurrentOrderId(Unit unit)
    {
        if(unit==null||unit.Removed)return 0;
        COrder order=unit?.Current;
        if(order==null)return 0;
        if(order.JassOrderId!=0)return order.JassOrderId;
        return order switch
        {
            COrderTarget {IsAttackCommand:true} or COrderTarget {IsPointAttackCommand:true}=>851983,
            MovementExecution=>851986, CastOrder cast=>OrderId(cast.Ability?.OrderId),
            PlayerHarvestOrder harvest=>harvest.CurrentJassOrderId,
            // AMAI 的 IsConstructionOrder(order)=order>852678 证明真实 WC3
            // 建造中农民的当前订单就是建筑 rawcode(BuildOrder 已带); 此前
            // ConstructionWorkOrder 返回 0 会被当成 idle 单位。
            ConstructionWorkOrder work=>FourCC(work.Structure?.TypeId),
            _=>0
        };
    }

    // Native 51a500 allocates six slots. 53b1e0 uses the original soft
    // float-to-integer conversion followed by an unsigned minimum.
    const int DefaultInventorySlots=6;
    bool InventoryGrantsItemAbilities(Unit unit)=>InventoryDataFlag(unit,"DataC");
    bool InventoryDataFlag(Unit unit,string field)
    {
        if(unit==null)return false;
        var abilities=unit.Abilities.Items;
        for(int i=0;i<abilities.Count;i++)
        {
            Ability ability=abilities[i];
            string code=(ability.ObjectData?.GetString("code",ability.ObjectData.BaseId)??ability.Rawcode)?.Trim();
            if(string.Equals(code,"AInv",StringComparison.OrdinalIgnoreCase))
                return RuntimeAbilityFactory.ReadFloat(ability.ObjectData,field,Math.Max(1,ability.Level),0)!=0;
        }
        return false;
    }
    bool AutomaticallyUsesItem(Unit unit,ItemRef item)=>item.Powerup&&InventoryGrantsItemAbilities(unit);
    bool CanPickUpItem(Unit unit,ItemRef item)=>unit?.Alive==true&&unit.HasUsableInventory
        &&InventoryDataFlag(unit,"DataD")&&item!=null&&(!item.Powerup||InventoryGrantsItemAbilities(unit));
    bool CanDropItem(Unit unit,ItemRef item)=>unit?.Alive==true&&unit.HasUsableInventory
        &&InventoryDataFlag(unit,"DataE")&&item is {Removed:false,Droppable:true}&&item.Owner==unit;
    bool CanReceiveTransferredItem(Unit source,Unit target)=>source?.Alive==true&&target?.Alive==true
        &&source!=target&&target.HasUsableInventory
        &&(_world?.AreAllied(source,target)??source.Team==target.Team);
    void RemoveItemEquipment(Unit unit,ItemRef item)
    {
        if(!_consumingItems.Contains(item))CancelItemCasts(unit,item);
        if(item.EquipmentBonusApplied)
        {
            unit.RemoveItemBonus(item.Bonus);
            item.EquipmentBonusApplied=false;
        }
        RemoveEquippedItemAbilities(unit,item);
    }
    void CancelItemCasts(Unit unit,ItemRef item)
    {
        if(unit.Current is CastOrder order&&ReferenceEquals(order.Ability?.SourceItem,item))
            unit.CancelOrders();
        Ability channel=unit.Abilities.ChannelingAbility;
        foreach(Ability cast in _activeItemCasts.Concat(unit.Abilities.Snapshot())
            .Append(channel).Where(cast=>cast!=null&&cast.Owner==unit
                &&ReferenceEquals(cast.SourceItem,item)).Distinct().ToArray())
        {
            cast.CancelCast();
            // A portal order may still be approaching before StartCast; its
            // temporary ability then has no cast-finish hook to remove itself.
            if(cast is RuntimeTownPortalAbility&&unit.Abilities.Items.Contains(cast))
                unit.RemoveAbility(cast);
            _activeItemCasts.Remove(cast);
        }
    }
    int InventoryCapacity(Unit unit)
    {
        if(unit==null)return 0;
        var abilities=unit.Abilities.Items;
        for(int i=0;i<abilities.Count;i++)
        {
            Ability ability=abilities[i];
            // Channel DataE temporarily withdraws inventory commands (548a90),
            // not its slots. Cargo likewise retains the passenger's storage.
            if(ability.IsDisabledExcept(AbilityDisableType.Cargo|AbilityDisableType.Illusion
                |AbilityDisableType.Channel))continue;
            string code=(ability.ObjectData?.GetString("code",ability.ObjectData.BaseId)??ability.Rawcode)?.Trim();
            if(!string.Equals(code,"AInv",StringComparison.OrdinalIgnoreCase))continue;
            float value=RuntimeAbilityFactory.ReadFloat(ability.ObjectData,"DataA",Math.Max(1,ability.Level),0);
            uint bits=BitConverter.SingleToUInt32Bits(value);
            int exponent=(int)((bits>>23)&255);
            if(exponent<127)return 0;
            uint mantissa=(bits&0x7fffff)|0x800000;
            uint magnitude=exponent<=150?mantissa>>(150-exponent):mantissa<<(exponent-150);
            uint slots=(bits&0x80000000)!=0?unchecked(0u-magnitude):magnitude;
            return (int)Math.Min(slots,(uint)DefaultInventorySlots);
        }
        return 0;
    }

    List<ItemRef> Inventory(Unit unit)
    {
        if(!_inventories.TryGetValue(unit,out List<ItemRef> items))
        {
            int capacity=InventoryCapacity(unit);
            _inventories[unit]=items=Enumerable.Repeat<ItemRef>(null,capacity).ToList();
            return items;
        }
        // Lifecycle notifications reconcile occupied slots. A plain inventory
        // read can resize empty storage without generating an extra drop event.
        int wanted=InventoryCapacity(unit);
        if(wanted>items.Count)items.AddRange(Enumerable.Repeat<ItemRef>(null,wanted-items.Count));
        if(wanted<items.Count)
        {
            bool occupied=false;
            for(int i=wanted;i<items.Count;i++)
                if(items[i]!=null){occupied=true;break;}
            if(!occupied)items.RemoveRange(wanted,items.Count-wanted);
        }
        return items;
    }
    readonly HashSet<Unit> _reconcilingInventories=new();
    void ReconcileUnitInventory(Unit unit)
    {
        // Ordinary live-owner path of 54e170/523460. Special native owner/death
        // flags and carrier boundary policy require their own lifecycle mapping.
        if(unit?.Alive!=true||!_inventories.TryGetValue(unit,out List<ItemRef> items)
            ||!_reconcilingInventories.Add(unit))return;
        try
        {
            int capacity=InventoryCapacity(unit);
            // Native slots are cleared from high to low before drop notification.
            // Snapshot protects the scan from equipped-ability removals and callbacks.
            foreach(ItemRef item in items.Skip(capacity).Reverse().Where(item=>item!=null).ToArray())
                DropItem(unit,item,force:true,clearSlotBeforeEvent:true);
            if(items.Count>capacity&&items.Skip(capacity).All(item=>item==null))
                items.RemoveRange(capacity,items.Count-capacity);
            if(items.Count<capacity)items.AddRange(Enumerable.Repeat<ItemRef>(null,capacity-items.Count));
        }
        finally{_reconcilingInventories.Remove(unit);}
    }
    void ReleaseEmptyRetiredInventory(Unit unit,List<ItemRef> inventory)
    {
        if(unit.Removed&&inventory.TrueForAll(static item=>item==null))
            _inventories.Remove(unit);
    }

    /// <summary>
    /// 物品原型是否 Powerup(审计 F16)。判定口径必须与 <see cref="CreateItem"/> 一致,
    /// 否则 IsItemIdPowerup 与 IsItemPowerup 会给出矛盾结果。
    /// 只读取独立 powerup 标志；分类不能代替缺失字段。
    /// </summary>
    bool IsItemIdPowerup(string rawcode)
    {
        ItemTypeData data=_objects?.GetItem(rawcode);
        if(data==null)return false;
        return data.GetInt("powerup",0,0,0)!=0;
    }

    ItemRef CreateScriptItem(int typeId,Vec2 position)
    {
        // Editor main recreates placements from war3mapUnits.doo. Claim each
        // original once; runtime creates, inventory grants and drops stay new.
        if(_mapBootstrap)
            foreach(ItemRef item in _preplacedItemCandidates)
                if(!item.Removed&&item.Owner==null&&item.TypeId==typeId
                    &&Vec2.Dist(item.Position,position)<1f
                    &&_claimedPreplacedItems.Add(item))return item;
        return CreateItem(typeId,position);
    }

    ItemRef CreateItem(int typeId,Vec2 position)
    {
        ItemTypeData data=_objects?.GetItem(Rawcode(JassVm.IntegerValue(typeId)));
        // Warcraft returns null for an invalid item type. Keeping an ItemRef here made
        // failed shop/recipe paths leak a BTNTemp placeholder into the inventory.
        if(_objects!=null&&data==null)return null;
        var item=new ItemRef(typeId,position){SpawnTick=_world.Tick};
        if(data!=null)
        {
            item.Name=data.GetString("Name",data.Id);
            item.Level=data.GetInt("Level",0,0,data.GetInt("level",0,0,0));
            item.Charges=ReadItemUses(data);
            item.Pawnable=data.GetInt("pawnable",0,0,1)!=0;
            item.Sellable=data.GetInt("sellable",0,0,1)!=0;
            // Native 6394e0 initializes independent droppable (0x400) and
            // sellable (0x800) flags from their respective ItemData columns.
            item.Droppable=data.GetInt("droppable",0,0,1)!=0;
            item.Type=ItemTypeId(data.GetString("class",""));
            // Powerup 必须读**物编的 powerup 字段**(审计 F16), 不能用 class=="PowerUp"
            // 推导: 稀疏地图物编中的 class=PowerUp 可以是正常装备；缺失
            // powerup 表示没有自动使用标志，不应把分类反推成自动使用。
            item.Powerup=data.GetInt("powerup",0,0,0)!=0;
            // 审计 F14: 读物编 HP 列作为物品最大生命值。此前 Life 上限恒为 1,
            // 使 rat3.HP=75 之类的物品实际仍是 1 点生命, 脚本无法按真实血量操作。
            // GetFloat 需要 (field, level, dataPointer, fallback) —— 物品表按 level 1
            // 存 HP(与 rat3 一致), 取 level=1 才是该物品自身的值。
            item.MaxLife=data.GetFloat("HP",1,0,1f);
            if(item.MaxLife<=0f)item.MaxLife=1f;
            item.Life=item.MaxLife;
            item.Bonus=ItemBonusReader.Read(_objects,data);
            item.NativeChargeState.Flags=NativeItemCharges.Initialize(item.NativeChargeState.Flags,
                item.Charges,data.GetInt("usable",0,0,0),data.GetInt("perishable",0,0,0),
                item.Droppable?1:0,item.Powerup?1:0,item.Sellable?1:0,item.Pawnable?1:0);
        }
        _items.Add(item);
        _stockPurchaseAttempt?.Created.Add(item);
        return item;
    }

    ItemRef PlaceRandomItem(ItemPoolRef pool,Vec2 position)
    {
        if(pool?.Destroyed!=false)return null;
        double total=pool.Entries.Sum(entry=>Math.Max(0,entry.Weight));
        if(total<=0)return null;
        double roll=_world.Rng.NextDouble()*total;
        foreach(ItemPoolEntry entry in pool.Entries)
        {
            roll-=Math.Max(0,entry.Weight);
            if(roll<0)return CreateItem(entry.TypeId,position);
        }
        return CreateItem(pool.Entries[^1].TypeId,position);
    }
    bool AddItem(Unit unit,ItemRef item,int requestedSlot=-1,bool fireEvent=true,bool allowFullStackMerge=true,bool animateGroundDeparture=true)
    {
        if(unit?.Alive!=true||!unit.HasUsableInventory||item==null||item.Removed||_removingItems.Contains(item))return false;
        if(item.IsIllusionCopy&&(item.Owner!=null||!unit.IsIllusion))return false;
        // Native 651470 rejects powerups without DataC even for forced scripts.
        if(item.Powerup&&!InventoryGrantsItemAbilities(unit))return false;
        if(AutomaticallyUsesItem(unit,item))
        {
            if(item.Owner!=null)
            {
                Unit previousOwner=item.Owner;
                List<ItemRef> previous=Inventory(previousOwner);
                int previousSlot=previous.IndexOf(item);
                if(previousSlot>=0)previous[previousSlot]=null;
                RemoveItemEquipment(previousOwner,item);
                ReleaseEmptyRetiredInventory(previousOwner,previous);
            }
            item.Owner=unit; item.PlayerId=unit.PlayerId; item.Position=unit.Pos;
            // Native powerup binder executes admitted children, notifies pickup,
            // then consumes. Pickup triggers observe the effect and the live item.
            bool used=UseItem(unit,item,beforeConsume:fireEvent?()=>
                _world.Events.Fire(new EventContext { Event=GameEvent.ItemPickedUp,
                    Source=unit,Target=unit,ManipulatedItem=item }):null);
            if(!used&&item.Owner==unit)item.Owner=null;
            if(_stockPurchaseAttempt?.PlayerId==unit.PlayerId)_stockPurchaseAttempt.Delivered=true;
            return true;
        }
        List<ItemRef> target=Inventory(unit);
        if(item.Owner==unit)
        {
            if(!_detachingItems.Contains(item))return true;
            // A drop callback can return a bound item to the same carrier.
            // Release its old slot before allocating, including a full bag.
            if(requestedSlot>=target.Count||requestedSlot>=0&&target[requestedSlot]!=null
                &&!ReferenceEquals(target[requestedSlot],item))return false;
            int previous=target.IndexOf(item);
            if(previous>=0)target[previous]=null;
        }
        int slot;
        if(requestedSlot>=0)
        {
            // 指定了格位就必须尊重它。原版 UnitAddItem 走到指定格被占用时直接
            // 返回失败, 不会退而求其次去别的空位(Game.dll 6f1fa7b0 ->
            // 6f64bf00 -> 6f524870, 0x6f5248c4 检查到非空即跳向 false 分支)。
            // 自动改塞其它格会让 RPG 的固定装备位/合成替换读写错格(审计 F02)。
            if(requestedSlot>=target.Count||target[requestedSlot]!=null)return false;
            slot=requestedSlot;
        }
        else
        {
            slot=target.FindIndex(existing=>existing==null);
            if(slot<0)
            {
                // 满栏: 仅同类型"可充能"物品允许合并(充能叠入现有堆叠), 否则购买失败。
                // 无限使用(Charges<=0)与唯一物品无法合并, 必须失败 —— 由调用方
                // (PurchaseStock 的 RemoveItem / UnitAddItemById 的清理)回收孤儿物品,
                // 杜绝商店脚下堆积。
                ItemRef stack=allowFullStackMerge?target.FirstOrDefault(existing=>existing!=null
                    &&existing.TypeId==item.TypeId&&existing.Charges>0&&item.Charges>0):null;
                if(stack==null)
                {
                    if(_stockPurchaseAttempt?.PlayerId==unit.PlayerId)
                        _stockPurchaseAttempt.InventoryFull=true;
                    return false;
                }
                stack.Charges+=item.Charges;
                if(_stockPurchaseAttempt?.PlayerId==unit.PlayerId)_stockPurchaseAttempt.Delivered=true;
                RemoveItem(item);
                return true;
            }
        }
        if(item.Owner!=null)
        {
            List<ItemRef> previous=Inventory(item.Owner);
            int old=previous.IndexOf(item);
            if(old>=0)previous[old]=null;
            RemoveItemEquipment(item.Owner,item);
            ReleaseEmptyRetiredInventory(item.Owner,previous);
        }
        // 从地面被拾取时留一份离场快照(I-92/I-95): 原版物品是"被吸走"的 ——
        // 播一段 Death 序列并缩小消失, 不是瞬间从画面里消失。
        if(animateGroundDeparture&&item.Owner==null&&!item.IsIllusionCopy)BeginGroundItemDepart(item);
        item.Owner=unit; item.PlayerId=unit.PlayerId; item.Position=unit.Pos;
        var cooldown=EffectiveItemCooldown(item);
        item.CooldownReadyTick=cooldown.ReadyTick;
        item.CooldownTotal=cooldown.Total;
        target[slot]=item;
        if(_detachingItems.Contains(item))_reattachedDuringDrop.Add(item);
        if(_stockPurchaseAttempt?.PlayerId==unit.PlayerId)_stockPurchaseAttempt.Delivered=true;
        if(InventoryGrantsItemAbilities(unit))
        {
            unit.ApplyItemBonus(item.Bonus);
            item.EquipmentBonusApplied=true;
            AddEquippedItemAbilities(unit,item);
        }
        if(fireEvent)
            _world.Events.Fire(new EventContext { Event=GameEvent.ItemPickedUp,Source=unit,
                Target=unit,ManipulatedItem=item });
        return true;
    }
    bool ScriptAddItem(Unit unit,ItemRef item)
    {
        if(unit?.Alive!=true||!unit.HasUsableInventory||item==null||item.Removed||_removingItems.Contains(item))return false;
        if(item.IsIllusionCopy&&(item.Owner!=null||!unit.IsIllusion))return false;
        if(item.Powerup&&!InventoryGrantsItemAbilities(unit))return false;
        // 1fa610 resolves the old carrier and calls 6775b0 before 64be90
        // receives the item. Map equipment triggers need that detach notification.
        bool hadCarrier=item.Owner!=null;
        if(item.Owner!=null&&!_detachingItems.Contains(item))
        {
            if(!DropItem(item.Owner,item,force:true)||item.Removed)return false;
            // A drop callback may have already rebound the item or removed it.
            if(item.Owner!=null)return item.Owner==unit;
        }
        return AddItem(unit,item,animateGroundDeparture:!hadCarrier);
    }

    readonly HashSet<ItemRef> _detachingItems=new();
    readonly HashSet<ItemRef> _reattachedDuringDrop=new();
    readonly HashSet<ItemRef> _removingItems=new();
    readonly HashSet<ItemRef> _consumingItems=new();
    bool DropItem(Unit unit,ItemRef item,bool force=false,bool clearSlotBeforeEvent=false)
    {
        if(unit==null||item?.Owner!=unit||!force&&!CanDropItem(unit,item))return false;
        if(item.IsIllusionCopy){RemoveItem(item);return false;}
        if(!_detachingItems.Add(item))return false;
        try
        {
            List<ItemRef> inventory=Inventory(unit);
            int slot=inventory.IndexOf(item);
            if(clearSlotBeforeEvent&&slot>=0)inventory[slot]=null;
            // Manual detach 549070 clears the slot AFTER 63d220's notification.
            // Death, capacity reconciliation and unit removal clear it BEFORE.
            // Both notify before removing item abilities and the owner reference.
            _world.Events.Fire(new EventContext { Event=GameEvent.ItemDropped,Source=unit,
                Target=unit,ManipulatedItem=item });
            if(!_reattachedDuringDrop.Contains(item)&&item.Owner==unit)
            {
                RemoveItemEquipment(unit,item);
                item.Owner=null; item.Position=unit.Pos;
            }
            if(!clearSlotBeforeEvent&&slot>=0&&slot<inventory.Count
                &&ReferenceEquals(inventory[slot],item)&&!_reattachedDuringDrop.Contains(item))inventory[slot]=null;
            ReleaseEmptyRetiredInventory(unit,inventory);
            return true;
        }
        finally{_detachingItems.Remove(item);_reattachedDuringDrop.Remove(item);}
    }
    void DropDeathItems(Unit unit)
    {
        if(unit==null||!_inventories.TryGetValue(unit,out List<ItemRef> inventory))return;
        // 5413d0 returns before the death-drop chain for an illusion owner.
        // Its copied handles are reclaimed when the image is fully removed.
        if(unit.IsIllusion)return;
        // Native 5413d0 reads DataB: Aien drops everything, AInv normally
        // retains equipment unless the individual item has its death-drop flag.
        bool dropAll=InventoryDataFlag(unit,"DataB");
        foreach(ItemRef item in inventory.Where(item=>item!=null&&(dropAll||item.DropOnDeath)).Reverse().ToArray())
            DropItem(unit,item,force:true,clearSlotBeforeEvent:true);
    }

    // Blizzard.j helpers, used only when the script library is absent.
    //
    // Weights are not percentages: Blizzard.j normalizes their sum and keeps
    // explicit 0/-1 no-item entries. Empty/unmatched distributions return -1.
    // Use the same RNG as GetRandomInt, including its invalid-range behavior.
    readonly List<(int ItemId,int Chance)> _randomDist=new();

    void RandomDistReset()=>_randomDist.Clear();

    void RandomDistAddItem(int itemId,int chance)
    {
        _randomDist.Add((itemId,chance));
    }

    int RandomDistChoose()
    {
        if(_randomDist.Count==0)return -1;
        int total=0;
        foreach((int _,int chance)in _randomDist)total=unchecked(total+chance);
        int roll=_world.ScriptRandom.GetInteger(1,total);
        int cumulative=0;
        foreach((int itemId,int chance)in _randomDist)
        {
            cumulative=unchecked(cumulative+chance);
            if(roll<=cumulative)return itemId;
        }
        return -1;
    }

    void DropConfiguredItems(Unit unit)
    {
        if(unit==null||!_unitDropSets.Remove(unit,out IReadOnlyList<MapItemDropSet> sets))
            return;
        DropConfiguredItems(sets,unit.Pos);
    }

    // 掉落散布: 原版多个物品掉在单位周围随机偏移的位置, 半径约 ±30 世界单位。
    // 用模拟层 RNG(_world.Rng)而非 Random, 保证 headless 与有渲染运行结果一致。
    Vec2 ScatterDropPosition(Vec2 origin)
    {
        double angle=_world.Rng.NextDouble()*Math.PI*2;
        double radius=_world.Rng.NextDouble()*30.0;
        return new Vec2(origin.X+(float)(Math.Cos(angle)*radius),
            origin.Y+(float)(Math.Sin(angle)*radius));
    }

    // 物品冷却(I-44)。原版物品冷却来自其 abilList 能力的 Cool 字段。
    // 关键: 冷却状态必须记在**物品**上, 不能设在 UseItem 里那个临时能力对象上 ——
    // 那个对象用完即弃, 下次使用会新建一个 CdRemaining=0 的能力, 冷却立刻失效。
        readonly Dictionary<(Unit Owner,string Group),(int ReadyTick,float Total)> _itemGroupCooldowns=new();

        string ItemCooldownGroup(ItemRef item)
        {
            string group=GetItemRuntimeData(item)
                ?.GetString("cooldownID","").Trim();
            return string.IsNullOrEmpty(group)||group is "_" or "-"?null:group;
        }

        (int ReadyTick,float Total) EffectiveItemCooldown(ItemRef item)
        {
            if(item==null)return default;
            var result=(ReadyTick:item.CooldownReadyTick,Total:item.CooldownTotal);
            string group=ItemCooldownGroup(item);
            if(item.Owner!=null&&group!=null
                &&_itemGroupCooldowns.TryGetValue((item.Owner,group),out var shared)
                &&shared.ReadyTick>result.ReadyTick)result=shared;
            return result;
        }

        void BeginItemCooldown(ItemRef item,Ability ability)
        {
            if(item==null||ability==null||ability.Cooldown<=0)return;
            item.CooldownTotal=ability.Cooldown;
            item.CooldownReadyTick=_world.Tick
                +(int)Math.Ceiling(ability.Cooldown/Math.Max(0.01f,World.TickSeconds));
            string group=ItemCooldownGroup(item);
            if(item.Owner!=null&&group!=null)
            {
                _itemGroupCooldowns[(item.Owner,group)]=(item.CooldownReadyTick,item.CooldownTotal);
                // Keep the cooldown on all carried copies when they are dropped or traded.
                foreach(ItemRef sibling in Inventory(item.Owner))
                    if(sibling!=null&&ItemCooldownGroup(sibling)==group
                        &&sibling.CooldownReadyTick<item.CooldownReadyTick)
                    {
                        sibling.CooldownReadyTick=item.CooldownReadyTick;
                        sibling.CooldownTotal=item.CooldownTotal;
                    }
            }
        }

        // 剩余冷却秒数; <=0 表示可用。
        public float ItemCooldownRemaining(ItemRef item)=>item==null?0f
            :Math.Max(0f,(EffectiveItemCooldown(item).ReadyTick-_world.Tick)*World.TickSeconds);

        // 冷却进度比例(1=刚用完, 0=可用), 供物品栏画遮罩。
        public float ItemCooldownRatio(ItemRef item)
        {
            float total=EffectiveItemCooldown(item).Total;
            if(total<=0f)return 0f;
            return Math.Clamp(ItemCooldownRemaining(item)/total,0f,1f);
        }

    void DropConfiguredItems(IReadOnlyList<MapItemDropSet> sets,Vec2 position)
    {
        foreach(MapItemDropSet set in sets??Array.Empty<MapItemDropSet>())
        {
            int roll=_world.Rng.Next(100);
            int cumulative=0;
            foreach(MapItemDrop candidate in set.Items)
            {
                cumulative+=Math.Clamp(candidate.Chance,0,100);
                if(roll>=cumulative)continue;
                string typeId=candidate.TypeId;
                int itemType=ResolvePlacedRandomItem(typeId);
                // 原版掉落物在单位周围随机散布, 不会全部叠在同一个点(视觉上像一坨)。
                if(itemType!=0)CreateItem(itemType,ScatterDropPosition(position));
                break;
            }
        }
    }

    // 单位类型固有掉落表 (UnitBalance.slk 的 drop 列): 形如 "itemA:chanceA,itemB:chanceB"，
    // 每个条目按几率独立掷骰掉落 (对标原版掉落表)。地图未放置掉落集的单位也走这里。
    void DropBalanceItems(Unit unit)
    {
        // Images share the source TypeId, but must not generate fresh equipment
        // from its default drop table when killed, expired or replaced on recast.
        if(unit==null||unit.IsIllusion)return;
        UnitTypeData data=_objects?.Get(unit.TypeId);
        if(data==null)return;
        string drop=data.GetString("drop","");
        if(string.IsNullOrWhiteSpace(drop))return;
        foreach(string entry in drop.Split(',',
            StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))
        {
            string itemId=entry;
            float chance=100f;
            int sep=entry.IndexOf(':');
            if(sep<0)sep=entry.IndexOf('*');
            if(sep>=0)
            {
                itemId=entry[..sep];
                float.TryParse(entry[(sep+1)..],out chance);
            }
            if(string.IsNullOrWhiteSpace(itemId)||itemId=="_")continue;
            if(_world.Rng.NextDouble()*100f<chance)
            {
                int itemType=ResolvePlacedRandomItem(itemId);
                if(itemType!=0)CreateItem(itemType,unit.Pos);
            }
        }
    }

    int ResolvePlacedRandomItem(string rawcode)
    {
        if(rawcode?.Length!=4||rawcode.All(c=>c=='\0'))return 0;
        // units.doo encodes editor "random class/level" entries as YiI1,
        // YjI2, YkI1... (class index starts at permanent=0). Passing that
        // sentinel to CreateItem fails because it is not an actual item type.
        if(rawcode[0]=='Y'&&rawcode[2]=='I'
            &&rawcode[1] is >= 'i' and <= 'p'
            &&rawcode[3] is >= '0' and <= '9')
            return ChooseRandomItemType(rawcode[1]-'i',rawcode[3]-'0');
        return FourCC(rawcode);
    }

    int ChooseRandomItemType(int type,int level)
    {
        ItemTypeData[] candidates=(_objects?.Items??Array.Empty<ItemTypeData>())
            // Game.dll 639b20: ANY (8) bypasses class; the legacy random
            // selector (7) uses oldLevel. Only -1 bypasses the level test.
            .Where(item=>(level==-1||item.GetInt(type==7?"oldLevel":"Level",0,0,0)==level)
                &&(type is 7 or 8||ItemTypeId(item.GetString("class",""))==type)
                &&item.GetInt("pickRandom",0,0,0)!=0)
            .OrderBy(item=>item.Id,StringComparer.Ordinal).ToArray();
        return candidates.Length==0?0:FourCC(candidates[_world.Rng.Next(candidates.Length)].Id);
    }

    void AwardKillBounty(EventContext context)
    {
        if(context?.SuppressKillBounty==true)return;
        Unit killed=context?.Target;
        Unit killer=context?.Killer??context?.Source;
        if(!TryRollKillBounty(killed,killer,out int gold,out int lumber))return;
        AwardFeedbackResources(killer,killed,gold,lumber);
    }

    int RollResourceBounty(int baseAmount,int dice,int sides)
    {
        long amount=Math.Max(0,baseAmount);
        if(sides>0)for(int index=0;index<Math.Max(0,dice);index++)amount+=_world.Rng.Next(1,sides+1);
        return (int)Math.Min(int.MaxValue,amount);
    }

    bool TryRollKillBounty(Unit killed,Unit killer,out int gold,out int lumber)
    {
        gold=lumber=0;
        if(killed==null||killer==null||killed.IsIllusion||killed.PlayerId==killer.PlayerId
            ||PlayerAllied(killer.PlayerId,killed.PlayerId))return false;
        PlayerRef defeated=Player(killed.PlayerId);
        // Neutral Hostile gives bounty by default in melee. Some generated map
        // scripts omit the bj_melee initialization call; still honor an explicit
        // SetPlayerState(...GIVES_BOUNTY, false).
        if(defeated.States.TryGetValue(7,out int givesBounty)
            ?givesBounty==0:killed.PlayerId!=12)return false;
        gold=RollResourceBounty(killed.GoldBountyBase,killed.GoldBountyDice,killed.GoldBountySides);
        lumber=RollResourceBounty(killed.LumberBountyBase,killed.LumberBountyDice,killed.LumberBountySides);
        return gold>0||lumber>0;
    }

    void AwardFeedbackResources(Unit receiver,Unit anchor,int gold,int lumber)
    {
        // 64cfa0 -> 23db10/23db50 requests upkeep and player tax, but displays
        // the original gross amount. Gold wins when both currencies are nonzero.
        foreach(var entry in new[]{(State:1,Amount:gold),(State:2,Amount:lumber)})
        {
            if(entry.Amount<=0)continue;
            AwardPillageResource(receiver.PlayerId,entry.State,entry.Amount);
            _world.Events.Fire(new EventContext {Event=GameEvent.PlayerStateChanged,
                TriggerPlayerId=receiver.PlayerId,PlayerStateId=entry.State});
        }
        if(CanPlayerControl(LocalPlayerId,receiver,advanced:true))
            FloatingText.ShowBounty(anchor,gold!=0?gold:lumber,lumber:gold==0);
    }

    void AwardTransmuteResources(Unit source,Unit victim,int gold,int lumber,bool allowBounty)
    {
        if(victim.IsIllusion)return;
        if(allowBounty&&TryRollKillBounty(victim,source,out int bountyGold,out int bountyLumber))
        {
            gold=SaturatingAdd(gold,bountyGold);lumber=SaturatingAdd(lumber,bountyLumber);
        }
        AwardFeedbackResources(source,victim,gold,lumber);
    }
    static readonly HashSet<string> PermanentItemEffectCodes=new(StringComparer.OrdinalIgnoreCase)
    {
        "AIAB","AISM","AIIM","AIXM","AISR","AIXS","AIIM2","AISM2","AIAM","AIAA","AIMI","AISI","AISB","AIMS","AIMM","AIMR","AIEM","ARET","AIGO","AILU","AIMO","AILP","AILM","AISP","AINV","AIGL","AIRI","AANS"
    };

    // Native flag and mana-regeneration children exist, but +98 auto-use always rejects.
    static readonly HashSet<string> RejectedPowerupChildCodes=new(StringComparer.Ordinal)
    {
        "AIfl","AIfm","AIfn","AIfo","AIfe","AIrm"
    };

    bool UseAutomaticPowerupChildren(Unit unit,ItemRef item,Vec2 point,Action beforeConsume)
    {
        var data=GetItemRuntimeData(item);
        int created=0;bool succeeded=false;
        foreach(string rawcode in RuntimeItemAbilityResolution.ReadAbilityIds(data))
        {
            if(created==4)break;
            AbilityTypeData childData=ItemAbilityDefinition(item,rawcode);
            if(childData==null)continue;
            string code=RuntimeAbilityFactory.ResolveBaseCode(childData);
            if(RejectedPowerupChildCodes.Contains(code)){created++;continue;}
            // Permanent item effects use the same interpreter as manual items.
            // Unimplemented generic records cannot stand in for created runtime children.
            if(!PermanentItemEffectCodes.Contains(code)&&!RuntimeAbilityFactory.IsItemFigurineAbility(childData)
                &&CreateInventoryItemAbility(rawcode,childData) is null or ObjectDataAbility)continue;
            created++;
            if(UseItem(unit,item,point,powerupChild:rawcode,deferCompletion:true))succeeded=true;
        }
        if(created>0&&!succeeded)return false;
        ConsumeItemCharge(item,beforeConsume);
        _world.Events.Fire(new EventContext { Event=GameEvent.ItemUsed,Source=unit,
            Target=unit,Point=point,ManipulatedItem=item });
        return true;
    }

    bool UseItem(Unit unit,ItemRef item,Vec2 point=default,JassValue targetValue=null,
        JassVm vm=null,Unit targetUnit=null,Action beforeConsume=null,string powerupChild=null,bool deferCompletion=false,ItemRef targetItem=null)
    {
        if(unit==null||item?.Owner!=unit||item.Removed)return false;
        // Aien carries items without installing their abilities (DataC=0).
        // Native 63ab40 produces no slot-use order without an active item child;
        // resolving metadata here must not manufacture that missing ability.
        if(!unit.HasUsableInventory||!InventoryGrantsItemAbilities(unit))return false;
        // 冷却中的物品不可再使用(回城卷轴/传送法杖等)。冷却记在物品上,
        // 见 BeginItemCooldown —— 临时能力对象用完即弃, 记不住状态。
        if(powerupChild==null&&ItemCooldownRemaining(item)>0f)return false;
        if(item.Powerup&&powerupChild==null)
            return UseAutomaticPowerupChildren(unit,item,unit.Pos,beforeConsume);
        var request = ResolveItemUseRequest(unit, item, point, targetValue, vm, targetUnit,
            beforeConsume, powerupChild, deferCompletion, targetItem);
        if (TryApproachItemPlacement(ref request) is bool placementResult) return placementResult;
        if (request.targetItem != null) return UseItemWidgetTarget(request);
        if (TryUseBuildingItem(request) is bool buildResult) return buildResult;
        if (TryUseTownPortalItem(request) is bool portalResult) return portalResult;
        if (TryUseRestorationItem(request) is bool restoreResult) return restoreResult;
        if (TryUseSummoningItem(request) is bool summonResult) return summonResult;
        // This legacy helper returns false for both unrecognized and rejected effects.
        // Preserve its existing fallback contract until that family is audited separately.
        if (TryExecutePermanentItemAbility(unit, item, request.sequence, request.target,
                request.point, beforeConsume, deferCompletion)) return true;
        if (TryUseRuntimeItemAbility(request) is bool runtimeResult) return runtimeResult;
        return UseScriptItemCarrier(request);
    }

    bool TryExecutePermanentItemAbility(Unit unit,ItemRef item,
        IEnumerable<string> abilitySequence,Unit target,Vec2 point,Action beforeConsume=null,bool deferCompletion=false)
    {
        foreach(string rawcode in abilitySequence)
        {
            AbilityTypeData data=ItemAbilityDefinition(item,rawcode);
            string baseCode=RuntimeAbilityFactory.ResolveBaseCode(data);
            bool nativeAttributeMod=baseCode.Equals("AIxm",StringComparison.OrdinalIgnoreCase);
            int a=nativeAttributeMod
                ?JassNativeReal.ToIntegerUnchecked(RuntimeAbilityFactory.ReadFloat(data,"DataA",1,0))
                :RuntimeAbilityFactory.ReadInt(data,"DataA",1,0);
            int b=nativeAttributeMod
                ?JassNativeReal.ToIntegerUnchecked(RuntimeAbilityFactory.ReadFloat(data,"DataB",1,0))
                :RuntimeAbilityFactory.ReadInt(data,"DataB",1,0);
            int c=nativeAttributeMod
                ?JassNativeReal.ToIntegerUnchecked(RuntimeAbilityFactory.ReadFloat(data,"DataC",1,0))
                :RuntimeAbilityFactory.ReadInt(data,"DataC",1,0);
            Hero hero=unit.Abilities.Get<Hero>();
            var context = new PermanentItemEffectContext(unit, hero, target, point, data, a, b, c);
            PermanentItemEffectResult result = ApplyPermanentItemEffect(baseCode.ToUpperInvariant(), context);
            if (result == PermanentItemEffectResult.Rejected) return false;
            if (result == PermanentItemEffectResult.Unmatched) continue;
            hero?.Recalculate();
            Vec2 effectPoint=target?.Pos??(point.X==0&&point.Y==0?unit.Pos:point);
            var presentation=new ObjectDataAbility { Owner=unit, SourceItem=item };
            presentation.BindObjectData(rawcode,data,initialLevel:1,learnable:false);
            presentation.PlayInstantItemEffect(target??unit,effectPoint);
            if(!deferCompletion)CompleteItemUse(item,new EventContext { Event=GameEvent.ItemUsed,Source=unit,
                Target=unit,Point=effectPoint,OrderTargetUnit=target,
                ManipulatedItem=item },beforeConsume);
            return true;
        }
        return false;
    }

    void AddItemResource(Unit unit,int state,int amount)
    {
        PlayerRef player=Player(unit.PlayerId);
        int previous=player.States.GetValueOrDefault(state);
        long value=(long)previous+Math.Max(0,amount);
        player.States[state]=(int)Math.Min(int.MaxValue,value);
        int credited=player.States[state]-previous;
        FirePlayerStateEvents(player,state);
        _world.Events.Fire(new EventContext { Event=GameEvent.PlayerStateChanged,
            TriggerPlayerId=player.Id,PlayerStateId=state });
        if(credited>0&&CanPlayerControl(LocalPlayerId,unit,advanced:true))
            FloatingText.ShowResource(unit,credited,lumber:state==2);
    }

    sealed class PlacementItemOrder(JassGameNatives owner,ItemRef item,Vec2 point,float range):COrder
    {
        MovementExecution _move;
        public override void OnStart(Unit unit)
        {
            float radius=range+unit.CollisionSize;
            _move=new MovementExecution(point,completionRadius:radius,searchGoalRadius:radius);
            _move.OnStart(unit);
        }
        public override void OnEnd(Unit unit,bool interrupted)=>_move?.OnEnd(unit,interrupted);
        public override COrder Execute(Unit unit,float dt)
        {
            if(item.Removed||item.Owner!=unit||!unit.Alive||unit.Snared||!unit.CanMove
                ||owner.ItemCooldownRemaining(item)>0)return null;
            bool arrived=MathF.Max(0,Vec2.Dist(unit.Pos,point)-unit.CollisionSize)<=range;
            if(!arrived)
            {
                bool moving=_move.Execute(unit,dt)!=null;
                arrived=MathF.Max(0,Vec2.Dist(unit.Pos,point)-unit.CollisionSize)<=range;
                if(!arrived)return moving?this:null;
            }
            if(arrived)
            {
                owner.UseItem(unit,item,point);
            }
            return null;
        }
        public override string ToString()=> $"使用物品 {item.Name}";
    }

    sealed class PlayerPickupItemOrder(JassGameNatives owner,ItemRef item):COrder
    {
        float PickupRange=>(owner._world?.GameplayConfig??War3GameplayConfig.Default).PickupItemRange;
        MovementExecution _move;
        public override void OnStart(Unit unit)
        {
            _move=new MovementExecution(item.Position,completionRadius:PickupRange,searchGoalRadius:PickupRange);
            _move.OnStart(unit);
        }
        public override void OnEnd(Unit unit,bool interrupted)=>_move?.OnEnd(unit,interrupted);
        public override COrder Execute(Unit unit,float dt)
        {
            if(item.Removed||item.Owner!=null||!item.Visible||!owner.CanPickUpItem(unit,item))return null;
            if(Vec2.Dist(unit.Pos,item.Position)<=PickupRange)
            {
                owner.AddItem(unit,item);
                return null;
            }
            // A small late relocation can put the item beyond the old route's
            // completion circle. Pickup must refresh every changed item point.
            _move.UpdateTarget(item.Position,0f,PickupRange);
            return _move.Execute(unit,dt)==null?null:this;
        }
        public override string ToString()=> $"拾取物品 {item.Name}";
    }

    sealed class PlayerDropItemPointOrder(JassGameNatives owner,ItemRef item,
        Vec2 destination):COrder
    {
        const float DropRange=96f;
        MovementExecution _move;

        public override void OnStart(Unit unit)
        {
            _move=new MovementExecution(destination,completionRadius:DropRange);
            _move.OnStart(unit);
        }

        public override void OnEnd(Unit unit,bool interrupted)=>
            _move?.OnEnd(unit,interrupted);

        public override COrder Execute(Unit unit,float dt)
        {
            if(item.Removed||item.Owner!=unit||!item.Droppable)return null;
            if(Vec2.Dist(unit.Pos,destination)<=DropRange)
            {
                if(owner.DropItem(unit,item)&&item.Owner==null&&!item.Removed)item.Position=destination;
                return null;
            }
            _move.UpdateTarget(destination,32f,DropRange);
            return _move.Execute(unit,dt)==null?null:this;
        }

        public override string ToString()=> $"丢弃物品 {item.Name}";
    }

    // ★ 物编 `uses`(ItemData.slk X6 列)的真实取值只有三种: 数字(有限次) 与
    //   "-"(**无限次**)。原版三库实测 131 个物品: uses="-" 82 个 / 1 → 40 / 3 → 7 / 5 → 2,
    //   **不存在 "0"**。
    //
    // ★★ 缺列必须读成"无限次"(0)，**不是** 1。
    //   实证 ((1)神之墓地3.4.26.w3x 物编全表交叉):
    //     · uses 有值的 197 个物品里, **195 个同时写了 perishable** ⇒ 两列成对出现;
    //     · uses 缺失的 328 个里, 只有 13 个写了 perishable;
    //     · 两列都缺的 **315 个**(占全表 60%)在 WE 里没勾任何消耗选项,
    //       语义是"永久、无限次使用"。
    //   `GetString` 是精确匹配(Fields.TryGetValue), 缺列返回 ""。此前 `raw.Length==0
    //   → return 1` 把这 315 个永续物品读成"1 次", 配合 IsPerishable 缺列默认 true,
    //   ConsumeItemCharge 走 `Charges<=0 假 / Charges>1 假 → RemoveItem`
    //   ⇒ **点一下就消失**(用户实机 2026-10-06 第 33 轮: 自由之翼 I02T 等全部消失)。
    //   写错了默认值, 60% 的物品全中招, 而它们恰恰是"不该消耗"的那一类。
    static int ReadItemUses(ItemTypeData data)
    {
        string raw=data?.GetString("uses",0,0)??string.Empty;
        raw=raw.Trim();
        // 缺列 = 物编未设次数 = 无限次。理由同上: 真正的消耗品必同时写 perishable。
        if(raw.Length==0)return 0;
        // "-" 是原版表示"无限次"的占位符, 也接受 0 / -1 两种显式写法。
        if(raw=="-"||raw=="0"||raw=="-1")return 0;
        return int.TryParse(raw,System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,out int v)&&v>0?v:1;
    }

    void CompleteItemUse(ItemRef item,EventContext context,Action beforeConsume=null)
        =>ConsumeItemCharge(item,beforeConsume,context);

    void NotifyItemUsed(ItemRef item,EventContext context)
    {
        // 660470 captures the player before its message. The unit message gets
        // a fresh context after player scripts return. Script getters reject a
        // Removed item even though this managed payload still holds its identity.
        Unit carrier=item.Owner;
        if(carrier==null||carrier.Removed)return;
        context??=new EventContext {Event=GameEvent.ItemUsed,Source=carrier,Target=carrier,
            TriggerUnit=carrier,Point=carrier.Pos,ManipulatedItem=item};
        context.TriggerUnit=carrier;
        context.TriggerPlayerId=carrier.PlayerId;
        _world.Events.FireScoped(context,EventSubscriptionScope.PlayerUnit);
        var unitContext=context.Snapshot();
        unitContext.TriggerPlayerId=-1;
        _world.Events.FireScoped(unitContext,EventSubscriptionScope.Unit);
        _world.Events.FireScoped(unitContext.Snapshot(),EventSubscriptionScope.Any);
    }

    void ConsumeItemCharge(ItemRef item,Action beforeConsume=null,EventContext context=null)
    {
        beforeConsume?.Invoke();
        if(item==null||item.Removed)return;
        // Automatic powerups take 63a1f0's death path, independent of charges or perishable.
        if(item.Powerup){RemoveConsumedItem(item);return;}
        var state=item.NativeChargeState;
        state.CarrierFirst=state.CarrierSecond=item.Owner==null?-1:0;
        NativeItemCharges.Consume(state,()=>item.Owner is {Removed:false} carrier?carrier:null,
            // Special callers without a use payload keep their existing event
            // entry until their specific +388/cancellation path is reconstructed.
            _=>{if(context!=null)NotifyItemUsed(item,context);},(_,_)=>item.Life=0,
            ()=>{if(!item.Removed)RemoveConsumedItem(item);},()=>{});
    }

    void RemoveConsumedItem(ItemRef item)
    {
        _consumingItems.Add(item);
        try{RemoveItem(item);}
        finally{_consumingItems.Remove(item);}
    }

    bool BeginTownPortal(Unit hero,ItemRef item,Unit target,Vec2 point,string rawcode="AItp")
    {
        if(hero?.Alive!=true||!hero.IsHero||target?.Alive!=true
            ||target.PlayerId!=hero.PlayerId||!target.IsBuilding
            ||target.Abilities.Get<ConstructionProgress>()!=null
            ||hero.Abilities.Get<RuntimeTownPortalAbility>()!=null)return false;
        AbilityTypeData data=ItemAbilityDefinition(item,rawcode);
        if(data==null)return false;
        RuntimeTownPortalAbility ability=null;
        ability=new RuntimeTownPortalAbility(
            (unit,destination)=>FindClearStockUnitSpawn(unit,destination),
            successful=>
            {
                if(!successful||item.Removed||item.Owner!=hero)return;
                // Native source bind/effect/outcome/+388 raises item-use messages
                // at consumption after teleport succeeds. An interrupted channel
                // clears the pending consumption bit and emits no item-use event.
                BeginItemCooldown(item,ability);
                CompleteItemUse(item,new EventContext {Event=GameEvent.ItemUsed,Source=hero,Target=hero,
                    Point=target.Pos,OrderTargetUnit=target,ManipulatedItem=item});
            });
        ability.BindObjectData(rawcode,data,initialLevel:1,learnable:false);
        ability.RefreshTownPortalData();
        ability.Owner=hero;
        ability.SourceItem=item;
        hero.AddAbility(ability);
        if(!ability.CheckTarget(target,target.Pos))
        {
            hero.RemoveAbility(ability);
            return false;
        }
        hero.ForceIssue(new CastOrder { Ability=ability,Target=target,Point=target.Pos,
            EventTargetKind=EventTargetKind.Unit });
        return true;
    }

    public bool UseInventorySlotHome(Unit hero,int slot)
    {
        if(hero?.Alive!=true||hero.Removed
            ||!InventoryItemRequiresBuildingTarget(hero,slot))return false;
        Vec2 home=GetPlayerStartLocationPosition(hero.PlayerId)??hero.Pos;
        Unit townHall=_world.Units.Where(unit=>unit.Alive&&!unit.Removed
                &&unit.PlayerId==hero.PlayerId&&unit.IsBuilding
                &&unit.Identity.Is(Classification.TownHall)
                &&unit.Abilities.Get<ConstructionProgress>()==null)
            .OrderBy(unit=>Vec2.Dist(unit.Pos,home)).ThenBy(unit=>unit.SimulationId)
            .FirstOrDefault();
        // Use the selected item and the normal cooldown/ownership/use-event path.
        return townHall!=null&&UseInventorySlotTarget(hero,slot,townHall);
    }

    bool TryUseTownPortalHome(Unit hero,Vec2 home)
    {
        if(hero?.Alive!=true||hero.Abilities.Get<RuntimeTownPortalAbility>()!=null)
            return false;
        Unit townHall=_world.Units.Where(unit=>unit.Alive&&!unit.Removed
                &&unit.PlayerId==hero.PlayerId&&unit.IsBuilding
                &&unit.Identity.Is(Classification.TownHall)
                &&unit.Abilities.Get<ConstructionProgress>()==null)
            .OrderBy(unit=>Vec2.Dist(unit.Pos,home)).ThenBy(unit=>unit.SimulationId)
            .FirstOrDefault();
        if(townHall==null)return false;
        ItemRef scroll=Inventory(hero).FirstOrDefault(
            item=>item?.TypeId==FourCC("stwp"));
        return scroll!=null&&BeginTownPortal(hero,scroll,townHall,townHall.Pos);
    }
    void RemoveItem(ItemRef item)
    {
        if(item==null||item.Removed||!_removingItems.Add(item))return;
        try
        {
            // 1f2d00 -> CItem +84/+124 -> 63a1f0 -> 63be70 detaches
            // held items and notifies drop before destruction. Reentrant removal
            // during an existing drop must finish cleanup without notifying twice.
            item.Life=0f;
            if(item.Owner!=null&&!item.IsIllusionCopy&&!_detachingItems.Contains(item))
                DropItem(item.Owner,item,force:true);
            if(item.Owner!=null)
            {
                List<ItemRef> inventory=Inventory(item.Owner);
                int slot=inventory.IndexOf(item);
                if(slot>=0)inventory[slot]=null;
                RemoveItemEquipment(item.Owner,item);
                ReleaseEmptyRetiredInventory(item.Owner,inventory);
            }
            item.Owner=null; item.Removed=true;
            _items.Remove(item);
            _vm.DestroyHandleObject(item,recycleId:false);
        }
        finally{_removingItems.Remove(item);}
    }
    bool MoveItemSlot(Unit unit,ItemRef item,int slot)
    {
        if(unit?.Alive!=true||!unit.HasUsableInventory||item is not {Removed:false}||item.Owner!=unit)
            return false;
        List<ItemRef> inventory=Inventory(unit);
        int current=inventory.IndexOf(item);
        if(current<0||slot<0||slot>=InventoryCapacity(unit)||slot>=inventory.Count)return false;
        // 54e870 permits moving to an empty slot without DataE. An occupied
        // destination must pass 528660's drop admission before it can be swapped.
        if(inventory[slot] is ItemRef displaced&&!CanDropItem(unit,displaced))return false;
        (inventory[current],inventory[slot])=(inventory[slot],inventory[current]);
        return true;
    }
    static Vec2 WidgetPosition(JassVm vm,JassValue value)
    {
        if(vm.TryGetHandleObject(value,out Unit unit))return unit.Pos;
        if(vm.TryGetHandleObject(value,out ItemRef item))return item.Position;
        if(vm.TryGetHandleObject(value,out DestructableRef destructable))return destructable.Position;
        return default;
    }
    static float WidgetLife(JassVm vm,JassValue value)
    {
        if(vm.TryGetHandleObject(value,out Unit unit))return unit.Life;
        if(vm.TryGetHandleObject(value,out ItemRef item))return item.Life;
        if(vm.TryGetHandleObject(value,out DestructableRef destructable))return destructable.Life;
        return 0;
    }
    void SetWidgetLife(JassVm vm,JassValue value,float life)
    {
        if(vm.TryGetHandleObject(value,out Unit unit))
        {
            unit.Life=Math.Clamp(life,0,unit.MaxLife);
            if(unit.Life<=0&&unit.Alive&&unit.World!=null)
                unit.Death.ResolveLifeDepleted(null);
        }
        else if(vm.TryGetHandleObject(value,out ItemRef item))
        {
            SetItemLife(item,life);
        }
        else if(vm.TryGetHandleObject(value,out DestructableRef destructable))
        {
            if(life<=0)KillDestructable(vm,destructable);
            else ReviveDestructable(destructable,life,"stand");
        }
    }

    void SetItemLife(ItemRef item,float life)
    {
        if(item==null||item.Removed)return;
        float clamped=Math.Clamp(life,0,item.MaxLife);
        // Original 63d710: transition from life > .4 into the death boundary,
        // including the .001 FloatMini tolerance. Direct setters ignore invulnerability.
        if(item.Life>.4f&&(clamped<=.4f||MathF.Abs(clamped-.4f)<.001f))KillItem(_vm,item);
        else item.Life=clamped;
    }

    void RegisterOrderNatives()
    {
        _vm.RegisterNative("IssueNeutralImmediateOrder",(v,a)=>{
            int orderId=ResolveGroupOrderId(Text(a[2]));
            return JassVm.BooleanValue(orderId!=0&&PurchaseStock(Obj<PlayerRef>(v,a[0]),
                Obj<Unit>(v,a[1]),orderId,out _));
        });
        _vm.RegisterNative("IssueNeutralImmediateOrderById",(v,a)=>JassVm.BooleanValue(
            PurchaseStock(Obj<PlayerRef>(v,a[0]),Obj<Unit>(v,a[1]),
                checked((int)Int(a[2])),out _)));
        _vm.RegisterNative("IssueImmediateOrder",(v,a)=>JassVm.BooleanValue(
            ObjOrNull<Unit>(v,a[0]) is { } unit&&IssueOrder(unit,OrderId(Text(a[1])),Text(a[1]),null,default)));
        _vm.RegisterNative("IssueImmediateOrderById",(v,a)=>JassVm.BooleanValue(
            ObjOrNull<Unit>(v,a[0]) is { } unit&&IssueOrder(unit,checked((int)Int(a[1])),"",null,default)));
        // Game.dll 6f1e9450 -> 6f1e9470 returns false for a null unit.
        // Maps can reach this from delayed callbacks with no enumeration unit.
        _vm.RegisterNative("IssuePointOrder",(v,a)=>JassVm.BooleanValue(IssueOrder(ObjOrNull<Unit>(v,a[0]),OrderId(Text(a[1])),Text(a[1]),null,new Vec2((float)Real(a[2]),(float)Real(a[3])))));
        _vm.RegisterNative("IssuePointOrderById",(v,a)=>JassVm.BooleanValue(IssueOrder(ObjOrNull<Unit>(v,a[0]),checked((int)Int(a[1])),"",null,new Vec2((float)Real(a[2]),(float)Real(a[3])))));
        _vm.RegisterNative("IssuePointOrderLoc",(v,a)=>JassVm.BooleanValue(
            ObjOrNull<Unit>(v,a[0]) is { } unit&&ObjOrNull<LocationRef>(v,a[2]) is { } location
                &&IssueOrder(unit,OrderId(Text(a[1])),Text(a[1]),null,location.Value)));
        _vm.RegisterNative("IssuePointOrderByIdLoc",(v,a)=>{
            Unit unit=ObjOrNull<Unit>(v,a[0]);
            LocationRef location=ObjOrNull<LocationRef>(v,a[2]);
            return JassVm.BooleanValue(unit!=null&&location!=null
                &&IssueOrder(unit,checked((int)Int(a[1])),"",null,location.Value));
        });
        _vm.RegisterNative("IssueTargetOrder",(v,a)=>JassVm.BooleanValue(IssueTargetOrder(v,ObjOrNull<Unit>(v,a[0]),OrderId(Text(a[1])),Text(a[1]),a[2])));
        _vm.RegisterNative("IssueTargetOrderById",(v,a)=>JassVm.BooleanValue(IssueTargetOrder(v,ObjOrNull<Unit>(v,a[0]),checked((int)Int(a[1])),"",a[2])));
        // 字符串建造单: 订单名走注册表, 否则按 rawcode 解析(FourCC 建筑单)。
        _vm.RegisterNative("IssueBuildOrder",(v,a)=>
        {
            string orderName=Text(a[1]);
            int orderId=ResolveGroupOrderId(orderName);
            return JassVm.BooleanValue(orderId!=0&&IssueOrder(Obj<Unit>(v,a[0]),
                orderId,orderName,null,
                new Vec2((float)Real(a[2]),(float)Real(a[3]))));
        });
        _vm.RegisterNative("IssueInstantPointOrder",(v,a)=>JassVm.BooleanValue(
            IssueOrder(Obj<Unit>(v,a[0]),OrderId(Text(a[1])),Text(a[1]),null,
                new Vec2((float)Real(a[2]),(float)Real(a[3])))));
        _vm.RegisterNative("IssueInstantPointOrderById",(v,a)=>JassVm.BooleanValue(
            IssueOrder(Obj<Unit>(v,a[0]),checked((int)Int(a[1])),"",null,
                new Vec2((float)Real(a[2]),(float)Real(a[3])))));
        _vm.RegisterNative("GetUnitCurrentOrder",(v,a)=>JassVm.IntegerValue(
            ObjOrNull<Unit>(v,a[0]) is { } unit?CurrentOrderId(unit):0));
        _vm.RegisterNative("GetRescuer",(v,a)=>UnitHandle(v,Context?.Source));
        _vm.RegisterNative("UnitAddIndicator",(v,a)=>
        {
            Unit unit=ObjOrNull<Unit>(v,a[0]);
            if(unit!=null)
            {
                uint red=(uint)Math.Clamp(Int(a[1]),0,255);
                uint green=(uint)Math.Clamp(Int(a[2]),0,255);
                uint blue=(uint)Math.Clamp(Int(a[3]),0,255);
                uint alpha=(uint)Math.Clamp(Int(a[4]),0,255);
                unit.IndicatorColorArgb=(alpha<<24)|(red<<16)|(green<<8)|blue;
                unit.IndicatorRemaining=2f;
            }
            return N();
        });
    }

    void RegisterItemNatives()
    {
        _vm.RegisterNative("AddItemToStock",(v,a)=>{var u=ObjOrNull<Unit>(v,a[0]);int typeId=checked((int)Int(a[1]));
            float cooldown=a.Count>4?(float)Real(a[4]):-1f;
            if(u!=null){_removedItemTypesFromAllStock.Remove(typeId);SetStock(_itemStocks,u,
            typeId,checked((int)Int(a[2])),checked((int)Int(a[3])),cooldown);}return N();});
        _vm.RegisterNative("AddUnitToStock",(v,a)=>{var u=ObjOrNull<Unit>(v,a[0]);
            float cooldown=a.Count>4?(float)Real(a[4]):-1f;
            if(u!=null)SetStock(_unitStocks,u,
            checked((int)Int(a[1])),checked((int)Int(a[2])),checked((int)Int(a[3])),cooldown);
            return N();});
        _vm.RegisterNative("RemoveItemFromStock",(v,a)=>{var u=ObjOrNull<Unit>(v,a[0]);if(u!=null)RemoveStock(_itemStocks,u,
            checked((int)Int(a[1])));return N();});
        _vm.RegisterNative("RemoveUnitFromStock",(v,a)=>{var u=ObjOrNull<Unit>(v,a[0]);if(u!=null)RemoveStock(_unitStocks,u,
            checked((int)Int(a[1])));return N();});
        _vm.RegisterNative("GetItemTypeId",(v,a)=>JassVm.IntegerValue(ObjOrNull<ItemRef>(v,a[0])?.TypeId??0));
        _vm.RegisterNative("GetItemX",(v,a)=>new JassReal(ObjOrNull<ItemRef>(v,a[0])?.Position.X??0));
        _vm.RegisterNative("GetItemY",(v,a)=>new JassReal(ObjOrNull<ItemRef>(v,a[0])?.Position.Y??0));
        // SetItemPosition 会把物品在"单位库存"与"地面"之间搬家, 因此**必须先从源容器
        // 释放**。原实现只改坐标, 物品仍留在 _inventories 的槽位里, 于是:
        //   * 地面枚举(GetGroundItems 过滤 Owner==null)与库存枚举同时能看到它 -> 复制;
        //   * 槽位被占住, 后续 UnitAddItem 找不到空位;
        //   * 装备的能力/加成仍然挂在单位身上(未走 RemoveEquippedItemAbilities)。
        // 对标原版: SetItemPosition 等价于"先 DropItem 再设坐标"。
        _vm.RegisterNative("SetItemPosition",(v,a)=>{var i=ObjOrNull<ItemRef>(v,a[0]);
            if(i==null)return N();
            // force=true: 原版 SetItemPosition 是脚本命令, 不受 Droppable/不可丢弃
            // 物品的 UI 限制约束(与 SetItemDroppable 显式设置后的语义一致)。
            if(i.Owner!=null)DropItem(i.Owner,i,force:true);
            i.Position=new Vec2((float)Real(a[1]),(float)Real(a[2]));
            return N();});
        _vm.RegisterNative("GetItemPlayer",(v,a)=>{var i=ObjOrNull<ItemRef>(v,a[0]);return i==null?JassVm.NullValue:Handle(v,"player",Player(i.PlayerId));});
        _vm.RegisterNative("SetItemPlayer",(v,a)=>{var i=ObjOrNull<ItemRef>(v,a[0]);var p=ObjOrNull<PlayerRef>(v,a[1]);if(i!=null&&p!=null)i.PlayerId=p.Id;return N();});
        _vm.RegisterNative("GetItemCharges",(v,a)=>JassVm.IntegerValue(ObjOrNull<ItemRef>(v,a[0])?.Charges??0));
        _vm.RegisterNative("SetItemCharges",(v,a)=>{var i=ObjOrNull<ItemRef>(v,a[0]);if(i!=null)i.Charges=Math.Max(0,checked((int)Int(a[1])));return N();});
        _vm.RegisterNative("GetItemUserData",(v,a)=>JassVm.IntegerValue(ObjOrNull<ItemRef>(v,a[0])?.UserData??0));
        _vm.RegisterNative("SetItemUserData",(v,a)=>{var i=ObjOrNull<ItemRef>(v,a[0]);if(i!=null)i.UserData=checked((int)Int(a[1]));return N();});
        _vm.RegisterNative("GetItemName",(v,a)=>new JassStr(ObjOrNull<ItemRef>(v,a[0])?.Name??""));
        _vm.RegisterNative("GetItemLevel",(v,a)=>JassVm.IntegerValue(ObjOrNull<ItemRef>(v,a[0])?.Level??0));
        _vm.RegisterNative("GetItemType",(v,a)=>{var i=ObjOrNull<ItemRef>(v,a[0]);return i==null?JassVm.NullValue:Enum(v,"itemtype",i.Type);});
        _vm.RegisterNative("IsItemOwned",(v,a)=>JassVm.BooleanValue(ObjOrNull<ItemRef>(v,a[0])?.Owner!=null));
        _vm.RegisterNative("IsItemVisible",(v,a)=>JassVm.BooleanValue(ObjOrNull<ItemRef>(v,a[0])?.Visible==true));
        _vm.RegisterNative("SetItemVisible",(v,a)=>{var i=ObjOrNull<ItemRef>(v,a[0]);if(i!=null)i.Visible=Bool(a[1]);return N();});
        _vm.RegisterNative("IsItemInvulnerable",(v,a)=>JassVm.BooleanValue(ObjOrNull<ItemRef>(v,a[0])?.Invulnerable==true));
        _vm.RegisterNative("SetItemInvulnerable",(v,a)=>{var i=ObjOrNull<ItemRef>(v,a[0]);if(i!=null)i.Invulnerable=Bool(a[1]);return N();});
        _vm.RegisterNative("IsItemPawnable",(v,a)=>JassVm.BooleanValue(ObjOrNull<ItemRef>(v,a[0])?.Pawnable==true));
        _vm.RegisterNative("SetItemPawnable",(v,a)=>{var i=ObjOrNull<ItemRef>(v,a[0]);if(i!=null)i.Pawnable=Bool(a[1]);return N();});
        _vm.RegisterNative("SetItemDroppable",(v,a)=>{var i=ObjOrNull<ItemRef>(v,a[0]);if(i!=null)i.Droppable=Bool(a[1]);return N();});
        _vm.RegisterNative("SetItemDropOnDeath",(v,a)=>{var i=ObjOrNull<ItemRef>(v,a[0]);if(i!=null)i.DropOnDeath=Bool(a[1]);return N();});
        _vm.RegisterNative("IsItemSellable",(v,a)=>JassVm.BooleanValue(ObjOrNull<ItemRef>(v,a[0])?.Sellable==true));
        _vm.RegisterNative("IsItemPowerup",(v,a)=>JassVm.BooleanValue(ObjOrNull<ItemRef>(v,a[0])?.Powerup==true));
        _vm.RegisterNative("IsItemIdSellable",(v,a)=>JassVm.BooleanValue(
            _objects?.GetItem(Rawcode(a[0]))?.GetInt("sellable",0,0,1)!=0));
        _vm.RegisterNative("IsItemIdPawnable",(v,a)=>JassVm.BooleanValue(
            _objects?.GetItem(Rawcode(a[0]))?.GetInt("pawnable",0,0,1)!=0));
        // 与 CreateItem 保持同一判定口径(审计 F16): 优先读物编 powerup 字段,
        // 字段缺失才回退 class。原先两处一个用 Type==2、一个用 class 字符串,
        // 地图把物品改成 PowerUp 分类但 powerup=0 时会出现"原型是 powerup、
        // 实例不是"的矛盾。
        _vm.RegisterNative("IsItemIdPowerup",(v,a)=>JassVm.BooleanValue(
            IsItemIdPowerup(Rawcode(a[0]))));
        _vm.RegisterNative("ChooseRandomItemEx",(v,a)=>{
            int type=EnumId(a[0]),level=checked((int)Int(a[1]));
            return JassVm.IntegerValue(ChooseRandomItemType(type,level));
        });
        // These are script functions, not Game.dll natives. Let Blizzard.j or
        // map-defined replacements own their arrays, weights and return values.
        _vm.RegisterScriptFallback("RandomDistReset",(_,__)=>{RandomDistReset();return N();});
        _vm.RegisterScriptFallback("RandomDistAddItem",(_,a)=>{
            RandomDistAddItem(checked((int)Int(a[0])),checked((int)Int(a[1])));
            return N();
        });
        _vm.RegisterScriptFallback("RandomDistChoose",(_,__)=>JassVm.IntegerValue(RandomDistChoose()));
        _vm.RegisterScriptFallback("RandomDistAddItemEx",(_,a)=>{
            RandomDistAddItem(checked((int)Int(a[0])),checked((int)Int(a[1])));
            return N();
        });
        _vm.RegisterNative("UnitAddItem",(v,a)=>JassVm.BooleanValue(ScriptAddItem(
            ObjOrNull<Unit>(v,a[0]),ObjOrNull<ItemRef>(v,a[1]))));
        _vm.RegisterNative("UnitAddItemById",(v,a)=>
        {
            Unit unit=Obj<Unit>(v,a[0]);
            ItemRef item=CreateItem(checked((int)Int(a[1])),unit.Pos);
            if(item==null)return JassVm.NullValue;
            // 背包满 / 无法放入时务必清理孤儿物品, 否则它会残留在创建点(商店脚下)
            // 无限堆积并"永久消失"。原版 AddItem 失败即视为购买失败, 物品不留下。
            if(!AddItem(unit,item)){ RemoveItem(item); return JassVm.NullValue; }
            return ItemHandle(v,item);
        });
        _vm.RegisterNative("UnitAddItemToSlotById",(v,a)=>
        {
            Unit unit=Obj<Unit>(v,a[0]);
            ItemRef item=CreateItem(checked((int)Int(a[1])),unit.Pos);
            if(item==null)return JassVm.BooleanValue(false);
            // A newly created slot grant has no ground-item fallback. In editor
            // maps the slot may already be populated by war3mapUnits.doo.
            bool added=AddItem(unit,item,checked((int)Int(a[2])));
            if(!added)RemoveItem(item);
            return JassVm.BooleanValue(added);
        });
        _vm.RegisterNative("UnitRemoveItem",(v,a)=>
        {
            Unit unit=ObjOrNull<Unit>(v,a[0]);
            ItemRef item=ObjOrNull<ItemRef>(v,a[1]);
            // Droppable restricts player orders, not authoritative quest scripts.
            if(unit!=null&&item?.Owner==unit)DropItem(unit,item,force:true);
            return N();
        });
        _vm.RegisterNative("UnitRemoveItemFromSlot",(v,a)=>{var u=ObjOrNull<Unit>(v,a[0]);
            if(u==null)return JassVm.NullValue;var list=Inventory(u);int slot=checked((int)Int(a[1]));if(slot<0||slot>=list.Count||list[slot]==null)return JassVm.NullValue;var i=list[slot];return DropItem(u,i,force:true)?Handle(v,"item",i):JassVm.NullValue;});
        // 注意 Contains(null): Inventory 的空槽位本身就是 null, 传 null item 会在
        // "有空槽"时错误返回 true。原版空物品返回 false, 故必须先判 item!=null。
        _vm.RegisterNative("UnitHasItem",(v,a)=>{
            Unit u=ObjOrNull<Unit>(v,a[0]);
            ItemRef it=ObjOrNull<ItemRef>(v,a[1]);
            return JassVm.BooleanValue(u!=null&&it!=null&&Inventory(u).Contains(it));});
        _vm.RegisterNative("UnitItemInSlot",(v,a)=>{var unit=ObjOrNull<Unit>(v,a[0]);if(unit==null)return JassVm.NullValue;var list=Inventory(unit);int slot=checked((int)Int(a[1]));return slot>=0&&slot<list.Count&&list[slot]!=null?Handle(v,"item",list[slot]):JassVm.NullValue;});
        _vm.RegisterNative("UnitInventorySize",(v,a)=>
        {
            // Game.dll 6f1faee0 returns zero for a null unit. DotA checks this
            // when its recipient search fails before scheduling ground delivery.
            Unit unit=ObjOrNull<Unit>(v,a[0]);
            if(unit==null)return JassVm.IntegerValue(0);
            int capacity=InventoryCapacity(unit);
            var items=Inventory(unit);
            if(_stockPurchaseAttempt?.PlayerId==unit.PlayerId&&capacity>0
                &&items.Take(capacity).All(item=>item!=null))_stockPurchaseAttempt.InventoryFull=true;
            return JassVm.IntegerValue(capacity);
        });
        _vm.RegisterNative("UnitDropItemPoint",(v,a)=>{
            var u=ObjOrNull<Unit>(v,a[0]);var i=ObjOrNull<ItemRef>(v,a[1]);
            if(!DropItem(u,i))return JassVm.BooleanValue(false);
            if(i.Owner==null&&!i.Removed)i.Position=new Vec2((float)Real(a[2]),(float)Real(a[3]));return JassVm.BooleanValue(true);});
        // ---- UnitDropItem: compatibility for hosts without Blizzard.j ----
        // World Editor drop triggers pass an item rawcode. The real helper is
        // defined by Blizzard.j; standalone tests historically also accepted
        // inventory handles here. Retain that adapter only for scriptless use.
        // UnitDropItem is a Blizzard.j function, not a common.j native.
        // Its script also sets DropID and unlocks marketplace stock. Keep the
        // legacy scriptless adapter only until that real function is loaded.
        _vm.RegisterScriptFallback("UnitDropItem",(v,a)=>{
            Unit unit=ObjOrNull<Unit>(v,a[0]);
            if(unit==null)return JassVm.BooleanValue(false);
            ItemRef owned=ObjOrNull<ItemRef>(v,a[1]);
            if(owned!=null)
            {
                // 句柄路径 = "把背包里这件物品丢到地上", 原版要求单位存活
                // (CanDropItem 内部同样校验 Alive==true)。
                if(owned.Owner==unit)return JassVm.BooleanValue(
                    unit.Alive&&DropItem(unit,owned));
                // 别人的物品: 原版不允许凭空移动他人物品, 但允许"按 id 生成同款"。
                if(owned.Owner!=null)return JassVm.BooleanValue(false);
            }
            // ★ rawcode 路径**不得**校验 Alive。
            //   World Editor 生成的死亡掉落触发器正是靠这个:
            //       call UnitDropItem( GetTriggerUnit(), itemID )   -- 单位此刻已死
            //   若加 Alive 守卫, unit.Alive 已是 false ⇒ 恒返回 false ⇒
            //   死亡掉落永远不发生(地图「永恒的荣耀自由镜头版」全图不掉落)。
            //   原版语义是"在单位所在位置生成该物品", 与死活无关, 只要位置有效。
            int rawcode=owned!=null?owned.TypeId:ResolvePlacedRandomItem(Rawcode(a[1]));
            if(rawcode==0)return JassVm.BooleanValue(false);
            ItemRef created=CreateItem(rawcode,ScatterDropPosition(unit.Pos));
            if(created==null)return JassVm.BooleanValue(false);
            created.PlayerId=unit.PlayerId;
            return JassVm.BooleanValue(true);
        });
        // UnitDropItem* 三兄弟与 UnitUseItem 同属"脚本会传 null 物品"的高风险族
        // (bj_lastCreatedItem / UnitItemInSlot 空槽都可能是 null), 一并改 ObjOrNull:
        //   Point  —— DropItem 内部 `item?.Owner!=unit` 已短路, null 时返回 false;
        //   Target —— 原写法直接 `i.Owner` 解引用, null 会 NRE;
        //   Slot   —— MoveItemSlot 未做空物品防护。
        // 原版对空句柄一律静默返回 false, 不抛(同 0x6f1fb430 的两路 null 检查)。
        _vm.RegisterNative("UnitDropItemSlot",(v,a)=>JassVm.BooleanValue(
            MoveItemSlot(ObjOrNull<Unit>(v,a[0]),ObjOrNull<ItemRef>(v,a[1]),checked((int)Int(a[2])))));
        _vm.RegisterNative("UnitDropItemTarget",(v,a)=>{
            var u=ObjOrNull<Unit>(v,a[0]);var i=ObjOrNull<ItemRef>(v,a[1]);
            if(!CanDropItem(u,i))return JassVm.BooleanValue(false);
            if(v.TryGetHandleObject(a[2],out Unit target))return JassVm.BooleanValue(
                GiveItemToUnit(u,i,target));
            if(!DropItem(u,i))return JassVm.BooleanValue(false);
            if(i.Owner==null&&!i.Removed)i.Position=WidgetPosition(v,a[2]);return JassVm.BooleanValue(true);});
        // ★ 原版 UnitUseItem (0x6f1fb430) 对 unit 与 item **两个句柄都做 null 检查**,
        // 任一为空则 return 0(=false), 不崩溃。见
        // [1]Reference/War3_decompile/jass_vm_native_decompile.c:10747-10777 与
        // natives.json:2858-2863 (sig "(Hunit;Hitem;)B"); Point/Target 同构(10782/10816)。
        // Dota.w3m 的 main() 阶段会走到 UnitUseItem(it, bj_lastCreatedItem), 而
        // bj_lastCreatedItem 是 Blizzard.j 全局 —— 脚本里大量 `set bj_lastCreatedItem=null`
        // 且 UnitItemInSlot 空槽返回 null, 所以它**常态就是 null**。我方此前用 Obj<T>
        // 抛 "Expected handle containing ItemRef", 直接让整张地图脚本启动失败
        // (stage=main, 所有触发器全废)。改为 ObjOrNull; UseItem 内部已有
        // `unit==null||item?.Owner!=unit||item.Removed` 的短路, 传 null 安全返回 false。
        _vm.RegisterNative("UnitUseItem",(v,a)=>JassVm.BooleanValue(
            UseItem(ObjOrNull<Unit>(v,a[0]),ObjOrNull<ItemRef>(v,a[1]))));
        _vm.RegisterNative("UnitUseItemPoint",(v,a)=>JassVm.BooleanValue(
            UseItem(ObjOrNull<Unit>(v,a[0]),ObjOrNull<ItemRef>(v,a[1]),
                new Vec2((float)Real(a[2]),(float)Real(a[3])))));
        _vm.RegisterNative("UnitUseItemTarget",(v,a)=>JassVm.BooleanValue(
            UseItem(ObjOrNull<Unit>(v,a[0]),ObjOrNull<ItemRef>(v,a[1]),
                targetValue:a[2],vm:v)));
    }

}

*/
