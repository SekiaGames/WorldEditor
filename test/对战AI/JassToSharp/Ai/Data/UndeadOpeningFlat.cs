//重构情况：从 Scripts/undead.ai build_sequence 的 SetBuildUnit/MeleeTownHall 调用序列转换（扁平化，未含条件）
namespace OpenWC3.JassToSharp.Ai.Data;

public static class UndeadOpeningFlat
{
    public static readonly MeleeAiStep[] Steps =
    {
        MeleeAiStep.Expand(1, "unpl"),
        MeleeAiStep.Unit(1, "uaco"),
        MeleeAiStep.Unit(1, "ugol"),
        MeleeAiStep.Unit(1, "usep"),
        MeleeAiStep.Unit(1, "ugho"),
        MeleeAiStep.Unit(1, "uzig"),
        MeleeAiStep.Unit(1, "uaod"),
        MeleeAiStep.Unit(2, "ugho"),
        MeleeAiStep.Unit(2, "uzig"),
        MeleeAiStep.Unit(3, "ugho"),
        MeleeAiStep.Hero(1),
        MeleeAiStep.Unit(6, "ugho"),
        MeleeAiStep.Unit(1, "ugrv"),
        MeleeAiStep.Unit(7, "ugho"),
        MeleeAiStep.Unit(1, "unp1"),
        MeleeAiStep.Unit(3, "uzig"),
        MeleeAiStep.Unit(8, "ugho"),
        MeleeAiStep.Unit(2, "ucry"),
        MeleeAiStep.Unit(1, "utom"),
        MeleeAiStep.Unit(3, "ucry"),
        MeleeAiStep.Hero(2),
        MeleeAiStep.Unit(1, "uaco"),
        MeleeAiStep.Unit(1, "usep"),
        MeleeAiStep.Hero(3),
        MeleeAiStep.Hero(2),
        MeleeAiStep.Hero(1),
        MeleeAiStep.Unit(1, "uaod"),
        MeleeAiStep.Unit(1, "usep"),
        MeleeAiStep.Unit(1, "ugrv"),
        MeleeAiStep.Unit(1, "usep"),
        MeleeAiStep.Expand(1, "unpl"),
        MeleeAiStep.Unit(1, "unp1"),
        MeleeAiStep.Unit(1, "uslh"),
        MeleeAiStep.Expand(1, "unpl"),
        MeleeAiStep.Unit(1, "unpl"),
        MeleeAiStep.Unit(1, "usep"),
        MeleeAiStep.Unit(1, "ugrv"),
        MeleeAiStep.Unit(1, "utom"),
        MeleeAiStep.Unit(1, "unp1"),
        MeleeAiStep.Unit(1, "uslh"),
        MeleeAiStep.Unit(1, "utod"),
        MeleeAiStep.Unit(1, "unp2"),
        MeleeAiStep.Unit(1, "usap"),
        MeleeAiStep.Unit(1, "ubon"),
        MeleeAiStep.Expand(2, "unpl"),
        MeleeAiStep.Unit(1, "uobs"),
        MeleeAiStep.Unit(1, "ubsp"),
        MeleeAiStep.Unit(4, "unec"),
        MeleeAiStep.Unit(2, "uban"),
        MeleeAiStep.Unit(2, "ubsp"),
        MeleeAiStep.Expand(3, "unpl"),
    };
}

/*
原文（Scripts/undead.ai，209 行）：

function build_sequence takes nothing returns nothing
    local integer WG2

    call InitBuildArray()

    if basic_opening then
        call MeleeTownHall( 0, NECROPOLIS_1 )

        call SetBuildUnit(  1, ACOLYTE          )
        call SetBuildUnit(  1, UNDEAD_MINE      )
        call SetBuildNext(  5, ACOLYTE          )
        call SetBuildUnit(  1, CRYPT            )
        call SetBuildUnit(  1, GHOUL            )
        call SetBuildUnit(  1, ZIGGURAT_1       )
        call SetBuildUnit(  1, UNDEAD_ALTAR     )
        call SetBuildUnit(  2, GHOUL            )
        call SetBuildUnit(  2, ZIGGURAT_1       )
        call SetBuildUnit(  3, GHOUL            )
        call SetBuildUnit(  1, hero_id          )
        call SetBuildUnit(  6, GHOUL            )
        call SetBuildUpgr(  1, UPG_CANNIBALIZE  )
        call SetBuildUnit(  1, GRAVEYARD        )
        call SetBuildUnit(  7, GHOUL            )
        call SetBuildUnit(  1, NECROPOLIS_2     )
        call SetBuildUnit(  3, ZIGGURAT_1       )
        call SetBuildUnit(  8, GHOUL            )
        call SetBuildUnit(  2, CRYPT_FIEND      )
        call SetBuildUnit(  1, TOMB_OF_RELICS   )
        call SetBuildUnit(  3, CRYPT_FIEND      )
        call SetBuildUpgr(  1, UPG_FIEND_WEB    )

        if MeleeDifficulty() != MELEE_NEWBIE then
            call SetBuildUnit(  1, hero_id2     )
        endif

        call undead_mine(1)
        call BasicExpansion( c_mines < 2, UNDEAD_MINE )

        return
    endif

    // need an acolyte or nothing will get built
    //
    if c_neclis_done >= 1 then
        call SetBuildUnit( 1, ACOLYTE )
    endif

    // keep producing mines and acolytes to get gold
    //
    call undead_mine(0)
    call undead_mine(1)

    if c_undead_mines >= 2 then
        call SetBuildNext( 10, ACOLYTE )
    elseif c_undead_mines == 1 then
        call SetBuildNext(  5, ACOLYTE )
    endif

    // ghouls collect lumber
    //
    set WG2 = Max(0,10 - c_wood / 120)
    if WG2 > 0 then
        call SetBuildUnit(   1, CRYPT   )
        call SetBuildNext( WG2, GHOUL   )
    endif

    // if we have low gold in our mines then we need to expand
    //
    if c_gold_owned < 2000 or (c_mines < 2 and c_acolyte > 5) then
        call BasicExpansion( c_mines < 2, UNDEAD_MINE )

        if MeleeDifficulty() != MELEE_NEWBIE then
            call GuardSecondary( 1, 2, ZIGGURAT_1 )
            if c_grave_done >= 1 then
                call GuardSecondary( 1, 2, ZIGGURAT_2 )
            endif
        endif
    endif

    // get enough burrows to handle current food demand
    //
    if c_food_used + 7 >= c_food_made then
        call SetBuildUnit(  c_zig_done + 1, ZIGGURAT_1 )
    endif

    // always rebuild heroes for defense
    //
    if c_altar_done >= 1 then
        if b_hero1_done and MeleeDifficulty() != MELEE_NEWBIE then
            if b_hero2_done then
                if b_hero3_done or c_citadel_done >= 1 then
                    call SetBuildUnit( 1, hero_id3 )
                endif
            else
                call SetBuildUnit( 1, hero_id2 )
            endif
        else
            call SetBuildUnit( 1, hero_id )
        endif
    else
        call SetBuildUnit( 1, UNDEAD_ALTAR )
    endif

    // minimum melee defense
    //
    if c_slaughter_done >= 1 and c_citadel_done >= 1 then
        call SetBuildNext( 2, ABOMINATION )
    else
        call SetBuildUnit(     1, CRYPT )
        call SetBuildNext( WG2+6, GHOUL )
    endif

    // minimum ranged/air defense
    //
    call SetBuildUnit( 1, GRAVEYARD )
    if c_citadel_done >= 1 then
        call SetBuildNext( 4, GARGOYLE )
    else
        call SetBuildUnit( 1, CRYPT         )
        call SetBuildNext( 3, CRYPT_FIEND   )
    endif

    // siege attackers
    //
    if c_boneyard_done >= 1 then
        call SetBuildNext( 2, FROST_WYRM )

    elseif c_slaughter_done >= 1 then
        call SetBuildNext( 2, MEAT_WAGON )
    else
        call MeleeTownHall( 0, NECROPOLIS_1 )
        call SetBuildUnit ( 1, NECROPOLIS_1 )

        call SetBuildUnit( 1, NECROPOLIS_2      )
        call SetBuildUnit( 1, SLAUGHTERHOUSE    )
        call SetBuildNext( 2, MEAT_WAGON        )
    endif

    // if we have a lot of gold then advance the tech tree
    //
    if c_gold > 1000 then

        call MeleeTownHall( 0, NECROPOLIS_1 )

        call SetBuildUnit( 1, NECROPOLIS_1      )
        call SetBuildUnit( 1, CRYPT             )
        call SetBuildUnit( 1, GRAVEYARD         )
        call SetBuildUnit( 1, TOMB_OF_RELICS    )
        call SetBuildUnit( 1, NECROPOLIS_2      )

        call SetBuildNext( Min(c_neclis_done,c_halls_done+1), NECROPOLIS_2 )
        call SetBuildNext( Min(c_zig_done,c_spirit_done+1),   ZIGGURAT_2   )

        call SetBuildUnit( 1, SLAUGHTERHOUSE    )
        call SetBuildUnit( 1, DAMNED_TEMPLE     )
        call SetBuildUnit( 1, NECROPOLIS_3      )
        call SetBuildUnit( 1, SAC_PIT           )
        call SetBuildUnit( 1, BONEYARD          )

        call do_upgrades()

        if c_gold > 2000 then
            call BuildFactory( CRYPT            )
            call BuildFactory( DAMNED_TEMPLE    )
            call BuildFactory( SLAUGHTERHOUSE   )
            call BuildFactory( BONEYARD         )
        endif

    elseif c_food_used >= UPKEEP_TIER1 then
        call do_upgrades()
    endif

    call undead_mine(1)
    call BasicExpansion( c_mines < 2, UNDEAD_MINE )
    call MeleeTownHall( 1, NECROPOLIS_1 )

    if c_food_used >= UPKEEP_TIER2-10 and c_gold < 2000 then
        return
    endif

    // extra troops
    //
    if c_citadel_done >= 1 and c_tomb_done >= 1 then
        call SetBuildUnit( 1, OBS_STATUE )
        if b_sphinx_ok then
            call SetBuildUnit( 1, BLK_SPHINX )
        endif
    endif

    if c_temple_done >= 1 then
        call SetBuildUnit( 4, NECRO     )
        call SetBuildUnit( 2, BANSHEE   )
    endif

    if c_citadel_done >= 1 and c_tomb_done >= 1 and b_sphinx_ok then
        call SetBuildUnit( 2, BLK_SPHINX )
    endif

    if c_gold_owned < 10000 then
        call undead_mine(2)
        call BasicExpansion( c_mines < 3, UNDEAD_MINE )
        call MeleeTownHall( 2, NECROPOLIS_1     )
        call GuardSecondary( 2, 2, ZIGGURAT_1   )
    endif

    if c_food_used >= 60 and c_zep < 3 then
        call GetZeppelin()
    endif
endfunction
*/
