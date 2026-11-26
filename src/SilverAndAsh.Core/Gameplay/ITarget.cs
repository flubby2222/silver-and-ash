using System.Numerics;
using SilverAndAsh.Core.Physics;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Something the weapons can hit: a training dummy, a ghoul, later the bat. Blade and pistol only know this
/// interface - a new kind of enemy needs no change to the weapons.
/// </summary>
public interface ITarget
{
    /// <summary>Number within its world, unique among all targets (events name targets by it).</summary>
    int Id { get; }

    /// <summary>True if it can be hit right now (not lying on the ground, not dead).</summary>
    bool IsHittable { get; }

    /// <summary>Its body for hits (and collisions).</summary>
    CylinderBody Body { get; }

    /// <summary>
    /// What a shot can hit: usually <see cref="Body"/>; a target whose shape is far from one round body gives several
    /// (the ghoul: torso and head, issue #433). Blade blows still go against <see cref="Body"/>.
    /// </summary>
    IReadOnlyList<CylinderBody> ShotBodies => [Body];

    /// <summary>Which zone a hit at <paramref name="point"/> (on the body's surface) belongs to.</summary>
    HitZone ZoneAt(Vector3 point);

    /// <summary>
    /// Takes <paramref name="damage"/> and reports what happened into <paramref name="events"/>. Returns the damage its hit
    /// event reports (issue #510: what Leech takes its share of) - 0 if it ignored the hit.
    /// </summary>
    int TakeDamage(int damage, ICollection<GameEvent> events, bool critical = false, DamageKind kind = DamageKind.Strike);
}
