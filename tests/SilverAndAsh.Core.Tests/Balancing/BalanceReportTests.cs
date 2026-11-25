using SilverAndAsh.Core.Balancing;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Balancing;

public class BalanceReportTests
{
    private static readonly IReadOnlyList<RoomTemplate> Templates = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));
    private static readonly LevelZero Catacombs = new(Templates);

    private static double Value(IReadOnlyList<Figure> figures, string group, string name) =>
        Assert.Single(figures, figure => figure.Group == group && figure.Name == name).Value;

    [Fact]
    public void Counts_are_the_real_counts_of_the_level()
    {
        var level = Catacombs.Build(4, Balance.Default);

        var figures = BalanceReport.Measure(level, Balance.Default);

        Assert.Equal(level.Rooms.Count(room => room.Kind == RoomKind.Combat), Value(figures, BalanceReport.Rooms, "Combat rooms"));
        Assert.Equal(1, Value(figures, BalanceReport.Rooms, "Secret rooms"));
        Assert.Equal(level.Level.GhoulPositions.Count, Value(figures, BalanceReport.Enemies, "Ghouls"));
        Assert.Equal(level.Level.BatPositions.Count, Value(figures, BalanceReport.Enemies, "Bats"));
        Assert.Equal(level.Chests!.Count, Value(figures, BalanceReport.Chests, "Chests"));
        Assert.Equal(level.Chests!.Sum(chest => chest.Silver), Value(figures, BalanceReport.Silver, "In chests"));
        Assert.Equal(
            level.Chests!.Count(chest => chest.IsLocked) + level.Doors.Count(door => door.Kind == DoorKind.Locked),
            Value(figures, BalanceReport.Keys, "Locks"));
        Assert.Equal(
            Enum.GetValues<ChestTier>().Sum(tier => Value(figures, BalanceReport.Chests, tier.ChestName())),
            Value(figures, BalanceReport.Chests, "Chests"));
    }

    [Fact]
    public void Chances_count_as_chance_times_number()
    {
        var always = Balance.Default with
        {
            Keys = new KeysBalance { RoomChance = 1f, EnemyChance = 1f },
            Silver = new SilverBalance { GhoulChance = 1f, GhoulMin = 2, GhoulMax = 2, BatChance = 0.5f, BatMin = 4, BatMax = 6 },
            Drops = new DropsBalance { GhoulChance = 1f, BatChance = 0f, ConsecratedAmmoShare = 0.25f },
        };
        var level = Catacombs.Build(4, always);
        var ghouls = level.Level.GhoulPositions.Count;
        var bats = level.Level.BatPositions.Count;
        var arenas = level.Rooms.Count(room => room.Kind is RoomKind.Combat or RoomKind.Boss);

        var figures = BalanceReport.Measure(level, always);

        Assert.Equal(arenas + ghouls + bats, Value(figures, BalanceReport.Keys, "Keys (expected)"), 6);
        Assert.Equal((ghouls * 2) + (bats * 0.5 * 5), Value(figures, BalanceReport.Silver, "From enemies (expected)"), 6);
        Assert.Equal(ghouls * 0.75, Value(figures, BalanceReport.Healing, "Blood vials from enemies (expected)"), 6);
    }

    [Fact]
    public void Healing_is_the_vials_times_what_one_heals()
    {
        var level = Catacombs.Build(9, Balance.Default);

        var figures = BalanceReport.Measure(level, Balance.Default);

        var vials = Value(figures, BalanceReport.Healing, "Blood vials from enemies (expected)")
            + Value(figures, BalanceReport.Healing, "Blood vials in chests");
        Assert.Equal(vials * Balance.Default.BloodVial.Heal, Value(figures, BalanceReport.Healing, "Health healed (expected)"), 6);
        Assert.Equal(
            vials * Balance.Default.BloodVial.Heal / Balance.Default.Player.MaxHealth,
            Value(figures, BalanceReport.Healing, "Healed / max health"), 6);
    }

    [Fact]
    public void The_time_estimate_grows_with_floor_and_enemy_health()
    {
        var level = Catacombs.Build(2, Balance.Default);
        var slow = Balance.Default with { Player = Balance.Default.Player with { MaxSpeed = Balance.Default.Player.MaxSpeed / 2 } };

        var normal = BalanceReport.Measure(level, Balance.Default);
        var slower = BalanceReport.Measure(level, slow);

        Assert.Equal(2 * Value(normal, BalanceReport.Time, "Walking (min)"), Value(slower, BalanceReport.Time, "Walking (min)"), 6);
        Assert.Equal(
            Value(normal, BalanceReport.Time, "Walking (min)") + Value(normal, BalanceReport.Time, "Fighting with the blade (min)"),
            Value(normal, BalanceReport.Time, "Total (min)"), 6);
    }

    [Fact]
    public void Walking_crosses_every_floor_cell_once_at_top_speed()
    {
        var level = Catacombs.Build(2, Balance.Default);
        var cells = level.Rooms.Sum(room => room.FloorCells.Count);

        var figures = BalanceReport.Measure(level, Balance.Default);

        Assert.Equal(cells, Value(figures, BalanceReport.Rooms, "Floor cells"));
        Assert.Equal(
            cells * (double)Level.CellSize / Balance.Default.Player.MaxSpeed / 60,
            Value(figures, BalanceReport.Time, "Walking (min)"), 6);
    }

    /// <summary>Level 1 and the levels at and around every threshold of the standard table (9, 10, 11, 19, 20, 21).</summary>
    public static TheoryData<int> LevelsAroundThresholds()
    {
        var levels = new TheoryData<int> { 1 };
        foreach (var row in Balance.Default.Experience.LevelCosts.Skip(1))
        {
            levels.Add(row.FromLevel - 1);
            levels.Add(row.FromLevel);
            levels.Add(row.FromLevel + 1);
        }

        return levels;
    }

    [Theory]
    [MemberData(nameof(LevelsAroundThresholds))]
    public void The_level_reached_follows_the_rows_of_the_level_costs(int level)
    {
        // Issue #503: the experience for a level is summed up straight from the rows of the standard table - one
        // point less is one level less.
        var rows = Balance.Default.Experience.LevelCosts;
        var needed = Enumerable.Range(0, level).Sum(below => rows.Last(row => row.FromLevel <= below).Cost);

        Assert.Equal(level, BalanceReport.LevelReached(needed, Balance.Default.Experience));
        Assert.Equal(level - 1, BalanceReport.LevelReached(needed - 1, Balance.Default.Experience));
    }

    [Fact]
    public void The_report_counts_levels_exactly_as_a_player_climbs_them()
    {
        // One rule for game and report (issue #503): any amount of experience leads a fresh player to the same level.
        for (var experience = 0; experience <= 20000; experience += 37)
        {
            var player = new Player(LevelParser.Parse("P"));
            player.AddExperience(experience, []);

            Assert.Equal(player.Level, BalanceReport.LevelReached(experience, Balance.Default.Experience));
        }
    }

    [Fact]
    public void Summarize_gives_average_smallest_and_largest_value_in_measured_order()
    {
        IReadOnlyList<Figure>[] levels =
        [
            [new("A", "x", 2), new("A", "y", 10)],
            [new("A", "x", 4), new("A", "y", 30)],
            [new("A", "x", 9), new("A", "y", 20)],
        ];

        var summary = BalanceReport.Summarize(levels);

        Assert.Equal([new FigureSummary("A", "x", 5, 2, 9), new FigureSummary("A", "y", 20, 10, 30)], summary);
    }

    [Fact]
    public void The_same_values_and_seeds_give_the_same_report()
    {
        Assert.Equal(BalanceReport.Run(Templates, Balance.Default, 5), BalanceReport.Run(Templates, Balance.Default, 5));
    }

    [Fact]
    public void Other_values_give_another_report()
    {
        var moreRooms = Balance.Default with { Layout = Balance.Default.Layout with { CombatRoomsMin = 12, CombatRoomsMax = 12 } };

        var report = BalanceReport.Run(Templates, moreRooms, 3);

        var combat = Assert.Single(report, line => line.Name == "Combat rooms");
        Assert.Equal(new FigureSummary(BalanceReport.Rooms, "Combat rooms", 12, 12, 12), combat);
    }

    [Fact]
    public void The_game_and_the_report_build_the_same_level()
    {
        var world = Catacombs.NewRun(6, Balance.Default);
        var level = Catacombs.Build(6, Balance.Default);

        Assert.Equal(world.Ghouls.Count, level.Level.GhoulPositions.Count);
        Assert.Equal(world.Doors.Count, level.Doors.Count);
        Assert.Equal(world.Chests.Select(chest => chest.Plan), level.Chests!);
    }
}
