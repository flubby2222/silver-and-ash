namespace SilverAndAsh.Core.World;

/// <summary>
/// Puts teleporters at the special places of a generated level (issue #436): one in the start room, near its middle,
/// and one in the room in front of the boss door, near that door. Worked out from the layout alone - no random number,
/// so the rooms, enemies and chests of every seed stay exactly as they were.
/// <para>
/// A teleporter stands on a floor cell of its room, never on a chest place, never on the player's start and at least
/// two cells from every door of the room (and from the start) - so nobody unlocks it by walking through a doorway.
/// If a room is too small for that, the rule is dropped and the nearest floor cell is taken.
/// </para>
/// </summary>
public static class TeleporterPlacement
{
    /// <summary>A copy of <paramref name="generated"/> with its teleporters.</summary>
    public static GeneratedLevel Place(GeneratedLevel generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        var plans = new List<TeleporterPlan>();
        var start = generated.Rooms.FirstOrDefault(room => room.Kind == RoomKind.Start);
        if (start is not null)
        {
            var (x, z) = Choose(generated, start, EnemyPlacement.Middle(start));
            plans.Add(new TeleporterPlan(x, z, start.Index, TeleporterSite.Start));
        }

        if (BossGate(generated) is { } gate && gate.Room.Index != start?.Index)
        {
            var (x, z) = Choose(generated, gate.Room, gate.Door);
            plans.Add(new TeleporterPlan(x, z, gate.Room.Index, TeleporterSite.BossGate));
        }

        return generated with { Teleporters = plans };
    }

    /// <summary>The room on the near side of the boss door and the door's cell - or null without a boss room.</summary>
    private static (PlacedRoom Room, (int X, int Z) Door)? BossGate(GeneratedLevel generated)
    {
        foreach (var door in generated.Doors)
        {
            var a = generated.Rooms[door.RoomA];
            var b = generated.Rooms[door.RoomB];
            if (a.Kind == RoomKind.Boss)
            {
                return (b, (door.X, door.Z));
            }

            if (b.Kind == RoomKind.Boss)
            {
                return (a, (door.X, door.Z));
            }
        }

        return null;
    }

    /// <summary>The allowed floor cell of <paramref name="room"/> nearest to <paramref name="near"/> (ties: smaller Z, then X).</summary>
    private static (int X, int Z) Choose(GeneratedLevel generated, PlacedRoom room, (int X, int Z) near)
    {
        var keepAway = generated.Doors
            .Where(door => door.RoomA == room.Index || door.RoomB == room.Index)
            .Select(door => (door.X, door.Z))
            .Append((generated.Level.PlayerStartX, generated.Level.PlayerStartZ))
            .ToList();
        var chestPlaces = (room.ChestSlots ?? []).ToHashSet();
        var allowed = room.FloorCells
            .Where(cell => !chestPlaces.Contains(cell) && keepAway.All(other => Steps(cell, other) >= 2))
            .ToList();
        var candidates = allowed.Count > 0 ? allowed : room.FloorCells.ToList();
        return candidates
            .OrderBy(cell => ((cell.X - near.X) * (cell.X - near.X)) + ((cell.Z - near.Z) * (cell.Z - near.Z)))
            .ThenBy(cell => cell.Z)
            .ThenBy(cell => cell.X)
            .First();
    }

    /// <summary>Steps between two cells when diagonal steps count as one (Chebyshev distance).</summary>
    private static int Steps((int X, int Z) a, (int X, int Z) b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Z - b.Z));
}
