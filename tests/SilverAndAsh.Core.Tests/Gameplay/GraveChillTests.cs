using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Issue #508: Grave Chill slows how a ghoul, a bat and Gorm cover ground - never their wind-ups. The tests chill an
/// enemy the way a weapon does (<see cref="GameWorld.ApplyStatus"/>, between two ticks), tick the world and measure the
/// ground it really covers, tick by tick: the length of its way on the floor plane, not a straight line.
/// </summary>
public class GraveChillTests
{
    private const float Dt = SimulationClock.SecondsPerTick;
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly GraveChillBalance Chill = Balance.Default.StatusEffects.GraveChill;
    private static readonly int ChillTicks = GraveChill.DurationTicks(Chill);

    /// <summary>The share of its speed a chilled enemy keeps, by the standard values.</summary>
    private static float Pace => 1f - Chill.Slow;

    /// <summary>A ghoul 10 m down a corridor - in sight, far enough to crawl for a while before it can strike.</summary>
    private const string GhoulCorridor = "P\n.\n.\n.\n.\nG";

    /// <summary>A ghoul 4 m away: it crawls a little and then winds up.</summary>
    private const string GhoulClose = "P\n.\nG";

    /// <summary>A ghoul 16 m down a corridor - for <see cref="FarSight"/>: room for a whole chill and the steps after it.</summary>
    private const string GhoulFar = "P\n.\n.\n.\n.\n.\n.\n.\nG";

    /// <summary>A 16 x 10 m room, the bat 14 m from the player - so far from its circle that it flies at full speed.</summary>
    private static readonly string BatRoom = string.Join('\n',
        "........",
        "........",
        "P......B",
        "........",
        "........");

    /// <summary>The same 30 m away - for <see cref="FarSight"/>: still far from its circle when a whole chill is over.</summary>
    private static readonly string BatFar = string.Join('\n',
        "................",
        "................",
        "P..............B",
        "................",
        "................");

    /// <summary>Enemies that see far - only to leave room for a whole chill before they reach the player.</summary>
    private static readonly Balance FarSight = Balance.Default with
    {
        Ghoul = Balance.Default.Ghoul with { SightRange = 40f },
        Bat = Balance.Default.Bat with { SightRange = 40f },
    };

    /// <summary>A Gorm who never attacks - to look at his walking alone.</summary>
    private static readonly Balance NoAttacks = Balance.Default with { Gorm = Balance.Default.Gorm with { NearRange = 0f, FarRange = 0f } };

    private static float Flat(Vector3 from, Vector3 to) => FlatDistance(from, to);

    /// <summary>Where <paramref name="enemy"/> stands (the enemy interface has no position of its own).</summary>
    private static Vector3 Where(IEnemy enemy) => enemy switch
    {
        Ghoul ghoul => ghoul.Position,
        Bat bat => bat.Position,
        Gorm gorm => gorm.Position,
        _ => throw new ArgumentOutOfRangeException(nameof(enemy)),
    };

    /// <summary>
    /// Ticks the world <paramref name="ticks"/> times and adds up the ground <paramref name="enemy"/> covers on the floor
    /// plane, tick by tick. If <paramref name="chilled"/>, it is chilled once before the first tick.
    /// </summary>
    private static float Way(GameWorld world, IEnemy enemy, int ticks, bool chilled)
    {
        if (chilled)
        {
            world.ApplyStatus(enemy, StatusKind.GraveChill, world.Player);
        }

        var way = 0f;
        for (var tick = 0; tick < ticks; tick++)
        {
            var before = Where(enemy);
            world.Tick(PlayerInput.None);
            way += Flat(before, Where(enemy));
        }

        return way;
    }

    /// <summary>
    /// Chills <paramref name="enemy"/> once, ticks the world until the chill is over and returns the ground covered on
    /// its last chilled tick and on the first tick after it.
    /// </summary>
    private static (float LastChilled, float FirstFree) StepsAroundTheEnd(GameWorld world, IEnemy enemy)
    {
        world.ApplyStatus(enemy, StatusKind.GraveChill, world.Player);
        Way(world, enemy, ChillTicks - 1, chilled: false);
        Assert.True(enemy.Status.Has(StatusKind.GraveChill), "The chill ended early - the test would prove nothing.");

        var lastChilled = Way(world, enemy, 1, chilled: false);
        Assert.False(enemy.Status.Has(StatusKind.GraveChill));
        var firstFree = Way(world, enemy, 1, chilled: false);
        return (lastChilled, firstFree);
    }

    /// <summary>
    /// Ticks the world, chilling <paramref name="enemy"/> anew before every tick if <paramref name="chilled"/> (as hits
    /// in a row would), until <paramref name="starts"/> reports a wind-up; then counts the ticks until
    /// <paramref name="ends"/> holds. Fails if the enemy never winds up or is not chilled at the end.
    /// </summary>
    private static int WindUpLength(
        GameWorld world, IEnemy enemy, bool chilled, Func<GameEvent, bool> starts, Func<GameWorld, bool> ends, int maxTicks)
    {
        int? start = null;
        for (var tick = 1; tick <= maxTicks; tick++)
        {
            if (chilled)
            {
                world.ApplyStatus(enemy, StatusKind.GraveChill, world.Player);
            }

            world.Tick(PlayerInput.None);
            if (start is null)
            {
                start = world.Events.Any(starts) ? tick : null;
                continue;
            }

            if (ends(world))
            {
                Assert.Equal(chilled, enemy.Status.Has(StatusKind.GraveChill));
                return tick - start.Value;
            }
        }

        throw new Xunit.Sdk.XunitException(start is null ? "It never wound up." : "Its wind-up never ended.");
    }

    // --- The rule alone ---

    [Fact]
    public void Without_a_chill_the_pace_is_full_and_with_one_it_is_reduced_by_the_slow()
    {
        var status = new StatusEffects();
        Assert.Equal(1f, GraveChill.Pace(status, Chill));

        status.Apply(StatusKind.GraveChill, ChillTicks, sourcePlayerId: 1);
        Assert.Equal(1f - Chill.Slow, GraveChill.Pace(status, Chill));

        // Other effects do not slow.
        var marked = new StatusEffects();
        marked.Apply(StatusKind.Marked, ChillTicks, sourcePlayerId: 1);
        marked.Apply(StatusKind.CleansingFlame, ChillTicks, sourcePlayerId: 1);
        Assert.Equal(1f, GraveChill.Pace(marked, Chill));
    }

    [Fact]
    public void A_chill_lasts_its_seconds_and_ends_with_an_event()
    {
        var world = World(GhoulCorridor);
        var ghoul = world.Ghouls[0];
        world.ApplyStatus(ghoul, StatusKind.GraveChill, world.Player);

        var events = Run(world, PlayerInput.None, ChillTicks - 1);
        Assert.True(ghoul.Status.Has(StatusKind.GraveChill));
        Assert.DoesNotContain(events, e => e is StatusEnded);

        events = Run(world, PlayerInput.None, 1);
        Assert.False(ghoul.Status.Has(StatusKind.GraveChill));
        Assert.Contains(new StatusEnded(ghoul.Id, StatusKind.GraveChill), events);
        Assert.Equal(SimulationClock.SecondsToTicks(Chill.Seconds), ChillTicks);
    }

    // --- Ghoul ---

    [Fact]
    public void A_chilled_ghoul_crawls_slower_by_the_slow()
    {
        var ticks = Second / 2;
        var free = World(GhoulCorridor);
        var cold = World(GhoulCorridor);

        var freeWay = Way(free, free.Ghouls[0], ticks, chilled: false);
        var coldWay = Way(cold, cold.Ghouls[0], ticks, chilled: true);

        Assert.Equal(Balance.Default.Ghoul.Speed * Dt * ticks, freeWay, 0.001f);
        Assert.Equal(freeWay * Pace, coldWay, 0.001f);
    }

    [Fact]
    public void A_chilled_ghoul_winds_up_as_long_as_one_without()
    {
        int Length(bool chilled)
        {
            var world = new GameWorld(LevelParser.Parse(GhoulClose), balance: Tough);
            var ghoul = world.Ghouls[0];
            return WindUpLength(world, ghoul, chilled, e => e is GhoulWindUp, _ => world.Events.Any(e => e is GhoulStruck), 3 * Second);
        }

        Assert.Equal(SimulationClock.SecondsToTicks(Balance.Default.Ghoul.WindUpTime), Length(chilled: false));
        Assert.Equal(Length(chilled: false), Length(chilled: true));
    }

    [Fact]
    public void A_ghoul_crawls_at_full_speed_again_once_the_chill_is_over()
    {
        var world = new GameWorld(LevelParser.Parse(GhoulFar), balance: FarSight);

        var (lastChilled, firstFree) = StepsAroundTheEnd(world, world.Ghouls[0]);

        Assert.Equal(Balance.Default.Ghoul.Speed * Pace * Dt, lastChilled, 0.0001f);
        Assert.Equal(Balance.Default.Ghoul.Speed * Dt, firstFree, 0.0001f);
    }

    // --- Bat ---

    [Fact]
    public void A_chilled_bat_flies_slower_by_the_slow()
    {
        var ticks = Second / 2;
        var free = World(BatRoom);
        var cold = World(BatRoom);

        var freeWay = Way(free, free.Bats[0], ticks, chilled: false);
        var coldWay = Way(cold, cold.Bats[0], ticks, chilled: true);

        Assert.Equal(Balance.Default.Bat.FlySpeed * Dt * ticks, freeWay, 0.001f);
        Assert.Equal(freeWay * Pace, coldWay, 0.001f);
    }

    [Fact]
    public void A_chilled_bat_screeches_as_long_as_one_without()
    {
        int Length(bool chilled)
        {
            var world = new GameWorld(LevelParser.Parse(BatRoom), balance: Tough);
            var bat = world.Bats[0];
            return WindUpLength(world, bat, chilled, e => e is BatScreeched, _ => bat.State == BatState.Diving, 8 * Second);
        }

        Assert.Equal(SimulationClock.SecondsToTicks(Balance.Default.Bat.ScreechTime), Length(chilled: false));
        Assert.Equal(Length(chilled: false), Length(chilled: true));
    }

    [Fact]
    public void A_bat_flies_at_full_speed_again_once_the_chill_is_over()
    {
        var world = new GameWorld(LevelParser.Parse(BatFar), balance: FarSight);

        var (lastChilled, firstFree) = StepsAroundTheEnd(world, world.Bats[0]);

        Assert.Equal(Balance.Default.Bat.FlySpeed * Pace * Dt, lastChilled, 0.0001f);
        Assert.Equal(Balance.Default.Bat.FlySpeed * Dt, firstFree, 0.0001f);
    }

    // --- Gorm ---

    [Fact]
    public void A_chilled_gorm_walks_slower_by_the_slow()
    {
        var ticks = Second / 2;
        var free = GormHallWorld(balance: NoAttacks);
        var cold = GormHallWorld(balance: NoAttacks);
        EnterGormHall(free);
        EnterGormHall(cold);

        var freeWay = Way(free, free.Gorm!, ticks, chilled: false);
        var coldWay = Way(cold, cold.Gorm!, ticks, chilled: true);

        Assert.Equal(Balance.Default.Gorm.Speed * Dt * ticks, freeWay, 0.001f);
        Assert.Equal(freeWay * Pace, coldWay, 0.001f);
    }

    [Fact]
    public void A_chilled_gorm_winds_up_as_long_as_one_without()
    {
        (GormAttack Attack, int Ticks) Length(bool chilled)
        {
            var world = GormHallWorld(balance: Tough);
            EnterGormHall(world);
            var gorm = world.Gorm!;
            GormAttack? attack = null;
            bool Starts(GameEvent e)
            {
                if (e is GormWindUp windUp)
                {
                    attack = windUp.Attack;
                    return true;
                }

                return false;
            }

            var ticks = WindUpLength(world, gorm, chilled, Starts, _ => gorm.State != GormState.WindingUp, 10 * Second);
            return (attack!.Value, ticks);
        }

        int Expected(GormAttack attack) => Math.Max(1, SimulationClock.SecondsToTicks(attack switch
        {
            GormAttack.Hammer => Balance.Default.Gorm.HammerWindUp,
            GormAttack.Swing => Balance.Default.Gorm.SwingWindUp,
            _ => Balance.Default.Gorm.ThrowWindUp,
        }));

        var free = Length(chilled: false);
        var cold = Length(chilled: true);
        Assert.Equal(Expected(free.Attack), free.Ticks);
        Assert.Equal(Expected(cold.Attack), cold.Ticks);
    }

    [Fact]
    public void Gorm_walks_at_full_speed_again_once_the_chill_is_over()
    {
        var world = GormHallWorld(balance: NoAttacks);
        EnterGormHall(world);

        var (lastChilled, firstFree) = StepsAroundTheEnd(world, world.Gorm!);

        Assert.Equal(Balance.Default.Gorm.Speed * Pace * Dt, lastChilled, 0.0001f);
        Assert.Equal(Balance.Default.Gorm.Speed * Dt, firstFree, 0.0001f);
    }

    private static GameWorld World(string map) => new(LevelParser.Parse(map));
}
