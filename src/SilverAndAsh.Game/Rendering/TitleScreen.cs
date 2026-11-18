using Raylib_cs;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The title screen: the game's name and a narrow panel with "Start", "Settings" and "Quit" (design "C2 Slim", issue
/// #371; where what sits decides <see cref="MenuLayout.Title"/>).
/// </summary>
internal static class TitleScreen
{
    public const int Start = 0;
    public const int Settings = 1;
    public const int Quit = 2;

    private static readonly Color Background = new(14, 11, 14, 255);

    /// <summary>The chosen button this frame (<see cref="Start"/>, <see cref="Settings"/>, <see cref="Quit"/>) or -1.</summary>
    public static int Update(MenuView menu)
    {
        var canvas = MenuArt.Canvas;
        return menu.Update(MenuArt.Layout.Title(canvas, menu.Selected, string.Empty).Rows, canvas, out _);
    }

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

        var canvas = MenuArt.Canvas;
        MenuArt.Draw(MenuArt.Layout.Title(canvas, menu.Selected, displayVersion), canvas);
    }
}
