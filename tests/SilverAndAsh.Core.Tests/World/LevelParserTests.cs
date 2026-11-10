using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

public class LevelParserTests
{
    [Fact]
    public void Reads_size_cells_and_player_start()
    {
        var level = LevelParser.Parse(
            """
            #####
            #P..#
            #.#.#
            #####
            """);

        Assert.Equal(5, level.Width);
        Assert.Equal(4, level.Depth);
        Assert.Equal((1, 1), (level.PlayerStartX, level.PlayerStartZ));
        Assert.True(level[0, 0].IsSolid);
        Assert.False(level[2, 1].IsSolid);
        Assert.True(level[2, 2].IsSolid);
        Assert.False(level[1, 1].IsSolid);
    }

    [Fact]
    public void Walkable_cells_have_the_standard_heights()
    {
        var level = LevelParser.Parse("P.");

        Assert.Equal(Cell.DefaultFloorHeight, level[1, 0].FloorHeight);
        Assert.Equal(Cell.DefaultCeilingHeight, level[1, 0].CeilingHeight);
    }

    [Fact]
    public void Player_starts_in_the_centre_of_the_cell_on_the_floor()
    {
        var level = LevelParser.Parse(
            """
            ###
            #.P
            """);

        // Cell (2, 1): centre at x = 2.5 * 2 m, z = 1.5 * 2 m.
        Assert.Equal(new Vector3(5f, 0f, 3f), level.PlayerStartPosition);
    }

    [Fact]
    public void Short_lines_are_filled_with_walls()
    {
        var level = LevelParser.Parse("P...\n..\n");

        Assert.Equal(4, level.Width);
        Assert.False(level[1, 1].IsSolid);
        Assert.True(level[2, 1].IsSolid);
        Assert.True(level[3, 1].IsSolid);
    }

    [Fact]
    public void Spaces_are_walls()
    {
        var level = LevelParser.Parse("P .");

        Assert.True(level[1, 0].IsSolid);
    }

    [Fact]
    public void Everything_outside_the_level_is_wall()
    {
        var level = LevelParser.Parse("P");

        Assert.True(level[-1, 0].IsSolid);
        Assert.True(level[1, 0].IsSolid);
        Assert.True(level[0, -1].IsSolid);
        Assert.True(level[0, 1].IsSolid);
    }

    [Theory]
    [InlineData("P.\r\n..\r\n")]   // Windows line endings
    [InlineData("P.\n..\n\n\n")]   // trailing empty lines
    public void Line_endings_and_trailing_empty_lines_do_not_matter(string text)
    {
        var level = LevelParser.Parse(text);

        Assert.Equal(2, level.Width);
        Assert.Equal(2, level.Depth);
    }

    [Fact]
    public void Unknown_symbol_is_reported_with_line_and_column()
    {
        var error = Assert.Throws<LevelFormatException>(() => LevelParser.Parse("P..\n.x.\n"));

        Assert.Equal(2, error.LineNumber);
        Assert.Contains("'x'", error.Message);
        Assert.Contains("column 2", error.Message);
    }

    [Fact]
    public void Missing_player_start_is_reported()
    {
        var error = Assert.Throws<LevelFormatException>(() => LevelParser.Parse("...\n..."));

        Assert.Contains("no player start", error.Message);
        Assert.Equal(0, error.LineNumber);
    }

    [Fact]
    public void Leading_empty_lines_count_for_line_numbers()
    {
        var error = Assert.Throws<LevelFormatException>(() => LevelParser.Parse("\n\nP.\n.\t."));

        Assert.Equal(4, error.LineNumber);
        Assert.Contains("column 2", error.Message);
    }

    [Fact]
    public void Second_player_start_is_reported_with_both_lines()
    {
        var error = Assert.Throws<LevelFormatException>(() => LevelParser.Parse("P..\n...\n..P"));

        Assert.Equal(3, error.LineNumber);
        Assert.Contains("column 3", error.Message);
        Assert.Contains("line 1", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n\n  \n")]
    public void Empty_level_is_reported(string text)
    {
        var error = Assert.Throws<LevelFormatException>(() => LevelParser.Parse(text));

        Assert.Contains("empty", error.Message);
        Assert.Equal(0, error.LineNumber);
    }

    [Fact]
    public void The_shipped_test_level_is_valid()
    {
        // Guards the level file that is delivered with the game.
        var level = LevelParser.ParseFile(RepositoryFiles.Find("assets/levels/test.txt"));

        Assert.True(level.Width >= 10);
        Assert.True(level.Depth >= 10);
        Assert.False(level[level.PlayerStartX, level.PlayerStartZ].IsSolid);
    }
}
