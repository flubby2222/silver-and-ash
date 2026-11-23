using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Issue #597, tick by tick in boss rooms as the game generates them: a column of the room covers a player from Gorm
/// until its last blow (issue #590), then it breaks, the attacks go through and the way is free. Gorm and the ghouls walk
/// round the columns.
/// </summary>
public class BossRoomCoverTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly int HitsToBreak = Balance.Default.Columns.HitsToBreak;
    private static readonly IReadOnlyList<RoomTemplate> Templates = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    /// <summary>A player who survives a lot - to watch many attacks in a row.</summary>
    private static readonly Balance Tough = Balance.Default with { Player = Balance.Default.Player with { MaxHealth = 100_000 } };

    private static readonly Balance NoAttacks = Tough with { Gorm = Tough.Gorm with { NearRange = 0f, FarRange = 0f } };

    /// <summary>
    /// A generated boss room, its column nearest to Gorm's place and two cells exactly in line through it, each 3.2 m
    /// from it: one on Gorm's side, one beyond.
    /// </summary>
    private sealed record Setup(GeneratedLevel Generated, Column Column, (int X, int Z) GormSide, (int X, int Z) Beyond)
    {
        public static Setup For(int seed)
        {
            var generated = new LevelZero(Templates).Build(seed, Balance.Default);
            var gorm = generated.Level.BossPositions.Single();
            var column = generated.Level.Columns.MinBy(column => Vector2.Distance(column.Centre, new Vector2(gorm.X, gorm.Z)));

            // A column stands on a cell corner: 3 m along X and 1 m along Z from it is the middle of a cell.
            var toGorm = new Vector2(gorm.X >= column.X ? 1f : -1f, gorm.Z >= column.Z ? 1f : -1f) * new Vector2(3f, 1f);
            var gormSide = Level.ToGrid(column.X + toGorm.X, column.Z + toGorm.Y);
            var beyond = Level.ToGrid(column.X - toGorm.X, column.Z - toGorm.Y);
            var floor = generated.Rooms.Single(room => room.Kind == RoomKind.Boss).FloorCells;
            Assert.Contains(gormSide, floor);
            Assert.Contains(beyond, floor);
            return new Setup(generated, column, gormSide, beyond);
        }

        /// <summary>The generated level with its columns, but the player starting at <paramref name="start"/> and only the given figures.</summary>
        public Level LevelFrom((int X, int Z) start, IEnumerable<(int X, int Z)>? ghouls = null, IEnumerable<(int X, int Z)>? bosses = null)
        {
            var level = Generated.Level;
            var cells = new Cell[level.Width, level.Depth];
            for (var x = 0; x < level.Width; x++)
            {
                for (var z = 0; z < level.Depth; z++)
                {
                    cells[x, z] = level[x, z];
                }
            }

            return new Level(cells, start.X, start.Z, ghoulCells: ghouls, bossCells: bosses).WithColumns(level.Columns);
        }
    }

    /// <summary>Gorm and one player on either side of the column, ticked together, the columns worn as in a world.</summary>
    private sealed class Duel
    {
        public Duel(Setup setup, Balance balance)
        {
            Level = setup.LevelFrom(setup.Beyond);
            Player = new Player(Level, balance: balance);
            var at = Level.CellCenter(setup.GormSide.X, setup.GormSide.Z);
            var facing = Vector3.Normalize((Player.Position - at) with { Y = 0f });
            Gorm = new Gorm(9, at, facing, balance, new Random(1));
            Wear = new ColumnWear(balance.Columns.HitsToBreak);
        }

        public Level Level { get; }

        public Player Player { get; }

        public Gorm Gorm { get; }

        public ColumnWear Wear { get; }

        public List<GameEvent> Events { get; } = [];

        public void Run(int ticks, Func<bool> until)
        {
            for (var tick = 0; tick < ticks && !until(); tick++)
            {
                Player.Tick(PlayerInput.None);
                Events.AddRange(Player.Events);
                var events = new List<GameEvent>();
                Gorm.Act(Level, [Player], [Player.Body], events, columns: Wear);
                Events.AddRange(events);
            }
        }
    }

    public static TheoryData<int> Seeds => new(Enumerable.Range(1, 6));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void A_column_of_the_boss_room_covers_until_its_last_blow_then_the_attacks_go_through(int seed)
    {
        var setup = Setup.For(seed);
        var duel = new Duel(setup, Tough);
        var health = duel.Player.Health;

        duel.Run(120 * Second, () => duel.Events.Contains(new ColumnBroke(setup.Column)));

        Assert.Contains(new ColumnBroke(setup.Column), duel.Events);
        Assert.Equal(
            Enumerable.Range(1, HitsToBreak).Select(hits => new ColumnHit(setup.Column, hits, HitsToBreak)),
            duel.Events.OfType<ColumnHit>().Where(hit => hit.Column == setup.Column));
        duel.Run(5 * Second, () => duel.Gorm.State is GormState.Recovering or GormState.Stuck);
        Assert.Equal(health, duel.Player.Health);

        var before = duel.Events.Count;
        duel.Run(30 * Second, () => duel.Events.Skip(before).Any(e => e is PlayerHit));
        Assert.True(duel.Player.Health < health, "After the column broke, the next attack still did not hit.");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Without_the_column_the_same_attack_hits(int seed)
    {
        // The other side of the coin: it is the column that kept the player unhurt, not the distance or the angle.
        var setup = Setup.For(seed);
        var without = setup with { Generated = setup.Generated with { Level = setup.Generated.Level.WithColumns(setup.Generated.Level.Columns.Where(c => c != setup.Column)) } };
        var duel = new Duel(without, Tough);
        var health = duel.Player.Health;

        duel.Run(30 * Second, () => duel.Events.Any(e => e is PlayerHit));

        Assert.True(duel.Player.Health < health, "Gorm never hit the player even without the column.");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void In_the_world_the_broken_column_leaves_the_boss_room_and_the_way_through_it_is_free(int seed)
    {
        var setup = Setup.For(seed);
        var gorm = setup.Generated.Level.BossPositions.Single();
        var level = setup.LevelFrom(setup.Beyond, bosses: [Level.ToGrid(gorm.X, gorm.Z)]);
        var world = new GameWorld(level, seed, Tough);
        var standing = level.Columns.Count;

        ColumnBroke? broke = null;
        var hits = 0;
        for (var tick = 0; tick < 180 * Second && broke is null; tick++)
        {
            world.Tick(PlayerInput.None);
            hits += world.Events.OfType<ColumnHit>().Count();
            broke = world.Events.OfType<ColumnBroke>().FirstOrDefault();
        }

        Assert.True(broke is not null, $"No column of the boss room broke ({hits} blows).");
        var column = broke.Column;
        var across = (From: new Vector3(column.X - 3f, 1f, column.Z), To: new Vector3(column.X + 3f, 1f, column.Z));
        Assert.Equal(standing - 1, world.Level.Columns.Count);
        Assert.DoesNotContain(column, world.Level.Columns);
        Assert.DoesNotContain(column, world.EnemyLevel.Columns);
        Assert.Null(world.Level.ColumnAt(new Vector3(column.X, 1f, column.Z)));
        Assert.True(LineOfSight.IsClear(world.Level, across.From, across.To));
        var (cellX, cellZ) = Level.ToGrid(column.X, column.Z);
        var where = new Vector3(column.X, world.Level[cellX, cellZ].FloorHeight, column.Z);
        Assert.False(CharacterCollision.Overlaps(world.Level, where, BodySizes.Gorm.Radius), "Gorm still cannot stand where the column stood.");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Gorm_walks_round_a_column_of_the_boss_room_up_to_the_player(int seed)
    {
        var setup = Setup.For(seed);
        var duel = new Duel(setup, NoAttacks);
        var near = NoAttacks.Gorm.StopDistance + 0.5f;

        duel.Run(10 * Second, () =>
        {
            Assert.False(CharacterCollision.Overlaps(duel.Level, duel.Gorm.Position, BodySizes.Gorm.Radius), $"Gorm inside a column at {duel.Gorm.Position}.");
            return Flat(duel.Gorm.Position, duel.Player.Position) <= near;
        });

        Assert.True(Flat(duel.Gorm.Position, duel.Player.Position) <= near, $"Gorm stuck at {duel.Gorm.Position}, the column at {setup.Column.Centre}.");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void A_ghoul_walks_round_a_column_of_the_boss_room_up_to_the_player(int seed)
    {
        var setup = Setup.For(seed);
        var world = new GameWorld(setup.LevelFrom(setup.Beyond, ghouls: [setup.GormSide]), seed);
        var ghoul = world.Ghouls[0];
        ghoul.TakeDamage(10, []);                                         // a hit wakes it, though the column hides the player

        var reached = false;
        for (var tick = 0; tick < 8 * Second && !reached; tick++)
        {
            world.Tick(PlayerInput.None);
            reached = world.Events.Any(e => e is GhoulWindUp);

            // Every tick, not only at the end (issue #608): on the way round it never steps into the column.
            Assert.False(
                CharacterCollision.Overlaps(world.Level, ghoul.Position, BodySizes.Ghoul.Radius),
                $"Tick {tick}: the ghoul at {ghoul.Position} is inside the column at {setup.Column.Centre}.");
        }

        Assert.True(reached, $"Stuck at {ghoul.Position}, the column at {setup.Column.Centre}.");
    }

    private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));
}
