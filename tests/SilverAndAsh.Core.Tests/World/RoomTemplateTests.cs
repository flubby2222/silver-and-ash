using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

public class RoomTemplateTests
{
    // A small combat room: door slots north (top), east (right) and south (bottom), two ghouls, one bat.
    private const string Small = """
        ##+##
        #G..#
        #.B.+
        #..G#
        ##+##
        """;

    [Fact]
    public void It_reads_size_floor_and_walls()
    {
        var room = RoomTemplateParser.Parse(Small, RoomKind.Combat, "small");

        Assert.Equal("small", room.Name);
        Assert.Equal(RoomKind.Combat, room.Kind);
        Assert.Equal(5, room.Width);
        Assert.Equal(5, room.Depth);
        Assert.True(room.CellAt(0, 0).IsSolid);
        Assert.False(room.CellAt(1, 1).IsSolid);
    }

    [Fact]
    public void It_finds_the_door_slots_and_their_sides()
    {
        var room = RoomTemplateParser.Parse(Small, RoomKind.Combat, "small");

        Assert.Equal(
            [new DoorSlot(2, 0, RoomSide.North), new DoorSlot(4, 2, RoomSide.East), new DoorSlot(2, 4, RoomSide.South)],
            room.DoorSlots.OrderBy(slot => slot.Z).ThenBy(slot => slot.X));
    }

    [Fact]
    public void A_door_slot_is_a_wall_until_the_generator_uses_it()
    {
        var room = RoomTemplateParser.Parse(Small, RoomKind.Combat, "small");

        Assert.True(room.CellAt(2, 0).IsSolid);
    }

    [Fact]
    public void It_finds_the_enemy_places()
    {
        var room = RoomTemplateParser.Parse(Small, RoomKind.Combat, "small");

        Assert.Equal([(1, 1), (3, 3)], room.GhoulSlots);
        Assert.Equal([(2, 2)], room.BatSlots);
    }

    [Theory]
    [InlineData(RoomKind.Combat, RoomTemplate.RoomCeilingHeight)]
    [InlineData(RoomKind.Boss, RoomTemplate.RoomCeilingHeight)]
    [InlineData(RoomKind.Corridor, Cell.DefaultCeilingHeight)]
    public void The_ceiling_height_depends_on_the_kind_of_room(RoomKind kind, float ceiling)
    {
        var text = kind == RoomKind.Boss ? "##+##\n#...#\n#####" : Small;

        var room = RoomTemplateParser.Parse(text, kind, "room");

        Assert.Equal(ceiling, room.CeilingHeight);
        Assert.Equal(ceiling, room.CellAt(1, 1).CeilingHeight);
    }

    [Fact]
    public void The_start_room_has_the_player_start()
    {
        var room = RoomTemplateParser.Parse("##+##\n#.P.#\n#####", RoomKind.Start, "start");

        Assert.Equal((2, 1), room.PlayerStart);
    }

    [Theory]
    [InlineData("##+##\n#.X.#\n#####", RoomKind.Combat, "Unknown symbol 'X'")]
    [InlineData("#####\n#...#\n#####", RoomKind.Combat, "no door slot")]
    [InlineData("#####\n#.+.+\n#####", RoomKind.Combat, "edge")]
    [InlineData("+####\n#...#\n#####", RoomKind.Combat, "corner")]
    [InlineData("##+##\n#.#.#\n#...#\n#####", RoomKind.Combat, "behind")]
    [InlineData("###+#\n#...#\n#...#\n#####", RoomKind.Combat, "beside")]
    [InlineData("#+###\n#...#\n#...#\n#####", RoomKind.Combat, "beside")]
    [InlineData("#####\n#...#\n#...+\n#####", RoomKind.Combat, "beside")]
    [InlineData("#####\n#...#\n#.###\n#.###\n+...#\n#####", RoomKind.Combat, "beside")]
    [InlineData("##+##\n#...#\n#...+\n#...#\n#####", RoomKind.Boss, "exactly one door slot")]
    [InlineData("##+##\n#...#\n#####", RoomKind.Start, "player start")]
    [InlineData("##+##\n#.P.#\n#####", RoomKind.Combat, "player start")]
    [InlineData("##+####\n#...#.#\n#...#.#\n#######", RoomKind.Combat, "connected")]
    [InlineData("##+##\n#.D.#\n#####", RoomKind.Combat, "Unknown symbol 'D'")]
    [InlineData("##+##\n#....\n#####", RoomKind.Combat, "edge must be wall")]
    [InlineData("##+#\n#...\n#..", RoomKind.Combat, "edge must be wall")]
    [InlineData("##+##\n#...#\n#.###", RoomKind.Combat, "edge must be wall")]
    public void Mistakes_are_reported_with_the_room_name(string text, RoomKind kind, string problem)
    {
        var error = Assert.Throws<LevelFormatException>(() => RoomTemplateParser.Parse(text, kind, "broken"));

        Assert.Contains("broken", error.Message);
        Assert.Contains(problem, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_corridor_door_may_sit_between_the_corridor_walls()
    {
        var corridor = RoomTemplateParser.Parse("#+###\n#.###\n#...+\n#####", RoomKind.Corridor, "bend");

        Assert.Equal(2, corridor.DoorSlots.Count);
    }

    [Fact]
    public void A_mistake_names_room_line_and_column()
    {
        var error = Assert.Throws<LevelFormatException>(
            () => RoomTemplateParser.Parse("##+##\n#.#.#\n#...#\n#####", RoomKind.Combat, "combat/broken.txt"));

        Assert.StartsWith("combat/broken.txt, line 1:", error.Message);
        Assert.Contains("column 3", error.Message);
        Assert.Equal(1, error.LineNumber);
    }

    [Fact]
    public void A_quarter_turn_swaps_width_and_depth_and_turns_the_doors()
    {
        // 5 wide, 3 deep, one door at the top (north). Turned clockwise: 3 wide, 5 deep, the door now east.
        var room = RoomTemplateParser.Parse("##+##\n#G..#\n#####", RoomKind.Combat, "wide");

        var turned = room.Rotated(1);

        Assert.Equal(3, turned.Width);
        Assert.Equal(5, turned.Depth);
        Assert.Equal([new DoorSlot(2, 2, RoomSide.East)], turned.DoorSlots);
        Assert.Equal([(1, 1)], turned.GhoulSlots);
        Assert.False(turned.CellAt(1, 1).IsSolid);
    }

    [Fact]
    public void Four_quarter_turns_give_the_same_room()
    {
        var room = RoomTemplateParser.Parse(Small, RoomKind.Combat, "small");

        var turned = room.Rotated(4);

        Assert.Equal(room.DoorSlots, turned.DoorSlots);
        Assert.Equal(room.GhoulSlots, turned.GhoulSlots);
        Assert.Equal(room.BatSlots, turned.BatSlots);
        Assert.True(Enumerable.Range(0, room.Width).All(x =>
            Enumerable.Range(0, room.Depth).All(z => room.CellAt(x, z) == turned.CellAt(x, z))));
    }

    [Fact]
    public void A_negative_turn_goes_anticlockwise()
    {
        var room = RoomTemplateParser.Parse("##+##\n#G..#\n#####", RoomKind.Combat, "wide");

        var turned = room.Rotated(-1);

        Assert.Equal(room.Rotated(3).DoorSlots, turned.DoorSlots);
        Assert.Equal([new DoorSlot(0, 2, RoomSide.West)], turned.DoorSlots);
    }

    [Fact]
    public void Mirroring_swaps_east_and_west()
    {
        var room = RoomTemplateParser.Parse("#####\n#G..#\n#...+\n#...#\n#####", RoomKind.Combat, "east");

        var mirrored = room.Mirrored();

        Assert.Equal([new DoorSlot(0, 2, RoomSide.West)], mirrored.DoorSlots);
        Assert.Equal([(3, 1)], mirrored.GhoulSlots);
    }

    [Fact]
    public void The_shipped_rooms_are_all_valid()
    {
        var rooms = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

        Assert.Single(rooms, room => room.Kind == RoomKind.Start);
        Assert.True(rooms.Count(room => room.Kind == RoomKind.Combat) >= 4);
        Assert.True(rooms.Count(room => room.Kind == RoomKind.Corridor) >= 3);
        Assert.Single(rooms, room => room.Kind == RoomKind.Boss);
    }

    [Fact]
    public void A_folder_that_names_no_kind_of_room_is_reported()
    {
        var root = Path.Combine(Path.GetTempPath(), $"rooms-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "kitchen"));
        try
        {
            var error = Assert.Throws<LevelFormatException>(() => RoomTemplateParser.LoadAll(root));

            Assert.Contains("kitchen", error.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void The_shipped_combat_rooms_are_big_enough_to_fight_in()
    {
        var rooms = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

        Assert.All(rooms.Where(room => room.Kind is RoomKind.Combat or RoomKind.Boss), room =>
        {
            Assert.InRange(room.Width, 10, 18);
            Assert.InRange(room.Depth, 10, 18);
            Assert.True(room.GhoulSlots.Count + room.BatSlots.Count >= 3, $"{room.Name} has too few enemy places.");
        });
    }
}
