using System.Globalization;
using Raylib_cs;
using SilverAndAsh.Core;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The settings (issue #349), from the title screen and from the Esc menu: mouse sensitivity, three volumes and window
/// or full screen. Left/Right (or a click on the left/right part of a row) change a value; every change counts at once.
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

    private const int RowCount = 6;
    private static readonly int[] ValueRows = [Sensitivity, Master, Effects, Ambience, Display];

    private static readonly Color Shade = new(0, 0, 0, 160);
    private static readonly Color Heading = new(214, 204, 180, 255);
    private static readonly Color Hint = new(150, 150, 160, 255);

    /// <summary>A menu with lower rows than the others, so six of them fit into a window 480 pixels high.</summary>
    public static MenuView NewMenu() => new(buttonHeight: 40, gap: 8);

    /// <summary>
    /// <see cref="Back"/> if "Back" was chosen this frame, otherwise -1. <paramref name="changed"/> holds the settings
    /// after this frame - with the nudged value changed, or window and full screen swapped.
    /// </summary>
    public static int Update(MenuView menu, GameSettings settings, out GameSettings changed)
    {
        var chosen = menu.Update(Items(settings), Top(menu), out var step);
        // Enter or a click in the middle swaps window and full screen; on the other rows only Left/Right change something.
        changed = step == 0 && chosen != Display ? settings : menu.Selected switch
        {
            Sensitivity => settings.ChangedSensitivity(step),
            Master => settings.ChangedMasterVolume(step),
            Effects => settings.ChangedEffectsVolume(step),
            Ambience => settings.ChangedAmbienceVolume(step),
            Display => settings with { FullScreen = !settings.FullScreen },
            _ => settings,
        };
        return chosen == Back ? Back : -1;
    }

    public static void Draw(MenuView menu, GameSettings settings)
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Shade);
        var top = Top(menu);
        MenuView.DrawTitle("SETTINGS", top - 60, 40, Heading);
        menu.Draw(Items(settings), top, ValueRows);
        MenuView.DrawTitle("Left/Right: change    Esc: back", top + menu.Height(RowCount) + 14, 18, Hint);
    }

    // The rows in the middle of the screen, a little lower, so the heading fits above them.
    private static int Top(MenuView menu) => (Raylib.GetScreenHeight() - menu.Height(RowCount)) / 2 + 20;

    private static string[] Items(GameSettings settings) =>
    [
        string.Create(CultureInfo.InvariantCulture, $"Mouse sensitivity {settings.MouseSensitivity:0.00}"),
        $"Master volume {Percent(settings.MasterVolume)}",
        $"Effects volume {Percent(settings.EffectsVolume)}",
        $"Ambience volume {Percent(settings.AmbienceVolume)}",
        settings.FullScreen ? "Full screen" : "Window",
        "Back",
    ];

    private static string Percent(float volume) =>
        string.Create(CultureInfo.InvariantCulture, $"{MathF.Round(volume * 100f):0}%");
}
