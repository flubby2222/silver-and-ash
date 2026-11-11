using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class StaminaTests
{
    private static readonly int RechargeTicks = SimulationClock.SecondsToTicks(Tuning.Player.StaminaRechargeTime);

    [Fact]
    public void A_full_bar_pays_for_exactly_the_tuned_number_of_jumps()
    {
        var stamina = new Stamina();

        for (var jump = 0; jump < Tuning.Player.StaminaJumps; jump++)
        {
            Assert.True(stamina.TryPay(), $"Jump {jump + 1} should be paid.");
        }

        Assert.False(stamina.TryPay());
        Assert.Equal(0f, stamina.Fraction);
    }

    [Fact]
    public void The_tuning_values_split_the_bar_evenly()
    {
        // Guards against tuning mistakes (code review): with uneven values a rest would stay in the bar,
        // and with a cost of 0 the jumps would be free.
        Assert.True(Stamina.CostPerSegment > 0);
        Assert.Equal(Stamina.Capacity, Stamina.CostPerSegment * Tuning.Player.StaminaJumps);
    }

    [Fact]
    public void Without_recharging_the_bar_stays_empty()
    {
        var stamina = EmptyStamina();

        Tick(stamina, RechargeTicks);

        Assert.Equal(0f, stamina.Fraction);
    }

    [Fact]
    public void Empty_to_full_takes_exactly_the_recharge_time()
    {
        var stamina = EmptyStamina();
        stamina.SetRecharging(true);

        Tick(stamina, RechargeTicks - 1);
        Assert.True(stamina.Fraction < 1f);

        stamina.Tick();
        Assert.Equal(1f, stamina.Fraction);
    }

    [Fact]
    public void One_jump_is_refilled_in_its_share_of_the_time()
    {
        var stamina = new Stamina();
        stamina.TryPay();
        stamina.SetRecharging(true);

        Tick(stamina, RechargeTicks / Tuning.Player.StaminaJumps);

        Assert.Equal(1f, stamina.Fraction);
    }

    [Fact]
    public void The_bar_never_overfills()
    {
        var stamina = new Stamina();
        stamina.SetRecharging(true);

        Tick(stamina, 2 * RechargeTicks);

        Assert.Equal(1f, stamina.Fraction);
    }

    private static Stamina EmptyStamina()
    {
        var stamina = new Stamina();
        while (stamina.TryPay())
        {
        }

        return stamina;
    }

    private static void Tick(Stamina stamina, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            stamina.Tick();
        }
    }
}
