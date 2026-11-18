using Raylib_cs;

namespace SilverAndAsh.Game.Rendering;

/// <summary>The title screen: the game's name, "Start", "Settings" and "Quit".</summary>
internal static class TitleScreen
{
    public const int Start = 0;
    public const int Settings = 1;
    public const int Quit = 2;

    private static readonly string[] Items = ["Start", "Settings", "Quit"];
    private static readonly Color Background = new(14, 11, 14, 255);
    private static readonly Color Name = new(214, 204, 180, 255);
    private static readonly Color Subtitle = new(150, 40, 46, 255);
    private static readonly Color Version = new(120, 120, 130, 255);

    // A little below the middle, so the subtitle keeps its distance from "Start" in a window 480 pixels high.
    private static int Top => Raylib.GetScreenHeight() / 2 + 14;

    /// <summary>The chosen button this frame (<see cref="Start"/>, <see cref="Settings"/>, <see cref="Quit"/>) or -1.</summary>
    public static int Update(MenuView menu) => menu.Update(Items, Top, out _);

    /// <summary>
    /// Draws the title screen; with <paramref name="buttons"/> false only its background, for the settings or the
    /// question before quitting on top.
    /// </summary>
    public static void Draw(MenuView menu, string displayVersion, bool buttons = true)
    {
        Raylib.ClearBackground(Background);
        if (!buttons)
        {
            return;
        }

        var height = Raylib.GetScreenHeight();
        MenuView.DrawTitle("SILVER & ASH", height / 4, 72, Name);
        MenuView.DrawTitle("a hunt in the catacombs", height / 4 + 86, 24, Subtitle);
        menu.Draw(Items, Top);

        var version = $"v{displayVersion}";
        Raylib.DrawText(version, Raylib.GetScreenWidth() - Raylib.MeasureText(version, 20) - 20, height - 30, 20, Version);
    }
}
