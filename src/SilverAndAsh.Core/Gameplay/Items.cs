namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Every item of the game (decision record 0008): passive, for the whole run, no place needed. The rules are in
/// <see cref="ItemEffects"/> (issues #195 and #196).
/// <para>
/// <see cref="Leech"/> is the item; the weapon effect of the same name is <see cref="StatusKind.Leech"/> (issue #510).
/// Both heal the same way (<see cref="Player.HealByLeech"/>), each with its own share
/// (<see cref="ItemsBalance.Leech"/> and <see cref="StatusEffectsBalance.Leech"/>).
/// </para>
/// </summary>
public enum ItemKind
{
    HuntersBoots,
    IronFlask,
    SilverRosary,
    QuickdrawHolster,
    BladeParry,
    HuntersEye,
    Leech,
    AshenHeart,
}

/// <summary>What an item is: its name and the line telling its effect (both shown in the game), and its tier.</summary>
public readonly record struct ItemInfo(string Name, ItemTier Tier, string Effect);

/// <summary>
/// The fixed facts about each item. Its numbers (how much faster, how much healing ...) are in balance.json
/// (<see cref="ItemsBalance"/>); the effect lines name the standard values.
/// </summary>
public static class Items
{
    /// <summary>The facts about <paramref name="kind"/>.</summary>
    public static ItemInfo Info(ItemKind kind) => kind switch
    {
        ItemKind.HuntersBoots => new("Hunter's Boots", ItemTier.Worn, "You run 10% faster."),
        ItemKind.IronFlask => new("Iron Flask", ItemTier.Worn, "Blood vials heal 50% more."),
        ItemKind.SilverRosary => new("Silver Rosary", ItemTier.Forged, "Every kill heals 3."),
        ItemKind.QuickdrawHolster => new("Quickdraw Holster", ItemTier.Forged, "Ranged weapons reload 30% faster."),
        ItemKind.BladeParry => new("Blade Parry", ItemTier.Royal, "Melee strikes shatter blood bolts."),
        ItemKind.HuntersEye => new("Hunter's Eye", ItemTier.Royal, "Headshots deal 50% more damage."),
        ItemKind.Leech => new("Leech", ItemTier.Vampiric, "5% of the damage you deal heals you."),
        ItemKind.AshenHeart => new("Ashen Heart", ItemTier.Vampiric, "Once: instead of dying, go on with 30 health. Then it breaks."),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown item."),
    };
}
