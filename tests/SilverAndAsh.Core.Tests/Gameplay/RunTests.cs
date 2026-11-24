using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class RunTests
{
    private static readonly IReadOnlyList<RoomTemplate> Templates = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    private static readonly LevelZero Catacombs = new(Templates);

    // Like ArenaTests: start room, door A, a room with one ghoul (here: the boss room or a combat room), door B, corridor.
    private const string Map = "#.P.#\n##.##\n#...#\n#...#\n#.G.#\n#...#\n##.##\n#...#\n#...#";

    private static readonly PlayerInput Forward = new(new Vector2(0f, 1f), Vector2.Zero, false);
    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };

    private static List<(int X, int Z)> Rows(int from, int to) =>
        Enumerable.Range(from, to - from + 1).SelectMany(z => new[] { (1, z), (2, z), (3, z) }).ToList();

    private static GameWorld World(RoomKind middle, bool withGhoul = true)
    {
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 5, 1, 0, Rows(0, 0), [], []),
            new(1, middle, "middle", 0, 2, 5, 4, 1, Rows(2, 5), [(2, 4)], []),
            new(2, RoomKind.Corridor, "corridor", 0, 7, 5, 2, 2, Rows(7, 8), [], []),
        ];
        LevelDoor[] doors = [new(2, 1, 0, 1, RoomSide.South), new(2, 6, 1, 2, RoomSide.South)];
        return new GameWorld(LevelParser.Parse(withGhoul ? Map : Map.Replace('G', '.')), 1, Balance.Default, doors, rooms);
    }

    private static List<GameEvent> EnterMiddleRoom(GameWorld world)
    {
        var events = Run(world, PressF, 1);
        events.AddRange(Run(world, Forward, 180, () => world.Rooms[1].IsEntered));
        return events;
    }

    private static string Layout(GameWorld world)
    {
        var level = world.LevelAsBuilt;
        return string.Concat(Enumerable.Range(0, level.Depth).SelectMany(z =>
            Enumerable.Range(0, level.Width).Select(x => level[x, z].IsSolid ? '#' : '.')));
    }

    [Fact]
    public void A_run_starts_in_the_start_room_of_a_generated_level_0()
    {
        var world = Catacombs.NewRun(123, Balance.Default);

        var start = world.Rooms[0];
        Assert.Equal(RoomKind.Start, start.Kind);
        Assert.Contains(Level.ToGrid(world.Player.Position.X, world.Player.Position.Z), start.Cells);
        Assert.Equal(123, world.Seed);
        Assert.Equal("THE CATACOMBS", world.LevelName);
        Assert.NotEmpty(world.Ghouls);
        Assert.NotEmpty(world.Doors);
        Assert.All(world.Doors, door => Assert.False(door.IsOpen));
    }

    [Fact]
    public void The_run_uses_the_given_balancing_values()
    {
        var balance = Balance.Default with { Layout = Balance.Default.Layout with { CombatRoomsMin = 3, CombatRoomsMax = 3 } };

        var world = Catacombs.NewRun(7, balance);

        Assert.Same(balance, world.Balance);
        Assert.Equal(3, world.Rooms.Count(room => room.Kind == RoomKind.Combat));
    }

    [Fact]
    public void The_same_seed_gives_the_same_run()
    {
        Assert.Equal(Layout(Catacombs.NewRun(55, Balance.Default)), Layout(Catacombs.NewRun(55, Balance.Default)));
    }

    [Fact]
    public void After_a_run_the_next_one_has_a_new_seed_and_a_new_layout()
    {
        var balance = Balance.Default with { Difficulty = Balance.Default.Difficulty with { BudgetNear = 12 } };
        var first = Catacombs.NewRun(55, balance);

        var next = Catacombs.NextRun(first);

        Assert.NotEqual(first.Seed, next.Seed);
        Assert.Equal(next.Seed, Catacombs.NextRun(first).Seed);
        Assert.NotEqual(Layout(first), Layout(next));
        Assert.Same(first.Balance, next.Balance);
        Assert.Equal("THE CATACOMBS", next.LevelName);
    }

    [Fact]
    public void Hand_made_levels_have_no_name()
    {
        Assert.Null(new GameWorld(LevelParser.Parse(Map)).LevelName);
    }

    /// <summary>Waits until the stairs stand and presses F (the player looks at them from the boss room's entrance).</summary>
    private static List<GameEvent> ClimbStairs(GameWorld world)
    {
        var events = Run(world, PlayerInput.None, SimulationClock.SecondsToTicks(Balance.Default.Gorm.StairsRiseTime) + 1);
        events.AddRange(Run(world, PressF, 1));
        return events;
    }

    [Fact]
    public void Clearing_the_boss_room_raises_the_stairs_but_does_not_win_yet()
    {
        var world = World(RoomKind.Boss);
        EnterMiddleRoom(world);
        Assert.Null(world.Stairs);

        world.Ghouls[0].TakeDamage(10_000, []);
        var events = Run(world, PlayerInput.None, 1);

        Assert.Contains(events, e => e is StairsRose);
        Assert.NotNull(world.Stairs);
        Assert.False(world.IsWon);
        Assert.DoesNotContain(events, e => e is RunWon);
    }

    [Fact]
    public void The_stairs_rise_in_the_middle_of_the_boss_room()
    {
        var world = World(RoomKind.Boss, withGhoul: false);

        EnterMiddleRoom(world);

        // The boss room is rows 2-5, columns 1-3: its middle lies between rows 3 and 4 - the earlier row wins.
        var stairs = Assert.IsType<Stairs>(world.Stairs);
        Assert.Equal(2.5f * Level.CellSize, stairs.Position.X, 0.01f);
        Assert.Equal(3.5f * Level.CellSize, stairs.Position.Z, 0.01f);
    }

    [Fact]
    public void Climbing_the_risen_stairs_wins_the_run()
    {
        var world = World(RoomKind.Boss, withGhoul: false);
        EnterMiddleRoom(world);

        var events = ClimbStairs(world);

        Assert.True(world.IsWon);
        Assert.Contains(new StairsClimbed(world.Player.Id), events);
        Assert.Single(events, e => e is RunWon);
    }

    [Fact]
    public void The_stairs_cannot_be_climbed_while_they_rise()
    {
        var world = World(RoomKind.Boss, withGhoul: false);
        EnterMiddleRoom(world);

        Run(world, PressF, 5);

        Assert.False(world.Stairs!.CanInteract);
        Assert.False(world.IsWon);
    }

    [Fact]
    public void The_stairs_say_what_F_does()
    {
        var world = World(RoomKind.Boss, withGhoul: false);
        EnterMiddleRoom(world);
        Run(world, PlayerInput.None, SimulationClock.SecondsToTicks(Balance.Default.Gorm.StairsRiseTime) + 1);

        var target = world.InteractionTarget(world.Player);

        Assert.Same(world.Stairs, target);
        Assert.Equal(new InteractionOffer("Climb the stairs", true), target!.OfferFor(world.Player));
    }

    [Fact]
    public void Clearing_a_combat_room_does_not_win()
    {
        var world = World(RoomKind.Combat);
        EnterMiddleRoom(world);

        world.Ghouls[0].TakeDamage(10_000, []);
        var events = Run(world, PlayerInput.None, 30);

        Assert.False(world.IsWon);
        Assert.DoesNotContain(events, e => e is RunWon);
        Assert.Null(world.Stairs);
        Assert.DoesNotContain(events, e => e is StairsRose);
    }

    [Fact]
    public void A_won_run_is_reported_once()
    {
        var world = World(RoomKind.Boss, withGhoul: false);
        EnterMiddleRoom(world);

        var events = ClimbStairs(world);
        events.AddRange(Run(world, PressF, 5));
        events.AddRange(Run(world, PlayerInput.None, 60));

        Assert.Single(events, e => e is RunWon);
    }

    [Fact]
    public void When_every_player_is_dead_the_run_is_lost_once()
    {
        var world = World(RoomKind.Combat);
        world.Player.TakeDamage(world.Player.MaxHealth, []);

        var events = Run(world, PlayerInput.None, 60);

        Assert.True(world.IsLost);
        Assert.Single(events, e => e is RunLost);
    }

    [Fact]
    public void A_living_player_has_not_lost()
    {
        var world = World(RoomKind.Combat);

        var events = Run(world, PlayerInput.None, 60);

        Assert.False(world.IsLost);
        Assert.DoesNotContain(events, e => e is RunLost);
    }

    [Fact]
    public void Dying_after_the_victory_does_not_lose_the_run()
    {
        var world = World(RoomKind.Boss, withGhoul: false);
        EnterMiddleRoom(world);
        ClimbStairs(world);

        world.Player.TakeDamage(world.Player.MaxHealth, []);
        var events = Run(world, PlayerInput.None, 10);

        Assert.True(world.IsWon);
        Assert.False(world.IsLost);
        Assert.DoesNotContain(events, e => e is RunLost);
    }
}
