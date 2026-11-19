//重构情况：从 Scripts/common.ai 的 rawcode 常量表转换（人族单位/建筑/科技）
namespace OpenWC3.JassToSharp.Ai.Data;

public static class HumanCodes
{
    public const string HumanPeasant = "hpea";
    public const string HumanMilitia = "hmil";
    public const string HumanFootman = "hfoo";
    public const string HumanRifleman = "hrif";
    public const string HumanKnight = "hkni";
    public const string HumanPriest = "hmpr";
    public const string HumanSorceress = "hsor";
    public const string HumanSpellBreaker = "hspt";
    public const string HumanGryphon = "hgry";
    public const string HumanCopter = "hgyr";
    public const string HumanDragonHawk = "hdhw";
    public const string HumanMortar = "hmtm";
    public const string HumanTank = "hmtt";

    public const string HumanTownHall = "htow";
    public const string HumanKeep = "hkee";
    public const string HumanCastle = "hcas";
    public const string HumanAltar = "halt";
    public const string HumanBarracks = "hbar";
    public const string HumanBlacksmith = "hbla";
    public const string HumanLumberMill = "hlum";
    public const string HumanWorkshop = "harm";
    public const string HumanSanctum = "hars";
    public const string HumanAviary = "hgra";
    public const string HumanArcaneVault = "hvlt";
    public const string HumanHouse = "hhou";
    public const string HumanWatchTower = "hwtw";
    public const string HumanGuardTower = "hgtw";
    public const string HumanArcaneTower = "hatw";

    public const string HumanUpgradeMelee = "Rhme";
    public const string HumanUpgradeRanged = "Rhra";
    public const string HumanUpgradeArmor = "Rhar";
    public const string HumanUpgradeMasonry = "Rhac";
    public const string HumanUpgradeDefend = "Rhde";
    public const string HumanUpgradeBreeding = "Rhan";
    public const string HumanUpgradePraying = "Rhpt";
    public const string HumanUpgradeSorcery = "Rhst";
    public const string HumanUpgradeLeather = "Rhla";
    public const string HumanUpgradeGunRange = "Rhri";
    public const string HumanUpgradeWood = "Rhlh";
    public const string HumanUpgradeSentinel = "Rhse";
    public const string HumanUpgradeBombs = "Rhgb";
    public const string HumanUpgradeHammers = "Rhhb";
    public const string HumanUpgradeControlMagic = "Rhss";
    public const string HumanUpgradeFrags = "Rhfs";
    public const string HumanUpgradeTank = "Rhrt";
    public const string HumanUpgradeFlak = "Rhfc";
    public const string HumanUpgradeCloud = "Rhcd";
}

/*
原文（Scripts/common.ai 人族 rawcode 常量，37 行）：

    constant integer FOOTMAN            = 'hfoo'
    constant integer GRYPHON            = 'hgry'
    constant integer KNIGHT             = 'hkni'
    constant integer MORTAR             = 'hmtm'
    constant integer PEASANT            = 'hpea'
    constant integer RIFLEMAN           = 'hrif'
    constant integer AVIARY             = 'hgra'
    constant integer BARRACKS           = 'hbar'
    constant integer BLACKSMITH         = 'hbla'
    constant integer CASTLE             = 'hcas'
    constant integer HOUSE              = 'hhou'
    constant integer HUMAN_ALTAR        = 'halt'
    constant integer KEEP               = 'hkee'
    constant integer LUMBER_MILL        = 'hlum'
    constant integer SANCTUM            = 'hars'
    constant integer TOWN_HALL          = 'htow'
    constant integer WORKSHOP           = 'harm'
    constant integer ARCANE_VAULT       = 'hvlt'
    constant integer UPG_MELEE          = 'Rhme'
    constant integer UPG_RANGED         = 'Rhra'
    constant integer UPG_ARMOR          = 'Rhar'
    constant integer UPG_MASONRY        = 'Rhac'
    constant integer UPG_DEFEND         = 'Rhde'
    constant integer UPG_BREEDING       = 'Rhan'
    constant integer UPG_PRAYING        = 'Rhpt'
    constant integer UPG_SORCERY        = 'Rhst'
    constant integer UPG_LEATHER        = 'Rhla'
    constant integer UPG_GUN_RANGE      = 'Rhri'
    constant integer UPG_WOOD           = 'Rhlh'
    constant integer UPG_SENTINEL       = 'Rhse'
    constant integer UPG_BOMBS          = 'Rhgb'
    constant integer UPG_HAMMERS        = 'Rhhb'
    constant integer UPG_CONT_MAGIC     = 'Rhss'
    constant integer UPG_FRAGS          = 'Rhfs'
    constant integer UPG_TANK           = 'Rhrt'
    constant integer UPG_FLAK           = 'Rhfc'
    constant integer UPG_CLOUD          = 'Rhcd'
*/
