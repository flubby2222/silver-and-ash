using System.Numerics;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class TrainingDummyTests
{
    private static readonly int DownTicks = SimulationClock.SecondsToTicks(Tuning.Combat.DummyDownTime);

    private readonly List<GameEvent> _events = [];

    [Fact]
    public void A_new_dummy_stands_with_full_health()
    {
        var dummy = new TrainingDummy(3, new Vector3(1f, 0f, 2f));

        Assert.Equal(Tuning.Combat.DummyMaxHealth, dummy.Health);
        Assert.False(dummy.IsDown);
        Assert.Equal(3, dummy.Id);
        Assert.Equal(new Vector3(1f, 0f, 2f), dummy.Body.Position);
    }

    [Fact]
    public void Damage_lowers_the_health_and_is_reported()
    {
        var dummy = new TrainingDummy(3, Vector3.Zero);

        dummy.TakeDamage(40, _events);

        Assert.Equal(Tuning.Combat.DummyMaxHealth - 40, dummy.Health);
        Assert.Equal(new DummyHit(3, 40), Assert.Single(_events));
    }

    [Fact]
    public void At_zero_health_the_dummy_falls_over()
    {
        var dummy = new TrainingDummy(3, Vector3.Zero);

        dummy.TakeDamage(60, _events);
        dummy.TakeDamage(60, _events);                      // more than is left

        Assert.Equal(0, dummy.Health);
        Assert.True(dummy.IsDown);
        Assert.Equal([new DummyHit(3, 60), new DummyHit(3, 60), new DummyKnockedDown(3)], _events);
    }

    [Fact]
    public void A_dummy_on_the_ground_ignores_hits()
    {
        var dummy = new TrainingDummy(3, Vector3.Zero);
        dummy.TakeDamage(Tuning.Combat.DummyMaxHealth, _events);
        _events.Clear();

        dummy.TakeDamage(40, _events);

        Assert.Empty(_events);
        Assert.Equal(0, dummy.Health);
    }

    [Fact]
    public void After_the_down_time_it_stands_up_with_full_health()
    {
        var dummy = new TrainingDummy(3, Vector3.Zero);
        dummy.TakeDamage(Tuning.Combat.DummyMaxHealth, _events);
        _events.Clear();

        for (var tick = 1; tick < DownTicks; tick++)
        {
            dummy.Tick(_events);
            Assert.True(dummy.IsDown, $"Up again after only {tick} ticks.");
        }

        Assert.Empty(_events);
        dummy.Tick(_events);

        Assert.False(dummy.IsDown);
        Assert.Equal(Tuning.Combat.DummyMaxHealth, dummy.Health);
        Assert.Equal(new DummyStoodUp(3), Assert.Single(_events));
    }

    [Fact]
    public void A_standing_dummy_does_nothing_on_its_own()
    {
        var dummy = new TrainingDummy(3, Vector3.Zero);

        for (var i = 0; i < 2 * DownTicks; i++)
        {
            dummy.Tick(_events);
        }

        Assert.Empty(_events);
        Assert.False(dummy.IsDown);
    }

    [Fact]
    public void Negative_damage_is_not_allowed()
    {
        var dummy = new TrainingDummy(3, Vector3.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() => dummy.TakeDamage(-5, _events));
    }
}
