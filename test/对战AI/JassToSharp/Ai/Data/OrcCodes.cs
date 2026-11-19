//重构情况：从 Scripts/common.ai 的 rawcode 常量表转换（兽族单位/建筑/科技/英雄）
namespace OpenWC3.JassToSharp.Ai.Data;

public static class OrcCodes
{
    public const string Peon = "opeo";
    public const string GreatHall = "ogre";
    public const string Stronghold = "ostr";
    public const string Fortress = "ofrt";
    public const string Altar = "oalt";
    public const string Burrow = "otrb";
    public const string Barracks = "obar";
    public const string Forge = "ofor";
    public const string VoodooLounge = "ovln";
    public const string WatchTower = "owtw";
    public const string Totem = "otto";
    public const string Bestiary = "obea";
    public const string Lodge = "osld";
    public const string Grunt = "ogru";
    public const string HeadHunter = "ohun";
    public const string Catapult = "ocat";
    public const string Tauren = "otau";
    public const string Raider = "orai";
    public const string Wyvern = "owyv";
    public const string Batrider = "otbr";
    public const string Shaman = "oshm";
    public const string WitchDoctor = "odoc";
    public const string SpiritWalker = "ospw";
    public const string KodoBeast = "okod";
    public const string FarSeer = "Ofar";
    public const string Blademaster = "Obla";
    public const string TaurenChieftain = "Otch";
    public const string UpgradeMelee = "Rome";
    public const string UpgradeRanged = "Rora";
    public const string UpgradeArmor = "Roar";
    public const string UpgradeSpikes = "Rosp";
    public const string UpgradePillage = "Ropg";
}

/*
原文（Scripts/common.ai 兽族 rawcode 常量，40 行）：

    constant integer CATAPULT           = 'ocat'
    constant integer WITCH_DOCTOR       = 'odoc'
    constant integer GRUNT              = 'ogru'
    constant integer HEAD_HUNTER        = 'ohun'
    constant integer KODO_BEAST         = 'okod'
    constant integer PEON               = 'opeo'
    constant integer RAIDER             = 'orai'
    constant integer SHAMAN             = 'oshm'
    constant integer TAUREN             = 'otau'
    constant integer WYVERN             = 'owyv'
    constant integer BATRIDER           = 'otbr'
    constant integer SPIRIT_WALKER      = 'ospw'
    constant integer ORC_ALTAR          = 'oalt'
    constant integer ORC_BARRACKS       = 'obar'
    constant integer BESTIARY           = 'obea'
    constant integer FORGE              = 'ofor'
    constant integer FORTRESS           = 'ofrt'
    constant integer GREAT_HALL         = 'ogre'
    constant integer LODGE              = 'osld'
    constant integer STRONGHOLD         = 'ostr'
    constant integer BURROW             = 'otrb'
    constant integer TOTEM              = 'otto'
    constant integer VOODOO_LOUNGE      = 'ovln'
    constant integer UPG_ORC_MELEE      = 'Rome'
    constant integer UPG_ORC_RANGED     = 'Rora'
    constant integer UPG_ORC_ARMOR      = 'Roar'
    constant integer UPG_ORC_WAR_DRUMS  = 'Rwdm'
    constant integer UPG_ORC_PILLAGE    = 'Ropg'
    constant integer UPG_ORC_BERSERK    = 'Robs'
    constant integer UPG_ORC_PULVERIZE  = 'Rows'
    constant integer UPG_ORC_ENSNARE    = 'Roen'
    constant integer UPG_ORC_VENOM      = 'Rovs'
    constant integer UPG_ORC_DOCS       = 'Rowd'
    constant integer UPG_ORC_SHAMAN     = 'Rost'
    constant integer UPG_ORC_SPIKES     = 'Rosp'
    constant integer UPG_ORC_BURROWS    = 'Rorb'
    constant integer UPG_ORC_REGEN      = 'Rotr'
    constant integer UPG_ORC_FIRE       = 'Rolf'
    constant integer UPG_ORC_SWALKER    = 'Rowt'
    constant integer UPG_ORC_BERSERKER  = 'Robk'
*/
