using System.Numerics;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class FirstPersonViewTests
{
    private const float Tolerance = 0.0001f;

    [Fact]
    public void A_new_view_looks_horizontally_along_plus_z()
    {
        var view = new FirstPersonView();

        AssertVector(new Vector3(0f, 0f, 1f), view.Forward);
    }

    [Fact]
    public void Moving_the_mouse_right_turns_right()
    {
        var view = new FirstPersonView();
        var rightBefore = view.FlatRight;

        view.ApplyMouse(new Vector2(100f, 0f));

        // Looking along +Z, "right" is -X - and the new view direction leans towards the old right side.
        AssertVector(new Vector3(-1f, 0f, 0f), rightBefore);
        Assert.True(view.Forward.X < 0f);
        Assert.True(Vector3.Dot(view.Forward, rightBefore) > 0f);
    }

    [Fact]
    public void Moving_the_mouse_up_looks_up()
    {
        var view = new FirstPersonView();

        // Screen coordinates: up is negative Y.
        view.ApplyMouse(new Vector2(0f, -100f));

        Assert.True(view.PitchRadians > 0f);
        Assert.True(view.Forward.Y > 0f);
    }

    [Fact]
    public void Turning_uses_the_sensitivity()
    {
        var view = new FirstPersonView();

        view.ApplyMouse(new Vector2(-200f, 0f), sensitivity: 0.001f);

        Assert.Equal(0.2f, view.YawRadians, Tolerance);
    }

    [Theory]
    [InlineData(-100000f)]
    [InlineData(100000f)]
    public void Looking_up_or_down_stops_just_before_90_degrees(float mouseY)
    {
        var view = new FirstPersonView();

        view.ApplyMouse(new Vector2(0f, mouseY));

        Assert.Equal(Tuning.Controls.MaxPitchRadians, MathF.Abs(view.PitchRadians), Tolerance);
        Assert.True(MathF.Abs(view.PitchRadians) < MathF.PI / 2f);
    }

    [Fact]
    public void Turning_a_lot_keeps_the_angle_small()
    {
        var view = new FirstPersonView();

        for (var i = 0; i < 1000; i++)
        {
            view.ApplyMouse(new Vector2(500f, 0f));
        }

        Assert.InRange(view.YawRadians, -MathF.PI, MathF.PI);
    }

    [Fact]
    public void Forward_is_always_a_unit_vector()
    {
        var view = new FirstPersonView();

        view.ApplyMouse(new Vector2(123f, -456f));

        Assert.Equal(1f, view.Forward.Length(), Tolerance);
    }

    [Fact]
    public void Looking_up_does_not_change_the_walking_directions()
    {
        var view = new FirstPersonView();
        view.ApplyMouse(new Vector2(300f, 0f));
        var flatBefore = view.FlatForward;

        view.ApplyMouse(new Vector2(0f, -300f));

        AssertVector(flatBefore, view.FlatForward);
        Assert.Equal(0f, view.FlatForward.Y);
        Assert.Equal(0f, Vector3.Dot(view.FlatForward, view.FlatRight), Tolerance);
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, Tolerance);
        Assert.Equal(expected.Y, actual.Y, Tolerance);
        Assert.Equal(expected.Z, actual.Z, Tolerance);
    }
}
