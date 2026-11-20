using Raylib_cs;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The travel of the teleporters (issue #480): for now only the hint at an unlocked teleporter. Pure presentation:
/// where the hint shows comes from Core (<see cref="Core.Gameplay.GameWorld.TeleporterUnder"/>).
/// </summary>
internal static class TravelWindow
{
    /// <summary>The key that opens the destination choice.</summary>
    public const KeyboardKey Key = KeyboardKey.T;

    /// <summary>The hint at an unlocked teleporter.</summary>
    public const string Hint = "Travel";

    private static readonly Color KeyColour = new(230, 190, 90, 255);
    private static readonly Color HintText = new(220, 220, 228, 255);

    /// <summary>"[T] Travel" on the line of the interaction hint, when nothing else is offered there.</summary>
    public static void DrawHint()
    {
        const string key = "[T] ";
        var line = HudTextLines.Of(HudText.InteractionPrompt, Raylib.GetScreenHeight());
        var keyWidth = PixelText.Measure(key, line.FontSize);
        var x = (Raylib.GetScreenWidth() - keyWidth - PixelText.Measure(Hint, line.FontSize)) / 2;
        PixelText.Draw(key, x, line.Top, line.FontSize, KeyColour);
        PixelText.Draw(Hint, x + keyWidth, line.Top, line.FontSize, HintText);
    }
}
