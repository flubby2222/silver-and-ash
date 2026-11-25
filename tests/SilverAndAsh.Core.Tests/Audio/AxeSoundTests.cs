using System.Numerics;
using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Audio;

/// <summary>
/// The sounds of the hunter's axe (issue #776): its swing, its hit on a creature and on the wooden dummy, and its charge,
/// whose thump falls on the tick the charge is full. Driven through a real world tick by tick where the timing matters.
/// </summary>
public class AxeSoundTests
{
    private static readonly SoundPack Pack1 = SoundPack.Pack1;
    private static readonly PlayerInput Melee = PlayerInput.None with { Melee = true };
    private static readonly int AxeFull = SimulationClock.SecondsToTicks(Balance.Default.HuntersAxe.ChargeTime);
    private static readonly int Lead = SoundBoard.ChargeLeadTicks(SoundKind.AxeCharge);

    /// <summary>Dummies and ghouls that survive every blow, so a hit is always a hit and nothing falls over.</summary>
    private static readonly Balance Sturdy = Balance.Default with
    {
        Dummy = Balance.Default.Dummy with { MaxHealth = 100_000 },
        Ghoul = Balance.Default.Ghoul with { MaxHealth = 100_000 },
    };

    private static GameWorld With(WeaponKind weapon, string map = "P\n.\n.")
    {
        var world = new GameWorld(LevelParser.Parse(map), 1, Sturdy);
        world.Player.EquipMelee(weapon);
        return world;
    }

    /// <summary>
    /// Runs <paramref name="world"/> one tick per input of <paramref name="inputs"/> and hands each tick's events to one
    /// <see cref="SoundCues"/>, as the game does; returns the cues of every tick and the world's events of every tick.
    /// </summary>
    private static (List<IReadOnlyList<SoundCue>> Cues, List<List<GameEvent>> Events) Play(GameWorld world, IEnumerable<PlayerInput> inputs)
    {
        var sounds = new SoundCues(world.Seed);
        var cues = new List<IReadOnlyList<SoundCue>>();
        var events = new List<List<GameEvent>>();
        foreach (var input in inputs)
        {
            var tick = Run(world, input, 1);
            events.Add(tick);
            cues.Add(sounds.For(tick, Pack1));
        }

        return (cues, events);
    }

    /// <summary>Holds the melee button <paramref name="held"/> ticks (the first is the press), then lets go for <paramref name="after"/> ticks.</summary>
    private static IEnumerable<PlayerInput> Hold(int held, int after) =>
        Enumerable.Repeat(Melee, held).Concat(Enumerable.Repeat(PlayerInput.None, after));

    private static List<int> TicksWith(List<IReadOnlyList<SoundCue>> cues, Func<SoundCue, bool> which) =>
        Enumerable.Range(0, cues.Count).Where(tick => cues[tick].Any(which)).ToList();

    private static bool StartsCharge(SoundCue cue) => cue.Kind == SoundKind.AxeCharge && !cue.Stop;

    private static bool StopsCharge(SoundCue cue) => cue.Kind == SoundKind.AxeCharge && cue.Stop;

    // ---- Swing and hit ---------------------------------------------------------------------------------------------

    [Fact]
    public void An_axe_swing_into_the_air_plays_the_axe_swing()
    {
        var (cues, _) = Play(With(WeaponKind.HuntersAxe), Hold(1, 1));

        Assert.Equal([SoundKind.AxeSwing], cues.SelectMany(tick => tick).Select(cue => cue.Kind));
    }

    [Fact]
    public void An_axe_on_a_ghoul_plays_only_the_wet_hit_and_on_the_dummy_only_the_hard_one()
    {
        var (onGhoul, ghoulEvents) = Play(With(WeaponKind.HuntersAxe, "P\nG"), Hold(1, 1));
        var (onDummy, dummyEvents) = Play(With(WeaponKind.HuntersAxe, "P\nD"), Hold(1, 1));

        Assert.False(Assert.Single(ghoulEvents.SelectMany(tick => tick).OfType<TargetHitBy>()).Hard);
        Assert.True(Assert.Single(dummyEvents.SelectMany(tick => tick).OfType<TargetHitBy>()).Hard);
        Assert.Contains(SoundKind.AxeHit, onGhoul.SelectMany(tick => tick).Select(cue => cue.Kind));
        Assert.DoesNotContain(SoundKind.AxeSwing, onGhoul.SelectMany(tick => tick).Select(cue => cue.Kind));
        Assert.Equal([SoundKind.AxeHardHit], onDummy.SelectMany(tick => tick).Select(cue => cue.Kind));
    }

    [Fact]
    public void The_blade_sounds_the_same_on_the_dummy_as_on_a_creature()
    {
        Assert.Equal(SoundKind.Blade, SoundBoard.KindFor(new TargetHitBy(3, 0, Weapon.Blade) { Hard = true }));
        Assert.Equal(SoundKind.Blade, SoundBoard.KindFor(new TargetHitBy(3, 0, Weapon.Blade)));
    }

    [Fact]
    public void The_axe_sounds_come_from_their_own_files()
    {
        var axe = new[] { SoundKind.AxeSwing, SoundKind.AxeHit, SoundKind.AxeHardHit, SoundKind.AxeCharge };
        var files = axe.SelectMany(kind => Pack1.Variants(kind)).ToList();

        Assert.All(axe, kind => Assert.NotEmpty(Pack1.Variants(kind)));
        Assert.Equal(files.Count, files.Distinct().Count());
        Assert.All(files, file => Assert.StartsWith("axt-", file));
    }

    [Fact]
    public void The_club_still_swings_and_hits_silently()
    {
        Assert.Null(SoundBoard.KindFor(new MeleeSwung(0, Vector3.Zero, Vector3.UnitZ, Weapon: WeaponKind.Club)));
        Assert.Null(SoundBoard.KindFor(new TargetHitBy(3, 0, Weapon.Club) { Hard = true }));
        Assert.False(SoundBoard.ChargeKinds.ContainsKey(WeaponKind.Club));
    }

    // ---- Charging --------------------------------------------------------------------------------------------------

    [Fact]
    public void Held_on_the_charge_sound_starts_so_that_its_thump_falls_on_the_full_charge()
    {
        var world = With(WeaponKind.HuntersAxe);
        var sounds = new SoundCues(world.Seed);
        int? started = null;
        int? full = null;
        for (var tick = 0; tick < AxeFull + 10; tick++)
        {
            var cues = sounds.For(Run(world, Melee, 1), Pack1);
            started ??= cues.Any(StartsCharge) ? tick : null;
            full ??= world.Player.MeleeCharge.IsFull ? tick : null;
        }

        Assert.True(AxeFull > Lead, "The axe charges longer than the drone before the thump, so the sound waits a little.");
        Assert.Equal(AxeFull - Lead, started);
        Assert.Equal(full, started + Lead);
    }

    [Fact]
    public void A_short_tap_of_the_axe_stays_without_a_charge_sound()
    {
        // Let go before the planned start: neither a start nor a stop.
        var (cues, _) = Play(With(WeaponKind.HuntersAxe), Hold(AxeFull - Lead - 1, 2 * AxeFull));

        Assert.Empty(TicksWith(cues, cue => cue.Kind == SoundKind.AxeCharge));
    }

    [Fact]
    public void Letting_go_before_the_thump_stops_the_charge_sound_in_the_tick_of_the_swing()
    {
        var held = AxeFull - (Lead / 2);
        var (cues, events) = Play(With(WeaponKind.HuntersAxe), Hold(held, AxeFull));
        var swung = Enumerable.Range(0, events.Count).Single(tick => events[tick].OfType<MeleeSwung>().Any());

        Assert.Equal([AxeFull - Lead], TicksWith(cues, StartsCharge));
        Assert.Equal([swung], TicksWith(cues, StopsCharge));
        var stop = cues[swung].Single(StopsCharge);
        Assert.Equal(cues[AxeFull - Lead].Single(StartsCharge).File, stop.File);
    }

    [Fact]
    public void Letting_go_after_the_thump_lets_the_tail_ring_on()
    {
        var (cues, _) = Play(With(WeaponKind.HuntersAxe), Hold(AxeFull + 5, AxeFull));

        Assert.Single(TicksWith(cues, StartsCharge));
        Assert.Empty(TicksWith(cues, StopsCharge));
    }

    [Fact]
    public void Charging_twice_plays_the_charge_sound_twice()
    {
        // A second charge after the cooldown plans its own sound: the first one leaves nothing behind.
        var cooldown = SimulationClock.SecondsToTicks(Balance.Default.HuntersAxe.Cooldown);
        var inputs = Hold(AxeFull + 2, cooldown + 2).Concat(Hold(AxeFull + 2, 2));
        var (cues, _) = Play(With(WeaponKind.HuntersAxe), inputs);

        Assert.Equal(2, TicksWith(cues, StartsCharge).Count);
        Assert.Empty(TicksWith(cues, StopsCharge));
    }

    [Fact]
    public void The_blade_charges_without_a_charge_sound()
    {
        var (cues, events) = Play(With(WeaponKind.SilverBlade), Hold(AxeFull + 5, 2));

        Assert.Single(events.SelectMany(tick => tick).OfType<MeleeChargeStarted>());
        Assert.Empty(TicksWith(cues, cue => cue.Kind == SoundKind.AxeCharge));
    }

    [Fact]
    public void Pressing_the_axe_tells_how_long_its_charge_takes()
    {
        var (_, events) = Play(With(WeaponKind.HuntersAxe), Hold(1, 0));

        var started = Assert.Single(events[0].OfType<MeleeChargeStarted>());
        Assert.Equal(WeaponKind.HuntersAxe, started.Weapon);
        Assert.Equal(AxeFull, started.TicksToFull);
    }

    [Fact]
    public void Dying_while_charging_stops_the_charge_sound()
    {
        var sounds = new SoundCues(1);
        var cues = new List<IReadOnlyList<SoundCue>> { sounds.For([new MeleeChargeStarted(0, WeaponKind.HuntersAxe, AxeFull)], Pack1) };
        for (var tick = 1; tick <= AxeFull - Lead + 2; tick++)
        {
            cues.Add(sounds.For([], Pack1));
        }

        cues.Add(sounds.For([new PlayerDied(0)], Pack1));
        cues.Add(sounds.For([], Pack1));

        Assert.Single(TicksWith(cues, StartsCharge));
        Assert.Equal([cues.Count - 2], TicksWith(cues, StopsCharge));
    }

    // ---- Pause and stall (review of PR #781) ------------------------------------------------------------------------

    /// <summary>
    /// Runs <paramref name="world"/> as the game does: every input is one tick, paused or not, and after each the sounds
    /// of the world (<see cref="SoundCues.For(GameWorld, SoundPack, SoundListener)"/>).
    /// </summary>
    private static List<IReadOnlyList<SoundCue>> PlayWorld(GameWorld world, IEnumerable<PlayerInput> inputs)
    {
        var sounds = new SoundCues(world.Seed);
        var cues = new List<IReadOnlyList<SoundCue>>();
        foreach (var input in inputs)
        {
            world.Tick(input);
            cues.Add(sounds.For(world, Pack1, SoundListener.Of(world.Player)));
        }

        return cues;
    }

    private static IEnumerable<PlayerInput> Paused(int ticks) =>
        Enumerable.Repeat(Melee with { Paused = true }, ticks);

    [Fact]
    public void The_charge_sound_waits_through_a_pause_and_still_stops_on_an_early_release_after_it()
    {
        // Pressed, paused long before the planned start, held on, paused again while the sound plays, let go before the
        // thump: the start counts only world ticks, the pauses stay silent and the release still stops the sound.
        var pause = 3 * AxeFull;
        var before = AxeFull - Lead - 5;
        var playing = 10;
        var inputs = Hold(before, 0).Concat(Paused(pause)).Concat(Hold(playing, 0)).Concat(Paused(pause)).Concat(Hold(0, 3));
        var cues = PlayWorld(With(WeaponKind.HuntersAxe), inputs);

        var startTick = before + pause + 5;
        var releaseTick = before + pause + playing + pause;
        Assert.Equal([startTick], TicksWith(cues, StartsCharge));
        Assert.Equal([releaseTick], TicksWith(cues, StopsCharge));
        Assert.All(Enumerable.Range(before, pause).Concat(Enumerable.Range(before + pause + playing, pause)), tick => Assert.Empty(cues[tick]));
    }

    [Fact]
    public void Held_on_through_a_pause_the_thump_still_falls_on_the_full_charge()
    {
        var world = With(WeaponKind.HuntersAxe);
        var inputs = Hold(AxeFull - Lead + 3, 0).Concat(Paused(2 * AxeFull)).Concat(Hold(Lead, 0));
        var sounds = new SoundCues(world.Seed);
        int? started = null;
        long? startedAt = null;
        long? fullAt = null;
        var heldInPause = new List<int>();
        var tick = 0;
        foreach (var input in inputs)
        {
            world.Tick(input);
            var cues = sounds.For(world, Pack1, SoundListener.Of(world.Player));
            started ??= cues.Any(StartsCharge) ? tick : null;
            startedAt ??= cues.Any(StartsCharge) ? world.Ticks : null;
            fullAt ??= world.Player.MeleeCharge.IsFull ? world.Ticks : null;
            if (input.Paused)
            {
                heldInPause.Add(sounds.PlayingChargeFiles.Count);
            }

            tick++;
        }

        // The sound started before the pause and is still before its thump all through it: the game holds it there.
        Assert.Equal(AxeFull - Lead, started);
        Assert.All(heldInPause, count => Assert.Equal(1, count));
        Assert.True(world.Player.MeleeCharge.IsFull);
        Assert.Equal(fullAt, startedAt + Lead);
    }

    [Fact]
    public void With_empty_stamina_holding_the_axe_plays_no_charge_sound()
    {
        var world = With(WeaponKind.HuntersAxe);
        var stamina = world.Player.Stamina;
        while (stamina.TryPayPart(1))
        {
        }

        var events = new List<GameEvent>();
        var sounds = new SoundCues(world.Seed);
        var cues = new List<IReadOnlyList<SoundCue>>();
        for (var tick = 0; tick < 2 * AxeFull; tick++)
        {
            world.Tick(Melee);
            events.AddRange(world.Events);
            cues.Add(sounds.For(world, Pack1, SoundListener.Of(world.Player)));
        }

        Assert.True(world.Player.MeleeCharge.IsStalled);
        Assert.False(world.Player.MeleeCharge.IsFull);
        Assert.Single(events.OfType<MeleeChargeStalled>());
        Assert.Empty(TicksWith(cues, cue => cue.Kind == SoundKind.AxeCharge));
    }

    [Fact]
    public void A_stall_while_the_charge_sound_plays_stops_it_before_the_thump()
    {
        var sounds = new SoundCues(1);
        var cues = new List<IReadOnlyList<SoundCue>> { sounds.For([new MeleeChargeStarted(0, WeaponKind.HuntersAxe, AxeFull)], Pack1) };
        for (var tick = 1; tick <= AxeFull - Lead + 2; tick++)
        {
            cues.Add(sounds.For([], Pack1));
        }

        cues.Add(sounds.For([new MeleeChargeStalled(0)], Pack1));
        for (var tick = 0; tick < Lead; tick++)
        {
            cues.Add(sounds.For([], Pack1));
        }

        Assert.Single(TicksWith(cues, StartsCharge));
        Assert.Equal([AxeFull - Lead + 3], TicksWith(cues, StopsCharge));
        Assert.Empty(sounds.PlayingChargeFiles);
    }

    [Fact]
    public void A_new_run_stops_the_charge_sound_of_the_old_one()
    {
        var sounds = new SoundCues(1);
        sounds.For([new MeleeChargeStarted(0, WeaponKind.HuntersAxe, AxeFull)], Pack1);
        IReadOnlyList<SoundCue> started = [];
        for (var tick = 1; tick <= AxeFull - Lead && started.Count == 0; tick++)
        {
            started = sounds.For([], Pack1);
        }

        var stops = sounds.StopCharges();

        Assert.Equal(Assert.Single(started, StartsCharge).File, Assert.Single(stops, StopsCharge).File);
        Assert.Empty(sounds.PlayingChargeFiles);
        Assert.Empty(sounds.StopCharges());
    }

    [Fact]
    public void Another_players_death_leaves_the_charge_sound_alone()
    {
        var sounds = new SoundCues(1);
        var cues = new List<IReadOnlyList<SoundCue>> { sounds.For([new MeleeChargeStarted(0, WeaponKind.HuntersAxe, AxeFull)], Pack1) };
        for (var tick = 1; tick <= AxeFull - Lead + 2; tick++)
        {
            cues.Add(sounds.For([], Pack1));
        }

        cues.Add(sounds.For([new PlayerDied(1)], Pack1));

        Assert.Single(TicksWith(cues, StartsCharge));
        Assert.Empty(TicksWith(cues, StopsCharge));
    }
}
