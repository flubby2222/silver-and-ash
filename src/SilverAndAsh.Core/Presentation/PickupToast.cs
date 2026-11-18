using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The text of the pickup toast above the bottom left of the HUD (issue #369), e.g. "Picked up Hunter's Eye": for
/// equipment, consumables and keys a player takes. Silver has none - its number in the HUD changes anyway.
/// </summary>
public static class PickupToast
{
    /// <summary>
    /// The toast text for <paramref name="gameEvent"/> if it is something player <paramref name="playerId"/> took,
    /// else null.
    /// </summary>
    public static string? TextFor(GameEvent gameEvent, int playerId) => gameEvent switch
    {
        EquipmentTaken taken when taken.PlayerId == playerId => Picked(taken.Piece.Name),
        PickupTaken taken when taken.PlayerId == playerId => Picked(NameOf(taken.Kind)),
        KeyTaken taken when taken.PlayerId == playerId => Picked("a Key"),
        _ => null,
    };

    /// <summary>Every text the toast can show - for the test that each fits its frame.</summary>
    public static IEnumerable<string> AllTexts() =>
        Equipment.All.Select(piece => Picked(piece.Name))
            .Concat(Enum.GetValues<ConsumableKind>().Select(kind => Picked(NameOf(kind))))
            .Append(Picked("a Key"));

    /// <summary>The name of a consumable as the toast shows it.</summary>
    public static string NameOf(ConsumableKind kind) => kind switch
    {
        ConsumableKind.BloodVial => "Blood Vial",
        ConsumableKind.ConsecratedAmmo => "Consecrated Ammo",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown consumable."),
    };

    private static string Picked(string name) => $"Picked up {name}";
}
