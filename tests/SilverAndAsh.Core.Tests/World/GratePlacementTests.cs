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
        foreach (var level in Levels.Value)
        {
            Assert.Contains(level.Doors, door => door.HasGrate);
            foreach (var door in level.Doors)
            {
                var kinds = KindsAround(level, door);
                var expected = door.Kind is DoorKind.Normal or DoorKind.Locked && kinds.Contains(RoomKind.Combat) && !kinds.Contains(RoomKind.Boss);
                Assert.Equal(expected, door.HasGrate);
            }
        }
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