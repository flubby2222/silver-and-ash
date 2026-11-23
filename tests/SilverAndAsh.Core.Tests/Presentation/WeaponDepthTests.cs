using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Issue #624: the depth of the weapons in the hands, under the world's projection as raylib builds it. Their near plane
/// is <see cref="WeaponPose.NearPlane"/> (with the world's the cuffs were cut open), and they stay in front of everything
/// of the world beyond <see cref="WeaponPose.Reach"/>, keeping their own order of depth.
/// </summary>
public class WeaponDepthTests
{
    private const float Tolerance = 1e-4f;

    private static readonly (float A, float B) World = WeaponDepth.Perspective(WeaponDepth.WorldNearPlane, WeaponDepth.WorldFarPlane);

    private static readonly (float A, float B) Weapons = WeaponDepth.Row(World.A, World.B);

    // From the weapons' near plane out to the world's far plane, each step 2 % further.
    private static IEnumerable<float> Distances()
    {
        for (var z = WeaponPose.NearPlane; z <= WeaponDepth.WorldFarPlane; z *= 1.02f)
        {
            yield return z;
        }

        yield return WeaponDepth.WorldFarPlane;
    }

    [Fact]
    public void The_world_row_maps_its_near_plane_to_minus_one_and_its_far_plane_to_one()
    {
        Assert.Equal(-1f, WeaponDepth.At(World.A, World.B, WeaponDepth.WorldNearPlane), Tolerance);
        Assert.Equal(1f, WeaponDepth.At(World.A, World.B, WeaponDepth.WorldFarPlane), Tolerance);
    }

    [Fact]
    public void The_weapons_near_plane_is_their_own_not_the_worlds()
    {
        Assert.Equal(-1f, WeaponDepth.At(Weapons.A, Weapons.B, WeaponPose.NearPlane), Tolerance);
        Assert.True(WeaponPose.NearPlane < WeaponDepth.WorldNearPlane, "The weapons' near plane should lie closer to the eyes than the world's.");
    }

    [Fact]
    public void Every_weapon_depth_lies_in_front_of_the_world_at_the_reach()
    {
        var reach = WeaponDepth.At(World.A, World.B, WeaponPose.Reach);
        foreach (var z in Distances())
        {
            var weapon = WeaponDepth.At(Weapons.A, Weapons.B, z);
            Assert.True(weapon < reach, $"A weapon point {z:0.0000} m ahead has depth {weapon}, not in front of the world at the reach ({reach}).");
        }
    }

    [Fact]
    public void Every_wall_lies_beyond_the_reach()
    {
        Assert.True(WeaponPose.Reach < BodySizes.Player.Radius, "A player keeps its radius from every wall; the reach must stay within it.");
    }

    [Fact]
    public void The_world_beyond_the_reach_lies_no_nearer_than_at_the_reach()
    {
        var reach = WeaponDepth.At(World.A, World.B, WeaponPose.Reach);
        foreach (var z in Distances().Where(z => z >= WeaponPose.Reach))
        {
            Assert.True(WeaponDepth.At(World.A, World.B, z) >= reach, $"The world {z:0.000} m ahead lies in front of the world at the reach.");
        }
    }

    [Fact]
    public void Within_the_weapons_the_farther_point_has_the_greater_depth()
    {
        // Out to 1 m - every weapon, the crate weapons without a model too (about 0.48 m), lies within; further out the
        // steps fall below the precision of a float.
        var distances = Distances().Where(z => z <= 1f).ToArray();
        foreach (var (near, far) in distances.Zip(distances.Skip(1)))
        {
            Assert.True(
                WeaponDepth.At(Weapons.A, Weapons.B, near) < WeaponDepth.At(Weapons.A, Weapons.B, far),
                $"The weapon depth does not grow from {near:0.0000} m to {far:0.0000} m.");
        }
    }
}
