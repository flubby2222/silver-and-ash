using System.Numerics;
using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Audio;

/// <summary>
/// Which sound the events of a tick ask for (issue #267) - with the old pack, as before issue #290. The new pack and
/// the switch between them are in <see cref="SoundPackTests"/>.
/// </summary>
public class SoundCuesTests
{
    private static readonly ShotFired PistolShot = new(Vector3.Zero, Vector3.UnitZ);
    private static readonly TargetHitBy BladeHit = new(0, 0, Weapon.Blade);
    private static readonly SoundPack Old = SoundPack.Old;

    [Fact]
    public void A_pistol_shot_plays_the_pistol()
    {
        var cue = Assert.Single(new SoundCues(1).For([PistolShot], Old));

        Assert.Equal(new SoundCue(SoundKind.Pistol, "pistole.wav", SoundBoard.SoundVolume), cue);
    }

    [Fact]
    public void Another_ranged_weapon_does_not_play_the_pistol()
    {
        Assert.Empty(new SoundCues(1).For([PistolShot with { Weapon = WeaponKind.Rifle }], Old));
    }

    [Fact]
    public void A_blade_hit_plays_one_of_the_blade_sounds()
    {
        var cue = Assert.Single(new SoundCues(1).For([new GhoulHit(3, 40), BladeHit with { TargetId = 3 }], Old));

        Assert.Equal(SoundKind.Blade, cue.Kind);
        Assert.Contains(cue.File, Old.Variants(SoundKind.Blade));
    }

    [Theory]
    [InlineData(Weapon.Pistol)]
    [InlineData(Weapon.Club)]
    [InlineData(Weapon.Rifle)]
    public void A_hit_with_another_weapon_plays_no_blade(Weapon weapon)
    {
        Assert.DoesNotContain(new SoundCues(1).For([BladeHit with { Weapon = weapon }], Old), cue => cue.Kind == SoundKind.Blade);
    }

    [Fact]
    public void A_swing_without_a_hit_plays_no_blade()
    {
        Assert.Empty(new SoundCues(1).For([new MeleeSwung(Vector3.Zero, Vector3.UnitZ)], Old));
    }

    [Fact]
    public void A_step_plays_one_of_the_step_sounds()
    {
        var cue = Assert.Single(new SoundCues(1).For([new PlayerStepped(0, Foot.Left)], Old));

        Assert.Equal(SoundKind.StepLeft, cue.Kind);
        Assert.Contains(cue.File, Old.Variants(SoundKind.StepLeft));
    }

    [Fact]
    public void A_ghoul_winding_up_plays_its_warning()
    {
        var cue = Assert.Single(new SoundCues(1).For([new GhoulWindUp(2)], Old));

        Assert.Equal(new SoundCue(SoundKind.GhoulWindUp, "ghul-ankuendigung.wav", SoundBoard.SoundVolume), cue);
    }

    [Fact]
    public void The_old_ambience_stays_checked_in_but_is_no_longer_played()
    {
        // Issue #271: the catacomb music plays instead; the file goes with the new sounds.
        Assert.Equal("ambiente-katakomben.ogg", SoundBoard.Ambience);
        Assert.Contains(SoundBoard.Ambience, SoundBoard.AllFiles);
        Assert.DoesNotContain(SoundBoard.Ambience, SoundBoard.SoundFiles);
        Assert.All(Enum.GetValues<MusicTrack>(), track => Assert.NotEqual(SoundBoard.Ambience, SoundBoard.MusicFile(track)));
    }

    [Fact]
    public void Events_without_a_sound_play_nothing()
    {
        Assert.Empty(new SoundCues(1).For([new DummyHit(0, 10), new ReloadStarted(0), new PlayerHit(0, 5)], Old));
    }

    [Fact]
    public void Each_kind_plays_once_per_tick_in_the_order_it_first_appears()
    {
        // The old pack has one set of steps for both feet: a left and a right step in one tick are one sound.
        var cues = new SoundCues(1).For(
            [new PlayerStepped(0, Foot.Left), BladeHit, BladeHit with { TargetId = 1 }, PistolShot, new PlayerStepped(0, Foot.Right)], Old);

        Assert.Equal([SoundKind.StepLeft, SoundKind.Blade, SoundKind.Pistol], cues.Select(cue => cue.Kind));
    }

    [Theory]
    [InlineData(SoundKind.Blade)]
    [InlineData(SoundKind.StepLeft)]
    public void A_variant_never_plays_twice_in_a_row_and_every_one_comes_up(SoundKind kind)
    {
        var gameEvent = kind == SoundKind.Blade ? (GameEvent)BladeHit : new PlayerStepped(0, Foot.Left);
        var cues = new SoundCues(7);

        var files = Enumerable.Range(0, 400).Select(_ => Assert.Single(cues.For([gameEvent], Old)).File).ToList();

        Assert.All(files.Zip(files.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second));
        Assert.Equal(Old.Variants(kind).Order(), files.Distinct().Order());
    }

    [Fact]
    public void Kinds_keep_their_own_last_variant()
    {
        // A step in between does not let the blade repeat its last file.
        var cues = new SoundCues(3);
        var blades = new List<string>();
        for (var i = 0; i < 200; i++)
        {
            blades.Add(Assert.Single(cues.For([BladeHit], Old)).File);
            cues.For([new PlayerStepped(0, Foot.Left)], Old);
        }

        Assert.All(blades.Zip(blades.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second));
    }

    [Fact]
    public void The_same_seed_picks_the_same_variants()
    {
        Assert.Equal(Picks(new SoundCues(42)), Picks(new SoundCues(42)));
        Assert.NotEqual(Picks(new SoundCues(42)), Picks(new SoundCues(43)));
    }

    [Fact]
    public void Picking_sounds_leaves_the_world_and_its_random_numbers_alone()
    {
        // Same seed, same inputs: with and without picking sounds the run stays the same (online co-op rule 3).
        var level = SilverAndAsh.Core.World.LevelParser.Parse("P\n.\n.\nG\n.");
        var quiet = new GameWorld(level, 5, Balance.Default);
        var loud = new GameWorld(level, 5, Balance.Default);
        var cues = new SoundCues(loud.Seed);
        var input = SilverAndAsh.Core.Input.PlayerInput.None with { Move = new Vector2(0f, 1f), Shoot = true };

        for (var tick = 0; tick < 4 * SimulationClock.TicksPerSecond; tick++)
        {
            quiet.Tick(input);
            loud.Tick(input);
            cues.For(loud.Events, SoundPack.Default);
            Assert.Equal(quiet.Events, loud.Events);
        }

        Assert.Equal(quiet.Player.Position, loud.Player.Position);
    }

    [Fact]
    public void Every_file_of_the_board_is_checked_in_and_matches_the_checksum_list()
    {
        // A sound renewed without renewing the list (tools/models/checksums.py) would stay silent in the game.
        var folder = RepositoryFiles.Find("assets/audio");
        var list = ModelChecksums.Parse(File.ReadAllText(Path.Combine(folder, SoundBoard.ChecksumFileName)));
        var files = Directory.GetFiles(folder)
            .Where(path => path.EndsWith(".wav", StringComparison.Ordinal) || path.EndsWith(".ogg", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(SoundBoard.AllFiles.Order(), files.Select(Path.GetFileName).Order());
        Assert.Equal(SoundBoard.AllFiles.Order(), list.Names.Order());
        Assert.All(files, path => Assert.Null(list.Check(path)));
    }

    private static List<string> Picks(SoundCues cues) =>
        [.. Enumerable.Range(0, 50).Select(i => Assert.Single(cues.For([i % 2 == 0 ? BladeHit : new PlayerStepped(0, Foot.Left)], Old)).File)];
}
