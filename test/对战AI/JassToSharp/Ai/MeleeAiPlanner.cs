//重构情况：从 Scripts/common.ai OneBuildLoop 的账本遍历转换
namespace OpenWC3.JassToSharp.Ai;

public static class MeleeAiPlanner
{
    public static void Build(IReadOnlyList<MeleeAiStep> steps, List<MeleeAiBuildEntry> plan,
        Func<int, string> heroCode)
    {
        plan.Clear();
        if (steps == null) return;
        foreach (MeleeAiStep step in steps)
        {
            if (step.Quantity <= 0) continue;
            switch (step.Kind)
            {
                case MeleeAiStepKind.Unit:
                    plan.Add(MeleeAiBuildEntry.Unit(step.Quantity, step.Id));
                    break;
                case MeleeAiStepKind.Upgrade:
                    plan.Add(MeleeAiBuildEntry.Upgrade(step.Quantity, step.Id));
                    break;
                case MeleeAiStepKind.Expand:
                    plan.Add(MeleeAiBuildEntry.Expand(step.Quantity, step.Id));
                    break;
                case MeleeAiStepKind.Hero:
                    string hero = heroCode?.Invoke(step.Quantity);
                    if (!string.IsNullOrEmpty(hero))
                        plan.Add(MeleeAiBuildEntry.Unit(step.Quantity, hero));
                    break;
            }
        }
    }
}

/*
原文（Scripts/common.ai，36 行）：

function OneBuildLoop takes nothing returns nothing
    local integer index = 0
    local integer qty
    local integer id
    local integer tp

    set total_gold = GetGold() - gold_buffer
    set total_wood = GetWood()

    loop
        exitwhen index == build_length

        set qty = build_qty [index]
        set id  = build_item[index]
        set tp  = build_type[index]

        //--------------------------------------------------------------------
        if tp == BUILD_UNIT then
            if not StartUnit(qty,id,build_town[index]) then
                return
            endif

        //--------------------------------------------------------------------
        elseif tp == BUILD_UPGRADE then
            call StartUpgrade(qty,id)

        //--------------------------------------------------------------------
        else // tp == BUILD_EXPAND
            if not StartExpansion(qty,id) then
                return
            endif
        endif

        set index = index + 1
    endloop
endfunction
*/
