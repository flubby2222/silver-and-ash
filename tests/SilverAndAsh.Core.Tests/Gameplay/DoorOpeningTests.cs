using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>How a door swings open (issue #245): a progress per tick, the time of its kind, away from the opener.</summary>
public class DoorOpeningTests
{
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };

    // The player starts in cell (1,0) looking along +Z; the door in cell (1,1) is 1 m in front of the eyes.
    private const string Hall = "#P#\n#.#\n#.#\n#.#\n#.#";

    private static GameWorld World(DoorKind kind = DoorKind.Normal, Balance? balance = null) =>
        new(LevelParser.Parse(Hall), 1, balance ?? Balance.Default, [new LevelDoor(1, 1, 0, 1, RoomSide.South, kind)]);

    private static int Ticks(DoorKind kind) => SimulationClock.SecondsToTicks(Balance.Default.Doors.OpenTime(kind));

    [Fact]
    public void A_closed_door_has_not_swung_at_all()
    {
        var world = World();

        RunTicks(world, PlayerInput.None, 30);

        Assert.Equal(0f, world.Doors[0].OpenFraction);
    }

    [Theory]
    [InlineData(DoorKind.Normal)]
    [InlineData(DoorKind.Boss)]
    public void The_leaves_swing_evenly_over_the_time_of_the_kind(DoorKind kind)
    {
        var world = World(kind);
        var door = world.Doors[0];
        var total = Ticks(kind);

        // The tick of F opens the door and already counts as the first tick of swinging.
        RunTicks(world, PressF, 1);
        Assert.Equal(1f / total, door.OpenFraction, 0.0001f);

        RunTicks(world, PlayerInput.None, (total / 2) - 1);
        Assert.Equal((float)(total / 2) / total, door.OpenFraction, 0.0001f);

        RunTicks(world, PlayerInput.None, total - (total / 2) - 1);
        Assert.True(door.OpenFraction < 1f, "One tick before its time the door must not be fully open yet.");

        RunTicks(world, PlayerInput.None, 1);
        Assert.Equal(1f, door.OpenFraction);

        RunTicks(world, PlayerInput.None, 60);
        Assert.Equal(1f, door.OpenFraction);
    }

    [Fact]
    public void The_times_come_from_the_balancing_values()
    {
        var slow = Balance.Default with { Doors = new DoorsBalance { NormalOpenTime = 2f } };
        var world = World(DoorKind.Normal, slow);

        RunTicks(world, PressF, 1);
        RunTicks(world, PlayerInput.None, SimulationClock.SecondsToTicks(1f) - 1);

        Assert.Equal(0.5f, world.Doors[0].OpenFraction, 0.0001f);
    }

    [Fact]
    public void Each_kind_takes_its_own_time_and_a_cracked_wall_none()
    {
        var doors = Balance.Default.Doors;

        Assert.Equal(doors.NormalOpenTime, doors.OpenTime(DoorKind.Normal));
        Assert.Equal(doors.LockedOpenTime, doors.OpenTime(DoorKind.Locked));
        Assert.Equal(doors.BossOpenTime, doors.OpenTime(DoorKind.Boss));
        Assert.Equal(0f, doors.OpenTime(DoorKind.Cracked));
        Assert.True(doors.NormalOpenTime < doors.LockedOpenTime && doors.LockedOpenTime < doors.BossOpenTime,
            "Heavier doors should take longer.");
    }

    [Fact]
    public void The_way_is_free_on_the_tick_the_door_opens_not_when_it_has_swung()
    {
        var world = World(DoorKind.Boss);

        RunTicks(world, PressF, 1);

        Assert.True(world.Doors[0].OpenFraction < 0.1f);
        Assert.False(world.Doors[0].Blocks);
        Assert.False(world.Level[1, 1].IsSolid);

        RunTicks(world, Forward, 60);
        Assert.True(world.Player.Position.Z > 3f, $"The player only got to z = {world.Player.Position.Z} through the swinging door.");
    }

    [Fact]
    public void Opened_from_the_north_the_leaves_swing_south_and_from_the_south_north()
    {
        var fromNorth = World();
        RunTicks(fromNorth, PressF, 1);

        // The same hall the other way round: the player starts south of the door and looks north at it.
        var map = "#.#\n#.#\n#.#\n#P#\n#.#";
        var fromSouth = new GameWorld(LevelParser.Parse(map), 1, Balance.Default, [new LevelDoor(1, 2, 0, 1, RoomSide.North)]);
        PhotoStudio.Face(fromSouth.Player, (fromSouth.Doors[0].BoxMin + fromSouth.Doors[0].BoxMax) / 2f);
        RunTicks(fromSouth, PressF, 1);

        Assert.True(fromNorth.Doors[0].IsOpen);
        Assert.True(fromNorth.Doors[0].OpenedFromLowSide);
        Assert.True(fromSouth.Doors[0].IsOpen);
        Assert.False(fromSouth.Doors[0].OpenedFromLowSide);
    }

    [Fact]
    public void A_door_with_an_east_west_passage_takes_the_side_along_x()
    {
        // The door in cell (2,1) lies between (1,1) and (3,1): the passage runs along X.
        const string Corridor = "#####\n#P..#\n#####";
        var fromWest = new GameWorld(LevelParser.Parse(Corridor), 1, Balance.Default, [new LevelDoor(2, 1, 0, 1, RoomSide.East)]);
        PhotoStudio.Face(fromWest.Player, (fromWest.Doors[0].BoxMin + fromWest.Doors[0].BoxMax) / 2f);
        RunTicks(fromWest, PressF, 1);

        const string Mirrored = "#####\n#..P#\n#####";
        var fromEast = new GameWorld(LevelParser.Parse(Mirrored), 1, Balance.Default, [new LevelDoor(2, 1, 0, 1, RoomSide.West)]);
        PhotoStudio.Face(fromEast.Player, (fromEast.Doors[0].BoxMin + fromEast.Doors[0].BoxMax) / 2f);
        RunTicks(fromEast, PressF, 1);

        Assert.True(fromWest.Doors[0].IsOpen);
        Assert.True(fromWest.Doors[0].OpenedFromLowSide);
        Assert.True(fromEast.Doors[0].IsOpen);
        Assert.False(fromEast.Doors[0].OpenedFromLowSide);
    }

    [Fact]
    public void The_same_presses_always_give_the_same_swing()
    {
        static float After(int ticks)
        {
            var world = World(DoorKind.Boss);
            RunTicks(world, PressF, 1);
            RunTicks(world, PlayerInput.None, ticks);
            return world.Doors[0].OpenFraction;
        }

        Assert.Equal(After(37), After(37));
    }
}
