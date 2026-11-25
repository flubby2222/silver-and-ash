using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using static SilverAndAsh.Core.Tests.Gameplay.JumpTestKit;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class PlayerMeleeChargeTests
{
    private const float Tolerance = 0.001f;

    private static readonly PlayerInput Melee = PlayerInput.None with { Melee = true };
    private static readonly int FullTicks = SimulationClock.SecondsToTicks(Balance.Default.Blade.ChargeTime);
    private static readonly int BladeTicks = SimulationClock.SecondsToTicks(Balance.Default.Blade.Cooldown);
    private static float SegmentFraction => 1f / Balance.Default.Player.StaminaJumps;

    [Fact]
    public void A_quick_click_swings_uncharged_when_the_button_is_let_go()
    {
        var player = RunningPlayer();

        player.Tick(Melee);
        // The press only says that a charge began (issue #776: the axe's charge sound starts from it); no swing yet.
        Assert.IsType<MeleeChargeStarted>(Assert.Single(player.Events));
        Assert.True(player.MeleeCharge.IsCharging);

        player.Tick(PlayerInput.None);

        var swing = Assert.IsType<MeleeSwung>(Assert.Single(player.Events));
        Assert.Equal(0f, swing.Charge);
        Assert.False(player.MeleeCharge.IsCharging);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void Holding_charges_linearly(float part)
    {
        var player = RunningPlayer();

        Assert.Equal(part, HoldAndRelease(player, (int)(part * FullTicks)).Charge, Tolerance);
    }

    [Fact]
    public void Holding_longer_than_the_charge_time_stays_fully_charged()
    {
        Assert.Equal(1f, HoldAndRelease(RunningPlayer(), 3 * FullTicks).Charge, Tolerance);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void Charging_costs_stamina_linearly_up_to_one_segment(float part)
    {
        // Measured while still holding, at the moment the charge is reached: once it is full, the bar refills
        // again (#116) - after letting go it would already be a little fuller.
        var player = RunningPlayer();

        player.Tick(Melee);
        RunTicks(player, Melee, (int)(part * FullTicks));

        var cost = part * Balance.Default.Blade.ChargeStaminaSegments * SegmentFraction;
        Assert.Equal(1f - cost, player.Stamina.Fraction, Tolerance);
    }

    [Fact]
    public void Charging_stops_when_the_stamina_runs_out()
    {
        // Half a segment left, in the air (so nothing refills): the blade charges only half way. The empty bar is
        // checked while still in the air - after the landing it refills although the button is held (#413).
        var player = InTheAirWithStamina(new Stamina().CostPerSegment / 2);
        player.Tick(Melee);
        RunTicks(player, Melee, FullTicks / 2 + 1);
        Assert.False(player.IsGrounded);
        Assert.Equal(0f, player.Stamina.Fraction);

        RunTicks(player, Melee, FullTicks / 2);
        player.Tick(PlayerInput.None);
        var swing = Assert.IsType<MeleeSwung>(Assert.Single(player.Events));
        Assert.Equal(0.5f, swing.Charge, Tolerance);
    }

    [Fact]
    public void With_an_empty_bar_the_blade_swings_uncharged()
    {
        var player = InTheAirWithStamina(0);

        Assert.Equal(0f, HoldAndRelease(player, FullTicks).Charge);
    }

    [Fact]
    public void The_bar_does_not_refill_while_charging()
    {
        // Two dodges: 2 of 5 segments are left. Standing still afterwards, the bar starts to refill.
        var player = RunningPlayer();
        var dodge = PlayerInput.None with { Dash = true };
        player.Tick(dodge);
        RunTicks(player, PlayerInput.None, 30);
        player.Tick(dodge);
        RunTicks(player, PlayerInput.None, 60);
        Assert.True(player.Stamina.IsRecharging);

        player.Tick(Melee);
        var before = player.Stamina.Fraction;
        RunTicks(player, Melee, FullTicks / 2);

        Assert.Equal(before - SegmentFraction / 2f, player.Stamina.Fraction, Tolerance);
    }

    [Fact]
    public void Once_fully_charged_the_bar_refills_while_the_button_is_held()
    {
        // As above: 2 of 5 segments left, standing still. Charge fully, then keep holding for half a second.
        var player = RunningPlayer();
        var dodge = PlayerInput.None with { Dash = true };
        player.Tick(dodge);
        RunTicks(player, PlayerInput.None, 30);
        player.Tick(dodge);
        RunTicks(player, PlayerInput.None, 60);
        player.Tick(Melee);
        RunTicks(player, Melee, FullTicks);
        Assert.Equal(1f, player.MeleeCharge.Fraction);
        var full = player.Stamina.Fraction;

        RunTicks(player, Melee, SimulationClock.TicksPerSecond / 2);

        // Half a second of refilling: a sixth of the bar (3 s from empty to full).
        Assert.True(player.MeleeCharge.IsCharging);
        Assert.Equal(full + 1f / 6f, player.Stamina.Fraction, Tolerance);
    }

    [Fact]
    public void The_cooldown_starts_when_the_button_is_let_go()
    {
        var player = RunningPlayer();
        player.Tick(Melee);
        Assert.True(player.MeleeCooldown.IsReady, "The cooldown must not start when the button is pressed.");
        RunTicks(player, Melee, FullTicks / 2);
        Assert.True(player.MeleeCooldown.IsReady);

        player.Tick(PlayerInput.None);

        Assert.False(player.MeleeCooldown.IsReady);
    }

    [Fact]
    public void A_press_during_the_cooldown_does_not_charge()
    {
        var player = RunningPlayer();
        HoldAndRelease(player, 0);

        player.Tick(Melee);
        RunTicks(player, Melee, 2 * BladeTicks);                // held until long after the cooldown
        player.Tick(PlayerInput.None);

        Assert.False(player.MeleeCharge.IsCharging);
        Assert.Empty(player.Events);
    }

    /// <summary>Presses the blade button, holds it for <paramref name="extraTicks"/> more ticks, lets go.</summary>
    private static MeleeSwung HoldAndRelease(Player player, int extraTicks)
    {
        player.Tick(Melee);
        RunTicks(player, Melee, extraTicks);
        player.Tick(PlayerInput.None);
        return Assert.IsType<MeleeSwung>(Assert.Single(player.Events));
    }

    /// <summary>Jumps and leaves exactly <paramref name="amount"/> in the bar (in the bar's own unit).</summary>
    private static Player InTheAirWithStamina(int amount)
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        var left = (int)MathF.Round(player.Stamina.Fraction * new Stamina().Capacity);
        Assert.True(player.Stamina.TryPayPart(left - amount));
        return player;
    }
}
