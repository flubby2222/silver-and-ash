using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Issue #771: the pulses of a Cleansing Flame do the damage of a hit but are no weapon hit - their events say
/// <see cref="DamageKind.Burn"/>, so nobody plays a hurt sound or a flinch for each pulse. The tests burn an enemy for the
/// whole flame, tick by tick, and read the events the world really reports; Game and the sound table look only at the
/// kind, so what they react to is tested here at its source.
/// </summary>
public class FlameReactionTests
{
    private static readonly CleansingFlameBalance Flame = Balance.Default.StatusEffects.CleansingFlame;
    private static readonly int FlameTicks = SimulationClock.SecondsToTicks(Flame.Seconds);

    private const string GhoulCorridor = "P\n.\n.\n.\nG";

    private static readonly string BatRoom = string.Join('\n', "........", "........", "P......B", "........", "........");

    /// <summary>The standard pulses, by tick of the flame (1 = the first), as the world should report them.</summary>
    private static List<(int Tick, int Damage)> StandardPulses() =>
        Enumerable.Range(1, FlameTicks).Select(tick => (Tick: tick, Damage: CleansingFlame.DamageOnTick(tick, Flame))).Where(pulse => pulse.Damage > 0).ToList();

    /// <summary>Sets <paramref name="enemy"/> alight and ticks twice as long as the flame; returns what the world reported on each tick.</summary>
    private static List<List<GameEvent>> BurnForWholeFlame(GameWorld world, IEnemy enemy)
    {
        Assert.True(world.ApplyStatus(enemy, StatusKind.CleansingFlame, world.Player));
        var ticks = new List<List<GameEvent>>();
        for (var tick = 0; tick < 2 * FlameTicks; tick++)
        {
            world.Tick(PlayerInput.None);
            ticks.Add([.. world.Events]);
        }

        return ticks;
    }

    private static void AssertEveryPulseBurns<THit>(List<List<GameEvent>> ticks, Func<THit, (int Damage, DamageKind Kind)> read)
        where THit : GameEvent
    {
        var pulses = StandardPulses();
        var seen = ticks.Select((events, index) => (Tick: index + 1, Hits: events.OfType<THit>().ToList())).Where(tick => tick.Hits.Count > 0).ToList();

        // One hit event on each pulse tick and none in between or after the flame - and every one of them a burn.
        Assert.Equal(pulses.Select(pulse => pulse.Tick), seen.Select(tick => tick.Tick));
        Assert.All(seen, tick => Assert.Single(tick.Hits));
        Assert.Equal(pulses.Select(pulse => pulse.Damage), seen.Select(tick => read(tick.Hits[0]).Damage));
        Assert.All(seen, tick => Assert.Equal(DamageKind.Burn, read(tick.Hits[0]).Kind));
    }

    [Fact]
    public void A_burning_ghoul_reports_every_pulse_as_a_burn()
    {
        var world = new GameWorld(LevelParser.Parse(GhoulCorridor), balance: Balance.Default with
        {
            Ghoul = Balance.Default.Ghoul with { Speed = 0f, MaxHealth = 100_000 },
        });
        var ticks = BurnForWholeFlame(world, world.Ghouls[0]);

        AssertEveryPulseBurns<GhoulHit>(ticks, hit => (hit.Damage, hit.Kind));
    }

    [Fact]
    public void A_burning_ghoul_makes_no_sound_for_any_pulse()
    {
        // Issue #798 (248): the ghoul's hit sound comes from the weapon table (TargetHitBy by weapon), and the flame has no
        // entry in it - the pulses of the whole flame, as the world reports them, ask the sound table for nothing.
        var world = new GameWorld(LevelParser.Parse(GhoulCorridor), balance: Balance.Default with
        {
            Ghoul = Balance.Default.Ghoul with { Speed = 0f, MaxHealth = 100_000 },
        });
        var ticks = BurnForWholeFlame(world, world.Ghouls[0]);

        var events = ticks.SelectMany(tick => tick).ToList();
        Assert.Equal(StandardPulses().Count, events.OfType<GhoulHit>().Count());
        Assert.Equal(StandardPulses().Count, events.OfType<TargetHitBy>().Count(hit => hit.Weapon == Weapon.CleansingFlame));
        Assert.All(events.OfType<GhoulHit>(), hit => Assert.Null(SoundBoard.KindFor(hit)));
        Assert.All(events.OfType<TargetHitBy>(), hit => Assert.Null(SoundBoard.KindFor(hit)));
        Assert.Null(SoundBoard.KindFor(new TargetHitBy(1, 5, Weapon.CleansingFlame)));
        Assert.Null(SoundBoard.KindFor(new TargetHitBy(1, 5, Weapon.CleansingFlame) { Hard = true }));
    }

    [Fact]
    public void A_burning_bat_reports_every_pulse_as_a_burn_and_the_sound_table_stays_silent_for_it()
    {
        var world = new GameWorld(LevelParser.Parse(BatRoom), balance: Balance.Default with
        {
            Bat = Balance.Default.Bat with { MaxHealth = 100_000 },
        });
        var ticks = BurnForWholeFlame(world, world.Bats[0]);

        AssertEveryPulseBurns<BatHit>(ticks, hit => (hit.Damage, hit.Kind));
        Assert.All(ticks.SelectMany(events => events).OfType<BatHit>(), hit => Assert.Null(SoundBoard.KindFor(hit)));
    }

    [Fact]
    public void A_burning_Gorm_reports_every_pulse_as_a_burn_and_the_sound_table_stays_silent_for_it()
    {
        var world = GormHallWorld(balance: Balance.Default with { Gorm = Balance.Default.Gorm with { MaxHealth = 100_000 } });
        EnterGormHall(world);
        var ticks = BurnForWholeFlame(world, world.Gorm!);

        AssertEveryPulseBurns<GormHit>(ticks, hit => (hit.Damage, hit.Kind));
        Assert.All(ticks.SelectMany(events => events).OfType<GormHit>(), hit => Assert.Null(SoundBoard.KindFor(hit)));
    }

    [Fact]
    public void A_burn_still_kills_for_the_player_who_set_the_flame()
    {
        var world = new GameWorld(LevelParser.Parse(GhoulCorridor), balance: Balance.Default with
        {
            Ghoul = Balance.Default.Ghoul with { Speed = 0f, MaxHealth = 1 },
        });
        var experienceBefore = world.Player.Experience;
        var ticks = BurnForWholeFlame(world, world.Ghouls[0]);

        Assert.True(world.Ghouls[0].IsDead);
        Assert.Contains(ticks, events => events.OfType<GhoulDied>().Any());
        Assert.True(world.Player.Experience > experienceBefore || world.Player.Level > 1);
    }

    [Fact]
    public void Weapon_hits_stay_strikes_and_keep_their_sounds()
    {
        var events = new List<GameEvent>();
        new GameWorld(LevelParser.Parse(BatRoom)).Bats[0].TakeDamage(1, events);
        Assert.Equal(DamageKind.Strike, Assert.Single(events.OfType<BatHit>()).Kind);

        Assert.Equal(SoundKind.BatHurt, SoundBoard.KindFor(new BatHit(1, 5)));
        Assert.Equal(SoundKind.GormHurt, SoundBoard.KindFor(new GormHit(1, 5)));
        Assert.Equal(SoundKind.GormHeadHurt, SoundBoard.KindFor(new GormHit(1, 5, true)));
        Assert.Equal(DamageKind.Strike, DamageKinds.Of(Weapon.Blade));
        Assert.Equal(DamageKind.Strike, DamageKinds.Of(Weapon.StakeCrossbow));
        Assert.Equal(DamageKind.Burn, DamageKinds.Of(Weapon.CleansingFlame));
    }
}
