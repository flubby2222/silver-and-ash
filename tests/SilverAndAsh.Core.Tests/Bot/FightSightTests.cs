using System.Numerics;
using SilverAndAsh.BotRun;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Bot;

// Issue #809: the fight bot knows only what a player at the screen knows - the enemies and keys it has a clear line of
// sight to, the last spot where it saw an enemy, and that a shut room is not cleared. Real worlds, tick by tick, with
// Balance.Default (the ghouls stand still: speed 0, so what the bot finds, it finds by looking).
public class FightSightTests
{
    private static readonly Balance Standing = Balance.Default with
    {
        Ghoul = Balance.Default.Ghoul with { Speed = 0f },
        Keys = Balance.Default.Keys with { EnemyChance = 1f },
    };

    // Every map gets a closed 3x3 boss room on its right (the route needs one); the rest is one room of the kind asked.
    private static (GameWorld World, Play Play) Start(
        string[] rows, RoomKind kind = RoomKind.Start, (int X, int Z)[]? ghoulSlots = null, int? roomColumns = null)
    {
        var left = rows[0].Length;
        var all = rows.Select((row, z) => row + (z is >= 1 and <= 3 ? "#...#" : "#####")).ToArray();
        var level = LevelParser.Parse(string.Join('\n', all));
        var open = Enumerable.Range(0, level.Width).SelectMany(x => Enumerable.Range(0, level.Depth).Select(z => (x, z)))
            .Where(cell => !level[cell.x, cell.z].IsSolid)
            .ToList();
        var main = open.Where(cell => cell.x < (roomColumns ?? left)).ToList();
        var boss = open.Where(cell => cell.x >= left).ToList();
        PlacedRoom[] rooms = kind == RoomKind.Start
            ? [new(0, kind, "t", 0, 0, left, level.Depth, 0, main, ghoulSlots ?? [], []), new(1, RoomKind.Boss, "boss", left, 0, 5, level.Depth, 1, boss, [], [])]
            : [new(0, RoomKind.Start, "s", 0, 0, 0, 0, 0, [], [], []), new(1, kind, "t", 0, 0, left, level.Depth, 1, main, ghoulSlots ?? [], []), new(2, RoomKind.Boss, "boss", left, 0, 5, level.Depth, 2, boss, [], [])];
        var world = new GameWorld(level, 1, Standing, [], rooms);
        return (world, new Play(world, new Report(), "test"));
    }

    // A dividing wall with one gap in the bottom row; the ghoul (G) is on the far side of it.
    private static readonly string[] Wall =
    [
        "#############",
        "#.....#.....#",
        "#.P...#.G...#",
        "#.....#.....#",
        "#.....#.....#",
        "#...........#",
        "#############",
    ];

    // A ghoul closed in by walls on all sides, 18 m from the player: it cannot be seen and cannot come.
    private static readonly string[] Pocket =
    [
        "###############",
        "#.............#",
        "#.............#",
        "#.P.......###.#",
        "#.........#G#.#",
        "#.........###.#",
        "#.............#",
        "###############",
    ];

    // The room the tests stand in: the one with the player's cells (the boss room and an empty start room do not count).
    private static RoomState TheRoom(GameWorld world) => world.Rooms.Single(room => room.Kind is not RoomKind.Boss && room.Cells.Count > 0);

    private static Vector3 Centre(GameWorld world, int x, int z) => world.Level.CellCenter(x, z);

    private static Ghoul TheGhoul(GameWorld world) => world.Ghouls.Single();

    [Fact]
    public void AGhoulBehindAWall_InTheSameRoom_IsNoTarget()
    {
        var (world, _) = Start(Wall);
        var memory = new FightMemory(world);

        memory.Look(world.Player, TheRoom(world));

        Assert.Empty(memory.Seen);
    }

    [Fact]
    public void WhenItComesInSight_TheGhoulIsATarget()
    {
        var (world, play) = Start(Wall);
        var memory = new FightMemory(world);
        play.PlaceAt((7, 5));

        memory.Look(world.Player, TheRoom(world));

        Assert.Same(TheGhoul(world), Assert.Single(memory.Seen));
    }

    [Theory]
    [InlineData(8, true)]
    [InlineData(10, false)]
    public void AnEnemyOutsideTheRoom_IsSeenOnlyUpToTheSightRange(int cellX, bool seen)
    {
        // The room is the first 6 columns; the ghoul stands out in the long hall, 10 m or 14 m from the player (a cell is 2 m).
        var (world, _) = Start(
            ["####################", "#..................#", "#..P.......G.......#", "#..................#", "#..................#"],
            roomColumns: 6);
        var memory = new FightMemory(world);
        Play.PlaceEnemyAt(TheGhoul(world), Centre(world, cellX, 2));

        memory.Look(world.Player, TheRoom(world));

        Assert.Equal(seen, memory.Seen.Count == 1);
    }

    // Seen at one spot, then gone (back in the closed pocket): what the bot remembers is the spot it saw.
    private static (GameWorld World, Play Play, FightMemory Memory, Vector3 Spot, Vector3 Pocket) SeenThenGone()
    {
        var (world, play) = Start(Pocket);
        var memory = new FightMemory(world);
        var ghoul = TheGhoul(world);
        var pocket = ghoul.Body.Position;
        var spot = Centre(world, 2, 6);
        Play.PlaceEnemyAt(ghoul, spot);
        memory.Look(world.Player, TheRoom(world));
        Assert.Single(memory.Seen);
        Play.PlaceEnemyAt(ghoul, pocket);
        memory.Look(world.Player, TheRoom(world));
        return (world, play, memory, spot, pocket);
    }

    [Fact]
    public void AfterTheGhoulIsGone_TheBotRemembersTheLastSpotItSaw_NotTheRealOne()
    {
        var (world, _, memory, spot, pocket) = SeenThenGone();

        var remembered = memory.LastSeenSpot(world.Player);

        Assert.Empty(memory.Seen);
        Assert.NotNull(remembered);
        Assert.True(Vector3.Distance(remembered.Value, spot) < 0.1f, $"remembered {remembered}, saw it at {spot}");
        Assert.True(Vector3.Distance(remembered.Value, pocket) > 5f);
    }

    [Fact]
    public void TheBotWalksToTheSpotItSawTheGhoul_NotToWhereItReallyIs()
    {
        var (world, play) = Start(Pocket);
        var bot = new FightBot(play, WeaponKind.SilverBlade, Standing);
        var ghoul = TheGhoul(world);
        var pocket = ghoul.Body.Position;
        Play.PlaceEnemyAt(ghoul, Centre(world, 2, 6));
        play.Tick(bot.Next());
        Play.PlaceEnemyAt(ghoul, pocket);
        var start = world.Player.Position;

        // The spot is 6 m south of the player; the pocket is 18 m east.
        for (var tick = 0; tick < 20; tick++)
        {
            play.Tick(bot.Next());
        }

        Assert.True(world.Player.Position.Z > start.Z + 0.5f, $"went from {start} to {world.Player.Position}");
        Assert.True(MathF.Abs(world.Player.Position.X - start.X) < 0.5f, $"went from {start} to {world.Player.Position}");
    }

    [Theory]
    [InlineData(0.9f, false)]
    [InlineData(1.1f, true)]
    public void ASpotIsForgotten_WhenTheBotStandsOnIt(float away, bool stillKnown)
    {
        var (world, play, memory, spot, _) = SeenThenGone();
        play.PlaceAt(spot + new Vector3(away, 0f, 0f));

        memory.Look(world.Player, TheRoom(world));

        Assert.Equal(stillKnown, memory.LastSeenSpot(world.Player) is not null);
    }

    [Fact]
    public void AGhoulSeenDying_IsNotRememberedAndNotGoneBackTo()
    {
        var (world, play) = Start(Pocket);
        var memory = new FightMemory(world);
        var ghoul = TheGhoul(world);
        Play.PlaceEnemyAt(ghoul, Centre(world, 2, 6));
        memory.Look(world.Player, TheRoom(world));
        Assert.NotNull(memory.LastSeenSpot(world.Player));

        ghoul.TakeDamage(1_000_000, []);
        play.Tick(PlayerInput.None);
        memory.Look(world.Player, TheRoom(world));

        Assert.Empty(memory.Seen);
        Assert.Null(memory.LastSeenSpot(world.Player));
    }

    // A key lies at the ghoul's place when it dies (the balance above makes every kill drop one).
    private static Loot KeyAt(GameWorld world, Play play, int x, int z)
    {
        var ghoul = TheGhoul(world);
        Play.PlaceEnemyAt(ghoul, Centre(world, x, z));
        ghoul.TakeDamage(1_000_000, []);
        play.Ticks(30, PlayerInput.None);
        return world.Loot.Single(each => each.Kind == LootKind.Key);
    }

    [Fact]
    public void AKeyBehindAWall_IsNotKnown_AndNotWalkedTo()
    {
        var (world, play) = Start(Wall);
        var key = KeyAt(world, play, 9, 2);
        var memory = new FightMemory(world);
        var bot = new FightBot(play, WeaponKind.SilverBlade, Standing);
        var start = world.Player.Position;

        memory.Look(world.Player, TheRoom(world));
        for (var tick = 0; tick < 20; tick++)
        {
            play.Tick(bot.Next());
        }

        Assert.False(memory.KnowsKey(key));
        Assert.True(Play.Distance(world.Player.Position, start) < 0.01f, "the bot walked to a key it cannot see");
    }

    [Fact]
    public void AKeyInSight_IsKnown_AndStaysKnownOutOfSight()
    {
        var (world, play) = Start(Wall);
        var key = KeyAt(world, play, 9, 2);
        var memory = new FightMemory(world);
        play.PlaceAt((7, 5));

        memory.Look(world.Player, TheRoom(world));
        Assert.True(memory.KnowsKey(key));

        play.PlaceAt((2, 2));
        memory.Look(world.Player, TheRoom(world));
        Assert.True(memory.KnowsKey(key));
    }

    [Fact]
    public void ABotThatKnowsAKey_WalksToIt()
    {
        var (world, play) = Start(Wall);
        KeyAt(world, play, 9, 2);
        var bot = new FightBot(play, WeaponKind.SilverBlade, Standing);
        play.PlaceAt((7, 5));

        for (var tick = 0; tick < 600 && world.Player.Keys == 0; tick++)
        {
            play.Tick(bot.Next());
        }

        Assert.Equal(1, world.Player.Keys);
    }

    // The key lies behind a step 0.75 m high, 5 m from the player: the line to a point at the floor would graze the step,
    // the line to the point 0.9 m above the floor passes over it.
    [Fact]
    public void AKeyBehindALowStep_IsSeenOverIt()
    {
        var (world, play) = Start(
            ["#############", "#.P...3.G...#", "#...........#", "#...........#", "#...........#", "#...........#", "#############"]);
        var key = KeyAt(world, play, 7, 1);
        var memory = new FightMemory(world);
        play.PlaceAt((2, 1));

        memory.Look(world.Player, TheRoom(world));

        Assert.True(memory.KnowsKey(key));
    }

    // A shut room that is not cleared: its ghoul stands behind the dividing wall, out of sight.
    private static (GameWorld World, Play Play) Arena() =>
        Start(Wall, RoomKind.Combat);

    [Fact]
    public void TheSearchOfARoom_FindsAGhoulBehindAWall()
    {
        var (world, play) = Arena();
        var bot = new FightBot(play, WeaponKind.SilverBlade, Standing);
        var ghoul = TheGhoul(world);

        var hurt = false;
        for (var tick = 0; tick < 1800 && !hurt; tick++)
        {
            play.Tick(bot.Next());
            hurt = ghoul.Health < Standing.Ghoul.MaxHealth;
        }

        Assert.True(hurt, $"the bot never found the ghoul; it ended at {world.Player.Position}");
    }

    [Fact]
    public void ARoomThatIsCleared_IsNotSearched()
    {
        var (world, play) = Arena();
        var memory = new FightMemory(world);
        TheGhoul(world).TakeDamage(1_000_000, []);
        play.Tick(PlayerInput.None);
        Assert.True(TheRoom(world).IsCleared);

        Assert.Null(memory.SearchPoint(world.Player, TheRoom(world)));
    }

    [Fact]
    public void ARoomThatIsNoArena_IsNotSearched()
    {
        var (world, _) = Start(Wall);

        Assert.Null(new FightMemory(world).SearchPoint(world.Player, TheRoom(world)));
    }

    // The search looks at the middle of the room first; every 10 ticks it counts what it sees as searched.
    [Theory]
    [InlineData(9, true)]
    [InlineData(10, false)]
    public void TheSearch_StartsInTheMiddle_UntilTheFirstLookRound(int ticks, bool inTheMiddle)
    {
        var (world, play) = Arena();
        var memory = new FightMemory(world);
        play.Ticks(ticks, PlayerInput.None);

        var point = memory.SearchPoint(world.Player, TheRoom(world))!.Value;

        // The middle cell is (5, 3), west of the dividing wall and in sight of the player at (2, 2); after the look
        // round the nearest cell it cannot see lies east of the wall.
        Assert.Equal(inTheMiddle, point.X < Centre(world, 6, 3).X);
    }

    [Theory]
    [InlineData(299, true)]
    [InlineData(300, false)]
    public void ACellOfTheSearch_IsGivenUpAfterThreeHundredTicks(int later, bool same)
    {
        var (world, play) = Arena();
        var memory = new FightMemory(world);
        play.Ticks(10, PlayerInput.None);
        var first = memory.SearchPoint(world.Player, TheRoom(world))!.Value;
        play.Ticks(later, PlayerInput.None);

        var again = memory.SearchPoint(world.Player, TheRoom(world))!.Value;

        Assert.Equal(same, again == first);
    }

    [Theory]
    [InlineData(0.9f, false)]
    [InlineData(1.1f, true)]
    public void ACellOfTheSearch_CountsAsReached_WhenTheBotStandsOnIt(float away, bool same)
    {
        var (world, play) = Arena();
        var memory = new FightMemory(world);
        play.Ticks(15, PlayerInput.None);
        var first = memory.SearchPoint(world.Player, TheRoom(world))!.Value;
        play.PlaceAt(first + new Vector3(away, 0f, 0f));

        var again = memory.SearchPoint(world.Player, TheRoom(world))!.Value;

        Assert.Equal(same, again == first);
    }

    // Issue #809 (hint 273): a ranged bot that sees the target closer than the keep-away distance keeps stepping back.
    [Fact]
    public void ARangedBot_ThatSeesAGhoulCloserThanKeepAway_WalksAway()
    {
        var (world, play) = Start(
            ["#############", "#...........#", "#.....P.....#", "#.G.........#", "#...........#", "#...........#", "#############"]);
        var bot = new FightBot(play, WeaponKind.Pistol, Standing);
        var ghoul = TheGhoul(world);
        Play.PlaceEnemyAt(ghoul, world.Player.Position + new Vector3(3f, 0f, 0f));
        var before = Play.Distance(world.Player.Position, ghoul.Body.Position);

        for (var tick = 0; tick < 30; tick++)
        {
            play.Tick(bot.Next());
        }

        var after = Play.Distance(world.Player.Position, ghoul.Body.Position);
        Assert.True(before - ghoul.Body.Radius < FightPolicy.KeepAway);
        Assert.True(after > before + 0.5f, $"the distance went from {before} to {after}");
    }

    private static FightView Quiet(bool hasSearch, bool hasKey) =>
        new(Standing.Player.MaxHealth, Standing.Player.MaxHealth, false, 2f, false, 6, null, false, null, hasKey ? 3f : null, HasSearch: hasSearch);

    [Theory]
    [InlineData(true, false, "Search")]
    [InlineData(true, true, "Key")]
    [InlineData(false, false, "Route")]
    public void NothingToFight_KeyBeforeSearchBeforeRoute(bool hasSearch, bool hasKey, string expected) =>
        Assert.Equal(expected, FightPolicy.Decide(Quiet(hasSearch, hasKey)).Goal.ToString());

    [Fact]
    public void ATargetOutOfSight_ComesBeforeTheSearch() =>
        Assert.Equal(
            FightGoal.Approach,
            FightPolicy.Decide(Quiet(true, false) with { TargetDistance = 8f, TargetInSight = false }).Goal);
}
