namespace SilverAndAsh.Core.Presentation;

/// <summary>A rectangle on the screen in pixels.</summary>
public readonly record struct ScreenRect(float X, float Y, float Width, float Height);

/// <summary>
/// Fits a picture into a window as large as possible without distorting it.
/// If the shapes do not match, black bars fill the rest ("letterbox" above/below, "pillarbox" left/right).
/// </summary>
public static class Letterbox
{
    /// <summary>
    /// Where to draw a picture of <paramref name="sourceWidth"/> × <paramref name="sourceHeight"/>
    /// inside a window of <paramref name="targetWidth"/> × <paramref name="targetHeight"/>: centred, aspect ratio kept.
    /// </summary>
    public static ScreenRect Fit(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);

        if (targetWidth <= 0 || targetHeight <= 0)
        {
            // Minimised window: nothing to draw.
            return new ScreenRect(0f, 0f, 0f, 0f);
        }

        // The smaller of the two scale factors is the one that still fits in both directions.
        var scale = MathF.Min((float)targetWidth / sourceWidth, (float)targetHeight / sourceHeight);
        var width = sourceWidth * scale;
        var height = sourceHeight * scale;

        return new ScreenRect((targetWidth - width) / 2f, (targetHeight - height) / 2f, width, height);
    }
}
