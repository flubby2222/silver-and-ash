using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Issue #539: no enemy stays stuck at a column. The grid search knows only cells and the column's cell is open, so the
/// way leads straight through it; <see cref="ColumnDetour"/> turns the enemy past its edge.
/// </summary>
public class ColumnEnemyTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;

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

    private static readonly Balance NoAttacks = Balance.Default with { Gorm = Balance.Default.Gorm with { NearRange = 0f, FarRange = 0f } };

    [Fact]
    public void Gorm_with_a_column_exactly_between_him_and_the_player_still_walks_up_to_the_player()
    {
        var level = LevelParser.Parse(GormHallMap);
        var column = Column.AtCell(3, 6);
        Assert.Equal(ColumnFit.Fits, ColumnPlacement.Check(level, column, [(3, 1)]));
        var world = GormHallWorld(level.WithColumns([column]), balance: NoAttacks);
        EnterGormHall(world);
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
}
