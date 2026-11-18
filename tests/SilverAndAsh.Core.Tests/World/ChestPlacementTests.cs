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

    [Fact]
    public void Every_chest_looks_with_its_front_towards_the_middle_of_its_room()
    {
        var all = Levels.Value.Select(level => ChestPlacement.Place(level, InEveryRoom))
            .SelectMany(level => level.Chests!.Select(chest => (level, chest)))
            .ToList();

        Assert.NotEmpty(all);
        foreach (var (level, chest) in all)
        {
            var room = level.Rooms[chest.Room];
            var middle = room.Z + (room.Depth / 2f);
            var centre = chest.Z + 0.5f;

            // On the middle row either way would do; it looks towards +Z there.
            var expected = centre <= middle ? ChestFront.PlusZ : ChestFront.MinusZ;
            Assert.Equal(expected, chest.Front);
        }

        // Both ways come up: the front is not simply always the same.
        Assert.Contains(all, pair => pair.chest.Front == ChestFront.PlusZ);
        Assert.Contains(all, pair => pair.chest.Front == ChestFront.MinusZ);
    }

    [Fact]
    public void The_same_seed_gives_the_same_fronts()
    {
        var level = Levels.Value[11];

        var first = ChestPlacement.Place(level, InEveryRoom).Chests!.Select(chest => chest.Front);
        var again = ChestPlacement.Place(level, InEveryRoom).Chests!.Select(chest => chest.Front);

        Assert.Equal(first, again);
    }

    // Hand-made cases (issue #324): generated levels never have them, but Place must handle them.

    /// <summary>A generated level with a treasure room and a secret room, as a base for the hand-made cases.</summary>
    private static GeneratedLevel WithTreasureAndSecret() => Levels.Value.First(level =>
        level.Rooms.Any(room => room.Kind == RoomKind.Treasure) && level.Rooms.Any(room => room.Kind == RoomKind.Secret));

    /// <summary><paramref name="level"/> with each room that <paramref name="change"/> touches replaced.</summary>
    private static GeneratedLevel Changed(GeneratedLevel level, Func<PlacedRoom, PlacedRoom> change) =>
        level with { Rooms = level.Rooms.Select(change).ToList() };

    private static List<ChestPlan> ChestsIn(GeneratedLevel level, RoomKind kind) =>
        level.Chests!.Where(chest => level.Rooms[chest.Room].Kind == kind).ToList();

    [Fact]
    public void A_level_without_combat_rooms_still_gets_its_treasure_and_secret_chests()
    {
        var level = Changed(WithTreasureAndSecret(), room => room.Kind == RoomKind.Combat ? room with { Kind = RoomKind.Corridor } : room);

        var placed = ChestPlacement.Place(level, InEveryRoom);

        Assert.DoesNotContain(placed.Chests!, chest => chest.RisesWhenCleared);
        Assert.All(placed.Rooms.Where(room => room.Kind == RoomKind.Treasure), room => Assert.InRange(
            placed.Chests!.Count(chest => chest.Room == room.Index),
            Math.Min(Standard.TreasureChestsMin, room.ChestSlots!.Count),
            Math.Min(Standard.TreasureChestsMax, room.ChestSlots!.Count)));
        Assert.Single(ChestsIn(placed, RoomKind.Secret));
    }

    [Fact]
    public void A_combat_room_without_chest_places_gets_no_chest_and_rolls_like_one_with_none()
    {
        var level = WithTreasureAndSecret();
        var target = level.Rooms.First(room => room.Kind == RoomKind.Combat).Index;

        var withoutList = ChestPlacement.Place(Changed(level, room => room.Index == target ? room with { ChestSlots = null } : room), InEveryRoom);
        var emptyList = ChestPlacement.Place(Changed(level, room => room.Index == target ? room with { ChestSlots = [] } : room), InEveryRoom);

        Assert.DoesNotContain(withoutList.Chests!, chest => chest.Room == target);
        Assert.NotEmpty(ChestsIn(withoutList, RoomKind.Combat));
        Assert.Equal(emptyList.Chests!, withoutList.Chests!);
    }

    [Fact]
    public void With_a_single_combat_room_its_chest_follows_the_near_shares()
    {
        // Near: only Beggar's; far: only King's. The only combat room is both the nearest and the farthest - it counts as near.
        var chests = InEveryRoom with
        {
            Beggar = Standard.Beggar with { CombatShareNear = 1f, CombatShareFar = 0f },
            Guard = Standard.Guard with { CombatShareNear = 0f, CombatShareFar = 0f },
            King = Standard.King with { CombatShareNear = 0f, CombatShareFar = 1f },
            Vampire = Standard.Vampire with { CombatShareNear = 0f, CombatShareFar = 0f },
        };

        foreach (var generated in Levels.Value.Take(20))
        {
            var only = generated.Rooms.Where(room => room.Kind == RoomKind.Combat).MaxBy(room => room.DistanceFromStart)!.Index;
            var level = Changed(generated, room => room.Kind == RoomKind.Combat && room.Index != only ? room with { Kind = RoomKind.Corridor } : room);

            var chest = Assert.Single(ChestsIn(ChestPlacement.Place(level, chests), RoomKind.Combat));

            Assert.Equal(only, chest.Room);
            Assert.Equal(ChestTier.Beggar, chest.Tier);
        }
    }

    [Fact]
    public void A_treasure_room_on_the_inner_side_of_a_locked_door_counts_as_locked()
    {
        // Ordinary treasure rooms: only King's; locked ones: only Vampire's. The door is turned round: the treasure room is room A.
        static ChestTierBalance Shares(ChestTierBalance values, float ordinary, float locked) =>
            values with { TreasureShare = ordinary, LockedTreasureShare = locked };
        var chests = Standard with
        {
            Beggar = Shares(Standard.Beggar, 0f, 0f),
            Guard = Shares(Standard.Guard, 0f, 0f),
            King = Shares(Standard.King, 1f, 0f),
            Vampire = Shares(Standard.Vampire, 0f, 1f),
        };
        var generated = WithTreasureAndSecret();
        var treasure = generated.Rooms.First(room => room.Kind == RoomKind.Treasure).Index;
        var level = generated with
        {
            Doors = generated.Doors.Select(door => door.RoomA == treasure || door.RoomB == treasure
                ? door with { RoomA = treasure, RoomB = door.RoomA == treasure ? door.RoomB : door.RoomA, Kind = DoorKind.Locked }
                : door).ToList(),
        };

        var inTreasure = ChestPlacement.Place(level, chests).Chests!.Where(chest => chest.Room == treasure).ToList();

        Assert.NotEmpty(inTreasure);
        Assert.All(inTreasure, chest => Assert.Equal(ChestTier.Vampire, chest.Tier));
    }

    [Fact]
    public void A_treasure_room_without_chest_places_gets_no_chest_and_rolls_like_one_with_none()
    {
        var level = WithTreasureAndSecret();
        var target = level.Rooms.First(room => room.Kind == RoomKind.Treasure).Index;

        var withoutList = ChestPlacement.Place(Changed(level, room => room.Index == target ? room with { ChestSlots = null } : room), Standard);
        var emptyList = ChestPlacement.Place(Changed(level, room => room.Index == target ? room with { ChestSlots = [] } : room), Standard);

        Assert.DoesNotContain(withoutList.Chests!, chest => chest.Room == target);
        Assert.Single(ChestsIn(withoutList, RoomKind.Secret));
        Assert.Equal(emptyList.Chests!, withoutList.Chests!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_secret_room_without_chest_places_gets_no_chest_and_the_others_stay(bool emptyList)
    {
        var level = WithTreasureAndSecret();
        var secret = level.Rooms.Single(room => room.Kind == RoomKind.Secret).Index;
        var withoutPlaces = Changed(level, room => room.Index == secret ? room with { ChestSlots = emptyList ? [] : null } : room);

        var before = ChestPlacement.Place(level, Standard).Chests!;
        var after = ChestPlacement.Place(withoutPlaces, Standard).Chests!;

        Assert.DoesNotContain(after, chest => chest.Room == secret);
        Assert.Equal(before.Where(chest => chest.Room != secret), after);
    }
}
