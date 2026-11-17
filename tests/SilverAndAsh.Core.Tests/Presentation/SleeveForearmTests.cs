using System.Numerics;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// The two-part sleeves of issue #265: the cuff hangs rigidly on the weapon, the forearm turns around its pivot so it
/// always points at the shoulder (<see cref="WeaponPose.ForearmTurn"/>). Checked on the checked-in models over the whole
/// swing, wind-up and recoil. Picture edges and margin as in <see cref="WeaponPoseTests"/>.
/// </summary>
public class SleeveForearmTests
{
    // One step of the fine sweeps: 1 % of the swing, the wind-up or the recoil.
    private const int Steps = 100;

    // From one step to the next the forearm may turn at most this far (degrees). A step of 1 % turns the blade by about
    // 1 degree around each of its axes (95 degrees of swing); a forearm that jumps or rolls over turns far more.
    private const float MaxDegreesPerStep = 3f;

    private static readonly float[] Trembles = [-0.0012f, 0f, 0.0012f];

    [Theory]
    [InlineData("blade_hand.glb")]
    [InlineData("pistol_hand.glb")]
    public void Both_parts_of_the_sleeve_are_in_the_file_in_the_order_of_the_rig(string file)
    {
        var names = WeaponPoseTests.Triangles(file).Select(part => part.Name);

        Assert.Equal(WeaponPose.HandParts.Select(part => part.Name), names);
    }

    [Theory]
    [InlineData("blade_hand.glb")]
    [InlineData("pistol_hand.glb")]
    public void At_rest_the_forearm_is_not_turned_and_points_at_the_shoulder_as_it_stands_in_the_file(string file)
    {
        var forearm = Forearm(file);
        var (turn, rest, shoulder) = file.StartsWith("blade", StringComparison.Ordinal)
            ? (WeaponPose.BladeForearm(0f, 0f, 0f, forearm.Pivot), WeaponPose.Blade(0f, 0f, 0f), WeaponPose.BladeShoulder)
            : (WeaponPose.PistolForearm(0f, forearm.Pivot), WeaponPose.Pistol(0f), WeaponPose.PistolShoulder);

        AssertNearlyIdentity(turn);

        // The tube is straight: the middle of its corners lies on its axis, which runs from the pivot to the shoulder.
        var axis = Vector3.Normalize(forearm.Corners.Aggregate(Vector3.Zero, (sum, corner) => sum + corner) / forearm.Corners.Length);
        var toShoulder = WeaponPose.ShoulderDirection(rest, forearm.Pivot, shoulder);
        Assert.True(Degrees(axis, toShoulder) < 1f, $"{file}: the forearm's axis is {Degrees(axis, toShoulder):0.00} degrees off the shoulder at rest.");
    }

    [Fact]
    public void In_every_blade_pose_the_forearm_points_at_the_shoulder_its_end_lies_outside_the_picture_and_it_never_crosses_the_middle()
    {
        var forearm = Forearm("blade_hand.glb");
        foreach (var swing in Fractions(20))
        {
            foreach (var windUp in Fractions(20))
            {
                foreach (var tremble in Trembles)
                {
                    var pose = WeaponPose.Blade(swing, windUp, tremble);
                    var turn = WeaponPose.BladeForearm(swing, windUp, tremble, forearm.Pivot);
                    var name = $"blade, swing {swing:0.00}, wind-up {windUp:0.00}, tremble {tremble}";
                    AssertPointsAtShoulder(forearm, pose, turn, WeaponPose.BladeShoulder, name);
                    AssertEndOutsideThePicture(forearm, pose, turn, name);
                    AssertNotInTheMiddle(forearm, pose, turn, name);
                    AssertNotCutOpenWithinThePicture(forearm, pose, turn, name);
                }
            }
        }
    }

    [Fact]
    public void In_every_pistol_pose_the_forearm_points_at_the_shoulder_its_end_lies_outside_the_picture_and_it_never_crosses_the_middle()
    {
        var forearm = Forearm("pistol_hand.glb");
        foreach (var kick in Fractions(Steps))
        {
            var pose = WeaponPose.Pistol(kick);
            var turn = WeaponPose.PistolForearm(kick, forearm.Pivot);
            var name = $"pistol, kick {kick:0.00}";
            AssertPointsAtShoulder(forearm, pose, turn, WeaponPose.PistolShoulder, name);
            AssertEndOutsideThePicture(forearm, pose, turn, name);
            AssertNotInTheMiddle(forearm, pose, turn, name);
            AssertNotCutOpenWithinThePicture(forearm, pose, turn, name);
        }
    }

    [Fact]
    public void Through_the_swing_and_the_wind_up_the_blade_forearm_turns_smoothly_without_rolling_over()
    {
        var pivot = Forearm("blade_hand.glb").Pivot;
        foreach (var held in (float[])[0f, 0.5f, 1f])
        {
            AssertSmooth(Fractions(Steps).Select(swing => WeaponPose.BladeForearm(swing, held, 0f, pivot)), $"blade swing, wind-up {held}");
            AssertSmooth(Fractions(Steps).Select(windUp => WeaponPose.BladeForearm(held, windUp, 0f, pivot)), $"blade wind-up, swing {held}");
        }

        // A charged swing: the wind-up eases off while the swing goes out.
        AssertSmooth(Fractions(Steps).Select(step => WeaponPose.BladeForearm(step, 1f - step, 0f, pivot)), "charged blade swing");
    }

    [Fact]
    public void Through_the_recoil_the_pistol_forearm_turns_smoothly_without_rolling_over()
    {
        var pivot = Forearm("pistol_hand.glb").Pivot;

        AssertSmooth(Fractions(Steps).Select(kick => WeaponPose.PistolForearm(kick, pivot)), "pistol recoil");
    }

    private static IEnumerable<float> Fractions(int steps) => Enumerable.Range(0, steps + 1).Select(step => step / (float)steps);

    private sealed record Part(Vector3 Pivot, Vector3[] Corners, int[] Triangles);

    // The forearm from the file: corners relative to its pivot.
    private static Part Forearm(string file)
    {
        var part = WeaponPoseTests.Triangles(file).Single(part => part.Name == WeaponPose.Forearm);
        return new Part(part.Pivot, part.Corners, part.Triangles);
    }

    // Corner of the forearm -> camera space, shrunk towards the eye as drawn (Rig.Pose with the parts on the grip).
    private static Matrix4x4 Frame(Part forearm, Matrix4x4 pose, Matrix4x4 turn) =>
        turn * Matrix4x4.CreateTranslation(forearm.Pivot) * pose * Matrix4x4.CreateScale(WeaponPose.ShrinkTowardsEye);

    private static void AssertPointsAtShoulder(Part forearm, Matrix4x4 pose, Matrix4x4 turn, Vector3 shoulder, string name)
    {
        // The axis at rest, turned, and placed: from the pivot it runs straight at the shoulder (before shrinking).
        var rest = name.StartsWith("blade", StringComparison.Ordinal) ? WeaponPose.Blade(0f, 0f, 0f) : WeaponPose.Pistol(0f);
        var axis = Vector3.TransformNormal(WeaponPose.ShoulderDirection(rest, forearm.Pivot, shoulder), turn);
        var pivot = Vector3.Transform(forearm.Pivot, pose);
        var placed = Vector3.Normalize(Vector3.TransformNormal(axis, pose));

        Assert.True(Degrees(placed, shoulder - pivot) < 0.05f, $"{name}: the forearm points {Degrees(placed, shoulder - pivot):0.000} degrees past the shoulder.");
    }

    // The back end of the tube (the corners of its far cap) never lies within the picture, widened by the margin for
    // bob and sway: behind the near plane or beside the picture's edges.
    private static void AssertEndOutsideThePicture(Part forearm, Matrix4x4 pose, Matrix4x4 turn, string name)
    {
        var axis = Vector3.Normalize(forearm.Corners.Aggregate(Vector3.Zero, (sum, corner) => sum + corner));
        var farthest = forearm.Corners.Max(corner => Vector3.Dot(corner, axis));
        var frame = Frame(forearm, pose, turn);
        foreach (var corner in forearm.Corners.Where(corner => Vector3.Dot(corner, axis) > farthest - 0.01f))
        {
            var point = Vector3.Transform(corner, frame);
            var inside = point.Z > WeaponPose.NearPlane
                && MathF.Abs(point.X) <= (point.Z * WeaponPoseTests.HalfWidth) + WeaponPoseTests.EdgeMargin
                && MathF.Abs(point.Y) <= (point.Z * WeaponPoseTests.HalfHeight) + WeaponPoseTests.EdgeMargin;
            Assert.False(inside, $"{name}: the end of the forearm lies within the picture at {point}.");
        }
    }

    // The middle of the picture: lines of sight through a grid over the middle fifth of its width and height. None of
    // them meets the forearm beyond the near plane.
    private static void AssertNotInTheMiddle(Part forearm, Matrix4x4 pose, Matrix4x4 turn, string name)
    {
        var frame = Frame(forearm, pose, turn);
        var placed = forearm.Corners.Select(corner => Vector3.Transform(corner, frame)).ToArray();
        foreach (var across in (float[])[-0.2f, -0.1f, 0f, 0.1f, 0.2f])
        {
            foreach (var up in (float[])[-0.2f, -0.1f, 0f, 0.1f, 0.2f])
            {
                var sight = new Vector3(across * WeaponPoseTests.HalfWidth, up * WeaponPoseTests.HalfHeight, 1f);
                for (var first = 0; first < forearm.Triangles.Length; first += 3)
                {
                    var hit = Hit(sight, placed[forearm.Triangles[first]], placed[forearm.Triangles[first + 1]], placed[forearm.Triangles[first + 2]]);
                    Assert.False(
                        hit is { } z && z > WeaponPose.NearPlane,
                        $"{name}: the forearm crosses the middle of the picture ({across}, {up}) {hit:0.000} m ahead.");
                }
            }
        }
    }

    // Where the near plane cuts the tube within the picture, one would look into it (issue #256).
    private static void AssertNotCutOpenWithinThePicture(Part forearm, Matrix4x4 pose, Matrix4x4 turn, string name)
    {
        var frame = Frame(forearm, pose, turn);
        var placed = forearm.Corners.Select(corner => Vector3.Transform(corner, frame)).ToArray();
        for (var first = 0; first < forearm.Triangles.Length; first += 3)
        {
            var triangle = new[] { placed[forearm.Triangles[first]], placed[forearm.Triangles[first + 1]], placed[forearm.Triangles[first + 2]] };
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

    // The forearm's direction (the turned axis) and its roll (a direction across the tube, turned) change only a little
    // from step to step: no jump, no rolling over.
    private static void AssertSmooth(IEnumerable<Matrix4x4> turns, string name)
    {
        Matrix4x4? last = null;
        var step = 0;
        foreach (var turn in turns)
        {
            if (last is { } before)
            {
                foreach (var direction in (Vector3[])[Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ])
                {
                    var angle = Degrees(Vector3.TransformNormal(direction, before), Vector3.TransformNormal(direction, turn));
                    Assert.True(angle <= MaxDegreesPerStep, $"{name}, step {step}: the forearm turns {angle:0.00} degrees at once.");
                }
            }

            last = turn;
            step++;
        }
    }

    private static void AssertNearlyIdentity(Matrix4x4 turn)
    {
        foreach (var direction in (Vector3[])[Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ])
        {
            Assert.True(Degrees(direction, Vector3.TransformNormal(direction, turn)) < 0.01f, $"At rest the forearm is turned: {turn}.");
        }
    }

    // Where the line of sight from the eye (the origin) along `sight` (z = 1) meets the triangle: its z, or null
    // (Moeller-Trumbore).
    private static float? Hit(Vector3 sight, Vector3 a, Vector3 b, Vector3 c)
    {
        var ab = b - a;
        var ac = c - a;
        var p = Vector3.Cross(sight, ac);
        var determinant = Vector3.Dot(ab, p);
        if (MathF.Abs(determinant) < 1e-9f)
        {
            return null;
        }

        var toEye = -a;
        var u = Vector3.Dot(toEye, p) / determinant;
        var q = Vector3.Cross(toEye, ab);
        var v = Vector3.Dot(sight, q) / determinant;
        if (u < 0f || v < 0f || u + v > 1f)
        {
            return null;
        }

        return Vector3.Dot(ac, q) / determinant;
    }

    private static float Degrees(Vector3 a, Vector3 b) =>
        MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(a), Vector3.Normalize(b)), -1f, 1f)) * 180f / MathF.PI;
}
