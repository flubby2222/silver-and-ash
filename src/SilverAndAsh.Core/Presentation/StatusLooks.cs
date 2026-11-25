using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The shape a status effect takes on the body of an enemy (issue #775). One more shape is one more value here and one
/// more entry in the game's table of drawings - never one more branch in the drawing of the others.
/// </summary>
public enum StatusForm
{
    /// <summary>A ring of small glowing motes circling the body (issue #506): the placeholder until a trial piece decides.</summary>
    Motes,

    /// <summary>
    /// Frost (Grave Chill, variant A of the effects trial piece #616): rime flecks on the body and a disc of rime on the
    /// floor below it; the body itself takes the cold tint of the look (<see cref="StatusLook.Tint"/>). See <see cref="FrostLook"/>.
    /// </summary>
    Frost,
}

/// <summary>
/// A tint over the whole body of an enemy while an effect lasts (issue #775): its surface blended toward
/// <paramref name="Colour"/> by <paramref name="Amount"/> (0..1), as a hit flash does - a hit flash wins over it.
/// </summary>
public readonly record struct StatusTint((byte R, byte G, byte B) Colour, float Amount);

/// <summary>
/// How one kind of status effect shows on an enemy (issue #506): the colour of its shimmer and of its time left, the
/// ramp of the main palette that colour is a step of, and whether it shows its time left at all (Leech acts at once and
/// shows none). Since issue #775 also the shape it takes on the body (<paramref name="Form"/>, motes unless a trial
/// piece decided another) and the tint of the whole body, if any (<paramref name="Tint"/>).
/// </summary>
public readonly record struct StatusLook(
    StatusKind Kind, string Ramp, (byte R, byte G, byte B) Colour, bool ShowsTimer,
    StatusForm Form = StatusForm.Motes, StatusTint? Tint = null);

/// <summary>
/// The looks of every status effect, one entry per kind (issue #506). A new kind is one more entry here - with its
/// colour and whether it shows a timer (Leech with <c>ShowsTimer: false</c>). Placeholders until the trial
/// piece of the effects decides: every colour a step of the main palette, each effect a hue of its own (violet mark,
/// warm flame, cold frost), never red (red on an enemy belongs to the eyes announcing a blow). A test checks the colours
/// against the palette file.
/// </summary>
public static class StatusLooks
{
    /// <summary>Every look, in the order of the kinds.</summary>
    public static IReadOnlyList<StatusLook> All { get; } =
    [
        // Marked: Werkstein 3 (209, 193, 200), the lightest step of the main palette with a violet hue (provisional).
        new(StatusKind.Marked, EffectColours.Ashlar, (209, 193, 200), ShowsTimer: true),

        // Cleansing Flame: Messing/Gold 7 (238, 228, 160), white gold - the brightest warm step, well away from the gold of
        // the Royal tier (Messing/Gold 5) and from red (provisional, issue #507).
        new(StatusKind.CleansingFlame, EffectColours.Brass, (238, 228, 160), ShowsTimer: true),

        // Grave Chill: Stoff 7 (168, 181, 199), the light frost of variant A of the effects trial piece (issue #616) - the
        // coldest light step of the palette, a blue-grey well apart from the warm flame and the violet mark (issue #508).
        // On the body as variant A of that trial piece draws it (issue #775): frost and a cold tint, see FrostLook.
        new(StatusKind.GraveChill, EffectColours.Cloth, FrostLook.Rime, ShowsTimer: true, Form: StatusForm.Frost, Tint: FrostLook.Tint),

        // Stun: Eisen 6 (163, 175, 187), the silver-grey of variant A of the effects trial piece (issue #509). Close to the
        // frost of Grave Chill in colour - the effects trial piece tells them apart by shape (sparks circling over the head).
        new(StatusKind.Stun, EffectColours.Iron, (163, 175, 187), ShowsTimer: true),

        // Leech (issue #510): acts on the player at once and never sits on an enemy, so today nothing draws this look.
        // Violet belongs to Leech (effects trial piece, issue #616, version b1), but the ramp "Violett" is not in the main
        // palette yet; until it is, Werkstein 2 (195, 182, 188) - a step of the palette's most violet ramp, one step below
        // the mark - stands in (provisional). No timer: Leech has no duration.
        new(StatusKind.Leech, EffectColours.Ashlar, (195, 182, 188), ShowsTimer: false),
    ];

    /// <summary>The look of <paramref name="kind"/> in <paramref name="looks"/>; a kind without one is an error of the table.</summary>
    public static StatusLook Of(StatusKind kind, IReadOnlyList<StatusLook> looks)
    {
        ArgumentNullException.ThrowIfNull(looks);
        foreach (var look in looks)
        {
            if (look.Kind == kind)
            {
                return look;
            }
        }

        throw new KeyNotFoundException($"No look for the status effect {kind}.");
    }

    /// <summary>
    /// The tint over the whole body of <paramref name="ghoul"/> (issue #775): that of its first effect, in the order of
    /// the kinds, whose look has one - null without such an effect, and once it is dead or still hidden, like the rest of
    /// the status display. It ends on the tick its effect ends.
    /// </summary>
    public static StatusTint? TintOf(Ghoul ghoul) => TintOf(ghoul, All);

    /// <inheritdoc cref="TintOf(Ghoul)"/>
    public static StatusTint? TintOf(Bat bat) => TintOf(bat, All);

    /// <inheritdoc cref="TintOf(Ghoul)"/>
    public static StatusTint? TintOf(Gorm gorm) => TintOf(gorm, All);

    /// <summary>The same with another table of looks (for tests).</summary>
    internal static StatusTint? TintOf(IEnemy enemy, IReadOnlyList<StatusLook> looks)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        ArgumentNullException.ThrowIfNull(looks);
        if (enemy.IsDead || enemy.IsHidden)
        {
            return null;
        }

        foreach (var (kind, _) in enemy.Status.InOrder())
        {
            if (Of(kind, looks).Tint is { } tint)
            {
                return tint;
            }
        }

        return null;
    }
}
