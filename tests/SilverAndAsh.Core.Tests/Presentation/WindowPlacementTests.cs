using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

public class WindowPlacementTests
{
    // The monitor setup where the bug was found: a small monitor left of the main monitor
    // (negative coordinates) and a small one to the right.
    private static readonly DesktopRect Main = new(0, 0, 3440, 1440);
    private static readonly DesktopRect Left = new(-1024, 92, 1024, 768);
    private static readonly DesktopRect Right = new(3440, 0, 640, 480);
    private static readonly DesktopRect[] Monitors = [Right, Left, Main];

    [Fact]
    public void A_window_on_the_main_monitor_stays_where_it_is()
    {
        var window = new DesktopRect(1080, 360, 1280, 720);

        Assert.Null(WindowPlacement.CorrectedPosition(window, Monitors, Main));
    }

    [Fact]
    public void A_window_on_a_monitor_with_negative_coordinates_stays_where_it_is()
    {
        var window = new DesktopRect(-900, 200, 800, 450);

        Assert.Null(WindowPlacement.CorrectedPosition(window, Monitors, Main));
    }

    [Fact]
    public void A_window_far_outside_every_monitor_is_centred_on_the_main_monitor()
    {
        // Exactly what was measured in the bug: x = 32767.
        var window = new DesktopRect(32767, 70, 1280, 720);

        var corrected = WindowPlacement.CorrectedPosition(window, Monitors, Main);

        Assert.Equal((1080, 360), corrected);
    }

    [Fact]
    public void A_position_close_to_the_largest_number_does_not_overflow()
    {
        // Measured with the fix in place: Raylib reported x = 2147482496. Without care, x + half the width
        // would wrap around to a large negative number.
        var window = new DesktopRect(int.MaxValue - 100, 0, 1280, 720);
        var everything = new DesktopRect(int.MinValue, 0, int.MaxValue, 2000);   // ends at -1

        Assert.Equal((1080, 360), WindowPlacement.CorrectedPosition(window, [everything, Main], Main));
    }

    [Fact]
    public void A_window_hanging_mostly_off_the_edge_is_brought_back()
    {
        // Centre at (3640, 1260): right of the main monitor and below the small right monitor - on no monitor.
        var window = new DesktopRect(3000, 900, 1280, 720);

        Assert.NotNull(WindowPlacement.CorrectedPosition(window, Monitors, Main));
    }

    [Fact]
    public void A_window_bigger_than_the_main_monitor_keeps_its_title_bar_on_screen()
    {
        var small = new DesktopRect(-1024, 92, 1024, 768);
        var window = new DesktopRect(50000, 50000, 1280, 900);   // wider AND taller than the monitor

        var corrected = WindowPlacement.CorrectedPosition(window, [small], small);

        Assert.Equal((-1024, 92), corrected);
    }

    [Fact]
    public void Contains_counts_the_left_and_top_edge_but_not_the_right_and_bottom_edge()
    {
        Assert.True(Left.Contains(-1024, 92));
        Assert.True(Left.Contains(-1, 859));
        Assert.False(Left.Contains(0, 100));
        Assert.False(Left.Contains(-500, 860));
    }
}
