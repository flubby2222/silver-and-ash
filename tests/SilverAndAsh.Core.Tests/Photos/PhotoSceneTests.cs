using System.Numerics;
using System.Text.RegularExpressions;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.Showroom;
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
    [InlineData("menu-travel", PhotoMenu.Travel)]
    [InlineData("menu-travel-sealed", PhotoMenu.Travel)]
    public void The_menu_scenes_show_their_menu(string name, PhotoMenu menu)
    {
        Assert.Equal(menu, Built(name).Menu);
    }

    // Issue #480: after the scene's tick the player stands on the start-hall teleporter, the boss gate free or sealed.
    [Theory]
    [InlineData("teleporter-hint", TravelState.Ready)]
    [InlineData("menu-travel", TravelState.Ready)]
    [InlineData("menu-travel-sealed", TravelState.Sealed)]
    public void The_travel_scenes_show_the_boss_gate_free_or_sealed(string name, TravelState bossGate)
    {
        var setup = Built(name);
        setup.Run();
        var choices = setup.World.TravelChoices(setup.World.Player);

        Assert.Equal(TravelState.Here, choices.Single(choice => choice.Teleporter.Plan.Site == TeleporterSite.Start).State);
        Assert.Equal(bossGate, choices.Single(choice => choice.Teleporter.Plan.Site == TeleporterSite.BossGate).State);
    }

    [Theory]
    [InlineData("teleporter-locked-3m", 3f, false)]
    [InlineData("teleporter-unlocked-3m", 3f, true)]
    [InlineData("teleporter-locked-10m", 10f, false)]
    [InlineData("teleporter-unlocked-10m", 10f, true)]
    public void The_teleporter_scenes_stand_at_their_distance(string name, float metres, bool unlocked)
    {
        var setup = Built(name);
        setup.Run();
        var teleporter = setup.World.Teleporters.Single();

        Assert.Equal(unlocked, teleporter.IsUnlocked);
        Assert.Equal(metres, Vector2.Distance(new(teleporter.Position.X, teleporter.Position.Z), new(setup.World.Player.Position.X, setup.World.Player.Position.Z)), 0.01f);
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
            "ghoul-4m", "ghoul-10m", "bat-4m", "bat-10m", "ghoul-windup", "gorm", "hud-weapons", "inventory", "map", "map-skillpoint",
            "model-probe-4m", "model-probe-10m", "chests-4m", "pistol-recoil", "blade-charged", "blade-swing-half", "blade-swing-full", "club-rest", "club-swing-half", "club-charged", "club-swing-full", "club-charged-release", "showroom-club", "hud-rifle",
            "bat-screech", "bat-dive", "gorm-swing-windup", "gorm-swing", "gorm-throw-windup", "gorm-phase2", "hud-reload", "hud-texts",
            "hud-calm", "hud-items", "hud-boss", "hud-skillpoint", "hud-pickup",
            "hud-reload-start", "hud-reload-mid", "hud-hit", "hud-lowlife", "hud-skillpoint-glow", "hud-blunderbuss", "hud-melee",
            "rifle-at-wall", "blunderbuss-at-wall",
            "gorm-hammer-windup", "gorm-shockwave",
            "door-normal", "door-normal-half", "door-normal-open", "door-locked", "door-locked-half", "door-locked-open",
            "door-boss", "door-boss-half", "door-boss-open", "door-cracked", "door-sideways-half", "door-level-seed1",
            "door-level-seed2-boss", "door-level-barred", "door-level-barred-shut", "door-normal-10m", "door-locked-10m", "door-boss-10m",
            "cracked-wall-2m", "cracked-wall-4m", "cracked-wall-10m", "cracked-wall-flat", "cracked-wall-a-4m",
            "cracked-wall-b-4m", "cracked-wall-c-4m", "cracked-wall-sideways-4m", "cracked-wall-secret-4m", "cracked-wall-break",
            "cracked-wall-break-dust", "cracked-wall-break-after",
            "showroom-a-corridor", "showroom-a-room", "showroom-b-corridor", "showroom-b-room", "showroom-c-corridor",
            "showroom-c-room", "stairs-4m", "stairs-10m", "well-ready", "well-spent", "well-hint",
            "teleporter-locked-3m", "teleporter-unlocked-3m", "teleporter-locked-10m", "teleporter-unlocked-10m",
            "teleporter-hint", "menu-travel", "menu-travel-sealed", "room-antechamber",
            "equipment-rifle", "equipment-worn", "equipment-forged", "equipment-royal", "equipment-vampiric", "equipment-taken-royal", "equipment-taken-vampiric",
            "menu-title", "menu-pause", "menu-settings", "menu-quit",
            "ghoul-shotbody-front", "ghoul-shotbody-side",
            "crossbow-skeleton-rest-4m", "crossbow-skeleton-announce-4m", "crossbow-skeleton-announce-2m",
            "crypt-warden-rest-4m", "crypt-warden-announce-4m", "crypt-warden-announce-2m",
            "wall-pillar-a-corridor", "wall-pillar-a-room", "wall-pillar-b-corridor", "wall-pillar-b-room",
            "wall-pillar-c-corridor", "wall-pillar-c-room",
            "ceiling-arch-a", "ceiling-arch-b", "ceiling-arch-c", "ceiling-arch-sequence",
            "wall-niche-a-corridor", "wall-niche-a-room", "wall-niche-b-corridor", "wall-niche-b-room",
            "wall-niche-c-corridor", "wall-niche-c-room",
            "portcullis-closed-room", "portcullis-closed-closet", "portcullis-closed-far",
            "portcullis-half-room", "portcullis-half-closet", "portcullis-half-far",
            "portcullis-open-room", "portcullis-open-closet", "portcullis-open-far",
            "column-corridor", "column-room",
            "column-intact-6m", "column-intact-10m", "column-hit1-6m", "column-hit1-10m",
            "column-hit2-6m", "column-hit2-10m", "column-broken-6m", "column-broken-10m",
            "wall-niche-deep-a-corridor", "wall-niche-deep-a-room", "wall-niche-deep-b-corridor", "wall-niche-deep-b-room",
            "wall-niche-deep-c-corridor", "wall-niche-deep-c-room",
            "wall-niche-shelf-a-corridor", "wall-niche-shelf-a-room", "wall-niche-shelf-b-corridor", "wall-niche-shelf-b-room",
            "wall-niche-shelf-c-corridor", "wall-niche-shelf-c-room",
            "wall-niche-bottles", "wall-niche-bottles-askew", "wall-niche-bottles-shelf", "wall-niche-bottles-shelf-askew",
            "wall-niche-candles", "wall-niche-candles-askew", "wall-niche-skulls", "wall-niche-skulls-askew",
            "boss-portal-front-6m", "boss-portal-front-6m-open", "boss-portal-back-6m", "boss-portal-back-6m-open",
            "boss-portal-barred", "boss-portal-gorm",
            "bullet-impact-wall",
        ];

        Assert.Equal(wanted.Order(), PhotoScenes.All.Select(scene => scene.Name).Order());
    }

    [Fact]
    public void Every_scene_keeps_its_place_name_description_and_setup_as_recorded()
    {
        // Issue #589: the list of the scenes was split into one list per theme. The recording holds, in the order of
        // --photo all, only what each scene lays down itself: name, ticks, menu, switches and description - nothing that
        // follows from the balancing (how long a scene runs, where the player ends up). A new scene adds one line; a
        // changed line of another scene means that scene changed too.
        var recorded = File.ReadAllLines(RepositoryFiles.Find("tests/SilverAndAsh.Core.Tests/Photos/photo-scenes.txt"));
        var studio = Studio();

        var now = PhotoScenes.All.Select(scene => Fingerprint(scene, studio)).ToArray();

        var differences = Enumerable.Range(0, Math.Max(recorded.Length, now.Length))
            .Where(line => line >= recorded.Length || line >= now.Length || recorded[line] != now[line])
            .Select(line => $"line {line + 1}:\n  recorded: {(line < recorded.Length ? recorded[line] : "(none)")}\n  now:      {(line < now.Length ? now[line] : "(none)")}")
            .ToList();
        Assert.True(differences.Count == 0, $"photo-scenes.txt differs from PhotoScenes.All:\n{FirstDifferences(differences)}");
    }

    // Issue #678: one line missing in the middle shifts every line after it; the first differences say what happened,
    // the rest is only counted.
    private const int ShownDifferences = 5;

    private static string FirstDifferences(IReadOnlyList<string> differences)
    {
        var shown = string.Join("\n", differences.Take(ShownDifferences));
        return differences.Count > ShownDifferences ? $"{shown}\n... and {differences.Count - ShownDifferences} more" : shown;
    }

    [Fact]
    public void The_recording_message_shows_the_first_differences_in_full_and_counts_the_rest()
    {
        var differences = Enumerable.Range(1, ShownDifferences + 3).Select(line => $"line {line}:\n  recorded: a\n  now:      b").ToList();

        var message = FirstDifferences(differences);

        Assert.StartsWith(differences[0] + "\n", message, StringComparison.Ordinal);
        Assert.Contains(differences[ShownDifferences - 1], message, StringComparison.Ordinal);
        Assert.DoesNotContain(differences[ShownDifferences], message, StringComparison.Ordinal);
        Assert.EndsWith("\n... and 3 more", message, StringComparison.Ordinal);
        Assert.Equal(string.Join("\n", differences.Take(2)), FirstDifferences(differences.Take(2).ToList()));
    }

    [Fact]
    public void The_recording_message_counts_the_rest_only_beyond_the_shown_differences()
    {
        // Issue #682: the edge of the shortened message - exactly ShownDifferences in full without a count, one more
        // with "... and 1 more" (with ">=" instead of ">" the first would end in "... and 0 more").
        var differences = Enumerable.Range(1, ShownDifferences + 1).Select(line => $"line {line}:\n  recorded: a\n  now:      b").ToList();

        Assert.Equal(string.Join("\n", differences.Take(ShownDifferences)), FirstDifferences(differences.Take(ShownDifferences).ToList()));
        Assert.Equal(string.Join("\n", differences.Take(ShownDifferences)) + "\n... and 1 more", FirstDifferences(differences));
    }

    private static string Fingerprint(PhotoScene scene, PhotoStudio studio)
    {
        var setup = scene.Build(studio);
        var flags = string.Concat(
            setup.IsModelProbe ? "P" : "-",
            setup.ShowsShotBodies ? "W" : "-",
            setup.ShowsOnlyTheWorld ? "O" : "-",
            setup.Input is null ? "-" : "I",
            setup.StopWhen is null ? "-" : "S");
        return string.Join(" | ", scene.Name, $"ticks {setup.Ticks} menu {setup.Menu} {flags}", scene.Description);
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

    public static TheoryData<string> NamesOfScenesWithAStopCondition()
    {
        var names = new TheoryData<string>();
        foreach (var scene in PhotoScenes.All.Where(scene => scene.Build(Studio()).StopWhen is not null))
        {
            names.Add(scene.Name);
        }

        return names;
    }

    [Fact]
    public void Some_scenes_have_a_stop_condition()
    {
        // Issue #678: guards the theory below - without any such scene it would check nothing and still pass.
        Assert.NotEmpty(NamesOfScenesWithAStopCondition());
    }

    [Theory]
    [MemberData(nameof(NamesOfScenesWithAStopCondition))]
    public void Every_scene_with_a_stop_condition_stops_before_its_tick_cap(string name)
    {
        // Issue #678: a stop condition that never fires lets the scene run to its cap and the photo silently shows
        // another moment. The scene runs as the game runs it (same studio, Balance.Default, same Run) and must stop early.
        var setup = Built(name);

        var ran = setup.Run();

        var problem = CapProblem(name, setup, ran);
        Assert.True(problem is null, problem);
    }

    // Issue #682: Run returns tick + 1 when the condition fires, so a condition that fires only at the last tick gives
    // the same count as one that never fires. The message names both, so nobody hunts for a condition that does fire.
    private static string? CapProblem(string name, PhotoSetup setup, int ran) =>
        ran < setup.Ticks
            ? null
            : $"{name}: did not stop before its cap, all {setup.Ticks} ticks ran (the stop condition fired only at the last tick or never)";

    [Fact]
    public void A_stop_condition_that_fires_only_at_the_last_tick_counts_as_not_stopping_and_the_message_says_so()
    {
        // Issue #682: a real scene with its condition swapped for one that fires after a given tick, run as the game
        // runs it.
        var name = PhotoScenes.All.Select(scene => scene.Name)
            .First(scene => Built(scene) is { StopWhen: not null, Ticks: >= 2 });

        int RanWithAConditionFiringAfter(int tick, out string? problem)
        {
            var built = Built(name);
            var ticks = 0;
            var setup = built with { StopWhen = _ => ++ticks >= tick };
            var ran = setup.Run();
            problem = CapProblem(name, setup, ran);
            return ran;
        }

        var cap = Built(name).Ticks;

        Assert.Equal(cap, RanWithAConditionFiringAfter(cap, out var atTheLastTick));
        Assert.NotNull(atTheLastTick);
        Assert.Contains("only at the last tick or never", atTheLastTick, StringComparison.Ordinal);
        Assert.Equal(cap - 1, RanWithAConditionFiringAfter(cap - 1, out var oneTickEarlier));
        Assert.Null(oneTickEarlier);
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

    [Theory]
    [InlineData("hud-reload-start", 0.1f)]
    [InlineData("hud-reload-mid", 0.5f)]
    public void The_reload_scenes_stop_at_their_share_of_the_reload(string name, float share)
    {
        var setup = Built(name);

        setup.Run();

        var weapon = HudReadout.Of(setup.World, setup.World.Player).Weapons.Single(weapon => weapon.IsReloading);
        Assert.Equal(share, weapon.ReloadProgress, 0.05f);
    }

    [Theory]
    [InlineData("hud-weapons", WeaponKind.Pistol, false)]
    [InlineData("hud-rifle", WeaponKind.Rifle, false)]
    [InlineData("hud-blunderbuss", WeaponKind.Blunderbuss, false)]
    [InlineData("hud-reload", WeaponKind.Pistol, false)]
    [InlineData("hud-melee", WeaponKind.Pistol, true)]
    [InlineData("rifle-at-wall", WeaponKind.Rifle, false)]
    [InlineData("blunderbuss-at-wall", WeaponKind.Blunderbuss, false)]
    public void The_ammo_scenes_end_with_their_weapon_chosen_and_the_melee_weapon_in_use_only_in_the_melee_scene(
        string name, WeaponKind chosen, bool meleeInUse)
    {
        // Issue #388: the photos of the large rounds number - pistol, rifle, blunderbuss, empty magazine and melee.
        var setup = Built(name);

        setup.Run();

        var readout = HudReadout.Of(setup.World, setup.World.Player);
        var weapon = Assert.Single(readout.Weapons, weapon => weapon.IsSelected);
        Assert.Equal(chosen, weapon.Kind);
        Assert.Equal(meleeInUse, readout.IsMeleeInUse);
        Assert.Equal(name == "hud-reload" ? 0 : weapon.MagazineSize, weapon.Rounds);
    }

    [Fact]
    public void The_impact_scene_ends_its_shot_in_the_wall_close_by_while_the_spark_still_shows()
    {
        // Issue #609: the picture of the spark. The bullet must end in the wall behind the player, not in the dummy.
        var setup = Built("bullet-impact-wall");
        var impacts = new List<(BulletImpact Impact, int Tick)>();
        var tick = 0;

        setup.Run(() =>
        {
            impacts.AddRange(setup.World.Events.OfType<BulletImpact>().Select(impact => (impact, tick)));
            tick++;
        });

        var (hit, at) = Assert.Single(impacts);
        Assert.Equal(1f, hit.Normal.Z, 3);
        Assert.InRange(Vector3.Distance(hit.Point, setup.World.Player.EyePosition), 2f, 4f);
        Assert.True((setup.Ticks - at) / (float)SimulationClock.TicksPerSecond < 0.08f, "The spark (0.08 s) has to be still there.");
    }

    [Fact]
    public void The_hit_scene_stops_just_after_a_blow_while_the_picture_is_still_dark()
    {
        var setup = Built("hud-hit");
        var effects = new ScreenEffects(setup.World.Player.Id);

        var ticks = setup.Run(() =>
        {
            foreach (var gameEvent in setup.World.Events)
            {
                effects.OnEvent(gameEvent);
            }

            effects.Tick(setup.World.Player);
        });

        Assert.True(ticks < setup.Ticks, "The ghoul should land a blow long before the scene gives up.");
        Assert.True(setup.World.Player.Health < setup.World.Player.MaxHealth);
        Assert.True(effects.Darkness > 0f);
    }

    [Fact]
    public void The_low_life_scene_shows_the_red_edge_at_its_strongest()
    {
        var setup = Built("hud-lowlife");
        var effects = new ScreenEffects(setup.World.Player.Id);

        setup.Run(() => effects.Tick(setup.World.Player));

        Assert.Equal(ScreenEffects.LowLifeMax, effects.LowLife, 0.05f);
    }

    [Fact]
    public void The_skill_point_scene_stops_while_the_gold_shine_is_strong()
    {
        var setup = Built("hud-skillpoint-glow");
        var effects = new ScreenEffects(setup.World.Player.Id);

        var ticks = setup.Run(() =>
        {
            foreach (var gameEvent in setup.World.Events)
            {
                effects.OnEvent(gameEvent);
            }

            effects.Tick(setup.World.Player);
        });

        Assert.True(ticks < setup.Ticks, "A shot ghoul should bring the level up long before the scene gives up.");
        Assert.Equal(1, setup.World.Player.Level);
        Assert.True(effects.SkillPoint > 0.5f);
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

    [Theory]
    [InlineData("gorm-swing-windup")]
    [InlineData("gorm-swing")]
    [InlineData("gorm-throw-windup")]
    [InlineData("gorm-hammer-windup")]
    [InlineData("gorm-phase2")]
    [InlineData("gorm-shockwave")]
    public void No_other_gorm_scene_stops_in_the_moment_this_gorm_scene_stops_in(string stopped)
    {
        // Issue #589: every scene that waits for a moment of Gorm's, against the world another of them stopped in.
        string[] moments = ["gorm-swing-windup", "gorm-swing", "gorm-throw-windup", "gorm-hammer-windup", "gorm-phase2", "gorm-shockwave"];
        var setup = Built(stopped);
        Assert.True(setup.Run() < setup.Ticks, $"{stopped} should reach its moment.");

        Assert.All(
            moments.Where(waiting => waiting != stopped),
            waiting => Assert.False(Built(waiting).StopWhen!(setup.World), $"{waiting} would stop in the moment of {stopped}."));
    }

    [Theory]
    [InlineData("gorm-swing-windup")]
    [InlineData("gorm-swing")]
    [InlineData("gorm-throw-windup")]
    [InlineData("gorm-hammer-windup")]
    [InlineData("gorm-phase2")]
    [InlineData("gorm-shockwave")]
    [InlineData("stairs-4m")]
    [InlineData("stairs-10m")]
    public void A_scene_waiting_for_gorm_or_his_stairs_does_not_stop_where_neither_is(string name)
    {
        // Issue #589: a hall with only a ghoul in it has no Gorm and no stairs.
        var ghoulHall = Built("ghoul-4m");
        ghoulHall.Run();

        Assert.Null(ghoulHall.World.Gorm);
        Assert.Null(ghoulHall.World.Stairs);
        Assert.False(Built(name).StopWhen!(ghoulHall.World));
    }

    [Theory]
    [InlineData("blade-charged")]
    [InlineData("club-charged")]
    public void A_charged_scene_does_not_stop_before_the_player_starts_to_charge(string name)
    {
        // Issue #589: before the first tick the melee button has not been held yet.
        var setup = Built(name);

        Assert.False(setup.StopWhen!(setup.World));
    }

    [Theory]
    [InlineData("inventory", MenuKey.Equipment)]
    [InlineData("map", MenuKey.Map)]
    [InlineData("map-skillpoint", MenuKey.Map)]
    public void A_menu_page_scene_presses_its_key_on_the_first_tick_only(string name, MenuKey key)
    {
        // Issue #589: the key is pressed once, as a player opens the page, not on every tick.
        var input = Built(name).Input!;

        Assert.Equal(key, input(0).PressMenu);
        Assert.Null(input(1).PressMenu);
    }

    [Fact]
    public void The_hud_scene_fills_every_weapon_place()
    {
        var setup = Built("hud-weapons");

        Assert.All(setup.World.Player.RangedSlots, Assert.NotNull);
        Assert.Equal(WeaponKind.Club, setup.World.Player.Melee);
    }

    [Fact]
    public void The_hud_items_and_calm_scenes_show_the_club_and_every_item_between_them()
    {
        var setup = Built("hud-items");
        var calm = Built("hud-calm").World.Player.Items;

        Assert.Equal(WeaponKind.Club, setup.World.Player.Melee);
        Assert.Equal(Enum.GetValues<ItemKind>().Order(), setup.World.Player.Items.Union(calm).Order());
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
    public void The_map_scene_with_a_skill_point_opens_the_map_page_while_a_boon_waits()
    {
        var setup = Built("map-skillpoint");

        setup.Run();

        Assert.Equal(PlayerMenuPage.Map, setup.World.Player.Menu.Page);
        Assert.True(setup.World.Player.PendingBoons > 0);
    }

    [Fact]
    public void Only_the_map_scenes_show_the_map_and_only_the_model_probes_are_probes()
    {
        var studio = Studio();
        var setups = PhotoScenes.All.ToDictionary(scene => scene.Name, scene => scene.Build(studio));

        Assert.Equal(["map", "map-skillpoint"], setups.Where(pair => pair.Value.Input?.Invoke(0).PressMenu == MenuKey.Map).Select(pair => pair.Key).Order());
        Assert.Equal(
            ["model-probe-10m", "model-probe-4m"],
            setups.Where(pair => pair.Value.IsModelProbe).Select(pair => pair.Key).Order());
        Assert.Equal(
            ["ghoul-shotbody-front", "ghoul-shotbody-side"],
            setups.Where(pair => pair.Value.ShowsShotBodies).Select(pair => pair.Key).Order());
    }

    [Theory]
    [InlineData("ghoul-shotbody-front", 1f)]
    [InlineData("ghoul-shotbody-side", 0f)]
    public void The_shot_body_scenes_show_the_wires_and_look_at_the_resting_ghoul_from_front_and_side(string name, float ahead)
    {
        // Issue #433: the showroom's ghoul never moves; the player stands 1.8 m from it, in front or to its side.
        var setup = Built(name);
        var ghoul = setup.World.Ghouls.MinBy(ghoul => Vector3.Distance(ghoul.Position, setup.World.Player.Position))!;
        var facing = ghoul.Facing;

        setup.Run();

        Assert.True(setup.ShowsShotBodies);
        Assert.Equal(facing, ghoul.Facing);
        var flat = (setup.World.Player.Position - ghoul.Position) with { Y = 0f };
        Assert.Equal(1.8f, flat.Length(), 0.01f);
        Assert.Equal(ahead, Vector3.Dot(Vector3.Normalize(flat), facing), 0.001f);
        var looking = Vector3.Normalize(setup.World.Player.View.Forward with { Y = 0f });
        Assert.True(Vector3.Dot(looking, -Vector3.Normalize(flat)) > 0.99f);
    }

    [Theory]
    [InlineData("crossbow-skeleton-rest-4m", CrossbowSkeletonStance.Rest, 4f)]
    [InlineData("crossbow-skeleton-announce-4m", CrossbowSkeletonStance.Announce, 4f)]
    [InlineData("crossbow-skeleton-announce-2m", CrossbowSkeletonStance.Announce, 2f)]
    public void The_crossbow_skeleton_scenes_stand_in_front_of_the_skeleton_in_its_pose(string name, CrossbowSkeletonStance stance, float distance)
    {
        // Issue #497: the player stands `distance` m in front of the showroom's skeleton of that pose, looking at it.
        var setup = Built(name);
        setup.Run();
        var world = setup.World;
        var skeleton = world.Showroom!.CrossbowSkeletons.MinBy(exhibit => Vector3.Distance(exhibit.Position, world.Player.Position))!;

        Assert.Equal(stance, skeleton.Stance);
        var flat = (world.Player.Position - skeleton.Position) with { Y = 0f };
        Assert.Equal(distance, flat.Length(), 0.01f);
        Assert.Equal(1f, Vector3.Dot(Vector3.Normalize(flat), skeleton.Facing), 0.001f);
        var looking = Vector3.Normalize(world.Player.View.Forward with { Y = 0f });
        Assert.True(Vector3.Dot(looking, -Vector3.Normalize(flat)) > 0.99f);
    }

    [Theory]
    [InlineData("crypt-warden-rest-4m", CryptWardenStance.Rest, 4f)]
    [InlineData("crypt-warden-announce-4m", CryptWardenStance.Announce, 4f)]
    [InlineData("crypt-warden-announce-2m", CryptWardenStance.Announce, 2f)]
    public void The_crypt_warden_scenes_stand_in_front_of_the_warden_in_its_pose(string name, CryptWardenStance stance, float distance)
    {
        // Issue #530: the player stands `distance` m in front of the showroom's warden of that pose, looking at it.
        var setup = Built(name);
        setup.Run();
        var world = setup.World;
        var warden = world.Showroom!.CryptWardens.MinBy(exhibit => Vector3.Distance(exhibit.Position, world.Player.Position))!;

        Assert.Equal(stance, warden.Stance);
        var flat = (world.Player.Position - warden.Position) with { Y = 0f };
        Assert.Equal(distance, flat.Length(), 0.01f);
        Assert.Equal(1f, Vector3.Dot(Vector3.Normalize(flat), warden.Facing), 0.001f);
        var looking = Vector3.Normalize(world.Player.View.Forward with { Y = 0f });
        Assert.True(Vector3.Dot(looking, -Vector3.Normalize(flat)) > 0.99f);
    }

    [Theory]
    [InlineData("wall-pillar-a-corridor", TextureVariant.A, 3.5f)]
    [InlineData("wall-pillar-b-room", TextureVariant.B, 6f)]
    [InlineData("wall-pillar-c-corridor", TextureVariant.C, 3.5f)]
    [InlineData("wall-pillar-c-room", TextureVariant.C, 6f)]
    public void The_wall_pillar_scenes_look_at_a_pillar_of_their_variant_and_height(string name, TextureVariant variant, float ceiling)
    {
        // Issue #536: the player stands on open floor of the part, south-east of the pillar, and looks at it.
        var setup = Built(name);
        setup.Run();
        var world = setup.World;
        var pillar = world.Showroom!.WallPillars.MinBy(exhibit => Vector3.Distance(exhibit.Position, world.Player.Position))!;

        Assert.Equal(variant, pillar.Variant);
        Assert.Equal(ceiling, pillar.CeilingHeight);
        Assert.Equal(ceiling, world.Level.CellAt(world.Player.Position).CeilingHeight);
        Assert.False(world.Level.CellAt(world.Player.Position).IsSolid);
        var towards = Vector3.Normalize((pillar.Position - world.Player.Position) with { Y = 0f });
        var looking = Vector3.Normalize(world.Player.View.Forward with { Y = 0f });
        Assert.True(Vector3.Dot(looking, towards) > 0.99f);
    }

    [Theory]
    [InlineData("column-corridor", 3.5f)]
    [InlineData("column-room", 6f)]
    public void The_column_scenes_look_at_the_column_under_their_ceiling(string name, float ceiling)
    {
        // Issue #539: the player stands on open floor in front of the column, beside nothing else, and looks at it.
        var setup = Built(name);
        setup.Run();
        var world = setup.World;
        var column = world.Level.Columns.MinBy(column => Vector2.Distance(column.Centre, new Vector2(world.Player.Position.X, world.Player.Position.Z)));

        Assert.Equal(ceiling, world.Level.CellAt(new Vector3(column.X, 0f, column.Z)).CeilingHeight);
        Assert.False(CharacterCollision.Overlaps(world.Level, world.Player.Position, BodySizes.Player.Radius));
        var towards = Vector3.Normalize(new Vector3(column.X, 0f, column.Z) - (world.Player.Position with { Y = 0f }));
        var looking = Vector3.Normalize(world.Player.View.Forward with { Y = 0f });
        Assert.True(Vector3.Dot(looking, towards) > 0.99f);
    }

    [Theory]
    [InlineData("ceiling-arch-a", TextureVariant.A, 1)]
    [InlineData("ceiling-arch-b", TextureVariant.B, 1)]
    [InlineData("ceiling-arch-c", TextureVariant.C, 1)]
    [InlineData("ceiling-arch-sequence", TextureVariant.A, 3)]
    public void The_ceiling_arch_scenes_look_down_an_arch_corridor_of_their_variant(string name, TextureVariant variant, int arches)
    {
        // Issue #537: the player stands in the arch corridor (3.5 m) east of the arches and looks west along it; in front
        // of it every arch of the scene, within 12 m.
        var setup = Built(name);
        setup.Run();
        var world = setup.World;
        var ahead = world.Showroom!.CeilingArches
            .Where(arch => arch.Position.X < world.Player.Position.X && MathF.Abs(arch.Position.Z - world.Player.Position.Z) < 0.01f)
            .OrderByDescending(arch => arch.Position.X)
            .ToList();

        Assert.Equal(3, ahead.Count);
        Assert.All(ahead, arch => Assert.Equal(variant, arch.Variant));
        Assert.InRange(world.Player.Position.X - ahead[arches - 1].Position.X, 2f, 12.5f);
        Assert.Equal(CeilingArchLook.CeilingHeight, world.Level.CellAt(world.Player.Position).CeilingHeight);
        Assert.False(world.Level.CellAt(world.Player.Position).IsSolid);
        var looking = Vector3.Normalize(world.Player.View.Forward with { Y = 0f });
        Assert.True(Vector3.Dot(looking, -Vector3.UnitX) > 0.99f);
    }

    [Theory]
    [InlineData("wall-niche-a-corridor", TextureVariant.A, WallNicheSize.Corridor)]
    [InlineData("wall-niche-a-room", TextureVariant.A, WallNicheSize.Room)]
    [InlineData("wall-niche-b-corridor", TextureVariant.B, WallNicheSize.Corridor)]
    [InlineData("wall-niche-b-room", TextureVariant.B, WallNicheSize.Room)]
    [InlineData("wall-niche-c-corridor", TextureVariant.C, WallNicheSize.Corridor)]
    [InlineData("wall-niche-c-room", TextureVariant.C, WallNicheSize.Room)]
    public void The_wall_niche_scenes_look_at_a_niche_of_their_variant_and_size_from_3_m(string name, TextureVariant variant, WallNicheSize size)
    {
        The_wall_niche_scenes_look_at_a_niche_of_their_form_variant_and_size_from_3_m(name, WallNicheForm.Flat, variant, size);
    }

    [Theory]
    [InlineData("wall-niche-deep-a-corridor", WallNicheForm.Deep, TextureVariant.A, WallNicheSize.Corridor)]
    [InlineData("wall-niche-deep-a-room", WallNicheForm.Deep, TextureVariant.A, WallNicheSize.Room)]
    [InlineData("wall-niche-deep-b-corridor", WallNicheForm.Deep, TextureVariant.B, WallNicheSize.Corridor)]
    [InlineData("wall-niche-deep-b-room", WallNicheForm.Deep, TextureVariant.B, WallNicheSize.Room)]
    [InlineData("wall-niche-deep-c-corridor", WallNicheForm.Deep, TextureVariant.C, WallNicheSize.Corridor)]
    [InlineData("wall-niche-deep-c-room", WallNicheForm.Deep, TextureVariant.C, WallNicheSize.Room)]
    [InlineData("wall-niche-shelf-a-corridor", WallNicheForm.DeepWithShelf, TextureVariant.A, WallNicheSize.Corridor)]
    [InlineData("wall-niche-shelf-a-room", WallNicheForm.DeepWithShelf, TextureVariant.A, WallNicheSize.Room)]
    [InlineData("wall-niche-shelf-b-corridor", WallNicheForm.DeepWithShelf, TextureVariant.B, WallNicheSize.Corridor)]
    [InlineData("wall-niche-shelf-b-room", WallNicheForm.DeepWithShelf, TextureVariant.B, WallNicheSize.Room)]
    [InlineData("wall-niche-shelf-c-corridor", WallNicheForm.DeepWithShelf, TextureVariant.C, WallNicheSize.Corridor)]
    [InlineData("wall-niche-shelf-c-room", WallNicheForm.DeepWithShelf, TextureVariant.C, WallNicheSize.Room)]
    public void The_wall_niche_scenes_look_at_a_niche_of_their_form_variant_and_size_from_3_m(
        string name, WallNicheForm form, TextureVariant variant, WallNicheSize size)
    {
        // Issues #538 and #553: the player stands on open floor of the part, 3 m in front of the niche, and looks at it.
        var setup = Built(name);
        setup.Run();
        var world = setup.World;
        var niche = world.Showroom!.WallNiches.MinBy(exhibit => Vector3.Distance(exhibit.Position, world.Player.Position))!;

        Assert.Equal(form, niche.Form);
        Assert.Equal(variant, niche.Variant);
        Assert.Equal(size, niche.Size);
        var cell = world.Level.CellAt(world.Player.Position);
        Assert.False(cell.IsSolid);
        Assert.Equal(size, WallNicheLook.SizeFor(cell.CeilingHeight - cell.FloorHeight));
        Assert.Equal(3f, Vector3.Dot(world.Player.Position - niche.Position, niche.Facing), 0.01f);
        var towards = Vector3.Normalize((niche.Position - world.Player.Position) with { Y = 0f });
        var looking = Vector3.Normalize(world.Player.View.Forward with { Y = 0f });
        Assert.True(Vector3.Dot(looking, towards) > 0.99f);
    }

    [Theory]
    [InlineData("wall-niche-bottles", WallNicheForm.Deep, WallNicheFilling.Bottles, 2f, 0.3f)]
    [InlineData("wall-niche-bottles-askew", WallNicheForm.Deep, WallNicheFilling.Bottles, 0.9f, 1.4f)]
    [InlineData("wall-niche-bottles-shelf", WallNicheForm.DeepWithShelf, WallNicheFilling.Bottles, 2f, 0.3f)]
    [InlineData("wall-niche-bottles-shelf-askew", WallNicheForm.DeepWithShelf, WallNicheFilling.Bottles, 0.9f, 1.4f)]
    [InlineData("wall-niche-candles", WallNicheForm.DeepWithShelf, WallNicheFilling.Candles, 2f, 0.3f)]
    [InlineData("wall-niche-candles-askew", WallNicheForm.DeepWithShelf, WallNicheFilling.Candles, 0.9f, 1.4f)]
    [InlineData("wall-niche-skulls", WallNicheForm.DeepWithShelf, WallNicheFilling.Skulls, 2f, 0.3f)]
    [InlineData("wall-niche-skulls-askew", WallNicheForm.DeepWithShelf, WallNicheFilling.Skulls, 0.9f, 1.4f)]
    public void The_filled_niche_scenes_look_into_the_pocket_of_a_niche_with_that_filling(
        string name, WallNicheForm form, WallNicheFilling filling, float away, float aside)
    {
        // Issue #553: on open floor, away metres in front of the niche and aside metres east, looking into its pocket -
        // askew the view runs along the wall, so the depth of the pocket shows.
        var setup = Built(name);
        setup.Run();
        var world = setup.World;
        var niche = world.Showroom!.WallNiches
            .Where(exhibit => exhibit.Variant == TextureVariant.A && exhibit.Form == form && exhibit.Filling == filling)
            .MinBy(exhibit => Vector3.Distance(exhibit.Position, world.Player.Position))!;

        Assert.False(world.Level.CellAt(world.Player.Position).IsSolid);
        var offset = world.Player.Position - niche.Position;
        Assert.Equal(away, Vector3.Dot(offset, niche.Facing), 0.01f);
        Assert.Equal(aside, offset.X, 0.01f);
        var (inside, _) = WallNicheFillingLook.Place(form, filling, niche.Position, niche.Facing);
        var towards = Vector3.Normalize((inside - world.Player.Position) with { Y = 0f });
        var looking = Vector3.Normalize(world.Player.View.Forward with { Y = 0f });
        Assert.True(Vector3.Dot(looking, towards) > 0.95f);
        if (aside > 1f)
        {
            // Askew: the view meets the wall at less than 45 degrees.
            Assert.True(MathF.Abs(Vector3.Dot(looking, niche.Facing)) < MathF.Sqrt(0.5f));
        }
    }

    [Theory]
    [InlineData("portcullis-closed-room", 3f)]
    [InlineData("portcullis-closed-closet", -3f)]
    [InlineData("portcullis-closed-far", 6f)]
    [InlineData("portcullis-half-room", 3f)]
    [InlineData("portcullis-half-closet", -3f)]
    [InlineData("portcullis-half-far", 6f)]
    [InlineData("portcullis-open-room", 3f)]
    [InlineData("portcullis-open-closet", -3f)]
    [InlineData("portcullis-open-far", 6f)]
    public void The_portcullis_scenes_look_at_the_doorway_from_open_floor(string name, float back)
    {
        // Issue #540: in front of the door plane (room), behind it (closet) or 6 m back, looking at the doorway.
        var setup = Built(name);
        setup.Run();
        var world = setup.World;
        var portcullis = Assert.Single(world.Showroom!.Portcullises, exhibit => exhibit.Variant == TextureVariant.A);

        Assert.False(world.Level.CellAt(world.Player.Position).IsSolid);
        Assert.Equal(back, world.Player.Position.Z - portcullis.Position.Z, 0.01f);
        var towards = Vector3.Normalize((portcullis.Position - world.Player.Position) with { Y = 0f });
        var looking = Vector3.Normalize(world.Player.View.Forward with { Y = 0f });
        Assert.True(Vector3.Dot(looking, towards) > 0.99f);
    }

    [Theory]
    [InlineData("portcullis-closed-room")]
    [InlineData("portcullis-half-room")]
    [InlineData("portcullis-open-room")]
    public void The_portcullis_scenes_show_the_grate_shut_half_lifted_and_open(string name)
    {
        // The grate's height comes from the world's ticks, as in the game: shut on the floor, about halfway, not drawn.
        var setup = Built(name);
        setup.Run();
        var height = ShowroomGrateCycle.Height(setup.World.Ticks, setup.World.Balance.Grates);

        switch (name)
        {
            case "portcullis-closed-room":
                Assert.Equal(0f, height);
                break;
            case "portcullis-half-room":
                Assert.InRange(height, 1.4f, 2f);
                Assert.True(GratePose.IsDrawn(height));
                break;
            default:
                Assert.False(GratePose.IsDrawn(height));
                break;
        }
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_boss_door_hall_has_a_door_over_two_columns_under_a_ceiling_as_high_as_the_portal(bool sideways)
    {
        // Issue #603 (review of PR #596): the door-boss scenes show the portal only if the hall's boss door is as wide as
        // its model (else the placeholder) and the hall is as high as its frame (else the ceiling cuts into the arch).
        var world = Studio().DoorHall(DoorKind.Boss, sideways);
        var door = Assert.Single(world.Doors);
        var high = DoorFrameLook.Height(DoorKind.Boss);

        Assert.Equal(DoorKind.Boss, door.Kind);
        Assert.Equal(2, door.Cells.Count);
        Assert.True(DoorFrameLook.Fits(door.Kind, door.Cells.Count), "The portal model stands in the hall, not the placeholder.");
        Assert.Equal(sideways ? 0 : 1, door.Cells.Max(cell => cell.X) - door.Cells.Min(cell => cell.X));
        Assert.Equal(sideways ? 1 : 0, door.Cells.Max(cell => cell.Z) - door.Cells.Min(cell => cell.Z));
        foreach (var cell in door.Cells)
        {
            var (front, back) = CrackedWallLook.FaceCells(cell.X, cell.Z, door.Side);
            foreach (var (x, z) in new[] { front, back })
            {
                Assert.False(world.Level[x, z].IsSolid, $"Cell ({x}, {z}) before or behind the door is walkable.");
                Assert.Equal(high, world.Level[x, z].CeilingHeight);
            }
        }

        // The door cells themselves: while the door bars them the world shows a wall there, but the door's box reads the
        // hall's own ceiling - and once open, the level shows it, with no step from 3.5 to 6 m inside the arch.
        Assert.Equal(high, door.BoxMax.Y);
        var opened = Studio().DoorHall(DoorKind.Boss, sideways, open: true);
        Assert.All(opened.Doors[0].Cells, cell =>
        {
            Assert.False(opened.Level[cell.X, cell.Z].IsSolid);
            Assert.Equal(high, opened.Level[cell.X, cell.Z].CeilingHeight);
        });
    }

    [Theory]
    [InlineData("door-level-barred", 1f)]
    [InlineData("door-level-barred-shut", 0f)]
    public void The_barred_door_scene_looks_from_inside_the_sealed_combat_room_at_a_door_with_its_grate_down(string name, float openFraction)
    {
        // Issue #558: the room sealed the regular way on entry; the scene stops once the grate in view is on the floor.
        // Issue #572: once with the door open (as the one the player came by), once closed.
        var setup = Built(name);

        var ticks = setup.Run();

        var world = setup.World;
        var player = world.Player;
        var door = world.Doors
            .Where(door => Raycast.AgainstBox(player.EyePosition, player.View.Forward, door.BoxMin, door.BoxMax) is not null)
            .MinBy(door => Vector3.Distance((door.BoxMin + door.BoxMax) / 2f, player.EyePosition))!;
        var room = world.Rooms.Single(room => room.Cells.Contains(Level.ToGrid(player.Position.X, player.Position.Z)));
        Assert.True(ticks < setup.Ticks, "The grate should be down long before the scene gives up.");
        Assert.Equal(RoomKind.Combat, room.Kind);
        Assert.True(room.IsSealed);
        Assert.Contains(door.Id, room.DoorIds);
        Assert.True(door.HasGrate);
        Assert.True(door.IsBarred);
        Assert.Equal(0f, door.GrateHeight);
        Assert.True(GratePose.IsDrawn(door.GrateHeight));
        Assert.Equal(openFraction, door.OpenFraction);
        Assert.True(world.Level[door.X, door.Z].IsSolid);
        Assert.InRange(Vector2.Distance(Flat((door.BoxMin + door.BoxMax) / 2f), Flat(player.Position)), 4f - 0.5f, 6f + 0.5f);
    }

    [Theory]
    [InlineData("boss-portal-front-6m", 6f, RoomKind.Antechamber, false, false)]
    [InlineData("boss-portal-front-6m-open", 6f, RoomKind.Antechamber, true, false)]
    [InlineData("boss-portal-back-6m", 6f, RoomKind.Boss, false, true)]
    [InlineData("boss-portal-back-6m-open", 6f, RoomKind.Boss, true, false)]
    [InlineData("boss-portal-barred", 6f, RoomKind.Boss, true, true)]
    [InlineData("boss-portal-gorm", 8f, RoomKind.Antechamber, true, false)]
    public void The_boss_portal_scenes_look_at_the_portal_from_their_room_and_distance(
        string name, float metres, RoomKind side, bool open, bool barred)
    {
        // Issue #568: straight out from the middle of the two door cells, looking at the portal.
        var setup = Built(name);
        setup.Run();

        var world = setup.World;
        var player = world.Player;
        var door = world.Doors.Single(door => door.Kind == DoorKind.Boss);
        var middle = (door.BoxMin + door.BoxMax) / 2f;
        var room = world.Rooms.Single(room => room.Cells.Contains(Level.ToGrid(player.Position.X, player.Position.Z)));
        Assert.Equal(2, door.Cells.Count);
        Assert.Equal(side, room.Kind);
        Assert.Equal(metres, Vector2.Distance(Flat(middle), Flat(player.Position)), 0.01f);
        Assert.NotNull(Raycast.AgainstBox(player.EyePosition, player.View.Forward, door.BoxMin, door.BoxMax));
        Assert.Equal(open, door.IsOpen);
        Assert.Equal(barred, door.IsBarred);
        Assert.False(GratePose.DrawsPlaceholderBars(door.Kind, door.IsBarred, door.HasGrate, gratesModelled: false));
        if (open && side == RoomKind.Antechamber)
        {
            Assert.Equal(1f, door.OpenFraction);
        }
    }

    [Fact]
    public void The_barred_portal_scene_waits_until_the_slam_is_over()
    {
        var setup = Built("boss-portal-barred");
        var ticks = setup.Run();
        var door = setup.World.Doors.Single(door => door.Kind == DoorKind.Boss);

        Assert.Equal(setup.World.Balance.Doors.BossShutTick, ticks);
        Assert.True(door.SlamsShut);
        Assert.Empty(setup.World.Events.OfType<PlayerHit>());
        Assert.Equal(0f, door.OpenFraction);
        Assert.All(door.Cells, cell => Assert.True(setup.World.Level[cell.X, cell.Z].IsSolid));
    }

    [Fact]
    public void The_gorm_portal_scene_has_gorm_asleep_just_behind_the_open_portal()
    {
        var setup = Built("boss-portal-gorm");
        setup.Run();
        var world = setup.World;
        var door = world.Doors.Single(door => door.Kind == DoorKind.Boss);
        var gorm = world.Gorm!;

        Assert.Equal(Level.CellSize, Vector2.Distance(Flat((door.BoxMin + door.BoxMax) / 2f), Flat(gorm.Position)), 0.01f);
        Assert.Equal(RoomKind.Boss, world.Rooms.Single(room => room.Cells.Contains(Level.ToGrid(gorm.Position.X, gorm.Position.Z))).Kind);
        Assert.True(world.IsAsleep(gorm));
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

    // Issue #716: the photo of the hint shows the well's name as the game offers it.
    [Fact]
    public void The_well_hint_scene_looks_at_sirius_relief_within_reach()
    {
        var setup = Built("well-hint");

        setup.Run();

        var world = setup.World;
        var target = Assert.IsType<WellOfSirius>(world.InteractionTarget(world.Player));
        Assert.Equal(new InteractionOffer("Use Sirius' Relief", true), target.OfferFor(world.Player));
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
