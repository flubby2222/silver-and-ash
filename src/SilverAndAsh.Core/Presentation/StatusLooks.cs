using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// How one kind of status effect shows on an enemy (issue #506): the colour of its shimmer and of its time left, the
/// ramp of the main palette that colour is a step of, and whether it shows its time left at all (Leech acts at once and
/// shows none).
/// </summary>
public readonly record struct StatusLook(StatusKind Kind, string Ramp, (byte R, byte G, byte B) Colour, bool ShowsTimer);

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
        new(StatusKind.GraveChill, EffectColours.Cloth, (168, 181, 199), ShowsTimer: true),

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
}
