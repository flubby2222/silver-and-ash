namespace SilverAndAsh.Core;

/// <summary>
/// Turns real elapsed time (which varies from frame to frame) into a whole number of
/// fixed simulation ticks. Leftover time that is shorter than one tick is kept for the next frame,
/// so no time is lost and the simulation runs at exactly <see cref="SimulationClock.TicksPerSecond"/>.
/// </summary>
public sealed class TickAccumulator
{
    /// <summary>
    /// Upper limit of ticks per frame. If the game freezes for a moment (e.g. the window is dragged),
    /// we do not try to catch up on all the missed time at once - that would make the next frame
    /// even slower ("spiral of death"). The game simply runs a little slower for that moment.
    /// </summary>
    public const int MaxTicksPerFrame = 5;

    private float _leftoverSeconds;

    /// <summary>Time that has been collected but is not yet enough for a whole tick.</summary>
    public float LeftoverSeconds => _leftoverSeconds;

    /// <summary>
    /// Adds the real time of one frame and returns how many simulation ticks should run now.
    /// </summary>
    public int Advance(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), elapsedSeconds, "Elapsed time must be a finite, non-negative number.");
        }

        _leftoverSeconds += elapsedSeconds;

        var ticks = 0;
        while (_leftoverSeconds >= SimulationClock.SecondsPerTick && ticks < MaxTicksPerFrame)
        {
            _leftoverSeconds -= SimulationClock.SecondsPerTick;
            ticks++;
        }

        if (ticks == MaxTicksPerFrame)
        {
            // Drop whatever we could not catch up on.
            _leftoverSeconds = MathF.Min(_leftoverSeconds, SimulationClock.SecondsPerTick);
        }

        return ticks;
    }
}
