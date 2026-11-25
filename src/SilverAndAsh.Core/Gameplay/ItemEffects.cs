namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// What the passive items do (decision record 0008, issue #195): one rule per item, each working only for the player who
/// carries the item. Pure functions of the items carried and the <see cref="ItemsBalance"/> - the player and the world
/// call them where the effect takes hold (top speed, a blood vial, a kill, the start of a reload). Without the item each
/// rule gives back exactly what it was given.
/// </summary>
public static class ItemEffects
{
    /// <summary>Hunter's Boots: the factor on the top speed - 1 without them, 1.1 with them (standard values).</summary>
    public static float SpeedFactor(IReadOnlyCollection<ItemKind> items, ItemsBalance values)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(values);
        return items.Contains(ItemKind.HuntersBoots) ? 1f + values.HuntersBootsSpeed : 1f;
    }

    /// <summary>
    /// Iron Flask: what a blood vial that heals <paramref name="heal"/> heals with it - 50 % more (standard), rounded to
    /// whole health, half up. Without the flask just <paramref name="heal"/>.
    /// </summary>
    public static int VialHeal(int heal, IReadOnlyCollection<ItemKind> items, ItemsBalance values)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(heal);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(values);
        return items.Contains(ItemKind.IronFlask)
            ? (int)Math.Round(heal * (1.0 + values.IronFlaskHeal), MidpointRounding.AwayFromZero)
            : heal;
    }

    /// <summary>Silver Rosary: the health a kill gives back - 3 (standard) with it, 0 without.</summary>
    public static int KillHeal(IReadOnlyCollection<ItemKind> items, ItemsBalance values)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(values);
        return items.Contains(ItemKind.SilverRosary) ? values.SilverRosaryHeal : 0;
    }

    /// <summary>
    /// Quickdraw Holster: how many ticks a reload of <paramref name="ticks"/> takes with it - 30 % less (standard),
    /// rounded to whole ticks, at least one. Without the holster just <paramref name="ticks"/>.
    /// </summary>
    public static int ReloadTicks(int ticks, IReadOnlyCollection<ItemKind> items, ItemsBalance values)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticks);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(values);
        return items.Contains(ItemKind.QuickdrawHolster)
            ? Math.Max(1, (int)Math.Round(ticks * (1.0 - values.QuickdrawHolsterReload), MidpointRounding.AwayFromZero))
            : ticks;
    }

    /// <summary>Blade Parry (issue #196): true if a melee swing of this player shatters the blood bolts in its cone.</summary>
    public static bool ShattersBolts(IReadOnlyCollection<ItemKind> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items.Contains(ItemKind.BladeParry);
    }

    /// <summary>
    /// Hunter's Eye (issue #196): the factor on a hit in <paramref name="zone"/> - 1.5 (standard) on a head, a boss's head
    /// too; 1 on the body and without the eye. It comes on top of the weapon's own headshot bonus.
    /// </summary>
    public static float HeadshotFactor(HitZone zone, IReadOnlyCollection<ItemKind> items, ItemsBalance values)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(values);
        return zone is HitZone.Head or HitZone.BossHead && items.Contains(ItemKind.HuntersEye) ? 1f + values.HuntersEyeHeadshot : 1f;
    }

    /// <summary>
    /// Leech, the item (issue #196): the share of the damage dealt that heals, or null without the item. The healing itself
    /// is the one of the weapon effect Leech (<see cref="Player.HealByLeech"/>), only with this share.
    /// </summary>
    public static LeechBalance? LeechItem(IReadOnlyCollection<ItemKind> items, ItemsBalance values)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(values);
        return items.Contains(ItemKind.Leech) ? values.Leech : null;
    }

    /// <summary>
    /// Ashen Heart (issue #196): the health a player with <paramref name="maxHealth"/> goes on with instead of dying -
    /// 30 (standard), never above the maximum - or null without the heart (then the player dies).
    /// </summary>
    public static int? AshenHeartHealth(IReadOnlyCollection<ItemKind> items, int maxHealth, ItemsBalance values)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHealth);
        return items.Contains(ItemKind.AshenHeart) ? Math.Min(values.AshenHeartHealth, maxHealth) : null;
    }
}
