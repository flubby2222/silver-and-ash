using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// What both pages of the menu share (issues #348, #390): the two tabs "[Tab] Map" and "[G] Equipment" at the same place
/// on both pages (<see cref="MenuLayout.TabTarget"/>), and the frame of values on the left. Pure presentation - which page
/// is open decides Core (<see cref="PlayerMenu"/>); a click on a tab goes to Core as input.
/// </summary>
internal static class MenuTabs
{
    /// <summary>The tab clicked in this frame as its menu key, or null without a click or off the tabs.</summary>
    public static MenuKey? Pick()
    {
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            return null;
        }

        var canvas = MenuArt.Canvas;
        return MenuLayout.TabAt(canvas, canvas.ToCanvas(Raylib.GetMousePosition()));
    }

    /// <summary>Draws the frame of values of <paramref name="player"/> - the same on both pages.</summary>
    public static void DrawSheet(GameWorld world, Player player)
    {
        var sheet = MenuArt.Layout.Sheet(CharacterSheet.Stats(world, player), CharacterSheet.Boons(player, world.Balance.Boons));
        MenuArt.DrawSheet(sheet, MenuArt.Canvas);
    }
}
