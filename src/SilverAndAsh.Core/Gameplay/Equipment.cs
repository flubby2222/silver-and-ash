namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// One piece of equipment that can be found as loot (decision record 0008): either a weapon or an item. Two pieces
/// are equal when they are the same weapon or the same item - there is only one of each in a run.
/// </summary>
public sealed record Equipment
{
    private Equipment(WeaponKind? weapon, ItemKind? item)
    {
        Weapon = weapon;
        Item = item;
    }

    /// <summary>Every piece of equipment of the game, weapons first, each in the order of its list.</summary>
    public static IReadOnlyList<Equipment> All { get; } =
        Array.AsReadOnly(Enum.GetValues<WeaponKind>().Select(Of).Concat(Enum.GetValues<ItemKind>().Select(Of)).ToArray());

    /// <summary>The weapon, or null for an item.</summary>
    public WeaponKind? Weapon { get; }

    /// <summary>The item, or null for a weapon.</summary>
    public ItemKind? Item { get; }

    /// <summary>True for a weapon, false for an item.</summary>
    public bool IsWeapon => Weapon is not null;

    /// <summary>Its name as shown in the game.</summary>
    public string Name => Weapon is { } weapon ? Weapons.Info(weapon).Name : Items.Info(Item!.Value).Name;

    /// <summary>Its tier: what colour it shows, and from which chests it can come.</summary>
    public ItemTier Tier => Weapon is { } weapon ? Weapons.Info(weapon).Tier : Items.Info(Item!.Value).Tier;

    /// <summary>The weapon <paramref name="kind"/> as equipment.</summary>
    public static Equipment Of(WeaponKind kind)
    {
        _ = Weapons.Info(kind);
        return new(kind, null);
    }

    /// <summary>The item <paramref name="kind"/> as equipment.</summary>
    public static Equipment Of(ItemKind kind)
    {
        _ = Items.Info(kind);
        return new(null, kind);
    }

    /// <summary>Its name, e.g. "Rifle".</summary>
    public override string ToString() => Name;
}
