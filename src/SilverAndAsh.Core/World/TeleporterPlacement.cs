using System.Numerics;

namespace SilverAndAsh.Core.World;

/// <summary>
/// Puts teleporters at the special places of a generated level (issue #436): one in the start room, near its middle,
/// and one in the room in front of the boss door. Worked out from the layout alone - no random number,
/// so the rooms, enemies and chests of every seed stay exactly as they were.
/// <para>
/// The room in front of the boss door is the antechamber (issue #565). There the teleporter stands in the corner across
/// from the boss door - the allowed cell farthest from it - turned towards the middle of the room. This place is a
/// provisional decision of the lead (vorläufige Entscheidung der Leitung). In front of any other room it stands at the
/// allowed cell nearest to the boss door, as before.
/// </para>
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
            plans.Add(BossGateTeleporter(generated, gate.Room, gate.Door));
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

    /// <summary>
    /// The teleporter at the boss door: in the antechamber in the corner across from the door, looking at the room's middle
    /// (provisional decision of the lead, issue #565); in any other room near the door, not turned.
    /// </summary>
    private static TeleporterPlan BossGateTeleporter(GeneratedLevel generated, PlacedRoom room, (int X, int Z) door)
    {
        if (room.Kind != RoomKind.Antechamber)
        {
            var (nearX, nearZ) = Choose(generated, room, door);
            return new TeleporterPlan(nearX, nearZ, room.Index, TeleporterSite.BossGate);
        }

        var (x, z) = Choose(generated, room, door, farthest: true);
        var middle = new Vector2(
            (float)room.FloorCells.Average(cell => cell.X), (float)room.FloorCells.Average(cell => cell.Z));
        return new TeleporterPlan(x, z, room.Index, TeleporterSite.BossGate) { Facing = Vector2.Normalize(middle - new Vector2(x, z)) };
    }

    /// <summary>
    /// The allowed floor cell of <paramref name="room"/> nearest to <paramref name="near"/> - or, with
    /// <paramref name="farthest"/>, farthest from it (ties: smaller Z, then X).
    /// </summary>
    private static (int X, int Z) Choose(GeneratedLevel generated, PlacedRoom room, (int X, int Z) near, bool farthest = false)
    {
        var keepAway = generated.Doors
            .Where(door => door.RoomA == room.Index || door.RoomB == room.Index)
            .SelectMany(door => door.Cells)
            .Append((generated.Level.PlayerStartX, generated.Level.PlayerStartZ))
            .ToList();
        var chestPlaces = (room.ChestSlots ?? []).ToHashSet();
        var allowed = room.FloorCells
            .Where(cell => !chestPlaces.Contains(cell) && keepAway.All(other => Steps(cell, other) >= 2))
            .ToList();
        var candidates = allowed.Count > 0 ? allowed : room.FloorCells.ToList();
        return candidates
            .OrderBy(cell => (farthest ? -1 : 1) * (((cell.X - near.X) * (cell.X - near.X)) + ((cell.Z - near.Z) * (cell.Z - near.Z))))
            .ThenBy(cell => cell.Z)
            .ThenBy(cell => cell.X)
            .First();
    }

    /// <summary>Steps between two cells when diagonal steps count as one (Chebyshev distance).</summary>
    private static int Steps((int X, int Z) a, (int X, int Z) b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Z - b.Z));
}
