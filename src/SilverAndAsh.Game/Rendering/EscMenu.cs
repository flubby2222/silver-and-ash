using Raylib_cs;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The menu on Esc: resume, settings, back to the title, quit (issue #349), in the design "C2 Slim" (issue #371). Alone
/// the world stands still meanwhile (the pause goes to Core as input).
/// </summary>
internal static class EscMenu
{
    public const int Resume = 0;
    public const int Settings = 1;
    public const int Title = 2;
    public const int Quit = 3;

    private static readonly Color Shade = new(0, 0, 0, 160);

    /// <summary>The chosen button this frame (<see cref="Resume"/>, <see cref="Settings"/> ...) or -1.</summary>
    public static int Update(MenuView menu)
    {
        var canvas = MenuArt.Canvas;
        return menu.Update(MenuArt.Layout.Pause(canvas, menu.Selected).Rows, canvas, out _);
    }

    public static void Draw(MenuView menu)
    {
        MenuArt.Shade(Shade);
        var canvas = MenuArt.Canvas;
        MenuArt.Draw(MenuArt.Layout.Pause(canvas, menu.Selected), canvas);
    }
}
