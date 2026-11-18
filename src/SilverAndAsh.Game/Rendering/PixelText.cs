using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Draws all texts of the game in its own pixel font A "Kapitell" (issue #367), from the font picture
/// <c>assets/fonts/kapitell.png</c> and its measure table. Enlarged only by whole numbers, point filter: hard pixels.
/// The calls mirror <c>Raylib.DrawText</c> and <c>Raylib.MeasureText</c> - a font size stands for a line that many
/// pixels high (see <see cref="PixelFont.ScaleFor"/>), so the layout of the screens stays as it was.
/// Like Raylib's own default font the font is loaded once per window and held here; until then (or if the files are
/// missing) the texts fall back to Raylib's font, with a warning in the log.
/// </summary>
internal static class PixelText
{
    private static PixelFont? _font;
    private static Texture2D _texture;
    private static Image _image;

    /// <summary>Loads the font after the window is open (the picture becomes a texture on the graphics card).</summary>
    public static void Load(string fontsDirectory, FileLog? log)
    {
        try
        {
            var font = PixelFont.Parse(File.ReadAllText(Path.Combine(fontsDirectory, "kapitell.json")));
            var imagePath = Path.Combine(fontsDirectory, font.Image);
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException("Font picture missing.", imagePath);
            }

            _image = Raylib.LoadImage(imagePath);
            if (_image.Width == 0)
            {
                throw new InvalidOperationException($"Font picture '{font.Image}' could not be read.");
            }

            _texture = Raylib.LoadTextureFromImage(_image);
            Raylib.SetTextureFilter(_texture, TextureFilter.Point);
            _font = font;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or System.Text.Json.JsonException or FormatException
                                              or KeyNotFoundException or InvalidOperationException)
        {
            log?.Warning("Pixel font not loaded, texts use Raylib's font.", exception);
        }
    }

    /// <summary>Frees the texture before the window closes.</summary>
    public static void Unload()
    {
        if (_font is null)
        {
            return;
        }

        Raylib.UnloadTexture(_texture);
        Raylib.UnloadImage(_image);
        _font = null;
    }

    /// <summary>Draws <paramref name="text"/> in a line <paramref name="fontSize"/> pixels high whose top is at <paramref name="y"/>.</summary>
    public static void Draw(string text, int x, int y, int fontSize, Color color)
    {
        if (_font is null)
        {
            Raylib.DrawText(text, x, y, fontSize, color);
            return;
        }

        DrawScaled(text, x, y + _font.TopOffset(fontSize), PixelFont.ScaleFor(fontSize), color);
    }

    /// <summary>
    /// Draws <paramref name="text"/> enlarged exactly <paramref name="scale"/> times; <paramref name="top"/> is the top of
    /// the capitals. For the large numbers of the HUD (<see cref="PixelFont.NumberScale"/>).
    /// </summary>
    public static void DrawScaled(string text, int x, int top, int scale, Color color)
    {
        if (_font is null)
        {
            Raylib.DrawText(text, x, top, 10 * scale, color);
            return;
        }

        var height = _font.LineHeight;
        foreach (var glyph in _font.Layout(text))
        {
            var source = new Rectangle(glyph.Cell.X, glyph.Cell.Y, glyph.Cell.Width, height);
            var target = new Rectangle(x + (glyph.X * scale), top, glyph.Cell.Width * scale, height * scale);
            Raylib.DrawTexturePro(_texture, source, target, Vector2.Zero, 0f, color);
        }
    }

    /// <summary>Width of <paramref name="text"/> in pixels at the given font size (as <c>Raylib.MeasureText</c>).</summary>
    public static int Measure(string text, int fontSize) =>
        _font is null ? Raylib.MeasureText(text, fontSize) : _font.Width(text, PixelFont.ScaleFor(fontSize));

    /// <summary>Width of <paramref name="text"/> in pixels enlarged exactly <paramref name="scale"/> times.</summary>
    public static int MeasureScaled(string text, int scale) =>
        _font is null ? Raylib.MeasureText(text, 10 * scale) : _font.Width(text, scale);

    /// <summary>Height of the capitals in pixels at the given enlargement.</summary>
    public static int CapHeight(int scale) => (_font?.CapHeight ?? 7) * scale;

    /// <summary>
    /// Writes <paramref name="text"/> into a picture in memory (as <c>Raylib.ImageDrawText</c>), pixel by pixel - for
    /// pictures saved to disk, such as the comparison sheet.
    /// </summary>
    public static void DrawOnImage(ref Image target, string text, int x, int y, int fontSize, Color color)
    {
        if (_font is null)
        {
            Raylib.ImageDrawText(ref target, text, x, y, fontSize, color);
            return;
        }

        var scale = PixelFont.ScaleFor(fontSize);
        var top = y + _font.TopOffset(fontSize);
        foreach (var glyph in _font.Layout(text))
        {
            for (var row = 0; row < _font.LineHeight; row++)
            {
                for (var column = 0; column < glyph.Cell.Width; column++)
                {
                    if (Raylib.GetImageColor(_image, glyph.Cell.X + column, glyph.Cell.Y + row).A == 0)
                    {
                        continue;
                    }

                    Raylib.ImageDrawRectangle(ref target, x + ((glyph.X + column) * scale), top + (row * scale), scale,
                        scale, color);
                }
            }
        }
    }
}
