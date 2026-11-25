using System.Collections.Frozen;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Audio;

/// <summary>The pieces of music (issue #271). At most one of them is heard at a time.</summary>
public enum MusicTrack
{
    /// <summary>No music.</summary>
    None,

    /// <summary>The theme of the title screen (main menu).</summary>
    Title,

    /// <summary>The music of the catacombs, while a run is played.</summary>
    Catacombs,

    /// <summary>The music of the boss fight (issue #552, "b - Basspuls" from the listening test of #548).</summary>
    BossFight,

    /// <summary>The music of the merchant's room (issue #552, "b - Chor-Atem" from the listening test of #548).</summary>
    Merchant,
}

/// <summary>
/// The part of a run the music follows (issue #552): the level itself, a boss fight, or a player at the merchant.
/// Which piece each part plays is <see cref="SoundBoard.TrackFor"/>.
/// </summary>
public enum RunPart
{
    /// <summary>Anywhere else in the level: the music of the level (today the catacombs).</summary>
    Level,

    /// <summary>From the moment the boss room seals with the boss alive until the boss fight ends.</summary>
    BossFight,

    /// <summary>
    /// While a player is in the merchant's room. Nothing reports this part yet: the merchant's room comes with issue
    /// #197, which adds its two events to <see cref="RunMusic"/>'s table.
    /// </summary>
    Merchant,
}

/// <summary>
/// Where the game stands, as far as the music cares (issue #271): on the title screen, in the Esc menu that holds the
/// world, on the end screen of a run - otherwise in a run, in <paramref name="Part"/> of it (issue #552).
/// </summary>
public readonly record struct MusicSituation(bool OnTitle, bool Paused, bool RunOver, RunPart Part = RunPart.Level);

/// <summary>Which music the screen asks for: this track, and whether it holds where it is (the Esc menu).</summary>
public readonly record struct MusicWish(MusicTrack Track, bool Paused);

/// <summary>
/// What the sound player does with the music in this frame: <see cref="Track"/> at <see cref="Volume"/> (0 to 1, before
/// <see cref="SoundBoard.MasterVolume"/>), held where it is while <see cref="Paused"/>. <see cref="Start"/> is true in the
/// one frame the track begins (from its start); every other track is stopped.
/// </summary>
public readonly record struct MusicMix(MusicTrack Track, float Volume, bool Paused, bool Start);

/// <summary>
/// Turns the music the screen asks for into what is heard, frame by frame (issue #271) - pure logic, testable without a
/// sound device. A change of track fades the old one out, then starts the new one from silence and fades it in
/// (the designer's suggestion: never two pieces at once, never a hard cut). Asking for the track that is already on
/// does not start it again - it only fades back up, also in the middle of fading out.
/// </summary>
public sealed class MusicMixer
{
    private MusicTrack _track = MusicTrack.None;

    // The fade of the current track, 0 (silent) to 1 (its full volume).
    private float _fade;

    /// <summary>One drawn frame of <paramref name="seconds"/>: steps the fades towards <paramref name="wish"/>.</summary>
    public MusicMix Step(MusicWish wish, float seconds)
    {
        seconds = MathF.Max(0f, seconds);
        var start = false;
        if (wish.Track == _track)
        {
            if (!wish.Paused)
            {
                _fade = MathF.Min(1f, _fade + (seconds / SoundBoard.MusicFadeInSeconds));
            }
        }
        else if (_track != MusicTrack.None && _fade > 0f)
        {
            // The old track goes out first; the menu holding the world does not hold this.
            _fade = MathF.Max(0f, _fade - (seconds / SoundBoard.MusicFadeOutSeconds));
        }

        if (wish.Track != _track && (_track == MusicTrack.None || _fade <= 0f))
        {
            _track = wish.Track;
            _fade = 0f;
            start = _track != MusicTrack.None;
        }

        var paused = wish.Paused && wish.Track == _track;
        return new MusicMix(_track, _fade * SoundBoard.MusicVolume(_track), paused, start);
    }
}

/// <summary>
/// Follows the events of a world to know which <see cref="RunPart"/> the music is in (issue #552) - pure logic, no sound
/// device. Core reports the changes as events (decision record 0003: sound only through events); this class remembers the
/// latest one. A different world (a new run, the level started again) begins in <see cref="RunPart.Level"/>, so nothing
/// of the old run's boss fight carries over.
/// <para>
/// A new part of a run comes in as one line in the table of events, not as another branch.
/// </para>
/// </summary>
public sealed class RunMusic
{
    // Which event moves the music to which part. The merchant's room (issue #197) adds its entering and leaving here.
    private static readonly FrozenDictionary<Type, RunPart> PartAfter = new Dictionary<Type, RunPart>
    {
        [typeof(BossFightStarted)] = RunPart.BossFight,
        [typeof(BossFightEnded)] = RunPart.Level,
    }.ToFrozenDictionary();

    private GameWorld? _world;
    private RunPart _part = RunPart.Level;

    /// <summary>The part of the run that <paramref name="world"/> is in, after the events read so far.</summary>
    public RunPart PartIn(GameWorld world)
    {
        Follow(world);
        return _part;
    }

    /// <summary>Reads the events of the latest tick of <paramref name="world"/> - call after every tick.</summary>
    public void Read(GameWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        Follow(world);
        foreach (var gameEvent in world.Events)
        {
            if (PartAfter.TryGetValue(gameEvent.GetType(), out var part))
            {
                _part = part;
            }
        }
    }

    // A world it has not read before starts from the level's own music.
    private void Follow(GameWorld world)
    {
        if (!ReferenceEquals(world, _world))
        {
            _world = world;
            _part = RunPart.Level;
        }
    }
}
