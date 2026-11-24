namespace SilverAndAsh.Core;

/// <summary>The two ways a portcullis moves (issue #540).</summary>
public enum GrateMotion
{
    /// <summary>From fully open down to the floor: two jerks, then the drop and a short bounce.</summary>
    Fall,

    /// <summary>From the floor up to fully open, evenly.</summary>
    Lift,
}

/// <summary>
/// The rule of a portcullis (issue #695): how high its spike tips hang above the floor a number of ticks after it began
/// to fall or rise, how long a motion lasts (<see cref="GratesBalance"/>) and when it blocks its door
/// (<see cref="GratesBalance.BlockHeight"/>). The passage of a door (<see cref="Gameplay.Door"/>) follows this, and the
/// picture (GratePose in Presentation) only reads it - a new animation of the grate is a change of the rule.
/// <para>
/// <see cref="Height"/> is a pure function of the ticks since the motion began. The shapes were judged in ticks of the
/// probe (fall 63, lift 126); other lengths from <see cref="GratesBalance"/> stretch them evenly.
/// </para>
/// </summary>
public static class GrateTravel
{
    /// <summary>
    /// The height of the spike tips above the floor when fully open, in metres: the top rail of the model
    /// (tools/models/portcullis.py) hangs far above the lintel there. A measure of the model, not a balancing value.
    /// </summary>
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

    /// <summary>
    /// Whether a grate at <paramref name="height"/> blocks its door on its way up (issue #558): until its spike tips hang
    /// above <see cref="GratesBalance.BlockHeight"/>. (A falling grate's door is barred from the tick its arena seals.)
    /// </summary>
    public static bool Blocks(float height, GratesBalance timing)
    {
        ArgumentNullException.ThrowIfNull(timing);
        return height < timing.BlockHeight;
    }

    /// <summary>The first tick of a fall at which the grate reaches the floor (47 of 63 as judged).</summary>
    public static int ShutTick(GratesBalance timing)
    {
        ArgumentNullException.ThrowIfNull(timing);
        return (int)MathF.Ceiling(ProbeShutTick * timing.FallTicks / ProbeFallTicks);
    }

    /// <summary>
    /// Where a <paramref name="motion"/> starts that begins while the grate hangs at <paramref name="height"/> (issue
    /// #558: a grate turned round on its way): the first tick at which a fall is no higher, or a lift no lower, than
    /// <paramref name="height"/>. A fall from fully open and a lift from the floor start at tick 0.
    /// </summary>
    public static int StartTick(float height, GrateMotion motion, GratesBalance timing)
    {
        ArgumentNullException.ThrowIfNull(timing);
        var length = MotionTicks(motion, timing);
        for (var tick = 0; tick < length; tick++)
        {
            var at = Height(tick, motion, timing);
            if (motion == GrateMotion.Fall ? at <= height : at >= height)
            {
                return tick;
            }
        }

        return length;
    }

    /// <summary>How many ticks <paramref name="motion"/> takes (from <see cref="GratesBalance"/>).</summary>
    public static int MotionTicks(GrateMotion motion, GratesBalance timing)
    {
        ArgumentNullException.ThrowIfNull(timing);
        return motion switch
        {
            GrateMotion.Fall => timing.FallTicks,
            GrateMotion.Lift => timing.LiftTicks,
            _ => throw new ArgumentOutOfRangeException(nameof(motion), motion, null),
        };
    }

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
