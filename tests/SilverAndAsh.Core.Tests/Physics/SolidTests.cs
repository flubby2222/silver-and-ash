using System.Numerics;
using SilverAndAsh.Core.Physics;

namespace SilverAndAsh.Core.Tests.Physics;

/// <summary>
/// Issue #694: the one resolve over <see cref="ISolid"/> pushes a body out of a solid it knows only through the
/// interface. The level does not hold such solids yet (that is a later step of the issue), so these tests call the
/// resolve that <see cref="CharacterCollision.Move"/> uses for bodies, boxes, columns and portal stone directly.
/// </summary>
public class SolidTests
{
    private const float Radius = 0.35f;
    private const float Height = 1.8f;
    private const float StepHeight = 0.4f;
    private const int Passes = 8;

    [Fact]
    public void A_solid_known_only_through_the_interface_pushes_a_body_out_and_takes_the_speed_into_it()
    {
        var wall = new TestWall(5f);
        var start = new Vector3(4.9f, 0f, 3f);
        var velocity = new Vector3(2f, 0f, 1f);

        var (position, after) = CharacterCollision.ResolveSolidOverlaps([wall], start, velocity, Radius, Height, StepHeight, Passes);

        Assert.Equal(5f - Radius, position.X, 0.0001f);
        Assert.Equal(start.Z, position.Z);
        Assert.Equal(0f, after.X);
        Assert.Equal(velocity.Z, after.Z);
    }

    [Fact]
    public void A_body_away_from_the_solid_stays_where_it_is()
    {
        var wall = new TestWall(5f);
        var start = new Vector3(4f, 0f, 3f);
        var velocity = new Vector3(2f, 0f, 1f);

        var (position, after) = CharacterCollision.ResolveSolidOverlaps([wall], start, velocity, Radius, Height, StepHeight, Passes);

        Assert.Equal(start, position);
        Assert.Equal(velocity, after);
        Assert.Equal(1, wall.Calls);
    }

    [Fact]
    public void After_a_pass_that_pushed_the_next_pass_asks_again_and_one_without_a_push_ends_it()
    {
        var wall = new TestWall(5f);

        CharacterCollision.ResolveSolidOverlaps([wall], new Vector3(4.9f, 0f, 3f), Vector3.Zero, Radius, Height, StepHeight, Passes);

        Assert.Equal(2, wall.Calls);
    }

    [Fact]
    public void A_solid_that_pushes_in_every_pass_is_asked_no_more_than_the_passes()
    {
        // Too narrow for the body: whichever side it is pushed to, the other wall holds it.
        var left = new TestWall(5f);
        var right = new TestWall(5f, facesLeft: false);

        CharacterCollision.ResolveSolidOverlaps([left, right], new Vector3(5f, 0f, 3f), Vector3.Zero, Radius, Height, StepHeight, 3);

        Assert.Equal(3, left.Calls);
        Assert.Equal(3, right.Calls);
    }

    [Fact]
    public void No_list_of_solids_leaves_the_body_alone()
    {
        var start = new Vector3(4.9f, 0f, 3f);
        var velocity = new Vector3(2f, 0f, 1f);

        var (position, after) = CharacterCollision.ResolveSolidOverlaps<TestWall>(null, start, velocity, Radius, Height, StepHeight, Passes);

        Assert.Equal(start, position);
        Assert.Equal(velocity, after);
    }

    /// <summary>
    /// A solid only this test knows: an endless wall across X at <see cref="X"/>, solid on the side of larger X
    /// (or, with facesLeft false, of smaller X). It counts how often it is asked.
    /// </summary>
    private sealed class TestWall(float x, bool facesLeft = true) : ISolid
    {
        public float X { get; } = x;

        public int Calls { get; private set; }

        public bool PushOut(ref Vector3 position, ref Vector3 velocity, float radius, float height, float stepHeight)
        {
            Calls++;
            var depth = facesLeft ? position.X + radius - X : X + radius - position.X;
            if (depth <= 0f)
            {
                return false;
            }

            var direction = facesLeft ? -1f : 1f;
            position.X += direction * depth;
            velocity.X = facesLeft ? MathF.Min(velocity.X, 0f) : MathF.Max(velocity.X, 0f);
            return true;
        }
    }
}
