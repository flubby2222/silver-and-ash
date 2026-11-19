using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// The club bent at the grip (issue #417): the sleeve stays as it is, only the club turns in the cuff. Checked on the
/// checked-in models (club.glb, club_hand.glb) through the swing, the wind-up and the charged swing, as WeaponView draws
/// them. Picture edges, margin and reach as in <see cref="WeaponPoseTests"/>.
/// </summary>
public class ClubBendTests
{
    private const float Tolerance = 0.0001f;

    // A single point of the club seen at the sleeve's outline is a ray grazing it; the handle showing is more than one.
    private const int GrazingPoints = 1;

    private static readonly float[] Trembles = [-0.0012f, 0f, 0.0012f];

    [Fact]
    public void At_rest_the_club_is_bent_35_degrees_up_and_10_outward()
    {
        Assert.Equal((35f, 10f), WeaponPose.ClubBendDegrees(swing: 0f, windUp: 0f));
    }

    [Fact]
    public void At_the_height_of_a_swing_where_it_hits_the_club_is_straight()
    {
        var (pitch, outward) = WeaponPose.ClubBendDegrees(swing: 1f, windUp: 0f);

        Assert.Equal(0f, pitch, Tolerance);
        Assert.Equal(0f, outward, Tolerance);
        foreach (var point in ClubCorners)
        {
            Assert.True(Vector3.Distance(point, Vector3.Transform(point, WeaponPose.ClubBend(1f, 0f))) < Tolerance);
        }
    }

    [Fact]
    public void Fully_wound_up_the_club_is_bent_20_degrees_up_and_10_inward()
    {
        var (pitch, outward) = WeaponPose.ClubBendDegrees(swing: 0f, windUp: 1f);

        Assert.Equal(20f, pitch, Tolerance);
        Assert.Equal(-10f, outward, Tolerance);
    }

    [Fact]
    public void At_rest_the_bent_club_raises_its_head_and_turns_it_outward_to_the_left()
    {
        var head = new Vector3(0f, 0f, 0.24f);
        var rest = WeaponPose.Blade(0f, 0f, 0f);
        var pivot = WeaponPose.ClubBendPivot;
        var pitchOnly = Matrix4x4.CreateTranslation(-pivot) * Matrix4x4.CreateRotationX(-35f * MathF.PI / 180f) * Matrix4x4.CreateTranslation(pivot);
        var straight = Vector3.Transform(head, rest);
        var raised = Vector3.Transform(head, pitchOnly * rest);
        var bent = Vector3.Transform(head, WeaponPose.ClubBend(0f, 0f) * rest);

        // Camera space: +x right, +y up. The pose rolls the club, so the turn outward is measured against the raise alone.
        Assert.True(raised.Y > straight.Y + 0.05f, $"The head should be raised: {straight} -> {raised}.");
        Assert.True(bent.X < raised.X - 0.01f, $"The head should turn outward, to the left: {raised} -> {bent}.");
    }

    [Fact]
    public void Only_the_club_bends_the_blade_stays_straight()
    {
        Assert.Equal(Matrix4x4.Identity, WeaponPose.MeleeBend(WeaponKind.SilverBlade, 0.3f, 0.5f));
        Assert.Equal(WeaponPose.ClubBend(0.3f, 0.5f), WeaponPose.MeleeBend(WeaponKind.Club, 0.3f, 0.5f));
    }

    [Fact]
    public void The_club_bends_around_a_point_on_its_handle_inside_the_cuff()
    {
        // The handle runs along the z axis from its end to the grip; the pivot lies on it, behind the cuff's mouth.
        Assert.Equal(0f, WeaponPose.ClubBendPivot.X);
        Assert.Equal(0f, WeaponPose.ClubBendPivot.Y);
        Assert.InRange(WeaponPose.ClubBendPivot.Z, ClubCorners.Min(corner => corner.Z), CuffMouth);
        Assert.True(Inside(WeaponPose.ClubBendPivot, Cuff), "The pivot should lie inside the cuff.");
    }

    [Fact]
    public void Everything_drawn_of_the_bent_club_stays_within_reach_of_the_eyes()
    {
        foreach (var (swing, windUp, tremble, name) in Poses())
        {
            var pose = WeaponPose.ClubBend(swing, windUp) * WeaponPose.Blade(swing, windUp, tremble);
            WeaponPoseTests.AssertWithinReach("club.glb", pose, _ => Matrix4x4.Identity);
        }
    }

    [Fact]
    public void Everything_drawn_of_the_club_sleeve_stays_within_reach_of_the_eyes_with_the_forearm_turned_as_drawn()
    {
        foreach (var (swing, windUp, tremble, name) in Poses())
        {
            var turn = WeaponPose.BladeForearm(swing, windUp, tremble, ForearmPivot);
            WeaponPoseTests.AssertWithinReach(
                "club_hand.glb",
                WeaponPose.Blade(swing, windUp, tremble),
                part => part == WeaponPose.Forearm ? turn : Matrix4x4.Identity);
        }
    }

    [Fact]
    public void The_near_plane_cuts_the_bent_club_open_nowhere_within_the_picture()
    {
        // The end of the handle may come closer than the near plane, but only beside the picture, where nobody sees the
        // cut (as for the sleeves in WeaponPoseTests).
        var shrink = Matrix4x4.CreateScale(WeaponPose.ShrinkTowardsEye);
        foreach (var (swing, windUp, tremble, name) in Poses())
        {
            var frame = WeaponPose.ClubBend(swing, windUp) * WeaponPose.Blade(swing, windUp, tremble) * shrink;
            foreach (var triangle in ClubTriangles)
            {
                var placed = triangle.Select(corner => Vector3.Transform(corner, frame)).ToArray();
                foreach (var (from, to) in placed.Zip(placed.Skip(1).Append(placed[0])))
                {
                    if ((from.Z - WeaponPose.NearPlane) * (to.Z - WeaponPose.NearPlane) >= 0f)
                    {
                        continue;
                    }

                    var cut = Vector3.Lerp(from, to, (WeaponPose.NearPlane - from.Z) / (to.Z - from.Z));
                    var inside = MathF.Abs(cut.X) <= cut.Z * WeaponPoseTests.HalfWidth && MathF.Abs(cut.Y) <= cut.Z * WeaponPoseTests.HalfHeight;
                    Assert.False(inside, $"{name}: the near plane cuts the club open within the picture at {cut}.");
                }
            }
        }
    }

    [Fact]
    public void Between_the_hit_and_the_full_wind_up_the_bend_changes_linearly()
    {
        Assert.Equal(17.5f, WeaponPose.ClubBendDegrees(swing: 0.5f, windUp: 0f).Pitch, Tolerance);
        Assert.Equal(5f, WeaponPose.ClubBendDegrees(swing: 0.5f, windUp: 0f).Outward, Tolerance);
        Assert.Equal(27.5f, WeaponPose.ClubBendDegrees(swing: 0f, windUp: 0.5f).Pitch, Tolerance);
        Assert.Equal(0f, WeaponPose.ClubBendDegrees(swing: 0f, windUp: 0.5f).Outward, Tolerance);

        // The charged swing: the wind-up eases off as the swing goes out, from 20 / -10 to 0 / 0.
        Assert.Equal(10f, WeaponPose.ClubBendDegrees(swing: 0.5f, windUp: 0.5f).Pitch, Tolerance);
        Assert.Equal(-5f, WeaponPose.ClubBendDegrees(swing: 0.5f, windUp: 0.5f).Outward, Tolerance);
    }

    [Fact]
    public void The_end_of_the_handle_never_pierces_the_sleeve()
    {
        // A corner of the club behind the cuff's mouth that the eyes see within the picture, past the sleeve and past the
        // club itself, is the handle sticking out of the sleeve (variant C of the probe, 50 degrees, did that at the front
        // of the cuff). The straight club already shows its knob below the sleeve at the height of the swing, where the
        // bend is 0, and on the way there; so the bent club may show no more of it than the straight club in the same pose.
        foreach (var (swing, windUp, name) in PiercingPoses())
        {
            var bend = WeaponPose.ClubBend(swing, windUp);
            var bent = Points(SeenBehindTheMouth(swing, windUp, bend), bend);
            var straight = Points(SeenBehindTheMouth(swing, windUp, Matrix4x4.Identity), Matrix4x4.Identity);
            Assert.True(
                bent.Count <= straight.Count + GrazingPoints,
                $"{name}: the handle sticks out of the sleeve, {bent.Count} points seen behind the mouth instead of {straight.Count} for the straight club: {string.Join(", ", bent)} (file space).");
        }
    }

    // The places of the given corners; corners at the same place (one per face of the mesh) count as one point.
    private static List<Vector3> Points(HashSet<int> corners, Matrix4x4 bend) =>
        corners.Select(index => Vector3.Transform(ClubCorners[index], bend))
            .DistinctBy(point => (MathF.Round(point.X, 4), MathF.Round(point.Y, 4), MathF.Round(point.Z, 4)))
            .ToList();

    // The indices of the club's corners behind the cuff's mouth that the eyes see within the picture (past the sleeve's
    // outline or into its mouth).
    private static HashSet<int> SeenBehindTheMouth(float swing, float windUp, Matrix4x4 bend)
    {
        var pose = WeaponPose.Blade(swing, windUp, 0f);
        Assert.True(Matrix4x4.Invert(pose, out var cameraToFile));
        var eye = Vector3.Transform(Vector3.Zero, cameraToFile);
        var toPicture = pose * Matrix4x4.CreateScale(WeaponPose.ShrinkTowardsEye);
        var cover = Sleeve(WeaponPose.BladeForearm(swing, windUp, 0f, ForearmPivot))
            .Concat(ClubTriangles.Select(triangle => triangle.Select(corner => Vector3.Transform(corner, bend)).ToArray()))
            .ToList();
        var seen = new HashSet<int>();
        for (var index = 0; index < ClubCorners.Length; index++)
        {
            var point = Vector3.Transform(ClubCorners[index], bend);
            if (point.Z < CuffMouth && InPicture(Vector3.Transform(point, toPicture)) && !cover.Any(triangle => Crosses(eye, point, triangle)))
            {
                seen.Add(index);
            }
        }

        return seen;
    }

    private static bool InPicture(Vector3 point) =>
        point.Z > WeaponPose.NearPlane
        && MathF.Abs(point.X) <= (point.Z * WeaponPoseTests.HalfWidth) + WeaponPoseTests.EdgeMargin
        && MathF.Abs(point.Y) <= (point.Z * WeaponPoseTests.HalfHeight) + WeaponPoseTests.EdgeMargin;

    // The poses the game shows, fewer than Poses() (each one casts a ray from the eyes to every corner past every
    // triangle): a plain swing with its overshoot, winding up, and the charged swing where the wind-up eases off as the
    // swing goes out.
    private static IEnumerable<(float Swing, float WindUp, string Name)> PiercingPoses()
    {
        var curve = Enumerable.Range(0, 101).Select(step => WeaponPose.SwingCurve(step / 100f)).ToArray();
        foreach (var swing in Enumerable.Range(0, 21).Select(step => step / 20f).Append(curve.Max()).Append(curve.Min()))
        {
            yield return (swing, 0f, $"club swing, swing {swing:0.000}");
        }

        foreach (var windUp in Enumerable.Range(1, 10).Select(step => step / 10f))
        {
            yield return (0f, windUp, $"club winding up, wind-up {windUp:0.0}");
        }

        foreach (var step in Enumerable.Range(1, 19).Select(step => step / 20f))
        {
            yield return (step, 1f - step, $"charged club swing, swing {step:0.00}");
        }
    }

    // Through the swing (with its overshoot), the wind-up, and the charged swing where the wind-up eases off as the
    // swing goes out.
    private static IEnumerable<(float Swing, float WindUp, float Tremble, string Name)> Poses()
    {
        var overshoot = Enumerable.Range(0, 101).Max(step => WeaponPose.SwingCurve(step / 100f));
        var swings = Enumerable.Range(0, 21).Select(step => step / 20f).Append(overshoot)
            .Concat(Enumerable.Range(0, 101).Select(step => WeaponPose.SwingCurve(step / 100f)).Where(value => value < 0f));
        foreach (var swing in swings)
        {
            foreach (var windUp in (float[])[0f, 0.25f, 0.5f, 0.75f, 1f])
            {
                foreach (var tremble in Trembles)
                {
                    yield return (swing, windUp, tremble, $"club, swing {swing:0.000}, wind-up {windUp:0.00}, tremble {tremble}");
                }
            }
        }

        foreach (var step in Enumerable.Range(0, 21).Select(step => step / 20f))
        {
            yield return (step, 1f - step, 0f, $"charged club swing, swing {step:0.00}");
        }
    }

    private static readonly Vector3[] ClubCorners =
        WeaponPoseTests.Triangles("club.glb").Single(part => part.Name == WeaponPose.Club).Corners;

    private static readonly Vector3[][] ClubTriangles = ClubTrianglesInFile();

    private static Vector3[][] ClubTrianglesInFile()
    {
        var (_, _, corners, triangles) = WeaponPoseTests.Triangles("club.glb").Single(part => part.Name == WeaponPose.Club);
        return Enumerable.Range(0, triangles.Length / 3)
            .Select(first => new[] { corners[triangles[3 * first]], corners[triangles[(3 * first) + 1]], corners[triangles[(3 * first) + 2]] })
            .ToArray();
    }

    // The cuff's triangles (it hangs on the grip unturned, its pivot at 0).
    private static readonly Vector3[][] Cuff = Triangles(WeaponPose.Cuff, Matrix4x4.Identity).ToArray();

    // The cuff's mouth: its front ring (the lowest corner of its front centimetre), where the club comes out.
    private static readonly float CuffMouth = CuffMouthZ();

    private static readonly Vector3 ForearmPivot =
        WeaponPoseTests.Triangles("club_hand.glb").Single(part => part.Name == WeaponPose.Forearm).Pivot;

    private static float CuffMouthZ()
    {
        var corners = Cuff.SelectMany(triangle => triangle).ToArray();
        var front = corners.Max(corner => corner.Z);
        return corners.Where(corner => corner.Z > front - 0.01f).Min(corner => corner.Z);
    }

    // Cuff and forearm in file space, the forearm turned around its pivot as drawn.
    private static IEnumerable<Vector3[]> Sleeve(Matrix4x4 forearmTurn) =>
        Triangles(WeaponPose.Cuff, Matrix4x4.Identity).Concat(Triangles(WeaponPose.Forearm, forearmTurn));

    private static IEnumerable<Vector3[]> Triangles(string part, Matrix4x4 turn)
    {
        var (_, pivot, corners, triangles) = WeaponPoseTests.Triangles("club_hand.glb").Single(found => found.Name == part);
        var frame = turn * Matrix4x4.CreateTranslation(pivot);
        for (var first = 0; first < triangles.Length; first += 3)
        {
            yield return
            [
                Vector3.Transform(corners[triangles[first]], frame),
                Vector3.Transform(corners[triangles[first + 1]], frame),
                Vector3.Transform(corners[triangles[first + 2]], frame),
            ];
        }
    }

    // Inside a closed mesh: rays in several directions cross it an odd number of times (a majority, so that a ray
    // grazing an edge does not decide).
    private static bool Inside(Vector3 point, Vector3[][] mesh)
    {
        Vector3[] directions = [new(0.31f, 0.17f, 0.93f), new(0.93f, 0.31f, 0.17f), new(-0.17f, 0.93f, 0.31f), new(-0.6f, -0.5f, 0.62f), new(0.2f, -0.95f, -0.2f)];
        var odd = directions.Count(direction => mesh.Count(triangle => Hit(point, direction, triangle) is > 0f) % 2 == 1);
        return odd > directions.Length / 2;
    }

    // Whether the segment from `from` to `to` passes through the triangle (not at its very ends).
    private static bool Crosses(Vector3 from, Vector3 to, Vector3[] triangle) =>
        Hit(from, to - from, triangle) is > 0f and < 0.999f;

    // Where the ray from `origin` along `direction` meets the triangle, in lengths of `direction` (Moeller-Trumbore).
    private static float? Hit(Vector3 origin, Vector3 direction, Vector3[] triangle)
    {
        var ab = triangle[1] - triangle[0];
        var ac = triangle[2] - triangle[0];
        var p = Vector3.Cross(direction, ac);
        var determinant = Vector3.Dot(ab, p);
        if (MathF.Abs(determinant) < 1e-12f)
        {
            return null;
        }

        var toOrigin = origin - triangle[0];
        var u = Vector3.Dot(toOrigin, p) / determinant;
        var q = Vector3.Cross(toOrigin, ab);
        var v = Vector3.Dot(direction, q) / determinant;
        if (u < 0f || v < 0f || u + v > 1f)
        {
            return null;
        }

        return Vector3.Dot(ac, q) / determinant;
    }
}
