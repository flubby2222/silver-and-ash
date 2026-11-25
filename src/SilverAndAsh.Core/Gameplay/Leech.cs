namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Leech (issue #510): when it takes hold, a share of the damage a hit dealt heals the player who dealt it - at once, with
/// no duration and no timer, never above that player's most health. Health is a whole number, so what a hit gives is
/// rarely whole: the fractions are saved up per player (<see cref="Player.LeechSaved"/>) until they make a whole point.
/// The rule alone, without a world.
/// </summary>
public static class Leech
{
    /// <summary>
    /// The health <paramref name="damage"/> is worth to Leech, fractions included. In <see cref="decimal"/>: the share
    /// from the balancing file (0.025) is kept exactly, so many small hits add up to exactly what one big hit gives -
    /// and the same on every machine (online co-op).
    /// </summary>
    public static decimal HealthFrom(int damage, LeechBalance values)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(damage);
        ArgumentNullException.ThrowIfNull(values);
        return damage * (decimal)values.Share;
    }

    /// <summary>
    /// <paramref name="saved"/> health split into the whole points to heal now and the fraction to save for later.
    /// </summary>
    public static (int Whole, decimal Fraction) Split(decimal saved)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(saved);
        var whole = decimal.Floor(saved);
        return ((int)whole, saved - whole);
    }
}
