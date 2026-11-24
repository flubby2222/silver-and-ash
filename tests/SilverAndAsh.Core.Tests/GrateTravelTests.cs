using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests;

/// <summary>
/// The rule of the portcullis (issue #695): <see cref="GrateTravel"/> decides how high a grate hangs and when it blocks;
/// the picture (<see cref="GratePose"/>) reads the same numbers. The shape of the motion itself is tested in
/// GratePoseTests, the door that follows it tick by tick in GrateTests.
/// </summary>
public class GrateTravelTests
{
    private static readonly GratesBalance Grates = Balance.Default.Grates;

    [Fact]
    public void A_grate_blocks_below_the_block_height_and_not_at_or_above_it()
    {
        Assert.True(GrateTravel.Blocks(0f, Grates));
        Assert.True(GrateTravel.Blocks(Grates.BlockHeight - 0.01f, Grates));
        Assert.False(GrateTravel.Blocks(Grates.BlockHeight, Grates));
        Assert.False(GrateTravel.Blocks(GrateTravel.OpenHeight, Grates));
    }

    [Fact]
    public void The_block_height_comes_from_the_balance()
    {
        var low = Grates with { BlockHeight = Grates.BlockHeight / 2f };
        var height = Grates.BlockHeight * 0.75f;

        Assert.True(GrateTravel.Blocks(height, Grates));
        Assert.False(GrateTravel.Blocks(height, low));
    }

    [Fact]
    public void Blocks_needs_a_balance()
    {
        Assert.Throws<ArgumentNullException>(() => GrateTravel.Blocks(1f, null!));
        Assert.Throws<ArgumentNullException>(() => GrateTravel.Height(1, GrateMotion.Fall, null!));
        Assert.Throws<ArgumentNullException>(() => GrateTravel.ShutTick(null!));
    }

    [Fact]
    public void The_pose_reads_the_rule()
    {
        Assert.Equal(GrateTravel.OpenHeight, GratePose.OpenHeight);
        for (var tick = 0; tick <= Grates.LiftTicks; tick++)
        {
            Assert.Equal(GrateTravel.Height(tick, GrateMotion.Fall, Grates), GratePose.Height(tick, GrateMotion.Fall, Grates));
            Assert.Equal(GrateTravel.Height(tick, GrateMotion.Lift, Grates), GratePose.Height(tick, GrateMotion.Lift, Grates));
        }

        Assert.Equal(GrateTravel.ShutTick(Grates), GratePose.ShutTick(Grates));
    }
}
