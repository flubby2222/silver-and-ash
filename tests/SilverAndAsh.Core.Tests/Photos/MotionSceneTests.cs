using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Photos;

public class MotionSceneTests
{
    private static readonly IReadOnlyList<RoomTemplate> Templates = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    private static PhotoStudio Studio() => new(Templates, Balance.Default);

    private static MotionScene Scene(string name) => MotionScenes.Find(name)!;

    public static TheoryData<string> SceneNames()
    {
        var names = new TheoryData<string>();
        foreach (var scene in MotionScenes.All)
        {
            names.Add(scene.Name);
        }

        return names;
    }

    // What one picture depends on in the Core: the tick, the events the game read since the last picture (they drive
    // swing, recoil and sounds in the views) and the state of the player, the weapons, the targets, the doors and
    // Gorm's attack with its shock wave.
    private static List<string> Filmed(MotionScene scene)
    {
        var setup = scene.Build(Studio());
        var world = setup.World;
        var events = new List<string>();
        var frames = new List<string>();
        setup.Film(
            scene.FrameEvery,
            frame: tick =>
            {
                var player = world.Player;
                var magazine = player.Ranged.Magazine;
                frames.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{tick}|{player.Position}|{player.View.Forward}|{player.MeleeCharge.IsCharging}|{player.MeleeCharge.Fraction}"
                    + $"|{player.MeleeCooldown.RemainingFraction}|{magazine.Rounds}|{magazine.ReloadProgress}"
                    + $"|{string.Join(";", world.Targets.Select(target => target.Body.Position))}"
                    + $"|{string.Join(";", world.Doors.Select(door => door.OpenFraction))}"
                    + $"|{world.Gorm?.State}|{world.Gorm?.StateProgress}|{world.Gorm?.Shockwave}"
                    + $"|{string.Join(";", events)}"));
                events.Clear();
            },
            afterTick: () => events.AddRange(world.Events.Select(e => e.ToString())));
        return frames;
    }

    [Fact]
    public void Every_clip_the_issue_asks_for_is_there()
    {
        string[] wanted = ["blade-charge", "blade-quick", "pistol-shot", "pistol-reload", "door-normal-opening", "door-boss-opening",
            "pistol-shot-in-swing", "pistol-reload-in-swing", "pistol-reload-cancelled", "rifle-reload-to-pistol",
            "gorm-slam", "gorm-slam-far", "gorm-slam-far-b", "gorm-slam-far-c", "stairs-rising", "column-breaking",
            "loot-shimmer-near", "loot-shimmer-far"];

        Assert.Equal(wanted.Order(), MotionScenes.All.Select(scene => scene.Name).Order());
    }

    [Fact]
    public void Clip_names_are_unique_safe_as_folder_names_and_no_photo_scene()
    {
        var names = MotionScenes.All.Select(scene => scene.Name).ToList();

        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(names, name => Assert.Matches(new Regex(@"^[a-z0-9-]+\z"), name));
        Assert.DoesNotContain(LaunchOptions.AllPhotos, names);
        Assert.DoesNotContain(LaunchOptions.ListPhotos, names);
        Assert.All(MotionScenes.All, scene => Assert.False(string.IsNullOrWhiteSpace(scene.Description)));
        Assert.All(MotionScenes.All, scene => Assert.Null(PhotoScenes.Find(scene.Name)));
    }

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void The_same_clip_filmed_twice_gives_the_same_ticks_and_states(string name)
    {
        var first = Filmed(Scene(name));
        var second = Filmed(Scene(name));

        Assert.True(first.Count > 10, $"Only {first.Count} pictures.");
        Assert.Equal(first, second);
    }

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void A_clip_takes_a_picture_before_the_first_tick_and_then_evenly_apart(string name)
    {
        var scene = Scene(name);
        var setup = scene.Build(Studio());

        var ticks = setup.Film(scene.FrameEvery, _ => { });

        Assert.Equal(0, ticks[0]);
        Assert.All(ticks.Zip(ticks.Skip(1)), pair => Assert.Equal(scene.FrameEvery, pair.Second - pair.First));
        Assert.Equal(setup.Ticks - (setup.Ticks % scene.FrameEvery), ticks[^1]);
        Assert.False(setup.World.Player.IsDead);
    }

    [Fact]
    public void Film_calls_the_picture_after_the_tick_and_its_reading()
    {
        var setup = new PhotoSetup(Studio().Arena('D', 10)) { Ticks = 5 };
        var log = new List<string>();

        var ticks = setup.Film(2, tick => log.Add($"frame {tick}"), () => log.Add("tick"));

        Assert.Equal([0, 2, 4], ticks);
        Assert.Equal(["frame 0", "tick", "tick", "frame 2", "tick", "tick", "frame 4", "tick"], log);
    }

    [Fact]
    public void Film_stops_where_the_scene_stops()
    {
        var run = 0;
        var setup = new PhotoSetup(Studio().Arena('D', 10)) { Ticks = 100, StopWhen = _ => ++run >= 7 };

        var ticks = setup.Film(3, _ => { });

        Assert.Equal([0, 3, 6], ticks);
        Assert.Equal(7, run);
    }

    [Fact]
    public void Film_needs_a_step_of_at_least_one_tick()
    {
        var setup = new PhotoSetup(Studio().Arena('D', 10));

        Assert.Throws<ArgumentOutOfRangeException>(() => setup.Film(0, _ => { }));
    }

    [Fact]
    public void The_charge_clip_charges_fully_swings_and_ends_at_rest()
    {
        var setup = Scene("blade-charge").Build(Studio());
        var charged = false;
        var swung = false;

        setup.Film(1, _ =>
        {
            charged |= setup.World.Player.MeleeCharge is { IsCharging: true, Fraction: >= 1f };
            swung |= setup.World.Events.OfType<MeleeSwung>().Any();
        });

        Assert.True(charged);
        Assert.True(swung);
        Assert.False(setup.World.Player.MeleeCharge.IsCharging);
        Assert.True(setup.World.Player.MeleeCooldown.IsReady);
    }

    [Fact]
    public void The_reload_clip_starts_with_a_round_missing_and_ends_with_a_full_magazine()
    {
        var setup = Scene("pistol-reload").Build(Studio());
        var magazine = setup.World.Player.Ranged.Magazine;
        Assert.True(magazine.Rounds < magazine.Size);
        var reloading = false;

        setup.Film(1, _ => reloading |= magazine.IsReloading);

        Assert.True(reloading);
        Assert.False(magazine.IsReloading);
        Assert.Equal(magazine.Size, magazine.Rounds);
    }

    [Fact]
    public void The_shot_clip_fires_one_shot()
    {
        var setup = Scene("pistol-shot").Build(Studio());
        var magazine = setup.World.Player.Ranged.Magazine;
        var full = magazine.Rounds;

        setup.Film(1, _ => { });

        Assert.Equal(full - 1, magazine.Rounds);
    }

    [Theory]
    [InlineData("door-normal-opening", DoorKind.Normal)]
    [InlineData("door-boss-opening", DoorKind.Boss)]
    public void A_door_clip_shows_the_whole_swing_from_nearly_closed_to_open(string name, DoorKind kind)
    {
        var setup = Scene(name).Build(Studio());
        var door = setup.World.Doors.Single();
        Assert.Equal(kind, door.Kind);
        var fractions = new List<float>();

        setup.Film(1, _ => fractions.Add(door.OpenFraction));

        Assert.True(fractions[0] < 0.05f, $"The door starts {fractions[0]} open.");
        Assert.Equal(1f, fractions[^1]);
        Assert.Equal(fractions.Order(), fractions);
    }

    // Issue #601: the breaking column seen from about 6 m with nothing over it - no hands, title or labels, and no
    // ceiling between the eye and its top (from the passage south of the hall its lower ceiling cut the column off).
    [Fact]
    public void The_column_clip_sees_the_whole_column_from_6_m_with_nothing_over_it()
    {
        var world = Scene("column-breaking").Build(Studio()).World;
        var column = world.Showroom!.Columns.Single(column => column.Stage == ColumnStage.Falling);
        var eye = world.Player.EyePosition;
        var top = column.Position + new Vector3(0f, RoomTemplate.RoomCeilingHeight - 0.1f, 0f);

        var distance = Vector2.Distance(new Vector2(eye.X, eye.Z), new Vector2(column.Position.X, column.Position.Z));
        Assert.InRange(distance, 5.5f, 6.5f);
        for (var step = 0; step <= 100; step++)
        {
            var point = Vector3.Lerp(eye, top, step / 100f);
            var (x, z) = Level.ToGrid(point.X, point.Z);
            Assert.True(world.Level[x, z].CeilingHeight >= point.Y, $"The ceiling of cell {x},{z} hides the column's top at {point}.");
        }
    }

    [Fact]
    public void Only_the_column_clip_hides_hands_title_and_labels()
    {
        var hiding = MotionScenes.All.Where(scene => scene.Build(Studio()).ShowsOnlyTheWorld).Select(scene => scene.Name);

        Assert.Equal(["column-breaking"], hiding);
    }
}
