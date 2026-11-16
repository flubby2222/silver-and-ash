using System.Numerics;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Physics;

public class LineOfSightTests
{
    // Cell (2, 0) is a wall: x from 4 to 6, z from 0 to 2.
    private static readonly Level WithPillar = LevelParser.Parse("P.#.\n....");

    [Fact]
    public void Open_floor_is_clear()
    {
        Assert.True(LineOfSight.IsClear(WithPillar, new Vector3(1f, 1f, 3f), new Vector3(7f, 1.5f, 3f)));
    }

    [Fact]
    public void A_wall_in_between_blocks()
    {
        Assert.False(LineOfSight.IsClear(WithPillar, new Vector3(3f, 1f, 1f), new Vector3(7f, 1f, 1f)));
    }

    [Fact]
    public void The_floor_and_the_ceiling_block()
    {
        var from = new Vector3(1f, 1f, 3f);

        Assert.False(LineOfSight.IsClear(WithPillar, from, new Vector3(1f, -0.5f, 3.5f)));
        Assert.False(LineOfSight.IsClear(WithPillar, from, new Vector3(1f, Cell.DefaultCeilingHeight + 0.5f, 3.5f)));
    }

    [Fact]
    public void Into_a_pit_is_clear()
    {
        var level = LevelParser.Parse("P_");

        Assert.True(LineOfSight.IsClear(level, new Vector3(1f, 1f, 1f), new Vector3(3f, -0.5f, 1f)));
    }

    [Fact]
    public void Outside_the_level_counts_as_wall()
    {
        Assert.True(LineOfSight.IsInsideGeometry(WithPillar, new Vector3(-1f, 1f, 1f)));
        Assert.True(LineOfSight.IsInsideGeometry(WithPillar, new Vector3(5f, 1f, 1f)));
        Assert.False(LineOfSight.IsInsideGeometry(WithPillar, new Vector3(1f, 1f, 1f)));
    }
}
