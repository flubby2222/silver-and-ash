using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Showroom;

/// <summary>
/// The breaking column of the showroom (issue #601) in a loop: it stands at Z2 for a while, breaks, falls, lies with its
/// dust settling, and stands again. Pure presentation - the column is only a picture there.
/// </summary>
public static class ShowroomColumnCycle
{
    /// <summary>Ticks the column stands at Z2 before it breaks (1 s).</summary>
    public const int StandTicks = 60;

    /// <summary>Ticks it lies broken after its dust has settled (1 s).</summary>
    public const int RestTicks = 60;

    /// <summary>Ticks of one whole loop: standing, the fall with its dust, lying.</summary>
    public static int Length => StandTicks + ColumnDust.Ticks + RestTicks;

    /// <summary>How the column looks after <paramref name="worldTicks"/> ticks of the world, turned by <paramref name="yaw"/>.</summary>
    public static ColumnLook Look(long worldTicks, float yaw)
    {
        var tick = (int)(((worldTicks % Length) + Length) % Length);
        if (tick < StandTicks)
        {
            return new ColumnLook(ColumnStage.Hit2, yaw);
        }

        var since = tick - StandTicks;
        return new ColumnLook(ColumnBreakLook.StageAfterBreak(since), yaw, since);
    }
}
