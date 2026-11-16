using System.Text.RegularExpressions;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

public class DitheringTests
{
    private const float Tolerance = 0.0001f;

    [Fact]
    public void The_pattern_has_sixteen_different_thresholds_between_0_and_1()
    {
        var thresholds = Enumerable.Range(0, 16).Select(i => Dithering.Threshold(i % 4, i / 4)).ToList();

        Assert.Equal(16, thresholds.Distinct().Count());
        Assert.All(thresholds, t => Assert.InRange(t, 0.01f, 0.99f));
        Assert.Equal(0.5f, thresholds.Average(), Tolerance);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 3)]
    [InlineData(-1, 2)]
    [InlineData(853, 479)]
    public void The_pattern_repeats_every_four_pixels(int x, int y)
    {
        Assert.Equal(Dithering.Threshold(x, y), Dithering.Threshold(x + 4, y));
        Assert.Equal(Dithering.Threshold(x, y), Dithering.Threshold(x, y + 4));
        Assert.Equal(Dithering.Threshold(x, y), Dithering.Threshold(x - 8, y - 12));
    }

    [Fact]
    public void Neighbouring_pixels_have_thresholds_far_apart()
    {
        // The point of a Bayer matrix: a 2 x 2 corner already contains a low and a high threshold.
        Assert.True(MathF.Abs(Dithering.Threshold(0, 0) - Dithering.Threshold(1, 0)) >= 0.5f);
        Assert.True(MathF.Abs(Dithering.Threshold(0, 0) - Dithering.Threshold(0, 1)) >= 0.5f);
    }

    [Fact]
    public void Steps_of_5_bits_stay_as_they_are_whatever_the_threshold()
    {
        for (var step = 0; step <= Dithering.HighestStep; step++)
        {
            var channel = step / (float)Dithering.HighestStep;
            for (var i = 0; i < 16; i++)
            {
                Assert.Equal(channel, Dithering.Quantize(channel, Dithering.Threshold(i % 4, i / 4)), Tolerance);
            }
        }
    }

    [Fact]
    public void Quantize_gives_only_32_steps()
    {
        var results = new HashSet<float>();
        for (var channel = 0f; channel <= 1f; channel += 0.001f)
        {
            results.Add(Dithering.Quantize(channel, Dithering.Threshold(1, 2)));
        }

        Assert.All(results, value => Assert.Equal(value * 31f, MathF.Round(value * 31f), Tolerance));
        Assert.True(results.Count <= 32);
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.333f)]
    [InlineData(0.5f)]
    [InlineData(0.77f)]
    public void Over_a_block_the_eye_mixes_the_tone_back(float channel)
    {
        var average = Enumerable.Range(0, 16)
            .Average(i => Dithering.Quantize(channel, Dithering.Threshold(i % 4, i / 4)));

        // Half of 1/16 of a step at most: the pattern has 16 thresholds.
        Assert.Equal(channel, average, 1f / 31f / 32f + Tolerance);
    }

    [Theory]
    [InlineData(-0.5f, 0f)]
    [InlineData(1.5f, 1f)]
    public void Quantize_keeps_colours_inside_0_and_1(float channel, float expected)
    {
        Assert.Equal(expected, Dithering.Quantize(channel, Dithering.Threshold(3, 3)), Tolerance);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(255, 255)]
    [InlineData(16, 16)]
    [InlineData(11, 8)]
    [InlineData(20, 16)]
    public void A_colour_byte_snaps_to_the_nearest_step(int channel, int expected)
    {
        Assert.Equal((byte)expected, Dithering.SnapToStep((byte)channel));
    }

    [Fact]
    public void A_snapped_colour_gets_no_pattern()
    {
        // The background must look the same at every pixel, or the edge of the view would flicker in dots.
        for (var value = 0; value <= 255; value++)
        {
            var snapped = Dithering.SnapToStep(value / 255f);
            var first = Dithering.Quantize(snapped, Dithering.Threshold(0, 0));
            Assert.All(Enumerable.Range(0, 16), i =>
                Assert.Equal(first, Dithering.Quantize(snapped, Dithering.Threshold(i % 4, i / 4)), Tolerance));

            // ... and the graphics card stores it as the same byte the background is cleared with.
            Assert.Equal(Dithering.SnapToStep((byte)value), (byte)MathF.Round(first * 255f));
        }
    }

    [Theory]
    [InlineData(0f, 0f)]       // close: nothing fades
    [InlineData(9f, 0f)]       // start: still everything there
    [InlineData(13.6f, 0.5f)]  // middle: half gone
    [InlineData(18.2f, 1f)]    // end: nothing left
    [InlineData(40f, 1f)]
    public void Fading_rises_from_start_to_end(float distance, float expected)
    {
        Assert.Equal(expected, Dithering.FadeAmount(distance, start: 9f, end: 18.2f), Tolerance);
    }

    [Fact]
    public void At_fade_0_every_pixel_stays_at_fade_1_every_pixel_goes()
    {
        Assert.Equal(0f, Dithering.LeftOutShare(0f));
        Assert.Equal(1f, Dithering.LeftOutShare(1f));
    }

    [Fact]
    public void The_share_of_left_out_pixels_follows_the_fade()
    {
        for (var fade = 0f; fade <= 1f; fade += 0.01f)
        {
            Assert.Equal(fade, Dithering.LeftOutShare(fade), 1f / 16f + Tolerance);
        }
    }

    [Fact]
    public void With_the_later_start_a_ghoul_at_10_m_keeps_almost_all_its_pixels()
    {
        // Decision record 0010: with the fog curve (from 3.25 m) over 40 % were missing at 10 m. From 9 m on: 1 in 16.
        var withFogCurve = Dithering.LeftOutShare(Dithering.FadeAmount(10f, 3.25f, 18.2f));
        var withLaterStart = Dithering.LeftOutShare(Dithering.FadeAmount(10f, 9f, 18.2f));

        Assert.True(withFogCurve >= 0.4f);
        Assert.True(withLaterStart <= 1f / 16f);
    }

    [Fact]
    public void The_shader_uses_the_same_pattern()
    {
        var shader = File.ReadAllText(RepositoryFiles.Find("assets/shaders/level.fs"));
        var match = Regex.Match(shader, @"int\s+bayer\[16\]\s*=\s*int\[16\]\(([^)]*)\)");

        Assert.True(match.Success, "level.fs must define 'const int bayer[16] = int[16](...)'.");
        var numbers = match.Groups[1].Value.Split(',').Select(n => int.Parse(n.Trim(), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(Dithering.BayerMatrix, numbers);
        Assert.Contains("31.0", shader, StringComparison.Ordinal);
    }
}
