//重构情况：从 Scripts/elf.ai build_sequence 的 SetBuildUnit/MeleeTownHall 调用序列转换（扁平化，未含条件）
namespace OpenWC3.JassToSharp.Ai.Data;

public static class ElfOpeningFlat
{
    public static readonly MeleeAiStep[] Steps =
    {
        MeleeAiStep.Unit(3, "earc"),
        MeleeAiStep.Unit(1, "etol"),
        MeleeAiStep.Unit(5, "ewsp"),
        MeleeAiStep.Unit(1, "eate"),
        MeleeAiStep.Unit(7, "ewsp"),
        MeleeAiStep.Unit(1, "emow"),
        MeleeAiStep.Unit(8, "ewsp"),
        MeleeAiStep.Unit(1, "eaom"),
        MeleeAiStep.Unit(9, "ewsp"),
        MeleeAiStep.Hero(1),
        MeleeAiStep.Unit(10, "ewsp"),
        MeleeAiStep.Unit(2, "emow"),
        MeleeAiStep.Unit(1, "eden"),
        MeleeAiStep.Unit(11, "ewsp"),
        MeleeAiStep.Unit(12, "ewsp"),
        MeleeAiStep.Unit(1, "edob"),
        MeleeAiStep.Unit(3, "emow"),
        MeleeAiStep.Unit(13, "ewsp"),
        MeleeAiStep.Unit(14, "ewsp"),
        MeleeAiStep.Unit(15, "ewsp"),
        MeleeAiStep.Unit(1, "etoa"),
        MeleeAiStep.Unit(4, "emow"),
        MeleeAiStep.Hero(2),
        MeleeAiStep.Expand(1, "etol"),
        MeleeAiStep.Expand(2, "etol"),
        MeleeAiStep.Expand(3, "etol"),
        MeleeAiStep.Hero(2),
        MeleeAiStep.Hero(1),
        MeleeAiStep.Unit(1, "eate"),
        MeleeAiStep.Unit(1, "eaom"),
        MeleeAiStep.Unit(2, "edry"),
        MeleeAiStep.Unit(1, "eaom"),
        MeleeAiStep.Unit(3, "earc"),
        MeleeAiStep.Unit(2, "echm"),
        MeleeAiStep.Unit(2, "ebal"),
        MeleeAiStep.Unit(1, "eaom"),
        MeleeAiStep.Unit(1, "edob"),
        MeleeAiStep.Unit(1, "etoa"),
        MeleeAiStep.Unit(1, "eden"),
        MeleeAiStep.Unit(1, "eaoe"),
        MeleeAiStep.Unit(1, "etoe"),
        MeleeAiStep.Unit(1, "eaow"),
        MeleeAiStep.Unit(1, "edos"),
        MeleeAiStep.Hero(3),
        MeleeAiStep.Unit(1, "edoc"),
        MeleeAiStep.Unit(1, "efdr"),
        MeleeAiStep.Unit(1, "edot"),
    };
}

/*
原文（Scripts/elf.ai，221 行）：

function build_sequence takes nothing returns nothing
    local boolean primary_melee
    local integer wisps

    call InitBuildArray()

    if basic_opening then
        call SetBuildUnit(  1, TREE_LIFE        )
        call SetBuildUnit(  5, WISP             )
        call SetBuildUnit(  1, ELF_ALTAR        )
        call SetBuildUnit(  7, WISP             )
        call SetBuildUnit(  1, MOON_WELL        )
        call SetBuildUnit(  8, WISP             )
        call SetBuildUnit(  1, ANCIENT_WAR      )
        call SetBuildUnit(  9, WISP             )
        call SetBuildUnit(  1, hero_id          )
        call SetBuildUnit( 10, WISP             )
        call SetBuildUnit(  2, MOON_WELL        )
        call basics(2)// (  1, ARCHER           )
        call SetBuildUnit(  1, DEN_OF_WONDERS   )
        call basics(4)// (  2, ARCHER           )
        call SetBuildUnit( 11, WISP             )
        call basics(6)// (  3, ARCHER           )
        call SetBuildUnit( 12, WISP             )
        call SetBuildUnit(  1, HUNTERS_HALL     )
        call SetBuildUnit(  3, MOON_WELL        )
        call SetBuildUnit( 13, WISP             )
        call basics(8)// (  4, ARCHER           )
        call SetBuildUnit( 14, WISP             )
        call basics(10)//(  5, ARCHER           )
        call SetBuildUnit( 15, WISP             )
        call basics(15)//(  6, ARCHER           )
                       //(  1, HUNTRESS         )
        call SetBuildUnit(  1, TREE_AGES        )

        call BasicExpansion( c_mines < 2, TREE_LIFE )

        call SetBuildUpgr(  1, UPG_STR_MOON     )
        call SetBuildUpgr(  1, UPG_MOON_ARMOR   )
        call SetBuildUnit(  4, MOON_WELL        )

        if MeleeDifficulty() != MELEE_NEWBIE then
            call SetBuildUnit(  1, hero_id2     )
        endif

        return
    endif

    if c_tree_life < 1 and c_wisp_done > 0 then
        call MeleeTownHall( 0, TREE_LIFE )
        call MeleeTownHall( 1, TREE_LIFE )
        call MeleeTownHall( 2, TREE_LIFE )
    endif

    if c_tree_life_done > 0 then 
        set wisps = 6 - GetWood() / 200
        if wisps < 3 then
            set wisps = 3
        endif
        if c_mines < 2 or c_tree_life_done < 2 then
            set wisps = wisps + 5
        else
            set wisps = wisps + 10
        endif
        if wisps > 15 then
            set wisps = 15
        endif
        call SetBuildNext( wisps, WISP )
    endif

    if c_gold > 500 and GetWood() < 100 then
        call SetBuildNext( 15, WISP )
    endif

    // having enough gold is the highest priority
    //
    if c_gold_owned < 2000 then
        call BasicExpansion( c_mines < 2, TREE_LIFE )
        if MeleeDifficulty() != MELEE_NEWBIE then
            call GuardSecondary( 1, 1, ANCIENT_PROTECT )
            call GuardSecondary( 1, 2, ANCIENT_PROTECT )
        endif
    endif

    // get enough moon wells to cover food need
    //
    if c_food_used + 7 > c_food_made then
        call SetBuildUnit( c_moon_well_done + 1, MOON_WELL )
    endif

    // recover heroes for basic defense
    //
    if c_altar_done >= 1 then

        if b_hero1_done and MeleeDifficulty() != MELEE_NEWBIE then
            call SetBuildUnit( 1, hero_id2 )
        else
            call SetBuildUnit( 1, hero_id  )
        endif
    else
        call SetBuildUnit( 1, ELF_ALTAR )
    endif

    // the primary melee force is the mountain giant
    //
    set primary_melee = c_lore_done >= 1 and c_wonders_done >= 1 and c_tree_ages_done >= 1
    if primary_melee then
        call SetBuildNext( 1, MOUNTAIN_GIANT )

    // the backup melee force is the huntress
    //
    else
        call SetBuildUnit( 1, ANCIENT_WAR   )
        call SetBuildNext( 3, HUNTRESS      )
    endif

    // the primary ranged force is the dryad
    //
    if c_lore_done >= 1 then
        call SetBuildUnit( 2, DRYAD )

    // the backup ranged force is the archer
    //
    else
        call SetBuildUnit( 1, ANCIENT_WAR   )
        call SetBuildUnit( 3, ARCHER        )
    endif

    // need siege to take out enemy towns and expansions
    //
    if b_acid_breath and c_roost_done >= 1 then
        call SetBuildUnit( 2, CHIMAERA )
    elseif c_mtn_giant < 1 then
        call SetBuildUnit( 2, BALLISTA )
    endif

    // if we have enough gold then advance on the tech tree
    //
    if c_gold > 1000 then

        if MeleeDifficulty() != MELEE_NEWBIE then
            call GuardSecondary( 1, 1, ANCIENT_PROTECT )
            call GuardSecondary( 1, 2, ANCIENT_PROTECT )
        endif

        call SetBuildUnit(  1, ANCIENT_WAR      )
        call SetBuildUnit(  1, HUNTERS_HALL     )
        call SetBuildUnit(  1, TREE_AGES        )
        call SetBuildUnit(  1, DEN_OF_WONDERS   )
        call SetBuildUnit(  1, ANCIENT_LORE     )
        call SetBuildUnit(  1, TREE_ETERNITY    )
        call SetBuildUnit(  1, ANCIENT_WIND     )
        call SetBuildUnit(  1, CHIMAERA_ROOST   )

        call do_upgrades()

        if c_gold > 2000 then
            call BuildFactory( ANCIENT_LORE     )
            call BuildFactory( ANCIENT_WAR      )
            call BuildFactory( CHIMAERA_ROOST   )
            call BuildFactory( ANCIENT_WIND     )
        endif

    elseif c_food_used >= UPKEEP_TIER1 then
        call do_upgrades()
    endif

    call BasicExpansion( c_mines < 2, TREE_LIFE )
    if MeleeDifficulty() != MELEE_NEWBIE then
        call GuardSecondary( 1, 1, ANCIENT_PROTECT )
        call GuardSecondary( 1, 2, ANCIENT_PROTECT )
    endif

    if c_food_used >= UPKEEP_TIER2 - 10 and c_gold < 2000 then
        return
    endif

    // build units from whatever buildings we already have
    //
    if primary_melee then
        call SetBuildNext( 3, MOUNTAIN_GIANT )
    else
        call SetBuildNext( 7, HUNTRESS )
    endif

    if c_lore_done >= 1 then
        call SetBuildNext( 4, DRYAD )
    else
        call SetBuildNext( 6, ARCHER )
    endif

    if c_tree_ages_done >= 1 then
        if c_tree_etern_done >= 1 and c_altar_done >= 1 and MeleeDifficulty() != MELEE_NEWBIE then
            call SetBuildUnit( 1, hero_id3 )
        endif

        if c_lore_done >= 1 then
            call SetBuildUnit( 1, DRUID_CLAW )
        endif
    endif

    if c_wind_done >= 1 then
        if c_wonders_done >= 1 then
            call SetBuildUnit( 1, FAERIE_DRAGON )
        endif

        call SetBuildUnit( 1, DRUID_TALON )
    endif

    if c_gold_owned < 10000 then
        call BasicExpansion( c_mines < 3, TREE_LIFE )
        if MeleeDifficulty() != MELEE_NEWBIE then
            call GuardSecondary( 2, 1, ANCIENT_PROTECT )
            call GuardSecondary( 2, 2, ANCIENT_PROTECT )
        endif
    endif

    if c_food_used >= 60 and c_zeps < 3 then
        call GetZeppelin()
    endif
endfunction
*/
