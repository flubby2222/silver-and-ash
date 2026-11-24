using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using static SilverAndAsh.Core.Tests.Gameplay.JumpTestKit;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Issue #413: the stamina bar must not stay empty while the blade button is held. If the stamina cannot pay the next
/// bit of the charge - because a dodge or dash took it, or because charging used it up by itself - the charge holds
/// where it is and the bar refills, even while the button is still held.
/// </summary>
public class PlayerMeleeChargeStallTests
{
    private const float Tolerance = 0.001f;

    private static readonly PlayerInput Melee = PlayerInput.None with { Melee = true };
    private static readonly PlayerInput MeleeAndEvade = Melee with { Dash = true };
    private static readonly int FullTicks = SimulationClock.SecondsToTicks(Balance.Default.Blade.ChargeTime);
    private static readonly int BladeTicks = SimulationClock.SecondsToTicks(Balance.Default.Blade.Cooldown);
    private static readonly Stamina Bar = new();

    // Long enough to refill an empty bar, plus a landing and the perfect window on top.
    private static readonly int LongHold = 2 * Bar.Capacity + AirTicks + Window;

    /// <summary>What is left after the evade, in quarters of a segment: nothing, a little, half a segment.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void A_dodge_while_charging_lets_the_bar_refill_while_the_button_is_held(int quartersLeft)
    {
        // Standing on the ground with exactly the dodge plus a little left: press, hold, dodge while holding.
        var player = RunningPlayer();
        var dodgeCost = Balance.Default.Player.DodgeStaminaSegments * Bar.CostPerSegment;
        LeaveInBar(player, dodgeCost + quartersLeft * Bar.CostPerSegment / 4);
        player.Tick(Melee);
        player.Tick(MeleeAndEvade);
        Assert.True(player.IsDodging, "The dodge must start while the button is held.");

        HoldLongAndCheckTheBarRefills(player);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void A_dash_while_charging_lets_the_bar_refill_after_landing_while_the_button_is_held(int quartersLeft)
    {
        // In the air with exactly the dash plus a little left: press, hold, dash while holding, land, keep holding.
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        Assert.False(player.IsGrounded);
        LeaveInBar(player, Bar.CostPerSegment + quartersLeft * Bar.CostPerSegment / 4);
        player.Tick(Melee);
        player.Tick(MeleeAndEvade);
        Assert.True(player.IsDashing, "The dash must start while the button is held.");

        HoldLongAndCheckTheBarRefills(player);
    }

    [Fact]
    public void Charging_that_uses_up_the_bar_by_itself_holds_the_charge_and_lets_the_bar_refill()
    {
        // On the ground with half of what a full charge costs: the charge stalls half way by itself.
        var player = RunningPlayer();
        var fullChargeCost = Balance.Default.Blade.ChargeStaminaSegments * Bar.CostPerSegment;
        LeaveInBar(player, fullChargeCost / 2);
        player.Tick(Melee);

        HoldLongAndCheckTheBarRefills(player);
    }

    [Fact]
    public void After_a_stalled_charge_the_next_charge_works_normally()
    {
        // Second run: the stall of the first hold must not carry over to the next press.
        var player = RunningPlayer();
        LeaveInBar(player, 0);
        player.Tick(Melee);
        RunTicks(player, Melee, FullTicks);
        Assert.True(player.MeleeCharge.IsStalled);
        player.Tick(PlayerInput.None);
        RunTicks(player, PlayerInput.None, Math.Max(BladeTicks, 2 * Bar.Capacity));
        Assert.Equal(1f, player.Stamina.Fraction);

        player.Tick(Melee);
        RunTicks(player, Melee, FullTicks);
        Assert.False(player.MeleeCharge.IsStalled);
        player.Tick(PlayerInput.None);

        var swing = Assert.IsType<MeleeSwung>(Assert.Single(player.Events));
        Assert.Equal(1f, swing.Charge, Tolerance);
    }

    /// <summary>
    /// Keeps holding the button for a long time: the charge must have stalled short of full and stay there, the bar
    /// must be full again while the button is still held, and letting go swings with the charge reached.
    /// </summary>
    private static void HoldLongAndCheckTheBarRefills(Player player)
    {
        RunTicks(player, Melee, FullTicks);
        Assert.True(player.MeleeCharge.IsCharging);
        Assert.True(player.MeleeCharge.Fraction < 1f, "The stamina must run out before the charge is full.");
        var held = player.MeleeCharge.Fraction;

        RunTicks(player, Melee, LongHold);

        Assert.True(player.IsGrounded);
        Assert.True(player.MeleeCharge.IsCharging, "The button is still held.");
        Assert.Equal(held, player.MeleeCharge.Fraction);
        Assert.Equal(1f, player.Stamina.Fraction);

        player.Tick(PlayerInput.None);
        var swing = Assert.IsType<MeleeSwung>(Assert.Single(player.Events));
        Assert.Equal(held, swing.Charge);
    }

    /// <summary>Leaves exactly <paramref name="amount"/> in the bar (in the bar's own unit).</summary>
    private static void LeaveInBar(Player player, int amount)
    {
        Assert.True(player.Stamina.TryPayPart(player.Stamina.Amount - amount));
        Assert.Equal(amount, player.Stamina.Amount);
    }
}
