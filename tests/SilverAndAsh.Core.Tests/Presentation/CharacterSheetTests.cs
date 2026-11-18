using System.Globalization;
using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>The lines of the map page (issue #347): a player's values and chosen boons, read from the game state.</summary>
public class CharacterSheetTests
{
    private static readonly Balance Default = Balance.Default;
    private static readonly BoonBalance Boons = Default.Boons;

    /// <summary>Every level up offers the whole pool - so a test can choose any boon.</summary>
    private static readonly Balance OfferAll = Default with
    {
        Boons = Default.Boons with { Offered = Enum.GetValues<Boon>().Length },
    };

    private static GameWorld World(string map = "P") => new(LevelParser.Parse(map), balance: OfferAll);

    private static void Choose(Player player, Boon boon, int times = 1)
    {
        for (var i = 0; i < times; i++)
        {
            player.AddExperience(player.ExperienceToNextLevel - player.Experience, []);
            Assert.True(player.Choose(boon, []));
        }
    }

    private static string Value(GameWorld world, string label) =>
        CharacterSheet.Stats(world, world.Player).Single(line => line.Label == label).Value;

    private static string Text(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    // Base damage x (1 + every damage boon), half points up - as the damage rules count.
    private static int Boosted(int baseDamage, int damageBoons) =>
        (int)MathF.Round(baseDamage * (1f + (Boons.Damage * damageBoons)), MidpointRounding.AwayFromZero);

    [Fact]
    public void A_fresh_player_shows_the_base_values()
    {
        var world = World();

        Assert.Equal(
            [
                new("Level", "1"),
                new("Health", Text($"{Default.Player.MaxHealth} / {Default.Player.MaxHealth}")),
                new("Stamina", Text($"{world.Player.Stamina.Segments} bars")),
                new("Stamina regen", "100 %"),
                new("Melee", Text($"{Default.Blade.Damage}  Silver Blade")),
                new("Ranged", Text($"{Default.Pistol.Damage}  Pistol")),
                new("Speed", Text($"{Default.Player.MaxSpeed:0.#} m/s")),
                new("Experience", "100 %"),
            ],
            CharacterSheet.Stats(world, world.Player));
        Assert.Empty(CharacterSheet.Boons(world.Player, Boons));
    }

    [Fact]
    public void Lost_health_shows()
    {
        var world = World();

        world.Player.TakeDamage(30, []);

        Assert.Equal(Text($"{Default.Player.MaxHealth - 30} / {Default.Player.MaxHealth}"), Value(world, "Health"));
    }

    [Fact]
    public void Damage_boons_raise_the_damage_of_both_weapons()
    {
        var world = World();

        Choose(world.Player, Boon.Damage, 3);

        Assert.Equal(Text($"{Boosted(Default.Blade.Damage, 3)}  Silver Blade"), Value(world, "Melee"));
        Assert.Equal(Text($"{Boosted(Default.Pistol.Damage, 3)}  Pistol"), Value(world, "Ranged"));
        Assert.NotEqual(Text($"{Default.Pistol.Damage}  Pistol"), Value(world, "Ranged"));
    }

    [Fact]
    public void The_damage_shown_is_the_damage_a_shot_does()
    {
        var world = new GameWorld(LevelParser.Parse("P\nD\n."), balance: OfferAll with
        {
            Dummy = Default.Dummy with { MaxHealth = 100_000 },
        });
        Choose(world.Player, Boon.Damage, 2);
        var shown = Value(world, "Ranged");
        LookAt(world.Player, world.Dummies[0].Position + new Vector3(0f, BodySizes.Dummy.Height / 2f, 0f));

        world.Tick(PlayerInput.None with { Shoot = true });

        var hit = Assert.Single(world.Events.OfType<DummyHit>());
        Assert.False(hit.Critical);
        Assert.Equal(Text($"{hit.Damage}  Pistol"), shown);
    }

    /// <summary>Turns the player's view (through the mouse, as in the game) to look at <paramref name="point"/>.</summary>
    private static void LookAt(Player player, Vector3 point)
    {
        var to = point - player.EyePosition;
        var yaw = MathF.Atan2(to.X, to.Z);
        var pitch = MathF.Atan2(to.Y, new Vector2(to.X, to.Z).Length());
        var turn = MathF.IEEERemainder(player.View.YawRadians - yaw, MathF.Tau);
        player.Look(PlayerInput.None with
        {
            Look = new Vector2(turn, player.View.PitchRadians - pitch) / ControlSettings.MouseSensitivity,
        });
    }

    [Fact]
    public void The_weapons_in_hand_are_shown_and_pellets_counted()
    {
        var world = World();
        world.Player.EquipMelee(WeaponKind.Club);
        world.Player.Equip(0, WeaponKind.Blunderbuss);

        Assert.Equal(Text($"{Default.Club.Damage}  Club"), Value(world, "Melee"));
        Assert.Equal(Text($"{Default.Blunderbuss.Damage} x {Default.Blunderbuss.Pellets}  Blunderbuss"), Value(world, "Ranged"));
    }

    [Fact]
    public void Health_stamina_and_experience_boons_change_their_values()
    {
        var world = World();

        Choose(world.Player, Boon.Health, 2);
        Choose(world.Player, Boon.StaminaRegen);
        Choose(world.Player, Boon.Experience, 2);

        var maxHealth = Default.Player.MaxHealth + (2 * Boons.Health);
        Assert.Equal(Text($"{maxHealth} / {maxHealth}"), Value(world, "Health"));
        Assert.Equal(Text($"{(1f + Boons.StaminaRegen) * 100f:0.#} %"), Value(world, "Stamina regen"));
        Assert.Equal(Text($"{(1f + (2 * Boons.Experience)) * 100f:0.#} %"), Value(world, "Experience"));
        Assert.Equal(Text($"{world.Player.Level}"), Value(world, "Level"));
        Assert.Equal("6", Value(world, "Level"));
    }

    [Fact]
    public void The_chosen_boons_are_listed_with_their_count_and_total()
    {
        var world = World();

        Choose(world.Player, Boon.Experience);
        Choose(world.Player, Boon.Damage, 2);

        Assert.Equal(
            [
                new("STRENGTH x2", Text($"+{Boons.Damage * 2 * 100f:0.#} % damage")),
                new("WISDOM x1", Text($"+{Boons.Experience * 100f:0.#} % experience")),
            ],
            CharacterSheet.Boons(world.Player, Boons));
    }

    [Fact]
    public void A_health_boon_counts_in_points()
    {
        var world = World();

        Choose(world.Player, Boon.Health, 3);

        Assert.Equal(
            [new SheetLine("VITALITY x3", Text($"+{Boons.Health * 3} max health"))],
            CharacterSheet.Boons(world.Player, Boons));
    }

    [Fact]
    public void Every_boon_has_a_name_and_an_effect()
    {
        Assert.All(Enum.GetValues<Boon>(), boon =>
        {
            Assert.False(string.IsNullOrWhiteSpace(BoonTexts.Name(boon)));
            Assert.StartsWith("+", BoonTexts.Effect(boon, Boons), StringComparison.Ordinal);
            Assert.EndsWith(BoonTexts.What(boon), BoonTexts.Effect(boon, Boons), StringComparison.Ordinal);
        });
        Assert.Equal(Text($"+{Boons.Damage * 100f:0.#} % damage"), BoonTexts.Effect(Boon.Damage, Boons));
        Assert.Equal(Text($"+{Boons.Health} max health"), BoonTexts.Effect(Boon.Health, Boons));
    }
}
