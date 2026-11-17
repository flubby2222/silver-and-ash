using System.Collections.Immutable;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Audio;

/// <summary>The short sounds of the game (issue #267); the ambience is a loop of its own (<see cref="SoundBoard.Ambience"/>).</summary>
public enum SoundKind
{
    /// <summary>A pistol shot.</summary>
    Pistol,

    /// <summary>The silver blade hitting a target.</summary>
    Blade,

    /// <summary>A player's footstep.</summary>
    Step,

    /// <summary>A ghoul drawing back for a blow - the warning before it strikes.</summary>
    GhoulWindUp,
}

/// <summary>One sound to play now: which file, at which volume (0 to 1, before <see cref="SoundBoard.MasterVolume"/>).</summary>
public readonly record struct SoundCue(SoundKind Kind, string File, float Volume);

/// <summary>
/// The files of assets/audio/ and their volumes, as the audio designer handed them over (issue #267). The files
/// already carry the loudness relation between each other, so every short sound plays at 1 and one master volume
/// turns all of them down together. The ambience has a value of its own, because nobody could judge it in the game yet.
/// </summary>
public static class SoundBoard
{
    /// <summary>Name of the checksum list next to the sounds (same format as the model list).</summary>
    public const string ChecksumFileName = "audio.sha256";

    /// <summary>The ambience of the catacombs, a seamless loop of 30 s.</summary>
    public const string Ambience = "ambiente-katakomben.ogg";

    /// <summary>Volume of everything the game plays (the designer's suggestion).</summary>
    public const float MasterVolume = 0.7f;

    /// <summary>Volume of the ambience loop (before <see cref="MasterVolume"/>).</summary>
    public const float AmbienceVolume = 1f;

    /// <summary>Volume of every short sound (before <see cref="MasterVolume"/>); the files carry their loudness in themselves.</summary>
    public const float SoundVolume = 1f;

    /// <summary>The files of <paramref name="kind"/>: one of them plays each time, never the same twice in a row.</summary>
    public static ImmutableArray<string> Variants(SoundKind kind) => kind switch
    {
        SoundKind.Pistol => ["pistole.wav"],
        SoundKind.Blade => ["klinge-1.wav", "klinge-2.wav", "klinge-3.wav"],
        SoundKind.Step => ["schritt-1.wav", "schritt-2.wav", "schritt-3.wav", "schritt-4.wav"],
        SoundKind.GhoulWindUp => ["ghul-ankuendigung.wav"],
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No sound for this kind."),
    };

    /// <summary>Every file the game loads from assets/audio/.</summary>
    public static ImmutableArray<string> AllFiles =>
        [.. Enum.GetValues<SoundKind>().SelectMany(kind => Variants(kind)), Ambience];

    /// <summary>
    /// Whether the ambience loop runs: while a run is being played - not on the title screen, not while the Esc menu
    /// holds the world, not once the run is over (end screen).
    /// </summary>
    public static bool AmbiencePlays(bool onTitle, bool paused, bool runOver) => !onTitle && !paused && !runOver;

    /// <summary>
    /// Which sound <paramref name="gameEvent"/> asks for, or null for none: a pistol shot, a hit with the silver blade
    /// (not one with the pistol or the club), a footstep, a ghoul's wind-up.
    /// </summary>
    public static SoundKind? KindFor(GameEvent gameEvent) => gameEvent switch
    {
        // Other ranged weapons get files of their own later.
        ShotFired { Weapon: WeaponKind.Pistol } => SoundKind.Pistol,
        TargetHitBy { Weapon: Weapon.Blade } => SoundKind.Blade,
        PlayerStepped => SoundKind.Step,
        GhoulWindUp => SoundKind.GhoulWindUp,
        _ => null,
    };
}

/// <summary>
/// Turns the events of a tick into the sounds to play (issue #267) - pure logic, testable without a sound device.
/// Each kind plays at most once per tick (a charged blade that hits three ghouls is one sound, not three at once -
/// without spatial sound they would only add up to one louder one). Of several files of a kind it picks one at random,
/// never the one it picked last time.
/// <para>
/// The random choice has its own generator, started from the world's seed: the world's own random numbers (spread,
/// loot, boons) stay untouched, so the same seed gives the same run with or without sound (online co-op rule 3).
/// </para>
/// </summary>
public sealed class SoundCues
{
    // Mixed into the seed so this generator does not run in step with any of the world's (which start from the seed).
    private const int SeedSalt = 0x50554E44;

    private readonly Random _random;
    private readonly Dictionary<SoundKind, int> _lastVariant = [];

    /// <summary>Starts the choice of variants from <paramref name="seed"/> (the world's seed).</summary>
    public SoundCues(int seed) => _random = new Random(seed ^ SeedSalt);

    /// <summary>The sounds to play for <paramref name="events"/> (the events of one tick), in the order they first appear.</summary>
    public IReadOnlyList<SoundCue> For(IEnumerable<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var cues = new List<SoundCue>();
        foreach (var kind in events.Select(SoundBoard.KindFor).OfType<SoundKind>().Distinct())
        {
            cues.Add(new SoundCue(kind, SoundBoard.Variants(kind)[NextVariant(kind)], SoundBoard.SoundVolume));
        }

        return cues;
    }

    /// <summary>A random variant of <paramref name="kind"/>, never the same as last time (if it has more than one).</summary>
    private int NextVariant(SoundKind kind)
    {
        var count = SoundBoard.Variants(kind).Length;
        int choice;
        if (count == 1)
        {
            choice = 0;
        }
        else if (_lastVariant.TryGetValue(kind, out var last))
        {
            // One of the others: draw among count - 1 and skip over the last one.
            choice = _random.Next(count - 1);
            if (choice >= last)
            {
                choice++;
            }
        }
        else
        {
            choice = _random.Next(count);
        }

        _lastVariant[kind] = choice;
        return choice;
    }
}
