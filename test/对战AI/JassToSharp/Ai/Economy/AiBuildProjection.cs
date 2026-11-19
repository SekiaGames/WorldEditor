//重构情况：从 Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Jass/JassGameNatives.Ai.BuildProjection.cs 转换（骨架，方法体待补）
//重构情况：从 Open-Warcraft3_Wiki/GameCore/L3_Simulation/Simulation/Jass/JassGameNatives.Ai.BuildProjection.cs 转换（选址投影，落到 Sim 寻路图）
using OpenWC3.Pathing;

namespace OpenWC3.JassToSharp.Ai.Economy;

public static class AiBuildProjection
{
    public static bool TryProject(CProximityMap map, Unit ghost, Vec2 desired, out Vec2 resolved,
        int rings = 31)
    {
        resolved = desired;
        if (map == null || ghost == null) return false;
        return map.TryFindNearestLegalPointInclusive(ghost, desired, rings, out resolved);
    }

    public static bool IsBlocked(CProximityMap map, Unit self, Vec2 point)
        => map != null && self != null
            && map.IsFootprintBlockedInclusive(point, self.MovementType, self.CollisionSize, self);
}

/*
原文（JassGameNatives.Ai.BuildProjection.cs，31 行）：

namespace OpenWC3.Game;

public sealed partial class JassGameNatives
{
    // 8918e0 writes min(cell, value). These writes commute: the union of
    // equal-radius squares around every rectangular footprint cell is one
    // expanded rectangle. Keep the hard footprint and signed terrain values.
    internal static void StampBuildProjection(sbyte[] cells,int size,
        int baseX,int baseY,int width,int height,sbyte zone,int margin)
    {
        if(width<=0||height<=0||zone<0||zone>3)return;
        int left=baseX-width/2,top=baseY-height/2;
        int right=left+width-1,bottom=top+height-1;
        StampRectangle(left,top,right,bottom,-99);
        // A negative native radius produces empty per-cell zone loops.
        if(margin>=0)
            StampRectangle(left-margin,top-margin,right+margin,bottom+margin,zone);

        void StampRectangle(int x0,int y0,int x1,int y1,sbyte value)
        {
            x0=Math.Max(0,x0);y0=Math.Max(0,y0);
            x1=Math.Min(size-1,x1);y1=Math.Min(size-1,y1);
            for(int y=y0;y<=y1;y++)
            {
                int end=y*size+x1;
                for(int index=y*size+x0;index<=end;index++)
                    if(cells[index]>value)cells[index]=value;
            }
        }
    }
}

*/
