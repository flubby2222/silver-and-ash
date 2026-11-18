using System.Numerics;
using System.Text.RegularExpressions;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.Presentation;
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

    [Theory]
    [InlineData("menu-title", PhotoMenu.Title)]
    [InlineData("menu-pause", PhotoMenu.Pause)]
    [InlineData("menu-settings", PhotoMenu.Settings)]
    [InlineData("menu-quit", PhotoMenu.Quit)]
    public void The_menu_scenes_show_their_menu(string name, PhotoMenu menu)
    {
        Assert.Equal(menu, Built(name).Menu);
    }

    [Fact]
    public void Scenes_of_the_game_show_no_menu()
    {
        Assert.All(
            PhotoScenes.All.Where(scene => !scene.Name.StartsWith("menu-", StringComparison.Ordinal)),
            scene => Assert.Equal(PhotoMenu.None, scene.Build(Studio()).Menu));
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
            "model-probe-4m", "model-probe-10m", "chests-4m", "pistol-recoil", "blade-charged", "blade-swing-half", "blade-swing-full", "hud-rifle",
            "bat-screech", "bat-dive", "gorm-swing-windup", "gorm-swing", "gorm-throw-windup", "gorm-phase2", "hud-reload", "hud-texts",
            "hud-calm", "hud-boss", "hud-skillpoint", "hud-pickup",
            "gorm-hammer-windup", "gorm-shockwave",
            "door-normal", "door-normal-half", "door-normal-open", "door-locked", "door-locked-half", "door-locked-open",
            "door-boss", "door-boss-half", "door-boss-open", "door-cracked", "door-sideways-half", "door-level-seed1",
            "door-level-seed2-boss", "door-normal-10m", "door-locked-10m", "door-boss-10m",
            "cracked-wall-2m", "cracked-wall-4m", "cracked-wall-10m", "cracked-wall-flat", "cracked-wall-a-4m",
            "cracked-wall-b-4m", "cracked-wall-c-4m", "cracked-wall-sideways-4m", "cracked-wall-secret-4m", "cracked-wall-break",
            "cracked-wall-break-after",
            "showroom-a-corridor", "showroom-a-room", "showroom-b-corridor", "showroom-b-room", "showroom-c-corridor",
            "showroom-c-room", "stairs-4m", "stairs-10m",
            "equipment-rifle", "equipment-worn", "equipment-forged", "equipment-royal", "equipment-vampiric", "equipment-taken-royal", "equipment-taken-vampiric",
            "menu-title", "menu-pause", "menu-settings", "menu-quit",
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

    [Fact]
    public void An_arena_can_be_as_high_as_gorms_boss_room()
    {
        var level = Studio().Arena('T', 8, RoomTemplate.RoomCeilingHeight).Level;
        var open = (from x in Enumerable.Range(0, level.Width)
                    from z in Enumerable.Range(0, level.Depth)
                    where !level[x, z].IsSolid
                    select level[x, z]).ToList();

        Assert.NotEmpty(open);
        Assert.All(open, cell => Assert.Equal(RoomTemplate.RoomCeilingHeight, cell.CeilingHeight));
        Assert.All(open, cell => Assert.Equal(0f, cell.FloorHeight));
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
    public void The_screech_scene_stops_halfway_through_the_screech()
    {
        var setup = Built("bat-screech");

        var ticks = setup.Run();

        Assert.True(ticks < setup.Ticks, "The bat should screech long before the scene gives up.");
        Assert.Equal(BatState.Screeching, setup.World.Bats[0].State);
        Assert.InRange(setup.World.Bats[0].ScreechProgress, 0.5f, 1f);
    }

    [Fact]
    public void The_dive_scene_stops_while_the_bat_dives()
    {
        var setup = Built("bat-dive");

        var ticks = setup.Run();

        Assert.True(ticks < setup.Ticks, "The bat should dive long before the scene gives up.");
        Assert.Equal(BatState.Diving, setup.World.Bats[0].State);
    }

    [Fact]
    public void The_shockwave_scene_stops_with_the_wave_half_way_out()
    {
        var setup = PhotoScenes.Find("gorm-shockwave")!.Build(Studio());

        var ticks = setup.Run();

        Assert.True(ticks < setup.Ticks, "Gorm should strike long before the scene gives up.");
        Assert.InRange(setup.World.Gorm!.Shockwave!.Value.Radius, 6f, 6.2f);
        Assert.False(setup.World.Player.IsDead);
    }

    [Theory]
    [InlineData("gorm-swing-windup", GormState.WindingUp, GormAttack.Swing, 0.82f)]
    [InlineData("gorm-swing", GormState.Swinging, GormAttack.Swing, 0.3f)]
    [InlineData("gorm-throw-windup", GormState.WindingUp, GormAttack.Throw, 0.5f)]
    public void The_gorm_attack_scenes_stop_in_their_moment(string name, GormState state, GormAttack attack, float progress)
    {
        var setup = Built(name);

        var ticks = setup.Run();

        Assert.True(ticks < setup.Ticks, "Gorm should come to this attack long before the scene gives up.");
        var gorm = setup.World.Gorm!;
        Assert.Equal(state, gorm.State);
        Assert.Equal(attack, gorm.Attack);
        Assert.InRange(gorm.StateProgress, progress, 1f);
    }

    [Fact]
    public void The_phase_2_scene_stops_at_the_end_of_his_roar()
    {
        var setup = Built("gorm-phase2");

        var ticks = setup.Run();

        Assert.True(ticks < setup.Ticks, "Gorm should come to the end of his roar long before the scene gives up.");
        Assert.True(setup.World.Gorm!.IsInPhase2);
        Assert.Equal(GormState.Roaring, setup.World.Gorm.State);
        Assert.InRange(setup.World.Gorm.StateProgress, 0.95f, 1f);
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
    public void The_map_scene_opens_the_map_page()
    {
        var setup = Built("map");

        setup.Run();

        Assert.Equal(PlayerMenuPage.Map, setup.World.Player.Menu.Page);
    }

    [Fact]
    public void Only_the_map_scene_shows_the_map_and_only_the_model_probes_are_probes()
    {
        var studio = Studio();
        var setups = PhotoScenes.All.ToDictionary(scene => scene.Name, scene => scene.Build(studio));

        Assert.Equal(["map"], setups.Where(pair => pair.Value.Input?.Invoke(0).PressMenu == MenuKey.Map).Select(pair => pair.Key));
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

    [Theory]
    [InlineData(DoorKind.Normal, false)]
    [InlineData(DoorKind.Locked, false)]
    [InlineData(DoorKind.Boss, false)]
    [InlineData(DoorKind.Normal, true)]
    public void The_open_door_hall_is_opened_the_regular_way_and_the_player_looks_at_it_from_the_start(DoorKind kind, bool sideways)
    {
        var world = Studio().DoorHall(kind, sideways, open: true);
        var door = world.Doors[0];
        var middle = (door.BoxMin + door.BoxMax) / 2f;

        Assert.True(door.IsOpen);
        Assert.True(door.OpenedFromLowSide, "The player opened it from their side: the leaves swing away from them.");
        Assert.False(world.Level[door.X, door.Z].IsSolid);
        Assert.Equal(0, world.Player.Keys);
        Assert.Equal(6f, sideways ? middle.X - world.Player.Position.X : middle.Z - world.Player.Position.Z, 0.001f);
        var towards = Vector3.Normalize(middle - world.Player.EyePosition);
        Assert.Equal(1f, Vector3.Dot(world.Player.View.Forward, towards), 0.001f);
    }

    [Fact]
    public void The_10_m_door_scenes_stand_10_m_before_a_closed_door()
    {
        foreach (var name in new[] { "door-normal-10m", "door-locked-10m", "door-boss-10m" })
        {
            var world = Built(name).World;
            var door = world.Doors[0];

            Assert.False(door.IsOpen);
            Assert.Equal(10f, ((door.BoxMin.Z + door.BoxMax.Z) / 2f) - world.Player.Position.Z, 0.001f);
        }
    }

    [Theory]
    [InlineData("cracked-wall-2m", 2f, null, false, false)]
    [InlineData("cracked-wall-4m", 4f, null, false, false)]
    [InlineData("cracked-wall-10m", 10f, null, false, false)]
    [InlineData("cracked-wall-a-4m", 4f, TextureVariant.A, false, false)]
    [InlineData("cracked-wall-b-4m", 4f, TextureVariant.B, false, false)]
    [InlineData("cracked-wall-c-4m", 4f, TextureVariant.C, false, false)]
    [InlineData("cracked-wall-sideways-4m", 4f, null, true, false)]
    [InlineData("cracked-wall-secret-4m", 4f, null, false, true)]
    public void A_cracked_wall_scene_looks_straight_at_the_face_it_promises(
        string name, float metres, TextureVariant? variant, bool sideways, bool fromSecretRoom)
    {
        var world = Built(name).World;
        var player = world.Player;
        var wall = world.Doors.Single(door => door.Kind == DoorKind.Cracked
            && Raycast.AgainstBox(player.EyePosition, player.View.Forward, door.BoxMin, door.BoxMax) is not null);
        var middle = (wall.BoxMin + wall.BoxMax) / 2f;
        var (front, back) = CrackedWallLook.FaceCells(wall.X, wall.Z, wall.Side);
        var (frontVariant, backVariant) = CrackedWallLook.Variants(world.Surfaces, wall.X, wall.Z, wall.Side);
        var playerCell = ((int)MathF.Floor(player.Position.X / Level.CellSize), (int)MathF.Floor(player.Position.Z / Level.CellSize));
        var faceCell = Vector2.Distance(Cell(front), Flat(player.Position)) < Vector2.Distance(Cell(back), Flat(player.Position)) ? front : back;
        var secret = world.Rooms.Where(room => room.Kind == RoomKind.Secret).SelectMany(room => room.Cells).ToHashSet();

        Assert.False(wall.IsOpen);
        Assert.Equal(metres, Vector2.Distance(Flat(middle), Flat(player.Position)), 0.001f);
        Assert.Equal(sideways, wall.Side is RoomSide.East or RoomSide.West);
        Assert.Equal(fromSecretRoom, secret.Contains(playerCell));
        Assert.Equal(fromSecretRoom, secret.Contains(faceCell));
        if (variant is { } wanted)
        {
            Assert.Equal(wanted, faceCell == front ? frontVariant : backVariant);
        }
    }

    [Fact]
    public void The_flat_cracked_wall_scene_looks_at_the_face_at_a_flat_angle()
    {
        var world = Built("cracked-wall-flat").World;
        var player = world.Player;
        var wall = world.Doors.First(door => door.Kind == DoorKind.Cracked
            && Raycast.AgainstBox(player.EyePosition, player.View.Forward, door.BoxMin, door.BoxMax) is not null);
        var (front, _) = CrackedWallLook.FaceCells(wall.X, wall.Z, wall.Side);
        var normal = Vector2.Normalize(Cell(front) - Flat((wall.BoxMin + wall.BoxMax) / 2f));
        var look = Vector2.Normalize(Flat(player.View.Forward));

        // 1 m before the face (2 m from the wall's middle) and 6 m along: about 9 degrees between the line of sight and the face.
        Assert.InRange(MathF.Abs(Vector2.Dot(normal, look)), 0.1f, 0.2f);
    }

    [Fact]
    public void The_break_scene_breaks_the_wall_with_a_charged_blow_and_waits_a_moment()
    {
        var setup = Built("cracked-wall-break");
        var broken = new List<int>();

        var ticks = setup.Run(() => broken.AddRange(setup.World.Events.OfType<WallBroken>().Select(wall => wall.DoorId)));

        var wall = Assert.Single(broken);
        Assert.True(setup.World.Doors[wall].IsOpen);
        Assert.True(ticks < setup.Ticks);
    }

    [Theory]
    [InlineData("stairs-4m", 4f)]
    [InlineData("stairs-10m", 10f)]
    public void The_stairs_scenes_wait_until_the_stairs_stand_and_look_at_them_from_their_distance(string name, float metres)
    {
        var setup = Built(name);
        var start = setup.World.Player.Body.Position;

        var ticks = setup.Run();

        // The stairs rose the regular way: Gorm dead, the room cleared on the first tick, the whole rise gone by.
        var stairs = Assert.IsType<Stairs>(setup.World.Stairs);
        Assert.True(stairs.CanInteract);
        Assert.True(ticks < setup.Ticks, "The stairs should stand long before the scene gives up.");
        Assert.InRange(Vector2.Distance(Flat(start), Flat(stairs.Position)), metres - Level.CellSize, metres + Level.CellSize);

        var middle = stairs.Position + new Vector3(0f, Stairs.Height / 2f, 0f);
        var towards = Vector3.Normalize(middle - setup.World.Player.EyePosition);
        Assert.Equal(1f, Vector3.Dot(setup.World.Player.View.Forward, towards), 0.01f);
    }

    [Fact]
    public void The_equipment_rifle_scene_looks_at_the_rifle_within_reach()
    {
        var world = Built("equipment-rifle").World;

        var target = Assert.IsType<WeaponDrop>(world.InteractionTarget(world.Player));
        Assert.Equal(WeaponKind.Rifle, target.Kind);
        Assert.Equal(new InteractionOffer("Take Rifle (Forged)", true, ItemTier.Forged), target.OfferFor(world.Player));
    }

    [Fact]
    public void The_hud_texts_scene_ends_with_every_hud_text_showing_at_once()
    {
        var setup = Built("hud-texts");

        setup.Run();

        var world = setup.World;
        Assert.Equal(AmmoHint.Reload, world.Player.AmmoHint);
        Assert.NotNull(world.InteractionTarget(world.Player));
        Assert.True(world.Player.PendingBoons > 0);
        Assert.True(world.Player.IsConsecrated);
    }

    [Theory]
    [InlineData("equipment-worn", ItemTier.Worn)]
    [InlineData("equipment-forged", ItemTier.Forged)]
    [InlineData("equipment-royal", ItemTier.Royal)]
    [InlineData("equipment-vampiric", ItemTier.Vampiric)]
    public void Each_tier_scene_looks_at_an_item_of_its_tier_within_reach(string name, ItemTier tier)
    {
        var world = Built(name).World;

        var target = Assert.IsType<ItemDrop>(world.InteractionTarget(world.Player));
        Assert.Equal(tier, target.Piece.Tier);
        Assert.True(target.OfferFor(world.Player).IsPossible);
    }

    [Theory]
    [InlineData("equipment-taken-royal", ItemKind.HuntersEye)]
    [InlineData("equipment-taken-vampiric", ItemKind.Leech)]
    public void The_equipment_taken_scenes_take_their_item_with_f(string name, ItemKind kind)
    {
        var setup = Built(name);
        var taken = new List<EquipmentTaken>();

        setup.Run(() => taken.AddRange(setup.World.Events.OfType<EquipmentTaken>()));

        Assert.Equal(Equipment.Of(kind), Assert.Single(taken).Piece);
        Assert.Equal([kind], setup.World.Player.Items);
    }

    private static Vector2 Flat(Vector3 point) => new(point.X, point.Z);

    private static Vector2 Cell((int X, int Z) cell) => new((cell.X + 0.5f) * Level.CellSize, (cell.Z + 0.5f) * Level.CellSize);
}
