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
    /// The file to draw: the one of <paramref name="wanted"/> (the variant the level really draws on the walls there) if
    /// it was loaded, else the one of variant A, else none - then the placeholder.
    /// </summary>
    public static TextureVariant? Pick(TextureVariant wanted, bool wantedLoaded, bool variantALoaded) =>
        wantedLoaded ? wanted : variantALoaded ? TextureVariant.A : null;

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
}
