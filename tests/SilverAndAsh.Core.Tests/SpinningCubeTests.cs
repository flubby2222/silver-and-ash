namespace SilverAndAsh.Core.Tests;

public class SpinningCubeTests
{
    private const float Tolerance = 0.0001f;

    [Fact]
    public void A_new_cube_is_not_rotated()
    {
        Assert.Equal(0f, new SpinningCube().RotationRadians);
    }

    [Fact]
    public void After_one_second_of_ticks_the_cube_has_turned_a_quarter()
    {
        var cube = new SpinningCube();

        RunTicks(cube, SimulationClock.TicksPerSecond);

        Assert.Equal(MathF.PI / 2f, cube.RotationRadians, Tolerance);
    }

    [Fact]
    public void Rotation_wraps_around_after_a_full_turn()
    {
        var cube = new SpinningCube();

        // 5 seconds = 450 degrees = one full turn plus 90 degrees.
        RunTicks(cube, SimulationClock.SecondsToTicks(5f));

        Assert.Equal(MathF.PI / 2f, cube.RotationRadians, 0.001f);
        Assert.InRange(cube.RotationRadians, 0f, MathF.Tau);
    }

    private static void RunTicks(SpinningCube cube, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            cube.Tick();
        }
    }
}
