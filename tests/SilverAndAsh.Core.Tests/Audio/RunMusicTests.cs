using System.Numerics;
using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Audio;

/// <summary>
/// Which music a run plays in which of its parts (issue #552): the level, the boss fight, the merchant. The tests play a
/// world tick by tick and read its events the way the game does after every tick.
/// </summary>
public class RunMusicTests
{
    private const float Frame = 1f / SimulationClock.TicksPerSecond;
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };

    // The frames the mixer needs to fade a track out completely (a frame or two either way for rounding).
    private static readonly int FadeOutFrames = (int)(SoundBoard.MusicFadeOutSeconds / Frame);

    [Fact]
    public void A_run_begins_with_the_level_s_music()
    {
        var world = GormHallWorld();
        var music = new RunMusic();

        Assert.Equal(RunPart.Level, music.PartIn(world));
        Assert.Equal(MusicTrack.Catacombs, Wish(music, world).Track);
    }

    [Fact]
    public void Entering_the_boss_room_switches_to_the_boss_music_in_the_tick_the_fight_starts()
    {
        var world = GormHallWorld(balance: Tough);
        var music = new RunMusic();
        var parts = new List<(bool Started, RunPart Part)>();

        Play(world, music, PressF, 1, parts);
        Play(world, music, Forward, 3 * Second, parts, until: () => world.Rooms[1].IsSealed);

        var startAt = parts.FindIndex(tick => tick.Started);
        Assert.True(startAt > 0, "The boss fight never started - the test would prove nothing.");
        Assert.All(parts[..startAt], tick => Assert.Equal(RunPart.Level, tick.Part));
        Assert.Equal(RunPart.BossFight, parts[startAt].Part);
        Assert.Equal(new MusicWish(MusicTrack.BossFight, false), Wish(music, world));
        Assert.Equal("musik-boss.ogg", SoundBoard.MusicFile(MusicTrack.BossFight));
    }

    [Fact]
    public void Killing_the_boss_ends_the_fight_and_brings_back_the_level_s_music()
    {
        var world = GormHallWorld(balance: Tough);
        var music = new RunMusic();
        Play(world, music, PressF, 1);
        Play(world, music, Forward, 3 * Second, until: () => world.Rooms[1].IsSealed);
        Play(world, music, PlayerInput.None, Second);
        Assert.Equal(RunPart.BossFight, music.PartIn(world));

        world.Gorm!.TakeDamage(world.Gorm.Health, []);
        var events = Play(world, music, PlayerInput.None, 1);

        Assert.Single(events, e => e is BossFightEnded);
        Assert.Equal(RunPart.Level, music.PartIn(world));
        Assert.Equal(new MusicWish(MusicTrack.Catacombs, false), Wish(music, world));
    }

    [Fact]
    public void The_fight_ends_only_once_and_the_level_s_music_stays()
    {
        var world = GormHallWorld(balance: Tough);
        var music = new RunMusic();
        Play(world, music, PressF, 1);
        Play(world, music, Forward, 3 * Second, until: () => world.Rooms[1].IsSealed);
        world.Gorm!.TakeDamage(world.Gorm.Health, []);

        var events = Play(world, music, PlayerInput.None, 5 * Second);

        Assert.Single(events, e => e is BossFightEnded);
        Assert.DoesNotContain(events, e => e is BossFightStarted);
        Assert.Equal(RunPart.Level, music.PartIn(world));
    }

    [Fact]
    public void A_boss_room_entered_with_its_boss_already_dead_starts_and_ends_no_fight()
    {
        var world = GormHallWorld();
        var music = new RunMusic();
        world.Gorm!.TakeDamage(world.Gorm.Health, []);

        var events = Play(world, music, PressF, 1);
        events.AddRange(Play(world, music, Forward, 3 * Second));

        Assert.True(world.Rooms[1].IsCleared, "The boss room was never entered - the test would prove nothing.");
        Assert.DoesNotContain(events, e => e is BossFightStarted or BossFightEnded);
        Assert.Equal(RunPart.Level, music.PartIn(world));
    }

    [Fact]
    public void A_player_dying_in_the_boss_fight_ends_the_music_on_the_end_screen_and_the_next_run_starts_with_the_level_s()
    {
        // A player of normal health walks up to Gorm and lets him strike until the player dies - as in the game.
        var world = GormHallWorld();
        var music = new RunMusic();
        Play(world, music, PressF, 1);
        Play(world, music, Forward, 3 * Second, until: () => world.Rooms[1].IsSealed);
        Assert.True(world.Rooms[1].IsSealed, "The boss room never sealed - the test would prove nothing.");

        var events = Play(world, music, Forward, 120 * Second, until: () => world.Player.IsDead);
        Assert.Contains(events, e => e is PlayerHit);
        Assert.Single(events, e => e is PlayerDied);
        events.AddRange(Play(world, music, PlayerInput.None, SimulationClock.SecondsToTicks(Balance.Default.RestartDelay) + 1));

        // Dying ends no boss fight - the boss lives. Until the end screen comes, the boss music goes on.
        Assert.False(world.Gorm!.IsDead);
        Assert.True(world.ShouldRestart);
        Assert.DoesNotContain(events, e => e is BossFightEnded);
        Assert.Equal(RunPart.BossFight, music.PartIn(world));
        Assert.Equal(new MusicWish(MusicTrack.None, false), Wish(music, world, runOver: true));

        // The game starts the next run in a new world (as Program does after the end screen).
        var next = GormHallWorld();
        Assert.Equal(RunPart.Level, music.PartIn(next));
        Assert.Equal(new MusicWish(MusicTrack.Catacombs, false), Wish(music, next));
    }

    [Fact]
    public void Clearing_an_ordinary_combat_room_ends_no_boss_fight()
    {
        var world = CombatRoomWorld();
        var music = new RunMusic();
        Play(world, music, PressF, 1);
        Play(world, music, Forward, 3 * Second, until: () => world.Rooms[1].IsSealed);
        Assert.True(world.Rooms[1].IsSealed, "The combat room never sealed - the test would prove nothing.");

        foreach (var target in world.Targets.ToList())
        {
            target.TakeDamage(10_000, []);
        }

        var events = Play(world, music, PlayerInput.None, Second);

        Assert.Contains(new RoomCleared(1), events);
        Assert.DoesNotContain(events, e => e is BossFightStarted or BossFightEnded);
        Assert.Equal(RunPart.Level, music.PartIn(world));
    }

    [Fact]
    public void Starting_the_level_again_in_the_middle_of_the_boss_fight_plays_the_level_s_music()
    {
        var world = GormHallWorld(balance: Tough);
        var music = new RunMusic();
        Play(world, music, PressF, 1);
        Play(world, music, Forward, 3 * Second, until: () => world.Rooms[1].IsSealed);
        Assert.Equal(RunPart.BossFight, music.PartIn(world));

        // F5 or New Run: a new world with the same seed, the old one is dropped without any further event.
        var again = GormHallWorld(seed: world.Seed, balance: Tough);
        Play(again, music, PlayerInput.None, 1);

        Assert.Equal(RunPart.Level, music.PartIn(again));
        Assert.Equal(MusicTrack.Catacombs, Wish(music, again).Track);
    }

    [Fact]
    public void Events_that_are_no_change_of_part_leave_the_music_where_it_is()
    {
        var world = GormHallWorld(balance: Tough);
        var music = new RunMusic();
        Play(world, music, PressF, 1);
        Play(world, music, Forward, 3 * Second, until: () => world.Rooms[1].IsSealed);

        var events = Play(world, music, PlayerInput.None, 3 * Second);

        Assert.NotEmpty(events);
        Assert.DoesNotContain(events, e => e is BossFightStarted or BossFightEnded);
        Assert.Equal(RunPart.BossFight, music.PartIn(world));
    }

    [Fact]
    public void Reading_no_world_is_refused()
    {
        var music = new RunMusic();

        Assert.Throws<ArgumentNullException>(() => music.Read(null!));
    }

    [Fact]
    public void Every_part_of_a_run_has_a_piece_of_its_own_with_a_file_and_a_volume()
    {
        Assert.Equal(MusicTrack.Catacombs, SoundBoard.TrackFor(RunPart.Level));
        Assert.Equal(MusicTrack.BossFight, SoundBoard.TrackFor(RunPart.BossFight));
        Assert.Equal(MusicTrack.Merchant, SoundBoard.TrackFor(RunPart.Merchant));
        Assert.Equal("musik-haendler.ogg", SoundBoard.MusicFile(MusicTrack.Merchant));

        var tracks = Enum.GetValues<RunPart>().Select(SoundBoard.TrackFor).ToList();
        Assert.Equal(tracks.Count, tracks.Distinct().Count());
        Assert.All(tracks, track =>
        {
            Assert.NotNull(SoundBoard.MusicFile(track));
            Assert.InRange(SoundBoard.MusicVolume(track), 0.01f, 1f);
            Assert.Contains(track, SoundBoard.MusicTracks);
        });
    }

    [Fact]
    public void A_part_of_a_run_the_game_does_not_know_has_no_music()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SoundBoard.TrackFor((RunPart)99));
    }

    [Fact]
    public void The_esc_menu_holds_the_music_of_any_part_and_the_title_and_the_end_screen_win_over_it()
    {
        foreach (var part in Enum.GetValues<RunPart>())
        {
            var track = SoundBoard.TrackFor(part);
            Assert.Equal(new MusicWish(track, true), SoundBoard.MusicFor(new MusicSituation(false, Paused: true, false, part)));
            Assert.Equal(new MusicWish(MusicTrack.None, false), SoundBoard.MusicFor(new MusicSituation(false, false, RunOver: true, part)));
            Assert.Equal(new MusicWish(MusicTrack.Title, false), SoundBoard.MusicFor(new MusicSituation(OnTitle: true, false, false, part)));
        }
    }

    [Fact]
    public void Into_the_boss_fight_and_out_again_the_music_fades_over_and_never_plays_two_pieces()
    {
        // As in the game: once per frame the mixer hears what the world asks for, then the world ticks and its events are read.
        var world = GormHallWorld(balance: Tough);
        var music = new RunMusic();
        var mixer = new MusicMixer();
        var frames = new List<(MusicMix Mix, bool Started, bool Ended)>();
        PlayAndMix(world, music, mixer, PlayerInput.None, (int)((SoundBoard.MusicFadeInSeconds + 1f) * Second), frames);
        Assert.Equal(SoundBoard.MusicVolume(MusicTrack.Catacombs), frames[^1].Mix.Volume, 4);

        PlayAndMix(world, music, mixer, PressF, 1, frames);
        PlayAndMix(world, music, mixer, Forward, 3 * Second, frames, until: () => world.Rooms[1].IsSealed);
        PlayAndMix(world, music, mixer, PlayerInput.None, (int)((SoundBoard.MusicFadeOutSeconds + SoundBoard.MusicFadeInSeconds + 1f) * Second), frames);
        world.Gorm!.TakeDamage(world.Gorm.Health, []);
        PlayAndMix(world, music, mixer, PlayerInput.None, (int)((SoundBoard.MusicFadeOutSeconds + SoundBoard.MusicFadeInSeconds + 1f) * Second), frames);

        var started = frames.FindIndex(frame => frame.Started);
        var ended = frames.FindIndex(frame => frame.Ended);
        Assert.True(started > 0 && ended > started, "The boss fight did not start and end - the test would prove nothing.");
        AssertFadesOver(frames[(started + 1)..(ended + 1)], MusicTrack.Catacombs, MusicTrack.BossFight);
        AssertFadesOver(frames[(ended + 1)..], MusicTrack.BossFight, MusicTrack.Catacombs);
    }

    // From the frame after the event: the old piece falls to silence within the fade-out, then the new one starts once
    // from silence and rises to its full volume - one piece at a time.
    private static void AssertFadesOver(List<(MusicMix Mix, bool Started, bool Ended)> frames, MusicTrack from, MusicTrack to)
    {
        var switchAt = frames.FindIndex(frame => frame.Mix.Track == to);
        Assert.InRange(switchAt, FadeOutFrames - 2, FadeOutFrames + 2);
        var fadeOut = frames[..switchAt];
        Assert.All(fadeOut, frame => Assert.Equal(from, frame.Mix.Track));
        for (var i = 1; i < fadeOut.Count; i++)
        {
            Assert.True(fadeOut[i].Mix.Volume <= fadeOut[i - 1].Mix.Volume, $"{from} rose at frame {i}");
        }

        var fadeIn = frames[switchAt..];
        Assert.True(fadeIn[0].Mix.Start);
        Assert.Equal(0f, fadeIn[0].Mix.Volume);
        Assert.Single(fadeIn, frame => frame.Mix.Start);
        Assert.All(fadeIn, frame => Assert.Equal(to, frame.Mix.Track));
        Assert.Equal(SoundBoard.MusicVolume(to), fadeIn[^1].Mix.Volume, 4);
    }

    // Room 0 (start) in row 0, a door in row 1, room 1 (an ordinary combat room with one ghoul) in rows 2-5.
    private static GameWorld CombatRoomWorld()
    {
        var cells = Enumerable.Range(2, 4).SelectMany(z => new[] { (1, z), (2, z), (3, z) }).ToList();
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 5, 1, 0, [(1, 0), (2, 0), (3, 0)], [], []),
            new(1, RoomKind.Combat, "arena", 0, 2, 5, 4, 1, cells, [(2, 4)], []),
        ];
        LevelDoor[] doors = [new(2, 1, 0, 1, RoomSide.South)];
        return new GameWorld(LevelParser.Parse("#.P.#\n##.##\n#...#\n#...#\n#.G.#\n#...#"), 1, Balance.Default, doors, rooms);
    }

    private static MusicWish Wish(RunMusic music, GameWorld world, bool runOver = false) =>
        SoundBoard.MusicFor(new MusicSituation(OnTitle: false, Paused: false, RunOver: runOver, Part: music.PartIn(world)));

    // Ticks the world and reads its events after every tick, as the game does; returns the events.
    private static List<GameEvent> Play(
        GameWorld world, RunMusic music, PlayerInput input, int ticks, List<(bool Started, RunPart Part)>? parts = null, Func<bool>? until = null)
    {
        var events = new List<GameEvent>();
        for (var i = 0; i < ticks && until?.Invoke() != true; i++)
        {
            world.Tick(input);
            music.Read(world);
            events.AddRange(world.Events);
            parts?.Add((world.Events.Any(e => e is BossFightStarted), music.PartIn(world)));
        }

        return events;
    }

    private static void PlayAndMix(
        GameWorld world, RunMusic music, MusicMixer mixer, PlayerInput input, int frames,
        List<(MusicMix Mix, bool Started, bool Ended)> record, Func<bool>? until = null)
    {
        for (var i = 0; i < frames && until?.Invoke() != true; i++)
        {
            var mix = mixer.Step(Wish(music, world), Frame);
            world.Tick(input);
            music.Read(world);
            record.Add((mix, world.Events.Any(e => e is BossFightStarted), world.Events.Any(e => e is BossFightEnded)));
        }
    }
}
