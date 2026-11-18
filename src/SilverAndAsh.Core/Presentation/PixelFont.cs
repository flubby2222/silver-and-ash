using System.Text;
using System.Text.Json;

namespace SilverAndAsh.Core.Presentation;

/// <summary>Where one character sits in the font picture: its cell's top left corner and width (pixels, unscaled).</summary>
public readonly record struct GlyphCell(int X, int Y, int Width);

/// <summary>One character of a laid-out text: its cell in the font picture and its left edge, unscaled, from the text's start.</summary>
public readonly record struct PlacedGlyph(GlyphCell Cell, int X);

/// <summary>
/// The game's own pixel font A "Kapitell" (issues #355, #367): the measure table of <c>assets/fonts/kapitell.json</c>,
/// which <c>tools/schrift/schrift_atlas.py</c> writes from the font's bit patterns. Pure measuring and laying out - the
/// Game project draws the cells from the font picture, enlarged only by whole numbers with hard pixels.
/// A character the font does not have becomes the visible <see cref="Fallback"/> box, never nothing.
/// </summary>
public sealed class PixelFont
{
    /// <summary>
    /// The enlargement of the large numbers in the HUD (level and rounds, issue #367): twice the size of the font,
    /// 14 px high capitals. Whole numbers only keep the pixels hard; a digit set of its own would be a new design.
    /// </summary>
    public const int NumberScale = 2;

    private readonly Dictionary<char, GlyphCell> _glyphs;

    private PixelFont(string image, int lineHeight, int capHeight, int baseline, int space, int gap, GlyphCell fallback,
        Dictionary<char, GlyphCell> glyphs)
    {
        Image = image;
        LineHeight = lineHeight;
        CapHeight = capHeight;
        Baseline = baseline;
        Space = space;
        Gap = gap;
        Fallback = fallback;
        _glyphs = glyphs;
    }

    /// <summary>File name of the font picture, next to the measure table.</summary>
    public string Image { get; }

    /// <summary>Height of every cell in pixels (unscaled); row 0 is the top of the capitals.</summary>
    public int LineHeight { get; }

    /// <summary>Height of the capitals in pixels (unscaled).</summary>
    public int CapHeight { get; }

    /// <summary>Row of the base line inside a cell.</summary>
    public int Baseline { get; }

    /// <summary>Width of a space in pixels (unscaled).</summary>
    public int Space { get; }

    /// <summary>Empty columns between two characters (unscaled).</summary>
    public int Gap { get; }

    /// <summary>The box drawn for a character the font does not have.</summary>
    public GlyphCell Fallback { get; }

    /// <summary>The characters the font has (without the space, which has no cell).</summary>
    public IReadOnlyCollection<char> Characters => _glyphs.Keys;

    /// <summary>Reads the measure table written by <c>schrift_atlas.py</c>.</summary>
    public static PixelFont Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var glyphs = new Dictionary<char, GlyphCell>();
        foreach (var entry in root.GetProperty("glyphs").EnumerateArray())
        {
            var text = entry.GetProperty("char").GetString() ?? string.Empty;
            if (text.Length != 1)
            {
                throw new FormatException($"A glyph must be exactly one character, not '{text}'.");
            }

            if (!glyphs.TryAdd(text[0], CellOf(entry)))
            {
                throw new FormatException($"The character '{text}' is in the font twice.");
            }
        }

        return new PixelFont(
            root.GetProperty("image").GetString() ?? string.Empty,
            root.GetProperty("lineHeight").GetInt32(),
            root.GetProperty("capHeight").GetInt32(),
            root.GetProperty("baseline").GetInt32(),
            root.GetProperty("space").GetInt32(),
            root.GetProperty("gap").GetInt32(),
            CellOf(root.GetProperty("fallback")),
            glyphs);
    }

    /// <summary>
    /// The whole-number enlargement that stands in for a Raylib font size (the height of a line in pixels): Raylib's
    /// own pixel font is 10 px high, so a size of 20 was it twice as large - the same holds for this font. At least 1.
    /// </summary>
    public static int ScaleFor(int fontSize) =>
        Math.Max(1, (int)Math.Round(fontSize / 10.0, MidpointRounding.AwayFromZero));

    /// <summary>
    /// How far below the top of a line <paramref name="fontSize"/> pixels high the capitals start: centred in the line,
    /// so the texts stay where the layout put them for Raylib's font.
    /// </summary>
    public int TopOffset(int fontSize) => Math.Max(0, (fontSize - (CapHeight * ScaleFor(fontSize))) / 2);

    /// <summary>Whether the font draws <paramref name="character"/> itself (the space counts) instead of the fallback box.</summary>
    public bool Has(char character) => character == ' ' || _glyphs.ContainsKey(character);

    /// <summary>The characters of <paramref name="text"/> the font does not have, each once, in order.</summary>
    public IReadOnlyList<string> Missing(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.EnumerateRunes().Where(rune => !Has(rune)).Select(rune => rune.ToString()).Distinct().ToList();
    }

    /// <summary>
    /// The characters of <paramref name="text"/> with their cells and left edges (unscaled); spaces take room but no
    /// cell, missing characters get the <see cref="Fallback"/> box.
    /// </summary>
    public IReadOnlyList<PlacedGlyph> Layout(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var placed = new List<PlacedGlyph>(text.Length);
        var x = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == ' ')
            {
                x += Space + Gap;
                continue;
            }

            var cell = CellOf(rune);
            placed.Add(new PlacedGlyph(cell, x));
            x += cell.Width + Gap;
        }

        return placed;
    }

    /// <summary>
    /// Width of <paramref name="text"/> in pixels at <paramref name="scale"/>: all characters with one gap between two
    /// of them, none after the last. An empty text is 0 wide.
    /// </summary>
    public int Width(string text, int scale = 1)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);
        var width = 0;
        var count = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            width += rune.Value == ' ' ? Space : CellOf(rune).Width;
            count++;
        }

        return count == 0 ? 0 : (width + ((count - 1) * Gap)) * scale;
    }

    private bool Has(Rune rune) => rune.IsBmp && Has((char)rune.Value);

    private GlyphCell CellOf(Rune rune) =>
        rune.IsBmp && _glyphs.TryGetValue((char)rune.Value, out var cell) ? cell : Fallback;

    private static GlyphCell CellOf(JsonElement entry) =>
        new(entry.GetProperty("x").GetInt32(), entry.GetProperty("y").GetInt32(), entry.GetProperty("w").GetInt32());
}
