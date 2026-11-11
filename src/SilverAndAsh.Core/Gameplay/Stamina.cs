namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// The stamina bar that pays for jumps and dashes (one segment each) and dodges (more). A full bar holds exactly
/// <see cref="Tuning.Player.StaminaJumps"/> of them. It refills at a steady rate
/// (empty to full in <see cref="Tuning.Player.StaminaRechargeTime"/> seconds), but only while recharging is switched on.
/// <para>
/// The amount is counted in whole ticks of recharge time, not as a fraction like 0.2: whole numbers add up
/// exactly, while 180 small float steps of 1/180 might end at 0.99999 and never be "full".
/// </para>
/// </summary>
public sealed class Stamina
{
    /// <summary>Recharge ticks from empty to full - also the size of a full bar.</summary>
    public static readonly int Capacity = SimulationClock.SecondsToTicks(Tuning.Player.StaminaRechargeTime);

    /// <summary>What one segment is worth: the cost of one jump or one dash.</summary>
    public static readonly int CostPerSegment = Capacity / Tuning.Player.StaminaJumps;

    private int _amount = Capacity;

    /// <summary>How full the bar is: 0 = empty, 1 = full.</summary>
    public float Fraction => (float)_amount / Capacity;

    /// <summary>True while the bar refills every tick.</summary>
    public bool IsRecharging { get; private set; }

    /// <summary>True if there is enough for one more jump or dash.</summary>
    public bool CanPay => CanPayFor(1);

    /// <summary>True if there is enough for <paramref name="segments"/> segments (e.g. 2 for a dodge).</summary>
    public bool CanPayFor(int segments) => _amount >= segments * CostPerSegment;

    /// <summary>
    /// Pays <paramref name="segments"/> segments (1 for a jump or a dash, 2 for a dodge), if there is enough.
    /// Returns false (and takes nothing) otherwise.
    /// </summary>
    public bool TryPay(int segments = 1)
    {
        if (!CanPayFor(segments))
        {
            return false;
        }

        _amount -= segments * CostPerSegment;
        return true;
    }

    /// <summary>Switches refilling on (standing on the ground) or off (jumping).</summary>
    public void SetRecharging(bool recharging) => IsRecharging = recharging;

    /// <summary>Advances by one simulation tick.</summary>
    public void Tick()
    {
        if (IsRecharging && _amount < Capacity)
        {
            _amount++;
        }
    }
}
