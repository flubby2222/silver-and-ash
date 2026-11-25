using System.Numerics;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The frost of Grave Chill on an enemy (issue #775), as variant A of the effects trial piece draws it (issue #616,
/// design/proben/grafik/statuseffekte-616/effekte.py, <c>grave_chill_a</c>; chosen on 2026-11-23): the body tinted 40 %
/// toward the cold, a few rime flecks on it and a round disc of rime on the floor below it - a light rim, inside a
/// dot pattern of two darker steps. Every colour a step of the ramp "Stoff" of the main palette (never violet: that
/// belongs to Leech; never red). The trial piece has no course in time: the frost is there as long as the chill and
/// gone with its last tick. The effect colours are not lit by the lantern, only covered by the fog, as in the trial piece.
/// Where flecks and disc lie (issue #785): the flecks only where the eye sees body (<see cref="FrostCore"/>), the disc on
/// the floors below it, cut at walls and at floors higher than the enemy's (<see cref="FrostDisc"/>).
/// </summary>
public static class FrostLook
{
    /// <summary>Stoff 7, "Reif hell": the rim of the disc and the flecks (also the colour of the timer).</summary>
    public static (byte R, byte G, byte B) Rime { get; } = (168, 181, 199);

    /// <summary>Stoff 6, the cold: the tint of the body and half of the dots inside the disc.</summary>
    public static (byte R, byte G, byte B) Cold { get; } = (126, 145, 178);

    /// <summary>Stoff 5, the dark rime: the other half of the dots inside the disc.</summary>
    public static (byte R, byte G, byte B) DarkRime { get; } = (86, 107, 154);

    /// <summary>The body blended 40 % toward the cold.</summary>
    public static StatusTint Tint { get; } = new(Cold, 0.4f);

    /// <summary>
    /// Radius of the disc on the floor below a ghoul, in metres, as in the trial piece (which shows a ghoul). Other
    /// enemies get a disc in the same proportion to their body: see <see cref="DiscRadiusFor"/>.
    /// </summary>
    public const float DiscRadius = 0.62f;

    /// <summary>
    /// Radius of the disc below an enemy whose body (for walking and bumping) has <paramref name="bodyRadius"/>, in
    /// metres: <see cref="DiscRadius"/> times the body's share of a ghoul's (decision of the lead on PR #783 - at
    /// 0.62 m the disc vanished between Gorm's legs). A ghoul keeps exactly 0.62 m.
    /// </summary>
    public static float DiscRadiusFor(float bodyRadius) => DiscRadius * (bodyRadius / BodySizes.Ghoul.Radius);

    /// <summary>
    /// Where the light rim begins, as a part of <see cref="DiscRadius"/>: the trial piece draws the rim where the
    /// squared distance from the middle passes 0.72 of the squared radius.
    /// </summary>
    public static float RimStart { get; } = MathF.Sqrt(0.72f);

    /// <summary>Inside the rim, this share of the dot pattern's pixels takes <see cref="Cold"/>, the rest <see cref="DarkRime"/>.</summary>
    public const float ColdShare = 0.5f;

    /// <summary>Edge of one rime fleck on the body, in metres (3 cm: one pixel at 10 m, three at 4 m).</summary>
    public const float FleckSize = 0.03f;

    /// <summary>How many flecks, as in the trial piece.</summary>
    public const int FleckCount = 14;

    /// <summary>Corners of the disc (issue #775): round at 4 m, about 50 pixels across.</summary>
    public const int DiscSegments = 32;

    // A fixed seed of our own. The trial piece also used 7, but it picked pixels of its silhouette with numpy's
    // generator; System.Random gives other numbers, so the pattern is not the trial piece's - only its count and size are.
    private const int FleckSeed = 7;

    // How far across its rod a fleck may sit (part of the rod's radius), so its 3 cm stay on the body at the rod's edge.
    private const float MostAcross = 0.8f;

    /// <summary>
    /// The flecks, scattered once with a fixed seed (issue #785: not the trial piece's pattern, see above). The same for
    /// every enemy and every frame, so a photo of the same tick looks the same; nothing of the game's randomness is used.
    /// Read-only: nobody can move a fleck for everyone else.
    /// </summary>
    public static IReadOnlyList<Fleck> Flecks { get; } = Array.AsReadOnly(Scatter());

    /// <summary>
    /// Where <paramref name="fleck"/> is drawn on the enemy of <paramref name="readout"/> seen from <paramref name="eye"/>
    /// (issue #785): on one of its rods of body (<see cref="EnemyStatusReadout.Cores"/>, or <see cref="FrostCore.Middle"/>
    /// without any), on the side of the rod facing the eye, then moved along the line of sight towards the eye - by the
    /// body's radius, at most half the way - so the model does not cover it. Seen from the eye it stays exactly over
    /// the rod: it never stands beside the figure, however far below or above the eye the enemy is.
    /// </summary>
    public static Vector3 FleckAt(Fleck fleck, EnemyStatusReadout readout, Vector3 eye)
    {
        ArgumentNullException.ThrowIfNull(readout);
        var cores = readout.Cores.Count > 0 ? readout.Cores : [FrostCore.Middle(readout.Feet, readout.Height, readout.Radius)];
        var core = Pick(cores, fleck.Pick);
        var middle = Vector3.Lerp(core.From, core.To, fleck.Along);

        var toEye = Direction(eye - middle, Vector3.UnitZ);
        var across = Direction(Vector3.Cross(Vector3.UnitY, toEye), Vector3.UnitX);
        var onRod = middle
            + (across * (fleck.Across * core.Radius))
            + (toEye * (MathF.Sqrt(1f - (fleck.Across * fleck.Across)) * core.Radius));

        var sight = eye - onRod;
        var lift = MathF.Min(readout.Radius, sight.Length() / 2f);
        return onRod + (Direction(sight, toEye) * lift);
    }

    // The rod a fleck sits on: by pick (0..1) along the rods, each as large a share as it is seen from the side.
    private static FrostCore Pick(IReadOnlyList<FrostCore> cores, float pick)
    {
        static float Seen(FrostCore core) => (Vector3.Distance(core.From, core.To) + (2f * core.Radius)) * core.Radius;
        var left = pick * cores.Sum(Seen);
        foreach (var core in cores)
        {
            left -= Seen(core);
            if (left < 0f)
            {
                return core;
            }
        }

        return cores[^1];
    }

    private static Vector3 Direction(Vector3 vector, Vector3 otherwise) =>
        vector.LengthSquared() > 1e-8f ? Vector3.Normalize(vector) : otherwise;

    private static Fleck[] Scatter()
    {
        var random = new Random(FleckSeed);
        var flecks = new Fleck[FleckCount];
        for (var i = 0; i < flecks.Length; i++)
        {
            var across = ((float)random.NextDouble() * 2f - 1f) * MostAcross;
            var along = (float)random.NextDouble();
            var pick = (float)random.NextDouble();
            flecks[i] = new Fleck(across, along, pick);
        }

        return flecks;
    }
}

/// <summary>
/// One rime fleck (issue #785): where on a rod of the body it sits - <paramref name="Across"/> the rod as seen (-1 left
/// .. 1 right, parts of its radius), <paramref name="Along"/> it (0 its lower end .. 1 its upper end) - and which rod
/// (<paramref name="Pick"/>, 0..1, each rod as large a share as it is seen).
/// </summary>
public readonly record struct Fleck(float Across, float Along, float Pick);
