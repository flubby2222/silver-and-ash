using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Physics;

/// <summary>Can one point "see" another, or is a wall, floor or ceiling in between?</summary>
public static class LineOfSight
{
    /// <summary>Distance between two checked points on the line, in metres. Much thinner than any wall.</summary>
    public const float StepLength = 0.05f;

    /// <summary>
    /// True if the straight line from <paramref name="from"/> to <paramref name="to"/> does not pass through
    /// a wall, below a floor or above a ceiling. The line is checked in small steps of <see cref="StepLength"/>.
    /// </summary>
    public static bool IsClear(Level level, Vector3 from, Vector3 to)
    {
        ArgumentNullException.ThrowIfNull(level);
        var steps = Math.Max(1, (int)MathF.Ceiling(Vector3.Distance(from, to) / StepLength));
        for (var step = 1; step <= steps; step++)
        {
            if (IsInsideGeometry(level, Vector3.Lerp(from, to, (float)step / steps)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True if <paramref name="point"/> is inside a wall, below the floor or above the ceiling.</summary>
    public static bool IsInsideGeometry(Level level, Vector3 point)
    {
        ArgumentNullException.ThrowIfNull(level);
        var cell = level.CellAt(point);
        return cell.IsSolid || point.Y < cell.FloorHeight || point.Y > cell.CeilingHeight;
    }
}
