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
    // 792: the stamina frame then starts so that a sixth item would end on it without spacing (StaminaBodyX mod 36 < 6).
    public static TheoryData<int> PictureWidths => new() { 854, 768, 1120, 1706, 640, 600, 792 };

    /// <summary>The groups of the HUD, each by the frames that make it.</summary>
    private static readonly Dictionary<string, string[]> Groups = new()
    {
        ["items"] = ["frame_item_0", "frame_item_1"],
        ["stamina"] = ["frame_stamina"],
        ["weapons"] = ["frame_weapon_0", "frame_weapon_1", "frame_weapon_2", "frame_weapon_3",
            "frame_weapon_0_active", "frame_weapon_1_active", "frame_weapon_2_active", "frame_weapon_3_active"],
        ["level"] = ["frame_level", "frame_life", "life_frame_end", "frame_xp", "frame_skill_0"],
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
            new(WeaponKind.Pistol, 6, false, true, 0f, 7),
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
            new(WeaponKind.Rifle, MostRounds, true, false, 0f, MostRounds),
            new(WeaponKind.Blunderbuss, MostRounds, false, true, 0f, MostRounds),
            new(WeaponKind.Pistol, MostRounds, false, false, 0f, MostRounds),
        ],
        Items = Enum.GetValues<ItemKind>().Concat(Enum.GetValues<ItemKind>()).ToList(),
        Boss = new HudBoss(Gorm.Name, 0.5f),
        ConsecratedSecondsLeft = 12.5f,
        MaxLife = Balance.Default.Boons.HealthCap, // the longest life bar the game can reach (issue #435)
        LifePerTick = Balance.Default.Boons.Health,
        SkillPoints = 99, // issue #502: the widest placeholder text
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

    [Fact]
    public void Every_weapon_and_item_has_a_picture_in_the_table_so_no_initial_stands_in()
    {
        // Issue #381: the initial is only a safety net for a kind added later without a picture.
        Assert.All(Enum.GetValues<WeaponKind>(), kind => Assert.True(HudLayout.IconOf(kind) is { } icon && Parts.Contains(icon),
            $"{kind} has no picture in the table"));
        Assert.All(Enum.GetValues<ItemKind>(), kind => Assert.True(HudLayout.IconOf(kind) is { } icon && Parts.Contains(icon),
            $"{kind} has no picture in the table"));

        var picture = Arrange(Busy());
        var initials = Enum.GetValues<WeaponKind>().Select(kind => Weapons.Info(kind).Name[..1])
            .Concat(Enum.GetValues<ItemKind>().Select(kind => Items.Info(kind).Name[..1]));
        Assert.DoesNotContain(picture.Texts, text => initials.Contains(text.Text));
    }

    [Fact]
    public void The_club_sits_in_the_middle_of_the_melee_frame()
    {
        var picture = Arrange(Busy());

        var club = Layout.Bounds(Single(picture, "icon_club"));
        var ground = Layout.BodyOf(Single(picture, "frame_weapon_0")).Inset(HudLayout.Rim);
        AssertCentredIn(ground, club);
        Assert.InRange((2 * club.Y) + club.Height - ((2 * ground.Y) + ground.Height), -1, 1);
        Assert.DoesNotContain(picture.Parts, part => part.Name == "icon_blade");
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
    public void Every_frame_has_a_body_inside_its_ornament_and_other_parts_are_all_body()
    {
        // Issue #435: the life frame is drawn cut to the bar's length and closed by its end cap; its body runs on into
        // that cap (and no further), as the end cap's own ornament closes it.
        static int Closing(HudPart part) => part.Name == "frame_life" ? Parts["life_frame_end"].Width - part.Body.X : 0;
        Assert.All(Parts.All, part =>
        {
            Assert.True(part.Body.X >= 0 && part.Body.Y >= 0 && part.Body.Right <= part.Width + Closing(part) && part.Body.Bottom <= part.Height,
                $"{part.Name}: body {part.Body} leaves the part");
            if (part.Name.StartsWith("frame_", StringComparison.Ordinal))
            {
                Assert.True(part.Body.Width < part.Width + Closing(part) && part.Body.Height < part.Height, $"{part.Name} has no ornament around its body");
            }
            else
            {
                Assert.Equal(new HudRect(0, 0, part.Width, part.Height), part.Body);
            }
        });
    }

    [Theory]
    [MemberData(nameof(PictureWidths))]
    public void The_bottom_frames_end_on_one_line_the_spacing_above_the_bottom_edge(int pictureWidth)
    {
        var canvas = HudCanvas.For(pictureWidth);
        var picture = Arrange(Busy(), canvas: canvas);

        var bottomFrames = picture.Parts.Where(part => part.Name.StartsWith("frame_item", StringComparison.Ordinal)
            || part.Name == "frame_stamina" || part.Name.StartsWith("frame_weapon", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(bottomFrames);
        Assert.All(bottomFrames, part => Assert.Equal(canvas.Height - HudLayout.Spacing, Layout.BodyOf(part).Bottom));
        Assert.Equal(480 - HudLayout.Spacing, HudLayout.BottomBaseline);
    }

    [Theory]
    [MemberData(nameof(PictureWidths))]
    public void The_groups_keep_the_spacing_to_their_corner_or_stand_in_the_middle(int pictureWidth)
    {
        var canvas = HudCanvas.For(pictureWidth);
        var picture = Arrange(Busy(), canvas: canvas);
        var gap = HudLayout.Spacing;
        HudRect Body(string name) => Layout.BodyOf(picture.Parts.First(part => part.Name == name));

        // Left: the spacing from the left edge (and the top edge for the top row: the life frame, issue #435).
        Assert.Equal(gap, Body("frame_level").X);
        Assert.Equal(gap, Body("frame_life").Y);
        Assert.Equal(gap, Body("frame_item_0").X);

        // Right: the spacing from the right edge.
        Assert.Equal((canvas.Width - gap, gap), (Body("frame_currency").Right, Body("frame_currency").Y));
        Assert.Equal(canvas.Width - gap, Body("frame_weapon_3").Right);

        // Middle: centred (within the half pixel of an odd difference); the boss the spacing below the top edge, centred
        // unless the life bar reaches that far - then the spacing right of it (issue #435).
        Assert.InRange(Body("frame_stamina").X + Body("frame_stamina").Right - canvas.Width, -1, 0);
        var bossCentred = (canvas.Width - Body("frame_boss").Width) / 2;
        Assert.Equal(Math.Max(bossCentred, Body("frame_life").Right + gap), Body("frame_boss").X);
        Assert.Equal(gap, Body("frame_boss").Y);
    }

    [Theory]
    [MemberData(nameof(PictureWidths))]
    public void Frames_in_a_row_keep_the_spacing_between_their_bodies(int pictureWidth)
    {
        // Issue #387: one rule for every gap - neighbour to neighbour as frame to edge.
        var canvas = HudCanvas.For(pictureWidth);
        var picture = Arrange(Busy(), canvas: canvas);

        List<HudRect> Row(Func<string, bool> inRow) =>
            picture.Parts.Where(part => inRow(part.Name)).Select(Layout.BodyOf).OrderBy(body => body.X).ToList();

        var rows = new Dictionary<string, List<HudRect>>
        {
            ["items"] = Row(name => name.StartsWith("frame_item", StringComparison.Ordinal)),
            ["weapons"] = Row(name => name.StartsWith("frame_weapon", StringComparison.Ordinal)),
            ["level and life"] = Row(name => name is "frame_level" or "frame_life"),
            ["level and experience"] = Row(name => name is "frame_level" or "frame_xp"),
        };

        Assert.True(rows["items"].Count >= 4, "Too few item frames - the case would prove little.");
        Assert.Equal(4, rows["weapons"].Count);
        Assert.Equal(2, rows["level and life"].Count);
        Assert.Equal(2, rows["level and experience"].Count);
        foreach (var (name, row) in rows)
        {
            for (var i = 1; i < row.Count; i++)
            {
                Assert.True(row[i].X - row[i - 1].Right == HudLayout.Spacing,
                    $"{name}: frame {i} stands {row[i].X - row[i - 1].Right} from the one before on {canvas}");
            }
        }

        // Issue #435, layout B: the life frame over the experience frame, one column, the spacing apart; the level frame
        // in the middle of their height; the skill frame in a second row the spacing below, at the left edge.
        HudRect Body(string name) => Layout.BodyOf(Single(picture, name));
        var (level, life, xp, skill) = (Body("frame_level"), Body("frame_life"), Body("frame_xp"), Body("frame_skill_0"));
        Assert.Equal(life.X, xp.X);
        Assert.Equal(life.Bottom + HudLayout.Spacing, xp.Y);
        Assert.InRange((2 * level.Y) + level.Height - (life.Y + xp.Bottom), -1, 1);
        Assert.Equal((HudLayout.Spacing, xp.Bottom + HudLayout.Spacing), (skill.X, skill.Y));

        // Between the groups of the bottom row at least the spacing as well.
        var stamina = Body("frame_stamina");
        Assert.True(stamina.X - rows["items"][^1].Right >= HudLayout.Spacing, $"items run up to the stamina frame on {canvas}");
        Assert.True(rows["weapons"][0].X - stamina.Right >= HudLayout.Spacing, $"weapons run up to the stamina frame on {canvas}");
    }

    [Theory]
    [MemberData(nameof(PictureWidths))]
    public void Inside_every_frame_the_contents_keep_equal_gaps_to_the_edge_and_to_each_other(int pictureWidth)
    {
        var canvas = HudCanvas.For(pictureWidth);
        foreach (var picture in new[] { Arrange(Busy(), LongestToast(), canvas), Arrange(Calm(), "Picked up Hunter's Eye", canvas) })
        {
            foreach (var frame in picture.Parts.Where(part => part.Name.StartsWith("frame_", StringComparison.Ordinal)))
            {
                var ground = Layout.BodyOf(frame).Inset(HudLayout.Rim);
                var contents = picture.Parts.Where(part => !part.Name.StartsWith("frame_", StringComparison.Ordinal)
                        && !part.Name.EndsWith("_full", StringComparison.Ordinal))
                    .Select(Layout.Bounds)
                    .Concat(picture.Texts.Select(Layout.Bounds))
                    .Where(rect => Contains(ground, (2 * rect.X) + rect.Width, (2 * rect.Y) + rect.Height))
                    .ToList();

                if (contents.Count == 0)
                {
                    Assert.True(frame.Name.StartsWith("frame_weapon", StringComparison.Ordinal), $"{frame.Name} is empty on {canvas}");
                    continue; // an empty weapon place
                }

                AssertSpreadEvenly(ground, Blocks(contents), $"{frame.Name} on {canvas}");
            }
        }
    }

    [Fact]
    public void A_sign_and_its_number_stand_the_sign_gap_apart()
    {
        var picture = Arrange(Busy(), "Picked up Hunter's Eye");

        var coin = Layout.Bounds(Single(picture, "icon_coin"));
        var key = Layout.Bounds(Single(picture, "icon_key"));
        var eye = Layout.Bounds(Single(picture, "icon_toast_eye"));
        Assert.Equal(coin.Right + HudLayout.SignGap, Assert.Single(picture.Texts, text => text.Text == "99999").X);
        Assert.Equal(key.Right + HudLayout.SignGap, picture.Texts.Single(text => text.Text == "99" && text.X > key.X).X);
        Assert.Equal(eye.Right + HudLayout.SignGap, Assert.Single(picture.Texts, text => text.Text == "Picked up Hunter's Eye").X);
    }

    [Theory]
    [MemberData(nameof(PictureWidths))]
    public void Signs_and_numbers_beside_each_other_each_stand_in_the_middle_of_the_ground_from_top_to_bottom(int pictureWidth)
    {
        // A sign row (coin and silver, key and keys, eye and toast) is spread across, so each of its pieces is centred
        // up and down on its own - a sign 1 px lower than its number is off.
        var picture = Arrange(Busy(), "Picked up Hunter's Eye", HudCanvas.For(pictureWidth));
        foreach (var (frame, signs, texts) in new[]
                 {
                     ("frame_currency", new[] { "icon_coin", "icon_key" }, new[] { "99999", "99" }),
                     ("frame_toast", new[] { "icon_toast_eye" }, new[] { "Picked up Hunter's Eye" }),
                 })
        {
            var ground = Layout.BodyOf(Single(picture, frame)).Inset(HudLayout.Rim);
            var pieces = signs.Select(sign => (sign, Layout.Bounds(Single(picture, sign))))
                .Concat(picture.Texts.Where(text => texts.Contains(text.Text) && Layout.Bounds(text).Overlaps(ground))
                    .Select(text => (text.Text, Layout.Bounds(text))))
                .ToList();

            Assert.Equal(signs.Length + texts.Length, pieces.Count);
            Assert.All(pieces, piece => Assert.True(
                piece.Item2.Y == HudLayout.Spread(ground.Y, ground.Height, [piece.Item2.Height])[0],
                $"{piece.Item1} at line {piece.Item2.Y} is not in the middle of {frame} ({ground}) on {pictureWidth}"));
        }
    }

    [Theory]
    [MemberData(nameof(PictureWidths))]
    public void What_stands_above_or_below_a_row_keeps_the_spacing_to_its_ornament(int pictureWidth)
    {
        var canvas = HudCanvas.For(pictureWidth);
        var picture = Arrange(Busy(), LongestToast(), canvas);
        var gap = HudLayout.Spacing;

        // The toast above the item frames' gables, at the left edge like them.
        var toast = Layout.BodyOf(Single(picture, "frame_toast"));
        var itemsTop = picture.Parts.Where(part => part.Name.StartsWith("frame_item", StringComparison.Ordinal)).Min(part => part.Y);
        Assert.Equal((gap, itemsTop - gap), (toast.X, toast.Bottom));

        // The rounds above the weapon frames' gables; the consecrated time above the rounds, ending with the weapon row
        // (issue #388; before, the consecrated time stood right above the gables).
        var consecrated = Layout.Bounds(Assert.Single(picture.Texts, text => text.Text.StartsWith("Consecrated", StringComparison.Ordinal)));
        var weaponsTop = picture.Parts.Where(part => part.Name.StartsWith("frame_weapon", StringComparison.Ordinal)).Min(part => part.Y);
        var rounds = Layout.Bounds(Assert.Single(LargeOverWeapons(picture)));
        Assert.Equal(weaponsTop - gap, rounds.Bottom);
        Assert.Equal((canvas.Width - gap, rounds.Y - gap), (consecrated.Right, consecrated.Bottom));
        var withoutRounds = Arrange(Busy() with { IsMeleeInUse = true }, LongestToast(), canvas);
        var alone = Layout.Bounds(Assert.Single(withoutRounds.Texts, text => text.Text.StartsWith("Consecrated", StringComparison.Ordinal)));
        Assert.Equal((canvas.Width - gap, weaponsTop - gap), (alone.Right, alone.Bottom));

        // The hint under the level row's rivets, starting with the level frame.
        var hint = Layout.Bounds(Assert.Single(picture.Texts, text => text.Text == HudLayout.LevelUpHint));
        var levelBottom = picture.Parts.Where(part => Groups["level"].Contains(part.Name)).Max(part => Layout.Bounds(part).Bottom);
        Assert.Equal((gap, levelBottom + gap), (hint.X, hint.Y));
    }

    [Fact]
    public void Spread_leaves_equal_gaps_before_between_and_after_and_centres_one_alone()
    {
        Assert.Equal([13, 19], HudLayout.Spread(10, 13, [3, 1]));             // 9 free: gaps 3, 3, 3
        Assert.Equal([1, 5], HudLayout.Spread(0, 10, [3, 3]));                 // 4 free: gaps 1, 1, 2
        Assert.Equal([15], HudLayout.Spread(10, 20, [10]));                    // alone: centred
        Assert.Equal([9], HudLayout.Spread(10, 8, [10]));                      // longer than the stretch: out on both sides
        Assert.Equal([100, 110, 120], HudLayout.Spread(100, 30, [10, 10, 10])); // no room: no gaps
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
    public void The_skill_point_and_the_yellow_hint_show_only_while_a_boon_waits()
    {
        var calm = Arrange(Calm());
        var waiting = Arrange(Calm() with { PendingBoons = 1 });

        Assert.DoesNotContain(calm.Parts, part => part.Name is "frame_skill_0" or "icon_skillpoint");
        Assert.DoesNotContain(calm.Texts, text => text.Color == HudTextColor.Hint);
        Single(waiting, "frame_skill_0");
        Single(waiting, "icon_skillpoint");

        // Issue #387: the key cap beside the star is gone; the hint stands under the level number and the experience bar.
        Assert.DoesNotContain(waiting.Parts, part => part.Name == "frame_prompt");
        var hint = Assert.Single(waiting.Texts, text => text.Color == HudTextColor.Hint);
        Assert.Equal("[X] to level up", hint.Text);
        Assert.Empty(Font.Missing(hint.Text));
        var hintRect = Layout.Bounds(hint);
        var level = Layout.Bounds(Single(waiting, "frame_level"));
        var xp = Layout.Bounds(Single(waiting, "frame_xp"));
        Assert.True(hintRect.Y >= Math.Max(level.Bottom, xp.Bottom), $"The hint {hintRect} is not under level {level} and bar {xp}.");
        Assert.True(hintRect.X < level.Right && hintRect.Right > xp.X, $"The hint {hintRect} does not reach under the number and the bar.");
    }

    [Fact]
    public void The_skill_points_show_beside_the_experience_bar_only_while_there_are_any()
    {
        // Issue #502: a placeholder text the spacing right of the experience body, in the middle of its height.
        var calm = Arrange(Calm());
        var one = Arrange(Calm() with { SkillPoints = 1 });
        var two = Arrange(Calm() with { SkillPoints = 2 });

        Assert.DoesNotContain(calm.Texts, text => text.Text.Contains("skill point", StringComparison.Ordinal));
        var placed = Assert.Single(one.Texts, text => text.Text == "1 skill point");
        Assert.Single(two.Texts, text => text.Text == "2 skill points");
        Assert.Equal(HudTextColor.Main, placed.Color);
        Assert.Empty(Font.Missing("0123456789 skill points"));

        var xp = Layout.BodyOf(Single(one, "frame_xp"));
        var rect = Layout.Bounds(placed);
        Assert.Equal(xp.Right + HudLayout.Spacing, rect.X);
        Assert.Equal(HudLayout.Spread(xp.Y, xp.Height, [rect.Height])[0], rect.Y);
    }

    [Theory]
    [MemberData(nameof(PictureWidths))]
    public void The_boss_keeps_the_spacing_right_of_the_skill_points_too(int pictureWidth)
    {
        // Issue #502: on the shortest life bar the skill points may reach further right than it - the boss frame, on the
        // line of the life frame and as deep as the experience bar, must not run into them.
        // This only states the contract: it does not guard the Math.Max line in HudLayout.ArrangeLevel. The boss frame
        // stands centred and on every canvas width of today lies right of the text anyway, so without that line it
        // would stay green as well.
        var canvas = HudCanvas.For(pictureWidth);
        var picture = Arrange(Busy() with { MaxLife = Balance.Default.Player.MaxHealth }, canvas: canvas);

        var points = Layout.Bounds(Assert.Single(picture.Texts, text => text.Text == HudLayout.SkillPointsText(99)));
        var boss = Layout.Bounds(Single(picture, "frame_boss"));
        Assert.False(points.Overlaps(boss), $"The skill points {points} run into the boss {boss} on {canvas}");
        Assert.True(Layout.BodyOf(Single(picture, "frame_boss")).X >= points.Right + HudLayout.Spacing);
    }

    [Fact]
    public void The_hint_is_the_yellow_of_the_table_and_no_other_hud_colour()
    {
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(RepositoryFiles.Find("assets/hud/hud_parts.json")));
        var hint = json.RootElement.GetProperty("game").GetProperty("text").GetProperty("hint");

        Assert.Equal(new HudColor(hint[0].GetByte(), hint[1].GetByte(), hint[2].GetByte()), Parts.TextHint);
        Assert.NotEqual(Parts.TextMain, Parts.TextHint);
        Assert.NotEqual(Parts.TextSecond, Parts.TextHint);
        Assert.NotEqual(Parts.TextPickup, Parts.TextHint);
        Assert.True(Parts.TextHint.R > Parts.TextHint.B + 60 && Parts.TextHint.G > Parts.TextHint.B + 60, $"{Parts.TextHint} is not yellow");
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
        var stamina = Layout.Bounds(Single(picture, "frame_stamina"));
        var life = Layout.BodyOf(Single(picture, "frame_life"));

        Assert.DoesNotContain(picture.Texts, text => Layout.Bounds(text).Overlaps(stamina) || Layout.Bounds(text).Overlaps(life));
        var texts = picture.Texts.Select(text => text.Text).ToList();
        Assert.Contains("99", texts);    // level and keys
        Assert.Equal(2, texts.Count(text => text == "99"));
        Assert.Contains("99999", texts); // silver
        // Issue #388: only the rounds of the chosen weapon, large; the other ranged weapons show none.
        Assert.Single(texts, text => text == MostRounds.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>The highest health at the start and after some health boons, up to the cap (issue #435).</summary>
    public static TheoryData<int> MaxLives => new()
    {
        Balance.Default.Player.MaxHealth,
        Balance.Default.Player.MaxHealth + (3 * Balance.Default.Boons.Health),
        Balance.Default.Boons.HealthCap,
    };

    [Theory]
    [MemberData(nameof(MaxLives))]
    public void The_life_bar_is_one_pixel_per_point_of_the_highest_health_and_its_frame_ends_with_it(int maxLife)
    {
        var picture = Arrange(Calm() with { MaxLife = maxLife, LifePerTick = Balance.Default.Boons.Health });

        var bar = Layout.Bounds(Single(picture, "life_bar_empty"));
        var cap = Layout.Bounds(Single(picture, "life_cap_empty"));
        Assert.Equal(bar.Right, cap.X);
        Assert.Equal(maxLife, cap.Right - bar.X);

        // The frame is cut to the bar and closed by its end cap right behind the cut; the bar fills its ground.
        var frame = Single(picture, "frame_life");
        var end = Single(picture, "life_frame_end");
        Assert.Equal((frame.X + frame.Width, frame.Y), (end.X, end.Y));
        var ground = Layout.BodyOf(frame).Inset(HudLayout.Rim);
        Assert.Equal((ground.X, ground.Right), (bar.X, cap.Right));
        Assert.Equal(Parts["frame_life"].Width + Parts["life_frame_end"].Width - (Longest - maxLife), Layout.Bounds(end).Right - frame.X);
    }

    [Fact]
    public void A_higher_maximum_moves_the_end_of_the_bar_and_its_frame_by_as_many_pixels()
    {
        // The counter-check to the length: same life share, two maxima - only the right end moves.
        var start = Balance.Default.Player.MaxHealth;
        var grown = start + (4 * Balance.Default.Boons.Health);
        var before = Arrange(Calm() with { MaxLife = start });
        var after = Arrange(Calm() with { MaxLife = grown });

        Assert.Equal(Single(before, "life_bar_empty").X, Single(after, "life_bar_empty").X);
        Assert.Equal(Single(before, "frame_life").X, Single(after, "frame_life").X);
        Assert.Equal(grown - start, Single(after, "life_cap_empty").X - Single(before, "life_cap_empty").X);
        Assert.Equal(grown - start, Single(after, "life_frame_end").X - Single(before, "life_frame_end").X);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(0.5f)]
    [InlineData(0.01f)]
    [InlineData(0f)]
    public void The_life_bar_fills_from_the_left_and_nothing_but_its_ticks_lies_on_it(float life)
    {
        var maxLife = Balance.Default.Player.MaxHealth;
        var shape = LifeBar.Shape(maxLife, life, Balance.Default.Boons.Health, Parts["life_cap_empty"].Width, Longest);
        Assert.True(life == 0f || shape.Filled > 0, "A sliver of life must still show - the case would prove nothing.");
        var picture = Arrange(Calm() with { Life = life, MaxLife = maxLife, LifePerTick = Balance.Default.Boons.Health });

        var empty = Single(picture, "life_bar_empty");
        Assert.Equal(shape.Filled, picture.Parts.SingleOrDefault(part => part.Name == "life_bar_full").Width);
        if (shape.Filled > 0)
        {
            Assert.Equal((empty.X, empty.Y), (Single(picture, "life_bar_full").X, Single(picture, "life_bar_full").Y));
        }

        Assert.Equal(life == 1f, picture.Parts.Any(part => part.Name == "life_cap_full"));

        // Issue #387: no light edge at the fill level - only the bar parts and the tick marks lie on the bar.
        var bar = Union([Layout.Bounds(empty), Layout.Bounds(Single(picture, "life_cap_empty"))])!.Value;
        Assert.All(picture.Parts.Where(part => !part.Name.StartsWith("life_", StringComparison.Ordinal) && part.Name != "frame_life"),
            part => Assert.False(Layout.Bounds(part).Overlaps(bar), $"{part.Name} lies on the life bar"));
        Assert.DoesNotContain(picture.Texts, text => Layout.Bounds(text).Overlaps(bar));
        Assert.DoesNotContain(picture.ReloadVeils, veil => veil.Overlaps(bar));
    }

    [Fact]
    public void Almost_full_life_rounds_to_the_full_bar_and_not_one_pixel_short()
    {
        var maxLife = Balance.Default.Player.MaxHealth;
        var cap = Parts["life_cap_empty"].Width;
        var inner = maxLife - (2 * cap);
        var life = (inner - 0.4f) / inner; // rounds to the whole bar, cutting off would leave one pixel short
        Assert.Equal(inner - 1, (int)(inner * life));

        var picture = Arrange(Calm() with { Life = life, MaxLife = maxLife });

        Assert.Equal(maxLife - cap, Single(picture, "life_bar_full").Width);
        Single(picture, "life_cap_full");
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(0.5f)]
    [InlineData(0f)]
    public void The_life_bar_has_a_tick_per_health_boon_every_fifth_long_dark_over_the_fill(float life)
    {
        var maxLife = Balance.Default.Boons.HealthCap;
        var step = Balance.Default.Boons.Health;
        var picture = Arrange(Calm() with { Life = life, MaxLife = maxLife, LifePerTick = step });
        var bar = Single(picture, "life_bar_empty");
        var cap = Parts["life_cap_empty"].Width;
        var filled = picture.Parts.SingleOrDefault(part => part.Name == "life_bar_full").Width;

        var ticks = picture.Parts.Where(part => part.Name.StartsWith("life_tick", StringComparison.Ordinal)).OrderBy(part => part.X).ToList();
        Assert.Equal((maxLife - (2 * cap) - 1) / step, ticks.Count);
        for (var i = 0; i < ticks.Count; i++)
        {
            var tick = ticks[i];
            var number = i + 1;
            var isLong = number % LifeBar.LongTickEvery == 0;
            var isDark = tick.X - bar.X < filled;
            Assert.Equal(bar.X + cap + (number * step), tick.X);
            Assert.Equal((isLong ? "life_tick_long_" : "life_tick_") + (isDark ? "full" : "empty"), tick.Name);

            // On the bar where the table puts it (long ones reach higher), never past its edge.
            var rect = Layout.Bounds(tick);
            Assert.Equal(Parts[tick.Name].LayoutY - Parts["life_bar_empty"].LayoutY, rect.Y - bar.Y);
            Assert.True(rect.Y > bar.Y && rect.Bottom < Layout.Bounds(bar).Bottom, $"{tick.Name} at {rect} leaves the bar");
        }

        Assert.Contains(ticks, tick => tick.Name.Contains("long", StringComparison.Ordinal));
        Assert.Equal(life == 1f, ticks.All(tick => tick.Name.EndsWith("_full", StringComparison.Ordinal)));
        Assert.Equal(life == 0f, ticks.All(tick => tick.Name.EndsWith("_empty", StringComparison.Ordinal)));
    }

    [Fact]
    public void A_long_life_bar_pushes_the_boss_right_on_a_narrow_canvas_but_not_on_a_wide_one()
    {
        // Issue #435: at the cap the life frame reaches past where the centred boss would start on 16:10.
        static HudRect Body(HudPicture picture, string name) => Layout.BodyOf(Single(picture, name));
        var fight = Calm() with { Boss = new HudBoss(Gorm.Name, 0.5f), MaxLife = Balance.Default.Boons.HealthCap };
        var bossWidth = Parts["frame_boss"].Body.Width;

        var narrow = Arrange(fight, canvas: HudCanvas.For(HudCanvas.MinimumWidth));
        Assert.True(Body(narrow, "frame_life").Right + HudLayout.Spacing > (HudCanvas.MinimumWidth - bossWidth) / 2,
            "The life bar ends before the centred boss - the case would prove nothing.");
        Assert.Equal(Body(narrow, "frame_life").Right + HudLayout.Spacing, Body(narrow, "frame_boss").X);

        var wide = Arrange(fight, canvas: Design);
        Assert.Equal((Design.Width - bossWidth) / 2, Body(wide, "frame_boss").X);
        Assert.True(Body(wide, "frame_boss").X > Body(wide, "frame_life").Right + HudLayout.Spacing);
    }

    [Fact]
    public void The_life_bar_never_grows_past_its_longest_part()
    {
        var picture = Arrange(Calm() with { MaxLife = Longest + 50 });

        Assert.Equal(Parts["life_bar_empty"].Width, Single(picture, "life_bar_empty").Width);
        Assert.Equal(Parts["frame_life"].Width, Single(picture, "frame_life").Width);
        Assert.True(Balance.Default.Boons.HealthCap <= Longest, "The cap lets the life bar outgrow its parts.");
    }

    /// <summary>The longest life bar the parts can draw: the stretched bar and its end cap.</summary>
    private static int Longest => Parts["life_bar_empty"].Width + Parts["life_cap_empty"].Width;

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
        // Issue #388: only the chosen weapon shows its rounds, so the two cases are two pictures.
        static HudReadout Chosen(bool reloading) => Calm() with
        {
            Keys = 0,
            Weapons =
            [
                new(WeaponKind.Club, null, false, false),
                new(WeaponKind.Rifle, 1, false, false, 0f, 4),
                new(WeaponKind.Blunderbuss, 0, reloading, true, 0f, 1),
                new(WeaponKind.Pistol, null, false, false),
            ],
        };

        Assert.Equal(HudTextColor.Second, Assert.Single(LargeOverWeapons(Arrange(Chosen(true)))).Color);
        Assert.Equal(HudTextColor.Main, Assert.Single(LargeOverWeapons(Arrange(Chosen(false)))).Color);
    }

    /// <summary>Every ranged place chosen in turn, each with rounds no other place has.</summary>
    public static TheoryData<int> RangedPlaces => new() { 1, 2, 3 };

    [Theory]
    [MemberData(nameof(RangedPlaces))]
    public void The_large_number_is_the_rounds_of_the_chosen_weapon_and_not_of_another(int chosen)
    {
        // Issue #388: rounds 3, 2 and 0 and magazines 7, 4 and 1 - each number belongs to one place only.
        int[] rounds = [3, 2, 0];
        int[] sizes = [7, 4, 1];
        WeaponKind[] kinds = [WeaponKind.Pistol, WeaponKind.Rifle, WeaponKind.Blunderbuss];
        var readout = Calm() with
        {
            Keys = 0,
            Silver = 0,
            Weapons = [new(WeaponKind.SilverBlade, null, false, false),
                .. Enumerable.Range(0, 3).Select(i => new HudWeapon(kinds[i], rounds[i], false, i + 1 == chosen, 0f, sizes[i]))],
        };

        var picture = Arrange(readout);

        var large = Assert.Single(LargeOverWeapons(picture));
        Assert.Equal(rounds[chosen - 1].ToString(CultureInfo.InvariantCulture), large.Text);
        var small = Assert.Single(picture.Texts, text => text.Text.StartsWith(HudLayout.MagazineMark, StringComparison.Ordinal));
        Assert.Equal(HudLayout.MagazineMark + sizes[chosen - 1].ToString(CultureInfo.InvariantCulture), small.Text);
        Assert.Equal(1, small.Scale);
    }

    [Theory]
    [MemberData(nameof(PictureWidths))]
    public void The_rounds_stand_large_in_the_middle_above_the_weapon_row_with_the_magazine_small_beside_them(int pictureWidth)
    {
        var canvas = HudCanvas.For(pictureWidth);
        var picture = Arrange(Calm(), canvas: canvas);

        var large = Assert.Single(picture.Texts, text => text.Text == "6");
        var small = Assert.Single(picture.Texts, text => text.Text == "/ 7");
        Assert.Equal((PixelFont.NumberScale, 1), (large.Scale, small.Scale));
        Assert.Equal((HudTextColor.Main, HudTextColor.Second), (large.Color, small.Color)); // neutral silver, no signal colour

        var (largeRect, smallRect) = (Layout.Bounds(large), Layout.Bounds(small));
        Assert.Equal(largeRect.Right + HudLayout.SignGap, smallRect.X);
        Assert.Equal(largeRect.Bottom, smallRect.Bottom); // one base line

        var frames = picture.Parts.Where(part => part.Name.StartsWith("frame_weapon", StringComparison.Ordinal)).ToList();
        var bodies = frames.Select(Layout.BodyOf).ToList();
        var (rowLeft, rowRight) = (bodies.Min(body => body.X), bodies.Max(body => body.Right));
        var both = Union([largeRect, smallRect])!.Value;
        Assert.InRange((2 * both.X) + both.Width - (rowLeft + rowRight), -1, 1);
        Assert.Equal(frames.Min(part => part.Y) - HudLayout.Spacing, both.Bottom);
    }

    [Theory]
    [MemberData(nameof(PictureWidths))]
    public void No_number_stands_beside_the_weapon_pictures_any_more(int pictureWidth)
    {
        // Issue #388: the small rounds beside each picture are gone - no text touches a weapon frame.
        var canvas = HudCanvas.For(pictureWidth);
        foreach (var picture in new[] { Arrange(Busy(), canvas: canvas), Arrange(Calm(), canvas: canvas) })
        {
            var frames = picture.Parts.Where(part => part.Name.StartsWith("frame_weapon", StringComparison.Ordinal)).Select(Layout.Bounds).ToList();
            Assert.Equal(4, frames.Count);
            Assert.All(picture.Texts, text => Assert.False(frames.Any(frame => frame.Overlaps(Layout.Bounds(text))),
                $"'{text.Text}' {Layout.Bounds(text)} lies on a weapon frame on {canvas}"));
        }
    }

    [Fact]
    public void No_rounds_show_while_the_melee_weapon_is_in_use_or_chosen()
    {
        Assert.Contains(Arrange(Calm()).Texts, text => text.Text == "6"); // the case below would prove nothing otherwise

        var inUse = Arrange(Calm() with { IsMeleeInUse = true });
        var meleeChosen = Arrange(Calm() with
        {
            Weapons = [new(WeaponKind.SilverBlade, null, false, true), new(WeaponKind.Pistol, 6, false, false, 0f, 7), new(null, null, false, false), new(null, null, false, false)],
        });

        foreach (var picture in new[] { inUse, meleeChosen })
        {
            Assert.DoesNotContain(picture.Texts, text => text.Text == "6" || text.Text.StartsWith(HudLayout.MagazineMark, StringComparison.Ordinal));
            Assert.Empty(LargeOverWeapons(picture));
        }
    }

    [Fact]
    public void With_the_large_rounds_the_hud_still_covers_less_than_a_tenth_of_the_picture()
    {
        // Issue #388: the number adds to what the HUD covers - at 854 x 480 all of it stays under 10 %.
        var canvas = HudCanvas.For(854);
        var picture = Arrange(Calm(), canvas: canvas);
        Assert.Single(LargeOverWeapons(picture)); // the number is in the count

        var covered = new bool[canvas.Width, canvas.Height];
        foreach (var rect in picture.Parts.Select(Layout.Bounds).Concat(picture.Texts.Select(Layout.Bounds)))
        {
            for (var x = Math.Max(0, rect.X); x < Math.Min(canvas.Width, rect.Right); x++)
            {
                for (var y = Math.Max(0, rect.Y); y < Math.Min(canvas.Height, rect.Bottom); y++)
                {
                    covered[x, y] = true;
                }
            }
        }

        var share = (double)covered.Cast<bool>().Count(pixel => pixel) / (canvas.Width * canvas.Height);
        Assert.True(share < 0.10, $"The HUD covers {share:P1} of the picture.");
    }

    [Fact]
    public void An_empty_magazine_shows_a_large_zero_in_the_main_colour()
    {
        var picture = Arrange(Calm() with
        {
            Weapons = [new(WeaponKind.SilverBlade, null, false, false), new(WeaponKind.Pistol, 0, false, true, 0f, 7), new(null, null, false, false), new(null, null, false, false)],
        });

        var zero = Assert.Single(LargeOverWeapons(picture));
        Assert.Equal("0", zero.Text);
        Assert.Equal((PixelFont.NumberScale, HudTextColor.Main), (zero.Scale, zero.Color));
    }

    [Fact]
    public void A_dead_player_has_no_crosshair_no_weapons_and_no_consecrated_time()
    {
        var picture = Arrange(Busy() with { IsDead = true });

        Assert.DoesNotContain(picture.Parts, part => part.Name == "crosshair" || part.Name.StartsWith("frame_weapon", StringComparison.Ordinal));
        Assert.DoesNotContain(picture.Texts, text => text.Text.StartsWith("Consecrated", StringComparison.Ordinal));
        Single(picture, "frame_stamina");
        Single(picture, "frame_life");
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
        // Since issue #381 the leech has its picture too; before, its initial "L" stood in.
        AssertCentredIn(Layout.Bounds(frames[2]), Layout.Bounds(Single(picture, "icon_leech")));
        Assert.DoesNotContain(picture.Texts, text => text.Text == "L");
    }

    // Whether the point (given doubled, to keep half pixels) lies in the rectangle.
    private static bool Contains(HudRect rect, int doubledX, int doubledY) =>
        2 * rect.X <= doubledX && doubledX < 2 * rect.Right && 2 * rect.Y <= doubledY && doubledY < 2 * rect.Bottom;

    // The contents of a frame as the eye groups them: things side by side at most a sign gap apart (a sign and its
    // number, interlocking bar segments) make one block.
    private static List<HudRect> Blocks(IEnumerable<HudRect> contents)
    {
        var blocks = contents.ToList();
        for (var merged = true; merged;)
        {
            merged = false;
            for (var i = 0; i < blocks.Count && !merged; i++)
            {
                for (var j = i + 1; j < blocks.Count && !merged; j++)
                {
                    var (a, b) = (blocks[i], blocks[j]);
                    var sideBySide = a.Y < b.Bottom && b.Y < a.Bottom;
                    var gap = Math.Max(a.X, b.X) - Math.Min(a.Right, b.Right);
                    if (sideBySide && gap <= HudLayout.SignGap)
                    {
                        blocks[i] = Union([a, b])!.Value;
                        blocks.RemoveAt(j);
                        merged = true;
                    }
                }
            }
        }

        return blocks;
    }

    // The blocks share the ground with equal gaps (within a pixel) to its edges and to each other along one line,
    // and stand in its middle across that line; one block alone stands in the middle both ways.
    private static void AssertSpreadEvenly(HudRect ground, List<HudRect> blocks, string what)
    {
        var vertical = blocks.Count > 1 && blocks.Any(a => blocks.Any(b => a != b && a.X < b.Right && b.X < a.Right));
        var (start, length) = vertical ? (ground.Y, ground.Height) : (ground.X, ground.Width);
        var line = blocks.Select(block => vertical ? (Start: block.Y, End: block.Bottom) : (Start: block.X, End: block.Right))
            .OrderBy(span => span.Start).ToList();

        var gaps = new List<int> { line[0].Start - start };
        gaps.AddRange(line.Zip(line.Skip(1), (before, after) => after.Start - before.End));
        gaps.Add(start + length - line[^1].End);
        Assert.True(gaps.Max() - gaps.Min() <= 1, $"{what}: gaps {string.Join(", ", gaps)} along the {(vertical ? "height" : "width")}");

        foreach (var block in blocks)
        {
            var across = vertical
                ? (2 * block.X) + block.Width - ((2 * ground.X) + ground.Width)
                : (2 * block.Y) + block.Height - ((2 * ground.Y) + ground.Height);
            Assert.True(Math.Abs(across) <= 1, $"{what}: {block} is {across / 2.0} off the middle of {ground}");
        }
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

        // Issue #389: at the start the veil is exactly the dark ground inside the silver rim - not on the rim, not beyond.
        var veil = Assert.Single(picture.ReloadVeils);
        var ground = Layout.BodyOf(picture.Parts.Single(part => part.Name == "frame_weapon_1_active")).Inset(HudLayout.Rim);
        Assert.Equal(ground, veil);
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.5f)]
    [InlineData(0.9f)]
    public void The_reload_veil_stays_inside_the_frame_ground_at_every_share(float progress)
    {
        var ground = new HudRect(731, 447, 32, 24);

        var veil = HudLayout.ReloadVeil(ground, progress)!.Value;

        Assert.True(veil.X >= ground.X && veil.Right <= ground.Right, $"veil {veil.X}..{veil.Right}, ground {ground.X}..{ground.Right}");
        Assert.Equal(ground.Y, veil.Y);
        Assert.Equal(ground.Height, veil.Height);
    }

    [Fact]
    public void The_reload_veil_front_travels_from_left_to_right_as_the_reload_comes_on()
    {
        var ground = new HudRect(731, 447, 32, 24);
        var start = HudLayout.ReloadVeil(ground, 0f)!.Value;
        var half = HudLayout.ReloadVeil(ground, 0.5f)!.Value;
        var nearlyDone = HudLayout.ReloadVeil(ground, 0.9f)!.Value;

        // The left edge (the light front) moves right; the right end stays at the rim.
        Assert.Equal(ground.X, start.X);
        Assert.True(start.X < half.X && half.X < nearlyDone.X, $"front at {start.X}, {half.X}, {nearlyDone.X}");
        Assert.All([start, half, nearlyDone], veil => Assert.Equal(ground.Right, veil.Right));
        Assert.Equal(ground.Width / 2, half.Width);
        Assert.Null(HudLayout.ReloadVeil(ground, 1f));
    }

    [Fact]
    public void The_reload_veil_width_is_rounded_not_cut_off()
    {
        // 32 * 0.8 = 25.6: rounded 26, cut off 25. The example must tell the two apart.
        var ground = new HudRect(731, 447, 32, 24);
        var exact = ground.Width * 0.8f;
        Assert.NotEqual((int)exact, (int)MathF.Round(exact));

        var veil = HudLayout.ReloadVeil(ground, 0.2f);

        Assert.Equal((int)MathF.Round(exact), veil!.Value.Width);
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

    /// <summary>The large texts standing over the weapon row (issue #388): the rounds number, or none.</summary>
    private static List<HudPlacedText> LargeOverWeapons(HudPicture picture)
    {
        var frames = picture.Parts.Where(part => part.Name.StartsWith("frame_weapon", StringComparison.Ordinal)).Select(Layout.Bounds).ToList();
        var (left, right, top) = (frames.Min(frame => frame.X), frames.Max(frame => frame.Right), frames.Min(frame => frame.Y));
        return picture.Texts.Where(text => text.Scale == PixelFont.NumberScale && Layout.Bounds(text) is var rect
            && rect.X >= left && rect.Right <= right && rect.Bottom <= top && rect.Y > top / 2).ToList();
    }
}
