using System.Numerics;

namespace SilverAndAsh.Core.Presentation;

/// <summary>One stone of the shock wave's ring: which part of <see cref="ShockwaveLook.FileName"/> and where it goes.</summary>
/// <param name="Part">Index in <see cref="ShockwaveLook.Parts"/>.</param>
/// <param name="Frame">Corner of the part's file (relative to its pivot, which is 0) -> world.</param>
public readonly record struct ShockwavePiece(int Part, Matrix4x4 Frame);

/// <summary>
/// How the shock wave of Gorm's hammer looks (issue #299, the designer's version A "breaking stone"): along its edge the
/// floor breaks open - groups of three stone plates thrown up and outwards (<see cref="Shard"/>), their outer faces and a
/// low broken lip at their foot in the freshly broken, palest stone, a dark furrow behind them, and behind that two rows
/// of dust (<see cref="Puff"/>), lower towards the inside. No red and no glow (issue #314): the danger is shown by the
/// stone's brightness and shape alone. Model assets/models/shock_wave.glb, built by tools/models/shock_wave.py.
/// <para>
/// The wave hits whoever its growing disc reaches (flat distance to its middle up to <c>Radius</c>, see
/// <see cref="Gameplay.Gorm.Shockwave"/>). So the stones sit inside that disc, their outermost point - the front of the
/// broken lip - exactly on its edge: what you see coming is where you are hit, and what has not reached your feet yet has not hit you.
/// Pure presentation; nothing here changes the wave.
/// </para>
/// </summary>
public static class ShockwaveLook
{
    /// <summary>The model file in assets/models.</summary>
    public const string FileName = "shock_wave.glb";

    /// <summary>The group of three stone plates with the broken lip in front and the furrow behind, 0.6 m along the circle.</summary>
    public const string Shard = "shard";

    /// <summary>A little heap of dust.</summary>
    public const string Puff = "puff";

    /// <summary>
    /// How far the shard reaches outwards from its origin (+x in its file): the front of its broken lip. The
    /// shards are set this much inside the wave's radius. A test checks it against the file.
    /// </summary>
    public const float ShardReach = 0.2f;

    /// <summary>The shard group's length along the circle in its file; it is stretched to the arc of one piece.</summary>
    public const float ShardLength = 0.6f;

    /// <summary>The smallest ring drawn (metres): right after the blow the radius is still close to 0.</summary>
    public const float MinRadius = 0.3f;

    // A shard is this much (and the hash below up to this much again) as high as in its file.
    private const float LowestShard = 0.75f;
    private const float ShardSpread = 0.5f;

    // Each piece of the ring: the shard on the edge, then two heaps of dust behind it (offset inwards, height).
    private static readonly (float Back, float Height)[] DustRows = [(-0.55f, 0.9f), (-1.1f, 0.6f)];

    private static readonly RigPart[] AllParts = [new(Shard, null), new(Puff, null)];

    /// <summary>The parts in the order of the rig; none hangs on another.</summary>
    public static IReadOnlyList<RigPart> Parts => AllParts;

    /// <summary>The visible outer edge of a wave of <paramref name="radius"/> (the ring is never drawn smaller than <see cref="MinRadius"/>).</summary>
    public static float Edge(float radius) => MathF.Max(MinRadius, radius);

    /// <summary>Pieces around the circle: as many as the old ring of boxes had, at least 12, about one per 0.63 m.</summary>
    public static int PieceCount(float radius) => Math.Max(12, (int)(Edge(radius) * 10f));

    /// <summary>
    /// Fills <paramref name="into"/> (cleared first) with the stones of a wave of <paramref name="radius"/> around
    /// <paramref name="origin"/>: for each piece of the circle one shard whose front ends on <see cref="Edge"/>, and the
    /// dust behind it (left out where it would lie closer than <see cref="MinRadius"/> to the middle). The same radius
    /// always gives the same stones; the shards' heights vary by a fixed pattern around the ring.
    /// </summary>
    public static void Place(Vector3 origin, float radius, List<ShockwavePiece> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        into.Clear();
        var edge = Edge(radius);
        var pieces = PieceCount(radius);
        var arc = 2f * MathF.PI * edge / pieces;
        for (var piece = 0; piece < pieces; piece++)
        {
            var angle = piece * 2f * MathF.PI / pieces;
            var outwards = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));

            // Local x points outwards, z along the circle (as the old ring of boxes was turned).
            var turn = Matrix4x4.CreateRotationY(-angle);
            var height = LowestShard + (ShardSpread * (((piece * 7919) % 13) / 13f));
            into.Add(new ShockwavePiece(0, Frame(Matrix4x4.CreateScale(1f, height, arc / ShardLength), turn, origin, outwards, edge - ShardReach)));

            foreach (var (back, dustHeight) in DustRows)
            {
                var at = edge - ShardReach + back;
                if (at >= MinRadius)
                {
                    into.Add(new ShockwavePiece(1, Frame(Matrix4x4.CreateScale(1f, dustHeight, 1f), turn, origin, outwards, at)));
                }
            }
        }
    }

    // Just above the floor, like the old ring, so the stones do not flicker against it.
    private static Matrix4x4 Frame(Matrix4x4 scale, Matrix4x4 turn, Vector3 origin, Vector3 outwards, float at) =>
        scale * turn * Matrix4x4.CreateTranslation(origin + (outwards * at) + new Vector3(0f, 0.02f, 0f));
}
