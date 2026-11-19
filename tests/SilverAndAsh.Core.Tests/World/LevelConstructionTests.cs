using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

/// <summary>
/// Checks of the <see cref="Level"/> constructor: which inputs it rejects, with which exception, parameter and message,
/// and what it keeps from valid figure lists (#462).
/// </summary>
public class LevelConstructionTests
{
    // A 3×1 strip: walkable – wall – walkable. (0, 0) is the player start.
    private static Cell[,] Strip()
    {
        var cells = new Cell[3, 1];
        cells[0, 0] = Cell.Floor;
        cells[1, 0] = Cell.Wall;
        cells[2, 0] = Cell.Floor;
        return cells;
    }

    private static Level WithFigures(string kind, IEnumerable<(int X, int Z)>? figures) => kind switch
    {
        "dummy" => new Level(Strip(), 0, 0, dummyCells: figures),
        "ghoul" => new Level(Strip(), 0, 0, ghoulCells: figures),
        "bat" => new Level(Strip(), 0, 0, batCells: figures),
        "boss" => new Level(Strip(), 0, 0, bossCells: figures),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static IReadOnlyList<System.Numerics.Vector3> PositionsOf(Level level, string kind) => kind switch
    {
        "dummy" => level.DummyPositions,
        "ghoul" => level.GhoulPositions,
        "bat" => level.BatPositions,
        "boss" => level.BossPositions,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    [Fact]
    public void Null_cells_are_rejected_with_the_parameter_name()
    {
        var error = Assert.Throws<ArgumentNullException>(() => new Level(null!, 0, 0));

        Assert.Equal("cells", error.ParamName);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(3, 0)]   // only the depth is missing: the second half of "Width == 0 || Depth == 0"
    public void A_grid_without_cells_in_one_direction_is_rejected(int width, int depth)
    {
        var error = Assert.Throws<ArgumentException>(() => new Level(new Cell[width, depth], 0, 0));

        Assert.Equal("cells", error.ParamName);
        Assert.StartsWith("A level needs at least one cell.", error.Message);
    }

    [Theory]
    [InlineData(1.0f, 1.0f)]   // ceiling on the floor
    [InlineData(2.0f, 1.5f)]   // ceiling below the floor
    public void A_walkable_cell_without_room_under_its_ceiling_is_rejected(float floor, float ceiling)
    {
        var cells = Strip();
        cells[2, 0] = new Cell(false, floor, ceiling);

        var error = Assert.Throws<ArgumentException>(() => new Level(cells, 0, 0));

        Assert.Equal("cells", error.ParamName);
        Assert.StartsWith("Every walkable cell needs a ceiling above its floor.", error.Message);
    }

    [Fact]
    public void A_solid_cell_needs_no_room_under_its_ceiling()
    {
        var cells = Strip();
        cells[1, 0] = new Cell(true, 0f, 0f);   // like default(Cell), but solid

        var level = new Level(cells, 0, 0);

        Assert.True(level[1, 0].IsSolid);
        Assert.Equal(3, level.Width);
        Assert.Equal(1, level.Depth);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(3, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 1)]
    [InlineData(1, 0)]   // the wall in the middle of the strip
    public void The_player_start_must_be_a_walkable_cell_inside_the_level(int x, int z)
    {
        var error = Assert.Throws<ArgumentException>(() => new Level(Strip(), x, z));

        Assert.Equal("The player must start on a walkable cell inside the level.", error.Message);
    }

    [Fact]
    public void A_valid_level_keeps_its_size_and_start()
    {
        var level = new Level(Strip(), 2, 0);

        Assert.Equal((3, 1), (level.Width, level.Depth));
        Assert.Equal((2, 0), (level.PlayerStartX, level.PlayerStartZ));
        Assert.Equal(level.CellCenter(2, 0), level.PlayerStartPosition);
    }

    [Theory]
    [InlineData("dummy", "dummyCells", "Training dummies must stand on walkable cells inside the level.")]
    [InlineData("ghoul", "ghoulCells", "Ghouls must start on walkable cells inside the level.")]
    [InlineData("bat", "batCells", "Bats must start above walkable cells inside the level.")]
    [InlineData("boss", "bossCells", "A boss must start on a walkable cell inside the level.")]
    public void A_figure_on_a_wall_is_rejected_with_its_own_message(string kind, string parameter, string message)
    {
        var error = Assert.Throws<ArgumentException>(() => WithFigures(kind, [(2, 0), (1, 0)]));

        Assert.Equal(parameter, error.ParamName);
        Assert.StartsWith(message, error.Message);
    }

    [Theory]
    [InlineData("dummy", "dummyCells", -1, 0)]
    [InlineData("dummy", "dummyCells", 0, 1)]
    [InlineData("ghoul", "ghoulCells", -1, 0)]
    [InlineData("ghoul", "ghoulCells", 0, 1)]
    [InlineData("bat", "batCells", 3, 0)]
    [InlineData("bat", "batCells", 0, -1)]
    [InlineData("boss", "bossCells", 3, 0)]
    [InlineData("boss", "bossCells", 0, -1)]
    public void A_figure_outside_the_level_is_rejected(string kind, string parameter, int x, int z)
    {
        var error = Assert.Throws<ArgumentException>(() => WithFigures(kind, [(x, z)]));

        Assert.Equal(parameter, error.ParamName);
    }

    [Theory]
    [InlineData("dummy")]
    [InlineData("ghoul")]
    [InlineData("bat")]
    [InlineData("boss")]
    public void Valid_figures_are_kept_in_order_at_their_cell_centres(string kind)
    {
        var level = WithFigures(kind, [(2, 0), (0, 0)]);

        Assert.Equal([level.CellCenter(2, 0), level.CellCenter(0, 0)], PositionsOf(level, kind));
    }

    [Theory]
    [InlineData("dummy")]
    [InlineData("ghoul")]
    [InlineData("bat")]
    [InlineData("boss")]
    public void Without_a_list_a_level_has_no_figures_of_that_kind(string kind)
    {
        var level = WithFigures(kind, null);

        Assert.Empty(PositionsOf(level, kind));
    }

    [Fact]
    public void Figure_lists_are_read_once_and_later_changes_do_not_reach_the_level()
    {
        var ghouls = new List<(int X, int Z)> { (2, 0) };
        var level = new Level(Strip(), 0, 0, ghoulCells: ghouls);

        ghouls.Add((0, 0));

        Assert.Equal([level.CellCenter(2, 0)], level.GhoulPositions);
    }
}
