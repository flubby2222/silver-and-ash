using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

/// <summary>
/// The antechamber in front of the boss room (issue #565): one per level, 4 x 5 cells, the entrance in the middle of a long
/// side, the boss door on a short side at the middle cell nearer the entrance - a quiet room without enemies or chests.
/// </summary>
public class AntechamberTests
{
    private const int ManySeeds = 100;

    private static readonly IReadOnlyList<RoomTemplate> Templates = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    // As the game builds its levels (LevelZero.Build). Built once: generating is the slow part.
    private static readonly Lazy<GeneratedLevel[]> Levels = new(() => Enumerable.Range(0, ManySeeds)
        .Select(seed => new LevelZero(Templates).Build(seed, Balance.Default))
        .ToArray());

    private static PlacedRoom TheAntechamber(GeneratedLevel level) => Assert.Single(level.Rooms, room => room.Kind == RoomKind.Antechamber);

    private static List<LevelDoor> DoorsOf(GeneratedLevel level, PlacedRoom room) =>
        level.Doors.Where(door => door.RoomA == room.Index || door.RoomB == room.Index).ToList();

    private static int Other(LevelDoor door, PlacedRoom room) => door.RoomA == room.Index ? door.RoomB : door.RoomA;

    /// <summary>Where a door sits on the floor box of a room: how many floor cells that side has, and the door's place on it (0 = first).</summary>
    private static (int SideLength, int Offset, bool OnWestOrEast) PlaceOnSide(PlacedRoom room, LevelDoor door)
    {
        var minX = room.FloorCells.Min(cell => cell.X);
        var maxX = room.FloorCells.Max(cell => cell.X);
        var minZ = room.FloorCells.Min(cell => cell.Z);
        var maxZ = room.FloorCells.Max(cell => cell.Z);
        if (door.X == minX - 1 || door.X == maxX + 1)
        {
            Assert.InRange(door.Z, minZ, maxZ);
            return (maxZ - minZ + 1, door.Z - minZ, true);
        }

        Assert.True(door.Z == minZ - 1 || door.Z == maxZ + 1, $"door at {door.X}/{door.Z} is not on a side of the room");
        Assert.InRange(door.X, minX, maxX);
        return (maxX - minX + 1, door.X - minX, false);
    }

    [Fact]
    public void Every_level_has_exactly_one_antechamber_of_4_by_5_cells_with_the_high_ceiling()
    {
        Assert.All(Levels.Value, level =>
        {
            Assert.Single(level.Rooms, room => room.Kind == RoomKind.Boss);
            var room = TheAntechamber(level);
            var width = room.FloorCells.Max(cell => cell.X) - room.FloorCells.Min(cell => cell.X) + 1;
            var depth = room.FloorCells.Max(cell => cell.Z) - room.FloorCells.Min(cell => cell.Z) + 1;

            Assert.Equal(Antechamber.Width * Antechamber.Depth, room.FloorCells.Count);
            Assert.Equal(
                [Antechamber.Width, Antechamber.Depth],
                new[] { width, depth }.Order());
            Assert.All(room.FloorCells, cell => Assert.Equal(RoomTemplate.RoomCeilingHeight, level.Level[cell.X, cell.Z].CeilingHeight));
        });
    }

    [Fact]
    public void The_entrance_sits_in_the_middle_of_a_long_side_and_the_boss_door_beside_it_on_a_short_side()
    {
        Assert.All(Levels.Value, level =>
        {
            var room = TheAntechamber(level);
            var doors = DoorsOf(level, room);
            Assert.Equal(2, doors.Count);

            var bossDoor = Assert.Single(doors, door => door.Kind == DoorKind.Boss);
            Assert.Equal(RoomKind.Boss, level.Rooms[Other(bossDoor, room)].Kind);
            var entrance = Assert.Single(doors, door => door.Kind == DoorKind.Normal);
            Assert.NotEqual(RoomKind.Boss, level.Rooms[Other(entrance, room)].Kind);

            var (entranceSide, entranceOffset, entranceOnWestOrEast) = PlaceOnSide(room, entrance);
            Assert.Equal(Antechamber.Depth, entranceSide);
            Assert.Equal(Antechamber.Depth / 2, entranceOffset);

            var (bossSide, bossOffset, bossOnWestOrEast) = PlaceOnSide(room, bossDoor);
            Assert.Equal(Antechamber.Width, bossSide);
            Assert.Contains(bossOffset, new[] { 1, 2 });
            Assert.NotEqual(entranceOnWestOrEast, bossOnWestOrEast);

            // Of the two middle cells, the one nearer the wall with the entrance.
            var along = bossOnWestOrEast ? (Door: bossDoor.Z, Entrance: entrance.Z) : (Door: bossDoor.X, Entrance: entrance.X);
            var otherMiddle = along.Door + (bossOffset == 1 ? 1 : -1);
            Assert.True(
                Math.Abs(along.Door - along.Entrance) < Math.Abs(otherMiddle - along.Entrance),
                $"seed {level.Seed}: the boss door is at the middle cell farther from the entrance");
        });
    }

    [Fact]
    public void The_antechamber_has_no_enemy_no_chest_no_well_and_no_grate_of_its_own()
    {
        Assert.All(Levels.Value, level =>
        {
            var room = TheAntechamber(level);
            var floor = room.FloorCells.ToHashSet();
            var enemies = level.Level.GhoulPositions.Concat(level.Level.BatPositions).Concat(level.Level.BossPositions)
                .Select(position => Level.ToGrid(position.X, position.Z));

            Assert.Empty(room.GhoulSlots);
            Assert.Empty(room.BatSlots);
            Assert.DoesNotContain(enemies, floor.Contains);
            Assert.DoesNotContain(level.Chests ?? [], chest => chest.Room == room.Index);
            Assert.NotEqual(room.Index, level.Well?.Room);

            // A grate only where the room before it is an arena (its rule), never at the boss door.
            Assert.All(DoorsOf(level, room).Where(door => door.HasGrate), door =>
            {
                Assert.NotEqual(DoorKind.Boss, door.Kind);
                Assert.Equal(RoomKind.Combat, level.Rooms[Other(door, room)].Kind);
            });
        });
    }

    [Fact]
    public void The_boss_room_is_still_the_farthest_room_and_lies_right_behind_the_antechamber()
    {
        Assert.All(Levels.Value, level =>
        {
            var boss = Assert.Single(level.Rooms, room => room.Kind == RoomKind.Boss);
            var antechamber = TheAntechamber(level);

            Assert.Equal(antechamber.DistanceFromStart + 1, boss.DistanceFromStart);
            Assert.All(
                level.Rooms.Where(room => room.Kind is RoomKind.Combat or RoomKind.Start),
                room => Assert.True(room.DistanceFromStart < antechamber.DistanceFromStart, $"seed {level.Seed}: room {room.Index} is as far as the antechamber"));
        });
    }

    [Fact]
    public void Standing_in_the_antechamber_enters_it_but_never_seals_it()
    {
        // Real ticks: the player starts in the middle of the antechamber and stands there for a second (issue #580: it
        // does not walk in - a player who starts in a room has entered it as well).
        foreach (var level in Levels.Value.Take(10))
        {
            var room = TheAntechamber(level);
            var middle = room.FloorCells[room.FloorCells.Count / 2];
            var world = new GameWorld(StartingAt(level.Level, middle), level.Seed, Balance.Default, level.Doors, level.Rooms, level.Chests, level.Well, level.Teleporters);

            var events = TestKit.Run(world, PlayerInput.None, SimulationClock.TicksPerSecond);

            var state = world.Rooms[room.Index];
            Assert.True(state.IsEntered, $"seed {level.Seed}: the antechamber was not entered");
            Assert.False(state.IsArena);
            Assert.False(state.IsSealed);
            Assert.DoesNotContain(events, happened => happened is RoomSealed);
            Assert.False(world.Rooms.Single(r => r.Kind == RoomKind.Boss).IsEntered);
        }
    }

    // The same level with the player start moved to cell (X, Z), as EmergenceTests does it.
    private static Level StartingAt(Level level, (int X, int Z) start)
    {
        var cells = new Cell[level.Width, level.Depth];
        for (var x = 0; x < level.Width; x++)
        {
            for (var z = 0; z < level.Depth; z++)
            {
                cells[x, z] = level[x, z];
            }
        }

        static (int X, int Z) ToCell(System.Numerics.Vector3 position) => Level.ToGrid(position.X, position.Z);
        return new Level(
            cells, start.X, start.Z, level.DummyPositions.Select(ToCell), level.GhoulPositions.Select(ToCell),
            level.BatPositions.Select(ToCell), level.BossPositions.Select(ToCell));
    }

    [Fact]
    public void The_antechamber_is_built_in_and_uses_no_template_file()
    {
        // Levels are built from templates without any antechamber - the generator brings its own.
        Assert.DoesNotContain(Templates, template => template.Kind == RoomKind.Antechamber);
        Assert.Equal(RoomKind.Antechamber, Antechamber.Template.Kind);
        Assert.Equal(2, Antechamber.Template.DoorSlots.Count);
    }

    [Fact]
    public void A_folder_for_antechambers_is_refused()
    {
        var directory = Directory.CreateTempSubdirectory("antechamber-folder");
        try
        {
            var folder = Directory.CreateDirectory(Path.Combine(directory.FullName, "antechamber"));
            File.WriteAllText(Path.Combine(folder.FullName, "room.txt"), "######\n#....#\n+....+\n######");

            var error = Assert.Throws<LevelFormatException>(() => RoomTemplateParser.LoadAll(directory.FullName));

            Assert.Contains("Unknown kind of room", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("antechamber", error.Message.Split("Folders may be:")[1], StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void If_no_antechamber_fits_in_front_of_the_boss_no_level_is_built()
    {
        // Error branch: the boss room is so wide that, wherever the antechamber stands, the boss room behind it runs into
        // the deep room in front of it. The generator gives up after its tries, as for any layout that does not fit.
        var layout = new LayoutBalance { CombatRoomsMin = 1, CombatRoomsMax = 1, CorridorChance = 0f, MaxLoops = 0 };

        Assert.Throws<InvalidOperationException>(() => LevelGenerator.Generate(DeepRoomsAndBoss(WideBoss), 3, layout));
    }

    [Fact]
    public void With_a_small_boss_room_the_same_rooms_build_a_level_with_an_antechamber()
    {
        // The counterpart of the error branch: only the boss room differs.
        var layout = new LayoutBalance { CombatRoomsMin = 1, CombatRoomsMax = 1, CorridorChance = 0f, MaxLoops = 0 };

        var generated = LevelGenerator.Generate(DeepRoomsAndBoss("##+##\n#...#\n#####"), 3, layout);

        Assert.Single(generated.Rooms, room => room.Kind == RoomKind.Antechamber);
    }

    [Fact]
    public void The_boss_door_takes_both_middle_cells_of_the_short_side_with_floor_on_both_sides()
    {
        // Issue #566: the door cell the antechamber had (X, Z) and the other middle cell beside it - one door over two cells.
        Assert.All(Levels.Value, level =>
        {
            var room = TheAntechamber(level);
            var boss = Assert.Single(level.Rooms, r => r.Kind == RoomKind.Boss);
            var bossDoor = Assert.Single(level.Doors, door => door.Kind == DoorKind.Boss);
            Assert.Equal(2, bossDoor.Cells.Count);
            Assert.Equal((bossDoor.X, bossDoor.Z), bossDoor.Cells[0]);

            var (_, firstOffset, onWestOrEast) = PlaceOnSide(room, bossDoor);
            var (sideLength, secondOffset, secondOnWestOrEast) = PlaceOnSide(room, bossDoor with { X = bossDoor.Cells[1].X, Z = bossDoor.Cells[1].Z });
            Assert.Equal(Antechamber.Width, sideLength);
            Assert.Equal(onWestOrEast, secondOnWestOrEast);
            Assert.Equal([1, 2], new[] { firstOffset, secondOffset }.Order());

            // Walkable, with the antechamber's floor on one side and the boss room's on the other.
            var (stepX, stepZ) = onWestOrEast ? (1, 0) : (0, 1);
            Assert.All(bossDoor.Cells, cell =>
            {
                Assert.False(level.Level[cell.X, cell.Z].IsSolid, $"seed {level.Seed}: door cell {cell} is solid");
                var sides = new[] { (cell.X - stepX, cell.Z - stepZ), (cell.X + stepX, cell.Z + stepZ) };
                Assert.Contains(sides, room.FloorCells.Contains);
                Assert.Contains(sides, boss.FloorCells.Contains);
            });
        });
    }

    [Fact]
    public void A_boss_room_with_wall_behind_the_second_door_cell_cannot_be_made()
    {
        // Issue #566: the second cell of the boss door lies beside the slot, so the boss room needs floor beside the floor
        // behind its slot. The template rule already demands it - a boss room one cell wide is refused, never placed. The
        // counterpart, three cells wide, builds a level (With_a_small_boss_room_the_same_rooms_build_a_level_with_an_antechamber).
        var error = Assert.Throws<LevelFormatException>(() => RoomTemplateParser.Parse("#+#\n#.#\n###", RoomKind.Boss, "boss"));

        Assert.Contains("flush with a side wall", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_boss_room_never_runs_into_the_corridor_in_front_of_the_antechamber()
    {
        // Issue #580: the corridor is about to be placed when the boss room behind the antechamber is chosen, so only
        // LevelGenerator.Overlaps keeps them apart. A short bend: the antechamber at its exit lies beside its long leg,
        // and a wide boss room on the antechamber's side towards the leg's far end would reach over that leg. On the other
        // side it fits - every level is built, and no floor cell belongs to two rooms.
        var corridor = string.Join('\n', "#+####", "#.####", "#.####", "#.####", "#.####", "#.####", "#.####", "#....+", "######");
        var boss = $"{new string('#', 8)}+{new string('#', 8)}\n#{new string('.', 15)}#\n#{new string('.', 15)}#\n{new string('#', 17)}";
        RoomTemplate[] rooms =
        [
            RoomTemplateParser.Parse("##+##\n#.P.#\n#####", RoomKind.Start, "start"),
            RoomTemplateParser.Parse("##+##\n#...#\n#...#\n##+##", RoomKind.Combat, "combat"),
            RoomTemplateParser.Parse(corridor, RoomKind.Corridor, "short-bend"),
            RoomTemplateParser.Parse(boss, RoomKind.Boss, "wide-boss"),
        ];
        var layout = new LayoutBalance { CombatRoomsMin = 1, CombatRoomsMax = 1, CorridorChance = 1f, MaxLoops = 0 };

        Assert.All(Enumerable.Range(0, 30), seed =>
        {
            var generated = LevelGenerator.Generate(rooms, seed, layout);
            var owner = new Dictionary<(int X, int Z), int>();
            Assert.All(generated.Rooms, room => Assert.All(room.FloorCells, cell =>
                Assert.True(owner.TryAdd(cell, room.Index), $"seed {seed}: room {room.Index} ({room.Kind}) runs into room {owner.GetValueOrDefault(cell)} at {cell}")));
        });
    }

    // 41 cells of floor from west to east, the door in the middle of the long side.
    private static readonly string WideBoss = $"{new string('#', 21)}+{new string('#', 21)}\n#{new string('.', 41)}#\n{new string('#', 43)}";

    // Start and combat room 13 x 13 with a door in the middle of every side: whatever stands in front of one of their doors
    // has deep floor on both sides of it.
    private static RoomTemplate[] DeepRoomsAndBoss(string boss)
    {
        var rows = new List<string> { "######+######" };
        for (var z = 1; z < 12; z++)
        {
            rows.Add(z == 6 ? "+...........+" : "#...........#");
        }

        rows.Add("######+######");
        var combat = string.Join('\n', rows);
        var start = combat.Remove((6 * 14) + 6, 1).Insert((6 * 14) + 6, "P");
        return
        [
            RoomTemplateParser.Parse(start, RoomKind.Start, "deep-start"),
            RoomTemplateParser.Parse(combat, RoomKind.Combat, "deep-combat"),
            RoomTemplateParser.Parse(boss, RoomKind.Boss, "boss"),
        ];
    }
}
