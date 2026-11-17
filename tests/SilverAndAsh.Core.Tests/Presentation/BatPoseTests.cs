using System.Numerics;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

public class BatPoseTests
{
    private const float Tolerance = 0.0001f;

    [Fact]
    public void The_model_file_has_every_part_with_the_pivots_of_the_hand_over()
    {
        var file = BatFile();

        Assert.Equal(BatPose.Parts.Select(part => part.Name), file.Select(part => part.Name));
        Assert.Equal(new Vector3(0.3f, 0.04f, 0.11f), file.Single(part => part.Name == BatPose.OuterWingRight).Pivot);
    }

    [Fact]
    public void In_the_middle_of_the_beat_the_inner_wing_is_in_the_hover_pose()
    {
        // The outer wing follows later, so it is still on its way up.
        Assert.Equal(BatPose.Hover with { OuterUp = 0f }, BatPose.Angles(BatStance.Hovering, 0f) with { OuterUp = 0f });
        Assert.Equal(BatPose.Hover.OuterUp, BatPose.Angles(BatStance.Hovering, BatPose.OuterWingLag).OuterUp, Tolerance);
    }

    [Fact]
    public void The_beat_swings_the_inner_wing_up_to_the_top_and_down_to_the_bottom()
    {
        Assert.Equal(BatPose.BeatUp.InnerUp, BatPose.Angles(BatStance.Hovering, MathF.PI / 2f).InnerUp, Tolerance);
        Assert.Equal(BatPose.BeatDown.InnerUp, BatPose.Angles(BatStance.Hovering, 3f * MathF.PI / 2f).InnerUp, Tolerance);
    }

    [Fact]
    public void The_outer_wing_follows_the_inner_one_a_little_later()
    {
        var angles = BatPose.Angles(BatStance.Hovering, MathF.PI / 2f + BatPose.OuterWingLag);

        Assert.Equal(BatPose.BeatUp.OuterUp, angles.OuterUp, Tolerance);
    }

    [Theory]
    [InlineData(BatStance.Screeching)]
    [InlineData(BatStance.Diving)]
    public void Only_hovering_beats_the_wings(BatStance stance)
    {
        Assert.Equal(BatPose.Angles(stance, 0f), BatPose.Angles(stance, 1.3f));
    }

    [Fact]
    public void Hovering_it_is_wide_and_flat()
    {
        var tip = WingTip(BatStance.Hovering);

        Assert.True(tip.X > 0.6f, $"Wing tip {tip}: spread wide.");
        Assert.True(MathF.Abs(tip.Y) < tip.X / 3f, $"Wing tip {tip}: nearly flat.");
    }

    [Fact]
    public void Screeching_it_is_tall_instead_of_wide_so_the_warning_reads_in_its_outline()
    {
        var hover = WingTip(BatStance.Hovering);
        var screech = WingTip(BatStance.Screeching);

        Assert.True(screech.Y > 2f * MathF.Abs(screech.X), $"Wing tip {screech}: the wings stand almost upright.");
        Assert.True(screech.Y - hover.Y > 0.5f, $"Wing tip {screech} against {hover}: well above the hovering wings.");
    }

    [Fact]
    public void Diving_the_wings_are_swept_back_behind_the_shoulders()
    {
        var hover = WingTip(BatStance.Hovering);
        var dive = WingTip(BatStance.Diving);

        Assert.True(dive.Z < hover.Z - 0.2f, $"Wing tip {dive} against {hover}: swept back.");
        Assert.True(dive.X < hover.X, $"Wing tip {dive} against {hover}: narrower.");
    }

    [Theory]
    [InlineData(BatStance.Hovering)]
    [InlineData(BatStance.Screeching)]
    [InlineData(BatStance.Diving)]
    public void Both_wings_mirror_each_other(BatStance stance)
    {
        var right = WingTip(stance);
        var left = WingTip(stance, right: false);

        Assert.Equal(right.X, -left.X, 0.001f);
        Assert.Equal(right.Y, left.Y, 0.001f);
        Assert.Equal(right.Z, left.Z, 0.001f);
    }

    // Where the far end of an outer wing lands for a stance (figure in its own space: origin the middle of the body).
    private static Vector3 WingTip(BatStance stance, bool right = true)
    {
        var file = BatFile();
        var rig = new Rig(BatPose.Parts, [.. BatPose.Parts.Select(part => file.Single(glb => glb.Name == part.Name).Pivot)]);
        var turns = new Matrix4x4[rig.Count];
        var frames = new Matrix4x4[rig.Count];
        BatPose.Turns(BatPose.Angles(stance, 0f), turns);
        rig.Pose(turns, Matrix4x4.Identity, frames);

        var name = right ? BatPose.OuterWingRight : BatPose.OuterWingLeft;
        var wing = file.Single(part => part.Name == name);
        var index = rig.Names.ToList().IndexOf(name);
        var tip = new Vector3(right ? wing.PositionMax.X : wing.PositionMin.X, 0f, 0f);
        return Vector3.Transform(tip, frames[index]);
    }

    private static IReadOnlyList<GlbPart> BatFile() =>
        GlbScene.Read(File.ReadAllBytes(RepositoryFiles.Find("assets/models/bat.glb")));
}
