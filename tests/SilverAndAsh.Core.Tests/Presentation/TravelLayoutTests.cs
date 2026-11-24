using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>The travel window (issue #480, design A of #477): where it draws what, and where the selection may rest.</summary>
public class TravelLayoutTests
{
    private static readonly PixelFont Font =
        PixelFont.Parse(File.ReadAllText(RepositoryFiles.Find("assets/fonts/kapitell.json")));

    private static readonly TravelLayout Layout = new(Font.Width, Font.CapHeight);

    // Three teleporters in a hall; the records below give them whatever state a test needs.
    private static readonly IReadOnlyList<Teleporter> Three = new GameWorld(
        LevelParser.Parse("#####\n#.P.#\n#...#\n#...#\n#...#\n#...#\n#####"),
        1,
        Balance.Default,
        teleporters: [new(2, 1, 0, TeleporterSite.Start), new(2, 4, 0, TeleporterSite.BossGate), new(1, 5, 0, TeleporterSite.Start)]).Teleporters;

    private static List<TravelChoice> Choices(params TravelState[] states) =>
        states.Select((state, i) => new TravelChoice(Three[i], state)).ToList();

    public static TheoryData<int, int> Windows => new() { { 854, 480 }, { 1920, 1080 }, { 1024, 768 }, { 3840, 1080 } };

    [Fact]
    public void The_selection_never_rests_on_the_own_teleporter_or_a_sealed_one()
    {
        var choices = Choices(TravelState.Here, TravelState.Ready, TravelState.Sealed);

        Assert.Equal(1, TravelCursor.First(choices));
        Assert.Equal(1, TravelCursor.Step(choices, 1, +1));
        Assert.Equal(1, TravelCursor.Step(choices, 1, -1));
        Assert.Equal(1, TravelCursor.Keep(choices, 2));
        Assert.Equal(1, TravelCursor.Click(choices, 1, 0));
        Assert.Equal(1, TravelCursor.Click(choices, 1, 2));
    }

    [Fact]
    public void The_arrows_step_round_through_the_free_destinations()
    {
        var choices = Choices(TravelState.Ready, TravelState.Here, TravelState.Ready);

        Assert.Equal(2, TravelCursor.Step(choices, 0, +1));
        Assert.Equal(0, TravelCursor.Step(choices, 2, +1));
        Assert.Equal(2, TravelCursor.Step(choices, 0, -1));
        Assert.Equal(2, TravelCursor.Click(choices, 0, 2));
        Assert.Equal(2, TravelCursor.Keep(choices, 2));
    }

    [Fact]
    public void Without_a_free_destination_nothing_is_selected()
    {
        var choices = Choices(TravelState.Here, TravelState.Sealed);

        Assert.Null(TravelCursor.First(choices));
        Assert.Null(TravelCursor.Step(choices, null, +1));
        Assert.Null(TravelCursor.Keep(choices, 1));
    }

    [Fact]
    public void A_room_sealed_while_the_window_is_open_moves_the_selection_away()
    {
        Assert.Equal(1, TravelCursor.Keep(Choices(TravelState.Here, TravelState.Ready, TravelState.Ready), 1));

        // The same destination, now sealed: the selection goes to the next free one.
        Assert.Equal(2, TravelCursor.Keep(Choices(TravelState.Here, TravelState.Sealed, TravelState.Ready), 1));
    }

    [Theory]
    [InlineData(TravelState.Here, 0, TravelMark.Here)]
    [InlineData(TravelState.Ready, null, TravelMark.Ready)]
    [InlineData(TravelState.Ready, 0, TravelMark.Chosen)]
    [InlineData(TravelState.Sealed, 0, TravelMark.Sealed)]
    public void The_mark_shows_the_state_from_core(TravelState state, int? selected, TravelMark mark)
    {
        Assert.Equal(mark, TravelLayout.MarkOf(new TravelChoice(Three[0], state), selected));
    }

    [Fact]
    public void A_destination_is_named_after_its_place_unless_its_teleporter_has_a_name_of_its_own()
    {
        // Issue #722: the test course names its teleporters after their stations; a generated level gives no name.
        var named = new GameWorld(
            LevelParser.Parse("#####\n#.P.#\n#...#\n#####"),
            1,
            Balance.Default,
            teleporters: [new(1, 2, 0, TeleporterSite.BossGate) { Name = "5 - Teleporter" }]).Teleporters[0];

        Assert.Equal(TeleporterLook.PlaceName(TeleporterSite.BossGate), TravelLayout.Name(new TravelChoice(Three[1], TravelState.Ready)));
        Assert.Equal("5 - Teleporter", TravelLayout.Name(new TravelChoice(named, TravelState.Ready)));
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public void Every_mark_lies_in_the_map_and_the_mouse_finds_it(int windowWidth, int windowHeight)
    {
        var canvas = MenuCanvas.For(windowWidth, windowHeight);
        var page = Layout.Page(canvas, Choices(TravelState.Here, TravelState.Ready, TravelState.Sealed), 1, Rooms());

        Assert.Equal(3, page.Spots.Count);
        foreach (var spot in page.Spots)
        {
            Assert.True(page.MapArea.Contains(spot.Centre), $"mark {spot.Choice.Teleporter.Id} at {spot.Centre} outside the map");
            Assert.Equal(spot.Choice.Teleporter.Id, TravelLayout.SpotAt(page, spot.Centre));
        }

        Assert.Null(TravelLayout.SpotAt(page, new Vector2(page.MapArea.X + 1, page.MapArea.Y + 1)));
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public void The_window_stays_on_the_canvas_and_its_texts_in_the_panel(int windowWidth, int windowHeight)
    {
        var canvas = MenuCanvas.For(windowWidth, windowHeight);
        var page = Layout.Page(canvas, Choices(TravelState.Here, TravelState.Ready, TravelState.Sealed), 1, Rooms());
        var panel = page.Frame.Panels.Single().Rect;

        Assert.True(panel.X >= 0 && panel.Right <= canvas.Width && panel.Y >= 0 && panel.Bottom <= canvas.Height, $"panel {panel} off the canvas");
        Assert.All(page.Frame.Texts, text =>
        {
            Assert.True(text.X >= panel.X && text.X + Font.Width(text.Text, text.Scale) <= panel.Right, $"'{text.Text}' sticks out");
            Assert.True(text.Top >= panel.Y && text.Top + (Font.CapHeight * text.Scale) <= panel.Bottom, $"'{text.Text}' sticks out");
        });
        Assert.All(page.Spots, spot => Assert.True(spot.Name.X + Font.Width(spot.Name.Text, 1) <= panel.Right, $"'{spot.Name.Text}' sticks out"));
    }

    [Fact]
    public void Neither_a_locked_entry_nor_a_count_is_shown()
    {
        var page = Layout.Page(MenuCanvas.For(854, 480), Choices(TravelState.Here, TravelState.Ready), 1, Rooms());
        var texts = page.Frame.Texts.Select(text => text.Text).Concat(page.Spots.Select(spot => spot.Name.Text)).ToList();

        Assert.DoesNotContain(texts, text => text.Contains("Locked", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(texts, text => text.Contains(" of ", StringComparison.Ordinal) || text.Contains("found", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(TravelLayout.Title, texts);
    }

    [Fact]
    public void Only_a_free_chosen_destination_gets_the_bright_button()
    {
        var canvas = MenuCanvas.For(854, 480);

        var free = Layout.Page(canvas, Choices(TravelState.Here, TravelState.Ready), 1, Rooms());
        Assert.Single(free.Frame.Cursors);
        Assert.Equal(MenuTextColor.Selected, free.Frame.Texts.Single(text => text.Text == TravelLayout.ButtonText).Color);
        Assert.Contains(free.Frame.Texts, text => text.Text == "Boss Gate" && text.Color == MenuTextColor.Selected);

        var sealedOnly = Layout.Page(canvas, Choices(TravelState.Here, TravelState.Sealed), null, Rooms());
        Assert.Empty(sealedOnly.Frame.Cursors);
        Assert.Equal(MenuTextColor.Quiet, sealedOnly.Frame.Texts.Single(text => text.Text == TravelLayout.ButtonText).Color);
    }

    [Fact]
    public void A_small_level_is_not_blown_up_and_sits_in_the_middle_of_the_map()
    {
        var area = new MenuRect(100, 100, 300, 200);
        var map = TravelLayout.Fit([(0, 0), (1, 0)], area);

        Assert.Equal(TravelLayout.MaxCellSize, map.Size);
        var middle = (map.CellCentre(0, 0) + map.CellCentre(1, 0)) / 2f;
        Assert.Equal(area.CentreX, middle.X, 1f);
        Assert.Equal(area.CentreY, map.CellCentre(0, 0).Y, 1f);
    }

    [Fact]
    public void A_large_level_shrinks_to_fit_the_map()
    {
        var area = new MenuRect(0, 0, 300, 200);
        var map = TravelLayout.Fit([(0, 0), (98, 40)], area);

        Assert.InRange(map.ToCanvas(map.MinX, map.MinZ).X, area.X, area.Right);
        Assert.InRange(map.ToCanvas(98 + 2, 40 + 2).X, area.X, area.Right);
        Assert.InRange(map.ToCanvas(98 + 2, 40 + 2).Y, area.Y, area.Bottom);
    }

    // The cells of the hall.
    private static List<(int X, int Z)> Rooms() =>
        [.. Enumerable.Range(1, 5).SelectMany(z => Enumerable.Range(1, 3).Select(x => (x, z)))];
}
