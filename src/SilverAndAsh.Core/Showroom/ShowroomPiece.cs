using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Showroom;

/// <summary>
/// One exhibit of the showroom (issue #283, docs/musterraum.md). <paramref name="Label"/> is the small text shown above
/// it in the picture. Wall pieces stand in every part of the tour; all other pieces stand once, side by side.
/// </summary>
public abstract record ShowroomPiece(string Label);

/// <summary>
/// A door of <paramref name="Kind"/> (a cracked wall too) in the side wall, with a closet behind it. It stands in every
/// part of the tour - every wall variant at corridor and at room height. A locked door gets a key on the floor in front.
/// </summary>
public sealed record WallPiece(string Label, DoorKind Kind) : ShowroomPiece(Label);

/// <summary>A closed chest of <paramref name="Tier"/>, unlocked, with a little silver inside.</summary>
public sealed record ChestPiece(string Label, ChestTier Tier) : ShowroomPiece(Label);

/// <summary>The spiral staircase, standing (it cannot be climbed in the showroom).</summary>
public sealed record StairsPiece(string Label) : ShowroomPiece(Label);

/// <summary>The enemies a showroom can show.</summary>
public enum ShowroomEnemy
{
    Ghoul,
    Bat,
    Gorm,
}

/// <summary>An enemy as a harmless exhibit: it neither moves, attacks nor takes damage. At most one Gorm.</summary>
public sealed record EnemyPiece(string Label, ShowroomEnemy Enemy) : ShowroomPiece(Label);

/// <summary>
/// The crossbow skeleton in <paramref name="Stance"/>, as a figure only (issue #497): it neither moves, shoots nor
/// blocks the way.
/// </summary>
public sealed record CrossbowSkeletonPiece(string Label, CrossbowSkeletonStance Stance) : ShowroomPiece(Label);

/// <summary>A weapon lying on the floor, glowing in the colour of its tier - taken with F, as in the game.</summary>
public sealed record WeaponPiece(string Label, WeaponKind Weapon) : ShowroomPiece(Label);

/// <summary>An item lying on the floor, glowing in the colour of its tier - taken with F, as in the game (issue #317).</summary>
public sealed record ItemPiece(string Label, ItemKind Item) : ShowroomPiece(Label);

/// <summary>Silver or a key lying on the floor (taken when walked over, as in the game).</summary>
public sealed record LootPiece(string Label, LootKind Loot, int Amount) : ShowroomPiece(Label);

/// <summary>A consumable lying on the floor (taken when walked over, as in the game).</summary>
public sealed record ConsumablePiece(string Label, ConsumableKind Consumable) : ShowroomPiece(Label);

/// <summary>
/// Everything the showroom shows, in this order. A new model of a kind that is listed here needs one more entry - and
/// nothing else (docs/musterraum.md).
/// </summary>
public static class ShowroomPieces
{
    /// <summary>All exhibits, in the order they are placed.</summary>
    public static IReadOnlyList<ShowroomPiece> All { get; } =
    [
        new WallPiece("Wooden door", DoorKind.Normal),
        new WallPiece("Locked door", DoorKind.Locked),
        new WallPiece("Boss door", DoorKind.Boss),
        new WallPiece("Cracked wall", DoorKind.Cracked),
        new ChestPiece("Beggar chest", ChestTier.Beggar),
        new ChestPiece("Guard chest", ChestTier.Guard),
        new ChestPiece("King chest", ChestTier.King),
        new ChestPiece("Vampire chest", ChestTier.Vampire),
        new StairsPiece("Spiral stairs"),
        new EnemyPiece("Ghoul", ShowroomEnemy.Ghoul),
        new EnemyPiece("Bat", ShowroomEnemy.Bat),
        new EnemyPiece("Gorm", ShowroomEnemy.Gorm),
        new CrossbowSkeletonPiece("Crossbow skeleton (rest)", CrossbowSkeletonStance.Rest),
        new CrossbowSkeletonPiece("Crossbow skeleton (announce)", CrossbowSkeletonStance.Announce),
        new WeaponPiece("Silver Blade (Worn)", WeaponKind.SilverBlade),
        new WeaponPiece("Club (Worn)", WeaponKind.Club),
        new WeaponPiece("Pistol (Worn)", WeaponKind.Pistol),
        new WeaponPiece("Rifle (Forged)", WeaponKind.Rifle),
        new WeaponPiece("Blunderbuss (Forged)", WeaponKind.Blunderbuss),
        new ItemPiece("Hunter's Boots (Worn)", ItemKind.HuntersBoots),
        new ItemPiece("Silver Rosary (Forged)", ItemKind.SilverRosary),
        new ItemPiece("Hunter's Eye (Royal)", ItemKind.HuntersEye),
        new ItemPiece("Leech (Vampiric)", ItemKind.Leech),
        new LootPiece("Silver", LootKind.Silver, 10),
        new ConsumablePiece("Blood vial", ConsumableKind.BloodVial),
        new ConsumablePiece("Consecrated ammo", ConsumableKind.ConsecratedAmmo),
    ];
}
