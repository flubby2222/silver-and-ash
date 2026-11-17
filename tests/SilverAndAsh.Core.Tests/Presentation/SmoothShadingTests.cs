using System.Numerics;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

public class SmoothShadingTests
{
    private const float Tolerance = 0.0001f;

    // Distinct values so a mix-up of two directions shows.
    private static readonly FacingBrightness Facing = new(PlusX: 1.0f, MinusX: 0.62f, Up: 0.9f, Down: 0.55f, PlusZ: 0.82f, MinusZ: 0.72f);

    public static TheoryData<Vector3, float> AxisNormals => new()
    {
        { Vector3.UnitX, 1.0f },
        { -Vector3.UnitX, 0.62f },
        { Vector3.UnitY, 0.9f },
        { -Vector3.UnitY, 0.55f },
        { Vector3.UnitZ, 0.82f },
        { -Vector3.UnitZ, 0.72f },
    };

    [Theory]
    [MemberData(nameof(AxisNormals))]
    public void A_normal_along_an_axis_gets_the_flat_value(Vector3 normal, float expected)
    {
        Assert.Equal(expected, SmoothShading.Brightness(normal, Facing), Tolerance);
    }

    [Fact]
    public void Halfway_between_two_directions_is_the_mean_of_both()
    {
        var upAndRight = Vector3.Normalize(new Vector3(1f, 1f, 0f));

        Assert.Equal((1.0f + 0.9f) / 2f, SmoothShading.Brightness(upAndRight, Facing), Tolerance);
    }

    [Fact]
    public void The_length_of_the_normal_does_not_matter()
    {
        var normal = new Vector3(0.3f, -0.8f, 0.5f);

        Assert.Equal(
            SmoothShading.Brightness(Vector3.Normalize(normal), Facing),
            SmoothShading.Brightness(normal * 7f, Facing),
            Tolerance);
    }

    [Fact]
    public void Every_direction_stays_between_the_darkest_and_the_brightest_value()
    {
        var random = new Random(232);
        for (var i = 0; i < 1000; i++)
        {
            var normal = new Vector3(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f, random.NextSingle() - 0.5f);
            var brightness = SmoothShading.Brightness(normal, Facing);

            Assert.InRange(brightness, 0.55f - Tolerance, 1.0f + Tolerance);
        }
    }

    [Fact]
    public void Turning_a_little_changes_the_brightness_only_a_little()
    {
        // Soft shading: no jumps like the flat shading has between two directions.
        var previous = SmoothShading.Brightness(Vector3.UnitX, Facing);
        for (var degrees = 1; degrees <= 360; degrees++)
        {
            var angle = degrees * MathF.PI / 180f;
            var brightness = SmoothShading.Brightness(new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)), Facing);

            Assert.True(MathF.Abs(brightness - previous) < 0.02f, $"Jump at {degrees} degrees.");
            previous = brightness;
        }
    }

    [Fact]
    public void A_zero_normal_gets_the_mean_instead_of_a_number_that_is_none()
    {
        var brightness = SmoothShading.Brightness(Vector3.Zero, Facing);

        Assert.Equal((1.0f + 0.62f + 0.9f + 0.55f + 0.82f + 0.72f) / 6f, brightness, Tolerance);
    }
}
