//重构情况：从 Scripts/common.ai 的 GetUnitCount/GetUnitCountDone/GetGold/GetWood/FoodUsed 等 native 查询转换
namespace OpenWC3.JassToSharp.Ai;

public interface IMeleeAiWorld
{
    int UnitCount(int player, string typeId);

    int UnitCountDone(int player, string typeId);

    int BuildingCount(int player, string typeId, bool doneOnly);

    int Gold(int player);

    int Lumber(int player);

    int FoodUsed(int player);

    int FoodMade(int player);

    int OwnedMineCount(int player);

    int OwnedMineGold(int player);
}

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
