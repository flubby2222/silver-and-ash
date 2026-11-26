using System.Numerics;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// The stake crossbow in the hand (issue #793): the parts of its two files, what is drawn when (loaded or relaxed, with or
/// without the stake), how the crank and the stake move through a reload of any length - checked on the checked-in files.
/// </summary>
public class StakeCrossbowPoseTests
{
    [Theory]
    [InlineData(StakeCrossbowPose.LoadedFile)]
    [InlineData(StakeCrossbowPose.RelaxedFile)]
    public void Both_files_have_the_three_parts_of_the_rig(string file)
    {
        Assert.Equal(
            StakeCrossbowPose.Parts.Select(part => part.Name).Order(),
            WeaponPoseTests.Triangles(file).Select(part => part.Name).Order());
    }

    [Fact]
    public void The_loaded_bow_is_bent_back_and_the_relaxed_one_nearly_straight()
    {
        // The tips of the bow (its widest points) lie further back (smaller z) in the loaded file.
        static float TipDepth(string file) => WeaponPoseTests.Triangles(file)
            .Single(part => part.Name == StakeCrossbowPose.Crossbow)
            .Corners.MaxBy(corner => MathF.Abs(corner.X)).Z;

        Assert.True(TipDepth(StakeCrossbowPose.LoadedFile) < TipDepth(StakeCrossbowPose.RelaxedFile) - 0.01f);
    }

    [Theory]
    [InlineData(StakeCrossbowPose.LoadedFile, 0f)]
    [InlineData(StakeCrossbowPose.RelaxedFile, 0f)]
    [InlineData(StakeCrossbowPose.LoadedFile, 1f)]
    public void Every_triangle_stays_within_reach_of_the_hand_at_rest_and_in_the_kick(string file, float kick)
    {
        WeaponPoseTests.AssertWithinReach(file, StakeCrossbowPose.Pose(kick), _ => Matrix4x4.Identity);
    }

    [Theory]
    [InlineData(0.05f)]
    [InlineData(0.5f)]
    [InlineData(0.8f)]
    [InlineData(0.97f)]
    public void Every_triangle_stays_within_reach_of_the_hand_through_the_reload(float progress)
    {
        var file = StakeCrossbowPose.ShowsLoaded(0, true, progress) ? StakeCrossbowPose.LoadedFile : StakeCrossbowPose.RelaxedFile;
        var turns = new Matrix4x4[StakeCrossbowPose.Parts.Count];
        StakeCrossbowPose.Turns(progress, 1f, turns);

        WeaponPoseTests.AssertWithinReach(
            file, StakeCrossbowPose.Pose(0f, progress, 1f), name => turns[IndexOf(name)]);
    }

    [Fact]
    public void The_reload_starts_and_ends_in_the_pose_at_rest()
    {
        AssertNear(StakeCrossbowPose.Pose(0f), StakeCrossbowPose.Pose(0f, 0f, 1f));
        AssertNear(StakeCrossbowPose.Pose(0f), StakeCrossbowPose.Pose(0f, 1f, 1f));
        var (dip, roll, shift) = StakeCrossbowPose.ReloadOffsets(0.5f);
        Assert.Equal(StakeCrossbowPose.ReloadDipDegrees, dip, 3);
        Assert.Equal(StakeCrossbowPose.ReloadRollDegrees, roll, 3);
        Assert.Equal(StakeCrossbowPose.ReloadShift, shift);
        Assert.Equal((0f, 0f, Vector3.Zero), StakeCrossbowPose.ReloadOffsets(1f));
    }

    [Fact]
    public void Without_the_weight_the_reload_shows_nothing_of_its_motion()
    {
        AssertNear(StakeCrossbowPose.Pose(0f), StakeCrossbowPose.Pose(0f, 0.5f, 0f));
        var turns = new Matrix4x4[StakeCrossbowPose.Parts.Count];
        StakeCrossbowPose.Turns(0.5f, 0f, turns);
        Assert.All(turns, turn => AssertNear(Matrix4x4.Identity, turn));
    }

    [Fact]
    public void The_crank_turns_a_whole_number_of_times_and_ends_where_it_began()
    {
        Assert.Equal(0f, StakeCrossbowPose.CrankDegrees(0f));
        Assert.Equal(0f, StakeCrossbowPose.CrankDegrees(0.05f));
        Assert.Equal(360f * StakeCrossbowPose.CrankTurns, StakeCrossbowPose.CrankDegrees(StakeCrossbowPose.CockedAt), 2);
        Assert.Equal(360f * StakeCrossbowPose.CrankTurns, StakeCrossbowPose.CrankDegrees(1f), 2);
        var angles = Enumerable.Range(0, 41).Select(step => StakeCrossbowPose.CrankDegrees(step / 40f)).ToList();
        Assert.Equal(angles.Order(), angles);
    }

    [Fact]
    public void The_string_catches_and_the_stake_is_laid_in_at_the_marks_of_the_sound()
    {
        // The reload sound of balance.json's time catches 0.6 s and lays the stake in 0.4 s before its end.
        var reload = Balance.Default.StakeCrossbow.ReloadTime;

        Assert.Equal(StakeCrossbowPose.CockedAt, 1f - (0.6f / reload), 2);
        Assert.Equal(StakeCrossbowPose.StakeAt, 1f - (0.4f / reload), 2);
    }

    [Theory]
    [InlineData(1, false, 0f, true, true)]
    [InlineData(1, true, 0.1f, true, true)]
    [InlineData(0, false, 0f, false, false)]
    [InlineData(0, true, 0.5f, false, false)]
    [InlineData(0, true, 0.81f, true, false)]
    [InlineData(0, true, 0.9f, true, true)]
    public void The_file_and_the_stake_follow_the_magazine_and_the_reload(int rounds, bool reloading, float progress, bool loaded, bool stake)
    {
        Assert.Equal(loaded, StakeCrossbowPose.ShowsLoaded(rounds, reloading, progress));
        Assert.Equal(stake, StakeCrossbowPose.ShowsStake(rounds, reloading, progress));
    }

    [Fact]
    public void The_stake_is_laid_down_from_above_and_then_lies()
    {
        Assert.Equal(0f, StakeCrossbowPose.StakeLift(0.3f));
        Assert.Equal(StakeCrossbowPose.StakeDropHeight, StakeCrossbowPose.StakeLift(StakeCrossbowPose.StakeAt), 4);
        Assert.Equal(0f, StakeCrossbowPose.StakeLift(StakeCrossbowPose.StakeAt + 0.06f), 4);
        Assert.Equal(0f, StakeCrossbowPose.StakeLift(1f), 4);
    }

    private static int IndexOf(string name) => StakeCrossbowPose.Parts.ToList().FindIndex(part => part.Name == name);

    private static void AssertNear(Matrix4x4 expected, Matrix4x4 actual)
    {
        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                Assert.Equal(Element(expected, row, column), Element(actual, row, column), 4);
            }
        }
    }

    private static float Element(Matrix4x4 matrix, int row, int column) => matrix[row, column];
}
