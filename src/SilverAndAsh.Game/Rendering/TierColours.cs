using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The colour of each tier (decision records 0006 and 0008): the same as the chests - Worn = wood brown, Forged = steel
/// blue, Royal = gold, Vampiric = blood red (pulsing). Wherever equipment shows, its frame or name has this colour.
/// </summary>
internal static class TierColours
{
    /// <summary>The colour of <paramref name="tier"/>; <paramref name="seconds"/> makes the Vampiric one pulse.</summary>
    public static Color Of(ItemTier tier, float seconds) => ChestView.Colours(ChestOf(tier), seconds).Body;

    private static ChestTier ChestOf(ItemTier tier) => tier switch
    {
        ItemTier.Worn => ChestTier.Beggar,
        ItemTier.Forged => ChestTier.Guard,
        ItemTier.Royal => ChestTier.King,
        _ => ChestTier.Vampire,
    };
}
