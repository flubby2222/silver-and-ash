using System.Numerics;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Issue #624: in the lower left of the picture a piece of the sleeve was missing - the near plane cut the back end of the
/// cuff open, and through the cut one looked into the tube, whose inside is not drawn. Here both parts of every sleeve,
/// cuff and forearm, in every pose of its weapon: no point of their surface within the picture lies nearer to the eyes
/// than <see cref="WeaponPose.NearPlane"/>. What lies there would be cut off and leave a hole. Picture edges and the
/// margin for bob and sway as in <see cref="WeaponPoseTests"/>; poses as drawn (forearm turned towards the shoulder,
/// shrunk towards the eye).
/// </summary>
public class SleeveNearPlaneTests
{
    private static readonly float[] Trembles = [-0.0012f, 0f, 0.0012f];

    [Theory]
    [InlineData("blade_hand.glb")]
    [InlineData("club_hand.glb")]
    public void No_part_of_a_melee_sleeve_comes_nearer_than_the_near_plane_within_the_picture(string file)
    {
        // The swing as WeaponView passes it on: SwingCurve in steps of 1 %, with its overshoot past 1 and its dip below 0.
        var pivot = ForearmPivot(file);
        foreach (var swing in Fractions(100).Select(WeaponPose.SwingCurve).Concat(Fractions(20)))
        {
            foreach (var windUp in Fractions(4))
            {
                foreach (var tremble in Trembles)
                {
                    AssertBeyondTheNearPlane(
                        file,
                        WeaponPose.Blade(swing, windUp, tremble),
                        WeaponPose.BladeForearm(swing, windUp, tremble, pivot),
                        $"swing {swing}, wind-up {windUp}, tremble {tremble}");
                }
            }
        }
    }

    [Fact]
    public void No_part_of_the_pistol_sleeve_comes_nearer_than_the_near_plane_within_the_picture()
    {
        // The reload in steps of 1 %: the hammer click near its end pushes the hand back towards the eyes.
        var pivot = ForearmPivot("pistol_hand.glb");
        foreach (var kick in Fractions(10))
        {
            foreach (var reload in Fractions(100))
            {
                foreach (var weight in Fractions(10))
                {
                    AssertBeyondTheNearPlane(
                        "pistol_hand.glb",
                        WeaponPose.Pistol(kick, reload, weight),
                        WeaponPose.PistolForearm(kick, reload, weight, pivot),
                        $"kick {kick}, reload {reload}, weight {weight}");
                }
            }
        }
    }

    private static IEnumerable<float> Fractions(int steps) => Enumerable.Range(0, steps + 1).Select(step => step / (float)steps);

    private static Vector3 ForearmPivot(string file) =>
        WeaponPoseTests.Triangles(file).Single(part => part.Name == WeaponPose.Forearm).Pivot;

    // Every triangle of cuff and forearm as drawn, clipped to the picture (widened by the margin) and to what lies in
    // front of the eyes. The piece left is flat, so its nearest point is one of its corners.
    private static void AssertBeyondTheNearPlane(string file, Matrix4x4 pose, Matrix4x4 forearmTurn, string label)
    {
        var shrink = Matrix4x4.CreateScale(WeaponPose.ShrinkTowardsEye);
        foreach (var (name, pivot, corners, triangles) in WeaponPoseTests.Triangles(file))
        {
            var turn = name == WeaponPose.Forearm ? forearmTurn : Matrix4x4.Identity;
            var frame = turn * Matrix4x4.CreateTranslation(pivot) * pose * shrink;
            var placed = corners.Select(corner => Vector3.Transform(corner, frame)).ToArray();
            for (var first = 0; first < triangles.Length; first += 3)
            {
                Vector3[] piece = [placed[triangles[first]], placed[triangles[first + 1]], placed[triangles[first + 2]]];
                piece = Clip(piece, point => point.Z);
                piece = Clip(piece, point => (point.Z * WeaponPoseTests.HalfHeight) + WeaponPoseTests.EdgeMargin - point.Y);
                piece = Clip(piece, point => (point.Z * WeaponPoseTests.HalfHeight) + WeaponPoseTests.EdgeMargin + point.Y);
                piece = Clip(piece, point => (point.Z * WeaponPoseTests.HalfWidth) + WeaponPoseTests.EdgeMargin - point.X);
                piece = Clip(piece, point => (point.Z * WeaponPoseTests.HalfWidth) + WeaponPoseTests.EdgeMargin + point.X);
                foreach (var point in piece)
                {
                    Assert.True(
                        point.Z > WeaponPose.NearPlane,
                        $"{file}, {name}, {label}: a point within the picture lies {point.Z:0.0000} m ahead of the eyes, " +
                        $"nearer than the near plane ({WeaponPose.NearPlane} m) - cut open there: {point}.");
                }
            }
        }
    }

    // Keeps the part of a convex polygon where inside(point) >= 0 (Sutherland-Hodgman); inside must be linear.
    private static Vector3[] Clip(Vector3[] polygon, Func<Vector3, float> inside)
    {
        var kept = new List<Vector3>();
        for (var i = 0; i < polygon.Length; i++)
        {
            var from = polygon[i];
            var to = polygon[(i + 1) % polygon.Length];
            var (a, b) = (inside(from), inside(to));
            if (a >= 0f)
            {
                kept.Add(from);
            }

            if (a * b < 0f)
            {
                kept.Add(Vector3.Lerp(from, to, a / (a - b)));
            }
        }

        return [.. kept];
    }
}
