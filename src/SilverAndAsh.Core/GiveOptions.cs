using System.Collections.Frozen;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core;

/// <summary>
/// The test option <c>--give &lt;name&gt;</c>: every run starts with this weapon or item, to try it out before it can be
/// found (issue #791 added the items). The names are those of <see cref="WeaponKind"/> and <see cref="ItemKind"/> in
/// any case, e.g. <c>club</c> or <c>leech</c>. The game honours it only in its debug version.
/// </summary>
public static class GiveOptions
{
    /// <summary>Every piece <c>--give</c> knows, by its name on the command line - weapons and items alike.</summary>
    private static readonly FrozenDictionary<string, Equipment> Pieces = Equipment.All.ToFrozenDictionary(
        piece => piece.Weapon?.ToString() ?? piece.Item!.Value.ToString(), StringComparer.OrdinalIgnoreCase);

    /// <summary>The known names, in lower case and in the order of <see cref="Equipment.All"/> - for the messages.</summary>
    public static string Known { get; } = string.Join(
        ", ", Equipment.All.Select(piece => (piece.Weapon?.ToString() ?? piece.Item!.Value.ToString()).ToLowerInvariant()));

    /// <summary>The piece named <paramref name="name"/>.</summary>
    /// <exception cref="ArgumentException">If no weapon or item has this name; the message names the known ones.</exception>
    public static Equipment Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Pieces.TryGetValue(name, out var piece) ? piece : throw new ArgumentException($"Unknown weapon '{name}'. Known: {Known}.");
    }

    /// <summary>
    /// Gives <paramref name="world"/>'s player the pieces in <paramref name="give"/>, in their order, and returns the
    /// world: a melee weapon goes into the hand (the one before into the bag), a ranged one into the first free place or
    /// into the bag when all are taken, an item among the items.
    /// </summary>
    public static GameWorld GiveTo(GameWorld world, IReadOnlyList<Equipment> give)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(give);
        foreach (var piece in give)
        {
            if (piece.Item is { } item)
            {
                world.Player.Take(item);
            }
            else
            {
                GiveWeapon(world.Player, piece.Weapon!.Value);
            }
        }

        return world;
    }

    private static void GiveWeapon(Player player, WeaponKind kind)
    {
        if (Weapons.Info(kind).Hand == WeaponHand.Melee)
        {
            player.Stow(player.Melee);
            player.EquipMelee(kind);
            return;
        }

        var free = player.RangedSlots.ToList().FindIndex(weapon => weapon is null);
        if (free >= 0)
        {
            player.Equip(free, kind);
        }
        else
        {
            player.Stow(kind);
        }
    }
}
