using System.Buffers.Binary;
using System.Numerics;
using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Audio;

/// <summary>
/// The sounds of the stake crossbow (issue #793): which event plays which of its four files, that the reload's one sound
/// ends with the weapon ready, and what the files hold (length of the reload, three hits of 40 ms without clipping).
/// </summary>
public class StakeCrossbowSoundTests
{
    private static readonly SoundPack Pack1 = SoundPack.Pack1;

    private static readonly (GameEvent Event, SoundKind? Kind)[] Table =
    [
        (new ShotFired(0, Vector3.Zero, Vector3.UnitZ, false, WeaponKind.StakeCrossbow), SoundKind.CrossbowShot),
        (new ShotFired(0, Vector3.Zero, Vector3.UnitZ), SoundKind.Pistol),
        (new TargetHitBy(1, 0, Weapon.StakeCrossbow), SoundKind.CrossbowHit),
        (new TargetHitBy(1, 0, Weapon.StakeCrossbow) { Hard = true }, SoundKind.CrossbowHardHit),
        (new BulletImpact(Vector3.Zero, Vector3.UnitZ, WeaponKind.StakeCrossbow), SoundKind.CrossbowHardHit),
        (new BulletImpact(Vector3.Zero, Vector3.UnitZ), null),
        (new ReloadStarted(0, WeaponKind.StakeCrossbow), SoundKind.CrossbowReload),
        (new ReloadStarted(0), SoundKind.ReloadStarted),
        (new ReloadFinished(0, WeaponKind.StakeCrossbow), null),
        (new ReloadFinished(0), SoundKind.ReloadFinished),
    ];

    [Fact]
    public void Every_event_of_the_crossbow_plays_its_sound_and_the_pistols_stay_as_they_were()
    {
        Assert.All(Table, row => Assert.Equal(row.Kind, SoundBoard.KindFor(row.Event)));
    }

    [Fact]
    public void The_crossbow_sounds_come_from_their_own_files()
    {
        var kinds = new[] { SoundKind.CrossbowShot, SoundKind.CrossbowHit, SoundKind.CrossbowHardHit, SoundKind.CrossbowReload };
        var files = kinds.SelectMany(kind => Pack1.Variants(kind)).ToList();

        Assert.All(kinds, kind => Assert.NotEmpty(Pack1.Variants(kind)));
        Assert.Equal(files.Count, files.Distinct().Count());
        Assert.All(files, file => Assert.StartsWith("armbrust-", file));
    }

    [Fact]
    public void The_reload_file_is_as_long_as_the_reload_of_balance_json()
    {
        var (seconds, _) = Read(Assert.Single(Pack1.Variants(SoundKind.CrossbowReload)));

        Assert.Equal(Balance.Default.StakeCrossbow.ReloadTime, seconds, 0.05);
    }

    [Fact]
    public void Three_hits_of_40_ms_each_fit_without_clipping()
    {
        // Issue #793: a stake through three creatures plays its hit three times in a row, 40 ms apart.
        var (_, samples) = Read(Assert.Single(Pack1.Variants(SoundKind.CrossbowHit)));
        var mix = new double[samples.Length + (2 * 1764)];
        for (var voice = 0; voice < 3; voice++)
        {
            for (var i = 0; i < samples.Length; i++)
            {
                mix[(voice * 1764) + i] += samples[i];
            }
        }

        Assert.True(mix.Max(Math.Abs) <= short.MaxValue, "Three hits at 40 ms clip.");
    }

    // The seconds and the 16-bit samples of a checked-in WAV (mono, PCM; the data chunk is searched).
    private static (double Seconds, short[] Samples) Read(string file)
    {
        var bytes = File.ReadAllBytes(RepositoryFiles.Find(Path.Combine("assets", "audio", file)));
        var rate = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24));
        var at = 12;
        while (System.Text.Encoding.ASCII.GetString(bytes, at, 4) != "data")
        {
            at += 8 + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(at + 4));
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(at + 4)) / 2;
        var samples = new short[length];
        for (var i = 0; i < length; i++)
        {
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(at + 8 + (2 * i)));
        }

        return ((double)length / rate, samples);
    }
}
