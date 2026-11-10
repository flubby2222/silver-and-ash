namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The light and fog formulas of the game. The graphics card runs the same formulas in the level shader
/// (assets/shaders/level.fs) for every pixel - here they live once more in C#, so they can be tested.
/// If you change one, change the other.
/// </summary>
public static class Lighting
{
    /// <summary>
    /// Brightness of the lantern the player carries: 1 right in front of the player, falling evenly to
    /// <paramref name="minimum"/> at <paramref name="range"/> metres and staying there.
    /// </summary>
    public static float LanternBrightness(float distance, float range, float minimum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(range);
        var t = Math.Clamp(distance / range, 0f, 1f);
        return 1f - (1f - minimum) * t;
    }

    /// <summary>
    /// How much of a point disappears in the fog: 0 (clear) up to <paramref name="start"/> metres,
    /// 1 (only fog colour) from <paramref name="end"/> metres on, with a soft S-curve in between ("smoothstep").
    /// </summary>
    public static float FogAmount(float distance, float start, float end)
    {
        if (end <= start)
        {
            throw new ArgumentException("The fog must end after it starts.", nameof(end));
        }

        var t = Math.Clamp((distance - start) / (end - start), 0f, 1f);

        // Smoothstep: starts and ends gently instead of with a visible kink.
        return t * t * (3f - 2f * t);
    }
}
