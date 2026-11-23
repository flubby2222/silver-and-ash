using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Presentation;

/// <summary>What a free-standing column looks like (issue #601): one model per stage.</summary>
public enum ColumnStage
{
    /// <summary>Not struck yet (Z0).</summary>
    Intact,

    /// <summary>Struck, more than one blow left (Z1): a small break-out on the side of the blows.</summary>
    Hit1,

    /// <summary>One blow left (Z2): a deep wedge, the upper part leaning.</summary>
    Hit2,

    /// <summary>Broken, its pieces still falling (the animation, <see cref="ColumnBreakLook.SwapTick"/> ticks).</summary>
    Falling,

    /// <summary>Broken, the pieces lying (Z3): a stump with rubble.</summary>
    Broken,
}

/// <summary>
/// How a column looks at one moment: its <paramref name="Stage"/>, its turn about the vertical in radians
/// (<paramref name="Yaw"/>, see <see cref="ColumnBreakLook.YawToward"/>) and, once broken, the ticks since the break.
/// </summary>
public readonly record struct ColumnLook(ColumnStage Stage, float Yaw, int? TicksSinceBreak = null)
{
    /// <summary>A column nobody has struck.</summary>
    public static ColumnLook Intact => new(ColumnStage.Intact, 0f);
}

/// <summary>
/// The breaking column (issue #601, probe #594): the models of its stages, assets/models/column_*.glb, built by
/// tools/models/column_break.py, and the fall between Z2 and Z3 baked from the probe's simulation
/// (<see cref="ColumnFallTable"/>). Pure presentation: when a column takes a blow or breaks, and that the stump gives no
/// cover and blocks nothing, decides Core's game logic (<see cref="Gameplay.ColumnWear"/>).
/// <para>
/// File space: origin in the middle of the column's foot on the floor, y up, the column 6 m high. The side the blows
/// came from (<see cref="HitSide"/>) lies 20 degrees from +x towards +z; the upper part falls roughly along +x.
/// </para>
/// </summary>
public static class ColumnBreakLook
{
    /// <summary>The height the models are built for: a room's ceiling, in metres.</summary>
    public const float ModelHeight = RoomTemplate.RoomCeilingHeight;

    /// <summary>The tick after the break at which the falling model gives way to the broken one (1.35 s).</summary>
    public const int SwapTick = ColumnFallTable.SwapTick;

    /// <summary>The part of the falling model that does not move: stump and the rubble of Z2.</summary>
    public const string FallBase = "base";

    // The side of the blows in the file: 20 degrees from +x towards +z (THETA in the script).
    private static readonly float HitAngle = 20f * MathF.PI / 180f;

    private static readonly Dictionary<ColumnStage, string> Files = new()
    {
        [ColumnStage.Intact] = "column_intact",
        [ColumnStage.Hit1] = "column_hit1",
        [ColumnStage.Hit2] = "column_hit2",
        [ColumnStage.Falling] = "column_falling",
        [ColumnStage.Broken] = "column_broken",
    };

    private static readonly RigPart[] FallParts = BuildFallParts();

    /// <summary>The side of the column the blows came from, in file space (horizontal, length 1).</summary>
    public static Vector3 HitSide => new(MathF.Cos(HitAngle), 0f, MathF.Sin(HitAngle));

    /// <summary>The file of <paramref name="stage"/>, e.g. column_hit1.glb.</summary>
    public static string FileName(ColumnStage stage) => Name(stage) + ".glb";

    /// <summary>
    /// The parts of <paramref name="stage"/>'s model in the order of the rig: a standing or lying column is one fixed part
    /// named as its file; the falling one is <see cref="FallBase"/> and then the pieces of <see cref="ColumnFallTable"/>.
    /// </summary>
    public static IReadOnlyList<RigPart> Parts(ColumnStage stage) =>
        stage == ColumnStage.Falling ? FallParts : [new RigPart(Name(stage), null)];

    /// <summary>
    /// The stage of a standing column that has taken <paramref name="hits"/> of the <paramref name="hitsToBreak"/> blows
    /// it bears: none intact, the last one before the break Z2, any earlier one Z1.
    /// </summary>
    public static ColumnStage StageOf(int hits, int hitsToBreak) => hits switch
    {
        <= 0 => ColumnStage.Intact,
        _ when hits >= hitsToBreak - 1 => ColumnStage.Hit2,
        _ => ColumnStage.Hit1,
    };

    /// <summary>The stage of a broken column <paramref name="ticksSinceBreak"/> ticks after its break.</summary>
    public static ColumnStage StageAfterBreak(int ticksSinceBreak) =>
        ticksSinceBreak < SwapTick ? ColumnStage.Falling : ColumnStage.Broken;

    /// <summary>
    /// The turn about the vertical (radians, as <see cref="Matrix4x4.CreateRotationY(float)"/>) that puts the side of the
    /// blows (<see cref="HitSide"/>) of a column standing at <paramref name="column"/> towards <paramref name="striker"/>.
    /// Straight above or below it: no turn.
    /// </summary>
    public static float YawToward(Vector2 column, Vector3 striker)
    {
        var towards = new Vector2(striker.X - column.X, striker.Z - column.Y);
        if (towards.LengthSquared() < 0.0001f)
        {
            return 0f;
        }

        // CreateRotationY(a) turns the direction at angle b = atan2(-z, x) to b + a.
        return MathF.Atan2(-towards.Y, towards.X) + HitAngle;
    }

    /// <summary>
    /// The whole column: file space -> world. Its foot at <paramref name="foot"/>, turned by <paramref name="yaw"/>, and
    /// stretched in height from <see cref="ModelHeight"/> to <paramref name="clearHeight"/> (floor to ceiling of its
    /// cell) - under a room's ceiling not at all.
    /// </summary>
    public static Matrix4x4 Figure(Vector3 foot, float clearHeight, float yaw) =>
        Matrix4x4.CreateScale(1f, clearHeight / ModelHeight, 1f) * Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(foot);

    /// <summary>
    /// The turns of the falling model's parts (order of <see cref="Parts"/> for <see cref="ColumnStage.Falling"/>)
    /// <paramref name="ticksSinceBreak"/> ticks after the break, for <see cref="Rig.Pose"/> with the pivots of the file:
    /// the base stays, each piece turns and moves as in the simulation. Before the break the first tick, after the
    /// swap the last one.
    /// </summary>
    public static void FallTurns(int ticksSinceBreak, Span<Matrix4x4> turns)
    {
        if (turns.Length != FallParts.Length)
        {
            throw new ArgumentException($"The falling column has {FallParts.Length} parts.", nameof(turns));
        }

        var tick = Math.Clamp(ticksSinceBreak, 0, SwapTick - 1);
        var pieces = ColumnFallTable.Parts.Count;
        var poses = ColumnFallTable.Poses.Slice(tick * pieces * 7, pieces * 7);
        turns[0] = Matrix4x4.Identity;
        for (var i = 0; i < pieces; i++)
        {
            var pose = poses.Slice(i * 7, 7);
            var pivot = PiecePivot(i);
            var middle = new Vector3(pose[4], pose[5], pose[6]);

            // A corner c of the piece (relative to its pivot) lies at turn(c) + middle; the rig adds the pivot itself.
            turns[i + 1] = Matrix4x4.CreateFromQuaternion(new Quaternion(pose[0], pose[1], pose[2], pose[3]))
                * Matrix4x4.CreateTranslation(middle - pivot);
        }
    }

    /// <summary>Where the middle of piece <paramref name="piece"/> (order of <see cref="ColumnFallTable.Parts"/>) is in file space at a tick of the fall.</summary>
    public static Vector3 PieceMiddle(int piece, int ticksSinceBreak)
    {
        var pieces = ColumnFallTable.Parts.Count;
        ArgumentOutOfRangeException.ThrowIfNegative(piece);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(piece, pieces);
        var tick = Math.Clamp(ticksSinceBreak, 0, SwapTick - 1);
        var pose = ColumnFallTable.Poses.Slice((tick * pieces * 7) + (piece * 7), 7);
        return new Vector3(pose[4], pose[5], pose[6]);
    }

    /// <summary>The point piece <paramref name="piece"/> (order of <see cref="FallPieces"/>) turns about, in file space - as the file has it.</summary>
    internal static Vector3 PiecePivot(int piece)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(piece);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(piece, ColumnFallTable.Parts.Count);
        var pivots = ColumnFallTable.Pivots;
        return new Vector3(pivots[piece * 3], pivots[(piece * 3) + 1], pivots[(piece * 3) + 2]);
    }

    /// <summary>The names of the falling pieces, in the order of the table.</summary>
    public static IReadOnlyList<string> FallPieces => [.. FallParts.Skip(1).Select(part => part.Name)];

    private static string Name(ColumnStage stage) =>
        Files.TryGetValue(stage, out var name) ? name : throw new ArgumentOutOfRangeException(nameof(stage), stage, null);

    private static RigPart[] BuildFallParts()
    {
        var parts = new List<RigPart> { new(FallBase, null) };
        foreach (var name in ColumnFallTable.Parts)
        {
            parts.Add(new RigPart(name, null));
        }

        return [.. parts];
    }
}
