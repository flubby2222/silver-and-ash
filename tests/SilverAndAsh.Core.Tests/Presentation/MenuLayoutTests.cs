using System.Buffers.Binary;
using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Where the menus draw what (issue #371): the approved design "C2 Slim" (854 x 480), kept whole and in the middle on
/// every window shape, built from the parts of <c>assets/menu/menu_parts.png</c>.
/// </summary>
public class MenuLayoutTests
{
    private static readonly MenuParts Parts =
        MenuParts.Parse(File.ReadAllText(RepositoryFiles.Find("assets/menu/menu_parts.json")));

    private static readonly PixelFont Font =
        PixelFont.Parse(File.ReadAllText(RepositoryFiles.Find("assets/fonts/kapitell.json")));

    private static readonly HudLayout HudLayout = new(
        HudParts.Parse(File.ReadAllText(RepositoryFiles.Find("assets/hud/hud_parts.json"))), Font);

    private static readonly MenuLayout Layout = MenuLayout.For(Font, HudLayout);

    /// <summary>16:9 (the photo window), 16:10, 21:9, 32:9, 4:3, 5:4, a large 16:9 and one dragged upright.</summary>
    public static TheoryData<int, int> Windows => new()
    {
        { 854, 480 }, { 1280, 800 }, { 2240, 960 }, { 3840, 1080 }, { 1024, 768 }, { 1280, 1024 }, { 1920, 1080 }, { 600, 900 },
    };

    private static IEnumerable<WeaponKind> AllWeapons => Enum.GetValues<WeaponKind>();

    // ---------------------------------------------------------------- the parts

    [Fact]
    public void The_table_matches_the_parts_picture()
    {
        var png = File.ReadAllBytes(RepositoryFiles.Find("assets/menu/" + Parts.Image));
        var width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4));
        var atlas = new MenuRect(0, 0, width, height);

        Assert.Equal(11, Parts.All.Count);
        foreach (var part in Parts.All)
        {
            Assert.True(atlas.Encloses(part.Source), $"{part.Name} lies outside the picture");
            Assert.All(Parts.All.Where(other => other.Name != part.Name),
                other => Assert.False(part.Source.Overlaps(other.Source), $"{part.Name} overlaps {other.Name}"));
        }
    }

    [Fact]
    public void The_layout_uses_the_shapes_the_parts_were_drawn_for()
    {
        Assert.Equal(MenuLayout.TabShapes, Parts.TabShapes);
        Assert.Equal(MenuLayout.SliderLeft, Parts.SliderLeft);
        Assert.Equal(MenuLayout.SliderRight, Parts.SliderRight);
        Assert.Equal(MenuLayout.CursorHeight, Parts.CursorShape.Height);
        Assert.Equal(Parts["slider_empty"].Width, Parts["slider_full"].Width);
        Assert.Equal(Parts["slider_empty"].Height, Parts["slider_full"].Height);
    }

    [Theory]
    [InlineData(307, 204, 240, 196)]
    [InlineData(200, 88, 454, 336)]
    [InlineData(40, 62, 774, 390)]
    [InlineData(12.5f, 30, 17, 17)]
    public void A_stretched_panel_covers_its_place_exactly_and_keeps_its_corners(float x, float y, float width, float height)
    {
        var panel = new MenuRect(x, y, width, height);
        var pieces = Parts.Panel(panel);
        var part = Parts["panel"];
        var target = new MenuRect(
            x + (part.LayoutX - Parts.PanelShape.X), y + (part.LayoutY - Parts.PanelShape.Y),
            part.Width + (width - Parts.PanelShape.Width), part.Height + (height - Parts.PanelShape.Height));

        Assert.Equal(9, pieces.Count);
        Assert.Equal(target.Width * target.Height, pieces.Sum(piece => piece.Target.Width * piece.Target.Height), 2);
        Assert.All(pieces, piece => Assert.True(target.Encloses(piece.Target), $"{piece.Target} sticks out of {target}"));
        Assert.All(pieces, piece => Assert.True(part.Source.Encloses(piece.Source), $"{piece.Source} is not part of the panel"));
        for (var i = 0; i < pieces.Count; i++)
        {
            for (var j = i + 1; j < pieces.Count; j++)
            {
                Assert.False(pieces[i].Target.Overlaps(pieces[j].Target), $"pieces {i} and {j} overlap");
            }
        }

        foreach (var corner in new[] { pieces[0], pieces[2], pieces[6], pieces[8] })
        {
            Assert.Equal(corner.Source.Width, corner.Target.Width);
            Assert.Equal(corner.Source.Height, corner.Target.Height);
        }
    }

    [Fact]
    public void The_panel_at_its_own_size_is_drawn_one_to_one()
    {
        var pieces = Parts.Panel(Parts.PanelShape);

        Assert.All(pieces, piece => Assert.Equal(piece.Source with { X = 0, Y = 0 }, piece.Target with { X = 0, Y = 0 }));
        Assert.Equal(new Vector2(Parts["panel"].LayoutX, Parts["panel"].LayoutY), new Vector2(pieces[0].Target.X, pieces[0].Target.Y));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_stretched_cursor_keeps_its_points(bool small)
    {
        var row = new MenuRect(214, 108, 426, small ? 30 : 36);
        var pieces = Parts.Cursor(row, small);
        var part = Parts[small ? "cursor_small" : "cursor"];

        Assert.Equal(3, pieces.Count);
        Assert.Equal(pieces[0].Source.Width, pieces[0].Target.Width);
        Assert.Equal(pieces[2].Source.Width, pieces[2].Target.Width);
        Assert.Equal(pieces[0].Target.Right, pieces[1].Target.X);
        Assert.Equal(pieces[1].Target.Right, pieces[2].Target.X);
        Assert.Equal(part.Width + (row.Width - (small ? Parts.CursorSmallShape : Parts.CursorShape).Width),
            pieces.Sum(piece => piece.Target.Width), 3);
    }

    [Fact]
    public void A_panel_too_small_for_its_corners_is_refused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Parts.Panel(new MenuRect(0, 0, 4, 4)));

    [Fact]
    public void The_gable_sits_over_the_middle_of_the_panel_as_in_the_design()
    {
        var gable = Parts.Gable(new MenuRect(307, 204, 240, 196));

        Assert.Equal(Parts["gable"].LayoutX, gable.Target.X);
        Assert.Equal(Parts["gable"].LayoutY, gable.Target.Y);
        Assert.Equal(427f, gable.Target.CentreX, 0);
    }

    [Theory]
    [InlineData(208)] // the question before quitting
    [InlineData(454)] // the settings
    public void The_gable_sits_over_the_middle_of_a_panel_of_any_width(int width)
    {
        var panel = new MenuRect(100, 150, width, 190);

        Assert.Equal(panel.CentreX, Parts.Gable(panel).Target.CentreX, 0);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void A_slider_is_filled_up_to_its_value_and_the_knob_sits_on_it(float fraction)
    {
        var slider = Parts.Slider(420, 138, fraction);
        var edge = 420 + (160 * fraction);

        Assert.Equal(edge, slider.EdgeX, 3);
        Assert.Equal(MathF.Round(edge) + 0.5f, slider.Knob.Target.CentreX, 3);
        if (fraction == 0f)
        {
            Assert.Null(slider.Full);
        }
        else
        {
            var full = slider.Full!.Value;
            Assert.Equal(full.Source.Width, full.Target.Width);
            Assert.Equal(fraction == 1f ? Parts["slider_full"].Width : MathF.Round(edge) - full.Target.X, full.Target.Width, 3);
        }
    }

    [Fact]
    public void The_knob_at_half_sits_where_the_design_drew_it()
    {
        var knob = Parts.Slider(420, 138, 0.5f).Knob;

        Assert.Equal(new MenuRect(Parts["knob"].LayoutX, Parts["knob"].LayoutY, Parts["knob"].Width, Parts["knob"].Height), knob.Target);
    }

    // ---------------------------------------------------------------- the canvas

    [Theory]
    [InlineData(854, 480, 854, 480, 0, 0, 1f)]
    [InlineData(1920, 1080, 1920 / 2.25f, 480, 0, 0, 2.25f)]
    [InlineData(1280, 800, 768, 480, -43, 0, 1280 / 768f)]
    [InlineData(1280, 1024, 768, 614, -43, 67, 1280 / 768f)]
    [InlineData(2240, 960, 1120, 480, 133, 0, 2f)]
    public void The_canvas_follows_the_picture_and_keeps_the_design_in_its_middle(
        int windowWidth, int windowHeight, float width, int height, int offsetX, int offsetY, float scale)
    {
        var canvas = MenuCanvas.For(windowWidth, windowHeight);

        Assert.Equal(MathF.Round(width / 2f) * 2f, canvas.Width);
        Assert.Equal(height, canvas.Height);
        Assert.Equal((canvas.Width - 854) / 2, canvas.OffsetX);
        Assert.Equal(offsetY, canvas.OffsetY);
        Assert.Equal(scale, canvas.Scale, 2);
        if (canvas.Width == 768)
        {
            Assert.Equal(offsetX, canvas.OffsetX);
        }
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public void A_window_point_turns_into_the_canvas_point_under_it(int windowWidth, int windowHeight)
    {
        var canvas = MenuCanvas.For(windowWidth, windowHeight);
        var point = new Vector2(123, 45);
        var window = new Vector2(canvas.Screen.X + (point.X * canvas.Scale), canvas.ToWindowY(point.Y));

        var back = canvas.ToCanvas(window);

        Assert.Equal(point.X, back.X, 2);
        Assert.Equal(point.Y, back.Y, 2);
    }

    [Fact]
    public void A_minimised_window_points_at_nothing()
    {
        var canvas = MenuCanvas.For(0, 0);
        var point = canvas.ToCanvas(new Vector2(10, 10));

        Assert.True(float.IsNaN(point.X) && float.IsNaN(point.Y));
        Assert.DoesNotContain(Layout.Title(canvas, 0, "1.0").Rows, row => row.Contains(point));
    }

    // ---------------------------------------------------------------- the screens

    public static TheoryData<string, int, int> ScreensAndWindows
    {
        get
        {
            var data = new TheoryData<string, int, int>();
            foreach (var screen in new[] { "title", "pause", "quit", "settings", "settings-full", "map", "equipment" })
            {
                foreach (var (width, height) in new[] { (854, 480), (1280, 800), (2240, 960), (3840, 1080), (1280, 1024), (600, 900) })
                {
                    data.Add(screen, width, height);
                }
            }

            return data;
        }
    }

    private static MenuPicture Screen(string name, MenuCanvas canvas, int selected = 0) => name switch
    {
        "title" => Layout.Title(canvas, selected, "0.9.0-dev+4f2a1c3"),
        "pause" => Layout.Pause(canvas, selected),
        "quit" => Layout.Quit(canvas, selected),
        "settings" => Layout.Settings(canvas, selected, new GameSettings()),
        "settings-full" => Layout.Settings(canvas, selected,
            new GameSettings { FullScreen = true, MouseSensitivity = GameSettings.MaxMouseSensitivity, MasterVolume = 1f }),
        "map" => Layout.Map(canvas).Frame,
        _ => Layout.Equipment(canvas, Player.InventorySlotCount).Frame,
    };

    [Theory]
    [MemberData(nameof(ScreensAndWindows))]
    public void Everything_of_a_screen_lies_on_the_canvas(string screen, int windowWidth, int windowHeight)
    {
        var canvas = MenuCanvas.For(windowWidth, windowHeight);
        var picture = Screen(screen, canvas);

        Assert.All(picture.Panels, panel => Assert.True(canvas.Bounds.Encloses(Outline(panel)), $"panel {panel} leaves the canvas"));
        Assert.All(picture.Texts, text => Assert.True(canvas.Bounds.Encloses(Layout.Bounds(text)), $"'{text.Text}' leaves the canvas"));
        Assert.All(picture.Tabs, tab => Assert.True(canvas.Bounds.Encloses(Parts.Tab(tab.Index, tab.Open, tab.Left, tab.Top).Target),
            $"tab {tab.Index} leaves the canvas"));
    }

    [Theory]
    [MemberData(nameof(ScreensAndWindows))]
    public void Entries_lie_in_their_panel_one_under_the_other(string screen, int windowWidth, int windowHeight)
    {
        var canvas = MenuCanvas.For(windowWidth, windowHeight);
        var picture = Screen(screen, canvas);

        foreach (var row in picture.Rows)
        {
            Assert.Contains(picture.Panels, panel => panel.Rect.Encloses(row));
        }

        for (var i = 1; i < picture.Rows.Count; i++)
        {
            Assert.True(picture.Rows[i - 1].Bottom <= picture.Rows[i].Y, $"rows {i - 1} and {i} overlap");
        }
    }

    [Theory]
    [MemberData(nameof(ScreensAndWindows))]
    public void Entries_keep_air_between_them(string screen, int windowWidth, int windowHeight)
    {
        var picture = Screen(screen, MenuCanvas.For(windowWidth, windowHeight));

        for (var i = 1; i < picture.Rows.Count; i++)
        {
            Assert.True(picture.Rows[i].Y - picture.Rows[i - 1].Bottom >= MinRowGap,
                $"rows {i - 1} and {i} are only {picture.Rows[i].Y - picture.Rows[i - 1].Bottom} px apart");
        }
    }

    [Theory]
    [MemberData(nameof(ScreensAndWindows))]
    public void A_screen_keeps_to_the_middle_of_the_canvas_as_in_the_design(string screen, int windowWidth, int windowHeight)
    {
        // The design is 854 x 480; on any other canvas the whole screen moves with it into the middle - on a tall
        // canvas (5:4, a window dragged upright) down by OffsetY, on a wide one sideways.
        var design = MenuCanvas.For(MenuCanvas.DesignWidth, MenuCanvas.DesignHeight);
        var canvas = MenuCanvas.For(windowWidth, windowHeight);
        var expected = Hull(screen, design);
        var actual = Hull(screen, canvas);

        var shiftX = (actual.CentreX - (canvas.Width / 2f)) - (expected.CentreX - (design.Width / 2f));
        var shiftY = (actual.CentreY - canvas.OffsetY) - (expected.CentreY - design.OffsetY);
        Assert.True(MathF.Abs(shiftY) <= 1.5f, $"{screen} sits {shiftY} px off the middle upright ({actual} on {canvas.Width} x {canvas.Height})");
        if (screen is "map" or "equipment")
        {
            // Since issue #390 both pages leave the frame of values free on the left (it keeps to the HUD's top left
            // corner): sideways they fill the room right of it - tested in The_pages_stand_right_of_the_frame_of_values.
            return;
        }

        Assert.True(MathF.Abs(shiftX) <= 1.5f, $"{screen} sits {shiftX} px off the middle sideways ({actual} on {canvas.Width} x {canvas.Height})");
    }

    [Theory]
    [MemberData(nameof(ScreensAndWindows))]
    public void No_two_texts_run_into_each_other(string screen, int windowWidth, int windowHeight)
    {
        var picture = Screen(screen, MenuCanvas.For(windowWidth, windowHeight));
        var bounds = picture.Texts.Select(Layout.Bounds).ToArray();

        for (var i = 0; i < bounds.Length; i++)
        {
            for (var j = i + 1; j < bounds.Length; j++)
            {
                Assert.False(bounds[i].Overlaps(bounds[j]), $"'{picture.Texts[i].Text}' runs into '{picture.Texts[j].Text}'");
            }
        }
    }

    [Theory]
    [InlineData("title", 3)]
    [InlineData("pause", 4)]
    [InlineData("quit", 2)]
    [InlineData("settings", 6)]
    public void Only_the_selected_entry_is_gold_and_in_the_cursor(string screen, int rows)
    {
        var canvas = MenuCanvas.For(854, 480);
        for (var selected = 0; selected < rows; selected++)
        {
            var picture = Screen(screen, canvas, selected);
            var row = picture.Rows[selected];

            Assert.Equal(rows, picture.Rows.Count);
            Assert.Contains(picture.Cursors, cursor => !cursor.Small && cursor.Rect == row);
            Assert.Single(picture.Cursors, cursor => !cursor.Small);
            var inRows = picture.Texts.Where(text => picture.Rows.Any(other => other.Encloses(Layout.Bounds(text)))).ToArray();
            var label = inRows.Where(text => row.Encloses(Layout.Bounds(text))).MinBy(text => text.X)!;
            Assert.Equal(MenuTextColor.Selected, label.Color);

            // Elsewhere only the display in use is gold (it has its own small cursor).
            Assert.All(inRows.Where(text => text.Color == MenuTextColor.Selected && !row.Encloses(Layout.Bounds(text))),
                text => Assert.Equal("Window", text.Text));
            Assert.All(picture.Rows.Where(other => other != row), other => Assert.DoesNotContain(inRows,
                text => text.Text != "Window" && text.Color == MenuTextColor.Selected && other.Encloses(Layout.Bounds(text))));
        }
    }

    [Fact]
    public void The_title_column_matches_the_design()
    {
        var picture = Layout.Title(MenuCanvas.For(854, 480), 0, "1.0");

        Assert.Equal(new MenuRect(307, 204, 240, 196), picture.Panels.Single().Rect);
        Assert.Equal([new MenuRect(323, 222, 208, 36), new MenuRect(323, 284, 208, 36), new MenuRect(323, 346, 208, 36)], picture.Rows);
        var start = picture.Texts.Single(text => text.Text == "Start");
        Assert.Equal(233, start.Top);
        Assert.Equal(2, start.Scale);
    }

    [Fact]
    public void The_title_screen_shows_only_the_name_the_entries_and_the_version()
    {
        // Issue #432: the line "a hunt in the catacombs" under the name is gone.
        var picture = Layout.Title(MenuCanvas.For(854, 480), 0, "1.0");

        Assert.Equal(["Quit", "Settings", "Silver & Ash", "Start", "v1.0"],
            picture.Texts.Select(text => text.Text).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_settings_label_ends_before_its_slider_and_every_value_after_it()
    {
        var canvas = MenuCanvas.For(854, 480);
        var settings = new GameSettings { MouseSensitivity = GameSettings.MaxMouseSensitivity, MasterVolume = 1f, EffectsVolume = 1f, AmbienceVolume = 1f };
        var picture = Layout.Settings(canvas, 0, settings);

        Assert.Equal(4, picture.Sliders.Count);
        foreach (var slider in picture.Sliders)
        {
            var row = picture.Rows.Single(r => r.Y <= slider.CentreY && slider.CentreY < r.Bottom);
            var texts = picture.Texts.Where(text => row.Encloses(Layout.Bounds(text))).Select(Layout.Bounds).ToArray();
            Assert.Equal(2, texts.Length);
            Assert.True(texts[0].Right + 8 <= slider.Left, "label runs into the slider");
            var knobAtFull = Parts.Slider(slider.Left, slider.CentreY, 1f).Knob.Target;
            Assert.True(texts[1].X >= knobAtFull.Right + 4, "value runs into the knob at full value");
            // The cursor narrows to its point over the last 18 px of the row (as in the design: values end at 622, the row at 640).
            Assert.True(texts[1].Right <= row.Right - 17, "value runs into the cursor's point");
        }
    }

    [Theory]
    [InlineData(false, "Window")]
    [InlineData(true, "Full screen")]
    public void The_small_cursor_frames_the_display_in_use(bool fullScreen, string inUse)
    {
        var picture = Layout.Settings(MenuCanvas.For(854, 480), 0, new GameSettings { FullScreen = fullScreen });
        var small = Assert.Single(picture.Cursors, cursor => cursor.Small);
        var text = picture.Texts.Single(t => t.Text == inUse);

        Assert.True(small.Rect.Encloses(Layout.Bounds(text)));
        Assert.Equal(MenuTextColor.Selected, text.Color);
        Assert.False(small.Rect.Overlaps(Layout.Bounds(picture.Texts.Single(t => t.Text == (fullScreen ? "Window" : "Full screen")))));
    }

    [Theory]
    [InlineData(0.2f, 0f)]
    [InlineData(1f, 0.8f / 2.8f)]
    [InlineData(3f, 1f)]
    public void The_sensitivity_slider_runs_from_the_lowest_to_the_highest_value(float sensitivity, float fraction)
    {
        var picture = Layout.Settings(MenuCanvas.For(854, 480), 0, new GameSettings { MouseSensitivity = sensitivity });

        Assert.Equal(fraction, picture.Sliders[0].Fraction, 3);
    }

    // ---------------------------------------------------------------- tabs and the equipment page

    [Theory]
    [MemberData(nameof(Windows))]
    public void Tab_labels_fit_their_tabs(int windowWidth, int windowHeight)
    {
        var canvas = MenuCanvas.For(windowWidth, windowHeight);
        foreach (var picture in new[] { Layout.Map(canvas).Frame, Layout.Equipment(canvas, 0).Frame })
        {
            Assert.Equal(2, picture.Tabs.Count);
            foreach (var tab in picture.Tabs)
            {
                var outline = MenuLayout.TabOutline(tab.Index, tab.Open, tab.Left, tab.Top);
                var label = picture.Texts.Single(text => text.Text == MenuLayout.TabLabels[tab.Index]);
                var bounds = Layout.Bounds(label);
                Assert.True(bounds.X >= outline.X + 12 && bounds.Right <= outline.Right - 12, $"'{label.Text}' is too wide for its tab");
                Assert.True(outline.Encloses(bounds), $"'{label.Text}' leaves its tab");
                Assert.Equal(tab.Open ? MenuTextColor.Selected : MenuTextColor.Quiet, label.Color);
            }

            Assert.Single(picture.Tabs, tab => tab.Open);
        }
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public void The_tabs_stand_at_the_same_place_on_both_pages(int windowWidth, int windowHeight)
    {
        // Issue #390: turning the page must not move the tabs - each tab covers the same rectangle on both pages, and the
        // mouse clicks it there.
        var canvas = MenuCanvas.For(windowWidth, windowHeight);
        var map = Layout.Map(canvas).Frame.Tabs;
        var equipment = Layout.Equipment(canvas, 0).Frame.Tabs;

        Assert.Equal(0, map.Single(tab => tab.Open).Index);
        Assert.Equal(1, equipment.Single(tab => tab.Open).Index);
        for (var i = 0; i < MenuLayout.TabLabels.Count; i++)
        {
            var onMap = map.Single(tab => tab.Index == i);
            var onEquipment = equipment.Single(tab => tab.Index == i);
            Assert.Equal((onMap.Left, onMap.Top), (onEquipment.Left, onEquipment.Top));
            Assert.Equal(MenuLayout.TabOutline(i, open: true, onMap.Left, onMap.Top), MenuLayout.TabOutline(i, open: true, onEquipment.Left, onEquipment.Top));
            Assert.Equal(MenuLayout.TabTarget(canvas, i), MenuLayout.TabOutline(i, open: true, onMap.Left, onMap.Top));
        }
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public void A_click_on_a_tab_turns_to_its_page(int windowWidth, int windowHeight)
    {
        var canvas = MenuCanvas.For(windowWidth, windowHeight);
        var map = MenuLayout.TabTarget(canvas, 0);
        var equipment = MenuLayout.TabTarget(canvas, 1);

        Assert.Equal(MenuKey.MapTab, MenuLayout.TabAt(canvas, new Vector2(map.CentreX, map.CentreY)));
        Assert.Equal(MenuKey.EquipmentTab, MenuLayout.TabAt(canvas, new Vector2(equipment.CentreX, equipment.CentreY)));
        Assert.Null(MenuLayout.TabAt(canvas, new Vector2(map.CentreX, map.Bottom + 4)));
        Assert.Null(MenuLayout.TabAt(canvas, new Vector2(float.NaN, float.NaN)));
        Assert.False(map.Overlaps(equipment));
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public void The_map_lies_under_the_tabs_and_the_hint(int windowWidth, int windowHeight)
    {
        var canvas = MenuCanvas.For(windowWidth, windowHeight);
        var page = Layout.Map(canvas);
        var hint = page.Frame.Texts.Single(text => text.Text == MenuLayout.MapHint);

        Assert.True(canvas.Bounds.Encloses(page.MapArea), $"{page.MapArea} leaves the canvas");
        Assert.True(page.MapArea.Width > 300 && page.MapArea.Height > 250, $"{page.MapArea} is too small for a map");
        Assert.True(page.MapArea.Y >= Layout.Bounds(hint).Bottom + 4, "the map runs into the hint");
        Assert.All(page.Frame.Tabs, tab => Assert.True(Parts.Tab(tab.Index, tab.Open, tab.Left, tab.Top).Target.Bottom <= Layout.Bounds(hint).Y,
            $"tab {tab.Index} runs into the hint"));
    }

    // ---------------------------------------------------------------- the frame of values (issue #390)

    [Theory]
    [MemberData(nameof(Windows))]
    public void The_frame_of_values_lies_under_the_experience_zone_and_off_the_hud(int windowWidth, int windowHeight)
    {
        // The HUD stays visible behind the menu: the frame of values starts under the level, the experience bar, the
        // waiting boon and the yellow hint to level up under them (top left), and touches no other part of the HUD
        // either - with everything shown at its widest.
        var canvas = MenuCanvas.For(windowWidth, windowHeight);
        var hud = HudLayout.Arrange(BusyHud(), "Picked up Hunter's Eye", new HudCanvas(canvas.Width, canvas.Height));
        var frame = WidestSheet().Frame;
        string[] zone = ["frame_level", "frame_xp", "xp_empty", "xp_full", "frame_skill_0", "frame_skill_1", "icon_skillpoint"];

        var zoneParts = hud.Parts.Where(part => zone.Contains(part.Name)).ToList();
        var hint = Assert.Single(hud.Texts, text => text.Text == HudLayout.LevelUpHint);
        Assert.Contains(zoneParts, part => part.Name == "icon_skillpoint");
        Assert.All(zoneParts, part => Assert.True(frame.Y >= HudLayout.Bounds(part).Y + HudLayout.Bounds(part).Height,
            $"the frame of values starts above the end of {part.Name}"));
        Assert.True(frame.Y >= HudLayout.Bounds(hint).Y + HudLayout.Bounds(hint).Height + HudLayout.Spacing,
            $"the frame of values starts less than the spacing under '{hint.Text}'");
        Assert.All(hud.Parts, part => Assert.False(frame.Overlaps(ToMenu(HudLayout.Bounds(part))), $"the frame of values covers {part.Name}"));
        Assert.All(hud.Texts, text => Assert.False(frame.Overlaps(ToMenu(HudLayout.Bounds(text))), $"the frame of values covers '{text.Text}'"));
    }

    [Fact]
    public void The_frame_of_values_holds_the_widest_lines_and_its_texts_keep_apart()
    {
        foreach (var sheet in new[] { WidestSheet(), Layout.Sheet(SheetLines(8), []) })
        {
            var bounds = sheet.Texts.Select(Layout.Bounds).ToArray();
            Assert.All(bounds, text => Assert.True(Inset(sheet.Frame, 2).Encloses(text), $"{text} leaves the frame {sheet.Frame}"));
            for (var i = 0; i < bounds.Length; i++)
            {
                Assert.All(bounds.Skip(i + 1), other => Assert.False(bounds[i].Overlaps(other), $"'{sheet.Texts[i].Text}' runs into another text"));
            }
        }

        Assert.Contains(Layout.Sheet(SheetLines(8), []).Texts, text => text.Text == "No boons yet");
    }

    [Fact]
    public void The_frame_of_values_holds_a_real_players_lines()
    {
        var world = new GameWorld(LevelParser.Parse("P"));
        var sheet = Layout.Sheet(CharacterSheet.Stats(world, world.Player), CharacterSheet.Boons(world.Player, world.Balance.Boons));

        Assert.Equal(MenuLayout.SheetWidth, sheet.Frame.Width);
        Assert.All(sheet.Texts, text => Assert.True(sheet.Frame.Encloses(Layout.Bounds(text)), $"'{text.Text}' leaves the frame"));
        Assert.All(sheet.Texts.Where(text => text.Scale == 1), text => Assert.NotEqual(MenuTextColor.Selected, text.Color));
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public void The_pages_stand_right_of_the_frame_of_values(int windowWidth, int windowHeight)
    {
        var canvas = MenuCanvas.For(windowWidth, windowHeight);
        var frame = WidestSheet().Frame;
        var map = Layout.Map(canvas);
        var equipment = Layout.Equipment(canvas, Player.InventorySlotCount);
        var taken = new List<MenuRect> { map.MapArea, equipment.Frame.Panels.Single().Rect };
        taken.AddRange(map.Frame.Tabs.Concat(equipment.Frame.Tabs).Select(tab => Parts.Tab(tab.Index, tab.Open, tab.Left, tab.Top).Target));
        taken.AddRange(map.Frame.Texts.Concat(equipment.Frame.Texts).Select(Layout.Bounds));

        Assert.All(taken, rect => Assert.True(rect.X >= frame.Right + 4, $"{rect} runs into the frame of values {frame}"));
        Assert.True(canvas.Bounds.Encloses(frame));
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public void The_equipment_page_keeps_its_boxes_items_and_help_inside_the_panel(int windowWidth, int windowHeight)
    {
        var canvas = MenuCanvas.For(windowWidth, windowHeight);
        var page = Layout.Equipment(canvas, 3);
        var panel = page.Frame.Panels.Single().Rect;
        var boxes = page.Hands.Concat(page.Bag).ToArray();

        Assert.Equal(1 + Player.RangedSlotCount, page.Hands.Count);
        Assert.Equal(Player.InventorySlotCount, page.Bag.Count);
        Assert.All(boxes, box => Assert.True(panel.Encloses(box), $"box {box} leaves the panel"));
        Assert.All(page.Frame.Texts.Where(text => !MenuLayout.TabLabels.Contains(text.Text)),
            text => Assert.True(panel.Encloses(Layout.Bounds(text)), $"'{text.Text}' leaves the panel"));
        Assert.True(panel.Encloses(Layout.Bounds(page.LastWeaponNote)), "the note leaves the panel");
        for (var i = 0; i < boxes.Length; i++)
        {
            Assert.All(boxes.Skip(i + 1), other => Assert.False(boxes[i].Overlaps(other), $"box {i} overlaps another"));
            Assert.All(page.Frame.Texts, text => Assert.False(boxes[i].Overlaps(Layout.Bounds(text)), $"'{text.Text}' covers box {i}"));
        }

        Assert.All(page.Frame.Texts, text => Assert.False(Layout.Bounds(page.LastWeaponNote).Overlaps(Layout.Bounds(text)),
            $"the note runs into '{text.Text}'"));
        Assert.True(panel.Encloses(page.Divider));
        Assert.All(boxes, box => Assert.False(box.Overlaps(page.Divider)));
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public void The_equipment_boxes_and_the_note_keep_air_between_them(int windowWidth, int windowHeight)
    {
        var page = Layout.Equipment(MenuCanvas.For(windowWidth, windowHeight), 3);
        var boxes = page.Hands.Concat(page.Bag).ToArray();
        var note = Layout.Bounds(page.LastWeaponNote);

        for (var i = 0; i < boxes.Length; i++)
        {
            Assert.All(boxes.Skip(i + 1), other => Assert.True(Apart(boxes[i], other, MinBoxGap), $"box {i} touches {other}"));
            Assert.True(Apart(note, boxes[i], MinBoxGap), $"the note {note} runs into box {i} {boxes[i]}");
        }

        Assert.True(note.Y >= page.Bag[^1].Bottom + MinBoxGap, "the note stands above the last row of boxes");
    }

    [Fact]
    public void The_equipment_page_fits_the_narrowest_canvas_that_used_to_cut_it_off()
    {
        // Before issue #371 the page was 932 px wide and was cut off in a window 854 x 480 (and at 16:10).
        foreach (var (width, height) in new[] { (854, 480), (768, 480), (1280, 800) })
        {
            var canvas = MenuCanvas.For(width, height);
            var panel = Layout.Equipment(canvas, 0).Frame.Panels.Single().Rect;

            Assert.True(panel.X >= 12 && panel.Right <= canvas.Width - 12, $"{width} x {height}: {panel}");
        }
    }

    [Fact]
    public void Every_weapon_name_and_kind_fits_its_box()
    {
        var box = Layout.Equipment(MenuCanvas.For(854, 480), 0).Hands[0];
        foreach (var kind in AllWeapons)
        {
            var info = Weapons.Info(kind);
            foreach (var tier in Enum.GetValues<ItemTier>())
            {
                var texts = Layout.Box(box, "LMB", info.Name, $"{info.Hand} - {tier}");

                foreach (var text in new[] { texts.Key, texts.Name, texts.Kind!.Value })
                {
                    Assert.True(box.Encloses(Layout.Bounds(text)), $"'{text.Text}' leaves its box");
                }

                Assert.Equal(2, texts.Name.Scale);
                Assert.False(Layout.Bounds(texts.Key).Overlaps(Layout.Bounds(texts.Name)));
                Assert.False(Layout.Bounds(texts.Name).Overlaps(Layout.Bounds(texts.Kind!.Value)));
            }
        }
    }

    [Fact]
    public void A_name_too_long_for_the_box_is_written_smaller()
    {
        var box = new MenuRect(0, 0, MenuLayout.BoxWidth, MenuLayout.BoxHeight);

        var texts = Layout.Box(box, "1", "Consecrated Blunderbuss", "Ranged - Royal");

        Assert.Equal(1, texts.Name.Scale);
        Assert.True(box.Encloses(Layout.Bounds(texts.Name)));
    }

    [Fact]
    public void An_empty_place_shows_a_dash()
    {
        var texts = Layout.Box(new MenuRect(0, 0, MenuLayout.BoxWidth, MenuLayout.BoxHeight), "2", null, null);

        Assert.Equal("-", texts.Name.Text);
        Assert.Null(texts.Kind);
    }

    // Air that must stay between two entries (rows of a menu) and between two boxes of the equipment page.
    private const float MinRowGap = 8f;
    private const float MinBoxGap = 4f;

    // The version on the title screen keeps to the corner of the canvas, not to the middle.
    private const string TitleVersionText = "v0.9.0-dev+4f2a1c3";

    // True if a and b are at least gap apart (side by side or one over the other) - touching is not apart.
    private static bool Apart(MenuRect a, MenuRect b, float gap) =>
        a.Right + gap <= b.X || b.Right + gap <= a.X || a.Bottom + gap <= b.Y || b.Bottom + gap <= a.Y;

    // The HUD with everything shown at its widest (as HudLayoutTests.Busy): waiting boons, a boss, items, the toast.
    private static HudReadout BusyHud() => new(
        IsDead: false,
        Life: 1f,
        Stamina: 1f,
        Level: 99,
        Experience: 0.4f,
        PendingBoons: 2,
        Silver: 99999,
        Keys: 99,
        Weapons:
        [
            new(WeaponKind.Club, null, false, false),
            new(WeaponKind.Rifle, 12, true, false),
            new(WeaponKind.Blunderbuss, 12, false, true),
            new(WeaponKind.Pistol, 12, false, false),
        ],
        Items: Enum.GetValues<ItemKind>().Concat(Enum.GetValues<ItemKind>()).ToList(),
        Boss: new HudBoss(Gorm.Name, 0.5f),
        ConsecratedSecondsLeft: 12.5f);

    // The values at their widest and every boon chosen nine times - the test of room for the frame of values.
    private static ValuesSheet WidestSheet()
    {
        IReadOnlyList<SheetLine> stats =
        [
            new("Level", "99"),
            new("Health", "999 / 999"),
            new("Stamina", "9.9 / 9 bars"),
            new("Stamina regen", "+125 %"),
            new("Melee", "999  Silver Blade"),
            new("Ranged", "99 x 12  Blunderbuss"),
            new("Speed", "99.9 m/s"),
            new("Experience", "199.5 %"),
        ];
        var boons = Enum.GetValues<Boon>()
            .Select(boon => new SheetLine($"{BoonTexts.Name(boon)} x9", $"{BoonTexts.Amount(boon, Balance.Default.Boons, 9)} {BoonTexts.What(boon)}"))
            .ToList();
        return Layout.Sheet(stats, boons);
    }

    private static IReadOnlyList<SheetLine> SheetLines(int count) =>
        Enumerable.Range(0, count).Select(i => new SheetLine($"Line {i}", $"{i}")).ToList();

    private static MenuRect ToMenu(HudRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    private static MenuRect Inset(MenuRect rect, float by) => new(rect.X + by, rect.Y + by, rect.Width - (2 * by), rect.Height - (2 * by));

    // Everything of a screen that keeps to the middle: panels with gable, tabs, rows, texts (not the version) and, on
    // the equipment page, its boxes and note.
    private static MenuRect Hull(string screen, MenuCanvas canvas)
    {
        var picture = Screen(screen, canvas);
        var rects = picture.Panels.Select(Outline)
            .Concat(picture.Tabs.Select(tab => Parts.Tab(tab.Index, tab.Open, tab.Left, tab.Top).Target))
            .Concat(picture.Rows)
            .Concat(picture.Texts.Where(text => text.Text != TitleVersionText).Select(Layout.Bounds))
            .ToList();
        if (screen == "equipment")
        {
            var page = Layout.Equipment(canvas, Player.InventorySlotCount);
            rects.AddRange(page.Hands);
            rects.AddRange(page.Bag);
            rects.Add(Layout.Bounds(page.LastWeaponNote));
        }

        return MenuRect.FromEdges(rects.Min(r => r.X), rects.Min(r => r.Y), rects.Max(r => r.Right), rects.Max(r => r.Bottom));
    }

    // The outline a panel covers with its gable.
    private static MenuRect Outline(MenuPanel panel)
    {
        var rect = panel.Rect;
        if (panel.Gable)
        {
            var gable = Parts.Gable(rect).Target;
            rect = MenuRect.FromEdges(rect.X, MathF.Min(rect.Y, gable.Y), rect.Right, rect.Bottom);
        }

        return rect;
    }
}
