using System.Numerics;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Presentation;

/// <summary>What a mark on the travel map stands for (issue #480, design A of #477).</summary>
public enum TravelMark
{
    /// <summary>The teleporter the player stands on: an arrowhead above a pale diamond.</summary>
    Here,

    /// <summary>A free destination: a pale diamond.</summary>
    Ready,

    /// <summary>The selected destination: a gold diamond in gold corners.</summary>
    Chosen,

    /// <summary>A destination in a sealed room: a grey diamond with a bar, its room hatched.</summary>
    Sealed,
}

/// <summary>
/// The rooms of the level fitted into the map area of the travel window: <see cref="Size"/> canvas pixels per cell, cell
/// (<see cref="MinX"/>, <see cref="MinZ"/>) at (<see cref="Left"/>, <see cref="Top"/>). North (the first row) is up.
/// </summary>
public readonly record struct TravelMap(float Left, float Top, float Size, int MinX, int MinZ)
{
    /// <summary>The canvas point of the level point (in cells, fractions allowed).</summary>
    public Vector2 ToCanvas(float cellX, float cellZ) => new(Left + ((cellX - MinX) * Size), Top + ((cellZ - MinZ) * Size));

    /// <summary>The middle of cell (<paramref name="x"/>, <paramref name="z"/>) on the canvas.</summary>
    public Vector2 CellCentre(int x, int z) => ToCanvas(x + 0.5f, z + 0.5f);
}

/// <summary>A destination on the travel map: its teleporter's choice, the middle of its mark and its name label.</summary>
public sealed record TravelSpot(TravelChoice Choice, Vector2 Centre, MenuRect Target, MenuText Name);

/// <summary>
/// The travel window: a panel with "Teleporters", the map of the rooms on the left with a mark per unlocked teleporter,
/// on the right the chosen destination with the Travel button and the legend, the keys at the bottom.
/// </summary>
public sealed record TravelPage(
    MenuPicture Frame,
    MenuRect MapArea,
    MenuRect Detail,
    MenuRect Button,
    TravelMap Map,
    IReadOnlyList<TravelSpot> Spots,
    IReadOnlyList<(TravelMark Mark, Vector2 Centre)> Legend);

/// <summary>
/// Where the travel window (issue #480, design A of issue #477 as the human chose it: no locked destinations, no count)
/// draws what, on the menu canvas of 854 x 480 kept in its middle. Pure arithmetic: the Game project draws the result and
/// asks it what lies under the mouse. Which destination is free or sealed comes from Core (<see cref="TravelChoice"/>).
/// </summary>
public sealed class TravelLayout
{
    /// <summary>The window's heading.</summary>
    public const string Title = "Teleporters";

    /// <summary>The text of the button.</summary>
    public const string ButtonText = "Travel [Enter]";

    /// <summary>The key lines at the bottom: keyboard, mouse, gamepad.</summary>
    public static readonly IReadOnlyList<string> KeyLines =
    [
        "Keys   Arrows: choose   Enter: travel   T or Esc: close",
        "Mouse  Click: choose   Double-click or button: travel",
        "Pad    D-pad: choose   A: travel   B: close",
    ];

    /// <summary>The names in the legend, in the order of <see cref="TravelMark"/>.</summary>
    public static readonly IReadOnlyList<string> LegendNames = ["You are here", "Ready", "Chosen", "Sealed"];

    /// <summary>Largest cell on the map, so that a small level does not fill the area with three huge squares.</summary>
    public const float MaxCellSize = 20f;

    /// <summary>Half the side of the square around a mark that the mouse can point at.</summary>
    public const float MarkReach = 9f;

    private static readonly MenuRect PanelShape = new(100, 46, 654, 388);
    private static readonly MenuRect MapShape = new(114, 92, 370, 280);
    private static readonly MenuRect DetailShape = new(498, 92, 242, 98);
    private static readonly MenuRect ButtonShape = new(510, 154, 218, 28);

    private readonly Func<string, int, int> _width;
    private readonly int _capHeight;

    /// <summary>A layout measuring texts with <paramref name="width"/> (text, enlargement), capitals <paramref name="capHeight"/> high at 1x.</summary>
    public TravelLayout(Func<string, int, int> width, int capHeight)
    {
        _width = width ?? throw new ArgumentNullException(nameof(width));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capHeight);
        _capHeight = capHeight;
    }

    /// <summary>
    /// The window on <paramref name="canvas"/> for <paramref name="choices"/> with <paramref name="selected"/> (a teleporter
    /// number, or null) shown on the right; <paramref name="rooms"/> are the cells of the rooms the map shows.
    /// </summary>
    public TravelPage Page(MenuCanvas canvas, IReadOnlyList<TravelChoice> choices, int? selected, IReadOnlyList<(int X, int Z)> rooms)
    {
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(rooms);
        var shift = new Vector2(canvas.OffsetX, canvas.OffsetY);
        var panel = Moved(PanelShape, shift);
        var mapArea = Moved(MapShape, shift);
        var detail = Moved(DetailShape, shift);
        var button = Moved(ButtonShape, shift);

        var texts = new List<MenuText>();
        var cursors = new List<MenuCursor>();
        texts.Add(new MenuText(Title, (int)panel.X + 14, (int)panel.Y + 12, 3, MenuTextColor.Main));

        var chosen = choices.FirstOrDefault(choice => choice.Teleporter.Id == selected);
        texts.Add(new MenuText("Chosen", (int)detail.X + 10, (int)detail.Y + 10, 1, MenuTextColor.Quiet));
        if (chosen is not null)
        {
            texts.Add(new MenuText(Name(chosen), (int)detail.X + 10, (int)detail.Y + 24, 2, MenuTextColor.Selected));
            texts.Add(new MenuText(StateText(chosen.State), (int)detail.X + 10, (int)detail.Y + 46, 1, MenuTextColor.Second));
        }
        else
        {
            texts.Add(new MenuText("No destination yet", (int)detail.X + 10, (int)detail.Y + 24, 2, MenuTextColor.Quiet));
        }

        var canTravel = chosen?.CanTravel == true;
        if (canTravel)
        {
            cursors.Add(new MenuCursor(button, Small: false));
        }

        texts.Add(Centred(ButtonText, button.CentreX, button.CentreY, 2, canTravel ? MenuTextColor.Selected : MenuTextColor.Quiet));

        var legendTop = detail.Bottom + 16;
        texts.Add(new MenuText("Legend", (int)detail.X, (int)legendTop, 1, MenuTextColor.Second));
        var legend = new List<(TravelMark, Vector2)>();
        for (var i = 0; i < LegendNames.Count; i++)
        {
            var centreY = legendTop + 30 + (i * 18);
            legend.Add(((TravelMark)i, new Vector2(detail.X + 12, centreY)));
            texts.Add(new MenuText(LegendNames[i], (int)detail.X + 28, TopFor(centreY, 1), 1, MenuTextColor.Second));
        }

        for (var i = 0; i < KeyLines.Count; i++)
        {
            texts.Add(new MenuText(KeyLines[i], (int)mapArea.X, (int)mapArea.Bottom + 12 + (i * 13), 1, MenuTextColor.Quiet));
        }

        var map = Fit(rooms.Concat(choices.Select(choice => (choice.Teleporter.Plan.X, choice.Teleporter.Plan.Z))), mapArea);
        var spots = choices.Select(choice => Spot(choice, map)).ToList();

        var frame = new MenuPicture([new MenuPanel(panel, Gable: true)], cursors, [], [], texts, [button]);
        return new TravelPage(frame, mapArea, detail, button, map, spots, legend);
    }

    /// <summary>The cells (plus a margin of one) fitted into <paramref name="area"/>, at most <see cref="MaxCellSize"/> per cell, in its middle.</summary>
    public static TravelMap Fit(IEnumerable<(int X, int Z)> cells, MenuRect area)
    {
        ArgumentNullException.ThrowIfNull(cells);
        var all = cells.ToList();
        if (all.Count == 0)
        {
            return new TravelMap(area.CentreX, area.CentreY, MaxCellSize, 0, 0);
        }

        var minX = all.Min(cell => cell.X) - 1;
        var minZ = all.Min(cell => cell.Z) - 1;
        var spanX = all.Max(cell => cell.X) - minX + 2;
        var spanZ = all.Max(cell => cell.Z) - minZ + 2;
        var size = MathF.Floor(MathF.Min(MathF.Min(area.Width / spanX, area.Height / spanZ), MaxCellSize));
        var left = MathF.Round(area.X + ((area.Width - (spanX * size)) / 2f));
        var top = MathF.Round(area.Y + ((area.Height - (spanZ * size)) / 2f));
        return new TravelMap(left, top, size, minX, minZ);
    }

    /// <summary>The teleporter number of the destination whose mark is under <paramref name="point"/> (canvas pixels), or null.</summary>
    public static int? SpotAt(TravelPage page, Vector2 point)
    {
        ArgumentNullException.ThrowIfNull(page);
        return page.Spots.FirstOrDefault(spot => spot.Target.Contains(point))?.Choice.Teleporter.Id;
    }

    /// <summary>The name of a destination: its teleporter's own name if it has one, else the place it marks.</summary>
    public static string Name(TravelChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        var plan = choice.Teleporter.Plan;
        return plan.Name ?? TeleporterLook.PlaceName(plan.Site);
    }

    /// <summary>The line under the chosen destination's name.</summary>
    public static string StateText(TravelState state) => state switch
    {
        TravelState.Here => "You are here",
        TravelState.Ready => "Ready",
        TravelState.Sealed => "Room sealed - wait for the fight to end",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    /// <summary>How the mark of <paramref name="choice"/> looks with <paramref name="selected"/> chosen.</summary>
    public static TravelMark MarkOf(TravelChoice choice, int? selected)
    {
        ArgumentNullException.ThrowIfNull(choice);
        return choice.State switch
        {
            TravelState.Here => TravelMark.Here,
            TravelState.Sealed => TravelMark.Sealed,
            _ when choice.Teleporter.Id == selected => TravelMark.Chosen,
            _ => TravelMark.Ready,
        };
    }

    private TravelSpot Spot(TravelChoice choice, TravelMap map)
    {
        var centre = map.CellCentre(choice.Teleporter.Plan.X, choice.Teleporter.Plan.Z);
        var target = new MenuRect(centre.X - MarkReach, centre.Y - MarkReach, 2 * MarkReach, 2 * MarkReach);
        var name = new MenuText(Name(choice), (int)MathF.Round(centre.X + MarkReach + 3), TopFor(centre.Y, 1), 1, MenuTextColor.Second);
        return new TravelSpot(choice, centre, target, name);
    }

    private MenuText Centred(string text, float centreX, float centreY, int scale, MenuTextColor color) =>
        new(text, (int)MathF.Round(centreX - (_width(text, scale) / 2f)), TopFor(centreY, scale), scale, color);

    private int TopFor(float centreY, int scale) => (int)MathF.Round(centreY - (_capHeight * scale / 2f));

    private static MenuRect Moved(MenuRect rect, Vector2 shift) => new(rect.X + shift.X, rect.Y + shift.Y, rect.Width, rect.Height);
}

/// <summary>
/// The selection in the travel window (issue #480): it only ever rests on a destination the player can travel to - not on
/// the teleporter they stand on, not on a sealed one. Arrows and the D-pad step through them in number order, round.
/// </summary>
public static class TravelCursor
{
    /// <summary>The first destination the player can travel to, or null if there is none.</summary>
    public static int? First(IReadOnlyList<TravelChoice> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        return choices.FirstOrDefault(choice => choice.CanTravel)?.Teleporter.Id;
    }

    /// <summary>
    /// <paramref name="selected"/> if it is still a destination; otherwise the first one (a room sealed meanwhile, or the
    /// selection was never set).
    /// </summary>
    public static int? Keep(IReadOnlyList<TravelChoice> choices, int? selected)
    {
        ArgumentNullException.ThrowIfNull(choices);
        return choices.Any(choice => choice.CanTravel && choice.Teleporter.Id == selected) ? selected : First(choices);
    }

    /// <summary>The next (<paramref name="step"/> +1) or previous (-1) destination after <paramref name="selected"/>, round.</summary>
    public static int? Step(IReadOnlyList<TravelChoice> choices, int? selected, int step)
    {
        ArgumentNullException.ThrowIfNull(choices);
        var free = choices.Where(choice => choice.CanTravel).Select(choice => choice.Teleporter.Id).ToList();
        if (free.Count == 0)
        {
            return null;
        }

        var at = selected is { } id ? free.IndexOf(id) : -1;
        if (at < 0)
        {
            return free[0];
        }

        return free[(((at + Math.Sign(step)) % free.Count) + free.Count) % free.Count];
    }

    /// <summary>The destination a click on <paramref name="target"/> selects: itself if the player can travel there, else nothing changes.</summary>
    public static int? Click(IReadOnlyList<TravelChoice> choices, int? selected, int? target)
    {
        ArgumentNullException.ThrowIfNull(choices);
        return choices.Any(choice => choice.CanTravel && choice.Teleporter.Id == target) ? target : selected;
    }
}
