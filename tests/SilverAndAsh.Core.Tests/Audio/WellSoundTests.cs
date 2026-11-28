using System.Numerics;
using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Tests.Gameplay;

namespace SilverAndAsh.Core.Tests.Audio;

/// <summary>
/// Issue #815: Sirius' Relief is heard - the coin at every throw, the luck only when the well gives, and its water as a
/// loop from where it stands, only close by. Played the way the game plays it: tick by tick from the world's events,
/// and the loop from the state of the world. The files are the three takes the human chose in the listening test of
/// issue #438 (throw "A", luck "A", water "C").
/// </summary>
public class WellSoundTests
{
    private const string ThrowFile = "brunnen-wurf.wav";
    private const string LuckFile = "brunnen-glueck.wav";
    private const string WaterFile = "brunnen-wasser.wav";
    private const int Bystander = 7;
    private const int SampleRate = 44_100;
    private const float Tolerance = 1e-5f;

    private static readonly WellBalance Well = Balance.Default.Well;

    private static bool IsWellSound(SoundCue cue) => cue.Kind is SoundKind.WellThrow or SoundKind.WellLuck;

    // Where the water of the corridor's well sounds from: the middle of its height.
    private static Vector3 WaterPlace(GameWorld world) => world.Well!.Position + new Vector3(0f, WellOfSirius.Height / 2f, 0f);

    // A listener at the water's height, so far to the +X side of it, looking along +Z (the right is -X, so the well is on the right).
    private static SoundListener Aside(GameWorld world, float metres) =>
        new(Bystander, WaterPlace(world) + new Vector3(metres, 0f, 0f), Vector3.UnitZ);

    private static LoopCue Water(GameWorld world, SoundListener listener) =>
        Assert.Single(SoundLoops.For(world, listener), cue => cue.File == WaterFile);

    [Fact]
    public void The_throw_and_the_luck_each_ask_for_their_own_sound()
    {
        Assert.Equal(SoundKind.WellThrow, SoundBoard.KindFor(new WellOfSiriusUsed(0)));
        Assert.Equal(SoundKind.WellLuck, SoundBoard.KindFor(new WellOfSiriusGave(0)));
    }

    [Fact]
    public void Pack_1_has_a_file_for_each_and_the_old_pack_none()
    {
        Assert.Equal([ThrowFile], SoundPack.Pack1.Variants(SoundKind.WellThrow).ToArray());
        Assert.Equal([LuckFile], SoundPack.Pack1.Variants(SoundKind.WellLuck).ToArray());
        Assert.True(SoundPack.Old.Variants(SoundKind.WellThrow).IsEmpty);
        Assert.True(SoundPack.Old.Variants(SoundKind.WellLuck).IsEmpty);
        Assert.Contains(ThrowFile, SoundBoard.SoundFiles);
        Assert.Contains(LuckFile, SoundBoard.SoundFiles);
    }

    [Fact]
    public void The_water_is_a_loop_the_game_loads_and_no_short_sound()
    {
        Assert.Equal(WaterFile, SoundLoops.WellWater);
        Assert.Contains(WaterFile, SoundLoops.Files);
        Assert.Contains(WaterFile, SoundBoard.AllFiles);
        Assert.DoesNotContain(WaterFile, SoundBoard.SoundFiles);
    }

    [Theory]
    [InlineData(WellOfSiriusTests.LuckySeed, new[] { ThrowFile, LuckFile })]
    [InlineData(WellOfSiriusTests.UnluckySeed, new[] { ThrowFile })]
    public void A_throw_plays_the_coin_and_only_a_lucky_one_the_luck(int seed, string[] expected)
    {
        var world = WellOfSiriusTests.Corridor(seed: seed);
        var cues = new SoundCues(world.Seed);
        var heard = new List<(int Tick, SoundCue Cue)>();
        var tick = 0;

        void Play(PlayerInput input, int ticks)
        {
            for (var i = 0; i < ticks; i++, tick++)
            {
                world.Tick(input);
                var played = cues.For(world.Events, SoundPack.Pack1, SoundListener.Of(world.Player), world.Balance.Hearing);
                heard.AddRange(played.Where(IsWellSound).Select(cue => (tick, cue)));
            }
        }

        // Walking up to the well is silent; F throws; holding F and waiting afterwards adds nothing.
        Play(PlayerInput.None with { Move = new Vector2(0f, 1f) }, 3 * SimulationClock.TicksPerSecond);
        Play(PlayerInput.None, SimulationClock.TicksPerSecond / 2);
        world.Player.Look(PlayerInput.None with { Look = new Vector2(0f, 20f * MathF.PI / 180f / ControlSettings.MouseSensitivity) });
        Assert.Empty(heard);
        var thrownAt = tick;
        Play(WellOfSiriusTests.PressF, 1);
        Assert.True(world.Well!.IsSpent, "The well was never used - the test would prove nothing.");
        Play(PlayerInput.None, 1);
        Play(WellOfSiriusTests.PressF, 1);
        Play(PlayerInput.None, 2 * SimulationClock.TicksPerSecond);

        Assert.Equal(expected, heard.Select(entry => entry.Cue.File).ToArray());
        Assert.All(heard, entry => Assert.Equal(thrownAt, entry.Tick));

        // Standing at the well, both are heard at practically full volume, from the middle (the well is straight ahead).
        Assert.All(heard, entry =>
        {
            Assert.InRange(entry.Cue.Volume, 0.99f * SoundBoard.SoundVolume, SoundBoard.SoundVolume);
            Assert.Equal(0f, entry.Cue.Pan, Tolerance);
        });
    }

    [Fact]
    public void A_throw_far_away_is_heard_quieter_and_from_its_side_and_not_at_all_out_of_range()
    {
        var place = new Vector3(10f, 0f, 20f);
        GameEvent[] events = [new WellOfSiriusUsed(3) { Place = place }, new WellOfSiriusGave(3) { Place = place }];
        var hearing = Balance.Default.Hearing;
        var ears = place + new Vector3((hearing.NearDistance + hearing.Range) / 2f, 0f, 0f);

        var cues = new SoundCues(1).For(events, SoundPack.Pack1, new SoundListener(Bystander, ears, Vector3.UnitZ), hearing);

        Assert.Equal([ThrowFile, LuckFile], cues.Select(cue => cue.File).ToArray());
        Assert.All(cues, cue =>
        {
            Assert.True(cue.Volume is > 0f and < SoundBoard.SoundVolume, $"volume {cue.Volume}");
            Assert.Equal(hearing.MaxPan, cue.Pan, Tolerance); // looking along +Z the right is -X: the well is on the right
        });

        var far = place + new Vector3(hearing.Range + 5f, 0f, 0f);
        Assert.Empty(new SoundCues(1).For(events, SoundPack.Pack1, new SoundListener(Bystander, far, Vector3.UnitZ), hearing));
    }

    [Fact]
    public void Close_to_a_ready_well_its_water_runs_at_its_full_volume()
    {
        var world = WellOfSiriusTests.Corridor();

        // The player at the start of the corridor is 6 m from the well: in range, but not close.
        var fromTheStart = Water(world, SoundListener.Of(world.Player));
        Assert.True(fromTheStart.Volume is > 0f and < 0.5f, $"volume {fromTheStart.Volume}");

        // Standing before it, as in the game: full volume.
        WellOfSiriusTests.StandBeforeWell(world);
        Assert.Equal(Well.WaterVolume, Water(world, SoundListener.Of(world.Player)).Volume, Tolerance);
        Assert.Equal(Well.WaterVolume, Water(world, Aside(world, Balance.Default.Hearing.NearDistance)).Volume, Tolerance);
    }

    [Fact]
    public void The_water_fades_with_the_distance_and_ends_at_its_own_range()
    {
        var world = WellOfSiriusTests.Corridor();
        var hearing = Balance.Default.Hearing;
        Assert.True(Well.WaterRange < hearing.Range, "The water is a sound of the close range.");

        var last = Well.WaterVolume;
        for (var metres = hearing.NearDistance + 1f; metres < Well.WaterRange; metres += 1f)
        {
            var listener = Aside(world, metres);
            var cue = Water(world, listener);
            var expected = Well.WaterVolume
                * Hearing.Mix(listener.Ears, listener.Facing, WaterPlace(world), hearing with { Range = Well.WaterRange }).Volume;

            Assert.Equal(expected, cue.Volume, Tolerance);
            Assert.True(cue.Volume > 0f && cue.Volume < last, $"{metres} m: volume {cue.Volume} after {last}");
            last = cue.Volume;
        }

        // At the range and beyond it is silent - where a short sound of the well would still be heard.
        foreach (var metres in new[] { Well.WaterRange, Well.WaterRange + 0.5f, hearing.Range })
        {
            Assert.Equal(new LoopCue(WaterFile, 0f, 0f), Water(world, Aside(world, metres)));
        }

        var justOutside = Aside(world, Well.WaterRange + 0.5f);
        Assert.True(Hearing.Mix(justOutside.Ears, justOutside.Facing, WaterPlace(world), hearing).Volume > 0f);
    }

    [Fact]
    public void The_range_and_the_volume_of_the_water_come_from_the_balancing_values()
    {
        var quietAndWide = Balance.Default with { Well = Well with { WaterRange = 2f * Well.WaterRange, WaterVolume = 0.5f * Well.WaterVolume } };
        var world = WellOfSiriusTests.Corridor(quietAndWide);

        Assert.Equal(0.5f * Well.WaterVolume, Water(world, Aside(world, 1f)).Volume, Tolerance);
        Assert.True(Water(world, Aside(world, Well.WaterRange + 0.5f)).Volume > 0f);

        var silent = Balance.Default with { Well = Well with { WaterVolume = 0f } };
        Assert.Equal(new LoopCue(WaterFile, 0f, 0f), Water(WellOfSiriusTests.Corridor(silent), Aside(world, 1f)));
    }

    [Fact]
    public void The_water_is_heard_from_the_side_the_well_is_on()
    {
        var world = WellOfSiriusTests.Corridor();
        var maxPan = Balance.Default.Hearing.MaxPan;

        // Looking along +Z the right is -X: from +X the well is on the right, from -X on the left, ahead in the middle.
        Assert.Equal(maxPan, Water(world, Aside(world, 4f)).Pan, Tolerance);
        Assert.Equal(-maxPan, Water(world, Aside(world, -4f)).Pan, Tolerance);
        var before = new SoundListener(Bystander, WaterPlace(world) - new Vector3(0f, 0f, 4f), Vector3.UnitZ);
        Assert.Equal(0f, Water(world, before).Pan, Tolerance);
    }

    [Fact]
    public void A_spent_well_is_dry_and_silent_and_a_restarted_one_runs_again()
    {
        var world = WellOfSiriusTests.Corridor();
        WellOfSiriusTests.StandBeforeWell(world);
        Assert.True(Water(world, SoundListener.Of(world.Player)).Volume > 0f);

        world.Tick(WellOfSiriusTests.PressF);

        Assert.True(world.Well!.IsSpent);
        Assert.Equal(new LoopCue(WaterFile, 0f, 0f), Water(world, SoundListener.Of(world.Player)));
        Assert.True(Water(world.Restarted(), Aside(world, 1f)).Volume > 0f);
    }

    [Fact]
    public void A_world_without_a_well_and_no_world_at_all_keep_every_loop_silent()
    {
        var world = new GameWorld(SilverAndAsh.Core.World.LevelParser.Parse("#P#\n#.#\n#.#\n#.#"), 1, Balance.Default);
        var silent = SoundLoops.Files.Select(file => new LoopCue(file, 0f, 0f)).ToArray();

        Assert.Equal(silent, SoundLoops.For(world, SoundListener.Of(world.Player)));
        Assert.Equal(silent, SoundLoops.Silent);
        Assert.NotEmpty(silent);
    }

    [Fact]
    public void A_loop_fades_in_and_out_instead_of_jumping()
    {
        var fader = new LoopFader();
        LoopCue[] on = [new(WaterFile, 0.8f, -0.5f)]; // the well on the left
        LoopCue[] off = [new(WaterFile, 0f, 0f)];
        var quarter = LoopFader.FadeSeconds / 4f;

        // In: a quarter of the full way per quarter of the fade time, never past the wish.
        Assert.Equal(new LoopCue(WaterFile, 0.25f, -0.5f), Assert.Single(fader.Step(on, quarter)));
        Assert.Equal(0.5f, fader.Step(on, quarter)[0].Volume, Tolerance);
        Assert.Equal(0.75f, fader.Step(on, quarter)[0].Volume, Tolerance);
        Assert.Equal(0.8f, fader.Step(on, quarter)[0].Volume, Tolerance);
        Assert.Equal(0.8f, fader.Step(on, quarter)[0].Volume, Tolerance);

        // Out (the well is spent): down at the same pace, and it stays on the side it was last heard from.
        var fading = fader.Step(off, quarter)[0];
        Assert.Equal(0.55f, fading.Volume, Tolerance);
        Assert.Equal(-0.5f, fading.Pan);
        Assert.Equal(0.55f, fader.Step(off, 0f)[0].Volume, Tolerance); // no time, no change (the world is held)
        Assert.Equal(0f, fader.Step(off, LoopFader.FadeSeconds)[0].Volume);
        Assert.Equal(0f, fader.Step(off, quarter)[0].Volume);
    }

    // Frames of the game at the well: what the fader makes of the wishes, frame by frame.
    private static LoopCue Frame(LoopFader fader, IReadOnlyList<LoopCue> wishes, bool held = false) =>
        Assert.Single(fader.Step(wishes, 1f / SimulationClock.TicksPerSecond, held), cue => cue.File == WaterFile);

    private static (GameWorld World, LoopFader Fader, LoopCue AtTheWell) HeardAtTheWell()
    {
        var world = WellOfSiriusTests.Corridor();
        WellOfSiriusTests.StandBeforeWell(world);
        var fader = new LoopFader();
        var cue = default(LoopCue);
        for (var frame = 0; frame < SimulationClock.TicksPerSecond; frame++)
        {
            cue = Frame(fader, SoundLoops.For(world, SoundListener.Of(world.Player)));
        }

        Assert.Equal(Well.WaterVolume, cue.Volume, Tolerance);
        return (world, fader, cue);
    }

    [Fact]
    public void Leaving_the_world_from_the_menu_the_water_is_silent_from_the_first_frame()
    {
        // Tester's finding on PR #817: at the well, Esc, then "Title" - the water came back for the 0.4 s of its fade.
        var (world, fader, atTheWell) = HeardAtTheWell();

        // The Esc menu: the world is held, the game still asks for the world's loops; the fader keeps the volume.
        for (var frame = 0; frame < 30; frame++)
        {
            Assert.Equal(atTheWell, Frame(fader, SoundLoops.For(world, SoundListener.Of(world.Player)), held: true));
        }

        // "Title": no world any more, and no hold. Silent in this very frame and after.
        Assert.Equal(0f, Frame(fader, SoundLoops.Silent).Volume);
        Assert.Equal(0f, Frame(fader, SoundLoops.Silent).Volume);
    }

    [Fact]
    public void Back_from_the_menu_into_the_world_the_water_goes_on_as_loud_as_before()
    {
        var (world, fader, atTheWell) = HeardAtTheWell();
        for (var frame = 0; frame < 30; frame++)
        {
            Frame(fader, SoundLoops.For(world, SoundListener.Of(world.Player)), held: true);
        }

        Assert.Equal(atTheWell, Frame(fader, SoundLoops.For(world, SoundListener.Of(world.Player))));
    }

    [Fact]
    public void Without_a_hold_a_loop_that_ends_still_fades_out()
    {
        // The well is spent or the run ends while the water sounds: it was audible, so it fades instead of clicking.
        var (_, fader, atTheWell) = HeardAtTheWell();

        var first = Frame(fader, SoundLoops.Silent);

        Assert.True(first.Volume > 0f && first.Volume < atTheWell.Volume, $"volume {first.Volume}");
        for (var frame = 0; frame < (int)(LoopFader.FadeSeconds * SimulationClock.TicksPerSecond) + 1; frame++)
        {
            first = Frame(fader, SoundLoops.Silent);
        }

        Assert.Equal(0f, first.Volume);
    }

    [Fact]
    public void A_loop_that_is_only_further_away_follows_at_once_within_the_fade_pace()
    {
        var fader = new LoopFader();
        fader.Step([new LoopCue(WaterFile, 1f, 0.2f)], LoopFader.FadeSeconds);

        // Walking: the wish changes a little per frame, far less than the fade allows - volume and side follow exactly.
        var next = fader.Step([new LoopCue(WaterFile, 0.98f, 0.3f)], 1f / 60f)[0];

        Assert.Equal(new LoopCue(WaterFile, 0.98f, 0.3f), next);
    }

    [Theory]
    [InlineData(ThrowFile, 0.5f, 1.5f)]
    [InlineData(LuckFile, 2f, 3.5f)]
    [InlineData(WaterFile, 12f, 12f)]
    public void The_files_are_mono_16_bit_of_the_chosen_length_and_not_overloaded(string file, float minSeconds, float maxSeconds)
    {
        var wav = GrateSoundTests.ReadWav(Path.Combine(RepositoryFiles.Find("assets/audio"), file));

        Assert.Equal(1, wav.Channels);
        Assert.Equal(16, wav.BitsPerSample);
        Assert.Equal(SampleRate, wav.SampleRate);
        Assert.InRange(wav.Samples.Length / (float)SampleRate, minSeconds, maxSeconds);

        var peak = wav.Samples.Max(sample => Math.Abs((int)sample)) / 32768f;
        Assert.True(peak is > 0.05f and <= 0.891f, $"peak {peak}");
    }

    [Fact]
    public void The_short_sounds_start_and_end_without_a_click_and_the_water_closes_its_loop()
    {
        foreach (var file in new[] { ThrowFile, LuckFile })
        {
            var wav = GrateSoundTests.ReadWav(Path.Combine(RepositoryFiles.Find("assets/audio"), file));
            Assert.True(Math.Abs((int)wav.Samples[0]) < 33 && Math.Abs((int)wav.Samples[^1]) < 33, $"{file}: click at the start or end");
        }

        // The loop: the step from the last sample back to the first is no bigger than the steps inside the file, and
        // the water is the quietest of the three (it runs all the time, the others are moments).
        var water = GrateSoundTests.ReadWav(Path.Combine(RepositoryFiles.Find("assets/audio"), WaterFile)).Samples;
        var biggestStepInside = Enumerable.Range(1, water.Length - 1).Max(i => Math.Abs(water[i] - water[i - 1]));
        Assert.True(Math.Abs(water[0] - water[^1]) <= biggestStepInside, "The loop has a step at its seam.");

        var waterPeak = water.Max(sample => Math.Abs((int)sample));
        var throwPeak = GrateSoundTests.ReadWav(Path.Combine(RepositoryFiles.Find("assets/audio"), ThrowFile)).Samples.Max(sample => Math.Abs((int)sample));
        Assert.True(waterPeak < throwPeak / 2, $"water peak {waterPeak}, throw peak {throwPeak}");
    }
}
