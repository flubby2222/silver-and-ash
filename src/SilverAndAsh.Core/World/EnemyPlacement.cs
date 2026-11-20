using System.Numerics;

namespace SilverAndAsh.Core.World;

/// <summary>
/// Puts enemies into a generated level with a <b>budget</b> (issue #157). Every combat room gets a budget that grows
/// with its distance from the start ("near" in the nearest combat room, "far" in the farthest, evenly in between); the
/// boss room has its own. The room buys enemies with it - each kind has a cost - and the kind is drawn by weights that
/// also go from "near" to "far". Every enemy stands on a random free floor cell of the room, keeping away from its
/// doors and from the other enemies. So the same room looks different every run. Every combat room gets at least one
/// enemy; no room holds more than <see cref="DifficultyBalance.MaxBatsPerRoom"/> bats; start rooms, corridors, treasure and secret rooms stay empty. The boss room holds its boss (Gorm) on his
/// place 'T' and - only if its budget is above 0 - more enemies. Same seed = same enemies.
/// </summary>
public static class EnemyPlacement
{
    private enum Kind
    {
        Ghoul,
        Bat,
    }

    /// <summary>A copy of <paramref name="generated"/> whose level has its ghouls and bats placed.</summary>
    public static GeneratedLevel Populate(GeneratedLevel generated, DifficultyBalance difficulty)
    {
        ArgumentNullException.ThrowIfNull(generated);
        ArgumentNullException.ThrowIfNull(difficulty);

        var random = new Random(generated.Seed);
        var ghouls = new List<(int X, int Z)>();
        var bats = new List<(int X, int Z)>();
        var bosses = new List<(int X, int Z)>();
        foreach (var room in generated.Rooms.Where(room => room.Kind is RoomKind.Combat or RoomKind.Boss))
        {
            var far = FarShare(room, generated.Rooms);
            var budget = BudgetOf(room, generated.Rooms, difficulty);
            var ghoulWeight = Lerp(difficulty.GhoulWeightNear, difficulty.GhoulWeightFar, far);
            var batWeight = Lerp(difficulty.BatWeightNear, difficulty.BatWeightFar, far);

            // The boss stands on his place ('T') - or, without one, on the floor cell nearest to the room's middle.
            if (room.Kind == RoomKind.Boss)
            {
                bosses.Add(room.BossSlots is { Count: > 0 } slots ? slots[0] : Middle(room));
            }

            var doors = DoorClearance.OfRoom(room, generated.Doors);
            var free = Shuffled(room.FloorCells, random)
                .Where(cell => DoorClearance.Keeps(cell, doors, difficulty))
                .Where(cell => !bosses.Contains(cell))
                .ToList();
            var placed = new List<((int X, int Z) Cell, Kind Kind)>();

            while (true)
            {
                var batsHere = placed.Count(enemy => enemy.Kind == Kind.Bat);
                var kind = Pick(budget, ghoulWeight, batsHere < difficulty.MaxBatsPerRoom ? batWeight : 0f, difficulty, random);
                if (kind is null)
                {
                    break;
                }

                var spot = free.FindIndex(cell => placed.All(other => Metres(cell, other.Cell) >= difficulty.Spacing));
                if (spot < 0)
                {
                    break;
                }

                placed.Add((free[spot], kind.Value));
                free.RemoveAt(spot);
                budget -= kind == Kind.Ghoul ? difficulty.GhoulCost : difficulty.BatCost;
            }

            // A combat room is never empty: one enemy even without budget or free place (then on the least bad cell).
            if (room.Kind == RoomKind.Combat && placed.Count == 0)
            {
                var cell = free.Count > 0 ? free[0] : DoorClearance.LeastBad(room.FloorCells, doors, difficulty);
                placed.Add((cell, ghoulWeight > 0f || batWeight <= 0f || difficulty.MaxBatsPerRoom <= 0 ? Kind.Ghoul : Kind.Bat));
            }

            ghouls.AddRange(placed.Where(enemy => enemy.Kind == Kind.Ghoul).Select(enemy => enemy.Cell));
            bats.AddRange(placed.Where(enemy => enemy.Kind == Kind.Bat).Select(enemy => enemy.Cell));
        }

        return generated with { Level = generated.Level.WithEnemies(ghouls, bats, bosses) };
    }

    /// <summary>
    /// The budget of <paramref name="room"/>: "near" in the nearest combat room, "far" in the farthest, evenly in
    /// between (rounded); the boss room's own. Other rooms have none.
    /// </summary>
    public static int BudgetOf(PlacedRoom room, IReadOnlyList<PlacedRoom> rooms, DifficultyBalance difficulty)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(difficulty);

        return room.Kind switch
        {
            RoomKind.Boss => difficulty.BudgetBoss,
            RoomKind.Combat => (int)MathF.Round(Lerp(difficulty.BudgetNear, difficulty.BudgetFar, FarShare(room, rooms)), MidpointRounding.AwayFromZero),
            _ => 0,
        };
    }

    // 0 = as near as the nearest combat room, 1 = as far as the farthest (and the boss room).
    private static float FarShare(PlacedRoom room, IReadOnlyList<PlacedRoom> rooms)
    {
        if (room.Kind == RoomKind.Boss)
        {
            return 1f;
        }

        var combat = rooms.Where(other => other.Kind == RoomKind.Combat).Select(other => other.DistanceFromStart).ToList();
        var nearest = combat.Min();
        var farthest = combat.Max();
        return farthest == nearest ? 0f : (room.DistanceFromStart - nearest) / (float)(farthest - nearest);
    }

    // The next enemy the budget can still buy, drawn by the weights - or null if it buys nothing more.
    private static Kind? Pick(int budget, float ghoulWeight, float batWeight, DifficultyBalance difficulty, Random random)
    {
        var ghoul = budget >= difficulty.GhoulCost ? MathF.Max(0f, ghoulWeight) : 0f;
        var bat = budget >= difficulty.BatCost ? MathF.Max(0f, batWeight) : 0f;
        if (ghoul + bat <= 0f)
        {
            return null;
        }

        return random.NextDouble() * (ghoul + bat) < ghoul ? Kind.Ghoul : Kind.Bat;
    }

    /// <summary>The floor cell of <paramref name="room"/> nearest to its middle.</summary>
    public static (int X, int Z) Middle(PlacedRoom room)
    {
        ArgumentNullException.ThrowIfNull(room);
        var x = room.FloorCells.Average(cell => cell.X);
        var z = room.FloorCells.Average(cell => cell.Z);
        return room.FloorCells
            .OrderBy(cell => ((cell.X - x) * (cell.X - x)) + ((cell.Z - z) * (cell.Z - z)))
            .ThenBy(cell => cell.Z)
            .ThenBy(cell => cell.X)
            .First();
    }

    private static float Lerp(float near, float far, float share) => near + ((far - near) * share);

    private static float Metres((int X, int Z) a, (int X, int Z) b) =>
        Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z)) * Level.CellSize;

    // The cells in random order (Fisher-Yates).
    private static List<(int X, int Z)> Shuffled(IReadOnlyList<(int X, int Z)> cells, Random random)
    {
        var shuffled = cells.ToList();
        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled;
    }
}
