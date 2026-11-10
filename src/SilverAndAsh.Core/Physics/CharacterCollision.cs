using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Physics;

/// <summary>
/// Moves an upright capsule (a character) through a level without passing through walls.
/// Walls are full-height cells, so from above the capsule is a circle and every wall is a square -
/// the whole problem becomes "circle against squares" on the ground plane.
/// <para>
/// The two steps of any collision code:
/// <b>detection</b> (does the circle overlap a wall?) and
/// <b>resolution</b> (push it out, and remove the part of the velocity that points into the wall,
/// so the character slides along instead of sticking).
/// </para>
/// </summary>
public static class CharacterCollision
{
    /// <summary>A move is split into steps of at most this fraction of the radius, so fast movement cannot skip walls.</summary>
    private const float MaxStepFraction = 0.5f;

    /// <summary>How often overlaps are resolved per step. Corners need more than one pass.</summary>
    private const int ResolveIterations = 8;

    /// <summary>Upper limit for steps per move, so absurd inputs cannot freeze the game.</summary>
    private const int MaxSteps = 1000;

    /// <summary>Distances below this are treated as zero (avoids dividing by zero).</summary>
    private const float Epsilon = 1e-5f;

    /// <summary>
    /// Moves <paramref name="position"/> by <paramref name="velocity"/> for <paramref name="seconds"/> and resolves
    /// collisions with solid cells. Height (Y) is not touched. The velocity that comes back has its "into the wall"
    /// part removed, which makes the character slide along walls.
    /// </summary>
    public static (Vector3 Position, Vector3 Velocity) Move(
        Level level, Vector3 position, Vector3 velocity, float radius, float seconds)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);
        if (!float.IsFinite(velocity.X) || !float.IsFinite(velocity.Z) || !float.IsFinite(seconds))
        {
            throw new ArgumentException("Velocity and time must be finite numbers.");
        }

        var horizontalDistance = new Vector2(velocity.X, velocity.Z).Length() * seconds;
        var steps = Math.Clamp((int)MathF.Ceiling(horizontalDistance / (radius * MaxStepFraction)), 1, MaxSteps);
        var stepSeconds = seconds / steps;

        for (var step = 0; step < steps; step++)
        {
            position += new Vector3(velocity.X, 0f, velocity.Z) * stepSeconds;
            (position, velocity) = ResolveOverlaps(level, position, velocity, radius);
        }

        return (position, velocity);
    }

    /// <summary>True if a circle at <paramref name="position"/> overlaps any solid cell.</summary>
    public static bool Overlaps(Level level, Vector3 position, float radius)
    {
        foreach (var (x, z) in CellsAround(position, radius))
        {
            if (level[x, z].IsSolid && PenetrationDepth(position, radius, x, z, out _) > Epsilon)
            {
                return true;
            }
        }

        return false;
    }

    private static (Vector3 Position, Vector3 Velocity) ResolveOverlaps(
        Level level, Vector3 position, Vector3 velocity, float radius)
    {
        for (var iteration = 0; iteration < ResolveIterations; iteration++)
        {
            var pushedOut = false;

            foreach (var (x, z) in CellsAround(position, radius))
            {
                if (!level[x, z].IsSolid)
                {
                    continue;
                }

                var depth = PenetrationDepth(position, radius, x, z, out var normal);
                if (depth <= 0f)
                {
                    continue;
                }

                // Resolution, part 1: push the circle out of the wall along the wall's normal.
                position += new Vector3(normal.X, 0f, normal.Y) * depth;

                // Resolution, part 2: remove the velocity component that points into the wall.
                // What remains points along the wall - that is the "sliding".
                var horizontal = new Vector2(velocity.X, velocity.Z);
                var intoWall = Vector2.Dot(horizontal, normal);
                if (intoWall < 0f)
                {
                    horizontal -= normal * intoWall;
                    velocity = new Vector3(horizontal.X, velocity.Y, horizontal.Y);
                }

                pushedOut = true;
            }

            if (!pushedOut)
            {
                break;
            }
        }

        return (position, velocity);
    }

    /// <summary>
    /// How deep the circle reaches into the square of cell (x, z), and in which direction (the normal)
    /// it has to be pushed to get out. Returns 0 or less if there is no overlap.
    /// </summary>
    private static float PenetrationDepth(Vector3 position, float radius, int x, int z, out Vector2 normal)
    {
        var minX = x * Level.CellSize;
        var minZ = z * Level.CellSize;
        var maxX = minX + Level.CellSize;
        var maxZ = minZ + Level.CellSize;
        var centre = new Vector2(position.X, position.Z);

        // Detection: the point of the square that is closest to the circle's centre.
        var closest = new Vector2(Math.Clamp(centre.X, minX, maxX), Math.Clamp(centre.Y, minZ, maxZ));
        var offset = centre - closest;
        var distance = offset.Length();

        if (distance > Epsilon)
        {
            normal = offset / distance;
            return radius - distance;
        }

        // The centre is inside the square (should only happen in odd cases): leave by the nearest side.
        var toLeft = centre.X - minX;
        var toRight = maxX - centre.X;
        var toBottom = centre.Y - minZ;
        var toTop = maxZ - centre.Y;
        var nearest = MathF.Min(MathF.Min(toLeft, toRight), MathF.Min(toBottom, toTop));

        normal = nearest == toLeft ? -Vector2.UnitX
            : nearest == toRight ? Vector2.UnitX
            : nearest == toBottom ? -Vector2.UnitY
            : Vector2.UnitY;
        return nearest + radius;
    }

    /// <summary>All grid cells that a circle at <paramref name="position"/> could touch.</summary>
    private static IEnumerable<(int X, int Z)> CellsAround(Vector3 position, float radius)
    {
        var (minX, minZ) = Level.ToGrid(position.X - radius, position.Z - radius);
        var (maxX, maxZ) = Level.ToGrid(position.X + radius, position.Z + radius);

        for (var z = minZ; z <= maxZ; z++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                yield return (x, z);
            }
        }
    }
}
