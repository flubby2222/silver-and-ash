using System.Numerics;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class GhoulTests
{
    private static readonly int CorpseTicks = SimulationClock.SecondsToTicks(Tuning.Ghoul.CorpseTime);

    private readonly List<GameEvent> _events = [];

    [Fact]
    public void A_new_ghoul_is_alive_with_full_health_and_can_be_hit()
    {
        var ghoul = new Ghoul(7, new Vector3(3f, 0f, 5f));

        Assert.Equal(Tuning.Ghoul.MaxHealth, ghoul.Health);
        Assert.False(ghoul.IsDead);
        Assert.True(ghoul.IsHittable);
        Assert.Equal(new Vector3(3f, 0f, 5f), ghoul.Body.Position);
        Assert.Equal(Tuning.Ghoul.Height, ghoul.Body.Height);
    }

    [Fact]
    public void Damage_lowers_the_health_and_is_reported()
    {
        var ghoul = new Ghoul(7, Vector3.Zero);

        ghoul.TakeDamage(40, _events, critical: true);

        Assert.Equal(Tuning.Ghoul.MaxHealth - 40, ghoul.Health);
        Assert.Equal(new GhoulHit(7, 40, Critical: true), Assert.Single(_events));
    }

    [Fact]
    public void At_zero_health_it_dies_once()
    {
        var ghoul = new Ghoul(7, Vector3.Zero);

        ghoul.TakeDamage(60, _events);
        ghoul.TakeDamage(60, _events);
        ghoul.TakeDamage(60, _events);                      // already dead: ignored

        Assert.Equal(0, ghoul.Health);
        Assert.True(ghoul.IsDead);
        Assert.False(ghoul.IsHittable);
        Assert.Equal([new GhoulHit(7, 60), new GhoulHit(7, 60), new GhoulDied(7)], _events);
    }

    [Fact]
    public void The_dead_body_disappears_after_the_corpse_time_and_never_comes_back()
    {
        var ghoul = new Ghoul(7, Vector3.Zero);
        ghoul.TakeDamage(Tuning.Ghoul.MaxHealth, _events);

        for (var tick = 1; tick < CorpseTicks; tick++)
        {
            ghoul.Tick(_events);
            Assert.False(ghoul.IsGone, $"Gone after only {tick} ticks.");
        }

        ghoul.Tick(_events);
        Assert.True(ghoul.IsGone);

        for (var i = 0; i < 3 * CorpseTicks; i++)
        {
            ghoul.Tick(_events);
        }

        Assert.True(ghoul.IsDead);
        Assert.Equal(0, ghoul.Health);
    }

    [Fact]
    public void A_living_ghoul_does_not_disappear()
    {
        var ghoul = new Ghoul(7, Vector3.Zero);

        for (var i = 0; i < 2 * CorpseTicks; i++)
        {
            ghoul.Tick(_events);
        }

        Assert.False(ghoul.IsGone);
    }

    [Fact]
    public void The_head_is_at_the_top_front_the_rest_is_body()
    {
        // Looking along +X, standing on a 0.5 m platform.
        var ghoul = new Ghoul(7, new Vector3(0f, 0.5f, 0f), Vector3.UnitX);
        var headHeight = 0.5f + Tuning.Ghoul.Height - Tuning.Ghoul.HeadHeight / 2f;
        var r = Tuning.Ghoul.Radius;

        Assert.Equal(HitZone.Head, ghoul.ZoneAt(new Vector3(r, headHeight, 0f)));                    // front, high
        Assert.Equal(HitZone.Head, ghoul.ZoneAt(new Vector3(0.3f, 0.5f + Tuning.Ghoul.Height, 0.1f)));
        Assert.Equal(HitZone.Body, ghoul.ZoneAt(new Vector3(-r, headHeight, 0f)));                   // back, high
        Assert.Equal(HitZone.Body, ghoul.ZoneAt(new Vector3(0f, headHeight, r)));                    // side, high
        Assert.Equal(HitZone.Body, ghoul.ZoneAt(new Vector3(r, 0.7f, 0f)));                          // front, low
    }
}
