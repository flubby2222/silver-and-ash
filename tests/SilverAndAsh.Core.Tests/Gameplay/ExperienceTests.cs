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
    /// A round level curve of its own (issue #586): 10 for the first level, then half as much again each time -
    /// 10, 15, 22.5 -> 23, 33.75 -> 34. Half points round up (22.5 -> 23, not to the even 22).
    /// </summary>
    private static readonly Balance Curve = Balance.Default with
    {
        Experience = new ExperienceBalance { FirstLevel = 10, Growth = 1.5f },
    };

    [Fact]
    public void A_new_player_is_level_zero_without_experience()
    {
        var player = new Player(LevelParser.Parse("P"));

        Assert.Equal(0, player.Level);
        Assert.Equal(0, player.Experience);
        Assert.Equal(Balance.Default.Experience.FirstLevel, player.ExperienceToNextLevel);
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
        Assert.Equal(15, player.ExperienceToNextLevel);     // 10 x 1.5
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
    public void Each_level_needs_the_first_times_growth_to_the_power_of_the_level_rounded()
    {
        // 10 x 1.5^level: 10, 15, 22.5 -> 23, 33.75 -> 34, rounded - not from the rounded last level (23 x 1.5 = 34.5 -> 35).
        var player = new Player(LevelParser.Parse("P"), balance: Curve);

        player.AddExperience(10, []);
        Assert.Equal(1, player.Level);
        Assert.Equal(15, player.ExperienceToNextLevel);

        player.AddExperience(15, []);
        Assert.Equal(2, player.Level);
        Assert.Equal(23, player.ExperienceToNextLevel);

        player.AddExperience(23, []);
        Assert.Equal(3, player.Level);
        Assert.Equal(34, player.ExperienceToNextLevel);
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
            Experience = new ExperienceBalance { FirstLevel = 0 },
        });

        player.AddExperience(10, []);

        Assert.Equal(0, player.Level);
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
            Experience = new ExperienceBalance { Ghoul = 70, Bat = 5, FirstLevel = 60, Growth = 1.5f },
        };
        var world = new GameWorld(LevelParser.Parse("P\n.\nG"), balance: balance);

        Shoot(world, world.Ghouls[0].Position + new Vector3(0f, 0.5f, 0f));

        // 70 experience: level 1 needs 60, then 10 are left of the 90 (60 x 1.5) for level 2.
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
            Experience = Frail.Experience with { FirstLevel = Frail.Experience.Ghoul },
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
