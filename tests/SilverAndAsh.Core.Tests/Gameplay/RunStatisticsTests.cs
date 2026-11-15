using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public sealed class RunStatisticsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"runs-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    // ---- Counting events -----------------------------------------------------------------------------------------

    [Fact]
    public void Kills_are_counted_per_kind_of_enemy()
    {
        var statistics = new RunStatistics();

        statistics.Count([new GhoulDied(1), new GhoulDied(2), new BatDied(3), new PlayerDied(0)]);

        Assert.Equal(2, statistics.GhoulsKilled);
        Assert.Equal(1, statistics.BatsKilled);
    }

    [Fact]
    public void Damage_dealt_counts_hits_on_enemies_but_not_on_training_dummies()
    {
        var statistics = new RunStatistics();

        statistics.Count([new GhoulHit(1, 30), new BatHit(2, 20), new DummyHit(3, 50)]);

        Assert.Equal(50, statistics.DamageDealt);
    }

    [Fact]
    public void Damage_taken_counts_every_hit_on_a_player()
    {
        var statistics = new RunStatistics();

        statistics.Count([new PlayerHit(0, 15), new PlayerHit(0, 12)]);
        statistics.Count([new PlayerHit(0, 3)]);

        Assert.Equal(30, statistics.DamageTaken);
    }

    [Fact]
    public void Cleared_rooms_are_counted()
    {
        var statistics = new RunStatistics();

        statistics.Count([new RoomCleared(1), new RoomSealed(2), new RoomCleared(4)]);

        Assert.Equal(2, statistics.RoomsCleared);
    }

    [Fact]
    public void Collected_silver_is_counted_but_dropped_silver_is_not()
    {
        var statistics = new RunStatistics();

        statistics.Count([new SilverDropped(1, 3, Vector3.Zero), new SilverTaken(1, 0, 3)]);
        statistics.Count([new SilverTaken(2, 0, 2)]);

        Assert.Equal(5, statistics.SilverCollected);
    }

    [Fact]
    public void Time_is_counted_in_ticks()
    {
        var statistics = new RunStatistics();

        for (var i = 0; i < 90; i++)
        {
            statistics.AddTick();
        }

        Assert.Equal(1.5f, statistics.Seconds, 0.0001f);
    }

    // ---- In the world ---------------------------------------------------------------------------------------------

    private const string Hall = "#P#\n#.#\n#.#\n#G#";

    private static void Aim(GameWorld world, Ghoul ghoul)
    {
        var body = ghoul.Body;
        var direction = Vector3.Normalize(body.Position + new Vector3(0f, body.Height / 2f, 0f) - world.Player.EyePosition);
        world.Player.Look(PlayerInput.None with
        {
            Look = new Vector2(
                -(MathF.Atan2(direction.X, direction.Z) - world.Player.View.YawRadians) / ControlSettings.MouseSensitivity,
                -(MathF.Asin(direction.Y) - world.Player.View.PitchRadians) / ControlSettings.MouseSensitivity),
        });
    }

    [Fact]
    public void The_world_counts_its_own_hits_and_kills()
    {
        // A weak ghoul: one pistol shot kills it (the pistol fires once per press).
        var balance = Balance.Default with { Ghoul = Balance.Default.Ghoul with { MaxHealth = 10 } };
        var world = new GameWorld(LevelParser.Parse(Hall), 0, balance);
        var events = new List<GameEvent>();

        Aim(world, world.Ghouls[0]);
        for (var i = 0; i < 30; i++)
        {
            world.Tick(PlayerInput.None with { Shoot = i == 0 });
            events.AddRange(world.Events);
        }

        Assert.True(world.Ghouls[0].IsDead);
        Assert.Equal(1, world.Statistics.GhoulsKilled);
        Assert.Equal(events.OfType<GhoulHit>().Sum(hit => hit.Damage), world.Statistics.DamageDealt);
        Assert.Equal(events.OfType<PlayerHit>().Sum(hit => hit.Damage), world.Statistics.DamageTaken);
    }

    [Fact]
    public void Time_runs_only_while_the_run_is_on()
    {
        var world = new GameWorld(LevelParser.Parse("#P#\n#.#"));

        for (var i = 0; i < 120; i++)
        {
            world.Tick(PlayerInput.None);
        }

        for (var i = 0; i < 300; i++)
        {
            world.Tick(PlayerInput.None with { Paused = true });
        }

        Assert.Equal(2f, world.Statistics.Seconds, 0.0001f);

        world.Player.TakeDamage(world.Player.MaxHealth, []);
        for (var i = 0; i < 120; i++)
        {
            world.Tick(PlayerInput.None);
        }

        // The tick in which the loss is noticed still counts; after that the clock stands.
        Assert.InRange(world.Statistics.Seconds, 2f, 2f + 1.5f / SimulationClock.TicksPerSecond);
    }

    [Fact]
    public void The_summary_holds_seed_outcome_level_and_rooms()
    {
        var world = new LevelZero(RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"))).NewRun(77, Balance.Default);

        var running = world.Summary();
        world.Player.TakeDamage(world.Player.MaxHealth, []);
        world.Tick(PlayerInput.None);
        var lost = world.Summary();

        Assert.Equal(77, running.Seed);
        Assert.Equal(RunOutcome.Running, running.Outcome);
        Assert.Equal(RunOutcome.Lost, lost.Outcome);
        Assert.Equal(1, lost.Level);
        Assert.Equal(world.Rooms.Count(room => room.IsArena), lost.ArenaRooms);
        Assert.Equal(0, lost.RoomsCleared);
    }

    [Fact]
    public void A_new_run_starts_at_zero()
    {
        var world = new GameWorld(LevelParser.Parse(Hall));
        for (var i = 0; i < 60; i++)
        {
            world.Tick(PlayerInput.None);
        }

        var restarted = world.Restarted();

        Assert.Equal(0f, restarted.Statistics.Seconds);
        Assert.Equal(0, restarted.Statistics.DamageTaken + restarted.Statistics.DamageDealt);
    }

    // ---- The end of a run is handed out once (end screen and run log) -------------------------------------------

    [Fact]
    public void A_running_world_has_no_end_summary_yet()
    {
        var world = new GameWorld(LevelParser.Parse(Hall));
        world.Tick(PlayerInput.None);

        Assert.Null(world.TakeEndSummary());
    }

    [Fact]
    public void A_lost_run_hands_out_its_summary_exactly_once()
    {
        var world = new GameWorld(LevelParser.Parse(Hall));
        world.Player.TakeDamage(world.Player.MaxHealth, []);
        world.Tick(PlayerInput.None);

        var first = world.TakeEndSummary();
        world.Tick(PlayerInput.None);
        var second = world.TakeEndSummary();

        Assert.NotNull(first);
        Assert.Equal(RunOutcome.Lost, first.Outcome);
        Assert.Null(second);
    }

    [Fact]
    public void A_won_run_hands_out_its_summary_exactly_once()
    {
        // Start room in row 0, a door in row 1, an empty boss room behind it: entering it wins the run.
        static List<(int X, int Z)> Rows(int from, int to) =>
            Enumerable.Range(from, to - from + 1).SelectMany(z => new[] { (1, z), (2, z), (3, z) }).ToList();
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 5, 1, 0, Rows(0, 0), [], []),
            new(1, RoomKind.Boss, "boss", 0, 2, 5, 4, 1, Rows(2, 5), [], []),
        ];
        var world = new GameWorld(
            LevelParser.Parse("#.P.#\n##.##\n#...#\n#...#\n#...#\n#...#"), 1, Balance.Default,
            [new LevelDoor(2, 1, 0, 1, RoomSide.South, DoorKind.Boss)], rooms);

        world.Tick(PlayerInput.None with { Interact = true });
        for (var i = 0; i < 3 * SimulationClock.TicksPerSecond && !world.IsWon; i++)
        {
            world.Tick(PlayerInput.None with { Move = new Vector2(0f, 1f) });
        }

        Assert.True(world.IsWon, "The run was not won - the test would prove nothing.");
        Assert.Equal(RunOutcome.Won, world.TakeEndSummary()?.Outcome);
        Assert.Null(world.TakeEndSummary());
    }

    [Fact]
    public void A_new_try_has_its_own_end_summary_again()
    {
        var world = new GameWorld(LevelParser.Parse(Hall));
        world.Player.TakeDamage(world.Player.MaxHealth, []);
        world.Tick(PlayerInput.None);
        world.TakeEndSummary();

        var next = world.Restarted();
        next.Player.TakeDamage(next.Player.MaxHealth, []);
        next.Tick(PlayerInput.None);

        Assert.NotNull(next.TakeEndSummary());
    }

    // ---- The local run log ----------------------------------------------------------------------------------------

    private static readonly RunSummary Example = new(
        Seed: 123, Outcome: RunOutcome.Won, Seconds: 754.4f, GhoulsKilled: 31, BatsKilled: 9, DamageDealt: 4210,
        DamageTaken: 380, Level: 6, RoomsCleared: 9, ArenaRooms: 9, Silver: 142);

    [Fact]
    public void A_summary_becomes_one_readable_line()
    {
        var line = Example.ToLogLine(new DateTimeOffset(2026, 11, 15, 18, 40, 12, TimeSpan.FromHours(1)));

        Assert.Equal(
            "2026-11-15 18:40:12 | won | seed 123 | 12:34 | ghouls 31 | bats 9 | dealt 4210 | taken 380 | level 6 | rooms 9/9 | silver 142",
            line);
        Assert.DoesNotContain('\n', line);
    }

    [Fact]
    public void Runs_are_appended_to_the_log_file()
    {
        var path = Path.Combine(_folder, "runs.log");

        Assert.True(RunLog.Append(path, "first"));
        Assert.True(RunLog.Append(path, "second"));

        Assert.Equal(["first", "second"], File.ReadAllLines(path));
    }

    [Fact]
    public void A_log_that_cannot_be_written_does_not_crash()
    {
        Directory.CreateDirectory(_folder);
        var blocker = Path.Combine(_folder, "blocker");
        File.WriteAllText(blocker, "x");

        Assert.False(RunLog.Append(Path.Combine(blocker, "runs.log"), "line"));
    }
}
