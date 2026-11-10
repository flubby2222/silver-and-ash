using System.Numerics;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Physics;

public class CharacterCollisionTests
{
    private const float Radius = 0.35f;
    private const float Tick = SimulationClock.SecondsPerTick;
    private const float Tolerance = 0.001f;

    // 3 x 3 floor cells (x and z from 2 m to 8 m) surrounded by walls.
    private static readonly Level Room = LevelParser.Parse(
        """
        #####
        #...#
        #.P.#
        #...#
        #####
        """);

    [Fact]
    public void Free_movement_is_not_changed()
    {
        var start = new Vector3(5f, 0f, 5f);
        var velocity = new Vector3(1f, 0f, 0f);

        var (position, newVelocity) = CharacterCollision.Move(Room, start, velocity, Radius, Tick);

        Assert.Equal(start.X + Tick, position.X, Tolerance);
        Assert.Equal(velocity, newVelocity);
    }

    [Fact]
    public void Running_straight_into_a_wall_stops_at_the_wall()
    {
        // The wall on the +X side starts at x = 8 m.
        var (position, velocity) = RunTicks(Room, new Vector3(5f, 0f, 5f), new Vector3(6f, 0f, 0f), 120);

        Assert.Equal(8f - Radius, position.X, Tolerance);
        Assert.Equal(0f, velocity.X, Tolerance);
    }

    [Fact]
    public void Running_diagonally_into_a_wall_slides_along_it()
    {
        // Mostly towards the wall (+X), a bit along it (+Z).
        var start = new Vector3(7f, 0f, 3f);
        var (position, velocity) = RunTicks(Room, start, new Vector3(5f, 0f, 1f), 30);

        Assert.Equal(8f - Radius, position.X, Tolerance);   // pressed against the wall
        Assert.True(position.Z > start.Z + 0.3f);             // but still moving along it
        Assert.Equal(1f, velocity.Z, Tolerance);              // sliding speed is kept
    }

    [Fact]
    public void Pushing_into_a_corner_stays_still_without_jitter()
    {
        var velocity = new Vector3(4f, 0f, 4f);
        var (position, _) = RunTicks(Room, new Vector3(5f, 0f, 5f), velocity, 120);

        Assert.Equal(8f - Radius, position.X, Tolerance);
        Assert.Equal(8f - Radius, position.Z, Tolerance);

        // Keep pushing and check EVERY tick: even a back-and-forth of one tick would be jitter.
        for (var tick = 0; tick < 60; tick++)
        {
            (position, _) = CharacterCollision.Move(Room, position, velocity, Radius, Tick);
            Assert.Equal(8f - Radius, position.X, 0.0001f);
            Assert.Equal(8f - Radius, position.Z, 0.0001f);
        }
    }

    [Fact]
    public void Sliding_along_a_wall_moves_forward_every_tick()
    {
        var position = new Vector3(7.65f, 0f, 3f);
        var velocity = new Vector3(5f, 0f, 1f);

        for (var tick = 0; tick < 30; tick++)
        {
            var before = position.Z;
            (position, _) = CharacterCollision.Move(Room, position, velocity, Radius, Tick);
            Assert.True(position.Z > before, $"Sliding stopped at tick {tick}.");
        }
    }

    [Fact]
    public void Invalid_velocity_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            CharacterCollision.Move(Room, new Vector3(5f, 0f, 5f), new Vector3(float.NaN, 0f, 0f), Radius, Tick));
    }

    [Fact]
    public void Walking_through_a_corridor_does_not_get_stuck()
    {
        var corridor = LevelParser.Parse(
            """
            ##########
            #P.......#
            ##########
            """);
        var start = corridor.PlayerStartPosition;

        // 14.65 m to the far wall at 6 m/s: 3 seconds are enough.
        var (position, _) = RunTicks(corridor, start, new Vector3(6f, 0f, 0f), 3 * SimulationClock.TicksPerSecond);

        Assert.Equal(18f - Radius, position.X, Tolerance);   // reached the far wall
        Assert.Equal(start.Z, position.Z, Tolerance);         // stayed in the middle
    }

    [Fact]
    public void Brushing_against_a_door_frame_slides_into_the_doorway()
    {
        // Doorway at x = 4..6 m in the wall row z = 2 (4..6 m).
        var level = LevelParser.Parse(
            """
            #####
            #...#
            ##.##
            #.P.#
            #####
            """);

        // Start so far left that the body clips the corner of the door frame (x = 4 m),
        // then walk straight "up" (-Z). The round body should slide around the corner into the doorway.
        var start = new Vector3(4.2f, 0f, 7f);
        var (position, _) = RunTicks(level, start, new Vector3(0f, 0f, -6f), 60);

        Assert.True(position.Z < 4f, $"Got stuck in front of the door frame at z = {position.Z}.");
        Assert.False(CharacterCollision.Overlaps(level, position, Radius));
    }

    [Theory]
    [InlineData(50f)]
    [InlineData(500f)]
    public void Very_fast_movement_cannot_pass_through_a_wall(float speed)
    {
        // A single wall cell between two rooms.
        var level = LevelParser.Parse("P.#..");
        var start = level.PlayerStartPosition;

        var (position, _) = RunTicks(level, start, new Vector3(speed, 0f, 0f), 10);

        Assert.True(position.X <= 4f - Radius + Tolerance, $"Tunnelled through the wall to x = {position.X}.");
    }

    [Fact]
    public void Height_is_never_changed()
    {
        var start = new Vector3(5f, 1.23f, 5f);

        var (position, _) = RunTicks(Room, start, new Vector3(6f, 0f, 6f), 60);

        Assert.Equal(1.23f, position.Y);
    }

    [Fact]
    public void Random_movement_never_ends_inside_a_wall()
    {
        var level = LevelParser.ParseFile(RepositoryFiles.Find("assets/levels/test.txt"));
        var random = new Random(12345);   // fixed seed: the same "random" run every time
        var position = level.PlayerStartPosition;

        for (var tick = 0; tick < 5000; tick++)
        {
            var angle = (float)(random.NextDouble() * MathF.Tau);
            var velocity = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * 6f;
            (position, _) = CharacterCollision.Move(level, position, velocity, Radius, Tick);

            Assert.False(CharacterCollision.Overlaps(level, position, Radius), $"Inside a wall after tick {tick} at {position}.");
        }
    }

    // Heights: prepared for stairs, platforms and low passages after 1.0.
    // Row of three cells along X: start (floor 0) | test cell | far cell (floor 0).
    private static Level RowWithMiddleCell(Cell middle)
    {
        var cells = new Cell[3, 1];
        cells[0, 0] = Cell.Floor;
        cells[1, 0] = middle;
        cells[2, 0] = Cell.Floor;
        return new Level(cells, 0, 0);
    }

    [Fact]
    public void A_small_step_can_be_walked_onto()
    {
        var level = RowWithMiddleCell(new Cell(false, 0.3f, 3f));

        var (position, _) = RunTicks(level, level.PlayerStartPosition, new Vector3(6f, 0f, 0f), 20);

        Assert.True(position.X > 2.5f, "The step should not block.");
        Assert.Equal(0.3f, CharacterCollision.FloorAndCeiling(level, position, Radius).Floor);
    }

    [Fact]
    public void A_high_ledge_blocks_like_a_wall()
    {
        var level = RowWithMiddleCell(new Cell(false, 1f, 3f));

        var (position, _) = RunTicks(level, level.PlayerStartPosition, new Vector3(6f, 0f, 0f), 60);

        Assert.Equal(2f - Radius, position.X, Tolerance);
    }

    [Fact]
    public void A_high_ledge_can_be_reached_when_the_feet_are_high_enough()
    {
        var level = RowWithMiddleCell(new Cell(false, 1f, 3f));

        // Feet at 0.9 m (in the middle of a jump): the ledge is within one step.
        var (position, _) = RunTicks(level, level.PlayerStartPosition with { Y = 0.9f }, new Vector3(6f, 0f, 0f), 20);

        Assert.True(position.X > 2.5f);
    }

    [Fact]
    public void A_passage_too_low_for_the_player_blocks()
    {
        var level = RowWithMiddleCell(new Cell(false, 0f, 1.5f));

        var (position, _) = RunTicks(level, level.PlayerStartPosition, new Vector3(6f, 0f, 0f), 60);

        Assert.Equal(2f - Radius, position.X, Tolerance);
    }

    [Fact]
    public void A_lower_floor_does_not_block_so_the_player_can_fall_into_a_pit()
    {
        var level = RowWithMiddleCell(new Cell(false, -2f, 3f));

        var (position, _) = RunTicks(level, level.PlayerStartPosition, new Vector3(6f, 0f, 0f), 20);

        Assert.True(position.X > 2.5f);
    }

    private static (Vector3 Position, Vector3 Velocity) RunTicks(Level level, Vector3 position, Vector3 velocity, int ticks)
    {
        var current = (position, velocity);
        for (var i = 0; i < ticks; i++)
        {
            // Like the player: wants to keep moving in the same direction every tick.
            current = CharacterCollision.Move(level, current.position, velocity, Radius, Tick);
        }

        return current;
    }
}
