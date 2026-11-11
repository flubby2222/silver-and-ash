using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.Gameplay.JumpTestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class PerfectJumpTests
{
    private const float Tolerance = 0.01f;

    [Fact]
    public void A_normal_jump_gives_no_bonus()
    {
        var player = RunningPlayer();

        player.Tick(ForwardJump);
        RunTicks(player, Forward, 20);

        Assert.False(player.HasPerfectJumpBonus);
        Assert.Equal(NormalAirSpeed, HorizontalSpeed(player), Tolerance);
    }

    [Fact]
    public void Horizontal_speed_ignores_falling_and_rising()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);                          // now moving up fast, too

        Assert.NotEqual(0f, player.Velocity.Y);
        Assert.Equal(HorizontalSpeed(player), player.HorizontalSpeed, Tolerance);
    }

    [Fact]
    public void Pressing_shortly_before_landing_jumps_again_with_the_bonus()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        RunTicks(player, Forward, AirTicks - Window / 2);   // just before landing

        player.Tick(ForwardJump);                          // press in the air - remembered
        RunTicks(player, Forward, Window);                 // land ... and jump off again by itself
        Assert.False(player.IsGrounded, "The buffered jump should have happened right after landing.");
        Assert.True(player.HasPerfectJumpBonus);

        RunTicks(player, Forward, 20);
        Assert.Equal(BonusAirSpeed, HorizontalSpeed(player), Tolerance);
    }

    [Fact]
    public void Pressing_shortly_after_landing_also_counts_as_perfect()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        RunTicks(player, Forward, AirTicks + Window / 2);  // landed a moment ago
        Assert.True(player.IsGrounded);

        player.Tick(ForwardJump);

        Assert.True(player.HasPerfectJumpBonus);
    }

    [Fact]
    public void Pressing_too_early_in_the_air_does_nothing()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        RunTicks(player, Forward, AirTicks - 3 * Window);  // far too early

        player.Tick(ForwardJump);
        RunTicks(player, Forward, 3 * Window + 5);

        Assert.True(player.IsGrounded, "A press far before landing must not be remembered.");
        Assert.False(player.HasPerfectJumpBonus);
    }

    [Fact]
    public void Pressing_too_late_after_landing_is_a_normal_jump()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        RunTicks(player, Forward, AirTicks + 3 * Window);

        player.Tick(ForwardJump);

        Assert.False(player.HasPerfectJumpBonus);
    }

    [Fact]
    public void The_bonus_does_not_stack_over_several_perfect_jumps()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);

        for (var hop = 0; hop < 4; hop++)
        {
            RunTicks(player, Forward, AirTicks - Window / 2);
            player.Tick(ForwardJump);                       // perfect again and again
        }

        RunTicks(player, Forward, 20);
        Assert.True(player.HasPerfectJumpBonus);
        Assert.Equal(BonusAirSpeed, HorizontalSpeed(player), Tolerance);
    }

    [Fact]
    public void A_normal_landing_ends_the_bonus()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        RunTicks(player, Forward, AirTicks - Window / 2);
        player.Tick(ForwardJump);                           // perfect jump
        RunTicks(player, Forward, Window);
        Assert.True(player.HasPerfectJumpBonus);

        RunTicks(player, Forward, AirTicks + 2 * Window);    // land without pressing

        Assert.True(player.IsGrounded);
        Assert.False(player.HasPerfectJumpBonus);
    }

    [Fact]
    public void A_perfect_landing_nods_less()
    {
        var normal = RunningPlayer();
        normal.Tick(ForwardJump);
        RunTicks(normal, Forward, AirTicks);
        var normalDip = DeepestDip(normal);

        var perfect = RunningPlayer();
        perfect.Tick(ForwardJump);
        RunTicks(perfect, Forward, AirTicks - Window / 2);
        perfect.Tick(ForwardJump);
        RunTicks(perfect, Forward, Window / 2 + 1);
        var perfectDip = DeepestDip(perfect);

        Assert.True(perfectDip < normalDip * 0.6f, $"Perfect {perfectDip:0.000} m vs. normal {normalDip:0.000} m.");
    }

    [Fact]
    public void A_perfect_press_after_landing_does_not_stack_the_bonus()
    {
        // Found by the code review: bonus jump -> land -> press shortly AFTER landing.
        // The speed must never exceed the bonus speed (it once reached about +44 %).
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        RunTicks(player, Forward, AirTicks - Window / 2);
        player.Tick(ForwardJump);                           // press in the air ...
        RunUntilLanded(player);                             // ... landing -> buffered perfect jump with bonus
        RunUntilLanded(player);                             // that jump's landing is a NORMAL one ...
        player.Tick(Forward);                               // ... one tick on the ground

        var fastest = 0f;
        player.Tick(ForwardJump);                           // perfect press after landing
        for (var i = 0; i < AirTicks; i++)
        {
            fastest = MathF.Max(fastest, HorizontalSpeed(player));
            player.Tick(Forward);
        }

        Assert.True(fastest <= BonusAirSpeed + Tolerance, $"Fastest speed {fastest:0.00} m/s, bonus speed {BonusAirSpeed:0.00} m/s.");
    }

    [Fact]
    public void A_buffered_perfect_landing_nods_exactly_by_the_dip_factor()
    {
        // Found by the code review: the dip was softened twice (0.4 x 0.4).
        var normal = RunningPlayer();
        normal.Tick(ForwardJump);
        RunTicks(normal, Forward, AirTicks - 1);
        var normalDip = DeepestDip(normal);

        var perfect = RunningPlayer();
        perfect.Tick(ForwardJump);
        RunTicks(perfect, Forward, AirTicks - Window / 2);
        perfect.Tick(ForwardJump);
        RunTicks(perfect, Forward, Window / 2 - 1);
        var perfectDip = DeepestDip(perfect);

        Assert.InRange(perfectDip / normalDip, Tuning.Player.PerfectJumpDipFactor - 0.08f, Tuning.Player.PerfectJumpDipFactor + 0.08f);
    }

    [Fact]
    public void Stepping_down_a_small_step_is_no_landing_for_the_perfect_window()
    {
        // Found by the code review: a tiny drop counted as a landing and opened the perfect window.
        var player = new Player(LevelParser.Parse("P\n1\n.\n.\n.\n.\n."));
        RunTicks(player, Forward, 30);                      // walk onto the step ...
        RunUntilLanded(player, Forward);                    // ... and drop off it (0.25 m)

        player.Tick(ForwardJump);                           // jump right after touching down

        Assert.False(player.HasPerfectJumpBonus);
    }
}
