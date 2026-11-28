using System.Numerics;
using System.Security.Cryptography;
using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Audio;

/// <summary>
/// Issue #820: the throwing knives are heard - the throw, the knife sticking in a target and the knife sticking in
/// stone, each with the take the human chose in the listening test of issue #204 (throw "A", target "A", stone "B").
/// Played the way the game plays it: tick by tick from the world's events, with the balancing values of the game. A
/// knife at the end of its range has no event and so no sound (issue #816). Since issue #822 a knife glances off stone
/// before it sticks there: every ricochet is heard with the stone's sound, from the place it glanced off.
/// </summary>
public class ThrowingKnivesSoundTests
{
    private const string ThrowFile = "wurfmesser-wurf.wav";
    private const string TargetFile = "wurfmesser-treffer-gegner.wav";
    private const string StoneFile = "wurfmesser-treffer-stein.wav";
    private const string Script = "tools/audio/messerklang.py";
    private const int Bystander = 7;
    private const int SampleRate = 44_100;
    private const float Tolerance = 1e-5f;

    private static readonly HearingBalance Ears = Balance.Default.Hearing;

    /// <summary>How often a knife is heard on stone: at every ricochet the game allows, and where it sticks.</summary>
    private static readonly int StoneSounds = Balance.Default.KnifeFlight.Ricochets + 1;
    private static readonly PlayerInput Shoot = PlayerInput.None with { Shoot = true };

    /// <summary>What was heard in one tick (the throw is tick 1), and the knife that stuck in that tick, if one did.</summary>
    private sealed record Heard(int Tick, SoundCue Cue, SoundListener Listener, KnifeStuck? Stuck);

    private static bool IsKnifeSound(SoundCue cue) => cue.Kind is SoundKind.KnifeThrow or SoundKind.KnifeHit or SoundKind.KnifeStoneHit;

    /// <summary>A one-cell corridor along +Z from the given rows; the player looks along it, the knives in hand.</summary>
    private static GameWorld World(string map)
    {
        var world = new GameWorld(LevelParser.Parse(map), 1, Balance.Default);
        world.Player.Equip(0, WeaponKind.ThrowingKnives);
        return world;
    }

    /// <summary>The corridor with <paramref name="free"/> empty cells between the player and <paramref name="end"/>.</summary>
    private static string Corridor(int free, string end) => "P\n" + string.Concat(Enumerable.Repeat(".\n", free)) + end;

    /// <summary>
    /// Turns the view (like the mouse would) to the middle of the body of the one target of the corridor, if it has one:
    /// a ghoul crawls below the height of the eyes, a knife thrown level flies over it.
    /// </summary>
    private static void AimAtTheTarget(GameWorld world)
    {
        if (world.Targets.SingleOrDefault() is not { } target)
        {
            return;
        }

        var player = world.Player;
        var middle = target.Body.Position + new Vector3(0f, target.Body.Height / 2f, 0f);
        var direction = Vector3.Normalize(middle - player.EyePosition);
        var pixels = new Vector2(
            -(MathF.Atan2(direction.X, direction.Z) - player.View.YawRadians) / ControlSettings.MouseSensitivity,
            -(MathF.Asin(direction.Y) - player.View.PitchRadians) / ControlSettings.MouseSensitivity);
        player.Look(PlayerInput.None with { Look = pixels });
    }

    /// <summary>
    /// One throw at the target (or along the corridor), then <paramref name="ticks"/> ticks in all without input: every knife sound the thrower hears, with
    /// its tick, and every <see cref="KnifeStuck"/> of the run.
    /// </summary>
    private static (List<Heard> Heard, List<KnifeStuck> Stuck) ThrowAndListen(GameWorld world, int ticks)
    {
        var cues = new SoundCues(world.Seed);
        var heard = new List<Heard>();
        var stuck = new List<KnifeStuck>();
        AimAtTheTarget(world);
        for (var tick = 1; tick <= ticks; tick++)
        {
            world.Tick(tick == 1 ? Shoot : PlayerInput.None);
            var listener = SoundListener.Of(world.Player);
            var stuckNow = world.Events.OfType<KnifeStuck>().SingleOrDefault();
            if (stuckNow is not null)
            {
                stuck.Add(stuckNow);
            }

            heard.AddRange(cues.For(world.Events, SoundPack.Pack1, listener, world.Balance.Hearing)
                .Where(IsKnifeSound)
                .Select(cue => new Heard(tick, cue, listener, stuckNow)));
        }

        return (heard, stuck);
    }

    [Fact]
    public void The_throw_and_the_knife_in_a_target_and_in_stone_each_ask_for_their_own_sound()
    {
        var point = new Vector3(1f, 2f, 3f);

        Assert.Equal(SoundKind.KnifeThrow, SoundBoard.KindFor(new ShotFired(0, point, Vector3.UnitZ, Weapon: WeaponKind.ThrowingKnives)));
        Assert.Equal(SoundKind.KnifeHit, SoundBoard.KindFor(new KnifeStuck(1, 0, WeaponKind.ThrowingKnives, point, Vector3.UnitZ, 4)));
        Assert.Equal(SoundKind.KnifeStoneHit, SoundBoard.KindFor(new KnifeStuck(1, 0, WeaponKind.ThrowingKnives, point, Vector3.UnitZ)));
    }

    [Fact]
    public void One_knife_makes_one_sound_where_it_sticks()
    {
        // The hit on the target and the mark on the stone come with the knife; they have no sound of their own.
        Assert.Null(SoundBoard.KindFor(new BulletImpact(Vector3.Zero, Vector3.UnitZ, WeaponKind.ThrowingKnives)));
        Assert.Null(SoundBoard.KindFor(new TargetHitBy(4, 0, Weapon.ThrowingKnives)));
        Assert.Null(SoundBoard.KindFor(new TargetHitBy(4, 0, Weapon.ThrowingKnives) { Hard = true }));

        // Something else that is thrown one day does not sound like a knife.
        Assert.Null(SoundBoard.KindFor(new KnifeStuck(1, 0, WeaponKind.Pistol, Vector3.Zero, Vector3.UnitZ)));
        Assert.Null(SoundBoard.KindFor(new KnifeStuck(1, 0, WeaponKind.Pistol, Vector3.Zero, Vector3.UnitZ, 4)));
    }

    [Fact]
    public void Pack_1_has_a_file_for_each_and_the_old_pack_none()
    {
        Assert.Equal([ThrowFile], SoundPack.Pack1.Variants(SoundKind.KnifeThrow).ToArray());
        Assert.Equal([TargetFile], SoundPack.Pack1.Variants(SoundKind.KnifeHit).ToArray());
        Assert.Equal([StoneFile], SoundPack.Pack1.Variants(SoundKind.KnifeStoneHit).ToArray());
        Assert.All(
            new[] { SoundKind.KnifeThrow, SoundKind.KnifeHit, SoundKind.KnifeStoneHit },
            kind => Assert.True(SoundPack.Old.Variants(kind).IsEmpty));
        Assert.All(new[] { ThrowFile, TargetFile, StoneFile }, file => Assert.Contains(file, SoundBoard.SoundFiles));
    }

    [Fact]
    public void A_knife_thrown_at_a_wall_is_heard_at_the_throw_at_each_ricochet_and_from_the_wall_it_sticks_in()
    {
        // Thrown level along the corridor: off the wall ahead, off the wall behind the thrower, and the wall ahead keeps it.
        var world = World(Corridor(5, "#"));
        var cues = new SoundCues(world.Seed);
        var heard = new List<(int Tick, SoundCue Cue, SoundListener Listener, GameEvent From)>();

        for (var tick = 1; tick <= 2 * SimulationClock.TicksPerSecond; tick++)
        {
            world.Tick(tick == 1 ? Shoot : PlayerInput.None);
            var listener = SoundListener.Of(world.Player);
            foreach (var cue in cues.For(world.Events, SoundPack.Pack1, listener, world.Balance.Hearing).Where(IsKnifeSound))
            {
                heard.Add((tick, cue, listener, world.Events.Single(e => e is ShotFired or KnifeRicocheted or KnifeStuck)));
            }
        }

        Assert.Equal(2, Balance.Default.KnifeFlight.Ricochets);
        Assert.Equal([ThrowFile, StoneFile, StoneFile, StoneFile], heard.Select(entry => entry.Cue.File).ToArray());
        Assert.Empty(world.FlyingKnives);

        // The throw in the tick of the trigger, in the thrower's own hand: the middle, full volume.
        Assert.Equal(1, heard[0].Tick);
        Assert.IsType<ShotFired>(heard[0].From);
        Assert.Equal(SoundBoard.SoundVolume, heard[0].Cue.Volume);
        Assert.Equal(0f, heard[0].Cue.Pan);

        // Each stone some ticks after the one before (the knife really flies), from where the knife met it.
        Assert.Equal([1, 2], heard.Select(entry => entry.From).OfType<KnifeRicocheted>().Select(ricochet => ricochet.Count).ToArray());
        var knife = Assert.IsType<KnifeStuck>(heard[3].From);
        Assert.Null(knife.TargetId);
        Assert.True(heard[1].Tick > 1 && heard[2].Tick > heard[1].Tick && heard[3].Tick > heard[2].Tick, "The stones are not heard one after the other.");
        Assert.All(heard.Skip(1), stone =>
        {
            Assert.NotNull(stone.From.Place);
            var mix = Hearing.Mix(stone.Listener.Ears, stone.Listener.Facing, stone.From.Place.Value, Ears);
            Assert.True(mix.Volume > 0f, $"volume {mix.Volume}");
            Assert.Equal(SoundBoard.SoundVolume * mix.Volume, stone.Cue.Volume, Tolerance);
            Assert.Equal(0f, stone.Cue.Pan, Tolerance);
        });

        // The wall ahead is far: quieter than the wall right behind the thrower.
        Assert.True(heard[1].Cue.Volume < SoundBoard.SoundVolume, $"volume {heard[1].Cue.Volume}");
        Assert.True(heard[2].Cue.Volume > heard[1].Cue.Volume, "The wall behind the thrower is nearer than the wall ahead.");
        Assert.Equal(heard[1].Cue.Volume, heard[3].Cue.Volume, Tolerance);
    }

    [Fact]
    public void A_ricochet_asks_for_the_stone_s_sound_and_only_for_the_knives()
    {
        var point = new Vector3(1f, 2f, 3f);

        Assert.Equal(SoundKind.KnifeStoneHit, SoundBoard.KindFor(new KnifeRicocheted(1, 0, WeaponKind.ThrowingKnives, point, Vector3.UnitX, Vector3.UnitZ, 1)));

        // Something else that glances off one day does not sound like a knife.
        Assert.Null(SoundBoard.KindFor(new KnifeRicocheted(1, 0, WeaponKind.Pistol, point, Vector3.UnitX, Vector3.UnitZ, 1)));
    }

    [Theory]
    [InlineData("G\n.")] // a ghoul
    [InlineData("D\n.")] // the training dummy
    public void A_knife_thrown_at_a_target_is_heard_at_the_throw_and_then_from_where_it_sticks_in_it(string target)
    {
        var world = World(Corridor(5, target));

        var (heard, stuck) = ThrowAndListen(world, 2 * SimulationClock.TicksPerSecond);

        var knife = Assert.Single(stuck);
        Assert.NotNull(knife.TargetId);
        Assert.Equal([ThrowFile, TargetFile], heard.Select(entry => entry.Cue.File).ToArray());
        Assert.Equal(1, heard[0].Tick);

        var hit = heard[1];
        Assert.Same(knife, hit.Stuck);
        Assert.True(hit.Tick > 1, $"stuck in tick {hit.Tick}");
        Assert.Equal(knife.Point, knife.Place);
        var mix = Hearing.Mix(hit.Listener.Ears, hit.Listener.Facing, knife.Point, Ears);
        Assert.True(mix.Volume is > 0f and < 1f, $"volume {mix.Volume}");
        Assert.Equal(SoundBoard.SoundVolume * mix.Volume, hit.Cue.Volume, Tolerance);
        Assert.Equal(0f, hit.Cue.Pan, Tolerance);
    }

    [Theory]
    [InlineData("#", StoneFile, true)]
    [InlineData("G\n.", TargetFile, false)]
    public void Someone_aside_hears_the_throw_and_the_knife_from_their_side_and_nobody_out_of_range(string end, string file, bool stone)
    {
        var world = World(Corridor(5, end));
        var aside = new Vector3((Ears.NearDistance + Ears.Range) / 2f, 0f, 0f);
        var far = new Vector3(Ears.Range + 5f, 0f, 0f);
        var cues = new SoundCues(world.Seed);
        var deaf = new SoundCues(world.Seed);
        var heard = new List<SoundCue>();
        AimAtTheTarget(world);

        for (var tick = 1; tick <= 2 * SimulationClock.TicksPerSecond; tick++)
        {
            world.Tick(tick == 1 ? Shoot : PlayerInput.None);

            // The listeners stand beside whatever sounds in this tick: the hand at the throw, the knife where it glances
            // off stone and where it sticks.
            foreach (var place in world.Events.Where(e => e is ShotFired or KnifeRicocheted or KnifeStuck).Select(e => e.Place))
            {
                Assert.NotNull(place);
                var near = new SoundListener(Bystander, place.Value + aside, Vector3.UnitZ);
                heard.AddRange(cues.For(world.Events, SoundPack.Pack1, near, Ears).Where(IsKnifeSound));
                var tooFar = new SoundListener(Bystander, place.Value + far, Vector3.UnitZ);
                Assert.DoesNotContain(deaf.For(world.Events, SoundPack.Pack1, tooFar, Ears), IsKnifeSound);
            }
        }

        // Stone is heard at every ricochet and where the knife sticks; a target once.
        Assert.Equal(new[] { ThrowFile }.Concat(Enumerable.Repeat(file, stone ? StoneSounds : 1)).ToArray(), heard.Select(cue => cue.File).ToArray());
        Assert.All(heard, cue =>
        {
            Assert.True(cue.Volume is > 0f and < SoundBoard.SoundVolume, $"volume {cue.Volume}");
            Assert.Equal(Ears.MaxPan, cue.Pan, Tolerance); // looking along +Z the right is -X: the sound is on the right
        });
    }

    [Fact]
    public void A_knife_at_the_end_of_its_range_drops_out_without_a_sound()
    {
        // The corridor is longer than the knife flies: no wall, no target, no event (issue #816) - only the throw is heard.
        var cells = (int)(Balance.Default.ThrowingKnives.Range / Level.CellSize) + 4;
        var world = World(Corridor(cells, "."));
        var flightTicks = SimulationClock.SecondsToTicks(Balance.Default.ThrowingKnives.Range / Balance.Default.KnifeFlight.Speed);

        var (heard, stuck) = ThrowAndListen(world, flightTicks + SimulationClock.TicksPerSecond);

        Assert.Empty(stuck);
        Assert.Empty(world.FlyingKnives);
        var only = Assert.Single(heard);
        Assert.Equal(ThrowFile, only.Cue.File);
        Assert.Equal(1, only.Tick);
    }

    [Fact]
    public void Three_knives_in_a_row_are_three_throws_and_three_knives_in_the_wall()
    {
        // Since issue #822 each knife glances off twice before it sticks, so the next is thrown while the one before
        // still flies: the sounds of the knives interleave. Every throw is heard once and every knife on every stone.
        var world = World(Corridor(5, "#"));
        var cues = new SoundCues(world.Seed);
        var heard = new List<string>();
        var knives = Balance.Default.ThrowingKnives.MagazineSize;
        var cooldown = SimulationClock.SecondsToTicks(Balance.Default.ThrowingKnives.Cooldown);
        var throwing = knives * (cooldown + 1);

        for (var tick = 0; tick < throwing + 2 * SimulationClock.TicksPerSecond; tick++)
        {
            world.Tick(tick < throwing && tick % (cooldown + 1) == 0 ? Shoot : PlayerInput.None);
            heard.AddRange(cues.For(world.Events, SoundPack.Pack1, SoundListener.Of(world.Player), world.Balance.Hearing)
                .Where(IsKnifeSound)
                .Select(cue => cue.File));
        }

        Assert.Equal(ThrowFile, heard[0]);
        Assert.Equal(knives, heard.Count(file => file == ThrowFile));
        Assert.Equal(knives * StoneSounds, heard.Count(file => file == StoneFile));
        Assert.Equal(knives * (1 + StoneSounds), heard.Count);
        Assert.Empty(world.FlyingKnives);
        Assert.Equal(knives, world.StuckKnives.Count(knife => knife.TargetId is null));
    }

    [Theory]
    [InlineData(ThrowFile)]
    [InlineData(TargetFile)]
    [InlineData(StoneFile)]
    public void The_files_are_the_takes_of_the_listening_test_byte_for_byte(string file)
    {
        // The script carries the checksums of the takes the human heard (ABGENOMMEN, from wav.sha256 of the sample) and
        // refuses to write anything else; the file the game loads must have the checksum written next to its name there.
        var bytes = File.ReadAllBytes(Path.Combine(RepositoryFiles.Find("assets/audio"), file));
        var checksum = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var script = File.ReadAllText(RepositoryFiles.Find(Script));

        var entry = script.IndexOf($"\"{file}\": (", StringComparison.Ordinal);
        Assert.True(entry >= 0, $"{Script} has no entry for {file}.");
        var end = script.IndexOf("),", entry, StringComparison.Ordinal);
        Assert.Contains($"\"{checksum}\"", script[entry..end], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ThrowFile, 0.46f)]
    [InlineData(TargetFile, 0.29f)]
    [InlineData(StoneFile, 0.50f)]
    public void The_files_are_mono_16_bit_as_long_as_the_takes_and_free_of_overload_and_clicks(string file, float seconds)
    {
        var wav = GrateSoundTests.ReadWav(Path.Combine(RepositoryFiles.Find("assets/audio"), file));

        Assert.Equal(1, wav.Channels);
        Assert.Equal(16, wav.BitsPerSample);
        Assert.Equal(SampleRate, wav.SampleRate);
        Assert.Equal(seconds, wav.Samples.Length / (float)SampleRate, 0.005f);

        var peak = wav.Samples.Max(sample => Math.Abs((int)sample)) / 32768f;
        Assert.True(peak is > 0.1f and <= 0.891f, $"peak {peak}");
        Assert.True(Math.Abs((int)wav.Samples[0]) < 33 && Math.Abs((int)wav.Samples[^1]) < 33, "click at the start or end");
    }
}
