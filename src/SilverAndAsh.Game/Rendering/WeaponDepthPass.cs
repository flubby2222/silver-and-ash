using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Draws the weapons in the hands with a depth of their own (<see cref="WeaponDepth"/>, issue #624): the projection of
/// the world, only its depth row replaced, so the picture stays the same and the near plane comes to
/// <see cref="WeaponPose.NearPlane"/>. Between BeginMode3D and EndMode3D.
/// </summary>
internal static class WeaponDepthPass
{
    /// <summary>Switches to the weapons' depth; returns the world's projection for <see cref="End"/>.</summary>
    public static Matrix4x4 Begin()
    {
        Rlgl.DrawRenderBatchActive();
        var world = Rlgl.GetMatrixProjection();

        // raylib looks along -z: clip z = M33 * z + M34 with w = -z, so the depth row of d = A - B / distance is
        // M33 = -A, M34 = -B.
        var (a, b) = WeaponDepth.Row(-world.M33, -world.M34);
        var weapons = world;
        weapons.M33 = -a;
        weapons.M34 = -b;
        Rlgl.SetMatrixProjection(weapons);
        return world;
    }

    /// <summary>Back to the world's projection.</summary>
    public static void End(Matrix4x4 world)
    {
        Rlgl.DrawRenderBatchActive();
        Rlgl.SetMatrixProjection(world);
    }
}
