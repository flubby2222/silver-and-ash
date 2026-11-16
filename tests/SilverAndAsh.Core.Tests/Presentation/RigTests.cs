using System.Numerics;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

public class RigTests
{
    private const float Tolerance = 0.0001f;

    // An arm: shoulder at 1 m height, elbow 0.5 m below it, a hand 0.5 m below the elbow.
    private static readonly RigPart[] Arm = [new("upper", null), new("lower", "upper")];
    private static readonly Vector3[] ArmPivots = [new(0f, 1f, 0f), new(0f, 0.5f, 0f)];

    [Fact]
    public void Lifting_brings_the_front_end_up_and_a_hanging_part_forward()
    {
        var front = Vector3.Transform(Vector3.UnitZ, Rig.Lift(90f));
        var hanging = Vector3.Transform(-Vector3.UnitY, Rig.Lift(90f));

        AssertClose(Vector3.UnitY, front);
        AssertClose(Vector3.UnitZ, hanging);
    }

    [Fact]
    public void Unturned_every_corner_stays_where_the_file_has_it()
    {
        var rig = new Rig(Arm, ArmPivots);
        var frames = new Matrix4x4[2];

        rig.Pose([Matrix4x4.Identity, Matrix4x4.Identity], Matrix4x4.Identity, frames);

        // The hand, 0.5 m below the elbow pivot, is at 0 m height in the figure.
        AssertClose(Vector3.Zero, Vector3.Transform(new Vector3(0f, -0.5f, 0f), frames[1]));
        AssertClose(ArmPivots[0], Vector3.Transform(Vector3.Zero, frames[0]));
    }

    [Fact]
    public void A_child_follows_its_parent_around_the_parents_pivot()
    {
        var rig = new Rig(Arm, ArmPivots);
        var frames = new Matrix4x4[2];

        // The upper arm lifted straight forward: elbow and hand are now in front of the shoulder, at its height.
        rig.Pose([Rig.Lift(90f), Matrix4x4.Identity], Matrix4x4.Identity, frames);

        AssertClose(new Vector3(0f, 1f, 0.5f), Vector3.Transform(Vector3.Zero, frames[1]));
        AssertClose(new Vector3(0f, 1f, 1f), Vector3.Transform(new Vector3(0f, -0.5f, 0f), frames[1]));
    }

    [Fact]
    public void A_part_turns_around_its_own_pivot_which_stays_in_place()
    {
        var rig = new Rig(Arm, ArmPivots);
        var frames = new Matrix4x4[2];

        rig.Pose([Matrix4x4.Identity, Rig.Lift(90f)], Matrix4x4.Identity, frames);

        AssertClose(ArmPivots[1], Vector3.Transform(Vector3.Zero, frames[1]));
        AssertClose(new Vector3(0f, 0.5f, 0.5f), Vector3.Transform(new Vector3(0f, -0.5f, 0f), frames[1]));
    }

    [Fact]
    public void The_whole_figure_is_placed_in_the_world_last()
    {
        var rig = new Rig(Arm, ArmPivots);
        var frames = new Matrix4x4[2];
        var figure = Matrix4x4.CreateRotationY(MathF.PI / 2f) * Matrix4x4.CreateTranslation(10f, 0f, 5f);

        rig.Pose([Rig.Lift(90f), Matrix4x4.Identity], figure, frames);

        // Facing +z turned a quarter around y faces +x: the lifted hand points 1 m that way from (10, _, 5).
        AssertClose(new Vector3(11f, 1f, 5f), Vector3.Transform(new Vector3(0f, -0.5f, 0f), frames[1]));
    }

    [Fact]
    public void A_parent_must_come_before_its_child()
    {
        Assert.Throws<ArgumentException>(() => new Rig([new("lower", "upper"), new("upper", null)], ArmPivots));
    }

    [Fact]
    public void A_name_may_be_there_only_once()
    {
        Assert.Throws<ArgumentException>(() => new Rig([new("upper", null), new("upper", null)], ArmPivots));
    }

    private static void AssertClose(Vector3 expected, Vector3 actual)
    {
        Assert.True(Vector3.Distance(expected, actual) < Tolerance, $"Expected {expected}, got {actual}.");
    }
}
