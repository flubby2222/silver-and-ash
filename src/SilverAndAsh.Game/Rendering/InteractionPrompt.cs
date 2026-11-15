using Raylib_cs;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>"[F] Open" under the crosshair while the player looks at something they can use.</summary>
internal static class InteractionPrompt
{
    private const int FontSize = 20;
    private static readonly Color Key = new(230, 190, 90, 255);
    private static readonly Color Text = new(220, 220, 228, 255);

    public static void Draw(IInteractable? target)
    {
        if (target is null)
        {
            return;
        }

        const string key = "[F] ";
        var keyWidth = Raylib.MeasureText(key, FontSize);
        var total = keyWidth + Raylib.MeasureText(target.Prompt, FontSize);
        var x = (Raylib.GetScreenWidth() - total) / 2;
        var y = Raylib.GetScreenHeight() / 2 + 40;
        Raylib.DrawText(key, x, y, FontSize, Key);
        Raylib.DrawText(target.Prompt, x + keyWidth, y, FontSize, Text);
    }
}
