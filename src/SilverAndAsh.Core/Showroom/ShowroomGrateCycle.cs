using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Showroom;

/// <summary>
/// The loop of the showroom's portcullises (issue #540): open for <see cref="HoldTicks"/>, the fall, shut for
/// <see cref="HoldTicks"/>, the lift - and again, counted from the world's first tick. Every portcullis of the showroom
/// moves in step. Only the showroom loops; in the game an arena will start each motion itself (part 2).
/// </summary>
public static class ShowroomGrateCycle
{
    /// <summary>Ticks the grate rests open and rests shut, 1.5 s each.</summary>
    public const int HoldTicks = 90;

    /// <summary>Ticks of one whole loop.</summary>
    public static int Length(GratesBalance timing)
    {
        ArgumentNullException.ThrowIfNull(timing);
        return (2 * HoldTicks) + timing.FallTicks + timing.LiftTicks;
    }

    /// <summary>The height of the spike tips (<see cref="GratePose.Height"/>) after <paramref name="worldTicks"/> ticks of the world.</summary>
    public static float Height(long worldTicks, GratesBalance timing)
    {
        var tick = (int)(((worldTicks % Length(timing)) + Length(timing)) % Length(timing));
        if (tick < HoldTicks)
        {
            return GratePose.OpenHeight;
        }

        tick -= HoldTicks;
        if (tick < timing.FallTicks)
        {
            return GratePose.Height(tick, GrateMotion.Fall, timing);
        }

        tick -= timing.FallTicks;
        if (tick < HoldTicks)
        {
            return 0f;
        }

        return GratePose.Height(tick - HoldTicks, GrateMotion.Lift, timing);
    }
}
