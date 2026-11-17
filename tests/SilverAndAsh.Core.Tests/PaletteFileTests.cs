using System.Text.Json;

namespace SilverAndAsh.Core.Tests;

/// <summary>
/// The main palette in assets/palette (tools/palette/hauptpalette.py writes the .json and the .gpl). These tests watch the
/// rules the human set for it; the row Werkstein came with the door frames (issue #303).
/// </summary>
public class PaletteFileTests
{
    private static readonly Lazy<JsonDocument> Palette = new(() =>
        JsonDocument.Parse(File.ReadAllText(RepositoryFiles.Find("assets/palette/hauptpalette.json"))));

    [Fact]
    public void Werkstein_is_an_accent_row_of_four_steps_with_its_purpose()
    {
        var row = Row("akzent", "Werkstein");

        Assert.Equal(4, Colors(row).Count);
        Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("zweck").GetString()));
        Assert.DoesNotContain(Rows("grund"), grund => grund.GetProperty("name").GetString() == "Werkstein");
    }

    [Fact]
    public void Werkstein_rises_in_brightness_and_lies_above_every_stone_step()
    {
        var werkstein = Colors(Row("akzent", "Werkstein")).Select(Luma).ToList();
        var brightestStone = Colors(Row("grund", "Stein")).Select(Luma).Max();

        Assert.Equal(werkstein.Order(), werkstein);
        Assert.Equal(werkstein.Count, werkstein.Distinct().Count());
        Assert.All(werkstein, luma => Assert.True(luma > brightestStone, $"Werkstein {luma} is not brighter than Stein {brightestStone}."));
    }

    [Fact]
    public void Werkstein_stays_a_dressed_grey_stone_and_never_a_coloured_one()
    {
        // The rows' hue is the stone's: a little more red and blue than green, never more than 40 apart (no tint).
        foreach (var (r, g, b) in Colors(Row("akzent", "Werkstein")))
        {
            Assert.True(r >= g && b >= g, $"({r}, {g}, {b}) is not the hue of the stone.");
            Assert.InRange(Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)), 0, 40);
        }
    }

    [Fact]
    public void The_gimp_file_lists_every_colour_of_the_json_in_its_order()
    {
        var expected = new[] { "grund", "akzent" }
            .SelectMany(group => Rows(group).SelectMany(row => Colors(row).Select((color, step) =>
                $"{color.R,3} {color.G,3} {color.B,3} {group}:{row.GetProperty("name").GetString()} {step}")))
            .ToList();
        var lines = File.ReadAllLines(RepositoryFiles.Find("assets/palette/hauptpalette.gpl"));

        Assert.Equal("GIMP Palette", lines[0]);
        Assert.Equal(expected, lines.Skip(1).Where(line => line.Length > 0 && char.IsAsciiDigit(line.TrimStart()[0])).ToList());
    }

    [Fact]
    public void No_two_colours_of_the_palette_look_the_same_in_the_games_five_bit_colour()
    {
        // ADR 0010: the game quantises to 5 bits per channel. Two colours that land on the same value would be one.
        var seen = new Dictionary<(int, int, int), string>();
        foreach (var group in new[] { "grund", "akzent" })
        {
            foreach (var row in Rows(group))
            {
                var name = row.GetProperty("name").GetString();
                foreach (var (color, step) in Colors(row).Select((color, step) => (color, step)))
                {
                    var key = (Snap5(color.R), Snap5(color.G), Snap5(color.B));
                    Assert.False(seen.TryGetValue(key, out var other), $"{name} {step} looks like {other} in 5 bits.");
                    seen[key] = $"{name} {step}";
                }
            }
        }
    }

    private static IEnumerable<JsonElement> Rows(string group) => Palette.Value.RootElement.GetProperty(group).EnumerateArray();

    private static JsonElement Row(string group, string name) =>
        Assert.Single(Rows(group), row => row.GetProperty("name").GetString() == name);

    private static List<(int R, int G, int B)> Colors(JsonElement row) => row.GetProperty("farben").EnumerateArray()
        .Select(color => color.EnumerateArray().Select(channel => channel.GetInt32()).ToArray())
        .Select(channels => (channels[0], channels[1], channels[2]))
        .ToList();

    private static double Luma((int R, int G, int B) color) => (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);

    private static int Snap5(int value) => (int)Math.Round(value * 31 / 255.0);
}
