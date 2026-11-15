using System.Numerics;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class GhoulTests
{
    private static readonly int CorpseTicks = SimulationClock.SecondsToTicks(Balance.Default.Ghoul.CorpseTime);

    private readonly List<GameEvent> _events = [];

    [Fact]
    public void A_new_ghoul_is_alive_with_full_health_and_can_be_hit()
    {
        var ghoul = new Ghoul(7, new Vector3(3f, 0f, 5f));

        Assert.Equal(Balance.Default.Ghoul.MaxHealth, ghoul.Health);
        Assert.False(ghoul.IsDead);
        Assert.True(ghoul.IsHittable);
        Assert.Equal(new Vector3(3f, 0f, 5f), ghoul.Body.Position);
        Assert.Equal(BodySizes.Ghoul.Height, ghoul.Body.Height);
    }

    [Fact]
    public void Damage_lowers_the_health_and_is_reported()
    {
        var ghoul = new Ghoul(7, Vector3.Zero);

        ghoul.TakeDamage(40, _events, critical: true);

        Assert.Equal(Balance.Default.Ghoul.MaxHealth - 40, ghoul.Health);
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
        ghoul.TakeDamage(Balance.Default.Ghoul.MaxHealth, _events);

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
        var headHeight = 0.5f + BodySizes.Ghoul.Height - BodySizes.Ghoul.HeadHeight / 2f;
        var r = BodySizes.Ghoul.Radius;

        Assert.Equal(HitZone.Head, ghoul.ZoneAt(new Vector3(r, headHeight, 0f)));                    // front, high
        Assert.Equal(HitZone.Head, ghoul.ZoneAt(new Vector3(0.3f, 0.5f + BodySizes.Ghoul.Height, 0.1f)));
        Assert.Equal(HitZone.Body, ghoul.ZoneAt(new Vector3(-r, headHeight, 0f)));                   // back, high
        Assert.Equal(HitZone.Body, ghoul.ZoneAt(new Vector3(0f, headHeight, r)));                    // side, high
        Assert.Equal(HitZone.Body, ghoul.ZoneAt(new Vector3(r, 0.7f, 0f)));                          // front, low
    }

    [Fact]
    public void Beside_the_head_is_body_even_high_at_the_front()
    {
        // Looking along +X. The drawn head is 0.26 m wide; a hit 0.2 m to its side, high at the front, is body.
        var ghoul = new Ghoul(7, Vector3.Zero, Vector3.UnitX);
        var high = BodySizes.Ghoul.Height - 0.05f;

        Assert.Equal(HitZone.Head, ghoul.ZoneAt(new Vector3(0.35f, high, 0.1f)));
        Assert.Equal(HitZone.Body, ghoul.ZoneAt(new Vector3(0.3f, high, 0.2f)));
        Assert.Equal(HitZone.Body, ghoul.ZoneAt(new Vector3(0.3f, high, -0.2f)));
        Assert.Equal(HitZone.Body, ghoul.ZoneAt(new Vector3(0.12f, high, 0f)));                      // still above the neck
    }

    [Fact]
    public void The_head_zone_follows_where_the_ghoul_looks()
    {
        // Looking along +Z now: the head lies in +Z, sideways is X.
        var ghoul = new Ghoul(7, Vector3.Zero, Vector3.UnitZ);
        var high = BodySizes.Ghoul.Height - 0.05f;

        Assert.Equal(HitZone.Head, ghoul.ZoneAt(new Vector3(0.1f, high, 0.35f)));
        Assert.Equal(HitZone.Body, ghoul.ZoneAt(new Vector3(0.2f, high, 0.3f)));
        Assert.Equal(HitZone.Body, ghoul.ZoneAt(new Vector3(0f, high, -0.35f)));
    }
}
