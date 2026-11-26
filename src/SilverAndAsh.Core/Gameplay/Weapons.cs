namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// The tier (rarity) of an item or weapon (decision records 0006 and 0008): the same four steps as the chests, so
/// loot can be read at a glance by its colour.
/// </summary>
public enum ItemTier
{
    /// <summary>Wood brown - Beggar's chest.</summary>
    Worn,

    /// <summary>Steel blue - Guard's chest.</summary>
    Forged,

    /// <summary>Gold - King's chest.</summary>
    Royal,

    /// <summary>Blood red - Vampire's chest.</summary>
    Vampiric,
}

/// <summary>Which hand a weapon is for: the left mouse button (one melee weapon) or the right one (up to three ranged).</summary>
public enum WeaponHand
{
    Melee,
    Ranged,
}

/// <summary>Every weapon of the game (decision record 0008). More come with v0.11 and v0.12.</summary>
public enum WeaponKind
{
    SilverBlade,
    Pistol,
    Club,
    Rifle,
    Blunderbuss,

    /// <summary>
    /// The hunter's axe (Royal, issue #201): slow, a wide swing that hits several enemies, heavy damage. Shown as
    /// "Hunting Axe" - "Hunter's Axe" is 2 pixels too wide for the equipment box and the pickup toast (provisional).
    /// </summary>
    HuntersAxe,

    /// <summary>
    /// The whip (Royal, issue #202): long reach, medium damage, and it pulls ghouls and bats in towards the one who
    /// swings it (Gorm stands firm).
    /// </summary>
    Whip,

    /// <summary>
    /// The stake crossbow (Vampiric, issue #203): one heavy stake per load that runs through every enemy in a row until
    /// a wall or a column stops it, and a slow reload. Shown as "Crossbow" - "Stake Crossbow" is too wide for the name in
    /// the equipment box and 3 pixels too wide for the pickup toast with an effect (provisional, as for the axe).
    /// </summary>
    StakeCrossbow,
}

/// <summary>What a weapon is: its name (shown in the game), its hand and its tier.</summary>
public readonly record struct WeaponInfo(string Name, WeaponHand Hand, ItemTier Tier);

/// <summary>The fixed facts about each weapon. Its numbers (damage, speed ...) are in balance.json.</summary>
public static class Weapons
{
    /// <summary>The facts about <paramref name="kind"/>.</summary>
    public static WeaponInfo Info(WeaponKind kind) => kind switch
    {
        WeaponKind.SilverBlade => new("Silver Blade", WeaponHand.Melee, ItemTier.Worn),
        WeaponKind.Pistol => new("Pistol", WeaponHand.Ranged, ItemTier.Worn),
        WeaponKind.Club => new("Club", WeaponHand.Melee, ItemTier.Worn),
        WeaponKind.Rifle => new("Rifle", WeaponHand.Ranged, ItemTier.Forged),
        WeaponKind.Blunderbuss => new("Blunderbuss", WeaponHand.Ranged, ItemTier.Forged),
        WeaponKind.HuntersAxe => new("Hunting Axe", WeaponHand.Melee, ItemTier.Royal),
        WeaponKind.Whip => new("Whip", WeaponHand.Melee, ItemTier.Royal),
        WeaponKind.StakeCrossbow => new("Crossbow", WeaponHand.Ranged, ItemTier.Vampiric),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown weapon."),
    };

    /// <summary>How the damage rules tell <paramref name="kind"/> apart (one table for every hit and the character sheet).</summary>
    public static Weapon ForDamage(WeaponKind kind) => kind switch
    {
        WeaponKind.SilverBlade => Weapon.Blade,
        WeaponKind.Club => Weapon.Club,
        WeaponKind.Pistol => Weapon.Pistol,
        WeaponKind.Rifle => Weapon.Rifle,
        WeaponKind.Blunderbuss => Weapon.Blunderbuss,
        WeaponKind.HuntersAxe => Weapon.HuntersAxe,
        WeaponKind.Whip => Weapon.Whip,
        WeaponKind.StakeCrossbow => Weapon.StakeCrossbow,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown weapon."),
    };
}

/// <summary>
/// A ranged weapon a player carries: its kind, and its own cooldown and magazine - each weapon keeps its rounds when
/// it is put away.
/// </summary>
public sealed class RangedWeapon
{
    /// <summary>A ready, full <paramref name="kind"/> with the values of <paramref name="balance"/>.</summary>
    public RangedWeapon(WeaponKind kind, Balance balance)
    {
        ArgumentNullException.ThrowIfNull(balance);
        if (Weapons.Info(kind).Hand != WeaponHand.Ranged)
        {
            throw new ArgumentException($"{kind} is no ranged weapon.", nameof(kind));
        }

        Kind = kind;
        var values = balance.Ranged(kind);
        Cooldown = new Cooldown(values.Cooldown);
        Magazine = new Magazine(values.MagazineSize, values.ReloadTime);
    }

    public WeaponKind Kind { get; }

    /// <summary>Its name, hand and tier.</summary>
    public WeaponInfo Info => Weapons.Info(Kind);

    /// <summary>Time between two shots.</summary>
    public Cooldown Cooldown { get; }

    /// <summary>Rounds left and the reload.</summary>
    public Magazine Magazine { get; }
}
