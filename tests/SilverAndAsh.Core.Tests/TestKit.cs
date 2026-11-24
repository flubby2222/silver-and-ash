using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;

namespace SilverAndAsh.Core.Tests;

/// <summary>
/// Helpers that many test classes share: driving the simulation tick by tick and measuring on the floor plane.
/// Use them with <c>using static SilverAndAsh.Core.Tests.TestKit;</c> instead of a private copy per class (#697).
/// </summary>
internal static class TestKit
{
    /// <summary>Ticks the world <paramref name="ticks"/> times with the same input every tick.</summary>
    public static void RunTicks(GameWorld world, PlayerInput input, int ticks)
    {
        for (var i = 0; i < ticks; i++)
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

    /// <summary>Distance between two points on the floor plane (X and Z), ignoring the height.</summary>
    public static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();
}
