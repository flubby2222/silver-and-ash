using System.Numerics;

namespace SilverAndAsh.Core.Physics;

/// <summary>
/// A solid box standing on the floor (axis-aligned, from <paramref name="Min"/> to <paramref name="Max"/>), e.g. a
/// chest. A character cannot walk through it, but can stand on its top - once it has jumped high enough.
/// </summary>
public readonly record struct BoxBody(Vector3 Min, Vector3 Max);
