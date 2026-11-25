using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Issue #506: the status display above an enemy. The effect goes on through the world (<see cref="GameWorld.ApplyStatus"/>)
/// and the world runs tick by tick; what the display shows comes from <see cref="EnemyStatusReadout.Of(GameWorld)"/>.
/// </summary>
public class EnemyStatusReadoutTests
{
    private static readonly int MarkTicks = SimulationClock.SecondsToTicks(Balance.Default.StatusEffects.Marked.Seconds);

    private const string Corridor = "P\n.\n.\n.\nG";

    // Kinds the game does not have yet, for order and limit: the table of looks is passed in, the kinds are cast.
    private static readonly StatusKind Second = (StatusKind)1;
    private static readonly StatusKind Third = (StatusKind)2;
    private static readonly StatusKind Fourth = (StatusKind)3;

    private static readonly IReadOnlyList<StatusLook> FourLooks =
    [
        new(StatusKind.Marked, EffectColours.Ashlar, (209, 193, 200), ShowsTimer: true),
        new(Second, EffectColours.Stone, (151, 127, 137), ShowsTimer: true),
        new(Third, EffectColours.Stone, (113, 97, 104), ShowsTimer: false),
        new(Fourth, EffectColours.Stone, (61, 52, 56), ShowsTimer: true),
    ];

    private static (GameWorld World, Ghoul Ghoul) MarkedGhoul()
    {
        var world = new GameWorld(LevelParser.Parse(Corridor));
        var ghoul = world.Ghouls[0];
        Assert.True(world.ApplyStatus(ghoul, StatusKind.Marked, world.Player));
        return (world, ghoul);
    }

    [Theory]
    [InlineData(1, "0.1s")]
    [InlineData(6, "0.1s")]
    [InlineData(7, "0.2s")]
    [InlineData(24, "0.4s")]
    [InlineData(60, "1.0s")]
    [InlineData(61, "1.1s")]
    [InlineData(180, "3.0s")]
    [InlineData(661, "11.1s")]
    public void The_timer_shows_the_ticks_left_in_seconds_rounded_up_to_a_tenth(int ticks, string text)
    {
        Assert.Equal(text, EnemyStatusReadout.TimerText(ticks));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_timer_without_ticks_left_is_an_error(int ticks)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EnemyStatusReadout.TimerText(ticks));
    }

    [Fact]
    public void A_marked_ghoul_shows_the_mark_with_its_colour_and_its_time_left_tick_by_tick()
    {
        var (world, ghoul) = MarkedGhoul();

        var readout = Assert.Single(EnemyStatusReadout.Of(world));
        Assert.Equal(ghoul.Id, readout.EnemyId);
        var look = StatusLooks.Of(StatusKind.Marked, StatusLooks.All);
        Assert.Equal([new StatusLine(StatusKind.Marked, look.Colour, EnemyStatusReadout.TimerText(MarkTicks))], readout.Lines);

        // 24 ticks left: "0.4s", the example of the issue.
        Assert.True(MarkTicks > 24, "The mark of the balance file must last longer than 0.4 s for this test.");
        RunTicks(world, PlayerInput.None, MarkTicks - 24);
        Assert.Equal("0.4s", Assert.Single(Assert.Single(EnemyStatusReadout.Of(world)).Lines).Timer);
    }

    [Fact]
    public void The_entry_is_there_until_the_last_tick_of_the_mark_and_gone_when_it_ends()
    {
        var (world, _) = MarkedGhoul();

        RunTicks(world, PlayerInput.None, MarkTicks - 1);
        Assert.Equal("0.1s", Assert.Single(Assert.Single(EnemyStatusReadout.Of(world)).Lines).Timer);

        world.Tick(PlayerInput.None);
        Assert.Empty(EnemyStatusReadout.Of(world));
    }

    [Fact]
    public void An_enemy_without_effects_shows_nothing()
    {
        var world = new GameWorld(LevelParser.Parse(Corridor));
        world.Tick(PlayerInput.None);

        Assert.Empty(EnemyStatusReadout.Of(world));
    }

    [Fact]
    public void A_dead_ghoul_shows_nothing_although_its_mark_still_runs()
    {
        var (world, ghoul) = MarkedGhoul();
        world.Tick(PlayerInput.None);
        Assert.Single(EnemyStatusReadout.Of(world));

        ghoul.TakeDamage(Balance.Default.Ghoul.MaxHealth, []);
        world.Tick(PlayerInput.None);

        Assert.True(ghoul.IsDead);
        Assert.True(ghoul.Status.Has(StatusKind.Marked), "The mark must still run, or the test would prove nothing.");
        Assert.Empty(EnemyStatusReadout.Of(world));
    }

    [Fact]
    public void The_display_follows_where_the_enemy_is_drawn_and_stands_above_its_head()
    {
        var (world, ghoul) = MarkedGhoul();
        RunTicks(world, PlayerInput.None, 10);

        var readout = Assert.Single(EnemyStatusReadout.Of(world));
        Assert.Equal(ghoul.ShownPosition, readout.Feet);
        Assert.Equal(BodySizes.Ghoul.HeadTop, readout.Height, 4);   // the head, not the taller walking body
        Assert.Equal(ghoul.Body.Radius, readout.Radius);
        Assert.True(readout.Anchor.Y > ghoul.ShownPosition.Y + BodySizes.Ghoul.HeadTop);
    }

    [Fact]
    public void Bats_and_gorm_show_their_mark_too()
    {
        var batWorld = new GameWorld(LevelParser.Parse("P\n.\n.\nB"));
        batWorld.ApplyStatus(batWorld.Bats[0], StatusKind.Marked, batWorld.Player);
        batWorld.Tick(PlayerInput.None);
        Assert.Equal(batWorld.Bats[0].Id, Assert.Single(EnemyStatusReadout.Of(batWorld)).EnemyId);

        var gormWorld = GormHallWorld();
        var gorm = gormWorld.Gorm!;
        gormWorld.ApplyStatus(gorm, StatusKind.Marked, gormWorld.Player);
        gormWorld.Tick(PlayerInput.None);
        var readout = Assert.Single(EnemyStatusReadout.Of(gormWorld));
        Assert.Equal(gorm.Id, readout.EnemyId);
        Assert.Equal(gorm.Position, readout.Feet);
    }

    [Fact]
    public void A_ghoul_still_hidden_in_the_floor_shows_nothing()
    {
        var world = HiddenGhoulWorld();
        var ghoul = world.Ghouls[0];
        Assert.True(world.ApplyStatus(ghoul, StatusKind.Marked, world.Player));
        world.Tick(PlayerInput.None);

        Assert.True(ghoul.Emergence.IsHidden, "The ghoul must still be hidden, or the test would prove nothing.");
        Assert.True(ghoul.Status.Has(StatusKind.Marked));
        Assert.Empty(EnemyStatusReadout.Of(world));
    }

    [Fact]
    public void The_lines_come_in_the_order_of_the_kinds_not_in_the_order_they_were_put_on()
    {
        var world = new GameWorld(LevelParser.Parse(Corridor));
        var ghoul = world.Ghouls[0];
        ghoul.Status.Apply(Third, 30, world.Player.Id);
        ghoul.Status.Apply(Second, 20, world.Player.Id);
        world.ApplyStatus(ghoul, StatusKind.Marked, world.Player);
        world.Tick(PlayerInput.None);

        var lines = Assert.Single(EnemyStatusReadout.Of(world, FourLooks)).Lines;

        Assert.Equal([StatusKind.Marked, Second, Third], lines.Select(line => line.Kind));
        Assert.Equal(EnemyStatusReadout.TimerText(MarkTicks - 1), lines[0].Timer);
        Assert.Equal(EnemyStatusReadout.TimerText(19), lines[1].Timer);
        Assert.Equal(FourLooks[1].Colour, lines[1].Colour);
    }

    [Fact]
    public void A_kind_without_a_timer_shows_its_colour_but_no_time()
    {
        var world = new GameWorld(LevelParser.Parse(Corridor));
        world.Ghouls[0].Status.Apply(Third, 30, world.Player.Id);
        world.Tick(PlayerInput.None);

        var line = Assert.Single(Assert.Single(EnemyStatusReadout.Of(world, FourLooks)).Lines);

        Assert.Equal(new StatusLine(Third, FourLooks[2].Colour, null), line);
    }

    [Fact]
    public void At_most_three_effects_stand_above_an_enemy_the_first_three_kinds()
    {
        var world = new GameWorld(LevelParser.Parse(Corridor));
        var ghoul = world.Ghouls[0];
        ghoul.Status.Apply(Fourth, 40, world.Player.Id);
        ghoul.Status.Apply(Third, 30, world.Player.Id);
        ghoul.Status.Apply(Second, 20, world.Player.Id);
        world.ApplyStatus(ghoul, StatusKind.Marked, world.Player);
        world.Tick(PlayerInput.None);

        var lines = Assert.Single(EnemyStatusReadout.Of(world, FourLooks)).Lines;

        Assert.Equal(EnemyStatusReadout.MaxLines, lines.Count);
        Assert.Equal([StatusKind.Marked, Second, Third], lines.Select(line => line.Kind));
    }

    [Fact]
    public void A_kind_without_a_look_is_an_error_of_the_table()
    {
        var world = new GameWorld(LevelParser.Parse(Corridor));
        world.Ghouls[0].Status.Apply(Second, 20, world.Player.Id);
        world.Tick(PlayerInput.None);

        Assert.Throws<KeyNotFoundException>(() => EnemyStatusReadout.Of(world));
    }

    [Fact]
    public void Every_kind_of_the_game_has_exactly_one_look()
    {
        foreach (var kind in Enum.GetValues<StatusKind>())
        {
            Assert.Single(StatusLooks.All, look => look.Kind == kind);
        }
    }

    [Fact]
    public void The_mark_shows_its_time_left()
    {
        Assert.True(StatusLooks.Of(StatusKind.Marked, StatusLooks.All).ShowsTimer);
    }

    // As in ArenaTests: a combat room whose ghoul waits in the floor until the room seals.
    private static GameWorld HiddenGhoulWorld()
    {
        const string map = "#.P.#\n##.##\n#...#\n#...#\n#.G.#\n#...#\n##.##\n#...#\n#...#";
        static List<(int X, int Z)> Rows(int from, int to) =>
            Enumerable.Range(from, to - from + 1).SelectMany(z => new[] { (1, z), (2, z), (3, z) }).ToList();
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 5, 1, 0, Rows(0, 0), [], []),
            new(1, RoomKind.Combat, "arena", 0, 2, 5, 4, 1, Rows(2, 5), [(2, 4)], []),
            new(2, RoomKind.Corridor, "corridor", 0, 7, 5, 2, 2, Rows(7, 8), [], []),
        ];
        LevelDoor[] doors = [new(2, 1, 0, 1, RoomSide.South), new(2, 6, 1, 2, RoomSide.South)];
        return new GameWorld(LevelParser.Parse(map), 1, Balance.Default, doors, rooms);
    }
}
