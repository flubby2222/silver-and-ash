using System.Numerics;

namespace SilverAndAsh.Game.Rendering;

/// <summary>Sets a model at its place in the world: figures, building blocks, anything whose file looks along +Z.</summary>
internal static class ModelPlacement
{
    /// <summary>Turns a model looking along +Z to <paramref name="facing"/> and stands it at <paramref name="position"/>.</summary>
    public static Matrix4x4 Place(Vector3 position, Vector3 facing) =>
        Matrix4x4.CreateRotationY(MathF.Atan2(facing.X, facing.Z)) * Matrix4x4.CreateTranslation(position);
}
