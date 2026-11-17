using System.Globalization;

namespace SilverAndAsh.Core.Diagnostics;

/// <summary>
/// What a frame-time measurement (<c>--bench</c>, issue #233) comes to: mean, median and the 99th percentile of the
/// frame times in milliseconds. The 99th percentile shows the rare slow frames a mean hides.
/// </summary>
public sealed record FrameTimeStatistics(int Count, double MeanMilliseconds, double MedianMilliseconds, double Percentile99Milliseconds)
{
    /// <summary>Works out the numbers from the measured frame times (milliseconds, in any order).</summary>
    /// <exception cref="ArgumentException">If there are no times, or one is negative, NaN or infinite.</exception>
    public static FrameTimeStatistics From(IReadOnlyCollection<double> frameMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(frameMilliseconds);
        if (frameMilliseconds.Count == 0)
        {
            throw new ArgumentException("A measurement needs at least one frame time.", nameof(frameMilliseconds));
        }

        if (frameMilliseconds.Any(time => !double.IsFinite(time) || time < 0))
        {
            throw new ArgumentException("Frame times must be finite and not negative.", nameof(frameMilliseconds));
        }

        var sorted = frameMilliseconds.Order().ToArray();
        var middle = sorted.Length / 2;
        var median = sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
        return new FrameTimeStatistics(sorted.Length, sorted.Average(), median, NearestRank(sorted, 0.99));
    }

    /// <summary>The frame rate the mean frame time stands for.</summary>
    public double MeanFramesPerSecond => MeanMilliseconds > 0 ? 1000 / MeanMilliseconds : double.PositiveInfinity;

    /// <summary>One line for the console and the log, e.g. "1000 frames: mean 1.23 ms (813 fps), median 1.20 ms, 99th percentile 1.80 ms".</summary>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Count} frames: mean {MeanMilliseconds:0.000} ms ({MeanFramesPerSecond:0} fps), median {MedianMilliseconds:0.000} ms, 99th percentile {Percentile99Milliseconds:0.000} ms");

    // Nearest-rank method: the smallest time that at least this share of all frames does not exceed.
    private static double NearestRank(double[] sorted, double share) =>
        sorted[Math.Clamp((int)Math.Ceiling(share * sorted.Length) - 1, 0, sorted.Length - 1)];
}
