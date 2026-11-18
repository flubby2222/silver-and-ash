using System.Globalization;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Presentation;

/// <summary>One line of the map page: a name on the left, its value on the right.</summary>
public readonly record struct SheetLine(string Label, string Value);

/// <summary>
/// The lines on the left of the map page (issue #347): a player's values and the boons they chose. Everything is read
/// from the game state - the damage through the world's own damage rules - so the page shows what really happens.
/// Pure text - the Game project draws it.
/// </summary>
public static class CharacterSheet
{
    /// <summary>
    /// The values of <paramref name="player"/> in <paramref name="world"/>: level, health, stamina, the damage of the
    /// melee weapon and of the ranged weapon in hand (one body hit, uncharged), walking speed and the experience bonus.
    /// </summary>
    public static IReadOnlyList<SheetLine> Stats(GameWorld world, Player player)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);
        var melee = world.Balance.Melee(player.Melee);
        var ranged = world.Balance.Ranged(player.Ranged.Kind);
        var meleeDamage = Damage(world, player, DamageWeapon(player.Melee), melee.Damage);
        var rangedDamage = Damage(world, player, DamageWeapon(player.Ranged.Kind), ranged.Damage);
        var perShot = ranged.Pellets > 1 ? Text($"{rangedDamage} x {ranged.Pellets}") : Text($"{rangedDamage}");

        return
        [
            new("Level", Text($"{player.Level}")),
            new("Health", Text($"{player.Health} / {player.MaxHealth}")),
            new("Stamina", Text($"{player.Stamina.Segments} bars")),
            new("Stamina regen", Percent(player.Stamina.RegenMultiplier)),
            new("Melee", Text($"{meleeDamage}  {Weapons.Info(player.Melee).Name}")),
            new("Ranged", Text($"{perShot}  {player.Ranged.Info.Name}")),
            new("Speed", Text($"{player.WalkSpeed:0.#} m/s")),
            new("Experience", Percent(player.ExperienceMultiplier)),
        ];
    }

    /// <summary>
    /// The boons <paramref name="player"/> chose, in the order of <see cref="Boon"/>, each with how often and what they
    /// give together, e.g. "STRENGTH x2" / "+10 % damage". Empty while none is chosen.
    /// </summary>
    public static IReadOnlyList<SheetLine> Boons(Player player, BoonBalance boons)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(boons);
        return Enum.GetValues<Boon>()
            .Where(boon => player.BoonCount(boon) > 0)
            .Select(boon => new SheetLine(
                Text($"{BoonTexts.Name(boon)} x{player.BoonCount(boon)}"),
                $"{BoonTexts.Amount(boon, boons, player.BoonCount(boon))} {BoonTexts.What(boon)}"))
            .ToList();
    }

    // One body hit, uncharged, with the player's damage boons - through the same rules as a real hit.
    private static int Damage(GameWorld world, Player player, Weapon weapon, int baseDamage) =>
        world.DamageRules.Calculate(new Hit(weapon, HitZone.Body, baseDamage, DamageBoons: player.BoonCount(Boon.Damage))).Amount;

    private static Weapon DamageWeapon(WeaponKind kind) => kind switch
    {
        WeaponKind.SilverBlade => Weapon.Blade,
        WeaponKind.Club => Weapon.Club,
        WeaponKind.Pistol => Weapon.Pistol,
        WeaponKind.Rifle => Weapon.Rifle,
        WeaponKind.Blunderbuss => Weapon.Blunderbuss,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown weapon."),
    };

    private static string Percent(float multiplier) => Text($"{multiplier * 100f:0.#} %");

    private static string Text(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
