using Raylib_cs;
using SilverAndAsh.Core.Audio;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The name of the sound pack for a moment after K switched it (issue #290): small, at the top of the screen, fading out.
/// </summary>
internal static class SoundPackNotice
{
    private const int FontSize = 22;
    private const int Top = 16;
    private const float FadeOutSeconds = 0.5f;
    private static readonly Color Text = new(214, 204, 180, 255);
    private static readonly Color Shadow = new(0, 0, 0, 200);

    public static void Draw(SoundPackSwitch packs)
    {
        if (packs.NoticeSecondsLeft <= 0f)
        {
            return;
        }

        var alpha = MathF.Min(1f, packs.NoticeSecondsLeft / FadeOutSeconds);
        var width = PixelText.Measure(packs.Notice, FontSize);
        var x = (Raylib.GetScreenWidth() - width) / 2;
        PixelText.Draw(packs.Notice, x + 2, Top + 2, FontSize, Raylib.Fade(Shadow, alpha));
        PixelText.Draw(packs.Notice, x, Top, FontSize, Raylib.Fade(Text, alpha));
    }
}
