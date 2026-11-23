using System.Numerics;

namespace SilverAndAsh.Core.World;

/// <summary>
/// One block of stone of the boss portal that stands out of its wall or fills its doorway (issue #598), in the portal's
/// own measure: <see cref="AcrossFrom"/>..<see cref="AcrossTo"/> metres from the middle of the passage to either side,
/// <see cref="AlongFrom"/>..<see cref="AlongTo"/> metres from the door plane to either side (the walls lie at 1 m),
/// <see cref="Bottom"/>..<see cref="Top"/> metres above the floor. Every block stands mirrored four times - left and
/// right of the passage, in front of and behind the door.
/// </summary>
public readonly record struct PortalBlock(float AcrossFrom, float AcrossTo, float AlongFrom, float AlongTo, float Bottom, float Top);

/// <summary>
/// The measure of the boss portal's stone (issue #568, tools/models/boss_portal.py - the numbers are those of the model
/// script): the stepped jambs (Gewände) of the three-run pointed arch, the plinth, the imposts, the buttresses and their
/// cover plates. The model and <see cref="BossPortal"/> - walking, sight, shots and way-finding - share these numbers
/// (issue #598). Only the pinnacles on the cover plates (above 5.45 m, out of everybody's reach) are left out.
/// </summary>
public static class BossPortalShape
{
    /// <summary>Half the width of the portal (and of the two door cells): it fills 2 m to either side of the passage.</summary>
    public const float HalfWidth = 2f;

    /// <summary>From the door plane to the wall face on either side - half a cell.</summary>
    public const float HalfDepth = Level.CellSize / 2f;

    /// <summary>The height of the portal: the ceiling of the boss door's cells and of the rooms it joins.</summary>
    public const float Height = 6f;

    /// <summary>Half the width of the opening at the door plane: the passage is 2 m wide there.</summary>
    public const float OpeningHalfWidth = 1f;

    /// <summary>Height of the springing: up to here the jambs stand straight, above it the arches begin.</summary>
    public const float Spring = 3.2f;

    /// <summary>
    /// All arches of one side are circles about the same point, <see cref="ArchCentre"/> metres across (on the far side
    /// of the middle, which makes the arch pointed) at the height of <see cref="Spring"/>.
    /// </summary>
    public const float ArchCentre = -1.5f;

    // The four runs (Läufe) of the stepped jambs, from the wall face inwards: each from this depth (metres from the door
    // plane) out to the one before it, with the radius of its arch. The opening of a run is its radius plus ArchCentre:
    // 1.6, 1.4, 1.2 and 1.0 m to either side.
    private static ReadOnlySpan<float> RunFrom => [0.85f, 0.6f, 0.35f, 0f];

    private static ReadOnlySpan<float> RunRadius => [3.1f, 2.9f, 2.7f, 2.5f];

    /// <summary>
    /// The blocks of stone: the four runs of each jamb (from the floor to the top, from their opening out to the side of
    /// the portal), then before each wall face the plinth, the impost, the buttress and its cover plate.
    /// </summary>
    public static IReadOnlyList<PortalBlock> Blocks { get; } =
    [
        Run(0), Run(1), Run(2), Run(3),
        new(OpeningHalfWidth, HalfWidth, HalfDepth, 1.125f, 0f, 0.5f),          // plinth (Sockel)
        new(OpeningHalfWidth, 1.72f, HalfDepth, 1.125f, Spring - 0.125f, Spring), // impost (Kämpfer)
        new(1.72f, HalfWidth, HalfDepth, 1.24f, 0.5f, 5.3f),                      // buttress (Strebepfeiler)
        new(1.66f, HalfWidth, HalfDepth, 1.3f, 5.3f, 5.45f),                      // cover plate (Deckplatte)
    ];

    /// <summary>Number of runs of each jamb.</summary>
    public static int Runs => RunFrom.Length;

    /// <summary>
    /// Half the width of the opening at <paramref name="along"/> metres from the door plane (either side): 1.6 m at the
    /// wall face, then a step of 0.2 m narrower for each run, down to 1 m at the door. Where two runs meet, the narrower.
    /// </summary>
    public static float OpeningAt(float along) => RunRadius[RunAt(along)] + ArchCentre;

    /// <summary>The depth (metres from the door plane) where run <paramref name="run"/> begins (0 = the outermost).</summary>
    public static float RunStart(int run) => RunFrom[run];

    /// <summary>The depth where run <paramref name="run"/> ends: the wall face for the outermost, else where the one before begins.</summary>
    public static float RunEnd(int run) => run == 0 ? HalfDepth : RunFrom[run - 1];

    /// <summary>The radius of the arch of run <paramref name="run"/>.</summary>
    public static float RunArchRadius(int run) => RunRadius[run];

    /// <summary>
    /// True if the point (<paramref name="across"/>, <paramref name="along"/>, <paramref name="up"/>) - metres from the
    /// middle of the passage, from the door plane and above the floor; signs do not matter - lies in the portal's stone.
    /// </summary>
    public static bool IsStone(float across, float along, float up)
    {
        across = MathF.Abs(across);
        along = MathF.Abs(along);
        if (across > HalfWidth || up < 0f || up > Height)
        {
            return false;
        }

        if (along <= HalfDepth)
        {
            return !IsOpen(across, along, up);
        }

        foreach (var block in Blocks)
        {
            if (block.AlongFrom >= HalfDepth && Holds(block, across, along, up))
            {
                return true;
            }
        }

        return false;
    }

    // Inside the doorway: open under the arch of the run at that depth - below the springing as wide as the run's opening.
    private static bool IsOpen(float across, float along, float up)
    {
        var radius = RunRadius[RunAt(along)];
        if (up < Spring)
        {
            return across < radius + ArchCentre;
        }

        var fromCentre = new Vector2(across - ArchCentre, up - Spring);
        return fromCentre.LengthSquared() < radius * radius;
    }

    /// <summary>
    /// The direction out of the stone at the underside of an arch, in the portal's measure (across, up) for a point with
    /// <paramref name="across"/> &gt;= 0: towards the common centre of the arches. Null below the springing.
    /// </summary>
    public static Vector2? ArchNormal(float across, float up)
    {
        if (up < Spring)
        {
            return null;
        }

        var towardsCentre = new Vector2(ArchCentre - across, Spring - up);
        return towardsCentre.LengthSquared() > 1e-12f ? Vector2.Normalize(towardsCentre) : null;
    }

    private static bool Holds(PortalBlock block, float across, float along, float up) =>
        across >= block.AcrossFrom && across <= block.AcrossTo && along >= block.AlongFrom && along <= block.AlongTo
        && up >= block.Bottom && up <= block.Top;

    // The run at a depth: the outermost whose start lies below it; where two meet (and at the door plane), the inner one.
    private static int RunAt(float along)
    {
        for (var run = 0; run < RunFrom.Length - 1; run++)
        {
            if (along > RunFrom[run])
            {
                return run;
            }
        }

        return RunFrom.Length - 1;
    }

    private static PortalBlock Run(int run) =>
        new(RunRadius[run] + ArchCentre, HalfWidth, RunFrom[run], run == 0 ? HalfDepth : RunFrom[run - 1], 0f, Height);
}

/// <summary>
/// The stone portal of a boss door (issue #598): the jambs left and right of the opening, the plinths and buttresses in
/// front of the walls (<see cref="BossPortalShape"/>). Its middle lies at (<paramref name="X"/>, <paramref name="Z"/>) on
/// the floor at <paramref name="Floor"/> - the middle of the two door cells - and the passage runs along Z
/// (<paramref name="PassesAlongZ"/>, a door in a north or south wall) or along X. Like a column it is part of the level:
/// walking (<see cref="Physics.CharacterCollision"/>), sight and shots (<see cref="Physics.LineOfSight.IsInsideGeometry"/>)
/// and way-finding (<see cref="Pathfinder"/>) stop at its stone, at every height; the door's cells stay open.
/// </summary>
public readonly record struct BossPortal(float X, float Z, float Floor, bool PassesAlongZ)
{
    /// <summary>The middle of the passage on the ground plane (X, Z).</summary>
    public Vector2 Middle => new(X, Z);

    /// <summary>True if <paramref name="point"/> lies in its stone.</summary>
    public bool Contains(Vector3 point)
    {
        var (across, along) = Local(point.X, point.Z);
        return BossPortalShape.IsStone(across, along, point.Y - Floor);
    }

    /// <summary>
    /// The rectangles on the ground plane (world X and Z, smallest and largest corner) of the blocks that stand in the
    /// way of a body from <paramref name="from"/> to <paramref name="to"/> metres above the floor.
    /// </summary>
    public IEnumerable<(Vector2 Min, Vector2 Max)> Footprint(float from, float to)
    {
        foreach (var block in BossPortalShape.Blocks)
        {
            if (block.Top <= from - Floor || block.Bottom >= to - Floor)
            {
                continue;
            }

            // Mirrored four times: left and right of the passage (side), in front of and behind the door (face).
            for (var side = 1f; side >= -1f; side -= 2f)
            {
                for (var face = 1f; face >= -1f; face -= 2f)
                {
                    yield return Rectangle(block, side, face);
                }
            }
        }
    }

    /// <summary>
    /// True if <paramref name="point"/> (any height) lies on the ground plan of a block that stands on the floor or
    /// reaches down below the springing - where nobody walks: the jambs, plinths and buttresses.
    /// </summary>
    public bool StandsOn(Vector2 point)
    {
        foreach (var (min, max) in Footprint(Floor, Floor + BossPortalShape.Spring))
        {
            if (point.X >= min.X && point.X <= max.X && point.Y >= min.Y && point.Y <= max.Y)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True if the middle of cell (<paramref name="x"/>, <paramref name="z"/>) lies in the doorway this portal frames
    /// (one of the door's two cells).
    /// </summary>
    public bool Frames(int x, int z)
    {
        var (across, along) = Local((x + 0.5f) * Level.CellSize, (z + 0.5f) * Level.CellSize);
        return MathF.Abs(across) < BossPortalShape.HalfWidth && MathF.Abs(along) < BossPortalShape.HalfDepth;
    }

    /// <summary>
    /// Where a way through cell (<paramref name="x"/>, <paramref name="z"/>) of the doorway goes: through the middle of
    /// the opening, at the depth of the cell's middle (the middles of the door cells lie on the faces of the jambs).
    /// </summary>
    public Vector3 PassagePoint(int x, int z)
    {
        var centre = new Vector3((x + 0.5f) * Level.CellSize, Floor, (z + 0.5f) * Level.CellSize);
        return PassesAlongZ ? centre with { X = X } : centre with { Z = Z };
    }

    /// <summary>
    /// Where across the passage (world X for a passage along Z, else world Z) a body of <paramref name="radius"/> comes
    /// to stand that the shutting door pushes out of its doorway, just past the wall face: inside the opening at the door
    /// plane. Plinth and buttress stand out of the wall into the room beside the opening - a body pushed out next to them
    /// would stand in their stone. A body as wide as the opening or wider goes to its middle.
    /// </summary>
    public float PushedAcross(float across, float radius)
    {
        var middle = PassesAlongZ ? X : Z;
        var half = MathF.Max(0f, BossPortalShape.OpeningHalfWidth - radius);
        return Math.Clamp(across, middle - half, middle + half);
    }

    /// <summary>
    /// The outward direction of the stone between a free point and a blocked point right next to it: the face of a jamb,
    /// block or plinth the line went through, or the underside of an arch. Null if <paramref name="blocked"/> is no stone
    /// of this portal.
    /// </summary>
    public Vector3? SideNormal(Vector3 free, Vector3 blocked)
    {
        if (!Contains(blocked))
        {
            return null;
        }

        var (across, along) = Local(free.X, free.Z);
        if (MathF.Abs(along) <= BossPortalShape.HalfDepth
            && BossPortalShape.ArchNormal(MathF.Abs(across), free.Y - Floor) is { } arch)
        {
            var outward = arch.X * MathF.Sign(across);
            return PassesAlongZ ? new Vector3(outward, arch.Y, 0f) : new Vector3(0f, arch.Y, outward);
        }

        if (Contains(new Vector3(blocked.X, free.Y, free.Z)))
        {
            return new Vector3(-MathF.Sign(blocked.X - free.X), 0f, 0f);
        }

        if (Contains(new Vector3(free.X, free.Y, blocked.Z)))
        {
            return new Vector3(0f, 0f, -MathF.Sign(blocked.Z - free.Z));
        }

        return new Vector3(0f, -MathF.Sign(blocked.Y - free.Y), 0f);
    }

    // Across = metres from the middle of the passage, along = metres from the door plane.
    private (float Across, float Along) Local(float worldX, float worldZ) =>
        PassesAlongZ ? (worldX - X, worldZ - Z) : (worldZ - Z, worldX - X);

    private (Vector2 Min, Vector2 Max) Rectangle(PortalBlock block, float side, float face)
    {
        var across = (Math.Min(side * block.AcrossFrom, side * block.AcrossTo), Math.Max(side * block.AcrossFrom, side * block.AcrossTo));
        var along = (Math.Min(face * block.AlongFrom, face * block.AlongTo), Math.Max(face * block.AlongFrom, face * block.AlongTo));
        return PassesAlongZ
            ? (new Vector2(X + across.Item1, Z + along.Item1), new Vector2(X + across.Item2, Z + along.Item2))
            : (new Vector2(X + along.Item1, Z + across.Item1), new Vector2(X + along.Item2, Z + across.Item2));
    }
}
