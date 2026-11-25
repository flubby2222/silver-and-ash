using System.Numerics;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The frost of Grave Chill on an enemy (issue #775), as variant A of the effects trial piece draws it (issue #616,
/// design/proben/grafik/statuseffekte-616/effekte.py, <c>grave_chill_a</c>; chosen on 2026-11-23): the body tinted 40 %
/// toward the cold, a few rime flecks on it and a round disc of rime on the floor below it - a light rim, inside a
/// dot pattern of two darker steps. Every colour a step of the ramp "Stoff" of the main palette (never violet: that
/// belongs to Leech; never red). The trial piece has no course in time: the frost is there as long as the chill and
/// gone with its last tick. The effect colours are not lit by the lantern, only covered by the fog, as in the trial piece.
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

    /// <summary>How many flecks, and the seed that scatters them - both as in the trial piece.</summary>
    public const int FleckCount = 14;

    private const int FleckSeed = 7;

    // Where the flecks may sit on the side of the body facing the eye: across from the middle (parts of the radius) and
    // up from the feet (parts of the height), so that they stay on the body as seen and do not stand beside it.
    private const float MostAcross = 0.6f;
    private const float LowestUp = 0.15f;
    private const float HighestUp = 0.75f;

    /// <summary>
    /// The flecks, scattered once with the seed of the trial piece: across from the middle (-1 left .. 1 right, as parts of
    /// the radius) and up from the feet (as parts of the height). The same for every enemy and every frame, so a photo
    /// of the same tick looks the same; nothing of the game's randomness is used.
    /// </summary>
    public static IReadOnlyList<(float Across, float Up)> Flecks { get; } = Scatter();

    /// <summary>
    /// Where <paramref name="fleck"/> sits on the enemy of <paramref name="readout"/> seen from <paramref name="eye"/>: on
    /// the side of its body facing the eye, its radius in front of its middle.
    /// </summary>
    public static Vector3 FleckAt((float Across, float Up) fleck, EnemyStatusReadout readout, Vector3 eye)
    {
        ArgumentNullException.ThrowIfNull(readout);
        var toEye = (eye - readout.Feet) with { Y = 0f };
        toEye = toEye.LengthSquared() > 1e-6f ? Vector3.Normalize(toEye) : Vector3.UnitZ;
        var across = new Vector3(-toEye.Z, 0f, toEye.X);
        return readout.Feet
            + (toEye * readout.Radius)
            + (across * (fleck.Across * readout.Radius))
            + new Vector3(0f, fleck.Up * readout.Height, 0f);
    }

    private static (float Across, float Up)[] Scatter()
    {
        var random = new Random(FleckSeed);
        var flecks = new (float Across, float Up)[FleckCount];
        for (var i = 0; i < flecks.Length; i++)
        {
            var across = ((float)random.NextDouble() * 2f - 1f) * MostAcross;
            var up = LowestUp + ((float)random.NextDouble() * (HighestUp - LowestUp));
            flecks[i] = (across, up);
        }

        return flecks;
    }
}
