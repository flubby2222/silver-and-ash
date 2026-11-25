using Raylib_cs;

namespace SilverAndAsh.Game.Rendering;

/// <summary>The pixels of a Raylib image as plain bytes, for checks without Raylib (smoke test, issue #329).</summary>
internal static class ImagePixels
{
    /// <summary>Red, green, blue, alpha of every pixel, row by row - whatever format the image is stored in.</summary>
    public static unsafe byte[] Rgba(Image image)
    {
        var count = image.Width * image.Height;
        var colours = Raylib.LoadImageColors(image);
        try
        {
            // Raylib's Color is exactly these four bytes.
            return new ReadOnlySpan<byte>(colours, count * sizeof(Color)).ToArray();
        }
        finally
        {
            Raylib.UnloadImageColors(colours);
        }
    }
}
