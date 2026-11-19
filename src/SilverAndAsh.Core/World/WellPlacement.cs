namespace SilverAndAsh.Core.World;

/// <summary>
/// Puts the Well of Sirius (issue #438) into a generated level: at most one, with the chance
/// <see cref="WellBalance.LevelChance"/>. It stands in the middle of a floor cell of a quiet room - not a combat or boss
/// room, so enemies never have to come out next to it - whose eight neighbours are floor of the same room too (no wall,
/// no door), with no chest and not the player's start in that block. Rolled with its own random numbers from the seed,
/// after the chests: it never shifts rooms, enemies or chests, and the same seed always gives the same well.
/// </summary>
public static class WellPlacement
{
    /// <summary>A copy of <paramref name="generated"/> with its well - or without one if the roll says so or no cell fits.</summary>
    public static GeneratedLevel Place(GeneratedLevel generated, WellBalance well)
    {
        ArgumentNullException.ThrowIfNull(generated);
        ArgumentNullException.ThrowIfNull(well);

        var random = new Random(unchecked((generated.Seed * 53) + 0x3E11));
        if (random.NextDouble() >= well.LevelChance)
        {
            return generated with { Well = null };
        }

        var cells = Candidates(generated);
        if (cells.Count == 0)
        {
            return generated with { Well = null };
        }

        var (x, z, room) = cells[random.Next(cells.Count)];
        return generated with { Well = new WellPlan(x, z, room) };
    }

    /// <summary>All cells a well may stand on, in a fixed order (room, then row, then column).</summary>
    public static IReadOnlyList<(int X, int Z, int Room)> Candidates(GeneratedLevel generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        var level = generated.Level;
        var chests = (generated.Chests ?? []).Select(chest => (chest.X, chest.Z)).ToHashSet();
        var start = (level.PlayerStartX, level.PlayerStartZ);

        var candidates = new List<(int X, int Z, int Room)>();
        foreach (var room in generated.Rooms.Where(IsQuiet))
        {
            var floor = room.FloorCells.ToHashSet();
            var blocked = chests.Concat((room.ChestSlots ?? []).Concat([start])).ToHashSet();
            foreach (var (x, z) in room.FloorCells.OrderBy(cell => cell.Z).ThenBy(cell => cell.X))
            {
                if (Block(x, z).All(floor.Contains) && !Block(x, z).Any(blocked.Contains))
                {
                    candidates.Add((x, z, room.Index));
                }
            }
        }

        return candidates;
    }

    // Combat and boss rooms are where enemies come out and fight; a well stands elsewhere.
    private static bool IsQuiet(PlacedRoom room) => room.Kind is not (RoomKind.Combat or RoomKind.Boss);

    // The cell and its eight neighbours.
    private static IEnumerable<(int X, int Z)> Block(int x, int z)
    {
        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                yield return (x + dx, z + dz);
            }
        }
    }
}
