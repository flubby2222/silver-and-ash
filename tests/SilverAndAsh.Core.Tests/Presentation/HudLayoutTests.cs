using System.Globalization;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Where the HUD draws what (issue #369): the approved design A3 (854 x 480) with its groups at the corners and in the
/// middle, kept apart on every window shape.
/// </summary>
public class HudLayoutTests
{
    private static readonly HudParts Parts =
        HudParts.Parse(File.ReadAllText(RepositoryFiles.Find("assets/hud/hud_parts.json")));

    private static readonly PixelFont Font =
        PixelFont.Parse(File.ReadAllText(RepositoryFiles.Find("assets/fonts/kapitell.json")));

    private static readonly HudLayout Layout = new(Parts, Font);

    private static readonly HudCanvas Design = new(854, 480);

    /// <summary>16:9, 16:10, 21:9, 32:9 and the narrow 4:3 and 5:4 (taller canvas).</summary>
    public static TheoryData<int> PictureWidths => new() { 854, 768, 1120, 1706, 640, 600 };

    /// <summary>The groups of the HUD, each by the frames that make it.</summary>
    private static readonly Dictionary<string, string[]> Groups = new()
    {
        ["items"] = ["frame_item_0", "frame_item_1"],
        ["vitals"] = ["frame_vitals"],
        ["weapons"] = ["frame_weapon_0", "frame_weapon_1", "frame_weapon_2", "frame_weapon_3",
            "frame_weapon_0_active", "frame_weapon_1_active", "frame_weapon_2_active", "frame_weapon_3_active"],
        ["level"] = ["frame_level", "frame_xp", "frame_skill_0", "frame_prompt"],
        ["boss"] = ["frame_boss"],
        ["currency"] = ["frame_currency"],
        ["toast"] = ["frame_toast"],
    };

    private static HudReadout Calm() => new(
        IsDead: false,
        Life: 1f,
        Stamina: 1f,
        Level: 3,
        Experience: 0.4f,
        PendingBoons: 0,
        Silver: 120,
        Keys: 1,
        Weapons:
        [
            new(WeaponKind.SilverBlade, null, false, false),
            new(WeaponKind.Pistol, 6, false, true),
            new(null, null, false, false),
            new(null, null, false, false),
        ],
        Items: [ItemKind.IronFlask],
        Boss: null,
        ConsecratedSecondsLeft: 0f);

    /// <summary>The most rounds any magazine holds - the widest number a weapon frame must take.</summary>
    private static int MostRounds => new[] { Balance.Default.Pistol, Balance.Default.Rifle, Balance.Default.Blunderbuss }
        .Max(weapon => weapon.MagazineSize);

    /// <summary>Everything at once and at its widest: the test of room.</summary>
    private static HudReadout Busy() => Calm() with
    {
        Level = 99,
        PendingBoons = 2,
        Silver = 99999,
        Keys = 99,
        Weapons =
        [
            new(WeaponKind.Club, null, false, false),
            new(WeaponKind.Rifle, MostRounds, true, false),
            new(WeaponKind.Blunderbuss, MostRounds, false, true),
            new(WeaponKind.Pistol, MostRounds, false, false),
        ],
        Items = Enum.GetValues<ItemKind>().Concat(Enum.GetValues<ItemKind>()).ToList(),
        Boss = new HudBoss(Gorm.Name, 0.5f),
        ConsecratedSecondsLeft = 12.5f,
    };

    private static HudPicture Arrange(HudReadout readout, string? toast = null, HudCanvas? canvas = null) =>
        Layout.Arrange(readout, toast, canvas ?? Design);

    private static HudPlacedPart Single(HudPicture picture, string name) => Assert.Single(picture.Parts, part => part.Name == name);

    private static string LongestToast() => PickupToast.AllTexts().MaxBy(text => Font.Width(text))!;

    [Fact]
    public void The_parts_table_names_its_picture_and_the_design_size()
    {
        Assert.Equal("hud_parts.png", Parts.Image);
        Assert.Equal(854, Parts.DesignWidth);
        Assert.Equal(480, Parts.DesignHeight);
        Assert.True(File.Exists(RepositoryFiles.Find("assets/hud/" + Parts.Image)));
    }

    [Fact]
    public void Every_weapon_and_item_picture_the_layout_names_is_in_the_table_with_its_lit_version()
    {
        foreach (var kind in HudLayout.WeaponsWithIcon)
        {
            Assert.True(Parts.Contains(HudLayout.IconOf(kind)!), $"{kind}");
            Assert.True(Parts.Contains(HudLayout.IconOf(kind) + "_active"), $"{kind} lit");
        }

        Assert.All(HudLayout.ItemsWithIcon, kind => Assert.True(Parts.Contains(HudLayout.IconOf(kind)!), $"{kind}"));
    }

    [Theory]
    [InlineData(854, 854, 480)]
    [InlineData(768, 768, 480)]
    [InlineData(1120, 1120, 480)]
    [InlineData(640, 768, 576)]
    [InlineData(600, 768, 614)]
    public void The_canvas_is_the_picture_but_never_narrower_than_16_to_10(int pictureWidth, int width, int height)
    {
        Assert.Equal(new HudCanvas(width, height), HudCanvas.For(pictureWidth));
    }

    [Fact]
    public void In_the_design_every_frame_is_where_the_approved_design_put_it()
    {
        var picture = Arrange(Busy(), LongestToast());

        foreach (var part in picture.Parts.Where(part => part.Name.StartsWith("frame_", StringComparison.Ordinal)
                     && part.Name is not ("frame_item_0" or "frame_item_1" or "frame_skill_0" or "frame_prompt")))
        {
            Assert.Equal((Parts[part.Name].LayoutX, Parts[part.Name].LayoutY), (part.X, part.Y));
        }
    }

    [Theory]
    [MemberData(nameof(PictureWidths))]
    public void The_bottom_frames_end_on_one_line_the_same_distance_above_the_bottom_edge(int pictureWidth)
    {
        var canvas = HudCanvas.For(pictureWidth);
        var picture = Arrange(Busy(), canvas: canvas);

        var bottomFrames = picture.Parts.Where(part => part.Name.StartsWith("frame_item", StringComparison.Ordinal)
            || part.Name == "frame_vitals" || part.Name.StartsWith("frame_weapon", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(bottomFrames);
        Assert.All(bottomFrames, part =>
            Assert.Equal(HudLayout.BottomBaseline - Parts[part.Name].LayoutY, canvas.Height - (480 - HudLayout.BottomBaseline) - part.Y));
    }

    [Theory]
    [MemberData(nameof(PictureWidths))]
    public void The_groups_keep_to_their_corner_or_the_middle(int pictureWidth)
    {
        var canvas = HudCanvas.For(pictureWidth);
        var picture = Arrange(Busy(), canvas: canvas);

        // Left: as far from the left edge as in the design.
        Assert.Equal(Parts["frame_level"].LayoutX, Single(picture, "frame_level").X);
        Assert.Equal(Parts["frame_item_0"].LayoutX, picture.Parts.First(part => part.Name == "frame_item_0").X);

        // Right: as far from the right edge as in the design.
        Assert.Equal(854 - Parts["frame_currency"].LayoutX, canvas.Width - Single(picture, "frame_currency").X);
        Assert.Equal(854 - Parts["frame_weapon_3"].LayoutX, canvas.Width - Single(picture, "frame_weapon_3").X);

        // Middle: as far from the middle as in the design (within the half pixel of an odd difference).
        Assert.InRange((2 * Single(picture, "frame_vitals").X) - canvas.Width, (2 * Parts["frame_vitals"].LayoutX) - 854 - 1,
            (2 * Parts["frame_vitals"].LayoutX) - 854);
        Assert.InRange((2 * Single(picture, "frame_boss").X) - canvas.Width, (2 * Parts["frame_boss"].LayoutX) - 854 - 1,
            (2 * Parts["frame_boss"].LayoutX) - 854);
    }

    [Theory]
    [MemberData(nameof(PictureWidths))]
    public void The_groups_stay_on_the_canvas_and_apart_even_with_everything_at_its_widest(int pictureWidth)
    {
        var canvas = HudCanvas.For(pictureWidth);
        var picture = Arrange(Busy(), LongestToast(), canvas);

        var all = picture.Parts.Select(Layout.Bounds).Concat(picture.Texts.Select(Layout.Bounds)).ToList();
        Assert.All(all, rect => Assert.True(rect.X >= 0 && rect.Y >= 0 && rect.Right <= canvas.Width && rect.Bottom <= canvas.Height,
            $"{rect} leaves the canvas {canvas}"));

        var groups = Groups.ToDictionary(group => group.Key, group => Union(picture.Parts.Where(part => group.Value.Contains(part.Name))
            .Select(Layout.Bounds)));
        Assert.All(groups, group => Assert.True(group.Value is not null, $"{group.Key} is missing"));
        foreach (var (name, rect) in groups)
        {
            foreach (var (other, otherRect) in groups.Where(group => string.CompareOrdinal(group.Key, name) > 0))
            {
                Assert.False(rect!.Value.Overlaps(otherRect!.Value), $"{name} {rect} runs into {other} {otherRect} on {canvas}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(PictureWidths))]
    public void Every_text_lies_inside_its_group_or_beside_it_without_touching_another(int pictureWidth)
    {
        var canvas = HudCanvas.For(pictureWidth);
        var picture = Arrange(Busy(), LongestToast(), canvas);
        var frames = picture.Parts.Where(part => part.Name.StartsWith("frame_", StringComparison.Ordinal)).Select(Layout.Bounds).ToList();

        foreach (var text in picture.Texts)
        {
            var rect = Layout.Bounds(text);
            var inside = frames.Count(frame => frame.X <= rect.X && rect.Right <= frame.Right && frame.Y <= rect.Y && rect.Bottom <= frame.Bottom);
            var touched = frames.Count(frame => frame.Overlaps(rect));
            Assert.True(touched == inside && touched <= 1, $"'{text.Text}' {rect} lies across a frame edge on {canvas}");
        }
    }

    [Fact]
    public void The_boss_shows_only_while_his_fight_is_on()
    {
        var calm = Arrange(Calm());
        var fight = Arrange(Calm() with { Boss = new HudBoss(Gorm.Name, 0.25f) });

        Assert.DoesNotContain(calm.Parts, part => part.Name.StartsWith("boss", StringComparison.Ordinal) || part.Name == "frame_boss");
        Assert.DoesNotContain(calm.Texts, text => text.Text == Gorm.Name);
        Assert.Single(fight.Texts, text => text.Text == Gorm.Name);
        Assert.Equal((int)MathF.Round(Parts["boss_full"].Width * 0.25f), Single(fight, "boss_full").Width);
    }

    [Fact]
    public void The_skill_point_shows_only_while_a_boon_waits_with_its_key()
    {
        var calm = Arrange(Calm());
        var waiting = Arrange(Calm() with { PendingBoons = 1 });

        Assert.DoesNotContain(calm.Parts, part => part.Name is "frame_skill_0" or "icon_skillpoint" or "frame_prompt");
        Assert.DoesNotContain(calm.Texts, text => text.Text == "X");
        Single(waiting, "icon_skillpoint");
        var key = Single(waiting, "frame_prompt");
        var letter = Assert.Single(waiting.Texts, text => text.Text == "X");
        Assert.True(Layout.Bounds(key).X < letter.X && Layout.Bounds(letter).Right <= Layout.Bounds(key).Right);
    }

    [Fact]
    public void The_toast_shows_only_with_a_text_in_gold()
    {
        var calm = Arrange(Calm());
        var toast = Arrange(Calm(), "Picked up Hunter's Eye");

        Assert.DoesNotContain(calm.Parts, part => part.Name == "frame_toast");
        Single(toast, "frame_toast");
        Assert.Equal(HudTextColor.Pickup, Assert.Single(toast.Texts, text => text.Text == "Picked up Hunter's Eye").Color);
        Assert.All(toast.Texts.Where(text => text.Text != "Picked up Hunter's Eye"), text => Assert.NotEqual(HudTextColor.Pickup, text.Color));
    }

    [Fact]
    public void Every_toast_text_fits_its_frame_and_the_font()
    {
        foreach (var text in PickupToast.AllTexts())
        {
            var picture = Arrange(Calm(), text);
            var frame = Layout.Bounds(Single(picture, "frame_toast"));
            var placed = Layout.Bounds(Assert.Single(picture.Texts, placedText => placedText.Text == text));

            Assert.True(placed.Right <= frame.Right - 3, $"'{text}' ends at {placed.Right}, the frame at {frame.Right}");
            Assert.Empty(Font.Missing(text));
        }
    }

    [Fact]
    public void Life_and_stamina_show_no_number_but_ammo_silver_level_and_keys_do()
    {
        var picture = Arrange(Busy());
        var vitals = Layout.Bounds(Single(picture, "frame_vitals"));

        Assert.DoesNotContain(picture.Texts, text => Layout.Bounds(text).Overlaps(vitals));
        var texts = picture.Texts.Select(text => text.Text).ToList();
        Assert.Contains("99", texts);    // level and keys
        Assert.Equal(2, texts.Count(text => text == "99"));
        Assert.Contains("99999", texts); // silver
        Assert.Equal(3, texts.Count(text => text == MostRounds.ToString(CultureInfo.InvariantCulture))); // rounds of the three ranged weapons
    }

    [Theory]
    [InlineData(1f, false)]
    [InlineData(0.5f, true)]
    [InlineData(0.01f, true)]
    [InlineData(0f, false)]
    public void The_life_bar_fills_from_the_left_with_a_light_edge_while_part_of_it_is_gone(float life, bool edge)
    {
        var width = (int)MathF.Round(Parts["life_full"].Width * life);
        Assert.True(life == 0f || width > 0, "A sliver of life must still show - the case would prove nothing.");
        var picture = Arrange(Calm() with { Life = life });

        Single(picture, "life_empty");
        Assert.Equal(width, picture.Parts.SingleOrDefault(part => part.Name == "life_full").Width);
        Assert.Equal(edge, picture.LifeEdge is not null);
        if (picture.LifeEdge is { } rect)
        {
            Assert.Equal(Single(picture, "life_full").X + width - 1, rect.X);
            Assert.Equal(1, rect.Width);
        }
    }

    [Fact]
    public void Almost_full_life_rounds_to_the_full_bar_and_not_one_pixel_short()
    {
        var full = Parts["life_full"].Width;
        var life = (full - 0.4f) / full; // rounds to the whole bar, cutting off would leave one pixel short
        Assert.Equal(full - 1, (int)(full * life));

        var picture = Arrange(Calm() with { Life = life });

        Assert.Equal(full, Single(picture, "life_full").Width);
        Assert.Null(picture.LifeEdge);
    }

    [Fact]
    public void The_stamina_fills_its_five_segments_one_after_the_other()
    {
        var picture = Arrange(Calm() with { Stamina = 0.5f });

        int Filled(int segment) => picture.Parts.SingleOrDefault(part => part.Name == $"stamina_{segment}_full").Width;

        Assert.Equal(Parts["stamina_0_full"].Width, Filled(0));
        Assert.Equal(Parts["stamina_1_full"].Width, Filled(1));
        Assert.Equal((int)MathF.Round(Parts["stamina_2_full"].Width * 0.5f), Filled(2));
        Assert.Equal(0, Filled(3));
        Assert.Equal(0, Filled(4));
        Assert.All(Enumerable.Range(0, 5), segment => Single(picture, $"stamina_{segment}_empty"));
    }

    [Fact]
    public void The_chosen_ranged_weapon_is_lit_and_the_melee_hand_never()
    {
        var picture = Arrange(Calm());

        Single(picture, "frame_weapon_0");
        Single(picture, "frame_weapon_1_active");
        Single(picture, "icon_pistol_active");
        Single(picture, "icon_blade");
        Assert.DoesNotContain(picture.Parts, part => part.Name is "frame_weapon_0_active" or "frame_weapon_1" or "icon_pistol");
    }

    [Fact]
    public void Rounds_while_reloading_are_in_the_quieter_colour()
    {
        var picture = Arrange(Calm() with
        {
            Keys = 0,
            Weapons =
            [
                new(WeaponKind.Club, null, false, false),
                new(WeaponKind.Rifle, 1, true, false),
                new(WeaponKind.Blunderbuss, 2, false, true),
                new(WeaponKind.Pistol, null, false, false),
            ],
        });

        Assert.Equal(HudTextColor.Second, Assert.Single(picture.Texts, text => text.Text == "1").Color);
        Assert.Equal(HudTextColor.Main, Assert.Single(picture.Texts, text => text.Text == "2").Color);
    }

    [Fact]
    public void A_dead_player_has_no_crosshair_no_weapons_and_no_consecrated_time()
    {
        var picture = Arrange(Busy() with { IsDead = true });

        Assert.DoesNotContain(picture.Parts, part => part.Name == "crosshair" || part.Name.StartsWith("frame_weapon", StringComparison.Ordinal));
        Assert.DoesNotContain(picture.Texts, text => text.Text.StartsWith("Consecrated", StringComparison.Ordinal));
        Single(picture, "frame_vitals");
    }

    [Theory]
    [InlineData(854, 480)]
    [InlineData(768, 576)]
    public void The_crosshair_sits_in_the_middle_of_the_canvas(int width, int height)
    {
        var picture = Arrange(Calm(), canvas: new HudCanvas(width, height));
        var crosshair = Single(picture, "crosshair");

        Assert.Equal(width / 2, crosshair.X + (Parts["crosshair"].Width / 2));
        Assert.Equal(height / 2, crosshair.Y + (Parts["crosshair"].Height / 2));
    }

    [Fact]
    public void The_consecrated_time_counts_whole_seconds_up()
    {
        var picture = Arrange(Calm() with { ConsecratedSecondsLeft = 4.2f });

        Assert.Single(picture.Texts, text => text.Text == "Consecrated 5s");
        Assert.DoesNotContain(Arrange(Calm()).Texts, text => text.Text.StartsWith("Consecrated", StringComparison.Ordinal));
    }

    [Fact]
    public void No_item_is_hidden_without_a_sign_more_than_fit_end_in_a_plus_frame()
    {
        var items = Enumerable.Repeat(ItemKind.IronFlask, 20).ToList();
        var picture = Arrange(Calm() with { Items = items }, canvas: HudCanvas.For(768));

        var frames = picture.Parts.Count(part => part.Name.StartsWith("frame_item", StringComparison.Ordinal));
        var icons = picture.Parts.Count(part => part.Name == "icon_flask");
        Assert.Equal(frames - 1, icons);
        Assert.Single(picture.Texts, text => text.Text == $"+{items.Count - icons}");
    }

    [Fact]
    public void Items_that_fit_show_one_frame_each_and_none_without_items()
    {
        Assert.DoesNotContain(Arrange(Calm() with { Items = [] }).Parts, part => part.Name.StartsWith("frame_item", StringComparison.Ordinal));

        var picture = Arrange(Calm() with { Items = [ItemKind.IronFlask, ItemKind.HuntersEye, ItemKind.Leech] });
        Assert.Equal(["frame_item_0", "frame_item_1", "frame_item_0"],
            picture.Parts.Where(part => part.Name.StartsWith("frame_item", StringComparison.Ordinal)).Select(part => part.Name));
        var frames = picture.Parts.Where(part => part.Name.StartsWith("frame_item", StringComparison.Ordinal)).ToList();
        var flask = Layout.Bounds(Single(picture, "icon_flask"));
        var eye = Layout.Bounds(Single(picture, "icon_eye"));
        AssertCentredIn(Layout.Bounds(frames[0]), flask);
        AssertCentredIn(Layout.Bounds(frames[1]), eye);
        Assert.InRange((2 * flask.Y) + flask.Height - ((2 * eye.Y) + eye.Height), -1, 1); // one line through both
        Assert.Single(picture.Texts, text => text.Text == "L");
    }

    private static void AssertCentredIn(HudRect frame, HudRect icon)
    {
        Assert.True(frame.X <= icon.X && icon.Right <= frame.Right && frame.Y <= icon.Y && icon.Bottom <= frame.Bottom,
            $"The icon {icon} lies outside its frame {frame}.");
        Assert.InRange((2 * icon.X) + icon.Width - ((2 * frame.X) + frame.Width), -1, 1);
    }

    [Fact]
    public void Every_text_the_hud_can_show_is_in_the_font()
    {
        var picture = Arrange(Busy() with { Items = Enum.GetValues<ItemKind>().ToList() }, LongestToast());

        Assert.All(picture.Texts, text => Assert.Empty(Font.Missing(text.Text)));
        Assert.All(Enum.GetValues<WeaponKind>(), kind => Assert.Empty(Font.Missing(Weapons.Info(kind).Name)));
    }

    [Fact]
    public void A_weapon_not_reloading_has_no_veil()
    {
        Assert.Empty(Arrange(Calm()).ReloadVeils);
    }

    [Theory]
    [MemberData(nameof(PictureWidths))]
    public void The_reload_veil_lies_inside_the_frame_of_the_weapon_being_reloaded(int pictureWidth)
    {
        var readout = Calm() with
        {
            Weapons =
            [
                new(WeaponKind.SilverBlade, null, false, false),
                new(WeaponKind.Pistol, 0, true, true, 0f),
                new(null, null, false, false),
                new(null, null, false, false),
            ],
        };

        var picture = Arrange(readout, canvas: HudCanvas.For(pictureWidth));

        var veil = Assert.Single(picture.ReloadVeils);
        var frame = Layout.Bounds(picture.Parts.Single(part => part.Name == "frame_weapon_1_active"));
        Assert.Equal(frame.X + HudLayout.VeilInset, veil.X);
        Assert.Equal(HudLayout.VeilWidth, veil.Width);
        Assert.Equal(HudLayout.VeilHeight, veil.Height);
        Assert.True(veil.Right <= frame.Right - HudLayout.VeilInset + 1, $"veil ends at {veil.Right}, frame at {frame.Right}");
        Assert.True(veil.Bottom <= frame.Bottom, $"veil ends at {veil.Bottom}, frame at {frame.Bottom}");
        Assert.True(veil.Y > frame.Y, $"veil starts at {veil.Y}, frame at {frame.Y}");
    }

    [Fact]
    public void The_reload_veil_shrinks_from_the_right_as_the_reload_comes_on()
    {
        var start = HudLayout.ReloadVeil(100, HudLayout.BottomBaseline, 0f);
        var half = HudLayout.ReloadVeil(100, HudLayout.BottomBaseline, 0.5f);
        var nearlyDone = HudLayout.ReloadVeil(100, HudLayout.BottomBaseline, 0.9f);
        var done = HudLayout.ReloadVeil(100, HudLayout.BottomBaseline, 1f);

        Assert.Equal(HudLayout.VeilWidth, start!.Value.Width);
        Assert.Equal(HudLayout.VeilWidth / 2, half!.Value.Width);
        Assert.Equal(start.Value.X, half.Value.X);
        Assert.True(nearlyDone!.Value.Width < half.Value.Width);
        Assert.Null(done);
        Assert.Equal(HudLayout.BottomBaseline - HudLayout.BottomBodyHeight + HudLayout.VeilInset, start.Value.Y);
    }

    [Fact]
    public void The_reload_veil_takes_its_look_from_the_table()
    {
        using var json = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(RepositoryFiles.Find("assets/hud/hud_parts.json")));
        var veil = json.RootElement.GetProperty("game").GetProperty("reloadVeil");

        Assert.Equal(veil.GetProperty("alpha").GetSingle(), Parts.ReloadVeilAlpha);
        Assert.Equal(veil.GetProperty("edgeAlpha").GetSingle(), Parts.ReloadVeilEdgeAlpha);
        Assert.Equal(veil.GetProperty("color")[0].GetInt32(), Parts.ReloadVeil.R);
        Assert.Equal(veil.GetProperty("edgeColor")[0].GetInt32(), Parts.ReloadVeilEdge.R);
        Assert.InRange(Parts.ReloadVeilAlpha, 0.01f, 1f);
        Assert.InRange(Parts.ReloadVeilEdgeAlpha, 0.01f, 1f);
        Assert.NotEqual(Parts.ReloadVeil, Parts.ReloadVeilEdge);
    }

    private static HudRect? Union(IEnumerable<HudRect> rects)
    {
        HudRect? union = null;
        foreach (var rect in rects)
        {
            union = union is not { } u
                ? rect
                : new HudRect(Math.Min(u.X, rect.X), Math.Min(u.Y, rect.Y),
                    Math.Max(u.Right, rect.Right) - Math.Min(u.X, rect.X), Math.Max(u.Bottom, rect.Bottom) - Math.Min(u.Y, rect.Y));
        }

        return union;
    }
}
