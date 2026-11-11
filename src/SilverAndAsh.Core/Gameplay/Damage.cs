namespace SilverAndAsh.Core.Gameplay;

/// <summary>The player's weapons.</summary>
public enum Weapon
{
    Blade,
    Pistol,
}

/// <summary>Where a body was hit.</summary>
public enum HitZone
{
    Body,
    Head,
}

/// <summary>
/// Everything about one hit that decides its damage: which weapon, which zone, its base damage and - for a charged
/// blade - how far it was charged (0 = not at all, 1 = fully).
/// </summary>
public sealed record Hit(Weapon Weapon, HitZone Zone, int BaseDamage, float Charge = 0f);

/// <summary>The damage a hit really does, and whether it counts as a critical hit (shown differently).</summary>
public readonly record struct DamageResult(int Amount, bool IsCritical);

/// <summary>
/// One rule that changes the damage of a hit, as a multiplier (1 = no change, 3 = triple damage).
/// Weapons know nothing about these rules - new effects (items, rage, ...) only add a rule.
/// </summary>
public interface IDamageRule
{
    /// <summary>True if this rule makes a hit critical when it raises the damage (e.g. a headshot).</summary>
    bool IsCritical { get; }

    /// <summary>The multiplier for <paramref name="hit"/>; 1 if the rule does not apply.</summary>
    float Multiplier(Hit hit);
}

/// <summary>A bonus for hitting a certain zone with a certain weapon, e.g. pistol + head = 3x.</summary>
public sealed record ZoneBonus(Weapon Weapon, HitZone Zone, float Factor) : IDamageRule
{
    public bool IsCritical => true;

    public float Multiplier(Hit hit) => hit.Weapon == Weapon && hit.Zone == Zone ? Factor : 1f;
}

/// <summary>
/// The list of damage rules that apply in a game. Damage = base damage x all multipliers, rounded.
/// Part of the game state (not static), so every game - and later every player - can have its own rules.
/// </summary>
public sealed class DamageRules
{
    private readonly List<IDamageRule> _rules = [];

    /// <summary>The rules of a normal game: headshots with the pistol do triple damage.</summary>
    public static DamageRules Standard()
    {
        var rules = new DamageRules();
        rules.Add(new ZoneBonus(Weapon.Pistol, HitZone.Head, Tuning.Combat.PistolHeadshotMultiplier));
        return rules;
    }

    /// <summary>Adds a rule (e.g. when an item is picked up).</summary>
    public void Add(IDamageRule rule) => _rules.Add(rule);

    /// <summary>Removes a rule again (e.g. when an item's effect ends). Returns false if it was not there.</summary>
    public bool Remove(IDamageRule rule) => _rules.Remove(rule);

    /// <summary>The damage <paramref name="hit"/> really does.</summary>
    public DamageResult Calculate(Hit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);
        var multiplier = 1f;
        var critical = false;
        foreach (var rule in _rules)
        {
            var factor = rule.Multiplier(hit);
            multiplier *= factor;
            critical |= rule.IsCritical && factor > 1f;
        }

        // Half points round up (10.5 -> 11). .NET's default would round them to the even number (10).
        var amount = (int)MathF.Round(hit.BaseDamage * multiplier, MidpointRounding.AwayFromZero);
        return new DamageResult(amount, critical);
    }
}
