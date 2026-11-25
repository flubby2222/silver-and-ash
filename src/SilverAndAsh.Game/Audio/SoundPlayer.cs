using System.Globalization;
using Raylib_cs;
using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Audio;

/// <summary>
/// Plays the sounds of assets/audio/ with raylib (issue #267): the short sounds that <see cref="SoundCues"/> picks from
/// the events of Core, and the music as looping streams (issue #271): which piece and how loud comes from
/// <see cref="MusicMixer"/> in Core, this class only plays it. No game rules in here.
/// <para>
/// Never fails: without a sound device the game runs silent, and a file that is missing, does not match the checksum
/// list or cannot be loaded costs a warning in the log - that one sound stays silent, everything else plays.
/// </para>
/// </summary>
internal sealed class SoundPlayer : IDisposable
{
    private readonly Dictionary<string, Sound> _sounds;
    private readonly Dictionary<MusicTrack, Music> _music;
    private readonly MusicMixer _mixer = new();
    private bool _deviceOpen;
    private readonly FileLog? _log;

    // The track that is playing (started, not stopped) and whether it is held.
    private MusicTrack _playing = MusicTrack.None;
    private bool _paused;

    // The sounds held while the world stands still (Hold); which ones, Core decides.
    private readonly HeldSounds _held = new();

    // The player's volumes from the settings (issue #349), each 0 to 1.
    private float _master = 1f;
    private float _effects = 1f;
    private float _ambience = 1f;

    private SoundPlayer(bool deviceOpen, Dictionary<string, Sound> sounds, Dictionary<MusicTrack, Music> music, FileLog? log)
    {
        _deviceOpen = deviceOpen;
        _sounds = sounds;
        _music = music;
        _log = log;
    }

    /// <summary>True while N has muted the game.</summary>
    public bool IsMuted { get; private set; }

    /// <summary>
    /// Opens the sound device and loads the short sounds and the music from <paramref name="directory"/> (not the old
    /// ambience, which the catacomb music replaces).
    /// Call after the window is open.
    /// </summary>
    public static SoundPlayer Open(string directory, FileLog? log)
    {
        Raylib.InitAudioDevice();
        if (!Raylib.IsAudioDeviceReady())
        {
            log?.Warning("No sound device could be opened - the game runs silent.");
            return new SoundPlayer(false, [], [], log);
        }

        Raylib.SetMasterVolume(SoundBoard.MasterVolume);
        log?.Info(string.Create(CultureInfo.InvariantCulture, $"Sound device opened, master volume {SoundBoard.MasterVolume:0.00}."));

        ModelChecksums? list = null;
        try
        {
            list = ModelChecksums.Parse(File.ReadAllText(Path.Combine(directory, SoundBoard.ChecksumFileName)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            log?.Warning($"Sounds in '{directory}': the checksum list could not be read ({exception.Message}) - the game runs silent.");
        }

        var sounds = new Dictionary<string, Sound>(StringComparer.Ordinal);
        var music = new Dictionary<MusicTrack, Music>();
        if (list is not null)
        {
            foreach (var name in SoundBoard.SoundFiles)
            {
                if (Checked(directory, name, list, log) is { } path)
                {
                    var sound = Raylib.LoadSound(path);
                    if (Raylib.IsSoundValid(sound))
                    {
                        sounds[name] = sound;
                    }
                    else
                    {
                        log?.Warning($"Sound '{name}' could not be loaded - it stays silent.");
                    }
                }
            }

            log?.Info($"Sounds loaded ({sounds.Count} of {SoundBoard.SoundFiles.Length}): {string.Join(", ", sounds.Keys.Order(StringComparer.Ordinal))}.");

            foreach (var track in SoundBoard.MusicTracks)
            {
                var name = SoundBoard.MusicFile(track)!;
                if (Checked(directory, name, list, log) is { } path)
                {
                    var stream = Raylib.LoadMusicStream(path);
                    if (Raylib.IsMusicValid(stream))
                    {
                        stream.Looping = true;
                        Raylib.SetMusicVolume(stream, 0f);
                        music[track] = stream;
                        log?.Info(string.Create(
                            CultureInfo.InvariantCulture,
                            $"Music loaded: {track} '{name}', {Raylib.GetMusicTimeLength(stream):0.0} s, volume {SoundBoard.MusicVolume(track):0.00}."));
                    }
                    else
                    {
                        log?.Warning($"Music '{name}' could not be loaded - {track} stays silent.");
                    }
                }
            }
        }

        return new SoundPlayer(true, sounds, music, log);
    }

    /// <summary>
    /// Plays <paramref name="cues"/> (the sounds of one tick), each at its volume and to its side (issue #491). raylib's
    /// pan runs from -1 (only the left speaker) to 1 (only the right), like <see cref="SoundCue.Pan"/> - measured with
    /// raylib 6 under Raylib-cs 8.1.0. A cue whose file is not loaded is left out; a cue with <see cref="SoundCue.Stop"/>
    /// stops its file (issue #776).
    /// <para>
    /// One sound per file: a sound that starts again while it still plays begins anew at the new place. A source that
    /// moves while its sound plays is heard where it was when the sound began.
    /// </para>
    /// </summary>
    public void Play(IEnumerable<SoundCue> cues)
    {
        ArgumentNullException.ThrowIfNull(cues);
        foreach (var cue in cues)
        {
            if (_sounds.TryGetValue(cue.File, out var sound) && cue.Stop)
            {
                // Stop on a paused sound does nothing (raylib, checked with Raylib-cs): a held sound is first taken out
                // of its pause - and out of the held ones, so the end of the stillness does not resume it.
                if (_held.Stop(cue.File))
                {
                    Raylib.ResumeSound(sound);
                }

                Raylib.StopSound(sound);
            }
            else if (_sounds.TryGetValue(cue.File, out sound))
            {
                Raylib.SetSoundVolume(sound, cue.Volume * _effects);
                Raylib.SetSoundPan(sound, cue.Pan);
                Raylib.PlaySound(sound);
            }
        }
    }

    /// <summary>
    /// Once per drawn frame: while <paramref name="held"/> (the world stands still - menu, lost focus), the sounds of
    /// <paramref name="files"/> that play are held where they are, and go on from there when the world does (issue #776:
    /// a charge sound whose thump must fall on the full charge). A file that stopped meanwhile stays silent
    /// (<see cref="HeldSounds"/>). Stop on a paused sound does not work in raylib: <see cref="Play"/> resumes a held
    /// sound before it stops it.
    /// </summary>
    public void Hold(IReadOnlyList<string> files, bool held)
    {
        ArgumentNullException.ThrowIfNull(files);
        var playing = files.Where(file => _sounds.TryGetValue(file, out var sound) && Raylib.IsSoundPlaying(sound));
        var step = _held.Frame(playing, held);
        foreach (var file in step.Pause)
        {
            Raylib.PauseSound(_sounds[file]);
        }

        foreach (var file in step.Resume)
        {
            Raylib.ResumeSound(_sounds[file]);
        }
    }

    /// <summary>
    /// Once per drawn frame: lets <see cref="MusicMixer"/> step the fades towards <paramref name="wish"/> (from
    /// <see cref="SoundBoard.MusicFor"/>) and plays what it says - one stream, every other one stopped.
    /// </summary>
    public void Update(MusicWish wish, float frameSeconds)
    {
        var mix = _mixer.Step(wish, frameSeconds);
        if (mix.Track != _playing || mix.Start)
        {
            if (_music.TryGetValue(_playing, out var old))
            {
                Raylib.StopMusicStream(old);
            }

            _playing = mix.Track;
            _paused = false;
            if (_music.TryGetValue(_playing, out var next))
            {
                Raylib.SetMusicVolume(next, 0f);
                Raylib.PlayMusicStream(next);
                _log?.Info($"Music started: {_playing} '{SoundBoard.MusicFile(_playing)}'.");
            }
        }

        if (!_music.TryGetValue(_playing, out var stream))
        {
            return;
        }

        if (mix.Paused != _paused)
        {
            if (mix.Paused)
            {
                Raylib.PauseMusicStream(stream);
            }
            else
            {
                Raylib.ResumeMusicStream(stream);
            }

            _paused = mix.Paused;
            _log?.Info(_paused ? $"Music held: {_playing}." : $"Music goes on: {_playing}.");
        }

        Raylib.SetMusicVolume(stream, mix.Volume * _ambience);
        if (!_paused)
        {
            Raylib.UpdateMusicStream(stream);
        }
    }

    /// <summary>N: everything silent, or back to the master volume.</summary>
    public void ToggleMute()
    {
        IsMuted = !IsMuted;
        ApplyMaster();
        _log?.Info(IsMuted ? "Sound muted (N)." : "Sound on again (N).");
    }

    /// <summary>
    /// Takes the player's volumes (issue #349): the master volume at once, effects with the next sound, the ambience
    /// with the next frame of music.
    /// </summary>
    public void SetVolumes(float master, float effects, float ambience)
    {
        _master = master;
        _effects = effects;
        _ambience = ambience;
        ApplyMaster();
    }

    private void ApplyMaster()
    {
        if (_deviceOpen)
        {
            Raylib.SetMasterVolume(IsMuted ? 0f : SoundBoard.MasterVolume * _master);
        }
    }

    public void Dispose()
    {
        if (!_deviceOpen)
        {
            return;
        }

        foreach (var sound in _sounds.Values)
        {
            Raylib.UnloadSound(sound);
        }

        _sounds.Clear();
        foreach (var stream in _music.Values)
        {
            Raylib.StopMusicStream(stream);
            Raylib.UnloadMusicStream(stream);
        }

        _music.Clear();

        Raylib.CloseAudioDevice();
        _deviceOpen = false;
    }

    /// <summary>The full path of <paramref name="name"/>, or null (with a warning) if it is missing or does not match the list.</summary>
    private static string? Checked(string directory, string name, ModelChecksums list, FileLog? log)
    {
        var path = Path.Combine(directory, name);
        var problem = list.Check(path);
        if (problem is null)
        {
            return path;
        }

        log?.Warning($"Sound '{name}' is not played: {problem}.");
        return null;
    }
}
