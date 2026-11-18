namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The short texts under the crosshair that can show at the same time, each with a line of its own (issue #340). The
/// skill point and the consecrated time have their place in the HUD frames since issue #369 (<see cref="HudLayout"/>).
/// </summary>
public enum HudText
{
    /// <summary>"Reload [R]" under the crosshair while the weapon in the hand is empty.</summary>
    ReloadHint,

    /// <summary>"[F] Open" and the like under the crosshair while a player looks at something they can use.</summary>
    InteractionPrompt,
}

/// <summary>Where a HUD text goes: the top edge of its line in screen pixels and its font size (the height of the line).</summary>
public readonly record struct HudTextLine(int Top, int FontSize)
{
    /// <summary>The first pixel row below the line.</summary>
    public int Bottom => Top + FontSize;
}

/// <summary>
/// Fixed lines for the HUD texts (issue #340): every text has a line of its own, so two texts that show at the same time
/// never lie on top of each other. The lines hang off the crosshair (screen centre), so they keep
/// their order in every window size. Pure arithmetic - the Game project draws the texts there.
/// </summary>
public static class HudTextLines
{
    /// <summary>Space between two lines, so the dark outline of one text never touches the next.</summary>
    public const int Gap = 6;

    /// <summary>The first line under the crosshair starts this far below the screen centre (below the crosshair's arms).</summary>
    public const int BelowCrosshair = 24;

    /// <summary>Font size of the texts under the crosshair.</summary>
    public const int CentreFontSize = 22;

    /// <summary>The smallest screen height the lines are laid out for: the game's picture height (ADR 0010).</summary>
    public const int SmallestScreenHeight = 480;

    /// <summary>The line of <paramref name="text"/> on a screen <paramref name="screenHeight"/> pixels high.</summary>
    public static HudTextLine Of(HudText text, int screenHeight)
    {
        var centreTop = (screenHeight / 2) + BelowCrosshair;
        return text switch
        {
            HudText.ReloadHint => new HudTextLine(centreTop, CentreFontSize),
            HudText.InteractionPrompt => new HudTextLine(centreTop + CentreFontSize + Gap, CentreFontSize),
            _ => throw new ArgumentOutOfRangeException(nameof(text), text, "Unknown HUD text."),
        };
    }
}
