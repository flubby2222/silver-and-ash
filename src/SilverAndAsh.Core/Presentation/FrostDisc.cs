using System.Numerics;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Presentation;

/// <summary>One flat triangle of the frost disc, counter-clockwise seen from above (the side the graphics card draws).</summary>
public readonly record struct DiscTriangle(Vector3 A, Vector3 B, Vector3 C);

/// <summary>
/// The disc of rime below a chilled enemy, as triangles (issue #775) - the inside with the dot pattern and the light rim -
/// laid onto the floors of the level (issue #785): every part of it lies on the floor of the cell it is over, so at a
/// step it neither floats over the lower floor nor sinks into the higher one. Cells that are walls, and floors higher
/// than the one the enemy stands on or flies above (<see cref="FloorBelow"/>), cut it off: the disc lies on what is
/// below the enemy, never on a step in front of it.
/// </summary>
public sealed record FrostDisc(IReadOnlyList<DiscTriangle> Inside, IReadOnlyList<DiscTriangle> Rim)
{
    // A floor counts as level with another this close to it (heights of cells are set in centimetres).
    private const float SameHeight = 1e-3f;

    /// <summary>
    /// The disc of <paramref name="radius"/> round <paramref name="centre"/> (its height does not count), laid onto the
    /// floors of <paramref name="level"/> not higher than <paramref name="floor"/>. On one floor, all inside one height,
    /// the triangles are the whole disc of <see cref="FrostLook.DiscSegments"/> corners, unchanged.
    /// </summary>
    public static FrostDisc Of(Level level, Vector3 centre, float radius, float floor)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        var inner = radius * FrostLook.RimStart;
        var flat = centre with { Y = 0f };
        var inside = new List<DiscTriangle>();
        var rim = new List<DiscTriangle>();
        for (var segment = 0; segment < FrostLook.DiscSegments; segment++)
        {
            Lay(level, floor, inside, flat, Around(flat, inner, segment + 1), Around(flat, inner, segment));
            Lay(level, floor, rim, Around(flat, radius, segment), Around(flat, inner, segment), Around(flat, radius, segment + 1));
            Lay(level, floor, rim, Around(flat, inner, segment), Around(flat, inner, segment + 1), Around(flat, radius, segment + 1));
        }

        return new FrostDisc(inside, rim);
    }

    /// <summary>
    /// The floor below <paramref name="body"/> (issue #785): the highest floor of a cell its circle covers that is not a
    /// wall and not higher than its feet - for an enemy that walks the floor it stands on (also at a step, where its
    /// middle may be over the lower cell), for a bat the floor it flies above.
    /// </summary>
    public static float FloorBelow(Level level, CylinderBody body)
    {
        ArgumentNullException.ThrowIfNull(level);
        var floor = float.NegativeInfinity;
        foreach (var (x, z) in CellsUnder(body.Position, body.Radius))
        {
            var cell = level[x, z];
            if (!cell.IsSolid && cell.FloorHeight <= body.Position.Y + SameHeight
                && Touches(body.Position, body.Radius, x, z))
            {
                floor = MathF.Max(floor, cell.FloorHeight);
            }
        }

        // Inside a wall (pushed there for a moment): the cell it is in.
        return float.IsNegativeInfinity(floor) ? level.CellAt(body.Position).FloorHeight : floor;
    }

    private static Vector3 Around(Vector3 centre, float radius, int segment)
    {
        var angle = segment * MathF.Tau / FrostLook.DiscSegments;
        return centre + new Vector3(MathF.Cos(angle) * radius, 0f, MathF.Sin(angle) * radius);
    }

    // One triangle of the disc: whole when every cell under it carries the disc at one height, else cut cell by cell.
    private static void Lay(Level level, float floor, List<DiscTriangle> into, Vector3 a, Vector3 b, Vector3 c)
    {
        var cells = CellsUnder(a, b, c).ToList();
        var heights = cells.Select(cell => Carries(level, floor, cell)).ToList();
        if (heights.All(height => height is not null) && heights.Distinct().Count() == 1)
        {
            var y = heights[0]!.Value;
            into.Add(new DiscTriangle(a with { Y = y }, b with { Y = y }, c with { Y = y }));
            return;
        }

        for (var i = 0; i < cells.Count; i++)
        {
            if (heights[i] is { } height)
            {
                Fan(Clip([a, b, c], cells[i]), height, into);
            }
        }
    }

    // The height at which a cell carries the disc: its floor, if it is no wall and not higher than the enemy's floor.
    private static float? Carries(Level level, float floor, (int X, int Z) at)
    {
        var cell = level[at.X, at.Z];
        return cell.IsSolid || cell.FloorHeight > floor + SameHeight ? null : cell.FloorHeight;
    }

    private static void Fan(List<Vector3> polygon, float height, List<DiscTriangle> into)
    {
        for (var i = 1; i + 1 < polygon.Count; i++)
        {
            // A corner of the cell only touched (a line, no area): nothing to draw.
            var area = Vector3.Cross(polygon[i] - polygon[0], polygon[i + 1] - polygon[0]).Y;
            if (MathF.Abs(area) < 1e-8f)
            {
                continue;
            }

            into.Add(new DiscTriangle(polygon[0] with { Y = height }, polygon[i] with { Y = height }, polygon[i + 1] with { Y = height }));
        }
    }

    // The part of a convex polygon (level, Y ignored) over one cell: cut by its four edges in turn (Sutherland-Hodgman).
    // Cutting keeps the order of the corners, so the triangles still face up.
    private static List<Vector3> Clip(List<Vector3> polygon, (int X, int Z) cell)
    {
        var low = new Vector2(cell.X * Level.CellSize, cell.Z * Level.CellSize);
        var high = low + new Vector2(Level.CellSize);
        polygon = ClipBy(polygon, p => p.X - low.X);
        polygon = ClipBy(polygon, p => high.X - p.X);
        polygon = ClipBy(polygon, p => p.Z - low.Y);
        return ClipBy(polygon, p => high.Y - p.Z);
    }

    // Keeps what lies where inside(p) >= 0.
    private static List<Vector3> ClipBy(List<Vector3> polygon, Func<Vector3, float> inside)
    {
        var kept = new List<Vector3>();
        for (var i = 0; i < polygon.Count; i++)
        {
            var from = polygon[i];
            var to = polygon[(i + 1) % polygon.Count];
            var (fromIn, toIn) = (inside(from), inside(to));
            if (fromIn >= 0f)
            {
                kept.Add(from);
            }

            if ((fromIn >= 0f) != (toIn >= 0f))
            {
                kept.Add(Vector3.Lerp(from, to, fromIn / (fromIn - toIn)));
            }
        }

        return kept;
    }

    private static IEnumerable<(int X, int Z)> CellsUnder(params Vector3[] corners)
    {
        var (lowX, lowZ) = Level.ToGrid(corners.Min(p => p.X), corners.Min(p => p.Z));
        var (highX, highZ) = Level.ToGrid(corners.Max(p => p.X), corners.Max(p => p.Z));
        for (var x = lowX; x <= highX; x++)
        {
            for (var z = lowZ; z <= highZ; z++)
            {
                yield return (x, z);
            }
        }
    }

    private static IEnumerable<(int X, int Z)> CellsUnder(Vector3 middle, float radius) =>
        CellsUnder(middle - new Vector3(radius, 0f, radius), middle + new Vector3(radius, 0f, radius));

    // True if the circle reaches into the cell (not only its corner box).
    private static bool Touches(Vector3 middle, float radius, int x, int z)
    {
        var nearestX = Math.Clamp(middle.X, x * Level.CellSize, (x + 1) * Level.CellSize);
        var nearestZ = Math.Clamp(middle.Z, z * Level.CellSize, (z + 1) * Level.CellSize);
        var dx = middle.X - nearestX;
        var dz = middle.Z - nearestZ;
        return (dx * dx) + (dz * dz) < radius * radius;
    }
}
