//重构情况：从 Scripts/common.ai 的 WaitForUnits 转换（待转换占位）
namespace OpenWC3.JassToSharp.Ai.Common;

public static class WaitForUnits
{
}

/*
原文（Scripts/common.ai，等待单位）：

function WaitForUnits takes integer unitid, integer qty returns nothing
    loop
        exitwhen TownCountDone(unitid) == qty
        call Sleep(2)
    endloop
endfunction
*/
