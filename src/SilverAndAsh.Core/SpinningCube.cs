namespace SilverAndAsh.Core;

/// <summary>
/// Placeholder "world" for the walking skeleton: a cube that turns around its vertical axis.
/// It only exists to prove that simulation (here) and rendering (in the Game project) work together.
/// </summary>
public sealed class SpinningCube
{
    /// <summary>Rotation speed: a quarter turn (90 degrees) per second.</summary>
    public const float RadiansPerSecond = MathF.PI / 2f;

    /// <summary>Current rotation around the vertical axis, always between 0 (inclusive) and 2π (exclusive).</summary>
    public float RotationRadians { get; private set; }

    /// <summary>Advances the cube by exactly one simulation tick.</summary>
    public void Tick()
    {
        var next = RotationRadians + RadiansPerSecond * SimulationClock.SecondsPerTick;
        RotationRadians = next % MathF.Tau;
    }
}
