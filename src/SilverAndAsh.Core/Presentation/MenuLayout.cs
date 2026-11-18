using System.Globalization;
using System.Numerics;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Presentation;

/// <summary>Which of the menu colours of <see cref="MenuParts"/> a text is drawn in.</summary>
public enum MenuTextColor
{
    /// <summary>Headings and values.</summary>
    Main,

    /// <summary>Entries that are not selected.</summary>
    Second,

    /// <summary>Gold: only the selection.</summary>
    Selected,

    /// <summary>Hints and closed tabs.</summary>
    Quiet,
}

/// <summary>
/// A text on the menu canvas in the pixel font: left edge, top of the capitals, whole-number enlargement and colour.
/// The game draws a dark shadow one pixel below and right of it.
/// </summary>
public readonly record struct MenuText(string Text, int X, int Top, int Scale, MenuTextColor Color);

/// <summary>A panel (with the gable over its upper edge or without).</summary>
public readonly record struct MenuPanel(MenuRect Rect, bool Gable);

/// <summary>The silver cursor around a selected entry; the small one for a short word in a row.</summary>
public readonly record struct MenuCursor(MenuRect Rect, bool Small);

/// <summary>A slider whose trough starts at <paramref name="Left"/>, filled to <paramref name="Fraction"/> (0 to 1).</summary>
public readonly record struct MenuSlider(float Left, float CentreY, float Fraction);

/// <summary>Tab <paramref name="Index"/> (0 map, 1 equipment) of a row of tabs starting at <paramref name="Left"/>, <paramref name="Top"/>.</summary>
public readonly record struct MenuTab(int Index, bool Open, float Left, float Top);

/// <summary>
/// One menu screen in drawing order: panels, cursors, sliders, tabs, texts. <paramref name="Rows"/> are the entries the
/// mouse can point at, in the order of the menu.
/// </summary>
public sealed record MenuPicture(
    IReadOnlyList<MenuPanel> Panels,
    IReadOnlyList<MenuCursor> Cursors,
    IReadOnlyList<MenuSlider> Sliders,
    IReadOnlyList<MenuTab> Tabs,
    IReadOnlyList<MenuText> Texts,
    IReadOnlyList<MenuRect> Rows);

/// <summary>The texts in one box of the equipment page: key, name (null for an empty place, which shows "-") and kind.</summary>
public readonly record struct EquipmentBoxTexts(MenuText Key, MenuText Name, MenuText? Kind);

/// <summary>
/// The equipment page: its frame (panel, tabs, headings, help), the boxes of the hands (melee, then the three ranged
/// places) and of the bag, the line between boxes and items, and where the note on the last ranged weapon goes.
/// </summary>
public sealed record EquipmentPage(
    MenuPicture Frame,
    IReadOnlyList<MenuRect> Hands,
    IReadOnlyList<MenuRect> Bag,
    MenuRect Divider,
    MenuText LastWeaponNote);

/// <summary>
/// The player's values and boons on both pages of the menu (issue #390): a see-through frame (no dark ground) under the
/// HUD's experience zone, its texts with their shadow.
/// </summary>
public sealed record ValuesSheet(MenuRect Frame, IReadOnlyList<MenuText> Texts);

/// <summary>The map page (issue #390): its tabs and hint, and the area the map is drawn in.</summary>
public sealed record MapPage(MenuPicture Frame, MenuRect MapArea);

/// <summary>
/// The canvas the menus are laid out on, in design pixels - the same as the HUD's (<see cref="HudCanvas"/>): 480 lines
/// over the picture of the 3D world, never narrower than 768. <see cref="Screen"/> is where it lies in the window.
/// The design is 854 x 480; on another canvas the menus keep to its middle (<see cref="OffsetX"/>, <see cref="OffsetY"/>).
/// </summary>
public readonly record struct MenuCanvas(int Width, int Height, ScreenRect Screen)
{
    /// <summary>Width of the approved design.</summary>
    public const int DesignWidth = 854;

    /// <summary>Height of the approved design.</summary>
    public const int DesignHeight = HudCanvas.DesignHeight;

    /// <summary>How far the design's middle moves to the right on this canvas (negative on a narrow one).</summary>
    public int OffsetX => (Width - DesignWidth) / 2;

    /// <summary>How far the design's middle moves down on this canvas (on a 4:3 or 5:4 one).</summary>
    public int OffsetY => (Height - DesignHeight) / 2;

    /// <summary>Window pixels per canvas pixel.</summary>
    public float Scale => Width > 0 ? Screen.Width / Width : 0f;

    /// <summary>The whole canvas.</summary>
    public MenuRect Bounds => new(0, 0, Width, Height);

    /// <summary>The canvas in a window <paramref name="windowWidth"/> x <paramref name="windowHeight"/>, placed like the 3D picture.</summary>
    public static MenuCanvas For(int windowWidth, int windowHeight)
    {
        var pictureWidth = PictureWidth.For(windowWidth, windowHeight, DesignHeight);
        var size = HudCanvas.For(pictureWidth);
        return new MenuCanvas(size.Width, size.Height, Letterbox.Fit(pictureWidth, DesignHeight, windowWidth, windowHeight));
    }

    /// <summary>The canvas point under the window point <paramref name="windowPoint"/> (both NaN in a minimised window).</summary>
    public Vector2 ToCanvas(Vector2 windowPoint) => Scale > 0f
        ? (windowPoint - new Vector2(Screen.X, Screen.Y)) / Scale
        : new Vector2(float.NaN, float.NaN);

    /// <summary>The window line of the canvas line <paramref name="canvasY"/>.</summary>
    public float ToWindowY(float canvasY) => Screen.Y + (canvasY * Scale);
}

/// <summary>
/// Where the menus draw what (issue #371): the approved design "C2 Slim" (art bible, section "Menus"), 854 x 480, kept
/// in the middle of the canvas - except the map and equipment pages (issue #390), which stand right of the frame of
/// values at the HUD's top left corner, with their tabs at the same place on both. Pure arithmetic - the Game project draws the result with the parts of
/// <see cref="MenuParts"/>. Texts are measured with the width function given, so the layout works with any font.
/// </summary>
public sealed class MenuLayout
{
    /// <summary>The entries of the title screen.</summary>
    public static readonly IReadOnlyList<string> TitleItems = ["Start", "Settings", "Quit"];

    /// <summary>The entries of the Esc menu.</summary>
    public static readonly IReadOnlyList<string> PauseItems = ["Resume", "Settings", "Back to title", "Quit"];

    /// <summary>The entries of the question before quitting; "Cancel" first, so a hasty Enter keeps the game open.</summary>
    public static readonly IReadOnlyList<string> QuitItems = ["Cancel", "Quit"];

    /// <summary>The labels of the settings rows.</summary>
    public static readonly IReadOnlyList<string> SettingsLabels =
        ["Mouse sensitivity", "Master volume", "Effects volume", "Ambience volume", "Display", "Back"];

    /// <summary>The labels of the two tabs (map, equipment), with their keys.</summary>
    public static readonly IReadOnlyList<string> TabLabels = ["[Tab] Map", "[G] Equipment"];

    /// <summary>The help line of the equipment page.</summary>
    public const string EquipmentHelp = "Left click: equip   Right click: drop   [G] or [Esc] close   [Tab] map";

    /// <summary>The hint under the tabs of the map page.</summary>
    public const string MapHint = "Tab or Esc: close   G: equipment";

    /// <summary>The note while the pointer is on the only ranged weapon left.</summary>
    public const string LastWeaponText = "Your last ranged weapon stays";

    /// <summary>Left edge of the frame of values on both pages (issue #390), near the HUD's level frame.</summary>
    public const int SheetLeft = 8;

    /// <summary>
    /// Top of the frame of values: <see cref="HudLayout.Spacing"/> under the HUD's top left group - level, experience,
    /// waiting boons and the yellow hint to level up (<see cref="SheetTopUnder"/>, issue #387) - so the hint stays
    /// readable with a menu open. The HUD keeps that group at the top left on every canvas, so the frame does not move
    /// with the menus' middle.
    /// </summary>
    public int SheetTop { get; }

    /// <summary>Width of the frame of values - the same for every player, so the tabs never move.</summary>
    public const int SheetWidth = 184;

    /// <summary>Distance between two entries of a column (art bible: 62 px).</summary>
    public const int RowPitch = 62;

    /// <summary>Height of the cursor around an entry (art bible: 36 px).</summary>
    public const int CursorHeight = 36;

    /// <summary>Width and height of a box on the equipment page.</summary>
    public const int BoxWidth = 128;

    /// <summary>Height of a box on the equipment page.</summary>
    public const int BoxHeight = 50;

    /// <summary>The two tabs as the design draws them (left, top, right, bottom); the parts table holds the same (tested).</summary>
    public static readonly IReadOnlyList<MenuRect> TabShapes =
        [MenuRect.FromEdges(40, 8, 160, 58), MenuRect.FromEdges(168, 8, 358, 58)];

    /// <summary>Left and right end of a slider's trough in the settings (the parts table holds the same, tested).</summary>
    public const float SliderLeft = 420f;

    /// <summary>Right end of a slider's trough.</summary>
    public const float SliderRight = 580f;

    private const int ColumnWidth = 240;
    private const int ColumnCursorWidth = 208;
    private const int ColumnPadding = 18;
    private const int EquipmentPadding = 12;
    private const int BoxGap = 6;
    private const int BoxColumns = 4;
    private const int TabsAbovePanel = 54;
    private const int PagePanelWidth = 774;
    private const int PagePanelTop = 62;
    private const int PagePanelHeight = 390;
    private const int PageMargin = 12;
    private const int SheetGap = 6;
    private const int SheetPadding = 8;
    private const int SheetLinePitch = 11;
    private const int SettingsValueRight = 622;

    // The design's sliders start at 420; at full value the knob (to 587) would then cover "100%" at 584, so they sit
    // 8 px further left (the longest label ends at 401).
    private const float SettingsSliderShift = -8f;

    private static readonly int[] SettingsRowCentres = [126, 176, 226, 276, 330, 382];

    private readonly Func<string, int, int> _width;
    private readonly int _capHeight;

    /// <summary>
    /// A layout measuring texts with <paramref name="width"/> (text, enlargement) and capitals
    /// <paramref name="capHeight"/> pixels high at 1x, its frame of values starting on line <paramref name="sheetTop"/>
    /// (<see cref="SheetTopUnder"/> with a HUD).
    /// </summary>
    public MenuLayout(Func<string, int, int> width, int capHeight, int sheetTop)
    {
        _width = width ?? throw new ArgumentNullException(nameof(width));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capHeight);
        ArgumentOutOfRangeException.ThrowIfNegative(sheetTop);
        _capHeight = capHeight;
        SheetTop = sheetTop;
    }

    /// <summary>A layout measuring with the pixel font <paramref name="font"/>, its frame of values under <paramref name="hud"/>.</summary>
    public static MenuLayout For(PixelFont font, HudLayout hud)
    {
        ArgumentNullException.ThrowIfNull(font);
        return new MenuLayout(font.Width, font.CapHeight, SheetTopUnder(hud));
    }

    /// <summary>Where the frame of values starts under the HUD <paramref name="hud"/>: the spacing below its top left group.</summary>
    public static int SheetTopUnder(HudLayout hud)
    {
        ArgumentNullException.ThrowIfNull(hud);
        return hud.TopLeftBottom + HudLayout.Spacing;
    }

    /// <summary>The rectangle a text covers, with its shadow one pixel below and right.</summary>
    public MenuRect Bounds(MenuText text) =>
        new(text.X, text.Top, _width(text.Text, text.Scale) + 1, (_capHeight * text.Scale) + 1);

    /// <summary>The title screen: the game's name, a narrow panel with three entries, the version at the bottom right.</summary>
    public MenuPicture Title(MenuCanvas canvas, int selected, string version)
    {
        ArgumentNullException.ThrowIfNull(version);
        var picture = new Builder(this);
        Column(picture, canvas, TitleItems, top: 204, selected);
        picture.Centred("Silver & Ash", canvas.Width / 2f, 110 + canvas.OffsetY, 4, MenuTextColor.Main);
        picture.Centred("a hunt in the catacombs", canvas.Width / 2f, 150 + canvas.OffsetY, 2, MenuTextColor.Quiet);
        var text = "v" + version;
        picture.Text(text, canvas.Width - 12 - _width(text, 1), canvas.Height - 12 - _capHeight, 1, MenuTextColor.Quiet);
        return picture.Build();
    }

    /// <summary>The Esc menu: "Paused" over a narrow panel with four entries, the hint below.</summary>
    public MenuPicture Pause(MenuCanvas canvas, int selected)
    {
        var picture = new Builder(this);
        var panel = Column(picture, canvas, PauseItems, top: 160, selected);
        picture.Centred("Paused", canvas.Width / 2f, panel.Y - 72, 3, MenuTextColor.Main);
        picture.Centred("Esc: back to the game", canvas.Width / 2f, panel.Bottom + 14, 1, MenuTextColor.Quiet);
        return picture.Build();
    }

    /// <summary>The question before quitting: "Quit?" in a small panel, "Cancel" over "Quit", the hint below.</summary>
    public MenuPicture Quit(MenuCanvas canvas, int selected)
    {
        var picture = new Builder(this);
        var centre = canvas.Width / 2f;
        var panel = new MenuRect(centre - 104, 146 + canvas.OffsetY, 208, 190);
        picture.Panels.Add(new MenuPanel(panel, Gable: true));
        picture.Centred("Quit?", centre, panel.Y + 20, 3, MenuTextColor.Main);
        for (var i = 0; i < QuitItems.Count; i++)
        {
            var row = new MenuRect(centre - 88, panel.Y + 58 + (i * 56), 176, CursorHeight);
            Entry(picture, row, QuitItems[i], i == selected);
        }

        picture.Centred("Esc: cancel", centre, panel.Bottom + 14, 1, MenuTextColor.Quiet);
        return picture.Build();
    }

    /// <summary>
    /// The settings: one row each for the mouse sensitivity, three volumes (with sliders), window or full screen, and
    /// "Back" - in a wide panel under the heading, the hint below.
    /// </summary>
    public MenuPicture Settings(MenuCanvas canvas, int selected, GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var picture = new Builder(this);
        var dx = canvas.OffsetX;
        var dy = canvas.OffsetY;
        var panel = new MenuRect(200 + dx, 88 + dy, 454, 336);
        picture.Panels.Add(new MenuPanel(panel, Gable: true));
        picture.Centred("Settings", panel.CentreX, panel.Y - 47, 3, MenuTextColor.Main);

        (float Fraction, string Value)[] values =
        [
            ((settings.MouseSensitivity - GameSettings.MinMouseSensitivity) / (GameSettings.MaxMouseSensitivity - GameSettings.MinMouseSensitivity),
                string.Create(CultureInfo.InvariantCulture, $"{settings.MouseSensitivity:0.0}x")),
            (settings.MasterVolume, Percent(settings.MasterVolume)),
            (settings.EffectsVolume, Percent(settings.EffectsVolume)),
            (settings.AmbienceVolume, Percent(settings.AmbienceVolume)),
        ];

        for (var i = 0; i < SettingsRowCentres.Length; i++)
        {
            var centreY = SettingsRowCentres[i] + dy;
            var row = new MenuRect(214 + dx, centreY - (CursorHeight / 2), 426, CursorHeight);
            var isSelected = i == selected;
            picture.Rows.Add(row);
            if (isSelected)
            {
                picture.Cursors.Add(new MenuCursor(row, Small: false));
            }

            if (i == SettingsLabels.Count - 1)
            {
                picture.Centred(SettingsLabels[i], row.CentreX, TopFor(centreY, 2), 2, isSelected ? MenuTextColor.Selected : MenuTextColor.Second);
                continue;
            }

            picture.Text(SettingsLabels[i], 226 + dx, TopFor(centreY, 2), 2, isSelected ? MenuTextColor.Selected : MenuTextColor.Second);
            if (i < values.Length)
            {
                picture.Sliders.Add(new MenuSlider(SliderLeft + SettingsSliderShift + dx, centreY, Math.Clamp(values[i].Fraction, 0f, 1f)));
                var value = values[i].Value;
                picture.Text(value, SettingsValueRight + dx - _width(value, 2), TopFor(centreY, 2), 2, MenuTextColor.Main);
                continue;
            }

            // Display: both choices side by side, the one in use gold in the small cursor.
            var window = new MenuText("Window", (int)(SliderLeft + SettingsSliderShift) + dx, TopFor(centreY, 2), 2,
                settings.FullScreen ? MenuTextColor.Main : MenuTextColor.Selected);
            var full = new MenuText("Full screen", SettingsValueRight + dx - _width("Full screen", 2), TopFor(centreY, 2), 2,
                settings.FullScreen ? MenuTextColor.Selected : MenuTextColor.Main);
            var chosen = settings.FullScreen ? full : window;
            picture.Cursors.Add(new MenuCursor(
                new MenuRect(chosen.X - 14, centreY - 15, _width(chosen.Text, 2) + 28, 30), Small: true));
            picture.Texts.Add(window);
            picture.Texts.Add(full);
        }

        picture.Centred("Left/Right: change    Esc: back", panel.CentreX, panel.Bottom + 12, 1, MenuTextColor.Quiet);
        return picture.Build();
    }

    /// <summary>
    /// The panel of the equipment page (issue #390): right of the frame of values, as wide as the design's (774) where the
    /// canvas has room, never closer than 12 px to its right edge; the tabs of both pages stand over its left end.
    /// </summary>
    public static MenuRect PagePanel(MenuCanvas canvas)
    {
        const int left = SheetLeft + SheetWidth + SheetGap;
        var room = canvas.Width - PageMargin - left;
        var width = Math.Min(PagePanelWidth, room);
        return new MenuRect(left + ((room - width) / 2), PagePanelTop + canvas.OffsetY, width, PagePanelHeight);
    }

    /// <summary>
    /// Where the mouse can click tab <paramref name="index"/> (0 map, 1 equipment): its outline as an open tab, at the same
    /// place on both pages.
    /// </summary>
    public static MenuRect TabTarget(MenuCanvas canvas, int index)
    {
        var panel = PagePanel(canvas);
        return TabOutline(index, open: true, panel.X, panel.Y - TabsAbovePanel);
    }

    /// <summary>The tab under the canvas point <paramref name="point"/> as the key of its click, or null.</summary>
    public static MenuKey? TabAt(MenuCanvas canvas, Vector2 point)
    {
        for (var i = 0; i < TabLabels.Count; i++)
        {
            if (TabTarget(canvas, i).Contains(point))
            {
                return i == 0 ? MenuKey.MapTab : MenuKey.EquipmentTab;
            }
        }

        return null;
    }

    /// <summary>
    /// The map page: the tabs where the equipment page has them, the hint under them, and the area for the map below -
    /// right of the frame of values.
    /// </summary>
    public MapPage Map(MenuCanvas canvas)
    {
        var picture = new Builder(this);
        var panel = PagePanel(canvas);
        var tabsTop = panel.Y - TabsAbovePanel;
        Tabs(picture, PlayerMenuPage.Map, panel.X, tabsTop);
        var hintTop = tabsTop + TabShapes[0].Height + 8;
        picture.Centred(MapHint, panel.CentreX, hintTop, 2, MenuTextColor.Quiet);
        var area = MenuRect.FromEdges(panel.X, hintTop + (2 * _capHeight) + 12, panel.Right, panel.Bottom - 22);
        return new MapPage(picture.Build(), area);
    }

    /// <summary>
    /// The frame of values on both pages (issue #390): "HUNTER" over the values, "BOONS" over the chosen boons, names
    /// left, values in a second column - small, as the HUD writes, in a frame without a ground.
    /// </summary>
    public ValuesSheet Sheet(IReadOnlyList<SheetLine> stats, IReadOnlyList<SheetLine> boons)
    {
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(boons);
        var texts = new List<MenuText>();
        var left = SheetLeft + SheetPadding;
        var labelWidth = stats.Concat(boons).Select(line => _width(line.Label, 1)).DefaultIfEmpty(0).Max();
        var valueX = left + labelWidth + SheetPadding;
        var y = SheetTop + SheetPadding;

        void Heading(string text)
        {
            texts.Add(new MenuText(text, left, y, 2, MenuTextColor.Quiet));
            y += (2 * _capHeight) + 6;
        }

        void Lines(IEnumerable<SheetLine> lines)
        {
            foreach (var line in lines)
            {
                texts.Add(new MenuText(line.Label, left, y, 1, MenuTextColor.Second));
                texts.Add(new MenuText(line.Value, valueX, y, 1, MenuTextColor.Main));
                y += SheetLinePitch;
            }
        }

        Heading("HUNTER");
        Lines(stats);
        y += 4;
        Heading("BOONS");
        if (boons.Count == 0)
        {
            texts.Add(new MenuText("No boons yet", left, y, 1, MenuTextColor.Quiet));
            y += SheetLinePitch;
        }

        Lines(boons);
        var bottom = y - SheetLinePitch + _capHeight + 1 + SheetPadding;
        return new ValuesSheet(new MenuRect(SheetLeft, SheetTop, SheetWidth, bottom - SheetTop), texts);
    }

    /// <summary>
    /// The equipment page with <paramref name="bagUsed"/> places of the bag taken (issue #390): the panel right of the
    /// frame of values (<see cref="PagePanel"/>), the tabs over its left end; the hands on top, the bag below, the items
    /// under a line, the help at the bottom.
    /// </summary>
    public EquipmentPage Equipment(MenuCanvas canvas, int bagUsed)
    {
        var picture = new Builder(this);
        var panel = PagePanel(canvas);
        picture.Panels.Add(new MenuPanel(panel, Gable: false));
        Tabs(picture, PlayerMenuPage.Equipment, panel.X, panel.Y - TabsAbovePanel);

        const int gridWidth = (BoxColumns * BoxWidth) + ((BoxColumns - 1) * BoxGap);
        var left = panel.X + MathF.Floor((panel.Width - gridWidth) / 2f);
        var handsTop = panel.Y + 38;
        var bagTop = panel.Y + 126;
        var hands = Enumerable.Range(0, 1 + Player.RangedSlotCount)
            .Select(i => new MenuRect(left + (i * (BoxWidth + BoxGap)), handsTop, BoxWidth, BoxHeight)).ToArray();
        var bag = Enumerable.Range(0, Player.InventorySlotCount)
            .Select(i => new MenuRect(left + (i % BoxColumns * (BoxWidth + BoxGap)), bagTop + (i / BoxColumns * (BoxHeight + BoxGap)), BoxWidth, BoxHeight))
            .ToArray();

        picture.Text("IN HAND", (int)left, (int)handsTop - 20, 2, MenuTextColor.Quiet);
        picture.Text(string.Create(CultureInfo.InvariantCulture, $"BAG  {bagUsed} / {Player.InventorySlotCount}"),
            (int)left, (int)bagTop - 20, 2, MenuTextColor.Quiet);

        var lastBox = bag[^1].Bottom;
        var note = new MenuText(LastWeaponText, (int)MathF.Round(left + (gridWidth / 2f) - (_width(LastWeaponText, 2) / 2f)),
            (int)lastBox + 18, 2, MenuTextColor.Selected);
        var divider = new MenuRect(left, lastBox + 42, gridWidth, 1);
        var itemsTop = (int)divider.Y + 10;
        picture.Text("ITEMS", (int)left, itemsTop, 2, MenuTextColor.Quiet);
        picture.Text("No items yet", (int)left, itemsTop + (2 * _capHeight) + 8, 2, MenuTextColor.Second);

        var helpScale = _width(EquipmentHelp, 2) <= panel.Width - (2 * EquipmentPadding) ? 2 : 1;
        picture.Centred(EquipmentHelp, panel.CentreX, panel.Bottom - 22 - (_capHeight * helpScale / 2), helpScale, MenuTextColor.Quiet);
        return new EquipmentPage(picture.Build(), hands, bag, divider, note);
    }

    /// <summary>
    /// The texts in a box of the equipment page: the key small at the top, the name below (smaller if it would not fit,
    /// "-" for an empty place), the kind small at the bottom.
    /// </summary>
    public EquipmentBoxTexts Box(MenuRect box, string key, string? name, string? kind)
    {
        ArgumentNullException.ThrowIfNull(key);
        var x = (int)box.X + 6;
        var keyText = new MenuText(key, x, (int)box.Y + 6, 1, MenuTextColor.Quiet);
        if (name is null)
        {
            return new EquipmentBoxTexts(keyText, new MenuText("-", x, (int)box.Y + 18, 2, MenuTextColor.Quiet), null);
        }

        var nameScale = _width(name, 2) <= box.Width - 12 ? 2 : 1;
        var nameTop = (int)box.Y + 18 + (nameScale == 2 ? 0 : _capHeight / 2);
        MenuText? kindText = kind is null ? null : new MenuText(kind, x, (int)box.Y + 37, 1, MenuTextColor.Quiet);
        return new EquipmentBoxTexts(keyText, new MenuText(name, x, nameTop, nameScale, MenuTextColor.Main), kindText);
    }

    // A narrow panel with a gable and one entry per row at the design's pitch; returns the panel.
    private MenuRect Column(Builder picture, MenuCanvas canvas, IReadOnlyList<string> items, int top, int selected)
    {
        var centre = canvas.Width / 2f;
        var height = (2 * ColumnPadding) + ((items.Count - 1) * RowPitch) + CursorHeight;
        var panel = new MenuRect(centre - (ColumnWidth / 2), top + canvas.OffsetY, ColumnWidth, height);
        picture.Panels.Add(new MenuPanel(panel, Gable: true));
        for (var i = 0; i < items.Count; i++)
        {
            var row = new MenuRect(centre - (ColumnCursorWidth / 2), panel.Y + ColumnPadding + (i * RowPitch), ColumnCursorWidth, CursorHeight);
            Entry(picture, row, items[i], i == selected);
        }

        return panel;
    }

    // One entry: its row for the mouse, the cursor if selected, the word in the middle.
    private void Entry(Builder picture, MenuRect row, string text, bool selected)
    {
        picture.Rows.Add(row);
        if (selected)
        {
            picture.Cursors.Add(new MenuCursor(row, Small: false));
        }

        picture.Centred(text, row.CentreX, TopFor(row.CentreY, 2), 2, selected ? MenuTextColor.Selected : MenuTextColor.Second);
    }

    // Both tabs from left, top (the top of an open tab); the open page's tab lit, its label gold.
    private void Tabs(Builder picture, PlayerMenuPage page, float left, float top)
    {
        for (var i = 0; i < TabLabels.Count; i++)
        {
            var open = (i == 0) == (page == PlayerMenuPage.Map);
            picture.Tabs.Add(new MenuTab(i, open, left, top));
            var outline = TabOutline(i, open, left, top);
            var centreY = outline.Bottom - (open ? 20 : 15);
            picture.Centred(TabLabels[i], outline.CentreX, TopFor(centreY, 2), 2, open ? MenuTextColor.Selected : MenuTextColor.Quiet);
        }
    }

    /// <summary>
    /// The outline of tab <paramref name="index"/> in a row starting at <paramref name="left"/>, <paramref name="top"/>
    /// (without the stud of the open one); a closed tab is 8 pixels lower, as in the design.
    /// </summary>
    public static MenuRect TabOutline(int index, bool open, float left, float top)
    {
        var shape = TabShapes[index];
        var lower = open ? 0f : 8f;
        return new MenuRect(shape.X - TabShapes[0].X + left, shape.Y - TabShapes[0].Y + top + lower, shape.Width, shape.Height - lower);
    }

    // The top of capitals whose middle is at centreY, as in the design (whole pixels).
    private int TopFor(float centreY, int scale) => (int)MathF.Round(centreY - (_capHeight * scale / 2f));

    private static string Percent(float volume) =>
        string.Create(CultureInfo.InvariantCulture, $"{MathF.Round(volume * 100f):0}%");

    private sealed class Builder(MenuLayout layout)
    {
        public List<MenuPanel> Panels { get; } = [];

        public List<MenuCursor> Cursors { get; } = [];

        public List<MenuSlider> Sliders { get; } = [];

        public List<MenuTab> Tabs { get; } = [];

        public List<MenuText> Texts { get; } = [];

        public List<MenuRect> Rows { get; } = [];

        public void Text(string text, int x, int top, int scale, MenuTextColor color) =>
            Texts.Add(new MenuText(text, x, top, scale, color));

        // A text centred on centreX (whole pixels), its capitals' top at top.
        public void Centred(string text, float centreX, float top, int scale, MenuTextColor color) =>
            Text(text, (int)MathF.Round(centreX - (layout._width(text, scale) / 2f)), (int)MathF.Round(top), scale, color);

        public MenuPicture Build() => new(Panels, Cursors, Sliders, Tabs, Texts, Rows);
    }
}
