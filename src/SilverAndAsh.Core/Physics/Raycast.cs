using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Physics;

/// <summary>Where a ray hit something: distance from the start, the point, and the surface's outward direction.</summary>
public readonly record struct RayHit(float Distance, Vector3 Point, Vector3 Normal);

/// <summary>
/// A ray ("Strahl"): a straight line from a start point in one direction - for the pistol's instant hit.
/// </summary>
public static class Raycast
{
    /// <summary>Distance between two checked points before the exact edge is searched, in metres.</summary>
    public const float StepLength = 0.1f;

    /// <summary>
    /// First wall, floor or ceiling the ray from <paramref name="origin"/> in <paramref name="direction"/> (length 1)
    /// hits within <paramref name="maxDistance"/>, or null.
    /// </summary>
    public static RayHit? AgainstLevel(Level level, Vector3 origin, Vector3 direction, float maxDistance)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (LineOfSight.IsInsideGeometry(level, origin))
        {
            return new RayHit(0f, origin, -direction);
        }

        // 1. Walk along the ray in steps until a point lies inside the geometry ...
        var free = 0f;
        while (free < maxDistance)
        {
            var next = MathF.Min(free + StepLength, maxDistance);
            if (LineOfSight.IsInsideGeometry(level, origin + direction * next))
            {
                // 2. ... then halve the gap between the last free and the first blocked point again and again
                //    ("bisection") - 30 halvings make it far finer than a millimetre.
                var blocked = next;
                for (var i = 0; i < 30; i++)
                {
                    var middle = (free + blocked) / 2f;
                    if (LineOfSight.IsInsideGeometry(level, origin + direction * middle))
                    {
                        blocked = middle;
                    }
                    else
                    {
                        free = middle;
                    }
                }

                var freePoint = origin + direction * free;
                return new RayHit(free, freePoint, SurfaceNormal(level, freePoint, origin + direction * blocked, direction));
            }

            free = next;
        }

        return null;
    }

    /// <summary>
    /// Which surface was crossed between a free point and a blocked point right next to it: a wall or the side of a
    /// platform if the ray went into another cell, otherwise the floor or the ceiling of the same cell.
    /// </summary>
    private static Vector3 SurfaceNormal(Level level, Vector3 free, Vector3 blocked, Vector3 direction)
    {
        var (freeX, freeZ) = Level.ToGrid(free.X, free.Z);
        var (blockedX, blockedZ) = Level.ToGrid(blocked.X, blocked.Z);
        if (freeX != blockedX)
        {
            return new Vector3(-MathF.Sign(direction.X), 0f, 0f);
        }

        if (freeZ != blockedZ)
        {
            return new Vector3(0f, 0f, -MathF.Sign(direction.Z));
        }

        return blocked.Y < level[blockedX, blockedZ].FloorHeight ? Vector3.UnitY : -Vector3.UnitY;
    }

    /// <summary>
    /// Distance along the ray to the first point of the upright cylinder <paramref name="body"/> (side or top/bottom),
    /// or null if the ray misses it or the body is behind the start.
    /// </summary>
    public static float? AgainstCylinder(Vector3 origin, Vector3 direction, CylinderBody body)
    {
        float? nearest = null;
        var bottom = body.Position.Y;
        var top = body.Position.Y + body.Height;

        // Side: seen from above the cylinder is a circle - solve "point on the ray lies on the circle"
        // (a quadratic equation a*t^2 + b*t + c = 0, the smaller solution is where the ray enters).
        var offset = new Vector2(origin.X - body.Position.X, origin.Z - body.Position.Z);
        var flat = new Vector2(direction.X, direction.Z);
        var a = Vector2.Dot(flat, flat);
        if (a > 1e-8f)
        {
            var b = 2f * Vector2.Dot(offset, flat);
            var c = Vector2.Dot(offset, offset) - body.Radius * body.Radius;
            var discriminant = b * b - 4f * a * c;
            if (discriminant >= 0f)
            {
                var t = (-b - MathF.Sqrt(discriminant)) / (2f * a);
                var y = origin.Y + direction.Y * t;
                if (t >= 0f && y >= bottom && y <= top)
                {
                    nearest = t;
                }
            }
        }

        // Top and bottom: where the ray crosses that height, is it inside the circle?
        if (MathF.Abs(direction.Y) > 1e-8f)
        {
            foreach (var capY in (ReadOnlySpan<float>)[top, bottom])
            {
                var t = (capY - origin.Y) / direction.Y;
                var point = origin + direction * t;
                var fromAxis = new Vector2(point.X - body.Position.X, point.Z - body.Position.Z);
                if (t >= 0f && fromAxis.LengthSquared() <= body.Radius * body.Radius && (nearest is null || t < nearest))
                {
                    nearest = t;
                }
            }
        }

        return nearest;
    }
}
