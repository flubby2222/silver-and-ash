namespace SilverAndAsh.Core.World;

/// <summary>
/// Puts enemies on the enemy places ('G', 'B') of a generated level (ADR 0005: harder the farther from the start).
/// In the combat room nearest to the start the "near" shares of <see cref="DifficultyBalance"/> are taken, in the
/// farthest one the "far" shares, in between it rises evenly. The boss room always gets the far shares. Start rooms
/// and corridors stay empty; every combat room gets at least one enemy. Same seed = same enemies.
/// </summary>
public static class EnemyPlacement
{
    /// <summary>A copy of <paramref name="generated"/> whose level has its ghouls and bats placed.</summary>
    public static GeneratedLevel Populate(GeneratedLevel generated, DifficultyBalance difficulty)
    {
        ArgumentNullException.ThrowIfNull(generated);
        ArgumentNullException.ThrowIfNull(difficulty);

        var random = new Random(generated.Seed);
        var combat = generated.Rooms.Where(room => room.Kind == RoomKind.Combat).ToList();
        var nearest = combat.Count > 0 ? combat.Min(room => room.DistanceFromStart) : 0;
        var farthest = combat.Count > 0 ? combat.Max(room => room.DistanceFromStart) : 0;

        var ghouls = new List<(int X, int Z)>();
        var bats = new List<(int X, int Z)>();
        foreach (var room in generated.Rooms.Where(room => room.Kind is RoomKind.Combat or RoomKind.Boss))
        {
            // 0 = as near as the nearest combat room, 1 = as far as the farthest (or the boss room).
            var far = room.Kind == RoomKind.Boss || farthest == nearest
                ? (room.Kind == RoomKind.Boss ? 1f : 0f)
                : (room.DistanceFromStart - nearest) / (float)(farthest - nearest);
            var ghoulShare = difficulty.GhoulShareNear + (difficulty.GhoulShareFar - difficulty.GhoulShareNear) * far;
            var batShare = difficulty.BatShareNear + (difficulty.BatShareFar - difficulty.BatShareNear) * far;

            var roomGhouls = Pick(room.GhoulSlots, Count(ghoulShare, room.GhoulSlots.Count), random);
            var roomBats = Pick(room.BatSlots, Count(batShare, room.BatSlots.Count), random);

            if (room.Kind == RoomKind.Combat && roomGhouls.Count + roomBats.Count == 0)
            {
                if (room.GhoulSlots.Count > 0)
                {
                    roomGhouls = Pick(room.GhoulSlots, 1, random);
                }
                else
                {
                    roomBats = Pick(room.BatSlots, 1, random);
                }
            }

            ghouls.AddRange(roomGhouls);
            bats.AddRange(roomBats);
        }

        return generated with { Level = generated.Level.WithEnemies(ghouls, bats) };
    }

    private static int Count(float share, int places) =>
        Math.Clamp((int)MathF.Round(share * places, MidpointRounding.AwayFromZero), 0, places);

    // 'count' of the places, chosen at random (shuffled with Fisher-Yates, then the first ones).
    private static List<(int X, int Z)> Pick(IReadOnlyList<(int X, int Z)> places, int count, Random random)
    {
        var shuffled = places.ToList();
        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled.Take(count).ToList();
    }
}
