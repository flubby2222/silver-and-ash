using System.Numerics;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Issue #750, art bible rule 21: the frames of the normal and the locked door are closed solids, node by node. The same
/// check as tools/models/check_closed.py (corners at the same place are one, every edge belongs to exactly two triangles),
/// and the look of the solid: each face reaches from the door plane to the wall and its triangles face outwards.
/// </summary>
public class DoorFrameClosedTests
{
    private const float Tolerance = 0.0001f;

    public static TheoryData<DoorKind, TextureVariant> ClosedFrames()
    {
        var data = new TheoryData<DoorKind, TextureVariant>();
        foreach (var kind in new[] { DoorKind.Normal, DoorKind.Locked })
        {
            foreach (var variant in new[] { TextureVariant.A, TextureVariant.B, TextureVariant.C })
            {
                data.Add(kind, variant);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ClosedFrames))]
    public void Every_node_of_a_door_frame_is_closed_with_no_edge_of_more_than_two_faces(DoorKind kind, TextureVariant variant)
    {
        var file = DoorFrameLook.FileName(kind, variant);
        foreach (var part in new[] { DoorFrameLook.Front, DoorFrameLook.Back })
        {
            var (open, shared) = EdgeCounts(file, part);

            Assert.True(open == 0, $"{part} of {file} has {open} open edges (an edge with one face).");
            Assert.True(shared == 0, $"{part} of {file} has {shared} edges with more than two faces.");
        }
    }

    [Theory]
    [MemberData(nameof(ClosedFrames))]
    public void A_door_frame_is_a_solid_from_the_door_plane_to_the_wall_with_its_faces_outwards(DoorKind kind, TextureVariant variant)
    {
        var file = DoorFrameLook.FileName(kind, variant);
        var reach = Level.CellSize / 2f;
        foreach (var (part, side) in new[] { (DoorFrameLook.Front, 1f), (DoorFrameLook.Back, -1f) })
        {
            var triangles = Triangles(file, part);
            var depths = triangles.SelectMany(t => t).Select(corner => corner.Z * side).ToList();

            // Closed means it has a back: the solid begins in the door plane itself, not in front of it.
            Assert.Equal(0f, depths.Min(), Tolerance);
            Assert.Equal(reach, depths.Max(), Tolerance);

            // Outwards: positive volume (sum of the signed tetrahedra to the origin). Turned inside out it would be negative
            // and Raylib, which culls the back of a face, would draw the inside of the frame.
            var volume = triangles.Sum(t => Vector3.Dot(t[0], Vector3.Cross(t[1], t[2]))) / 6f;
            Assert.True(volume > 0.5f, $"{part} of {file} has the volume {volume} m3: its faces point inwards or it is not a solid.");
        }
    }

    private static (int Open, int Shared) EdgeCounts(string file, string part)
    {
        var corners = new Dictionary<(long, long, long), int>();
        var edges = new Dictionary<(int, int), int>();
        foreach (var triangle in Triangles(file, part))
        {
            var ids = triangle.Select(corner => corners.TryAdd(Key(corner), corners.Count) ? corners.Count - 1 : corners[Key(corner)]).ToArray();
            if (ids[0] == ids[1] || ids[1] == ids[2] || ids[0] == ids[2])
            {
                continue;
            }

            for (var i = 0; i < 3; i++)
            {
                var (a, b) = (ids[i], ids[(i + 1) % 3]);
                var edge = a < b ? (a, b) : (b, a);
                edges[edge] = edges.GetValueOrDefault(edge) + 1;
            }
        }

        return (edges.Values.Count(count => count == 1), edges.Values.Count(count => count > 2));
    }

    private static (long, long, long) Key(Vector3 corner) =>
        ((long)Math.Round(corner.X * 1e5), (long)Math.Round(corner.Y * 1e5), (long)Math.Round(corner.Z * 1e5));

    private static List<Vector3[]> Triangles(string file, string part)
    {
        var (_, pivot, corners, indices) = WeaponPoseTests.Triangles(file).Single(p => p.Name == part);
        var result = new List<Vector3[]>();
        for (var first = 0; first < indices.Length; first += 3)
        {
            result.Add([corners[indices[first]] + pivot, corners[indices[first + 1]] + pivot, corners[indices[first + 2]] + pivot]);
        }

        return result;
    }
}
