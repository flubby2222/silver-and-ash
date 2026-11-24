using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests;

/// <summary>
/// Test worlds that many test classes build the same way (#697): one place to change when the world's construction changes.
/// </summary>
internal static partial class TestKit
{
    /// <summary>A player who survives a lot - to watch many attacks in a row.</summary>
    public static readonly Balance Tough = Balance.Default with { Player = Balance.Default.Player with { MaxHealth = 100_000 } };

    /// <summary>
    /// The small boss fight: room 0 (start) in row 0, the boss door in row 1, the boss room (5 wide) in rows 2-11 with
    /// Gorm ('T') in row 10. Build it with <see cref="GormHallWorld"/>.
    /// </summary>
    public const string GormHallMap = "##.P.##\n###.###\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#..T..#\n#.....#";

    /// <summary>
    /// The world of <see cref="GormHallMap"/> (or of <paramref name="level"/>, laid out the same way) with its two rooms and
    /// the boss door between them. <paramref name="inHall"/> picks the cells of the hall's rectangle that belong to the boss
    /// room (all of them by default).
    /// </summary>
    public static GameWorld GormHallWorld(
        Level? level = null, int seed = 1, Balance? balance = null, Func<(int X, int Z), bool>? inHall = null)
    {
        var hall = Enumerable.Range(2, 10).SelectMany(z => Enumerable.Range(1, 5).Select(x => (x, z))).Where(inHall ?? (_ => true)).ToList();
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 7, 1, 0, [(2, 0), (3, 0), (4, 0)], [], []),
            new(1, RoomKind.Boss, "hall", 0, 2, 7, 10, 1, hall, [], []),
        ];
        LevelDoor[] doors = [new(3, 1, 0, 1, RoomSide.South, DoorKind.Boss)];
        return new GameWorld(level ?? LevelParser.Parse(GormHallMap), seed, balance ?? Balance.Default, doors, rooms);
    }

    /// <summary>
    /// Opens the boss door of a <see cref="GormHallWorld"/> and walks in until the boss room seals (at most three seconds).
    /// </summary>
    public static void EnterGormHall(GameWorld world)
    {
        world.Tick(PlayerInput.None with { Interact = true });
        for (var tick = 0; tick < 3 * SimulationClock.TicksPerSecond && !world.Rooms[1].IsSealed; tick++)
        {
            world.Tick(PlayerInput.None with { Move = new Vector2(0f, 1f) });
        }

        Assert.True(world.Rooms[1].IsSealed, "The boss room never sealed - the test would prove nothing.");
    }
}
