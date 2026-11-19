//重构情况：从 Scripts/common.ai 的 GuardSecondary 转换（待转换占位）
namespace OpenWC3.JassToSharp.Ai.Common;

public static class GuardSecondary
{
}

/*
原文（Scripts/common.ai，守卫点）：

function GuardSecondary takes integer townid, integer qty, integer unitid returns nothing
    if TownHasHall(townid) and TownHasMine(townid) then
        call SecondaryTown( townid, qty, unitid )
    endif
endfunction
*/
