namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// The stamina bar that pays for jumps and dashes (one segment each) and dodges (more). A full bar holds exactly
/// <see cref="PlayerBalance.StaminaJumps"/> of them. It refills at a steady rate
/// (empty to full in <see cref="PlayerBalance.StaminaRechargeTime"/> seconds), but only while recharging is switched on.
/// <para>
/// The amount is counted in whole ticks of recharge time, not as a fraction like 0.2: whole numbers add up
/// exactly, while 180 small float steps of 1/180 might end at 0.99999 and never be "full".
/// </para>
/// </summary>
public sealed class Stamina
{
    private int _amount;

    /// <summary>A full bar sized by <paramref name="balance"/> (the standard values if none is given).</summary>
    public Stamina(PlayerBalance? balance = null)
    {
        balance ??= Balance.Default.Player;
        Capacity = SimulationClock.SecondsToTicks(balance.StaminaRechargeTime);
        CostPerSegment = Capacity / balance.StaminaJumps;
        _amount = Capacity;
    }

    /// <summary>Recharge ticks from empty to full - also the size of a full bar.</summary>
    public int Capacity { get; }

    /// <summary>What one segment is worth: the cost of one jump or one dash.</summary>
    public int CostPerSegment { get; }

    /// <summary>How many segments a full bar has (for drawing).</summary>
    public int Segments => Capacity / CostPerSegment;

    /// <summary>
    /// How fast the bar refills: 1 = normal, 1.05 = 5 % faster (the <see cref="Boon.StaminaRegen"/> bonus).
    /// The bar counts in whole units, so the part beyond them is carried over from tick to tick.
    /// </summary>
    public float RegenMultiplier { get; set; } = 1f;

    private float _regenCarry;

    /// <summary>What is in the bar now, in the units of <see cref="Capacity"/> (0 to <see cref="Capacity"/>).</summary>
    public int Amount => _amount;

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

    /// <summary>
    /// Pays a part of a segment, in the bar's own unit (<see cref="CostPerSegment"/> per segment) - for costs that grow
    /// bit by bit, like charging the blade. Returns false (and takes nothing) if there is not enough.
    /// </summary>
    public bool TryPayPart(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (_amount < amount)
        {
            return false;
        }

        _amount -= amount;
        return true;
    }

    /// <summary>Switches refilling on (standing on the ground) or off (jumping).</summary>
    public void SetRecharging(bool recharging) => IsRecharging = recharging;

    /// <summary>Advances by one simulation tick.</summary>
    public void Tick()
    {
        if (IsRecharging && _amount < Capacity)
        {
            // Normally one unit per tick; faster regeneration adds the extra bit up until it is a whole unit.
            _regenCarry += RegenMultiplier;
            var units = (int)_regenCarry;
            _regenCarry -= units;
            _amount = Math.Min(Capacity, _amount + units);
        }
    }
}
