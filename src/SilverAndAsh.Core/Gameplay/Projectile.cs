using System.Numerics;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// A flying projectile - unlike the pistol's instant hit it travels and can be dodged. A small ball that flies in a
/// straight line until it hits a player, a wall, the floor or the ceiling, or until its lifetime is over.
/// Built for any enemy: the bat's blood bolt is the first.
/// </summary>
public sealed class Projectile
{
    /// <summary>Creates a projectile at <paramref name="position"/> (its centre) moving with <paramref name="velocity"/>.</summary>
    public Projectile(int id, Vector3 position, Vector3 velocity, int damage, float radius, float lifeSeconds)
    {
        Id = id;
        Position = position;
        PreviousPosition = position;
        Velocity = velocity;
        Damage = damage;
        Radius = radius;
        TicksLeft = SimulationClock.SecondsToTicks(lifeSeconds);
    }

    /// <summary>Number within its world (events name the projectile by it).</summary>
    public int Id { get; }

    /// <summary>Centre of the ball.</summary>
    public Vector3 Position { get; internal set; }

    /// <summary>Centre one tick earlier, for smooth drawing between ticks.</summary>
    public Vector3 PreviousPosition { get; internal set; }

    /// <summary>Movement in metres per second.</summary>
    public Vector3 Velocity { get; }

    /// <summary>Damage to a player it hits.</summary>
    public int Damage { get; }

    /// <summary>Radius of the ball in metres.</summary>
    public float Radius { get; }

    /// <summary>Ticks until it disappears by itself.</summary>
    public int TicksLeft { get; internal set; }

    /// <summary>True once it hit something or its lifetime is over.</summary>
    public bool IsGone { get; internal set; }
}
