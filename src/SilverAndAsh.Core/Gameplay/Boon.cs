namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// The bonuses a player chooses from after a level up (decision record 0004). Each can be chosen again and again;
/// they last for the run and are gone after death. Values in <see cref="BoonBalance"/>.
/// </summary>
public enum Boon
{
    /// <summary>More maximum health - and the same amount healed at once.</summary>
    Vitality,

    /// <summary>More damage with every weapon (a damage rule; chosen twice, it adds up).</summary>
    Strength,

    /// <summary>One more stamina segment - one more jump, dash or a bigger part of a dodge.</summary>
    Endurance,
}

/// <summary>Damage rule: every chosen <see cref="Boon.Strength"/> adds <paramref name="PerStack"/> to the damage (0.15 = +15 %).</summary>
public sealed record StrengthBonus(float PerStack) : IDamageRule
{
    public bool IsCritical => false;

    public float Multiplier(Hit hit) => 1f + PerStack * hit.Strength;
}
