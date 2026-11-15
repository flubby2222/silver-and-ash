namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// The pool of bonuses a level up offers (decision record 0004): small steps, each counted from the base value and
/// chosen again and again. They last for the run and are gone after death. Values in <see cref="BoonBalance"/>.
/// A new boon only needs a name here and its effect in <see cref="Player.Choose"/> - it joins the pool by itself.
/// </summary>
public enum Boon
{
    /// <summary>The stamina bar refills a bit faster.</summary>
    StaminaRegen,

    /// <summary>A bit more maximum health - and the same amount healed at once.</summary>
    Health,

    /// <summary>A bit more damage with every weapon (a damage rule).</summary>
    Damage,

    /// <summary>A bit more experience for every kill.</summary>
    Experience,
}

/// <summary>
/// Damage rule: every chosen <see cref="Boon.Damage"/> adds <paramref name="PerBoon"/> of the base damage
/// (0.05 = +5 %; three times = +15 %, not 1.05³).
/// </summary>
public sealed record DamageBoonBonus(float PerBoon) : IDamageRule
{
    public bool IsCritical => false;

    public float Multiplier(Hit hit) => 1f + PerBoon * hit.DamageBoons;
}
