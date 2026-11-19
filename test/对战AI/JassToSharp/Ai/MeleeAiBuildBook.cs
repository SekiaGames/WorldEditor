//重构情况：从 Scripts/common.ai InitBuildArray/SetBuildUnit/SetBuildNext/SetBuildUpgr/SetBuildUnitEx/SetBuildUpgrEx 转换
namespace OpenWC3.JassToSharp.Ai;

public sealed class MeleeAiBuildBook
{
    readonly IMeleeAiWorld _world;
    readonly int _player;
    readonly List<MeleeAiBuildEntry> _entries = new();

    public MeleeAiBuildBook(IMeleeAiWorld world, int player)
    {
        _world = world;
        _player = player;
    }

    public List<MeleeAiBuildEntry> Entries => _entries;

    public int Count => _entries.Count;

    public void InitBuildArray() => _entries.Clear();

    void SetBuildAll(MeleeAiBuildKind kind, int quantity, string id, int town)
    {
        if (quantity <= 0) return;
        _entries.Add(new MeleeAiBuildEntry(kind, quantity, id, town));
    }

    public void SetBuildUnit(int quantity, string id) =>
        SetBuildAll(MeleeAiBuildKind.Unit, quantity, id, -1);

    public void SetBuildNext(int quantity, string id)
    {
        if (_world.UnitCount(_player, id) >= quantity) return;
        SetBuildAll(MeleeAiBuildKind.Unit, _world.UnitCountDone(_player, id) + 1, id, -1);
    }

    public void SecondaryTown(int town, int quantity, string id) =>
        SetBuildAll(MeleeAiBuildKind.Unit, quantity, id, town);

    public void SetBuildUpgr(int quantity, string id, bool newbie)
    {
        if (newbie && quantity != 1) return;
        SetBuildAll(MeleeAiBuildKind.Upgrade, quantity, id, -1);
    }

    public void SetBuildUnitEx(int easy, int normal, int hard, string id, int difficulty) =>
        SetBuildAll(MeleeAiBuildKind.Unit, difficulty switch
        {
            <= 0 => easy,
            1 => normal,
            _ => hard,
        }, id, -1);

    public void SetBuildUpgrEx(int easy, int normal, int hard, string id, int difficulty, bool newbie)
    {
        int quantity = difficulty switch
        {
            <= 0 => easy,
            1 => normal,
            _ => hard,
        };
        SetBuildUpgr(quantity, id, newbie);
    }
}

/*
原文（Scripts/common.ai，3 行）：

function InitBuildArray takes nothing returns nothing
    set build_length = 0
endfunction
*/

/*
原文（Scripts/common.ai，9 行）：

function SetBuildAll takes integer t, integer qty, integer unitid, integer town returns nothing
    if qty > 0 then
        set build_qty[build_length] = qty
        set build_type[build_length] = t
        set build_item[build_length] = unitid
        set build_town[build_length] = town
        set build_length = build_length + 1
    endif
endfunction
*/

/*
原文（Scripts/common.ai，3 行）：

function SetBuildUnit takes integer qty, integer unitid returns nothing
    call SetBuildAll(BUILD_UNIT,qty,unitid,-1)
endfunction
*/

/*
原文（Scripts/common.ai，7 行）：

function SetBuildNext takes integer qty, integer unitid returns nothing
    local integer has = GetUnitCount(unitid)
    if has >= qty then
        return
    endif
    call SetBuildAll(BUILD_UNIT,GetUnitCountDone(unitid)+1,unitid,-1)
endfunction
*/

/*
原文（Scripts/common.ai，9 行）：

function SetBuildUnitEx takes integer easy, integer med, integer hard, integer unitid returns nothing
    if difficulty == EASY then
        call SetBuildAll(BUILD_UNIT,easy,unitid,-1)
    elseif difficulty == NORMAL then
        call SetBuildAll(BUILD_UNIT,med,unitid,-1)
    else
        call SetBuildAll(BUILD_UNIT,hard,unitid,-1)
    endif
endfunction
*/

/*
原文（Scripts/common.ai，5 行）：

function SetBuildUpgr takes integer qty, integer unitid returns nothing
    if MeleeDifficulty() != MELEE_NEWBIE or qty == 1 then
        call SetBuildAll(BUILD_UPGRADE,qty,unitid,-1)
    endif
endfunction
*/

/*
原文（Scripts/common.ai，9 行）：

function SetBuildUpgrEx takes integer easy, integer med, integer hard, integer unitid returns nothing
    if difficulty == EASY then
        call SetBuildAll(BUILD_UPGRADE,easy,unitid,-1)
    elseif difficulty == NORMAL then
        call SetBuildAll(BUILD_UPGRADE,med,unitid,-1)
    else
        call SetBuildAll(BUILD_UPGRADE,hard,unitid,-1)
    endif
endfunction
*/

/*
原文（Scripts/common.ai，3 行）：

function SecondaryTown takes integer town, integer qty, integer unitid returns nothing
    call SetBuildAll(BUILD_UNIT,qty,unitid,town)
endfunction
*/
