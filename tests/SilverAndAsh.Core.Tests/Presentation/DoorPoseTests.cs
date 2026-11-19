using System.Numerics;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

public class DoorPoseTests
{
    private const float Tolerance = 0.0001f;

    // The pivots as in tools/models/door.py: hinges at the outer edges of the passage, the lock on the leaf.
    private static readonly Vector3[] DoublePivots = [new(0.895f, 0f, 0f), new(-0.895f, 0f, 0f)];
    private static readonly Vector3[] LockedPivots = [new(0.865f, 0f, 0f), new(-0.4f, 1f, 0f)];

    [Theory]
    [InlineData(DoorKind.Normal, "door_normal.glb")]
    [InlineData(DoorKind.Locked, "door_locked.glb")]
    [InlineData(DoorKind.Boss, "door_boss.glb")]
    [InlineData(DoorKind.Cracked, "door_cracked.glb")]
    public void Every_kind_has_its_own_file(DoorKind kind, string file)
    {
        Assert.Equal(file, DoorPose.FileName(kind));
    }

    [Fact]
    public void Wooden_and_boss_doors_are_two_leaves_and_nothing_that_stands_still()
    {
        foreach (var kind in new[] { DoorKind.Normal, DoorKind.Boss })
        {
            Assert.Equal([new RigPart("leaf_a", null), new RigPart("leaf_b", null)], DoorPose.Parts(kind));
        }
    }

    [Fact]
    public void A_locked_door_has_one_leaf_and_the_lock_hangs_on_it()
    {
        Assert.Equal([new RigPart("leaf", null), new RigPart("lock", "leaf")], DoorPose.Parts(DoorKind.Locked));
    }

    [Fact]
    public void A_cracked_wall_is_only_its_cracks()
    {
        Assert.Equal([new RigPart("cracks", null)], DoorPose.Parts(DoorKind.Cracked));
    }

    [Theory]
    [InlineData(DoorKind.Normal, 90f)]
    [InlineData(DoorKind.Boss, 90f)]
    [InlineData(DoorKind.Locked, 80f)]
    [InlineData(DoorKind.Cracked, 0f)]
    public void Fully_open_the_leaves_turn_as_far_as_the_kind_allows(DoorKind kind, float degrees)
    {
        Assert.Equal(degrees, DoorPose.OpenDegrees(kind));
    }

    [Theory]
    [InlineData(DoorKind.Normal)]
    [InlineData(DoorKind.Locked)]
    [InlineData(DoorKind.Boss)]
    [InlineData(DoorKind.Cracked)]
    public void Closed_every_part_stands_as_in_the_file(DoorKind kind)
    {
        var turns = new Matrix4x4[DoorPose.Parts(kind).Count];

        DoorPose.Turns(kind, 0f, towardsPlusZ: true, turns);

        Assert.All(turns, turn => Assert.True(turn.IsIdentity));
    }

    [Theory]
    [InlineData(true, 1f)]
    [InlineData(false, -1f)]
    public void Fully_open_both_leaves_of_a_double_door_lie_on_the_side_away_from_the_opener(bool towardsPlusZ, float side)
    {
        var rig = new Rig(DoorPose.Parts(DoorKind.Normal), DoublePivots);
        var turns = new Matrix4x4[2];
        var frames = new Matrix4x4[2];
        DoorPose.Turns(DoorKind.Normal, 1f, towardsPlusZ, turns);
        rig.Pose(turns, Matrix4x4.Identity, frames);

        // The free edges: leaf_a reaches from its hinge 0.9 m towards -x, leaf_b towards +x.
        var edgeA = Vector3.Transform(new Vector3(-0.9f, 0f, 0f), frames[0]);
        var edgeB = Vector3.Transform(new Vector3(0.9f, 0f, 0f), frames[1]);

        Assert.Equal(0.9f * side, edgeA.Z, Tolerance);
        Assert.Equal(0.9f * side, edgeB.Z, Tolerance);
        Assert.Equal(0.895f, edgeA.X, Tolerance);
        Assert.Equal(-0.895f, edgeB.X, Tolerance);
    }

    [Fact]
    public void The_leaf_of_a_locked_door_stops_at_80_degrees_and_takes_its_lock_along()
    {
        var rig = new Rig(DoorPose.Parts(DoorKind.Locked), LockedPivots);
        var turns = new Matrix4x4[2];
        var frames = new Matrix4x4[2];
        DoorPose.Turns(DoorKind.Locked, 1f, towardsPlusZ: true, turns);
        rig.Pose(turns, Matrix4x4.Identity, frames);

        var edge = Vector3.Transform(new Vector3(-1f, 0f, 0f), frames[0]);
        var angle = MathF.Atan2(edge.Z, 0.865f - edge.X) * 180f / MathF.PI;
        var lockAt = Vector3.Transform(Vector3.Zero, frames[1]);

        Assert.Equal(80f, angle, 0.01f);
        Assert.True(lockAt.Z > 1f, $"The lock must swing with the leaf to +z, not stay at z = {lockAt.Z}.");
        Assert.Equal(1f, lockAt.Y, Tolerance);
    }

    [Fact]
    public void Halfway_in_time_the_leaves_are_halfway_and_the_swing_starts_and_ends_slowly()
    {
        Assert.Equal(0f, DoorPose.Ease(0f));
        Assert.Equal(0.5f, DoorPose.Ease(0.5f), Tolerance);
        Assert.Equal(1f, DoorPose.Ease(1f));
        Assert.True(DoorPose.Ease(0.1f) < 0.1f, "The swing should start slowly.");
        Assert.True(DoorPose.Ease(0.9f) > 0.9f, "The swing should end slowly.");
    }

    [Fact]
    public void A_door_with_an_east_west_passage_is_turned_so_the_file_s_plus_z_becomes_plus_x()
    {
        var forward = Vector3.Transform(Vector3.UnitZ, Matrix4x4.CreateRotationY(DoorPose.Yaw(RoomSide.East)));

        Assert.Equal(1f, forward.X, Tolerance);
        Assert.Equal(0f, forward.Z, Tolerance);
        Assert.Equal(DoorPose.Yaw(RoomSide.East), DoorPose.Yaw(RoomSide.West));
        Assert.Equal(0f, DoorPose.Yaw(RoomSide.North));
        Assert.Equal(0f, DoorPose.Yaw(RoomSide.South));
    }

    [Fact]
    public void The_lock_shows_only_while_the_door_is_shut()
    {
        Assert.True(DoorPose.ShowsLock(isOpen: false));
        Assert.False(DoorPose.ShowsLock(isOpen: true));
    }
}
