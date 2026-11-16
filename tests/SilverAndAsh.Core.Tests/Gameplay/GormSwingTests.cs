using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>Gorm's chain swing as a visible sweep from his left over the front to his right (issue #181).</summary>
public class GormSwingTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly GormBalance Values = Balance.Default.Gorm;

    /// <summary>Players who survive a lot - to watch many attacks in a row.</summary>
    private static readonly Balance Tough = Balance.Default with { Player = Balance.Default.Player with { MaxHealth = 100_000 } };

    /// <summary>Gorm's cell: column 7, row 6 of the hall. He starts looking along -Z (towards row 0).</summary>
    private const int GormX = 7;
    private const int GormZ = 6;

    /// <summary>An open hall, 15 cells wide and 25 deep, with a player start at (<paramref name="x"/>, <paramref name="z"/>).</summary>
    private static Level Hall(int x, int z)
    {
        var rows = Enumerable.Range(0, 25).Select(row => new string(Enumerable.Range(0, 15).Select(column => (column, row) == (x, z) ? 'P' : '.').ToArray()));
        return LevelParser.Parse(string.Join('\n', rows));
    }

    /// <summary>
    /// Gorm in the hall with players at the given cells (each from a hall of its own, all the same but for the start).
    /// The first player is the nearest: he turns to them.
    /// </summary>
    private sealed class Fight
    {
        public Fight(params (int X, int Z)[] cells)
            : this(Tough, cells)
        {
        }

        public Fight(Balance balance, params (int X, int Z)[] cells)
        {
            Level = Hall(GormX, GormZ);
            Players = cells.Select(cell => new Player(Hall(cell.X, cell.Z), balance: balance)).ToList();
            Gorm = new Gorm(9, Level.CellCenter(GormX, GormZ), -Vector3.UnitZ, balance, new Random(1));
        }

        public Level Level { get; }

        public List<Player> Players { get; }

        public Gorm Gorm { get; }

        public List<GameEvent> Events { get; } = [];

        public int Ticks { get; private set; }

        public void Tick()
        {
            foreach (var player in Players)
            {
                player.Tick(PlayerInput.None);
            }

            var events = new List<GameEvent>();
            Gorm.Act(Level, Players, Players.Select(player => player.Body).ToList(), events);
            Events.AddRange(events);
            Ticks++;
        }

        /// <summary>Runs until his next swing starts its sweep (at most a minute).</summary>
        public void UntilSweep()
        {
            for (var tick = 0; tick < 60 * Second && Gorm.State != GormState.Swinging; tick++)
            {
                Tick();
            }

            Assert.Equal(GormState.Swinging, Gorm.State);
        }

        /// <summary>Runs the sweep to its end; returns the tick each player was hit at (null: not hit).</summary>
        public List<int?> Sweep()
        {
            var health = Players.Select(player => player.Health).ToList();
            var hitAt = Players.Select(_ => (int?)null).ToList();
            for (var tick = 0; tick < 5 * Second && Gorm.State == GormState.Swinging; tick++)
            {
                Tick();
                for (var index = 0; index < Players.Count; index++)
                {
                    if (hitAt[index] is null && Players[index].Health < health[index])
                    {
                        hitAt[index] = Ticks;
                        Assert.Equal(health[index] - Values.SwingDamage, Players[index].Health);
                    }
                }
            }

            Assert.Equal(GormState.Recovering, Gorm.State);
            return hitAt;
        }
    }

    [Fact]
    public void The_chain_sweeps_from_his_left_over_the_front_to_his_right()
    {
        // In front of him (the nearest, he turns to them), to his left and to his right, all within reach.
        // Looking along -Z, his left is -X.
        var fight = new Fight((GormX, GormZ - 3), (GormX - 3, GormZ - 1), (GormX + 3, GormZ - 1));
        fight.UntilSweep();

        var hitAt = fight.Sweep();

        Assert.All(hitAt, tick => Assert.NotNull(tick));
        var (front, left, right) = (hitAt[0]!.Value, hitAt[1]!.Value, hitAt[2]!.Value);
        Assert.True(left < front && front < right, $"Hit at left {left}, front {front}, right {right}.");
    }

    [Fact]
    public void Behind_him_the_chain_never_comes()
    {
        // One player in front (the nearest), one behind him within reach.
        var fight = new Fight((GormX, GormZ - 2), (GormX, GormZ + 3));
        fight.UntilSweep();
        Assert.True(new Vector2(fight.Gorm.Position.X - fight.Players[1].Position.X, fight.Gorm.Position.Z - fight.Players[1].Position.Z).Length() <= Values.SwingReach);

        var hitAt = fight.Sweep();

        Assert.NotNull(hitAt[0]);
        Assert.Null(hitAt[1]);
        Assert.Contains(new GormSwung(9, true), fight.Events);
    }

    [Fact]
    public void Standing_in_front_the_chain_hits_once_halfway_through_the_sweep()
    {
        var fight = new Fight((GormX, GormZ - 3));
        fight.UntilSweep();
        var start = fight.Ticks;

        var hitAt = fight.Sweep();

        var sweep = SimulationClock.SecondsToTicks(Values.SwingTime);
        Assert.InRange(hitAt[0]!.Value - start, (sweep / 2) - 1, (sweep / 2) + 1);
        Assert.Single(fight.Events.OfType<GormSwung>());
    }

    [Fact]
    public void The_chain_points_to_his_left_at_the_start_and_to_his_right_at_the_end_and_nowhere_otherwise()
    {
        var fight = new Fight((GormX, GormZ - 3));
        Assert.Null(fight.Gorm.SwingDirection);
        fight.UntilSweep();
        var gorm = fight.Gorm;
        var right = new Vector3(-gorm.Facing.Z, 0f, gorm.Facing.X);

        var first = gorm.SwingDirection!.Value;
        while (gorm.State == GormState.Swinging)
        {
            var before = gorm.SwingDirection!.Value;
            fight.Tick();
            if (gorm.State == GormState.Swinging)
            {
                Assert.True(Vector3.Dot(gorm.SwingDirection!.Value - before, right) >= 0f, "The chain moved back towards his left.");
            }
            else
            {
                Assert.True(Vector3.Dot(before, right) > 0.95f, $"The sweep ended pointing {before}, not to his right.");
            }
        }

        Assert.True(Vector3.Dot(first, -right) > 0.95f, $"The sweep started pointing {first}, not to his left.");
        Assert.Null(gorm.SwingDirection);
    }

    [Fact]
    public void A_whole_circle_also_hits_behind_him()
    {
        // The arc comes from balance.json: 360 degrees reach all round.
        var round = Tough with { Gorm = Tough.Gorm with { SwingArc = 360f } };
        var fight = new Fight(round, (GormX, GormZ - 2), (GormX, GormZ + 3));
        fight.UntilSweep();

        var hitAt = fight.Sweep();

        Assert.NotNull(hitAt[0]);
        Assert.NotNull(hitAt[1]);
    }
}
