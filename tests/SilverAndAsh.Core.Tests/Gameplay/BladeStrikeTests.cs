using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class BladeStrikeTests
{
    private const float BodyRadius = 0.3f;

    // An open room; the eye stands in the middle (cell 5, 5 -> x = z = 11 m) at eye height.
    private static readonly Level Field = LevelParser.Parse(string.Join('\n',
        Enumerable.Range(0, 11).Select(row => row == 5 ? ".....P....." : "...........")));

    private static readonly Vector3 Eye = new(11f, Tuning.Player.EyeHeight, 11f);
    private static readonly Vector3 Ahead = Vector3.UnitZ;

    [Fact]
    public void A_body_right_in_front_is_hit()
    {
        Assert.True(Hits(At(0f, 1.5f)));
    }

    [Fact]
    public void The_range_counts_to_the_surface_of_the_body()
    {
        var range = Tuning.Combat.BladeRange;

        Assert.True(Hits(At(0f, range + BodyRadius - 0.05f)));
        Assert.False(Hits(At(0f, range + BodyRadius + 0.1f)));
    }

    [Fact]
    public void The_cone_is_as_wide_as_the_blade_angle()
    {
        var half = Tuning.Combat.BladeAngle / 2f;

        Assert.True(Hits(At(half - 5f, 1.5f)));
        Assert.True(Hits(At(-(half - 5f), 1.5f)));
        Assert.False(Hits(At(half + 5f, 1.5f)));
    }

    [Fact]
    public void Nothing_behind_is_hit()
    {
        Assert.False(Hits(At(180f, 1f)));
    }

    [Fact]
    public void The_cone_also_points_up_and_down()
    {
        // Same body at eye height in front: looking 30 degrees down still hits, 60 degrees up does not.
        var body = At(0f, 1.2f);
        var down30 = Vector3.Normalize(new Vector3(0f, -MathF.Tan(30f * MathF.PI / 180f), 1f));
        var up60 = Vector3.Normalize(new Vector3(0f, MathF.Tan(60f * MathF.PI / 180f), 1f));

        Assert.True(BladeStrike.Hits(Field, Eye, down30, body));
        Assert.False(BladeStrike.Hits(Field, Eye, up60, body));
    }

    [Fact]
    public void A_wall_in_between_protects_the_body()
    {
        // The player stands in the corner of cell (0, 0) next to the wall cell (1, 0); the body stands diagonally
        // behind the wall's corner, close enough for the blade - but the straight line runs through the wall.
        var body = new CylinderBody(new Vector3(3f, 0f, 3f), BodyRadius, Tuning.Combat.DummyHeight);
        var eye = new Vector3(1.65f, Tuning.Player.EyeHeight, 1.3f);
        var direction = Vector3.Normalize(new Vector3(body.Position.X, eye.Y, body.Position.Z) - eye);

        Assert.False(BladeStrike.Hits(LevelParser.Parse("P#\n.D"), eye, direction, body));
        Assert.True(BladeStrike.Hits(LevelParser.Parse("P.\n.D"), eye, direction, body));
    }

    private static bool Hits(CylinderBody body) => BladeStrike.Hits(Field, Eye, Ahead, body);

    /// <summary>A dummy-sized body on the floor, <paramref name="degrees"/> to the side of straight ahead.</summary>
    private static CylinderBody At(float degrees, float distance)
    {
        var radians = degrees * MathF.PI / 180f;
        var position = new Vector3(Eye.X + MathF.Sin(radians) * distance, 0f, Eye.Z + MathF.Cos(radians) * distance);
        return new CylinderBody(position, BodyRadius, Tuning.Combat.DummyHeight);
    }
}
