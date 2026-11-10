namespace SilverAndAsh.Core.Tests;

public class SimulationClockTests
{
    [Fact]
    public void Simulation_runs_at_60_ticks_per_second()
    {
        Assert.Equal(60, SimulationClock.TicksPerSecond);
        Assert.Equal(1f / 60f, SimulationClock.SecondsPerTick);
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(1f, 60)]
    [InlineData(0.5f, 30)]
    [InlineData(8f, 480)]      // Blood rage duration
    [InlineData(0.01f, 1)]     // 0.6 ticks rounds up to 1
    [InlineData(0.005f, 0)]    // 0.3 ticks rounds down to 0
    public void SecondsToTicks_converts_and_rounds_to_nearest_tick(float seconds, int expectedTicks)
    {
        Assert.Equal(expectedTicks, SimulationClock.SecondsToTicks(seconds));
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void SecondsToTicks_rejects_invalid_durations(float seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SimulationClock.SecondsToTicks(seconds));
    }
}
