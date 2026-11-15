using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

public class TreasureRoomTests
{
    private const int ManySeeds = 100;

    private static readonly IReadOnlyList<RoomTemplate> Rooms = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    private static readonly LayoutBalance Layout = Balance.Default.Layout;

    // As in the game: level, then enemies, then chests.
    private static readonly Lazy<GeneratedLevel[]> ManyLevels = new(() => Enumerable.Range(0, ManySeeds)
        .Select(seed => ChestPlacement.Place(
            EnemyPlacement.Populate(LevelGenerator.Generate(Rooms, seed, Layout), Balance.Default.Difficulty), Balance.Default.Chests))
        .ToArray());

    private static List<PlacedRoom> Treasures(GeneratedLevel level) => level.Rooms.Where(room => room.Kind == RoomKind.Treasure).ToList();

    private static List<LevelDoor> DoorsOf(GeneratedLevel level, PlacedRoom room) =>
        level.Doors.Where(door => door.RoomA == room.Index || door.RoomB == room.Index).ToList();

    private static bool IsLocked(GeneratedLevel level, PlacedRoom room) => DoorsOf(level, room).Any(door => door.Kind == DoorKind.Locked);

    [Fact]
    public void There_are_enough_treasure_templates_and_each_has_one_entrance_and_chest_places()
    {
        var treasures = Rooms.Where(room => room.Kind == RoomKind.Treasure).ToList();

        Assert.True(treasures.Count >= 3, $"Only {treasures.Count} treasure templates.");
        Assert.All(treasures, room =>
        {
            Assert.Single(room.DoorSlots);
            Assert.InRange(room.ChestSlots.Count, 1, 2);
            Assert.Empty(room.GhoulSlots);
            Assert.Empty(room.BatSlots);
        });
    }

    [Fact]
    public void Every_level_has_one_ordinary_and_one_locked_treasure_room_each_with_a_single_door()
    {
        Assert.All(ManyLevels.Value, level =>
        {
            var treasures = Treasures(level);

            Assert.Equal(2, treasures.Count);
            Assert.All(treasures, room => Assert.Single(DoorsOf(level, room)));
            Assert.Single(treasures, room => IsLocked(level, room));
        });
    }

    [Fact]
    public void The_locked_door_is_the_only_lock_and_leads_into_the_treasure_room()
    {
        Assert.All(ManyLevels.Value, level =>
        {
            var locked = Assert.Single(level.Doors, door => door.Kind == DoorKind.Locked);

            Assert.Equal(RoomKind.Treasure, level.Rooms[locked.RoomB].Kind);
        });
    }

    [Fact]
    public void The_boss_is_reached_without_a_key_and_only_the_locked_treasure_room_needs_one()
    {
        Assert.All(ManyLevels.Value, level =>
        {
            // Walk from the start through every door except locked ones.
            var reached = new HashSet<int> { 0 };
            var open = new Queue<int>([0]);
            while (open.TryDequeue(out var room))
            {
                foreach (var door in level.Doors.Where(door => door.Kind != DoorKind.Locked && (door.RoomA == room || door.RoomB == room)))
                {
                    var other = door.RoomA == room ? door.RoomB : door.RoomA;
                    if (reached.Add(other))
                    {
                        open.Enqueue(other);
                    }
                }
            }

            var behindLock = level.Rooms.Where(room => !reached.Contains(room.Index)).ToList();
            var lockedTreasure = Assert.Single(behindLock);
            Assert.Equal(RoomKind.Treasure, lockedTreasure.Kind);
        });
    }

    [Fact]
    public void Treasure_rooms_never_lie_at_the_start_room()
    {
        Assert.All(ManyLevels.Value, level => Assert.All(Treasures(level), room =>
        {
            var door = Assert.Single(DoorsOf(level, room));
            var outside = door.RoomA == room.Index ? door.RoomB : door.RoomA;
            Assert.NotEqual(0, outside);

            // Through a corridor: the corridor's other door is not at the start room either.
            if (level.Rooms[outside].Kind == RoomKind.Corridor)
            {
                Assert.All(DoorsOf(level, level.Rooms[outside]), corridorDoor =>
                    Assert.False(corridorDoor.RoomA == 0 || corridorDoor.RoomB == 0, $"Seed {level.Seed}: treasure room behind the start room."));
            }
        }));
    }

    [Fact]
    public void Treasure_rooms_hold_no_enemies()
    {
        Assert.All(ManyLevels.Value, level => Assert.All(Treasures(level), room =>
        {
            var floor = room.FloorCells.ToHashSet();
            var enemies = level.Level.GhoulPositions.Concat(level.Level.BatPositions)
                .Select(position => ((int)(position.X / Level.CellSize), (int)(position.Z / Level.CellSize)));
            Assert.DoesNotContain(enemies, floor.Contains);
        }));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 0)]
    [InlineData(0, 2)]
    public void The_number_of_treasure_rooms_comes_from_the_layout(int ordinary, int locked)
    {
        var layout = Layout with { TreasureRooms = ordinary, LockedTreasureRooms = locked };

        foreach (var seed in Enumerable.Range(0, 20))
        {
            var level = LevelGenerator.Generate(Rooms, seed, layout);
            var treasures = Treasures(level);
            Assert.Equal(ordinary + locked, treasures.Count);
            Assert.Equal(locked, treasures.Count(room => IsLocked(level, room)));
        }
    }

    [Fact]
    public void Without_treasure_templates_a_level_is_built_without_treasure_rooms()
    {
        var level = LevelGenerator.Generate(Rooms.Where(room => room.Kind != RoomKind.Treasure).ToList(), 5, Layout);

        Assert.Empty(Treasures(level));
        Assert.DoesNotContain(level.Doors, door => door.Kind == DoorKind.Locked);
    }

    // ---- Chests in treasure rooms -----------------------------------------------------------------------------------

    [Fact]
    public void Treasure_rooms_hold_one_or_two_chests_on_their_chest_places_standing_from_the_start()
    {
        var counts = new HashSet<int>();
        Assert.All(ManyLevels.Value, level => Assert.All(Treasures(level), room =>
        {
            var chests = level.Chests!.Where(chest => chest.Room == room.Index).ToList();
            counts.Add(chests.Count);
            Assert.InRange(chests.Count, 1, 2);
            Assert.All(chests, chest =>
            {
                Assert.Contains((chest.X, chest.Z), room.ChestSlots!);
                Assert.False(chest.RisesWhenCleared);
            });
            Assert.Equal(chests.Count, chests.Select(chest => (chest.X, chest.Z)).Distinct().Count());
        }));

        Assert.Equal([1, 2], counts.Order());
    }

    [Fact]
    public void Each_kind_of_treasure_room_uses_its_own_shares()
    {
        // Ordinary treasure rooms: only King's; locked ones: only Vampire's.
        var standard = Balance.Default.Chests;
        static ChestTierBalance Shares(ChestTierBalance values, float ordinary, float locked) =>
            values with { TreasureShare = ordinary, LockedTreasureShare = locked };
        var chests = standard with
        {
            Beggar = Shares(standard.Beggar, 0f, 0f),
            Guard = Shares(standard.Guard, 0f, 0f),
            King = Shares(standard.King, 1f, 0f),
            Vampire = Shares(standard.Vampire, 0f, 1f),
        };

        Assert.All(ManyLevels.Value.Take(30), generated =>
        {
            var level = ChestPlacement.Place(generated with { Chests = null }, chests);
            Assert.All(Treasures(level), room => Assert.All(
                level.Chests!.Where(chest => chest.Room == room.Index),
                chest => Assert.Equal(IsLocked(level, room) ? ChestTier.Vampire : ChestTier.King, chest.Tier)));
        });
    }

    [Fact]
    public void Chests_behind_the_lock_are_better_by_standard()
    {
        // Ordinary: King's or Vampire's in 35 %; locked: in 55 %.
        var ordinary = new List<ChestPlan>();
        var locked = new List<ChestPlan>();
        foreach (var level in ManyLevels.Value)
        {
            foreach (var room in Treasures(level))
            {
                (IsLocked(level, room) ? locked : ordinary).AddRange(level.Chests!.Where(chest => chest.Room == room.Index));
            }
        }

        static float HighShare(List<ChestPlan> chests) => chests.Count(chest => chest.Tier >= ChestTier.King) / (float)chests.Count;
        Assert.InRange(HighShare(ordinary), 0.25f, 0.45f);
        Assert.InRange(HighShare(locked), 0.45f, 0.65f);
    }

    [Fact]
    public void Treasure_chests_do_not_change_the_chests_of_combat_rooms()
    {
        var level = ManyLevels.Value[4] with { Chests = null };
        var noTreasureChests = Balance.Default.Chests with { TreasureChestsMin = 0, TreasureChestsMax = 0 };

        var with = ChestPlacement.Place(level, Balance.Default.Chests).Chests!.Where(chest => level.Rooms[chest.Room].Kind == RoomKind.Combat);
        var without = ChestPlacement.Place(level, noTreasureChests).Chests!;

        Assert.Equal(without, with);
    }
}
