using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

public class WeaponPoseTests
{
    private const float Tolerance = 0.0001f;

    // The placeholder's camera matrix looking along +z: right is -x in the world, up +y, forward +z (it mirrors).
    private static readonly Matrix4x4 CameraLookingAlongZ = new(
        -1f, 0f, 0f, 0f,
        0f, 1f, 0f, 0f,
        0f, 0f, 1f, 0f,
        0f, 0f, 0f, 1f);

    public static TheoryData<float> Kicks() => [0f, 0.5f, 1f];

    [Theory]
    [MemberData(nameof(Kicks))]
    public void A_pistol_pose_with_the_mirrored_camera_is_a_real_turn(float kick)
    {
        var world = WeaponPose.Pistol(kick) * CameraLookingAlongZ;

        Assert.Equal(1f, world.GetDeterminant(), 0.001f);
    }

    [Fact]
    public void A_blade_pose_with_the_mirrored_camera_is_a_real_turn()
    {
        var world = WeaponPose.Blade(swing: 0.6f, windUp: 0.3f, tremble: 0.001f) * CameraLookingAlongZ;

        Assert.Equal(1f, world.GetDeterminant(), 0.001f);
    }

    [Fact]
    public void The_lock_side_of_the_file_ends_up_on_the_right()
    {
        // In the file the lock and the hammer lie at -x (the weapon's right); in camera space right is +x.
        var lockSide = Vector3.Transform(new Vector3(-0.02f, 0f, 0f), WeaponPose.FileToCamera);

        Assert.True(lockSide.X > 0f);
    }

    [Fact]
    public void At_rest_the_pistol_sits_at_its_grip_with_the_barrel_along_the_line_of_sight_and_its_top_towards_the_middle()
    {
        var pose = WeaponPose.Pistol(kick: 0f);

        Assert.Equal(WeaponPose.PistolGrip, Vector3.Transform(Vector3.Zero, pose));
        var barrel = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, pose));
        Assert.Equal(1f, barrel.Z, Tolerance);
        // The pistol is on the right: the middle of the picture lies to its left (-x).
        var top = Vector3.TransformNormal(Vector3.UnitY, pose);
        Assert.Equal(-MathF.Sin(WeaponPose.PistolRollDegrees * MathF.PI / 180f), top.X, Tolerance);
    }

    [Fact]
    public void Recoil_kicks_the_pistol_up_and_back_and_drops_the_hammer_forward()
    {
        var rest = WeaponPose.Pistol(kick: 0f);
        var kicked = WeaponPose.Pistol(kick: 1f);

        Assert.Equal(WeaponPose.PistolGrip + WeaponPose.RecoilShift, Vector3.Transform(Vector3.Zero, kicked));
        var barrel = Vector3.TransformNormal(Vector3.UnitZ, kicked);
        Assert.True(barrel.Y > Vector3.TransformNormal(Vector3.UnitZ, rest).Y + 0.2f, "The barrel should point up after a shot.");

        // The hammer's top swings to the front.
        var top = Vector3.Transform(Vector3.UnitY, Rig.Lift(WeaponPose.CockDegrees(kick: 1f)));
        Assert.True(top.Z > 0.5f);
        Assert.Equal(0f, WeaponPose.CockDegrees(kick: 0f));
    }

    [Fact]
    public void At_rest_the_blade_leans_its_top_towards_the_middle()
    {
        var pose = WeaponPose.Blade(swing: 0f, windUp: 0f, tremble: 0f);

        Assert.Equal(WeaponPose.BladeGrip, Vector3.Transform(Vector3.Zero, pose));
        // The blade is on the left: the middle of the picture lies to its right (+x).
        Assert.True(Vector3.TransformNormal(Vector3.UnitY, pose).X > 0.3f);
    }

    [Fact]
    public void Winding_up_turns_the_tip_left_and_a_swing_far_to_the_right()
    {
        var tip = new Vector3(0f, 0f, 0.15f);
        var rest = Vector3.Transform(tip, WeaponPose.Blade(0f, 0f, 0f)) - WeaponPose.BladeGrip;
        var woundUp = Vector3.Transform(tip, WeaponPose.Blade(0f, 1f, 0f)) - (WeaponPose.BladeGrip + WeaponPose.WindUpShift);
        var swung = Vector3.Transform(tip, WeaponPose.Blade(1f, 0f, 0f)) - (WeaponPose.BladeGrip + WeaponPose.SwingShift);

        Assert.True(woundUp.X < rest.X - 0.05f, "Winding up should turn the tip to the left.");
        Assert.True(swung.X > 0.1f, "The swing should turn the tip far to the right.");
    }

    [Fact]
    public void Shrinking_towards_the_eye_keeps_every_corner_on_its_line_of_sight()
    {
        var corner = Vector3.Transform(new Vector3(0.01f, 0.02f, 0.15f), WeaponPose.Pistol(0.3f));
        var shrunk = Vector3.Transform(corner, Matrix4x4.CreateScale(WeaponPose.ShrinkTowardsEye));

        Assert.Equal(1f, Vector3.Dot(Vector3.Normalize(corner), Vector3.Normalize(shrunk)), Tolerance);
        Assert.True(shrunk.Length() < corner.Length());
    }

    [Theory]
    [InlineData("pistol.glb")]
    [InlineData("pistol_hand.glb")]
    public void Everything_drawn_of_the_pistol_and_its_hand_stays_within_reach_of_the_eyes(string file)
    {
        foreach (var kick in Enumerable.Range(0, 11).Select(step => step / 10f))
        {
            var cock = WeaponPose.CockDegrees(kick);
            AssertWithinReach(file, WeaponPose.Pistol(kick), part => part == WeaponPose.Cock ? Rig.Lift(cock) : Matrix4x4.Identity);
        }
    }

    [Fact]
    public void Everything_drawn_of_the_blade_stays_within_reach_of_the_eyes()
    {
        foreach (var pose in BladePoses(lastSwing: 1f))
        {
            AssertWithinReach("blade.glb", pose, _ => Matrix4x4.Identity);
        }
    }

    [Fact]
    public void Everything_drawn_of_the_blade_hand_stays_within_reach_until_the_swing_turns_the_sleeve_out()
    {
        // Here the forearm stays unturned, as it stands in the file - a pose drawn only at rest. Rigid, it would swing
        // out ahead from about a third of the swing on. As drawn, turned towards the shoulder, the whole swing is
        // checked in SleeveForearmTests (issue #265).
        foreach (var pose in BladePoses(lastSwing: 0.3f))
        {
            AssertWithinReach("blade_hand.glb", pose, _ => Matrix4x4.Identity);
        }
    }

    private static IEnumerable<Matrix4x4> BladePoses(float lastSwing) =>
        from swing in Enumerable.Range(0, 21).Select(step => step / 20f)
        where swing <= lastSwing + Tolerance
        from windUp in (float[])[0f, 0.5f, 1f]
        from tremble in (float[])[-0.0012f, 0.0012f]
        select WeaponPose.Blade(swing, windUp, tremble);

    [Fact]
    public void Winding_up_the_blade_cuts_no_sleeve_open_at_the_near_plane_within_the_picture()
    {
        // Issue #256: fully wound up, the near plane cut the long sleeve open in the lower left of the picture.
        foreach (var windUp in (float[])[0.5f, 1f])
        {
            foreach (var tremble in (float[])[-0.0012f, 0.0012f])
            {
                var cuts = Placed("blade_hand.glb", WeaponPose.Blade(0f, windUp, tremble), _ => Matrix4x4.Identity)
                    .SelectMany(part => part.Polygons.SelectMany(polygon => polygon.Zip(polygon.Skip(1).Append(polygon[0]))))
                    .Where(side => (side.First.Z - WeaponPose.NearPlane) * (side.Second.Z - WeaponPose.NearPlane) < 0f)
                    .Select(side => Vector3.Lerp(side.First, side.Second, (WeaponPose.NearPlane - side.First.Z) / (side.Second.Z - side.First.Z)))
                    .Where(cut => MathF.Abs(cut.X) <= cut.Z * HalfWidth && MathF.Abs(cut.Y) <= cut.Z * HalfHeight)
                    .ToList();

                Assert.True(cuts.Count == 0, $"Wound up {windUp}: the near plane cuts the sleeve open at {cuts.Count} points within the picture.");
            }
        }
    }

    // Vertical field of view 70 degrees (LookSettings.FieldOfViewDegrees in the game) and the 16:9 window (1280 x 720
    // in Program.cs): the slope of the picture's edges in camera space.
    internal static readonly float HalfHeight = MathF.Tan(35f * MathF.PI / 180f);
    internal static readonly float HalfWidth = HalfHeight * 16f / 9f;

    // The picture's edges are widened by this much (metres) for bob, sway and the landing dip, which move the weapons a
    // little after the pose.
    internal const float EdgeMargin = 0.02f;

    // Every triangle of the checked-in model, turned around its part's pivot as the rig does (all parts hang on the
    // grip at 0, 0, 0), through the pose and shrunk towards the eye.
    private static IEnumerable<(string Name, List<Vector3[]> Polygons)> Placed(string file, Matrix4x4 pose, Func<string, Matrix4x4> turnOf)
    {
        var shrink = Matrix4x4.CreateScale(WeaponPose.ShrinkTowardsEye);
        foreach (var (name, pivot, corners, triangles) in Triangles(file))
        {
            var frame = turnOf(name) * Matrix4x4.CreateTranslation(pivot) * pose * shrink;
            var placed = corners.Select(corner => Vector3.Transform(corner, frame)).ToArray();
            var polygons = new List<Vector3[]>();
            for (var first = 0; first < triangles.Length; first += 3)
            {
                polygons.Add([placed[triangles[first]], placed[triangles[first + 1]], placed[triangles[first + 2]]]);
            }

            yield return (name, polygons);
        }
    }

    // Of each triangle only the piece beyond the near plane and within the picture's edges is drawn
    // (the sleeves are long on purpose and run out of the picture). That piece is the triangle clipped by those
    // planes; its farthest point is one of its corners. None lies farther from the eye (the camera-space origin) than
    // the reach. Only the parts drawn with the weapons are checked: all of them, or those named by drawnWithWeapons (the
    // whip's lash is drawn in the world's depth, issue #786).
    internal static void AssertWithinReach(string file, Matrix4x4 pose, Func<string, Matrix4x4> turnOf, Func<string, bool>? drawnWithWeapons = null)
    {
        foreach (var (name, polygons) in Placed(file, pose, turnOf))
        {
            if (drawnWithWeapons is not null && !drawnWithWeapons(name))
            {
                continue;
            }

            foreach (var triangle in polygons)
            {
                var drawn = Clip(triangle, point => point.Z - WeaponPose.NearPlane);
                drawn = Clip(drawn, point => (point.Z * HalfHeight) + EdgeMargin - point.Y);
                drawn = Clip(drawn, point => (point.Z * HalfHeight) + EdgeMargin + point.Y);
                drawn = Clip(drawn, point => (point.Z * HalfWidth) + EdgeMargin - point.X);
                drawn = Clip(drawn, point => (point.Z * HalfWidth) + EdgeMargin + point.X);
                foreach (var point in drawn)
                {
                    AssertNear(file, name, point);
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

    private static void AssertNear(string file, string part, Vector3 point)
    {
        var distance = point.Length();
        Assert.True(
            distance <= WeaponPose.Reach,
            $"{file}, part '{part}': a drawn point lies {distance:0.000} m from the eyes, more than {WeaponPose.Reach} m.");
    }

    // The corners and triangles of every part with a mesh, read straight from the .glb (the files are checked by
    // GlbScene and its tests; here only what the test needs).
    internal static IEnumerable<(string Name, Vector3 Pivot, Vector3[] Corners, int[] Triangles)> Triangles(string file)
    {
        var bytes = File.ReadAllBytes(RepositoryFiles.Find(Path.Combine("assets", "models", file)));
        var jsonLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12));
        using var json = JsonDocument.Parse(bytes.AsMemory(20, jsonLength));
        var data = bytes.AsSpan(20 + jsonLength + 8).ToArray();
        var root = json.RootElement;
        var accessors = root.GetProperty("accessors");
        var views = root.GetProperty("bufferViews");
        var result = new List<(string, Vector3, Vector3[], int[])>();
        foreach (var node in root.GetProperty("nodes").EnumerateArray())
        {
            if (!node.TryGetProperty("mesh", out var mesh))
            {
                continue;
            }

            var primitive = root.GetProperty("meshes")[mesh.GetInt32()].GetProperty("primitives")[0];
            var positions = accessors[primitive.GetProperty("attributes").GetProperty("POSITION").GetInt32()];
            var indices = accessors[primitive.GetProperty("indices").GetInt32()];
            var pivot = node.TryGetProperty("translation", out var t)
                ? new Vector3(t[0].GetSingle(), t[1].GetSingle(), t[2].GetSingle())
                : Vector3.Zero;
            var corners = new Vector3[positions.GetProperty("count").GetInt32()];
            var at = Start(positions, views);
            for (var i = 0; i < corners.Length; i++)
            {
                corners[i] = new Vector3(
                    BitConverter.ToSingle(data, at + (12 * i)),
                    BitConverter.ToSingle(data, at + (12 * i) + 4),
                    BitConverter.ToSingle(data, at + (12 * i) + 8));
            }

            var triangles = new int[indices.GetProperty("count").GetInt32()];
            var wide = indices.GetProperty("componentType").GetInt32() == 5125;
            at = Start(indices, views);
            for (var i = 0; i < triangles.Length; i++)
            {
                triangles[i] = wide ? (int)BitConverter.ToUInt32(data, at + (4 * i)) : BitConverter.ToUInt16(data, at + (2 * i));
            }

            result.Add((node.GetProperty("name").GetString()!, pivot, corners, triangles));
        }

        return result;
    }

    private static int Start(JsonElement accessor, JsonElement views)
    {
        var view = views[accessor.GetProperty("bufferView").GetInt32()];
        return (view.TryGetProperty("byteOffset", out var viewOffset) ? viewOffset.GetInt32() : 0)
            + (accessor.TryGetProperty("byteOffset", out var offset) ? offset.GetInt32() : 0);
    }
}
