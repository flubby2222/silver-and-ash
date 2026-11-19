using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The red eyes of a ghoul announcing a blow (issue #447): while it draws back (<see cref="Ghoul.IsAnnouncingBlow"/>)
/// its eyes glow blood red, brighter in hard steps towards the blow - no soft glow, no halo (art bible, "Farben").
/// Provisional until the next play test.
/// </summary>
public static class GhoulEyes
{
    /// <summary>The steps of the glow: rows of the blood red ramp in assets/palette/hauptpalette.json, dark to bright.</summary>
    public static IReadOnlyList<int> BloodRedSteps { get; } = [3, 5, 6];

    /// <summary>
    /// The step of the blood red ramp the eyes of <paramref name="ghoul"/> glow in right now, or null when they do not
    /// glow: the wind-up split into equal thirds, one step each.
    /// </summary>
    public static int? GlowStep(Ghoul ghoul)
    {
        ArgumentNullException.ThrowIfNull(ghoul);
        if (!ghoul.IsAnnouncingBlow)
        {
            return null;
        }

        // The small nudge keeps float rounding (1 - 20/30 = 0.33333331) from holding a step one tick too long.
        var third = Math.Clamp((int)(ghoul.WindUpProgress * BloodRedSteps.Count + 1e-4f), 0, BloodRedSteps.Count - 1);
        return BloodRedSteps[third];
    }
}
