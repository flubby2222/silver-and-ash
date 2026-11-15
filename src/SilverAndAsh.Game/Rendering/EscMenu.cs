using System.Globalization;
using Raylib_cs;
using SilverAndAsh.Core;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The menu on Esc: resume, mouse sensitivity, back to the title, quit. Alone the world stands still meanwhile
/// (the pause goes to Core as input).
/// </summary>
internal static class EscMenu
{
    public const int Resume = 0;
    public const int Sensitivity = 1;
    public const int Title = 2;
    public const int Quit = 3;

    private static readonly Color Shade = new(0, 0, 0, 160);
    private static readonly Color Heading = new(214, 204, 180, 255);
    private static readonly Color Hint = new(150, 150, 160, 255);

    private static int Top => Raylib.GetScreenHeight() / 2 - 70;

    /// <summary>
    /// The chosen button this frame (or -1). <paramref name="changed"/> holds the settings after this frame - with a
    /// new sensitivity if it was nudged (arrow keys, or a click on the left/right half of its row).
    /// </summary>
    public static int Update(MenuView menu, GameSettings settings, out GameSettings changed)
    {
        var chosen = menu.Update(Items(settings), Top, out var step);
        changed = menu.Selected == Sensitivity && step != 0 ? settings.Changed(step) : settings;
        return chosen == Sensitivity ? -1 : chosen;
    }

    public static void Draw(MenuView menu, GameSettings settings)
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Shade);
        MenuView.DrawTitle("PAUSED", Top - 110, 56, Heading);
        menu.Draw(Items(settings), Top, Sensitivity);
        MenuView.DrawTitle("Esc: back to the game", Top + 4 * 64 + 20, 18, Hint);
    }

    private static string[] Items(GameSettings settings) =>
    [
        "Resume",
        string.Create(CultureInfo.InvariantCulture, $"Mouse sensitivity {settings.MouseSensitivity:0.00}"),
        "Title",
        "Quit",
    ];
}
