using System.Numerics;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

public class GhoulPoseTests
{
    private const float Tolerance = 0.0001f;

    [Fact]
    public void Resting_only_the_jaw_hangs_open()
    {
        var angles = AnglesOf(new GhoulMotion(0f, 0f, 0f, 0f));

        Assert.Equal(GhoulPose.JawResting, angles[GhoulPose.Jaw], Tolerance);
        Assert.True(GhoulPose.JawResting < 0f, "The jaw opens downward.");
        Assert.All(angles.Where(pair => pair.Key != GhoulPose.Jaw), pair => Assert.Equal(0f, pair.Value, Tolerance));
    }

    [Fact]
    public void Wound_up_it_rears_up_lifts_its_arms_and_opens_its_mouth_wider()
    {
        var angles = AnglesOf(new GhoulMotion(1f, 0f, 0f, 0f));

        Assert.Equal(GhoulPose.TorsoWoundUp, angles[GhoulPose.Torso], Tolerance);
        Assert.Equal(GhoulPose.HeadWoundUp, angles[GhoulPose.Head], Tolerance);
        Assert.Equal(GhoulPose.JawWoundUp, angles[GhoulPose.Jaw], Tolerance);
        Assert.Equal(GhoulPose.UpperArmLeftWoundUp, angles[GhoulPose.UpperArmLeft], Tolerance);
        Assert.Equal(GhoulPose.UpperArmRightWoundUp, angles[GhoulPose.UpperArmRight], Tolerance);
        Assert.Equal(GhoulPose.ForearmLeftWoundUp, angles[GhoulPose.ForearmLeft], Tolerance);
        Assert.Equal(GhoulPose.ForearmRightWoundUp, angles[GhoulPose.ForearmRight], Tolerance);
        Assert.True(GhoulPose.TorsoWoundUp > 0f && GhoulPose.UpperArmRightWoundUp > 90f, "Rearing up and arms raised high.");
        Assert.True(GhoulPose.JawWoundUp < GhoulPose.JawResting, "The jaw opens further.");
    }

    [Fact]
    public void Halfway_wound_up_is_halfway_between_rest_and_wound_up()
    {
        var rest = AnglesOf(new GhoulMotion(0f, 0f, 0f, 0f));
        var full = AnglesOf(new GhoulMotion(1f, 0f, 0f, 0f));
        var half = AnglesOf(new GhoulMotion(0.5f, 0f, 0f, 0f));

        Assert.All(half, pair => Assert.Equal((rest[pair.Key] + full[pair.Key]) / 2f, pair.Value, Tolerance));
    }

    [Fact]
    public void The_blow_throws_the_torso_down_and_the_arms_forward()
    {
        var angles = AnglesOf(new GhoulMotion(0f, 1f, 0f, 0f));

        Assert.Equal(GhoulPose.TorsoLunge, angles[GhoulPose.Torso], Tolerance);
        Assert.True(angles[GhoulPose.Torso] < 0f);
        Assert.Equal(GhoulPose.UpperArmLunge, angles[GhoulPose.UpperArmLeft], Tolerance);
        Assert.Equal(GhoulPose.UpperArmLunge, angles[GhoulPose.UpperArmRight], Tolerance);
    }

    [Fact]
    public void Crawling_swings_the_arms_in_turn_and_each_leg_with_the_other_arm()
    {
        // A quarter cycle: the right arm is furthest forward.
        var angles = AnglesOf(new GhoulMotion(0f, 0f, MathF.PI / 2f, 1f));

        Assert.Equal(GhoulPose.UpperArmCrawlSwing, angles[GhoulPose.UpperArmRight], Tolerance);
        Assert.Equal(-GhoulPose.UpperArmCrawlSwing, angles[GhoulPose.UpperArmLeft], Tolerance);
        Assert.Equal(GhoulPose.ThighCrawlSwing, angles[GhoulPose.ThighLeft], Tolerance);
        Assert.Equal(-GhoulPose.ThighCrawlSwing, angles[GhoulPose.ThighRight], Tolerance);
    }

    [Fact]
    public void Standing_still_there_is_no_crawling_swing()
    {
        var still = AnglesOf(new GhoulMotion(0f, 0f, 1.3f, 0f));
        var rest = AnglesOf(new GhoulMotion(0f, 0f, 0f, 0f));

        Assert.All(still, pair => Assert.Equal(rest[pair.Key], pair.Value, Tolerance));
    }

    [Fact]
    public void Values_out_of_range_are_held_at_the_ends()
    {
        var beyond = AnglesOf(new GhoulMotion(3f, 0f, 0f, 0f));
        var full = AnglesOf(new GhoulMotion(1f, 0f, 0f, 0f));

        Assert.All(beyond, pair => Assert.Equal(full[pair.Key], pair.Value, Tolerance));
    }

    [Fact]
    public void Every_part_has_a_parent_that_comes_before_it()
    {
        // The rig checks the order - it must not throw with the ghoul's parts.
        var rig = new Rig(GhoulPose.Parts, [.. GhoulPose.Parts.Select(_ => Vector3.Zero)]);

        Assert.Equal(GhoulPose.Parts.Count, rig.Count);
    }

    [Fact]
    public void The_model_file_has_every_part_the_pose_moves()
    {
        var parts = GlbScene.Read(File.ReadAllBytes(RepositoryFiles.Find("assets/models/ghoul.glb")));

        Assert.Equal(
            GhoulPose.Parts.Select(part => part.Name).Order(),
            parts.Select(part => part.Name).Order());
    }

    [Fact]
    public void Wound_up_the_head_is_higher_than_resting()
    {
        // The pose with the pivots of the model file: rearing up must bring the head up, not down.
        var file = GlbScene.Read(File.ReadAllBytes(RepositoryFiles.Find("assets/models/ghoul.glb")));
        var rig = new Rig(GhoulPose.Parts, [.. GhoulPose.Parts.Select(part => file.Single(p => p.Name == part.Name).Pivot)]);
        var head = GhoulPose.Parts.ToList().FindIndex(part => part.Name == GhoulPose.Head);

        Assert.True(HeadPosition(rig, head, 1f).Y > HeadPosition(rig, head, 0f).Y + 0.1f);
    }

    private static Vector3 HeadPosition(Rig rig, int head, float windUp)
    {
        var degrees = new float[rig.Count];
        GhoulPose.Angles(new GhoulMotion(windUp, 0f, 0f, 0f), degrees);
        var turns = degrees.Select(Rig.Lift).ToArray();
        var frames = new Matrix4x4[rig.Count];
        rig.Pose(turns, Matrix4x4.Identity, frames);
        return Vector3.Transform(Vector3.Zero, frames[head]);
    }

    private static Dictionary<string, float> AnglesOf(GhoulMotion motion)
    {
        var degrees = new float[GhoulPose.Parts.Count];
        GhoulPose.Angles(motion, degrees);
        return GhoulPose.Parts.Select((part, i) => (part.Name, degrees[i])).ToDictionary(pair => pair.Name, pair => pair.Item2);
    }
}
