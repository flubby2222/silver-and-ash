using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class ExperienceTests
{
    /// <summary>Enemies with 1 health: any hit kills (to test what a kill gives).</summary>
    private static readonly Balance Frail = Balance.Default with
    {
        Ghoul = Balance.Default.Ghoul with { MaxHealth = 1 },
        Bat = Balance.Default.Bat with { MaxHealth = 1 },
    };

    /// <summary>
    /// A round level curve of its own (issue #586, as a table since #503): 10 for the first level, 15 for the second
    /// and the third, 34 from the fourth on.
    /// </summary>
    private static readonly Balance Curve = Balance.Default with
    {
        Experience = new ExperienceBalance { LevelCosts = [new(0, 10), new(1, 15), new(3, 34)] },
    };

    [Fact]
    public void A_new_player_is_level_zero_without_experience()
    {
        var player = new Player(LevelParser.Parse("P"));

        Assert.Equal(0, player.Level);
        Assert.Equal(0, player.Experience);
        Assert.Equal(Balance.Default.Experience.LevelCosts[0].Cost, player.ExperienceToNextLevel);
    }

    [Fact]
    public void The_first_threshold_leads_to_level_one_with_one_skill_point_and_the_thresholds_stay()
    {
        // Issue #391: counting starts at 0, but every threshold and skill point stays where it was.
        var player = new Player(LevelParser.Parse("P"), balance: Curve);
        var events = new List<GameEvent>();

        player.AddExperience(9, events);
        Assert.Equal(0, player.Level);
        Assert.Equal(0, player.PendingBoons);

        player.AddExperience(1, events);
        Assert.Equal(1, player.Level);
        Assert.Equal(1, player.PendingBoons);
        Assert.Equal(new LevelUp(player.Id, 1), Assert.Single(events.OfType<LevelUp>()));
        Assert.Equal(0, player.Experience);
        Assert.Equal(15, player.ExperienceToNextLevel);     // the row from level 1 on
    }

    [Fact]
    public void Killing_a_ghoul_gives_its_experience()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nG"), balance: Frail);
        var ghoul = world.Ghouls[0];

        Shoot(world, ghoul.Position + new Vector3(0f, 0.5f, 0f));

        Assert.True(ghoul.IsDead);
        Assert.Equal(Balance.Default.Experience.Ghoul, world.Player.Experience);
        Assert.Contains(world.Events, e => e == new ExperienceGained(world.Player.Id, Balance.Default.Experience.Ghoul));
    }

    [Fact]
    public void Killing_a_bat_gives_its_experience()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nB"), balance: Frail);
        var bat = world.Bats[0];

        Shoot(world, bat.Position + new Vector3(0f, BodySizes.Bat.Height / 2f, 0f));

        Assert.True(bat.IsDead);
        Assert.Equal(Balance.Default.Experience.Bat, world.Player.Experience);
    }

    [Fact]
    public void The_blade_counts_as_well()
    {
        var world = new GameWorld(LevelParser.Parse("P\nG"), balance: Frail);
        var ghoul = world.Ghouls[0];
        Face(world.Player, ghoul.Position + new Vector3(0f, 0.5f, 0f));

        world.Tick(PlayerInput.None with { Melee = true });
        world.Tick(PlayerInput.None);

        Assert.True(ghoul.IsDead);
        Assert.Equal(Balance.Default.Experience.Ghoul, world.Player.Experience);
    }

    [Fact]
    public void A_hit_that_does_not_kill_gives_nothing()
    {
        // A ghoul of its own (issue #586): 100 health, a shot does 10.
        var world = new GameWorld(LevelParser.Parse("P\n.\nG"), balance: Balance.Default with
        {
            Ghoul = Balance.Default.Ghoul with { MaxHealth = 100 },
            Pistol = Balance.Default.Pistol with { Damage = 10 },
        });
        var ghoul = world.Ghouls[0];

        Shoot(world, ghoul.Position + new Vector3(0f, 0.3f, 0f));

        Assert.Equal(90, ghoul.Health);
        Assert.False(ghoul.IsDead);
        Assert.Equal(0, world.Player.Experience);
    }

    [Fact]
    public void A_death_without_a_player_behind_it_gives_nothing()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nG"));

        world.Ghouls[0].TakeDamage(Balance.Default.Ghoul.MaxHealth, []);
        Assert.True(world.Ghouls[0].IsDead);
        world.Tick(PlayerInput.None);

        Assert.Equal(0, world.Player.Experience);
    }

    [Fact]
    public void Dummies_give_nothing()
    {
        var world = new GameWorld(LevelParser.Parse("P\nD"), balance: Balance.Default with
        {
            Dummy = Balance.Default.Dummy with { MaxHealth = 1 },
        });
        var dummy = world.Dummies[0];
        Face(world.Player, dummy.Position + new Vector3(0f, 1f, 0f));

        world.Tick(PlayerInput.None with { Melee = true });
        world.Tick(PlayerInput.None);

        Assert.True(dummy.IsDown);
        Assert.Equal(0, world.Player.Experience);
    }

    [Fact]
    public void Each_level_needs_the_cost_of_the_row_it_falls_in()
    {
        // Issue #503 (before: 10 x 1.5^level, rounded): 10, then 15 for levels 1 and 2 alike, then 34 from level 3 on.
        var player = new Player(LevelParser.Parse("P"), balance: Curve);

        player.AddExperience(10, []);
        Assert.Equal(1, player.Level);
        Assert.Equal(15, player.ExperienceToNextLevel);

        player.AddExperience(15, []);
        Assert.Equal(2, player.Level);
        Assert.Equal(15, player.ExperienceToNextLevel);

        player.AddExperience(15, []);
        Assert.Equal(3, player.Level);
        Assert.Equal(34, player.ExperienceToNextLevel);

        player.AddExperience(34, []);
        Assert.Equal(4, player.Level);
        Assert.Equal(34, player.ExperienceToNextLevel);     // the last row holds on
    }

    [Fact]
    public void What_is_left_over_counts_towards_the_next_level()
    {
        var player = new Player(LevelParser.Parse("P"), balance: Curve);

        player.AddExperience(13, []);

        Assert.Equal(1, player.Level);
        Assert.Equal(3, player.Experience);
    }

    [Fact]
    public void Enough_experience_for_two_levels_gives_both()
    {
        var player = new Player(LevelParser.Parse("P"), balance: Curve);
        var events = new List<GameEvent>();

        player.AddExperience(27, events);       // 10 + 15, 2 left

        Assert.Equal(2, player.Level);
        Assert.Equal(2, player.Experience);
        Assert.Equal([new LevelUp(player.Id, 1), new LevelUp(player.Id, 2)], events.OfType<LevelUp>());
    }

    [Fact]
    public void Just_below_the_threshold_is_not_enough()
    {
        var player = new Player(LevelParser.Parse("P"), balance: Curve);
        var events = new List<GameEvent>();

        player.AddExperience(9, events);

        Assert.Equal(0, player.Level);
        Assert.DoesNotContain(events, e => e is LevelUp);
    }

    [Fact]
    public void Levels_that_need_nothing_do_not_go_on_forever()
    {
        // The balancing file refuses such values, but code could still set them.
        var player = new Player(LevelParser.Parse("P"), balance: Balance.Default with
        {
            Experience = new ExperienceBalance { LevelCosts = [new(0, 0)] },
        });

        player.AddExperience(10, []);

        Assert.Equal(0, player.Level);
    }

    [Fact]
    public void Without_any_row_no_level_is_climbed()
    {
        // Issue #503: an empty table (refused by the balancing file, possible in code) costs nothing - and stops there.
        var player = new Player(LevelParser.Parse("P"), balance: Balance.Default with
        {
            Experience = new ExperienceBalance { LevelCosts = [] },
        });

        player.AddExperience(10, []);

        Assert.Equal(0, player.Level);
        Assert.Equal(0, player.ExperienceToNextLevel);
    }

    [Fact]
    public void The_dead_gain_nothing()
    {
        var player = new Player(LevelParser.Parse("P"), balance: Curve);
        player.TakeDamage(player.MaxHealth, []);

        player.AddExperience(50, []);           // alive, that would be three levels

        Assert.Equal(0, player.Level);
        Assert.Equal(0, player.Experience);
    }

    [Fact]
    public void A_restart_begins_at_level_zero_again()
    {
        var world = new GameWorld(LevelParser.Parse("P"), balance: Curve);
        world.Player.AddExperience(30, []);     // 10 + 15, 5 left
        Assert.Equal(2, world.Player.Level);

        var restarted = world.Restarted();

        Assert.Equal(0, restarted.Player.Level);
        Assert.Equal(0, restarted.Player.Experience);
    }

    [Fact]
    public void Experience_and_thresholds_come_from_the_world()
    {
        var balance = Frail with
        {
            Experience = new ExperienceBalance { Ghoul = 70, Bat = 5, LevelCosts = [new(0, 60), new(1, 90)] },
        };
        var world = new GameWorld(LevelParser.Parse("P\n.\nG"), balance: balance);

        Shoot(world, world.Ghouls[0].Position + new Vector3(0f, 0.5f, 0f));

        // 70 experience: level 1 needs 60, then 10 are left of the 90 (the second row) for level 2.
        Assert.Equal(1, world.Player.Level);
        Assert.Equal(10, world.Player.Experience);
        Assert.Equal(90, world.Player.ExperienceToNextLevel);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void Every_skill_point_is_reported_by_exactly_one_level_up(int levels)
    {
        // Issue #375: a level up is a skill point (one boon to choose); the sound plays on its event.
        var player = new Player(LevelParser.Parse("P"));
        var needed = 0;
        var probe = new Player(LevelParser.Parse("P"));
        for (var i = 0; i < levels; i++)
        {
            needed += probe.ExperienceToNextLevel;
            probe.AddExperience(probe.ExperienceToNextLevel, []);
        }

        var events = new List<GameEvent>();
        player.AddExperience(needed, events);

        Assert.Equal(levels, player.PendingBoons);
        Assert.Equal(Enumerable.Range(1, levels).Select(level => new LevelUp(player.Id, level)), events.OfType<LevelUp>());
    }

    [Fact]
    public void A_kill_that_pays_for_a_level_reports_it_once_in_the_world_and_not_again_while_the_choice_waits()
    {
        // Issue #375: the first level costs exactly one ghoul here, so the kill gives one skill point.
        var balance = Frail with
        {
            Experience = Frail.Experience with { LevelCosts = [new(0, Frail.Experience.Ghoul)] },
        };
        var world = new GameWorld(LevelParser.Parse("P\n.\nG"), balance: balance);

        Shoot(world, world.Ghouls[0].Position + new Vector3(0f, 0.5f, 0f));

        Assert.Equal(1, world.Player.PendingBoons);
        Assert.Equal(new LevelUp(world.Player.Id, 1), Assert.Single(world.Events.OfType<LevelUp>()));
        for (var tick = 0; tick < SimulationClock.TicksPerSecond; tick++)
        {
            world.Tick(PlayerInput.None);
            Assert.DoesNotContain(world.Events, e => e is LevelUp);
        }
    }

    /// <summary>Every threshold of the standard table after the first row (issue #503: from 10 and from 20 on).</summary>
    public static TheoryData<int> Thresholds() => new(Enumerable.Range(1, Balance.Default.Experience.LevelCosts.Count - 1));

    [Theory]
    [MemberData(nameof(Thresholds))]
    public void A_threshold_raises_the_cost_from_its_level_on_and_not_before(int row)
    {
        // Issue #503 with the standard table: 9 -> 10 still costs the old row, 10 -> 11 and 11 -> 12 the new one
        // (and the same at 19, 20, 21). The experience to get there is summed up straight from the rows.
        var rows = Balance.Default.Experience.LevelCosts;
        var threshold = rows[row].FromLevel;
        var player = new Player(LevelParser.Parse("P"));
        player.AddExperience(ExperienceForLevel(threshold - 1), []);
        Assert.Equal(threshold - 1, player.Level);
        Assert.Equal(rows[row - 1].Cost, player.ExperienceToNextLevel);

        player.AddExperience(rows[row - 1].Cost, []);
        Assert.Equal(threshold, player.Level);
        Assert.Equal(0, player.Experience);
        Assert.Equal(rows[row].Cost, player.ExperienceToNextLevel);

        player.AddExperience(rows[row].Cost - 1, []);
        Assert.Equal(threshold, player.Level);      // one point short of the new cost

        player.AddExperience(1, []);
        Assert.Equal(threshold + 1, player.Level);
        Assert.Equal(rows[row].Cost, player.ExperienceToNextLevel);
    }

    [Fact]
    public void One_kill_in_the_tick_climbs_several_levels_across_a_threshold()
    {
        // Issue #503: two levels before the first threshold, a ghoul worth two levels of the old row, one of the new
        // and half a new level more - shot in the tick, it brings all three level ups, and the rest counts on.
        var rows = Balance.Default.Experience.LevelCosts;
        var threshold = rows[1].FromLevel;
        var leftOver = rows[1].Cost / 2;
        var balance = Frail with
        {
            Experience = Frail.Experience with { Ghoul = 2 * rows[0].Cost + rows[1].Cost + leftOver },
        };
        var world = new GameWorld(LevelParser.Parse("P\n.\nG"), balance: balance);
        var player = world.Player;
        player.AddExperience(ExperienceForLevel(threshold - 2), []);
        Assert.Equal(threshold - 2, player.Level);

        Shoot(world, world.Ghouls[0].Position + new Vector3(0f, 0.5f, 0f));

        Assert.True(world.Ghouls[0].IsDead);
        Assert.Equal(
            [new LevelUp(player.Id, threshold - 1), new LevelUp(player.Id, threshold), new LevelUp(player.Id, threshold + 1)],
            world.Events.OfType<LevelUp>());
        Assert.Equal(threshold + 1, player.Level);
        Assert.Equal(leftOver, player.Experience);
        Assert.Equal(rows[1].Cost, player.ExperienceToNextLevel);
    }

    /// <summary>Experience from level 0 to <paramref name="level"/>, summed up straight from the rows of the standard table.</summary>
    private static int ExperienceForLevel(int level)
    {
        var rows = Balance.Default.Experience.LevelCosts;
        return Enumerable.Range(0, level).Sum(below => rows.Last(row => row.FromLevel <= below).Cost);
    }

    private static void Shoot(GameWorld world, Vector3 point)
    {
        Face(world.Player, point);
        world.Tick(PlayerInput.None with { Shoot = true });
    }

    private static void Face(Player player, Vector3 point)
    {
        var direction = Vector3.Normalize(point - player.EyePosition);
        player.Look(PlayerInput.None with
        {
            Look = new Vector2(
                -(MathF.Atan2(direction.X, direction.Z) - player.View.YawRadians) / ControlSettings.MouseSensitivity,
                -(MathF.Asin(direction.Y) - player.View.PitchRadians) / ControlSettings.MouseSensitivity),
        });
    }
}
