using System.Collections.Immutable;

namespace SilverAndAsh.Core.Audio;

/// <summary>
/// One set of files for the short sounds (issue #290): which files of assets/audio/ play for which
/// <see cref="SoundKind"/>. The game has two - the first sounds (issue #267) and sound pack 1 - and switches between
/// them with a key, so the human can compare them in the game. Which one plays is presentation only: it never goes
/// into the game state, the same run sounds different but plays the same.
/// <para>
/// The loudness lives in the files (the audio designer set the peak per category, see
/// tools/audio/paket1/uebersicht.md), so every file plays at <see cref="SoundBoard.SoundVolume"/>.
/// </para>
/// </summary>
public sealed class SoundPack
{
    private readonly ImmutableDictionary<SoundKind, ImmutableArray<string>> _files;

    private SoundPack(string name, ImmutableDictionary<SoundKind, ImmutableArray<string>> files)
    {
        Name = name;
        _files = files;
    }

    /// <summary>The name shown when the pack is switched on.</summary>
    public string Name { get; }

    /// <summary>The first sounds of the game (issue #267): pistol, blade hit, steps, the ghoul's wind-up - nothing else.</summary>
    public static SoundPack Old { get; } = new("old sounds", new Dictionary<SoundKind, ImmutableArray<string>>
    {
        // Before #290 every pistol shot played the same file, consecrated or not.
        [SoundKind.Pistol] = ["pistole.wav"],
        [SoundKind.ConsecratedShot] = ["pistole.wav"],
        [SoundKind.Blade] = ["klinge-1.wav", "klinge-2.wav", "klinge-3.wav"],

        // One set for both feet: the same files - so they share their last pick (see SoundCues).
        [SoundKind.StepLeft] = OldSteps,
        [SoundKind.StepRight] = OldSteps,
        [SoundKind.GhoulWindUp] = ["ghul-ankuendigung.wav"],
    }.ToImmutableDictionary());

    /// <summary>
    /// Sound pack 1 (issue #290), built by the audio designer from five sampled sources in SoundCraft. The mapping is
    /// the one of the hand-over (design/uebergabe/README-audio-paket-1.md, commit 2a87efb), for every event Core has.
    /// </summary>
    public static SoundPack Pack1 { get; } = new("sound pack 1", new Dictionary<SoundKind, ImmutableArray<string>>
    {
        [SoundKind.Pistol] = ["p1-schuss-1.wav", "p1-schuss-2.wav", "p1-schuss-3.wav"],

        // The audio designer's suggestion (not ordered): the same shot with a short bell, so the consecration is heard.
        [SoundKind.ConsecratedShot] = ["p1-schuss-geweiht-1.wav", "p1-schuss-geweiht-2.wav"],
        [SoundKind.ReloadStarted] = ["p1-nachladen-start.wav"],
        [SoundKind.ReloadFinished] = ["p1-nachladen-bereit.wav"],
        [SoundKind.BladeSwing] = ["p1-schwung-1.wav", "p1-schwung-2.wav"],
        [SoundKind.Blade] = ["p1-treffer-1.wav", "p1-treffer-2.wav", "p1-treffer-3.wav"],
        [SoundKind.StepLeft] = ["p1-schritt-l1.wav", "p1-schritt-l2.wav", "p1-schritt-l3.wav"],
        [SoundKind.StepRight] = ["p1-schritt-r1.wav", "p1-schritt-r2.wav", "p1-schritt-r3.wav"],
        [SoundKind.PlayerHurt] = ["p1-schmerz.wav"],
        [SoundKind.GhoulWindUp] = ["p1-ghul-ankuendigung.wav"],
        [SoundKind.GhoulDied] = ["p1-ghul-sterben.wav"],
        [SoundKind.DoorOpened] = ["p1-tuer-oeffnen.wav"],
        [SoundKind.ChestOpened] = ["p1-kiste-oeffnen.wav"],
        [SoundKind.SilverTaken] = ["p1-aufheben-silber.wav"],
        [SoundKind.BloodVialTaken] = ["p1-aufheben-phiole.wav"],
        [SoundKind.ConsecratedAmmoTaken] = ["p1-aufheben-weihmunition.wav"],
        [SoundKind.KeyTaken] = ["p1-aufheben-schluessel.wav"],
    }.ToImmutableDictionary());

    /// <summary>The pack the game starts with: the new one.</summary>
    public static SoundPack Default => Pack1;

    /// <summary>Every pack, in the order the key steps through them (starting with <see cref="Default"/>).</summary>
    public static ImmutableArray<SoundPack> All => [Pack1, Old];

    /// <summary>
    /// The files of sound pack 1 that no event plays yet - Core has no event for them (jump, dash, landing, a ghoul
    /// noticing a player, picking up a weapon, the end of the consecration, a menu click). Checked in with the pack, so
    /// the follow-up issue #291 only adds the events.
    /// </summary>
    public static ImmutableArray<string> Pack1WithoutEvent =>
    [
        "p1-ghul-entdecken.wav", "p1-sprung.wav", "p1-dash.wav", "p1-landung.wav", "p1-aufheben-ausruestung.wav",
        "p1-weihe-ende.wav", "p1-menue-klick.wav",
    ];

    /// <summary>
    /// The files of <paramref name="kind"/> in this pack: one of them plays each time, never the same twice in a row.
    /// Empty if the pack has no sound for it.
    /// </summary>
    public ImmutableArray<string> Variants(SoundKind kind) => _files.TryGetValue(kind, out var files) ? files : [];

    /// <summary>Every file this pack plays.</summary>
    public ImmutableArray<string> Files => [.. Enum.GetValues<SoundKind>().SelectMany(kind => Variants(kind)).Distinct()];

    /// <summary>The pack after <paramref name="pack"/> in <see cref="All"/> (after the last one comes the first again).</summary>
    public static SoundPack After(SoundPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var index = All.IndexOf(pack);
        return All[(index + 1) % All.Length];
    }

    public override string ToString() => Name;

    private static ImmutableArray<string> OldSteps => ["schritt-1.wav", "schritt-2.wav", "schritt-3.wav", "schritt-4.wav"];
}

/// <summary>
/// Which sound pack plays (issue #290) and the short note that names it after a switch. Presentation only - the
/// game keeps one of these next to the sound player, never in the world (a second computer may play the other pack).
/// </summary>
public sealed class SoundPackSwitch
{
    /// <summary>Seconds the name of the pack stays on screen after a switch.</summary>
    public const float NoticeSeconds = 2.5f;

    /// <summary>The pack that plays now; the game starts with <see cref="SoundPack.Default"/>.</summary>
    public SoundPack Current { get; private set; } = SoundPack.Default;

    /// <summary>Seconds the note is still shown (0 = hidden).</summary>
    public float NoticeSecondsLeft { get; private set; }

    /// <summary>The note to show, e.g. "Sounds: sound pack 1".</summary>
    public string Notice => $"Sounds: {Current.Name}";

    /// <summary>The key was pressed: the next pack plays, and its name shows for <see cref="NoticeSeconds"/>.</summary>
    public void Toggle()
    {
        Current = SoundPack.After(Current);
        NoticeSecondsLeft = NoticeSeconds;
    }

    /// <summary>Once per drawn frame: counts the note down by <paramref name="frameSeconds"/>.</summary>
    public void Update(float frameSeconds) => NoticeSecondsLeft = MathF.Max(0f, NoticeSecondsLeft - frameSeconds);
}
