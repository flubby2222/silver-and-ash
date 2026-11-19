namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The menu parts drawn from an atlas that may be finer than the canvas (issue #409). The double atlas (issue #402)
/// gives every measure - parts, shapes, slices - at twice the canvas size: the wanted rectangles are doubled, cut from
/// it by <see cref="MenuParts"/>, and the targets halved again. So every piece keeps its place and size on the screen
/// and only the picture gets finer. With <see cref="Scale"/> 1 it is <see cref="MenuParts"/> unchanged.
/// </summary>
public sealed class MenuAtlas
{
    /// <param name="parts">The table of the picture drawn from.</param>
    /// <param name="scale">How much finer the picture is than the canvas (1 or 2).</param>
    public MenuAtlas(MenuParts parts, int scale)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentOutOfRangeException.ThrowIfLessThan(scale, 1);
        Parts = parts;
        Scale = scale;
    }

    /// <summary>The table of the picture drawn from (also its colours).</summary>
    public MenuParts Parts { get; }

    /// <summary>How much finer the picture is than the canvas.</summary>
    public int Scale { get; }

    public IReadOnlyList<MenuPiece> Panel(MenuRect panel) => Down(Parts.Panel(Up(panel)));

    public IReadOnlyList<MenuPiece> Cursor(MenuRect cursor, bool small = false) => Down(Parts.Cursor(Up(cursor), small));

    public MenuPiece Gable(MenuRect panel) => Down(Parts.Gable(Up(panel)));

    public SliderPieces Slider(float left, float centreY, float fraction)
    {
        var pieces = Parts.Slider(left * Scale, centreY * Scale, fraction);
        return new SliderPieces(
            Down(pieces.Empty), pieces.Full is { } full ? Down(full) : null, Down(pieces.Knob), pieces.EdgeX / Scale);
    }

    public MenuPiece Tab(int index, bool open, float left, float top) => Down(Parts.Tab(index, open, left * Scale, top * Scale));

    private MenuRect Up(MenuRect rect) => new(rect.X * Scale, rect.Y * Scale, rect.Width * Scale, rect.Height * Scale);

    private MenuPiece Down(MenuPiece piece) => piece with
    {
        Target = new MenuRect(piece.Target.X / Scale, piece.Target.Y / Scale, piece.Target.Width / Scale, piece.Target.Height / Scale),
    };

    private MenuPiece[] Down(IReadOnlyList<MenuPiece> pieces) => pieces.Select(Down).ToArray();
}
