using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Issue #539: no enemy stays stuck at a column. The grid search knows only cells and the column's cell is open, so the
/// way leads straight through it; <see cref="ColumnDetour"/> turns the enemy past its edge.
/// </summary>
public class ColumnEnemyTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };

    // 11 x 7 open cells: the player in the middle row at the west end, a ghoul at the east end, 20 m apart.
    private const string GhoulHall =
        "...........\n" +
        "...........\n" +
        "...........\n" +
        "P.........G\n" +
        "...........\n" +
        "...........\n" +
        "...........";

    [Theory]
    [InlineData(5)]                          // half way
    [InlineData(8)]                          // close to the ghoul's start
    [InlineData(2)]                          // close to the player
    public void A_ghoul_with_a_column_exactly_between_it_and_the_player_still_reaches_the_player(int columnX)
    {
        var level = LevelParser.Parse(GhoulHall);
        var column = Column.AtCell(columnX, 3);
        Assert.Equal(ColumnFit.Fits, ColumnPlacement.Check(level, column, []));
        var world = new GameWorld(level.WithColumns([column]));
        var ghoul = world.Ghouls[0];
        Assert.Equal(ghoul.Position.Z, column.Z);                         // exactly in line with the player ...
        Assert.Equal(world.Player.Position.Z, column.Z);
        ghoul.TakeDamage(10, []);                                         // ... which hides the player: a hit wakes it

        var reached = RunUntil(world, PlayerInput.None, 8 * Second, w => w.Events.Any(e => e is GhoulWindUp));

        Assert.True(reached, $"Stuck at {ghoul.Position}, column at {column.Centre}.");
        Assert.False(CharacterCollision.Overlaps(world.Level, ghoul.Position, BodySizes.Ghoul.Radius));
    }

    [Fact]
    public void A_ghoul_walking_beside_a_column_is_not_turned_aside()
    {
        // The column two rows north of the line: the way is clear, the ghoul walks exactly as without it.
        var level = LevelParser.Parse(GhoulHall);
        var with = new GameWorld(level.WithColumns([Column.AtCell(5, 1)]));
        var without = new GameWorld(level);
        with.Ghouls[0].TakeDamage(10, []);
        without.Ghouls[0].TakeDamage(10, []);

        for (var tick = 0; tick < 2 * Second; tick++)
        {
            with.Tick(PlayerInput.None);
            without.Tick(PlayerInput.None);
            Assert.Equal(without.Ghouls[0].Position, with.Ghouls[0].Position);
        }
    }

    // As in GormTests: room 0 (start) in row 0, door in row 1, the boss room (5 wide) in rows 2-11, Gorm ('T') in row 10.
    private const string GormMap = "##.P.##\n###.###\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#..T..#\n#.....#";

    private static readonly Balance NoAttacks = Balance.Default with { Gorm = Balance.Default.Gorm with { NearRange = 0f, FarRange = 0f } };

    [Fact]
    public void Gorm_with_a_column_exactly_between_him_and_the_player_still_walks_up_to_the_player()
    {
        var level = LevelParser.Parse(GormMap);
        var column = Column.AtCell(3, 6);
        Assert.Equal(ColumnFit.Fits, ColumnPlacement.Check(level, column, [(3, 1)]));
        var world = GormWorld(level.WithColumns([column]));
        Enter(world);
        var gorm = world.Gorm!;
        Assert.Equal(gorm.Position.X, column.X, 0.01f);                   // in line with the column and the player
        Assert.Equal(world.Player.Position.X, column.X, 0.01f);

        for (var tick = 0; tick < 10 * Second && FlatDistance(gorm.Position, world.Player.Position) > Balance.Default.Gorm.StopDistance + 0.2f; tick++)
        {
            world.Tick(PlayerInput.None);
        }

        Assert.True(gorm.Position.Z < column.Z, $"Gorm never got past the column, stuck at {gorm.Position}.");
        Assert.InRange(FlatDistance(gorm.Position, world.Player.Position), 0f, Balance.Default.Gorm.StopDistance + 0.2f);
    }

    private static GameWorld GormWorld(Level level)
    {
        var rows = Enumerable.Range(2, 10).SelectMany(z => Enumerable.Range(1, 5).Select(x => (x, z))).ToList();
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 7, 1, 0, [(2, 0), (3, 0), (4, 0)], [], []),
            new(1, RoomKind.Boss, "hall", 0, 2, 7, 10, 1, rows, [], []),
        ];
        LevelDoor[] doors = [new(3, 1, 0, 1, RoomSide.South, DoorKind.Boss)];
        return new GameWorld(level, 1, NoAttacks, doors, rooms);
    }

    private static void Enter(GameWorld world)
    {
        world.Tick(PressF);
        for (var tick = 0; tick < 3 * Second && !world.Rooms[1].IsSealed; tick++)
        {
            world.Tick(Forward);
        }

        Assert.True(world.Rooms[1].IsSealed, "The boss room never sealed - the test would prove nothing.");
    }

    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

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
