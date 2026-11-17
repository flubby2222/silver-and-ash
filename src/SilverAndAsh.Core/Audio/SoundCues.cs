using System.Collections.Immutable;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Audio;

/// <summary>
/// The short sounds of the game (issues #267, #290); the music loops on its own (<see cref="MusicTrack"/>). Which file
/// plays for each is up to the <see cref="SoundPack"/>.
/// </summary>
public enum SoundKind
{
    /// <summary>A pistol shot.</summary>
    Pistol,

    /// <summary>A pistol shot with consecrated ammunition.</summary>
    ConsecratedShot,

    /// <summary>A player starts to reload.</summary>
    ReloadStarted,

    /// <summary>A player's weapon is loaded again.</summary>
    ReloadFinished,

    /// <summary>A swing of the silver blade that hits nothing.</summary>
    BladeSwing,

    /// <summary>The silver blade hitting a target.</summary>
    Blade,

    /// <summary>A player's step with the left foot.</summary>
    StepLeft,

    /// <summary>A player's step with the right foot.</summary>
    StepRight,

    /// <summary>A player is hurt.</summary>
    PlayerHurt,

    /// <summary>A ghoul drawing back for a blow - the warning before it strikes.</summary>
    GhoulWindUp,

    /// <summary>A ghoul dies.</summary>
    GhoulDied,

    /// <summary>A door opens.</summary>
    DoorOpened,

    /// <summary>A chest opens.</summary>
    ChestOpened,

    /// <summary>A player picks up silver.</summary>
    SilverTaken,

    /// <summary>A player picks up a blood vial.</summary>
    BloodVialTaken,

    /// <summary>A player picks up consecrated ammunition.</summary>
    ConsecratedAmmoTaken,

    /// <summary>A player picks up a key.</summary>
    KeyTaken,

    /// <summary>A player jumps (issue #291).</summary>
    Jump,

    /// <summary>A player dashes in the air or dodges on the ground - both are the Shift move (issue #291).</summary>
    Dash,

    /// <summary>A player lands after a drop of at least <see cref="SoundBoard.LandingSoundMinimumFallHeight"/> (issue #291).</summary>
    Land,

    /// <summary>A ghoul notices a player and starts to chase (issue #291).</summary>
    GhoulNoticed,

    /// <summary>A player's consecrated ammunition runs out (issue #291).</summary>
    ConsecrationEnded,

    /// <summary>A menu button is selected or chosen - played by the game itself, no event of Core (issue #291).</summary>
    MenuClick,
}

/// <summary>One sound to play now: which file, at which volume (0 to 1, before <see cref="SoundBoard.MasterVolume"/>).</summary>
public readonly record struct SoundCue(SoundKind Kind, string File, float Volume);

/// <summary>
/// The files of assets/audio/ and their volumes, as the audio designer handed them over (issues #267, #290; which file
/// plays for which sound is in <see cref="SoundPack"/>). The files
/// already carry the loudness relation between each other, so every short sound plays at 1 and one master volume
/// turns all of them down together. The music has values of its own (issue #271), because nobody could judge it in the game yet.
/// </summary>
public static class SoundBoard
{
    /// <summary>Name of the checksum list next to the sounds (same format as the model list).</summary>
    public const string ChecksumFileName = "audio.sha256";

    /// <summary>
    /// The old ambience of the catacombs (issue #267). Since issue #271 the catacomb music plays instead; the file stays
    /// checked in until the new sounds replace or remove it, but the game no longer loads it.
    /// </summary>
    public const string Ambience = "ambiente-katakomben.ogg";

    /// <summary>The theme of the title screen, a seamless loop of 72 s.</summary>
    public const string TitleMusic = "musik-hauptmenue.ogg";

    /// <summary>The music of the catacombs, a seamless loop of 64 s - sparser and quieter, so the sounds stand above it.</summary>
    public const string CatacombsMusic = "musik-katakomben.ogg";

    /// <summary>Volume of everything the game plays (the designer's suggestion).</summary>
    public const float MasterVolume = 0.7f;

    /// <summary>Volume of the title theme (before <see cref="MasterVolume"/>); the louder of the two files.</summary>
    public const float TitleMusicVolume = 0.8f;

    /// <summary>Volume of the catacomb music (before <see cref="MasterVolume"/>); the file itself is about 12 LU quieter.</summary>
    public const float CatacombsMusicVolume = 1f;

    /// <summary>Seconds a piece of music takes to fade out before the next one starts.</summary>
    public const float MusicFadeOutSeconds = 1.5f;

    /// <summary>Seconds a piece of music takes to fade in from silence.</summary>
    public const float MusicFadeInSeconds = 3f;

    /// <summary>
    /// Volume of every short sound (before <see cref="MasterVolume"/>). The files carry their loudness in themselves -
    /// the audio designer set the peak per category (tools/audio/paket1/uebersicht.md) - so there is no volume per kind.
    /// </summary>
    public const float SoundVolume = 1f;

    /// <summary>
    /// Metres a player must drop (from the highest point in the air) for a landing to be heard (issue #291). Above the
    /// step height of 0.4 m (<see cref="BodySizes.Player"/>), so walking up and down steps stays silent; a normal jump
    /// on flat ground drops about 1.3 m and is heard. Presentation, not a game rule - so not in balance.json.
    /// </summary>
    public const float LandingSoundMinimumFallHeight = 0.6f;

    /// <summary>Every short sound the game loads from assets/audio/: the files of every <see cref="SoundPack"/>.</summary>
    public static ImmutableArray<string> SoundFiles => [.. SoundPack.All.SelectMany(pack => pack.Files).Distinct()];

    /// <summary>The file of <paramref name="track"/>, or null for no music.</summary>
    public static string? MusicFile(MusicTrack track) => track switch
    {
        MusicTrack.None => null,
        MusicTrack.Title => TitleMusic,
        MusicTrack.Catacombs => CatacombsMusic,
        _ => throw new ArgumentOutOfRangeException(nameof(track), track, "No file for this track."),
    };

    /// <summary>The volume of <paramref name="track"/> (before <see cref="MasterVolume"/>), 0 for no music.</summary>
    public static float MusicVolume(MusicTrack track) => track switch
    {
        MusicTrack.None => 0f,
        MusicTrack.Title => TitleMusicVolume,
        MusicTrack.Catacombs => CatacombsMusicVolume,
        _ => throw new ArgumentOutOfRangeException(nameof(track), track, "No volume for this track."),
    };

    /// <summary>Every piece of music the game loads (every <see cref="MusicTrack"/> but <see cref="MusicTrack.None"/>).</summary>
    public static ImmutableArray<MusicTrack> MusicTracks => [.. Enum.GetValues<MusicTrack>().Where(track => track != MusicTrack.None)];

    /// <summary>
    /// Every file of assets/audio/: the short sounds, the files of sound pack 1 that wait for their events, the music and
    /// the old ambience (no longer played).
    /// </summary>
    public static ImmutableArray<string> AllFiles =>
        [.. SoundFiles, .. SoundPack.Pack1WithoutEvent, .. MusicTracks.Select(track => MusicFile(track)!), Ambience];

    /// <summary>
    /// Which music <paramref name="situation"/> asks for (issue #271): the title theme on the title screen; the catacomb
    /// music while a run is played, held where it is while the Esc menu holds the world; none on the end screen - the
    /// run is over, the silence marks it, and the title theme belongs to the title.
    /// <para>
    /// More pieces for parts of a run (a boss fight, the merchant) come in as a value of <see cref="MusicTrack"/>, a field
    /// of <see cref="MusicSituation"/> and one line here before the catacombs.
    /// </para>
    /// </summary>
    public static MusicWish MusicFor(MusicSituation situation) =>
        situation.OnTitle ? new MusicWish(MusicTrack.Title, false)
        : situation.RunOver ? new MusicWish(MusicTrack.None, false)
        : new MusicWish(MusicTrack.Catacombs, situation.Paused);

    /// <summary>
    /// Which sound <paramref name="gameEvent"/> asks for, or null for none: a pistol shot (consecrated or not), reloading,
    /// a swing and a hit of the silver blade (not one with the pistol or the club), a footstep per foot, a hurt player,
    /// a ghoul's wind-up and death, a door or chest opening, picking up silver, a consumable or a key; a jump, a dash or
    /// dodge, a landing from high enough (<see cref="LandingSoundMinimumFallHeight"/>), a ghoul noticing a player and
    /// the end of the consecration (issue #291).
    /// </summary>
    public static SoundKind? KindFor(GameEvent gameEvent) => gameEvent switch
    {
        // Other ranged weapons get files of their own later.
        ShotFired { Weapon: WeaponKind.Pistol, Consecrated: true } => SoundKind.ConsecratedShot,
        ShotFired { Weapon: WeaponKind.Pistol } => SoundKind.Pistol,
        ReloadStarted => SoundKind.ReloadStarted,
        ReloadFinished => SoundKind.ReloadFinished,

        // The club has no sound yet.
        MeleeSwung { Weapon: WeaponKind.SilverBlade } => SoundKind.BladeSwing,
        TargetHitBy { Weapon: Weapon.Blade } => SoundKind.Blade,
        PlayerStepped { Foot: Foot.Left } => SoundKind.StepLeft,
        PlayerStepped => SoundKind.StepRight,
        PlayerHit => SoundKind.PlayerHurt,
        GhoulWindUp => SoundKind.GhoulWindUp,
        GhoulDied => SoundKind.GhoulDied,
        DoorOpened => SoundKind.DoorOpened,
        ChestOpened => SoundKind.ChestOpened,
        SilverTaken => SoundKind.SilverTaken,
        PickupTaken { Kind: ConsumableKind.BloodVial } => SoundKind.BloodVialTaken,
        PickupTaken { Kind: ConsumableKind.ConsecratedAmmo } => SoundKind.ConsecratedAmmoTaken,
        KeyTaken => SoundKind.KeyTaken,
        PlayerJumped => SoundKind.Jump,
        PlayerDashed or PlayerDodged => SoundKind.Dash,
        PlayerLanded { FallHeight: >= LandingSoundMinimumFallHeight } => SoundKind.Land,
        GhoulNoticed => SoundKind.GhoulNoticed,
        ConsecrationEnded => SoundKind.ConsecrationEnded,
        _ => null,
    };
}

/// <summary>
/// Turns the events of a tick into the sounds to play (issues #267, #290) - pure logic, testable without a sound device.
/// <list type="bullet">
/// <item>Each set of files plays at most once per tick (a charged blade that hits three ghouls is one sound, not three
/// at once - without spatial sound they would only add up to one louder one). Kinds that share their files in a pack
/// (the old pack has one set for both feet) count as one.</item>
/// <item>A blade swing that hits something in the same tick plays only the hit: Core resolves the swing in the tick it
/// is made, so a <see cref="TargetHitBy"/> with the blade comes in the same events. The swing sound is the sound of a
/// miss; both together would smear into one.</item>
/// <item>Of several files it picks one at random, never the one it picked last time from that set.</item>
/// </list>
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

    // The last pick of each set of files, keyed by the files themselves: kinds sharing them share it, too.
    private readonly Dictionary<string, int> _lastVariant = new(StringComparer.Ordinal);

    /// <summary>Starts the choice of variants from <paramref name="seed"/> (the world's seed).</summary>
    public SoundCues(int seed) => _random = new Random(seed ^ SeedSalt);

    /// <summary>
    /// The sounds of <paramref name="pack"/> to play for <paramref name="events"/> (the events of one tick), in the order
    /// they first appear.
    /// </summary>
    public IReadOnlyList<SoundCue> For(IEnumerable<GameEvent> events, SoundPack pack)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(pack);
        var kinds = events.Select(SoundBoard.KindFor).OfType<SoundKind>().Distinct().ToList();
        if (kinds.Contains(SoundKind.Blade))
        {
            kinds.Remove(SoundKind.BladeSwing);
        }

        var cues = new List<SoundCue>();
        var setsPlayed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kind in kinds)
        {
            var files = pack.Variants(kind);
            var set = string.Join('|', files);
            if (files.IsEmpty || !setsPlayed.Add(set))
            {
                continue;
            }

            cues.Add(new SoundCue(kind, files[NextVariant(set, files.Length)], SoundBoard.SoundVolume));
        }

        return cues;
    }

    /// <summary>
    /// The sound of <paramref name="pack"/> for <paramref name="kind"/> without an event of Core - the game's own
    /// sounds such as <see cref="SoundKind.MenuClick"/> (issue #291). Empty if the pack has none.
    /// </summary>
    public IReadOnlyList<SoundCue> For(SoundKind kind, SoundPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var files = pack.Variants(kind);
        return files.IsEmpty ? [] : [new SoundCue(kind, files[NextVariant(string.Join('|', files), files.Length)], SoundBoard.SoundVolume)];
    }

    /// <summary>A random one of the <paramref name="count"/> files of <paramref name="set"/>, never the same as last time (if it has more than one).</summary>
    private int NextVariant(string set, int count)
    {
        int choice;
        if (count == 1)
        {
            choice = 0;
        }
        else if (_lastVariant.TryGetValue(set, out var last))
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

        _lastVariant[set] = choice;
        return choice;
    }
}
