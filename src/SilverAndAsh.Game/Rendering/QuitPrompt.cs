using Raylib_cs;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The question before the game closes (issue #349): "Quit" on the title screen or in the Esc menu asks first.
/// "Cancel" comes first and is selected, so a hasty Enter keeps the game open.
/// </summary>
internal static class QuitPrompt
{
    public const int Cancel = 0;
    public const int Quit = 1;

    private static readonly string[] Items = ["Cancel", "Quit"];
    private static readonly Color Shade = new(0, 0, 0, 160);
    private static readonly Color Heading = new(214, 204, 180, 255);
    private static readonly Color Hint = new(150, 150, 160, 255);

    private static int Top => Raylib.GetScreenHeight() / 2 - 20;

    /// <summary>The chosen button this frame (<see cref="Cancel"/>, <see cref="Quit"/>) or -1.</summary>
    public static int Update(MenuView menu) => menu.Update(Items, Top, out _);

    public static void Draw(MenuView menu)
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Shade);
        MenuView.DrawTitle("QUIT THE GAME?", Top - 80, 40, Heading);
        menu.Draw(Items, Top);
        MenuView.DrawTitle("Esc: cancel", Top + menu.Height(Items.Length) + 20, 18, Hint);
    }
}
