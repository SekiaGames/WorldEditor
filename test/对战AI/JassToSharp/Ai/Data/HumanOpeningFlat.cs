//重构情况：从 Scripts/human.ai build_sequence 的 SetBuildUnit/MeleeTownHall 调用序列转换（扁平化，未含条件）
namespace OpenWC3.JassToSharp.Ai.Data;

public static class HumanOpeningFlat
{
    public static readonly MeleeAiStep[] Steps =
    {
        MeleeAiStep.Expand(1, "htow"),
        MeleeAiStep.Expand(2, "htow"),
        MeleeAiStep.Unit(6, "hpea"),
        MeleeAiStep.Unit(1, "halt"),
        MeleeAiStep.Unit(7, "hpea"),
        MeleeAiStep.Unit(1, "hhou"),
        MeleeAiStep.Unit(1, "hbar"),
        MeleeAiStep.Unit(9, "hpea"),
        MeleeAiStep.Unit(2, "hhou"),
        MeleeAiStep.Hero(1),
        MeleeAiStep.Unit(11, "hpea"),
        MeleeAiStep.Unit(1, "hfoo"),
        MeleeAiStep.Unit(3, "hhou"),
        MeleeAiStep.Unit(12, "hpea"),
        MeleeAiStep.Unit(2, "hfoo"),
        MeleeAiStep.Unit(13, "hpea"),
        MeleeAiStep.Unit(3, "hfoo"),
        MeleeAiStep.Unit(14, "hpea"),
        MeleeAiStep.Unit(4, "hhou"),
        MeleeAiStep.Unit(15, "hpea"),
        MeleeAiStep.Unit(4, "hfoo"),
        MeleeAiStep.Unit(16, "hpea"),
        MeleeAiStep.Unit(5, "hfoo"),
        MeleeAiStep.Unit(1, "hbla"),
        MeleeAiStep.Unit(6, "hfoo"),
        MeleeAiStep.Unit(5, "hhou"),
        MeleeAiStep.Unit(8, "hfoo"),
        MeleeAiStep.Unit(6, "hhou"),
        MeleeAiStep.Unit(2, "hrif"),
        MeleeAiStep.Unit(7, "hhou"),
        MeleeAiStep.Unit(3, "hrif"),
        MeleeAiStep.Unit(1, "hvlt"),
        MeleeAiStep.Unit(4, "hrif"),
        MeleeAiStep.Unit(1, "hlum"),
        MeleeAiStep.Unit(1, "hkee"),
        MeleeAiStep.Unit(7, "hhou"),
        MeleeAiStep.Unit(1, "harm"),
        MeleeAiStep.Hero(2),
        MeleeAiStep.Unit(1, "hcas"),
        MeleeAiStep.Unit(1, "hars"),
        MeleeAiStep.Unit(2, "hmtm"),
        MeleeAiStep.Expand(1, "htow"),
        MeleeAiStep.Expand(2, "htow"),
        MeleeAiStep.Expand(3, "htow"),
        MeleeAiStep.Hero(3),
        MeleeAiStep.Hero(2),
        MeleeAiStep.Hero(1),
        MeleeAiStep.Unit(1, "halt"),
        MeleeAiStep.Unit(1, "hbar"),
        MeleeAiStep.Unit(2, "hkni"),
        MeleeAiStep.Unit(4, "hfoo"),
        MeleeAiStep.Unit(3, "hgry"),
        MeleeAiStep.Unit(1, "hbla"),
        MeleeAiStep.Unit(4, "hrif"),
        MeleeAiStep.Unit(1, "hkee"),
        MeleeAiStep.Unit(1, "harm"),
        MeleeAiStep.Unit(2, "hmtm"),
        MeleeAiStep.Unit(1, "hvlt"),
        MeleeAiStep.Unit(1, "hbla"),
        MeleeAiStep.Unit(1, "hlum"),
        MeleeAiStep.Unit(1, "hars"),
        MeleeAiStep.Unit(1, "hcas"),
        MeleeAiStep.Unit(1, "hgra"),
    };
}

/*
原文（Scripts/human.ai，248 行）：

function build_sequence takes nothing returns nothing
    local integer peasants

    call InitBuildArray()

    if basic_opening then

        call MeleeTownHall( 0, TOWN_HALL )
        call MeleeTownHall( 1, TOWN_HALL )

        call SetBuildUnit(  6, PEASANT      )
        call SetBuildUnit(  1, HUMAN_ALTAR  )
        call SetBuildUnit(  7, PEASANT      )
        call SetBuildUnit(  1, HOUSE        )
        call SetBuildUnit(  1, BARRACKS     )
        call SetBuildUnit(  9, PEASANT      )
        call SetBuildUnit(  2, HOUSE        )
        call SetBuildUnit(  1, hero_id      )
        call SetBuildUnit( 11, PEASANT      )
        call SetBuildUnit(  1, FOOTMAN      )
        call SetBuildUnit(  3, HOUSE        )
        call SetBuildUnit( 12, PEASANT      )
        call SetBuildUnit(  2, FOOTMAN      )
        call SetBuildUnit( 13, PEASANT      )
        call SetBuildUnit(  3, FOOTMAN      )
        call SetBuildUnit( 14, PEASANT      )
        call SetBuildUnit(  4, HOUSE        )
        call SetBuildUnit( 15, PEASANT      )
        call SetBuildUnit(  4, FOOTMAN      )
        call SetBuildUnit( 16, PEASANT      )
        call SetBuildUnit(  5, FOOTMAN      )
        call SetBuildUnit(  1, BLACKSMITH   )
        call SetBuildUnit(  6, FOOTMAN      )
        call SetBuildUnit(  5, HOUSE        )
        call SetBuildUnit(  8, FOOTMAN      )
        call SetBuildUnit(  6, HOUSE        )
        call SetBuildUnit(  2, RIFLEMAN     )
        call SetBuildUnit(  7, HOUSE        )
        call SetBuildUnit(  3, RIFLEMAN     )
        call SetBuildUnit(  1, ARCANE_VAULT )

        call BasicExpansion( c_mines < 2, TOWN_HALL )

        call SetBuildUnit(  4, RIFLEMAN     )
        call SetBuildUnit(  1, LUMBER_MILL  )

        if MeleeDifficulty() != MELEE_NEWBIE then
            call GuardSecondary( 1, 2, WATCH_TOWER  )
            if c_mill_done >= 1 then
                call GuardSecondary( 1, 1, GUARD_TOWER  )
            endif
            if c_watch_done >= 1 then
                call GuardSecondary( 1, 1, ARCANE_TOWER )
            endif
        endif

        call SetBuildUnit(  1, KEEP         )
        call SetBuildUpgr(  1, UPG_DEFEND   )
        call SetBuildUpgr(  1, UPG_ARMOR    )
        call SetBuildUnit(  7, HOUSE        )
        call SetBuildUpgr(  1, UPG_MELEE    )

        if c_keep_done < 1 then
            return
        endif

        call SetBuildUnit(  1, WORKSHOP     )

        if MeleeDifficulty() != MELEE_NEWBIE then
            call SetBuildUnit(  1, hero_id2 )
        endif

        call SetBuildUnit(  1, CASTLE       )

        call GuardSecondary( 0, 2, WATCH_TOWER  )
        if c_watch_done >= 3 then
            call GuardSecondary( 0, 1, GUARD_TOWER  )
            if c_watch_done >= 4 then
                call GuardSecondary( 0, 1, ARCANE_TOWER )
            endif
        endif

        call SetBuildUpgr(  1, UPG_RANGED   )
        call SetBuildUnit(  1, SANCTUM      )
        call SetBuildUnit(  2, MORTAR       )
        call SetBuildUpgr(  1, UPG_WOOD     )
        call SetBuildUpgr(  2, UPG_ARMOR    )
        call SetBuildUpgr(  2, UPG_WOOD     )
        return
    endif

    // need a peasant or nothing will get built
    //
    if c_hall_done >= 1 then
        set peasants = 6 - GetWood() / 200
        if peasants < 3 then
            set peasants = 3
        endif
        if c_mines < 2 then
            set peasants = peasants + 5
        else
            set peasants = peasants + 10
        endif
        if peasants > 15 then
            set peasants = 15
        endif
        call SetBuildNext( peasants, PEASANT )
    endif

    // need a hall or we can't resource and make more peasants
    //
    if c_hall < 1 and c_peasant_done >= 1 then
        call MeleeTownHall( 0, TOWN_HALL )
        call MeleeTownHall( 1, TOWN_HALL )
        call MeleeTownHall( 2, TOWN_HALL )
    endif

    if c_gold > 500 and GetWood() < 100 then
        call SetBuildNext( 20, PEASANT )
    endif

    // if we have low gold in our mines then we need to expand
    //
    if c_gold_owned < 2000 then
        call BasicExpansion( c_mines < 2, TOWN_HALL )
        if MeleeDifficulty() != MELEE_NEWBIE then
            call GuardSecondary( 1, 2, WATCH_TOWER  )
            if c_watch >= 2 then
                call GuardSecondary( 1, 1, GUARD_TOWER  )
                call GuardSecondary( 1, 1, ARCANE_TOWER )
            endif
        endif
    endif

    // get enough burrows to handle current food demand
    //
    if c_food_used + 5 >= c_food_made then
        call SetBuildUnit(  c_house_done + 1, HOUSE )
    endif

    // always rebuild heroes for defense
    //
    if c_altar_done >= 1 then
        if b_hero1_done and MeleeDifficulty() != MELEE_NEWBIE then
            if b_hero2_done then
                if b_hero3_done or c_castle_done >= 1 then
                    call SetBuildUnit( 1, hero_id3 )
                endif
            else
                call SetBuildUnit( 1, hero_id2 )
            endif
        else
            call SetBuildUnit( 1, hero_id )
        endif
    else
        call SetBuildUnit( 1, HUMAN_ALTAR )
    endif

    // minimum melee defense
    //
    call SetBuildUnit( 1, BARRACKS )
    if b_knights_ok then
        call SetBuildUnit( 2, KNIGHT    )
    else
        call SetBuildUnit( 4, FOOTMAN   )
    endif

    // minimum ranged/air defense
    //
    if c_aviary_done >= 1 then
        call SetBuildUnit( 3, GRYPHON    )
    else
        call SetBuildUnit( 1, BLACKSMITH )
        if c_smith_done >= 1 or c_gold < 1000 then
            call SetBuildUnit( 4, RIFLEMEN   )
        endif
    endif

    // siege attackers
    //
    call SetBuildUnit( 1, KEEP      )
    call SetBuildUnit( 1, WORKSHOP  )
    call SetBuildUnit( 2, MORTAR    )

    call SetBuildUnit( c_watch_done - c_arcane, GUARD_TOWER )
    
    // if we have a lot of gold then advance the tech tree
    //
    if c_gold > 1000 then
        call SetBuildUnit( 1, ARCANE_VAULT  )
        call SetBuildUnit( 1, BLACKSMITH    )
        call SetBuildUnit( 1, LUMBER_MILL   )
        call SetBuildUnit( 1, SANCTUM       )
        call SetBuildUnit( 1, CASTLE        )
        call SetBuildUnit( 1, AVIARY        )

        call do_upgrades()

        if c_gold > 2000 then
            call BuildFactory( BARRACKS     )
            call BuildFactory( SANCTUM      )
            call BuildFactory( AVIARY       )
        endif

        call SetBuildUpgr( 1, UPG_SENTINEL  )

    elseif c_food_used >= UPKEEP_TIER1 then
        call do_upgrades()
    endif

    call BasicExpansion( c_mines < 2, TOWN_HALL )

    if c_food_used >= UPKEEP_TIER2-10 and c_gold < 2000 then
        return
    endif

    // full up with more troops in general
    //
    if b_knights_ok then
        call SetBuildNext( 4, KNIGHT )
    endif

    if c_sanctum_done >= 1 then
        call SetBuildNext( 2, PRIEST        )
        call SetBuildNext( 1, SORCERESS     )
        call SetBuildNext( 1, SPELL_BREAKER )

        if c_aviary_done >= 1 then
            call SetBuildNext( 4, GRYPHON           )
            call SetBuildNext( 2, HUMAN_DRAGON_HAWK )
        endif

        if c_workshop_done >= 1 then
            call SetBuildNext( 2, COPTER )
        endif

        call SetBuildNext( 2, SORCERESS     )
        call SetBuildNext( 1, SPELL_BREAKER )
    endif

    if c_gold_owned < 10000 then
        call BasicExpansion( c_mines < 3, TOWN_HALL )
    endif

    if c_food_used >= 60 and c_zep < 3 then
        call GetZeppelin()
    endif
endfunction
*/
