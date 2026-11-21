using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Presentation;

/// <summary>The two ways a portcullis moves (issue #540).</summary>
public enum GrateMotion
{
    /// <summary>From fully open down to the floor: two jerks, then the drop and a short bounce.</summary>
    Fall,

    /// <summary>From the floor up to fully open, evenly.</summary>
    Lift,
}

/// <summary>
/// The portcullis (issue #540): an iron grate in a normal or locked door frame, and how high it hangs. The models are
/// assets/models/portcullis.glb (one part, <see cref="Grate"/>) and the stone guide ring in the arch of the door plane,
/// portcullis_guide_{a,b,c}.glb (one part, <see cref="Guide"/>), both built by tools/models/portcullis.py.
/// <para>
/// File space as for the door frame: origin in the middle of the door plane on the floor (the middle of the door's cell).
/// The grate moves as a whole along y by <see cref="Height"/> - the height of its spike tips above the floor; the ring
/// stands still. Fully open (<see cref="OpenHeight"/>) the grate is not drawn; on the way the lintel hides its upper part.
/// </para>
/// <para>
/// <see cref="Height"/> is a pure function of the ticks since the motion began, so the picture and, later, the passage
/// (issue #540, part 2) use the same number. The shapes were judged in ticks of the probe (fall 63, lift 126); other
/// lengths from <see cref="GratesBalance"/> stretch them evenly.
/// </para>
/// </summary>
public static class GratePose
{
    /// <summary>The grate: bars, bands and the top rail.</summary>
    public const string Grate = "grate";

    /// <summary>The stone guide ring in the arch.</summary>
    public const string Guide = "guide";

    /// <summary>The file of the grate.</summary>
    public const string FileName = "portcullis.glb";

    /// <summary>The height of the spike tips above the floor when fully open, in metres (top rail far above the lintel).</summary>
    public const float OpenHeight = 3.40f;

    // The shapes as judged, in ticks of the probe (design/proben/grafik/fallgitter-540, branch feat/fallgitter-bewegung-probe-540).
    private const float ProbeFallTicks = 63f;
    private const float ProbeLiftTicks = 126f;

    // Fall "b - two jerks": the first jerk drops to Hang by tick 12 and wobbles out until 26, the drop reaches the floor at
    // 47, then the grate bounces twice (11 and 5 ticks).
    private const float FirstJerkEnd = 12f;
    private const float Hang = 2.55f;
    private const float WobbleTicks = 12f;
    private const float WobbleSize = 0.05f;
    private const float DropStart = 26f;
    private const float ProbeShutTick = 47f;
    private static readonly (float Start, float Length, float Peak)[] Bounces = [(47f, 11f, 0.05f), (58f, 5f, 0.009f)];

    // Lift: a short wait, then speeding up over LiftRamp ticks to an even speed that reaches the top at the last tick.
    private const float LiftWait = 6f;
    private const float LiftRamp = 12f;
    private const float LiftSpeed = OpenHeight / (ProbeLiftTicks - LiftWait - (LiftRamp / 2f));

    private static readonly RigPart[] GrateParts = [new(Grate, null)];
    private static readonly RigPart[] GuideParts = [new(Guide, null)];

    /// <summary>The parts of the grate model: the grate alone, fixed (it moves as a whole).</summary>
    public static IReadOnlyList<RigPart> Parts => GrateParts;

    /// <summary>The parts of a guide ring model: the ring alone, fixed.</summary>
    public static IReadOnlyList<RigPart> GuideRigParts => GuideParts;

    /// <summary>The file of the guide ring for <paramref name="variant"/>, e.g. portcullis_guide_b.glb.</summary>
    public static string GuideFileName(TextureVariant variant)
    {
        if (variant is not (TextureVariant.A or TextureVariant.B or TextureVariant.C))
        {
            throw new ArgumentOutOfRangeException(nameof(variant), variant, null);
        }

        return $"portcullis_guide_{variant.ToString().ToLowerInvariant()}.glb";
    }

    /// <summary>
    /// The height of the spike tips above the floor (metres, 0..<see cref="OpenHeight"/>) <paramref name="ticks"/> after
    /// a <paramref name="motion"/> began. Before the start a fall hangs fully open and a lift stands on the floor; after
    /// the end a fall stands on the floor and a lift hangs fully open.
    /// </summary>
    public static float Height(int ticks, GrateMotion motion, GratesBalance timing)
    {
        ArgumentNullException.ThrowIfNull(timing);
        return motion switch
        {
            GrateMotion.Fall => FallHeight(ticks, timing.FallTicks),
            GrateMotion.Lift => LiftHeight(ticks, timing.LiftTicks),
            _ => throw new ArgumentOutOfRangeException(nameof(motion), motion, null),
        };
    }

    /// <summary>The first tick of a fall at which the grate reaches the floor (47 of 63 as judged).</summary>
    public static int ShutTick(GratesBalance timing)
    {
        ArgumentNullException.ThrowIfNull(timing);
        return (int)MathF.Ceiling(ProbeShutTick * timing.FallTicks / ProbeFallTicks);
    }

    /// <summary>Whether a grate at <paramref name="height"/> is drawn: not when fully open.</summary>
    public static bool IsDrawn(float height) => height < OpenHeight;

    /// <summary>
    /// The guide ring to draw: the one of <paramref name="wanted"/> (the variant of the door frame) if it was loaded,
    /// else the one of variant A, else none - then the grate runs without a ring.
    /// </summary>
    public static TextureVariant? Pick(TextureVariant wanted, bool wantedLoaded, bool variantALoaded) =>
        WallPillarLook.Pick(wanted, wantedLoaded, variantALoaded);

    private static float FallHeight(int ticks, int fallTicks)
    {
        if (ticks <= 0)
        {
            return OpenHeight;
        }

        if (ticks >= fallTicks)
        {
            return 0f;
        }

        var t = ticks * ProbeFallTicks / fallTicks;
        if (t < FirstJerkEnd)
        {
            var f = t / FirstJerkEnd;
            return OpenHeight - ((OpenHeight - Hang) * f * f);
        }

        if (t < DropStart)
        {
            var s = t - FirstJerkEnd;
            return s < WobbleTicks ? Hang - (WobbleSize * MathF.Exp(-s / 4f) * MathF.Sin(2f * MathF.PI * s / 8f)) : Hang;
        }

        if (t < ProbeShutTick)
        {
            var u = (t - DropStart) / (ProbeShutTick - DropStart);
            return Hang * (1f - (u * u));
        }

        var height = 0f;
        foreach (var (start, length, peak) in Bounces)
        {
            var u = (t - start) / length;
            if (u is >= 0f and <= 1f)
            {
                height += peak * 4f * u * (1f - u);
            }
        }

        return height;
    }

    private static float LiftHeight(int ticks, int liftTicks)
    {
        if (ticks <= 0)
        {
            return 0f;
        }

        if (ticks >= liftTicks)
        {
            return OpenHeight;
        }

        var s = (ticks * ProbeLiftTicks / liftTicks) - LiftWait;
        if (s <= 0f)
        {
            return 0f;
        }

        return s < LiftRamp
            ? LiftSpeed * s * s / (2f * LiftRamp)
            : MathF.Min(OpenHeight, LiftSpeed * (s - (LiftRamp / 2f)));
    }
}
