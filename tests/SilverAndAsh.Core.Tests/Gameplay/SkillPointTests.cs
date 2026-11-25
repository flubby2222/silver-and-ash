using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Skill points (issue #502, skills draft section 1): a player gains one on every level that is a multiple of
/// <see cref="SkillsBalance.LevelsPerPoint"/>, reported as <see cref="SkillPointGained"/>.
/// </summary>
public class SkillPointTests
{
    private static int Step => Balance.Default.Skills.LevelsPerPoint;

    /// <summary>Ghouls that die of any hit and stand still (they neither flee the line of fire nor strike back).</summary>
    private static readonly Balance StillFrail = Balance.Default with
    {
        Ghoul = Balance.Default.Ghoul with { MaxHealth = 1, Speed = 0f },
    };

    [Fact]
    public void A_new_player_has_no_skill_points()
    {
        Assert.Equal(0, new Player(LevelParser.Parse("P")).SkillPoints);
    }

    [Fact]
    public void Killing_ghouls_in_the_tick_until_the_level_of_the_first_point_gives_exactly_that_point()
    {
        // The real way (issue #502): shoot ghoul after ghoul through GameWorld.Tick, choose every boon on offer, until the
        // level that brings the first point. The experience and the step are the standard ones.
        const int Ghouls = 21;
        var world = new GameWorld(LevelParser.Parse(string.Join('\n',
            "#" + new string('G', Ghouls) + "#",
            "#" + new string('.', Ghouls) + "#",
            "#" + new string('.', Ghouls) + "#",
            "#" + new string('.', Ghouls / 2) + "P" + new string('.', Ghouls / 2) + "#")), balance: StillFrail);
        var player = world.Player;
        var gained = new List<SkillPointGained>();
        var levelsSeen = new List<int>();

        for (var tick = 0; player.Level < Step && tick < 60 * SimulationClock.TicksPerSecond; tick++)
        {
            world.Tick(NextInput(world, tick));
            gained.AddRange(world.Events.OfType<SkillPointGained>());
            levelsSeen.AddRange(world.Events.OfType<LevelUp>().Select(levelUp => levelUp.Level));
        }

        Assert.Equal(Step, player.Level);
        Assert.Equal(Enumerable.Range(1, Step), levelsSeen);
        Assert.Equal(Step / Step, player.SkillPoints);
        Assert.Equal([new SkillPointGained(player.Id, 1)], gained);
    }

    [Fact]
    public void The_levels_before_the_step_bring_no_point()
    {
        var player = new Player(LevelParser.Parse("P"));
        var events = new List<GameEvent>();

        while (player.Level < Step - 1)
        {
            player.AddExperience(player.ExperienceToNextLevel, events);
        }

        Assert.Equal(0, player.SkillPoints);
        Assert.DoesNotContain(events, e => e is SkillPointGained);
    }

    [Fact]
    public void A_kill_worth_several_steps_reports_every_point_right_after_its_level_up()
    {
        // One ghoul worth exactly the experience of two steps of levels, counted on a probe with the standard curve.
        var probe = new Player(LevelParser.Parse("P"));
        var needed = 0;
        while (probe.Level < 2 * Step)
        {
            needed += probe.ExperienceToNextLevel;
            probe.AddExperience(probe.ExperienceToNextLevel, []);
        }

        var balance = StillFrail with { Experience = Balance.Default.Experience with { Ghoul = needed } };
        var world = new GameWorld(LevelParser.Parse("P\n.\nG"), balance: balance);
        var player = world.Player;

        Shoot(world, world.Ghouls[0].Position + new Vector3(0f, 0.5f, 0f));

        Assert.Equal(2 * Step, player.Level);
        Assert.Equal(2, player.SkillPoints);
        var progress = world.Events.Where(e => e is LevelUp or SkillPointGained).ToList();
        var expected = new List<GameEvent>();
        for (var level = 1; level <= 2 * Step; level++)
        {
            expected.Add(new LevelUp(player.Id, level));
            if (level % Step == 0)
            {
                expected.Add(new SkillPointGained(player.Id, level / Step));
            }
        }

        Assert.Equal(expected, progress);
    }

    [Fact]
    public void The_step_comes_from_the_balance()
    {
        var player = new Player(LevelParser.Parse("P"), balance: Balance.Default with
        {
            Skills = new SkillsBalance { LevelsPerPoint = Step + 1 },
        });

        while (player.Level < Step + 1)
        {
            Assert.Equal(0, player.SkillPoints);
            player.AddExperience(player.ExperienceToNextLevel, []);
        }

        Assert.Equal(1, player.SkillPoints);
    }

    [Fact]
    public void A_step_of_zero_set_in_code_brings_no_points_and_no_crash()
    {
        // The balancing file refuses 0, but code could still set it.
        var player = new Player(LevelParser.Parse("P"), balance: Balance.Default with
        {
            Skills = new SkillsBalance { LevelsPerPoint = 0 },
        });

        player.AddExperience(player.ExperienceToNextLevel, []);

        Assert.Equal(1, player.Level);
        Assert.Equal(0, player.SkillPoints);
    }

    [Fact]
    public void The_dead_gain_no_skill_points()
    {
        var player = new Player(LevelParser.Parse("P"));
        var needed = ExperienceForLevel(Step);
        player.TakeDamage(player.MaxHealth, []);
        var events = new List<GameEvent>();

        player.AddExperience(needed, events);   // alive, that would be the first point

        Assert.Equal(0, player.SkillPoints);
        Assert.DoesNotContain(events, e => e is SkillPointGained);
    }

    [Fact]
    public void A_restart_after_death_begins_without_skill_points_again()
    {
        var world = new GameWorld(LevelParser.Parse("P"));
        world.Player.AddExperience(ExperienceForLevel(Step), []);
        Assert.Equal(1, world.Player.SkillPoints);
        world.Player.TakeDamage(world.Player.MaxHealth, []);

        var restarted = world.Restarted();

        Assert.Equal(0, restarted.Player.SkillPoints);
    }

    [Fact]
    public void Every_player_counts_his_own_points()
    {
        // Online co-op kept open (decision record 0003): the points belong to one player, the event names him.
        var level = LevelParser.Parse("P");
        var first = new Player(level);
        var second = new Player(level);
        var events = new List<GameEvent>();

        first.AddExperience(ExperienceForLevel(Step), events);

        Assert.Equal((1, 0), (first.SkillPoints, second.SkillPoints));
        Assert.Equal(first.Id, Assert.Single(events.OfType<SkillPointGained>()).PlayerId);
    }

    /// <summary>The experience a fresh player with the standard curve needs to reach <paramref name="level"/>.</summary>
    private static int ExperienceForLevel(int level)
    {
        var probe = new Player(LevelParser.Parse("P"));
        var needed = 0;
        while (probe.Level < level)
        {
            needed += probe.ExperienceToNextLevel;
            probe.AddExperience(probe.ExperienceToNextLevel, []);
        }

        return needed;
    }

    /// <summary>
    /// What a player does in the tick: choose the first boon on offer while one waits, reload an empty magazine, else
    /// aim at the nearest living ghoul and pull the trigger every other tick (a held trigger is not a new shot).
    /// </summary>
    private static PlayerInput NextInput(GameWorld world, int tick)
    {
        var player = world.Player;
        if (player.CurrentOffer.Count > 0)
        {
            return PlayerInput.None with { ChooseBoon = player.CurrentOffer[0] };
        }

        if (player.Ranged.Magazine.Rounds == 0)
        {
            return PlayerInput.None with { Reload = !player.Ranged.Magazine.IsReloading };
        }

        var target = world.Ghouls.Where(ghoul => !ghoul.IsDead)
            .MinBy(ghoul => Vector3.DistanceSquared(ghoul.Position, player.Position));
        if (target is null)
        {
            return PlayerInput.None;
        }

        Face(player, target.Position + new Vector3(0f, 0.5f, 0f));
        return PlayerInput.None with { Shoot = tick % 2 == 0 };
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
