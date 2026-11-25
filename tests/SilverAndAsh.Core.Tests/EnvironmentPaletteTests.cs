using System.Text.Json;

namespace SilverAndAsh.Core.Tests;

/// <summary>
/// Issue #280: the environment (every PNG in assets/textures, cracked wall, door frames, secret-room wall door) is painted with the colours
/// of the palette row Stein only, and the frame of the boss gate may add the row Werkstein (art bible, colour rule 6).
/// Issue #543: the door leaves keep the wood, iron and brass they have today, listed here as the named rows "Tür"; the spiral
/// stairs use Stein, Werkstein, the brass of their lamp (Messing/Gold) and the dark steps of the row Eisen for their railing (issue #765).
/// Issue #540: the portcullis is painted with the neutral iron K2 (row "Gitter Eisen"), its guide ring with the wall tile.
/// </summary>
public class EnvironmentPaletteTests
{
    private const string PalettePath = "assets/palette/hauptpalette.json";

    // Every PNG in assets/models belongs to exactly one of these groups. A new file that is in none makes
    // Every_model_texture_belongs_to_exactly_one_group fail, so it has to be placed on purpose.
    private static readonly string[] StoneModels =
    [
        "cracked_wall_a", "cracked_wall_b", "cracked_wall_c", "door_cracked",
        "door_frame_normal_a", "door_frame_normal_b", "door_frame_normal_c",
        "door_frame_locked_a", "door_frame_locked_b", "door_frame_locked_c",
        "wall_pillar_corridor_a", "wall_pillar_corridor_b", "wall_pillar_corridor_c",
        "wall_pillar_room_a", "wall_pillar_room_b", "wall_pillar_room_c",
        "ceiling_arch_a", "ceiling_arch_b", "ceiling_arch_c",
        "wall_niche_corridor_a", "wall_niche_corridor_b", "wall_niche_corridor_c",
        "wall_niche_room_a", "wall_niche_room_b", "wall_niche_room_c",
        "portcullis_guide_a", "portcullis_guide_b", "portcullis_guide_c",
        "wall_niche_deep_a", "wall_niche_deep_b", "wall_niche_deep_c",
        "wall_niche_shelf_a", "wall_niche_shelf_b", "wall_niche_shelf_c",
        "column_intact", "column_hit1", "column_hit2", "column_falling", "column_broken",
    ];

    private static readonly string[] StoneAndWerksteinModels = ["door_frame_boss_a", "door_frame_boss_b", "door_frame_boss_c"];

    /// <summary>The door leaves (with their head panels): only the colours of the rows "Tür" below.</summary>
    private static readonly string[] DoorLeafModels = ["door_normal", "door_locked", "door_boss"];

    /// <summary>The spiral stairs: Stein for wall and steps, Werkstein for the edges, Messing/Gold for the lamp, Eisen for the railing.</summary>
    private static readonly string[] StairsModels = ["stairs_spiral_a", "stairs_spiral_b", "stairs_spiral_c"];

    /// <summary>The portcullis (issue #540): only the neutral iron row "Gitter Eisen" below.</summary>
    private static readonly string[] GrateModels = ["portcullis"];

    /// <summary>
    /// The neutral iron K2 from issue #543, dark to light. It is not in the main palette: the human chose it for the
    /// portcullis only, because the bluish row Eisen draws the eye next to the rosy stone; the door leaves keep their iron.
    /// </summary>
    private static readonly (byte R, byte G, byte B)[] GrateIronRow = [(32, 32, 33), (62, 62, 63), (104, 104, 106), (148, 148, 151)];

    /// <summary>
    /// The fillings of the deep wall niches (issue #553): bottles, candles, skulls - small things of glass, wax, bone and
    /// dust. Only colours of the main palette, ground and accent rows (tools/models/wall_niche_filling.py), no door colour.
    /// </summary>
    private static readonly string[] NicheFillingModels = ["wall_niche_bottles", "wall_niche_candles", "wall_niche_skulls"];

    /// <summary>Not environment (figures, weapons, chests, enemies, effects): they keep their own colours.</summary>
    private static readonly string[] NotEnvironmentModels =
    [
        "bat", "blade", "club", "ghoul", "hunters_axe", "pistol", "shock_wave", "well_of_sirius",
        "chest_beggar", "chest_guard", "chest_king", "chest_vampire",
        "crossbow_skeleton_announce", "crossbow_skeleton_rest", "crypt_warden_announce", "crypt_warden_rest",
        "gorm_body", "gorm_body_phase2", "gorm_limbs", "teleporter_locked", "teleporter_unlocked",
    ];

    /// <summary>
    /// The colours the door atlases have today (issue #543: the human judged the doors good as they are). They are not in
    /// the main palette: several wood tones fall on a colour of Stein in the 15-bit picture of the game, which the palette
    /// file forbids within its rows. A new colour in a door atlas has to be added here on purpose.
    /// </summary>
    /// <remarks>
    /// Issue #568: the boss gate is new (tools/models/boss_portal.py) and has wood and iron only, in its own rows "Bosstor".
    /// The colours that only the old boss gate had (its glass, glow, brass and some wood and iron tones) are gone.
    /// </remarks>
    private static readonly Dictionary<string, (byte R, byte G, byte B)[]> DoorRows = new()
    {
        ["Tür Holz"] =
        [
            (16, 12, 16), (36, 30, 32), (65, 54, 45), (84, 70, 58), (93, 77, 64), (101, 84, 69), (110, 92, 76),
            (41, 35, 43),
        ],
        ["Tür Eisen"] =
        [
            (88, 91, 102), (108, 111, 124), (136, 141, 157), (197, 203, 226),
            (25, 21, 27), (37, 34, 39), (44, 45, 52), (48, 49, 58), (52, 54, 64), (59, 61, 71), (73, 73, 81),
            (89, 92, 104), (125, 130, 146), (185, 191, 213),
        ],
        ["Tür Messing"] =
        [
            (71, 57, 30), (131, 85, 50), (133, 91, 52), (143, 114, 60),
            (145, 115, 60), (166, 129, 68), (167, 129, 69), (215, 172, 90), (227, 151, 86),
        ],
        ["Tür Glut"] = [(220, 107, 62), (221, 109, 63), (226, 128, 75), (233, 135, 79)],
        ["Bosstor Holz"] =
        [
            (24, 18, 24), (25, 21, 26), (26, 22, 27), (27, 23, 28), (28, 24, 29), (29, 24, 30), (29, 25, 31), (30, 25, 31),
            (31, 26, 32), (31, 27, 33), (32, 27, 34), (33, 28, 34), (33, 28, 35), (34, 29, 36), (36, 30, 37), (39, 33, 40),
            (44, 37, 46),
        ],
        ["Bosstor Eisen"] =
        [
            (39, 38, 44), (57, 55, 64), (65, 63, 73), (74, 71, 83), (82, 79, 92), (87, 84, 98), (92, 89, 103), (100, 97, 113),
            (115, 111, 130), (129, 125, 145), (147, 142, 165), (173, 167, 193), (199, 193, 221), (210, 204, 234),
        ],
    };

    /// <summary>Atlas fields no face uses (door leaves, stairs): black, never on screen.</summary>
    private static readonly (byte R, byte G, byte B) UnusedField = (0, 0, 0);

    public static TheoryData<string> StoneOnly()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(RepositoryFiles.Find("assets/textures"), "*.png").Order())
        {
            data.Add("assets/textures/" + Path.GetFileName(file));
        }

        foreach (var name in StoneModels)
        {
            data.Add($"assets/models/{name}.png");
        }

        return data;
    }

    public static TheoryData<string> DoorLeaves() => [.. DoorLeafModels];

    public static TheoryData<string> Stairs() => [.. StairsModels];

    public static TheoryData<string> NicheFillings() => [.. NicheFillingModels];

    [Theory]
    [MemberData(nameof(NicheFillings))]
    public void A_niche_filling_has_only_colours_of_the_main_palette(string name)
    {
        var allowed = MainPaletteColors(RepositoryFiles.Find(PalettePath));
        var foreign = ForeignColors(RepositoryFiles.Find($"assets/models/{name}.png"), allowed);

        Assert.True(foreign.Count == 0, $"{name}.png has {foreign.Count} colours outside the main palette, e.g. {Show(foreign.FirstOrDefault())}.");
    }

    [Fact]
    public void A_door_colour_in_a_niche_filling_makes_the_check_fail()
    {
        // Counter-proof: one pixel of the bottles painted with a colour of the door atlases that the main palette has not.
        var allowed = MainPaletteColors(RepositoryFiles.Find(PalettePath));
        var (_, _, rgba) = TestPng.Read(RepositoryFiles.Find("assets/models/wall_niche_bottles.png"));
        Assert.Empty(ForeignColors(rgba, allowed));

        var door = DoorRows.Values.SelectMany(row => row).First(color => !allowed.Contains(color));
        var changed = (byte[])rgba.Clone();
        (changed[0], changed[1], changed[2]) = door;

        Assert.Equal([door], ForeignColors(changed, allowed));
    }

    [Theory]
    [MemberData(nameof(StoneOnly))]
    public void Environment_texture_has_only_colours_of_the_row_Stein(string file)
    {
        var allowed = StoneColors(RepositoryFiles.Find(PalettePath));
        var foreign = ForeignColors(RepositoryFiles.Find(file), allowed);

        Assert.True(foreign.Count == 0, $"{file} has {foreign.Count} colours outside the row Stein, e.g. {Show(foreign.FirstOrDefault())}.");
    }

    [Theory]
    [InlineData("a")]
    [InlineData("b")]
    [InlineData("c")]
    public void The_boss_frame_has_only_Stein_and_the_row_Werkstein(string variant)
    {
        var palette = RepositoryFiles.Find(PalettePath);
        var allowed = StoneColors(palette);
        allowed.UnionWith(RowColors(palette, "akzent", "Werkstein"));
        var file = RepositoryFiles.Find($"assets/models/{StoneAndWerksteinModels.Single(n => n.EndsWith(variant))}.png");

        Assert.Empty(ForeignColors(file, allowed));
        Assert.NotEmpty(ForeignColors(file, StoneColors(palette))); // the gate really uses its Werkstein
    }

    [Theory]
    [MemberData(nameof(DoorLeaves))]
    public void A_door_leaf_has_only_the_colours_of_the_rows_Tuer(string name)
    {
        var foreign = ForeignColors(RepositoryFiles.Find($"assets/models/{name}.png"), DoorColors());

        Assert.True(foreign.Count == 0, $"{name}.png has {foreign.Count} colours outside the rows Tür, e.g. {Show(foreign.FirstOrDefault())}.");
    }

    [Fact]
    public void Every_colour_of_the_rows_Tuer_is_used_by_a_door_leaf()
    {
        // No dead entries: the rows list what the doors have today, not a range of tolerated colours.
        var used = new HashSet<(byte, byte, byte)>();
        foreach (var name in DoorLeafModels)
        {
            var rgba = TestPng.Read(RepositoryFiles.Find($"assets/models/{name}.png")).Rgba;
            used.UnionWith(ForeignColors(rgba, []));
        }

        Assert.Empty(DoorColors().Except(used).Select(Show));
        Assert.Equal(DoorRows.Values.Sum(row => row.Length) + 1, DoorColors().Count); // no colour in two rows
    }

    [Fact]
    public void A_foreign_colour_in_a_door_atlas_makes_the_check_fail()
    {
        // Counter-proof: one pixel of the wood door painted with a colour of the main palette that no door has (Blutrot).
        var (_, _, rgba) = TestPng.Read(RepositoryFiles.Find("assets/models/door_normal.png"));
        Assert.Empty(ForeignColors(rgba, DoorColors()));

        var blood = RowColors(RepositoryFiles.Find(PalettePath), "akzent", "Blutrot").First();
        var changed = (byte[])rgba.Clone();
        (changed[0], changed[1], changed[2]) = blood;

        Assert.Equal([blood], ForeignColors(changed, DoorColors()));
    }

    [Theory]
    [MemberData(nameof(Stairs))]
    public void The_spiral_stairs_have_only_Stein_Werkstein_Messing_and_Eisen(string name)
    {
        var palette = RepositoryFiles.Find(PalettePath);
        var file = RepositoryFiles.Find($"assets/models/{name}.png");

        var foreign = ForeignColors(file, StairsColors(palette));
        Assert.True(foreign.Count == 0, $"{name}.png has {foreign.Count} colours outside Stein, Werkstein, Messing/Gold and Eisen, e.g. {Show(foreign.FirstOrDefault())}.");
        AssertUsesRow(file, RowColors(palette, "akzent", "Werkstein"), "Werkstein");
        AssertUsesRow(file, RowColors(palette, "akzent", "Messing/Gold"), "Messing/Gold");
        AssertUsesRow(file, RowColors(palette, "akzent", "Eisen"), "Eisen");
    }

    [Fact]
    public void A_blood_red_pixel_in_a_spiral_stairs_atlas_makes_the_check_fail()
    {
        // Counter-proof: one pixel painted with a colour of the main palette that the stairs do not use (Blutrot).
        var palette = RepositoryFiles.Find(PalettePath);
        var (_, _, rgba) = TestPng.Read(RepositoryFiles.Find("assets/models/stairs_spiral_b.png"));
        Assert.Empty(ForeignColors(rgba, StairsColors(palette)));

        var blood = RowColors(palette, "akzent", "Blutrot").First();
        var changed = (byte[])rgba.Clone();
        (changed[0], changed[1], changed[2]) = blood;

        Assert.Equal([blood], ForeignColors(changed, StairsColors(palette)));
    }

    // The railing of the stairs (issue #765) is iron of the main palette's row Eisen, not the portcullis's K2.
    private static HashSet<(byte R, byte G, byte B)> StairsColors(string palette)
    {
        var allowed = StoneColors(palette);
        allowed.UnionWith(RowColors(palette, "akzent", "Werkstein"));
        allowed.UnionWith(RowColors(palette, "akzent", "Messing/Gold"));
        allowed.UnionWith(RowColors(palette, "akzent", "Eisen"));
        allowed.Add(UnusedField);
        return allowed;
    }

    [Fact]
    public void The_portcullis_has_only_and_all_colours_of_the_row_Gitter_Eisen()
    {
        var used = ForeignColors(RepositoryFiles.Find($"assets/models/{GrateModels.Single()}.png"), []);

        Assert.Equal(GrateIronRow.Order(), used.Order());
    }

    [Fact]
    public void The_bluish_iron_of_the_palette_in_the_portcullis_atlas_makes_the_check_fail()
    {
        // Counter-proof: one pixel of the grate painted with the iron the probe had before issue #543 (row Eisen).
        var allowed = GrateIronRow.ToHashSet();
        var (_, _, rgba) = TestPng.Read(RepositoryFiles.Find("assets/models/portcullis.png"));
        Assert.Empty(ForeignColors(rgba, allowed));

        var bluish = RowColors(RepositoryFiles.Find(PalettePath), "akzent", "Eisen").First(c => !allowed.Contains(c));
        var changed = (byte[])rgba.Clone();
        (changed[0], changed[1], changed[2]) = bluish;

        Assert.Equal([bluish], ForeignColors(changed, allowed));
    }

    // The black of unused atlas fields belongs to neither row, so it can never count as a use.
    private static void AssertUsesRow(string file, HashSet<(byte R, byte G, byte B)> row, string rowName)
    {
        var used = ForeignColors(file, []);
        used.Remove(UnusedField);
        used.IntersectWith(row);
        Assert.True(used.Count > 0, $"{Path.GetFileName(file)} uses no colour of the row {rowName}.");
    }

    [Fact]
    public void One_foreign_pixel_makes_the_check_fail()
    {
        // Counter-proof: a real environment texture with a single pixel changed by one step is found.
        var allowed = StoneColors(RepositoryFiles.Find(PalettePath));
        var (width, height, rgba) = TestPng.Read(RepositoryFiles.Find("assets/textures/catacomb_wall.png"));
        Assert.Empty(ForeignColors(rgba, allowed));

        var changed = (byte[])rgba.Clone();
        changed[0] = (byte)(changed[0] == 0 ? 1 : changed[0] - 1);
        var foreign = ForeignColors(changed, allowed);

        Assert.Single(foreign);
        Assert.Equal(width * height * 4, changed.Length);
    }

    [Fact]
    public void A_colour_of_another_ground_row_is_foreign_in_the_environment()
    {
        // Counter-proof for the narrowed check (PR #542): the rows Haut/Knochen and Stoff are ground rows, but no wall colour.
        var palette = RepositoryFiles.Find(PalettePath);
        var rgba = TestPng.Read(RepositoryFiles.Find("assets/textures/catacomb_wall.png")).Rgba;

        foreach (var row in new[] { "Haut/Knochen", "Stoff" })
        {
            var color = RowColors(palette, "grund", row).First();
            var changed = (byte[])rgba.Clone();
            (changed[0], changed[1], changed[2]) = color;

            Assert.Equal([color], ForeignColors(changed, StoneColors(palette)));
        }
    }

    [Fact]
    public void A_picture_painted_with_free_colours_is_rejected()
    {
        // The state before issue #280: 24 colours that were no palette colours. Here a smooth ramp stands for them.
        var allowed = StoneColors(RepositoryFiles.Find(PalettePath));
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
        Assert.Throws<FileNotFoundException>(() => StoneColors(Path.Combine(Path.GetTempPath(), "silver-ash-missing.json")));
        Assert.Throws<InvalidOperationException>(() => RowColors(RepositoryFiles.Find(PalettePath), "akzent", "Gibt es nicht"));
    }

    [Fact]
    public void Every_model_texture_belongs_to_exactly_one_group()
    {
        var files = Directory.GetFiles(RepositoryFiles.Find("assets/models"), "*.png").Select(Path.GetFileNameWithoutExtension).OfType<string>();

        Assert.Empty(Unplaced(files));
    }

    [Fact]
    public void A_new_model_texture_in_no_group_is_found_and_no_name_is_in_two_groups()
    {
        Assert.Equal(["rubble_block"], Unplaced(["door_cracked", "rubble_block", "bat"]));

        var all = AllGroups().ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Fact]
    public void Every_listed_model_texture_exists()
    {
        foreach (var name in AllGroups())
        {
            Assert.True(File.Exists(RepositoryFiles.Find($"assets/models/{name}.png")), $"{name}.png is listed but missing.");
        }
    }

    private static IEnumerable<string> AllGroups() =>
        StoneModels.Concat(StoneAndWerksteinModels).Concat(DoorLeafModels).Concat(StairsModels).Concat(GrateModels)
            .Concat(NicheFillingModels).Concat(NotEnvironmentModels);

    private static List<string> Unplaced(IEnumerable<string> names)
    {
        var placed = AllGroups().ToHashSet();
        return names.Where(name => !placed.Contains(name)).Order().ToList();
    }

    private static HashSet<(byte R, byte G, byte B)> DoorColors()
    {
        var colors = DoorRows.Values.SelectMany(row => row).ToHashSet();
        colors.Add(UnusedField);
        return colors;
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

    private static HashSet<(byte R, byte G, byte B)> StoneColors(string palettePath) => RowColors(palettePath, "grund", "Stein");

    // Every colour of every ground and accent row.
    private static HashSet<(byte R, byte G, byte B)> MainPaletteColors(string palettePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(palettePath));
        return [.. new[] { "grund", "akzent" }.SelectMany(group => document.RootElement.GetProperty(group).EnumerateArray()).SelectMany(Colors)];
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
