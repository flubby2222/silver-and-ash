using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// The handle and the pommel of the blade and the club stay inside the closed cuff (issue #637). The cuff hangs unturned on
/// the grip (pivot 0, as WeaponView draws it), so whatever lies inside it is hidden in every pose. Before, the pommel
/// reached past the end of the cuff, which is offset to the side; at rest the forearm still hid it, but in the full swing
/// the forearm turns towards the shoulder and a silver (or wooden) knob stood at the lower left of the picture.
/// Checked on the checked-in models.
/// </summary>
public class MeleeHandleInCuffTests
{
    [Theory]
    [InlineData(WeaponKind.SilverBlade)]
    [InlineData(WeaponKind.Club)]
    public void Every_corner_of_the_weapon_behind_the_cuffs_mouth_lies_inside_the_cuff(WeaponKind kind)
    {
        var files = WeaponPose.MeleeModel(kind)!;
        var weapon = WeaponPoseTests.Triangles(files.Weapon).Single(part => part.Name == files.Parts[0].Name);
        var cuff = CuffTriangles(files.Hand);
        var mouth = MouthZ(cuff);

        var behind = weapon.Corners.Where(corner => corner.Z < mouth).Distinct().ToList();
        var outside = behind.Where(corner => !ClubBendTests.Inside(corner, cuff)).ToList();

        // The handle really reaches behind the mouth (the check has something to check).
        Assert.True(behind.Count > 8, $"{files.Weapon}: only {behind.Count} corners behind the cuff's mouth at z {mouth:0.0000}.");
        Assert.True(outside.Count == 0, $"{files.Weapon}: {outside.Count} corners behind the cuff's mouth stick out of the cuff: {string.Join(", ", outside)} (file space).");
    }

    // The cuff's triangles in file space, around its pivot as drawn.
    private static Vector3[][] CuffTriangles(string handFile)
    {
        var (_, pivot, corners, triangles) = WeaponPoseTests.Triangles(handFile).Single(part => part.Name == WeaponPose.Cuff);
        return Enumerable.Range(0, triangles.Length / 3)
            .Select(first => new[]
            {
                corners[triangles[3 * first]] + pivot,
                corners[triangles[(3 * first) + 1]] + pivot,
                corners[triangles[(3 * first) + 2]] + pivot,
            })
            .ToArray();
    }

    // The cuff's mouth: its front ring (the lowest corner of its front centimetre), where the weapon comes out.
    private static float MouthZ(Vector3[][] cuff)
    {
        var corners = cuff.SelectMany(triangle => triangle).ToArray();
        var front = corners.Max(corner => corner.Z);
        return corners.Where(corner => corner.Z > front - 0.01f).Min(corner => corner.Z);
    }
}
