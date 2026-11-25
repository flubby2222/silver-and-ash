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
}
