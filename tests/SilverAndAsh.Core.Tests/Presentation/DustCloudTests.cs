using System.Numerics;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>How much a cloud of dust hides what lies behind it (issue #614, <see cref="DustCloud.Cover"/>).</summary>
public class DustCloudTests
{
    private static readonly Vector3 Eye = Vector3.Zero;
    private static readonly Vector3 Target = new(0f, 0f, 10f);

    [Fact]
    public void A_full_puff_between_eye_and_target_lays_all_its_shells_over_it()
    {
        var puff = new DustPuff(new Vector3(0f, 0f, 5f), 1f, 1f);

        var expected = 1f - DustCloud.Shells.Aggregate(1f, (through, shell) => through * (1f - (shell.Alpha / 255f)));
        Assert.Equal(expected, DustCloud.Cover(Eye, Target, [puff]), 4);
    }

    [Fact]
    public void A_thinner_puff_hides_less_and_two_hide_more_than_one()
    {
        var full = new DustPuff(new Vector3(0f, 0f, 5f), 1f, 1f);
        var thin = full with { Opacity = 0.5f };
        var second = full with { Position = new Vector3(0f, 0f, 7f) };

        Assert.True(DustCloud.Cover(Eye, Target, [thin]) < DustCloud.Cover(Eye, Target, [full]));
        Assert.True(DustCloud.Cover(Eye, Target, [full, second]) > DustCloud.Cover(Eye, Target, [full]));
    }

    [Fact]
    public void Only_the_shells_the_line_of_sight_passes_hide_anything()
    {
        // Off to the side the line passes the outer shell only, farther off none.
        var outerOnly = new DustPuff(new Vector3(0.9f, 0f, 5f), 1f, 1f);
        var aside = new DustPuff(new Vector3(1.1f, 0f, 5f), 1f, 1f);

        Assert.Equal(DustCloud.Shells[0].Alpha / 255f, DustCloud.Cover(Eye, Target, [outerOnly]), 4);
        Assert.Equal(0f, DustCloud.Cover(Eye, Target, [aside]));
    }

    [Fact]
    public void Dust_behind_the_target_or_the_eye_hides_nothing()
    {
        var beyond = new DustPuff(new Vector3(0f, 0f, 12f), 1f, 1f);
        var behindTheEye = new DustPuff(new Vector3(0f, 0f, -3f), 1f, 1f);

        Assert.Equal(0f, DustCloud.Cover(Eye, Target, [beyond, behindTheEye]));
    }

    [Fact]
    public void A_shell_the_eye_is_inside_shows_nothing()
    {
        // The eye 0.8 m from the middle: inside the outer shell (1 m), outside the two inner ones (0.72 m, 0.45 m).
        var around = new DustPuff(new Vector3(0f, 0f, 0.8f), 1f, 1f);

        var inner = 1f - ((1f - (DustCloud.Shells[1].Alpha / 255f)) * (1f - (DustCloud.Shells[2].Alpha / 255f)));
        Assert.Equal(inner, DustCloud.Cover(Eye, Target, [around]), 4);
    }

    [Fact]
    public void A_target_at_the_eye_is_not_hidden()
    {
        Assert.Equal(0f, DustCloud.Cover(Eye, Eye, [new DustPuff(new Vector3(0f, 0f, 0.1f), 1f, 1f)]));
    }
}
