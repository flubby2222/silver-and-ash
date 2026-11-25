using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Issue #504: status effects on enemies and the first of them, Marked. No weapon or skill sets one yet: the tests put
/// it on through the world's test path (<see cref="GameWorld.ApplyStatus"/>) and then run the world tick by tick.
/// </summary>
public class StatusEffectTests
{
    private static readonly PlayerInput Shoot = PlayerInput.None with { Shoot = true };
    private static readonly MarkedBalance Marked = Balance.Default.StatusEffects.Marked;
    private static readonly int MarkTicks = SimulationClock.SecondsToTicks(Marked.Seconds);

    // From 16 m away (as in the ghoul's hit-zone tests): the ray runs almost flat over the ghoul.
    private const string LongCorridor = "P\n.\n.\n.\n.\n.\n.\n.\nG";

    private static int PlainDamage => Balance.Default.Pistol.Damage;

    private static int MarkedDamage =>
        (int)MathF.Round(Balance.Default.Pistol.Damage * (1f + Marked.Bonus), MidpointRounding.AwayFromZero);

    [Fact]
    public void A_marked_ghoul_takes_the_bonus_from_a_body_shot_an_unmarked_one_does_not()
    {
        Assert.NotEqual(PlainDamage, MarkedDamage);   // otherwise the test could not tell the two apart

        var plain = new GameWorld(LevelParser.Parse(LongCorridor));
        ShootAtTheBody(plain);

        var marked = new GameWorld(LevelParser.Parse(LongCorridor));
        Assert.True(marked.ApplyStatus(marked.Ghouls[0], StatusKind.Marked, marked.Player));
        ShootAtTheBody(marked);

        Assert.Contains(new GhoulHit(plain.Ghouls[0].Id, PlainDamage), plain.Events);
        Assert.Contains(new GhoulHit(marked.Ghouls[0].Id, MarkedDamage), marked.Events);   // not critical either
        Assert.Equal(Balance.Default.Ghoul.MaxHealth - MarkedDamage, marked.Ghouls[0].Health);
    }

    [Fact]
    public void A_shot_in_the_last_tick_of_the_mark_gets_the_bonus_one_tick_later_it_does_not()
    {
        var last = new GameWorld(LevelParser.Parse(LongCorridor));
        last.ApplyStatus(last.Ghouls[0], StatusKind.Marked, last.Player);
        RunTicks(last, PlayerInput.None, MarkTicks - 1);
        ShootAtTheBody(last);                                       // tick number MarkTicks after the mark

        var after = new GameWorld(LevelParser.Parse(LongCorridor));
        after.ApplyStatus(after.Ghouls[0], StatusKind.Marked, after.Player);
        RunTicks(after, PlayerInput.None, MarkTicks);
        ShootAtTheBody(after);                                      // one tick later

        Assert.Contains(last.Events, e => e is GhoulHit { Damage: var damage } && damage == MarkedDamage);
        Assert.Contains(after.Events, e => e is GhoulHit { Damage: var damage } && damage == PlainDamage);
    }

    [Theory]
    [InlineData("ghoul")]
    [InlineData("bat")]
    [InlineData("gorm")]
    public void The_mark_ends_exactly_after_its_duration(string kind)
    {
        var (world, enemy) = WorldWith(kind);

        world.ApplyStatus(enemy, StatusKind.Marked, world.Player);
        Assert.Contains(new StatusApplied(enemy.Id, StatusKind.Marked, world.Player.Id), world.Events);
        Assert.Equal(new StatusEffect(MarkTicks, world.Player.Id, false), enemy.Status.Get(StatusKind.Marked));

        var events = Run(world, PlayerInput.None, MarkTicks - 1);
        Assert.True(enemy.Status.Has(StatusKind.Marked), "One tick before its end the mark must still last.");
        Assert.DoesNotContain(events, e => e is StatusEnded);

        world.Tick(PlayerInput.None);
        Assert.False(enemy.Status.Has(StatusKind.Marked));
        Assert.Equal([new StatusEnded(enemy.Id, StatusKind.Marked)], world.Events.OfType<StatusEnded>());

        // It is reported once, not again on the following ticks.
        Assert.DoesNotContain(Run(world, PlayerInput.None, 10), e => e is StatusEnded);
    }

    [Fact]
    public void A_refresh_halfway_lasts_the_full_duration_again()
    {
        var world = new GameWorld(LevelParser.Parse(LongCorridor));
        var ghoul = world.Ghouls[0];
        world.ApplyStatus(ghoul, StatusKind.Marked, world.Player);
        var half = MarkTicks / 2;
        RunTicks(world, PlayerInput.None, half);

        world.ApplyStatus(ghoul, StatusKind.Marked, world.Player);

        // A refresh is reported like a fresh mark.
        Assert.Contains(new StatusApplied(ghoul.Id, StatusKind.Marked, world.Player.Id), world.Events);
        Assert.Equal(MarkTicks, ghoul.Status.Get(StatusKind.Marked)?.TicksLeft);

        // It now ends a full duration after the refresh, not at the end of the first mark.
        var endedIn = 0;
        for (var tick = 1; tick <= 2 * MarkTicks && endedIn == 0; tick++)
        {
            world.Tick(PlayerInput.None);
            endedIn = world.Events.Contains(new StatusEnded(ghoul.Id, StatusKind.Marked)) ? tick : 0;
        }

        Assert.Equal(MarkTicks, endedIn);
    }

    [Fact]
    public void A_longer_refresh_extends_the_mark()
    {
        // The skills will bring longer marks (a synergy); counted from the standard duration.
        var status = new StatusEffects();
        status.Apply(StatusKind.Marked, MarkTicks, sourcePlayerId: 0);
        CountDown(status, MarkTicks / 2);

        status.Apply(StatusKind.Marked, 2 * MarkTicks, sourcePlayerId: 1, synergy: true);

        Assert.Equal(new StatusEffect(2 * MarkTicks, 1, true), status.Get(StatusKind.Marked));
        Assert.Equal(2 * MarkTicks, TicksUntilEnd(status));
    }

    [Fact]
    public void A_shorter_refresh_does_not_shorten_the_mark()
    {
        var status = new StatusEffects();
        status.Apply(StatusKind.Marked, 2 * MarkTicks, sourcePlayerId: 0, synergy: true);
        CountDown(status, MarkTicks / 2);
        var left = 2 * MarkTicks - MarkTicks / 2;

        status.Apply(StatusKind.Marked, MarkTicks, sourcePlayerId: 1);

        // What is left stays, and with it the source and the synergy of the longer mark.
        Assert.Equal(new StatusEffect(left, 0, true), status.Get(StatusKind.Marked));
        Assert.Equal(left, TicksUntilEnd(status));
    }

    [Fact]
    public void A_dead_enemy_takes_no_mark()
    {
        var world = new GameWorld(LevelParser.Parse(LongCorridor));
        var ghoul = world.Ghouls[0];
        ghoul.TakeDamage(Balance.Default.Ghoul.MaxHealth, []);

        Assert.False(world.ApplyStatus(ghoul, StatusKind.Marked, world.Player));

        Assert.False(ghoul.Status.Has(StatusKind.Marked));
        Assert.DoesNotContain(world.Events, e => e is StatusApplied);
    }

    [Fact]
    public void The_mark_works_in_full_on_a_boss_head_and_stacks_with_it_without_being_critical_itself()
    {
        var rules = DamageRules.Standard(Balance.Default);
        var status = new StatusEffects();
        status.Apply(StatusKind.Marked, MarkTicks, sourcePlayerId: 0);
        var damage = Balance.Default.Pistol.Damage;

        var head = rules.Calculate(new Hit(Weapon.Pistol, HitZone.BossHead, damage, TargetStatus: status));
        var body = rules.Calculate(new Hit(Weapon.Pistol, HitZone.Body, damage, TargetStatus: status));
        var unmarked = rules.Calculate(new Hit(Weapon.Pistol, HitZone.BossHead, damage, TargetStatus: new StatusEffects()));

        var expected = (int)MathF.Round(damage * (Balance.Default.Gorm.HeadMultiplier * (1f + Marked.Bonus)), MidpointRounding.AwayFromZero);
        Assert.Equal(new DamageResult(expected, IsCritical: true), head);
        Assert.Equal(new DamageResult(MarkedDamage, IsCritical: false), body);
        Assert.True(unmarked.Amount < head.Amount);
    }

    [Fact]
    public void An_unmarked_world_reports_no_status_events()
    {
        var events = Run(new GameWorld(LevelParser.Parse(LongCorridor)), PlayerInput.None, MarkTicks + 1);

        Assert.DoesNotContain(events, e => e is StatusApplied or StatusEnded);
    }

    private static (GameWorld World, IEnemy Enemy) WorldWith(string kind)
    {
        switch (kind)
        {
            case "ghoul":
            {
                var world = new GameWorld(LevelParser.Parse(LongCorridor));
                return (world, world.Ghouls[0]);
            }

            case "bat":
            {
                var world = new GameWorld(LevelParser.Parse("P\n.\n.\nB"));
                return (world, world.Bats[0]);
            }

            default:
            {
                var world = GormHallWorld();
                return (world, world.Gorm!);
            }
        }
    }

    private static void CountDown(StatusEffects status, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            status.Tick(0, []);
        }
    }

    /// <summary>Counts the effects down tick by tick and returns in which tick the mark ended.</summary>
    private static int TicksUntilEnd(StatusEffects status)
    {
        for (var tick = 1; tick <= 100 * MarkTicks; tick++)
        {
            var events = new List<GameEvent>();
            status.Tick(0, events);
            if (events.Contains(new StatusEnded(0, StatusKind.Marked)))
            {
                return tick;
            }
        }

        return -1;
    }

    /// <summary>
    /// Aims at the ghoul's shoulder, beside the head (as in its hit-zone tests: straight at the torso the ray meets the
    /// head first), and fires the pistol - a body shot, not critical.
    /// </summary>
    private static void ShootAtTheBody(GameWorld world)
    {
        var ghoul = world.Ghouls[0];
        var point = ghoul.Position + ghoul.Facing * BodySizes.Ghoul.TorsoForward
            + new Vector3(0f, BodySizes.Ghoul.TorsoHeight - 0.1f, 0f)
            + new Vector3(ghoul.Facing.Z, 0f, -ghoul.Facing.X) * (BodySizes.Ghoul.TorsoRadius - 0.1f);
        var direction = Vector3.Normalize(point - world.Player.EyePosition);
        world.Player.Look(PlayerInput.None with
        {
            Look = new Vector2(
                -(MathF.Atan2(direction.X, direction.Z) - world.Player.View.YawRadians) / ControlSettings.MouseSensitivity,
                -(MathF.Asin(direction.Y) - world.Player.View.PitchRadians) / ControlSettings.MouseSensitivity),
        });
        world.Tick(Shoot);
    }
}
