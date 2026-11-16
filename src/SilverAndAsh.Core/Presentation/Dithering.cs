namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The dot pattern ("ordered dithering") of the picture variants in issue #234 (decision record 0010, open question):
/// 15-bit colour (5 bits per channel) and fading out at the edge of the view by leaving pixels out.
/// The graphics card runs the same formulas in the level shader (assets/shaders/level.fs) for every pixel - here they
/// live once more in C#, so they can be tested. If you change one, change the other.
/// </summary>
public static class Dithering
{
    /// <summary>Size of the pattern: it repeats every 4 pixels to the right and down.</summary>
    public const int PatternSize = 4;

    /// <summary>Bits per colour channel after rasterising: 5, like the PlayStation's 15-bit picture.</summary>
    public const int BitsPerChannel = 5;

    /// <summary>Highest step of a channel with <see cref="BitsPerChannel"/> bits: 31.</summary>
    public const int HighestStep = (1 << BitsPerChannel) - 1;

    /// <summary>
    /// The 4 x 4 Bayer matrix, row by row: the order in which the 16 pixels of a block are "switched on".
    /// Neighbouring numbers lie far apart, so every share of switched-on pixels is spread evenly over the block.
    /// The shader holds the same 16 numbers in the same order (a test compares them).
    /// </summary>
    public static IReadOnlyList<int> BayerMatrix { get; } =
    [
        0, 8, 2, 10,
        12, 4, 14, 6,
        3, 11, 1, 9,
        15, 7, 13, 5,
    ];

    /// <summary>
    /// The threshold of the pattern at pixel (<paramref name="x"/>, <paramref name="y"/>) of the low-resolution picture:
    /// one of 16 values between 0 and 1 (never exactly 0 or 1). It depends only on the position in the picture, so
    /// the pattern stays still on the screen while the camera moves.
    /// </summary>
    public static float Threshold(int x, int y)
    {
        var column = ((x % PatternSize) + PatternSize) % PatternSize;
        var row = ((y % PatternSize) + PatternSize) % PatternSize;
        return (BayerMatrix[row * PatternSize + column] + 0.5f) / (PatternSize * PatternSize);
    }

    /// <summary>
    /// Rasterises one colour channel (0..1) to 5 bits: the result is one of the 32 steps 0, 1/31, ... 1.
    /// Between two steps the threshold decides - over a 4 x 4 block the share of pixels on the upper step matches how
    /// far the colour lies above the lower step, so the eye mixes the tone back.
    /// </summary>
    public static float Quantize(float channel, float threshold)
    {
        var step = MathF.Floor(Math.Clamp(channel, 0f, 1f) * HighestStep + threshold);
        return Math.Clamp(step, 0f, HighestStep) / HighestStep;
    }

    /// <summary>
    /// A colour channel (0..1) moved to the nearest of the 32 steps, exactly (e.g. 11/255 -> 1/31).
    /// For colours that must not get a pattern of their own, such as the fog colour: <see cref="Quantize"/> leaves a
    /// step as it is at every threshold. Hand it to the shader as this float, not as a byte - the byte of a step is
    /// rounded and can lie just below it.
    /// </summary>
    public static float SnapToStep(float channel) => MathF.Round(Math.Clamp(channel, 0f, 1f) * HighestStep) / HighestStep;

    /// <summary>
    /// A colour byte (0..255) moved to the nearest 5-bit step, as the graphics card stores that step (e.g. 11 -> 8).
    /// For the background colour, which is cleared directly and never goes through the shader.
    /// </summary>
    public static byte SnapToStep(byte channel) => (byte)MathF.Round(SnapToStep(channel / 255f) * 255f);

    /// <summary>
    /// How much of a point has faded out at the edge of the view: 0 (all pixels drawn) up to <paramref name="start"/>
    /// metres, 1 (none drawn) from <paramref name="end"/> metres on, with the same soft S-curve as the fog in between.
    /// </summary>
    public static float FadeAmount(float distance, float start, float end) => Lighting.FogAmount(distance, start, end);

    /// <summary>
    /// True if the pixel with this <paramref name="threshold"/> is left out (shows what lies behind it) at
    /// <paramref name="fade"/>: at fade 0 every pixel stays, at fade 1 every pixel goes.
    /// </summary>
    public static bool IsLeftOut(float fade, float threshold) => fade > threshold;

    /// <summary>Share (0..1) of the pixels of a 4 x 4 block that are left out at <paramref name="fade"/>.</summary>
    public static float LeftOutShare(float fade)
    {
        var count = 0;
        for (var y = 0; y < PatternSize; y++)
        {
            for (var x = 0; x < PatternSize; x++)
            {
                if (IsLeftOut(fade, Threshold(x, y)))
                {
                    count++;
                }
            }
        }

        return count / (float)(PatternSize * PatternSize);
    }
}
