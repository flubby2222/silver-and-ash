using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Tests.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

/// <summary>Issue #539: the columns of a level and the rule where one may stand.</summary>
public class ColumnPlacementTests
{
    // Just over and just under a limit: far below any length the rule could mean, far above float rounding at 20 m.
    private const float Hair = 0.001f;

    private static readonly float Required = ColumnPlacement.RequiredGap(Balance.Default.Columns);
    private static readonly (int X, int Z)[] NoDoors = [];

    // 9 x 9 open cells (0 .. 18 m); beyond the edge is wall.
    private static readonly Level Hall = ColumnCollisionTests.Open(9, 9);

    [Fact]
    public void The_required_gap_is_the_widest_figure_plus_the_play()
    {
        var widest = new[] { BodySizes.Player.Radius, BodySizes.Dummy.Radius, BodySizes.Gorm.Radius, BodySizes.Ghoul.Radius, BodySizes.Bat.Radius }.Max();

        Assert.Equal(2f * widest, BodySizes.WidestFigureDiameter);
        Assert.Equal(BodySizes.WidestFigureDiameter + Balance.Default.Columns.Play, Required);
        Assert.Equal(Required, ColumnPlacement.RequiredGap());
        Assert.Equal(BodySizes.WidestFigureDiameter + 1f, ColumnPlacement.RequiredGap(new ColumnsBalance { Play = 1f }));
    }

    [Fact]
    public void A_column_in_the_middle_of_an_open_hall_fits()
    {
        Assert.Equal(ColumnFit.Fits, ColumnPlacement.Check(Hall, Column.AtCell(4, 4), NoDoors));
    }

    [Theory]
    [InlineData(float.NaN, 9f)]
    [InlineData(9f, float.PositiveInfinity)]
    [InlineData(-1f, 9f)]
    [InlineData(9f, 18.5f)]
    public void Outside_the_level_or_not_a_number_is_not_on_open_floor(float x, float z)
    {
        Assert.Equal(ColumnFit.NotOnOpenFloor, ColumnPlacement.Check(Hall, new Column(x, z), NoDoors));
    }

    [Fact]
    public void On_a_wall_is_not_on_open_floor()
    {
        var walled = Hall.WithCells([(4, 4, Cell.Wall)]);

        Assert.Equal(ColumnFit.NotOnOpenFloor, ColumnPlacement.Check(walled, Column.AtCell(4, 4), NoDoors));
    }

    [Fact]
    public void The_gap_to_a_wall_must_be_the_required_gap()
    {
        // The edge of the level at x = 0 is wall: the column's side the required gap away fits, a hair nearer does not.
        Assert.Equal(ColumnFit.Fits, ColumnPlacement.Check(Hall, new Column(Column.Radius + Required + Hair, 9f), NoDoors));
        Assert.Equal(ColumnFit.TooCloseToWall, ColumnPlacement.Check(Hall, new Column(Column.Radius + Required - Hair, 9f), NoDoors));
    }

    [Fact]
    public void Touching_a_wall_is_too_close_as_well_since_nothing_leans_on_a_column()
    {
        Assert.Equal(ColumnFit.TooCloseToWall, ColumnPlacement.Check(Hall, new Column(Column.Radius, 9f), NoDoors));
        Assert.Equal(ColumnFit.TooCloseToWall, ColumnPlacement.Check(Hall, Column.AtCell(0, 4), NoDoors));
    }

    [Fact]
    public void A_wall_cell_diagonally_off_counts_by_its_nearest_corner()
    {
        // A wall cell whose corner (8, 8) lies the required gap minus a hair from the column's side, on the diagonal.
        var walled = Hall.WithCells([(3, 3, Cell.Wall)]);
        var offset = (Column.Radius + Required) / MathF.Sqrt(2f);

        Assert.Equal(ColumnFit.Fits, ColumnPlacement.Check(walled, new Column(8f + offset + Hair, 8f + offset + Hair), NoDoors));
        Assert.Equal(ColumnFit.TooCloseToWall, ColumnPlacement.Check(walled, new Column(8f + offset - Hair, 8f + offset - Hair), NoDoors));
    }

    [Fact]
    public void The_gap_to_a_floor_more_than_a_step_higher_must_be_the_required_gap()
    {
        // A ledge on cell (2, 4) (x 4 .. 6 m): too high to step up, so the column must keep the gap to it.
        var ledge = Hall.WithCells([(2, 4, new Cell(false, BodySizes.Player.StepHeight + 0.5f, Cell.DefaultCeilingHeight))]);
        var limit = 6f + Column.Radius + Required;

        Assert.Equal(ColumnFit.Fits, ColumnPlacement.Check(ledge, new Column(limit + Hair, 9f), NoDoors));
        Assert.Equal(ColumnFit.TooCloseToEdge, ColumnPlacement.Check(ledge, new Column(limit - Hair, 9f), NoDoors));
    }

    [Fact]
    public void A_floor_only_a_step_higher_is_no_edge()
    {
        var step = Hall.WithCells([(3, 4, new Cell(false, BodySizes.Player.StepHeight, Cell.DefaultCeilingHeight))]);

        Assert.Equal(ColumnFit.Fits, ColumnPlacement.Check(step, Column.AtCell(4, 4), NoDoors));
    }

    [Fact]
    public void The_gap_to_a_door_must_be_the_required_gap()
    {
        (int X, int Z)[] door = [(2, 4)];
        var limit = 6f + Column.Radius + Required;

        Assert.Equal(ColumnFit.Fits, ColumnPlacement.Check(Hall, new Column(limit + Hair, 9f), door));
        Assert.Equal(ColumnFit.TooCloseToDoor, ColumnPlacement.Check(Hall, new Column(limit - Hair, 9f), door));
    }

    [Fact]
    public void No_column_fits_in_or_right_before_a_door_with_a_portcullis()
    {
        // As the game builds its levels (LevelZero.Build), portcullises placed by GratePlacement (issue #558).
        var templates = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));
        var checkedCells = 0;
        foreach (var seed in Enumerable.Range(0, 10))
        {
            var generated = new LevelZero(templates).Build(seed, Balance.Default);
            var doorCells = generated.Doors.Select(door => (door.X, door.Z)).ToList();
            foreach (var door in generated.Doors.Where(door => door.HasGrate))
            {
                (int X, int Z)[] cells = [(door.X, door.Z), (door.X + 1, door.Z), (door.X - 1, door.Z), (door.X, door.Z + 1), (door.X, door.Z - 1)];
                foreach (var cell in cells.Where(cell => !generated.Level[cell.X, cell.Z].IsSolid))
                {
                    Assert.NotEqual(ColumnFit.Fits, ColumnPlacement.Check(generated.Level, Column.AtCell(cell.X, cell.Z), doorCells, Balance.Default.Columns));
                    checkedCells++;
                }
            }
        }

        Assert.True(checkedCells > 0, "no door with a portcullis was checked");
    }

    [Fact]
    public void The_gap_to_another_column_must_be_the_required_gap()
    {
        var first = Column.AtCell(2, 4);
        var withOne = Hall.WithColumns([first]);
        var limit = first.X + (2f * Column.Radius) + Required;

        Assert.Equal(ColumnFit.Fits, ColumnPlacement.Check(withOne, new Column(limit + Hair, first.Z), NoDoors));
        Assert.Equal(ColumnFit.TooCloseToColumn, ColumnPlacement.Check(withOne, new Column(limit - Hair, first.Z), NoDoors));
        Assert.Equal(ColumnFit.TooCloseToColumn, ColumnPlacement.Check(withOne, first, NoDoors));
    }

    [Fact]
    public void More_play_asks_for_a_wider_gap()
    {
        var column = new Column(Column.Radius + Required + Hair, 9f);

        Assert.Equal(ColumnFit.Fits, ColumnPlacement.Check(Hall, column, NoDoors, Balance.Default.Columns));
        Assert.Equal(ColumnFit.TooCloseToWall, ColumnPlacement.Check(Hall, column, NoDoors, new ColumnsBalance { Play = Balance.Default.Columns.Play + 0.1f }));
    }

    [Fact]
    public void Missing_arguments_are_refused()
    {
        Assert.Throws<ArgumentNullException>(() => ColumnPlacement.Check(null!, Column.AtCell(4, 4), NoDoors));
        Assert.Throws<ArgumentNullException>(() => ColumnPlacement.Check(Hall, Column.AtCell(4, 4), null!));
    }

    [Fact]
    public void A_level_has_no_columns_until_it_is_given_some()
    {
        Assert.Empty(Hall.Columns);
        Assert.Null(Hall.ColumnAt(new Vector3(9f, 1f, 9f)));
    }

    [Fact]
    public void A_level_knows_its_columns_and_what_lies_inside_them()
    {
        var level = Hall.WithColumns([Column.AtCell(4, 4)]);

        Assert.Equal([new Column(9f, 9f)], level.Columns);
        Assert.Equal(new Column(9f, 9f), level.ColumnAt(new Vector3(9f + Column.Radius - 0.01f, 1f, 9f)));
        Assert.Null(level.ColumnAt(new Vector3(9f + Column.Radius + 0.01f, 1f, 9f)));
        Assert.Empty(Hall.Columns);                                      // the level it came from stays as it was
    }

    [Fact]
    public void Changing_cells_cut_outs_or_enemies_keeps_the_columns()
    {
        var level = Hall.WithColumns([Column.AtCell(4, 4)]);

        Assert.Equal(level.Columns, level.WithCells([(0, 0, Cell.Floor)]).Columns);
        Assert.Equal(level.Columns, level.WithWallCutouts([]).Columns);
        Assert.Equal(level.Columns, level.WithEnemies([], []).Columns);
    }

    [Theory]
    [InlineData(float.NaN, 9f)]
    [InlineData(-0.5f, 9f)]
    [InlineData(9f, 19f)]
    public void A_column_outside_the_level_is_refused(float x, float z)
    {
        Assert.Throws<ArgumentException>(() => Hall.WithColumns([new Column(x, z)]));
    }

    [Fact]
    public void A_column_in_a_wall_is_refused()
    {
        var walled = Hall.WithCells([(4, 4, Cell.Wall)]);

        Assert.Throws<ArgumentException>(() => walled.WithColumns([Column.AtCell(4, 4)]));
    }

    [Fact]
    public void A_column_in_a_cell_stands_in_its_middle()
    {
        Assert.Equal(Hall.CellCenter(3, 5).X, Column.AtCell(3, 5).X);
        Assert.Equal(Hall.CellCenter(3, 5).Z, Column.AtCell(3, 5).Z);
    }
}
