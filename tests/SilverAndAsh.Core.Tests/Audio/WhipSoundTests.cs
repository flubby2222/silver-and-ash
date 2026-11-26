using System.Numerics;
using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Audio;

/// <summary>
/// The sounds of the whip (issue #786): its swing, its hit on a creature and on the wooden dummy, its pull and its
/// charge, whose peak falls on the tick the charge is full. Driven through a real world tick by tick where the timing
/// matters; the charge's pause and stop are the axe's (<see cref="AxeSoundTests"/>), the same code for every entry of
/// <see cref="SoundBoard.ChargeKinds"/>.
/// </summary>
public class WhipSoundTests
{
    private static readonly SoundPack Pack1 = SoundPack.Pack1;
    private static readonly PlayerInput Melee = PlayerInput.None with { Melee = true };
    private static readonly int WhipFull = SimulationClock.SecondsToTicks(Balance.Default.Whip.ChargeTime);
    private static readonly int Lead = SoundBoard.ChargeLeadTicks(SoundKind.WhipCharge);

    /// <summary>Dummies and ghouls that survive every blow and ghouls that stand still, so a hit is always a hit.</summary>
    private static readonly Balance Sturdy = Balance.Default with
    {
        Dummy = Balance.Default.Dummy with { MaxHealth = 100_000 },
        Ghoul = Balance.Default.Ghoul with { MaxHealth = 100_000, SightRange = 0f },
    };

    private static GameWorld With(string map = "P\n.\n.")
    {
        var world = new GameWorld(LevelParser.Parse(map), 1, Sturdy);
        world.Player.EquipMelee(WeaponKind.Whip);
        return world;
    }

    /// <summary>Runs one tick per input and hands each tick's events to one <see cref="SoundCues"/>, as the game does.</summary>
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

    private static List<SoundKind> Kinds(List<IReadOnlyList<SoundCue>> cues) => [.. cues.SelectMany(tick => tick).Select(cue => cue.Kind)];

    private static List<int> TicksWith(List<IReadOnlyList<SoundCue>> cues, Func<SoundCue, bool> which) =>
        Enumerable.Range(0, cues.Count).Where(tick => cues[tick].Any(which)).ToList();

    private static bool StartsCharge(SoundCue cue) => cue.Kind == SoundKind.WhipCharge && !cue.Stop;

    // ---- Swing, hit and pull ---------------------------------------------------------------------------------------

    [Fact]
    public void A_whip_swing_into_the_air_plays_the_whip_swing()
    {
        var (cues, _) = Play(With(), Hold(1, 1));

        Assert.Equal([SoundKind.WhipSwing], Kinds(cues));
    }

    [Fact]
    public void A_whip_on_the_dummy_plays_only_the_hard_hit()
    {
        var (cues, events) = Play(With("P\nD"), Hold(1, 1));

        Assert.True(Assert.Single(events.SelectMany(tick => tick).OfType<TargetHitBy>()).Hard);
        Assert.Equal([SoundKind.WhipHardHit], Kinds(cues));
    }

    [Fact]
    public void A_whip_pulling_a_ghoul_in_plays_its_hit_and_the_pull_in_the_same_tick_without_the_swing()
    {
        var (cues, events) = Play(With("P\n.\nG\n."), Hold(1, 1));

        var swung = Enumerable.Range(0, events.Count).Single(tick => events[tick].OfType<MeleeSwung>().Any());
        Assert.Single(events[swung].OfType<MeleePulled>());
        Assert.False(Assert.Single(events[swung].OfType<TargetHitBy>()).Hard);
        Assert.Equal([SoundKind.WhipHit, SoundKind.WhipPull], cues[swung].Select(cue => cue.Kind).Order());
        Assert.DoesNotContain(SoundKind.WhipSwing, Kinds(cues));
    }

    [Fact]
    public void A_ghoul_already_close_is_hit_but_not_pulled_and_plays_no_pull()
    {
        // Nearer than the pull distance: the whip hits, the ghoul stays put - no pull, no jerk.
        var world = With("P\n.\nG");
        world.Ghouls[0].PlaceAt(world.Player.Position + new Vector3(0f, 0f, Balance.Default.Whip.PullTo - 0.2f));
        var (cues, events) = Play(world, Hold(1, 1));

        Assert.DoesNotContain(events.SelectMany(tick => tick), e => e is MeleePulled);
        Assert.Contains(SoundKind.WhipHit, Kinds(cues));
        Assert.DoesNotContain(SoundKind.WhipPull, Kinds(cues));
    }

    [Fact]
    public void The_pull_sounds_the_same_however_far_the_creature_is_pulled()
    {
        // #751, note 189: the sound does not read the distance - a short jerk and a long one are the same leather.
        var near = SoundBoard.KindFor(new MeleePulled(3, 0, WeaponKind.Whip, 0.05f));
        var far = SoundBoard.KindFor(new MeleePulled(3, 0, WeaponKind.Whip, 4.5f));

        Assert.Equal(SoundKind.WhipPull, near);
        Assert.Equal(near, far);
    }

    [Fact]
    public void A_weapon_without_a_pull_sound_pulls_silently()
    {
        Assert.Null(SoundBoard.KindFor(new MeleePulled(3, 0, WeaponKind.Club, 1f)));
    }

    [Fact]
    public void The_whip_sounds_come_from_their_own_files()
    {
        var whip = new[] { SoundKind.WhipSwing, SoundKind.WhipHit, SoundKind.WhipHardHit, SoundKind.WhipCharge, SoundKind.WhipPull };
        var files = whip.SelectMany(kind => Pack1.Variants(kind)).ToList();

        Assert.All(whip, kind => Assert.NotEmpty(Pack1.Variants(kind)));
        Assert.Equal(files.Count, files.Distinct().Count());
        Assert.All(files, file => Assert.StartsWith("peitsche-", file));
    }

    // ---- Charging --------------------------------------------------------------------------------------------------

    [Fact]
    public void Held_on_the_whirl_starts_so_that_its_peak_falls_on_the_full_charge()
    {
        var world = With();
        var sounds = new SoundCues(world.Seed);
        int? started = null;
        int? full = null;
        for (var tick = 0; tick < WhipFull + 10; tick++)
        {
            var cues = sounds.For(Run(world, Melee, 1), Pack1);
            started ??= cues.Any(StartsCharge) ? tick : null;
            full ??= world.Player.MeleeCharge.IsFull ? tick : null;
        }

        Assert.True(Lead > 0, "The whirl has a peak, so it must start before the charge is full.");
        Assert.True(WhipFull > Lead, "The whip charges longer than the whirl runs up to its peak, so the sound waits a little.");
        Assert.Equal(WhipFull - Lead, started);
        Assert.Equal(full, started + Lead);
    }

    [Fact]
    public void Letting_go_of_the_whip_before_the_peak_stops_the_whirl_in_the_tick_of_the_swing()
    {
        var (cues, events) = Play(With(), Hold(WhipFull - (Lead / 2), WhipFull));
        var swung = Enumerable.Range(0, events.Count).Single(tick => events[tick].OfType<MeleeSwung>().Any());

        Assert.Equal([WhipFull - Lead], TicksWith(cues, StartsCharge));
        Assert.Equal([swung], TicksWith(cues, cue => cue.Kind == SoundKind.WhipCharge && cue.Stop));
    }

    [Fact]
    public void The_charge_sounds_of_the_whip_and_the_axe_are_their_own()
    {
        Assert.Equal(SoundKind.WhipCharge, SoundBoard.ChargeKinds[WeaponKind.Whip]);
        Assert.NotEqual(SoundBoard.ChargeKinds[WeaponKind.HuntersAxe], SoundBoard.ChargeKinds[WeaponKind.Whip]);
        Assert.Null(SoundBoard.KindFor(new MeleeSwung(0, Vector3.Zero, Vector3.UnitZ, Weapon: WeaponKind.Club)));
    }
}
