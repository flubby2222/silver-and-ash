namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Grave Chill (issue #508): a chilled enemy walks and flies slower. The rule alone, without a world - the one place
/// every enemy asks how fast it may cover ground. Wind-ups and attacks never ask: they keep their length.
/// </summary>
public static class GraveChill
{
    /// <summary>Ticks a fresh chill lasts (at least one).</summary>
    public static int DurationTicks(GraveChillBalance values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Math.Max(1, SimulationClock.SecondsToTicks(values.Seconds));
    }

    /// <summary>
    /// The factor on an enemy's walking and flying speed: 1 without a chill, 1 minus <see cref="GraveChillBalance.Slow"/>
    /// while one lasts. A refresh never slows harder - there is only one chill.
    /// </summary>
    public static float Pace(StatusEffects status, GraveChillBalance values)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(values);
        return status.Has(StatusKind.GraveChill) ? 1f - values.Slow : 1f;
    }
}
