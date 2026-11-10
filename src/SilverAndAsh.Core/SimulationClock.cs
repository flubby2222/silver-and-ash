namespace SilverAndAsh.Core;

/// <summary>
/// The fixed timestep of the game simulation.
/// The world always advances in equal steps ("ticks"), no matter how fast the screen draws.
/// This keeps the game logic deterministic and testable without a window.
/// </summary>
public static class SimulationClock
{
    /// <summary>How many simulation steps happen per second of game time.</summary>
    public const int TicksPerSecond = 60;

    /// <summary>The length of one simulation step in seconds.</summary>
    public const float SecondsPerTick = 1f / TicksPerSecond;

    /// <summary>
    /// Converts a duration in seconds to a whole number of ticks, rounded to the nearest tick.
    /// Used for things like cooldowns ("0.5 seconds" = 30 ticks).
    /// </summary>
    public static int SecondsToTicks(float seconds)
    {
        if (!float.IsFinite(seconds) || seconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Duration must be a finite, non-negative number.");
        }

        // AwayFromZero: exactly half a tick rounds up (the default would round to the nearest even number).
        return (int)MathF.Round(seconds * TicksPerSecond, MidpointRounding.AwayFromZero);
    }
}
