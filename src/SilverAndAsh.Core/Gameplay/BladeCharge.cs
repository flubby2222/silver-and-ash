namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Charging the silver blade: hold the button, release to swing harder. Linear from 0 (released at once) to 1
/// (held for <see cref="Tuning.Combat.BladeChargeTime"/>). Charging costs up to
/// <see cref="Tuning.Combat.BladeChargeStaminaSegments"/> stamina segments, linearly as well - and stops when the
/// stamina cannot pay for the next bit.
/// <para>
/// Its own small class (like <see cref="Cooldown"/>): the player only starts, ticks and releases it, and what the
/// charge is worth is decided by a damage rule (<see cref="ChargeBonus"/>). Items can later change either side.
/// </para>
/// </summary>
public sealed class BladeCharge
{
    private static readonly int FullTicks = SimulationClock.SecondsToTicks(Tuning.Combat.BladeChargeTime);
    private static readonly int FullCost = Stamina.CostPerSegment * Tuning.Combat.BladeChargeStaminaSegments;

    private int _ticks;
    private int _paid;

    /// <summary>True while the button is held and the blade is being charged.</summary>
    public bool IsCharging { get; private set; }

    /// <summary>How far the blade is charged: 0 = not at all, 1 = fully.</summary>
    public float Fraction => (float)_ticks / FullTicks;

    /// <summary>Starts charging (the button was just pressed).</summary>
    public void Start()
    {
        IsCharging = true;
        _ticks = 0;
        _paid = 0;
    }

    /// <summary>
    /// One more tick of holding: charges a bit further if not yet full and the stamina can pay for that bit.
    /// The cost is spread evenly: after half the time, half the cost is paid.
    /// </summary>
    public void Tick(Stamina stamina)
    {
        ArgumentNullException.ThrowIfNull(stamina);
        if (!IsCharging || _ticks >= FullTicks)
        {
            return;
        }

        // Whole numbers: what should be paid after one more tick, minus what was paid already. Rounded UP, so every
        // tick costs something - rounded down, the first tick was free and an empty bar could still charge a bit.
        var owed = ((_ticks + 1) * FullCost + FullTicks - 1) / FullTicks;
        if (stamina.TryPayPart(owed - _paid))
        {
            _ticks++;
            _paid = owed;
        }
    }

    /// <summary>Stops charging (the button was let go) and returns how far it was charged.</summary>
    public float Release()
    {
        var charge = Fraction;
        IsCharging = false;
        _ticks = 0;
        _paid = 0;
        return charge;
    }
}

/// <summary>Damage rule: a charged blade hits harder, linearly up to <see cref="Tuning.Combat.BladeChargeMaxMultiplier"/>.</summary>
public sealed record ChargeBonus(float MaxMultiplier) : IDamageRule
{
    public bool IsCritical => false;

    public float Multiplier(Hit hit) => hit.Weapon == Weapon.Blade ? 1f + hit.Charge * (MaxMultiplier - 1f) : 1f;
}
