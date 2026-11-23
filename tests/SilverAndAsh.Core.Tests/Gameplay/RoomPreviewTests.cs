using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class RoomPreviewTests
{
    private const int Seed = 528;

    public static TheoryData<string> EveryTemplate()
    {
        var data = new TheoryData<string>();
        var rooms = RepositoryFiles.Find("assets/rooms");
        foreach (var file in Directory.EnumerateFiles(rooms, "*.txt", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetRelativePath(rooms, file).Replace('\\', '/'));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryTemplate))]
    public void Every_template_becomes_a_closed_level_with_one_player_on_walkable_floor(string template)
    {
        var preview = new RoomPreview(Load(template));

        var world = preview.NewWorld(Seed, Balance.Default);
        var level = world.Level;

        Assert.Single(world.Players);
        var (startX, startZ) = Level.ToGrid(world.Player.Position.X, world.Player.Position.Z);
        Assert.Equal(preview.Entrance, (startX, startZ));
        Assert.False(level[startX, startZ].IsSolid);
        Assert.False(level[startX + preview.Inwards.X, startZ + preview.Inwards.Z].IsSolid);
        for (var x = 0; x < level.Width; x++)
        {
            Assert.True(level[x, 0].IsSolid, $"{template}: edge cell {x}/0 is open.");
            Assert.True(level[x, level.Depth - 1].IsSolid, $"{template}: edge cell {x}/{level.Depth - 1} is open.");
        }

        for (var z = 0; z < level.Depth; z++)
        {
            Assert.True(level[0, z].IsSolid, $"{template}: edge cell 0/{z} is open.");
            Assert.True(level[level.Width - 1, z].IsSolid, $"{template}: edge cell {level.Width - 1}/{z} is open.");
        }

        foreach (var slot in preview.Template.DoorSlots.Skip(1))
        {
            Assert.True(level[slot.X + 1, slot.Z + 1].IsSolid, $"{template}: door slot {slot.X}/{slot.Z} is open.");
        }
    }

    [Theory]
    [MemberData(nameof(EveryTemplate))]
    public void Every_template_can_be_walked_into_from_its_doorway(string template)
    {
        var world = new RoomPreview(Load(template)).NewWorld(Seed, Balance.Default);
        var start = world.Player.Position;

        // Two seconds straight ahead: the player leaves the doorway and stands in the room, never outside it.
        for (var tick = 0; tick < 120; tick++)
        {
            world.Tick(PlayerInput.None with { Move = new(0f, 1f) });
        }

        var (x, z) = Level.ToGrid(world.Player.Position.X, world.Player.Position.Z);
        Assert.True(Vector3Distance(start, world.Player.Position) > Level.CellSize, $"{template}: the player did not get into the room.");
        Assert.True(x > 0 && z > 0 && x < world.Level.Width - 1 && z < world.Level.Depth - 1, $"{template}: the player left the room.");
        Assert.False(world.Level[x, z].IsSolid);
    }

    [Fact]
    public void The_player_starts_looking_into_the_room()
    {
        var preview = new RoomPreview(Load("combat/l-hall.txt"));

        var forward = preview.NewWorld(Seed, Balance.Default).Player.View.FlatForward;

        // The first door slot of l-hall lies in its north wall: into the room is +Z.
        Assert.Equal((0, 1), preview.Inwards);
        Assert.Equal(0f, forward.X, 3);
        Assert.Equal(1f, forward.Z, 3);
    }

    [Fact]
    public void A_combat_room_gets_its_enemies_as_in_a_run()
    {
        var world = new RoomPreview(Load("combat/l-hall.txt")).NewWorld(Seed, Balance.Default);

        Assert.NotEmpty(world.Ghouls.Concat<object>(world.Bats));
    }

    [Fact]
    public void A_boss_room_gets_its_boss()
    {
        var world = new RoomPreview(Load("boss/great-hall.txt")).NewWorld(Seed, Balance.Default);

        Assert.NotNull(world.Gorm);
    }

    /// <summary>Issue #608: <c>--room</c> shows the boss room as a run does - with its columns, with or without enemies.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_boss_room_gets_its_columns_as_in_a_run(bool withEnemies)
    {
        var preview = new RoomPreview(Load("boss/great-hall.txt"), withEnemies);
        var bare = preview.Build(Seed, Balance.Default with { Columns = Balance.Default.Columns with { BossRoomPerSide = 0 } });

        var world = preview.NewWorld(Seed, Balance.Default);

        Assert.Empty(bare.Level.Columns);
        var expected = BossRoomColumns.Place(bare, Balance.Default.Columns).Level.Columns;
        Assert.NotEmpty(expected);
        Assert.Equal(expected, world.Level.Columns);
    }

    [Fact]
    public void A_combat_room_gets_no_columns()
    {
        var world = new RoomPreview(Load("combat/l-hall.txt")).NewWorld(Seed, Balance.Default);

        Assert.Empty(world.Level.Columns);
    }

    [Theory]
    [InlineData("combat/l-hall.txt")]
    [InlineData("boss/great-hall.txt")]
    public void Without_enemies_the_room_stays_empty(string template)
    {
        var world = new RoomPreview(Load(template), withEnemies: false).NewWorld(Seed, Balance.Default);

        Assert.Empty(world.Ghouls);
        Assert.Empty(world.Bats);
        Assert.Null(world.Gorm);
    }

    [Fact]
    public void Chests_are_placed_without_enemies_too()
    {
        var preview = new RoomPreview(Load("treasure/vault.txt"), withEnemies: false);

        Assert.NotEmpty(preview.NewWorld(Seed, Balance.Default).Chests);
    }

    [Fact]
    public void The_next_world_is_the_same_room_again()
    {
        var preview = new RoomPreview(Load("combat/l-hall.txt"));
        var first = preview.NewWorld(Seed, Balance.Default);

        var next = preview.NextWorld(first);

        Assert.Equal(first.Level.Width, next.Level.Width);
        Assert.Equal(first.Player.Position, next.Player.Position);
        Assert.Equal(Seed, next.Seed);
    }

    [Fact]
    public void The_entrance_photo_is_named_after_the_template()
    {
        var scene = new RoomPreview(Load("combat/l-hall.txt")).EntrancePhoto(Seed);

        Assert.Equal("room-combat-l-hall", scene.Name);
    }

    [Fact]
    public void A_relative_path_is_also_found_below_the_base_directory()
    {
        var repository = Path.GetDirectoryName(Path.GetDirectoryName(RepositoryFiles.Find("assets/rooms")))!;

        var template = RoomPreview.Load(Path.Combine("assets", "rooms", "combat", "l-hall.txt"), repository);

        Assert.Equal("combat/l-hall.txt", template.Name);
        Assert.Equal(RoomKind.Combat, template.Kind);
    }

    [Fact]
    public void A_missing_file_is_named()
    {
        var error = Assert.Throws<FileNotFoundException>(() => RoomPreview.Load("assets/rooms/combat/no-such-room.txt", AppContext.BaseDirectory));

        Assert.Contains("no-such-room.txt", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_invalid_template_is_refused()
    {
        using var folder = new TemplateFolder("combat");
        var file = folder.Write("broken.txt", LHall().Replace("C", "Q", StringComparison.Ordinal));

        Assert.Throws<LevelFormatException>(() => RoomPreview.Load(file));
    }

    [Fact]
    public void A_template_without_a_door_slot_is_refused()
    {
        using var folder = new TemplateFolder("combat");
        var file = folder.Write("closed.txt", LHall().Replace('+', '#'));

        var error = Assert.Throws<LevelFormatException>(() => RoomPreview.Load(file));

        Assert.Contains("door slot", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_template_outside_a_room_kind_folder_is_refused()
    {
        using var folder = new TemplateFolder("kitchen");
        var file = folder.Write("l-hall.txt", LHall());

        var error = Assert.Throws<LevelFormatException>(() => RoomPreview.Load(file));

        Assert.Contains("kitchen", error.Message, StringComparison.Ordinal);
    }

    private static RoomTemplate Load(string template) => RoomPreview.Load(Path.Combine(RepositoryFiles.Find("assets/rooms"), template));

    private static string LHall() => File.ReadAllText(RepositoryFiles.Find("assets/rooms/combat/l-hall.txt"));

    private static float Vector3Distance(System.Numerics.Vector3 a, System.Numerics.Vector3 b) => System.Numerics.Vector3.Distance(a, b);

    // A folder named like a room kind below the test output (never the system temp folder), deleted afterwards.
    private sealed class TemplateFolder : IDisposable
    {
        private readonly string _root = Path.Combine(AppContext.BaseDirectory, "room-preview-tests", Guid.NewGuid().ToString("N"));

        public TemplateFolder(string kind)
        {
            Kind = Path.Combine(_root, kind);
            Directory.CreateDirectory(Kind);
        }

        private string Kind { get; }

        public string Write(string name, string text)
        {
            var file = Path.Combine(Kind, name);
            File.WriteAllText(file, text);
            return file;
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
