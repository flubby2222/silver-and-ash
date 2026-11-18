using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

public class HudTextLinesTests
{
    // The crosshair's arms reach this far below the screen centre (gap 4 + length 6 in CombatHud, plus the outline).
    private const int CrosshairReach = 11;

    // The dark outline drawn around a text reaches one pixel beyond it on every side.
    private const int Outline = 1;

    public static TheoryData<int> ScreenHeights => [HudTextLines.SmallestScreenHeight, 720, 768, 1080, 1440, 2160];

    private static HudText[] AllTexts => Enum.GetValues<HudText>();

    [Theory]
    [MemberData(nameof(ScreenHeights))]
    public void No_two_texts_overlap_even_with_their_outlines(int screenHeight)
    {
        var texts = AllTexts;
        for (var i = 0; i < texts.Length; i++)
        {
            for (var j = i + 1; j < texts.Length; j++)
            {
                var a = HudTextLines.Of(texts[i], screenHeight);
                var b = HudTextLines.Of(texts[j], screenHeight);
                var apart = a.Bottom + Outline < b.Top - Outline || b.Bottom + Outline < a.Top - Outline;
                Assert.True(apart, $"{texts[i]} ({a.Top}..{a.Bottom}) and {texts[j]} ({b.Top}..{b.Bottom}) overlap at height {screenHeight}.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(ScreenHeights))]
    public void Every_text_lies_on_the_screen(int screenHeight)
    {
        foreach (var text in AllTexts)
        {
            var line = HudTextLines.Of(text, screenHeight);
            Assert.InRange(line.Top, 0, screenHeight - line.FontSize);
        }
    }

    [Theory]
    [MemberData(nameof(ScreenHeights))]
    public void The_texts_under_the_crosshair_start_below_its_arms(int screenHeight)
    {
        foreach (var text in new[] { HudText.ReloadHint, HudText.InteractionPrompt })
        {
            Assert.True(
                HudTextLines.Of(text, screenHeight).Top > (screenHeight / 2) + CrosshairReach,
                $"{text} touches the crosshair at height {screenHeight}.");
        }
    }

    [Fact]
    public void The_reload_hint_is_the_first_line_under_the_crosshair()
    {
        var height = HudTextLines.SmallestScreenHeight;

        Assert.True(HudTextLines.Of(HudText.ReloadHint, height).Top < HudTextLines.Of(HudText.InteractionPrompt, height).Top);
    }

    [Fact]
    public void An_unknown_text_has_no_line()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HudTextLines.Of((HudText)99, HudTextLines.SmallestScreenHeight));
    }
}
