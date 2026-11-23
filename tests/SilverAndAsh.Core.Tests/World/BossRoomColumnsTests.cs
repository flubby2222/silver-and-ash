using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

/// <summary>
/// Issue #597: the boss room gets free-standing columns instead of wall blocks - on a grid round its middle, each where
/// <see cref="ColumnPlacement"/> allows it, clear of the doors, of Gorm's place and of the staircase's cell.
/// </summary>
public class BossRoomColumnsTests
{
    private static readonly IReadOnlyList<RoomTemplate> Templates = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    private static readonly ColumnsBalance Standard = Balance.Default.Columns;

    // As the game builds its levels (LevelZero.Build). Built once: generating is the slow part.
    private static readonly Lazy<GeneratedLevel[]> Levels = new(() => Enumerable.Range(0, 30)
        .Select(seed => new LevelZero(Templates).Build(seed, Balance.Default))
        .ToArray());

    private static PlacedRoom Boss(GeneratedLevel level) => level.Rooms.Single(room => room.Kind == RoomKind.Boss);

    [Fact]
    public void With_the_standard_values_every_boss_room_has_its_full_grid_of_columns()
    {
        var expected = Standard.BossRoomPerSide * Standard.BossRoomPerSide;
        Assert.True(expected > 0, "The standard values put no column in the boss room - the test would prove nothing.");

        Assert.All(Levels.Value, level => Assert.Equal(expected, level.Level.Columns.Count));
    }

    [Fact]
    public void Every_column_stands_in_the_boss_room_where_the_placement_rule_allows_it()
    {
        foreach (var level in Levels.Value)
        {
            var floor = Boss(level).FloorCells.ToHashSet();
            var doorCells = level.Doors.SelectMany(door => door.Cells).ToList();
            foreach (var column in level.Level.Columns)
            {
                var others = level.Level.WithColumns(level.Level.Columns.Where(other => other != column));
                Assert.Contains(Level.ToGrid(column.X, column.Z), floor);
                Assert.Equal(ColumnFit.Fits, ColumnPlacement.Check(others, column, doorCells, Standard));
            }
        }
    }

    [Fact]
    public void No_column_comes_near_gorms_place_or_the_cell_the_staircase_rises_in()
    {
        var required = ColumnPlacement.RequiredGap(Standard);
        foreach (var level in Levels.Value)
        {
            var boss = Boss(level);
            var gorm = Level.ToGrid(level.Level.BossPositions.Single().X, level.Level.BossPositions.Single().Z);
            Assert.Contains(gorm, BossRoomColumns.KeptClear(boss));
            Assert.Contains(Stairs.CellIn(boss.FloorCells), BossRoomColumns.KeptClear(boss));
            Assert.All(level.Level.Columns, column => Assert.All(
                BossRoomColumns.KeptClear(boss), cell => Assert.True(ColumnPlacement.GapToCell(column, cell) >= required)));
        }
    }

    [Fact]
    public void No_figure_starts_inside_a_column()
    {
        foreach (var level in Levels.Value)
        {
            Assert.All(level.Level.BossPositions, at => Assert.False(CharacterCollision.Overlaps(level.Level, at, BodySizes.Gorm.Radius)));
            Assert.All(level.Level.GhoulPositions, at => Assert.False(CharacterCollision.Overlaps(level.Level, at, BodySizes.Ghoul.Radius)));
            Assert.All(level.Level.BatPositions, at => Assert.False(CharacterCollision.Overlaps(level.Level, at, BodySizes.Bat.Radius)));
        }
    }

    [Fact]
    public void The_columns_stand_on_cell_corners_spacing_apart_round_the_rooms_middle()
    {
        foreach (var level in Levels.Value)
        {
            var columns = level.Level.Columns;
            var boss = Boss(level);
            var middle = new Vector2(
                boss.FloorCells.Average(cell => (cell.X + 0.5f) * Level.CellSize),
                boss.FloorCells.Average(cell => (cell.Z + 0.5f) * Level.CellSize));

            Assert.All(columns, column => Assert.Equal(Vector2.Zero, new Vector2(column.X % Level.CellSize, column.Z % Level.CellSize)));
            Assert.Equal(Standard.BossRoomSpacing, columns.Max(c => c.X) - columns.Min(c => c.X), 3);
            Assert.Equal(Standard.BossRoomSpacing, columns.Max(c => c.Z) - columns.Min(c => c.Z), 3);
            var centre = columns.Aggregate(Vector2.Zero, (sum, column) => sum + column.Centre) / columns.Count;
            Assert.True(Vector2.Distance(middle, centre) <= Level.CellSize, $"Grid centred on {centre}, the room's middle is {middle}.");
        }
    }

    [Fact]
    public void Only_the_boss_room_gets_columns_and_without_a_boss_room_nothing_changes()
    {
        var generated = LevelGenerator.Generate(Templates, 3, Balance.Default.Layout);
        Assert.Empty(generated.Level.Columns);

        var withoutBoss = generated with { Rooms = generated.Rooms.Where(room => room.Kind != RoomKind.Boss).ToList() };

        Assert.Same(withoutBoss, BossRoomColumns.Place(withoutBoss, Standard));
    }

    [Fact]
    public void None_to_a_side_means_no_columns()
    {
        var generated = LevelGenerator.Generate(Templates, 3, Balance.Default.Layout);

        var placed = BossRoomColumns.Place(generated, Standard with { BossRoomPerSide = 0 });

        Assert.Empty(placed.Level.Columns);
    }

    [Fact]
    public void A_grid_point_at_the_wall_stays_empty()
    {
        // So wide apart that each column stands on the floor of the 14 x 14 cell hall, but only 1.5 m from its walls.
        var generated = LevelGenerator.Generate(Templates, 3, Balance.Default.Layout);
        var grid = Standard with { BossRoomSpacing = 24f };
        var points = BossRoomColumns.Grid(Boss(generated), grid).ToList();
        var floor = Boss(generated).FloorCells.ToHashSet();

        var placed = BossRoomColumns.Place(generated, grid);

        Assert.Equal(4, points.Count);
        Assert.All(points, point => Assert.Contains(Level.ToGrid(point.X, point.Z), floor));
        Assert.All(points, point => Assert.Equal(ColumnFit.TooCloseToWall, ColumnPlacement.Check(generated.Level, point, [], grid)));
        Assert.Empty(placed.Level.Columns);
    }

    [Fact]
    public void The_grid_points_on_the_staircase_and_at_gorms_place_stay_empty()
    {
        // Three to a side: the middle one would stand where the staircase rises after Gorm's death, the middle one of the
        // side Gorm waits on (which side that is depends on how the room is turned) right beside him.
        var generated = LevelGenerator.Generate(Templates, 3, Balance.Default.Layout);
        var boss = Boss(generated);
        var grid = Standard with { BossRoomPerSide = 3, BossRoomSpacing = 8f };
        var points = BossRoomColumns.Grid(boss, grid).ToList();
        var (stairs, gorm) = (Stairs.CellIn(boss.FloorCells), Assert.Single(boss.BossSlots!));
        var required = ColumnPlacement.RequiredGap(grid);

        var placed = BossRoomColumns.Place(generated, grid);

        Assert.Single(points, point => ColumnPlacement.GapToCell(point, stairs) < required);
        Assert.Single(points, point => ColumnPlacement.GapToCell(point, gorm) < required);
        Assert.Equal(points.Where(point => ColumnPlacement.GapToCell(point, stairs) >= required && ColumnPlacement.GapToCell(point, gorm) >= required), placed.Level.Columns);
        Assert.Equal(7, placed.Level.Columns.Count);
    }

    [Fact]
    public void The_same_seed_gives_the_same_columns()
    {
        var again = new LevelZero(Templates).Build(7, Balance.Default);

        Assert.Equal(Levels.Value[7].Level.Columns, again.Level.Columns);
    }

    [Fact]
    public void The_boss_room_has_no_wall_inside_only_the_columns_give_cover()
    {
        // The wall blocks the columns replace (issue #597): inside the boss room's outer wall every cell is open.
        foreach (var level in Levels.Value)
        {
            var boss = Boss(level);
            for (var x = boss.X + 1; x < boss.X + boss.Width - 1; x++)
            {
                for (var z = boss.Z + 1; z < boss.Z + boss.Depth - 1; z++)
                {
                    Assert.False(level.Level[x, z].IsSolid, $"Wall cell {x}/{z} inside the boss room (seed {level.Seed}).");
                }
            }
        }
    }
}
