using Raylib_cs;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The menu on Esc: resume, settings, back to the title, quit (issue #349). Alone the world stands still meanwhile
/// (the pause goes to Core as input).
/// </summary>
internal static class EscMenu
{
    public const int Resume = 0;
    public const int Settings = 1;
    public const int Title = 2;
    public const int Quit = 3;

    private static readonly string[] Items = ["Resume", "Settings", "Back to title", "Quit"];
    private static readonly Color Shade = new(0, 0, 0, 160);
    private static readonly Color Heading = new(214, 204, 180, 255);
    private static readonly Color Hint = new(150, 150, 160, 255);

    // High enough that the hint below four buttons stays above the stamina bar in a window 480 pixels high.
    private static int Top => Raylib.GetScreenHeight() / 2 - 90;

    /// <summary>The chosen button this frame (<see cref="Resume"/>, <see cref="Settings"/> ...) or -1.</summary>
    public static int Update(MenuView menu) => menu.Update(Items, Top, out _);

    public static void Draw(MenuView menu)
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Shade);
        MenuView.DrawTitle("PAUSED", Top - 100, 56, Heading);
        menu.Draw(Items, Top);
        MenuView.DrawTitle("Esc: back to the game", Top + menu.Height(Items.Length) + 20, 18, Hint);
    }
}
