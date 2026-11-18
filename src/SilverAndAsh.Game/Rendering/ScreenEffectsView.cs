using Raylib_cs;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Draws the effects at the edge of the picture (issue #370) into the small picture of the 3D world, so they get its
/// big pixels: the darkening after a hit, the slowly pulsing dark red edge at low life and the short gold shine of a new
/// skill point. The edges are set pixel by pixel in the pattern of the dithering (art bible, "Effects": hard pixels,
/// no soft glow). When and how strongly comes from <see cref="ScreenEffects"/> in Core - this only draws.
/// </summary>
internal sealed class ScreenEffectsView : IDisposable
{
    // Palette colours (main palette): blood red 2 for the low-life edge, brass 5 for the skill point, stone 0 (the fog)
    // for the darkening.
    private static readonly Color LowLifeColor = new(105, 20, 34, 255);
    private static readonly Color SkillPointColor = new(214, 170, 64, 255);
    private static readonly Color DarkColor = new(16, 11, 20, 255);

    private Color[] _pixels = [];
    private Texture2D _texture;
    private int _width;
    private int _height;

    /// <summary>
    /// Draws the effects of <paramref name="effects"/> over a picture <paramref name="width"/> x
    /// <paramref name="height"/>. Call while drawing into the small picture, after the 3D world.
    /// </summary>
    public void Draw(ScreenEffects effects, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(effects);
        if (effects.Darkness > 0f)
        {
            Raylib.DrawRectangle(0, 0, width, height, DarkColor with { A = (byte)MathF.Round(255f * effects.Darkness) });
        }

        if (effects.LowLife <= 0f && effects.SkillPoint <= 0f)
        {
            return;
        }

        Fit(width, height);
        Array.Clear(_pixels);
        var band = ScreenEffects.EdgeBandPixels(height);
        for (var y = 0; y < height; y++)
        {
            var inRowBand = y < band || y >= height - band;
            for (var x = 0; x < width; x++)
            {
                if (!inRowBand && x >= band && x < width - band)
                {
                    // Nothing in the middle: jump to the right band.
                    x = width - band - 1;
                    continue;
                }

                // Gold over red: the shine is short and should be seen even at low life.
                if (ScreenEffects.IsEdgePixelOn(x, y, width, height, effects.SkillPoint))
                {
                    _pixels[(y * width) + x] = SkillPointColor;
                }
                else if (ScreenEffects.IsEdgePixelOn(x, y, width, height, effects.LowLife))
                {
                    _pixels[(y * width) + x] = LowLifeColor;
                }
            }
        }

        Raylib.UpdateTexture(_texture, _pixels);
        Raylib.DrawTexture(_texture, 0, 0, Color.White);
    }

    public void Dispose()
    {
        if (_width > 0)
        {
            Raylib.UnloadTexture(_texture);
            _width = 0;
        }
    }

    // A texture as large as the picture, made again when the window changes the picture's size.
    private void Fit(int width, int height)
    {
        if (width == _width && height == _height)
        {
            return;
        }

        Dispose();
        var image = Raylib.GenImageColor(width, height, Color.Blank);
        _texture = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);
        Raylib.SetTextureFilter(_texture, TextureFilter.Point);
        _pixels = new Color[width * height];
        _width = width;
        _height = height;
    }
}
