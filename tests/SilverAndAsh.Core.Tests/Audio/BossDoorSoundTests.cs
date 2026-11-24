using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Tests.Gameplay;

namespace SilverAndAsh.Core.Tests.Audio;

/// <summary>
/// Issue #569: the boss door is heard - rumbling and the slam when it swings shut, grinding over rubble when it swings
/// open - played the way the game plays it: tick by tick from the world's events, placed for the listening player.
/// The world is the one of <see cref="BossDoorTests"/>.
/// </summary>
public class BossDoorSoundTests
{
    private const string ClosingFile = "bosstuer-schliessen.wav";
    private const string OpeningFile = "bosstuer-oeffnen.wav";
    private const string TicksFile = "bosstuer-ticks.json";
    private const string Script = "tools/audio/bosstuer_klang.py";
    private const int Bystander = 7;
    private const float Tolerance = 1e-5f;
    private static readonly DoorsBalance Doors = Balance.Default.Doors;
    private static readonly int OpenTicks = SimulationClock.SecondsToTicks(Doors.BossOpenTime);

    // One tick of the world and the sounds the listening player hears from it.
    private static IReadOnlyList<SoundCue> Tick(GameWorld world, SoundCues cues, PlayerInput input)
    {
        world.Tick(input);
        return cues.For(world.Events, SoundPack.Pack1, SoundListener.Of(world.Player), world.Balance.Hearing);
    }

    private static bool IsDoorSound(SoundCue cue) =>
        cue.Kind is SoundKind.BossDoorClosing or SoundKind.BossDoorOpening or SoundKind.DoorOpened;

    [Fact]
    public void The_boss_door_plays_its_closing_sound_once_in_the_tick_it_starts_to_swing_shut()
    {
        var world = BossDoorTests.World();
        var door = world.Doors[BossDoorTests.BossDoor];
        var cues = new SoundCues(world.Seed);
        var heard = new List<(int Tick, bool Sealed, List<SoundCue> Cues)>();
        int? sealedAt = null;
        int? shutAt = null;

        // Open it, walk in until the room seals, and on through the whole slam.
        for (var tick = 0; tick < 1 + (3 * SimulationClock.TicksPerSecond) + Doors.BossCloseTicks + 10; tick++)
        {
            var input = tick == 0 ? BossDoorTests.PressF
                : sealedAt is null ? BossDoorTests.IntoTheOpening(world) : PlayerInput.None;
            var played = Tick(world, cues, input);
            var sealedNow = world.Events.Contains(new RoomSealed(1));
            sealedAt ??= sealedNow ? tick : null;
            shutAt ??= door.Blocks ? tick : null;
            if (sealedNow || played.Any(cue => cue.Kind == SoundKind.BossDoorClosing))
            {
                heard.Add((tick, sealedNow, [.. played.Where(IsDoorSound)]));
            }
        }

        Assert.True(sealedAt.HasValue, "The boss room never sealed - the test would prove nothing.");
        Assert.True(shutAt.HasValue, "The boss door never shut - the test would prove nothing.");

        // Exactly one closing sound, in the sealing tick - the tick the leaves begin to swing.
        var (atTick, sealedThen, kinds) = Assert.Single(heard);
        Assert.Equal(sealedAt, atTick);
        Assert.True(sealedThen);
        var cue = Assert.Single(kinds);
        Assert.Equal(SoundKind.BossDoorClosing, cue.Kind);
        Assert.Equal(ClosingFile, cue.File);
        Assert.InRange(cue.Volume, 0.01f, SoundBoard.SoundVolume);

        // The leaves hit their stop in the BossShutTick-th tick, counting the tick the sound starts in as the first: the
        // file has its slam BossShutTick ticks after its start (tools/audio/bosstuer_klang.py), one tick after that.
        Assert.Equal(Doors.BossShutTick, shutAt!.Value - atTick + 1);
    }

    [Fact]
    public void A_player_opening_the_boss_door_hears_its_grinding_and_not_the_creak_of_an_ordinary_door()
    {
        var world = BossDoorTests.World();
        var cues = new SoundCues(world.Seed);

        var opened = Tick(world, cues, BossDoorTests.PressF);

        // Both events come in that tick; only the boss door's own sound plays.
        Assert.Contains(world.Events, e => e is DoorOpened { DoorId: BossDoorTests.BossDoor });
        Assert.Contains(world.Events, e => e is DoorSwingStarted { DoorId: BossDoorTests.BossDoor, Closing: false });
        var cue = Assert.Single(opened, IsDoorSound);
        Assert.Equal(SoundKind.BossDoorOpening, cue.Kind);
        Assert.Equal(OpeningFile, cue.File);
        Assert.InRange(cue.Volume, 0.01f, SoundBoard.SoundVolume);

        // And never again while it swings open.
        for (var tick = 0; tick < OpenTicks + 10; tick++)
        {
            Assert.DoesNotContain(Tick(world, cues, PlayerInput.None), IsDoorSound);
        }
    }

    [Fact]
    public void The_boss_door_opening_again_after_the_boss_room_is_cleared_plays_its_grinding_once()
    {
        var world = BossDoorTests.World();
        var cues = new SoundCues(world.Seed);
        Tick(world, cues, BossDoorTests.PressF);
        for (var tick = 0; tick < 3 * SimulationClock.TicksPerSecond && !world.Rooms[1].IsSealed; tick++)
        {
            Tick(world, cues, BossDoorTests.IntoTheOpening(world));
        }

        Assert.True(world.Rooms[1].IsSealed);
        for (var tick = 0; tick < Doors.BossCloseTicks + 10; tick++)
        {
            Tick(world, cues, PlayerInput.None);
        }

        // The boss is dead: the door swings open again by itself - no player opened it, so no DoorOpened.
        BossDoorTests.KillEverything(world);
        var heard = new List<SoundCue>();
        for (var tick = 0; tick < OpenTicks + 10; tick++)
        {
            heard.AddRange(Tick(world, cues, PlayerInput.None).Where(IsDoorSound));
        }

        var cue = Assert.Single(heard);
        Assert.Equal(SoundKind.BossDoorOpening, cue.Kind);
        Assert.Equal(OpeningFile, cue.File);
    }

    [Fact]
    public void The_boss_door_sounds_are_built_on_the_ticks_of_the_balance()
    {
        // Issue #668: the files carry the slam and the length of the opening fixed in them; the script writes the ticks
        // it built them on next to them. A balancing round that moves either must build and listen to them anew.
        using var built = JsonDocument.Parse(File.ReadAllText(RepositoryFiles.Find("assets/audio/" + TicksFile)));
        var shutTick = built.RootElement.GetProperty("bossShutTick").GetInt32();
        var openTicks = built.RootElement.GetProperty("bossOpenTicks").GetInt32();

        // Issue #680: the script's own constants, too - the files are built on those, so a companion file set to the
        // balance by hand must not turn the test green. Only the file is read, Python is not run.
        var script = File.ReadAllText(RepositoryFiles.Find(Script));
        var scriptShutTick = ScriptConstant(script, "ANSCHLAG_TICK");
        var scriptOpenTicks = ScriptConstant(script, "OEFFNEN_TICKS");

        Assert.True(
            shutTick == Doors.BossShutTick && openTicks == OpenTicks,
            $"The boss door sounds are built on the slam at tick {shutTick} and an opening of {openTicks} ticks, but the "
            + $"balance has bossShutTick {Doors.BossShutTick} and an opening of {OpenTicks} ticks (bossOpenTime "
            + $"{Doors.BossOpenTime} s). Set the ticks in {Script}, run it, run tools/models/checksums.py and have the "
            + "sounds listened to again (audio bible, rule 13).");
        Assert.True(
            scriptShutTick == shutTick && scriptOpenTicks == openTicks,
            $"{Script} builds the boss door sounds on the slam at tick {scriptShutTick} (ANSCHLAG_TICK) and an opening "
            + $"of {scriptOpenTicks} ticks (OEFFNEN_TICKS), but assets/audio/{TicksFile} says {shutTick} and "
            + $"{openTicks}: the companion file was changed by hand or the script was not run. Set the ticks in "
            + $"{Script} to the balance (bossShutTick {Doors.BossShutTick}, {OpenTicks} ticks for bossOpenTime), run "
            + "it, run tools/models/checksums.py and have the sounds listened to again (audio bible, rule 13).");
    }

    // The operator in front of "=" in an augmented assignment ("+=", "//=", ...).
    private const string AugmentingOperator = @"(?://|\*\*|<<|>>|[-+*/%@&|^])";

    // An assignment at the start of a line (not indented, not a comment): the targets left of the first "=" that is not
    // part of "==", "<=", ">=" or "!=", with an augmenting operator or an annotation (": int") allowed.
    private static readonly Regex Assignment = new(
        $@"^(?<targets>[A-Za-z_(\[*][\w \t,()\[\]*.:]*?)[ \t]*{AugmentingOperator}?=(?!=)",
        RegexOptions.Multiline);

    // The value of a constant "NAME = 123" at the start of a line of the script. Issue #684: every assignment at the start
    // of a line that binds the name counts - also computed ("NAME = NAME - 8"), as a tuple ("NAME, OTHER = 70, 120") or
    // augmented ("NAME += 8") - so a later rebinding cannot leave the test reading a number the script no longer uses.
    private static int ScriptConstant(string script, string name)
    {
        var binds = new Regex($@"(?<![\w.]){name}(?!\w)");
        // Right of the first "=" of such a line the name is bound again by a chain ("X = NAME = 70") or a second
        // statement ("X = 30; NAME = 70") - "=" or ";" in front of it - and by a walrus anywhere ("X = (NAME := 70)").
        // A keyword argument ("f(NAME=3)") or a comparison ("NAME <= 3") binds nothing.
        var chained = new Regex($@"(?<=(?:^|[=;])[ \t]*){name}[ \t]*{AugmentingOperator}?=(?!=)");
        var walrus = new Regex($@"(?<![\w.]){name}[ \t]*:=");
        var bindings = new List<string>();
        foreach (Match m in Assignment.Matches(script))
        {
            var assignment = script[m.Index..].Split('\n')[0].TrimEnd('\r');
            var rest = assignment[m.Length..];
            var count = (binds.IsMatch(m.Groups["targets"].Value) ? 1 : 0) + chained.Count(rest) + walrus.Count(rest);
            bindings.AddRange(Enumerable.Repeat(assignment, count));
        }

        Assert.True(
            bindings.Count == 1,
            $"{Script} must set {name} exactly once at the start of a line, as \"{name} = <ticks>\", but binds it "
            + $"{bindings.Count} times ({string.Join(" | ", bindings)}). Keep the one line {name} = <ticks> and do not "
            + "set, compute or unpack it anywhere else in the script.");
        var line = bindings[0];
        var value = Regex.Match(line, $@"^{name}[ \t]*=[ \t]*(\d+)[ \t]*(?:#.*)?$");
        Assert.True(
            value.Success,
            $"{Script} sets {name} in \"{line}\", which is not a plain number of ticks. Write it as {name} = <ticks> "
            + "(a whole number, a comment behind it is fine) so the test can read it without running Python.");
        return int.Parse(value.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    [Fact]
    public void A_tick_constant_missing_from_the_script_or_set_twice_is_reported_not_read()
    {
        // Error branches of ScriptConstant: a renamed, commented out or doubled constant must not pass for a value.
        Assert.Equal(78, ScriptConstant("X = 1\nANSCHLAG_TICK = 78  # slam\r\nOEFFNEN_TICKS = 132\n", "ANSCHLAG_TICK"));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => ScriptConstant("ANSCHLAG = 78\n", "ANSCHLAG_TICK"));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => ScriptConstant("# ANSCHLAG_TICK = 78\n", "ANSCHLAG_TICK"));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(
            () => ScriptConstant("ANSCHLAG_TICK = 78\nANSCHLAG_TICK = 70\n", "ANSCHLAG_TICK"));
    }

    [Fact]
    public void Uses_comparisons_and_look_alike_names_of_a_tick_constant_are_not_taken_for_a_rebinding()
    {
        // The other side of issue #684: reading, comparing or a longer name is no binding, nor is an indented line.
        const string script = "ANSCHLAG_TICK = 78\r\n"
            + "ANSCHLAG_TICKS = ANSCHLAG_TICK - 8\r\n"
            + "ENDE = ANSCHLAG_TICK + 40\r\n"
            + "bauen(ANSCHLAG_TICK == 78, x=ANSCHLAG_TICK)\r\n"
            + "assert ANSCHLAG_TICK >= 1 and ANSCHLAG_TICK <= 99 and ANSCHLAG_TICK != 0\r\n"
            + "r2.ANSCHLAG_TICK = 70\r\n"
            + "    ANSCHLAG_TICK = 70\r\n"
            + "# ANSCHLAG_TICK, OEFFNEN_TICKS = 70, 120\r\n";

        Assert.Equal(78, ScriptConstant(script, "ANSCHLAG_TICK"));
    }

    [Fact]
    public void A_tick_constant_computed_again_further_down_the_script_is_reported_not_read()
    {
        var error = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
            () => ScriptConstant("ANSCHLAG_TICK = 78\nOEFFNEN_TICKS = 132\nANSCHLAG_TICK = ANSCHLAG_TICK - 8\n", "ANSCHLAG_TICK"));
        Assert.Contains("binds it 2 times", error.Message, StringComparison.Ordinal);
        Assert.Contains("Keep the one line ANSCHLAG_TICK = <ticks>", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_tick_constant_set_again_in_a_tuple_is_reported_not_read()
    {
        foreach (var tuple in new[]
        {
            "ANSCHLAG_TICK, OEFFNEN_TICKS = 70, 120",
            "OEFFNEN_TICKS, ANSCHLAG_TICK = 120, 70",
            "(ANSCHLAG_TICK, OEFFNEN_TICKS) = (70, 120)",
            "[OEFFNEN_TICKS,ANSCHLAG_TICK] = [120, 70]",
        })
        {
            var error = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
                () => ScriptConstant($"ANSCHLAG_TICK = 78\r\nOEFFNEN_TICKS = 132\r\n{tuple}\r\n", "ANSCHLAG_TICK"));
            Assert.Contains("binds it 2 times", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_tick_constant_moved_by_an_augmented_assignment_is_reported_not_read()
    {
        foreach (var augmented in new[] { "ANSCHLAG_TICK += 8", "ANSCHLAG_TICK -= 8", "ANSCHLAG_TICK //= 2" })
        {
            var error = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
                () => ScriptConstant($"ANSCHLAG_TICK = 78\n{augmented}\n", "ANSCHLAG_TICK"));
            Assert.Contains("binds it 2 times", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_tick_constant_bound_again_further_right_in_a_chained_assignment_is_reported_not_read()
    {
        foreach (var chain in new[] { "ENDE_TICK = ANSCHLAG_TICK = 70", "A = B = ANSCHLAG_TICK = 70", "ENDE_TICK=ANSCHLAG_TICK=70" })
        {
            var error = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
                () => ScriptConstant($"ANSCHLAG_TICK = 78\r\nOEFFNEN_TICKS = 132\r\n{chain}\r\n", "ANSCHLAG_TICK"));
            Assert.Contains("binds it 2 times", error.Message, StringComparison.Ordinal);
            Assert.Contains(chain, error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_tick_constant_bound_again_by_a_walrus_right_of_an_assignment_is_reported_not_read()
    {
        foreach (var walrus in new[] { "ENDE_TICK = (ANSCHLAG_TICK := 70)", "ENDE_TICK = bauen(1, (ANSCHLAG_TICK:=70))" })
        {
            var error = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
                () => ScriptConstant($"ANSCHLAG_TICK = 78\r\nOEFFNEN_TICKS = 132\r\n{walrus}\r\n", "ANSCHLAG_TICK"));
            Assert.Contains("binds it 2 times", error.Message, StringComparison.Ordinal);
            Assert.Contains(walrus, error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_tick_constant_bound_again_in_a_second_statement_after_a_semicolon_is_reported_not_read()
    {
        foreach (var statements in new[] { "ANLAUF_TICKS = 30; ANSCHLAG_TICK = 70", "ANLAUF_TICKS = 30;ANSCHLAG_TICK -= 8" })
        {
            var error = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
                () => ScriptConstant($"ANSCHLAG_TICK = 78\n{statements}\n", "ANSCHLAG_TICK"));
            Assert.Contains("binds it 2 times", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Keyword_arguments_comparisons_and_comments_right_of_an_assignment_are_not_taken_for_a_rebinding()
    {
        // The other side of the chained, walrus and semicolon checks: right of an assignment these only read the name.
        const string script = "ANSCHLAG_TICK = 78\r\n"
            + "ENDE = bauen(ANSCHLAG_TICK=3, tick = ANSCHLAG_TICK)\r\n"
            + "OK = ANSCHLAG_TICK == 78 or ANSCHLAG_TICK <= 99 or ANSCHLAG_TICK >= 1 or ANSCHLAG_TICK != 0\r\n"
            + "LETZTER = ANSCHLAG_TICK  # ANSCHLAG_TICK = 70 stand hier frueher\r\n"
            + "r2.WERT = r2.ANSCHLAG_TICK = 70\r\n"
            + "LANG = ANSCHLAG_TICKS = 70\r\n";

        Assert.Equal(78, ScriptConstant(script, "ANSCHLAG_TICK"));
    }

    [Fact]
    public void A_tick_constant_set_once_but_not_as_a_plain_number_is_reported_not_read()
    {
        // Bound exactly once, but in a form the test cannot read without running Python.
        foreach (var once in new[]
        {
            "ANSCHLAG_TICK = 70 + 8",
            "ANSCHLAG_TICK, OEFFNEN_TICKS = 78, 132",
            "ANSCHLAG_TICK: int = 78",
            "ANSCHLAG_TICK = 78.0",
        })
        {
            var error = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => ScriptConstant($"{once}\r\n", "ANSCHLAG_TICK"));
            Assert.Contains($"sets ANSCHLAG_TICK in \"{once}\", which is not a plain number", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_listener_beside_the_boss_door_hears_both_swings_from_that_side()
    {
        var world = BossDoorTests.World();
        var door = world.Doors[BossDoorTests.BossDoor];
        var middle = (door.BoxMin + door.BoxMax) / 2f;

        world.Tick(BossDoorTests.PressF);
        AssertHeardFromTheSide(world.Events, middle, SoundKind.BossDoorOpening);

        for (var tick = 0; tick < 3 * SimulationClock.TicksPerSecond && !world.Rooms[1].IsSealed; tick++)
        {
            world.Tick(BossDoorTests.IntoTheOpening(world));
        }

        Assert.True(world.Rooms[1].IsSealed, "The boss room never sealed - the test would prove nothing.");
        AssertHeardFromTheSide(world.Events, middle, SoundKind.BossDoorClosing);
    }

    // Issue #668: two listeners - not the player who walks - level with the door's middle, as far to either side as
    // halfway into the hearing range, both looking through the door (+Z, so their right is -X). The one at -X has the
    // door to the left, the one at +X to the right; each at the volume of that distance.
    private static void AssertHeardFromTheSide(IReadOnlyList<GameEvent> events, Vector3 middle, SoundKind kind)
    {
        var hearing = Balance.Default.Hearing;
        var aside = new Vector3((hearing.NearDistance + hearing.Range) / 2f, 0f, 0f);
        foreach (var (ears, pan) in new[] { (middle - aside, -hearing.MaxPan), (middle + aside, hearing.MaxPan) })
        {
            var listener = new SoundListener(Bystander, ears, Vector3.UnitZ);
            var cue = Assert.Single(new SoundCues(1).For(events, SoundPack.Pack1, listener, hearing), c => c.Kind == kind);
            var volume = SoundBoard.SoundVolume * Hearing.Mix(ears, Vector3.UnitZ, middle, hearing).Volume;

            Assert.Equal(pan, cue.Pan, Tolerance);
            Assert.Equal(volume, cue.Volume, Tolerance);
            Assert.True(cue.Volume is > 0f and < SoundBoard.SoundVolume, $"volume {cue.Volume}");
        }
    }

    [Fact]
    public void Each_swing_asks_for_its_own_sound()
    {
        Assert.Equal(SoundKind.BossDoorClosing, SoundBoard.KindFor(new DoorSwingStarted(0, Closing: true)));
        Assert.Equal(SoundKind.BossDoorOpening, SoundBoard.KindFor(new DoorSwingStarted(0, Closing: false)));
    }

    [Fact]
    public void The_creak_stays_for_another_door_and_in_a_pack_without_the_grinding()
    {
        // Error branches of the replacement: a door opened in the same tick as another one's swing still creaks, and a
        // pack without a file for the grinding keeps the creak (the old pack has neither, so nothing plays there).
        GameEvent[] otherDoor = [new DoorOpened(3), new DoorSwingStarted(0, Closing: false)];
        var kinds = new SoundCues(1).For(otherDoor, SoundPack.Pack1).Select(cue => cue.Kind);
        Assert.Equal([SoundKind.DoorOpened, SoundKind.BossDoorOpening], kinds);

        GameEvent[] sameDoor = [new DoorOpened(0), new DoorSwingStarted(0, Closing: false)];
        Assert.False(SoundBoard.IsReplacedIn(sameDoor[0], sameDoor, SoundPack.Old));
        Assert.True(SoundBoard.IsReplacedIn(sameDoor[0], sameDoor, SoundPack.Pack1));

        // A closing swing of the same door does not silence a creak.
        GameEvent[] closing = [new DoorOpened(0), new DoorSwingStarted(0, Closing: true)];
        Assert.False(SoundBoard.IsReplacedIn(closing[0], closing, SoundPack.Pack1));
    }
}
