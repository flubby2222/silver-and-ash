using System.Globalization;
using Raylib_cs;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// One way of drawing the 3D world, to compare pictures (issue #234). Decision record 0010 leaves open whether the game
/// gets 15-bit colour with a dot pattern, a fade-out after the pattern instead of fog, and a lower resolution.
/// </summary>
/// <param name="Number">1 to 4, as chosen with <c>--look</c>.</param>
/// <param name="Label">One line for the comparison pictures.</param>
/// <param name="RenderHeight">Height of the low-resolution picture; its width follows the window (PictureWidth, issue #350).</param>
/// <param name="Quantize">Rasterise every colour to 5 bits per channel with the 4 x 4 pattern.</param>
/// <param name="PatternFade">Leave pixels out after the pattern instead of mixing in fog.</param>
internal sealed record LookVariant(int Number, string Label, int RenderHeight, bool Quantize, bool PatternFade)
{
    /// <summary>
    /// Colour the 3D world is cleared with. With the pattern it is moved to a 5-bit step, so the far end of the view
    /// matches the fully fogged colour exactly and gets no pattern of its own.
    /// </summary>
    public Color BackgroundColor => Quantize
        ? new Color(
            Dithering.SnapToStep(LookSettings.BackgroundColor.R),
            Dithering.SnapToStep(LookSettings.BackgroundColor.G),
            Dithering.SnapToStep(LookSettings.BackgroundColor.B),
            (byte)255)
        : LookSettings.BackgroundColor;
}

/// <summary>
/// The picture variants of issue #234, with their values in one place next to <see cref="LookSettings"/>.
/// The game uses <see cref="Default"/>, in the debug and the release version; the others only with <c>--look</c> or
/// <c>--photo compare</c> (debug version).
/// </summary>
internal static class LookVariants
{
    /// <summary>
    /// Distance in metres where the pattern fade starts leaving out pixels. With the fog start (3.25 m) over 40 % of a
    /// ghoul's pixels were missing at 10 m and thin limbs fell apart (decision record 0010) - so it starts later.
    /// </summary>
    public const float FadeStart = 9f;

    /// <summary>Distance in metres where every pixel is left out: the end of the fog, the range of the lantern.</summary>
    public const float FadeEnd = LookSettings.FogEnd;

    /// <summary>
    /// The lower resolution of variant 4: the pattern gets coarser, targets get smaller. 640 x 360 at 16:9; the width
    /// follows the window like in the game (issue #350).
    /// </summary>
    public const int LowRenderHeight = 360;

    /// <summary>Variants 1 to 4, in order.</summary>
    public static IReadOnlyList<LookVariant> All { get; } =
    [
        new(1, string.Create(CultureInfo.InvariantCulture, $"1  no grain: fog, full colour, {LookSettings.RenderHeight} lines"),
            LookSettings.RenderHeight, Quantize: false, PatternFade: false),
        new(2, "2  15 bit + 4x4 pattern, fog",
            LookSettings.RenderHeight, Quantize: true, PatternFade: false),
        new(3, string.Create(CultureInfo.InvariantCulture, $"3  15 bit + pattern fade {FadeStart:0} - {FadeEnd:0.#} m, no fog"),
            LookSettings.RenderHeight, Quantize: true, PatternFade: true),
        new(4, string.Create(CultureInfo.InvariantCulture, $"4  like 3 in {LowRenderHeight} lines"),
            LowRenderHeight, Quantize: true, PatternFade: true),
    ];

    /// <summary>
    /// The game as it is: variant 2, 15-bit colour with the fixed 4 x 4 pattern and fog (issue #237) - chosen by the
    /// human from comparison pictures (decision record 0010). The one place that decides the standard picture.
    /// </summary>
    public static LookVariant Default => All[1];

    /// <summary>Variant <paramref name="number"/> (1 to 4), or the default for null.</summary>
    public static LookVariant Get(int? number) => number is { } n ? All[n - 1] : Default;
}
