using Raylib_cs;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The settings (issue #349), from the title screen and from the Esc menu: mouse sensitivity, three volumes and window
/// or full screen. Left/Right change a value, the mouse drags a slider (issue #426); every change counts at once.
/// What a value means and where it may go is decided by <see cref="GameSettings"/>.
/// </summary>
internal static class SettingsMenu
{
    public const int Sensitivity = 0;
    public const int Master = 1;
    public const int Effects = 2;
    public const int Ambience = 3;
    public const int Display = 4;
    public const int Back = 5;

    private static readonly Color Shade = new(0, 0, 0, 160);

    /// <summary>The menu of the settings; where its rows sit decides <see cref="MenuLayout.Settings"/>.</summary>
    public static MenuView NewMenu() => new();

    /// <summary>
    /// <see cref="Back"/> if "Back" was chosen this frame, otherwise -1. <paramref name="changed"/> holds the settings
    /// after this frame - with the nudged or dragged value changed, or window and full screen swapped.
    /// <paramref name="drag"/> remembers the slider the mouse holds from one frame to the next (issue #426).
    /// </summary>
    public static int Update(MenuView menu, SettingsSliderDrag drag, GameSettings settings, out GameSettings changed)
    {
        var canvas = MenuArt.Canvas;
        var picture = MenuArt.Layout.Settings(canvas, menu.Selected, settings);
        var chosen = menu.Update(picture.Rows, canvas, out var step);
        // Enter or a click swaps window and full screen; on the slider rows Left/Right nudge the value.
        changed = step == 0 && chosen != Display ? settings : menu.Selected switch
        {
            Sensitivity => settings.ChangedSensitivity(step),
            Master => settings.ChangedMasterVolume(step),
            Effects => settings.ChangedEffectsVolume(step),
            Ambience => settings.ChangedAmbienceVolume(step),
            Display => settings with { FullScreen = !settings.FullScreen },
            _ => settings,
        };

        // The mouse on a slider: a press puts the value at the pointer, holding the button drags it both ways.
        changed = drag.Update(changed, picture.Sliders, canvas.ToCanvas(Raylib.GetMousePosition()),
            Raylib.IsMouseButtonPressed(MouseButton.Left), Raylib.IsMouseButtonDown(MouseButton.Left));
        menu.Hold(drag.Row);
        return chosen == Back ? Back : -1;
    }

    /// <summary>Draws the settings in the design "C2 Slim" (issue #371): a slider per value, the display in use framed.</summary>
    public static void Draw(MenuView menu, GameSettings settings)
    {
        MenuArt.Shade(Shade);
        var canvas = MenuArt.Canvas;
        MenuArt.Draw(MenuArt.Layout.Settings(canvas, menu.Selected, settings), canvas);
    }
}
