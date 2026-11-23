using System.Numerics;

namespace SilverAndAsh.Core.World;

/// <summary>A door of a room, seen from that room: its cell and the way into the room through it (flat, length 1).</summary>
/// <param name="Cell">The door cell.</param>
/// <param name="Inward">The direction whoever comes in through the door walks and looks (X, Z).</param>
public readonly record struct RoomDoor((int X, int Z) Cell, Vector2 Inward);

/// <summary>
/// How far an enemy must start from the doors of its room (issue #490). Straight into the room (inside the cone of
/// <see cref="DifficultyBalance.DoorConeAngle"/>) whoever comes in sees it, so <see cref="DifficultyBalance.DoorDistance"/>
/// is enough; beside the door frame nobody sees it, so there it needs <see cref="DifficultyBalance.SideDoorDistance"/>.
/// The level generator's enemy placement and the world (where an enemy comes out) both ask this one rule.
/// </summary>
public static class DoorClearance
{
    /// <summary>
    /// The door at <paramref name="door"/> seen from the room with the floor <paramref name="roomCells"/>: the way in
    /// points to the room's floor cells nearest to the door (to their middle if there are several).
    /// </summary>
    public static RoomDoor Of((int X, int Z) door, IEnumerable<(int X, int Z)> roomCells)
    {
        ArgumentNullException.ThrowIfNull(roomCells);
        var from = new Vector2(door.X, door.Z);
        var offsets = roomCells.Select(cell => new Vector2(cell.X, cell.Z) - from).ToList();
        if (offsets.Count == 0)
        {
            throw new ArgumentException("A room needs floor cells.", nameof(roomCells));
        }

        var nearest = offsets.Min(offset => offset.LengthSquared());
        var inward = offsets.Where(offset => offset.LengthSquared() <= nearest + 0.001f).Aggregate(Vector2.Zero, (sum, offset) => sum + offset);
        return new RoomDoor(door, inward == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(inward));
    }

    /// <summary>
    /// The doors of <paramref name="room"/> among <paramref name="doors"/>, each seen from that room - one for every cell
    /// of a door (issue #566: the boss door has two).
    /// </summary>
    public static IReadOnlyList<RoomDoor> OfRoom(PlacedRoom room, IEnumerable<LevelDoor> doors)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(doors);
        return doors
            .Where(door => door.RoomA == room.Index || door.RoomB == room.Index)
            .SelectMany(door => door.Cells.Select(cell => Of(cell, room.FloorCells)))
            .ToList();
    }

    /// <summary>True if an enemy on <paramref name="cell"/> keeps far enough from every one of <paramref name="doors"/>.</summary>
    public static bool Keeps((int X, int Z) cell, IEnumerable<RoomDoor> doors, DifficultyBalance difficulty) =>
        Shortfall(cell, doors, difficulty) <= 0f;

    /// <summary>
    /// Metres an enemy on <paramref name="cell"/> is too near to the worst of <paramref name="doors"/> (0 or less: far
    /// enough from all). With no cell that keeps the rule, the one with the least shortfall is the least bad.
    /// </summary>
    public static float Shortfall((int X, int Z) cell, IEnumerable<RoomDoor> doors, DifficultyBalance difficulty)
    {
        ArgumentNullException.ThrowIfNull(doors);
        ArgumentNullException.ThrowIfNull(difficulty);
        return doors.Select(door => Needed(cell, door, difficulty) - Metres(cell, door)).DefaultIfEmpty(float.MinValue).Max();
    }

    /// <summary>
    /// For a room where no cell keeps the rule: the cell of <paramref name="cells"/> with the least
    /// <see cref="Shortfall"/> - as far into the cone or as far from the doors as the room allows. Equal ones: the one
    /// farther from the nearest door, then the first by row and column. No chance involved.
    /// </summary>
    public static (int X, int Z) LeastBad(IEnumerable<(int X, int Z)> cells, IReadOnlyList<RoomDoor> doors, DifficultyBalance difficulty)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(doors);
        return cells
            .OrderBy(cell => Shortfall(cell, doors, difficulty))
            .ThenByDescending(cell => doors.Select(door => Metres(cell, door)).DefaultIfEmpty(0f).Min())
            .ThenBy(cell => cell.Z)
            .ThenBy(cell => cell.X)
            .First();
    }

    /// <summary>The least distance (metres) <paramref name="cell"/> needs from <paramref name="door"/>: straight in or beside it.</summary>
    public static float Needed((int X, int Z) cell, RoomDoor door, DifficultyBalance difficulty)
    {
        ArgumentNullException.ThrowIfNull(difficulty);
        // A little leeway, so that a cone of exactly 45 degrees takes in the diagonal cells.
        return AngleDegrees(cell, door) <= difficulty.DoorConeAngle + 0.001f ? difficulty.DoorDistance : difficulty.SideDoorDistance;
    }

    /// <summary>Degrees between the way in through <paramref name="door"/> and the line from the door to <paramref name="cell"/> (0 = straight ahead).</summary>
    public static float AngleDegrees((int X, int Z) cell, RoomDoor door)
    {
        var towards = new Vector2(cell.X - door.Cell.X, cell.Z - door.Cell.Z);
        if (towards == Vector2.Zero || door.Inward == Vector2.Zero)
        {
            return 180f;
        }

        var cosine = Math.Clamp(Vector2.Dot(Vector2.Normalize(towards), door.Inward), -1f, 1f);
        return MathF.Acos(cosine) * 180f / MathF.PI;
    }

    /// <summary>Metres from the middle of <paramref name="cell"/> to the middle of the door cell.</summary>
    public static float Metres((int X, int Z) cell, RoomDoor door) =>
        Vector2.Distance(new Vector2(cell.X, cell.Z), new Vector2(door.Cell.X, door.Cell.Z)) * Level.CellSize;
}
