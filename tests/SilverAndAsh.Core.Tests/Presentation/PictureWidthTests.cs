using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

public class PictureWidthTests
{
    private const int Lines = 480;

    [Theory]
    [InlineData(1280, 720, 854)]   // 16:9, the photo window
    [InlineData(1920, 1080, 854)]  // 16:9
    [InlineData(3840, 2160, 854)]  // 16:9 in 4K: the same picture, only scaled up further
    [InlineData(2520, 1080, 1120)] // 21:9
    [InlineData(1920, 1200, 768)]  // 16:10
    [InlineData(1680, 1050, 768)]  // 16:10
    [InlineData(1024, 768, 640)]   // 4:3
    [InlineData(1600, 1200, 640)]  // 4:3
    public void The_width_follows_the_shape_of_the_window(int windowWidth, int windowHeight, int expected)
    {
        Assert.Equal(expected, PictureWidth.For(windowWidth, windowHeight, Lines));
    }

    [Fact]
    public void A_real_ultra_wide_monitor_sees_more_to_the_sides()
    {
        // 3440 x 1440 is sold as 21:9 but is a little wider (43:18).
        Assert.Equal(1146, PictureWidth.For(3440, 1440, Lines));
    }

    [Fact]
    public void Inside_the_limits_the_picture_has_the_shape_of_the_window()
    {
        // Nothing stretched, no bars: the picture is at most one pixel wider or narrower than the exact shape.
        for (var width = 600; width <= 5120; width += 37)
        {
            foreach (var height in new[] { 480, 720, 768, 1050, 1080, 1440 })
            {
                var aspect = (double)width / height;
                if (aspect < PictureWidth.NarrowestAspect || aspect > PictureWidth.WidestAspect)
                {
                    continue;
                }

                var picture = PictureWidth.For(width, height, Lines);
                Assert.True(
                    Math.Abs(picture - (Lines * aspect)) <= 1.0001,
                    $"{width} x {height}: picture {picture} x {Lines}, exact {Lines * aspect:0.00}");
            }
        }
    }

    [Fact]
    public void The_width_is_always_even()
    {
        for (var width = 100; width <= 6000; width += 13)
        {
            Assert.Equal(0, PictureWidth.For(width, 1080, Lines) % 2);
        }
    }

    [Theory]
    [InlineData(1080, 1920)] // a monitor turned upright
    [InlineData(400, 1000)]  // a window dragged very narrow
    [InlineData(1280, 1024)] // exactly 5:4
    public void Narrow_windows_are_held_at_five_to_four(int windowWidth, int windowHeight)
    {
        Assert.Equal(600, PictureWidth.For(windowWidth, windowHeight, Lines));
    }

    [Theory]
    [InlineData(5120, 1440)] // exactly 32:9
    [InlineData(10000, 1000)]
    [InlineData(3000, 200)]  // a window dragged very flat
    public void Wide_windows_are_held_at_thirty_two_to_nine(int windowWidth, int windowHeight)
    {
        Assert.Equal(1706, PictureWidth.For(windowWidth, windowHeight, Lines));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1280, 0)]
    [InlineData(0, 720)]
    public void A_minimised_window_keeps_the_standard_shape(int windowWidth, int windowHeight)
    {
        Assert.Equal(854, PictureWidth.For(windowWidth, windowHeight, Lines));
    }

    [Fact]
    public void Other_picture_heights_follow_the_same_rule()
    {
        // Picture variant 4 (decision record 0010) has 360 lines: 16:9 gives 640 like before.
        Assert.Equal(640, PictureWidth.For(1280, 720, 360));
        Assert.Equal(480, PictureWidth.For(1024, 768, 360));
    }

    [Fact]
    public void A_picture_without_lines_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PictureWidth.For(1280, 720, 0));
    }
}
