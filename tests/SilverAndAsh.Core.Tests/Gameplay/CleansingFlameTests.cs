using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Issue #507: Cleansing Flame, damage over time in pulses. The tests set a ghoul alight the way a weapon does
/// (<see cref="GameWorld.ApplyStatus"/>, between two ticks) and tick the world, reading the hits it really reports.
/// </summary>
public class CleansingFlameTests
{
    private static readonly CleansingFlameBalance Flame = Balance.Default.StatusEffects.CleansingFlame;
    private static readonly int FlameTicks = SimulationClock.SecondsToTicks(Flame.Seconds);
    private static readonly int PulseTicks = CleansingFlame.PulseTicks(Flame);

    private const string Corridor = "P\n.\n.\n.\nG";

    /// <summary>A ghoul that stands still and does not die of the flame, unless <paramref name="health"/> says so.</summary>
    private static Balance StillGhoul(int health = 100_000) => Balance.Default with
    {
        Ghoul = Balance.Default.Ghoul with { Speed = 0f, MaxHealth = health },
    };

    /// <summary>The damage the whole flame deals by the standard values, summed up tick by tick.</summary>
    private static int WholeFlame => Enumerable.Range(1, FlameTicks).Sum(tick => CleansingFlame.DamageOnTick(tick, Flame));

    /// <summary>Ticks the world <paramref name="ticks"/> times; returns every hit with the tick (1 = the first) it came on.</summary>
    private static List<(int Tick, GhoulHit Hit)> Hits(GameWorld world, int ticks, List<GameEvent>? events = null)
    {
        var hits = new List<(int, GhoulHit)>();
        for (var tick = 1; tick <= ticks; tick++)
        {
            world.Tick(PlayerInput.None);
            events?.AddRange(world.Events);
            hits.AddRange(world.Events.OfType<GhoulHit>().Select(hit => (tick, hit)));
        }

        return hits;
    }

    [Fact]
    public void The_flame_deals_its_damage_in_pulses_every_half_second_while_it_lasts()
    {
        var world = new GameWorld(LevelParser.Parse(Corridor), balance: StillGhoul());
        var ghoul = world.Ghouls[0];
        world.ApplyStatus(ghoul, StatusKind.CleansingFlame, world.Player);

        // Twice as long as the flame: nothing may come after it.
        var hits = Hits(world, 2 * FlameTicks);

        var pulses = Enumerable.Range(1, FlameTicks / PulseTicks).Select(pulse => pulse * PulseTicks).ToList();
        Assert.Equal(pulses, hits.Select(hit => hit.Tick));
        Assert.Equal(SimulationClock.SecondsToTicks(Flame.TickSeconds), PulseTicks);
        Assert.All(hits, hit => Assert.Equal(ghoul.Id, hit.Hit.GhoulId));
        Assert.All(hits, hit => Assert.False(hit.Hit.Critical));
        Assert.False(ghoul.Status.Has(StatusKind.CleansingFlame));
    }

    [Fact]
    public void The_whole_flame_deals_its_damage_per_second_times_its_seconds()
    {
        var world = new GameWorld(LevelParser.Parse(Corridor), balance: StillGhoul());
        var ghoul = world.Ghouls[0];
        var before = ghoul.Health;
        world.ApplyStatus(ghoul, StatusKind.CleansingFlame, world.Player);

        var hits = Hits(world, 2 * FlameTicks);

        var expected = (int)MathF.Round(Flame.DamagePerSecond * Flame.Seconds);   // 5/s for 2 s: 10
        Assert.Equal(expected, hits.Sum(hit => hit.Hit.Damage));
        Assert.Equal(expected, before - ghoul.Health);
        Assert.Equal(expected, WholeFlame);
    }

    [Fact]
    public void Every_pulse_is_reported_as_a_hit_of_the_flame_by_the_player_who_set_it_off()
    {
        var world = new GameWorld(LevelParser.Parse(Corridor), balance: StillGhoul());
        world.Player.Id = 3;
        var ghoul = world.Ghouls[0];
        world.ApplyStatus(ghoul, StatusKind.CleansingFlame, world.Player);
        var events = new List<GameEvent>();

        Hits(world, FlameTicks, events);

        var byFlame = events.OfType<TargetHitBy>().ToList();
        Assert.Equal(FlameTicks / PulseTicks, byFlame.Count);
        Assert.All(byFlame, hit => Assert.Equal(new TargetHitBy(ghoul.Id, 3, Weapon.CleansingFlame), hit));
    }

    [Fact]
    public void A_refresh_makes_the_flame_last_longer_but_never_burn_harder()
    {
        // Set alight, and again after three quarters of a pulse past the first: the flame keeps its beat and lasts a
        // whole flame from the refresh on. One hit per pulse, each of the pulse's own size - nothing doubles.
        var world = new GameWorld(LevelParser.Parse(Corridor), balance: StillGhoul());
        var ghoul = world.Ghouls[0];
        world.ApplyStatus(ghoul, StatusKind.CleansingFlame, world.Player);
        var refreshAt = PulseTicks + (PulseTicks * 3 / 4);

        var hits = Hits(world, refreshAt);
        world.ApplyStatus(ghoul, StatusKind.CleansingFlame, world.Player);
        hits.AddRange(Hits(world, 2 * FlameTicks).Select(hit => (hit.Tick + refreshAt, hit.Hit)));

        var expected = Enumerable.Range(1, refreshAt + FlameTicks)
            .Select(tick => (Tick: tick, Damage: CleansingFlame.DamageOnTick(tick, Flame)))
            .Where(pulse => pulse.Damage > 0)
            .ToList();
        Assert.Equal(expected, hits.Select(hit => (hit.Tick, hit.Hit.Damage)));
        Assert.True(expected.Count > FlameTicks / PulseTicks, "The refresh did not make the flame last longer.");
        Assert.True(hits.Sum(hit => hit.Hit.Damage) < 2 * WholeFlame, "The refresh stacked a second flame.");
        Assert.False(ghoul.Status.Has(StatusKind.CleansingFlame));
    }

    [Fact]
    public void A_refresh_every_tick_never_holds_the_pulses_back()
    {
        // The beat is counted from when the flame began, not from the last refresh: a flame refreshed all the time still burns.
        var world = new GameWorld(LevelParser.Parse(Corridor), balance: StillGhoul());
        var ghoul = world.Ghouls[0];
        var hits = new List<(int Tick, GhoulHit Hit)>();
        for (var tick = 1; tick <= FlameTicks; tick++)
        {
            world.ApplyStatus(ghoul, StatusKind.CleansingFlame, world.Player);
            hits.AddRange(Hits(world, 1).Select(hit => (tick, hit.Hit)));
        }

        Assert.Equal(FlameTicks / PulseTicks, hits.Count);
        Assert.Equal(WholeFlame, hits.Sum(hit => hit.Hit.Damage));
    }

    [Fact]
    public void A_kill_by_the_flame_gives_its_experience_to_the_player_who_set_it_off()
    {
        var world = new GameWorld(LevelParser.Parse(Corridor), balance: StillGhoul(WholeFlame));
        var ghoul = world.Ghouls[0];
        world.ApplyStatus(ghoul, StatusKind.CleansingFlame, world.Player);
        var events = new List<GameEvent>();

        Hits(world, FlameTicks, events);

        Assert.True(ghoul.IsDead);
        Assert.Equal(new ExperienceGained(world.Player.Id, Balance.Default.Experience.Ghoul), Assert.Single(events.OfType<ExperienceGained>()));
    }

    [Fact]
    public void A_flame_set_off_by_another_player_gives_this_one_nothing()
    {
        // Online co-op: the kill counts for the player who set the flame off, not for any player. The other one is not
        // in this world (it has one player for now) - so its flame finds no player to count for, and this one gets nothing.
        var world = new GameWorld(LevelParser.Parse(Corridor), balance: StillGhoul(WholeFlame));
        var other = new Player(LevelParser.Parse("P")) { Id = world.Player.Id + 1 };
        var ghoul = world.Ghouls[0];
        world.ApplyStatus(ghoul, StatusKind.CleansingFlame, other);
        Assert.Equal(other.Id, ghoul.Status.Get(StatusKind.CleansingFlame)?.SourcePlayerId);
        var events = new List<GameEvent>();

        Hits(world, FlameTicks, events);

        Assert.DoesNotContain(events, e => e is ExperienceGained);
        Assert.DoesNotContain(events, e => e is TargetHitBy { PlayerId: var id } && id == world.Player.Id);
        Assert.Equal(0, world.Player.Experience);
    }

    [Fact]
    public void A_dead_enemy_takes_no_more_pulses()
    {
        var world = new GameWorld(LevelParser.Parse(Corridor), balance: StillGhoul(1));
        var ghoul = world.Ghouls[0];
        world.ApplyStatus(ghoul, StatusKind.CleansingFlame, world.Player);
        world.Tick(PlayerInput.None);
        ghoul.TakeDamage(1, []);
        Assert.True(ghoul.IsDead);
        var events = new List<GameEvent>();

        var hits = Hits(world, 2 * FlameTicks, events);

        Assert.Empty(hits);
        Assert.DoesNotContain(events, e => e is TargetHitBy or ExperienceGained);
    }

    [Fact]
    public void The_age_of_an_effect_counts_from_its_start_survives_a_refresh_and_ends_with_it()
    {
        var status = new StatusEffects();
        Assert.Equal(0, status.AgeOf(StatusKind.CleansingFlame));

        status.Apply(StatusKind.CleansingFlame, 3, 1);
        status.Tick(0, []);
        status.Apply(StatusKind.CleansingFlame, 3, 1);   // refreshed: lasts longer, keeps its age
        Assert.Equal(1, status.AgeOf(StatusKind.CleansingFlame));
        status.Tick(0, []);
        Assert.Equal(2, status.AgeOf(StatusKind.CleansingFlame));

        status.Tick(0, []);
        status.Tick(0, []);
        Assert.False(status.Has(StatusKind.CleansingFlame));
        Assert.Equal(0, status.AgeOf(StatusKind.CleansingFlame));

        status.Apply(StatusKind.CleansingFlame, 3, 1);   // a new flame starts from the beginning
        Assert.Equal(0, status.AgeOf(StatusKind.CleansingFlame));
    }

    [Fact]
    public void Pulses_that_do_not_come_out_even_take_turns_and_lose_nothing()
    {
        // 5 per second in pulses of half a second: 2.5 each - 2, 3, 2, 3 ... and after n pulses n x 2.5 rounded down.
        var values = new CleansingFlameBalance { Seconds = 10f, DamagePerSecond = 5f, TickSeconds = 0.5f };
        var pulse = CleansingFlame.PulseTicks(values);
        var sum = 0;
        for (var n = 1; n <= 20; n++)
        {
            sum += CleansingFlame.DamageOnTick(n * pulse, values);
            Assert.Equal((int)Math.Floor(n * values.DamagePerSecond * values.TickSeconds), sum);
        }
    }

    [Fact]
    public void Between_the_pulses_and_before_the_flame_there_is_no_damage()
    {
        Assert.True(PulseTicks > 2, "A pulse on every other tick leaves no tick between - the test would prove nothing.");
        int[] quiet = [0, -PulseTicks, 1, PulseTicks - 1, PulseTicks + 1, (2 * PulseTicks) - 1];

        Assert.All(quiet, tick => Assert.Equal(0, CleansingFlame.DamageOnTick(tick, Flame)));
        Assert.True(CleansingFlame.DamageOnTick(PulseTicks, Flame) > 0);
    }

    [Fact]
    public void A_pulse_shorter_than_a_tick_burns_on_every_tick()
    {
        var values = new CleansingFlameBalance { Seconds = 1f, DamagePerSecond = 60f, TickSeconds = 0.001f };

        Assert.Equal(1, CleansingFlame.PulseTicks(values));
        Assert.All(Enumerable.Range(1, SimulationClock.TicksPerSecond), tick => Assert.Equal(1, CleansingFlame.DamageOnTick(tick, values)));
    }

    [Fact]
    public void A_marked_burning_enemy_takes_the_mark_s_bonus_on_every_pulse()
    {
        // Provisional (issue #507): a pulse is a hit like any other, so the damage rules - the mark too - apply to it.
        // A hotter flame than the standard, so the bonus shows after rounding.
        var hot = Flame with { DamagePerSecond = Flame.DamagePerSecond * 8f };
        var balance = StillGhoul() with { StatusEffects = Balance.Default.StatusEffects with { CleansingFlame = hot } };
        var world = new GameWorld(LevelParser.Parse(Corridor), balance: balance);
        var ghoul = world.Ghouls[0];
        world.ApplyStatus(ghoul, StatusKind.Marked, world.Player);
        world.ApplyStatus(ghoul, StatusKind.CleansingFlame, world.Player);

        var first = Hits(world, PulseTicks)[0].Hit;

        var plain = CleansingFlame.DamageOnTick(PulseTicks, hot);
        Assert.NotEqual(plain, first.Damage);
        Assert.Equal((int)MathF.Round(plain * (1f + Balance.Default.StatusEffects.Marked.Bonus), MidpointRounding.AwayFromZero), first.Damage);
    }
}
