using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

// Issue #436, part 1: the rules of the teleporters, driven through the real tick with Balance.Default.
public class TeleporterTests
{
    // A hall without rooms. Teleporter 0 stands on the player's start, 1 further south on the way, 2 aside in a corner.
    private const string Hall = "#####\n#.P.#\n#...#\n#...#\n#...#\n#...#\n#####";

    private static readonly TeleporterPlan[] HallTeleporters =
    [
        new(2, 1, 0, TeleporterSite.Start),
        new(2, 4, 0, TeleporterSite.Start),
        new(1, 5, 0, TeleporterSite.Start),
    ];

    private static readonly PlayerInput Forward = new(new Vector2(0f, 1f), Vector2.Zero, false);

    private static GameWorld HallWorld(Balance? balance = null) =>
        new(LevelParser.Parse(Hall), 1, balance ?? Balance.Default, teleporters: HallTeleporters);

    // Walks from the start (teleporter 0) to teleporter 1 and stops on it the tick it is unlocked: 0 and 1 are
    // unlocked, 2 is not, and the player stands on 1.
    private static GameWorld OnSecondTeleporter()
    {
        var world = HallWorld();
        Run(world, Forward, 240, () => world.Teleporters[1].IsUnlocked);
        Assert.True(world.Teleporters[1].IsUnlocked, "the walk never reached teleporter 1");
        Assert.Same(world.Teleporters[1], world.TeleporterUnder(world.Player));
        return world;
    }

    private static Vector2 Ground(Vector3 position) => new(position.X, position.Z);

    // Two equal worlds get the same tick, once with the request and once without: a refused request must leave the
    // world exactly as no request would.
    private static void AssertRefused(Func<GameWorld> arrange, int? target, Action<GameWorld>? before = null)
    {
        var asked = arrange();
        var notAsked = arrange();
        before?.Invoke(asked);
        before?.Invoke(notAsked);

        asked.Tick(PlayerInput.None with { TeleportTo = target });
        notAsked.Tick(PlayerInput.None);

        Assert.DoesNotContain(asked.Events, e => e is PlayerTeleported);
        Assert.Equal(notAsked.Player.Position, asked.Player.Position);
        Assert.Equal(notAsked.Events, asked.Events);
    }

    [Fact]
    public void Stepping_onto_a_teleporter_unlocks_it_once_for_good_and_raises_one_event()
    {
        var world = HallWorld();
        Assert.All(world.Teleporters, teleporter => Assert.False(teleporter.IsUnlocked));

        var events = Run(world, Forward, 240, () => world.Teleporters[1].IsUnlocked);
        events.AddRange(Run(world, Forward, 30));   // on and over it: no second event

        var unlocked = events.OfType<TeleporterUnlocked>().ToList();
        Assert.Equal([0, 1], unlocked.Select(e => e.TeleporterId));
        Assert.All(unlocked, e => Assert.Equal(world.Player.Id, e.PlayerId));
        Assert.Equal(world.Teleporters[1].Position, unlocked[1].Position);
        Assert.True(world.Teleporters[0].IsUnlocked);
        Assert.False(world.Teleporters[2].IsUnlocked);
    }

    [Theory]
    [InlineData(2.05f, true)]
    [InlineData(1.95f, false)]
    public void The_radius_comes_from_the_balancing_values(float radius, bool unlocks)
    {
        // Teleporter 1 stands one cell (2 m) east of the start; the player does not move.
        TeleporterPlan[] plans = [new(3, 1, 0, TeleporterSite.Start)];
        var balance = Balance.Default with { Teleporters = new TeleportersBalance { Radius = radius } };
        var world = new GameWorld(LevelParser.Parse(Hall), 1, balance, teleporters: plans);

        Run(world, PlayerInput.None, 1);

        Assert.Equal(2f, Vector2.Distance(Ground(world.Player.Position), Ground(world.Teleporters[0].Position)), 3);
        Assert.Equal(unlocks, world.Teleporters[0].IsUnlocked);
    }

    [Fact]
    public void A_player_on_an_unlocked_teleporter_travels_to_another_unlocked_one_and_back()
    {
        var world = OnSecondTeleporter();
        var from = world.Teleporters[1].Position;
        var to = world.Teleporters[0].Position;

        world.Tick(PlayerInput.None with { TeleportTo = 0 });

        Assert.Equal(Ground(to), Ground(world.Player.Position));
        var teleported = Assert.Single(world.Events.OfType<PlayerTeleported>());
        Assert.Equal(new PlayerTeleported(world.Player.Id, 1, 0, from, to), teleported);

        world.Tick(PlayerInput.None with { TeleportTo = 1 });

        Assert.Equal(Ground(from), Ground(world.Player.Position));
        Assert.Single(world.Events.OfType<PlayerTeleported>());
    }

    // Review finding: a dodge or dash started in the travel tick must not carry the player off the target afterwards.
    private static void AssertStaysOnTarget(GameWorld world, PlayerInput travel)
    {
        world.Tick(travel);
        Assert.Single(world.Events.OfType<PlayerTeleported>());

        Run(world, PlayerInput.None, 60);

        Assert.Same(world.Teleporters[0], world.TeleporterUnder(world.Player));
        Assert.Equal(0f, Vector2.Distance(Ground(world.Teleporters[0].Position), Ground(world.Player.Position)), 3);
    }

    [Fact]
    public void A_dodge_started_in_the_travel_tick_ends_with_the_travel()
    {
        // On the ground the dash button dodges.
        var world = OnSecondTeleporter();
        Assert.True(world.Player.IsGrounded);

        AssertStaysOnTarget(world, Forward with { Dash = true, TeleportTo = 0 });
    }

    [Fact]
    public void A_dash_started_in_the_travel_tick_ends_with_the_travel()
    {
        // In the air the dash button dashes; the teleporter still counts below the jumping player.
        var world = OnSecondTeleporter();
        Run(world, PlayerInput.None with { Jump = true }, 1);
        Run(world, PlayerInput.None, 30, () => !world.Player.IsGrounded);
        Assert.False(world.Player.IsGrounded);
        Assert.Same(world.Teleporters[1], world.TeleporterUnder(world.Player));

        AssertStaysOnTarget(world, Forward with { Dash = true, TeleportTo = 0 });
    }

    [Fact]
    public void A_locked_target_is_refused()
    {
        AssertRefused(OnSecondTeleporter, 2);
    }

    [Fact]
    public void The_teleporter_the_player_stands_on_is_refused_as_target()
    {
        AssertRefused(OnSecondTeleporter, 1);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void A_teleporter_number_that_does_not_exist_is_refused(int target)
    {
        AssertRefused(OnSecondTeleporter, target);
    }

    [Fact]
    public void A_player_who_stands_on_no_teleporter_cannot_travel()
    {
        GameWorld Past()
        {
            var world = OnSecondTeleporter();
            Run(world, Forward, 120, () => world.TeleporterUnder(world.Player) is null);
            Assert.Null(world.TeleporterUnder(world.Player));
            return world;
        }

        AssertRefused(Past, 0);
    }

    [Fact]
    public void A_dead_player_cannot_travel()
    {
        AssertRefused(OnSecondTeleporter, 0, world => world.Player.TakeDamage(10_000, []));
    }

    [Fact]
    public void A_dead_player_unlocks_nothing()
    {
        // Teleporter 0 stands on the start: dead before the first tick, the player never unlocks it.
        var world = HallWorld();
        world.Player.TakeDamage(10_000, []);

        var events = Run(world, PlayerInput.None, 5);

        Assert.False(world.Teleporters[0].IsUnlocked);
        Assert.DoesNotContain(events, e => e is TeleporterUnlocked);
    }

    [Fact]
    public void A_restart_keeps_the_teleporters_but_locks_them_again()
    {
        var world = OnSecondTeleporter();

        var restarted = world.Restarted();

        Assert.Equal(HallTeleporters, restarted.Teleporters.Select(teleporter => teleporter.Plan));
        Assert.All(restarted.Teleporters, teleporter => Assert.False(teleporter.IsUnlocked));
    }

    // Like RunTests: start room, door, a combat room with one ghoul, door, corridor. Teleporter 0 on the start,
    // teleporter 1 inside the combat room.
    private const string Arena = "#.P.#\n##.##\n#...#\n#...#\n#.G.#\n#...#\n##.##\n#...#\n#...#";

    private static List<(int X, int Z)> Rows(int from, int to) =>
        Enumerable.Range(from, to - from + 1).SelectMany(z => new[] { (1, z), (2, z), (3, z) }).ToList();

    // The player has unlocked the start teleporter, walked into the combat room - which sealed - and stands on its teleporter.
    private static GameWorld InSealedRoom()
    {
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 5, 1, 0, Rows(0, 0), [], []),
            new(1, RoomKind.Combat, "middle", 0, 2, 5, 4, 1, Rows(2, 5), [(2, 4)], []),
            new(2, RoomKind.Corridor, "corridor", 0, 7, 5, 2, 2, Rows(7, 8), [], []),
        ];
        LevelDoor[] doors = [new(2, 1, 0, 1, RoomSide.South), new(2, 6, 1, 2, RoomSide.South)];
        TeleporterPlan[] teleporters = [new(2, 0, 0, TeleporterSite.Start), new(2, 3, 1, TeleporterSite.BossGate)];
        var world = new GameWorld(LevelParser.Parse(Arena), 1, Balance.Default, doors, rooms, teleporters: teleporters);

        Run(world, PlayerInput.None with { Interact = true }, 1);   // opens the door
        Run(world, Forward, 240, () => world.Teleporters[1].IsUnlocked);

        Assert.True(world.Teleporters[0].IsUnlocked);
        Assert.True(world.Teleporters[1].IsUnlocked, "the walk never reached the teleporter in the combat room");
        Assert.True(world.Rooms[1].IsSealed);
        return world;
    }

    [Fact]
    public void A_teleporter_in_a_sealed_combat_room_rests_until_the_fight_is_over()
    {
        AssertRefused(InSealedRoom, 0);

        // Counter-check: the same place works once the room is cleared.
        var world = InSealedRoom();
        world.Ghouls[0].TakeDamage(10_000, []);
        Run(world, PlayerInput.None, 60, () => !world.Rooms[1].IsSealed);
        Assert.False(world.Rooms[1].IsSealed);
        Assert.NotNull(world.TeleporterUnder(world.Player));

        world.Tick(PlayerInput.None with { TeleportTo = 0 });

        Assert.Equal(Ground(world.Teleporters[0].Position), Ground(world.Player.Position));
    }

    // ---- Issue #480 ----

    [Fact]
    public void The_whole_way_from_the_choice_to_the_destination_runs_through_the_tick()
    {
        var world = OnSecondTeleporter();

        // The choice lists the unlocked teleporters only: 1 is where the player stands, 0 is free, 2 is still locked.
        Assert.Equal(
            [new TravelChoice(world.Teleporters[0], TravelState.Ready), new TravelChoice(world.Teleporters[1], TravelState.Here)],
            world.TravelChoices(world.Player));

        var target = world.TravelChoices(world.Player).First(choice => choice.CanTravel).Teleporter.Id;
        var events = Run(world, PlayerInput.None with { TeleportTo = target }, 1);
        events.AddRange(Run(world, PlayerInput.None, 30));

        Assert.Single(events.OfType<PlayerTeleported>());
        Assert.Same(world.Teleporters[0], world.TeleporterUnder(world.Player));
        Assert.Equal(Ground(world.Teleporters[0].Position), Ground(world.Player.Position));
        Assert.Equal(world.Teleporters[0].Position.Y, world.Player.Position.Y, 3);
        Assert.Equal(TravelState.Here, world.TravelChoices(world.Player).Single(choice => choice.Teleporter.Id == 0).State);
    }

    [Fact]
    public void A_player_who_stands_on_no_teleporter_gets_no_choice()
    {
        var world = OnSecondTeleporter();
        Run(world, Forward, 120, () => world.TeleporterUnder(world.Player) is null);

        Assert.Empty(world.TravelChoices(world.Player));
    }

    // Trap 1: the reach must not count a teleporter above or below the feet. Teleporter 0 stands one cell (2 m) east
    // of the start; the radius would reach it on the ground plane.
    [Fact]
    public void A_teleporter_on_a_platform_above_the_feet_is_out_of_reach()
    {
        TeleporterPlan[] plans = [new(3, 1, 0, TeleporterSite.Start)];
        var balance = Balance.Default with { Teleporters = Balance.Default.Teleporters with { Radius = 2.05f } };
        var world = new GameWorld(LevelParser.Parse("#####\n#.P3#\n#...#\n#####"), 1, balance, teleporters: plans);

        Run(world, PlayerInput.None, 5);

        Assert.True(world.Teleporters[0].Position.Y > world.Player.Position.Y + 0.5f);
        Assert.False(world.Teleporters[0].IsUnlocked);
    }

    [Theory]
    [InlineData(0.95f, false)]
    [InlineData(1.05f, true)]
    public void How_high_above_a_teleporter_the_feet_still_count_comes_from_the_balancing_values(float height, bool unlocks)
    {
        // Teleporter 0 stands in a pit 1 m below the floor the player stands on.
        TeleporterPlan[] plans = [new(3, 1, 0, TeleporterSite.Start)];
        var balance = Balance.Default with { Teleporters = Balance.Default.Teleporters with { Radius = 2.05f, Height = height } };
        var world = new GameWorld(LevelParser.Parse("#####\n#.P_#\n#...#\n#####"), 1, balance, teleporters: plans);

        Run(world, PlayerInput.None, 5);

        Assert.Equal(1f, world.Player.Position.Y - world.Teleporters[0].Position.Y, 3);
        Assert.Equal(unlocks, world.Teleporters[0].IsUnlocked);
    }

    [Fact]
    public void A_jump_does_not_leave_the_teleporter_with_the_default_height()
    {
        var world = OnSecondTeleporter();
        Run(world, PlayerInput.None with { Jump = true }, 1);
        var highest = 0f;
        Run(world, PlayerInput.None, 120, () =>
        {
            highest = MathF.Max(highest, world.Player.Position.Y - world.Teleporters[1].Position.Y);
            Assert.Same(world.Teleporters[1], world.TeleporterUnder(world.Player));
            return highest > 0.1f && world.Player.IsGrounded;
        });

        Assert.True(highest > 1f, $"the jump only reached {highest} m");
    }

    // Trap 2: a strike or shot in the travel tick. The attack happens where the player stands and the travel is refused;
    // one tick later the travel works (counter-check).
    private static void AssertAttackWinsOverTravel(Func<int, PlayerInput> attack, Func<GameEvent, bool> isAttack)
    {
        var probe = OnSecondTeleporter();
        var ticks = 0;
        do
        {
            probe.Tick(attack(ticks));
            ticks++;
            Assert.True(ticks < 120, "the attack never came");
        }
        while (!probe.Events.Any(isAttack));

        var asked = OnSecondTeleporter();
        var notAsked = OnSecondTeleporter();
        for (var tick = 0; tick < ticks - 1; tick++)
        {
            asked.Tick(attack(tick));
            notAsked.Tick(attack(tick));
        }

        asked.Tick(attack(ticks - 1) with { TeleportTo = 0 });
        notAsked.Tick(attack(ticks - 1));

        Assert.Contains(asked.Events, e => isAttack(e));
        Assert.DoesNotContain(asked.Events, e => e is PlayerTeleported);
        Assert.Equal(notAsked.Player.Position, asked.Player.Position);
        Assert.Equal(notAsked.Events, asked.Events);

        asked.Tick(PlayerInput.None with { TeleportTo = 0 });
        Assert.Single(asked.Events.OfType<PlayerTeleported>());
    }

    [Fact]
    public void A_strike_in_the_travel_tick_happens_here_and_the_travel_is_refused()
    {
        // The blade swings when the button is let go: pressed for one tick, released in the next.
        AssertAttackWinsOverTravel(tick => PlayerInput.None with { Melee = tick == 0 }, e => e is MeleeSwung);
    }

    [Fact]
    public void A_shot_in_the_travel_tick_happens_here_and_the_travel_is_refused()
    {
        AssertAttackWinsOverTravel(_ => PlayerInput.None with { Shoot = true }, e => e is ShotFired);
    }

    // Trap 3: in co-op another player may have sealed the room of the destination. With one player the same state comes
    // from a combat room without doors: walked in, it seals (and its teleporter is unlocked); walked out again, the player
    // stands on the start teleporter while the fight behind them is still on.
    private const string OpenArena = "#####\n#.P.#\n#...#\n#...#\n#...#\n#...#\n#...#\n#.G.#\n#####";

    private static GameWorld DestinationSealed()
    {
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 1, 5, 2, 0, Rows(1, 2), [], []),
            new(1, RoomKind.Combat, "middle", 0, 3, 5, 5, 1, Rows(3, 7), [(2, 7)], []),
        ];
        TeleporterPlan[] teleporters = [new(2, 1, 0, TeleporterSite.Start), new(2, 3, 1, TeleporterSite.BossGate)];
        var world = new GameWorld(LevelParser.Parse(OpenArena), 1, Balance.Default, [], rooms, teleporters: teleporters);

        Run(world, Forward, 240, () => world.Teleporters[1].IsUnlocked);
        Assert.True(world.Teleporters[1].IsUnlocked, "the walk never reached the teleporter in the combat room");
        Assert.True(world.Rooms[1].IsSealed);

        Run(world, Forward with { Move = new Vector2(0f, -1f) }, 240, () => world.TeleporterUnder(world.Player) is not null);
        Assert.Same(world.Teleporters[0], world.TeleporterUnder(world.Player));
        Assert.True(world.Rooms[1].IsSealed);
        Assert.False(world.Player.IsDead);
        return world;
    }

    [Fact]
    public void A_destination_in_a_sealed_room_is_shown_as_sealed_and_refused()
    {
        var world = DestinationSealed();
        Assert.Equal(TravelState.Sealed, world.TravelChoices(world.Player).Single(choice => choice.Teleporter.Id == 1).State);
        Assert.False(world.TravelChoices(world.Player).Single(choice => choice.Teleporter.Id == 1).CanTravel);

        AssertRefused(DestinationSealed, 1);

        // Counter-check: once the fight is over the same destination is free.
        world.Ghouls[0].TakeDamage(10_000, []);
        Run(world, PlayerInput.None, 60, () => !world.Rooms[1].IsSealed);
        Assert.False(world.Rooms[1].IsSealed);
        Assert.Equal(TravelState.Ready, world.TravelChoices(world.Player).Single(choice => choice.Teleporter.Id == 1).State);

        world.Tick(PlayerInput.None with { TeleportTo = 1 });

        Assert.Equal(Ground(world.Teleporters[1].Position), Ground(world.Player.Position));
    }
}
