using System.Numerics;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// The whip in the hand (issue #786): its rig, when the strike file is shown, how the lash rolls out and how far it
/// reaches, the whirl while charging - checked on the checked-in files, frame by frame as the game draws it.
/// </summary>
public class WhipPoseTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void The_whip_is_a_handle_with_a_chain_of_fourteen_links()
    {
        var parts = WhipPose.Parts;

        Assert.Equal(1 + WhipPose.SegmentCount, parts.Count);
        Assert.Equal(new RigPart(WhipPose.Handle, null), parts[0]);
        Assert.Equal(new RigPart("seg01", WhipPose.Handle), parts[1]);
        for (var link = 2; link <= WhipPose.SegmentCount; link++)
        {
            Assert.Equal(new RigPart(WhipPose.Segment(link), WhipPose.Segment(link - 1)), parts[link]);
        }
    }

    [Theory]
    [InlineData("whip_rest.glb")]
    [InlineData("whip_strike.glb")]
    public void Both_whip_files_have_the_parts_of_the_rig(string file)
    {
        var scene = Scene(file);

        Assert.Equal(WhipPose.Parts.Select(part => part.Name), scene.Select(part => part.Name));
    }

    [Fact]
    public void A_swing_shows_the_strike_file_from_half_way_out_until_the_way_back_is_half_done()
    {
        // At 60 frames a second through the whip's swing time (0.9 of its cooldown from balance.json).
        var seconds = WeaponPose.SwingSeconds(WeaponKind.Whip, Balance.Default.Whip.Cooldown);
        var frames = (int)MathF.Ceiling(seconds * 60f);
        var shown = Enumerable.Range(0, frames + 1)
            .Select(frame => MathF.Min(1f, frame / 60f / seconds))
            .Select(progress => (Progress: progress, Shown: WhipPose.ShowsStrike(progress, WeaponPose.SwingCurve(progress))))
            .ToList();

        Assert.False(shown[0].Shown, "The swing's first frame still shows the rest file.");
        Assert.False(shown[^1].Shown, "A finished swing (progress 1) shows the rest file again.");

        // Shown in one piece: out within the strike (its first quarter), still out after the hold (the hit), gone
        // before the swing ends.
        var first = shown.FindIndex(step => step.Shown);
        var last = shown.FindLastIndex(step => step.Shown);
        Assert.InRange(first, 1, frames);
        Assert.All(shown.Skip(first).Take(last - first + 1), step => Assert.True(step.Shown));
        Assert.True(shown[first].Progress < WeaponPose.StrikeEnd, $"Out only at progress {shown[first].Progress:0.000}.");
        Assert.True(shown[last].Progress > WeaponPose.HoldEnd);
        Assert.True(last < frames - 1, "The rest file should take over before the swing ends.");
    }

    [Fact]
    public void At_the_hit_the_lash_lies_straight_and_only_its_first_link_carries_the_shortening_and_the_aim()
    {
        var turns = new Matrix4x4[WhipPose.Parts.Count];
        WhipPose.LashTurns(WeaponPose.HoldEnd, turns);

        Assert.Equal(Matrix4x4.Identity, turns[0]);
        AssertNear(Matrix4x4.CreateScale(WhipPose.LashScale) * WhipPose.Aim, turns[1]);
        for (var link = 2; link < turns.Length; link++)
        {
            AssertNear(Matrix4x4.Identity, turns[link]);
        }
    }

    [Fact]
    public void The_lash_rolls_out_from_the_hand_to_the_tip()
    {
        // At the start every joint is curled up the full way; half-way to the hit the joints near the hand are
        // straighter than those near the tip.
        var start = new Matrix4x4[WhipPose.Parts.Count];
        WhipPose.LashTurns(0f, start);
        AssertNear(Matrix4x4.CreateRotationY(Radians(WhipPose.TrailDegrees)) * Rig.Lift(WhipPose.CurlDegrees), start[WhipPose.SegmentCount]);

        var half = new Matrix4x4[WhipPose.Parts.Count];
        WhipPose.LashTurns(WeaponPose.HoldEnd / 2f, half);
        var bends = Enumerable.Range(2, WhipPose.SegmentCount - 1).Select(link => Bend(half[link])).ToList();

        Assert.True(bends[0] < bends[^1] - 5f, $"Near the hand {bends[0]:0.0} degrees, at the tip {bends[^1]:0.0}.");
        Assert.Equal(bends.Order(), bends);
    }

    [Fact]
    public void On_the_way_back_the_lash_sags_towards_the_floor()
    {
        var end = new Matrix4x4[WhipPose.Parts.Count];
        WhipPose.LashTurns(1f, end);

        // Every joint after the first turned down by the sag (a lift by minus the sag).
        AssertNear(Rig.Lift(-WhipPose.SagDegrees), end[WhipPose.SegmentCount]);
        Assert.True(Vector3.TransformNormal(Vector3.UnitZ, end[WhipPose.SegmentCount]).Y < 0f);
    }

    [Fact]
    public void At_the_hit_the_tip_of_the_lash_lies_about_as_far_from_the_eyes_as_the_whip_reaches()
    {
        // Posed as the game draws the lash at the end of the hold: unshrunk, through the bend and the swing pose.
        var range = Balance.Default.Whip.Range;
        var tip = TipDistance(WeaponPose.HoldEnd);

        Assert.InRange(tip, 0.9f * range, 1.1f * range);
    }

    [Fact]
    public void At_the_hit_the_tip_of_the_lash_lies_on_the_axis_of_the_view()
    {
        // The swing holds the handle across the body to the right; the aim turns the lash towards the crosshair, so the
        // whip is seen to hit what it is aimed at (camera space: +z ahead). Posed as the game draws it, unshrunk.
        var tip = TipAt(WeaponPose.HoldEnd);
        var off = MathF.Acos(Vector3.Dot(Vector3.Normalize(tip), Vector3.UnitZ)) * 180f / MathF.PI;

        Assert.True(off < 5f, $"The tip lies {off:0.0} degrees off the view's axis, at {tip}.");
    }

    [Fact]
    public void Everything_drawn_of_the_whip_with_the_weapons_stays_within_reach_of_the_eyes()
    {
        // The rest file whole (also whirling while charging), of the strike file only the handle (its lash is drawn in
        // the world's depth). Through the swing at 60 frames a second and the wind-up.
        var restPivots = Scene("whip_rest.glb").Select(part => part.Pivot).ToArray();
        foreach (var (progress, windUp, name) in Poses())
        {
            var swing = WeaponPose.SwingCurve(progress);
            var pose = WeaponPose.MeleeBend(WeaponKind.Whip, swing, windUp) * WeaponPose.Blade(swing, windUp, 0f);
            if (WhipPose.ShowsStrike(progress, swing))
            {
                WeaponPoseTests.AssertWithinReach("whip_strike.glb", pose, _ => Matrix4x4.Identity, part => part == WhipPose.Handle);
                continue;
            }

            // The loop is shrunk towards the eyes a little more than the handle, as WeaponView draws it.
            var loopPose = pose * Matrix4x4.CreateScale(WhipPose.LoopNearer);
            WeaponPoseTests.AssertWithinReach("whip_rest.glb", pose, _ => Matrix4x4.Identity, part => part == WhipPose.Handle);
            foreach (var whirl in (float[])[0f, 0.25f, 0.5f, 0.75f])
            {
                var failure = Record.Exception(() => WeaponPoseTests.AssertWithinReach(
                    "whip_rest.glb", loopPose, part => WhirlOf(part, whirl, restPivots), part => part != WhipPose.Handle));
                Assert.True(failure is null, $"{name}, whirl {whirl}: {failure?.Message}");
            }
        }
    }

    [Fact]
    public void The_near_plane_cuts_the_whip_open_nowhere_within_the_picture()
    {
        // As for the club: the end of the handle may come closer than the near plane, but only beside the picture.
        var shrink = Matrix4x4.CreateScale(WeaponPose.ShrinkTowardsEye);
        foreach (var file in (string[])["whip_rest.glb", "whip_strike.glb"])
        {
            var parts = WeaponPoseTests.Triangles(file).Where(part => file == "whip_rest.glb" || part.Name == WhipPose.Handle).ToList();
            foreach (var (progress, windUp, name) in Poses())
            {
                var swing = WeaponPose.SwingCurve(progress);
                var frame = WeaponPose.MeleeBend(WeaponKind.Whip, swing, windUp) * WeaponPose.Blade(swing, windUp, 0f) * shrink;
                foreach (var (part, pivot, corners, triangles) in parts)
                {
                    var nearer = part == WhipPose.Handle ? frame : frame * Matrix4x4.CreateScale(WhipPose.LoopNearer);
                    var placed = corners.Select(corner => Vector3.Transform(corner + pivot, nearer)).ToArray();
                    for (var first = 0; first < triangles.Length; first += 3)
                    {
                        var triangle = new[] { placed[triangles[first]], placed[triangles[first + 1]], placed[triangles[first + 2]] };
                        foreach (var (from, to) in triangle.Zip(triangle.Skip(1).Append(triangle[0])))
                        {
                            if ((from.Z - WeaponPose.NearPlane) * (to.Z - WeaponPose.NearPlane) >= 0f)
                            {
                                continue;
                            }

                            var cut = Vector3.Lerp(from, to, (WeaponPose.NearPlane - from.Z) / (to.Z - from.Z));
                            var inside = MathF.Abs(cut.X) <= cut.Z * WeaponPoseTests.HalfWidth && MathF.Abs(cut.Y) <= cut.Z * WeaponPoseTests.HalfHeight;
                            Assert.False(inside, $"{file}, {name}: the near plane cuts the whip open within the picture at {cut}.");
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void At_rest_the_loop_hangs_as_in_the_file_and_whirling_turns_it_around_the_handle()
    {
        var turns = new Matrix4x4[WhipPose.Parts.Count];
        WhipPose.RestTurns(0f, turns);
        Assert.All(turns, turn => AssertNear(Matrix4x4.Identity, turn));

        WhipPose.RestTurns(0.25f, turns);
        AssertNear(Matrix4x4.CreateRotationZ(MathF.PI / 2f), turns[1]);
        Assert.All(turns.Where((_, part) => part != 1), turn => AssertNear(Matrix4x4.Identity, turn));
    }

    [Theory]
    [InlineData(60f)]
    [InlineData(144f)]
    public void Charging_whirls_the_loop_and_after_the_release_it_finishes_the_turn_and_hangs_down_again(float framesPerSecond)
    {
        // As WeaponView does it: the wind-up grows over the charge time from balance.json, the whirl steps every frame.
        var frame = 1f / framesPerSecond;
        var chargeTime = Balance.Default.Whip.ChargeTime;
        var whirl = 0f;
        var travelled = 0f;
        for (var step = 1; step * frame <= 0.6f * chargeTime; step++)
        {
            var next = WhipPose.WhirlStep(whirl, MathF.Min(1f, step * frame / chargeTime), charging: true, frame);
            Assert.InRange(next, 0f, 1f);
            travelled += next >= whirl ? next - whirl : next + 1f - whirl;
            whirl = next;
        }

        // Even the first quarter of the wind-up whirls (at least a quarter of the speed).
        Assert.True(travelled > 0.25f * WhipPose.WhirlTurnsPerSecond * 0.6f * chargeTime * 0.9f, $"Whirled only {travelled:0.00} turns.");
        Assert.True(whirl > 0f);

        // Released: at full speed back to 0, never further than one turn, then it stays there.
        var frames = 0;
        while (whirl > 0f)
        {
            var next = WhipPose.WhirlStep(whirl, 0f, charging: false, frame);
            Assert.True(next == 0f || next > whirl, $"The whirl went back from {whirl} to {next}.");
            whirl = next;
            frames++;
            Assert.True(frames <= framesPerSecond / WhipPose.WhirlTurnsPerSecond + 1f, "The loop never came to rest.");
        }

        Assert.Equal(0f, WhipPose.WhirlStep(0f, 0f, charging: false, frame));
    }

    [Fact]
    public void Turns_for_another_number_of_parts_are_refused()
    {
        var turns = new Matrix4x4[WhipPose.Parts.Count - 1];

        Assert.Throws<ArgumentException>(() => WhipPose.LashTurns(0.5f, turns));
        Assert.Throws<ArgumentException>(() => WhipPose.RestTurns(0.5f, turns));
    }

    [Fact]
    public void The_whip_is_scaled_around_the_grip_and_bends_there_like_the_club()
    {
        Assert.Equal(
            Matrix4x4.CreateScale(WhipPose.Scale) * WeaponPose.ClubBend(0.3f, 0.5f),
            WeaponPose.MeleeBend(WeaponKind.Whip, 0.3f, 0.5f));
    }

    // The farthest corner of the lash's last link from the eyes (camera space), the strike file posed at progress as
    // the game poses the lash: unshrunk.
    private static float TipDistance(float progress) => TipAt(progress).Length();

    // That corner itself.
    private static Vector3 TipAt(float progress)
    {
        var scene = Scene("whip_strike.glb");
        var rig = new Rig(WhipPose.Parts, scene.Select(part => part.Pivot).ToArray());
        var turns = new Matrix4x4[rig.Count];
        var frames = new Matrix4x4[rig.Count];
        WhipPose.LashTurns(progress, turns);
        var swing = WeaponPose.SwingCurve(progress);
        rig.Pose(turns, WeaponPose.MeleeBend(WeaponKind.Whip, swing, 0f) * WeaponPose.Blade(swing, 0f, 0f), frames);
        var tip = WeaponPoseTests.Triangles("whip_strike.glb").Single(part => part.Name == WhipPose.Segment(WhipPose.SegmentCount));
        return tip.Corners.Select(corner => Vector3.Transform(corner, frames[^1])).MaxBy(corner => corner.Length());
    }

    // The whirl as a turn for WeaponPoseTests' placing (each part at its own pivot, no chain): the loop's parts all turn
    // with the first link around its pivot on the handle's axis.
    private static Matrix4x4 WhirlOf(string part, float whirl, Vector3[] pivots)
    {
        var index = WhipPose.Parts.Select(rigPart => rigPart.Name).ToList().IndexOf(part);
        if (index < 1)
        {
            return Matrix4x4.Identity;
        }

        var turn = Matrix4x4.CreateRotationZ(whirl * 2f * MathF.PI);
        return Matrix4x4.CreateTranslation(pivots[index] - pivots[1]) * turn * Matrix4x4.CreateTranslation(pivots[1] - pivots[index]);
    }

    // Through the swing frame by frame at 60 frames a second (its time from the cooldown) and through the wind-up.
    private static IEnumerable<(float Progress, float WindUp, string Name)> Poses()
    {
        var seconds = WeaponPose.SwingSeconds(WeaponKind.Whip, Balance.Default.Whip.Cooldown);
        for (var frame = 0; frame / 60f < seconds; frame++)
        {
            var progress = frame / 60f / seconds;
            yield return (progress, 0f, $"swing, progress {progress:0.000}");
        }

        foreach (var windUp in (float[])[0f, 0.25f, 0.5f, 0.75f, 1f])
        {
            yield return (1f, windUp, $"at rest, wind-up {windUp:0.00}");
        }
    }

    private static IReadOnlyList<GlbPart> Scene(string file) =>
        GlbScene.Read(File.ReadAllBytes(RepositoryFiles.Find($"assets/models/{file}")));

    // How far a turn bends the lash's +z away from straight (degrees).
    private static float Bend(Matrix4x4 turn) =>
        MathF.Acos(Math.Clamp(Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, turn)).Z, -1f, 1f)) * 180f / MathF.PI;

    private static float Radians(float degrees) => degrees * MathF.PI / 180f;

    private static void AssertNear(Matrix4x4 expected, Matrix4x4 actual)
    {
        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                Assert.True(
                    MathF.Abs(expected[row, column] - actual[row, column]) < Tolerance,
                    $"Expected {expected}, got {actual}.");
            }
        }
    }
}
