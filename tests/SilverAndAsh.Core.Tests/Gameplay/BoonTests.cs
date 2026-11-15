using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class BoonTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };

    [Fact]
    public void A_level_up_opens_a_choice()
    {
        var player = new Player(LevelParser.Parse("P"));

        player.AddExperience(100, []);

        Assert.Equal(1, player.PendingBoons);
    }

    [Fact]
    public void Two_levels_at_once_open_two_choices_one_after_the_other()
    {
        var world = new GameWorld(LevelParser.Parse("P"));
        var player = world.Player;
        player.AddExperience(260, []);
        Assert.Equal(2, player.PendingBoons);

        world.Tick(PlayerInput.None with { ChooseBoon = Boon.Vitality });
        Assert.Equal(1, player.PendingBoons);

        world.Tick(PlayerInput.None with { ChooseBoon = Boon.Endurance });
        Assert.Equal(0, player.PendingBoons);
        Assert.Equal(Balance.Default.Player.MaxHealth + Balance.Default.Boons.VitalityHealth, player.MaxHealth);
        Assert.Equal(Balance.Default.Player.StaminaJumps + 1, player.Stamina.Segments);
    }

    [Fact]
    public void While_a_choice_is_open_the_world_stands_still()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nG"));
        var player = world.Player;
        var ghoul = world.Ghouls[0];
        player.AddExperience(100, []);
        var playerAt = player.Position;
        var ghoulAt = ghoul.Position;

        for (var tick = 0; tick < Second; tick++)
        {
            world.Tick(Forward);
        }

        Assert.Equal(playerAt, player.Position);
        Assert.Equal(ghoulAt, ghoul.Position);
        Assert.Empty(world.Events);
    }

    [Fact]
    public void After_the_choice_the_world_goes_on()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n."));
        var player = world.Player;
        player.AddExperience(100, []);
        var start = player.Position;

        world.Tick(PlayerInput.None with { ChooseBoon = Boon.Strength });
        for (var tick = 0; tick < Second / 4; tick++)
        {
            world.Tick(Forward);
        }

        Assert.True(player.Position.Z > start.Z + 0.5f, "The player did not walk after choosing.");
    }

    [Fact]
    public void The_view_does_not_turn_while_choosing()
    {
        var player = new Player(LevelParser.Parse("P"));
        player.AddExperience(100, []);
        var yaw = player.View.YawRadians;

        player.Look(PlayerInput.None with { Look = new Vector2(200f, 0f) });

        Assert.Equal(yaw, player.View.YawRadians);
    }

    [Fact]
    public void A_choice_without_a_level_up_does_nothing()
    {
        var world = new GameWorld(LevelParser.Parse("P"));

        world.Tick(PlayerInput.None with { ChooseBoon = Boon.Vitality });

        Assert.Equal(Balance.Default.Player.MaxHealth, world.Player.MaxHealth);
        Assert.DoesNotContain(world.Events, e => e is BoonChosen);
    }

    [Fact]
    public void A_player_without_a_level_up_cannot_choose()
    {
        var player = new Player(LevelParser.Parse("P"));
        var events = new List<GameEvent>();

        var chosen = player.Choose(Boon.Vitality, events);

        Assert.False(chosen);
        Assert.Equal(Balance.Default.Player.MaxHealth, player.MaxHealth);
        Assert.Empty(events);
    }

    [Fact]
    public void Choosing_is_reported()
    {
        var world = new GameWorld(LevelParser.Parse("P"));
        world.Player.AddExperience(100, []);

        world.Tick(PlayerInput.None with { ChooseBoon = Boon.Strength });

        Assert.Contains(new BoonChosen(world.Player.Id, Boon.Strength), world.Events);
    }

    [Fact]
    public void Vitality_raises_the_maximum_and_heals_as_much()
    {
        var player = new Player(LevelParser.Parse("P"));
        player.TakeDamage(50, []);
        player.AddExperience(100, []);

        player.Choose(Boon.Vitality, []);

        Assert.Equal(120, player.MaxHealth);
        Assert.Equal(70, player.Health);
    }

    [Fact]
    public void Strength_raises_the_damage_of_both_weapons()
    {
        var rules = DamageRules.Standard();

        Assert.Equal(46, rules.Calculate(new Hit(Weapon.Blade, HitZone.Body, 40, Strength: 1)).Amount);
        Assert.Equal(40, rules.Calculate(new Hit(Weapon.Pistol, HitZone.Body, 35, Strength: 1)).Amount);  // 40.25
        Assert.Equal(40, rules.Calculate(new Hit(Weapon.Blade, HitZone.Body, 40)).Amount);
    }

    [Fact]
    public void Strength_chosen_twice_adds_up()
    {
        var rules = DamageRules.Standard();

        Assert.Equal(52, rules.Calculate(new Hit(Weapon.Blade, HitZone.Body, 40, Strength: 2)).Amount);    // 40 x 1.3
    }

    [Fact]
    public void A_strong_player_hits_harder_in_the_world()
    {
        var world = new GameWorld(LevelParser.Parse("P\nD"));
        var dummy = world.Dummies[0];
        world.Player.AddExperience(100, []);
        world.Tick(PlayerInput.None with { ChooseBoon = Boon.Strength });
        Face(world.Player, dummy.Position + new Vector3(0f, 1f, 0f));

        world.Tick(PlayerInput.None with { Melee = true });
        world.Tick(PlayerInput.None);

        Assert.Equal(Balance.Default.Dummy.MaxHealth - 46, dummy.Health);
    }

    [Fact]
    public void Endurance_adds_a_full_stamina_segment()
    {
        var player = new Player(LevelParser.Parse("P"));
        player.AddExperience(100, []);

        player.Choose(Boon.Endurance, []);

        Assert.Equal(Balance.Default.Player.StaminaJumps + 1, player.Stamina.Segments);
        Assert.Equal(1f, player.Stamina.Fraction, 0.001f);
    }

    [Fact]
    public void A_restart_takes_every_boon_away()
    {
        var world = new GameWorld(LevelParser.Parse("P"));
        world.Player.AddExperience(250, []);
        world.Tick(PlayerInput.None with { ChooseBoon = Boon.Vitality });
        world.Tick(PlayerInput.None with { ChooseBoon = Boon.Endurance });

        var restarted = world.Restarted().Player;

        Assert.Equal(Balance.Default.Player.MaxHealth, restarted.MaxHealth);
        Assert.Equal(Balance.Default.Player.StaminaJumps, restarted.Stamina.Segments);
        Assert.Equal(0, restarted.PendingBoons);
    }

    [Fact]
    public void The_boons_come_from_the_world()
    {
        var player = new Player(LevelParser.Parse("P"), balance: Balance.Default with
        {
            Boons = new BoonBalance { VitalityHealth = 50, StrengthDamage = 0.5f, EnduranceSegments = 2 },
        });
        player.AddExperience(250, []);

        player.Choose(Boon.Vitality, []);
        player.Choose(Boon.Endurance, []);

        Assert.Equal(150, player.MaxHealth);
        Assert.Equal(Balance.Default.Player.StaminaJumps + 2, player.Stamina.Segments);
        Assert.Equal(60, DamageRules.Standard(Balance.Default with
        {
            Boons = new BoonBalance { StrengthDamage = 0.5f },
        }).Calculate(new Hit(Weapon.Blade, HitZone.Body, 40, Strength: 1)).Amount);
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
