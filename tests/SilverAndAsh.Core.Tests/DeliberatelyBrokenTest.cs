namespace SilverAndAsh.Core.Tests;

// TEMPORARY: proves that the CI turns red when a test fails. Reverted in the next commit.
public class DeliberatelyBrokenTest
{
    [Fact]
    public void This_test_fails_on_purpose()
    {
        Assert.Equal(61, SimulationClock.TicksPerSecond);
    }
}
