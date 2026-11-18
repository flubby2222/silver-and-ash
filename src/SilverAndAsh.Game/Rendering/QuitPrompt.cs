using Raylib_cs;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The question before the game closes (issue #349): "Quit" on the title screen or in the Esc menu asks first.
/// "Cancel" comes first and is selected, so a hasty Enter keeps the game open. Design "C2 Slim" (issue #371).
/// </summary>
internal static class QuitPrompt
{
    public const int Cancel = 0;
    public const int Quit = 1;

    private static readonly Color Shade = new(0, 0, 0, 160);

    /// <summary>The chosen button this frame (<see cref="Cancel"/>, <see cref="Quit"/>) or -1.</summary>
    public static int Update(MenuView menu)
    {
        var canvas = MenuArt.Canvas;
        return menu.Update(MenuArt.Layout.Quit(canvas, menu.Selected).Rows, canvas, out _);
    }

    public static void Draw(MenuView menu)
    {
        MenuArt.Shade(Shade);
        var canvas = MenuArt.Canvas;
        MenuArt.Draw(MenuArt.Layout.Quit(canvas, menu.Selected), canvas);
    }
}
