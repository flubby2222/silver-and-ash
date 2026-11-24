using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests;

/// <summary>
/// Helpers that many test classes share: driving the simulation tick by tick and measuring on the floor plane.
/// Use them with <c>using static SilverAndAsh.Core.Tests.TestKit;</c> instead of a private copy per class (#697).
/// </summary>
internal static class TestKit
{
    /// <summary>
    /// Ticks the world <paramref name="ticks"/> times with the same input every tick, or fewer if
    /// <paramref name="until"/> is given and turns true (it is asked before every tick).
    /// </summary>
    public static void RunTicks(GameWorld world, PlayerInput input, int ticks, Func<bool>? until = null)
    {
        for (var i = 0; i < ticks && until?.Invoke() != true; i++)
        {
            world.Tick(input);
        }
    }

    /// <summary>Ticks the player alone <paramref name="ticks"/> times with the same input every tick.</summary>
    public static void RunTicks(Player player, PlayerInput input, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            player.Tick(input);
        }
    }

    /// <summary>
    /// Moves a body of the given radius through the level for <paramref name="ticks"/> ticks, wanting the same
    /// <paramref name="velocity"/> every tick (like a player holding a direction). Returns where it ends and its last velocity.
    /// </summary>
    public static (Vector3 Position, Vector3 Velocity) RunTicks(Level level, Vector3 position, Vector3 velocity, float radius, int ticks)
    {
        var current = (Position: position, Velocity: velocity);
        for (var i = 0; i < ticks; i++)
        {
            current = CharacterCollision.Move(level, current.Position, velocity, radius, SimulationClock.SecondsPerTick);
        }

        return current;
    }

    /// <summary>
    /// Like <see cref="RunTicks(GameWorld, PlayerInput, int, Func{bool}?)"/>, and returns the events of every tick it ran, in order.
    /// </summary>
    public static List<GameEvent> Run(GameWorld world, PlayerInput input, int ticks, Func<bool>? until = null)
    {
        var events = new List<GameEvent>();
        for (var i = 0; i < ticks && until?.Invoke() != true; i++)
        {
            world.Tick(input);
            events.AddRange(world.Events);
        }

        return events;
    }

    /// <summary>
    /// Ticks the world until <paramref name="condition"/> holds after a tick, at most <paramref name="maxTicks"/> times.
    /// Returns whether it held.
    /// </summary>
    public static bool RunUntil(GameWorld world, PlayerInput input, int maxTicks, Func<GameWorld, bool> condition)
    {
        for (var i = 0; i < maxTicks; i++)
        {
            world.Tick(input);
            if (condition(world))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Distance between two points on the floor plane (X and Z), ignoring the height.</summary>
    public static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();
}
