using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

public class GormPoseTests
{
    private const float Tolerance = 0.001f;

    [Fact]
    public void The_model_file_has_every_part_with_the_pivots_of_the_hand_over()
    {
        var file = GormFile();

        Assert.Equal(GormPose.Parts.Select(part => part.Name).Order(), file.Select(part => part.Name).Order());
        Assert.Equal(new Vector3(0f, 2.5f, 0.42f), Pivot(file, GormPose.Head), new VectorComparer());
        Assert.Equal(new Vector3(-1.04f, 1.38f, 0.42f), Pivot(file, GormPose.ChainForearm), new VectorComparer());
        Assert.Equal(new Vector3(1.0f, 0.56f, 0.58f), Pivot(file, GormPose.Club), new VectorComparer());
    }

    [Fact]
    public void Standing_his_mask_lies_in_the_head_zone_of_his_hit_body()
    {
        var mask = MaskCentre(Motion(GormState.Walking));

        AssertInHeadZone(mask, BodySizes.Gorm.Height);
    }

    [Fact]
    public void In_phase_2_his_mask_still_lies_in_the_head_zone()
    {
        var mask = MaskCentre(Motion(GormState.Walking) with { Phase2 = true });

        AssertInHeadZone(mask, BodySizes.Gorm.Height);
    }

    [Fact]
    public void With_the_club_stuck_he_bends_forward_so_far_that_his_mask_goes_down_with_the_head_zone()
    {
        var mask = MaskCentre(Motion(GormState.Stuck, GormAttack.Hammer));

        AssertInHeadZone(mask, BodySizes.Gorm.Height - Gorm.StuckBend);
    }

    [Theory]
    [InlineData(-90f)]
    [InlineData(-45f)]
    [InlineData(0f)]
    [InlineData(45f)]
    [InlineData(90f)]
    public void Swinging_his_fist_holds_the_chain_at_the_height_it_hits(float degrees)
    {
        var area = Balance.Default.Gorm;
        var fist = FistAt(Motion(GormState.Swinging, GormAttack.Swing) with { SwingAngle = degrees * MathF.PI / 180f });

        Assert.InRange(fist.Y, area.SwingLow, area.SwingHigh);
    }

    [Fact]
    public void Winding_up_the_swing_he_whirls_the_chain_above_his_head_but_below_the_ceiling()
    {
        // The whirled chain lies level at his fist: above his head (and his mask), with room for the links (their
        // half width) below the lowest ceiling of a room. Phase 2 bends him forward, the fist must stay up.
        var linkHalf = 0.12f;
        foreach (var phase2 in new[] { false, true })
        {
            var fist = FistAt(Motion(GormState.WindingUp, GormAttack.Swing) with { Progress = 1f, Phase2 = phase2 });

            Assert.InRange(fist.Y, BodySizes.Gorm.Height - BodySizes.Gorm.HeadHeight + linkHalf, SilverAndAsh.Core.World.Cell.DefaultCeilingHeight - linkHalf);
        }
    }

    [Fact]
    public void Winding_up_the_hammer_he_raises_the_club_above_his_head_but_not_into_the_ceiling()
    {
        var file = GormFile();
        var club = GormPose.IndexOf(GormPose.Club);
        var frames = Frames(Motion(GormState.WindingUp, GormAttack.Hammer) with { Progress = 1f });
        var part = file.Single(part => part.Name == GormPose.Club);

        var top = Corners(part).Max(corner => Vector3.Transform(corner, frames[club]).Y);

        Assert.InRange(top, BodySizes.Gorm.Height, SilverAndAsh.Core.World.Cell.DefaultCeilingHeight);
    }

    [Fact]
    public void Swinging_the_chain_arm_follows_the_chain_from_the_front_to_his_right()
    {
        Assert.Equal(90f, GormPose.ArmTurn(0f), Tolerance);
        Assert.Equal(0f, GormPose.ArmTurn(MathF.PI / 2f), Tolerance);
        Assert.Equal(120f, GormPose.ArmTurn(-MathF.PI / 2f), Tolerance);

        // His right is -x: with the chain out to his right, the fist is far out on -x; straight ahead, in front of him.
        var right = FistAt(Motion(GormState.Swinging, GormAttack.Swing) with { SwingAngle = MathF.PI / 2f });
        var ahead = FistAt(Motion(GormState.Swinging, GormAttack.Swing) with { SwingAngle = 0f });
        Assert.True(right.X < -1.2f, $"Fist {right}: out to his right.");
        Assert.True(ahead.Z > 1f, $"Fist {ahead}: in front of him.");
    }

    [Fact]
    public void The_angle_of_the_sweep_counts_from_his_facing_towards_his_right()
    {
        var facing = Vector3.Normalize(new Vector3(1f, 0f, 1f));
        var right = new Vector3(-facing.Z, 0f, facing.X);

        Assert.Equal(0f, GormPose.AngleOf(facing, facing), Tolerance);
        Assert.Equal(MathF.PI / 2f, GormPose.AngleOf(right, facing), Tolerance);
        Assert.Equal(-MathF.PI / 2f, GormPose.AngleOf(-right, facing), Tolerance);
    }

    [Fact]
    public void Roaring_he_comes_quickly_into_the_pose_holds_it_and_goes_back()
    {
        var head = GormPose.IndexOf(GormPose.Head);

        Assert.Equal(GormPose.Rest.GetValueOrDefault(GormPose.Head).Lift, Joints(Motion(GormState.Roaring) with { Progress = 0f })[head].Lift, Tolerance);
        Assert.Equal(GormPose.Roar[GormPose.Head].Lift, Joints(Motion(GormState.Roaring) with { Progress = 0.5f })[head].Lift, Tolerance);
    }

    [Fact]
    public void Walking_the_legs_swing_against_each_other()
    {
        var joints = Joints(Motion(GormState.Walking) with { StridePhase = MathF.PI / 2f, Stride = 1f });

        Assert.Equal(18f, joints[GormPose.IndexOf(GormPose.ThighClubSide)].Lift, Tolerance);
        Assert.Equal(-18f, joints[GormPose.IndexOf(GormPose.ThighChainSide)].Lift, Tolerance);
    }

    [Fact]
    public void Both_arms_mirror_each_other_in_the_same_pose()
    {
        // Phase 2 rest holds both arms alike (8, 18): seen from each shoulder (where the bent torso has taken it - the
        // shoulders sit at different heights), the elbows lie mirrored.
        var frames = Frames(Motion(GormState.Walking) with { Phase2 = true });
        var club = frames[GormPose.IndexOf(GormPose.ClubForearm)].Translation - frames[GormPose.IndexOf(GormPose.ClubUpperArm)].Translation;
        var chain = frames[GormPose.IndexOf(GormPose.ChainForearm)].Translation - frames[GormPose.IndexOf(GormPose.ChainUpperArm)].Translation;

        Assert.True(club.X > 0.25f, $"Club elbow {club}: spread outwards.");
        Assert.Equal(club.X, -chain.X, 0.01f);
        Assert.Equal(club.Y, chain.Y, 0.01f);
        Assert.Equal(club.Z, chain.Z, 0.01f);
    }

    [Fact]
    public void The_chain_starts_at_the_fist_and_its_last_link_ends_exactly_where_it_reaches()
    {
        var from = new Vector3(-1.2f, 1.35f, 0.3f);
        var to = new Vector3(-8f, 1.35f, 1f);
        var links = new List<Matrix4x4>();

        GormChain.Lay(from, to, links);

        var half = new Vector3(0f, GormChain.LinkLength / 2f, 0f);
        var along = Vector3.Normalize(to - from);
        var ends = links.SelectMany(link => new[] { Vector3.Transform(-half, link), Vector3.Transform(half, link) }).ToList();
        Assert.Equal(Vector3.Dot(from, along), ends.Min(end => Vector3.Dot(end, along)), Tolerance);
        Assert.Equal(Vector3.Dot(to, along), ends.Max(end => Vector3.Dot(end, along)), Tolerance);

        // No gaps: neighbouring links lie no further apart than the spacing.
        for (var i = 1; i < links.Count; i++)
        {
            Assert.True(Vector3.Distance(links[i].Translation, links[i - 1].Translation) <= GormChain.Spacing + Tolerance);
        }
    }

    [Fact]
    public void The_links_alternate_turned_a_quarter_round_the_chain()
    {
        var links = new List<Matrix4x4>();
        GormChain.Lay(Vector3.Zero, new Vector3(0f, 0f, 2f), links);

        var first = Vector3.TransformNormal(Vector3.UnitX, links[0]);
        var second = Vector3.TransformNormal(Vector3.UnitX, links[1]);
        Assert.Equal(0f, Vector3.Dot(first, second), Tolerance);
        Assert.Equal(1f, MathF.Abs(Vector3.Dot(first, Vector3.TransformNormal(Vector3.UnitX, links[2]))), Tolerance);
    }

    [Fact]
    public void The_far_end_of_the_hook_lies_exactly_at_its_tip()
    {
        var hook = GormFile().Single(part => part.Name == GormPose.Hook);
        Assert.Equal(-GormChain.HookFileLength, hook.PositionMin.Y, Tolerance);

        var tip = new Vector3(3f, 1.4f, 5f);
        var along = Vector3.Normalize(new Vector3(1f, 0f, 2f));
        var frame = GormChain.HookAt(tip, along, Vector3.UnitY);

        var far = Vector3.Transform(new Vector3(0f, hook.PositionMin.Y, 0f), frame);
        Assert.Equal(tip, far, new VectorComparer());
        Assert.Equal(GormChain.HookEye(tip, along), frame.Translation, new VectorComparer());
        Assert.Equal(GormChain.HookLength, Vector3.Distance(frame.Translation, tip), Tolerance);
    }

    [Fact]
    public void The_sweeping_chain_ends_as_far_as_the_swing_reaches_at_the_middle_of_its_height()
    {
        var gorm = Balance.Default.Gorm;
        var area = (gorm.SwingReach, gorm.SwingLow, gorm.SwingHigh);
        var position = new Vector3(10f, 0.5f, 4f);

        var end = GormChain.SwingEnd(position, Vector3.Normalize(new Vector3(-1f, 0f, 1f)), area);

        Assert.Equal(gorm.SwingReach, Vector2.Distance(new(end.X, end.Z), new(position.X, position.Z)), Tolerance);
        Assert.Equal(position.Y + ((gorm.SwingLow + gorm.SwingHigh) / 2f), end.Y, Tolerance);
    }

    [Fact]
    public void The_whirled_chain_is_as_long_as_the_swing_will_reach()
    {
        var position = new Vector3(2f, 0f, 3f);
        var fist = new Vector3(1.5f, 4.1f, 3.4f);

        var end = GormChain.WhirlEnd(position, fist, new Vector3(0f, 0f, -1f), 8f);

        Assert.Equal(new Vector3(2f, 4.1f, -5f), end, new VectorComparer());
    }

    private static void AssertInHeadZone(Vector3 mask, float top)
    {
        Assert.InRange(mask.Y, top - BodySizes.Gorm.HeadHeight, top);
        Assert.True(mask.Z > 0f, $"Mask {mask}: in front of his middle.");
        Assert.InRange(mask.X, -BodySizes.Gorm.HeadWidth / 2f, BodySizes.Gorm.HeadWidth / 2f);
    }

    private static GormMotion Motion(GormState state, GormAttack? attack = null) =>
        new(state, attack, 0.5f, false, 0f, 0f, null);

    private static GormJoint[] Joints(GormMotion motion)
    {
        var joints = new GormJoint[GormPose.Parts.Count];
        GormPose.Joints(motion, joints);
        return joints;
    }

    private static Matrix4x4[] Frames(GormMotion motion)
    {
        var file = GormFile();
        var rig = new Rig(GormPose.Parts, [.. GormPose.Parts.Select(part => Pivot(file, part.Name))]);
        var turns = new Matrix4x4[rig.Count];
        var frames = new Matrix4x4[rig.Count];
        GormPose.Turns(Joints(motion), turns);
        rig.Pose(turns, Matrix4x4.Identity, frames);
        return frames;
    }

    // The middle of the head with its iron mask (the head part is mask and skull), figure standing at the origin.
    private static Vector3 MaskCentre(GormMotion motion)
    {
        var head = GormFile().Single(part => part.Name == GormPose.Head);
        return Vector3.Transform((head.PositionMin + head.PositionMax) / 2f, Frames(motion)[GormPose.IndexOf(GormPose.Head)]);
    }

    private static Vector3 FistAt(GormMotion motion) =>
        Vector3.Transform(GormPose.Fist, Frames(motion)[GormPose.IndexOf(GormPose.ChainForearm)]);

    // The corners of a part's box in the file (around its pivot) - the club fits it closely.
    private static IEnumerable<Vector3> Corners(GlbPart part) =>
        from x in new[] { part.PositionMin.X, part.PositionMax.X }
        from y in new[] { part.PositionMin.Y, part.PositionMax.Y }
        from z in new[] { part.PositionMin.Z, part.PositionMax.Z }
        select new Vector3(x, y, z);

    private static Vector3 Pivot(IReadOnlyList<GlbPart> file, string name) => file.Single(part => part.Name == name).Pivot;

    private static IReadOnlyList<GlbPart> GormFile() =>
        GlbScene.Read(File.ReadAllBytes(RepositoryFiles.Find("assets/models/gorm.glb")));

    private sealed class VectorComparer : IEqualityComparer<Vector3>
    {
        public bool Equals(Vector3 x, Vector3 y) => Vector3.Distance(x, y) < Tolerance;

        public int GetHashCode(Vector3 obj) => 0;
    }
}
