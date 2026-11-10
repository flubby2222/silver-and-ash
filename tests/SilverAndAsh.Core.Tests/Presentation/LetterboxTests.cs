using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

public class LetterboxTests
{
    private const float Tolerance = 0.01f;

    [Fact]
    public void Same_shape_fills_the_whole_window()
    {
        var rect = Letterbox.Fit(854, 480, 1708, 960);

        AssertRect(new ScreenRect(0f, 0f, 1708f, 960f), rect);
    }

    [Fact]
    public void A_window_of_almost_the_same_shape_is_filled()
    {
        // 854 x 480 is very close to 16:9 - a 1280 x 720 window has only a hairline bar.
        var rect = Letterbox.Fit(854, 480, 1280, 720);

        Assert.Equal(1280f, rect.Width, 1f);
        Assert.Equal(720f, rect.Height, 1f);
    }

    [Fact]
    public void A_wider_window_gets_bars_left_and_right()
    {
        // Ultra-wide 21:9 window.
        var rect = Letterbox.Fit(854, 480, 3440, 1440);

        Assert.Equal(1440f, rect.Height, Tolerance);
        Assert.Equal(854f * 1440f / 480f, rect.Width, Tolerance);
        Assert.Equal((3440f - rect.Width) / 2f, rect.X, Tolerance);
        Assert.Equal(0f, rect.Y, Tolerance);
    }

    [Fact]
    public void A_taller_window_gets_bars_above_and_below()
    {
        var rect = Letterbox.Fit(854, 480, 1000, 1000);

        Assert.Equal(1000f, rect.Width, Tolerance);
        Assert.Equal(480f * 1000f / 854f, rect.Height, Tolerance);
        Assert.Equal(0f, rect.X, Tolerance);
        Assert.Equal((1000f - rect.Height) / 2f, rect.Y, Tolerance);
    }

    [Fact]
    public void The_aspect_ratio_is_always_kept()
    {
        foreach (var (w, h) in new[] { (800, 600), (1920, 1080), (3440, 1440), (300, 900) })
        {
            var rect = Letterbox.Fit(854, 480, w, h);
            Assert.Equal(854f / 480f, rect.Width / rect.Height, 0.001f);
        }
    }

    [Fact]
    public void A_minimised_window_draws_nothing()
    {
        var rect = Letterbox.Fit(854, 480, 0, 0);

        Assert.Equal(0f, rect.Width);
        Assert.Equal(0f, rect.Height);
    }

    private static void AssertRect(ScreenRect expected, ScreenRect actual)
    {
        Assert.Equal(expected.X, actual.X, Tolerance);
        Assert.Equal(expected.Y, actual.Y, Tolerance);
        Assert.Equal(expected.Width, actual.Width, Tolerance);
        Assert.Equal(expected.Height, actual.Height, Tolerance);
    }
}
