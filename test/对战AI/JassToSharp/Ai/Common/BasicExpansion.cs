//重构情况：从 Scripts/common.ai 的 BasicExpansion 转换（待转换占位）
namespace OpenWC3.JassToSharp.Ai.Common;

public static class BasicExpansion
{
}

/*
原文（Scripts/common.ai，基础扩张）：

function BasicExpansion takes boolean build_it, integer unitid returns nothing
    if build_it and HallsCompleted(unitid) then
        call SetBuildExpa( TownCount(unitid)+1, unitid )
    endif
endfunction
*/
