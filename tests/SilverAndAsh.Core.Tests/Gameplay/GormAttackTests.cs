using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>Gorm's three attacks (decision record 0007, issue #165): each has its one answer.</summary>
public class GormAttackTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly GormBalance Values = Balance.Default.Gorm;

    /// <summary>A player who survives a lot - to watch many attacks in a row.</summary>
    private static readonly Balance Tough = Balance.Default with { Player = Balance.Default.Player with { MaxHealth = 100_000 } };

    /// <summary>An open hall, 15 cells wide and 25 deep (50 m - farther than his throw); the player in column 7, row 3, looking along +Z.</summary>
    private static Level Hall(params (int X, int Z)[] pillars)
    {
        var rows = Enumerable.Range(0, 25).Select(z => Enumerable.Range(0, 15).Select(x =>
            (x, z) == (7, 3) ? 'P' : pillars.Contains((x, z)) ? '#' : '.').ToArray()).Select(row => new string(row));
        return LevelParser.Parse(string.Join('\n', rows));
    }

    /// <summary>Gorm and one player facing each other in a hall, ticked together.</summary>
    private sealed class Duel
    {
        public Duel(float distance, Balance? balance = null, Level? level = null)
        {
            Level = level ?? Hall();
            Balance = balance ?? Tough;
            Player = new Player(Level, balance: Balance);
            Gorm = new Gorm(9, Player.Position + new Vector3(0f, 0f, distance), -Vector3.UnitZ, Balance, new Random(1));
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

        /// <summary>Runs until Gorm starts winding up <paramref name="attack"/> (at most a minute).</summary>
        public void UntilWindUp(GormAttack attack)
        {
            Run(60 * Second, until: () => Gorm.State == GormState.WindingUp && Gorm.Attack == attack);
            Assert.True(Gorm.State == GormState.WindingUp && Gorm.Attack == attack, $"Gorm never wound up his {attack}.");
        }
    }

    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    // ---- Choosing ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Close_up_he_uses_hammer_and_chain_swing_never_the_same_twice_in_a_row()
    {
        var duel = new Duel(3f);

        duel.Run(60 * Second);

        var attacks = duel.Events.OfType<GormWindUp>().Select(windUp => windUp.Attack).ToList();
        Assert.True(attacks.Count >= 10, $"Only {attacks.Count} attacks in a minute.");
        Assert.All(attacks, attack => Assert.NotEqual(GormAttack.Throw, attack));
        Assert.Contains(GormAttack.Hammer, attacks);
        Assert.Contains(GormAttack.Swing, attacks);
        Assert.All(attacks.Zip(attacks.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second));
    }

    [Fact]
    public void His_first_attack_close_up_is_left_to_chance()
    {
        var first = Enumerable.Range(0, 20).Select(seed =>
        {
            var level = Hall();
            var player = new Player(level, balance: Tough);
            var gorm = new Gorm(9, player.Position + new Vector3(0f, 0f, 3f), -Vector3.UnitZ, Tough, new Random(seed));
            gorm.Act(level, [player], [player.Body], []);
            return gorm.Attack;
        }).ToHashSet();

        Assert.Equal([GormAttack.Hammer, GormAttack.Swing], first.Order());
    }

    [Fact]
    public void Farther_away_he_throws_his_chain()
    {
        var duel = new Duel(9f);

        duel.Tick(PlayerInput.None);

        Assert.Equal(GormState.WindingUp, duel.Gorm.State);
        Assert.Equal(GormAttack.Throw, duel.Gorm.Attack);
        Assert.Contains(new GormWindUp(9, GormAttack.Throw), duel.Events);
    }

    [Fact]
    public void He_never_throws_twice_in_a_row_but_walks_closer_after_a_throw()
    {
        // The player steps aside so the hook misses, then stays far away: the next attack is no throw.
        var duel = new Duel(12f);
        duel.Run(Second, until: () => duel.Events.OfType<GormThrew>().Any());
        duel.Run(Second, PlayerInput.None with { Move = new Vector2(1f, 0f) }, () => duel.Events.OfType<HookStopped>().Any());
        duel.Run(SimulationClock.SecondsToTicks(Values.PauseTime) + 5);

        Assert.Single(duel.Events.OfType<GormThrew>());
        Assert.Equal(GormState.Walking, duel.Gorm.State);
    }

    [Fact]
    public void Beyond_his_far_range_he_walks_closer_instead()
    {
        var duel = new Duel(Values.FarRange + 4f);
        var start = duel.Gorm.Position;

        duel.Run(Second / 2);

        Assert.Equal(GormState.Walking, duel.Gorm.State);
        Assert.True(FlatDistance(start, duel.Gorm.Position) > 1f, "He did not walk.");
    }

    [Fact]
    public void After_every_attack_he_rests_before_the_next()
    {
        var duel = new Duel(3f);
        duel.UntilWindUp(GormAttack.Swing);
        duel.Run(2 * Second, until: () => duel.Events.OfType<GormSwung>().Any());
        var restStart = duel.Events.Count;

        duel.Run(SimulationClock.SecondsToTicks(Values.PauseTime) - 1);

        Assert.Equal(GormState.Recovering, duel.Gorm.State);
        Assert.DoesNotContain(duel.Events.Skip(restStart), e => e is GormWindUp);
        duel.Run(2);
        Assert.Contains(duel.Events.Skip(restStart), e => e is GormWindUp);
    }

    [Fact]
    public void Every_attack_is_announced_before_it_hits()
    {
        var duel = new Duel(3f);

        duel.Run(20 * Second);

        var events = duel.Events;
        foreach (var (hit, index) in events.Select((e, i) => (e, i)).Where(pair => pair.e is PlayerHit))
        {
            var windUp = events.Take(index).OfType<GormWindUp>().LastOrDefault();
            Assert.NotNull(windUp);
        }
    }

    // ---- Hammer -----------------------------------------------------------------------------------------------------

    [Fact]
    public void The_hammer_s_shock_wave_hits_a_player_on_the_ground()
    {
        var duel = new Duel(3f);
        duel.UntilWindUp(GormAttack.Hammer);
        var health = duel.Player.Health;

        duel.Run(3 * Second, until: () => duel.Gorm.State != GormState.WindingUp && duel.Gorm.State != GormState.Slamming);

        Assert.Equal(health - Values.HammerDamage, duel.Player.Health);
        Assert.Contains(new ShockwaveHit(duel.Player.Id), duel.Events);
        Assert.Equal(GormState.Recovering, duel.Gorm.State);
    }

    [Fact]
    public void Jumping_over_the_shock_wave_leaves_the_player_unhurt_and_gets_the_club_stuck()
    {
        var duel = new Duel(3f);
        duel.UntilWindUp(GormAttack.Hammer);
        var health = duel.Player.Health;

        // Jump the moment the club comes down.
        duel.Run(Second, until: () => duel.Gorm.State == GormState.Slamming);
        duel.Tick(PlayerInput.None with { Jump = true });
        duel.Run(2 * Second, until: () => duel.Gorm.State != GormState.Slamming);

        Assert.Equal(health, duel.Player.Health);
        Assert.DoesNotContain(duel.Events, e => e is ShockwaveHit);
        Assert.Equal(GormState.Stuck, duel.Gorm.State);
        Assert.Contains(new ClubStuck(9), duel.Events);
    }

    [Fact]
    public void Out_of_the_shock_wave_s_reach_the_club_gets_stuck_too()
    {
        // Close enough for the hammer (4 m), but the wave is shorter here.
        var shortWave = Tough with { Gorm = Values with { ShockwaveRadius = 2f } };
        var duel = new Duel(3.5f, shortWave);
        duel.UntilWindUp(GormAttack.Hammer);

        duel.Run(3 * Second, until: () => duel.Gorm.State is GormState.Stuck or GormState.Recovering);

        Assert.Equal(GormState.Stuck, duel.Gorm.State);
    }

    [Fact]
    public void While_the_club_is_stuck_he_takes_double_damage_and_his_head_comes_down()
    {
        var duel = new Duel(3f);
        duel.UntilWindUp(GormAttack.Hammer);
        duel.Run(Second, until: () => duel.Gorm.State == GormState.Slamming);
        duel.Tick(PlayerInput.None with { Jump = true });
        duel.Run(2 * Second, until: () => duel.Gorm.State == GormState.Stuck);
        var gorm = duel.Gorm;
        var events = new List<GameEvent>();

        gorm.TakeDamage(40, events);

        Assert.Equal(new GormHit(9, 80), events[0]);
        var front = gorm.Position + (gorm.Facing * BodySizes.Gorm.Radius);
        var normalHead = front with { Y = gorm.Position.Y + BodySizes.Gorm.Height - 0.1f };
        var bentHead = front with { Y = gorm.Position.Y + BodySizes.Gorm.Height - Gorm.StuckBend - 0.1f };
        Assert.Equal(HitZone.BossHead, gorm.ZoneAt(bentHead));
        Assert.Equal(HitZone.Body, gorm.ZoneAt(normalHead));
    }

    [Fact]
    public void The_stuck_club_comes_free_after_its_time()
    {
        var duel = new Duel(3f);
        duel.UntilWindUp(GormAttack.Hammer);
        duel.Run(Second, until: () => duel.Gorm.State == GormState.Slamming);
        duel.Tick(PlayerInput.None with { Jump = true });
        duel.Run(2 * Second, until: () => duel.Gorm.State == GormState.Stuck);

        duel.Run(SimulationClock.SecondsToTicks(Values.StuckTime));

        Assert.Equal(GormState.Recovering, duel.Gorm.State);
        Assert.Contains(new ClubFreed(9), duel.Events);
        var events = new List<GameEvent>();
        duel.Gorm.TakeDamage(40, events);
        Assert.Equal(new GormHit(9, 40), events[0]);
    }

    // ---- Chain swing ------------------------------------------------------------------------------------------------

    [Fact]
    public void The_chain_swing_hits_a_standing_player()
    {
        var duel = new Duel(3f);
        duel.UntilWindUp(GormAttack.Swing);
        var health = duel.Player.Health;

        duel.Run(Second, until: () => duel.Events.OfType<GormSwung>().Any());

        Assert.Equal(health - Values.SwingDamage, duel.Player.Health);
        Assert.Contains(new GormSwung(9, true), duel.Events);
    }

    [Fact]
    public void Jumping_does_not_save_from_the_chain_swing()
    {
        var duel = new Duel(3f);
        duel.UntilWindUp(GormAttack.Swing);
        var health = duel.Player.Health;

        // Jump so the player is at the top of the jump when the chain comes round.
        duel.Run(Second, until: () => duel.Gorm.StateProgress >= 0.4f);
        duel.Tick(PlayerInput.None with { Jump = true });
        duel.Run(Second, until: () => duel.Events.OfType<GormSwung>().Any());

        Assert.False(duel.Player.IsGrounded);
        Assert.Equal(health - Values.SwingDamage, duel.Player.Health);
    }

    [Fact]
    public void Ducking_in_a_dodge_saves_from_the_chain_swing()
    {
        var duel = new Duel(3f);
        duel.UntilWindUp(GormAttack.Swing);
        var health = duel.Player.Health;

        // Dodge (sideways, staying in reach) just before the chain comes round.
        duel.Run(Second, until: () => duel.Gorm.StateProgress >= 0.75f);
        duel.Tick(PlayerInput.None with { Dash = true, Move = new Vector2(1f, 0f) });
        duel.Run(Second, until: () => duel.Events.OfType<GormSwung>().Any());

        Assert.True(FlatDistance(duel.Gorm.Position, duel.Player.Position) <= Values.SwingReach, "The dodge left the chain's reach - the test would prove nothing.");
        Assert.Equal(health, duel.Player.Health);
        Assert.Contains(new GormSwung(9, false), duel.Events);
    }

    [Fact]
    public void Out_of_the_chain_s_reach_the_swing_misses()
    {
        var shortChain = Tough with { Gorm = Values with { SwingReach = 2f } };
        var duel = new Duel(3f, shortChain);
        duel.UntilWindUp(GormAttack.Swing);
        var health = duel.Player.Health;

        duel.Run(Second, until: () => duel.Events.OfType<GormSwung>().Any());

        Assert.Equal(health, duel.Player.Health);
    }

    // ---- Chain throw ------------------------------------------------------------------------------------------------

    [Fact]
    public void The_hook_hits_a_player_who_stands_still_and_pulls_them_close()
    {
        var duel = new Duel(9f);
        var health = duel.Player.Health;

        duel.Run(3 * Second, until: () => duel.Events.OfType<HookHit>().Any());
        Assert.True(duel.Player.IsPulled);
        duel.Run(SimulationClock.SecondsToTicks(Values.PullTime) + 1);

        Assert.Equal(health - Values.ThrowDamage, duel.Player.Health);
        Assert.False(duel.Player.IsPulled);
        Assert.Equal(Values.PullDistance, FlatDistance(duel.Gorm.Position, duel.Player.Position), 0.3f);
    }

    [Fact]
    public void While_pulled_the_player_cannot_walk_away()
    {
        var duel = new Duel(9f);
        duel.Run(3 * Second, until: () => duel.Events.OfType<HookHit>().Any());
        var before = FlatDistance(duel.Gorm.Position, duel.Player.Position);

        // Walking backwards (away) the whole time: the pull still wins.
        duel.Run(SimulationClock.SecondsToTicks(Values.PullTime), PlayerInput.None with { Move = new Vector2(0f, -1f) });

        Assert.True(FlatDistance(duel.Gorm.Position, duel.Player.Position) < before - 3f);
    }

    [Fact]
    public void While_pulled_the_player_cannot_jump_either()
    {
        var duel = new Duel(9f);
        duel.Run(3 * Second, until: () => duel.Events.OfType<HookHit>().Any());

        duel.Run(SimulationClock.SecondsToTicks(Values.PullTime) - 1, PlayerInput.None with { Jump = true });

        Assert.True(duel.Player.IsGrounded, "The player jumped while being pulled.");
    }

    [Fact]
    public void Stepping_aside_lets_the_hook_fly_past()
    {
        var duel = new Duel(9f);
        var health = duel.Player.Health;

        // The hook flies to where the player stood at the throw: run sideways the moment it leaves the hand.
        duel.Run(Second, until: () => duel.Events.OfType<GormThrew>().Any());
        duel.Run(Second, PlayerInput.None with { Move = new Vector2(1f, 0f) }, until: () => duel.Events.OfType<HookStopped>().Any());

        Assert.Equal(health, duel.Player.Health);
        Assert.DoesNotContain(duel.Events, e => e is HookHit);
        Assert.Contains(duel.Events, e => e is HookStopped);
        Assert.False(duel.Player.IsPulled);
    }

    [Fact]
    public void A_pillar_stops_the_hook()
    {
        // Gorm stands 9 m ahead with a clear line, so he winds up a throw. Meanwhile the player runs to the right -
        // when the hook leaves his hand, the pillar in cell (6, 5) lies on the new line ("right" is -X when looking along +Z).
        var duel = new Duel(9f, level: Hall((6, 5)));
        duel.Tick(PlayerInput.None);
        Assert.Equal(GormAttack.Throw, duel.Gorm.Attack);
        var health = duel.Player.Health;

        duel.Run(3 * Second, PlayerInput.None with { Move = new Vector2(1f, 0f) },
            () => duel.Events.OfType<HookStopped>().Any() || duel.Events.OfType<HookHit>().Any());

        Assert.Equal(health, duel.Player.Health);
        var stopped = Assert.Single(duel.Events.OfType<HookStopped>());
        Assert.True(FlatDistance(stopped.At, duel.Level.CellCenter(6, 5)) < 2f, $"The hook stopped at {stopped.At}, not at the pillar.");
    }

    [Fact]
    public void Without_a_clear_line_he_does_not_throw()
    {
        var duel = new Duel(9f, level: Hall((7, 5)));

        duel.Tick(PlayerInput.None);

        Assert.NotEqual(GormAttack.Throw, duel.Gorm.Attack);
    }

    [Fact]
    public void A_dead_gorm_does_nothing_and_his_shock_wave_and_hook_are_gone()
    {
        var duel = new Duel(9f);
        duel.Run(Second, until: () => duel.Gorm.Hook is not null);
        Assert.NotNull(duel.Gorm.Hook);

        duel.Gorm.TakeDamage(100_000, []);
        var health = duel.Player.Health;
        duel.Run(3 * Second);

        Assert.Null(duel.Gorm.Hook);
        Assert.Null(duel.Gorm.Shockwave);
        Assert.Equal(health, duel.Player.Health);
    }
}
