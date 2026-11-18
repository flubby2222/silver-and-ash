using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Checks the colours in the HUD parts picture itself (<c>assets/hud/hud_parts.png</c>): the layout tests only see
/// names and sizes, a wrong colour in the picture would pass them.
/// </summary>
public class HudPartsPictureTests
{
    private const string Folder = "assets/hud";

    private static readonly HudParts Parts =
        HudParts.Parse(File.ReadAllText(Path.Combine(RepositoryFiles.Find(Folder), "hud_parts.json")));

    private static readonly (int Width, int Height, byte[] Rgba) Picture =
        TestPng.Read(Path.Combine(RepositoryFiles.Find(Folder), Parts.Image));

    [Fact]
    public void The_experience_fill_is_gold_like_the_level_up_hint()
    {
        // Issue #387, the verdict after the playtest: experience is golden, in the gold of the hint "[X] to level up".
        var fill = MeanOfFill("xp");

        Assert.True(IsGold(fill), $"xp_full averages {fill}, not gold");
        Assert.True(IsGold(Parts.TextHint), $"the hint colour {Parts.TextHint} is not gold");
    }

    [Fact]
    public void The_gold_check_tells_gold_from_the_red_life_and_the_silver_frames()
    {
        // The other side of the check above: the red life and the silver frames are not gold.
        Assert.False(IsGold(MeanOfFill("life")), "the red life fill counts as gold");
        Assert.False(IsGold(MeanOfOpaque("frame_level")), "the silver level frame counts as gold");
    }

    /// <summary>Gold: red and green clearly above blue, red at least green (brass, not olive, not grey, not red).</summary>
    private static bool IsGold(HudColor color) =>
        color.R >= color.G && color.G - color.B >= 40 && color.R - color.B >= 60;

    /// <summary>
    /// The mean of the fill of a bar: the pixels where <c>{bar}_full</c> differs from <c>{bar}_empty</c> (the frame
    /// over the bar is the same in both).
    /// </summary>
    private static HudColor MeanOfFill(string bar) => Mean($"{bar}_full", $"{bar}_empty");

    private static HudColor MeanOfOpaque(string name) => Mean(name, null);

    private static HudColor Mean(string name, string? unlike)
    {
        var part = Parts[name];
        var other = unlike is null ? part : Parts[unlike];
        Assert.Equal((part.Width, part.Height), (other.Width, other.Height));
        long r = 0, g = 0, b = 0, count = 0;
        for (var y = 0; y < part.Height; y++)
        {
            for (var x = 0; x < part.Width; x++)
            {
                var at = (((part.Y + y) * Picture.Width) + part.X + x) * 4;
                var then = (((other.Y + y) * Picture.Width) + other.X + x) * 4;
                var differs = unlike is null || Picture.Rgba.AsSpan(at, 4).SequenceEqual(Picture.Rgba.AsSpan(then, 4)) is false;
                if (Picture.Rgba[at + 3] == 255 && differs)
                {
                    r += Picture.Rgba[at];
                    g += Picture.Rgba[at + 1];
                    b += Picture.Rgba[at + 2];
                    count++;
                }
            }
        }

        Assert.True(count > 0, $"{name} has no opaque pixel");
        return new HudColor((byte)(r / count), (byte)(g / count), (byte)(b / count));
    }
}
