using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

/// <summary>
/// Which doors get a portcullis (issue #558, provisional decision of the lead): the normal and locked doors of combat
/// rooms, none of the boss room, no cracked wall - from the level alone, nothing rolled.
/// </summary>
public class GratePlacementTests
{
    private static readonly IReadOnlyList<RoomTemplate> Templates = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    // As the game builds its levels (LevelZero.Build). Built once: generating is the slow part.
    private static readonly Lazy<GeneratedLevel[]> Levels = new(() => Enumerable.Range(0, 60)
        .Select(seed => new LevelZero(Templates).Build(seed, Balance.Default))
        .ToArray());

    private static RoomKind[] KindsAround(GeneratedLevel level, LevelDoor door) => [level.Rooms[door.RoomA].Kind, level.Rooms[door.RoomB].Kind];

    [Fact]
    public void Every_built_level_has_its_grates_at_the_doors_of_its_combat_rooms_and_only_there()
    {
        // Seen from the rooms, not from the rule: every combat room has a grate in each of its doors except cracked
        // walls and the boss door, and each grate hangs in a door of some combat room.
        foreach (var level in Levels.Value)
        {
            var combatRooms = level.Rooms.Where(room => room.Kind == RoomKind.Combat).Select(room => room.Index).ToHashSet();
            Assert.NotEmpty(combatRooms);
            foreach (var room in combatRooms)
            {
                var doors = level.Doors.Where(door => door.RoomA == room || door.RoomB == room).ToList();
                Assert.NotEmpty(doors);
                Assert.All(doors.Where(door => door.Kind is not (DoorKind.Cracked or DoorKind.Boss)), door => Assert.True(door.HasGrate, $"room {room}: door at {door.X}/{door.Z} has no grate"));
            }

            Assert.All(level.Doors.Where(door => door.HasGrate), door =>
                Assert.True(combatRooms.Contains(door.RoomA) || combatRooms.Contains(door.RoomB), $"grate at {door.X}/{door.Z} is not at a combat room"));
        }
    }

    [Fact]
    public void Every_door_of_the_boss_room_is_a_boss_door_which_never_gets_a_grate()
    {
        // So no extra rule for the boss room is needed: its doors are of the kind that gets no grate.
        foreach (var level in Levels.Value)
        {
            var boss = level.Rooms.Single(room => room.Kind == RoomKind.Boss).Index;
            var doors = level.Doors.Where(door => door.RoomA == boss || door.RoomB == boss).ToList();
            Assert.NotEmpty(doors);
            Assert.All(doors, door => Assert.Equal(DoorKind.Boss, door.Kind));
        }

        var rooms = Levels.Value[0].Rooms;
        var combat = rooms.First(room => room.Kind == RoomKind.Combat).Index;
        Assert.False(GratePlacement.GetsGrate(new LevelDoor(0, 0, combat, combat, RoomSide.South, DoorKind.Boss), rooms));
        Assert.True(GratePlacement.GetsGrate(new LevelDoor(0, 0, combat, combat, RoomSide.South, DoorKind.Locked), rooms));
    }

    [Fact]
    public void No_door_of_the_boss_room_and_no_cracked_wall_gets_a_grate()
    {
        var doors = Levels.Value.SelectMany(level => level.Doors.Select(door => (level, door))).ToList();
        Assert.Contains(doors, pair => KindsAround(pair.level, pair.door).Contains(RoomKind.Boss));
        Assert.DoesNotContain(doors, pair => pair.door.HasGrate && KindsAround(pair.level, pair.door).Contains(RoomKind.Boss));
        Assert.DoesNotContain(doors, pair => pair.door.HasGrate && pair.door.Kind == DoorKind.Cracked);
    }

    [Fact]
    public void The_same_seed_gets_the_same_grates_and_placing_twice_changes_nothing()
    {
        var level = Levels.Value[7];
        var again = new LevelZero(Templates).Build(7, Balance.Default);
        Assert.Equal(level.Doors.Select(door => door.HasGrate), again.Doors.Select(door => door.HasGrate));
        Assert.Equal(level.Doors.Select(door => door.HasGrate), GratePlacement.Place(level).Doors.Select(door => door.HasGrate));
    }

    [Fact]
    public void Placing_keeps_everything_but_the_grates()
    {
        var level = Levels.Value[3];
        var bare = level with { Doors = level.Doors.Select(door => door with { HasGrate = false }).ToList() };

        var placed = GratePlacement.Place(bare);

        Assert.Same(bare.Rooms, placed.Rooms);
        Assert.Same(bare.Level, placed.Level);
        Assert.Equal(level.Doors, placed.Doors);
    }

    [Fact]
    public void Placement_refuses_a_missing_level_door_or_room_list()
    {
        var level = Levels.Value[0];
        Assert.Throws<ArgumentNullException>(() => GratePlacement.Place(null!));
        Assert.Throws<ArgumentNullException>(() => GratePlacement.GetsGrate(null!, level.Rooms));
        Assert.Throws<ArgumentNullException>(() => GratePlacement.GetsGrate(level.Doors[0], null!));
    }
}