using System.Numerics;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

public class ChestPoseTests
{
    private const float Tolerance = 0.0001f;

    // The hinge of the lid as in chest.py: on the top edge of the base, at the back (-z).
    private static readonly Vector3 Hinge = new(0f, 0.455f, -0.365f);

    [Fact]
    public void The_lid_and_the_lock_hang_on_the_base()
    {
        var rig = new Rig(ChestPose.Parts, [Vector3.Zero, Hinge, new Vector3(0f, 0.47f, 0.39f)]);

        Assert.Equal([ChestPose.Base, ChestPose.Lid, ChestPose.Lock], rig.Names);
    }

    [Fact]
    public void A_closed_chest_keeps_its_lid_as_in_the_file()
    {
        Assert.Equal(0f, ChestPose.LidDegrees(isOpen: false), Tolerance);
    }

    [Fact]
    public void An_open_lid_swings_up_and_over_the_back_edge()
    {
        var rig = new Rig(ChestPose.Parts, [Vector3.Zero, Hinge, new Vector3(0f, 0.47f, 0.39f)]);
        var frames = new Matrix4x4[3];
        rig.Pose([Matrix4x4.Identity, Rig.Lift(ChestPose.LidDegrees(isOpen: true)), Matrix4x4.Identity], Matrix4x4.Identity, frames);

        // The front edge of the lid lies 0.73 m in front of the hinge when closed.
        var frontEdge = Vector3.Transform(new Vector3(0f, 0f, 0.73f), frames[1]);

        Assert.True(frontEdge.Z < Hinge.Z, $"Open, the front edge must lie behind the hinge, not at z = {frontEdge.Z}.");
        Assert.True(frontEdge.Y > Hinge.Y + 0.5f, $"Open, the front edge must stand high above the hinge, not at y = {frontEdge.Y}.");
    }

    [Fact]
    public void The_turn_brings_the_front_where_the_plan_says()
    {
        var front = new Vector3(0f, 0f, 1f);

        var plus = Vector3.Transform(front, Matrix4x4.CreateRotationY(ChestPose.Yaw(ChestFront.PlusZ)));
        var minus = Vector3.Transform(front, Matrix4x4.CreateRotationY(ChestPose.Yaw(ChestFront.MinusZ)));

        Assert.Equal(1f, plus.Z, Tolerance);
        Assert.Equal(-1f, minus.Z, Tolerance);
        Assert.Equal(0f, minus.X, Tolerance);
    }
}
