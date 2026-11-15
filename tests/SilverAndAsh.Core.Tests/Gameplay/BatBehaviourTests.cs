using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class BatBehaviourTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly int SpitTicks = SimulationClock.SecondsToTicks(Balance.Default.Bat.SpitInterval);
    private static readonly int SpitWindUpTicks = SimulationClock.SecondsToTicks(Balance.Default.Bat.SpitWindUpTime);
    private static readonly int ScreechTicks = SimulationClock.SecondsToTicks(Balance.Default.Bat.ScreechTime);

    /// <summary>A 14 x 14 m room: the player in the middle (7, 7), the bat in a corner above (1, 1) - 8.5 m away, in sight.</summary>
    private static readonly Level BigRoom = LevelParser.Parse(string.Join('\n',
        "B......",
        ".......",
        ".......",
        "...P...",
        ".......",
        ".......",
        "......."));

    [Fact]
    public void It_lurks_while_a_wall_hides_the_player()
    {
        var world = new GameWorld(LevelParser.Parse("P.#.B"));
        var bat = world.Bats[0];
        var start = bat.Position;

        var seen = Run(world, PlayerInput.None, 3 * Second);

        Assert.Equal(BatState.Lurking, bat.State);
        Assert.Equal(start, bat.Position);
        Assert.DoesNotContain(seen, e => e.Event is BatSpitWindUp or BatSpat or BatScreeched);
    }

    [Fact]
    public void It_lurks_while_the_player_is_too_far_away()
    {
        // 18 m along a straight corridor: in plain sight, but farther than its 15 m.
        var world = new GameWorld(LevelParser.Parse("P........B"));

        Run(world, PlayerInput.None, Second);

        Assert.Equal(BatState.Lurking, world.Bats[0].State);
    }

    [Fact]
    public void It_notices_a_player_it_can_see()
    {
        var world = new GameWorld(LevelParser.Parse("P...B"));

        world.Tick(PlayerInput.None);

        Assert.Equal(BatState.Circling, world.Bats[0].State);
    }

    [Fact]
    public void A_hit_wakes_it_even_without_sight()
    {
        var world = new GameWorld(LevelParser.Parse("P.#.B"));
        var bat = world.Bats[0];

        bat.TakeDamage(1, []);
        world.Tick(PlayerInput.None);

        Assert.NotEqual(BatState.Lurking, bat.State);
    }

    [Fact]
    public void It_circles_the_player_at_its_distance_and_height()
    {
        var world = new GameWorld(BigRoom);
        var bat = world.Bats[0];
        var centre = world.Player.Position;

        // Time to get there, then watch it until just before its first dive.
        Run(world, PlayerInput.None, 5 * Second / 2);
        var heights = new List<float>();
        var turned = 0f;
        var lastAngle = AngleAround(centre, bat.Position);
        for (var i = 0; i < 3 * Second; i++)
        {
            world.Tick(PlayerInput.None);
            var distance = FlatDistance(centre, bat.Position);
            Assert.InRange(distance, bat.CircleRadius - 0.6f, bat.CircleRadius + 0.6f);
            Assert.InRange(bat.Position.Y, Balance.Default.Bat.CircleHeightMin - 0.05f, Balance.Default.Bat.CircleHeightMax + 0.05f);
            heights.Add(bat.Position.Y);

            var angle = AngleAround(centre, bat.Position);
            turned += MathF.Abs(WrapAngle(angle - lastAngle));
            lastAngle = angle;
        }

        Assert.True(turned > MathF.PI / 2f, $"It only went {turned * 180f / MathF.PI:0} degrees around the player.");
        Assert.True(heights.Max() - heights.Min() > 0.3f, "It does not rise and sink while circling.");
    }

    [Fact]
    public void It_turns_round_every_few_seconds_while_circling()
    {
        var world = new GameWorld(BigRoom, seed: 3);
        var bat = world.Bats[0];
        var minTicks = SimulationClock.SecondsToTicks(Balance.Default.Bat.TurnRoundMin);
        var maxTicks = SimulationClock.SecondsToTicks(Balance.Default.Bat.TurnRoundMax);

        // Count only the ticks spent circling: screech, dive and rise pause the turning.
        var circlingTicks = 0;
        var direction = bat.CircleDirection;
        var turns = new List<int> { 0 };
        for (var tick = 0; tick < 10 * Second; tick++)
        {
            world.Tick(PlayerInput.None);
            if (bat.State == BatState.Circling)
            {
                circlingTicks++;
            }

            if (bat.CircleDirection != direction)
            {
                turns.Add(circlingTicks);
                direction = bat.CircleDirection;
            }
        }

        Assert.True(turns.Count >= 3, $"Only {turns.Count - 1} turns in 10 s.");
        Assert.All(turns.Zip(turns.Skip(1), (a, b) => b - a), gap => Assert.InRange(gap, minTicks, maxTicks));
    }

    [Fact]
    public void Every_bat_gets_its_own_circle()
    {
        var bats = Enumerable.Range(0, 20).Select(seed => new GameWorld(BigRoom, seed).Bats[0]).ToList();

        Assert.All(bats, bat => Assert.InRange(bat.CircleRadius, Balance.Default.Bat.CircleRadiusMin, Balance.Default.Bat.CircleRadiusMax));
        Assert.True(bats.Select(bat => bat.CircleRadius).Distinct().Count() > 10, "The distances hardly differ.");
        Assert.Contains(bats, bat => bat.CircleDirection > 0);
        Assert.Contains(bats, bat => bat.CircleDirection < 0);
    }

    [Fact]
    public void Bats_do_not_rise_and_sink_in_step()
    {
        // Two bats that notice the player in the same tick, at the same start height.
        var level = LevelParser.Parse(string.Join('\n', "B.....B", ".......", ".......", "...P..."));
        var outOfStep = 0;
        for (var seed = 0; seed < 10; seed++)
        {
            var world = new GameWorld(level, seed);
            Run(world, PlayerInput.None, 3 * Second / 2);
            if (MathF.Abs(world.Bats[0].Position.Y - world.Bats[1].Position.Y) > 0.1f)
            {
                outOfStep++;
            }
        }

        Assert.True(outOfStep >= 5, $"Only {outOfStep} of 10 pairs fly at different heights.");
    }

    [Fact]
    public void The_same_seed_gives_the_same_flight()
    {
        Assert.Equal(FlightAfterFiveSeconds(7), FlightAfterFiveSeconds(7));
    }

    [Fact]
    public void Other_seeds_give_other_flights()
    {
        var reference = FlightAfterFiveSeconds(7);

        Assert.Contains(Enumerable.Range(0, 5), seed => FlightAfterFiveSeconds(seed) != reference);
    }

    [Fact]
    public void A_restart_brings_a_new_seed()
    {
        var world = new GameWorld(BigRoom, seed: 7);

        Assert.NotEqual(world.Seed, world.Restarted().Seed);
    }

    private static Vector3 FlightAfterFiveSeconds(int seed)
    {
        var world = new GameWorld(BigRoom, seed);
        Run(world, PlayerInput.None, 5 * Second);
        return world.Bats[0].Position;
    }

    [Fact]
    public void It_spits_every_two_seconds_after_a_warning()
    {
        var world = new GameWorld(BigRoom);

        var seen = Run(world, PlayerInput.None, 5 * Second);

        var windUps = seen.Where(e => e.Event is BatSpitWindUp).Select(e => e.Tick).ToList();
        var spits = seen.Where(e => e.Event is BatSpat).Select(e => e.Tick).ToList();
        Assert.Equal(2, spits.Count);
        Assert.Equal(2, windUps.Count);
        Assert.Equal(SpitTicks, spits[1] - spits[0]);
        Assert.Equal(SpitWindUpTicks, spits[0] - windUps[0]);
        Assert.Equal(SpitWindUpTicks, spits[1] - windUps[1]);

        // Every spit is a blood bolt that flies off in the same tick.
        Assert.All(spits, tick => Assert.Contains(seen, e => e.Tick == tick && e.Event is ProjectileFired));
    }

    [Fact]
    public void The_warning_shows_how_far_the_spit_has_come()
    {
        var world = new GameWorld(BigRoom);
        var bat = world.Bats[0];

        Run(world, PlayerInput.None, 1);
        Assert.Equal(0f, bat.SpitWindUpProgress);

        var seen = RunUntil(world, PlayerInput.None, e => e is BatSpitWindUp, 3 * Second);
        Assert.NotEmpty(seen);
        Run(world, PlayerInput.None, SpitWindUpTicks / 2);

        Assert.InRange(bat.SpitWindUpProgress, 0.4f, 0.65f);
    }

    [Fact]
    public void Its_bolts_hit_a_player_who_stands_still()
    {
        var world = new GameWorld(BigRoom);

        Run(world, PlayerInput.None, 5 * Second);

        Assert.Equal(Balance.Default.Player.MaxHealth - 2 * Balance.Default.BloodBolt.Damage, world.Player.Health);
    }

    [Fact]
    public void It_screeches_then_dives_and_bites_a_player_who_stands_still()
    {
        var world = new GameWorld(BigRoom);
        var bat = world.Bats[0];

        var beforeScreech = RunUntil(world, PlayerInput.None, e => e is BatScreeched, 7 * Second);
        var screech = beforeScreech.Count;
        Assert.Equal(BatState.Screeching, bat.State);

        var after = Run(world, PlayerInput.None, 3 * Second / 2);

        var bite = Assert.Single(after, e => e.Event is BatBit);
        Assert.Equal(new BatBit(bat.Id, world.Player.Id), bite.Event);
        Assert.Contains(after, e => e.Tick == bite.Tick && e.Event == new PlayerHit(world.Player.Id, Balance.Default.Bat.BiteDamage));
        Assert.True(bite.Tick >= ScreechTicks, $"It bit {bite.Tick} ticks after the screech, before the screech was over.");
        Assert.True(screech >= SimulationClock.SecondsToTicks(Balance.Default.Bat.DiveInterval) - 1, $"First dive already after {screech} ticks.");
    }

    [Fact]
    public void It_is_low_while_diving()
    {
        var world = new GameWorld(BigRoom);
        var bat = world.Bats[0];
        RunUntil(world, PlayerInput.None, e => e is BatScreeched, 7 * Second);

        RunUntil(world, PlayerInput.None, e => e is BatBit, 2 * Second);

        // Below the player's head: in the blade's reach.
        Assert.True(bat.Position.Y < BodySizes.Player.Height, $"Its body starts at {bat.Position.Y} m.");
    }

    [Fact]
    public void Stepping_aside_during_the_screech_makes_it_miss()
    {
        var world = new GameWorld(BigRoom);
        var bat = world.Bats[0];
        RunUntil(world, PlayerInput.None, e => e is BatScreeched, 7 * Second);

        // Turn sideways to the bat and run.
        var toBat = (bat.Position - world.Player.Position) with { Y = 0f };
        Face(world.Player, new Vector3(-toBat.Z, 0f, toBat.X));
        var seen = Run(world, PlayerInput.None with { Move = new Vector2(0f, 1f) }, 3 * Second / 2);

        Assert.Contains(seen, e => e.Event == new BatDiveMissed(bat.Id));
        Assert.DoesNotContain(seen, e => e.Event is BatBit);
    }

    [Fact]
    public void After_the_dive_it_rises_and_circles_again()
    {
        var world = new GameWorld(BigRoom);
        var bat = world.Bats[0];
        RunUntil(world, PlayerInput.None, e => e is BatScreeched, 7 * Second);
        RunUntil(world, PlayerInput.None, e => e is BatBit, 2 * Second);

        Run(world, PlayerInput.None, 5 * Second / 2);

        Assert.Equal(BatState.Circling, bat.State);
        Assert.True(bat.Position.Y >= Balance.Default.Bat.CircleHeightMin - 0.05f, $"Still down at {bat.Position.Y} m.");
    }

    [Fact]
    public void It_finds_its_way_around_a_corner_and_spits_only_once_it_sees_the_player()
    {
        // The bat is in the lower corridor, the player in the upper one; the only way is round the right end.
        var world = new GameWorld(LevelParser.Parse(string.Join('\n',
            "P.....",
            "#####.",
            "B.....")));
        var bat = world.Bats[0];
        var player = world.Player;
        bat.TakeDamage(1, []);

        int? firstSight = null;
        var seen = new List<(int Tick, GameEvent Event)>();
        for (var tick = 0; tick < 5 * Second; tick++)
        {
            world.Tick(PlayerInput.None);
            seen.AddRange(world.Events.Select(e => (tick, e)));
            Assert.False(world.Level.CellAt(bat.Position).IsSolid, $"Inside a wall at {bat.Position}.");
            var centre = bat.Position + new Vector3(0f, BodySizes.Bat.Height / 2f, 0f);
            if (firstSight is null && LineOfSight.IsClear(world.Level, centre, player.EyePosition))
            {
                firstSight = tick;
            }
        }

        Assert.NotNull(firstSight);
        Assert.DoesNotContain(seen, e => e.Tick < firstSight && e.Event is BatSpitWindUp);
    }

    [Fact]
    public void It_lets_go_of_a_dead_player()
    {
        var world = new GameWorld(BigRoom);
        var bat = world.Bats[0];
        Run(world, PlayerInput.None, Second);

        world.Player.TakeDamage(Balance.Default.Player.MaxHealth, []);
        var seen = Run(world, PlayerInput.None, 3 * Second);

        Assert.Equal(BatState.Lurking, bat.State);
        Assert.DoesNotContain(seen, e => e.Event is BatSpat or BatScreeched);
    }

    [Fact]
    public void A_dead_bat_does_nothing_more()
    {
        var world = new GameWorld(BigRoom);
        var bat = world.Bats[0];
        Run(world, PlayerInput.None, Second);

        bat.TakeDamage(Balance.Default.Bat.MaxHealth, []);
        var seen = Run(world, PlayerInput.None, 7 * Second);

        Assert.DoesNotContain(seen, e => e.Event is BatSpitWindUp or BatSpat or BatScreeched or BatBit);
    }

    private static void Face(Player player, Vector3 direction)
    {
        var yaw = MathF.Atan2(direction.X, direction.Z);
        player.Look(PlayerInput.None with
        {
            Look = new Vector2(-(yaw - player.View.YawRadians) / ControlSettings.MouseSensitivity, 0f),
        });
    }

    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    private static float AngleAround(Vector3 centre, Vector3 point) => MathF.Atan2(point.Z - centre.Z, point.X - centre.X);

    private static float WrapAngle(float angle) => MathF.IEEERemainder(angle, 2f * MathF.PI);

    /// <summary>Runs the world and returns every event with the tick (counted from 0) it happened in.</summary>
    private static List<(int Tick, GameEvent Event)> Run(GameWorld world, PlayerInput input, int ticks)
    {
        var seen = new List<(int Tick, GameEvent Event)>();
        for (var tick = 0; tick < ticks; tick++)
        {
            world.Tick(input);
            seen.AddRange(world.Events.Select(e => (tick, e)));
        }

        return seen;
    }

    /// <summary>Runs until an event matches (that tick included) and returns the ticks run; fails after <paramref name="maxTicks"/>.</summary>
    private static List<int> RunUntil(GameWorld world, PlayerInput input, Func<GameEvent, bool> match, int maxTicks)
    {
        var ticks = new List<int>();
        for (var tick = 0; tick < maxTicks; tick++)
        {
            world.Tick(input);
            ticks.Add(tick);
            if (world.Events.Any(match))
            {
                return ticks;
            }
        }

        Assert.Fail($"No matching event within {maxTicks} ticks.");
        return ticks;
    }
}
