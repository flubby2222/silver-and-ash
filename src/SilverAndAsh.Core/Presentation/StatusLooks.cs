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
/// colour and whether it shows a timer (Leech will come with <c>ShowsTimer: false</c>). Placeholders until the trial
/// piece of the effects decides: every colour a step of the main palette, the row of the effects violet, never red (red
/// on an enemy belongs to the eyes announcing a blow). A test checks the colours against the palette file.
/// </summary>
public static class StatusLooks
{
    /// <summary>Every look, in the order of the kinds.</summary>
    public static IReadOnlyList<StatusLook> All { get; } =
    [
        // Marked: Werkstein 3 (209, 193, 200), the lightest step of the main palette with a violet hue (provisional).
        new(StatusKind.Marked, EffectColours.Ashlar, (209, 193, 200), ShowsTimer: true),
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
