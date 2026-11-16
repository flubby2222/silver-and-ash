using System.Numerics;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Physics;

/// <summary>Solid boxes (chests) in the character collision: block sideways, carry from above (issue #184).</summary>
public class BoxCollisionTests
{
    private const float Radius = 0.35f;
    private const float Tick = SimulationClock.SecondsPerTick;
    private const float Gravity = 20f;

    // An open hall, 5 x 5 floor cells (x and z from 2 m to 12 m) inside walls.
    private static readonly Level Hall = LevelParser.Parse(
        """
        #######
        #.....#
        #.....#
        #..P..#
        #.....#
        #.....#
        #######
        """);

    // A chest-sized box (1 m wide, 0.7 m deep, 0.7 m high) at x 7, z 9.
    private static readonly BoxBody Chest = new(new Vector3(6.5f, 0f, 8.65f), new Vector3(7.5f, 0.7f, 9.35f));

    private static (Vector3 Position, Vector3 Velocity) Walk(Vector3 start, Vector3 velocity, int ticks, BoxBody[] boxes)
    {
        var position = start;
        for (var tick = 0; tick < ticks; tick++)
        {
            (position, velocity) = CharacterCollision.Move(Hall, position, velocity, Radius, Tick, boxes: boxes);
        }

        return (position, velocity);
    }

    [Fact]
    public void Walking_into_a_box_stops_in_front_of_it_and_kills_the_speed_into_it()
    {
        var (position, velocity) = Walk(new Vector3(7f, 0f, 6f), new Vector3(0f, 0f, 6f), 120, [Chest]);

        Assert.Equal(Chest.Min.Z - Radius, position.Z, 0.01f);
        Assert.Equal(0f, velocity.Z, 0.001f);
    }

    [Fact]
    public void Along_a_box_the_character_slides_on()
    {
        // Diagonally against its near side: the sideways part stays.
        var (position, velocity) = Walk(new Vector3(6.6f, 0f, 8f), new Vector3(2f, 0f, 2f), 20, [Chest]);

        Assert.True(position.Z <= Chest.Min.Z - Radius + 0.01f);
        Assert.Equal(2f, velocity.X, 0.001f);
    }

    [Fact]
    public void Without_the_box_the_same_walk_goes_through()
    {
        var (position, _) = Walk(new Vector3(7f, 0f, 6f), new Vector3(0f, 0f, 6f), 60, []);

        Assert.True(position.Z > Chest.Max.Z, $"Stopped at {position.Z}.");
    }

    [Fact]
    public void Feet_high_enough_pass_over_the_box()
    {
        // Feet less than a step below its top: no longer in the way.
        var start = new Vector3(7f, Chest.Max.Y - BodySizes.Player.StepHeight + 0.01f, 6f);

        var (position, _) = Walk(start, new Vector3(0f, 0f, 6f), 60, [Chest]);

        Assert.True(position.Z > Chest.Max.Z, $"Stopped at {position.Z}.");
    }

    [Fact]
    public void A_box_above_the_head_is_no_obstacle()
    {
        var floating = new BoxBody(Chest.Min with { Y = 2f }, Chest.Max with { Y = 2.5f });

        var (position, _) = Walk(new Vector3(7f, 0f, 6f), new Vector3(0f, 0f, 6f), 60, [floating]);

        Assert.True(position.Z > Chest.Max.Z, $"Stopped at {position.Z}.");
    }

    [Fact]
    public void Falling_onto_a_box_lands_on_its_top()
    {
        var position = new Vector3(7f, 2f, 9f);
        var velocity = Vector3.Zero;
        var grounded = false;
        for (var tick = 0; tick < 120 && !grounded; tick++)
        {
            (position, velocity, grounded) = CharacterCollision.MoveVertically(
                Hall, position, velocity, Radius, BodySizes.Player.Height, Gravity, Tick, [Chest]);
        }

        Assert.True(grounded);
        Assert.Equal(Chest.Max.Y, position.Y, 0.001f);
    }

    [Fact]
    public void Standing_on_the_edge_of_a_box_still_stands_on_it()
    {
        // Only the rim of the circle is over the box.
        var position = new Vector3(7f, Chest.Max.Y, Chest.Max.Z + Radius - 0.05f);

        var (after, _, grounded) = CharacterCollision.MoveVertically(
            Hall, position, Vector3.Zero, Radius, BodySizes.Player.Height, Gravity, Tick, [Chest]);

        Assert.True(grounded);
        Assert.Equal(Chest.Max.Y, after.Y, 0.001f);
    }

    [Fact]
    public void Beside_the_box_the_floor_is_the_floor()
    {
        var position = new Vector3(7f, Chest.Max.Y, Chest.Max.Z + Radius + 0.05f);
        var velocity = Vector3.Zero;
        var grounded = false;
        for (var tick = 0; tick < 120 && !grounded; tick++)
        {
            (position, velocity, grounded) = CharacterCollision.MoveVertically(
                Hall, position, velocity, Radius, BodySizes.Player.Height, Gravity, Tick, [Chest]);
        }

        Assert.Equal(0f, position.Y, 0.001f);
    }

    [Fact]
    public void A_box_growing_under_the_feet_lifts_the_character()
    {
        var position = new Vector3(7f, 0f, 9f);
        var velocity = Vector3.Zero;
        for (var step = 1; step <= 60; step++)
        {
            var rising = new BoxBody(Chest.Min, Chest.Max with { Y = Chest.Max.Y * step / 60f });
            (position, velocity) = CharacterCollision.Move(Hall, position, velocity, Radius, Tick, boxes: [rising]);
            (position, velocity, _) = CharacterCollision.MoveVertically(
                Hall, position, velocity, Radius, BodySizes.Player.Height, Gravity, Tick, [rising]);
        }

        Assert.Equal(Chest.Max.Y, position.Y, 0.001f);
        Assert.Equal(7f, position.X, 0.001f);
        Assert.Equal(9f, position.Z, 0.001f);
    }
}
