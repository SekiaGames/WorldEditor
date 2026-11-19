//重构情况：从 Scripts/common.ai SetBuildAll 的 build_type/build_qty/build_item/build_town 四数组转换
namespace OpenWC3.JassToSharp.Ai;

public enum MeleeAiBuildKind
{
    Unit,
    Upgrade,
    Expand,
}

public readonly record struct MeleeAiBuildEntry(
    MeleeAiBuildKind Kind, int Quantity, string Id, int Town)
{
    public static MeleeAiBuildEntry Unit(int quantity, string id) =>
        new(MeleeAiBuildKind.Unit, quantity, id, -1);

    public static MeleeAiBuildEntry Upgrade(int quantity, string id) =>
        new(MeleeAiBuildKind.Upgrade, quantity, id, -1);

    public static MeleeAiBuildEntry Expand(int quantity, string id) =>
        new(MeleeAiBuildKind.Expand, quantity, id, -1);
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
