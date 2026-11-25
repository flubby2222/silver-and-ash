using System.Numerics;
using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Tests.Gameplay;

namespace SilverAndAsh.Core.Tests.Audio;

/// <summary>
/// Issue #558: the portcullis is heard - iron sliding down at an arena door when the room seals, sliding up when it is
/// cleared - played the way the game plays it: tick by tick from the world's events, placed for the listening player.
/// The files are the two takes the human chose in round 2 of the listening test of issue #540.
/// </summary>
public class GrateSoundTests
{
    private const string FallingFile = "fallgitter-fallen.wav";
    private const string RisingFile = "fallgitter-heben.wav";
    private const int Bystander = 7;
    private const float Tolerance = 1e-5f;

    // The take "1 - without chain" is built for the fall of 63 ticks (stalls at 12 and 26, lands at 47) and the take
    // "2 - smoother" for the rise of 126 ticks (knock at the end); sample rate of every file of the game.
    private const int BuiltForFallTicks = 63;
    private const int BuiltForLiftTicks = 126;
    private const int SampleRate = 44_100;

    // One tick of the world and the sounds the listening player hears from it.
    private static IReadOnlyList<SoundCue> Tick(GameWorld world, SoundCues cues, PlayerInput input)
    {
        world.Tick(input);
        return cues.For(world.Events, SoundPack.Pack1, SoundListener.Of(world.Player), world.Balance.Hearing);
    }

    private static bool IsGrateSound(SoundCue cue) => cue.Kind is SoundKind.GrateFalling or SoundKind.GrateRising;

    [Fact]
    public void Each_direction_of_the_grate_asks_for_its_own_sound()
    {
        Assert.Equal(SoundKind.GrateFalling, SoundBoard.KindFor(new GrateStarted(0, Falling: true)));
        Assert.Equal(SoundKind.GrateRising, SoundBoard.KindFor(new GrateStarted(0, Falling: false)));
    }

    [Fact]
    public void Pack_1_has_a_file_for_each_direction_and_the_old_pack_none()
    {
        Assert.Equal([FallingFile], SoundPack.Pack1.Variants(SoundKind.GrateFalling).ToArray());
        Assert.Equal([RisingFile], SoundPack.Pack1.Variants(SoundKind.GrateRising).ToArray());
        Assert.True(SoundPack.Old.Variants(SoundKind.GrateFalling).IsEmpty);
        Assert.True(SoundPack.Old.Variants(SoundKind.GrateRising).IsEmpty);
        Assert.Contains(FallingFile, SoundBoard.SoundFiles);
        Assert.Contains(RisingFile, SoundBoard.SoundFiles);
    }

    [Fact]
    public void A_sealing_arena_plays_the_falling_sound_once_and_a_cleared_one_the_rising_sound_once()
    {
        var world = GrateTests.World();
        var cues = new SoundCues(world.Seed);
        var heard = new List<(int Tick, SoundCue Cue)>();
        var tick = 0;

        // Open door A and walk in until the room seals.
        for (; tick < 1 + (3 * SimulationClock.TicksPerSecond) && !world.Rooms[1].IsSealed; tick++)
        {
            var played = Tick(world, cues, tick == 0 ? GrateTests.PressF : GrateTests.Forward);
            heard.AddRange(played.Where(IsGrateSound).Select(cue => (tick, cue)));
        }

        Assert.True(world.Rooms[1].IsSealed, "The arena never sealed - the test would prove nothing.");
        var sealedAt = tick - 1;

        // Both doors of the arena have a grate and fall in the sealing tick, with the same file: one sound, not two.
        var (atTick, falling) = Assert.Single(heard);
        Assert.Equal(sealedAt, atTick);
        Assert.Equal(SoundKind.GrateFalling, falling.Kind);
        Assert.Equal(FallingFile, falling.File);

        // The fall runs on without a second sound; clearing the room starts the rise.
        heard.Clear();
        for (var i = 0; i < BuiltForFallTicks; i++)
        {
            heard.AddRange(Tick(world, cues, PlayerInput.None).Where(IsGrateSound).Select(cue => (tick + i, cue)));
        }

        Assert.Empty(heard);
        BossDoorTests.KillEverything(world);
        for (var i = 0; i < BuiltForLiftTicks + 1; i++)
        {
            heard.AddRange(Tick(world, cues, PlayerInput.None).Where(IsGrateSound).Select(cue => (i, cue)));
        }

        var (_, rising) = Assert.Single(heard);
        Assert.Equal(SoundKind.GrateRising, rising.Kind);
        Assert.Equal(RisingFile, rising.File);
    }

    [Theory]
    [InlineData(true, SoundKind.GrateFalling)]
    [InlineData(false, SoundKind.GrateRising)]
    public void A_listener_beside_the_door_hears_the_grate_from_that_side(bool falling, SoundKind kind)
    {
        var middle = new Vector3(10f, 1f, 20f);
        GameEvent[] events = [new GrateStarted(3, falling) { Place = middle }];
        var hearing = Balance.Default.Hearing;
        var aside = new Vector3((hearing.NearDistance + hearing.Range) / 2f, 0f, 0f);

        // Level with the door's middle, looking through it (+Z, so the right is -X): the one at -X has the door on the
        // left, the one at +X on the right; each at the volume of that distance.
        foreach (var (ears, pan) in new[] { (middle - aside, -hearing.MaxPan), (middle + aside, hearing.MaxPan) })
        {
            var listener = new SoundListener(Bystander, ears, Vector3.UnitZ);
            var cue = Assert.Single(new SoundCues(1).For(events, SoundPack.Pack1, listener, hearing));
            var volume = SoundBoard.SoundVolume * Hearing.Mix(ears, Vector3.UnitZ, middle, hearing).Volume;

            Assert.Equal(kind, cue.Kind);
            Assert.Equal(pan, cue.Pan, Tolerance);
            Assert.Equal(volume, cue.Volume, Tolerance);
            Assert.True(cue.Volume is > 0f and < SoundBoard.SoundVolume, $"volume {cue.Volume}");
        }
    }

    [Fact]
    public void A_grate_beyond_the_hearing_range_is_not_heard()
    {
        var middle = new Vector3(10f, 1f, 20f);
        GameEvent[] events = [new GrateStarted(3, true) { Place = middle }];
        var hearing = Balance.Default.Hearing;
        var far = middle + new Vector3(hearing.Range + 5f, 0f, 0f);

        Assert.Empty(new SoundCues(1).For(events, SoundPack.Pack1, new SoundListener(Bystander, far, Vector3.UnitZ), hearing));
    }

    [Fact]
    public void The_files_are_built_for_the_fall_and_the_rise_the_balancing_gives_the_grates()
    {
        // The fall sounds stall at ticks 12 and 26 and land at 47; the rise ends at tick 126 - all counted from the
        // start of the motion. A balancing round that changes the ticks must rebuild and re-listen to the sounds
        // (tools/audio/fallgitterklang.py), so this goes red until then.
        Assert.Equal(BuiltForFallTicks, Balance.Default.Grates.FallTicks);
        Assert.Equal(BuiltForLiftTicks, Balance.Default.Grates.LiftTicks);
    }

    [Theory]
    [InlineData(FallingFile, BuiltForFallTicks)]
    [InlineData(RisingFile, BuiltForLiftTicks)]
    public void The_files_are_mono_16_bit_long_enough_for_the_motion_and_quiet_at_both_ends(string file, int ticks)
    {
        var wav = ReadWav(Path.Combine(RepositoryFiles.Find("assets/audio"), file));

        Assert.Equal(1, wav.Channels);
        Assert.Equal(16, wav.BitsPerSample);
        Assert.Equal(SampleRate, wav.SampleRate);

        // The motion plus its after-sound (0.7 s after the fall, 0.8 s after the rise in the take).
        Assert.True(wav.Samples.Length >= ticks * SampleRate / SimulationClock.TicksPerSecond);

        // No overload (the peak keeps a dB of room) and no click at the start or the end.
        var peak = wav.Samples.Max(sample => Math.Abs(sample)) / 32768f;
        Assert.True(peak is > 0.1f and <= 0.891f, $"peak {peak}");
        Assert.True(Math.Abs(wav.Samples[0]) < 33 && Math.Abs(wav.Samples[^1]) < 33, "click at the start or end");
    }

    private sealed record WavFile(int Channels, int SampleRate, int BitsPerSample, short[] Samples);

    // A canonical RIFF/WAVE file: "fmt " chunk, then "data" with 16-bit samples.
    private static WavFile ReadWav(string path)
    {
        var bytes = File.ReadAllBytes(path);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(bytes, 8, 4));
        int channels = 0, rate = 0, bits = 0;
        short[] samples = [];
        for (var at = 12; at + 8 <= bytes.Length;)
        {
            var id = System.Text.Encoding.ASCII.GetString(bytes, at, 4);
            var size = BitConverter.ToInt32(bytes, at + 4);
            if (id == "fmt ")
            {
                channels = BitConverter.ToInt16(bytes, at + 10);
                rate = BitConverter.ToInt32(bytes, at + 12);
                bits = BitConverter.ToInt16(bytes, at + 22);
            }
            else if (id == "data")
            {
                samples = new short[size / 2];
                Buffer.BlockCopy(bytes, at + 8, samples, 0, size);
            }

            at += 8 + size + (size & 1);
        }

        return new WavFile(channels, rate, bits, samples);
    }
}
