using System.Numerics;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// A target that belongs to a room and fights the players: a ghoul, a bat, Gorm (not a training dummy). The world
/// lists every kind once (<c>GameWorld.Enemies</c>); what applies to all of them - belonging to a room, being pushed out
/// of a doorway that shuts (issue #572), being dead, hidden or out (issue #580) - goes through that list and this
/// interface, so a new kind of enemy cannot be forgotten there.
/// </summary>
internal interface IEnemy : ITarget
{
    /// <summary>True once it is beaten: it no longer counts for clearing its room or taking a cell there.</summary>
    bool IsDead { get; }

    /// <summary>
    /// True while it still waits inside the floor or the ceiling of a combat room (issue #424) - it wakes only when its
    /// wave comes out. An enemy that never hides (Gorm) is never hidden.
    /// </summary>
    bool IsHidden { get; }

    /// <summary>
    /// True once it is fully out (issue #424: while it comes out it does not attack). Only then does it act. An enemy
    /// that never hides (Gorm) is always out.
    /// </summary>
    bool IsOut { get; }

    /// <summary>The status effects it carries (issue #504); the world counts them down every tick.</summary>
    StatusEffects Status { get; }

    /// <summary>
    /// Where it is drawn (issue #506: the status display follows it): sunk into the floor or the ceiling while it is
    /// hidden or coming out; an enemy that never hides (Gorm) is drawn where it is.
    /// </summary>
    Vector3 ShownPosition { get; }

    /// <summary>Puts it somewhere else at once, without walking there.</summary>
    void PlaceAt(Vector3 position);
}
