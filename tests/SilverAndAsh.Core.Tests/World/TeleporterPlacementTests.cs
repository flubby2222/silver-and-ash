using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

public class TeleporterPlacementTests
{
    private static readonly IReadOnlyList<RoomTemplate> Templates = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    private static readonly LevelZero Catacombs = new(Templates);

    // 100 levels with fixed seeds, built exactly as a run builds them. Built once: generating is the slow part.
    private static readonly Lazy<GeneratedLevel[]> Levels = new(() => Enumerable.Range(0, 100)
        .Select(seed => Catacombs.Build(seed, Balance.Default))
        .ToArray());

    private static (PlacedRoom Room, LevelDoor Door) BossGate(GeneratedLevel level)
    {
        var door = level.Doors.Single(door => level.Rooms[door.RoomA].Kind == RoomKind.Boss || level.Rooms[door.RoomB].Kind == RoomKind.Boss);
        var outside = level.Rooms[door.RoomA].Kind == RoomKind.Boss ? door.RoomB : door.RoomA;
        return (level.Rooms[outside], door);
    }

    private static int Steps((int X, int Z) a, (int X, int Z) b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Z - b.Z));

    [Fact]
    public void Every_level_has_one_teleporter_in_the_start_room_and_one_in_front_of_the_boss_door()
    {
        Assert.All(Levels.Value, level =>
        {
            var teleporters = level.Teleporters!;
            var start = Assert.Single(teleporters, teleporter => teleporter.Site == TeleporterSite.Start);
            Assert.Equal(RoomKind.Start, level.Rooms[start.Room].Kind);

            var (room, _) = BossGate(level);
            var gate = Assert.Single(teleporters, teleporter => teleporter.Site == TeleporterSite.BossGate);
            Assert.Equal(room.Index, gate.Room);
            Assert.Equal(2, teleporters.Count);
        });
    }

    [Fact]
    public void Every_teleporter_stands_on_a_floor_cell_of_its_room_never_in_a_wall_and_can_be_reached_from_the_start()
    {
        Assert.All(Levels.Value, level =>
        {
            var start = (level.Level.PlayerStartX, level.Level.PlayerStartZ);
            Assert.All(level.Teleporters!, teleporter =>
            {
                Assert.Contains((teleporter.X, teleporter.Z), level.Rooms[teleporter.Room].FloorCells);
                Assert.False(level.Level[teleporter.X, teleporter.Z].IsSolid);

                // All doors are open in the generated level: this is the way a player can walk (opening them on the way).
                Assert.NotNull(Pathfinder.FindPath(level.Level, start, (teleporter.X, teleporter.Z)));
            });
        });
    }

    [Fact]
    public void A_teleporter_keeps_two_cells_from_doors_and_the_start_and_never_stands_on_a_chest_place()
    {
        Assert.All(Levels.Value, level =>
        {
            var start = (level.Level.PlayerStartX, level.Level.PlayerStartZ);
            Assert.All(level.Teleporters!, teleporter =>
            {
                var cell = (teleporter.X, teleporter.Z);
                var room = level.Rooms[teleporter.Room];
                Assert.DoesNotContain(cell, room.ChestSlots ?? []);
                Assert.True(Steps(cell, start) >= 2, $"seed {level.Seed}: teleporter at {cell} next to the start {start}");
                Assert.All(
                    level.Doors.Where(door => door.RoomA == room.Index || door.RoomB == room.Index),
                    door => Assert.True(Steps(cell, (door.X, door.Z)) >= 2, $"seed {level.Seed}: teleporter at {cell} next to door ({door.X}, {door.Z})"));
            });
        });
    }

    [Fact]
    public void The_boss_gate_teleporter_is_the_allowed_cell_nearest_to_the_boss_door()
    {
        Assert.All(Levels.Value, level =>
        {
            var (room, door) = BossGate(level);
            var gate = level.Teleporters!.Single(teleporter => teleporter.Site == TeleporterSite.BossGate);
            var distance = Distance((gate.X, gate.Z), (door.X, door.Z));
            var start = (level.Level.PlayerStartX, level.Level.PlayerStartZ);
            var doors = level.Doors.Where(d => d.RoomA == room.Index || d.RoomB == room.Index).Select(d => (d.X, d.Z)).Append(start).ToList();
            var nearer = room.FloorCells.Where(cell =>
                Distance(cell, (door.X, door.Z)) < distance
                && !(room.ChestSlots ?? []).Contains(cell)
                && doors.All(other => Steps(cell, other) >= 2));
            Assert.Empty(nearer);
        });

        static int Distance((int X, int Z) a, (int X, int Z) b) => ((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z));
    }

    [Fact]
    public void Placing_teleporters_changes_nothing_else_of_the_level()
    {
        // No random number is used: the rooms, enemies and chests of a seed stay exactly as they were.
        foreach (var seed in new[] { 0, 7, 42 })
        {
            var balance = Balance.Default;
            var without = ChestPlacement.Place(
                EnemyPlacement.Populate(LevelGenerator.Generate(Templates, seed, balance.Layout), balance.Difficulty), balance.Chests);

            var with = TeleporterPlacement.Place(without);

            Assert.Null(without.Teleporters);
            Assert.Same(without.Level, with.Level);
            Assert.Same(without.Rooms, with.Rooms);
            Assert.Same(without.Doors, with.Doors);
            Assert.Same(without.Chests, with.Chests);
            Assert.Equal(Catacombs.Build(seed, balance).Teleporters, with.Teleporters);
        }
    }

    [Fact]
    public void The_same_seed_gives_the_same_teleporters()
    {
        Assert.Equal(Catacombs.Build(5, Balance.Default).Teleporters, Catacombs.Build(5, Balance.Default).Teleporters);
    }

    [Fact]
    public void A_run_starts_with_the_levels_teleporters_all_locked_and_a_restart_keeps_them()
    {
        var world = Catacombs.NewRun(3, Balance.Default);
        var plans = Catacombs.Build(3, Balance.Default).Teleporters!;

        Assert.Equal(plans, world.Teleporters.Select(teleporter => teleporter.Plan));
        Assert.All(world.Teleporters, teleporter => Assert.False(teleporter.IsUnlocked));
        Assert.Equal(plans, world.Restarted().Teleporters.Select(teleporter => teleporter.Plan));
    }

    [Fact]
    public void A_level_without_a_boss_room_gets_only_the_start_teleporter()
    {
        var level = Levels.Value[0];
        var withoutBoss = level with
        {
            Rooms = level.Rooms.Select(room => room.Kind == RoomKind.Boss ? room with { Kind = RoomKind.Combat } : room).ToList(),
        };

        var placed = TeleporterPlacement.Place(withoutBoss);

        Assert.Equal(TeleporterSite.Start, Assert.Single(placed.Teleporters!).Site);
    }
}
