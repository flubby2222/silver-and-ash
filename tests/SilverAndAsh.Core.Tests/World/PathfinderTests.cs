using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

public class PathfinderTests
{
    private const float Radius = 0.4f;

    [Fact]
    public void A_straight_corridor_gives_a_straight_path()
    {
        var level = LevelParser.Parse("P....");

        var path = Pathfinder.FindPath(level, (0, 0), (4, 0));

        Assert.Equal([(0, 0), (1, 0), (2, 0), (3, 0), (4, 0)], path);
    }

    [Fact]
    public void The_start_is_also_the_goal()
    {
        Assert.Equal([(0, 0)], Pathfinder.FindPath(LevelParser.Parse("P.."), (0, 0), (0, 0)));
    }

    [Fact]
    public void The_path_goes_around_a_wall_the_shortest_way()
    {
        var level = LevelParser.Parse(
            """
            .....
            P###.
            .....
            """);

        var path = Pathfinder.FindPath(level, (0, 1), (4, 1));

        // Up, along the top row, down: 7 cells. Diagonals past the wall's corners are not allowed.
        Assert.NotNull(path);
        Assert.Equal(7, path.Count);
        AssertValid(level, path);
    }

    [Fact]
    public void The_path_leads_through_a_door_into_the_next_room()
    {
        var level = LevelParser.Parse(
            """
            P..#...
            ...#...
            .......
            """);

        var path = Pathfinder.FindPath(level, (0, 0), (6, 0));

        Assert.NotNull(path);
        Assert.Contains((3, 2), path);
        AssertValid(level, path);
    }

    [Fact]
    public void A_diagonal_step_never_cuts_a_wall_corner()
    {
        var level = LevelParser.Parse("P.\n#.");

        Assert.Equal([(0, 0), (1, 0), (1, 1)], Pathfinder.FindPath(level, (0, 0), (1, 1)));
    }

    [Fact]
    public void A_free_diagonal_step_is_used()
    {
        var level = LevelParser.Parse("P.\n..");

        Assert.Equal([(0, 0), (1, 1)], Pathfinder.FindPath(level, (0, 0), (1, 1)));
    }

    [Fact]
    public void Stairs_go_up_a_high_platform_does_not()
    {
        Assert.NotNull(Pathfinder.FindPath(LevelParser.Parse("P123"), (0, 0), (3, 0)));
        Assert.Null(Pathfinder.FindPath(LevelParser.Parse("P3"), (0, 0), (1, 0)));
    }

    [Fact]
    public void Jumping_down_is_fine()
    {
        Assert.NotNull(Pathfinder.FindPath(LevelParser.Parse("3P"), (0, 0), (1, 0)));
    }

    [Fact]
    public void Into_a_pit_yes_out_of_it_no()
    {
        var level = LevelParser.Parse("P_.");

        Assert.NotNull(Pathfinder.FindPath(level, (0, 0), (1, 0)));
        Assert.Null(Pathfinder.FindPath(level, (1, 0), (2, 0)));
        Assert.Null(Pathfinder.FindPath(level, (0, 0), (2, 0)));       // the only way leads through the pit
    }

    [Fact]
    public void An_unreachable_goal_gives_no_path()
    {
        var level = LevelParser.Parse("P#.");

        Assert.Null(Pathfinder.FindPath(level, (0, 0), (2, 0)));
        Assert.Null(Pathfinder.FindPath(level, (0, 0), (1, 0)));       // the goal is a wall
        Assert.Null(Pathfinder.FindPath(level, (0, 0), (9, 9)));       // outside the level
    }

    [Fact]
    public void Straight_walking_is_blocked_by_walls_and_high_platforms_but_not_by_open_floor()
    {
        var level = LevelParser.Parse("P..\n.#3\n...");

        Assert.True(Pathfinder.CanWalkStraight(level, level.CellCenter(0, 0), level.CellCenter(2, 0), Radius));
        Assert.False(Pathfinder.CanWalkStraight(level, level.CellCenter(0, 0), level.CellCenter(2, 2), Radius));   // through the wall
        Assert.False(Pathfinder.CanWalkStraight(level, level.CellCenter(2, 0), level.CellCenter(2, 2), Radius));   // over the high platform
        // The middle line stays in the free column, but the body (radius 0.4) grazes the wall cell (x from 2 m).
        var grazing = level.CellCenter(0, 2) + new Vector3(0.9f, 0f, 0f);
        Assert.False(Pathfinder.CanWalkStraight(level, level.CellCenter(0, 0), grazing, Radius));
        Assert.True(Pathfinder.CanWalkStraight(level, level.CellCenter(0, 0), level.CellCenter(0, 2), Radius));
    }

    [Fact]
    public void In_an_open_room_the_smoothed_path_goes_straight_to_the_goal()
    {
        var level = LevelParser.Parse("P....\n.....\n.....");
        var path = Pathfinder.FindPath(level, (0, 0), (4, 2))!;

        var points = Pathfinder.Smooth(level, path, Radius);

        Assert.Equal([level.CellCenter(4, 2)], points);
    }

    [Fact]
    public void Around_a_wall_the_smoothed_path_keeps_the_corners_and_every_leg_is_walkable()
    {
        var level = LevelParser.Parse(
            """
            .....
            P###.
            .....
            """);
        var path = Pathfinder.FindPath(level, (0, 1), (4, 1))!;

        var points = Pathfinder.Smooth(level, path, Radius);

        Assert.True(points.Count is > 1 and < 7, $"{points.Count} points.");
        Assert.Equal(level.CellCenter(4, 1), points[^1]);
        var from = level.CellCenter(0, 1);
        foreach (var point in points)
        {
            Assert.True(Pathfinder.CanWalkStraight(level, from, point, Radius), $"Cannot walk from {from} to {point}.");
            from = point;
        }
    }

    [Theory]
    // Open room, straight across: 4 straight steps.
    [InlineData(".....|P....|.....", 0, 1, 4, 1, 4.0)]
    // Open room, diagonal: 2 diagonal + 2 straight steps.
    [InlineData("P....|.....|.....", 0, 0, 4, 2, 4.828)]
    // A small maze: the only way in is from the left, 10 straight steps (no diagonal fits past the corners).
    [InlineData("P........|.#######.|.#.....#.|.#.###.#.|.......#.|########.|.........", 0, 0, 4, 2, 10.0)]
    // Straight down in an open room: 2 straight steps (a zigzag of 2 diagonals has as many steps, but is longer).
    [InlineData("P..|...|...", 0, 0, 0, 2, 2.0)]
    // A cup that opens towards the start: heading straight for the goal leads into a dead end.
    // Shortest: around the top - 2 diagonals and 8 straight steps.
    [InlineData(".........|..#####..|P.....#.G|..#####..|.........", 0, 2, 8, 2, 10.828)]
    public void The_path_is_the_shortest_one(string rows, int sx, int sz, int gx, int gz, double shortest)
    {
        var level = LevelParser.Parse(rows.Replace('|', '\n'));

        var path = Pathfinder.FindPath(level, (sx, sz), (gx, gz));

        Assert.NotNull(path);
        AssertValid(level, path);
        Assert.Equal(shortest, Length(path), 2);
    }

    /// <summary>True length of a path: 1 per straight step, about 1.41 per diagonal step.</summary>
    private static double Length(IReadOnlyList<(int X, int Z)> path)
    {
        var length = 0.0;
        for (var i = 1; i < path.Count; i++)
        {
            var diagonal = path[i].X != path[i - 1].X && path[i].Z != path[i - 1].Z;
            length += diagonal ? Math.Sqrt(2) : 1;
        }

        return length;
    }

    /// <summary>Every step goes to a neighbour (also diagonal), never into a wall.</summary>
    private static void AssertValid(Level level, IReadOnlyList<(int X, int Z)> path)
    {
        for (var i = 1; i < path.Count; i++)
        {
            var (x, z) = path[i];
            Assert.False(level[x, z].IsSolid, $"Step {i} goes into a wall at ({x}, {z}).");
            Assert.True(Math.Abs(x - path[i - 1].X) <= 1 && Math.Abs(z - path[i - 1].Z) <= 1, $"Step {i} is not to a neighbour.");
        }
    }
}
