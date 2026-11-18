using Raylib_cs;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The level name in big letters for a few seconds when a run starts (fading in and out).
/// </summary>
internal static class LevelBanner
{
    /// <summary>How long the level name stays, in seconds.</summary>
    public const float Duration = 3.5f;

    private const float Fade = 0.6f;
    private static readonly Color Name = new(214, 204, 180, 255);

    /// <param name="secondsLeft">Counts down from <see cref="Duration"/> to 0.</param>
    public static void Draw(string? levelName, float secondsLeft)
    {
        if (levelName is null || secondsLeft <= 0f)
        {
            return;
        }

        // Fade in during the first part, out during the last.
        var shown = Duration - secondsLeft;
        var alpha = MathF.Min(1f, MathF.Min(shown, secondsLeft) / Fade);
        DrawCentred(levelName, Raylib.GetScreenHeight() / 3, 48, Raylib.Fade(Name, alpha));
    }

    private static void DrawCentred(string text, int y, int fontSize, Color color)
    {
        var width = PixelText.Measure(text, fontSize);
        PixelText.Draw(text, (Raylib.GetScreenWidth() - width) / 2, y, fontSize, color);
    }
}
