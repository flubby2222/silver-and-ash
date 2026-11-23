namespace SilverAndAsh.Core.World;

/// <summary>
/// The antechamber in front of the boss room (issue #565): a quiet room of 4 x 5 cells (8 x 10 m) with the high room
/// ceiling, no enemies, no chests. Its entrance sits in the middle of one long (5-cell) side; the boss door sits on a
/// short (4-cell) side next to it, at the middle cell nearer the entrance - so the way to the boss turns once.
/// <para>
/// The layout is built in, not read from assets/rooms/: every level with a boss room gets exactly this room between
/// the corridor (or the room) before it and the boss room, turned and mirrored to fit - no random number is used for it.
/// </para>
/// </summary>
public static class Antechamber
{
    /// <summary>Floor cells from west to east, on the short side with the boss door.</summary>
    public const int Width = 4;

    /// <summary>Floor cells from north to south, on the long side with the entrance.</summary>
    public const int Depth = 5;

    /// <summary>The name in messages and logs.</summary>
    public const string Name = "antechamber (built in)";

    // Entrance '+' on the west side, row 3 of 5 (the middle); boss door '+' on the north side, column 2 of 4.
    private static readonly IReadOnlyList<string> Layout =
    [
        "##+###",
        "#....#",
        "#....#",
        "+....#",
        "#....#",
        "#....#",
        "######",
    ];

    /// <summary>The room template, unturned: entrance in the west, boss door in the north.</summary>
    public static RoomTemplate Template { get; } = RoomTemplateParser.Parse(string.Join('\n', Layout), RoomKind.Antechamber, Name);

    /// <summary>
    /// True if <paramref name="slot"/> of <paramref name="template"/> (the antechamber, turned or mirrored) is its entrance:
    /// the door slot on a long side. The other one leads to the boss room.
    /// </summary>
    public static bool IsEntrance(RoomTemplate template, DoorSlot slot)
    {
        ArgumentNullException.ThrowIfNull(template);
        var onWestOrEast = slot.Side is RoomSide.West or RoomSide.East;
        return onWestOrEast == template.Depth > template.Width;
    }
}
