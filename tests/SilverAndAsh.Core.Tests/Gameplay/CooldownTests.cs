using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class CooldownTests
{
    [Fact]
    public void A_new_cooldown_is_ready()
    {
        var cooldown = new Cooldown(0.4f);

        Assert.True(cooldown.IsReady);
        Assert.Equal(0f, cooldown.RemainingFraction);
    }

    [Fact]
    public void After_use_it_waits_exactly_its_duration()
    {
        var cooldown = new Cooldown(0.4f);
        Assert.Equal(24, cooldown.DurationTicks);

        Assert.True(cooldown.TryUse());
        for (var tick = 1; tick < 24; tick++)
        {
            cooldown.Tick();
            Assert.False(cooldown.IsReady, $"Ready again after only {tick} ticks.");
        }

        cooldown.Tick();
        Assert.True(cooldown.IsReady);
    }

    [Fact]
    public void Using_it_while_waiting_does_nothing()
    {
        var cooldown = new Cooldown(0.4f);
        cooldown.TryUse();
        cooldown.Tick();
        var before = cooldown.RemainingFraction;

        Assert.False(cooldown.TryUse());
        Assert.Equal(before, cooldown.RemainingFraction);
    }

    [Fact]
    public void The_remaining_fraction_goes_from_one_to_zero()
    {
        var cooldown = new Cooldown(0.4f);
        cooldown.TryUse();
        Assert.Equal(1f, cooldown.RemainingFraction);

        for (var i = 0; i < 12; i++)
        {
            cooldown.Tick();
        }

        Assert.Equal(0.5f, cooldown.RemainingFraction, 0.001f);
        Assert.Equal(0.2f, cooldown.RemainingSeconds, 0.001f);
    }

    [Fact]
    public void Ticking_a_ready_cooldown_keeps_it_ready()
    {
        var cooldown = new Cooldown(0.4f);
        cooldown.Tick();
        cooldown.Tick();

        Assert.True(cooldown.IsReady);
        Assert.Equal(0f, cooldown.RemainingFraction);
    }
}
