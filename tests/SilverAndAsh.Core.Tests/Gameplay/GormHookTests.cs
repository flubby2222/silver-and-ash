using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Issue #437: Gorm's hook leads a running player and locks shortly before the throw; after a long chase he throws
/// anyway, with a warning first. A missed hook lies on the floor until the pause ends.
/// </summary>
public class GormHookTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly GormBalance Values = Balance.Default.Gorm;
    private static readonly int LockTicks = SimulationClock.SecondsToTicks(Values.HookLockTime);
    private static readonly int ChaseThrowTicks = SimulationClock.SecondsToTicks(Values.ChaseThrowTime);
    private static readonly int ChaseWarnTicks = SimulationClock.SecondsToTicks(Values.ChaseWarnTime);

    /// <summary>A player who survives a lot - so a hit does not end the duel.</summary>
    private static readonly Balance Tough = Balance.Default with { Player = Balance.Default.Player with { MaxHealth = 100_000 } };

    private static readonly PlayerInput RunRight = PlayerInput.None with { Move = new Vector2(1f, 0f) };

    /// <summary>A hall 15 cells wide; the player in column 7 of row <paramref name="playerRow"/>, looking along +Z.</summary>
    private static Level Hall(int depth = 25, int playerRow = 3, Func<int, int, bool>? wall = null)
    {
        var rows = Enumerable.Range(0, depth).Select(z => new string(Enumerable.Range(0, 15).Select(x =>
            (x, z) == (7, playerRow) ? 'P' : wall?.Invoke(x, z) == true ? '#' : '.').ToArray()));
        return LevelParser.Parse(string.Join('\n', rows));
    }

    /// <summary>Gorm and one player in a hall, ticked together; Gorm stands <c>distance</c> m along +Z from the player (behind them if negative), facing them.</summary>
    private sealed class Duel
    {
        public Duel(float distance, Balance? balance = null, Level? level = null)
        {
            Level = level ?? Hall();
            Balance = balance ?? Tough;
            Player = new Player(Level, balance: Balance);
            var facing = distance >= 0f ? -Vector3.UnitZ : Vector3.UnitZ;
            Gorm = new Gorm(9, Player.Position + new Vector3(0f, 0f, distance), facing, Balance, new Random(1));
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

        public bool Saw<T>() => Events.OfType<T>().Any();

        /// <summary>Runs with <paramref name="input"/> until Gorm's aim locks (at most two seconds).</summary>
        public GormHookLocked UntilLocked(PlayerInput? input = null)
        {
            Run(2 * Second, input, Saw<GormHookLocked>);
            return Assert.Single(Events.OfType<GormHookLocked>());
        }

        /// <summary>Runs with <paramref name="input"/> until the hook has hit or stopped (at most three seconds).</summary>
        public void UntilHookEnds(PlayerInput? input = null) =>
            Run(3 * Second, input, () => Saw<HookHit>() || Saw<HookStopped>());
    }

    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    // ---- Lead ------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(9f)]
    [InlineData(12f)]
    public void The_hook_hits_a_player_who_runs_on_at_a_steady_pace(float distance)
    {
        var duel = new Duel(distance);

        duel.UntilLocked(RunRight);
        duel.UntilHookEnds(RunRight);

        Assert.True(duel.Saw<HookHit>(), $"The hook missed a steady runner at {distance} m.");
    }

    [Fact]
    public void Without_a_lead_the_same_runner_escapes()
    {
        var duel = new Duel(12f, Tough with { Gorm = Values with { HookLead = 0f } });

        duel.UntilLocked(RunRight);
        duel.UntilHookEnds(RunRight);

        Assert.False(duel.Saw<HookHit>());
        Assert.True(duel.Saw<HookStopped>());
    }

    [Fact]
    public void The_lead_is_at_most_its_maximum_ahead()
    {
        // At 25 m the hook needs well over a second: a runner gets farther than the cap allows.
        var duel = new Duel(25f);

        var locked = duel.UntilLocked(RunRight);

        Assert.Equal(Values.HookLeadMax, FlatDistance(locked.Aim, duel.Player.Position), 3);
    }

    [Fact]
    public void A_standing_player_is_aimed_at_where_they_stand()
    {
        var duel = new Duel(12f);

        var locked = duel.UntilLocked();

        Assert.Equal(0f, FlatDistance(locked.Aim, duel.Player.Position), 3);
    }

    // ---- Lock ------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_aim_locks_the_lock_time_before_the_throw_and_he_stands_rigid()
    {
        var duel = new Duel(12f);
        duel.UntilLocked(RunRight);
        var facing = duel.Gorm.Facing;
        Assert.Equal(GormState.WindingUp, duel.Gorm.State);
        Assert.NotNull(duel.Gorm.HookAim);

        var ticks = 0;
        while (!duel.Saw<GormThrew>() && ticks < 2 * Second)
        {
            duel.Tick(RunRight);
            ticks++;
            Assert.Equal(facing, duel.Gorm.Facing);
        }

        Assert.Equal(LockTicks, ticks);
        Assert.Null(duel.Gorm.HookAim);
    }

    [Fact]
    public void Stopping_after_the_lock_escapes_the_hook()
    {
        var duel = new Duel(12f);

        duel.UntilLocked(RunRight);
        duel.UntilHookEnds();

        Assert.False(duel.Saw<HookHit>());
        Assert.True(duel.Saw<HookStopped>());
    }

    [Fact]
    public void Dodging_after_the_lock_escapes_the_hook()
    {
        var duel = new Duel(12f);

        duel.UntilLocked(RunRight);
        duel.Tick(RunRight with { Dash = true });
        duel.UntilHookEnds(RunRight);

        Assert.True(duel.Saw<PlayerDodged>());
        Assert.False(duel.Saw<HookHit>());
        Assert.True(duel.Saw<HookStopped>());
    }

    [Fact]
    public void If_a_pillar_hides_the_lead_he_aims_where_the_player_stands()
    {
        // The pillar stands between his hand and the point ahead of the runner, not between him and the runner.
        var open = new Duel(9f);
        var openAim = open.UntilLocked(RunRight).Aim;
        var hidden = new Duel(9f, level: Hall(wall: (x, z) => (x, z) == (5, 5)));
        var hiddenAim = hidden.UntilLocked(RunRight).Aim;

        Assert.True(FlatDistance(openAim, open.Player.Position) > 1f, "Without the pillar he leads the runner.");
        Assert.Equal(0f, FlatDistance(hiddenAim, hidden.Player.Position), 3);
    }

    [Fact]
    public void If_his_target_dies_before_the_lock_he_throws_straight_on()
    {
        var duel = new Duel(12f, Balance.Default);
        duel.Tick(RunRight);
        Assert.Equal(GormAttack.Throw, duel.Gorm.Attack);
        var facing = duel.Gorm.Facing;

        duel.Player.TakeDamage(Balance.Default.Player.MaxHealth, []);
        var locked = duel.UntilLocked();
        duel.Run(Second, until: duel.Saw<GormThrew>);

        var expected = duel.Gorm.Position + (facing * Values.FarRange);
        Assert.Equal(0f, FlatDistance(locked.Aim, expected), 3);
        Assert.Equal(1f, Vector3.Dot(facing, Assert.Single(duel.Events.OfType<GormThrew>()).Direction), 4);
    }

    // ---- Missed hook -----------------------------------------------------------------------------------------------

    [Fact]
    public void A_missed_hook_lies_on_the_floor_until_his_pause_ends()
    {
        var duel = new Duel(12f);
        duel.UntilLocked(RunRight);
        var last = Vector3.Zero;
        for (var tick = 0; tick < 3 * Second && !duel.Saw<HookStopped>(); tick++)
        {
            last = duel.Gorm.Hook ?? last;
            Assert.Null(duel.Gorm.LyingHook);
            duel.Tick(PlayerInput.None);
        }

        // It falls where it last flew free (the stopping point may lie inside the wall).
        var lying = Assert.NotNull(duel.Gorm.LyingHook);
        Assert.Equal(0f, FlatDistance(lying, last), 3);
        Assert.Equal(duel.Level.CellAt(last).FloorHeight, lying.Y);

        duel.Run(5 * Second, until: () => duel.Gorm.State == GormState.Walking);
        Assert.Equal(GormState.Walking, duel.Gorm.State);
        Assert.Null(duel.Gorm.LyingHook);
    }

    // ---- Long chase ------------------------------------------------------------------------------------------------

    /// <summary>
    /// A long hall; Gorm 12 m behind the player. He throws at once; the player steps aside after the lock, then keeps
    /// 15 m ahead of him - too far for hammer and swing, and he never throws twice in a row on his own.
    /// </summary>
    private static Duel ChaseAfterAMiss()
    {
        var duel = new Duel(-12f, level: Hall(depth: 60, playerRow: 10));
        duel.UntilLocked();
        duel.UntilHookEnds(RunRight);
        Assert.False(duel.Saw<HookHit>());
        duel.Run(5 * Second, until: () => duel.Gorm.State == GormState.Walking);
        Assert.Equal(GormState.Walking, duel.Gorm.State);
        duel.Events.Clear();
        return duel;
    }

    private static PlayerInput KeepAhead(Duel duel) =>
        FlatDistance(duel.Gorm.Position, duel.Player.Position) < 15f
            ? PlayerInput.None with { Move = new Vector2(0f, 1f) }
            : PlayerInput.None;

    [Fact]
    public void After_a_long_chase_he_warns_and_then_throws_anyway()
    {
        var duel = ChaseAfterAMiss();

        int? warned = null;
        int? threw = null;
        for (var tick = 1; tick <= ChaseThrowTicks + Second && threw is null; tick++)
        {
            duel.Tick(KeepAhead(duel));
            if (warned is null && duel.Gorm.ChaseWarning is not null)
            {
                warned = tick;
            }

            if (duel.Events.Contains(new GormWindUp(9, GormAttack.Throw)))
            {
                threw = tick;
            }
        }

        Assert.Equal(ChaseThrowTicks - ChaseWarnTicks, warned);
        Assert.Equal(ChaseThrowTicks, threw);
        Assert.DoesNotContain(duel.Events, e => e is GormWindUp { Attack: not GormAttack.Throw });
    }

    [Fact]
    public void The_chase_warning_grows_from_nothing_to_full()
    {
        var duel = ChaseAfterAMiss();

        for (var tick = 1; tick < ChaseThrowTicks - ChaseWarnTicks; tick++)
        {
            duel.Tick(KeepAhead(duel));
        }

        Assert.Null(duel.Gorm.ChaseWarning);
        var values = new List<float>();
        for (var tick = 0; tick < ChaseWarnTicks; tick++)
        {
            duel.Tick(KeepAhead(duel));
            values.Add(Assert.NotNull(duel.Gorm.ChaseWarning));
        }

        Assert.Equal(0f, values[0]);
        Assert.Equal((ChaseWarnTicks - 1f) / ChaseWarnTicks, values[^1], 4);
        Assert.Equal(values.Order(), values);
    }

    [Fact]
    public void A_target_out_of_sight_is_warned_of_but_not_thrown_at()
    {
        // A full wall across the hall: he cannot see or reach the player behind it.
        var duel = new Duel(23f, level: Hall(wall: (_, z) => z == 8));

        duel.Run(ChaseThrowTicks + (2 * Second));

        Assert.DoesNotContain(duel.Events, e => e is GormWindUp);
        Assert.Equal(GormState.Walking, duel.Gorm.State);
        Assert.Equal(1f, duel.Gorm.ChaseWarning);
    }

    [Fact]
    public void When_his_target_dies_the_chase_starts_over()
    {
        var duel = ChaseAfterAMiss();
        for (var tick = 0; tick < ChaseThrowTicks && duel.Gorm.ChaseWarning is null; tick++)
        {
            duel.Tick(KeepAhead(duel));
        }

        Assert.NotNull(duel.Gorm.ChaseWarning);

        duel.Player.TakeDamage(Tough.Player.MaxHealth, []);
        Assert.True(duel.Player.IsDead);
        duel.Run(ChaseThrowTicks + Second);

        Assert.Null(duel.Gorm.ChaseWarning);
        Assert.DoesNotContain(duel.Events, e => e is GormWindUp);
    }

    /// <summary>One tick of the approach in <see cref="ApproachFromBeyondFarRange"/>: the distance Gorm decides on, and what came of it.</summary>
    private readonly record struct ApproachTick(float Distance, float? Warning, bool Threw);

    /// <summary>
    /// After a missed throw the player runs ahead and keeps more than <c>FarRange</c> + 3 m away until the chase count
    /// has run out (warning full), then stands still. Gorm walks up to them; returns every tick from then on until he
    /// throws (at most ten seconds).
    /// </summary>
    private static List<ApproachTick> ApproachFromBeyondFarRange()
    {
        var duel = new Duel(-12f, level: Hall(depth: 60, playerRow: 10));
        duel.UntilLocked();
        duel.UntilHookEnds(RunRight);
        Assert.False(duel.Saw<HookHit>());
        var away = PlayerInput.None with { Move = new Vector2(0f, 1f) };
        for (var tick = 0; tick < 3 * ChaseThrowTicks && duel.Gorm.ChaseWarning != 1f; tick++)
        {
            duel.Tick(FlatDistance(duel.Gorm.Position, duel.Player.Position) < Values.FarRange + 3f ? away : PlayerInput.None);
        }

        Assert.Equal(1f, duel.Gorm.ChaseWarning);
        Assert.Equal(GormState.Walking, duel.Gorm.State);
        duel.Events.Clear();

        var ticks = new List<ApproachTick>();
        for (var tick = 0; tick < 10 * Second && !duel.Saw<GormWindUp>(); tick++)
        {
            // Gorm decides on the distance at the start of his part of the tick; the standing player does not move.
            var distance = FlatDistance(duel.Gorm.Position, duel.Player.Position);
            duel.Tick(PlayerInput.None);
            ticks.Add(new ApproachTick(distance, duel.Gorm.ChaseWarning, duel.Saw<GormWindUp>()));
        }

        return ticks;
    }

    [Fact]
    public void A_target_in_sight_but_beyond_far_range_is_not_thrown_at_after_the_chase()
    {
        var ticks = ApproachFromBeyondFarRange();

        var beyond = ticks.TakeWhile(tick => tick.Distance > Values.FarRange).ToList();
        Assert.True(beyond.Count > Second, "He walked up for over a second with the chase run out.");
        Assert.All(beyond, tick => Assert.False(tick.Threw, $"He threw at {tick.Distance} m."));
        Assert.All(beyond, tick => Assert.Equal(1f, tick.Warning));

        // The last tick beyond is just beyond: one step of his walk.
        var step = Values.Speed / SimulationClock.TicksPerSecond;
        Assert.InRange(beyond[^1].Distance - Values.FarRange, 0f, step + 0.001f);
    }

    [Fact]
    public void A_target_just_within_far_range_is_thrown_at_after_the_chase()
    {
        var ticks = ApproachFromBeyondFarRange();

        var throwing = Assert.Single(ticks, tick => tick.Threw);
        Assert.Equal(ticks[^1], throwing);
        Assert.InRange(Values.FarRange - throwing.Distance, 0f, (Values.Speed / SimulationClock.TicksPerSecond) + 0.001f);
    }

    [Fact]
    public void With_two_players_the_chase_counts_on_when_his_target_dies_and_starts_over_when_both_are_dead()
    {
        // Behind a full wall across the hall, so the count runs without a throw. Both players stand at the start.
        var level = Hall(wall: (_, z) => z == 8);
        var first = new Player(level, balance: Tough);
        var second = new Player(level, balance: Tough);
        var gorm = new Gorm(9, first.Position + new Vector3(0f, 0f, 23f), -Vector3.UnitZ, Tough, new Random(1));
        var events = new List<GameEvent>();
        void Tick()
        {
            first.Tick(PlayerInput.None);
            second.Tick(PlayerInput.None);
            gorm.Act(level, [first, second], [first.Body, second.Body], events);
        }

        var ticks = 0;
        while (gorm.ChaseWarning is null && ticks < ChaseThrowTicks)
        {
            Tick();
            ticks++;
        }

        Assert.Equal(ChaseThrowTicks - ChaseWarnTicks, ticks);

        // His target - one of the two - dies; he turns to the other, and the count goes on instead of starting over.
        var target = FlatDistance(gorm.Position, first.Position) <= FlatDistance(gorm.Position, second.Position) ? first : second;
        target.TakeDamage(Tough.Player.MaxHealth, []);
        Assert.True(target.IsDead);
        Tick();
        Assert.Equal(1f / ChaseWarnTicks, gorm.ChaseWarning);

        // Only when no player is left alive does it start over.
        var other = target == first ? second : first;
        other.TakeDamage(Tough.Player.MaxHealth, []);
        Tick();
        Assert.Null(gorm.ChaseWarning);
        Assert.DoesNotContain(events, e => e is GormWindUp);
    }
}
