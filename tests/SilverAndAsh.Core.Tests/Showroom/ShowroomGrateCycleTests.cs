using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.Showroom;

namespace SilverAndAsh.Core.Tests.Showroom;

public class ShowroomGrateCycleTests
{
    private static readonly GratesBalance Timing = Balance.Default.Grates;

    private static int Hold => ShowroomGrateCycle.HoldTicks;

    [Fact]
    public void A_loop_is_open_the_fall_shut_and_the_lift()
    {
        Assert.Equal((2 * Hold) + Timing.FallTicks + Timing.LiftTicks, ShowroomGrateCycle.Length(Timing));

        for (var t = 0; t < Hold; t++)
        {
            Assert.Equal(GratePose.OpenHeight, ShowroomGrateCycle.Height(t, Timing));
        }

        for (var t = 0; t < Timing.FallTicks; t++)
        {
            Assert.Equal(GratePose.Height(t, GrateMotion.Fall, Timing), ShowroomGrateCycle.Height(Hold + t, Timing));
        }

        var shut = Hold + Timing.FallTicks;
        for (var t = 0; t < Hold; t++)
        {
            Assert.Equal(0f, ShowroomGrateCycle.Height(shut + t, Timing));
        }

        for (var t = 0; t < Timing.LiftTicks; t++)
        {
            Assert.Equal(GratePose.Height(t, GrateMotion.Lift, Timing), ShowroomGrateCycle.Height(shut + Hold + t, Timing));
        }
    }

    [Fact]
    public void The_second_loop_repeats_the_first()
    {
        var length = ShowroomGrateCycle.Length(Timing);
        for (var t = 0; t < length; t++)
        {
            Assert.Equal(ShowroomGrateCycle.Height(t, Timing), ShowroomGrateCycle.Height(length + t, Timing));
        }
    }

    [Fact]
    public void The_grate_never_jumps_where_one_phase_meets_the_next()
    {
        // From one tick to the next the grate moves at most as far as in the steepest tick of the drop.
        var steepest = 0f;
        for (var t = 1; t < Timing.FallTicks; t++)
        {
            steepest = MathF.Max(steepest, GratePose.Height(t - 1, GrateMotion.Fall, Timing) - GratePose.Height(t, GrateMotion.Fall, Timing));
        }

        var length = ShowroomGrateCycle.Length(Timing);
        for (var t = 1; t <= 2 * length; t++)
        {
            var step = MathF.Abs(ShowroomGrateCycle.Height(t, Timing) - ShowroomGrateCycle.Height(t - 1, Timing));
            Assert.True(step <= steepest + 0.0001f, $"tick {t}: a jump of {step} m");
        }
    }

    [Fact]
    public void The_showroom_s_portcullis_follows_the_world_s_ticks_and_stands_still_in_the_pause()
    {
        var world = ShowroomLayout.Build().NewWorld();
        Assert.Equal(0, world.Ticks);
        for (var t = 0; t < Hold + 20; t++)
        {
            world.Tick(PlayerInput.None);
        }

        Assert.Equal(Hold + 20, world.Ticks);
        var falling = ShowroomGrateCycle.Height(world.Ticks, world.Balance.Grates);
        Assert.Equal(GratePose.Height(20, GrateMotion.Fall, Timing), falling);

        // Paused ticks do not count: the grate hangs where it was.
        for (var t = 0; t < 30; t++)
        {
            world.Tick(PlayerInput.None with { Paused = true });
        }

        Assert.Equal(Hold + 20, world.Ticks);
        Assert.Equal(falling, ShowroomGrateCycle.Height(world.Ticks, world.Balance.Grates));
    }
}
