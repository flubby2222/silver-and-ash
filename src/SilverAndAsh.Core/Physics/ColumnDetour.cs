using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Physics;

/// <summary>
/// The smallest way round a free-standing column (issue #539) for an enemy walking straight at a point. The grid search
/// (<see cref="Pathfinder"/>) knows only cells; a column leaves its cell open, so an enemy heading straight through it
/// would push against its middle and stand still. Instead it heads past the column's edge - on the side away from it, or
/// always the same side if it stands exactly in line - until the way is clear. Proper way-finding round obstacles is
/// issue #535.
/// </summary>
public static class ColumnDetour
{
    /// <summary>Room in metres kept between the body and the column while going round it.</summary>
    public const float Margin = 0.1f;

    private const float Epsilon = 1e-5f;

    /// <summary>
    /// Where a body of <paramref name="bodyRadius"/> at <paramref name="from"/> should head to reach
    /// <paramref name="to"/>: <paramref name="to"/> itself if no column stands in the way (on the ground plane, widened
    /// by the body and <see cref="Margin"/>), otherwise a point as far away as <paramref name="to"/> past the edge of the
    /// nearest column in the way. The height is always that of <paramref name="to"/>.
    /// </summary>
    public static Vector3 Around(Level level, Vector3 from, Vector3 to, float bodyRadius)
    {
        ArgumentNullException.ThrowIfNull(level);
        var start = new Vector2(from.X, from.Z);
        var way = new Vector2(to.X, to.Z) - start;
        var length = way.Length();
        if (length < Epsilon || NearestInTheWay(level, start, way / length, length, bodyRadius) is not { } column)
        {
            return to;
        }

        var reach = Column.Radius + bodyRadius + Margin;
        var toCentre = column.Centre - start;
        var distance = toCentre.Length();
        var towards = distance > Epsilon ? toCentre / distance : way / length;

        // The side to pass on: away from the column's middle; exactly in line, always the same (clockwise) side.
        var side = Cross(way, toCentre) >= 0f ? -1f : 1f;

        // Outside the widened circle: along the tangent past its edge. Already in it: round it, across the radius.
        var angle = distance > reach ? MathF.Asin(reach / distance) : MathF.PI / 2f;
        var heading = Rotate(towards, side * angle);
        var point = start + (heading * length);
        return new Vector3(point.X, to.Y, point.Y);
    }

    /// <summary>
    /// True if a body of <paramref name="bodyRadius"/> could stand with its middle at <paramref name="point"/> without
    /// touching a column (widened by <see cref="Margin"/>) - a waypoint where this is false can never be reached.
    /// </summary>
    public static bool CanStandAt(Level level, Vector3 point, float bodyRadius)
    {
        ArgumentNullException.ThrowIfNull(level);
        var flat = new Vector2(point.X, point.Z);
        var reach = Column.Radius + bodyRadius + Margin;
        return !level.Columns.Any(column => Vector2.DistanceSquared(flat, column.Centre) < reach * reach);
    }

    /// <summary>
    /// Drops the waypoints at the front of <paramref name="waypoints"/> where a body of <paramref name="bodyRadius"/>
    /// cannot stand (<see cref="CanStandAt"/>: a cell's middle under a column), so it heads for the next one instead of
    /// circling the column. The last one stays - near the goal the enemy walks straight at its target.
    /// </summary>
    public static void SkipUnreachable(List<Vector3> waypoints, Level level, float bodyRadius)
    {
        ArgumentNullException.ThrowIfNull(waypoints);
        while (waypoints.Count > 1 && !CanStandAt(level, waypoints[0], bodyRadius))
        {
            waypoints.RemoveAt(0);
        }
    }

    // The column nearest along the way whose widened circle the way runs into, ahead of the start; or null.
    private static Column? NearestInTheWay(Level level, Vector2 start, Vector2 direction, float length, float bodyRadius)
    {
        var reach = Column.Radius + bodyRadius + Margin;
        Column? nearest = null;
        var nearestAlong = float.MaxValue;
        foreach (var column in level.Columns)
        {
            var along = Vector2.Dot(column.Centre - start, direction);
            if (along <= 0f)
            {
                continue;
            }

            var closest = start + (direction * MathF.Min(along, length));
            if (Vector2.DistanceSquared(closest, column.Centre) < reach * reach && along < nearestAlong)
            {
                nearest = column;
                nearestAlong = along;
            }
        }

        return nearest;
    }

    private static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);

    private static Vector2 Rotate(Vector2 v, float angle)
    {
        var (sin, cos) = MathF.SinCos(angle);
        return new Vector2((v.X * cos) - (v.Y * sin), (v.X * sin) + (v.Y * cos));
    }
}
