using SilverAndAsh.Core.Input;

namespace SilverAndAsh.Core.Tests.Input;

public class FocusPauseTests
{
    // One frame of the running game as Program does it: first the menu, then whether the mouse is captured.
    private static (bool MenuOpen, bool CapturesMouse) Frame(bool menuOpen, bool windowFocused, bool overlayOpen = false)
    {
        var open = FocusPause.MenuOpen(menuOpen, windowFocused);
        return (open, FocusPause.CapturesMouse(open, onEndScreen: false, onTitle: false, overlayOpen));
    }

    [Fact]
    public void While_the_window_has_the_focus_the_run_goes_on_with_a_captured_mouse()
    {
        var frame = Frame(menuOpen: false, windowFocused: true);

        Assert.False(frame.MenuOpen);
        Assert.True(frame.CapturesMouse);
    }

    [Fact]
    public void Alt_tab_opens_the_menu_frees_the_mouse_and_it_stays_open_after_coming_back()
    {
        // Playing, then Alt+Tab (focus lost for a few frames), then back in the game.
        var focus = new[] { true, true, false, false, true, true };
        var menuOpen = false;
        var menu = new List<bool>();
        var capture = new List<bool>();
        foreach (var focused in focus)
        {
            (menuOpen, var captures) = Frame(menuOpen, focused);
            menu.Add(menuOpen);
            capture.Add(captures);
        }

        Assert.Equal([false, false, true, true, true, true], menu);
        Assert.Equal([true, true, false, false, false, false], capture);
    }

    [Fact]
    public void Losing_the_focus_with_an_overlay_open_still_opens_the_menu()
    {
        var frame = Frame(menuOpen: false, windowFocused: false, overlayOpen: true);

        Assert.True(frame.MenuOpen);
        Assert.False(frame.CapturesMouse);
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void Menu_end_screen_title_and_overlays_leave_the_mouse_free(
        bool menuOpen, bool onEndScreen, bool onTitle, bool overlayOpen)
    {
        Assert.False(FocusPause.CapturesMouse(menuOpen, onEndScreen, onTitle, overlayOpen));
    }
}
