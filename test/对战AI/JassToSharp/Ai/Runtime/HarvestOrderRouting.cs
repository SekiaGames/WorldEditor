//重构情况：从 Open-Warcraft3_Wiki JassGameNatives.Harvest 的 HarvestGold/HarvestWood 下单路径转换（852018 接单路由）
using OpenWC3;

namespace OpenWC3.JassToSharp.Ai.Runtime;

public static class HarvestOrderRouting
{
    public const int OrderHarvest = 852018;

    public static Action<Unit, Unit> Handler;
}
