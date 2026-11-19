//重构情况：从 Scripts/common.ai SetBuildAll 与四族 build_sequence 的调用形态转换
namespace OpenWC3.JassToSharp.Ai;

public enum MeleeAiStepKind
{
    Unit,
    Expand,
    Upgrade,
    Hero,
}

public readonly record struct MeleeAiStep(MeleeAiStepKind Kind, int Quantity, string Id, int Town)
{
    public static MeleeAiStep Unit(int quantity, string id) =>
        new(MeleeAiStepKind.Unit, quantity, id, -1);

    public static MeleeAiStep Expand(int town, string id) =>
        new(MeleeAiStepKind.Expand, 1, id, town);

    public static MeleeAiStep Upgrade(int quantity, string id) =>
        new(MeleeAiStepKind.Upgrade, quantity, id, -1);

    public static MeleeAiStep Hero(int quantity) =>
        new(MeleeAiStepKind.Hero, quantity, string.Empty, -1);
}

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

function SecondaryTown takes integer town, integer qty, integer unitid returns nothing
    call SetBuildAll(BUILD_UNIT,qty,unitid,town)
endfunction
*/
