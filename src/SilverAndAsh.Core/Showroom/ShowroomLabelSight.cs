using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Showroom;

/// <summary>
/// Which showroom labels the eye can see: near enough, and no wall, closed door, floor or ceiling in between. Without it,
/// the labels of the other sections would shine through the walls.
/// </summary>
public static class ShowroomLabelSight
{
    /// <summary>Labels further away than this are left out, so the picture does not fill with text.</summary>
    public const float MaxDistance = 16f;

    // Steps along the line of sight, well below a cell (2 m), so no wall corner is skipped.
    private const float Step = 0.25f;

    /// <summary>True if <paramref name="label"/> is within <see cref="MaxDistance"/> of <paramref name="eye"/> and nothing solid of <paramref name="level"/> stands between them.</summary>
    public static bool Sees(Level level, Vector3 eye, Vector3 label)
    {
        ArgumentNullException.ThrowIfNull(level);
        var way = label - eye;
        var length = way.Length();
        if (length > MaxDistance)
        {
            return false;
        }

        var steps = (int)(length / Step);
        for (var i = 1; i < steps; i++)
        {
            var point = eye + (way * (i * Step / length));
            var (x, z) = Level.ToGrid(point.X, point.Z);
            var cell = level[x, z];
            if (cell.IsSolid || point.Y < cell.FloorHeight || point.Y > cell.CeilingHeight)
            {
                return false;
            }
        }

        return true;
    }
}
