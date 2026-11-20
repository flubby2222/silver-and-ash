using System.Numerics;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>Loot lying in the showroom from the start: <paramref name="Amount"/> of <paramref name="Kind"/> at <paramref name="Position"/>.</summary>
public sealed record ExhibitLoot(LootKind Kind, int Amount, Vector3 Position);

/// <summary>A consumable lying in the showroom from the start.</summary>
public sealed record ExhibitConsumable(ConsumableKind Kind, Vector3 Position);

/// <summary>A weapon lying in the showroom from the start.</summary>
public sealed record ExhibitWeapon(WeaponKind Kind, Vector3 Position);

/// <summary>An item lying in the showroom from the start (issue #317) - it can be taken with F, as in the game.</summary>
public sealed record ExhibitItem(ItemKind Kind, Vector3 Position);

/// <summary>
/// A crossbow skeleton standing in the showroom in <paramref name="Stance"/>, looking along <paramref name="Facing"/>
/// (issue #497). Only a figure: no body, no collision, no shots - the enemy itself comes with issue #334.
/// </summary>
public sealed record ExhibitCrossbowSkeleton(CrossbowSkeletonStance Stance, Vector3 Position, Vector3 Facing);

/// <summary>
/// A crypt warden standing in the showroom in <paramref name="Stance"/>, looking along <paramref name="Facing"/>
/// (issue #530). Only a figure: no body, no collision, no attack - the enemy itself comes with issue #335.
/// </summary>
public sealed record ExhibitCryptWarden(CryptWardenStance Stance, Vector3 Position, Vector3 Facing);

/// <summary>
/// A wall pillar standing in the showroom against a wall of <paramref name="Variant"/> under a ceiling of
/// <paramref name="CeilingHeight"/> metres, on the floor at <paramref name="Position"/> (the wall's face), looking along
/// <paramref name="Facing"/> (issue #536). Only decoration: no body, no collision (<see cref="WallPillarLook"/>).
/// </summary>
public sealed record ExhibitWallPillar(TextureVariant Variant, float CeilingHeight, Vector3 Position, Vector3 Facing);

/// <summary>
/// What the showroom (issue #283) puts into a world besides its level, doors, chests and enemies. A world with exhibits
/// is a showroom (<see cref="GameWorld.IsShowroom"/>): its enemies are exhibits that neither move, attack nor take damage,
/// and its staircase stands from the start but cannot be climbed. A normal game never has exhibits.
/// </summary>
/// <param name="Stairs">Bottom centre of the staircase, or null for none.</param>
public sealed record ShowroomExhibits(
    Vector3? Stairs,
    IReadOnlyList<ExhibitLoot> Loot,
    IReadOnlyList<ExhibitConsumable> Consumables,
    IReadOnlyList<ExhibitWeapon> Weapons,
    IReadOnlyList<ExhibitItem> Items)
{
    /// <summary>Crossbow skeletons standing as figures (issue #497).</summary>
    public IReadOnlyList<ExhibitCrossbowSkeleton> CrossbowSkeletons { get; init; } = [];

    /// <summary>Crypt wardens standing as figures (issue #530).</summary>
    public IReadOnlyList<ExhibitCryptWarden> CryptWardens { get; init; } = [];

    /// <summary>Wall pillars against the north walls of the sections (issue #536).</summary>
    public IReadOnlyList<ExhibitWallPillar> WallPillars { get; init; } = [];
}
