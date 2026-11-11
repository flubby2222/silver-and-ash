using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.Gameplay.JumpTestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class PlayerStaminaTests
{
    private const float Tolerance = 0.01f;

    private static readonly int RechargeTicks = SimulationClock.SecondsToTicks(Tuning.Player.StaminaRechargeTime);

    [Fact]
    public void Four_perfect_jumps_in_a_row_use_up_the_whole_bar()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);

        ChainTimedJumps(player, Tuning.Player.StaminaPerfectJumps);

        Assert.True(player.HasPerfectJumpBonus);
        Assert.Equal(0f, player.Stamina.Fraction);
    }

    [Fact]
    public void The_fifth_timed_jump_is_exhausted_and_slower_than_normal()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);

        ChainTimedJumps(player, Tuning.Player.StaminaPerfectJumps + 1);
        RunTicks(player, Forward, 20);

        Assert.False(player.HasPerfectJumpBonus);
        Assert.True(player.IsExhausted);
        Assert.Equal(ExhaustedAirSpeed, HorizontalSpeed(player), Tolerance);
    }

    [Fact]
    public void Standing_after_a_landing_ends_the_exhaustion()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        ChainTimedJumps(player, Tuning.Player.StaminaPerfectJumps + 1);
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
        ChainTimedJumps(player, Tuning.Player.StaminaPerfectJumps);
        RunUntilLanded(player);
        player.Tick(Forward);
        var before = HorizontalSpeed(player);

        player.Tick(ForwardJump);                           // timed, but the bar is empty

        Assert.True(player.IsExhausted);
        Assert.True(HorizontalSpeed(player) < before * 0.8f, $"Speed {before:0.00} -> {HorizontalSpeed(player):0.00} m/s.");
    }

    [Fact]
    public void Starting_a_chain_stops_the_refilling()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        ChainTimedJumps(player, Tuning.Player.StaminaPerfectJumps);
        RunUntilLanded(player);
        RunTicks(player, Forward, Window + 20);             // chain over, refilling
        Assert.True(player.Stamina.IsRecharging);

        player.Tick(ForwardJump);                           // a normal jump - refilling goes on ...
        PressShortlyBeforeLanding(player);                  // ... until a timed jump starts a new chain
        player.Tick(Forward);

        Assert.False(player.Stamina.IsRecharging);
    }

    [Fact]
    public void The_bar_does_not_refill_during_a_chain()
    {
        // Pressing shortly AFTER each landing: every landing is a normal one for a moment.
        // Recharging must still not start, or a long chain would get more than four perfect jumps.
        var player = RunningPlayer();
        player.Tick(ForwardJump);

        for (var hop = 0; hop < Tuning.Player.StaminaPerfectJumps; hop++)
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
    public void After_a_normal_landing_the_empty_bar_is_full_again_after_the_recharge_time()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        ChainTimedJumps(player, Tuning.Player.StaminaPerfectJumps);
        RunUntilLanded(player);                             // normal landing - the chain is over

        // Recharging starts once the perfect window has passed, then takes the full recharge time.
        RunTicks(player, Forward, Window + RechargeTicks - 1);
        Assert.True(player.Stamina.Fraction < 1f);

        player.Tick(Forward);
        Assert.Equal(1f, player.Stamina.Fraction);
    }

    [Fact]
    public void The_bar_keeps_refilling_during_normal_jumps()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        ChainTimedJumps(player, Tuning.Player.StaminaPerfectJumps);
        RunUntilLanded(player);
        RunTicks(player, Forward, Window + 5);
        var before = player.Stamina.Fraction;

        player.Tick(ForwardJump);                           // a normal jump (window long over)
        RunTicks(player, Forward, 20);

        Assert.False(player.IsGrounded);
        Assert.True(player.Stamina.Fraction > before);
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
        ChainTimedJumps(exhausted, Tuning.Player.StaminaPerfectJumps);
        PressShortlyBeforeLanding(exhausted);               // timed, but the bar is empty
        var exhaustedDip = DeepestDip(exhausted);

        Assert.True(exhaustedDip > normalDip * 0.95f, $"Exhausted {exhaustedDip:0.000} m vs. normal {normalDip:0.000} m.");
    }
}
