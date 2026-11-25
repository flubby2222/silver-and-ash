using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The spiral staircase of stone (issue #300): one model per wall variant, assets/models/stairs_spiral_{a,b,c}.glb. Each
/// is built on the real wall texture of its variant (the floor tile of the same letter on the treads), so its joints
/// continue the grid of the room. Two parts, neither hangs on the other and neither moves in the file:
/// <see cref="Stone"/> (plinth, pole, twenty steps, capital) and <see cref="Glow"/> (the lamp on top).
/// <para>
/// The staircase stands in the file space of <see cref="Stairs"/> (origin on the floor in its middle, never turned)
/// and shows the wall variant of the room it rises in. While it rises it comes up out of the floor
/// (<see cref="Sink"/>); the lamp is lit only once it stands (<see cref="ShowsGlow"/>) and then pulses
/// (<see cref="GlowTint"/>), as the light of the placeholder did.
/// </para>
/// </summary>
public static class StairsLook
{
    public const string Stone = "stairs";
    public const string Glow = "glow";

    private static readonly RigPart[] AllParts = [new(Stone, null), new(Glow, null)];

    /// <summary>The parts in the order of the rig.</summary>
    public static IReadOnlyList<RigPart> Parts => AllParts;

    /// <summary>The file of the staircase for <paramref name="variant"/>, e.g. stairs_spiral_b.glb.</summary>
    public static string FileName(TextureVariant variant)
    {
        if (variant is not (TextureVariant.A or TextureVariant.B or TextureVariant.C))
        {
            throw new ArgumentOutOfRangeException(nameof(variant), variant, null);
        }

        return $"stairs_spiral_{variant.ToString().ToLowerInvariant()}.glb";
    }

    /// <summary>The wall variant the staircase at <paramref name="position"/> shows: the one of the walls of its cell's room.</summary>
    public static TextureVariant Variant(SurfaceVariants surfaces, Vector3 position)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        var (x, z) = Level.ToGrid(position.X, position.Z);
        return surfaces.At(x, z, SurfaceKind.Wall);
    }

    /// <summary>
    /// How far below its place the staircase is drawn at <paramref name="riseFraction"/> (0: all of it in the floor,
    /// 1: standing): fast at first, slowing down at the end.
    /// </summary>
    public static float Sink(float riseFraction)
    {
        var left = 1f - Math.Clamp(riseFraction, 0f, 1f);
        return left * left * Stairs.Height;
    }

    /// <summary>Whether the lamp on top is lit: only once the staircase stands and can be climbed.</summary>
    public static bool ShowsGlow(bool canInteract) => canInteract;

    /// <summary>The brightness of the lamp at <paramref name="seconds"/>: from dark to full and back, about every 3 s.</summary>
    public static float GlowTint(float seconds) => 0.5f + (0.5f * MathF.Sin(seconds * 2f));

    /// <summary>
    /// How thick the lamp of the placeholder is (the stand-in without a model): a flat box lying at the top of the
    /// staircase's box, under the ceiling of the room (issue #727; it used to stick out above <see cref="Stairs.Height"/>).
    /// </summary>
    public const float PlaceholderLampThickness = 0.1f;

    /// <summary>
    /// The numbers of the placeholder (the stand-in without a model) and of the cull sphere, here and not in the view (issue
    /// #750): the view has no second set, and a test checks them against the model and the box of the stairs.
    /// </summary>
    public const int PlaceholderSteps = 24;

    /// <summary>How many placeholder steps make one turn round the pole.</summary>
    public const int PlaceholderStepsPerTurn = 8;

    /// <summary>The width of the placeholder's pole, in metres (it is square).</summary>
    public const float PlaceholderPoleWidth = 0.35f;

    /// <summary>The thickness of a placeholder step.</summary>
    public const float PlaceholderStepThickness = 0.12f;

    /// <summary>The width of a placeholder step (its length is <see cref="Stairs.Radius"/>).</summary>
    public const float PlaceholderStepWidth = 0.55f;

    /// <summary>The side of the placeholder's lamp (it is square, <see cref="PlaceholderLampThickness"/> thick).</summary>
    public const float PlaceholderLampWidth = 0.5f;

    /// <summary>
    /// The radius of the sphere round the middle of the staircase (at half its height) that the view tests against the view
    /// cone: the box of the stairs has its corners 3.45 m from there, the model and the placeholder are round and reach 3.23
    /// and 3.24 m; a little more, for safety.
    /// </summary>
    public const float BoundingRadius = 3.5f;

    /// <summary>The height of the middle of the placeholder's lamp: its top ends at <see cref="Stairs.Height"/>.</summary>
    public static float PlaceholderLampMiddle => Stairs.Height - (PlaceholderLampThickness / 2f);
}
