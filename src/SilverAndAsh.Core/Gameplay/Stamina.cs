namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// The stamina bar that pays for perfect jumps. A full bar holds exactly
/// <see cref="Tuning.Player.StaminaPerfectJumps"/> of them. It refills at a steady rate
/// (empty to full in <see cref="Tuning.Player.StaminaRechargeTime"/> seconds), but only while recharging is switched on.
/// <para>
/// The amount is counted in whole ticks of recharge time, not as a fraction like 0.25: whole numbers add up
/// exactly, while 180 small float steps of 1/180 might end at 0.99999 and never be "full".
/// </para>
/// </summary>
public sealed class Stamina
{
    /// <summary>Recharge ticks from empty to full - also the size of a full bar.</summary>
    public static readonly int Capacity = SimulationClock.SecondsToTicks(Tuning.Player.StaminaRechargeTime);

    /// <summary>What one perfect jump costs.</summary>
    public static readonly int CostPerJump = Capacity / Tuning.Player.StaminaPerfectJumps;

    private int _amount = Capacity;

    /// <summary>How full the bar is: 0 = empty, 1 = full.</summary>
    public float Fraction => (float)_amount / Capacity;

    /// <summary>True while the bar refills every tick.</summary>
    public bool IsRecharging { get; private set; }

    /// <summary>True if there is enough for one more perfect jump.</summary>
    public bool CanPayForJump => _amount >= CostPerJump;

    /// <summary>Pays for one perfect jump, if there is enough. Returns false (and takes nothing) otherwise.</summary>
    public bool TryPayForJump()
    {
        if (!CanPayForJump)
        {
            return false;
        }

        _amount -= CostPerJump;
        return true;
    }

    /// <summary>Switches refilling on (after a normal landing) or off (while chaining timed jumps).</summary>
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
