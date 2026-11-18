using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The two tabs of the menu (issue #348): "[Tab] Map" and "[G] Equipment", the open page lit - in the design "C2 Slim"
/// (issue #371). Pure presentation - which page is open decides Core (<see cref="PlayerMenu"/>). The equipment page
/// draws its tabs with its panel (<see cref="InventoryWindow"/>).
/// </summary>
internal static class MenuTabs
{
    /// <summary>Draws the tabs of the map page centred at the top; returns the window line under them.</summary>
    public static float DrawMap()
    {
        var canvas = MenuArt.Canvas;
        MenuArt.Draw(MenuArt.Layout.MapTabs(canvas, out var bottom), canvas);
        return canvas.ToWindowY(bottom);
    }
}
