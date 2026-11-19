//重构情况：从 Scripts/orc.ai build_sequence 的 SetBuildUnit/MeleeTownHall 调用序列转换（扁平化，未含条件）
namespace OpenWC3.JassToSharp.Ai.Data;

public static class OrcOpeningFlat
{
    public static readonly MeleeAiStep[] Steps =
    {
        MeleeAiStep.Expand(1, "ogre"),
        MeleeAiStep.Expand(2, "ogre"),
        MeleeAiStep.Unit(6, "opeo"),
        MeleeAiStep.Unit(1, "oalt"),
        MeleeAiStep.Unit(7, "opeo"),
        MeleeAiStep.Unit(1, "otrb"),
        MeleeAiStep.Unit(8, "opeo"),
        MeleeAiStep.Unit(2, "otrb"),
        MeleeAiStep.Unit(10, "opeo"),
        MeleeAiStep.Hero(1),
        MeleeAiStep.Unit(11, "opeo"),
        MeleeAiStep.Unit(1, "obar"),
        MeleeAiStep.Unit(1, "ofor"),
        MeleeAiStep.Unit(14, "opeo"),
        MeleeAiStep.Unit(1, "ogru"),
        MeleeAiStep.Unit(2, "ogru"),
        MeleeAiStep.Unit(3, "otrb"),
        MeleeAiStep.Unit(1, "ohun"),
        MeleeAiStep.Unit(1, "ovln"),
        MeleeAiStep.Unit(3, "ohun"),
        MeleeAiStep.Unit(4, "ogru"),
        MeleeAiStep.Unit(4, "otrb"),
        MeleeAiStep.Unit(6, "ogru"),
        MeleeAiStep.Unit(1, "ostr"),
        MeleeAiStep.Hero(2),
        MeleeAiStep.Unit(5, "otrb"),
        MeleeAiStep.Unit(1, "ocat"),
        MeleeAiStep.Unit(2, "ocat"),
        MeleeAiStep.Expand(1, "ogre"),
        MeleeAiStep.Expand(2, "ogre"),
        MeleeAiStep.Expand(3, "ogre"),
        MeleeAiStep.Hero(2),
        MeleeAiStep.Hero(1),
        MeleeAiStep.Unit(1, "oalt"),
        MeleeAiStep.Unit(2, "otau"),
        MeleeAiStep.Unit(1, "obar"),
        MeleeAiStep.Unit(3, "ogru"),
        MeleeAiStep.Unit(2, "owyv"),
        MeleeAiStep.Unit(2, "orai"),
        MeleeAiStep.Unit(1, "obar"),
        MeleeAiStep.Unit(3, "ohun"),
        MeleeAiStep.Unit(2, "otbr"),
        MeleeAiStep.Unit(3, "orai"),
        MeleeAiStep.Unit(1, "obar"),
        MeleeAiStep.Unit(3, "ohun"),
        MeleeAiStep.Unit(2, "ocat"),
        MeleeAiStep.Unit(1, "obar"),
        MeleeAiStep.Unit(1, "ofor"),
        MeleeAiStep.Unit(1, "ovln"),
        MeleeAiStep.Unit(1, "ostr"),
        MeleeAiStep.Unit(1, "osld"),
        MeleeAiStep.Unit(1, "obea"),
        MeleeAiStep.Unit(1, "ofrt"),
        MeleeAiStep.Unit(1, "otto"),
        MeleeAiStep.Unit(1, "okod"),
        MeleeAiStep.Unit(1, "oshm"),
        MeleeAiStep.Unit(1, "odoc"),
        MeleeAiStep.Unit(1, "ospw"),
    };
}

/*
原文（Scripts/orc.ai，228 行）：

function build_sequence takes nothing returns nothing
    local integer peons

    call InitBuildArray()

    if basic_opening then

        call MeleeTownHall( 0, GREAT_HALL )
        call MeleeTownHall( 1, GREAT_HALL )

        call SetBuildUnit(  6, PEON             )
        call SetBuildUnit(  1, ORC_ALTAR        )
        call SetBuildUnit(  7, PEON             )
        call SetBuildUnit(  1, BURROW           )
        call SetBuildUnit(  8, PEON             )
        call SetBuildUnit(  2, BURROW           )
        call SetBuildUnit( 10, PEON             )
        call SetBuildUnit(  1, hero_id          )
        call SetBuildUnit( 11, PEON             )
        call SetBuildUnit(  1, ORC_BARRACKS     )
        call SetBuildUnit(  1, FORGE            )
        call SetBuildUnit( 14, PEON             )
        call SetBuildUnit(  1, GRUNT            )
        call SetBuildUpgr(  1, UPG_ORC_MELEE    )
        call SetBuildUnit(  2, GRUNT            )
        call SetBuildUnit(  3, BURROW           )
        call SetBuildUnit(  1, HEAD_HUNTER      )
        call SetBuildUnit(  1, VOODOO_LOUNGE    )
        call SetBuildUpgr(  1, UPG_ORC_RANGED   )
        call SetBuildUnit(  3, HEAD_HUNTER      )
        call SetBuildUpgr(  1, UPG_ORC_ARMOR    )
        call SetBuildUnit(  4, GRUNT            )

        call BasicExpansion( c_mines < 2, GREAT_HALL )

        if MeleeDifficulty() != MELEE_NEWBIE then
            call GuardSecondary( 1, 1, BURROW )
            call GuardSecondary( 1, 2, ORC_WATCH_TOWER )
        endif

        call SetBuildUnit(  4, BURROW           )
        call SetBuildUnit(  6, GRUNT            )
        call SetBuildUnit(  1, STRONGHOLD       )
        call SetBuildUpgr(  1, UPG_ORC_SPIKES   )

        if MeleeDifficulty() != MELEE_NEWBIE then
            call SetBuildUnit(  1, hero_id2     )
        endif

        call SetBuildUnit(  5, BURROW           )
        call SetBuildUnit(  1, CATAPULT         )
        call SetBuildUpgr(  2, UPG_ORC_MELEE    )
        call SetBuildUpgr(  1, UPG_ORC_PILLAGE  )
        call SetBuildUnit(  2, CATAPULT         )
        return
    endif

    // need a peon or nothing will get built
    //
    if c_great_hall_done >= 1 then
        set peons = 6 - GetWood() / 200
        if peons < 3 then
            set peons = 3
        endif
        if c_mines < 2 or c_great_hall_done < 2 then
            set peons = peons + 5
        else
            set peons = peons + 10
        endif
        if peons > 15 then
            set peons = 15
        endif
        call SetBuildNext( peons, PEON )
    endif

    // need a great hall or we can't resource and make more peons
    //
    if c_great_hall < 1 and c_peon_done >= 1 then
        call MeleeTownHall( 0, GREAT_HALL )
        call MeleeTownHall( 1, GREAT_HALL )
        call MeleeTownHall( 2, GREAT_HALL )
    endif

    if c_gold > 500 and GetWood() < 100 then
        call SetBuildNext( 15, PEON )
    endif

    // if we have low gold in our mines then we need to expand
    //
    if c_gold_owned < 2000 then
        call BasicExpansion( c_mines < 2, GREAT_HALL )
        if MeleeDifficulty() != MELEE_NEWBIE then
            call GuardSecondary( 1, 1, BURROW )
        endif
    endif

    // get enough burrows to handle current food demand
    //
    if c_food_used + 5 >= c_food_made then
        call SetBuildUnit(  c_burrow_done + 1, BURROW )
    endif

    // always rebuild heroes for defense
    //
    if c_altar_done >= 1 then
        if b_hero1_done and MeleeDifficulty() != MELEE_NEWBIE then
            call SetBuildUnit( 1, hero_id2 )
        else
            call SetBuildUnit( 1, hero_id )
        endif
    else
        call SetBuildUnit( 1, ORC_ALTAR )
    endif

    // minimum melee defense
    //
    if c_totem_done >= 1 then
        call SetBuildUnit( 2, TAUREN        )
    else
        call SetBuildUnit( 1, ORC_BARRACKS  )
        call SetBuildUnit( 3, GRUNT         )
    endif

    // minimum ranged/air defense and siege units
    //
    if c_bestiary_done >= 1 then
        if c_fortress_done >= 1 then
            call SetBuildUnit( 2, WYVERN )
        elseif b_ensnare then
            call SetBuildUnit( 2, RAIDER )
        else
            call SetBuildUnit( 1, ORC_BARRACKS  )
            call SetBuildUnit( 3, hunter_code   )
        endif

        if c_voodoo_done >= 1 and c_fortress_done >= 1 then
            call SetBuildUnit( 2, BATRIDER  )
        else
            call SetBuildUnit( 3, RAIDER    )
        endif
    else
        call SetBuildUnit( 1, ORC_BARRACKS  )
        call SetBuildUnit( 3, hunter_code   )
        call SetBuildUnit( 2, CATAPULT      )
    endif

    // if we have a lot of gold then advance the tech tree
    //
    if c_gold > 1000 then

        if MeleeDifficulty() != MELEE_NEWBIE then
            call GuardSecondary( 1, 1, BURROW           )
            call GuardSecondary( 1, 1, ORC_WATCH_TOWER  )
            call GuardSecondary( 1, 2, BURROW           )
            call GuardSecondary( 1, 2, ORC_WATCH_TOWER  )
        endif

        call SetBuildUnit( 1, ORC_BARRACKS  )
        call SetBuildUnit( 1, FORGE         )
        call SetBuildUnit( 1, VOODOO_LOUNGE )
        call SetBuildUnit( 1, STRONGHOLD    )
        call SetBuildUnit( 1, LODGE         )
        call SetBuildUnit( 1, BESTIARY      )
        call SetBuildUnit( 1, FORTRESS      )
        call SetBuildUnit( 1, TOTEM         )

        call do_upgrades()

        if c_gold > 2000 then
            call BuildFactory( ORC_BARRACKS )
            call BuildFactory( LODGE        )
            call BuildFactory( BESTIARY     )
            call BuildFactory( TOTEM        )
        endif

    elseif c_food_used >= UPKEEP_TIER1 then
        call do_upgrades()
    endif

    call BasicExpansion( c_mines < 2, GREAT_HALL )
    if MeleeDifficulty() != MELEE_NEWBIE then
        call GuardSecondary( 1, 1, BURROW )
        call GuardSecondary( 1, 2, ORC_WATCH_TOWER )
    endif

    if c_food_used >= UPKEEP_TIER2 - 10 and c_gold < 2000 then
        return
    endif

    // full up with more troops in general
    //
    if c_totem_done >= 1 then
        call SetBuildNext( 4, TAUREN    )
    else
        call SetBuildNext( 6, GRUNT     )
    endif

    if c_bestiary_done >= 1 then
        if c_fortress_done >= 1 then
            call SetBuildNext( 3, WYVERN )
        elseif b_ensnare then
            call SetBuildNext( 4, RAIDER )
        endif
        if c_forge_done >= 1 then
            call SetBuildUnit( 1, KODO_BEAST )
        endif
    endif

    if c_lodge_done >= 1 then
        call SetBuildUnit( 1, SHAMAN        )
        call SetBuildUnit( 1, WITCH_DOCTOR  )
        call SetBuildUnit( 1, SPIRIT_WALKER )
        call SetBuildNext( 4, SHAMAN        )
        call SetBuildNext( 2, WITCH_DOCTOR  )
    endif

    if c_gold_owned < 10000 then
        call BasicExpansion( c_mines < 3, GREAT_HALL )
        if MeleeDifficulty() != MELEE_NEWBIE then
            call GuardSecondary( 2, 1, BURROW )
            call GuardSecondary( 2, 2, ORC_WATCH_TOWER )
        endif
    endif

    if c_food_used >= 60 and c_zep < 3 then
        call GetZeppelin()
    endif
endfunction
*/
