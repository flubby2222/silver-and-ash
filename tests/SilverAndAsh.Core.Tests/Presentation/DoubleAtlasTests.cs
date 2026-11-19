using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Issue #409: HUD and menus draw from the atlases in double resolution when they are there, otherwise from the atlases
/// of today - and every part keeps its place and size on the screen either way.
/// </summary>
public class DoubleAtlasTests
{
    private static readonly string HudFolder = RepositoryFiles.Find("assets/hud");
    private static readonly string MenuFolder = RepositoryFiles.Find("assets/menu");

    private static readonly HudParts Hud = HudParts.Parse(File.ReadAllText(Path.Combine(HudFolder, "hud_parts.json")));
    private static readonly HudParts HudX2 = HudParts.Parse(File.ReadAllText(Path.Combine(HudFolder, "hud_parts_x2.json")));
    private static readonly MenuParts Menu = MenuParts.Parse(File.ReadAllText(Path.Combine(MenuFolder, "menu_parts.json")));
    private static readonly MenuParts MenuX2 = MenuParts.Parse(File.ReadAllText(Path.Combine(MenuFolder, "menu_parts_x2.json")));

    [Theory]
    [InlineData("hud", "hud_parts")]
    [InlineData("menu", "menu_parts")]
    public void The_game_draws_from_the_double_atlas_of_the_repository(string folder, string name)
    {
        var directory = RepositoryFiles.Find("assets/" + folder);

        var file = AtlasChoice.Pick(directory, name, File.Exists);

        Assert.Equal(2, file.Scale);
        Assert.Equal(Path.Combine(directory, name + "_x2.json"), file.Table);
    }

    [Fact]
    public void Without_the_double_picture_or_table_it_falls_back_to_the_atlas_of_today()
    {
        var all = new HashSet<string> { Path.Combine("d", "hud_parts.json"), Path.Combine("d", "hud_parts_x2.json"), Path.Combine("d", "hud_parts_x2.png") };

        Assert.Equal(new AtlasFile(Path.Combine("d", "hud_parts_x2.json"), 2), AtlasChoice.Pick("d", "hud_parts", all.Contains));
        foreach (var missing in new[] { "hud_parts_x2.json", "hud_parts_x2.png" })
        {
            var some = all.Where(path => path != Path.Combine("d", missing)).ToHashSet();
            Assert.Equal(new AtlasFile(Path.Combine("d", "hud_parts.json"), 1), AtlasChoice.Pick("d", "hud_parts", some.Contains));
        }
    }

    [Fact]
    public void Every_hud_part_keeps_its_place_and_size_signs_keep_their_middle()
    {
        var atlas = new HudAtlas(Hud, HudX2, 2);
        foreach (var part in Hud.All)
        {
            var piece = atlas.Piece(new HudPlacedPart(part.Name, part.LayoutX, part.LayoutY, part.Width));
            var fine = HudX2[part.Name];
            Assert.Equal(new MenuRect(fine.X, fine.Y, fine.Width, fine.Height), piece.Source);
            if (part.Name.StartsWith("icon_", StringComparison.Ordinal))
            {
                Assert.Equal(part.LayoutX + (part.Width / 2f), piece.Target.CentreX);
                Assert.Equal(part.LayoutY + (part.Height / 2f), piece.Target.CentreY);
                Assert.True(piece.Target.Width >= part.Width && piece.Target.Height >= part.Height, part.Name);
            }
            else
            {
                Assert.Equal(new MenuRect(part.LayoutX, part.LayoutY, part.Width, part.Height), piece.Target);
            }
        }
    }

    [Fact]
    public void A_cut_bar_takes_the_same_share_of_the_finer_picture()
    {
        var life = Hud["life_full"];

        var piece = new HudAtlas(Hud, HudX2, 2).Piece(new HudPlacedPart("life_full", 100, 200, 51));

        Assert.Equal(102f, piece.Source.Width);
        Assert.Equal(new MenuRect(100, 200, 51, life.Height), piece.Target);
    }

    [Fact]
    public void With_the_atlas_of_today_the_hud_draws_as_before()
    {
        var piece = new HudAtlas(Hud, Hud, 1).Piece(new HudPlacedPart("life_full", 100, 200, 51));
        var life = Hud["life_full"];

        Assert.Equal(new MenuRect(life.X, life.Y, 51, life.Height), piece.Source);
        Assert.Equal(new MenuRect(100, 200, 51, life.Height), piece.Target);
    }

    [Fact]
    public void Menu_pieces_land_where_the_atlas_of_today_puts_them()
    {
        var plain = new MenuAtlas(Menu, 1);
        var fine = new MenuAtlas(MenuX2, 2);
        var panel = new MenuRect(40, 60, 500, 300);
        var cursor = new MenuRect(120, 140, 200, 30);

        Assert.Equal(Targets(plain.Panel(panel)), Targets(fine.Panel(panel)));
        Assert.Equal(Targets(plain.Cursor(cursor)), Targets(fine.Cursor(cursor)));
        Assert.Equal(Targets(plain.Cursor(cursor, small: true)), Targets(fine.Cursor(cursor, small: true)));
        foreach (var (index, open) in new[] { (0, false), (0, true), (1, false), (1, true) })
        {
            Assert.Equal(plain.Tab(index, open, 30, 10).Target, fine.Tab(index, open, 30, 10).Target);
        }

        Assert.Equal(plain.Slider(400, 120, 0.5f).Empty.Target, fine.Slider(400, 120, 0.5f).Empty.Target);
        Assert.Equal(plain.Slider(400, 120, 1f).Full?.Target, fine.Slider(400, 120, 1f).Full?.Target);
        Assert.Equal(Menu.Panel(panel), plain.Panel(panel));
    }

    private static MenuRect[] Targets(IReadOnlyList<MenuPiece> pieces) => pieces.Select(piece => piece.Target).ToArray();
}
