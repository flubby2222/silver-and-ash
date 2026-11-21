using System.Numerics;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

public class GratePoseTests
{
    private const float Tolerance = 0.001f;

    // The heights the game draws, tick for tick.
    private static readonly GratesBalance Timing = Balance.Default.Grates;

    private static float Fall(int ticks) => GratePose.Height(ticks, GrateMotion.Fall, Timing);

    private static float Lift(int ticks) => GratePose.Height(ticks, GrateMotion.Lift, Timing);

    [Fact]
    public void The_shipped_lengths_are_the_judged_ones()
    {
        // The shapes were judged at 63 and 126 ticks (issue #540, fall "b - two jerks"); the table below is theirs.
        Assert.Equal(63, Timing.FallTicks);
        Assert.Equal(126, Timing.LiftTicks);
    }

    [Theory]
    [InlineData(0, 3.400f)]
    [InlineData(12, 2.550f)]
    [InlineData(13, 2.522f)]
    [InlineData(14, 2.520f)]
    [InlineData(17, 2.560f)]
    [InlineData(18, 2.561f)]
    [InlineData(24, 2.550f)]
    [InlineData(26, 2.550f)]
    [InlineData(37, 1.850f)]
    [InlineData(38, 1.717f)]
    [InlineData(46, 0.237f)]
    [InlineData(47, 0.000f)]
    [InlineData(52, 0.050f)]
    [InlineData(58, 0.000f)]
    [InlineData(60, 0.009f)]
    [InlineData(63, 0.000f)]
    public void The_fall_takes_the_judged_heights(int ticks, float height)
    {
        Assert.Equal(height, Fall(ticks), Tolerance);
    }

    [Fact]
    public void The_first_jerk_only_goes_down()
    {
        for (var t = 1; t <= 12; t++)
        {
            Assert.True(Fall(t) < Fall(t - 1), $"tick {t}: {Fall(t)} not below {Fall(t - 1)}");
        }
    }

    [Fact]
    public void After_the_first_jerk_the_grate_wobbles_once_down_and_up_and_settles_on_its_hang()
    {
        // Down to its lowest at tick 14, up past the hang to its highest at 18, back to the hang by 24 and still till 26.
        var hang = Fall(12);
        for (var t = 13; t <= 14; t++)
        {
            Assert.True(Fall(t) < Fall(t - 1), $"tick {t}");
        }

        for (var t = 15; t <= 18; t++)
        {
            Assert.True(Fall(t) > Fall(t - 1), $"tick {t}");
        }

        for (var t = 19; t <= 22; t++)
        {
            Assert.True(Fall(t) < Fall(t - 1), $"tick {t}");
        }

        Assert.True(Fall(17) > hang && Fall(14) < hang);
        for (var t = 24; t <= 26; t++)
        {
            Assert.Equal(hang, Fall(t), Tolerance);
        }
    }

    [Fact]
    public void The_second_jerk_drops_ever_faster_to_the_floor()
    {
        for (var t = 27; t <= 47; t++)
        {
            Assert.True(Fall(t) < Fall(t - 1), $"tick {t}: {Fall(t)} not below {Fall(t - 1)}");
        }

        for (var t = 28; t <= 46; t++)
        {
            Assert.True(Fall(t - 1) - Fall(t) > Fall(t - 2) - Fall(t - 1), $"tick {t} not faster");
        }
    }

    [Fact]
    public void The_grate_jerks_twice_with_a_hang_between()
    {
        // The two jerks are the two stretches with a drop of more than 0.04 m per tick; between them it hangs.
        var jerks = 0;
        var inJerk = false;
        for (var t = 1; t <= Timing.FallTicks; t++)
        {
            var fast = Fall(t - 1) - Fall(t) > 0.04f;
            if (fast && !inJerk)
            {
                jerks++;
            }

            inJerk = fast;
        }

        Assert.Equal(2, jerks);
    }

    [Fact]
    public void From_tick_47_the_grate_is_shut_and_bounces_only_a_hand_s_breadth()
    {
        Assert.Equal(47, GratePose.ShutTick(Timing));
        Assert.True(Fall(46) > 0.1f);
        for (var t = GratePose.ShutTick(Timing); t <= Timing.FallTicks + 10; t++)
        {
            Assert.InRange(Fall(t), 0f, 0.05f + Tolerance);
        }
    }

    [Fact]
    public void The_two_bounces_rise_and_fall_once_each_the_second_smaller()
    {
        // The first bounce peaks between ticks 52 and 53, which stand equally high.
        for (var t = 48; t <= 52; t++)
        {
            Assert.True(Fall(t) > Fall(t - 1), $"tick {t}");
        }

        Assert.Equal(Fall(52), Fall(53), Tolerance);
        for (var t = 54; t <= 58; t++)
        {
            Assert.True(Fall(t) < Fall(t - 1), $"tick {t}");
        }

        Assert.True(Fall(60) > Fall(58) && Fall(60) > Fall(63));
        Assert.True(Fall(60) < Fall(52) / 2f);
    }

    [Fact]
    public void Before_and_after_the_fall_the_grate_is_open_and_shut()
    {
        Assert.Equal(GratePose.OpenHeight, Fall(-5));
        Assert.Equal(GratePose.OpenHeight, Fall(0));
        Assert.Equal(0f, Fall(Timing.FallTicks));
        Assert.Equal(0f, Fall(Timing.FallTicks + 100));
    }

    [Theory]
    [InlineData(6, 0.000f)]
    [InlineData(12, 0.045f)]
    [InlineData(18, 0.179f)]
    [InlineData(126, 3.400f)]
    public void The_lift_takes_the_judged_heights(int ticks, float height)
    {
        Assert.Equal(height, Lift(ticks), Tolerance);
    }

    [Fact]
    public void The_lift_waits_then_rises_evenly_to_fully_open_at_its_last_tick()
    {
        for (var t = 0; t <= 6; t++)
        {
            Assert.Equal(0f, Lift(t));
        }

        for (var t = 7; t <= Timing.LiftTicks; t++)
        {
            Assert.True(Lift(t) > Lift(t - 1), $"tick {t}: {Lift(t)} not above {Lift(t - 1)}");
        }

        // After the ramp every tick lifts by the same step.
        var step = Lift(30) - Lift(29);
        for (var t = 31; t <= Timing.LiftTicks; t++)
        {
            Assert.Equal(step, Lift(t) - Lift(t - 1), Tolerance);
        }

        Assert.Equal(GratePose.OpenHeight, Lift(Timing.LiftTicks));
        Assert.Equal(GratePose.OpenHeight, Lift(Timing.LiftTicks + 50));
        Assert.Equal(0f, Lift(-3));
    }

    [Fact]
    public void The_lift_clears_a_figure_s_head_at_tick_75_and_the_arch_at_tick_124()
    {
        // 1.85 m (the passage of part 2) and the apex of the arch; the lift is the one of the probe.
        Assert.Equal(75, FirstLiftTickAtLeast(1.85f));
        Assert.Equal(124, FirstLiftTickAtLeast(3.32f));
    }

    [Fact]
    public void Other_lengths_stretch_the_judged_shapes_evenly()
    {
        var twice = new GratesBalance { FallTicks = 126, LiftTicks = 252 };
        Assert.Equal(94, GratePose.ShutTick(twice));
        Assert.Equal(Fall(12), GratePose.Height(24, GrateMotion.Fall, twice), Tolerance);
        Assert.Equal(Fall(37), GratePose.Height(74, GrateMotion.Fall, twice), Tolerance);
        Assert.Equal(0f, GratePose.Height(94, GrateMotion.Fall, twice), Tolerance);
        Assert.Equal(Lift(18), GratePose.Height(36, GrateMotion.Lift, twice), Tolerance);
        Assert.Equal(GratePose.OpenHeight, GratePose.Height(252, GrateMotion.Lift, twice));
    }

    [Fact]
    public void Only_the_two_motions_have_heights()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GratePose.Height(5, (GrateMotion)7, Timing));
    }

    [Fact]
    public void Fully_open_the_grate_is_not_drawn()
    {
        Assert.False(GratePose.IsDrawn(GratePose.OpenHeight));
        Assert.False(GratePose.IsDrawn(Fall(0)));
        Assert.True(GratePose.IsDrawn(Fall(1)));
        Assert.True(GratePose.IsDrawn(Lift(Timing.LiftTicks - 1)));
        Assert.True(GratePose.IsDrawn(0f));
    }

    [Theory]
    [InlineData(TextureVariant.A, "portcullis_guide_a.glb")]
    [InlineData(TextureVariant.B, "portcullis_guide_b.glb")]
    [InlineData(TextureVariant.C, "portcullis_guide_c.glb")]
    public void Every_wall_variant_has_its_own_guide_ring(TextureVariant variant, string file)
    {
        Assert.Equal(file, GratePose.GuideFileName(variant));
    }

    [Fact]
    public void Only_the_three_wall_variants_have_guide_rings()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GratePose.GuideFileName((TextureVariant)7));
    }

    [Theory]
    [InlineData(TextureVariant.B, true, true, TextureVariant.B)]
    [InlineData(TextureVariant.C, false, true, TextureVariant.A)]
    [InlineData(TextureVariant.C, false, false, null)]
    public void A_missing_guide_ring_falls_back_to_variant_a_then_to_none(
        TextureVariant wanted, bool wantedLoaded, bool variantALoaded, TextureVariant? expected)
    {
        Assert.Equal(expected, GratePose.Pick(wanted, wantedLoaded, variantALoaded));
    }

    [Fact]
    public void The_grate_file_stands_on_its_spike_tips_in_the_door_plane()
    {
        var parts = ReadParts(GratePose.FileName);
        Assert.Equal(GratePose.Parts.Select(part => part.Name), parts.Keys);
        var grate = parts[GratePose.Grate];
        Assert.Equal(Vector3.Zero, grate.Pivot);

        // Spike tips on the floor, centred on the door plane; when fully open the top rail stands far above the lintel.
        Assert.Equal(0f, grate.PositionMin.Y, Tolerance);
        Assert.Equal(-grate.PositionMax.X, grate.PositionMin.X, Tolerance);
        Assert.Equal(-grate.PositionMax.Z, grate.PositionMin.Z, Tolerance);
        Assert.True(grate.PositionMax.Y > Fall(47) + 3f);
    }

    [Theory]
    [InlineData(TextureVariant.A)]
    [InlineData(TextureVariant.B)]
    [InlineData(TextureVariant.C)]
    public void Every_guide_ring_stands_still_in_the_door_plane_up_to_the_arch(TextureVariant variant)
    {
        var parts = ReadParts(GratePose.GuideFileName(variant));
        Assert.Equal(GratePose.GuideRigParts.Select(part => part.Name), parts.Keys);
        var guide = parts[GratePose.Guide];
        Assert.Equal(Vector3.Zero, guide.Pivot);
        Assert.Equal(-guide.PositionMax.X, guide.PositionMin.X, Tolerance);
        Assert.True(guide.PositionMin.Y >= 0f);
    }

    [Fact]
    public void The_guide_rings_were_built_on_the_wall_textures_in_the_game()
    {
        // tools/models/portcullis.py builds the rings on the wall textures and notes which ones it read. Once a wall
        // texture changes, the ring no longer matches the frame it sits in: run the script again.
        var built = Lines(RepositoryFiles.Find("tools/models/portcullis_sources.sha256"));
        var current = Lines(RepositoryFiles.Find("assets/textures/textures.sha256"));

        Assert.Equal(3, built.Count);
        foreach (var line in built)
        {
            Assert.True(
                current.Contains(line),
                $"The portcullis guide rings were built on '{line}', which is no longer in assets/textures. " +
                "Run tools/models/portcullis.py again (see assets/HERKUNFT.md).");
        }
    }

    private static int FirstLiftTickAtLeast(float height)
    {
        var t = 0;
        while (Lift(t) < height)
        {
            t++;
        }

        return t;
    }

    private static Dictionary<string, GlbPart> ReadParts(string file)
    {
        var path = RepositoryFiles.Find($"assets/models/{file}");
        Assert.True(File.Exists(path), $"{path} is missing.");
        return GlbScene.Read(File.ReadAllBytes(path)).ToDictionary(part => part.Name);
    }

    private static List<string> Lines(string path) => File.ReadAllLines(path)
        .Select(line => line.Trim())
        .Where(line => line.Length > 0 && !line.StartsWith('#'))
        .ToList();
}
