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
