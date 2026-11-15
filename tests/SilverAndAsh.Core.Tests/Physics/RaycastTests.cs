using System.Numerics;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Physics;

public class RaycastTests
{
    private const float Tolerance = 0.01f;

    // Wall cell (2, 0): x from 4 to 6.
    private static readonly Level Room = LevelParser.Parse("P.#\n...");

    [Fact]
    public void A_wall_is_hit_at_its_surface_facing_back()
    {
        var hit = Raycast.AgainstLevel(Room, new Vector3(1f, 1f, 1f), Vector3.UnitX, 50f);

        Assert.NotNull(hit);
        Assert.Equal(3f, hit.Value.Distance, Tolerance);
        Assert.Equal(4f, hit.Value.Point.X, Tolerance);
        Assert.Equal(-Vector3.UnitX, hit.Value.Normal);
    }

    [Fact]
    public void Floor_and_ceiling_are_hit()
    {
        var down = Raycast.AgainstLevel(Room, new Vector3(1f, 1f, 1f), -Vector3.UnitY, 50f);
        var up = Raycast.AgainstLevel(Room, new Vector3(1f, 1f, 1f), Vector3.UnitY, 50f);

        Assert.Equal(1f, down!.Value.Distance, Tolerance);
        Assert.Equal(Vector3.UnitY, down.Value.Normal);
        Assert.Equal(Cell.DefaultCeilingHeight - 1f, up!.Value.Distance, Tolerance);
        Assert.Equal(-Vector3.UnitY, up.Value.Normal);
    }

    [Fact]
    public void Nothing_is_hit_beyond_the_maximum_distance()
    {
        Assert.Null(Raycast.AgainstLevel(Room, new Vector3(1f, 1f, 1f), Vector3.UnitX, 2.5f));
    }

    [Fact]
    public void The_side_of_a_platform_is_hit_low_down()
    {
        var level = LevelParser.Parse("P3");

        var hit = Raycast.AgainstLevel(level, new Vector3(1f, 0.3f, 1f), Vector3.UnitX, 50f);

        Assert.Equal(2f, hit!.Value.Point.X, Tolerance);
        Assert.Equal(-Vector3.UnitX, hit.Value.Normal);
    }

    [Fact]
    public void A_shot_into_a_pit_hits_its_floor()
    {
        var level = LevelParser.Parse("P_");
        // Steep enough to reach the pit floor (x from 2 to 4) before the outer wall.
        var direction = Vector3.Normalize(new Vector3(1f, -2f, 0f));

        var hit = Raycast.AgainstLevel(level, new Vector3(2.2f, 1f, 1f), direction, 50f);

        Assert.Equal(LevelParser.PitFloorHeight, hit!.Value.Point.Y, Tolerance);
        Assert.Equal(Vector3.UnitY, hit.Value.Normal);
    }

    [Fact]
    public void A_cylinder_is_hit_at_its_side()
    {
        var body = new CylinderBody(new Vector3(5f, 0f, 0f), 0.3f, 1.8f);

        Assert.Equal(4.7f, Raycast.AgainstCylinder(new Vector3(0f, 1f, 0f), Vector3.UnitX, body)!.Value, Tolerance);
    }

    [Fact]
    public void A_ray_past_or_over_the_cylinder_misses()
    {
        var body = new CylinderBody(new Vector3(5f, 0f, 0f), 0.3f, 1.8f);

        Assert.Null(Raycast.AgainstCylinder(new Vector3(0f, 1f, 0.5f), Vector3.UnitX, body));
        Assert.Null(Raycast.AgainstCylinder(new Vector3(0f, 2f, 0f), Vector3.UnitX, body));
    }

    [Fact]
    public void A_ray_from_above_hits_the_top()
    {
        var body = new CylinderBody(new Vector3(5f, 0f, 0f), 0.3f, 1.8f);

        Assert.Equal(1.2f, Raycast.AgainstCylinder(new Vector3(5f, 3f, 0f), -Vector3.UnitY, body)!.Value, Tolerance);
    }

    [Fact]
    public void A_cylinder_behind_the_start_is_not_hit()
    {
        var body = new CylinderBody(new Vector3(-5f, 0f, 0f), 0.3f, 1.8f);

        Assert.Null(Raycast.AgainstCylinder(new Vector3(0f, 1f, 0f), Vector3.UnitX, body));
    }

    [Theory]
    [InlineData(1f, 0f, 0f, 2f)]    // straight at the -X face
    [InlineData(0f, 0f, 1f, 4f)]    // straight at the -Z face
    public void A_box_is_hit_at_its_near_face(float dx, float dy, float dz, float expected)
    {
        var min = new Vector3(-1f, 0f, -1f) + new Vector3(3f, 0f, 5f) * new Vector3(dx, 0f, dz);
        var max = min + new Vector3(2f, 3f, 2f);

        var distance = Raycast.AgainstBox(new Vector3(0f, 1f, 0f), new Vector3(dx, dy, dz), min, max);

        Assert.NotNull(distance);
        Assert.Equal(expected, distance!.Value, 0.0001f);
    }

    [Fact]
    public void A_box_beside_or_behind_the_ray_is_missed()
    {
        var min = new Vector3(2f, 0f, -1f);
        var max = new Vector3(4f, 3f, 1f);

        Assert.Null(Raycast.AgainstBox(new Vector3(0f, 1f, 0f), -Vector3.UnitX, min, max));
        Assert.Null(Raycast.AgainstBox(new Vector3(0f, 1f, 0f), Vector3.UnitZ, min, max));
        Assert.Null(Raycast.AgainstBox(new Vector3(0f, 4f, 0f), Vector3.UnitX, min, max));
    }

    [Fact]
    public void A_ray_starting_inside_a_box_hits_it_at_once()
    {
        Assert.Equal(0f, Raycast.AgainstBox(new Vector3(1f, 1f, 1f), Vector3.UnitX, Vector3.Zero, new Vector3(2f, 2f, 2f)));
    }
}
