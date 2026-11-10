namespace SilverAndAsh.Core.Tests;

public class TickAccumulatorTests
{
    private const float OneTick = SimulationClock.SecondsPerTick;

    [Fact]
    public void Exactly_one_tick_of_time_gives_one_tick()
    {
        var accumulator = new TickAccumulator();

        Assert.Equal(1, accumulator.Advance(OneTick));
    }

    [Fact]
    public void Less_than_one_tick_is_kept_for_the_next_frame()
    {
        var accumulator = new TickAccumulator();

        Assert.Equal(0, accumulator.Advance(OneTick * 0.6f));
        Assert.Equal(1, accumulator.Advance(OneTick * 0.6f));
        Assert.Equal(OneTick * 0.2f, accumulator.LeftoverSeconds, 0.0001f);
    }

    [Fact]
    public void A_slow_frame_runs_several_ticks()
    {
        var accumulator = new TickAccumulator();

        // A 30 FPS frame lasts two ticks.
        Assert.Equal(2, accumulator.Advance(OneTick * 2.0001f));
    }

    [Fact]
    public void One_second_of_frames_at_144_fps_gives_60_ticks()
    {
        var accumulator = new TickAccumulator();
        var ticks = 0;

        for (var frame = 0; frame < 144; frame++)
        {
            ticks += accumulator.Advance(1f / 144f);
        }

        // Floating point rounding may leave the last tick in the leftover.
        Assert.InRange(ticks, 59, 60);
    }

    [Fact]
    public void A_long_freeze_is_capped_instead_of_catching_up()
    {
        var accumulator = new TickAccumulator();

        Assert.Equal(TickAccumulator.MaxTicksPerFrame, accumulator.Advance(2f));
        Assert.True(accumulator.LeftoverSeconds <= OneTick);
    }

    [Fact]
    public void After_a_freeze_the_backlog_is_dropped()
    {
        var accumulator = new TickAccumulator();
        accumulator.Advance(2f);

        // The next tiny frame must not try to catch up on the 2 seconds of the freeze.
        Assert.InRange(accumulator.Advance(0.001f), 0, 1);
    }

    [Fact]
    public void Alpha_tells_how_far_we_are_towards_the_next_tick()
    {
        var accumulator = new TickAccumulator();

        accumulator.Advance(OneTick * 1.25f);

        Assert.Equal(0.25f, accumulator.Alpha, 0.001f);
    }

    [Fact]
    public void Alpha_stays_between_zero_and_one()
    {
        var accumulator = new TickAccumulator();

        Assert.Equal(0f, accumulator.Alpha);
        accumulator.Advance(10f);
        Assert.InRange(accumulator.Alpha, 0f, 1f);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Invalid_elapsed_time_is_rejected(float elapsedSeconds)
    {
        var accumulator = new TickAccumulator();

        Assert.Throws<ArgumentOutOfRangeException>(() => accumulator.Advance(elapsedSeconds));
    }
}
