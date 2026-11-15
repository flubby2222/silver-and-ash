namespace SilverAndAsh.Core.World;

/// <summary>The side of a room template, seen from above: north is the first line of the file, west the first column.</summary>
public enum RoomSide
{
    North,
    East,
    South,
    West,
}

/// <summary>
/// A place on the outer wall of a room template ('+') where the generator may put a door.
/// The cell stays a wall until a door is placed there.
/// </summary>
/// <param name="X">Column of the slot inside the template (0-based).</param>
/// <param name="Z">Row of the slot inside the template (0-based).</param>
/// <param name="Side">The outer wall the slot sits in; a door here leads in that direction.</param>
public readonly record struct DoorSlot(int X, int Z, RoomSide Side);
