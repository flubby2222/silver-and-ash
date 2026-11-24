namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Loot, consumables, weapons and items lying on the floor of a world from the start - in a normal game with every
/// rule, unlike the showroom's exhibits (<see cref="ShowroomExhibits"/>). The test course (issue #718) lays its finds
/// with it; a generated run has none.
/// </summary>
public sealed record GroundFinds(
    IReadOnlyList<ExhibitLoot> Loot,
    IReadOnlyList<ExhibitConsumable> Consumables,
    IReadOnlyList<ExhibitWeapon> Weapons,
    IReadOnlyList<ExhibitItem> Items);
