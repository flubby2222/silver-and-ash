using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// The pistol's reload motion and the blade's swing curve (issue #292): the checks the sleeve tests do for recoil and
/// swing, over the whole reload, plus the picture's HUD, the bend of the sleeve and the fade-out of a cancelled reload.
/// </summary>
public class ReloadAndSwingPoseTests
{
    private const int Steps = 100;

    // The bend between the cuff (rigid on the weapon) and the forearm (turned to the shoulder) may reach this far in
    // degrees. Beyond about 50 the flat dark caps of the two tubes show in the gap; the first swing (95 degrees of yaw)
    // bent 80, the one now 41.
    private const float MaxBendDegrees = 50f;

    // 1280 x 720, as WeaponPoseTests: the HUD's "Reloading..." line and bar, right-aligned in the lower right corner.
    private const float ScreenWidth = 1280f;
    private const float ScreenHeight = 720f;
    private static readonly (float Left, float Top, float Right, float Bottom) ReloadDisplay = (1100f, 640f, 1260f, 700f);

    // A step is 1 % of the reload (15 ms at 1.5 s): the hand moves out in the first 20 % - up to 7 degrees per step on the way back. A forearm
    // that jumps or rolls over turns far more.
    private const float ReloadMaxDegreesPerStep = 8f;

    private static IEnumerable<float> Fractions() => Enumerable.Range(0, Steps + 1).Select(step => step / (float)Steps);

    private static readonly float[] Weights = [0.5f, 1f];

    private static Vector3 Pivot(string file) => WeaponPoseTests.Triangles(file).Single(part => part.Name == WeaponPose.Forearm).Pivot;

    // ---- the reload ----------------------------------------------------------------------------------------------

    [Fact]
    public void The_reload_motion_is_zero_at_both_ends_and_never_leaves_the_resting_pose_at_weight_zero()
    {
        foreach (var progress in (float[])[0f, 1f])
        {
            var (roll, lift, shift) = WeaponPose.ReloadOffsets(progress);
            Assert.Equal(0f, roll, 5);
            Assert.Equal(0f, lift, 5);
            Assert.True(shift.Length() < 1e-5f, $"The hand is shifted by {shift} at {progress}.");
        }

        foreach (var progress in Fractions())
        {
            var (rest, now) = (WeaponPose.Pistol(0f), WeaponPose.Pistol(0f, progress, 0f));
            Assert.True(
                Vector3.Distance(Vector3.Transform(new Vector3(0f, 0f, 0.2f), rest), Vector3.Transform(new Vector3(0f, 0f, 0.2f), now)) < 1e-5f,
                $"At weight 0 the pistol is out of its resting pose at {progress}.");
        }
    }

    [Fact]
    public void Everything_drawn_of_the_pistol_stays_within_reach_of_the_eyes_over_the_whole_reload()
    {
        var pivot = Pivot("pistol_hand.glb");
        foreach (var weight in Weights)
        {
            foreach (var progress in Fractions())
            {
                var pose = WeaponPose.Pistol(0f, progress, weight);
                WeaponPoseTests.AssertWithinReach("pistol.glb", pose, _ => Matrix4x4.Identity);
                var turn = WeaponPose.PistolForearm(0f, progress, weight, pivot);
                WeaponPoseTests.AssertWithinReach("pistol_hand.glb", pose, part => part == WeaponPose.Forearm ? turn : Matrix4x4.Identity);
            }
        }
    }

    [Fact]
    public void Over_the_whole_reload_the_forearm_ends_outside_the_picture_and_the_near_plane_does_not_cut_it_open()
    {
        var file = WeaponPoseTests.Triangles("pistol_hand.glb").Single(part => part.Name == WeaponPose.Forearm);
        var axis = Vector3.Normalize(file.Corners.Aggregate(Vector3.Zero, (sum, corner) => sum + corner));
        var farthest = file.Corners.Max(corner => Vector3.Dot(corner, axis));
        foreach (var weight in Weights)
        {
            foreach (var progress in Fractions())
            {
                var pose = WeaponPose.Pistol(0f, progress, weight);
                var turn = WeaponPose.PistolForearm(0f, progress, weight, file.Pivot);
                var frame = turn * Matrix4x4.CreateTranslation(file.Pivot) * pose * Matrix4x4.CreateScale(WeaponPose.ShrinkTowardsEye);
                var name = $"reload {progress:0.00}, weight {weight}";

                foreach (var corner in file.Corners.Where(corner => Vector3.Dot(corner, axis) > farthest - 0.01f))
                {
                    var point = Vector3.Transform(corner, frame);
                    var inside = point.Z > WeaponPose.NearPlane
                        && MathF.Abs(point.X) <= (point.Z * WeaponPoseTests.HalfWidth) + WeaponPoseTests.EdgeMargin
                        && MathF.Abs(point.Y) <= (point.Z * WeaponPoseTests.HalfHeight) + WeaponPoseTests.EdgeMargin;
                    Assert.False(inside, $"{name}: the end of the forearm lies within the picture at {point}.");
                }

                var placed = file.Corners.Select(corner => Vector3.Transform(corner, frame)).ToArray();
                for (var first = 0; first < file.Triangles.Length; first += 3)
                {
                    var triangle = new[] { placed[file.Triangles[first]], placed[file.Triangles[first + 1]], placed[file.Triangles[first + 2]] };
                    foreach (var (from, to) in triangle.Zip(triangle.Skip(1).Append(triangle[0])))
                    {
                        if ((from.Z - WeaponPose.NearPlane) * (to.Z - WeaponPose.NearPlane) >= 0f)
                        {
                            continue;
                        }

                        var cut = Vector3.Lerp(from, to, (WeaponPose.NearPlane - from.Z) / (to.Z - from.Z));
                        var inside = MathF.Abs(cut.X) <= cut.Z * WeaponPoseTests.HalfWidth && MathF.Abs(cut.Y) <= cut.Z * WeaponPoseTests.HalfHeight;
                        Assert.False(inside, $"{name}: the near plane cuts the forearm open within the picture at {cut}.");
                    }
                }
            }
        }
    }

    [Fact]
    public void Over_the_whole_reload_the_forearm_turns_smoothly_without_rolling_over()
    {
        var pivot = Pivot("pistol_hand.glb");
        foreach (var weight in Weights)
        {
            AssertSmooth(Fractions().Select(progress => WeaponPose.PistolForearm(0f, progress, weight, pivot)), $"reload, weight {weight}", ReloadMaxDegreesPerStep);
        }
    }

    [Fact]
    public void Over_the_whole_reload_the_pistol_stays_right_of_the_middle_and_off_the_reload_display_of_the_hud()
    {
        foreach (var weight in Weights)
        {
            foreach (var progress in Fractions())
            {
                var triangles = ScreenTriangles(WeaponPose.Pistol(0f, progress, weight));
                var name = $"reload {progress:0.00}, weight {weight}";

                // Right of the middle: no corner (in front of the near plane) lies in the left half.
                Assert.True(
                    triangles.SelectMany(triangle => triangle).All(corner => corner.X >= ScreenWidth / 2f),
                    $"{name}: the pistol reaches into the left half of the picture.");

                // Only the pistol body (pistol.glb) is tested, not the hand and sleeve (pistol_hand.glb): the sleeve already
                // covers the box at (1100, 640) at rest, so the display is not free of the whole picture of the hand.
                // The line "Reloading..." and its bar (CombatHud) stay free: no sample point of that box is covered.
                for (var x = ReloadDisplay.Left; x <= ReloadDisplay.Right; x += 4f)
                {
                    for (var y = ReloadDisplay.Top; y <= ReloadDisplay.Bottom; y += 4f)
                    {
                        Assert.False(
                            triangles.Any(triangle => Covers(triangle, new Vector2(x, y))),
                            $"{name}: the pistol covers the reload display of the HUD at ({x}, {y}).");
                    }
                }
            }
        }
    }

    // The pistol's parts (not the muzzle flash, it lights up only after a shot) as triangles on the 1280 x 720 screen;
    // triangles that reach behind the near plane are left out (the camera cuts them, the sleeve's tests look at those).
    private static List<Vector2[]> ScreenTriangles(Matrix4x4 pose)
    {
        var frame = pose * Matrix4x4.CreateScale(WeaponPose.ShrinkTowardsEye);
        var result = new List<Vector2[]>();
        foreach (var (name, _, corners, indices) in WeaponPoseTests.Triangles("pistol.glb"))
        {
            if (name == WeaponPose.Flash)
            {
                continue;
            }

            var placed = corners.Select(corner => Vector3.Transform(corner, frame)).ToArray();
            for (var first = 0; first < indices.Length; first += 3)
            {
                var three = new[] { placed[indices[first]], placed[indices[first + 1]], placed[indices[first + 2]] };
                if (three.Any(point => point.Z <= WeaponPose.NearPlane))
                {
                    continue;
                }

                result.Add([.. three.Select(point => new Vector2(
                    (ScreenWidth / 2f) + (point.X / point.Z / WeaponPoseTests.HalfWidth * (ScreenWidth / 2f)),
                    (ScreenHeight / 2f) - (point.Y / point.Z / WeaponPoseTests.HalfHeight * (ScreenHeight / 2f))))]);
            }
        }

        return result;
    }

    private static bool Covers(Vector2[] triangle, Vector2 point)
    {
        float Side(Vector2 a, Vector2 b) => ((b.X - a.X) * (point.Y - a.Y)) - ((b.Y - a.Y) * (point.X - a.X));
        var (d1, d2, d3) = (Side(triangle[0], triangle[1]), Side(triangle[1], triangle[2]), Side(triangle[2], triangle[0]));
        return !((d1 < 0f || d2 < 0f || d3 < 0f) && (d1 > 0f || d2 > 0f || d3 > 0f));
    }

    // ---- cancelling the reload ----------------------------------------------------------------------------------

    [Fact]
    public void A_cancelled_reload_fades_out_in_a_tenth_of_a_second_instead_of_jumping()
    {
        const float Frame = 1f / 60f;
        var weight = 1f;
        var previous = weight;
        var frames = 0;
        while (weight > 0f)
        {
            weight = WeaponPose.ReloadWeightStep(weight, reloading: false, Frame);
            Assert.True(weight < previous, "The weight falls with every frame.");
            Assert.True(previous - weight <= 0.2f, $"The weight jumps from {previous} to {weight} in one frame.");
            previous = weight;
            frames++;
            Assert.True(frames <= 8, "The fade takes longer than it should.");
        }

        Assert.True(frames >= 5, $"The weight is gone after {frames} frames: that is a jump, not a fade.");
    }

    [Fact]
    public void The_reload_fades_in_the_same_way_and_the_weight_stays_between_zero_and_one()
    {
        var weight = 0f;
        for (var frame = 0; frame < 30; frame++)
        {
            var next = WeaponPose.ReloadWeightStep(weight, reloading: true, 1f / 60f);
            Assert.InRange(next, weight, 1f);
            weight = next;
        }

        Assert.Equal(1f, weight);
        Assert.Equal(0f, WeaponPose.ReloadWeightStep(0f, reloading: false, 1f));
        Assert.Equal(1f, WeaponPose.ReloadWeightStep(1f, reloading: true, 1f));
    }

    [Fact]
    public void While_a_reload_fades_out_pistol_and_forearm_move_a_little_at_a_time()
    {
        var pivot = Pivot("pistol_hand.glb");
        foreach (var progress in (float[])[0.1f, 0.5f, 0.85f])
        {
            var weights = new List<float> { 1f };
            while (weights[^1] > 0f)
            {
                weights.Add(WeaponPose.ReloadWeightStep(weights[^1], reloading: false, 1f / 60f));
            }

            AssertSmooth(weights.Select(weight => WeaponPose.PistolForearm(0f, progress, weight, pivot)), $"fade-out at {progress}", 10f);
            var poses = weights.Select(weight => WeaponPose.Pistol(0f, progress, weight)).ToArray();
            for (var i = 1; i < poses.Length; i++)
            {
                var jump = Vector3.Distance(Vector3.Transform(new Vector3(0f, 0f, 0.2f), poses[i - 1]), Vector3.Transform(new Vector3(0f, 0f, 0.2f), poses[i]));
                Assert.True(jump < 0.06f, $"fade-out at {progress}, frame {i}: the muzzle jumps {jump:0.000} m.");
            }
        }
    }

    // ---- the blade's swing ---------------------------------------------------------------------------------------

    [Fact]
    public void The_swing_curve_starts_and_ends_at_rest_and_is_at_its_height_when_the_blade_hits()
    {
        Assert.Equal(0f, WeaponPose.SwingCurve(0f), 5);
        Assert.Equal(0f, WeaponPose.SwingCurve(1f), 5);

        // The hit comes after the quick stroke (the first quarter of the swing's time) and the curve holds its height
        // until 40 %: it is at least 99 % out in all of that stretch, and never more than the overshoot beyond it.
        foreach (var fraction in Enumerable.Range(25, 16).Select(percent => percent / 100f))
        {
            var value = WeaponPose.SwingCurve(fraction);
            Assert.InRange(value, 0.99f, 1f + WeaponPose.SwingOvershoot + 0.001f);
        }

        // The stroke out is quick, the way back slow: one tick (a seventeenth) after the half of the stroke is more
        // than half out.
        Assert.True(WeaponPose.SwingCurve(0.25f / 3f) > 0.5f);
    }

    [Fact]
    public void The_swing_curve_does_not_run_backwards_while_going_out_and_undershoots_only_a_little_on_the_way_back()
    {
        var last = 0f;
        foreach (var fraction in Enumerable.Range(0, 26).Select(step => step / 100f))
        {
            var value = WeaponPose.SwingCurve(fraction);
            Assert.True(value >= last - 1e-5f, $"The curve goes back at {fraction}.");
            last = value;
        }

        foreach (var fraction in Fractions())
        {
            // The way back undershoots a little (4 % of the swing), the stroke overshoots by SwingOvershoot.
            Assert.InRange(WeaponPose.SwingCurve(fraction), -0.045f, 1f + WeaponPose.SwingOvershoot + 0.001f);
        }
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void The_bend_between_cuff_and_forearm_stays_below_the_limit_where_the_dark_caps_show(float windUp)
    {
        // The cuff hangs rigidly on the blade: in file space it keeps pointing as it does at rest. The forearm turns to
        // the shoulder. The angle between the two is the bend of the sleeve.
        var pivot = Pivot("blade_hand.glb");
        var rest = Vector3.Normalize(WeaponPose.ShoulderDirection(WeaponPose.Blade(0f, 0f, 0f), pivot, WeaponPose.BladeShoulder));
        var worst = 0f;
        foreach (var fraction in Fractions())
        {
            var swing = WeaponPose.SwingCurve(fraction);
            var now = Vector3.Normalize(WeaponPose.ShoulderDirection(WeaponPose.Blade(swing, windUp, 0f), pivot, WeaponPose.BladeShoulder));
            worst = Math.Max(worst, MathF.Acos(Math.Clamp(Vector3.Dot(rest, now), -1f, 1f)) * 180f / MathF.PI);
        }

        Assert.True(worst <= MaxBendDegrees, $"The sleeve bends {worst:0.0} degrees (wind-up {windUp}), more than {MaxBendDegrees}.");
    }

    [Fact]
    public void The_old_swing_of_95_degrees_would_have_gone_over_the_limit_so_the_limit_catches_the_artefact()
    {
        // The same pose with the first swing's angles: yaw 95, roll +25, no dip, the old shift.
        var pivot = Pivot("blade_hand.glb");
        var rest = Vector3.Normalize(WeaponPose.ShoulderDirection(WeaponPose.Blade(0f, 0f, 0f), pivot, WeaponPose.BladeShoulder));
        var old = Matrix4x4.CreateScale(-1f, 1f, 1f) *
            Matrix4x4.CreateRotationZ(-((WeaponPose.BladeRestRollDegrees + 25f) * MathF.PI / 180f)) *
            Matrix4x4.CreateRotationY(95f * MathF.PI / 180f) *
            Matrix4x4.CreateTranslation(WeaponPose.BladeGrip + new Vector3(0.06f, 0.04f, 0f));
        var now = Vector3.Normalize(WeaponPose.ShoulderDirection(old, pivot, WeaponPose.BladeShoulder));
        var bend = MathF.Acos(Math.Clamp(Vector3.Dot(rest, now), -1f, 1f)) * 180f / MathF.PI;

        Assert.True(bend > MaxBendDegrees, $"The old swing bends only {bend:0.0} degrees: the limit would not have caught it.");
    }

    // The direction and roll of the forearm change by at most `limit` degrees per step.
    [Fact]
    public void A_reload_of_the_rifle_never_moves_the_pistol_after_the_switch()
    {
        var motion = new ReloadMotion();
        const float Frame = 1f / 60f;

        // The rifle reloads for a while: no motion of its own, so nothing shows.
        for (var i = 0; i < 60; i++)
        {
            motion.Step(WeaponKind.Rifle, true, i / 150f, Frame);
            Assert.Equal(0f, motion.Weight);
            Assert.Equal(0f, motion.Progress);
        }

        // Switched to the pistol (which does not reload): rest, from the first frame on.
        for (var i = 0; i < 10; i++)
        {
            motion.Step(WeaponKind.Pistol, false, 0f, Frame);
            Assert.Equal(0f, motion.Weight);
            Assert.Equal(0f, motion.Progress);
        }
    }

    [Fact]
    public void Switching_the_weapon_resets_a_running_reload_at_once()
    {
        var motion = new ReloadMotion();
        for (var i = 0; i < 40; i++)
        {
            motion.Step(WeaponKind.Pistol, true, i / 90f, 1f / 60f);
        }

        Assert.Equal(1f, motion.Weight);

        motion.Step(WeaponKind.Rifle, true, 0.5f, 1f / 60f);
        motion.Step(WeaponKind.Pistol, false, 0f, 1f / 60f);

        Assert.Equal(0f, motion.Weight);
        Assert.Equal(0f, motion.Progress);
    }

    [Fact]
    public void The_pistol_reload_follows_the_magazine_and_fades_in()
    {
        var motion = new ReloadMotion();

        motion.Step(WeaponKind.Pistol, true, 0.3f, 1f / 60f);

        Assert.Equal(0.3f, motion.Progress);
        Assert.InRange(motion.Weight, 0.1f, 0.2f);
    }

    private static void AssertSmooth(IEnumerable<Matrix4x4> turns, string name, float limit = 3f)
    {
        Matrix4x4? last = null;
        var step = 0;
        foreach (var turn in turns)
        {
            if (last is { } before)
            {
                foreach (var direction in (Vector3[])[Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ])
                {
                    var angle = MathF.Acos(Math.Clamp(
                        Vector3.Dot(Vector3.Normalize(Vector3.TransformNormal(direction, before)), Vector3.Normalize(Vector3.TransformNormal(direction, turn))),
                        -1f,
                        1f)) * 180f / MathF.PI;
                    Assert.True(angle <= limit, $"{name}, step {step}: the forearm turns {angle:0.00} degrees at once.");
                }
            }

            last = turn;
            step++;
        }
    }
}
