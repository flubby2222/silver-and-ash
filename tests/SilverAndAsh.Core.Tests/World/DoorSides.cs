using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

/// <summary>
/// Issue #490, worked out in the tests on their own (not with <see cref="DoorClearance"/>): where a cell lies seen from
/// a door of its room - metres, and degrees off the way in (the way in leads to the floor cell next to the door).
/// </summary>
internal static class DoorSides
{
    /// <summary>Metres and degrees of <paramref name="cell"/> from <paramref name="door"/>, seen from the room <paramref name="roomCells"/>.</summary>
    public static (float Metres, float Degrees) From((int X, int Z) door, (int X, int Z) cell, IReadOnlyCollection<(int X, int Z)> roomCells)
    {
        (int X, int Z)[] steps = [(0, 1), (0, -1), (1, 0), (-1, 0)];
        var step = steps.Single(step => roomCells.Contains((door.X + step.X, door.Z + step.Z)));
        var towards = new Vector2(cell.X - door.X, cell.Z - door.Z);
        var cosine = Vector2.Dot(Vector2.Normalize(towards), new Vector2(step.X, step.Z));
        return (towards.Length() * Level.CellSize, MathF.Acos(Math.Clamp(cosine, -1f, 1f)) * 180f / MathF.PI);
    }

    /// <summary>True if <paramref name="cell"/> breaks the rule for <paramref name="door"/>: too near straight ahead, or beside the door nearer than the side distance.</summary>
    public static bool Breaks((int X, int Z) door, (int X, int Z) cell, IReadOnlyCollection<(int X, int Z)> roomCells, DifficultyBalance difficulty)
    {
        var (metres, degrees) = From(door, cell, roomCells);
        var needed = degrees <= difficulty.DoorConeAngle + 0.01f ? difficulty.DoorDistance : difficulty.SideDoorDistance;
        return metres < needed - 0.001f;
    }
}
