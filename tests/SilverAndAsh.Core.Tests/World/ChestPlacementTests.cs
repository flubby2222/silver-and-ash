using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

public class ChestPlacementTests
{
    private static readonly IReadOnlyList<RoomTemplate> Templates = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    // Built once: generating levels is the slow part. Enemies are placed first, as in the game.
    private static readonly Lazy<GeneratedLevel[]> Levels = new(() => Enumerable.Range(0, 150)
        .Select(seed => EnemyPlacement.Populate(LevelGenerator.Generate(Templates, seed, Balance.Default.Layout), Balance.Default.Difficulty))
        .ToArray());

    private static readonly ChestsBalance Standard = Balance.Default.Chests;

    /// <summary>Every combat room gets a chest - to look at many chests.</summary>
    private static readonly ChestsBalance InEveryRoom = Standard with { CombatRoomChance = 1f };

    /// <summary>The chests of combat rooms over all levels (treasure rooms are looked at further down).</summary>
    private static List<(GeneratedLevel Level, ChestPlan Chest)> AllChests(ChestsBalance chests) =>
        AllChestsIn(chests, RoomKind.Combat);

    private static List<ChestPlan> CombatChests(GeneratedLevel level) =>
        level.Chests!.Where(chest => level.Rooms[chest.Room].Kind == RoomKind.Combat).ToList();

    private static List<(GeneratedLevel Level, ChestPlan Chest)> AllChestsIn(ChestsBalance chests, RoomKind kind) =>
        Levels.Value.Select(level => ChestPlacement.Place(level, chests))
            .SelectMany(level => level.Chests!.Where(chest => level.Rooms[chest.Room].Kind == kind).Select(chest => (level, chest)))
            .ToList();

    /// <summary>Only <paramref name="tier"/> can come up in combat rooms.</summary>
    private static ChestsBalance Only(ChestTier tier)
    {
        static ChestTierBalance Share(ChestTierBalance values, float share) => values with { CombatShareNear = share, CombatShareFar = share };
        return InEveryRoom with
        {
            Beggar = Share(Standard.Beggar, tier == ChestTier.Beggar ? 1f : 0f),
            Guard = Share(Standard.Guard, tier == ChestTier.Guard ? 1f : 0f),
            King = Share(Standard.King, tier == ChestTier.King ? 1f : 0f),
            Vampire = Share(Standard.Vampire, tier == ChestTier.Vampire ? 1f : 0f),
        };
    }

    [Fact]
    public void Every_combat_template_has_a_chest_place()
    {
        Assert.All(Templates.Where(t => t.Kind == RoomKind.Combat), template => Assert.NotEmpty(template.ChestSlots));
    }

    [Fact]
    public void Chests_stand_only_on_chest_places_of_combat_rooms_one_per_room_and_rise_when_cleared()
    {
        foreach (var (level, chest) in AllChests(InEveryRoom))
        {
            var room = level.Rooms[chest.Room];
            Assert.Equal(RoomKind.Combat, room.Kind);
            Assert.Contains((chest.X, chest.Z), room.ChestSlots!);
            Assert.False(level.Level[chest.X, chest.Z].IsSolid);
            Assert.True(chest.RisesWhenCleared);
        }

        Assert.All(Levels.Value.Select(level => ChestPlacement.Place(level, InEveryRoom)), level =>
        {
            var combatChests = CombatChests(level);
            Assert.Equal(level.Rooms.Count(room => room.Kind == RoomKind.Combat), combatChests.Count);
            Assert.Equal(combatChests.Count, combatChests.Select(chest => chest.Room).Distinct().Count());
        });
    }

    [Fact]
    public void About_every_fourth_combat_room_gets_a_chest_by_standard()
    {
        var combatRooms = Levels.Value.Sum(level => level.Rooms.Count(room => room.Kind == RoomKind.Combat));
        var chests = AllChests(Standard).Count;

        Assert.InRange(chests / (float)combatRooms, 0.2f, 0.3f);
    }

    [Fact]
    public void Without_a_chance_no_combat_room_gets_a_chest()
    {
        Assert.Empty(AllChests(Standard with { CombatRoomChance = 0f }));
    }

    [Fact]
    public void Beggars_chests_are_most_common_and_vampires_chests_never_stand_in_combat_rooms()
    {
        var tiers = AllChests(InEveryRoom).GroupBy(pair => pair.Chest.Tier).ToDictionary(group => group.Key, group => group.Count());
        var total = tiers.Values.Sum();

        // Standard shares: Beggar's 70 % near to 55 % far, Guard's 27 % to 37 %, King's 3 % to 8 %, Vampire's 0 %.
        Assert.InRange(tiers.GetValueOrDefault(ChestTier.Beggar) / (float)total, 0.55f, 0.7f);
        Assert.InRange(tiers.GetValueOrDefault(ChestTier.Guard) / (float)total, 0.25f, 0.4f);
        Assert.InRange(tiers.GetValueOrDefault(ChestTier.King) / (float)total, 0.02f, 0.1f);
        Assert.False(tiers.ContainsKey(ChestTier.Vampire));
    }

    [Fact]
    public void Chests_get_better_the_farther_the_room_lies_from_the_start()
    {
        // Near: only Beggar's; far: only King's. The nearest combat room gets a Beggar's Chest, the farthest a King's.
        var chests = InEveryRoom with
        {
            Beggar = Standard.Beggar with { CombatShareNear = 1f, CombatShareFar = 0f },
            Guard = Standard.Guard with { CombatShareNear = 0f, CombatShareFar = 0f },
            King = Standard.King with { CombatShareNear = 0f, CombatShareFar = 1f },
        };

        foreach (var level in Levels.Value.Select(level => ChestPlacement.Place(level, chests)))
        {
            var combatChests = CombatChests(level);
            var distances = combatChests.Select(chest => level.Rooms[chest.Room].DistanceFromStart).ToList();
            var nearest = combatChests[distances.IndexOf(distances.Min())];
            var farthest = combatChests[distances.IndexOf(distances.Max())];
            Assert.Equal(ChestTier.Beggar, nearest.Tier);
            Assert.Equal(ChestTier.King, farthest.Tier);
        }
    }

    [Fact]
    public void All_shares_at_zero_give_beggars_chests()
    {
        var none = new ChestTierBalance();
        var chests = InEveryRoom with
        {
            Beggar = Standard.Beggar with { CombatShareNear = 0f, CombatShareFar = 0f },
            Guard = Standard.Guard with { CombatShareNear = 0f, CombatShareFar = 0f },
            King = Standard.King with { CombatShareNear = 0f, CombatShareFar = 0f },
            Vampire = none,
        };

        Assert.All(AllChests(chests), pair => Assert.Equal(ChestTier.Beggar, pair.Chest.Tier));
    }

    [Theory]
    [InlineData(ChestTier.Beggar, 0.1f)]
    [InlineData(ChestTier.Guard, 0.35f)]
    [InlineData(ChestTier.King, 0.7f)]
    [InlineData(ChestTier.Vampire, 1f)]
    public void Each_tier_is_locked_as_often_as_balanced(ChestTier tier, float chance)
    {
        var chests = AllChests(Only(tier)).Select(pair => pair.Chest).ToList();

        Assert.All(chests, chest => Assert.Equal(tier, chest.Tier));
        Assert.InRange(chests.Count(chest => chest.IsLocked) / (float)chests.Count, chance - 0.07f, chance + 0.07f);
    }

    [Theory]
    [InlineData(ChestTier.Beggar, 10, 20)]
    [InlineData(ChestTier.Guard, 20, 35)]
    [InlineData(ChestTier.King, 40, 60)]
    [InlineData(ChestTier.Vampire, 80, 120)]
    public void Each_tier_holds_its_silver_and_maybe_a_blood_vial(ChestTier tier, int min, int max)
    {
        var chests = AllChests(Only(tier)).Select(pair => pair.Chest).ToList();
        var vialChance = Standard.Of(tier).BloodVialChance;

        Assert.All(chests, chest => Assert.InRange(chest.Silver, min, max));
        Assert.Contains(chests, chest => chest.Silver == min);
        Assert.Contains(chests, chest => chest.Silver == max);
        Assert.InRange(chests.Count(chest => chest.HasBloodVial) / (float)chests.Count, vialChance - 0.07f, vialChance + 0.07f);
    }

    [Fact]
    public void The_same_seed_gives_the_same_chests()
    {
        var level = Levels.Value[7];

        Assert.Equal(ChestPlacement.Place(level, InEveryRoom).Chests!, ChestPlacement.Place(level, InEveryRoom).Chests!);
        Assert.NotEqual(ChestPlacement.Place(level, InEveryRoom).Chests!, ChestPlacement.Place(Levels.Value[8], InEveryRoom).Chests!);
    }

    [Fact]
    public void Chests_change_nothing_else_of_the_level()
    {
        var level = Levels.Value[3];

        var placed = ChestPlacement.Place(level, InEveryRoom);

        Assert.Same(level.Level, placed.Level);
        Assert.Same(level.Rooms, placed.Rooms);
        Assert.Same(level.Doors, placed.Doors);
    }
}
