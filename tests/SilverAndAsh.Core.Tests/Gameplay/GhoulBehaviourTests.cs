using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class GhoulBehaviourTests
{
    private static readonly int WindUpTicks = SimulationClock.SecondsToTicks(Tuning.Ghoul.WindUpTime);
    private static readonly int RecoverTicks = SimulationClock.SecondsToTicks(Tuning.Ghoul.RecoverTime);
    private static readonly int Second = SimulationClock.TicksPerSecond;

    [Fact]
    public void It_lurks_while_a_wall_hides_the_player()
    {
        // Player at x = 1, ghoul at x = 9: close enough (8 m), but the wall at x = 4..6 is in between.
        var world = new GameWorld(LevelParser.Parse("P.#.G"));
        var start = world.Ghouls[0].Position;

        Run(world, PlayerInput.None, 2 * Second);

        Assert.Equal(GhoulState.Lurking, world.Ghouls[0].State);
        Assert.Equal(start, world.Ghouls[0].Position);
    }

    [Fact]
    public void It_lurks_while_the_player_is_too_far_away()
    {
        // 16 m: farther than the sight range of 12 m.
        var world = new GameWorld(LevelParser.Parse("P.......G"));

        Run(world, PlayerInput.None, 2 * Second);

        Assert.Equal(GhoulState.Lurking, world.Ghouls[0].State);
    }

    [Fact]
    public void It_notices_a_visible_player_nearby_and_crawls_towards_them()
    {
        var world = new GameWorld(LevelParser.Parse("P...G"));             // 8 m, nothing in between
        var startDistance = Distance(world);

        Run(world, PlayerInput.None, Second / 2);

        Assert.Equal(GhoulState.Chasing, world.Ghouls[0].State);
        Assert.Equal(startDistance - Tuning.Ghoul.Speed / 2f, Distance(world), 0.3f);
    }

    [Fact]
    public void A_hit_wakes_it_up_even_from_far_away()
    {
        var world = new GameWorld(LevelParser.Parse("P.......G"));

        world.Ghouls[0].TakeDamage(10, []);
        Run(world, PlayerInput.None, 1);

        Assert.Equal(GhoulState.Chasing, world.Ghouls[0].State);
    }

    [Fact]
    public void It_finds_its_way_around_a_corner()
    {
        // The ghoul (top right) cannot see the player (bottom left) through the wall row - a hit wakes it up.
        // The way leads along the top row and down the left column: about 12 m.
        var world = new GameWorld(LevelParser.Parse("....G\n.####\nP...."));
        world.Ghouls[0].TakeDamage(10, []);

        var reached = RunUntil(world, PlayerInput.None, 6 * Second, w => w.Events.Any(e => e is GhoulWindUp));

        Assert.True(reached, $"Did not reach the player, stuck at {world.Ghouls[0].Position}.");
    }

    [Fact]
    public void After_winding_up_the_blow_hits_for_its_damage()
    {
        var world = new GameWorld(LevelParser.Parse("P..G"));
        Assert.True(RunUntil(world, PlayerInput.None, 3 * Second, w => w.Events.Any(e => e is GhoulWindUp)));
        Assert.Equal(GhoulState.WindingUp, world.Ghouls[0].State);

        Run(world, PlayerInput.None, WindUpTicks - 1);
        Assert.Equal(Tuning.Combat.PlayerMaxHealth, world.Player.Health);

        world.Tick(PlayerInput.None);

        Assert.Equal(Tuning.Combat.PlayerMaxHealth - Tuning.Ghoul.AttackDamage, world.Player.Health);
        Assert.Contains(new PlayerHit(world.Player.Id, Tuning.Ghoul.AttackDamage), world.Events);
        Assert.Contains(new GhoulStruck(world.Ghouls[0].Id, Hit: true), world.Events);
    }

    [Fact]
    public void Stepping_back_during_the_wind_up_makes_the_blow_miss()
    {
        // The player stands in the middle of a corridor and steps away from the ghoul (to the -X side, which is
        // "right" when looking along +Z) as soon as it draws back.
        var world = new GameWorld(LevelParser.Parse("...P...G"));
        Assert.True(RunUntil(world, PlayerInput.None, 3 * Second, w => w.Events.Any(e => e is GhoulWindUp)));

        var seen = new List<GameEvent>();
        for (var i = 0; i < WindUpTicks; i++)
        {
            world.Tick(PlayerInput.None with { Move = new Vector2(1f, 0f) });
            seen.AddRange(world.Events);
        }

        Assert.Contains(new GhoulStruck(world.Ghouls[0].Id, Hit: false), seen);
        Assert.Equal(Tuning.Combat.PlayerMaxHealth, world.Player.Health);
    }

    [Fact]
    public void The_blow_reaches_farther_than_where_it_starts_a_small_step_back_is_not_enough()
    {
        // Wish from playtesting: the player is fast, so the blow reaches farther (HitReach) than the distance at
        // which the ghoul starts it (AttackReach). One short step back during the wind-up still gets you hit.
        var world = new GameWorld(LevelParser.Parse("...P...G"));
        Assert.True(RunUntil(world, PlayerInput.None, 3 * Second, w => w.Events.Any(e => e is GhoulWindUp)));

        Run(world, PlayerInput.None with { Move = new Vector2(1f, 0f) }, 6);    // a short step away
        Run(world, PlayerInput.None, WindUpTicks - 7);
        var distance = Distance(world);
        Assert.InRange(distance, Tuning.Ghoul.AttackReach, Tuning.Ghoul.HitReach);   // the test only means something here

        world.Tick(PlayerInput.None);

        Assert.Contains(new GhoulStruck(world.Ghouls[0].Id, Hit: true), world.Events);
    }

    [Fact]
    public void After_a_blow_it_rests_before_the_next_one()
    {
        var world = new GameWorld(LevelParser.Parse("P..G"));
        Assert.True(RunUntil(world, PlayerInput.None, 3 * Second, w => w.Events.Any(e => e is PlayerHit)));

        // Rest, then a fresh wind-up: no second blow before that.
        var ticksUntilNextHit = 0;
        do
        {
            world.Tick(PlayerInput.None);
            ticksUntilNextHit++;
        }
        while (!world.Events.Any(e => e is PlayerHit) && ticksUntilNextHit < 5 * Second);

        Assert.Equal(RecoverTicks + WindUpTicks, ticksUntilNextHit);
    }

    [Fact]
    public void A_hit_during_the_wind_up_does_not_stop_the_blow()
    {
        var world = new GameWorld(LevelParser.Parse("P..G"));
        Assert.True(RunUntil(world, PlayerInput.None, 3 * Second, w => w.Events.Any(e => e is GhoulWindUp)));

        world.Ghouls[0].TakeDamage(10, []);
        Run(world, PlayerInput.None, WindUpTicks);

        Assert.Equal(Tuning.Combat.PlayerMaxHealth - Tuning.Ghoul.AttackDamage, world.Player.Health);
    }

    [Fact]
    public void Two_ghouls_do_not_crawl_into_each_other()
    {
        var world = new GameWorld(LevelParser.Parse("P....GG"));
        var closest = float.MaxValue;

        for (var i = 0; i < 4 * Second; i++)
        {
            world.Tick(PlayerInput.None);
            var a = world.Ghouls[0].Position;
            var b = world.Ghouls[1].Position;
            closest = MathF.Min(closest, new Vector2(a.X - b.X, a.Z - b.Z).Length());
        }

        Assert.True(closest >= 2f * Tuning.Ghoul.Radius - 0.05f, $"Came as close as {closest:0.00} m.");
    }

    [Fact]
    public void A_dead_ghoul_does_nothing()
    {
        var world = new GameWorld(LevelParser.Parse("P..G"));
        world.Ghouls[0].TakeDamage(Tuning.Ghoul.MaxHealth, []);
        var position = world.Ghouls[0].Position;

        var anything = RunUntil(world, PlayerInput.None, 3 * Second, w => w.Events.Any(e => e is GhoulWindUp or PlayerHit));

        Assert.False(anything);
        Assert.Equal(position, world.Ghouls[0].Position);
    }

    [Fact]
    public void The_player_loses_health_never_below_zero()
    {
        var player = new Player(LevelParser.Parse("P"));
        var events = new List<GameEvent>();

        player.TakeDamage(30, events);
        player.TakeDamage(500, events);

        Assert.Equal(0, player.Health);
        // Since #79 reaching 0 also reports the death.
        Assert.Equal([new PlayerHit(player.Id, 30), new PlayerHit(player.Id, 70), new PlayerDied(player.Id)], events);
    }

    private static float Distance(GameWorld world)
    {
        var a = world.Player.Position;
        var b = world.Ghouls[0].Position;
        return new Vector2(a.X - b.X, a.Z - b.Z).Length();
    }

    private static void Run(GameWorld world, PlayerInput input, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            world.Tick(input);
        }
    }

    /// <summary>Ticks until <paramref name="condition"/> holds after a tick (true), or <paramref name="maxTicks"/> passed (false).</summary>
    private static bool RunUntil(GameWorld world, PlayerInput input, int maxTicks, Func<GameWorld, bool> condition)
    {
        for (var i = 0; i < maxTicks; i++)
        {
            world.Tick(input);
            if (condition(world))
            {
                return true;
            }
        }

        return false;
    }
}
