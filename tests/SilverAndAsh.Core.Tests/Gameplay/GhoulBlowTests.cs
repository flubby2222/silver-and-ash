using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Issue #574: a ghoul's blow does not reach round a column or the edge of a doorway. Between its wind-up (at
/// <see cref="GhoulBalance.AttackReach"/>) and its blow (up to <see cref="GhoulBalance.HitReach"/>) a player can step
/// behind cover and still be in reach and in front of it - then only the line of sight decides.
/// </summary>
public class GhoulBlowTests
{
    private static readonly int WindUpTicks = SimulationClock.SecondsToTicks(Balance.Default.Ghoul.WindUpTime);

    // 11 x 7 open cells: a player in the middle, a ghoul at the east end, 10 m away along the same row.
    private const string Hall =
        "...........\n" +
        "...........\n" +
        "...........\n" +
        ".....P....G\n" +
        "...........\n" +
        "...........\n" +
        "...........";

    // South of the player, a little towards the ghoul: the ghoul crawls round its north side, winds up north-east of
    // the player, and a step back to the south-east puts the column between them.
    // (The player starts looking along +Z: Move.X = -1 steps towards +X, Move.Y = -1 towards -Z.)
    private static readonly Column Cover = new(12.25f, 6.75f);

    private static readonly Vector2 BackAndAside = Vector2.Normalize(new Vector2(-1f, -1f));
    private static readonly Vector2 Back = new(0f, -1f);
    private const int StepTicks = 15;

    // Two rooms: a wall in row 3 with a doorway in cell (4, 3). The player in the south room, the ghoul at the far end
    // of the north room - out of its sight range, so it only comes once it is woken.
    private const string TwoRooms =
        "........." + "\n" +
        "........." + "\n" +
        ".....P..." + "\n" +
        "####.####" + "\n" +
        "........." + "\n" +
        "........." + "\n" +
        "........." + "\n" +
        "........." + "\n" +
        "........." + "\n" +
        "........." + "\n" +
        "........." + "\n" +
        "........." + "\n" +
        "....G....";

    // First a few steps west, to just below the east edge of the doorway; once the ghoul in the doorway winds up, a step
    // east along the wall puts the edge between them.
    private static readonly Vector2 West = new(1f, 0f);
    private const int ApproachTicks = 14;
    private static readonly Vector2 EastAlongTheWall = Vector2.Normalize(new Vector2(-1f, 1f));
    private const int SideStepTicks = 20;

    [Fact]
    public void A_ghoul_blow_misses_a_player_who_stepped_behind_a_column()
    {
        var world = new GameWorld(LevelParser.Parse(Hall).WithColumns([Cover]));
        world.Ghouls[0].TakeDamage(10, []);

        var struck = StepAsideDuringTheWindUp(world, BackAndAside, StepTicks);

        AssertReachAndAngleWouldHit(world);
        Assert.False(SeesThePlayer(world), "The column must stand between them - else the test proves nothing.");
        Assert.False(struck.Hit, "The blow went round the column.");
        Assert.Equal(world.Player.MaxHealth, world.Player.Health);
    }

    [Fact]
    public void A_ghoul_blow_misses_a_player_who_stepped_behind_a_door_jamb()
    {
        var world = new GameWorld(LevelParser.Parse(TwoRooms));
        for (var tick = 0; tick < ApproachTicks; tick++)
        {
            world.Tick(PlayerInput.None with { Move = West });
        }

        Assert.Equal(GhoulState.Lurking, world.Ghouls[0].State);
        world.Ghouls[0].TakeDamage(10, []);

        var struck = StepAsideDuringTheWindUp(world, EastAlongTheWall, SideStepTicks);

        AssertReachAndAngleWouldHit(world);
        Assert.False(SeesThePlayer(world), "The edge of the doorway must stand between them - else the test proves nothing.");
        Assert.False(struck.Hit, "The blow went round the edge of the doorway.");
        Assert.Equal(world.Player.MaxHealth, world.Player.Health);
    }

    [Fact]
    public void A_ghoul_blow_still_hits_a_player_who_steps_back_in_the_open()
    {
        // Without the column the ghoul crawls straight along the row and winds up due east of the player. The step back
        // to the south-east would leave its angle anyway, so here the player steps straight back (south): still in
        // reach, still in front - and nothing in between.
        var world = new GameWorld(LevelParser.Parse(Hall));
        world.Ghouls[0].TakeDamage(10, []);

        var struck = StepAsideDuringTheWindUp(world, Back, StepTicks);

        AssertReachAndAngleWouldHit(world);
        Assert.True(SeesThePlayer(world));
        Assert.True(struck.Hit, "In the open the step back is too short: the blow must still land.");
        Assert.Equal(world.Player.MaxHealth - Balance.Default.Ghoul.AttackDamage, world.Player.Health);
    }

    /// <summary>
    /// Ticks until the ghoul winds up (it must still see the player then), steps with <paramref name="move"/> for
    /// <paramref name="stepTicks"/> ticks and stands until the blow. Returns the blow.
    /// </summary>
    private static GhoulStruck StepAsideDuringTheWindUp(GameWorld world, Vector2 move, int stepTicks)
    {
        Assert.True(stepTicks < WindUpTicks, "The step must end before the blow.");
        RunUntil(world, PlayerInput.None, 10 * SimulationClock.TicksPerSecond, w => w.Events.Any(e => e is GhoulWindUp));
        Assert.True(SeesThePlayer(world), "At the wind-up the ghoul must still see the player.");

        for (var tick = 0; tick < stepTicks; tick++)
        {
            world.Tick(PlayerInput.None with { Move = move });
            Assert.DoesNotContain(world.Events, e => e is GhoulStruck);
        }

        RunUntil(world, PlayerInput.None, WindUpTicks, w => w.Events.Any(e => e is GhoulStruck));
        return world.Events.OfType<GhoulStruck>().Single();
    }

    /// <summary>Distance, height and angle alone would let the blow land - so only the line of sight decides.</summary>
    private static void AssertReachAndAngleWouldHit(GameWorld world)
    {
        var ghoul = world.Ghouls[0];
        var player = world.Player;
        var flat = (player.Position - ghoul.Position) with { Y = 0f };
        Assert.InRange(flat.Length(), 0f, Balance.Default.Ghoul.HitReach);
        Assert.InRange(MathF.Abs(player.Position.Y - ghoul.Position.Y), 0f, BodySizes.Ghoul.Height);
        var cosine = MathF.Cos(Balance.Default.Ghoul.AttackHalfAngle * MathF.PI / 180f);
        Assert.InRange(Vector3.Dot(Vector3.Normalize(flat), ghoul.Facing), cosine, 1f);
    }

    /// <summary>The line the ghoul sees along: from its eye to the player's eye.</summary>
    private static bool SeesThePlayer(GameWorld world)
    {
        var ghoul = world.Ghouls[0];
        var eye = ghoul.Position + new Vector3(0f, BodySizes.Ghoul.EyeHeight, 0f);
        return LineOfSight.IsClear(world.Level, eye, world.Player.EyePosition);
    }

    private static void RunUntil(GameWorld world, PlayerInput input, int maxTicks, Func<GameWorld, bool> condition)
    {
        for (var tick = 0; tick < maxTicks; tick++)
        {
            world.Tick(input);
            if (condition(world))
            {
                return;
            }
        }

        Assert.Fail($"Nothing happened within {maxTicks} ticks.");
    }
}
