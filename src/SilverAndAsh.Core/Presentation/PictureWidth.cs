namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// How wide the low-resolution picture of the 3D world is (issue #350). Its height stays fixed (480 lines); the width
/// follows the shape of the window, so a wider screen sees more to the sides - no bars, nothing stretched.
/// 16:9 gives 854, 21:9 gives 1120, 16:10 gives 768, 4:3 gives 640.
/// </summary>
/// <remarks>
/// Pure arithmetic without a window, so it can be tested here; the game (SilverAndAsh.Game) only asks for the number.
/// </remarks>
public static class PictureWidth
{
    /// <summary>
    /// The narrowest picture shape: 5:4, the shape of the oldest common monitors. Narrower windows (e.g. dragged
    /// upright) get bars above and below instead of an ever thinner view.
    /// </summary>
    public const float NarrowestAspect = 5f / 4f;

    /// <summary>
    /// The widest picture shape: 32:9, the widest common monitors (5120 x 1440). Wider windows get bars left and right;
    /// beyond this the edges of the view would be stretched far too much (horizontal field of view about 136 degrees).
    /// </summary>
    public const float WidestAspect = 32f / 9f;

    /// <summary>The shape used while the window has no size (minimised): 16:9, the shape of the photo window.</summary>
    public const float StandardAspect = 16f / 9f;

    /// <summary>
    /// Width of a picture <paramref name="pictureHeight"/> lines high that fills a window of
    /// <paramref name="windowWidth"/> x <paramref name="windowHeight"/>, rounded to an even number of pixels
    /// (so the middle of the picture lies between two pixels, and 16:9 gives 854 like before).
    /// Window shapes outside <see cref="NarrowestAspect"/> .. <see cref="WidestAspect"/> are held at the limit.
    /// </summary>
    public static int For(int windowWidth, int windowHeight, int pictureHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pictureHeight);

        var aspect = windowWidth > 0 && windowHeight > 0
            ? Math.Clamp((double)windowWidth / windowHeight, NarrowestAspect, WidestAspect)
            : StandardAspect;

        return (int)Math.Round(pictureHeight * aspect / 2d, MidpointRounding.AwayFromZero) * 2;
    }
}
