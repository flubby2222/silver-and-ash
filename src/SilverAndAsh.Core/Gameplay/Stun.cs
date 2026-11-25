namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Stun (issue #509): a stunned ghoul or bat stands still and attacks no more - a blow it is drawing back for breaks
/// off. Gorm only staggers for a moment: he stops walking, but an attack he announces goes on; after that he cannot be
/// stunned for a while. The rule alone, without a world - the one place every enemy asks whether it may act.
/// </summary>
public static class Stun
{
    /// <summary>Ticks a fresh stun lasts on a ghoul or a bat (at least one).</summary>
    public static int DurationTicks(StunBalance values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Math.Max(1, SimulationClock.SecondsToTicks(values.Seconds));
    }

    /// <summary>Ticks Gorm staggers from a stun (at least one).</summary>
    public static int GormStaggerTicks(StunBalance values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Math.Max(1, SimulationClock.SecondsToTicks(values.GormStaggerSeconds));
    }

    /// <summary>Ticks after his stagger in which a new stun does nothing to Gorm.</summary>
    public static int GormImmuneTicks(StunBalance values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return SimulationClock.SecondsToTicks(values.GormImmuneSeconds);
    }

    /// <summary>True while a stun holds the enemy carrying <paramref name="status"/>.</summary>
    public static bool Holds(StatusEffects status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return status.Has(StatusKind.Stun);
    }

    /// <summary>
    /// A stun lands on Gorm (issue #509): he staggers for <see cref="GormStaggerTicks"/> and is immune from now until
    /// <see cref="GormImmuneTicks"/> after the stagger - also during it, so a second stun never stretches the stagger.
    /// Returns the ticks to stagger, or 0 while he is immune: then nothing happens.
    /// </summary>
    public static int LandOnGorm(StatusEffects status, StunBalance values)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(values);
        if (status.IsImmuneTo(StatusKind.Stun))
        {
            return 0;
        }

        var stagger = GormStaggerTicks(values);
        status.MakeImmune(StatusKind.Stun, stagger + GormImmuneTicks(values));
        return stagger;
    }
}
