using Raylib_cs;

namespace SilverAndAsh.Game.Rendering;

/// <summary>The title screen: the game's name, "Start" and "Quit".</summary>
internal static class TitleScreen
{
    public const int Start = 0;
    public const int Quit = 1;

    private static readonly string[] Items = ["Start", "Quit"];
    private static readonly Color Background = new(14, 11, 14, 255);
    private static readonly Color Name = new(214, 204, 180, 255);
    private static readonly Color Subtitle = new(150, 40, 46, 255);
    private static readonly Color Version = new(120, 120, 130, 255);

    private static int Top => Raylib.GetScreenHeight() / 2;

    /// <summary>The chosen button this frame (<see cref="Start"/>, <see cref="Quit"/>) or -1.</summary>
    public static int Update(MenuView menu) => menu.Update(Items, Top, out _);

    public static void Draw(MenuView menu, string displayVersion)
    {
        Raylib.ClearBackground(Background);
        var height = Raylib.GetScreenHeight();
        MenuView.DrawTitle("SILVER & ASH", height / 4, 72, Name);
        MenuView.DrawTitle("a hunt in the catacombs", height / 4 + 86, 24, Subtitle);
        menu.Draw(Items, Top);

        var version = $"v{displayVersion}";
        Raylib.DrawText(version, Raylib.GetScreenWidth() - Raylib.MeasureText(version, 20) - 20, height - 30, 20, Version);
    }
}
