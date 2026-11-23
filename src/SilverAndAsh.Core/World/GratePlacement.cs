namespace SilverAndAsh.Core.World;

/// <summary>
/// Hangs portcullises into the doors of a generated level (issue #558). For now (a provisional decision of the lead):
/// every normal or locked door of a combat room gets one - an arena that can be sealed. The boss room's door is a boss
/// door and gets none (it is barred at once as before), nor does a cracked wall. Nothing is rolled: the same level
/// always gets the same grates, and rooms, enemies, chests, teleporters and the well stay exactly as they were.
/// </summary>
public static class GratePlacement
{
    /// <summary>A copy of <paramref name="generated"/> whose arena doors have a grate (<see cref="LevelDoor.HasGrate"/>).</summary>
    public static GeneratedLevel Place(GeneratedLevel generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        return generated with { Doors = generated.Doors.Select(door => door with { HasGrate = GetsGrate(door, generated.Rooms) }).ToList() };
    }

    /// <summary>Whether <paramref name="door"/> gets a grate: a normal or locked door of a combat room.</summary>
    public static bool GetsGrate(LevelDoor door, IReadOnlyList<PlacedRoom> rooms)
    {
        ArgumentNullException.ThrowIfNull(door);
        ArgumentNullException.ThrowIfNull(rooms);
        if (door.Kind is not (DoorKind.Normal or DoorKind.Locked))
        {
            return false;
        }

        return rooms[door.RoomA].Kind == RoomKind.Combat || rooms[door.RoomB].Kind == RoomKind.Combat;
    }
}