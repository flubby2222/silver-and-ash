using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The texts of the card after taking a piece of equipment (issue #317): its tier line, what it does and, for an item
/// whose effect is not in the game yet, a note saying so (issue #791). The game only draws them.
/// </summary>
public static class EquipmentCardText
{
    /// <summary>The note on the card of an item whose effect comes later.</summary>
    public const string NoEffectYet = "No effect yet - it comes in a later version.";

    /// <summary>The line under the name, e.g. "Vampiric item".</summary>
    public static string TierLine(Equipment piece)
    {
        ArgumentNullException.ThrowIfNull(piece);
        return $"{piece.Tier} {(piece.IsWeapon ? "weapon" : "item")}";
    }

    /// <summary>What the piece does.</summary>
    public static string Effect(Equipment piece)
    {
        ArgumentNullException.ThrowIfNull(piece);
        if (piece.Weapon is { } weapon)
        {
            var hand = Weapons.Info(weapon).Hand == WeaponHand.Melee ? "Melee weapon" : "Ranged weapon";
            return $"{hand} - now in your bag. Open it with G to take it in hand.";
        }

        return Items.Info(piece.Item!.Value).Effect;
    }

    /// <summary>
    /// <see cref="NoEffectYet"/> for an item whose effect is not in the game yet (<see cref="Items.HasEffect"/>), else
    /// null - a weapon never has a note.
    /// </summary>
    public static string? Note(Equipment piece)
    {
        ArgumentNullException.ThrowIfNull(piece);
        return piece.Item is { } item && !Items.HasEffect(item) ? NoEffectYet : null;
    }
}
