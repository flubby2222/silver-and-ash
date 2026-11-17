using System.Globalization;
using Raylib_cs;
using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Audio;

/// <summary>
/// Plays the sounds of assets/audio/ with raylib (issue #267): the short sounds that <see cref="SoundCues"/> picks from
/// the events of Core, and the ambience as a looping stream while a run is played. No game rules in here.
/// <para>
/// Never fails: without a sound device the game runs silent, and a file that is missing, does not match the checksum
/// list or cannot be loaded costs a warning in the log - that one sound stays silent, everything else plays.
/// </para>
/// </summary>
internal sealed class SoundPlayer : IDisposable
{
    private readonly Dictionary<string, Sound> _sounds;
    private readonly Music? _ambience;
    private bool _deviceOpen;
    private readonly FileLog? _log;
    private bool _ambienceStarted;
    private bool _ambiencePaused;

    private SoundPlayer(bool deviceOpen, Dictionary<string, Sound> sounds, Music? ambience, FileLog? log)
    {
        _deviceOpen = deviceOpen;
        _sounds = sounds;
        _ambience = ambience;
        _log = log;
    }

    /// <summary>True while N has muted the game.</summary>
    public bool IsMuted { get; private set; }

    /// <summary>
    /// Opens the sound device and loads every file of <see cref="SoundBoard.AllFiles"/> from <paramref name="directory"/>.
    /// Call after the window is open.
    /// </summary>
    public static SoundPlayer Open(string directory, FileLog? log)
    {
        Raylib.InitAudioDevice();
        if (!Raylib.IsAudioDeviceReady())
        {
            log?.Warning("No sound device could be opened - the game runs silent.");
            return new SoundPlayer(false, [], null, log);
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
        Music? ambience = null;
        if (list is not null)
        {
            foreach (var name in SoundBoard.AllFiles.Where(name => name != SoundBoard.Ambience))
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

            if (Checked(directory, SoundBoard.Ambience, list, log) is { } ambiencePath)
            {
                var music = Raylib.LoadMusicStream(ambiencePath);
                if (Raylib.IsMusicValid(music))
                {
                    music.Looping = true;
                    Raylib.SetMusicVolume(music, SoundBoard.AmbienceVolume);
                    ambience = music;
                }
                else
                {
                    log?.Warning($"Ambience '{SoundBoard.Ambience}' could not be loaded - it stays silent.");
                }
            }

            var loaded = sounds.Keys.Order(StringComparer.Ordinal).Concat(ambience is null ? [] : [SoundBoard.Ambience]).ToList();
            log?.Info($"Sounds loaded ({loaded.Count} of {SoundBoard.AllFiles.Length}): {string.Join(", ", loaded)}.");
        }

        return new SoundPlayer(true, sounds, ambience, log);
    }

    /// <summary>Plays <paramref name="cues"/> (the sounds of one tick). A cue whose file is not loaded is left out.</summary>
    public void Play(IEnumerable<SoundCue> cues)
    {
        ArgumentNullException.ThrowIfNull(cues);
        foreach (var cue in cues)
        {
            if (_sounds.TryGetValue(cue.File, out var sound))
            {
                Raylib.SetSoundVolume(sound, cue.Volume);
                Raylib.PlaySound(sound);
            }
        }
    }

    /// <summary>
    /// Once per drawn frame: keeps the ambience stream fed while <paramref name="ambiencePlays"/>, pauses it otherwise
    /// (it goes on where it stopped).
    /// </summary>
    public void Update(bool ambiencePlays)
    {
        if (_ambience is not { } music)
        {
            return;
        }

        if (ambiencePlays)
        {
            if (!_ambienceStarted)
            {
                Raylib.PlayMusicStream(music);
                _ambienceStarted = true;
            }
            else if (_ambiencePaused)
            {
                Raylib.ResumeMusicStream(music);
            }

            _ambiencePaused = false;
            Raylib.UpdateMusicStream(music);
        }
        else if (_ambienceStarted && !_ambiencePaused)
        {
            Raylib.PauseMusicStream(music);
            _ambiencePaused = true;
        }
    }

    /// <summary>N: everything silent, or back to the master volume.</summary>
    public void ToggleMute()
    {
        IsMuted = !IsMuted;
        if (_deviceOpen)
        {
            Raylib.SetMasterVolume(IsMuted ? 0f : SoundBoard.MasterVolume);
        }

        _log?.Info(IsMuted ? "Sound muted (N)." : "Sound on again (N).");
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
        if (_ambience is { } music)
        {
            Raylib.StopMusicStream(music);
            Raylib.UnloadMusicStream(music);
        }

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
