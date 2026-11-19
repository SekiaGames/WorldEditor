//重构情况：从 Scripts/common.ai 的 TownHasMine/TownHasHall/TownCount/TownCountDone/TownCountEx 转换（待转换占位）
namespace OpenWC3.JassToSharp.Ai.Common;

public static class Town
{
}

/*
原文（Scripts/common.ai，城镇查询 native 与计数）：

native TownHasMine          takes integer townid                        returns boolean

native TownHasHall          takes integer townid                        returns boolean

function TownCount takes integer base returns integer
    return TownCountEx(base,false,-1)
endfunction

function TownCountDone takes integer base returns integer
    return TownCountEx(base,true,-1)
endfunction

function TownCountEx takes integer unitid, boolean only_done, integer townid returns integer

    local integer have_qty = GetUnitCountEx(unitid,only_done,townid)

    if unitid == TOWN_HALL then
        set have_qty = have_qty + GetUnitCountEx(KEEP,false,townid) + GetUnitCountEx(CASTLE,false,townid)
    elseif unitid == KEEP then
        set have_qty = have_qty  + GetUnitCountEx(CASTLE,false,townid)

    elseif unitid == WATCH_TOWER then
        set have_qty = have_qty + GetUnitCountEx(GUARD_TOWER,false,townid) + GetUnitCountEx(CANNON_TOWER,false,townid) + GetUnitCountEx(ARCANE_TOWER,false,townid)

    elseif unitid == PEASANT then
        set have_qty = have_qty + GetUnitCountEx(MILITIA,false,townid)

    elseif unitid == GREAT_HALL then
        set have_qty = have_qty + GetUnitCountEx(STRONGHOLD,false,townid) + GetUnitCountEx(FORTRESS,false,townid)
    elseif unitid == STRONGHOLD then
        set have_qty = have_qty + GetUnitCountEx(FORTRESS,false,townid)

    elseif unitid == HEAD_HUNTER then
        set have_qty = have_qty + GetUnitCountEx(BERSERKER,false,townid)

    elseif unitid == SPIRIT_WALKER then
        set have_qty = have_qty + GetUnitCountEx(SPIRIT_WALKER_M,false,townid)
    elseif unitid == SPIRIT_WALKER_M then
        set have_qty = have_qty + GetUnitCountEx(SPIRIT_WALKER,only_done,townid)

    elseif unitid == NECROPOLIS_1 then
        set have_qty = have_qty + GetUnitCountEx(NECROPOLIS_2,false,townid) + GetUnitCountEx(NECROPOLIS_3,false,townid)
    elseif unitid == NECROPOLIS_2 then
        set have_qty = have_qty + GetUnitCountEx(NECROPOLIS_3,false,townid)

    elseif unitid == ZIGGURAT_1 then
        set have_qty = have_qty + GetUnitCountEx(ZIGGURAT_2,false,townid) + GetUnitCountEx(ZIGGURAT_FROST,false,townid)

    elseif unitid == GARGOYLE then
        set have_qty = have_qty + GetUnitCountEx(GARGOYLE_MORPH,false,townid)

    elseif unitid == TREE_LIFE then
        set have_qty = have_qty + GetUnitCountEx(TREE_AGES,false,townid) + GetUnitCountEx(TREE_ETERNITY,false,townid)
    elseif unitid == TREE_AGES then
        set have_qty = have_qty + GetUnitCountEx(TREE_ETERNITY,false,townid)

    elseif unitid == DRUID_TALON then
        set have_qty = have_qty + GetUnitCountEx(DRUID_TALON_M,false,townid)
    elseif unitid == DRUID_TALON_M then
        set have_qty = have_qty + GetUnitCountEx(DRUID_TALON,only_done,townid)

    elseif unitid == DRUID_CLAW then
        set have_qty = have_qty + GetUnitCountEx(DRUID_CLAW_M,false,townid)
    elseif unitid == DRUID_CLAW_M then
        set have_qty = have_qty + GetUnitCountEx(DRUID_CLAW,only_done,townid)

    elseif unitid == ILLIDAN then
        set have_qty = have_qty + GetUnitCountEx(ILLIDAN_DEMON,false,townid)

    endif
    return have_qty
endfunction
*/
