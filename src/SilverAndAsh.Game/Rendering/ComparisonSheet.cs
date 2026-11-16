using Raylib_cs;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Puts four screenshots of the same scene together, 2 x 2, each with a label strip above it (picture variants,
/// issue #234). Debug tool for <c>--photo compare</c>.
/// </summary>
internal static class ComparisonSheet
{
    private const int LabelHeight = 44;
    private const int FontSize = 30;
    private static readonly Color LabelBackground = new(30, 30, 34, 255);
    private static readonly Color LabelColor = new(235, 235, 240, 255);

    /// <summary>Loads the pictures (all of the same size), puts them together left to right, top to bottom and saves the sheet.</summary>
    public static void Save(IReadOnlyList<(string Path, string Label)> pictures, string path)
    {
        var images = pictures.Select(picture => Raylib.LoadImage(picture.Path)).ToList();
        try
        {
            var width = images[0].Width;
            var height = images[0].Height;
            var sheet = Raylib.GenImageColor(width * 2, (height + LabelHeight) * 2, LabelBackground);
            for (var i = 0; i < images.Count; i++)
            {
                var left = i % 2 * width;
                var top = i / 2 * (height + LabelHeight);
                Raylib.ImageDrawText(ref sheet, pictures[i].Label, left + 12, top + (LabelHeight - FontSize) / 2, FontSize, LabelColor);
                Raylib.ImageDraw(
                    ref sheet, images[i],
                    new Rectangle(0, 0, images[i].Width, images[i].Height),
                    new Rectangle(left, top + LabelHeight, width, height),
                    Color.White);
            }

            Raylib.ExportImage(sheet, path);
            Raylib.UnloadImage(sheet);
        }
        finally
        {
            foreach (var image in images)
            {
                Raylib.UnloadImage(image);
            }
        }
    }
}
