using System.Numerics;
using System.Text.RegularExpressions;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Issue #750, from the review of PR #729: the spiral stairs keep their triangle budget (art bible, exception of the
/// stairs), and the view has no second set of numbers next to <see cref="StairsLook"/>.
/// </summary>
public class StairsBudgetAndViewTests
{
    // Art bible, table "Budget": a prop has up to 400 triangles; the spiral stairs may have 1300 (exception of 2026-11-25,
    // issue #765), with an atlas of 256 x 512 instead of 64 x 64.
    private const int StairsTriangleBudget = 1300;

    public static TheoryData<TextureVariant> EveryVariant() => new(TextureVariant.A, TextureVariant.B, TextureVariant.C);

    [Theory]
    [MemberData(nameof(EveryVariant))]
    public void Every_file_of_the_stairs_stays_within_the_triangle_budget_of_the_exception(TextureVariant variant)
    {
        var file = StairsLook.FileName(variant);

        var triangles = WeaponPoseTests.Triangles(file).Sum(part => part.Triangles.Length / 3);

        Assert.True(triangles <= StairsTriangleBudget, $"{file} has {triangles} triangles, the exception of the art bible allows {StairsTriangleBudget}.");
        Assert.True(triangles > 400, $"{file} has {triangles} triangles: with so few the exception of the art bible is no longer needed, take the row out.");
    }

    [Theory]
    [MemberData(nameof(EveryVariant))]
    public void The_cull_sphere_of_the_view_holds_every_corner_of_the_model_and_of_the_box_of_the_stairs(TextureVariant variant)
    {
        var middle = new Vector3(0f, Stairs.Height / 2f, 0f);

        // The model: round the middle at half height, as the view tests it against the view cone.
        var farthest = WeaponPoseTests.Triangles(StairsLook.FileName(variant))
            .SelectMany(part => part.Corners.Select(corner => corner + part.Pivot))
            .Max(corner => Vector3.Distance(corner, middle));
        Assert.True(farthest < StairsLook.BoundingRadius, $"The model reaches {farthest} m from its middle, the cull sphere only {StairsLook.BoundingRadius} m.");

        // The placeholder: the end of its steps (corner of the last step) and of its pole, and the lamp.
        var step = new Vector3(Stairs.Radius, StairsLook.PlaceholderStepThickness / 2f, StairsLook.PlaceholderStepWidth / 2f);
        var placeholder = Math.Max(
            Vector3.Distance(new Vector3(step.X, Stairs.Height, step.Z), middle),
            Vector3.Distance(new Vector3(step.X, 0f, step.Z), middle));
        Assert.True(placeholder < StairsLook.BoundingRadius, $"The placeholder reaches {placeholder} m, the cull sphere only {StairsLook.BoundingRadius} m.");

        // And the sphere is not wasted on a box far bigger than the stairs: it stays within one metre of the box's corners.
        var corner3 = Vector3.Distance(new Vector3(Stairs.Radius, Stairs.Height, Stairs.Radius), middle);
        Assert.InRange(StairsLook.BoundingRadius, 0f, corner3 + 1f);
    }

    [Fact]
    public void The_view_of_the_stairs_has_no_decimal_number_of_its_own()
    {
        // StairsView is in Game and cannot be run here: its numbers are read from the file instead. Every size, count and
        // radius it uses is a constant of StairsLook (issue #750); a literal like 0.35f next to them is a second set that
        // drifts from the first.
        var source = File.ReadAllText(RepositoryFiles.Find("src/SilverAndAsh.Game/Rendering/StairsView.cs"));
        var code = Regex.Replace(source, @"//.*|/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        var literals = Regex.Matches(code, @"(?<![\w.])\d+\.\d+f?\b").Select(match => match.Value).ToList();

        Assert.True(literals.Count == 0, $"StairsView has its own numbers: {string.Join(", ", literals)}. Take them from StairsLook.");
    }

    [Fact]
    public void The_view_of_the_stairs_takes_the_placeholder_and_the_cull_sphere_from_the_look()
    {
        var source = File.ReadAllText(RepositoryFiles.Find("src/SilverAndAsh.Game/Rendering/StairsView.cs"));

        foreach (var name in new[]
        {
            nameof(StairsLook.PlaceholderSteps), nameof(StairsLook.PlaceholderStepsPerTurn), nameof(StairsLook.PlaceholderPoleWidth),
            nameof(StairsLook.PlaceholderStepThickness), nameof(StairsLook.PlaceholderStepWidth), nameof(StairsLook.PlaceholderLampWidth),
            nameof(StairsLook.PlaceholderLampThickness), nameof(StairsLook.PlaceholderLampMiddle), nameof(StairsLook.BoundingRadius),
        })
        {
            Assert.Contains($"StairsLook.{name}", source, StringComparison.Ordinal);
        }
    }
}
