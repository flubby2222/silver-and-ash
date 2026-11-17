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
}

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
