namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Findings for issue #535, step 1: what enemies do TODAY with height differences in a room. These tests record the
/// current behaviour (also where it is wrong); tests named "Finding_535_..." show a misbehaviour that step 2 is meant
/// to change. All runs use <see cref="Balance.Default"/> and drive the real world tick by tick.
/// </summary>
public class HeightFindingsTests
{
    private const int GhoulSeconds = 10;
    private const int BatSeconds = 15;

    [Theory]
    [InlineData(HeightCase.StepUp)]
    [InlineData(HeightCase.Podium)]
    [InlineData(HeightCase.Trench)]
    [InlineData(HeightCase.InPit)]
    public void Ghoul_reaches_the_player_over_a_quarter_metre_step(HeightCase room)
    {
        var seen = HeightFindingsKit.Observe(room, 0.25f, bat: false, GhoulSeconds);

        Assert.True(seen.Reached, seen.Line());
        Assert.Equal(0, seen.Reversals);
    }

    [Theory]
    [InlineData(HeightCase.StepUp, 0.5f)]
    [InlineData(HeightCase.StepUp, 1f)]
    [InlineData(HeightCase.StepUp, 2f)]
    [InlineData(HeightCase.Podium, 0.5f)]
    [InlineData(HeightCase.Podium, 1f)]
    [InlineData(HeightCase.Podium, 2f)]
    [InlineData(HeightCase.Trench, 0.5f)]
    [InlineData(HeightCase.Trench, 1f)]
    [InlineData(HeightCase.Trench, 2f)]
    [InlineData(HeightCase.InPit, 0.5f)]
    [InlineData(HeightCase.InPit, 1f)]
    [InlineData(HeightCase.InPit, 2f)]
    public void Finding_535_ghoul_stands_still_when_no_path_climbs_at_most_the_step_height(HeightCase room, float height)
    {
        var seen = HeightFindingsKit.Observe(room, height, bat: false, GhoulSeconds);

        // Today: no path -> no waypoints -> Stand(). It does not even walk to the edge, and it does not jitter.
        Assert.False(seen.Reached, seen.Line());
        Assert.True(seen.MovedMetres < 0.5f, seen.Line());
        Assert.Equal(0, seen.Reversals);
    }

    [Theory]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(2f)]
    public void Ghoul_drops_down_any_height_and_reaches_the_player(float height)
    {
        var seen = HeightFindingsKit.Observe(HeightCase.StepDown, height, bat: false, GhoulSeconds);

        Assert.True(seen.Reached, seen.Line());
        Assert.Equal(-height, seen.EndHeightAboveStart, 0.01f);
        Assert.Equal(0, seen.TicksInsideFloor);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void Ghoul_takes_the_stairs_when_the_direct_step_is_too_high(float height)
    {
        var direct = HeightFindingsKit.Observe(HeightCase.StepUp, 0.25f, bat: false, GhoulSeconds);
        var seen = HeightFindingsKit.Observe(HeightCase.StepUpWithStairs, height, bat: false, GhoulSeconds);

        Assert.True(seen.Reached, seen.Line());
        Assert.True(seen.MovedMetres > direct.MovedMetres + 3f, $"{direct.Line()}\n{seen.Line()}");
        Assert.Equal(height, seen.EndHeightAboveStart, 0.01f);
    }

    [Theory]
    [InlineData(HeightCase.StepUp, 0.5f)]
    [InlineData(HeightCase.StepUp, 2f)]
    [InlineData(HeightCase.StepDown, 2f)]
    [InlineData(HeightCase.Podium, 1f)]
    [InlineData(HeightCase.Trench, 2f)]
    [InlineData(HeightCase.InPit, 2f)]
    [InlineData(HeightCase.StepUpWithStairs, 1f)]
    public void Bat_reaches_the_player_at_heights_up_to_two_metres(HeightCase room, float height)
    {
        var seen = HeightFindingsKit.Observe(room, height, bat: true, BatSeconds);

        Assert.True(seen.Reached, seen.Line());
        Assert.Equal(0, seen.TicksInsideFloor);
    }

    [Theory]
    [InlineData(HeightCase.StepUp)]
    [InlineData(HeightCase.InPit)]
    public void Finding_535_bat_dips_below_the_floor_for_some_ticks_at_three_metres(HeightCase room)
    {
        var seen = HeightFindingsKit.Observe(room, 3f, bat: true, BatSeconds);

        // Today: it still reaches the player, but for a few ticks it is below the floor of the cell under it.
        Assert.True(seen.Reached, seen.Line());
        Assert.True(seen.TicksInsideFloor > 0, seen.Line());
    }
}
