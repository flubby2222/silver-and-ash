using System.Numerics;
using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Audio;

/// <summary>Sound pack 1, the old pack and the key that switches between them (issue #290).</summary>
public class SoundPackTests
{
    private static readonly SoundPack Pack1 = SoundPack.Pack1;
    private static readonly MeleeSwung BladeSwing = new(0, Vector3.Zero, Vector3.UnitZ);
    private static readonly TargetHitBy BladeHit = new(0, 0, Weapon.Blade);

    public static TheoryData<GameEvent, SoundKind, string[]> Pack1Events => new()
    {
        { new ShotFired(0, Vector3.Zero, Vector3.UnitZ), SoundKind.Pistol, ["p1-schuss-1.wav", "p1-schuss-2.wav", "p1-schuss-3.wav"] },
        { new ShotFired(0, Vector3.Zero, Vector3.UnitZ, Consecrated: true), SoundKind.ConsecratedShot, ["p1-schuss-geweiht-1.wav", "p1-schuss-geweiht-2.wav"] },
        { new ReloadStarted(0), SoundKind.ReloadStarted, ["p1-nachladen-start.wav"] },
        { new ReloadFinished(0), SoundKind.ReloadFinished, ["p1-nachladen-bereit.wav"] },
        { BladeSwing, SoundKind.BladeSwing, ["p1-schwung-1.wav", "p1-schwung-2.wav"] },
        { BladeHit, SoundKind.Blade, ["p1-treffer-1.wav", "p1-treffer-2.wav", "p1-treffer-3.wav"] },
        { new PlayerStepped(0, Foot.Left), SoundKind.StepLeft, ["p1-schritt-l1.wav", "p1-schritt-l2.wav", "p1-schritt-l3.wav"] },
        { new PlayerStepped(0, Foot.Right), SoundKind.StepRight, ["p1-schritt-r1.wav", "p1-schritt-r2.wav", "p1-schritt-r3.wav"] },
        { new PlayerHit(0, 5), SoundKind.PlayerHurt, ["p1-schmerz.wav"] },
        { new GhoulWindUp(2), SoundKind.GhoulWindUp, ["p1-ghul-ankuendigung.wav"] },
        { new GhoulDied(2), SoundKind.GhoulDied, ["p1-ghul-sterben.wav", "p1-ghul-sterben-2.wav"] },
        { new DoorOpened(1), SoundKind.DoorOpened, ["p1-tuer-oeffnen.wav"] },
        { new ChestOpened(1, 0, ChestTier.Guard), SoundKind.ChestOpened, ["p1-kiste-oeffnen.wav"] },
        { new SilverTaken(1, 0, 10), SoundKind.SilverTaken, ["p1-aufheben-silber.wav"] },
        { new PickupTaken(1, 0, ConsumableKind.BloodVial), SoundKind.BloodVialTaken, ["p1-aufheben-phiole.wav"] },
        { new PickupTaken(1, 0, ConsumableKind.ConsecratedAmmo), SoundKind.ConsecratedAmmoTaken, ["p1-aufheben-weihmunition.wav"] },
        { new KeyTaken(1, 0), SoundKind.KeyTaken, ["p1-aufheben-schluessel.wav"] },

        // Issue #291.
        { new PlayerJumped(0), SoundKind.Jump, ["p1-sprung.wav"] },
        { new PlayerDashed(0), SoundKind.Dash, ["p1-dash.wav"] },
        { new PlayerDodged(0), SoundKind.Dash, ["p1-dash.wav"] },
        { new PlayerLanded(0, SoundBoard.LandingSoundMinimumFallHeight), SoundKind.Land, ["p1-landung.wav"] },
        { new PlayerLanded(0, 3f), SoundKind.Land, ["p1-landung.wav"] },
        { new GhoulNoticed(2, 0), SoundKind.GhoulNoticed, ["p1-ghul-entdecken.wav", "p1-ghul-entdecken-2.wav"] },
        { new ConsecrationEnded(0), SoundKind.ConsecrationEnded, ["p1-weihe-ende.wav"] },

        // Issue #297.
        { new BatFlapped(4, Vector3.Zero), SoundKind.BatFlap, ["p1-fledermaus-flattern-1.wav", "p1-fledermaus-flattern-2.wav"] },
        { new BatSpitWindUp(4), SoundKind.BatSpitWindUp, ["p1-fledermaus-spuck-ankuendigung.wav"] },
        { new BatSpat(4, Vector3.Zero, Vector3.UnitZ), SoundKind.BatSpat, ["p1-fledermaus-spucken.wav"] },
        { new BatScreeched(4), SoundKind.BatScreech, ["p1-fledermaus-kreischen.wav"] },
        { new BatBit(4, 0), SoundKind.BatBite, ["p1-fledermaus-biss.wav"] },
        { new BatDiveMissed(4), SoundKind.BatDiveMissed, ["p1-fledermaus-fehlsturz.wav"] },
        { new BatHit(4, 10), SoundKind.BatHurt, ["p1-fledermaus-treffer.wav"] },
        { new BatDied(4), SoundKind.BatDied, ["p1-fledermaus-sterben.wav"] },

        // Issue #298: Gorm.
        { new GormStepped(9), SoundKind.GormStep, ["p1-gorm-schritt-1.wav", "p1-gorm-schritt-2.wav"] },
        { new GormWindUp(9, GormAttack.Hammer), SoundKind.GormHammerWindUp, ["p1-gorm-ankuendigung-hammer.wav"] },
        { new GormWindUp(9, GormAttack.Swing), SoundKind.GormSwingWindUp, ["p1-gorm-ankuendigung-schwung.wav"] },
        { new GormWindUp(9, GormAttack.Throw), SoundKind.GormThrowWindUp, ["p1-gorm-ankuendigung-wurf.wav"] },
        { new GormSlammed(9), SoundKind.GormSlam, ["p1-gorm-aufschlag.wav"] },
        { new GormSwingStarted(9), SoundKind.GormChain, ["p1-gorm-kette.wav"] },
        { new GormThrew(9, Vector3.Zero, Vector3.UnitZ), SoundKind.GormThrow, ["p1-gorm-haken-wurf.wav"] },
        { new HookHit(0), SoundKind.GormHookHit, ["p1-gorm-haken-treffer.wav"] },
        { new GormHit(9, 40), SoundKind.GormHurt, ["p1-gorm-treffer-1.wav", "p1-gorm-treffer-2.wav"] },
        { new GormHit(9, 40, Critical: true), SoundKind.GormHeadHurt, ["p1-gorm-treffer-kopf.wav"] },
        { new GormRoared(9), SoundKind.GormRoar, ["p1-gorm-bruellen.wav"] },
        { new GormDied(9), SoundKind.GormDied, ["p1-gorm-sterben.wav"] },

        // Issue #317: taking equipment - every tier; the Vampiric one has a kind of its own (same file until its own comes).
        { new EquipmentTaken(3, 0, Equipment.Of(WeaponKind.Club), Vector3.Zero), SoundKind.EquipmentTaken, ["p1-aufheben-ausruestung.wav"] },
        { new EquipmentTaken(3, 0, Equipment.Of(WeaponKind.Rifle), Vector3.Zero), SoundKind.EquipmentTaken, ["p1-aufheben-ausruestung.wav"] },
        { new EquipmentTaken(3, 0, Equipment.Of(ItemKind.HuntersEye), Vector3.Zero), SoundKind.EquipmentTaken, ["p1-aufheben-ausruestung.wav"] },
        { new EquipmentTaken(3, 0, Equipment.Of(ItemKind.Leech), Vector3.Zero), SoundKind.VampiricEquipmentTaken, ["p1-aufheben-ausruestung.wav"] },

        // Issue #375: a skill point (level up) - the strike with choir from the listening test of #372.
        { new LevelUp(0, 2), SoundKind.SkillPoint, ["skillpunkt.wav"] },

        // Issue #605: the breaking column - the blow before the last one, and the break.
        { new ColumnHit(Pillar, HitsToBreak - 1, HitsToBreak), SoundKind.ColumnHit2, ["saeule-treffer-stufe2.wav"] },
        { new ColumnBroke(Pillar), SoundKind.ColumnBroke, ["saeule-bruch.wav"] },

        // Issue #569: the boss door swinging shut and swinging open.
        { new DoorSwingStarted(0, Closing: true), SoundKind.BossDoorClosing, ["bosstuer-schliessen.wav"] },
        { new DoorSwingStarted(0, Closing: false), SoundKind.BossDoorOpening, ["bosstuer-oeffnen.wav"] },
    };

    private static readonly Column Pillar = Column.AtCell(3, 6);
    private static readonly int HitsToBreak = Balance.Default.Columns.HitsToBreak;

    [Fact]
    public void Several_skill_points_in_one_tick_play_the_sound_once_and_the_next_tick_again()
    {
        // Issue #375: two levels from one kill would only add up to one louder strike.
        var cues = new SoundCues(1);

        var first = Assert.Single(cues.For([new LevelUp(0, 2), new LevelUp(0, 3)], Pack1));
        var second = Assert.Single(cues.For([new LevelUp(0, 4)], Pack1));

        Assert.Equal("skillpunkt.wav", first.File);
        Assert.Equal("skillpunkt.wav", second.File);
        Assert.Empty(cues.For([new ExperienceGained(0, 30)], Pack1));
    }

    private static readonly GameEvent[] BatEvents =
    [
        new BatFlapped(4, Vector3.Zero), new BatSpitWindUp(4), new BatSpat(4, Vector3.Zero, Vector3.UnitZ), new BatScreeched(4),
        new BatBit(4, 0), new BatDiveMissed(4), new BatHit(4, 10), new BatDied(4),
    ];

    private static readonly GameEvent[] GormEvents =
    [
        new GormStepped(9), new GormWindUp(9, GormAttack.Hammer), new GormWindUp(9, GormAttack.Swing),
        new GormWindUp(9, GormAttack.Throw), new GormSlammed(9), new GormSwingStarted(9),
        new GormThrew(9, Vector3.Zero, Vector3.UnitZ), new HookHit(0), new GormHit(9, 40), new GormHit(9, 40, Critical: true),
        new GormRoared(9), new GormDied(9),
    ];

    [Theory]
    [MemberData(nameof(Pack1Events))]
    public void Each_event_plays_its_files_of_pack_1(GameEvent gameEvent, SoundKind kind, string[] files)
    {
        Assert.Equal(kind, SoundBoard.KindFor(gameEvent));
        Assert.Equal(files, Pack1.Variants(kind));

        var cue = Assert.Single(new SoundCues(1).For([gameEvent], Pack1));
        Assert.Equal(kind, cue.Kind);
        Assert.Contains(cue.File, files);
        Assert.Equal(SoundBoard.SoundVolume, cue.Volume);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.25f)]
    [InlineData(0.4f)]
    [InlineData(SoundBoard.LandingSoundMinimumFallHeight - 0.01f)]
    public void A_landing_from_below_the_threshold_plays_nothing(float fallHeight)
    {
        Assert.Null(SoundBoard.KindFor(new PlayerLanded(0, fallHeight)));
        Assert.Empty(new SoundCues(1).For([new PlayerLanded(0, fallHeight)], Pack1));
    }

    [Fact]
    public void The_landing_threshold_lies_above_the_step_height_and_below_a_normal_jump()
    {
        Assert.True(SoundBoard.LandingSoundMinimumFallHeight > BodySizes.Player.StepHeight);
        Assert.True(SoundBoard.LandingSoundMinimumFallHeight < Balance.Default.Player.JumpHeight);
    }

    [Theory]
    [InlineData(SoundKind.GhoulNoticed)]
    [InlineData(SoundKind.GhoulDied)]
    public void The_two_ghoul_takes_take_turns(SoundKind kind)
    {
        var cues = new SoundCues(5);
        GameEvent gameEvent = kind == SoundKind.GhoulDied ? new GhoulDied(1) : new GhoulNoticed(1, 0);

        var files = Enumerable.Range(0, 20).Select(_ => Assert.Single(cues.For([gameEvent], Pack1)).File).ToList();

        // Two files, never the same twice in a row: they alternate, decided by the seed.
        Assert.All(files.Zip(files.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second));
        Assert.Equal(Pack1.Variants(kind).Order(), files.Distinct().Order());
    }

    [Fact]
    public void The_menu_click_plays_without_an_event_in_pack_1_only()
    {
        var cue = Assert.Single(new SoundCues(0).For(SoundKind.MenuClick, Pack1));
        Assert.Equal("p1-menue-klick.wav", cue.File);
        Assert.Equal(SoundBoard.SoundVolume, cue.Volume);

        Assert.Empty(new SoundCues(0).For(SoundKind.MenuClick, SoundPack.Old));
    }

    [Fact]
    public void The_menu_click_belongs_to_no_event_of_core()
    {
        GameEvent[] events =
        [
            new PlayerJumped(0), new PlayerDashed(0), new PlayerDodged(0), new PlayerLanded(0, 9f), new GhoulNoticed(1, 0),
            new ConsecrationEnded(0), new PlayerStepped(0, Foot.Left), new GhoulDied(1),
        ];

        Assert.DoesNotContain(SoundKind.MenuClick, events.Select(SoundBoard.KindFor));
    }

    [Fact]
    public void Pack_1_has_a_sound_for_every_kind()
    {
        Assert.All(Enum.GetValues<SoundKind>(), kind => Assert.NotEmpty(Pack1.Variants(kind)));
    }

    [Fact]
    public void The_old_pack_plays_a_consecrated_shot_like_any_pistol_shot()
    {
        var cue = Assert.Single(new SoundCues(1).For([new ShotFired(0, Vector3.Zero, Vector3.UnitZ, Consecrated: true)], SoundPack.Old));

        Assert.Equal("pistole.wav", cue.File);
    }

    [Fact]
    public void The_old_pack_plays_nothing_for_the_new_kinds()
    {
        Assert.Empty(new SoundCues(1).For(
            [BladeSwing, new ReloadFinished(0), new GhoulDied(1), new DoorOpened(1), new KeyTaken(1, 0)], SoundPack.Old));

        // Issue #291: silent in the old pack, too.
        Assert.Empty(new SoundCues(1).For(
            [new PlayerJumped(0), new PlayerDashed(0), new PlayerDodged(0), new PlayerLanded(0, 3f), new GhoulNoticed(1, 0),
             new ConsecrationEnded(0)],
            SoundPack.Old));

        // Issue #297: the bat is silent in the old pack.
        Assert.All(BatEvents, gameEvent => Assert.Empty(new SoundCues(1).For([gameEvent], SoundPack.Old)));

        // Issue #298: so is Gorm.
        Assert.All(GormEvents, gameEvent => Assert.Empty(new SoundCues(1).For([gameEvent], SoundPack.Old)));

        // Issue #317: and taking equipment, of every tier.
        Assert.All(Equipment.All, piece =>
            Assert.Empty(new SoundCues(1).For([new EquipmentTaken(3, 0, piece, Vector3.Zero)], SoundPack.Old)));

        // Issue #375: and the skill point.
        Assert.Empty(new SoundCues(1).For([new LevelUp(0, 2)], SoundPack.Old));

        // Issue #605: and the breaking column.
        Assert.Empty(new SoundCues(1).For(
            [new ColumnHit(Pillar, 1, HitsToBreak), new ColumnHit(Pillar, HitsToBreak - 1, HitsToBreak), new ColumnBroke(Pillar)],
            SoundPack.Old));
    }

    [Fact]
    public void Gorms_three_warnings_differ_from_each_other_and_from_the_other_enemies()
    {
        // Issue #298: each attack has its own answer (jump, duck, step aside) - so each has its own warning.
        SoundKind[] gorm = [SoundKind.GormHammerWindUp, SoundKind.GormSwingWindUp, SoundKind.GormThrowWindUp];
        SoundKind[] others = [SoundKind.GhoulWindUp, SoundKind.BatSpitWindUp, SoundKind.BatScreech];

        var files = gorm.Select(kind => Pack1.Variants(kind)).ToList();
        Assert.All(files, variants => Assert.NotEmpty(variants));
        Assert.Equal(files.Sum(variants => variants.Length), files.SelectMany(variants => variants).Distinct().Count());
        Assert.Empty(files.SelectMany(variants => variants).Intersect(others.SelectMany(kind => Pack1.Variants(kind))));
    }

    [Fact]
    public void Every_warning_of_gorm_comes_through_in_a_busy_tick()
    {
        // Steps, a chain, a hit and a step of a player in the same tick hold back no warning.
        foreach (var attack in new[] { GormAttack.Hammer, GormAttack.Swing, GormAttack.Throw })
        {
            var cues = new SoundCues(1).For(
                [new GormStepped(9), new PlayerStepped(0, Foot.Left), new GormSwingStarted(9), new GormHit(9, 10),
                 new GormWindUp(9, attack)],
                Pack1, new Vector3(100f, 0f, 0f));

            Assert.Contains(cues, cue => cue.Kind == SoundBoard.KindFor(new GormWindUp(9, attack)));
        }
    }

    [Fact]
    public void The_end_of_gorms_swing_plays_nothing()
    {
        // Review of #310: the chain sounds as the sweep starts (GormSwingStarted); its end only tells whether it hit.
        Assert.Null(SoundBoard.KindFor(new GormSwung(9, Hit: true)));
        Assert.Null(SoundBoard.KindFor(new GormSwung(9, Hit: false)));
    }

    [Fact]
    public void Gorm_is_heard_from_anywhere()
    {
        // No range for Gorm: he only acts awake in his boss room, and his warnings must always come through.
        var far = new Vector3(100f, 0f, 0f);

        Assert.All(GormEvents, gameEvent => Assert.True(SoundBoard.IsInEarshot(gameEvent, far)));
    }

    [Fact]
    public void A_blade_hit_on_gorm_plays_his_own_hit_in_pack_1()
    {
        var cues = new SoundCues(1).For([BladeSwing, new GormHit(9, 40), BladeHit with { TargetId = 9 }], Pack1);

        Assert.Equal([SoundKind.GormHurt], cues.Select(cue => cue.Kind));
    }

    [Fact]
    public void A_blade_hit_on_gorm_plays_the_blade_in_the_old_pack()
    {
        var cues = new SoundCues(1).For([BladeSwing, new GormHit(9, 40), BladeHit with { TargetId = 9 }], SoundPack.Old);

        Assert.Equal([SoundKind.Blade], cues.Select(cue => cue.Kind));
    }

    [Fact]
    public void A_pistol_shot_to_gorms_head_plays_the_shot_and_his_head_hit()
    {
        var cues = new SoundCues(1).For(
            [new ShotFired(0, Vector3.Zero, Vector3.UnitZ), new GormHit(9, 10, Critical: true), new TargetHitBy(9, 0, Weapon.Pistol)],
            Pack1);

        Assert.Equal([SoundKind.Pistol, SoundKind.GormHeadHurt], cues.Select(cue => cue.Kind));
    }

    [Fact]
    public void The_bat_warnings_differ_from_the_ghouls_and_from_each_other()
    {
        // Issue #297: spit and dive are announced by other files than the ghoul's attack, and not by the same one.
        var spit = Pack1.Variants(SoundKind.BatSpitWindUp);
        var screech = Pack1.Variants(SoundKind.BatScreech);
        var ghoul = Pack1.Variants(SoundKind.GhoulWindUp);

        Assert.Empty(spit.Intersect(ghoul));
        Assert.Empty(screech.Intersect(ghoul));
        Assert.Empty(spit.Intersect(screech));
    }

    [Fact]
    public void A_blade_hit_on_a_bat_plays_the_bats_own_hit_in_pack_1()
    {
        // Both are short knocks: the bat's own one says what was hit, and the two would smear into one.
        var cues = new SoundCues(1).For([BladeSwing, new BatHit(4, 40), BladeHit with { TargetId = 4 }], Pack1);

        Assert.Equal([SoundKind.BatHurt], cues.Select(cue => cue.Kind));
    }

    [Fact]
    public void A_blade_hit_on_a_bat_plays_the_blade_in_the_old_pack()
    {
        // The old pack has no sound for the bat's hit: the blade's hit plays as before issue #297.
        var cues = new SoundCues(1).For([BladeSwing, new BatHit(4, 40), BladeHit with { TargetId = 4 }], SoundPack.Old);

        Assert.Equal([SoundKind.Blade], cues.Select(cue => cue.Kind));
    }

    [Fact]
    public void A_pistol_hit_on_a_bat_plays_the_shot_and_the_bats_hit()
    {
        var cues = new SoundCues(1).For(
            [new ShotFired(0, Vector3.Zero, Vector3.UnitZ), new BatHit(4, 10), new TargetHitBy(4, 0, Weapon.Pistol)], Pack1);

        Assert.Equal([SoundKind.Pistol, SoundKind.BatHurt], cues.Select(cue => cue.Kind));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(5f)]
    [InlineData(SoundBoard.BatFlapHearingDistance - 0.01f)]
    public void A_wing_beat_near_the_listener_is_heard(float distance)
    {
        var listener = new Vector3(3f, 1.6f, -2f);
        var flap = new BatFlapped(4, listener + Vector3.Normalize(new Vector3(1f, 0.5f, 2f)) * distance);

        var cue = Assert.Single(new SoundCues(1).For([flap], Pack1, listener));
        Assert.Equal(SoundKind.BatFlap, cue.Kind);
    }

    [Theory]
    [InlineData(SoundBoard.BatFlapHearingDistance + 0.01f)]
    [InlineData(15f)]
    public void A_wing_beat_far_from_the_listener_is_not(float distance)
    {
        var listener = new Vector3(3f, 1.6f, -2f);
        var flap = new BatFlapped(4, listener + new Vector3(0f, 0f, distance));

        Assert.Empty(new SoundCues(1).For([flap], Pack1, listener));
    }

    [Fact]
    public void The_hearing_distance_covers_a_bat_circling_a_player()
    {
        Assert.True(SoundBoard.BatFlapHearingDistance > Balance.Default.Bat.CircleRadiusMax);
        Assert.True(SoundBoard.BatFlapHearingDistance < Balance.Default.Bat.SightRange);
    }

    [Fact]
    public void Only_the_wing_beat_has_a_range()
    {
        // Warnings must always come through, wherever the bat is.
        var far = new Vector3(100f, 0f, 0f);
        GameEvent[] events = [new BatScreeched(4), new BatSpitWindUp(5), new BatDied(6), new PlayerStepped(0, Foot.Left)];

        Assert.All(events, gameEvent => Assert.True(SoundBoard.IsInEarshot(gameEvent, far)));
        Assert.True(SoundBoard.IsInEarshot(new BatFlapped(4, far), null));
    }

    [Fact]
    public void Wing_beats_of_several_bats_are_thinned_out_to_the_minimum_gap()
    {
        var cues = new SoundCues(1);
        var gap = SimulationClock.SecondsToTicks(SoundBoard.BatFlapMinimumGap);
        var heard = new List<int>();

        // Three bats taking turns, one beat every third tick: never two heard closer than the gap.
        for (var tick = 0; tick < 4 * SimulationClock.TicksPerSecond; tick++)
        {
            GameEvent[] events = tick % 3 == 0 ? [new BatFlapped(4 + tick % 9 / 3, Vector3.Zero)] : [];
            if (cues.For(events, Pack1).Any(cue => cue.Kind == SoundKind.BatFlap))
            {
                heard.Add(tick);
            }
        }

        Assert.Equal(0, heard[0]);
        Assert.All(heard.Zip(heard.Skip(1)), pair => Assert.InRange(pair.Second - pair.First, gap, gap + 2));
    }

    [Fact]
    public void One_bat_is_heard_on_every_wing_beat()
    {
        Assert.True(Bat.FlapInterval >= SoundBoard.BatFlapMinimumGap);

        var cues = new SoundCues(1);
        var interval = SimulationClock.SecondsToTicks(Bat.FlapInterval);
        var heard = 0;
        for (var tick = 0; tick < 10 * interval; tick++)
        {
            GameEvent[] events = tick % interval == 0 ? [new BatFlapped(4, Vector3.Zero)] : [];
            heard += cues.For(events, Pack1).Count;
        }

        Assert.Equal(10, heard);
    }

    [Fact]
    public void The_minimum_gap_holds_back_only_the_wing_beat()
    {
        var cues = new SoundCues(1);
        cues.For([new BatFlapped(4, Vector3.Zero)], Pack1);

        var cue = Assert.Single(cues.For([new BatFlapped(5, Vector3.Zero), new BatScreeched(5)], Pack1));
        Assert.Equal(SoundKind.BatScreech, cue.Kind);
        Assert.Single(cues.For([new BatScreeched(5)], Pack1));
    }

    [Fact]
    public void The_wing_beats_take_turns()
    {
        var cues = new SoundCues(3);
        var interval = SimulationClock.SecondsToTicks(Bat.FlapInterval);
        var files = new List<string>();
        for (var tick = 0; tick < 20 * interval; tick++)
        {
            GameEvent[] events = tick % interval == 0 ? [new BatFlapped(4, Vector3.Zero)] : [];
            files.AddRange(cues.For(events, Pack1).Select(cue => cue.File));
        }

        Assert.Equal(20, files.Count);
        Assert.All(files.Zip(files.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second));
    }

    [Fact]
    public void A_swing_without_a_hit_plays_the_swing()
    {
        var cue = Assert.Single(new SoundCues(1).For([BladeSwing], Pack1));

        Assert.Equal(SoundKind.BladeSwing, cue.Kind);
    }

    [Fact]
    public void A_swing_that_hits_in_the_same_tick_plays_only_the_hit()
    {
        // Core resolves the swing in its own tick: swing and hit come together, and both sounds would smear into one.
        var cues = new SoundCues(1).For([BladeSwing, new GhoulHit(3, 40), BladeHit with { TargetId = 3 }], Pack1);

        Assert.Equal([SoundKind.Blade], cues.Select(cue => cue.Kind));
    }

    [Fact]
    public void A_swing_with_the_club_plays_no_swing()
    {
        Assert.Empty(new SoundCues(1).For([BladeSwing with { Weapon = WeaponKind.Club }], Pack1));
    }

    [Fact]
    public void A_left_and_a_right_step_draw_from_their_own_files()
    {
        var cues = new SoundCues(5);
        for (var i = 0; i < 100; i++)
        {
            var foot = i % 2 == 0 ? Foot.Left : Foot.Right;
            var cue = Assert.Single(cues.For([new PlayerStepped(0, foot)], Pack1));
            Assert.Contains(cue.File, Pack1.Variants(foot == Foot.Left ? SoundKind.StepLeft : SoundKind.StepRight));
        }
    }

    [Theory]
    [InlineData(Foot.Left)]
    [InlineData(Foot.Right)]
    public void Steps_never_repeat_their_last_file_and_use_all_of_them(Foot foot)
    {
        var cues = new SoundCues(11);
        var step = new PlayerStepped(0, foot);

        var files = Enumerable.Range(0, 300).Select(_ => Assert.Single(cues.For([step], Pack1)).File).ToList();

        Assert.All(files.Zip(files.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second));
        Assert.Equal(Pack1.Variants(foot == Foot.Left ? SoundKind.StepLeft : SoundKind.StepRight).Order(), files.Distinct().Order());
    }

    [Fact]
    public void The_same_seed_picks_the_same_steps()
    {
        static List<string> Walk(int seed)
        {
            var cues = new SoundCues(seed);
            return [.. Enumerable.Range(0, 60).Select(i =>
                Assert.Single(cues.For([new PlayerStepped(0, i % 2 == 0 ? Foot.Left : Foot.Right)], Pack1)).File)];
        }

        Assert.Equal(Walk(42), Walk(42));
        Assert.NotEqual(Walk(42), Walk(43));
    }

    [Fact]
    public void Every_file_of_every_pack_is_loaded_and_checked_in()
    {
        Assert.All(SoundPack.All, pack => Assert.All(pack.Files, file => Assert.Contains(file, SoundBoard.SoundFiles)));
        Assert.All(SoundBoard.SoundFiles, file => Assert.Contains(file, SoundBoard.AllFiles));
    }

    [Fact]
    public void No_file_of_pack_1_waits_for_its_event_any_more()
    {
        // Since #317 the last waiting file, the equipment pickup, has its event: it is played now and still checked in.
        Assert.Empty(SoundPack.Pack1WithoutEvent);
        Assert.Contains("p1-aufheben-ausruestung.wav", SoundBoard.SoundFiles);
        Assert.Contains("p1-aufheben-ausruestung.wav", SoundBoard.AllFiles);
    }

    [Fact]
    public void The_game_starts_with_pack_1()
    {
        Assert.Same(SoundPack.Pack1, SoundPack.Default);
        Assert.Same(SoundPack.Default, new SoundPackSwitch().Current);
    }

    [Fact]
    public void After_the_last_pack_comes_the_first_again()
    {
        Assert.Same(SoundPack.Old, SoundPack.After(SoundPack.Pack1));
        Assert.Same(SoundPack.Pack1, SoundPack.After(SoundPack.Old));
    }

    [Fact]
    public void The_key_switches_the_pack_and_shows_its_name_for_a_while()
    {
        var packs = new SoundPackSwitch();
        Assert.Equal(0f, packs.NoticeSecondsLeft);

        packs.Toggle();
        Assert.Same(SoundPack.Old, packs.Current);
        Assert.Equal(SoundPackSwitch.NoticeSeconds, packs.NoticeSecondsLeft);
        Assert.Contains(SoundPack.Old.Name, packs.Notice);

        packs.Update(SoundPackSwitch.NoticeSeconds / 2f);
        Assert.Equal(SoundPackSwitch.NoticeSeconds / 2f, packs.NoticeSecondsLeft, 0.001f);
        packs.Update(SoundPackSwitch.NoticeSeconds);
        Assert.Equal(0f, packs.NoticeSecondsLeft);

        packs.Toggle();
        Assert.Same(SoundPack.Pack1, packs.Current);
        Assert.Contains(SoundPack.Pack1.Name, packs.Notice);
    }

    [Fact]
    public void Switching_the_pack_keeps_the_choice_of_variants_going()
    {
        // The pack only names the files: the same cues object serves both, so a switch needs no new one.
        var cues = new SoundCues(1);
        Assert.Contains(Assert.Single(cues.For([BladeHit], Pack1)).File, Pack1.Variants(SoundKind.Blade));
        Assert.Contains(Assert.Single(cues.For([BladeHit], SoundPack.Old)).File, SoundPack.Old.Variants(SoundKind.Blade));
    }
}
