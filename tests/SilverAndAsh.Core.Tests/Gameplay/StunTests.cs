using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Issue #509: a stunned ghoul or bat stands still and attacks no more - a blow it draws back for breaks off; Gorm only
/// staggers, his announced attacks go on, and afterwards he is immune for a while. The tests stun an enemy the way a
/// weapon does (<see cref="GameWorld.ApplyStatus"/>, between two ticks), tick the world and look at what it really does,
/// tick by tick.
/// </summary>
public class StunTests
{
    private const float Dt = SimulationClock.SecondsPerTick;
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly StunBalance Values = Balance.Default.StatusEffects.Stun;
    private static readonly int StunTicks = Stun.DurationTicks(Values);
    private static readonly int StaggerTicks = Stun.GormStaggerTicks(Values);
    private static readonly int ImmuneTicks = Stun.GormImmuneTicks(Values);

    /// <summary>A ghoul 10 m down a corridor - in sight, far enough to crawl for a while before it can strike.</summary>
    private const string GhoulCorridor = "P\n.\n.\n.\n.\nG";

    /// <summary>A ghoul 4 m away: it crawls a little and then winds up.</summary>
    private const string GhoulClose = "P\n.\nG";

    /// <summary>A ghoul 16 m down a corridor - for <see cref="FarSight"/>: room for a whole stun and the steps after it.</summary>
    private const string GhoulFar = "P\n.\n.\n.\n.\n.\n.\n.\nG";

    /// <summary>A 16 x 10 m room, the bat 14 m from the player - so far from its circle that it flies at full speed.</summary>
    private static readonly string BatRoom = string.Join('\n',
        "........",
        "........",
        "P......B",
        "........",
        "........");

    /// <summary>The same 30 m away - for <see cref="FarSight"/>: still far from its circle when a whole stun is over.</summary>
    private static readonly string BatFar = string.Join('\n',
        "................",
        "................",
        "P..............B",
        "................",
        "................");

    /// <summary>Enemies that see far - only to leave room for a whole stun before they reach the player.</summary>
    private static readonly Balance FarSight = Balance.Default with
    {
        Ghoul = Balance.Default.Ghoul with { SightRange = 40f },
        Bat = Balance.Default.Bat with { SightRange = 40f },
    };

    /// <summary>A Gorm who never attacks - to look at his walking alone.</summary>
    private static readonly Balance NoAttacks = Balance.Default with { Gorm = Balance.Default.Gorm with { NearRange = 0f, FarRange = 0f } };

    /// <summary>Where <paramref name="enemy"/> stands (the enemy interface has no position of its own).</summary>
    private static Vector3 Where(IEnemy enemy) => enemy switch
    {
        Ghoul ghoul => ghoul.Position,
        Bat bat => bat.Position,
        Gorm gorm => gorm.Position,
        _ => throw new ArgumentOutOfRangeException(nameof(enemy)),
    };

    /// <summary>Ticks the world once and returns the ground <paramref name="enemy"/> covered on the floor plane.</summary>
    private static float Step(GameWorld world, IEnemy enemy)
    {
        var before = Where(enemy);
        world.Tick(PlayerInput.None);
        return FlatDistance(before, Where(enemy));
    }

    /// <summary>The ground <paramref name="enemy"/> covers tick by tick over <paramref name="ticks"/> ticks, one entry per tick.</summary>
    private static List<float> Steps(GameWorld world, IEnemy enemy, int ticks) =>
        Enumerable.Range(0, ticks).Select(_ => Step(world, enemy)).ToList();

    /// <summary>Ticks the world <paramref name="ticks"/> times and collects every event.</summary>
    private static List<GameEvent> Ticks(GameWorld world, int ticks) => Run(world, PlayerInput.None, ticks);

    /// <summary>Stuns <paramref name="enemy"/> as a weapon would and checks that it took hold.</summary>
    private static void Stunned(GameWorld world, IEnemy enemy) =>
        Assert.True(world.ApplyStatus(enemy, StatusKind.Stun, world.Player), "The stun did not take hold.");

    private static GameWorld World(string map, Balance? balance = null) => new(LevelParser.Parse(map), balance: balance);

    // --- The rule alone ---

    [Fact]
    public void The_durations_come_from_the_values()
    {
        Assert.Equal(SimulationClock.SecondsToTicks(Values.Seconds), StunTicks);
        Assert.Equal(SimulationClock.SecondsToTicks(Values.GormStaggerSeconds), StaggerTicks);
        Assert.Equal(SimulationClock.SecondsToTicks(Values.GormImmuneSeconds), ImmuneTicks);

        // The tests below would prove little if the stagger were as long as a stun.
        Assert.True(StaggerTicks < StunTicks, "The stagger is meant to be shorter than a stun.");
    }

    [Fact]
    public void A_stun_or_a_stagger_shorter_than_a_tick_still_lasts_one()
    {
        var tiny = Values with { Seconds = Dt / 4f, GormStaggerSeconds = Dt / 4f };
        Assert.Equal(1, Stun.DurationTicks(tiny));
        Assert.Equal(1, Stun.GormStaggerTicks(tiny));
    }

    [Fact]
    public void Only_a_stun_holds_an_enemy()
    {
        var status = new StatusEffects();
        status.Apply(StatusKind.Marked, StunTicks, sourcePlayerId: 1);
        status.Apply(StatusKind.CleansingFlame, StunTicks, sourcePlayerId: 1);
        status.Apply(StatusKind.GraveChill, StunTicks, sourcePlayerId: 1);
        Assert.False(Stun.Holds(status));

        status.Apply(StatusKind.Stun, StunTicks, sourcePlayerId: 1);
        Assert.True(Stun.Holds(status));
    }

    [Fact]
    public void The_rule_refuses_missing_values()
    {
        Assert.Throws<ArgumentNullException>(() => Stun.DurationTicks(null!));
        Assert.Throws<ArgumentNullException>(() => Stun.GormStaggerTicks(null!));
        Assert.Throws<ArgumentNullException>(() => Stun.GormImmuneTicks(null!));
        Assert.Throws<ArgumentNullException>(() => Stun.Holds(null!));
        Assert.Throws<ArgumentNullException>(() => Stun.LandOnGorm(null!, Values));
        Assert.Throws<ArgumentNullException>(() => Stun.LandOnGorm(new StatusEffects(), null!));
    }

    [Fact]
    public void A_stun_on_gorm_staggers_him_and_makes_him_immune_until_well_after_the_stagger()
    {
        var status = new StatusEffects();
        Assert.Equal(StaggerTicks, Stun.LandOnGorm(status, Values));

        // Immune from the stagger on, for the stagger and the immune time after it - and not one tick longer.
        var events = new List<GameEvent>();
        for (var tick = 0; tick < StaggerTicks + ImmuneTicks - 1; tick++)
        {
            status.Tick(targetId: 1, events);
            Assert.True(status.IsImmuneTo(StatusKind.Stun));
            Assert.Equal(0, Stun.LandOnGorm(status, Values));
        }

        status.Tick(targetId: 1, events);
        Assert.False(status.IsImmuneTo(StatusKind.Stun));
        Assert.Empty(events);
        Assert.Equal(StaggerTicks, Stun.LandOnGorm(status, Values));
    }

    [Fact]
    public void An_immunity_needs_at_least_one_tick_and_a_shorter_one_never_cuts_a_longer_one_short()
    {
        var status = new StatusEffects();
        Assert.Throws<ArgumentOutOfRangeException>(() => status.MakeImmune(StatusKind.Stun, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => status.MakeImmune(StatusKind.Stun, -1));
        Assert.False(status.IsImmuneTo(StatusKind.Stun));

        status.MakeImmune(StatusKind.Stun, 3);
        status.MakeImmune(StatusKind.Stun, 1);
        var events = new List<GameEvent>();
        status.Tick(targetId: 1, events);
        status.Tick(targetId: 1, events);
        Assert.True(status.IsImmuneTo(StatusKind.Stun));
        Assert.False(status.IsImmuneTo(StatusKind.GraveChill));
        status.Tick(targetId: 1, events);
        Assert.False(status.IsImmuneTo(StatusKind.Stun));
        Assert.Empty(events);
    }

    // --- How long it lasts ---

    [Fact]
    public void A_stun_lasts_its_seconds_and_ends_with_an_event()
    {
        var world = World(GhoulCorridor);
        var ghoul = world.Ghouls[0];
        world.ApplyStatus(ghoul, StatusKind.Stun, world.Player);
        Assert.Contains(new StatusApplied(ghoul.Id, StatusKind.Stun, world.Player.Id), world.Events);

        var events = Ticks(world, StunTicks - 1);
        Assert.True(ghoul.Status.Has(StatusKind.Stun));
        Assert.DoesNotContain(events, e => e is StatusEnded);

        events = Ticks(world, 1);
        Assert.False(ghoul.Status.Has(StatusKind.Stun));
        Assert.Contains(new StatusEnded(ghoul.Id, StatusKind.Stun), events);
    }

    [Fact]
    public void A_stun_lasts_the_seconds_of_the_world_s_own_values()
    {
        // Not the standard seconds, and not those of another effect: the world must take the length from the stun's values.
        var longer = Tough with
        {
            StatusEffects = Tough.StatusEffects with { Stun = Values with { Seconds = Values.Seconds + 0.5f } },
        };
        var ticks = Stun.DurationTicks(longer.StatusEffects.Stun);
        Assert.NotEqual(StunTicks, ticks);
        Assert.NotEqual(SimulationClock.SecondsToTicks(longer.StatusEffects.GraveChill.Seconds), ticks);
        Assert.NotEqual(SimulationClock.SecondsToTicks(longer.StatusEffects.Marked.Seconds), ticks);

        var world = World(GhoulCorridor, longer);
        var ghoul = world.Ghouls[0];
        Stunned(world, ghoul);

        Ticks(world, ticks - 1);
        Assert.True(ghoul.Status.Has(StatusKind.Stun));
        Assert.Contains(new StatusEnded(ghoul.Id, StatusKind.Stun), Ticks(world, 1));
    }

    [Fact]
    public void A_second_stun_on_a_ghoul_starts_its_time_anew()
    {
        // Only Gorm grows immune: a ghoul can be stunned again and again.
        var world = World(GhoulCorridor);
        var ghoul = world.Ghouls[0];
        Stunned(world, ghoul);
        Ticks(world, StunTicks - 1);
        Stunned(world, ghoul);

        Ticks(world, StunTicks - 1);
        Assert.True(ghoul.Status.Has(StatusKind.Stun));
        Assert.Contains(new StatusEnded(ghoul.Id, StatusKind.Stun), Ticks(world, 1));
    }

    [Fact]
    public void A_dead_enemy_takes_no_stun()
    {
        var world = World(GhoulCorridor);
        var ghoul = world.Ghouls[0];
        ghoul.TakeDamage(ghoul.Health, new List<GameEvent>());
        Assert.True(ghoul.IsDead);

        Assert.False(world.ApplyStatus(ghoul, StatusKind.Stun, world.Player));
        Assert.False(ghoul.Status.Has(StatusKind.Stun));
        Assert.DoesNotContain(world.Events, e => e is StatusApplied);
    }

    // --- Ghoul ---

    [Fact]
    public void A_stunned_ghoul_stands_still_while_the_stun_lasts()
    {
        var world = World(GhoulCorridor);
        var ghoul = world.Ghouls[0];
        Stunned(world, ghoul);

        Assert.All(Steps(world, ghoul, StunTicks), step => Assert.Equal(0f, step));
    }

    [Fact]
    public void A_ghoul_stunned_during_its_wind_up_does_not_strike_and_winds_up_anew_afterwards()
    {
        var world = World(GhoulClose, Tough);
        var ghoul = world.Ghouls[0];
        Assert.True(RunUntil(world, PlayerInput.None, 3 * Second, w => w.Events.Any(e => e is GhoulWindUp)), "It never wound up.");
        Assert.Equal(GhoulState.WindingUp, ghoul.State);

        Stunned(world, ghoul);
        var during = Ticks(world, StunTicks);
        Assert.DoesNotContain(during, e => e is GhoulStruck);
        Assert.NotEqual(GhoulState.WindingUp, ghoul.State);
        Assert.False(ghoul.Status.Has(StatusKind.Stun));

        // After the stun it acts normally again: a whole new wind-up, then the blow - nothing of the old one is left.
        var windUp = SimulationClock.SecondsToTicks(Balance.Default.Ghoul.WindUpTime);
        int? windUpAt = null;
        for (var tick = 1; tick <= 3 * Second; tick++)
        {
            var events = Ticks(world, 1);
            windUpAt ??= events.Any(e => e is GhoulWindUp) ? tick : null;
            if (events.Any(e => e is GhoulStruck))
            {
                Assert.NotNull(windUpAt);
                Assert.Equal(windUp, tick - windUpAt.Value);
                return;
            }
        }

        throw new Xunit.Sdk.XunitException("It never struck after the stun.");
    }

    [Fact]
    public void A_ghoul_crawls_at_full_speed_again_once_the_stun_is_over()
    {
        var world = World(GhoulFar, FarSight);
        var ghoul = world.Ghouls[0];
        Assert.True(Step(world, ghoul) > 0f, "It did not crawl to begin with - the test would prove nothing.");

        Stunned(world, ghoul);
        var steps = Steps(world, ghoul, StunTicks + 1);

        Assert.All(steps.Take(StunTicks), step => Assert.Equal(0f, step));
        Assert.Equal(Balance.Default.Ghoul.Speed * Dt, steps[^1], 0.0001f);
    }

    // --- Bat ---

    [Fact]
    public void A_stunned_bat_hangs_still_in_the_air_and_does_not_fall()
    {
        var world = World(BatRoom);
        var bat = world.Bats[0];
        Assert.True(Step(world, bat) > 0f, "It did not fly to begin with - the test would prove nothing.");

        Stunned(world, bat);
        var before = bat.Position;
        var events = Ticks(world, StunTicks);

        Assert.Equal(before, bat.Position);
        Assert.DoesNotContain(events, e => e is BatSpitWindUp or BatSpat or BatScreeched or BatBit);
    }

    [Fact]
    public void A_bat_flies_at_full_speed_again_once_the_stun_is_over()
    {
        var world = World(BatFar, FarSight);
        var bat = world.Bats[0];
        Assert.True(Step(world, bat) > 0f, "It did not fly to begin with - the test would prove nothing.");

        Stunned(world, bat);
        var steps = Steps(world, bat, StunTicks + 1);

        Assert.All(steps.Take(StunTicks), step => Assert.Equal(0f, step));
        Assert.Equal(Balance.Default.Bat.FlySpeed * Dt, steps[^1], 0.0001f);
    }

    [Fact]
    public void A_bat_stunned_during_its_dive_breaks_it_off_and_rises_afterwards()
    {
        var world = World(BatRoom, Tough);
        var bat = world.Bats[0];
        Assert.True(RunUntil(world, PlayerInput.None, 8 * Second, _ => bat.State == BatState.Diving), "It never dived.");
        var top = bat.Position.Y;
        Assert.True(RunUntil(world, PlayerInput.None, Second, _ => bat.Position.Y < top - 0.2f), "It never came down.");
        Assert.Equal(BatState.Diving, bat.State);

        Stunned(world, bat);
        var before = bat.Position;
        var events = Ticks(world, StunTicks);

        Assert.Equal(before, bat.Position);
        Assert.Equal(BatState.Rising, bat.State);
        Assert.DoesNotContain(events, e => e is BatBit or BatDiveMissed);

        // Once free it climbs back up to its circle.
        world.Tick(PlayerInput.None);
        Assert.Equal(Balance.Default.Bat.ClimbSpeed * Dt, bat.Position.Y - before.Y, 0.00001f);
    }

    [Fact]
    public void A_bat_stunned_during_its_screech_does_not_dive_and_screeches_anew_afterwards()
    {
        var world = World(BatRoom, Tough);
        var bat = world.Bats[0];
        Assert.True(RunUntil(world, PlayerInput.None, 8 * Second, w => w.Events.Any(e => e is BatScreeched)), "It never screeched.");

        Stunned(world, bat);
        Ticks(world, StunTicks);
        Assert.Equal(BatState.Circling, bat.State);

        // A dive comes again only after a whole new screech.
        var screech = SimulationClock.SecondsToTicks(Balance.Default.Bat.ScreechTime);
        int? screechAt = null;
        for (var tick = 1; tick <= 8 * Second; tick++)
        {
            var events = Ticks(world, 1);
            screechAt ??= events.Any(e => e is BatScreeched) ? tick : null;
            if (bat.State == BatState.Diving)
            {
                Assert.NotNull(screechAt);
                Assert.Equal(screech, tick - screechAt.Value);
                return;
            }
        }

        throw new Xunit.Sdk.XunitException("It never dived after the stun.");
    }

    [Fact]
    public void A_bat_stunned_while_it_glows_to_spit_does_not_spit_and_warns_anew_afterwards()
    {
        var world = World(BatRoom, Tough);
        var bat = world.Bats[0];
        Assert.True(RunUntil(world, PlayerInput.None, 30 * Second, w => w.Events.Any(e => e is BatSpitWindUp)), "It never got ready to spit.");

        Stunned(world, bat);
        Assert.DoesNotContain(Ticks(world, StunTicks), e => e is BatSpat);
        Assert.Equal(0f, bat.SpitWindUpProgress);

        // The next spit comes only after a whole new warning.
        int? warnedAt = null;
        for (var tick = 1; tick <= 30 * Second; tick++)
        {
            var events = Ticks(world, 1);
            warnedAt ??= events.Any(e => e is BatSpitWindUp) ? tick : null;
            if (events.Any(e => e is BatSpat))
            {
                Assert.NotNull(warnedAt);
                Assert.Equal(SimulationClock.SecondsToTicks(Balance.Default.Bat.SpitWindUpTime), tick - warnedAt.Value);
                return;
            }
        }

        throw new Xunit.Sdk.XunitException("It never spat after the stun.");
    }

    // --- Gorm ---

    [Fact]
    public void Gorm_only_staggers_for_his_own_short_time_and_then_walks_on_at_full_speed()
    {
        var world = GormHallWorld(balance: NoAttacks);
        EnterGormHall(world);
        var gorm = world.Gorm!;
        Assert.True(Step(world, gorm) > 0f, "He did not walk to begin with - the test would prove nothing.");

        Stunned(world, gorm);
        var events = new List<GameEvent>();
        var steps = new List<float>();
        for (var tick = 0; tick < StaggerTicks + 1; tick++)
        {
            steps.Add(Step(world, gorm));
            events.AddRange(world.Events);
        }

        Assert.All(steps.Take(StaggerTicks), step => Assert.Equal(0f, step));
        Assert.Equal(Balance.Default.Gorm.Speed * Dt, steps[^1], 0.0001f);
        Assert.Single(events, e => e is StatusEnded { Kind: StatusKind.Stun });
        Assert.False(gorm.Status.Has(StatusKind.Stun));
    }

    [Fact]
    public void Gorm_staggers_and_stays_immune_as_long_as_the_world_s_own_values_say()
    {
        // Other values than the standard ones, the stagger also unlike the stun of a ghoul: the world must take each of them.
        var own = NoAttacks with
        {
            StatusEffects = NoAttacks.StatusEffects with
            {
                Stun = Values with { Seconds = 1f, GormStaggerSeconds = 0.5f, GormImmuneSeconds = 2f },
            },
        };
        var stagger = Stun.GormStaggerTicks(own.StatusEffects.Stun);
        var immune = Stun.GormImmuneTicks(own.StatusEffects.Stun);
        Assert.NotEqual(StaggerTicks, stagger);
        Assert.NotEqual(Stun.DurationTicks(own.StatusEffects.Stun), stagger);
        Assert.NotEqual(ImmuneTicks, immune);

        var world = GormHallWorld(balance: own);
        EnterGormHall(world);
        var gorm = world.Gorm!;
        Stunned(world, gorm);

        Ticks(world, stagger - 1);
        Assert.True(gorm.Status.Has(StatusKind.Stun));
        Assert.Contains(new StatusEnded(gorm.Id, StatusKind.Stun), Ticks(world, 1));

        Ticks(world, immune - 1);
        Assert.False(world.ApplyStatus(gorm, StatusKind.Stun, world.Player));
        Ticks(world, 1);
        Stunned(world, gorm);
    }

    [Fact]
    public void A_second_stun_on_gorm_while_he_is_immune_does_nothing_and_reports_nothing()
    {
        var world = GormHallWorld(balance: NoAttacks);
        EnterGormHall(world);
        var gorm = world.Gorm!;
        Stunned(world, gorm);

        // During the stagger: it does not stretch it.
        Ticks(world, StaggerTicks - 1);
        Assert.False(world.ApplyStatus(gorm, StatusKind.Stun, world.Player));
        Assert.DoesNotContain(world.Events, e => e is StatusApplied);
        Assert.Contains(new StatusEnded(gorm.Id, StatusKind.Stun), Ticks(world, 1));

        // After the stagger, to the last immune tick: no stun, no event, and he walks on.
        Ticks(world, ImmuneTicks - 1);
        Assert.False(world.ApplyStatus(gorm, StatusKind.Stun, world.Player));
        Assert.DoesNotContain(world.Events, e => e is StatusApplied);
        Assert.False(gorm.Status.Has(StatusKind.Stun));
        Assert.Equal(Balance.Default.Gorm.Speed * Dt, Step(world, gorm), 0.0001f);

        // Once the immunity is over a stun takes hold again, with its event, and he staggers once more.
        Stunned(world, gorm);
        Assert.Contains(new StatusApplied(gorm.Id, StatusKind.Stun, world.Player.Id), world.Events);
        Assert.Equal(0f, Step(world, gorm));
    }

    [Fact]
    public void Gorm_immune_to_stuns_still_takes_other_effects()
    {
        var world = GormHallWorld(balance: NoAttacks);
        EnterGormHall(world);
        var gorm = world.Gorm!;
        Stunned(world, gorm);
        Ticks(world, 1);

        Assert.True(world.ApplyStatus(gorm, StatusKind.GraveChill, world.Player));
        Assert.True(gorm.Status.Has(StatusKind.GraveChill));
    }

    [Fact]
    public void Gorm_stunned_during_his_announcement_still_attacks_as_one_without()
    {
        // The same fight twice; in the second, a stun lands right as he announces his first attack.
        static List<(GormState State, Vector3 Position, bool Attacked)> Trace(bool stunned)
        {
            var world = GormHallWorld(balance: Tough);
            EnterGormHall(world);
            var gorm = world.Gorm!;
            Assert.True(RunUntil(world, PlayerInput.None, 10 * Second, w => w.Events.Any(e => e is GormWindUp)), "He never wound up.");
            if (stunned)
            {
                Stunned(world, gorm);
            }

            var trace = new List<(GormState, Vector3, bool)>();
            for (var tick = 0; tick < 3 * Second; tick++)
            {
                world.Tick(PlayerInput.None);
                var attacked = world.Events.Any(e => e is GormSlammed or GormSwingStarted or GormThrew);
                trace.Add((gorm.State, gorm.Position, attacked));
                if (attacked)
                {
                    return trace;
                }
            }

            throw new Xunit.Sdk.XunitException("His attack never came.");
        }

        var free = Trace(stunned: false);
        Assert.Equal(free, Trace(stunned: true));
    }
}
