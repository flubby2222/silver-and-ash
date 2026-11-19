namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// Where a placed HUD part comes from in the parts picture and where it lands on the canvas (issue #409).
/// <see cref="HudLayout"/> always arranges with the parts table of today (854 x 480); the picture may be the atlas in
/// double resolution (issue #402), whose parts have the same names at double position and size. A part then takes the
/// double rectangle from the picture and is drawn at half scale, so it keeps its place and size on the screen. Only
/// the signs (<c>icon_*</c>) are larger by design: they keep their middle.
/// </summary>
public sealed class HudAtlas
{
    private readonly HudParts _layout;

    /// <param name="layout">The table <see cref="HudLayout"/> arranges with.</param>
    /// <param name="picture">The table of the picture drawn from: <paramref name="layout"/> itself or its double.</param>
    /// <param name="scale">How much finer <paramref name="picture"/> is than <paramref name="layout"/> (1 or 2).</param>
    public HudAtlas(HudParts layout, HudParts picture, int scale)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(picture);
        ArgumentOutOfRangeException.ThrowIfLessThan(scale, 1);
        _layout = layout;
        Picture = picture;
        Scale = scale;
    }

    /// <summary>The table of the picture drawn from.</summary>
    public HudParts Picture { get; }

    /// <summary>How much finer the picture is than the canvas.</summary>
    public int Scale { get; }

    /// <summary>The rectangle in the picture and the rectangle on the canvas of <paramref name="placed"/>.</summary>
    public MenuPiece Piece(HudPlacedPart placed)
    {
        var plain = _layout[placed.Name];
        var fine = Picture[placed.Name];

        // Bars are cut from the left (fill level); a cut part takes the same share of the finer picture.
        var sourceWidth = placed.Width < plain.Width ? placed.Width * Scale : fine.Width;

        // Zero for parts that are exactly doubled; signs keep their middle.
        var dx = ((float)fine.LayoutX / Scale) - plain.LayoutX;
        var dy = ((float)fine.LayoutY / Scale) - plain.LayoutY;
        return new MenuPiece(
            new MenuRect(fine.X, fine.Y, sourceWidth, fine.Height),
            new MenuRect(placed.X + dx, placed.Y + dy, (float)sourceWidth / Scale, (float)fine.Height / Scale));
    }
}
