namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// The waiting time after an attack before the same weapon can be used again ("cooldown").
/// Counted in whole ticks, like the stamina - whole numbers never end at 0.00001 instead of 0.
/// </summary>
public sealed class Cooldown
{
    /// <summary>Creates a cooldown that lasts <paramref name="seconds"/> after each use.</summary>
    public Cooldown(float seconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seconds);
        DurationTicks = SimulationClock.SecondsToTicks(seconds);
    }

    /// <summary>How long the cooldown lasts, in ticks.</summary>
    public int DurationTicks { get; }

    private int _ticksLeft;

    /// <summary>True when the weapon can be used.</summary>
    public bool IsReady => _ticksLeft == 0;

    /// <summary>How much of the waiting time is left: 1 = just used, 0 = ready. For the HUD.</summary>
    public float RemainingFraction => (float)_ticksLeft / DurationTicks;

    /// <summary>Seconds left until the weapon is ready again (0 = ready).</summary>
    public float RemainingSeconds => _ticksLeft * SimulationClock.SecondsPerTick;

    /// <summary>Starts the cooldown if the weapon is ready. Returns false (and changes nothing) otherwise.</summary>
    public bool TryUse()
    {
        if (!IsReady)
        {
            return false;
        }

        _ticksLeft = DurationTicks;
        return true;
    }

    /// <summary>Advances by one simulation tick.</summary>
    public void Tick()
    {
        if (_ticksLeft > 0)
        {
            _ticksLeft--;
        }
    }
}
