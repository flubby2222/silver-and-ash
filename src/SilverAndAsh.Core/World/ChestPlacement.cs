namespace SilverAndAsh.Core.World;

/// <summary>
/// Puts chests on the chest places ('C') of a generated level (decision record 0006). Treasure rooms get 1-2 chests
/// (better ones behind a locked door) that stand from the start. About every fourth combat room
/// (<see cref="ChestsBalance.CombatRoomChance"/>) gets one, on one of its chest places; it rises from the floor once the
/// room is cleared. Its tier follows the combat shares of <see cref="ChestTierBalance"/> - "near" in the combat room
/// nearest to the start, "far" in the farthest, rising evenly in between. Lock and contents are rolled at once, so the
/// same seed always gives the same chests.
/// </summary>
public static class ChestPlacement
{
    /// <summary>A copy of <paramref name="generated"/> with its chests.</summary>
    public static GeneratedLevel Place(GeneratedLevel generated, ChestsBalance chests)
    {
        ArgumentNullException.ThrowIfNull(generated);
        ArgumentNullException.ThrowIfNull(chests);

        // Own random numbers (from the seed): chests never shift the enemies' places, and enemies never the chests.
        var random = new Random(unchecked((generated.Seed * 31) + 0xC4E57));
        var combat = generated.Rooms.Where(room => room.Kind == RoomKind.Combat).ToList();
        var nearest = combat.Count > 0 ? combat.Min(room => room.DistanceFromStart) : 0;
        var farthest = combat.Count > 0 ? combat.Max(room => room.DistanceFromStart) : 0;

        var plans = new List<ChestPlan>();
        foreach (var room in combat)
        {
            // Every combat room rolls, with or without a chest place - so templates never shift the others' chests.
            var hasChest = random.NextDouble() < chests.CombatRoomChance;
            var places = room.ChestSlots ?? [];
            if (!hasChest || places.Count == 0)
            {
                continue;
            }

            var (x, z) = places[random.Next(places.Count)];
            var far = farthest == nearest ? 0f : (room.DistanceFromStart - nearest) / (float)(farthest - nearest);
            var tier = PickTier(chests, values => values.CombatShareNear + ((values.CombatShareFar - values.CombatShareNear) * far), random);
            plans.Add(Roll(chests, tier, x, z, room.Index, risesWhenCleared: true, random));
        }

        // Treasure rooms: 1-2 chests standing from the start, better behind a locked door. Rolled after the combat
        // rooms' chests, so these stay as they were.
        foreach (var room in generated.Rooms.Where(room => room.Kind == RoomKind.Treasure))
        {
            var locked = generated.Doors.Any(door => (door.RoomA == room.Index || door.RoomB == room.Index) && door.Kind == DoorKind.Locked);
            var places = Shuffled(room.ChestSlots ?? [], random);
            var count = Math.Min(random.Next(chests.TreasureChestsMin, chests.TreasureChestsMax + 1), places.Count);
            foreach (var (x, z) in places.Take(count))
            {
                var tier = PickTier(chests, values => locked ? values.LockedTreasureShare : values.TreasureShare, random);
                plans.Add(Roll(chests, tier, x, z, room.Index, risesWhenCleared: false, random));
            }
        }

        return generated with { Chests = plans };
    }

    /// <summary>Lock and contents of a chest of <paramref name="tier"/> at (<paramref name="x"/>, <paramref name="z"/>).</summary>
    private static ChestPlan Roll(ChestsBalance chests, ChestTier tier, int x, int z, int room, bool risesWhenCleared, Random random)
    {
        var values = chests.Of(tier);
        var isLocked = random.NextDouble() < values.LockedChance;
        var silver = random.Next(values.SilverMin, values.SilverMax + 1);
        var hasVial = random.NextDouble() < values.BloodVialChance;
        return new ChestPlan(x, z, room, tier, isLocked, silver, hasVial, risesWhenCleared);
    }

    /// <summary>The places in random order (Fisher-Yates).</summary>
    private static List<(int X, int Z)> Shuffled(IReadOnlyList<(int X, int Z)> places, Random random)
    {
        var shuffled = places.ToList();
        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled;
    }

    /// <summary>
    /// A tier drawn by the <paramref name="share"/> each tier has here. The shares need not add up to 1; if all are 0
    /// it is a Beggar's Chest.
    /// </summary>
    private static ChestTier PickTier(ChestsBalance chests, Func<ChestTierBalance, float> share, Random random)
    {
        var tiers = Enum.GetValues<ChestTier>();
        var shares = tiers.Select(tier => MathF.Max(0f, share(chests.Of(tier)))).ToList();

        var total = shares.Sum();
        if (total <= 0f)
        {
            return ChestTier.Beggar;
        }

        var roll = (float)random.NextDouble() * total;
        for (var i = 0; i < tiers.Length; i++)
        {
            if (roll < shares[i])
            {
                return tiers[i];
            }

            roll -= shares[i];
        }

        // Only reached through rounding at the very top: the last tier with a share.
        return tiers[shares.FindLastIndex(share => share > 0f)];
    }
}
