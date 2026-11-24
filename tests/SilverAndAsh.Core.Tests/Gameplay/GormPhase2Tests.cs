using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>Gorm's phase 2 and the stones from the ceiling (decision record 0007, issue #166).</summary>
public class GormPhase2Tests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly GormBalance Values = Balance.Default.Gorm;

    /// <summary>A player who survives a lot - to watch many attacks in a row.</summary>
    private static readonly Balance Tough = Balance.Default with { Player = Balance.Default.Player with { MaxHealth = 100_000 } };

    /// <summary>The damage that brings him from full health to exactly the limit of phase 2.</summary>
    private static int DownToHalf(GormBalance gorm) => gorm.MaxHealth - (int)MathF.Floor(gorm.MaxHealth * gorm.Phase2Health);

    /// <summary>An open hall, 15 cells wide and 25 deep; the player in column 7, row 3, looking along +Z.</summary>
    private static Level Hall()
    {
        var rows = Enumerable.Range(0, 25).Select(z => new string(Enumerable.Range(0, 15).Select(x => (x, z) == (7, 3) ? 'P' : '.').ToArray()));
        return LevelParser.Parse(string.Join('\n', rows));
    }

    /// <summary>A corridor one cell wide between walls, the player in its middle - most of the floor around him is wall.</summary>
    private static Level Corridor()
    {
        var rows = Enumerable.Range(0, 25).Select(z => z == 3 ? "######P######" : "######.######");
        return LevelParser.Parse(string.Join('\n', rows));
    }

    /// <summary>Gorm and one player facing each other, ticked together.</summary>
    private sealed class Duel
    {
        public Duel(float distance, Balance? balance = null, Level? level = null, int seed = 1, IReadOnlySet<(int X, int Z)>? arena = null)
        {
            Level = level ?? Hall();
            Balance = balance ?? Tough;
            Player = new Player(Level, balance: Balance);
            Gorm = new Gorm(9, Player.Position + new Vector3(0f, 0f, distance), -Vector3.UnitZ, Balance, new Random(seed), arena);
        }

        public Level Level { get; }

        public Balance Balance { get; }

        public Player Player { get; }

        public Gorm Gorm { get; }

        public List<GameEvent> Events { get; } = [];

        public void Tick(PlayerInput input)
        {
            Player.Tick(input);
            Events.AddRange(Player.Events);
            var events = new List<GameEvent>();
            Gorm.Act(Level, [Player], [Player.Body], events);
            Events.AddRange(events);
        }

        public void Run(int ticks, PlayerInput? input = null, Func<bool>? until = null)
        {
            for (var tick = 0; tick < ticks && until?.Invoke() != true; tick++)
            {
                Tick(input ?? PlayerInput.None);
            }
        }

        /// <summary>Wounds him down to half his health and waits until his roar is over.</summary>
        public void IntoPhase2()
        {
            Gorm.TakeDamage(DownToHalf(Balance.Gorm), Events);
            Assert.True(Gorm.IsInPhase2);
            Run(SimulationClock.SecondsToTicks(Balance.Gorm.RoarTime));
            Assert.NotEqual(GormState.Roaring, Gorm.State);
        }

        /// <summary>Runs until his next hammer strikes (at most a minute); returns the events from that tick on.</summary>
        public List<GameEvent> UntilSlam()
        {
            Run(60 * Second, until: () => Gorm.State == GormState.WindingUp && Gorm.Attack == GormAttack.Hammer);
            Assert.True(Gorm is { State: GormState.WindingUp, Attack: GormAttack.Hammer }, "Gorm never wound up his hammer.");
            Run(10 * Second, until: () => Gorm.State == GormState.Slamming);
            Assert.Equal(GormState.Slamming, Gorm.State);
            return Events.SkipWhile(e => e is not GormSlammed).ToList();
        }

        public List<Vector3> StoneSpots() => Events.OfType<StoneFalling>().Select(stone => stone.At).ToList();
    }

    // ---- Phase 2 ----------------------------------------------------------------------------------------------------

    [Fact]
    public void Phase_2_begins_exactly_once_when_he_falls_to_half_his_health()
    {
        var gorm = new Gorm(9, Vector3.Zero);
        var events = new List<GameEvent>();

        gorm.TakeDamage(DownToHalf(Values) - 1, events);
        Assert.False(gorm.IsInPhase2);
        Assert.DoesNotContain(events, e => e is GormRoared);

        gorm.TakeDamage(1, events);
        Assert.True(gorm.IsInPhase2);
        Assert.Equal(GormState.Roaring, gorm.State);

        gorm.TakeDamage(1, events);
        gorm.TakeDamage(DownToHalf(Values) / 2, events);
        Assert.Single(events.OfType<GormRoared>());
        Assert.Equal(new GormRoared(9), events.OfType<GormRoared>().Single());
    }

    [Fact]
    public void A_deadly_blow_past_half_is_no_roar()
    {
        var gorm = new Gorm(9, Vector3.Zero);
        var events = new List<GameEvent>();

        gorm.TakeDamage(Values.MaxHealth, events);

        Assert.True(gorm.IsDead);
        Assert.DoesNotContain(events, e => e is GormRoared);
    }

    [Fact]
    public void While_roaring_he_does_not_attack_then_he_fights_on()
    {
        var duel = new Duel(3f);
        duel.Gorm.TakeDamage(DownToHalf(Values), duel.Events);

        duel.Run(SimulationClock.SecondsToTicks(Values.RoarTime) - 1);
        Assert.Equal(GormState.Roaring, duel.Gorm.State);
        Assert.DoesNotContain(duel.Events, e => e is GormWindUp);

        duel.Run(2);
        Assert.Contains(duel.Events, e => e is GormWindUp);
    }

    [Fact]
    public void The_roar_breaks_off_what_he_was_doing()
    {
        var duel = new Duel(3f);
        duel.Run(60 * Second, until: () => duel.Gorm.State == GormState.Slamming);
        Assert.NotNull(duel.Gorm.Shockwave);

        duel.Gorm.TakeDamage(DownToHalf(Values), duel.Events);

        Assert.Equal(GormState.Roaring, duel.Gorm.State);
        Assert.Null(duel.Gorm.Shockwave);
        Assert.Null(duel.Gorm.Attack);
    }

    [Fact]
    public void In_phase_2_he_walks_faster_by_the_set_factor()
    {
        static float Walked(bool phase2)
        {
            var duel = new Duel(Values.FarRange + 8f);
            if (phase2)
            {
                duel.IntoPhase2();
            }

            var start = duel.Gorm.Position;
            duel.Run(Second);
            Assert.Equal(GormState.Walking, duel.Gorm.State);
            return FlatDistance(start, duel.Gorm.Position);
        }

        Assert.Equal(Values.Speed, Walked(false), 0.1f);
        Assert.Equal(Values.Speed * Values.Phase2SpeedUp, Walked(true), 0.1f);
    }

    [Theory]
    [InlineData(GormAttack.Hammer)]
    [InlineData(GormAttack.Throw)]
    public void In_phase_2_his_hammer_and_throw_warnings_are_shorter_by_the_set_factor(GormAttack attack)
    {
        // Close up for the hammer, beyond his near range for the throw.
        static int WindUp(bool phase2, GormAttack attack)
        {
            var duel = new Duel(attack == GormAttack.Throw ? Values.NearRange + 2f : 3f);
            if (phase2)
            {
                duel.IntoPhase2();
            }

            duel.Run(60 * Second, until: () => duel.Gorm.State == GormState.WindingUp && duel.Gorm.Attack == attack);
            Assert.True(duel.Gorm.State == GormState.WindingUp && duel.Gorm.Attack == attack, $"Gorm never wound up his {attack}.");
            var ticks = 0;
            duel.Run(10 * Second, until: () => { ticks++; return duel.Gorm.State != GormState.WindingUp; });
            return ticks - 1;
        }

        var seconds = attack == GormAttack.Hammer ? Values.HammerWindUp : Values.ThrowWindUp;
        Assert.Equal(SimulationClock.SecondsToTicks(seconds), WindUp(false, attack));
        Assert.Equal(SimulationClock.SecondsToTicks(seconds / Values.Phase2SpeedUp), WindUp(true, attack));
    }

    [Fact]
    public void In_phase_2_his_swing_s_warning_sweep_and_pause_are_shorter_by_the_set_factor()
    {
        static (int WindUp, int Sweep, int Pause) Durations(bool phase2)
        {
            var duel = new Duel(3f);
            if (phase2)
            {
                duel.IntoPhase2();
            }

            duel.Run(60 * Second, until: () => duel.Gorm is { State: GormState.WindingUp, Attack: GormAttack.Swing });
            var windUp = 0;
            duel.Run(10 * Second, until: () => { windUp++; return duel.Gorm.State != GormState.WindingUp; });
            Assert.Equal(GormState.Swinging, duel.Gorm.State);
            var sweep = 0;
            duel.Run(10 * Second, until: () => { sweep++; return duel.Gorm.State != GormState.Swinging; });
            Assert.Equal(GormState.Recovering, duel.Gorm.State);
            var pause = 0;
            duel.Run(10 * Second, until: () => { pause++; return duel.Gorm.State != GormState.Recovering; });
            return (windUp - 1, sweep - 1, pause - 1);
        }

        static int Ticks(float seconds, bool phase2) => SimulationClock.SecondsToTicks(phase2 ? seconds / Values.Phase2SpeedUp : seconds);

        foreach (var phase2 in new[] { false, true })
        {
            Assert.Equal((Ticks(Values.SwingWindUp, phase2), Ticks(Values.SwingTime, phase2), Ticks(Values.PauseTime, phase2)), Durations(phase2));
        }
    }

    // ---- Stones -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Before_phase_2_his_hammer_brings_down_no_stones()
    {
        var duel = new Duel(3f);

        duel.UntilSlam();
        duel.Run(3 * Second);

        Assert.DoesNotContain(duel.Events, e => e is StoneFalling);
        Assert.Empty(duel.Gorm.Stones);
    }

    [Fact]
    public void In_phase_2_his_hammer_casts_the_set_number_of_shadows_near_the_player_landing_after_the_warning()
    {
        var duel = new Duel(3f);
        duel.IntoPhase2();

        var fromSlam = duel.UntilSlam();
        var playerAt = duel.Player.Position;

        var shadows = fromSlam.OfType<StoneFalling>().ToList();
        Assert.Equal(Values.StoneCount, shadows.Count);
        Assert.All(shadows, shadow => Assert.InRange(FlatDistance(shadow.At, playerAt), 0f, Values.StoneSpread));
        Assert.Equal(shadows.Select(shadow => shadow.At), duel.Gorm.Stones.Select(stone => stone.At));

        var warning = SimulationClock.SecondsToTicks(Values.StoneWarning);
        duel.Run(warning - 1);
        Assert.DoesNotContain(duel.Events, e => e is StoneLanded);
        Assert.Equal(Values.StoneCount, duel.Gorm.Stones.Count());

        duel.Run(1);
        Assert.Equal(shadows.Select(shadow => shadow.At).Order(Comparer<Vector3>.Create((a, b) => (a.X, a.Z).CompareTo((b.X, b.Z)))),
            duel.Events.OfType<StoneLanded>().Select(landed => landed.At).Order(Comparer<Vector3>.Create((a, b) => (a.X, a.Z).CompareTo((b.X, b.Z)))));
        Assert.Empty(duel.Gorm.Stones);
    }

    [Fact]
    public void A_stone_hits_whoever_stands_where_it_lands()
    {
        // All stones land right at the player's feet.
        var balance = Tough with { Gorm = Tough.Gorm with { StoneSpread = 0.01f } };
        var duel = new Duel(3f, balance);
        duel.IntoPhase2();
        duel.UntilSlam();

        duel.Run(SimulationClock.SecondsToTicks(Values.StoneWarning));

        Assert.Equal(Values.StoneCount, duel.Events.OfType<StoneHit>().Count(hit => hit.PlayerId == duel.Player.Id));
        Assert.Contains(duel.Events.SkipWhile(e => e is not StoneLanded), e => e is PlayerHit { Damage: var damage } && damage == Values.StoneDamage);
    }

    [Fact]
    public void Who_steps_out_of_the_shadow_is_not_hit()
    {
        var balance = Tough with { Gorm = Tough.Gorm with { StoneSpread = 0.01f } };
        var duel = new Duel(3f, balance);
        duel.IntoPhase2();
        duel.UntilSlam();

        // Sideways, until well out of the stones' radius.
        duel.Run(SimulationClock.SecondsToTicks(Values.StoneWarning), PlayerInput.None with { Move = new Vector2(1f, 0f) });

        Assert.Equal(Values.StoneCount, duel.Events.OfType<StoneLanded>().Count());
        Assert.All(duel.Events.OfType<StoneLanded>(), landed => Assert.True(FlatDistance(landed.At, duel.Player.Position) > Values.StoneRadius));
        Assert.DoesNotContain(duel.Events, e => e is StoneHit);
    }

    [Fact]
    public void A_stone_hits_only_within_its_radius()
    {
        // The stones land at the player's feet; the player takes a few steps aside and stands still - the same steps
        // every time. The radius is set just above or just below the distance they end up at.
        static (float Distance, bool Hit) StepAside(float radius)
        {
            var balance = Tough with { Gorm = Tough.Gorm with { StoneSpread = 0.0001f, StoneRadius = radius } };
            var duel = new Duel(3f, balance);
            duel.IntoPhase2();
            duel.UntilSlam();
            var spot = duel.Gorm.Stones.First().At;
            duel.Run(5, PlayerInput.None with { Move = new Vector2(1f, 0f) });
            duel.Run(2 * Second, until: () => duel.Events.OfType<StoneLanded>().Any());
            Assert.Contains(duel.Events, e => e is StoneLanded);
            return (FlatDistance(spot, duel.Player.Position), duel.Events.OfType<StoneHit>().Any());
        }

        var (distance, _) = StepAside(Values.StoneRadius);
        Assert.InRange(distance, 0.2f, 3f);
        Assert.Equal((distance, true), StepAside(distance + 0.005f));
        Assert.Equal((distance, false), StepAside(distance - 0.005f));
    }

    [Fact]
    public void Stones_fall_only_on_open_floor()
    {
        var balance = Tough with { Gorm = Tough.Gorm with { StoneCount = 40 } };
        var duel = new Duel(3f, balance, Corridor());
        duel.IntoPhase2();

        duel.UntilSlam();

        var spots = duel.StoneSpots();
        Assert.Equal(40, spots.Count);
        Assert.All(spots, spot =>
        {
            var (x, z) = Level.ToGrid(spot.X, spot.Z);
            Assert.True(duel.Level.IsInside(x, z) && !duel.Level[x, z].IsSolid, $"A stone fell into the wall at {spot}.");
            Assert.Equal(duel.Level[x, z].FloorHeight, spot.Y);
        });
    }

    /// <summary>
    /// Issue #608: a column stands on open floor, but no stone falls into it. Eight columns ring the player, so without
    /// the rule about one stone in eight would land in one.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Stones_never_fall_into_a_column(int seed)
    {
        Column[] columns =
        [
            Column.AtCell(6, 2), Column.AtCell(7, 2), Column.AtCell(8, 2), Column.AtCell(6, 3),
            Column.AtCell(8, 3), Column.AtCell(6, 4), Column.AtCell(8, 4), Column.AtCell(7, 1),
        ];
        var balance = Tough with { Gorm = Tough.Gorm with { StoneCount = 200 } };
        var duel = new Duel(3f, balance, Hall().WithColumns(columns), seed);
        duel.IntoPhase2();

        duel.UntilSlam();

        var spots = duel.StoneSpots();
        Assert.Equal(200, spots.Count);
        Assert.All(spots, spot => Assert.All(columns, column => Assert.True(
            Vector2.Distance(column.Centre, new Vector2(spot.X, spot.Z)) >= Column.Radius, $"A stone fell into the column at {column.Centre}: {spot}.")));
    }

    [Fact]
    public void Stones_fall_only_inside_his_room()
    {
        // His room: the three rows around the player. The rest of the hall is open, but not his.
        var arena = Enumerable.Range(2, 3).SelectMany(z => Enumerable.Range(0, 15).Select(x => (x, z))).ToHashSet();
        var balance = Tough with { Gorm = Tough.Gorm with { StoneCount = 40 } };
        var duel = new Duel(3f, balance, arena: arena);
        duel.IntoPhase2();

        duel.UntilSlam();

        var spots = duel.StoneSpots();
        Assert.Equal(40, spots.Count);
        Assert.All(spots, spot => Assert.Contains(Level.ToGrid(spot.X, spot.Z), arena));
    }

    [Fact]
    public void The_same_seed_brings_the_stones_down_on_the_same_spots()
    {
        static List<Vector3> Spots(int seed)
        {
            var duel = new Duel(3f, seed: seed);
            duel.IntoPhase2();
            duel.UntilSlam();
            return duel.StoneSpots();
        }

        Assert.Equal(Spots(3), Spots(3));
        Assert.NotEqual(Spots(3), Spots(4));
    }

    [Fact]
    public void When_he_dies_his_falling_stones_vanish()
    {
        var duel = new Duel(3f);
        duel.IntoPhase2();
        duel.UntilSlam();
        Assert.NotEmpty(duel.Gorm.Stones);

        duel.Gorm.TakeDamage(Values.MaxHealth, duel.Events);
        duel.Run(SimulationClock.SecondsToTicks(Values.StoneWarning) + 1);

        Assert.Empty(duel.Gorm.Stones);
        Assert.DoesNotContain(duel.Events, e => e is StoneLanded);
    }

    // ---- In the game world ------------------------------------------------------------------------------------------

    [Fact]
    public void In_the_game_world_his_stones_fall_only_in_the_boss_room()
    {
        // Room 0 (start) in row 0, door in row 1, the boss room in rows 2-11. The player stays right behind the door:
        // without the room as limit many stones would land in the start room.
        const string Map = "##.P.##\n###.###\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#..T..#\n#.....#";
        var bossRoom = Enumerable.Range(2, 10).SelectMany(z => Enumerable.Range(1, 5).Select(x => (x, z))).ToList();
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 7, 1, 0, [(2, 0), (3, 0), (4, 0)], [], []),
            new(1, RoomKind.Boss, "hall", 0, 2, 7, 10, 1, bossRoom, [], []),
        ];
        LevelDoor[] doors = [new(3, 1, 0, 1, RoomSide.South, DoorKind.Boss)];
        var balance = Tough with { Gorm = Tough.Gorm with { StoneCount = 40 } };
        var world = new GameWorld(LevelParser.Parse(Map), 1, balance, doors, rooms);
        var events = new List<GameEvent>();
        void Run(PlayerInput input, int ticks, Func<bool> until)
        {
            for (var tick = 0; tick < ticks && !until(); tick++)
            {
                world.Tick(input);
                events.AddRange(world.Events);
            }
        }

        Run(PlayerInput.None with { Interact = true }, 1, () => false);
        Run(PlayerInput.None with { Move = new Vector2(0f, 1f) }, 3 * Second, () => world.Rooms[1].IsSealed);
        Assert.True(world.Rooms[1].IsSealed, "The boss room never sealed - the test would prove nothing.");
        world.Gorm!.TakeDamage(DownToHalf(balance.Gorm), events);
        Run(PlayerInput.None, 60 * Second, () => events.Any(e => e is StoneFalling));

        var spots = events.OfType<StoneFalling>().Select(stone => Level.ToGrid(stone.At.X, stone.At.Z)).ToList();
        Assert.Equal(40, spots.Count);
        Assert.All(spots, spot => Assert.Contains(spot, bossRoom));
    }
}
