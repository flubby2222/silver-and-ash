using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>Treasure and secret rooms are safe: enemies never go in, however they were woken.</summary>
public class SafeRoomTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly PlayerInput Back = PlayerInput.None with { Move = new Vector2(0f, -1f) };
    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };

    // Room 0 (rows 0-2) is the room to flee into, its door is cell (2, 3). Room 1 (rows 4-9) is a corridor - so its
    // enemy is awake from the start - with the player in row 4, right at the door, and the enemy in row 8.
    private const string Map = "#...#\n#...#\n#...#\n##.##\n#.P.#\n#...#\n#...#\n#...#\n#.E.#\n#...#";

    private static List<(int X, int Z)> Rows(int from, int to) =>
        Enumerable.Range(from, to - from + 1).SelectMany(z => new[] { (1, z), (2, z), (3, z) }).ToList();

    /// <summary>The world after the player turned round, opened the door and ran into room 0.</summary>
    private static GameWorld Fled(RoomKind refuge, char enemy)
    {
        PlacedRoom[] rooms =
        [
            new(0, refuge, "refuge", 0, 0, 5, 3, 1, Rows(0, 2), [], []),
            new(1, RoomKind.Corridor, "enemies", 0, 4, 5, 6, 0, Rows(4, 9), [], []),
        ];
        LevelDoor[] doors = [new(2, 3, 1, 0, RoomSide.North)];
        var world = new GameWorld(LevelParser.Parse(Map.Replace('E', enemy)), 1, Balance.Default, doors, rooms);
        var player = world.Player;

        player.Look(PlayerInput.None with { Look = new Vector2(MathF.PI / ControlSettings.MouseSensitivity, 0f) });
        world.Tick(PressF);
        Assert.True(world.Doors[0].IsOpen, "The door did not open - the test would prove nothing.");
        for (var tick = 0; Level.ToGrid(player.Position.X, player.Position.Z).Z > 1; tick++)
        {
            Assert.True(tick < 2 * Second, "The player never got into the refuge.");
            world.Tick(Forward);
        }

        return world;
    }

    /// <summary>Runs for <paramref name="seconds"/>: did the enemy ever stand in the refuge or its door, and what health was lost?</summary>
    private static (bool CameIn, int HealthLost) Wait(GameWorld world, ITarget enemy, int seconds, PlayerInput? input = null)
    {
        var start = world.Player.Health;
        var cameIn = false;
        for (var tick = 0; tick < seconds * Second; tick++)
        {
            world.Tick(input ?? PlayerInput.None);
            var (_, z) = Level.ToGrid(enemy.Body.Position.X, enemy.Body.Position.Z);
            cameIn |= z <= 3;
        }

        return (cameIn, start - world.Player.Health);
    }

    [Theory]
    [InlineData(RoomKind.Treasure)]
    [InlineData(RoomKind.Secret)]
    public void A_ghoul_does_not_follow_into_a_safe_room(RoomKind safe)
    {
        var world = Fled(safe, 'G');

        var (cameIn, lost) = Wait(world, world.Ghouls[0], 6);

        Assert.False(cameIn, "The ghoul came into the safe room.");
        Assert.Equal(0, lost);
    }

    [Fact]
    public void A_ghoul_does_follow_into_an_ordinary_room()
    {
        // The same chase into an ordinary room: the ghoul comes and attacks - so the test above proves something.
        var world = Fled(RoomKind.Corridor, 'G');

        var (cameIn, lost) = Wait(world, world.Ghouls[0], 6);

        Assert.True(cameIn);
        Assert.True(lost > 0);
    }

    [Theory]
    [InlineData(RoomKind.Treasure)]
    [InlineData(RoomKind.Secret)]
    public void A_bat_does_not_fly_into_a_safe_room_nor_spit_into_it(RoomKind safe)
    {
        var world = Fled(safe, 'B');

        var (cameIn, lost) = Wait(world, world.Bats[0], 8);

        Assert.False(cameIn, "The bat flew into the safe room.");
        Assert.Equal(0, lost);
    }

    [Fact]
    public void A_bat_does_go_after_the_player_in_an_ordinary_room()
    {
        var world = Fled(RoomKind.Corridor, 'B');

        var (_, lost) = Wait(world, world.Bats[0], 8);

        Assert.True(lost > 0);
    }

    [Fact]
    public void Leaving_the_safe_room_makes_the_player_a_target_again()
    {
        var world = Fled(RoomKind.Treasure, 'G');
        Wait(world, world.Ghouls[0], 2);

        // Back out through the door into the ghoul's room (the player still looks into the refuge).
        var (_, lost) = Wait(world, world.Ghouls[0], 5, Back);

        Assert.True(lost > 0);
    }

    [Fact]
    public void Enemies_see_safe_rooms_and_their_doors_as_wall_the_player_does_not()
    {
        var world = Fled(RoomKind.Treasure, 'G');

        Assert.True(world.EnemyLevel[2, 1].IsSolid);
        Assert.True(world.EnemyLevel[2, 3].IsSolid);
        Assert.False(world.EnemyLevel[2, 6].IsSolid);
        Assert.False(world.Level[2, 1].IsSolid);
        Assert.False(world.Level[2, 3].IsSolid);
    }
}
