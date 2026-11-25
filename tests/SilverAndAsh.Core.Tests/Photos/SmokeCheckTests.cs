using SilverAndAsh.Core.Photos;

namespace SilverAndAsh.Core.Tests.Photos;

/// <summary>
/// The smoke test of the game (<c>--smoke</c>, issue #329): the game draws every photo scene in a hidden window and hands
/// the pixels over; whether a picture is a single colour, which scene failed and the exit code are decided here.
/// </summary>
public sealed class SmokeCheckTests
{
    private static readonly byte[] Grey = [40, 40, 40, 255];

    [Fact]
    public void Without_the_option_the_arguments_stay_as_they_are()
    {
        var (others, smoke) = SmokeCheck.Extract(["--seed", "7"]);

        Assert.False(smoke);
        Assert.Equal(["--seed", "7"], others);
    }

    [Fact]
    public void The_option_alone_is_taken_out()
    {
        var (others, smoke) = SmokeCheck.Extract([SmokeCheck.Option]);

        Assert.True(smoke);
        Assert.Empty(others);
    }

    [Fact]
    public void The_option_twice_is_refused()
    {
        var error = Assert.Throws<ArgumentException>(() => SmokeCheck.Extract([SmokeCheck.Option, SmokeCheck.Option]));

        Assert.Equal("'--smoke' may come only once.", error.Message);
    }

    [Fact]
    public void The_option_with_another_one_is_refused_and_names_it()
    {
        var error = Assert.Throws<ArgumentException>(() => SmokeCheck.Extract(["--look", "3", SmokeCheck.Option]));

        Assert.Equal(
            "'--smoke' goes alone - it draws every photo scene in the standard window. Found also: --look 3.", error.Message);
    }

    [Fact]
    public void A_picture_of_one_colour_is_a_single_colour()
    {
        Assert.True(SmokeCheck.IsSingleColour(Pixels(Grey, Grey, Grey)));
    }

    [Fact]
    public void A_picture_with_one_pixel_or_none_is_a_single_colour()
    {
        Assert.True(SmokeCheck.IsSingleColour(Grey));
        Assert.True(SmokeCheck.IsSingleColour([]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void One_channel_of_the_last_pixel_differing_makes_two_colours(int channel)
    {
        var other = (byte[])Grey.Clone();
        other[channel]++;

        Assert.False(SmokeCheck.IsSingleColour(Pixels(Grey, Grey, other)));
    }

    [Fact]
    public void A_picture_differing_only_in_alpha_is_a_single_colour()
    {
        byte[] seeThrough = [40, 40, 40, 0];

        Assert.True(SmokeCheck.IsSingleColour(Pixels(Grey, seeThrough)));
    }

    [Fact]
    public void Bytes_that_are_no_whole_pixels_are_refused()
    {
        var error = Assert.Throws<ArgumentException>(() => SmokeCheck.IsSingleColour(new byte[6]));

        Assert.Equal("rgba", error.ParamName);
    }

    [Fact]
    public void Two_pictures_with_more_than_one_colour_pass()
    {
        var mixed = Pixels(Grey, [200, 10, 10, 255]);

        Assert.Null(SmokeCheck.Judge(mixed, mixed));
    }

    [Fact]
    public void A_flat_3D_picture_fails_and_names_its_colour()
    {
        Assert.Equal(
            "the 3D picture is a single colour (40, 40, 40)",
            SmokeCheck.Judge(Pixels(Grey, Grey), Pixels(Grey, [200, 10, 10, 255])));
    }

    [Fact]
    public void A_flat_window_fails_and_names_its_colour()
    {
        Assert.Equal(
            "the window is a single colour (0, 0, 0)",
            SmokeCheck.Judge(Pixels(Grey, [200, 10, 10, 255]), Pixels([0, 0, 0, 255], [0, 0, 0, 255])));
    }

    [Fact]
    public void Both_pictures_flat_name_both_and_a_picture_without_pixels_says_so()
    {
        Assert.Equal(
            "the 3D picture is a single colour (no pixels); the window is a single colour (40, 40, 40)",
            SmokeCheck.Judge([], Grey));
    }

    [Fact]
    public void A_scene_that_threw_names_the_exception_and_its_message()
    {
        Assert.Equal(
            "threw InvalidOperationException: no ghoul in the room",
            SmokeCheck.Crashed(new InvalidOperationException("no ghoul in the room")));
    }

    [Fact]
    public void A_run_without_failures_passes_with_exit_code_0()
    {
        var summary = SmokeCheck.Summarise([new("ghoul-4m", null), new("room-combat", null)]);

        Assert.Equal(SmokeCheck.PassedExitCode, summary.ExitCode);
        Assert.Equal(0, SmokeCheck.PassedExitCode);
        Assert.Equal(["SMOKE 2 scene(s) drawn, 0 failed."], summary.Lines);
    }

    [Fact]
    public void Every_failed_scene_gets_a_line_in_its_order_and_the_run_fails()
    {
        var summary = SmokeCheck.Summarise(
        [
            new("ghoul-4m", "threw NullReferenceException: boom"),
            new("room-combat", null),
            new("bat-2m", "the window is a single colour (0, 0, 0)"),
        ]);

        Assert.Equal(SmokeCheck.FailedExitCode, summary.ExitCode);
        Assert.NotEqual(0, SmokeCheck.FailedExitCode);
        Assert.Equal(
            [
                "FAILED ghoul-4m: threw NullReferenceException: boom",
                "FAILED bat-2m: the window is a single colour (0, 0, 0)",
                "SMOKE 3 scene(s) drawn, 2 failed.",
            ],
            summary.Lines);
    }

    [Fact]
    public void A_run_without_any_scene_fails()
    {
        var summary = SmokeCheck.Summarise([]);

        Assert.Equal(SmokeCheck.FailedExitCode, summary.ExitCode);
        Assert.Equal(["SMOKE no photo scene was drawn - nothing checked."], summary.Lines);
    }

    [Fact]
    public void A_window_that_does_not_open_has_its_own_exit_code()
    {
        Assert.NotEqual(SmokeCheck.PassedExitCode, SmokeCheck.NoWindowExitCode);
        Assert.NotEqual(SmokeCheck.FailedExitCode, SmokeCheck.NoWindowExitCode);
    }

    private static byte[] Pixels(params byte[][] pixels) => [.. pixels.SelectMany(pixel => pixel)];
}
