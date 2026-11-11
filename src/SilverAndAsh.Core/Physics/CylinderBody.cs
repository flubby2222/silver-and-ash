using System.Numerics;

namespace SilverAndAsh.Core.Physics;

/// <summary>
/// A standing body that others cannot walk through, as an upright cylinder:
/// <paramref name="Position"/> = centre of its feet, plus radius and height. Used for training dummies now,
/// for enemies later.
/// </summary>
public readonly record struct CylinderBody(Vector3 Position, float Radius, float Height);
