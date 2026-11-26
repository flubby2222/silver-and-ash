using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Audio;

/// <summary>
/// The reload sound of the stake crossbow when the reload breaks off (issue #800, hint 242): it is one file of 3 s that
/// starts with <see cref="ReloadStarted"/>, so a reload that ends early (weapon switch, swap from the bag, dropping the
/// weapon, death, a new run) must stop it - Core tells the break as <see cref="ReloadCancelled"/>. Driven through a real
/// world tick by tick with <see cref="Balance.Default"/>, the cues made by one <see cref="SoundCues"/> as the game does.
/// </summary>
public class ReloadCancelSoundTests
{
    private static readonly SoundPack Pack1 = SoundPack.Pack1;
    private static readonly int ReloadTicks = SimulationClock.SecondsToTicks(Balance.Default.StakeCrossbow.ReloadTime);
    private static readonly PlayerInput Reload = PlayerInput.None with { Reload = true };
    private static readonly PlayerInput OpenMenu = PlayerInput.None with { PressMenu = MenuKey.Equipment };

    /// <summary>A world with an empty stake crossbow in the hand (place 0) and the given weapons in the other ranged places.</summary>
    private static GameWorld With(string map = "P\n.\n.", params WeaponKind[] others)
    {
        var world = new GameWorld(LevelParser.Parse(map), 1);
        world.Player.Equip(0, WeaponKind.StakeCrossbow);
        for (var place = 0; place < others.Length; place++)
        {
            world.Player.Equip(place + 1, others[place]);
        }

        Assert.True(world.Player.Ranged.Magazine.TryTake());
        return world;
    }

    /// <summary>The cues of every tick of <paramref name="inputs"/>, made by one <see cref="SoundCues"/>; the events go to <paramref name="events"/>.</summary>
    private static List<IReadOnlyList<SoundCue>> Play(GameWorld world, SoundCues sounds, List<GameEvent> events, params PlayerInput[] inputs)
    {
        var cues = new List<IReadOnlyList<SoundCue>>();
        foreach (var input in inputs)
        {
            world.Tick(input);
            events.AddRange(world.Events);
            cues.Add(sounds.For(world.Events.ToList(), Pack1));
        }

        return cues;
    }

    private static PlayerInput[] Idle(int ticks) => [.. Enumerable.Repeat(PlayerInput.None, ticks)];

    private static bool StartsReload(SoundCue cue) => cue.Kind == SoundKind.CrossbowReload && !cue.Stop;

    private static bool StopsReload(SoundCue cue) => cue.Kind == SoundKind.CrossbowReload && cue.Stop;

    private static string ReloadFile => Assert.Single(Pack1.Variants(SoundKind.CrossbowReload));

    /// <summary>Starts the reload, lets it run <paramref name="ticks"/> more ticks and returns those cues (the first entry is the press).</summary>
    private static List<IReadOnlyList<SoundCue>> StartAndRun(GameWorld world, SoundCues sounds, List<GameEvent> events, int ticks) =>
        Play(world, sounds, events, [Reload, .. Idle(ticks)]);

    private static void AssertStoppedOnceAt(List<IReadOnlyList<SoundCue>> cues, int tick)
    {
        Assert.Equal([0], Enumerable.Range(0, cues.Count).Where(i => cues[i].Any(StartsReload)));
        Assert.Equal([tick], Enumerable.Range(0, cues.Count).Where(i => cues[i].Any(StopsReload)));
        Assert.Equal(ReloadFile, cues[tick].Single(StopsReload).File);
    }

    // ---- A full reload ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_reload_that_runs_to_its_end_plays_its_sound_once_and_never_stops_it()
    {
        var world = With();
        var events = new List<GameEvent>();

        var cues = StartAndRun(world, new SoundCues(world.Seed), events, ReloadTicks + 30);

        Assert.Single(events.OfType<ReloadFinished>());
        Assert.DoesNotContain(events, e => e is ReloadCancelled);
        Assert.Equal([0], Enumerable.Range(0, cues.Count).Where(i => cues[i].Any(StartsReload)));
        Assert.DoesNotContain(cues, tick => tick.Any(StopsReload));
        Assert.Equal(ReloadFile, cues[0].Single(StartsReload).File);
    }

    // ---- Every way a reload breaks off -----------------------------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Switching_the_weapon_stops_the_reload_sound_in_the_tick_of_the_switch(bool byNumberKey)
    {
        var world = With("P\n.\n.", WeaponKind.Pistol);
        var events = new List<GameEvent>();
        var sounds = new SoundCues(world.Seed);
        var switchInput = byNumberKey ? PlayerInput.None with { SelectSlot = 1 } : PlayerInput.None with { CycleSlot = 1 };

        var cues = Play(world, sounds, events, [Reload, .. Idle(40), switchInput, .. Idle(ReloadTicks)]);

        AssertStoppedOnceAt(cues, 41);
        Assert.Equal(WeaponKind.Pistol, world.Player.Ranged.Kind);
        Assert.DoesNotContain(events, e => e is ReloadFinished);
        Assert.Equal(new ReloadCancelled(world.Player.Id, WeaponKind.StakeCrossbow) { Place = world.Player.Position }, Assert.Single(events.OfType<ReloadCancelled>()));
    }

    [Fact]
    public void Swapping_the_weapon_in_the_hand_with_one_from_the_bag_stops_the_reload_sound()
    {
        var world = With("P\n.\n.", WeaponKind.Rifle, WeaponKind.Blunderbuss);
        world.Player.Stow(WeaponKind.Pistol);
        var events = new List<GameEvent>();
        var sounds = new SoundCues(world.Seed);
        var click = PlayerInput.None with { InventoryClick = new InventoryClick(InventoryPlace.Bag, 0) };

        var cues = Play(world, sounds, events, [OpenMenu, Reload, .. Idle(40), click, .. Idle(ReloadTicks)]);

        Assert.Equal(WeaponKind.Pistol, world.Player.Ranged.Kind);
        Assert.Equal(WeaponKind.StakeCrossbow, world.Player.Bag[0]);
        Assert.Single(events.OfType<ReloadCancelled>());
        Assert.Equal([1], Enumerable.Range(0, cues.Count).Where(i => cues[i].Any(StartsReload)));
        Assert.Equal([42], Enumerable.Range(0, cues.Count).Where(i => cues[i].Any(StopsReload)));
    }

    [Fact]
    public void Dropping_the_weapon_in_the_hand_stops_the_reload_sound()
    {
        var world = With("P\n.\n.", WeaponKind.Pistol);
        var events = new List<GameEvent>();
        var sounds = new SoundCues(world.Seed);
        var drop = PlayerInput.None with { InventoryClick = new InventoryClick(InventoryPlace.Ranged, 0, Drop: true) };

        var cues = Play(world, sounds, events, [OpenMenu, Reload, .. Idle(40), drop, .. Idle(ReloadTicks)]);

        Assert.Single(world.WeaponDrops);
        Assert.Single(events.OfType<ReloadCancelled>());
        Assert.Equal([1], Enumerable.Range(0, cues.Count).Where(i => cues[i].Any(StartsReload)));
        Assert.Equal([42], Enumerable.Range(0, cues.Count).Where(i => cues[i].Any(StopsReload)));
    }

    [Fact]
    public void The_death_of_the_player_stops_the_reload_sound()
    {
        // A ghoul in reach that kills with one blow, long before the reload of 3 s is over.
        var balance = Balance.Default with { Ghoul = Balance.Default.Ghoul with { AttackDamage = 10_000 } };
        var world = new GameWorld(LevelParser.Parse("P\nG"), 1, balance);
        world.Player.Equip(0, WeaponKind.StakeCrossbow);
        Assert.True(world.Player.Ranged.Magazine.TryTake());
        var events = new List<GameEvent>();
        var sounds = new SoundCues(world.Seed);

        var cues = Play(world, sounds, events, [Reload, .. Idle(ReloadTicks)]);

        var died = Enumerable.Range(0, cues.Count).Single(i => cues[i].Any(StopsReload));
        Assert.Single(events.OfType<PlayerDied>());
        Assert.True(died < ReloadTicks, "The player lived through the whole reload - the test would prove nothing.");
        Assert.Equal([0], Enumerable.Range(0, cues.Count).Where(i => cues[i].Any(StartsReload)));
        Assert.Equal(ReloadFile, cues[died].Single(StopsReload).File);
    }

    [Fact]
    public void A_new_run_stops_the_reload_sound_of_the_old_one()
    {
        var world = With();
        var events = new List<GameEvent>();
        var sounds = new SoundCues(world.Seed);
        StartAndRun(world, sounds, events, 40);

        var stops = sounds.StopCharges();

        Assert.Equal(ReloadFile, Assert.Single(stops, StopsReload).File);
        Assert.DoesNotContain(sounds.StopCharges(), StopsReload);
    }

    // ---- Where it must stay silent ---------------------------------------------------------------------------------

    [Fact]
    public void A_weapon_switch_after_the_reload_is_done_stops_nothing()
    {
        var world = With("P\n.\n.", WeaponKind.Pistol);
        var events = new List<GameEvent>();
        var sounds = new SoundCues(world.Seed);

        var cues = Play(world, sounds, events, [Reload, .. Idle(ReloadTicks + 5), PlayerInput.None with { SelectSlot = 1 }, .. Idle(5)]);

        Assert.DoesNotContain(events, e => e is ReloadCancelled);
        Assert.DoesNotContain(cues, tick => tick.Any(StopsReload));
    }

    [Fact]
    public void A_death_after_the_reload_is_done_stops_nothing()
    {
        var world = With();
        var events = new List<GameEvent>();
        var sounds = new SoundCues(world.Seed);
        StartAndRun(world, sounds, events, ReloadTicks + 5);
        Assert.Single(events.OfType<ReloadFinished>());

        Assert.DoesNotContain(sounds.For([new PlayerDied(world.Player.Id)], Pack1), StopsReload);
        Assert.DoesNotContain(sounds.StopCharges(), StopsReload);
    }

    [Fact]
    public void A_switch_without_a_reload_under_way_stops_nothing()
    {
        var world = With("P\n.\n.", WeaponKind.Pistol);
        var events = new List<GameEvent>();
        var sounds = new SoundCues(world.Seed);

        var cues = Play(world, sounds, events, PlayerInput.None with { SelectSlot = 1 }, PlayerInput.None);

        Assert.DoesNotContain(events, e => e is ReloadCancelled);
        Assert.DoesNotContain(cues, tick => tick.Any(StopsReload));
    }

    [Fact]
    public void Another_players_cancelled_reload_leaves_the_sound_alone()
    {
        var sounds = new SoundCues(1);
        sounds.For([new ReloadStarted(0, WeaponKind.StakeCrossbow)], Pack1);

        var cues = sounds.For([new ReloadCancelled(1, WeaponKind.StakeCrossbow), new PlayerDied(1)], Pack1);

        Assert.DoesNotContain(cues, StopsReload);
        Assert.Equal(ReloadFile, Assert.Single(sounds.For([new ReloadCancelled(0, WeaponKind.StakeCrossbow)], Pack1), StopsReload).File);
    }

    [Fact]
    public void The_pistols_reload_sound_is_not_stopped_and_the_cancel_itself_makes_no_sound()
    {
        var sounds = new SoundCues(1);
        sounds.For([new ReloadStarted(0)], Pack1);

        var cues = sounds.For([new ReloadCancelled(0, WeaponKind.Pistol)], Pack1);

        Assert.Empty(cues);
        Assert.Null(SoundBoard.KindFor(new ReloadCancelled(0, WeaponKind.StakeCrossbow)));
    }
}
