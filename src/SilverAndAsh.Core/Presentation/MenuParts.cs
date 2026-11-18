using System.Numerics;
using System.Text.Json;

namespace SilverAndAsh.Core.Presentation;

/// <summary>A rectangle on the menu canvas (or in the parts picture), in design pixels.</summary>
public readonly record struct MenuRect(float X, float Y, float Width, float Height)
{
    /// <summary>The right edge.</summary>
    public float Right => X + Width;

    /// <summary>The lower edge.</summary>
    public float Bottom => Y + Height;

    /// <summary>The middle, left to right.</summary>
    public float CentreX => X + (Width / 2f);

    /// <summary>The middle, top to bottom.</summary>
    public float CentreY => Y + (Height / 2f);

    /// <summary>Whether <paramref name="point"/> lies inside (left and top edge included, right and lower edge not).</summary>
    public bool Contains(Vector2 point) => point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;

    /// <summary>Whether <paramref name="other"/> lies completely inside this rectangle.</summary>
    public bool Encloses(MenuRect other) =>
        other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;

    /// <summary>Whether the two rectangles share any area.</summary>
    public bool Overlaps(MenuRect other) =>
        X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

    /// <summary>A rectangle from its edges.</summary>
    public static MenuRect FromEdges(float left, float top, float right, float bottom) => new(left, top, right - left, bottom - top);
}

/// <summary>
/// One picture part of the menus (issue #371): its rectangle in the parts picture and where the approved design "C2 Slim"
/// put its top left corner (854 x 480 design pixels).
/// </summary>
public readonly record struct MenuPart(string Name, int X, int Y, int Width, int Height, int LayoutX, int LayoutY)
{
    /// <summary>The part's rectangle in the parts picture.</summary>
    public MenuRect Source => new(X, Y, Width, Height);
}

/// <summary>One piece to draw: a rectangle of the parts picture and where it goes on the canvas (stretched if the sizes differ).</summary>
public readonly record struct MenuPiece(MenuRect Source, MenuRect Target);

/// <summary>A colour of the menus from the parts table (red, green, blue).</summary>
public readonly record struct MenuColor(byte R, byte G, byte B);

/// <summary>The pieces of one slider: the empty trough, its gold filling up to the value, the knob and the light edge at the value.</summary>
public readonly record struct SliderPieces(MenuPiece Empty, MenuPiece? Full, MenuPiece Knob, float EdgeX);

/// <summary>
/// The measure table of the menu parts, <c>assets/menu/menu_parts.json</c> (issue #371, written by
/// <c>tools/menu/menue_teile.py</c>), and where its parts go for a wanted size: a panel is stretched in nine pieces (corners
/// stay 1:1), a cursor in three (the points stay 1:1); gable, slider, knob and tabs keep their size.
/// Pure arithmetic - the Game project draws the pieces from the picture the table names.
/// </summary>
public sealed class MenuParts
{
    private readonly Dictionary<string, MenuPart> _parts;

    private MenuParts(JsonElement root, Dictionary<string, MenuPart> parts)
    {
        _parts = parts;
        Image = root.GetProperty("image").GetString() ?? string.Empty;
        var shapes = root.GetProperty("shapes");
        PanelShape = Edges(shapes.GetProperty("panel"));
        CursorShape = Edges(shapes.GetProperty("cursor"));
        CursorSmallShape = Edges(shapes.GetProperty("cursorSmall"));
        var slider = shapes.GetProperty("slider");
        SliderLeft = slider[0].GetSingle();
        SliderRight = slider[1].GetSingle();
        SliderCentreY = slider[2].GetSingle();
        KnobCentre = PointOf(shapes.GetProperty("knobCentre"));
        TabShapes = shapes.GetProperty("tabs").EnumerateArray().Select(Edges).ToArray();
        GableAnchor = PointOf(shapes.GetProperty("gableAnchor"));
        var slice = root.GetProperty("slice");
        PanelSlice = slice.GetProperty("panel").GetInt32();
        CursorSlice = slice.GetProperty("cursor").GetInt32();
        CursorSmallSlice = slice.GetProperty("cursorSmall").GetInt32();
        var game = root.GetProperty("game");
        var text = game.GetProperty("text");
        TextMain = ColorOf(text.GetProperty("main"));
        TextSecond = ColorOf(text.GetProperty("second"));
        TextSelected = ColorOf(text.GetProperty("selected"));
        TextQuiet = ColorOf(text.GetProperty("quiet"));
        TextShadow = ColorOf(text.GetProperty("shadow"));
        SliderEdge = ColorOf(game.GetProperty("sliderEdge"));
    }

    /// <summary>File name of the parts picture, next to the table.</summary>
    public string Image { get; }

    /// <summary>The panel the part "panel" was drawn for (its outline, without the soft outer pixels).</summary>
    public MenuRect PanelShape { get; }

    /// <summary>The cursor the part "cursor" was drawn for.</summary>
    public MenuRect CursorShape { get; }

    /// <summary>The cursor the part "cursor_small" was drawn for.</summary>
    public MenuRect CursorSmallShape { get; }

    /// <summary>Left end of the slider trough the slider parts were drawn for.</summary>
    public float SliderLeft { get; }

    /// <summary>Right end of that trough.</summary>
    public float SliderRight { get; }

    /// <summary>The middle of that trough, top to bottom.</summary>
    public float SliderCentreY { get; }

    /// <summary>Where the middle of the knob was drawn.</summary>
    public Vector2 KnobCentre { get; }

    /// <summary>The two tabs (map, equipment) as the design placed them; the open one rises above its top.</summary>
    public IReadOnlyList<MenuRect> TabShapes { get; }

    /// <summary>The middle of the panel's upper edge the gable was drawn for.</summary>
    public Vector2 GableAnchor { get; }

    /// <summary>Pixels of each panel edge that stay 1:1 (corners with their studs).</summary>
    public int PanelSlice { get; }

    /// <summary>Pixels at each end of the cursor that stay 1:1 (its points).</summary>
    public int CursorSlice { get; }

    /// <summary>The same for the small cursor.</summary>
    public int CursorSmallSlice { get; }

    /// <summary>Headings and quiet entries.</summary>
    public MenuColor TextMain { get; }

    /// <summary>Entries that are not selected.</summary>
    public MenuColor TextSecond { get; }

    /// <summary>Gold: only the selection.</summary>
    public MenuColor TextSelected { get; }

    /// <summary>Hints and closed tabs.</summary>
    public MenuColor TextQuiet { get; }

    /// <summary>The dark shadow under every text.</summary>
    public MenuColor TextShadow { get; }

    /// <summary>The light line at the value of a slider.</summary>
    public MenuColor SliderEdge { get; }

    /// <summary>Every part, in the order of the table.</summary>
    public IReadOnlyCollection<MenuPart> All => _parts.Values;

    /// <summary>The part named <paramref name="name"/>.</summary>
    /// <exception cref="KeyNotFoundException">If the table has no such part.</exception>
    public MenuPart this[string name] =>
        _parts.TryGetValue(name, out var part) ? part : throw new KeyNotFoundException($"The menus have no part '{name}'.");

    /// <summary>Reads the table written by the menu parts script.</summary>
    public static MenuParts Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var parts = new Dictionary<string, MenuPart>();
        foreach (var entry in root.GetProperty("parts").EnumerateArray())
        {
            var part = new MenuPart(
                entry.GetProperty("name").GetString() ?? string.Empty,
                entry.GetProperty("x").GetInt32(),
                entry.GetProperty("y").GetInt32(),
                entry.GetProperty("w").GetInt32(),
                entry.GetProperty("h").GetInt32(),
                entry.GetProperty("layoutX").GetInt32(),
                entry.GetProperty("layoutY").GetInt32());
            if (!parts.TryAdd(part.Name, part))
            {
                throw new FormatException($"The menu part '{part.Name}' is in the table twice.");
            }
        }

        return new MenuParts(root.Clone(), parts);
    }

    /// <summary>The nine pieces of a panel whose outline is <paramref name="panel"/>.</summary>
    public IReadOnlyList<MenuPiece> Panel(MenuRect panel) =>
        Stretched(this["panel"], PanelShape, panel, PanelSlice, PanelSlice);

    /// <summary>The three pieces of a cursor whose outline is <paramref name="cursor"/>; the small one for short words.</summary>
    public IReadOnlyList<MenuPiece> Cursor(MenuRect cursor, bool small = false) => small
        ? Stretched(this["cursor_small"], CursorSmallShape, cursor, CursorSmallSlice, 0)
        : Stretched(this["cursor"], CursorShape, cursor, CursorSlice, 0);

    /// <summary>The gable with its eye, over the middle of the upper edge of <paramref name="panel"/>.</summary>
    public MenuPiece Gable(MenuRect panel)
    {
        var gable = this["gable"];
        return new MenuPiece(gable.Source, new MenuRect(
            panel.CentreX + (gable.LayoutX - GableAnchor.X), panel.Y + (gable.LayoutY - GableAnchor.Y), gable.Width, gable.Height));
    }

    /// <summary>
    /// A slider whose trough starts at <paramref name="left"/> with its middle at <paramref name="centreY"/>, filled to
    /// <paramref name="fraction"/> (0 to 1). The knob sits on whole pixels like in the design.
    /// </summary>
    public SliderPieces Slider(float left, float centreY, float fraction)
    {
        fraction = Math.Clamp(fraction, 0f, 1f);
        var empty = this["slider_empty"];
        var full = this["slider_full"];
        var knob = this["knob"];
        var dx = left - SliderLeft;
        var dy = centreY - SliderCentreY;
        var target = new MenuRect(empty.LayoutX + dx, empty.LayoutY + dy, empty.Width, empty.Height);

        var edge = left + ((SliderRight - SliderLeft) * fraction);
        // Full: the whole part, with the round end of the gold beyond the trough's end.
        var visible = fraction >= 1f
            ? full.Width
            : MathF.Min(full.Width, MathF.Round(SliderLeft - full.LayoutX + ((SliderRight - SliderLeft) * fraction)));
        MenuPiece? filled = fraction > 0f && visible > 0f
            ? new MenuPiece(full.Source with { Width = visible }, target with { X = full.LayoutX + dx, Width = visible })
            : null;

        var knobX = MathF.Round(edge) + 0.5f;
        var knobTarget = new MenuRect(knobX - (KnobCentre.X - knob.LayoutX), centreY - (KnobCentre.Y - knob.LayoutY), knob.Width, knob.Height);
        return new SliderPieces(new MenuPiece(empty.Source, target), filled, new MenuPiece(knob.Source, knobTarget), edge);
    }

    /// <summary>
    /// Tab <paramref name="index"/> (0 map, 1 equipment) of a row of tabs whose first tab starts at
    /// <paramref name="left"/>, <paramref name="top"/> (the top of an open tab).
    /// </summary>
    public MenuPiece Tab(int index, bool open, float left, float top)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, TabShapes.Count);
        var part = this[(index == 0 ? "tab_map" : "tab_equipment") + (open ? "_active" : string.Empty)];
        var dx = left - TabShapes[0].X;
        var dy = top - TabShapes[0].Y;
        return new MenuPiece(part.Source, new MenuRect(part.LayoutX + dx, part.LayoutY + dy, part.Width, part.Height));
    }

    /// <summary>
    /// Pieces of <paramref name="part"/>, drawn for the outline <paramref name="shape"/>, stretched to the outline
    /// <paramref name="wanted"/>: <paramref name="sliceX"/> pixels left and right and <paramref name="sliceY"/> pixels top and
    /// bottom stay 1:1, the rest is stretched (nine pieces; three if <paramref name="sliceY"/> is 0).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">If the wanted outline is too small for the parts that stay 1:1.</exception>
    public static IReadOnlyList<MenuPiece> Stretched(MenuPart part, MenuRect shape, MenuRect wanted, int sliceX, int sliceY)
    {
        var target = new MenuRect(
            wanted.X + (part.LayoutX - shape.X),
            wanted.Y + (part.LayoutY - shape.Y),
            part.Width + (wanted.Width - shape.Width),
            part.Height + (wanted.Height - shape.Height));
        if (target.Width < 2 * sliceX + 1 || target.Height < 2 * sliceY + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(wanted), wanted, $"Too small for the part '{part.Name}'.");
        }

        var columns = Cuts(part.X, part.Width, target.X, target.Width, sliceX);
        var rows = Cuts(part.Y, part.Height, target.Y, target.Height, sliceY);
        var pieces = new List<MenuPiece>(columns.Count * rows.Count);
        foreach (var (rowSource, rowSize, rowTarget, rowTargetSize) in rows)
        {
            foreach (var (columnSource, columnSize, columnTarget, columnTargetSize) in columns)
            {
                pieces.Add(new MenuPiece(
                    new MenuRect(columnSource, rowSource, columnSize, rowSize),
                    new MenuRect(columnTarget, rowTarget, columnTargetSize, rowTargetSize)));
            }
        }

        return pieces;
    }

    private static List<(float Source, float Size, float Target, float TargetSize)> Cuts(
        float source, float size, float target, float targetSize, int slice)
    {
        if (slice == 0)
        {
            return [(source, size, target, targetSize)];
        }

        return
        [
            (source, slice, target, slice),
            (source + slice, size - (2 * slice), target + slice, targetSize - (2 * slice)),
            (source + size - slice, slice, target + targetSize - slice, slice),
        ];
    }

    private static MenuRect Edges(JsonElement box) =>
        MenuRect.FromEdges(box[0].GetSingle(), box[1].GetSingle(), box[2].GetSingle(), box[3].GetSingle());

    private static Vector2 PointOf(JsonElement point) => new(point[0].GetSingle(), point[1].GetSingle());

    private static MenuColor ColorOf(JsonElement rgb) =>
        new(rgb[0].GetByte(), rgb[1].GetByte(), rgb[2].GetByte());
}
