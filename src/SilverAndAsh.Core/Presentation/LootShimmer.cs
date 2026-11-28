using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The shimmer of loot with a tier (issue #825, rule 31 of the art bible; the accepted trial piece is round 4 of issue
/// #819, design/proben/grafik/throwing-knives-beute-819/runde-4/schimmer4.py). A piece floats freely, without a plate,
/// and shows its tier in three parts, all in the colour of the tier:
/// <list type="bullet">
/// <item>A, the flash: the piece itself lights up and dims in <see cref="FlashSteps"/> steps.</item>
/// <item>B, the floor glow: a dot pattern on the floor below it, denser in the middle, pulsing more slowly; its
/// strength has a factor per tier (<see cref="GlowFactor"/>), measured on the trial piece.</item>
/// <item>C, the sparks: <see cref="SparkCount"/> small boxes rising round the piece.</item>
/// </list>
/// Every number of the look stands here, in one place; the game only draws what this class works out. Each piece has
/// its own place in the pulse (<see cref="Phase"/>), so two pieces side by side do not blink in step.
/// </summary>
public static class LootShimmer
{
    /// <summary>Seconds for one pulse of the flash (A) and for the way of one spark (C).</summary>
    public const float FlashPeriod = 1.73f;

    /// <summary>How many steps the flash has between dark and bright (the first trial had 4: too coarse).</summary>
    public const int FlashSteps = 8;

    /// <summary>How far a model's surface is blended toward the colour of the tier at the darkest step (0..1).</summary>
    public const float FlashLow = 0.20f;

    /// <summary>How far a model's surface is blended toward the colour of the tier at the brightest step (0..1).</summary>
    public const float FlashHigh = 0.70f;

    /// <summary>How bright a box in the colour of the tier is at the darkest step (1 = the full colour at the brightest).</summary>
    public const float BoxLow = 0.60f;

    /// <summary>Seconds for one pulse of the floor glow (B), of the bobbing and for half a turn of the piece.</summary>
    public const float GlowPeriod = 2.6f;

    /// <summary>The share of the dot pattern in the middle of the glow at its weakest, in sixteenths (the pattern has 16 thresholds).</summary>
    public const int GlowLowSixteenths = 4;

    /// <summary>The share of the dot pattern in the middle of the glow at its strongest, in sixteenths, before the factor of the tier.</summary>
    public const int GlowHighSixteenths = 12;

    /// <summary>The largest share the glow ever has: one threshold of the pattern always stays free, so it never becomes a plate.</summary>
    public const float GlowShareLimit = 15f / 16f;

    /// <summary>The glow lies this far above the floor, in metres - above the frost disc (0.015 m), so the two never fight for a pixel.</summary>
    public const float GlowLift = 0.020f;

    /// <summary>
    /// Pieces lying close together get their glows at slightly different heights, in metres per step of
    /// <see cref="GlowLiftSteps"/>: where two glows overlap, one of them always lies clearly above (no flicker).
    /// </summary>
    public const float GlowLiftStep = 0.001f;

    /// <summary>How many different heights the glows take, by the id of the piece.</summary>
    public const int GlowLiftSteps = 4;

    /// <summary>How many sparks a piece has; about <see cref="SparkLife"/> of them show at a time.</summary>
    public const int SparkCount = 9;

    /// <summary>The share of its way a spark shows (0..1); the rest of the time it is gone.</summary>
    public const float SparkLife = 0.75f;

    /// <summary>Edge of a spark in the lower half of its way, in metres.</summary>
    public const float SparkEdge = 0.03f;

    /// <summary>Edge of a spark in the upper half of its way, in metres: it shrinks before it goes out.</summary>
    public const float SparkEdgeHigh = 0.02f;

    /// <summary>Height above the floor at which a spark begins, in metres.</summary>
    public const float SparkStart = 0.06f;

    /// <summary>How far a spark would rise in a whole period, in metres (it goes out after <see cref="SparkLife"/> of it).</summary>
    public const float SparkRise = 0.75f;

    /// <summary>Radius of the innermost ring of sparks round the piece, in metres.</summary>
    public const float SparkRadius = 0.20f;

    /// <summary>Each of the five rings of sparks lies this much farther out than the one before, in metres.</summary>
    public const float SparkRadiusStep = 0.02f;

    /// <summary>Degrees from one spark to the next round the piece (the golden angle: they never line up).</summary>
    public const float SparkAngleStep = 137.5f;

    /// <summary>Degrees a spark turns round the piece on a whole period of its way.</summary>
    public const float SparkTwist = 90f;

    /// <summary>A piece bobs this far up and down round its height, in metres.</summary>
    public const float Bob = 0.04f;

    /// <summary>Seconds for one full turn of a piece.</summary>
    public const float TurnPeriod = 2f * GlowPeriod;

    /// <summary>The middle of a piece shown as a box (until it has a model of its own) floats this high above the floor, in metres (without the bobbing).</summary>
    public const float BoxHeight = 0.2f;

    /// <summary>The lowest point of the throwing knives floats this high above the floor, in metres (without the bobbing).</summary>
    public const float KnivesHeight = 0.12f;

    /// <summary>The three knives of the loot "Dreistich" (form a2) lean this far outward from the upright, in degrees; their tips meet at the bottom.</summary>
    public const float KnivesLeanDegrees = 24f;

    /// <summary>How much larger than in the world's hand a knife of the loot is (the trial piece: 2.5), so it can be seen from 8 m.</summary>
    public const float KnivesScale = 2.5f;

    /// <summary>
    /// The rings of the floor glow, from the middle outward: the radius in metres up to which each reaches and the part
    /// of the middle's share its dot pattern has.
    /// </summary>
    public static IReadOnlyList<GlowRing> Rings { get; } =
    [
        new(0.16f, 1.0f),
        new(0.27f, 0.6f),
        new(0.36f, 0.3f),
    ];

    // The factor of each tier for the floor glow: measured on the trial piece so that all four read equally strong on
    // the floor (gold is bright by itself and gets the least).
    private static readonly IReadOnlyDictionary<ItemTier, float> GlowFactors = new Dictionary<ItemTier, float>
    {
        [ItemTier.Worn] = 1.25f,
        [ItemTier.Forged] = 0.98f,
        [ItemTier.Royal] = 0.40f,
        [ItemTier.Vampiric] = 0.76f,
    };

    /// <summary>The factor of <paramref name="tier"/> for the strength of its floor glow.</summary>
    public static float GlowFactor(ItemTier tier) =>
        GlowFactors.TryGetValue(tier, out var factor)
            ? factor
            : throw new ArgumentOutOfRangeException(nameof(tier), tier, "This tier has no factor for its floor glow.");

    /// <summary>
    /// The place of the piece with <paramref name="id"/> in every pulse, as a share of a period (0..1): pieces with
    /// neighbouring ids lie about a sixth of a period apart.
    /// </summary>
    public static float Phase(int id)
    {
        var turns = id / MathF.Tau;
        return turns - MathF.Floor(turns);
    }

    /// <summary>
    /// The flash (A) of piece <paramref name="id"/> at <paramref name="seconds"/> of the world: 0 (darkest) to 1
    /// (brightest) in <see cref="FlashSteps"/> even steps, up and down once every <see cref="FlashPeriod"/>.
    /// </summary>
    public static float FlashLevel(double seconds, int id)
    {
        var steps = FlashSteps - 1;
        return MathF.Round(Wave(seconds, FlashPeriod, Phase(id)) * steps) / steps;
    }

    /// <summary>How far a model's surface is blended toward the colour of its tier at flash <paramref name="level"/> (0..1).</summary>
    public static float FlashAmount(float level) => FlashLow + ((FlashHigh - FlashLow) * level);

    /// <summary>How bright a box in the colour of its tier is at flash <paramref name="level"/> (0..1): never darker than <see cref="BoxLow"/>.</summary>
    public static float BoxBrightness(float level) => BoxLow + ((1f - BoxLow) * level);

    /// <summary>
    /// The share of the dot pattern in the middle of the floor glow (B) of piece <paramref name="id"/> of
    /// <paramref name="tier"/> at <paramref name="seconds"/> of the world (0..1): between
    /// <see cref="GlowLowSixteenths"/> and <see cref="GlowHighSixteenths"/> sixteenths, in whole sixteenths, times the
    /// factor of the tier - never above <see cref="GlowShareLimit"/>. A ring farther out has its part of it
    /// (<see cref="GlowRing.Part"/>).
    /// </summary>
    public static float GlowShare(ItemTier tier, double seconds, int id)
    {
        var sixteenths = GlowLowSixteenths
            + MathF.Round(Wave(seconds, GlowPeriod, Phase(id)) * (GlowHighSixteenths - GlowLowSixteenths));
        return MathF.Min(sixteenths / 16f * GlowFactor(tier), GlowShareLimit);
    }

    /// <summary>The height above the floor at which the glow of piece <paramref name="id"/> lies, in metres.</summary>
    public static float GlowLiftOf(int id) => GlowLift + (GlowLiftStep * (((id % GlowLiftSteps) + GlowLiftSteps) % GlowLiftSteps));

    /// <summary>
    /// The floor glow round <paramref name="centre"/> as discs of triangles, one per ring of <see cref="Rings"/>, laid
    /// onto the floors of <paramref name="level"/> not higher than <paramref name="floor"/> and cut off at walls (like
    /// the frost disc, <see cref="FrostDisc.Of"/>). Every disc is whole, the larger ones reach below the smaller ones:
    /// the thresholds of the dot pattern lie inside one another, so drawn in one colour at one height the middle shows
    /// exactly the share of the innermost ring.
    /// </summary>
    public static IReadOnlyList<GlowDisc> GlowDiscs(Level level, Vector3 centre, float floor)
    {
        ArgumentNullException.ThrowIfNull(level);
        var discs = new GlowDisc[Rings.Count];
        for (var ring = 0; ring < Rings.Count; ring++)
        {
            var disc = FrostDisc.Of(level, centre, Rings[ring].Radius, floor);
            discs[ring] = new GlowDisc(Rings[ring].Part, [.. disc.Inside, .. disc.Rim]);
        }

        return discs;
    }

    /// <summary>
    /// The sparks (C) of piece <paramref name="id"/> that show at <paramref name="seconds"/> of the world, each with its
    /// middle relative to the point on the floor below the piece and its edge. A spark rises from
    /// <see cref="SparkStart"/> turning round the piece, shrinks half way up and goes out after <see cref="SparkLife"/>
    /// of its way; the sparks follow one another evenly.
    /// </summary>
    public static IReadOnlyList<LootSpark> Sparks(double seconds, int id)
    {
        var sparks = new List<LootSpark>(SparkCount);
        var turns = (seconds / FlashPeriod) + Phase(id);
        for (var spark = 0; spark < SparkCount; spark++)
        {
            var way = Fraction(turns + ((double)spark / SparkCount));
            if (way >= SparkLife)
            {
                continue;
            }

            var angle = ((SparkAngleStep * spark) + (SparkTwist * way)) * MathF.PI / 180f;
            var radius = SparkRadius + (SparkRadiusStep * (spark * 7 % 5));
            var at = new Vector3(MathF.Cos(angle) * radius, SparkStart + (SparkRise * way), MathF.Sin(angle) * radius);
            sparks.Add(new LootSpark(at, way < SparkLife / 2f ? SparkEdge : SparkEdgeHigh));
        }

        return sparks;
    }

    /// <summary>How far above (or below) its height piece <paramref name="id"/> floats at <paramref name="seconds"/> of the world, in metres.</summary>
    public static float BobOf(double seconds, int id) =>
        Bob * MathF.Sin(MathF.Tau * Fraction((seconds / GlowPeriod) + Phase(id)));

    /// <summary>How far piece <paramref name="id"/> has turned round the upright at <paramref name="seconds"/> of the world, in radians.</summary>
    public static float TurnOf(double seconds, int id) => MathF.Tau * Fraction((seconds / TurnPeriod) + Phase(id));

    /// <summary>
    /// Where knife <paramref name="index"/> (0..2) of the throwing knives lying as loot stands in the piece (form a2
    /// "Dreistich"): the piece's origin is its lowest point, where the three tips meet; each knife leans
    /// <see cref="KnivesLeanDegrees"/> outward, grip up, the three evenly round the upright. For the knife's file
    /// (<see cref="ThrowingKnivesPose.KnifeFile"/>: origin at the base of the blade, tip to +z, <see cref="ThrowingKnivesPose.FileScale"/> times its size).
    /// </summary>
    public static Matrix4x4 KnifeInPiece(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, ThrowingKnivesPose.Count);
        return Matrix4x4.CreateScale(KnivesScale / ThrowingKnivesPose.FileScale)
            * Matrix4x4.CreateRotationX(MathF.PI / 2f)
            * Matrix4x4.CreateTranslation(0f, ThrowingKnivesPose.BladeLength * KnivesScale, 0f)
            * Matrix4x4.CreateRotationZ(-KnivesLeanDegrees * MathF.PI / 180f)
            * Matrix4x4.CreateRotationY(index * MathF.Tau / ThrowingKnivesPose.Count);
    }

    // 0 at the start of a period, 1 in its middle, 0 again at its end - a soft swing; `phase` shifts it by a share of the period.
    private static float Wave(double seconds, float period, float phase) =>
        0.5f - (0.5f * MathF.Cos(MathF.Tau * Fraction((seconds / period) + phase)));

    private static float Fraction(double value) => (float)(value - Math.Floor(value));
}

/// <summary>One ring of the floor glow (issue #825): it reaches out to <paramref name="Radius"/> metres and its dot pattern has <paramref name="Part"/> of the share in the middle.</summary>
public readonly record struct GlowRing(float Radius, float Part);

/// <summary>One disc of the floor glow laid onto the floor (issue #825): its triangles and the part of the middle's share its dot pattern has.</summary>
public sealed record GlowDisc(float Part, IReadOnlyList<DiscTriangle> Triangles);

/// <summary>One spark of a piece of loot (issue #825): its middle relative to the point on the floor below the piece, and its edge in metres.</summary>
public readonly record struct LootSpark(Vector3 Offset, float Edge);
