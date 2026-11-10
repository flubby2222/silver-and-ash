using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

public class LevelTests
{
    [Fact]
    public void Player_cannot_start_inside_a_wall()
    {
        var cells = new Cell[2, 1];
        cells[0, 0] = Cell.Wall;
        cells[1, 0] = Cell.Floor;

        Assert.Throws<ArgumentException>(() => new Level(cells, 0, 0));
    }

    [Fact]
    public void Player_cannot_start_outside_the_level()
    {
        var cells = new Cell[1, 1];
        cells[0, 0] = Cell.Floor;

        Assert.Throws<ArgumentException>(() => new Level(cells, 5, 0));
    }

    [Fact]
    public void A_level_without_cells_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new Level(new Cell[0, 0], 0, 0));
    }

    [Fact]
    public void Cells_that_were_never_filled_in_are_rejected()
    {
        // default(Cell) would be a walkable cell with floor and ceiling at 0 m.
        var cells = new Cell[2, 1];
        cells[0, 0] = Cell.Floor;

        Assert.Throws<ArgumentException>(() => new Level(cells, 0, 0));
    }

    [Fact]
    public void Changing_the_original_array_does_not_change_the_level()
    {
        var cells = new Cell[2, 1];
        cells[0, 0] = Cell.Floor;
        cells[1, 0] = Cell.Floor;
        var level = new Level(cells, 0, 0);

        cells[1, 0] = Cell.Wall;

        Assert.False(level[1, 0].IsSolid);
    }

    [Theory]
    [InlineData(0f, 0f, 0, 0)]
    [InlineData(1.99f, 0.5f, 0, 0)]
    [InlineData(2f, 0f, 1, 0)]
    [InlineData(5f, 3f, 2, 1)]
    [InlineData(-0.5f, 0f, -1, 0)]   // Floor, not truncation: just left of the level is outside
    [InlineData(0f, -2.5f, 0, -2)]
    public void World_positions_map_to_grid_cells(float worldX, float worldZ, int expectedX, int expectedZ)
    {
        Assert.Equal((expectedX, expectedZ), Level.ToGrid(worldX, worldZ));
    }

    [Fact]
    public void CellAt_finds_the_cell_under_a_world_position()
    {
        var level = LevelParser.Parse("P#");

        Assert.False(level.CellAt(new Vector3(1f, 0f, 1f)).IsSolid);
        Assert.True(level.CellAt(new Vector3(3f, 0f, 1f)).IsSolid);
        Assert.True(level.CellAt(new Vector3(-0.1f, 0f, 1f)).IsSolid);
    }

    [Fact]
    public void IsInside_knows_the_borders()
    {
        var level = LevelParser.Parse("P.\n..");

        Assert.True(level.IsInside(0, 0));
        Assert.True(level.IsInside(1, 1));
        Assert.False(level.IsInside(2, 0));
        Assert.False(level.IsInside(0, -1));
    }

    [Fact]
    public void CellCenter_and_ToGrid_fit_together()
    {
        var level = LevelParser.Parse("...\n..P");

        var centre = level.CellCenter(2, 1);

        Assert.Equal((2, 1), Level.ToGrid(centre.X, centre.Z));
    }
}
