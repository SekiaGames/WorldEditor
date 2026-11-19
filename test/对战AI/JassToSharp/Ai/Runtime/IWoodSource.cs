//重构情况：从 Open-Warcraft3_Wiki JassGameNatives.Ai.Queries 的 FindTree（可砍伐目标查找）与 JassGameNatives.Harvest 的树采集转换，抽象为接口由场景实现
using OpenWC3;

namespace OpenWC3.JassToSharp.Ai.Runtime;

public interface IWoodSource
{
    bool TryNearestTree(Vec2 from, out int treeId, out Vec2 position);

    Vec2 PositionOf(int treeId);

    bool IsAlive(int treeId);

    float RadiusOf(int treeId);

    int Chop(int treeId, int amount);
}
