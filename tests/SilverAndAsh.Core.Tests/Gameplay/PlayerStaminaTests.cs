using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.Gameplay.JumpTestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class PlayerStaminaTests
{
    private const float Tolerance = 0.01f;

    private static readonly int RechargeTicks = SimulationClock.SecondsToTicks(Tuning.Player.StaminaRechargeTime);

    // Every jump costs one segment - the first (normal) jump of a chain, too.
    private static readonly int TimedJumpsUntilEmpty = Tuning.Player.StaminaJumps - 1;

    [Fact]
    public void A_normal_jump_costs_one_segment()
    {
        var player = RunningPlayer();

        player.Tick(ForwardJump);

        var expected = (Tuning.Player.StaminaJumps - 1f) / Tuning.Player.StaminaJumps;
        Assert.Equal(expected, player.Stamina.Fraction, Tolerance);
        Assert.False(player.HasPerfectJumpBonus);
        Assert.False(player.IsExhausted);
    }

    [Fact]
    public void A_full_bar_pays_for_a_whole_chain_of_jumps()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);

        ChainTimedJumps(player, TimedJumpsUntilEmpty);

        Assert.True(player.HasPerfectJumpBonus);
        Assert.Equal(0f, player.Stamina.Fraction);
    }

    [Fact]
    public void The_next_timed_jump_on_an_empty_bar_is_exhausted_and_slower_than_normal()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);

        ChainTimedJumps(player, TimedJumpsUntilEmpty + 1);
        RunTicks(player, Forward, 20);

        Assert.False(player.HasPerfectJumpBonus);
        Assert.True(player.IsExhausted);
        Assert.Equal(ExhaustedAirSpeed, HorizontalSpeed(player), Tolerance);
    }

    [Fact]
    public void A_normal_jump_on_an_empty_bar_is_exhausted_too()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        ChainTimedJumps(player, TimedJumpsUntilEmpty);
        RunUntilLanded(player);
        RunTicks(player, Forward, Window + 1);              // chain over - but only 1 tick of refilling

        player.Tick(ForwardJump);                           // too late for a timed jump
        RunTicks(player, Forward, 20);

        Assert.True(player.IsExhausted);
        Assert.Equal(ExhaustedAirSpeed, HorizontalSpeed(player), Tolerance);
    }

    [Fact]
    public void Standing_after_a_landing_ends_the_exhaustion()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        ChainTimedJumps(player, TimedJumpsUntilEmpty + 1);
        RunUntilLanded(player);

        RunTicks(player, Forward, Window);
        Assert.True(player.IsExhausted, "Within the window a timed press could still continue the chain.");

        player.Tick(Forward);
        Assert.False(player.IsExhausted);
    }

    [Fact]
    public void An_exhausted_press_after_landing_drops_the_speed_at_once()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        ChainTimedJumps(player, TimedJumpsUntilEmpty);
        RunUntilLanded(player);
        player.Tick(Forward);
        var before = HorizontalSpeed(player);

        player.Tick(ForwardJump);                           // timed, but the bar is empty

        Assert.True(player.IsExhausted);
        Assert.True(HorizontalSpeed(player) < before * 0.8f, $"Speed {before:0.00} -> {HorizontalSpeed(player):0.00} m/s.");
    }

    [Fact]
    public void The_bar_refills_only_while_standing()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        ChainTimedJumps(player, TimedJumpsUntilEmpty);
        RunUntilLanded(player);
        RunTicks(player, Forward, Window + 60);             // chain over, refilling on the ground
        Assert.True(player.Stamina.IsRecharging);

        player.Tick(ForwardJump);                           // a normal jump ...
        var inTheAir = player.Stamina.Fraction;
        RunTicks(player, Forward, 20);

        Assert.False(player.IsGrounded);
        Assert.False(player.Stamina.IsRecharging);
        Assert.Equal(inTheAir, player.Stamina.Fraction);    // ... no refilling in the air
    }

    [Fact]
    public void The_bar_does_not_refill_during_a_chain()
    {
        // Pressing shortly AFTER each landing: the feet touch the ground for a moment every time.
        // Refilling must still not start, or a long chain would get more jumps than the bar holds.
        var player = RunningPlayer();
        player.Tick(ForwardJump);

        for (var hop = 0; hop < TimedJumpsUntilEmpty; hop++)
        {
            RunUntilLanded(player);
            RunTicks(player, Forward, Window / 2);
            player.Tick(ForwardJump);
        }

        Assert.True(player.HasPerfectJumpBonus);
        Assert.Equal(0f, player.Stamina.Fraction);
        Assert.False(player.Stamina.IsRecharging);
    }

    [Fact]
    public void After_the_chain_the_empty_bar_is_full_again_after_the_recharge_time()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        ChainTimedJumps(player, TimedJumpsUntilEmpty);
        RunUntilLanded(player);                             // landing without pressing

        // Refilling starts once the perfect window has passed, then takes the full recharge time.
        RunTicks(player, Forward, Window + RechargeTicks - 1);
        Assert.True(player.Stamina.Fraction < 1f);

        player.Tick(Forward);
        Assert.Equal(1f, player.Stamina.Fraction);
    }

    [Theory]
    [InlineData('1')]
    [InlineData('2')]
    [InlineData('3')]
    public void Touching_a_platform_edge_while_rising_does_not_break_the_chain(char platform)
    {
        // Found by the code review: jumping off right in front of a platform "lands" on its edge after a few ticks,
        // while still rising. That counted as a normal landing - bonus gone, and the bar refilled mid-chain.
        // Every platform position is tried, because only some take-off points hit the edge.
        for (var row = 2; row < 30; row++)
        {
            var rows = Enumerable.Repeat(".", 60).ToArray();
            rows[row] = platform.ToString();
            var player = new Player(LevelParser.Parse("P\n" + string.Join('\n', rows)));
            var chainStarted = false;

            // Pressing every other tick: every real landing is a buffered timed jump - the chain never ends on purpose.
            for (var tick = 0; tick < 400; tick++)
            {
                player.Tick(tick % 2 == 0 ? ForwardJump : Forward);
                chainStarted |= player.HasPerfectJumpBonus;

                if (chainStarted)
                {
                    Assert.True(player.HasPerfectJumpBonus || player.IsExhausted, $"Platform in row {row}, tick {tick}: chain ended.");
                    Assert.False(player.Stamina.IsRecharging, $"Platform in row {row}, tick {tick}: refilling during the chain.");
                }
            }
        }
    }

    [Fact]
    public void An_exhausted_landing_is_not_softened()
    {
        var normal = RunningPlayer();
        normal.Tick(ForwardJump);
        RunTicks(normal, Forward, AirTicks - Window / 2);   // same moments as PressShortlyBeforeLanding ...
        RunUntilLanded(normal);                             // ... but without pressing
        var normalDip = DeepestDip(normal);

        var exhausted = RunningPlayer();
        exhausted.Tick(ForwardJump);
        ChainTimedJumps(exhausted, TimedJumpsUntilEmpty);
        PressShortlyBeforeLanding(exhausted);               // timed, but the bar is empty
        var exhaustedDip = DeepestDip(exhausted);

        Assert.True(exhaustedDip > normalDip * 0.95f, $"Exhausted {exhaustedDip:0.000} m vs. normal {normalDip:0.000} m.");
    }
}
