using System.Collections.Immutable;
using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

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

    /// <summary>A bat's wing beat, near enough to be heard (issue #297).</summary>
    BatFlap,

    /// <summary>A bat starts glowing before a spit - the warning (issue #297).</summary>
    BatSpitWindUp,

    /// <summary>A bat spits a blood bolt (issue #297).</summary>
    BatSpat,

    /// <summary>A bat screeches before its dive - the warning (issue #297).</summary>
    BatScreech,

    /// <summary>A bat bites a player in its dive (issue #297).</summary>
    BatBite,

    /// <summary>A bat dives into empty air (issue #297).</summary>
    BatDiveMissed,

    /// <summary>A bat is hit (issue #297).</summary>
    BatHurt,

    /// <summary>A bat dies (issue #297).</summary>
    BatDied,

    /// <summary>Gorm puts a heavy foot down (issue #298).</summary>
    GormStep,

    /// <summary>Gorm raises his club - the warning before the hammer blow: jump (issue #298).</summary>
    GormHammerWindUp,

    /// <summary>Gorm spins up his chain - the warning before the swing: duck in a dodge (issue #298).</summary>
    GormSwingWindUp,

    /// <summary>Gorm's hook glows - the warning before the throw: step aside (issue #298).</summary>
    GormThrowWindUp,

    /// <summary>Gorm's hammer strikes the floor, the shock wave starts (issue #298).</summary>
    GormSlam,

    /// <summary>Gorm's chain sweeps round (issue #298) - from the start of the sweep, not when it is over.</summary>
    GormChain,

    /// <summary>Gorm throws his hook (issue #298).</summary>
    GormThrow,

    /// <summary>Gorm's hook hits a player (issue #298).</summary>
    GormHookHit,

    /// <summary>Gorm is hit on his body (issue #298).</summary>
    GormHurt,

    /// <summary>Gorm is hit on his head - a critical hit (issue #298).</summary>
    GormHeadHurt,

    /// <summary>Gorm roars at half health: phase 2 (issue #298).</summary>
    GormRoar,

    /// <summary>Gorm dies (issue #298).</summary>
    GormDied,

    /// <summary>A player takes a piece of equipment from the floor with F - Worn, Forged or Royal (issue #317).</summary>
    EquipmentTaken,

    /// <summary>
    /// A player takes a Vampiric piece - the top tier gets a sound of its own (issue #317, the human's wish on #195).
    /// Until the audio designer has made it, it plays the same file as <see cref="EquipmentTaken"/>.
    /// </summary>
    VampiricEquipmentTaken,

    /// <summary>
    /// A player gains a skill point: a level up, which offers one boon to choose (issue #375). A deep strike with a
    /// short dark choir, chosen by the human in the listening test of issue #372.
    /// </summary>
    SkillPoint,

    /// <summary>
    /// A free-standing column takes a blow and still has more than one left (Z1, issue #605): a heavy, dull crack with
    /// a short trickle of rubble, chosen by the human in round 4 of the listening test.
    /// </summary>
    ColumnHit1,

    /// <summary>
    /// A column takes the blow before its last one (Z2, issue #605): longer rubble with gaps - deliberately the brighter
    /// take of the listening test, so the last warning before the break stands out.
    /// </summary>
    ColumnHit2,

    /// <summary>
    /// A column breaks (issue #605): grinding, the upper part landing at 0.70 s, then rubble. Plays instead of the blow
    /// that broke it.
    /// </summary>
    ColumnBroke,

    /// <summary>
    /// The boss door swings shut (issue #569): heavy rumbling through the run-up, then the slam - 78 ticks after the
    /// sound begins, where the leaves hit their stop (<see cref="DoorsBalance.BossShutTick"/>) - and a long echo.
    /// Take 2 of round 2 of the listening test of issue #549.
    /// </summary>
    BossDoorClosing,

    /// <summary>
    /// The boss door swings open (issue #569): stone grinding over fine rubble for its 132 ticks of opening, without a
    /// stop. Take 1 of round 2 of the listening test of issue #549. Plays instead of the creak of an ordinary door
    /// (<see cref="DoorOpened"/>) when a player opens the boss door.
    /// </summary>
    BossDoorOpening,
}

/// <summary>
/// One sound to play now: which file, at which volume (0 to 1, before <see cref="SoundBoard.MasterVolume"/>) and how far
/// to the side (<see cref="SoundMix.Pan"/>: -1 left, 0 the middle, 1 right; issue #491).
/// </summary>
public readonly record struct SoundCue(SoundKind Kind, string File, float Volume, float Pan = 0f);

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

    /// <summary>Volume of the catacomb music (before <see cref="MasterVolume"/>); the file itself is about as loud as the title file (-22.5 LUFS, raised by 10 dB in issue #427).</summary>
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

    /// <summary>
    /// Metres from the listener (a player's eyes) within which a bat's wing beat is heard (issue #297). The game has no
    /// spatial sound yet (#225), so this is a plain threshold: a bat circling a player (4 to 6 m) is heard, one across
    /// the room is not. Presentation, not a game rule - so not in balance.json.
    /// </summary>
    public const float BatFlapHearingDistance = 8f;

    /// <summary>
    /// Seconds at least between two wing beats that are heard (issue #297), whichever bat beats: several bats round a
    /// player flap out of step, and without this they would add up to a rattle. One bat beats every
    /// <see cref="Bat.FlapInterval"/>, which is longer, so a single bat is heard on every beat.
    /// </summary>
    public const float BatFlapMinimumGap = 0.25f;

    /// <summary>
    /// The kinds that are a creature's own sound of being hit (issue #297). When one of them plays, the blade's hit
    /// of the same tick does not: both are short knocks and would smear into one, and the creature's own one says what
    /// was hit. (Without a file for it in a pack, the blade's hit plays as before.)
    /// </summary>
    public static ImmutableArray<SoundKind> OwnHitKinds =>
        [SoundKind.BatHurt, SoundKind.GormHurt, SoundKind.GormHeadHurt];

    /// <summary>The seconds that must at least pass between two sounds of <paramref name="kind"/> (0: no limit).</summary>
    public static float MinimumGap(SoundKind kind) => kind == SoundKind.BatFlap ? BatFlapMinimumGap : 0f;

    /// <summary>
    /// Whether <paramref name="gameEvent"/> happens near enough to <paramref name="listener"/> to be heard. Only the
    /// wing beat has a range (<see cref="BatFlapHearingDistance"/>); without a listener everything counts as near.
    /// </summary>
    public static bool IsInEarshot(GameEvent gameEvent, Vector3? listener) => gameEvent switch
    {
        BatFlapped flap when listener is { } ear => Vector3.Distance(flap.At, ear) <= BatFlapHearingDistance,
        _ => true,
    };

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
    /// the end of the consecration (issue #291); a bat's wing beat, warnings, spit, bite, missed dive, hit and death
    /// (issue #297); Gorm's steps, a warning of its own for each of his three attacks, the hammer's impact, the chain,
    /// the throw and the hook's hit, a hit on his body or head, his roar and his death (issue #298); taking equipment,
    /// a Vampiric piece with a sound of its own (issue #317); a skill point from a level up (issue #375); the events
    /// of <see cref="KindsByEventType"/> - a blow on a column by its stage and the column's break (issue #605), the boss
    /// door swinging shut or open (issue #569).
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
        BatFlapped => SoundKind.BatFlap,
        BatSpitWindUp => SoundKind.BatSpitWindUp,
        BatSpat => SoundKind.BatSpat,
        BatScreeched => SoundKind.BatScreech,
        BatBit => SoundKind.BatBite,
        BatDiveMissed => SoundKind.BatDiveMissed,
        BatHit => SoundKind.BatHurt,
        BatDied => SoundKind.BatDied,
        GormStepped => SoundKind.GormStep,
        GormWindUp { Attack: GormAttack.Hammer } => SoundKind.GormHammerWindUp,
        GormWindUp { Attack: GormAttack.Swing } => SoundKind.GormSwingWindUp,
        GormWindUp => SoundKind.GormThrowWindUp,
        GormSlammed => SoundKind.GormSlam,
        GormSwingStarted => SoundKind.GormChain,
        GormThrew => SoundKind.GormThrow,
        HookHit => SoundKind.GormHookHit,
        GormHit { Critical: true } => SoundKind.GormHeadHurt,
        GormHit => SoundKind.GormHurt,
        GormRoared => SoundKind.GormRoar,
        GormDied => SoundKind.GormDied,
        EquipmentTaken { Tier: ItemTier.Vampiric } => SoundKind.VampiricEquipmentTaken,
        EquipmentTaken => SoundKind.EquipmentTaken,
        LevelUp => SoundKind.SkillPoint,
        _ => KindsByEventType.TryGetValue(gameEvent.GetType(), out var kindOf) ? kindOf(gameEvent) : null,
    };

    /// <summary>
    /// The sounds of events that came after the big switch in <see cref="KindFor"/>, one entry per event type (issue
    /// #605): a new event gets a line here instead of another arm there.
    /// </summary>
    private static readonly ImmutableDictionary<Type, Func<GameEvent, SoundKind?>> KindsByEventType =
        new Dictionary<Type, Func<GameEvent, SoundKind?>>
        {
            [typeof(ColumnHit)] = gameEvent => ColumnHitKind((ColumnHit)gameEvent),
            [typeof(ColumnBroke)] = _ => SoundKind.ColumnBroke,
            [typeof(DoorSwingStarted)] = gameEvent =>
                ((DoorSwingStarted)gameEvent).Closing ? SoundKind.BossDoorClosing : SoundKind.BossDoorOpening,
        }.ToImmutableDictionary();

    /// <summary>
    /// Whether <paramref name="gameEvent"/> stays silent because another event of the same tick (<paramref name="tick"/>)
    /// plays the sound of the same thing in <paramref name="pack"/>: a player opening the boss door raises both
    /// <see cref="DoorOpened"/> and <see cref="DoorSwingStarted"/> - only the boss door's grinding plays, not the creak of
    /// an ordinary door on top (issue #569). Without a file for the grinding in the pack, the creak plays as before.
    /// </summary>
    public static bool IsReplacedIn(GameEvent gameEvent, IReadOnlyList<GameEvent> tick, SoundPack pack)
    {
        ArgumentNullException.ThrowIfNull(tick);
        ArgumentNullException.ThrowIfNull(pack);
        return gameEvent is DoorOpened opened
            && !pack.Variants(SoundKind.BossDoorOpening).IsEmpty
            && tick.Any(other => other is DoorSwingStarted { Closing: false } swing && swing.DoorId == opened.DoorId);
    }

    /// <summary>
    /// The sound of a blow on a column (issue #605), by the stage it leaves the column in (<see cref="ColumnBreakLook.StageOf"/>,
    /// the same stages the models show): Z1 or Z2. The blow that breaks it plays nothing - <see cref="ColumnBroke"/> of
    /// the same tick plays the break instead.
    /// </summary>
    private static SoundKind? ColumnHitKind(ColumnHit hit) =>
        hit.Hits >= hit.HitsToBreak ? null
        : ColumnBreakLook.StageOf(hit.Hits, hit.HitsToBreak) switch
        {
            ColumnStage.Hit1 => SoundKind.ColumnHit1,
            ColumnStage.Hit2 => SoundKind.ColumnHit2,
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
/// <item>A creature's own hit sound (<see cref="SoundBoard.OwnHitKinds"/>, e.g. a bat's or Gorm's) replaces the
/// blade's hit of the same tick (issues #297, #298).</item>
/// <item>The boss door's grinding replaces the creak of an ordinary door of the same tick (<see cref="SoundBoard.IsReplacedIn"/>,
/// issue #569).</item>
/// <item>Of several files it picks one at random, never the one it picked last time from that set.</item>
/// <item>A wing beat plays only near the listener (<see cref="SoundBoard.IsInEarshot"/>) and not sooner than
/// <see cref="SoundBoard.MinimumGap"/> after the last one, from whichever bat (issue #297). For that it counts the
/// ticks: call it once per tick.</item>
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

    // Ticks seen so far (one call of For with events per tick) and the tick each kind with a minimum gap last played.
    private long _tick;
    private readonly Dictionary<SoundKind, long> _lastPlayedTick = [];

    /// <summary>Starts the choice of variants from <paramref name="seed"/> (the world's seed).</summary>
    public SoundCues(int seed) => _random = new Random(seed ^ SeedSalt);

    /// <summary>
    /// The sounds of <paramref name="pack"/> to play for <paramref name="events"/> (the events of one tick), in the order
    /// they first appear. <paramref name="listener"/>: where the listening player's ears are (their eyes) - sounds
    /// with a range (<see cref="SoundBoard.IsInEarshot"/>) farther away stay silent; without one, all count as near.
    /// </summary>
    public IReadOnlyList<SoundCue> For(IEnumerable<GameEvent> events, SoundPack pack, Vector3? listener = null)
    {
        ArgumentNullException.ThrowIfNull(events);
        return Cues(events.Where(gameEvent => SoundBoard.IsInEarshot(gameEvent, listener)), pack, _ => SoundMix.Centre);
    }

    /// <summary>
    /// Like the other <see cref="For(IEnumerable{GameEvent}, SoundPack, Vector3?)"/>, but placed (issue #491): every
    /// sound with a place is heard from there by <paramref name="listener"/> (<see cref="Hearing.MixFor"/>) - quieter
    /// with distance, silent beyond <see cref="HearingBalance.Range"/>, to the side it comes from. A sound without a place
    /// and the listener's own body sounds stay in the middle at full volume. Of several events of one kind in a tick the
    /// loudest decides.
    /// </summary>
    public IReadOnlyList<SoundCue> For(
        IEnumerable<GameEvent> events, SoundPack pack, SoundListener listener, HearingBalance hearing)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(hearing);
        return Cues(
            events.Where(gameEvent => SoundBoard.IsInEarshot(gameEvent, listener.Ears)),
            pack,
            gameEvent => Hearing.MixFor(gameEvent, listener, hearing));
    }

    /// <summary>The cues for <paramref name="events"/> (already filtered by earshot), each kind with the loudest of its <paramref name="mixFor"/>.</summary>
    private List<SoundCue> Cues(IEnumerable<GameEvent> events, SoundPack pack, Func<GameEvent, SoundMix> mixFor)
    {
        ArgumentNullException.ThrowIfNull(pack);
        _tick++;
        var mixes = new Dictionary<SoundKind, SoundMix>();
        var kinds = new List<SoundKind>();
        var tick = events.ToList();
        foreach (var gameEvent in tick)
        {
            if (SoundBoard.KindFor(gameEvent) is not { } kind || SoundBoard.IsReplacedIn(gameEvent, tick, pack))
            {
                continue;
            }

            var mix = mixFor(gameEvent);
            if (mix.Volume <= 0f)
            {
                continue;
            }

            if (!mixes.TryGetValue(kind, out var heard))
            {
                kinds.Add(kind);
                mixes[kind] = mix;
            }
            else if (mix.Volume > heard.Volume)
            {
                mixes[kind] = mix;
            }
        }

        if (kinds.Contains(SoundKind.Blade))
        {
            kinds.Remove(SoundKind.BladeSwing);
            if (kinds.Any(kind => SoundBoard.OwnHitKinds.Contains(kind) && !pack.Variants(kind).IsEmpty))
            {
                kinds.Remove(SoundKind.Blade);
            }
        }

        var cues = new List<SoundCue>();
        var setsPlayed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kind in kinds)
        {
            var files = pack.Variants(kind);
            var set = string.Join('|', files);
            if (files.IsEmpty || TooSoon(kind) || !setsPlayed.Add(set))
            {
                continue;
            }

            if (SoundBoard.MinimumGap(kind) > 0f)
            {
                _lastPlayedTick[kind] = _tick;
            }

            var mix = mixes[kind];
            cues.Add(new SoundCue(kind, files[NextVariant(set, files.Length)], SoundBoard.SoundVolume * mix.Volume, mix.Pan));
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

    /// <summary>True if <paramref name="kind"/> played less than its <see cref="SoundBoard.MinimumGap"/> ago.</summary>
    private bool TooSoon(SoundKind kind)
    {
        var gap = SoundBoard.MinimumGap(kind);
        return gap > 0f && _lastPlayedTick.TryGetValue(kind, out var last)
            && _tick - last < SimulationClock.SecondsToTicks(gap);
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
