using System.Text.Json;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// One picture part of the HUD (issue #369): its rectangle in the parts picture and where the approved design put its
/// top left corner (854 x 480 design pixels). Parts are drawn 1:1, never stretched.
/// </summary>
public readonly record struct HudPart(string Name, int X, int Y, int Width, int Height, int LayoutX, int LayoutY)
{
    /// <summary>
    /// The body of a frame inside the part (issue #387): the silver rim with the dark ground in it, without the ornament
    /// around (rivets, gable). Measured from the part's top left corner; the whole part for a part that is no frame.
    /// </summary>
    public HudRect Body { get; init; } = new(0, 0, Width, Height);
}

/// <summary>A colour of the HUD from the parts table (red, green, blue).</summary>
public readonly record struct HudColor(byte R, byte G, byte B);

/// <summary>
/// The measure table of the HUD parts, <c>assets/hud/hud_parts.json</c> (issues #341, #369): every part with its
/// rectangle and design position, plus the colours the game draws itself (texts, the reload veil).
/// Pure reading - the Game project draws the parts from the picture the table names.
/// </summary>
public sealed class HudParts
{
    private readonly Dictionary<string, HudPart> _parts;

    private HudParts(string image, int designWidth, int designHeight, Dictionary<string, HudPart> parts,
        HudColor textMain, HudColor textSecond, HudColor textShadow, HudColor textPickup, HudColor textHint)
    {
        Image = image;
        DesignWidth = designWidth;
        DesignHeight = designHeight;
        _parts = parts;
        TextHint = textHint;
        TextMain = textMain;
        TextSecond = textSecond;
        TextShadow = textShadow;
        TextPickup = textPickup;
    }

    /// <summary>File name of the parts picture, next to the table.</summary>
    public string Image { get; }

    /// <summary>Width of the design the layout positions belong to (854).</summary>
    public int DesignWidth { get; }

    /// <summary>Height of the design the layout positions belong to (480).</summary>
    public int DesignHeight { get; }

    /// <summary>The colour of numbers and names.</summary>
    public HudColor TextMain { get; }

    /// <summary>The colour of quieter texts (e.g. rounds while reloading).</summary>
    public HudColor TextSecond { get; }

    /// <summary>The dark shadow one pixel below and right of every text.</summary>
    public HudColor TextShadow { get; }

    /// <summary>Gold: only the pickup toast.</summary>
    public HudColor TextPickup { get; }

    /// <summary>Yellow, the gold of the experience bar: only the hint to level up (issue #387).</summary>
    public HudColor TextHint { get; }

    /// <summary>The reload veil over a weapon picture (issue #370): its colour and opacity (0 to 1) ...</summary>
    public HudColor ReloadVeil { get; private init; }

    /// <inheritdoc cref="ReloadVeil"/>
    public float ReloadVeilAlpha { get; private init; }

    /// <summary>... and the colour and opacity of its light front edge.</summary>
    public HudColor ReloadVeilEdge { get; private init; }

    /// <inheritdoc cref="ReloadVeilEdge"/>
    public float ReloadVeilEdgeAlpha { get; private init; }

    /// <summary>Every part, in the order of the table.</summary>
    public IReadOnlyCollection<HudPart> All => _parts.Values;

    /// <summary>The part named <paramref name="name"/>.</summary>
    /// <exception cref="KeyNotFoundException">If the table has no such part.</exception>
    public HudPart this[string name] =>
        _parts.TryGetValue(name, out var part) ? part : throw new KeyNotFoundException($"The HUD has no part '{name}'.");

    /// <summary>Whether the table has a part named <paramref name="name"/>.</summary>
    public bool Contains(string name) => _parts.ContainsKey(name);

    /// <summary>Reads the table written by the HUD parts script.</summary>
    public static HudParts Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var parts = new Dictionary<string, HudPart>();
        foreach (var entry in root.GetProperty("parts").EnumerateArray())
        {
            var part = new HudPart(
                entry.GetProperty("name").GetString() ?? string.Empty,
                entry.GetProperty("x").GetInt32(),
                entry.GetProperty("y").GetInt32(),
                entry.GetProperty("w").GetInt32(),
                entry.GetProperty("h").GetInt32(),
                entry.GetProperty("layoutX").GetInt32(),
                entry.GetProperty("layoutY").GetInt32());
            if (entry.TryGetProperty("body", out var body))
            {
                part = part with { Body = new HudRect(body[0].GetInt32(), body[1].GetInt32(), body[2].GetInt32(), body[3].GetInt32()) };
            }

            if (!parts.TryAdd(part.Name, part))
            {
                throw new FormatException($"The HUD part '{part.Name}' is in the table twice.");
            }
        }

        var game = root.GetProperty("game");
        var text = game.GetProperty("text");
        var veil = game.GetProperty("reloadVeil");
        return new HudParts(
            root.GetProperty("image").GetString() ?? string.Empty,
            root.GetProperty("designWidth").GetInt32(),
            root.GetProperty("designHeight").GetInt32(),
            parts,
            ColorOf(text.GetProperty("main")),
            ColorOf(text.GetProperty("second")),
            ColorOf(text.GetProperty("shadow")),
            ColorOf(text.GetProperty("pickup")),
            ColorOf(text.GetProperty("hint")))
        {
            ReloadVeil = ColorOf(veil.GetProperty("color")),
            ReloadVeilAlpha = Math.Clamp(veil.GetProperty("alpha").GetSingle(), 0f, 1f),
            ReloadVeilEdge = ColorOf(veil.GetProperty("edgeColor")),
            ReloadVeilEdgeAlpha = Math.Clamp(veil.GetProperty("edgeAlpha").GetSingle(), 0f, 1f),
        };
    }

    private static HudColor ColorOf(JsonElement rgb) =>
        new(rgb[0].GetByte(), rgb[1].GetByte(), rgb[2].GetByte());
}
