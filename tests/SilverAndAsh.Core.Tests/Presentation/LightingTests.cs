using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

public class LightingTests
{
    private const float Tolerance = 0.0001f;

    [Theory]
    [InlineData(0f, 1f)]       // right in front: full light
    [InlineData(7f, 0.675f)]   // halfway: halfway between 1 and the minimum
    [InlineData(14f, 0.35f)]   // at the end of the range: minimum
    [InlineData(30f, 0.35f)]   // beyond: stays at the minimum
    public void Lantern_light_falls_off_with_distance(float distance, float expected)
    {
        Assert.Equal(expected, Lighting.LanternBrightness(distance, range: 14f, minimum: 0.35f), Tolerance);
    }

    [Fact]
    public void Lantern_light_never_gets_brighter_further_away()
    {
        var previous = float.MaxValue;
        for (var distance = 0f; distance <= 20f; distance += 0.25f)
        {
            var brightness = Lighting.LanternBrightness(distance, 14f, 0.35f);
            Assert.True(brightness <= previous);
            previous = brightness;
        }
    }

    [Theory]
    [InlineData(0f, 0f)]       // close: no fog
    [InlineData(2.5f, 0f)]     // start of the fog: still clear
    [InlineData(8.25f, 0.5f)]  // exactly in the middle: half fog
    [InlineData(14f, 1f)]      // end: only fog colour
    [InlineData(50f, 1f)]      // beyond: stays fog
    public void Fog_rises_from_start_to_end(float distance, float expected)
    {
        Assert.Equal(expected, Lighting.FogAmount(distance, start: 2.5f, end: 14f), Tolerance);
    }

    [Fact]
    public void Fog_starts_and_ends_gently()
    {
        // Smoothstep: just after the start and just before the end, the change is much smaller than linear.
        const float Step = 0.1f;
        var nearStart = Lighting.FogAmount(2.5f + Step, 2.5f, 14f);
        var linear = Step / (14f - 2.5f);

        Assert.True(nearStart < linear);
        Assert.True(1f - Lighting.FogAmount(14f - Step, 2.5f, 14f) < linear);
    }

    [Fact]
    public void Fog_that_ends_before_it_starts_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Lighting.FogAmount(5f, 10f, 10f));
    }
}
