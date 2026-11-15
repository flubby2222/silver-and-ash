namespace SilverAndAsh.Core.World;

/// <summary>
/// The four tiers of chests (decision record 0006) - from the plain wooden chest up to the red one. Items and weapons
/// will use the same tiers from v0.11 (Worn, Forged, Royal, Vampiric). Higher tiers are locked more often and hold more.
/// </summary>
public enum ChestTier
{
    /// <summary>Beggar's Chest: wood. Item tier "Worn".</summary>
    Beggar,

    /// <summary>Guard's Chest: metal. Item tier "Forged".</summary>
    Guard,

    /// <summary>King's Chest: gold. Item tier "Royal".</summary>
    King,

    /// <summary>Vampire's Chest: red, always locked. Item tier "Vampiric".</summary>
    Vampire,
}

/// <summary>Names of the chest tiers as shown in the game.</summary>
public static class ChestTierNames
{
    /// <summary>"Beggar's Chest", "Guard's Chest", "King's Chest" or "Vampire's Chest".</summary>
    public static string ChestName(this ChestTier tier) => tier switch
    {
        ChestTier.Beggar => "Beggar's Chest",
        ChestTier.Guard => "Guard's Chest",
        ChestTier.King => "King's Chest",
        ChestTier.Vampire => "Vampire's Chest",
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown chest tier."),
    };
}
