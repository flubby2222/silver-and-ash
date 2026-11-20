using System.Text.Json;

namespace SilverAndAsh.Core.Tests;

/// <summary>
/// Issue #280: the environment (wall, floor, ceiling, cracked wall, door frames, rubble door) is painted with the colours
/// of the main palette's ground rows only - the row Stein - and the frame of the boss gate may add the row Werkstein
/// (art bible, colour rule 6). The door leaves are not part of this yet: wood and iron need greyer steps in the palette.
/// </summary>
public class EnvironmentPaletteTests
{
    private const string PalettePath = "assets/palette/hauptpalette.json";

    public static TheoryData<string> StoneOnly()
    {
        var data = new TheoryData<string>();
        foreach (var kind in new[] { "wall", "floor", "ceiling" })
        {
            foreach (var variant in new[] { "", "_b", "_c" })
            {
                data.Add($"assets/textures/catacomb_{kind}{variant}.png");
            }
        }

        foreach (var variant in new[] { "a", "b", "c" })
        {
            data.Add($"assets/models/cracked_wall_{variant}.png");
            data.Add($"assets/models/door_frame_normal_{variant}.png");
            data.Add($"assets/models/door_frame_locked_{variant}.png");
        }

        data.Add("assets/models/door_cracked.png");
        return data;
    }

    [Theory]
    [MemberData(nameof(StoneOnly))]
    public void Environment_texture_has_only_colours_of_the_ground_rows(string file)
    {
        var allowed = GroundColors(RepositoryFiles.Find(PalettePath));
        var foreign = ForeignColors(RepositoryFiles.Find(file), allowed);

        Assert.True(foreign.Count == 0, $"{file} has {foreign.Count} colours outside the ground rows, e.g. {Show(foreign.FirstOrDefault())}.");
    }

    [Theory]
    [InlineData("a")]
    [InlineData("b")]
    [InlineData("c")]
    public void The_boss_frame_has_only_ground_colours_and_the_row_Werkstein(string variant)
    {
        var palette = RepositoryFiles.Find(PalettePath);
        var allowed = GroundColors(palette);
        allowed.UnionWith(RowColors(palette, "akzent", "Werkstein"));
        var file = RepositoryFiles.Find($"assets/models/door_frame_boss_{variant}.png");

        Assert.Empty(ForeignColors(file, allowed));
        Assert.NotEmpty(ForeignColors(file, GroundColors(palette))); // the gate really uses its Werkstein
    }

    [Fact]
    public void One_foreign_pixel_makes_the_check_fail()
    {
        // Counter-proof: a real environment texture with a single pixel changed by one step is found.
        var allowed = GroundColors(RepositoryFiles.Find(PalettePath));
        var (width, height, rgba) = TestPng.Read(RepositoryFiles.Find("assets/textures/catacomb_wall.png"));
        Assert.Empty(ForeignColors(rgba, allowed));

        var changed = (byte[])rgba.Clone();
        changed[0] = (byte)(changed[0] == 0 ? 1 : changed[0] - 1);
        var foreign = ForeignColors(changed, allowed);

        Assert.Single(foreign);
        Assert.Equal(width * height * 4, changed.Length);
    }

    [Fact]
    public void A_picture_painted_with_free_colours_is_rejected()
    {
        // The state before issue #280: 24 colours that were no palette colours. Here a smooth ramp stands for them.
        var allowed = GroundColors(RepositoryFiles.Find(PalettePath));
        var rgba = new byte[64 * 4];
        for (var i = 0; i < 64; i++)
        {
            rgba[i * 4] = (byte)(i * 3);
            rgba[(i * 4) + 1] = (byte)((i * 3) + 1);
            rgba[(i * 4) + 2] = (byte)((i * 3) + 2);
            rgba[(i * 4) + 3] = 255;
        }

        Assert.True(ForeignColors(rgba, allowed).Count > 50);
    }

    [Fact]
    public void A_missing_texture_is_an_error_and_not_an_empty_pass()
    {
        Assert.Throws<FileNotFoundException>(() => RepositoryFiles.Find("assets/textures/catacomb_not_there.png"));
        Assert.Throws<FileNotFoundException>(() => ForeignColors(Path.Combine(Path.GetTempPath(), "silver-ash-missing.png"), []));
    }

    [Fact]
    public void A_missing_palette_or_row_is_an_error_and_not_an_empty_pass()
    {
        Assert.Throws<FileNotFoundException>(() => RepositoryFiles.Find("assets/palette/not_there.json"));
        Assert.Throws<FileNotFoundException>(() => GroundColors(Path.Combine(Path.GetTempPath(), "silver-ash-missing.json")));
        Assert.Throws<InvalidOperationException>(() => RowColors(RepositoryFiles.Find(PalettePath), "akzent", "Gibt es nicht"));
    }

    private static HashSet<(byte R, byte G, byte B)> ForeignColors(string path, HashSet<(byte R, byte G, byte B)> allowed) =>
        ForeignColors(TestPng.Read(path).Rgba, allowed);

    private static HashSet<(byte R, byte G, byte B)> ForeignColors(byte[] rgba, HashSet<(byte R, byte G, byte B)> allowed)
    {
        var foreign = new HashSet<(byte, byte, byte)>();
        for (var i = 0; i < rgba.Length; i += 4)
        {
            var color = (rgba[i], rgba[i + 1], rgba[i + 2]);
            if (!allowed.Contains(color))
            {
                foreign.Add(color);
            }
        }

        return foreign;
    }

    private static HashSet<(byte R, byte G, byte B)> GroundColors(string palettePath)
    {
        var colors = new HashSet<(byte, byte, byte)>();
        using var document = JsonDocument.Parse(File.ReadAllText(palettePath));
        foreach (var row in document.RootElement.GetProperty("grund").EnumerateArray())
        {
            colors.UnionWith(Colors(row));
        }

        return colors;
    }

    private static HashSet<(byte R, byte G, byte B)> RowColors(string palettePath, string group, string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(palettePath));
        var row = document.RootElement.GetProperty(group).EnumerateArray()
            .Single(r => r.GetProperty("name").GetString() == name);
        return [.. Colors(row)];
    }

    private static IEnumerable<(byte, byte, byte)> Colors(JsonElement row) => row.GetProperty("farben").EnumerateArray()
        .Select(color => color.EnumerateArray().Select(channel => (byte)channel.GetInt32()).ToArray())
        .Select(channels => (channels[0], channels[1], channels[2]));

    private static string Show((byte R, byte G, byte B) color) => $"({color.R}, {color.G}, {color.B})";
}
