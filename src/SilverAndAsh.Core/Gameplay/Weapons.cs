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
        (Cooldown, Magazine) = kind switch
        {
            _ => (new Cooldown(balance.Pistol.Cooldown), new Magazine(balance.Pistol.MagazineSize, balance.Pistol.ReloadTime)),
        };
    }

    public WeaponKind Kind { get; }

    /// <summary>Its name, hand and tier.</summary>
    public WeaponInfo Info => Weapons.Info(Kind);

    /// <summary>Time between two shots.</summary>
    public Cooldown Cooldown { get; }

    /// <summary>Rounds left and the reload.</summary>
    public Magazine Magazine { get; }
}
