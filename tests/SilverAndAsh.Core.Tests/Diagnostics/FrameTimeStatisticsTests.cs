using SilverAndAsh.Core.Diagnostics;

namespace SilverAndAsh.Core.Tests.Diagnostics;

public class FrameTimeStatisticsTests
{
    [Fact]
    public void Mean_median_and_percentile_of_an_odd_count()
    {
        var statistics = FrameTimeStatistics.From([3.0, 1.0, 2.0, 10.0, 4.0]);

        Assert.Equal(5, statistics.Count);
        Assert.Equal(4.0, statistics.MeanMilliseconds, 9);
        Assert.Equal(3.0, statistics.MedianMilliseconds, 9);
        Assert.Equal(10.0, statistics.Percentile99Milliseconds, 9);
    }

    [Fact]
    public void The_median_of_an_even_count_lies_between_the_middle_two()
    {
        Assert.Equal(2.5, FrameTimeStatistics.From([4.0, 1.0, 3.0, 2.0]).MedianMilliseconds, 9);
    }

    [Fact]
    public void The_99th_percentile_of_1000_frames_ignores_only_the_10_slowest()
    {
        // 1000 frames: 1 ms ... 1000 ms. 99 % of them take at most 990 ms.
        var times = Enumerable.Range(1, 1000).Select(time => (double)time).Reverse().ToArray();

        var statistics = FrameTimeStatistics.From(times);

        Assert.Equal(990.0, statistics.Percentile99Milliseconds, 9);
        Assert.Equal(500.5, statistics.MedianMilliseconds, 9);
        Assert.Equal(500.5, statistics.MeanMilliseconds, 9);
    }

    [Fact]
    public void A_single_frame_is_all_three()
    {
        var statistics = FrameTimeStatistics.From([7.5]);

        Assert.Equal(7.5, statistics.MeanMilliseconds, 9);
        Assert.Equal(7.5, statistics.MedianMilliseconds, 9);
        Assert.Equal(7.5, statistics.Percentile99Milliseconds, 9);
    }

    [Fact]
    public void A_few_slow_frames_raise_the_mean_and_the_percentile_but_not_the_median()
    {
        var times = Enumerable.Repeat(2.0, 980).Concat(Enumerable.Repeat(50.0, 20)).ToArray();

        var statistics = FrameTimeStatistics.From(times);

        Assert.Equal(2.0, statistics.MedianMilliseconds, 9);
        Assert.Equal(50.0, statistics.Percentile99Milliseconds, 9);
        Assert.Equal(2.96, statistics.MeanMilliseconds, 9);
    }

    [Fact]
    public void The_line_names_all_numbers_with_a_point_as_decimal_separator()
    {
        var line = FrameTimeStatistics.From([2.0, 2.0, 4.0, 8.0]).ToString();

        Assert.Equal("4 frames: mean 4.000 ms (250 fps), median 3.000 ms, 99th percentile 8.000 ms", line);
    }

    [Fact]
    public void No_frames_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => FrameTimeStatistics.From([]));
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Impossible_times_are_rejected(double time)
    {
        Assert.Throws<ArgumentException>(() => FrameTimeStatistics.From([1.0, time]));
    }
}
