using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

public class LevelGeneratorTests
{
    private const int ManySeeds = 100;

    private static readonly IReadOnlyList<RoomTemplate> Rooms = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    private static readonly LayoutBalance Layout = Balance.Default.Layout;

    private static readonly Lazy<GeneratedLevel[]> ManyLevels =
        new(() => Enumerable.Range(0, ManySeeds).Select(seed => LevelGenerator.Generate(Rooms, seed, Layout)).ToArray());

    [Fact]
    public void The_same_seed_gives_the_same_level()
    {
        var first = LevelGenerator.Generate(Rooms, 123, Layout);
        var second = LevelGenerator.Generate(Rooms, 123, Layout);

        Assert.Equal(first.Seed, second.Seed);
        Assert.Equal(Describe(first), Describe(second));
    }

    [Fact]
    public void Different_seeds_give_different_levels()
    {
        var layouts = ManyLevels.Value.Select(Describe).Distinct().Count();

        Assert.True(layouts > ManySeeds * 9 / 10, $"Only {layouts} different levels from {ManySeeds} seeds.");
    }

    [Fact]
    public void Every_walkable_cell_can_be_reached_from_the_start()
    {
        Assert.All(ManyLevels.Value, generated =>
        {
            var level = generated.Level;
            var reached = Reachable(level, (level.PlayerStartX, level.PlayerStartZ));

            for (var x = 0; x < level.Width; x++)
            {
                for (var z = 0; z < level.Depth; z++)
                {
                    Assert.True(level[x, z].IsSolid || reached.Contains((x, z)), $"Seed {generated.Seed}: cell {x}/{z} unreachable.");
                }
            }
        });
    }

    [Fact]
    public void Rooms_do_not_overlap_and_their_floor_is_walkable()
    {
        Assert.All(ManyLevels.Value, generated =>
        {
            var owner = new Dictionary<(int X, int Z), int>();
            foreach (var room in generated.Rooms)
            {
                Assert.NotEmpty(room.FloorCells);
                foreach (var cell in room.FloorCells)
                {
                    Assert.True(owner.TryAdd(cell, room.Index), $"Seed {generated.Seed}: rooms overlap at {cell}.");
                    Assert.False(generated.Level[cell.X, cell.Z].IsSolid);
                    Assert.InRange(cell.X, room.X, room.X + room.Width - 1);
                    Assert.InRange(cell.Z, room.Z, room.Z + room.Depth - 1);
                }
            }
        });
    }

    [Fact]
    public void Every_door_joins_exactly_two_rooms_that_lie_on_its_two_sides()
    {
        Assert.All(ManyLevels.Value, generated =>
        {
            Assert.All(generated.Doors, door =>
            {
                Assert.NotEqual(door.RoomA, door.RoomB);
                Assert.False(generated.Level[door.X, door.Z].IsSolid);

                var a = generated.Rooms[door.RoomA].FloorCells.ToHashSet();
                var b = generated.Rooms[door.RoomB].FloorCells.ToHashSet();
                var (dx, dz) = door.Side switch
                {
                    RoomSide.North => (0, -1),
                    RoomSide.South => (0, 1),
                    RoomSide.West => (-1, 0),
                    _ => (1, 0),
                };

                // Seen from room A the door is in its wall on side 'Side': A lies behind it, B in front.
                Assert.Contains((door.X - dx, door.Z - dz), a);
                Assert.Contains((door.X + dx, door.Z + dz), b);
            });

            // No two doors between the same pair of rooms.
            var pairs = generated.Doors.Select(door => (Math.Min(door.RoomA, door.RoomB), Math.Max(door.RoomA, door.RoomB)));
            Assert.Equal(generated.Doors.Count, pairs.Distinct().Count());
        });
    }

    [Fact]
    public void Only_door_cells_connect_the_rooms()
    {
        Assert.All(ManyLevels.Value, generated =>
        {
            var doorCells = generated.Doors.Select(door => (door.X, door.Z)).ToHashSet();
            var roomOf = generated.Rooms.SelectMany(room => room.FloorCells.Select(cell => (cell, room.Index)))
                .ToDictionary(pair => pair.cell, pair => pair.Index);

            foreach (var (cell, room) in roomOf)
            {
                foreach (var next in Neighbours(cell))
                {
                    var walkable = !generated.Level[next.X, next.Z].IsSolid;
                    Assert.True(
                        !walkable || doorCells.Contains(next) || roomOf.GetValueOrDefault(next, -1) == room,
                        $"Seed {generated.Seed}: room {room} leaks into {next} without a door.");
                }
            }
        });
    }

    [Fact]
    public void There_is_one_start_room_with_the_player_start_and_the_set_number_of_combat_rooms()
    {
        Assert.All(ManyLevels.Value, generated =>
        {
            var start = Assert.Single(generated.Rooms, room => room.Kind == RoomKind.Start);
            Assert.Equal(0, start.Index);
            Assert.Contains((generated.Level.PlayerStartX, generated.Level.PlayerStartZ), start.FloorCells);

            var combat = generated.Rooms.Count(room => room.Kind == RoomKind.Combat);
            Assert.InRange(combat, Layout.CombatRoomsMin, Layout.CombatRoomsMax);
        });
    }

    [Fact]
    public void The_boss_room_has_one_entrance_and_lies_farthest_from_the_start()
    {
        Assert.All(ManyLevels.Value, generated =>
        {
            var boss = Assert.Single(generated.Rooms, room => room.Kind == RoomKind.Boss);

            Assert.Single(generated.Doors, door => door.RoomA == boss.Index || door.RoomB == boss.Index);
            Assert.All(
                generated.Rooms.Where(room => room != boss),
                room => Assert.True(room.DistanceFromStart < boss.DistanceFromStart, $"Seed {generated.Seed}: {room.TemplateName} is as far as the boss."));
        });
    }

    [Fact]
    public void The_door_of_the_boss_room_is_a_boss_door_one_treasure_room_is_locked_and_all_others_are_normal()
    {
        Assert.All(ManyLevels.Value, generated =>
        {
            var boss = generated.Rooms.Single(room => room.Kind == RoomKind.Boss).Index;
            bool IsTreasure(int room) => generated.Rooms[room].Kind == RoomKind.Treasure;

            Assert.All(generated.Doors, door =>
            {
                if (door.RoomA == boss || door.RoomB == boss)
                {
                    Assert.Equal(DoorKind.Boss, door.Kind);
                }
                else if (door.Kind == DoorKind.Locked)
                {
                    Assert.True(IsTreasure(door.RoomA) || IsTreasure(door.RoomB), $"Seed {generated.Seed}: a lock not at a treasure room.");
                }
                else
                {
                    Assert.Equal(DoorKind.Normal, door.Kind);
                }
            });
            Assert.Single(generated.Doors, door => door.Kind == DoorKind.Locked);
        });
    }

    [Fact]
    public void Distance_from_the_start_counts_the_rooms_on_the_shortest_way()
    {
        Assert.All(ManyLevels.Value, generated =>
        {
            Assert.Equal(0, generated.Rooms[0].DistanceFromStart);
            var neighbours = generated.Rooms.ToDictionary(room => room.Index, _ => new List<int>());
            foreach (var door in generated.Doors)
            {
                neighbours[door.RoomA].Add(door.RoomB);
                neighbours[door.RoomB].Add(door.RoomA);
            }

            foreach (var room in generated.Rooms.Skip(1))
            {
                var distances = neighbours[room.Index].Select(other => generated.Rooms[other].DistanceFromStart).ToList();
                Assert.Equal(room.DistanceFromStart - 1, distances.Min());
            }
        });
    }

    [Fact]
    public void Corridors_lead_from_one_room_to_another()
    {
        Assert.All(ManyLevels.Value, generated =>
        {
            Assert.All(generated.Rooms.Where(room => room.Kind == RoomKind.Corridor), corridor =>
            {
                var others = generated.Doors
                    .Where(door => door.RoomA == corridor.Index || door.RoomB == corridor.Index)
                    .Select(door => generated.Rooms[door.RoomA == corridor.Index ? door.RoomB : door.RoomA])
                    .ToList();

                Assert.Equal(2, others.Count);
                Assert.All(others, other => Assert.NotEqual(RoomKind.Corridor, other.Kind));
            });
        });
    }

    [Fact]
    public void No_two_rooms_are_linked_twice()
    {
        // A loop must be a real second way round: two rooms never share two doors, nor a door and a corridor.
        Assert.All(ManyLevels.Value, generated =>
        {
            var links = new List<(int, int)>();
            foreach (var door in generated.Doors)
            {
                var a = generated.Rooms[door.RoomA];
                var b = generated.Rooms[door.RoomB];
                if (a.Kind != RoomKind.Corridor && b.Kind != RoomKind.Corridor)
                {
                    links.Add((Math.Min(a.Index, b.Index), Math.Max(a.Index, b.Index)));
                }
            }

            foreach (var corridor in generated.Rooms.Where(room => room.Kind == RoomKind.Corridor))
            {
                var ends = generated.Doors
                    .Where(door => door.RoomA == corridor.Index || door.RoomB == corridor.Index)
                    .Select(door => door.RoomA == corridor.Index ? door.RoomB : door.RoomA)
                    .Order()
                    .ToList();
                links.Add((ends[0], ends[1]));
            }

            Assert.Equal(links.Count, links.Distinct().Count());
        });
    }

    [Fact]
    public void A_loop_corridor_never_runs_beside_a_corridor_between_the_same_rooms()
    {
        // Start and combat room each have two door slots side by side: after the first corridor, a second one
        // would fit right next to it - a "loop" that leads nowhere new.
        RoomTemplate[] rooms =
        [
            RoomTemplateParser.Parse("##+#+##\n#.....#\n#..P..#\n#######", RoomKind.Start, "start"),
            RoomTemplateParser.Parse("###+###\n#.....#\n#.....#\n##+#+##", RoomKind.Combat, "two-below"),
            RoomTemplateParser.Parse("#+#\n#.#\n#+#", RoomKind.Corridor, "short"),
            RoomTemplateParser.Parse("##+##\n#...#\n#####", RoomKind.Boss, "boss"),
        ];
        var layout = new LayoutBalance { CombatRoomsMin = 1, CombatRoomsMax = 1, CorridorChance = 1f, MaxLoops = 1 };

        Assert.All(Enumerable.Range(0, 30), seed =>
        {
            var generated = LevelGenerator.Generate(rooms, seed, layout);
            var corridorEnds = generated.Rooms.Where(room => room.Kind == RoomKind.Corridor).Select(corridor =>
                string.Join("-", generated.Doors
                    .Where(door => door.RoomA == corridor.Index || door.RoomB == corridor.Index)
                    .Select(door => door.RoomA == corridor.Index ? door.RoomB : door.RoomA)
                    .Order()))
                .ToList();

            Assert.Equal(corridorEnds.Count, corridorEnds.Distinct().Count());
        });
    }

    [Fact]
    public void The_corridor_chance_sets_how_often_a_room_is_reached_through_a_corridor()
    {
        // Loops add a few more corridors, and a corridor is skipped where none fits - so only roughly.
        var levels = Enumerable.Range(0, 50)
            .Select(seed => LevelGenerator.Generate(Rooms, seed, Layout with { CorridorChance = 0.5f, MaxLoops = 0 }))
            .ToList();
        var rooms = levels.Sum(level => level.Rooms.Count(room => room.Kind is RoomKind.Combat or RoomKind.Boss));
        var corridors = levels.Sum(level => level.Rooms.Count(room => room.Kind == RoomKind.Corridor));

        Assert.InRange((double)corridors / rooms, 0.35, 0.65);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    public void If_the_boss_cannot_be_the_farthest_room_no_level_is_built(float corridorChance)
    {
        // The only combat room is a dead end, so the boss could only go next to the start - as close as the combat room.
        RoomTemplate[] rooms =
        [
            RoomTemplateParser.Parse("##+##\n#.P.#\n##+##", RoomKind.Start, "start"),
            RoomTemplateParser.Parse("##+##\n#...#\n#####", RoomKind.Combat, "dead-end"),
            RoomTemplateParser.Parse("#+#\n#.#\n#+#", RoomKind.Corridor, "corridor"),
            RoomTemplateParser.Parse("##+##\n#...#\n#####", RoomKind.Boss, "boss"),
        ];
        var layout = new LayoutBalance { CombatRoomsMin = 1, CombatRoomsMax = 1, CorridorChance = corridorChance, MaxLoops = 0 };

        Assert.Throws<InvalidOperationException>(() => LevelGenerator.Generate(rooms, 3, layout));
    }

    [Fact]
    public void Loops_happen_but_not_more_than_allowed()
    {
        // A level without loops is a tree: exactly one door less than rooms. Every extra door closes a loop.
        var loops = ManyLevels.Value.Select(generated => generated.Doors.Count - (generated.Rooms.Count - 1)).ToList();

        Assert.All(loops, count => Assert.InRange(count, 0, Layout.MaxLoops));
        Assert.True(loops.Count(count => count > 0) >= ManySeeds / 2, $"Only {loops.Count(count => count > 0)} of {ManySeeds} levels have a loop.");
    }

    [Fact]
    public void The_layout_values_shape_the_level()
    {
        var layout = new LayoutBalance { CombatRoomsMin = 3, CombatRoomsMax = 3, CorridorChance = 0f, MaxLoops = 0 };

        var generated = LevelGenerator.Generate(Rooms, 7, layout);

        Assert.Equal(3, generated.Rooms.Count(room => room.Kind == RoomKind.Combat));
        Assert.DoesNotContain(generated.Rooms, room => room.Kind == RoomKind.Corridor);
        Assert.Equal(generated.Rooms.Count - 1, generated.Doors.Count);
    }

    [Fact]
    public void Without_corridor_chance_there_are_no_corridors_at_all()
    {
        var layout = Layout with { CorridorChance = 0f };

        Assert.All(Enumerable.Range(0, 20), seed => Assert.DoesNotContain(
            LevelGenerator.Generate(Rooms, seed, layout).Rooms, room => room.Kind == RoomKind.Corridor));
    }

    [Fact]
    public void With_corridors_always_every_room_is_reached_through_a_corridor()
    {
        var layout = Layout with { CorridorChance = 1f, MaxLoops = 0 };

        var generated = LevelGenerator.Generate(Rooms, 11, layout);

        Assert.All(generated.Doors, door =>
            Assert.Contains(RoomKind.Corridor, new[] { generated.Rooms[door.RoomA].Kind, generated.Rooms[door.RoomB].Kind }));
    }

    [Fact]
    public void Enemy_places_lie_on_the_floor_of_their_room()
    {
        Assert.All(ManyLevels.Value, generated =>
        {
            Assert.All(generated.Rooms, room =>
            {
                var floor = room.FloorCells.ToHashSet();
                Assert.All(room.GhoulSlots.Concat(room.BatSlots), slot => Assert.Contains(slot, floor));
            });

            Assert.Contains(generated.Rooms, room => room.GhoulSlots.Count > 0);
        });
    }

    [Fact]
    public void Rooms_keep_their_ceiling_and_doors_have_the_low_one()
    {
        var generated = LevelGenerator.Generate(Rooms, 5, Layout);

        Assert.All(generated.Rooms, room => Assert.All(room.FloorCells, cell =>
            Assert.Equal(RoomTemplate.CeilingFor(room.Kind), generated.Level[cell.X, cell.Z].CeilingHeight)));
        Assert.All(generated.Doors, door =>
            Assert.Equal(Cell.DefaultCeilingHeight, generated.Level[door.X, door.Z].CeilingHeight));
    }

    [Theory]
    [InlineData(RoomKind.Start)]
    [InlineData(RoomKind.Combat)]
    [InlineData(RoomKind.Boss)]
    public void A_missing_kind_of_room_is_reported(RoomKind missing)
    {
        var error = Assert.Throws<ArgumentException>(
            () => LevelGenerator.Generate(Rooms.Where(room => room.Kind != missing).ToList(), 1, Layout));

        Assert.Contains(missing.ToString(), error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(RoomKind.Boss, true)]
    [InlineData(RoomKind.Treasure, true)]
    [InlineData(RoomKind.Start, false)]
    [InlineData(RoomKind.Combat, false)]
    [InlineData(RoomKind.Corridor, false)]
    public void Only_some_kinds_of_room_have_a_single_entrance(RoomKind kind, bool single)
    {
        Assert.Equal(single, kind.HasSingleEntrance());
    }

    private static string Describe(GeneratedLevel generated)
    {
        var level = generated.Level;
        var cells = string.Concat(Enumerable.Range(0, level.Depth).SelectMany(z =>
            Enumerable.Range(0, level.Width).Select(x => level[x, z].IsSolid ? '#' : '.')));
        var rooms = string.Join(";", generated.Rooms.Select(room => $"{room.Kind}:{room.TemplateName}@{room.X},{room.Z}"));
        var doors = string.Join(";", generated.Doors.Select(door => $"{door.X},{door.Z}:{door.RoomA}-{door.RoomB}"));
        return $"{level.Width}x{level.Depth}|{cells}|{rooms}|{doors}";
    }

    private static HashSet<(int X, int Z)> Reachable(Level level, (int X, int Z) start)
    {
        var reached = new HashSet<(int X, int Z)> { start };
        var open = new Queue<(int X, int Z)>(reached);
        while (open.TryDequeue(out var cell))
        {
            foreach (var next in Neighbours(cell))
            {
                if (level.IsInside(next.X, next.Z) && !level[next.X, next.Z].IsSolid && reached.Add(next))
                {
                    open.Enqueue(next);
                }
            }
        }

        return reached;
    }

    private static (int X, int Z)[] Neighbours((int X, int Z) cell) =>
        [(cell.X + 1, cell.Z), (cell.X - 1, cell.Z), (cell.X, cell.Z + 1), (cell.X, cell.Z - 1)];
}
