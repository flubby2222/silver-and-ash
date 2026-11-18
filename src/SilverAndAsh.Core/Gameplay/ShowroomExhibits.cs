using System.Numerics;

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
    IReadOnlyList<ExhibitItem> Items);
