using SilverAndAsh.BotRun;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Bot;

// Issue #807: a ranged bot that does not see its target must not back away from it. In the runs that ended as STUCK a
// ghoul lurked 4.6 to 4.9 m away behind one wall cell; the bot stepped back (too close), then walked up (not in
// sight), and so on for 30 seconds. Real worlds, tick by tick, with Balance.Default.
public class FightStuckTests
{
    private static readonly Balance Balance = Balance.Default;

    private static GameWorld WorldOf(string map)
    {
        var level = LevelParser.Parse(map);
        var cells = Enumerable.Range(0, level.Width).SelectMany(x => Enumerable.Range(0, level.Depth).Select(z => (x, z)))
            .Where(cell => !level[cell.x, cell.z].IsSolid)
            .ToList();
        PlacedRoom[] rooms = [new(0, RoomKind.Start, "start", 0, 0, level.Width, level.Depth, 0, cells, [], [])];
        return new GameWorld(level, 1, Balance, [], rooms);
    }

    // The ghoul lurks 4 m from the player (3.5 m to its edge), one wall cell between them; the way round is open.
    private const string WallBetween = "#############\n#...........#\n#...........#\n#....P#G....#\n#...........#\n#...........#\n#############";

    // The ghoul stands in a closed pocket of wall cells (diagonals too): no way to it, 4 m from the player.
    private const string WallAround = "###########\n#.........#\n#...###...#\n#..P#G#...#\n#...###...#\n#.........#\n###########";

    private static (GameWorld World, Play Play, FightBot Bot) Start(string map)
    {
        var world = WorldOf(map);
        var play = new Play(world, new Report(), "test");
        return (world, play, new FightBot(play, WeaponKind.Pistol, Balance));
    }

    private static FightView RangedView(float target, bool inSight) =>
        new(Balance.Player.MaxHealth, Balance.Player.MaxHealth, true, 100f, false, 6, target, inSight, null, null);

    [Theory]
    [InlineData(4.4f, true, "Retreat")]
    [InlineData(4.4f, false, "Approach")]
    [InlineData(0.5f, false, "Approach")]
    [InlineData(4.6f, true, "Fight")]
    public void ARangedBot_StepsBackOnlyFromATargetItSees(float target, bool inSight, string expected) =>
        Assert.Equal(expected, FightPolicy.Decide(RangedView(target, inSight)).Goal.ToString());

    private static int GhoulHealth(GameWorld world) => world.Ghouls.Single().Health;

    [Fact]
    public void ARangedBot_WalksRoundAWallToAGhoulItCannotSee_AndShootsIt()
    {
        var (world, play, bot) = Start(WallBetween);
        var full = GhoulHealth(world);
        var start = world.Player.Position;

        var firstHit = -1;
        for (var tick = 1; tick <= 900 && firstHit < 0; tick++)
        {
            play.Tick(bot.Next());
            firstHit = GhoulHealth(world) < full ? tick : -1;
        }

        Assert.True(firstHit > 0, "the bot never hurt the ghoul");
        Assert.True(firstHit <= 600, $"the first hit came only at tick {firstHit}");
        Assert.False(world.Player.IsDead);

        // It went round the wall (the way is at least 2 cells up or down), not through it.
        Assert.True(MathF.Abs(world.Player.Position.Z - start.Z) > 1.5f || world.Player.Position.X > start.X + 1f);
    }

    [Fact]
    public void ARangedBot_ThatCannotSeeAGhoul_DoesNotStepBack_WhateverTheDistance()
    {
        var (world, play, bot) = Start(WallBetween);
        var start = world.Player.Position;

        // The ghoul lurks 3.5 m from the player's edge, inside the keep-away distance - and out of sight. In the first
        // second the bot walks towards it (up or down along the wall), never away to the west.
        for (var tick = 0; tick < 60; tick++)
        {
            play.Tick(bot.Next());
            Assert.True(world.Player.Position.X >= start.X - 0.01f, $"tick {tick + 1}: stepped back to {world.Player.Position.X}");
        }
    }

    [Fact]
    public void WithNoWayToTheGhoul_TheBotKeepsTryingAndNothingBreaks()
    {
        var (world, play, bot) = Start(WallAround);
        var full = GhoulHealth(world);

        for (var tick = 0; tick < 600; tick++)
        {
            play.Tick(bot.Next());
        }

        // The ghoul is out of reach and out of sight: it stays whole, the bot lives - the run would end as STUCK, as it should.
        Assert.Equal(full, GhoulHealth(world));
        Assert.False(world.Player.IsDead);
        Assert.Equal(0, world.Statistics.DamageDealt);
    }
}
