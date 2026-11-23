using System.Numerics;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// A target that belongs to a room and fights the players: a ghoul, a bat, Gorm (not a training dummy). The world
/// lists every kind once (<c>GameWorld.Enemies</c>); what applies to all of them - belonging to a room, being pushed out
/// of a doorway that shuts (issue #572) - goes through that list, so a new kind of enemy cannot be forgotten there.
/// </summary>
internal interface IEnemy : ITarget
{
    /// <summary>Puts it somewhere else at once, without walking there.</summary>
    void PlaceAt(Vector3 position);
}
