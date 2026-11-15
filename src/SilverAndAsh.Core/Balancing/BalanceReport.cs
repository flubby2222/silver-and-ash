using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Balancing;

/// <summary>One number about one level, e.g. ("Enemies", "Ghouls", 31). The group sorts the report into blocks.</summary>
public sealed record Figure(string Group, string Name, double Value);

/// <summary>One figure over many levels: its average, smallest and largest value.</summary>
public sealed record FigureSummary(string Group, string Name, double Average, double Min, double Max);

/// <summary>
/// The balance report: what a level brings with a set of balancing values - without playing. It measures levels built
/// exactly as in the game (<see cref="LevelZero.Build"/>) and sums them up. Counts (rooms, enemies, chests ...) are
/// exact; everything left to chance (drops, keys from enemies) is its expected value - chance times number. The play
/// time is a rough estimate and labelled as one.
/// </summary>
public static class BalanceReport
{
    public const string Rooms = "Rooms";
    public const string Enemies = "Enemies";
    public const string Silver = "Silver";
    public const string Keys = "Keys and locks";
    public const string Chests = "Chests";
    public const string Healing = "Healing";
    public const string Time = "Time (rough estimate)";

    /// <summary>Builds the levels of <paramref name="seeds"/> seeds (0, 1, 2 ...) with <paramref name="balance"/> and sums them up.</summary>
    public static IReadOnlyList<FigureSummary> Run(IReadOnlyList<RoomTemplate> templates, Balance balance, int seeds)
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(balance);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seeds);

        // Every level is built from its own seed and shares nothing with the others, so they can be built side by side
        // on all processor cores; AsOrdered keeps the seed order, so the report stays the same.
        var levelZero = new LevelZero(templates);
        var levels = Enumerable.Range(0, seeds).AsParallel().AsOrdered()
            .Select(seed => Measure(levelZero.Build(seed, balance), balance))
            .ToList();
        return Summarize(levels);
    }

    /// <summary>All figures of one built level (enemies and chests already placed).</summary>
    public static IReadOnlyList<Figure> Measure(GeneratedLevel level, Balance balance)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(balance);

        var rooms = level.Rooms;
        int Count(RoomKind kind) => rooms.Count(room => room.Kind == kind);
        var ghouls = level.Level.GhoulPositions.Count;
        var bats = level.Level.BatPositions.Count;
        var enemies = ghouls + bats;
        var combatRooms = rooms.Where(room => room.Kind == RoomKind.Combat).ToList();
        var enemyCells = level.Level.GhoulPositions.Concat(level.Level.BatPositions)
            .Select(position => Level.ToGrid(position.X, position.Z))
            .ToList();
        var chests = level.Chests ?? [];
        var arenas = Count(RoomKind.Combat) + Count(RoomKind.Boss);
        var floorCells = rooms.Sum(room => room.FloorCells.Count);

        var experience = (ghouls * balance.Experience.Ghoul) + (bats * balance.Experience.Bat);
        var enemyHealth = (ghouls * balance.Ghoul.MaxHealth) + (bats * balance.Bat.MaxHealth);

        static double Average(int min, int max) => (min + max) / 2.0;
        var silverFromEnemies = (ghouls * (double)balance.Silver.GhoulChance * Average(balance.Silver.GhoulMin, balance.Silver.GhoulMax))
            + (bats * (double)balance.Silver.BatChance * Average(balance.Silver.BatMin, balance.Silver.BatMax));
        var silverInChests = chests.Sum(chest => chest.Silver);

        var keys = (arenas * (double)balance.Keys.RoomChance) + (enemies * (double)balance.Keys.EnemyChance);
        var lockedDoors = level.Doors.Count(door => door.Kind == DoorKind.Locked);
        var lockedChests = chests.Count(chest => chest.IsLocked);

        var drops = (ghouls * (double)balance.Drops.GhoulChance) + (bats * (double)balance.Drops.BatChance);
        var vialsFromEnemies = drops * (1 - (double)balance.Drops.ConsecratedAmmoShare);
        var vialsInChests = chests.Count(chest => chest.HasBloodVial);
        var healing = (vialsFromEnemies + vialsInChests) * balance.BloodVial.Heal;

        // Rough: every floor cell crossed once at top speed, and every enemy cut down with the blade alone.
        var walkingSeconds = floorCells * (double)Level.CellSize / balance.Player.MaxSpeed;
        var fightingSeconds = enemyHealth / (balance.Blade.Damage / (double)balance.Blade.Cooldown);

        List<Figure> figures =
        [
            new(Rooms, "Combat rooms", Count(RoomKind.Combat)),
            new(Rooms, "Corridors", Count(RoomKind.Corridor)),
            new(Rooms, "Treasure rooms", Count(RoomKind.Treasure)),
            new(Rooms, "Secret rooms", Count(RoomKind.Secret)),
            new(Rooms, "Loops", level.Doors.Count - (rooms.Count - 1)),
            new(Rooms, "Boss distance (rooms)", rooms.Single(room => room.Kind == RoomKind.Boss).DistanceFromStart),
            new(Rooms, "Floor cells", floorCells),

            new(Enemies, "Ghouls", ghouls),
            new(Enemies, "Bats", bats),
            new(Enemies, "Enemies", enemies),
            new(Enemies, "Budget per combat room", combatRooms.Count == 0 ? 0 : combatRooms.Average(room => EnemyPlacement.BudgetOf(room, rooms, balance.Difficulty))),
            new(Enemies, "Enemies per combat room", combatRooms.Count == 0 ? 0 : combatRooms.Average(room => enemyCells.Count(room.FloorCells.Contains))),
            new(Enemies, "Enemy health", enemyHealth),
            new(Enemies, "Experience", experience),
            new(Enemies, "Player level reached", LevelReached(experience, balance.Experience)),

            new(Silver, "From enemies (expected)", silverFromEnemies),
            new(Silver, "In chests", silverInChests),
            new(Silver, "Total", silverFromEnemies + silverInChests),

            new(Keys, "Keys (expected)", keys),
            new(Keys, "Locked doors", lockedDoors),
            new(Keys, "Locked chests", lockedChests),
            new(Keys, "Locks", lockedDoors + lockedChests),
            new(Keys, "Keys per lock", lockedDoors + lockedChests == 0 ? 0 : keys / (lockedDoors + lockedChests)),
        ];

        figures.Add(new(Chests, "Chests", chests.Count));
        foreach (var tier in Enum.GetValues<ChestTier>())
        {
            figures.Add(new(Chests, tier.ChestName(), chests.Count(chest => chest.Tier == tier)));
        }

        figures.AddRange(
        [
            new(Healing, "Blood vials from enemies (expected)", vialsFromEnemies),
            new(Healing, "Blood vials in chests", vialsInChests),
            new(Healing, "Health healed (expected)", healing),
            new(Healing, "Healed / max health", healing / balance.Player.MaxHealth),

            new(Time, "Walking (min)", walkingSeconds / 60),
            new(Time, "Fighting with the blade (min)", fightingSeconds / 60),
            new(Time, "Total (min)", (walkingSeconds + fightingSeconds) / 60),
        ]);
        return figures;
    }

    /// <summary>Average, smallest and largest value of every figure over all <paramref name="levels"/>, in the order they were measured.</summary>
    public static IReadOnlyList<FigureSummary> Summarize(IEnumerable<IReadOnlyList<Figure>> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        return levels
            .SelectMany(figures => figures)
            .GroupBy(figure => (figure.Group, figure.Name))
            .Select(group => new FigureSummary(
                group.Key.Group,
                group.Key.Name,
                group.Average(figure => figure.Value),
                group.Min(figure => figure.Value),
                group.Max(figure => figure.Value)))
            .ToList();
    }

    /// <summary>The player level <paramref name="experience"/> leads to - as <see cref="Player.ExperienceToNextLevel"/> counts it, without boons.</summary>
    public static int LevelReached(int experience, ExperienceBalance balance)
    {
        ArgumentNullException.ThrowIfNull(balance);
        var level = 1;
        var left = experience;
        while (true)
        {
            var needed = (int)MathF.Round(balance.FirstLevel * MathF.Pow(balance.Growth, level - 1), MidpointRounding.AwayFromZero);
            if (needed <= 0 || left < needed)
            {
                return level;
            }

            left -= needed;
            level++;
        }
    }
}
