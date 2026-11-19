using System.Numerics;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The mouse on the sliders of the settings (issue #426): pressing the left button on a slider puts its value where the
/// pointer is, and while the button stays down the value follows the pointer both ways - also past the ends of the
/// trough, where it stops at the lowest or highest value. Letting go ends the drag. Pure arithmetic on the canvas of
/// <see cref="MenuLayout.Settings"/>; the Game project only passes in the pointer and the button.
/// </summary>
public sealed class SettingsSliderDrag
{
    /// <summary>
    /// How far left and right of its trough a slider still takes a press - the knob reaches 7 px over the trough's
    /// right end; the labels end before and the values start after this margin (tested).
    /// </summary>
    public const float Grab = 8f;

    /// <summary>The settings row whose slider is being dragged, or null while none is.</summary>
    public int? Row { get; private set; }

    /// <summary>True while a slider is being dragged.</summary>
    public bool IsDragging => Row is not null;

    /// <summary>
    /// Ends a drag without the mouse - when the settings page is left or opened again while the button is still down
    /// (issue #426): without it the old slider would jump to the pointer the next time the page shows.
    /// </summary>
    public void End() => Row = null;

    /// <summary>
    /// Whether settings changed but not yet written (<paramref name="unsaved"/>) should go to the file now: not while a
    /// slider is dragged - its value counts at once, but is written once, when the drag ends.
    /// </summary>
    public bool WriteNow(bool unsaved) => unsaved && !IsDragging;

    /// <summary>
    /// One frame of the mouse at the canvas point <paramref name="point"/>: <paramref name="pressed"/> if the left button
    /// went down this frame, <paramref name="held"/> while it is down. Returns <paramref name="settings"/> with the
    /// dragged value set where the pointer is, or unchanged when no slider is dragged.
    /// </summary>
    public GameSettings Update(GameSettings settings, IReadOnlyList<MenuSlider> sliders, Vector2 point, bool pressed, bool held)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(sliders);
        if (pressed)
        {
            Row = SliderAt(sliders, point);
        }
        else if (!held)
        {
            Row = null;
        }

        return Row is { } row && row < sliders.Count && !float.IsNaN(point.X)
            ? WithFraction(settings, row, Fraction(sliders[row], point.X))
            : settings;
    }

    /// <summary>The index of the slider under the canvas point <paramref name="point"/>, or null.</summary>
    public static int? SliderAt(IReadOnlyList<MenuSlider> sliders, Vector2 point)
    {
        ArgumentNullException.ThrowIfNull(sliders);
        for (var i = 0; i < sliders.Count; i++)
        {
            var slider = sliders[i];
            var area = MenuRect.FromEdges(slider.Left - Grab, slider.CentreY - (MenuLayout.CursorHeight / 2f),
                slider.Left + TroughLength + Grab, slider.CentreY + (MenuLayout.CursorHeight / 2f));
            if (area.Contains(point))
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>
    /// How far along the trough of <paramref name="slider"/> the canvas column <paramref name="canvasX"/> lies: 0 at its
    /// left end, 1 at its right end, held within 0 to 1 beyond them - the inverse of where the knob is drawn.
    /// </summary>
    public static float Fraction(MenuSlider slider, float canvasX) =>
        Math.Clamp((canvasX - slider.Left) / TroughLength, 0f, 1f);

    /// <summary>
    /// <paramref name="settings"/> with the value of settings row <paramref name="row"/> (0 sensitivity, 1 master,
    /// 2 effects, 3 ambience) at <paramref name="fraction"/> of its range. The sensitivity lands on whole steps of
    /// <see cref="GameSettings.MouseSensitivityStep"/>, as the menu shows it; volumes on whole percent.
    /// </summary>
    public static GameSettings WithFraction(GameSettings settings, int row, float fraction)
    {
        ArgumentNullException.ThrowIfNull(settings);
        fraction = Math.Clamp(fraction, 0f, 1f);
        var changed = row switch
        {
            0 => settings with
            {
                MouseSensitivity = MathF.Round((GameSettings.MinMouseSensitivity
                    + (fraction * (GameSettings.MaxMouseSensitivity - GameSettings.MinMouseSensitivity))) / GameSettings.MouseSensitivityStep)
                    * GameSettings.MouseSensitivityStep,
            },
            1 => settings with { MasterVolume = fraction },
            2 => settings with { EffectsVolume = fraction },
            3 => settings with { AmbienceVolume = fraction },
            _ => throw new ArgumentOutOfRangeException(nameof(row), row, "Only the first four settings rows have a slider."),
        };
        return changed.InRange();
    }

    private static float TroughLength => MenuLayout.SliderRight - MenuLayout.SliderLeft;
}
