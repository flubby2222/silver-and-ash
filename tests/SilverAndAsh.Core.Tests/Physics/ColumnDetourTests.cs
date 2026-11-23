using System.Numerics;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Physics;

/// <summary>Issue #539: the smallest way round a column for an enemy walking straight at a point.</summary>
public class ColumnDetourTests
{
    private static readonly float Body = BodySizes.Ghoul.Radius;
    private static readonly float Reach = Column.Radius + Body + ColumnDetour.Margin;
    private static readonly Column Middle = Column.AtCell(4, 4);
    private static readonly Level Hall = ColumnCollisionTests.Open(9, 9).WithColumns([Middle]);

    [Fact]
    public void Without_a_column_in_the_way_the_target_stays_the_same()
    {
        var from = new Vector3(1f, 0f, 1f);
        var to = new Vector3(17f, 0.5f, 9f);

        var beside = new Vector3(17f, 0f, 1f);

        Assert.Equal(to, ColumnDetour.Around(ColumnCollisionTests.Open(9, 9), from, to, Body));
        Assert.Equal(beside, ColumnDetour.Around(Hall, from, beside, Body));                           // passes far beside it
    }

    [Fact]
    public void A_column_behind_the_body_or_beyond_the_target_is_not_in_the_way()
    {
        var from = new Vector3(Middle.X - 4f, 0f, Middle.Z);

        Assert.Equal(new Vector3(1f, 0f, Middle.Z), ColumnDetour.Around(Hall, from, new Vector3(1f, 0f, Middle.Z), Body));
        Assert.Equal(new Vector3(Middle.X - 2f, 0f, Middle.Z), ColumnDetour.Around(Hall, from, new Vector3(Middle.X - 2f, 0f, Middle.Z), Body));
    }

    [Fact]
    public void Exactly_in_line_it_heads_past_the_edge_always_on_the_same_side()
    {
        // Walking +X straight at the middle: the heading touches the widened circle, on the clockwise side (-Z) ...
        var east = Heading(new Vector3(Middle.X - 4f, 0f, Middle.Z), new Vector3(Middle.X + 4f, 0f, Middle.Z));
        Assert.True(east.Y < 0f);
        Assert.Equal(Reach, Miss(new Vector2(Middle.X - 4f, Middle.Z), east), 0.001f);

        // ... and walking -X the same way round, which is the other side of the hall (+Z).
        var west = Heading(new Vector3(Middle.X + 4f, 0f, Middle.Z), new Vector3(Middle.X - 4f, 0f, Middle.Z));
        Assert.True(west.Y > 0f);
    }

    [Fact]
    public void Slightly_beside_the_line_it_passes_on_the_side_it_is_on()
    {
        var above = Heading(new Vector3(Middle.X - 4f, 0f, Middle.Z + 0.1f), new Vector3(Middle.X + 4f, 0f, Middle.Z + 0.1f));
        var below = Heading(new Vector3(Middle.X - 4f, 0f, Middle.Z - 0.1f), new Vector3(Middle.X + 4f, 0f, Middle.Z - 0.1f));

        Assert.True(above.Y > 0f);
        Assert.True(below.Y < 0f);
    }

    [Fact]
    public void The_new_target_is_as_far_as_the_old_and_at_its_height()
    {
        var from = new Vector3(Middle.X - 4f, 0f, Middle.Z);
        var to = new Vector3(Middle.X + 4f, 0.25f, Middle.Z);

        var around = ColumnDetour.Around(Hall, from, to, Body);

        Assert.Equal(8f, new Vector2(around.X - from.X, around.Z - from.Z).Length(), 0.001f);
        Assert.Equal(0.25f, around.Y);
    }

    [Fact]
    public void Already_against_the_column_it_heads_across_the_radius()
    {
        var from = new Vector3(Middle.X - Column.Radius - Body, 0f, Middle.Z);
        var heading = Heading(from, new Vector3(Middle.X + 4f, 0f, Middle.Z));

        Assert.Equal(0f, heading.X, 0.001f);
        Assert.Equal(-1f, heading.Y, 0.001f);
    }

    [Fact]
    public void A_waypoint_under_a_column_is_skipped_unless_it_is_the_last()
    {
        var under = new Vector3(Middle.X, 0f, Middle.Z);
        var beyond = new Vector3(Middle.X + 4f, 0f, Middle.Z);
        List<Vector3> waypoints = [under, beyond];

        ColumnDetour.SkipUnreachable(waypoints, Hall, Body);
        Assert.Equal([beyond], waypoints);

        List<Vector3> last = [under];
        ColumnDetour.SkipUnreachable(last, Hall, Body);
        Assert.Equal([under], last);

        Assert.False(ColumnDetour.CanStandAt(Hall, new Vector3(Middle.X + Reach - 0.01f, 0f, Middle.Z), Body));
        Assert.True(ColumnDetour.CanStandAt(Hall, new Vector3(Middle.X + Reach + 0.01f, 0f, Middle.Z), Body));
    }

    // The direction (on the ground plane, length 1) the detour sends a body from from to to.
    private static Vector2 Heading(Vector3 from, Vector3 to)
    {
        var around = ColumnDetour.Around(Hall, from, to, Body);
        return Vector2.Normalize(new Vector2(around.X - from.X, around.Z - from.Z));
    }

    // How close a straight line from start along heading comes to the column's middle.
    private static float Miss(Vector2 start, Vector2 heading)
    {
        var toCentre = Middle.Centre - start;
        return MathF.Abs((heading.X * toCentre.Y) - (heading.Y * toCentre.X));
    }
}
