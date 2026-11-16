using SilverAndAsh.Core.Input;

namespace SilverAndAsh.Core.Tests.Input;

public class CaptureClickFilterTests
{
    [Fact]
    public void Without_a_capture_click_the_button_passes_through()
    {
        var filter = new CaptureClickFilter();

        Assert.True(filter.Filter(true));
        Assert.False(filter.Filter(false));
    }

    [Fact]
    public void The_capture_click_does_not_count_while_it_is_held()
    {
        var filter = new CaptureClickFilter();

        filter.MouseCaptured();

        Assert.False(filter.Filter(true));
        Assert.False(filter.Filter(true));   // still held a few frames later
    }

    [Fact]
    public void After_letting_go_the_next_click_counts()
    {
        var filter = new CaptureClickFilter();
        filter.MouseCaptured();
        filter.Filter(true);

        Assert.False(filter.Filter(false));  // let go
        Assert.True(filter.Filter(true));    // a new click: a real swing
    }
}
