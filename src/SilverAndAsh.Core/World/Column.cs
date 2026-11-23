using System.Numerics;

namespace SilverAndAsh.Core.World;

/// <summary>
/// A free-standing column (issue #539): part of the level, from floor to ceiling, with its middle at
/// (<paramref name="X"/>, <paramref name="Z"/>) in world metres. Seen from above it is a circle of <see cref="Radius"/>
/// (the twelve-sided model lies inside it, measured over its corners). It blocks every figure, sight and every shot at
/// every height - like a wall, but round.
/// </summary>
/// <param name="X">World X of its middle, in metres.</param>
/// <param name="Z">World Z of its middle, in metres.</param>
public readonly record struct Column(float X, float Z)
{
    /// <summary>Radius in metres of the circle it fills (the accepted probe, issue #539: twelve sides over 0.5 m).</summary>
    public const float Radius = 0.5f;

    /// <summary>Its middle on the ground plane (X, Z).</summary>
    public Vector2 Centre => new(X, Z);

    /// <summary>A column in the middle of cell (<paramref name="x"/>, <paramref name="z"/>).</summary>
    public static Column AtCell(int x, int z) => new((x + 0.5f) * Level.CellSize, (z + 0.5f) * Level.CellSize);

    /// <summary>True if <paramref name="point"/> lies inside its circle (any height).</summary>
    public bool Contains(Vector3 point) =>
        Vector2.DistanceSquared(new Vector2(point.X, point.Z), Centre) < Radius * Radius;
}
