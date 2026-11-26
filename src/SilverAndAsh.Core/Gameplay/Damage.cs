namespace SilverAndAsh.Core.Gameplay;

/// <summary>The player's weapons, as the damage rules tell them apart.</summary>
public enum Weapon
{
    Blade,
    Pistol,
    Club,
    Rifle,
    Blunderbuss,

    /// <summary>
    /// Not a weapon in hand: the pulses of a Cleansing Flame (issue #507). Its damage counts for the player who set the
    /// flame off; no zone bonus and no charge apply to it.
    /// </summary>
    CleansingFlame,

    /// <summary>The hunter's axe (issue #201).</summary>
    HuntersAxe,

    /// <summary>The whip (issue #202).</summary>
    Whip,

    /// <summary>The stake crossbow (issue #203).</summary>
    StakeCrossbow,
}

/// <summary>How damage reaches a target (issue #771): a blow or shot, or the pulse of a flame.</summary>
public enum DamageKind
{
    /// <summary>A hit of a weapon: the target reacts to it (hurt sound, flash, flinch).</summary>
    Strike,

    /// <summary>Burning (Cleansing Flame): damage and kill credit as for a strike, but no reaction to each pulse.</summary>
    Burn,
}

/// <summary>Which <see cref="DamageKind"/> the damage of a weapon is - one table entry per weapon that is not a strike.</summary>
public static class DamageKinds
{
    private static readonly HashSet<Weapon> Burning = [Weapon.CleansingFlame];

    /// <summary>The kind of damage <paramref name="weapon"/> does.</summary>
    public static DamageKind Of(Weapon weapon) => Burning.Contains(weapon) ? DamageKind.Burn : DamageKind.Strike;
}

/// <summary>Where a body was hit.</summary>
public enum HitZone
{
    Body,
    Head,

    /// <summary>A boss's head - its own weak spot with its own multiplier (Gorm: x1.5, not the ghouls' x3).</summary>
    BossHead,
}

/// <summary>
/// Everything about one hit that decides its damage: which weapon, which zone, its base damage, - for a charged
/// blade - how far it was charged (0 = not at all, 1 = fully), and how often the attacker chose
/// <see cref="Boon.Damage"/>. <paramref name="TargetStatus"/>: the status effects the target carries (issue #504; none
/// for a target that cannot carry any, like a training dummy). <paramref name="AttackerItems"/>: the items the attacker
/// carries (issue #196: Hunter's Eye; none if not given).
/// </summary>
public sealed record Hit(
    Weapon Weapon, HitZone Zone, int BaseDamage, float Charge = 0f, int DamageBoons = 0, StatusEffects? TargetStatus = null,
    IReadOnlyList<ItemKind>? AttackerItems = null);

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
/// Marked (issue #504): a marked target takes <paramref name="Bonus"/> more damage from every hit (0.125 = 12.5 %),
/// whatever the weapon and the zone. Not critical by itself.
/// </summary>
public sealed record MarkedBonus(float Bonus) : IDamageRule
{
    public bool IsCritical => false;

    public float Multiplier(Hit hit) => hit.TargetStatus?.Has(StatusKind.Marked) == true ? 1f + Bonus : 1f;
}

/// <summary>
/// Hunter's Eye (issue #196): a headshot - a boss's head too - of an attacker who carries the item does more damage, on
/// top of the weapon's own headshot bonus (<see cref="ItemEffects.HeadshotFactor"/>). Critical like every headshot.
/// </summary>
public sealed record HuntersEyeBonus(ItemsBalance Values) : IDamageRule
{
    public bool IsCritical => true;

    public float Multiplier(Hit hit) => ItemEffects.HeadshotFactor(hit.Zone, hit.AttackerItems ?? [], Values);
}

/// <summary>
/// The list of damage rules that apply in a game. Damage = base damage x all multipliers, rounded.
/// Part of the game state (not static), so every game - and later every player - can have its own rules.
/// </summary>
public sealed class DamageRules
{
    private readonly List<IDamageRule> _rules = [];

    /// <summary>
    /// The rules of a normal game: headshots with the pistol do triple damage, a charged blade up to double, every
    /// chosen Damage boon a bit more, a marked target a bit more - with the values from <paramref name="balance"/> (the standard values if none
    /// is given).
    /// </summary>
    public static DamageRules Standard(Balance? balance = null)
    {
        balance ??= Balance.Default;
        var rules = new DamageRules();
        rules.Add(new ZoneBonus(Weapon.Pistol, HitZone.Head, balance.Pistol.HeadshotMultiplier));
        rules.Add(new ZoneBonus(Weapon.Pistol, HitZone.BossHead, balance.Gorm.HeadMultiplier));
        rules.Add(new ZoneBonus(Weapon.Rifle, HitZone.Head, balance.Rifle.HeadshotMultiplier));
        rules.Add(new ZoneBonus(Weapon.Rifle, HitZone.BossHead, balance.Gorm.HeadMultiplier));
        rules.Add(new ZoneBonus(Weapon.Blunderbuss, HitZone.Head, balance.Blunderbuss.HeadshotMultiplier));
        rules.Add(new ZoneBonus(Weapon.Blunderbuss, HitZone.BossHead, balance.Gorm.HeadMultiplier));
        rules.Add(new ZoneBonus(Weapon.StakeCrossbow, HitZone.Head, balance.StakeCrossbow.HeadshotMultiplier));
        rules.Add(new ZoneBonus(Weapon.StakeCrossbow, HitZone.BossHead, balance.Gorm.HeadMultiplier));
        rules.Add(new ChargeBonus(balance.Blade.ChargeMaxMultiplier));
        rules.Add(new ChargeBonus(balance.Club.ChargeMaxMultiplier, Weapon.Club));
        rules.Add(new ChargeBonus(balance.HuntersAxe.ChargeMaxMultiplier, Weapon.HuntersAxe));
        rules.Add(new ChargeBonus(balance.Whip.ChargeMaxMultiplier, Weapon.Whip));
        rules.Add(new DamageBoonBonus(balance.Boons.Damage));
        rules.Add(new MarkedBonus(balance.StatusEffects.Marked.Bonus));
        rules.Add(new HuntersEyeBonus(balance.Items));
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
