namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Cleansing Flame (issue #507): damage over time, dealt in pulses. The rule alone, without a world: which tick of a
/// flame deals how much damage. The world burns every enemy that carries it at the end of each tick.
/// </summary>
public static class CleansingFlame
{
    /// <summary>Ticks between two pulses (at least one).</summary>
    public static int PulseTicks(CleansingFlameBalance values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Math.Max(1, SimulationClock.SecondsToTicks(values.TickSeconds));
    }

    /// <summary>Ticks a flame burns from being set alight (at least one).</summary>
    public static int DurationTicks(CleansingFlameBalance values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Math.Max(1, SimulationClock.SecondsToTicks(values.Seconds));
    }

    /// <summary>
    /// True if the flame ends on a pulse: its ticks are a whole number of pulses. Otherwise the ticks after the last
    /// pulse would burn for nothing and the flame would deal less than its damage per second times its seconds -
    /// the balancing file refuses such values (issue #507). Counted in ticks, as the game runs, not in seconds.
    /// </summary>
    public static bool EndsOnAPulse(CleansingFlameBalance values) => DurationTicks(values) % PulseTicks(values) == 0;

    /// <summary>
    /// The damage on tick <paramref name="tickOfFlame"/> of a flame (1 = the tick it was put on, as its first tick
    /// counted): 0 between the pulses, on every pulse its share. Pulses that do not come out even take turns: the
    /// damage of the first n pulses is always n pulses' worth rounded down (2.5 per pulse: 2, 3, 2, 3), so nothing is
    /// lost and nothing is added over the whole flame.
    /// </summary>
    public static int DamageOnTick(int tickOfFlame, CleansingFlameBalance values)
    {
        var pulse = PulseTicks(values);
        if (tickOfFlame <= 0 || tickOfFlame % pulse != 0)
        {
            return 0;
        }

        var pulses = tickOfFlame / pulse;
        return DamageOfPulses(pulses, pulse, values) - DamageOfPulses(pulses - 1, pulse, values);
    }

    /// <summary>The damage of the first <paramref name="pulses"/> pulses together, rounded down.</summary>
    private static int DamageOfPulses(int pulses, int pulseTicks, CleansingFlameBalance values) =>
        (int)Math.Floor((pulses * (double)values.DamagePerSecond * pulseTicks / SimulationClock.TicksPerSecond) + 1e-6);
}
