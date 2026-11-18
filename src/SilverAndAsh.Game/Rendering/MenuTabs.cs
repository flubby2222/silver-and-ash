using Raylib_cs;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The two tabs on top of the menu (issue #348): "[Tab] Map" and "[G] Equipment", the open page lit. Pure presentation -
/// which page is open decides Core (<see cref="PlayerMenu"/>).
/// </summary>
internal static class MenuTabs
{
    /// <summary>The upper edge of the tabs; the pages start their own lines below <see cref="Bottom"/>.</summary>
    public const int Top = 12;

    /// <summary>The lower edge of the tabs.</summary>
    public const int Bottom = Top + Height;

    private const int Height = 28;
    private const int FontSize = 18;
    private const int Padding = 14;
    private const int Gap = 6;

    private static readonly Color Open = new(22, 18, 26, 255);
    private static readonly Color Closed = new(14, 10, 16, 200);
    private static readonly Color Gold = new(232, 196, 96, 255);
    private static readonly Color Muted = new(150, 150, 165, 255);
    private static readonly Color Edge = new(90, 88, 98, 255);

    /// <summary>Draws both tabs, centred on <paramref name="centreX"/>, <paramref name="page"/> lit.</summary>
    public static void Draw(PlayerMenuPage page, int centreX)
    {
        (PlayerMenuPage Page, string Text)[] tabs = [(PlayerMenuPage.Map, "[Tab] Map"), (PlayerMenuPage.Equipment, "[G] Equipment")];
        var widths = tabs.Select(tab => PixelText.Measure(tab.Text, FontSize) + (2 * Padding)).ToArray();
        var x = centreX - ((widths.Sum() + Gap) / 2);
        for (var i = 0; i < tabs.Length; i++)
        {
            var lit = tabs[i].Page == page;
            Raylib.DrawRectangle(x, Top, widths[i], Height, lit ? Open : Closed);
            Raylib.DrawRectangleLines(x, Top, widths[i], Height, lit ? Gold : Edge);
            PixelText.Draw(tabs[i].Text, x + Padding, Top + ((Height - FontSize) / 2), FontSize, lit ? Gold : Muted);
            x += widths[i] + Gap;
        }
    }
}
