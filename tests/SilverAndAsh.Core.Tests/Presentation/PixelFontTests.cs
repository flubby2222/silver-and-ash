using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

public class PixelFontTests
{
    // A small font of its own: A 6 wide, B 4 wide, space 3, gap 1, fallback box 4 wide.
    private const string SmallFont = """
        {
         "image": "small.png", "width": 32, "height": 9, "lineHeight": 9, "capHeight": 7, "baseline": 6,
         "space": 3, "gap": 1,
         "fallback": { "x": 20, "y": 0, "w": 4 },
         "glyphs": [ { "char": "A", "x": 0, "y": 0, "w": 6 }, { "char": "B", "x": 7, "y": 0, "w": 4 } ]
        }
        """;

    [Fact]
    public void Two_characters_are_both_wide_plus_one_gap_and_no_gap_after_the_last()
    {
        var font = PixelFont.Parse(SmallFont);

        Assert.Equal(6, font.Width("A"));
        Assert.Equal(6 + 1 + 4, font.Width("AB"));
        Assert.Equal(6 + 1 + 3 + 1 + 4, font.Width("A B"));
        Assert.Equal(0, font.Width(string.Empty));
    }

    [Fact]
    public void The_width_grows_by_whole_numbers()
    {
        var font = PixelFont.Parse(SmallFont);

        Assert.Equal(3 * (6 + 1 + 4), font.Width("AB", 3));
    }

    [Fact]
    public void A_missing_character_takes_the_fallback_box_and_its_width()
    {
        var font = PixelFont.Parse(SmallFont);

        var placed = font.Layout("A?B");

        Assert.Equal(font.Fallback, placed[1].Cell);
        Assert.Equal([0, 7, 12], placed.Select(glyph => glyph.X));
        Assert.Equal(6 + 1 + 4 + 1 + 4, font.Width("A?B"));
        Assert.Equal(["?"], font.Missing("A?B ?"));
    }

    [Fact]
    public void The_layout_places_each_character_after_the_last_one_and_its_gap()
    {
        var font = PixelFont.Parse(SmallFont);

        var placed = font.Layout("B A");

        Assert.Equal([0, 4 + 1 + 3 + 1], placed.Select(glyph => glyph.X));
        Assert.Equal(new GlyphCell(0, 0, 6), placed[1].Cell);
    }

    [Theory]
    [InlineData(10, 1)]
    [InlineData(14, 1)]
    [InlineData(16, 2)]
    [InlineData(20, 2)]
    [InlineData(24, 2)]
    [InlineData(30, 3)]
    [InlineData(48, 5)]
    [InlineData(4, 1)]
    public void A_font_size_becomes_the_whole_number_nearest_to_tenths_of_it(int fontSize, int scale)
    {
        Assert.Equal(scale, PixelFont.ScaleFor(fontSize));
    }

    [Fact]
    public void The_capitals_sit_in_the_middle_of_the_old_line()
    {
        var font = PixelFont.Parse(SmallFont);

        Assert.Equal((20 - 14) / 2, font.TopOffset(20));
        Assert.Equal(0, font.TopOffset(4));
    }

    [Fact]
    public void The_game_font_measures_the_level_text_with_its_own_widths()
    {
        var font = GameFont();

        // L 5, V 7, space 3, 1 3 - and a gap of 1 between each two.
        Assert.Equal(5 + 1 + 7 + 1 + 3 + 1 + 3, font.Width("LV 1"));
        Assert.Equal(2 * 21, font.Width("LV 1", PixelFont.NumberScale));
    }

    [Fact]
    public void The_game_font_tells_zero_from_the_letter_o()
    {
        var font = GameFont();

        Assert.NotEqual(font.Layout("0")[0].Cell, font.Layout("O")[0].Cell);
        Assert.NotEqual(font.Fallback, font.Layout("0")[0].Cell);
    }

    [Fact]
    public void The_game_font_picture_is_next_to_its_table_and_has_room_for_every_cell()
    {
        var font = GameFont();
        var table = RepositoryFiles.Find("assets/fonts/kapitell.json");

        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(table)!, font.Image)));
        Assert.Equal(9, font.LineHeight);
        Assert.Equal(90, font.Characters.Count);
    }

    private static PixelFont GameFont() =>
        PixelFont.Parse(File.ReadAllText(RepositoryFiles.Find("assets/fonts/kapitell.json")));
}
