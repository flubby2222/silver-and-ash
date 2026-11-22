namespace SilverAndAsh.Core.World;

/// <summary>
/// Hangs portcullises into the doors of a generated level (issue #558). For now (a provisional decision of the lead):
/// every normal or locked door of a combat room gets one - an arena that can be sealed - but no door of the boss room,
/// whose boss door is barred at once as before, and no cracked wall. Nothing is rolled: the same level always gets the
/// same grates, and rooms, enemies, chests, teleporters and the well stay exactly as they were.
/// </summary>
public static class GratePlacement
{
    /// <summary>A copy of <paramref name="generated"/> whose arena doors have a grate (<see cref="LevelDoor.HasGrate"/>).</summary>
    public static GeneratedLevel Place(GeneratedLevel generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        return generated with { Doors = generated.Doors.Select(door => door with { HasGrate = GetsGrate(door, generated.Rooms) }).ToList() };
    }

    /// <summary>Whether <paramref name="door"/> gets a grate: a normal or locked door of a combat room, not of the boss room.</summary>
    public static bool GetsGrate(LevelDoor door, IReadOnlyList<PlacedRoom> rooms)
    {
        ArgumentNullException.ThrowIfNull(door);
        ArgumentNullException.ThrowIfNull(rooms);
        if (door.Kind is not (DoorKind.Normal or DoorKind.Locked))
        {
            return false;
        }

        var kinds = new[] { rooms[door.RoomA].Kind, rooms[door.RoomB].Kind };
        return kinds.Contains(RoomKind.Combat) && !kinds.Contains(RoomKind.Boss);
    }
}