using System.Numerics;
using System.Text.RegularExpressions;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Photos;

public class PhotoSceneTests
{
    private static readonly IReadOnlyList<RoomTemplate> Templates = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    private static PhotoStudio Studio() => new(Templates, Balance.Default);

    private static PhotoSetup Built(string name) => PhotoScenes.Find(name)!.Build(Studio());

    public static TheoryData<string> SceneNames()
    {
        var names = new TheoryData<string>();
        foreach (var scene in PhotoScenes.All)
        {
            names.Add(scene.Name);
        }

        return names;
    }

    [Fact]
    public void The_look_comparison_takes_the_scenes_issue_234_asks_for()
    {
        string[] wanted = ["room-corridor", "room-combat", "ghoul-4m", "ghoul-10m", "ghoul-windup", "bat-10m", "gorm"];

        Assert.Equal(wanted, PhotoScenes.LookComparison.Select(scene => scene.Name));
    }

    [Fact]
    public void Every_scene_the_issue_asks_for_is_there()
    {
        string[] wanted =
        [
            "start-room", "room-combat", "room-corridor", "room-treasure", "room-secret", "room-boss",
            "ghoul-4m", "ghoul-10m", "bat-4m", "bat-10m", "ghoul-windup", "gorm", "hud-weapons", "inventory", "map",
            "model-probe-4m", "model-probe-10m", "chests-4m",
        ];

        Assert.Equal(wanted.Order(), PhotoScenes.All.Select(scene => scene.Name).Order());
    }

    [Fact]
    public void Scene_names_are_unique_and_safe_as_file_names()
    {
        var names = PhotoScenes.All.Select(scene => scene.Name).ToList();

        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(names, name => Assert.Matches(new Regex(@"^[a-z0-9-]+\z"), name));
        Assert.DoesNotContain(LaunchOptions.AllPhotos, names);
        Assert.DoesNotContain(LaunchOptions.ListPhotos, names);
        Assert.All(PhotoScenes.All, scene => Assert.False(string.IsNullOrWhiteSpace(scene.Description)));
    }

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void Every_scene_builds_and_runs_with_a_living_player(string name)
    {
        var setup = Built(name);

        var ticks = setup.Run();

        Assert.InRange(ticks, 1, Math.Max(1, setup.Ticks));
        Assert.False(setup.World.Player.IsDead);
    }

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void The_same_scene_built_twice_gives_the_same_state(string name)
    {
        var first = Built(name);
        var second = Built(name);

        var firstTicks = first.Run();
        var secondTicks = second.Run();

        Assert.Equal(firstTicks, secondTicks);
        Assert.Equal(first.World.Seed, second.World.Seed);
        Assert.Equal(first.World.Player.Position, second.World.Player.Position);
        Assert.Equal(first.World.Player.View.Forward, second.World.Player.View.Forward);
        Assert.Equal(
            first.World.Targets.Select(target => target.Body.Position),
            second.World.Targets.Select(target => target.Body.Position));
    }

    [Theory]
    [InlineData('G', 4)]
    [InlineData('G', 10)]
    [InlineData('B', 4)]
    [InlineData('B', 10)]
    public void An_arena_puts_the_figure_the_given_distance_in_front_of_the_player(char figure, int metres)
    {
        var world = Studio().Arena(figure, metres);
        var target = world.Targets.Single();

        var flat = (target.Body.Position - world.Player.Position) with { Y = 0f };
        Assert.Equal(metres, flat.Length(), 0.001f);

        // Looking at it: the view points from the eyes to the middle of its body.
        var middle = target.Body.Position + new Vector3(0f, target.Body.Height / 2f, 0f);
        var towards = Vector3.Normalize(middle - world.Player.EyePosition);
        Assert.Equal(1f, Vector3.Dot(world.Player.View.Forward, towards), 0.001f);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    [InlineData(-2)]
    public void An_arena_distance_must_be_a_positive_multiple_of_the_cell_size(int metres)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Studio().Arena('G', metres));
    }

    [Theory]
    [InlineData("room-combat", RoomKind.Combat)]
    [InlineData("room-corridor", RoomKind.Corridor)]
    [InlineData("room-treasure", RoomKind.Treasure)]
    [InlineData("room-secret", RoomKind.Secret)]
    [InlineData("room-boss", RoomKind.Boss)]
    public void A_room_scene_puts_the_player_into_a_room_of_its_kind(string name, RoomKind kind)
    {
        var setup = Built(name);

        setup.Run();

        var cell = Level.ToGrid(setup.World.Player.Position.X, setup.World.Player.Position.Z);
        var room = Assert.Single(setup.World.Rooms, room => room.Cells.Contains(cell));
        Assert.Equal(kind, room.Kind);
        Assert.True(room.IsDiscovered);
    }

    [Fact]
    public void The_windup_scene_stops_while_the_ghoul_draws_back()
    {
        var setup = Built("ghoul-windup");

        var ticks = setup.Run();

        Assert.True(ticks < setup.Ticks, "The ghoul should start its blow long before the scene gives up.");
        Assert.Equal(GhoulState.WindingUp, setup.World.Ghouls[0].State);
        Assert.InRange(setup.World.Ghouls[0].WindUpProgress, 0.5f, 1f);
    }

    [Fact]
    public void The_hud_scene_fills_every_weapon_place()
    {
        var setup = Built("hud-weapons");

        Assert.All(setup.World.Player.RangedSlots, Assert.NotNull);
        Assert.Equal(WeaponKind.Club, setup.World.Player.Melee);
    }

    [Fact]
    public void The_inventory_scene_opens_the_window_with_a_full_bag()
    {
        var setup = Built("inventory");

        setup.Run();

        Assert.True(setup.World.Player.IsInventoryOpen);
        Assert.All(setup.World.Player.Bag, kind => Assert.NotNull(kind));
    }

    [Fact]
    public void Only_the_map_scene_shows_the_map_and_only_the_model_probes_are_probes()
    {
        var studio = Studio();
        var setups = PhotoScenes.All.ToDictionary(scene => scene.Name, scene => scene.Build(studio));

        Assert.Equal(["map"], setups.Where(pair => pair.Value.MapOpen).Select(pair => pair.Key));
        Assert.Equal(
            ["model-probe-10m", "model-probe-4m"],
            setups.Where(pair => pair.Value.IsModelProbe).Select(pair => pair.Key).Order());
    }

    [Fact]
    public void The_gorm_scene_looks_at_gorm()
    {
        var world = Built("gorm").World;
        var gorm = world.Gorm!;

        var middle = gorm.Body.Position + new Vector3(0f, gorm.Body.Height / 2f, 0f);
        var towards = Vector3.Normalize(middle - world.Player.EyePosition);
        Assert.Equal(1f, Vector3.Dot(world.Player.View.Forward, towards), 0.001f);
    }

    [Fact]
    public void The_chest_scene_shows_every_tier_closed_and_one_open_all_facing_the_player()
    {
        var world = Built("chests-4m").World;

        Assert.Equal(Enum.GetValues<ChestTier>().Order(), world.Chests.Where(chest => !chest.IsOpen).Select(chest => chest.Tier).Order());
        Assert.All(world.Chests.Where(chest => !chest.IsOpen), chest => Assert.True(chest.IsLocked));
        var open = Assert.Single(world.Chests, chest => chest.IsOpen);
        Assert.False(open.IsLocked);
        Assert.All(world.Chests, chest =>
        {
            Assert.Equal(ChestFront.MinusZ, chest.Plan.Front);
            Assert.True(chest.Position.Z > world.Player.EyePosition.Z, "The chests stand in front of the player, on the -z side of their fronts.");
            Assert.Equal(4f, chest.Position.Z - world.Player.Body.Position.Z, 0.001f);
        });
    }
}
