using System.Globalization;
using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// The first four items (decision record 0008, issue #195): Hunter's Boots, Iron Flask, Silver Rosary and Quickdraw
/// Holster - each with and without the item, through the ticks as in the game.
/// </summary>
public class ItemEffectTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly ItemsBalance Values = Balance.Default.Items;
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly PlayerInput Shoot = PlayerInput.None with { Shoot = true };
    private static readonly PlayerInput Reload = PlayerInput.None with { Reload = true };

    /// <summary>Every kill drops a blood vial (to have one to walk over).</summary>
    private static readonly Balance AlwaysDrop = Balance.Default with
    {
        Drops = Balance.Default.Drops with { GhoulChance = 1f, BatChance = 1f },
    };

    /// <summary>Ghouls with 1 health: one pistol shot kills.</summary>
    private static readonly Balance FrailGhouls = Balance.Default with
    {
        Ghoul = Balance.Default.Ghoul with { MaxHealth = 1 },
    };

    /// <summary>A corridor along +Z: the player in row 0, a ghoul 4 m ahead, room to walk on.</summary>
    private static readonly Level GhoulAhead = LevelParser.Parse("P\n.\nG\n.\n.");

    // ---- Hunter's Boots --------------------------------------------------------------------------------------------

    [Fact]
    public void Without_the_boots_a_player_walks_at_the_standard_top_speed()
    {
        var player = PlayerInCorridor();

        RunTicks(player, Forward, Second);

        Assert.Equal(Balance.Default.Player.MaxSpeed, player.HorizontalSpeed, 0.001f);
        Assert.Equal(Balance.Default.Player.MaxSpeed, player.WalkSpeed);
    }

    [Fact]
    public void With_the_boots_a_player_walks_faster()
    {
        var player = PlayerInCorridor();
        player.Take(ItemKind.HuntersBoots);

        RunTicks(player, Forward, Second);

        var expected = Balance.Default.Player.MaxSpeed * (1f + Values.HuntersBootsSpeed);
        Assert.Equal(expected, player.HorizontalSpeed, 0.001f);
        Assert.Equal(expected, player.WalkSpeed, 0.001f);
    }

    [Fact]
    public void With_the_boots_a_player_covers_more_ground_in_the_same_time()
    {
        var plain = PlayerInCorridor();
        var booted = PlayerInCorridor();
        booted.Take(ItemKind.HuntersBoots);
        var start = plain.Position;

        RunTicks(plain, Forward, 2 * Second);
        RunTicks(booted, Forward, 2 * Second);

        var plainWay = Vector3.Distance(start, plain.Position);
        var bootedWay = Vector3.Distance(start, booted.Position);
        Assert.True(bootedWay > plainWay, $"With the boots {bootedWay} m, without {plainWay} m.");
    }

    [Fact]
    public void The_boots_also_count_in_the_air()
    {
        var plain = PlayerInCorridor();
        var booted = PlayerInCorridor();
        booted.Take(ItemKind.HuntersBoots);
        var jumpForward = Forward with { Jump = true };

        RunTicks(plain, Forward, Second);
        RunTicks(booted, Forward, Second);
        plain.Tick(jumpForward);
        booted.Tick(jumpForward);
        RunTicks(plain, Forward, 10);
        RunTicks(booted, Forward, 10);

        Assert.False(booted.IsGrounded, "The player landed already - the test would prove nothing.");
        Assert.Equal(plain.HorizontalSpeed * (1f + Values.HuntersBootsSpeed), booted.HorizontalSpeed, 0.01f);
    }

    // ---- Iron Flask ------------------------------------------------------------------------------------------------

    [Fact]
    public void Without_the_flask_a_blood_vial_heals_its_standard_amount()
    {
        var (world, seen) = WalkOverAVial(health: 30, item: null);

        Assert.Equal(30 + Balance.Default.BloodVial.Heal, world.Player.Health);
        Assert.Contains(seen, e => e == new PlayerHealed(world.Player.Id, Balance.Default.BloodVial.Heal));
    }

    [Fact]
    public void With_the_flask_a_blood_vial_heals_more()
    {
        var (world, seen) = WalkOverAVial(health: 30, item: ItemKind.IronFlask);

        var heal = (int)Math.Round(Balance.Default.BloodVial.Heal * (1.0 + Values.IronFlaskHeal), MidpointRounding.AwayFromZero);
        Assert.True(heal > Balance.Default.BloodVial.Heal);
        Assert.Equal(30 + heal, world.Player.Health);
        Assert.Contains(seen, e => e == new PlayerHealed(world.Player.Id, heal));
    }

    [Fact]
    public void With_the_flask_healing_still_stops_at_the_maximum()
    {
        var health = Balance.Default.Player.MaxHealth - Balance.Default.BloodVial.Heal - 1;   // more than the vial alone, less than with the flask
        var (world, seen) = WalkOverAVial(health, ItemKind.IronFlask);

        Assert.Equal(world.Player.MaxHealth, world.Player.Health);
        Assert.Contains(seen, e => e == new PlayerHealed(world.Player.Id, Balance.Default.BloodVial.Heal + 1));
    }

    [Fact]
    public void The_flask_rounds_half_a_point_up()
    {
        // 3 * 1.5 = 4.5: half up gives 5, rounding to even would give 4. (The standard vial of 30 never lands on a half.)
        var items = new[] { ItemKind.IronFlask };
        Assert.Equal(4.5, 3 * (1.0 + Values.IronFlaskHeal));

        Assert.Equal(5, ItemEffects.VialHeal(3, items, Values));
        Assert.Equal(0, ItemEffects.VialHeal(0, items, Values));
    }

    [Fact]
    public void A_vial_cannot_heal_a_negative_amount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ItemEffects.VialHeal(-1, [ItemKind.IronFlask], Values));
    }

    // ---- Silver Rosary ---------------------------------------------------------------------------------------------

    [Fact]
    public void Without_the_rosary_a_kill_heals_nothing()
    {
        var world = new GameWorld(GhoulAhead, balance: FrailGhouls);
        world.Player.TakeDamage(20, []);

        var events = ShootAt(world, world.Ghouls[0]);

        Assert.True(world.Ghouls[0].IsDead);
        Assert.Equal(Balance.Default.Player.MaxHealth - 20, world.Player.Health);
        Assert.DoesNotContain(events, e => e is PlayerHealed);
    }

    [Fact]
    public void With_the_rosary_every_kill_heals()
    {
        var world = new GameWorld(GhoulAhead, balance: FrailGhouls);
        world.Player.Take(ItemKind.SilverRosary);
        world.Player.TakeDamage(20, []);

        var events = ShootAt(world, world.Ghouls[0]);

        Assert.True(world.Ghouls[0].IsDead);
        Assert.Equal(Balance.Default.Player.MaxHealth - 20 + Values.SilverRosaryHeal, world.Player.Health);
        Assert.Contains(events, e => e == new PlayerHealed(world.Player.Id, Values.SilverRosaryHeal));
    }

    [Fact]
    public void With_the_rosary_a_hit_that_does_not_kill_heals_nothing()
    {
        var world = new GameWorld(GhoulAhead);
        world.Player.Take(ItemKind.SilverRosary);
        world.Player.TakeDamage(20, []);

        var events = ShootAt(world, world.Ghouls[0]);

        Assert.Contains(events, e => e is GhoulHit);
        Assert.False(world.Ghouls[0].IsDead);
        Assert.DoesNotContain(events, e => e is PlayerHealed);
    }

    [Fact]
    public void With_the_rosary_a_kill_at_full_health_heals_nothing()
    {
        var world = new GameWorld(GhoulAhead, balance: FrailGhouls);
        world.Player.Take(ItemKind.SilverRosary);

        var events = ShootAt(world, world.Ghouls[0]);

        Assert.True(world.Ghouls[0].IsDead);
        Assert.Equal(world.Player.MaxHealth, world.Player.Health);
        Assert.DoesNotContain(events, e => e is PlayerHealed);
    }

    [Fact]
    public void With_the_rosary_killing_gorm_heals_too()
    {
        // Provisional (issue #195): "every kill" - the boss as well.
        var world = GormHallWorld(balance: Balance.Default with { Gorm = Balance.Default.Gorm with { MaxHealth = 10 } });
        world.Player.Take(ItemKind.SilverRosary);
        EnterGormHall(world);
        Run(world, PlayerInput.None, 2 * Second);
        world.Player.TakeDamage(20, []);
        Assert.True(world.Player.Health < world.Player.MaxHealth, "The player is at full health - the test would prove nothing.");

        var events = Run(world, Shoot, 1);

        Assert.Contains(events, e => e is GormDied);
        Assert.Contains(events, e => e == new PlayerHealed(world.Player.Id, Values.SilverRosaryHeal));
    }

    [Fact]
    public void With_the_rosary_a_kill_by_the_flame_heals_the_player_who_set_it_off()
    {
        // Provisional (issue #195): a kill by damage over time counts too. The ghoul stands still (it would hit the player
        // otherwise) and has exactly the health the whole standard flame takes; the player has number 3, so the heal
        // is seen to go to the one who set the flame off.
        var flame = Balance.Default.StatusEffects.CleansingFlame;
        var flameTicks = CleansingFlame.DurationTicks(flame);
        var wholeFlame = Enumerable.Range(1, flameTicks).Sum(tick => CleansingFlame.DamageOnTick(tick, flame));
        var world = new GameWorld(GhoulAhead, balance: Balance.Default with
        {
            Ghoul = Balance.Default.Ghoul with { Speed = 0f, MaxHealth = wholeFlame },
        });
        world.Player.Id = 3;
        world.Player.Take(ItemKind.SilverRosary);
        world.Player.TakeDamage(20, []);
        var ghoul = world.Ghouls[0];
        world.ApplyStatus(ghoul, StatusKind.CleansingFlame, world.Player);

        var events = Run(world, PlayerInput.None, flameTicks);

        Assert.True(ghoul.IsDead, "The flame did not kill - the test would prove nothing.");
        Assert.Contains(events, e => e == new TargetHitBy(ghoul.Id, 3, Weapon.CleansingFlame));
        Assert.Equal(new PlayerHealed(3, Values.SilverRosaryHeal), Assert.Single(events.OfType<PlayerHealed>()));
        Assert.Equal(Balance.Default.Player.MaxHealth - 20 + Values.SilverRosaryHeal, world.Player.Health);
    }

    [Fact]
    public void With_the_rosary_only_the_player_who_killed_heals()
    {
        // Online co-op (decision record 0003): the kill heals its attacker, not every player. The world holds one player
        // for now (GameWorld.Players), so the other one - also with a rosary, also hurt - stands beside it, as in the
        // leech tests: the kill must not reach it, and the heal names the number of the killer.
        var world = new GameWorld(GhoulAhead, balance: FrailGhouls);
        var killer = world.Player;
        killer.Id = 2;
        killer.Take(ItemKind.SilverRosary);
        killer.TakeDamage(20, []);
        var other = new Player(LevelParser.Parse("P")) { Id = 1 };
        other.Take(ItemKind.SilverRosary);
        other.TakeDamage(20, []);

        var events = ShootAt(world, world.Ghouls[0]);

        Assert.True(world.Ghouls[0].IsDead);
        Assert.Equal(new PlayerHealed(2, Values.SilverRosaryHeal), Assert.Single(events.OfType<PlayerHealed>()));
        Assert.Equal(Balance.Default.Player.MaxHealth - 20 + Values.SilverRosaryHeal, killer.Health);
        Assert.Equal(Balance.Default.Player.MaxHealth - 20, other.Health);
    }

    [Fact]
    public void With_the_rosary_a_dead_player_heals_nothing()
    {
        var player = new Player(LevelParser.Parse("P"));
        player.Take(ItemKind.SilverRosary);
        player.TakeDamage(player.Health, []);
        var events = new List<GameEvent>();

        Assert.Equal(0, player.HealByKill(events));
        Assert.Empty(events);
        Assert.Equal(0, player.Health);
    }

    // ---- Quickdraw Holster -----------------------------------------------------------------------------------------

    [Fact]
    public void Without_the_holster_the_pistol_reloads_in_its_standard_time()
    {
        var player = new Player(LevelParser.Parse("P"));

        Assert.Equal(SimulationClock.SecondsToTicks(Balance.Default.Pistol.ReloadTime), TicksToReload(player));
    }

    [Fact]
    public void With_the_holster_the_pistol_reloads_faster()
    {
        var player = new Player(LevelParser.Parse("P"));
        player.Take(ItemKind.QuickdrawHolster);

        var standard = SimulationClock.SecondsToTicks(Balance.Default.Pistol.ReloadTime);
        var expected = (int)Math.Round(standard * (1.0 - Values.QuickdrawHolsterReload), MidpointRounding.AwayFromZero);
        Assert.True(expected < standard);
        Assert.Equal(expected, TicksToReload(player));
        Assert.Equal(Balance.Default.Pistol.MagazineSize, player.Ranged.Magazine.Rounds);
    }

    [Theory]
    [InlineData(WeaponKind.Rifle)]
    [InlineData(WeaponKind.Blunderbuss)]
    public void With_the_holster_every_ranged_weapon_reloads_faster(WeaponKind kind)
    {
        var plain = new Player(LevelParser.Parse("P"));
        var holstered = new Player(LevelParser.Parse("P"));
        holstered.Take(ItemKind.QuickdrawHolster);
        plain.Equip(0, kind);
        holstered.Equip(0, kind);

        var standard = TicksToReload(plain);

        var expected = (int)Math.Round(standard * (1.0 - Values.QuickdrawHolsterReload), MidpointRounding.AwayFromZero);
        Assert.Equal(expected, TicksToReload(holstered));
    }

    [Fact]
    public void The_reload_bar_fills_over_the_shorter_reload()
    {
        var player = new Player(LevelParser.Parse("P"));
        player.Take(ItemKind.QuickdrawHolster);
        Tick(player, Shoot);
        Tick(player, Reload);
        var length = ItemEffects.ReloadTicks(player.Ranged.Magazine.ReloadTicks, player.Items, Values);

        RunTicks(player, PlayerInput.None, length / 2);   // the R tick only starts the reload

        Assert.Equal(0.5f, player.Ranged.Magazine.ReloadProgress, 1f / length);
    }

    [Fact]
    public void A_holster_taken_during_a_reload_counts_from_the_next_one()
    {
        // Provisional (issue #195): a reload under way keeps its length.
        var player = new Player(LevelParser.Parse("P"));
        var standard = SimulationClock.SecondsToTicks(Balance.Default.Pistol.ReloadTime);
        Tick(player, Shoot);
        Tick(player, Reload);
        player.Take(ItemKind.QuickdrawHolster);

        var ticks = TicksUntilReloaded(player);

        Assert.Equal(standard, ticks);
        Assert.True(TicksToReload(player) < standard);
    }

    [Fact]
    public void A_reload_shorter_than_one_tick_is_refused()
    {
        var magazine = new Magazine(3, 1f);
        magazine.TryTake();

        Assert.Throws<ArgumentOutOfRangeException>(() => magazine.StartReload(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ItemEffects.ReloadTicks(0, [ItemKind.QuickdrawHolster], Values));
        Assert.False(magazine.IsReloading);
    }

    [Fact]
    public void The_holster_never_makes_a_reload_shorter_than_one_tick()
    {
        Assert.Equal(1, ItemEffects.ReloadTicks(1, [ItemKind.QuickdrawHolster], Values with { QuickdrawHolsterReload = 0.9f }));
    }

    // ---- All four --------------------------------------------------------------------------------------------------

    [Fact]
    public void Without_their_item_the_rules_change_nothing()
    {
        ItemKind[] others = [ItemKind.BladeParry, ItemKind.HuntersEye, ItemKind.Leech, ItemKind.AshenHeart];

        Assert.Equal(1f, ItemEffects.SpeedFactor(others, Values));
        Assert.Equal(Balance.Default.BloodVial.Heal, ItemEffects.VialHeal(Balance.Default.BloodVial.Heal, others, Values));
        Assert.Equal(0, ItemEffects.KillHeal(others, Values));
        Assert.Equal(90, ItemEffects.ReloadTicks(90, others, Values));
    }

    [Fact]
    public void The_effect_lines_name_the_standard_values()
    {
        // The lines shown in the game are fixed text; if the balancing changes a number, they must change with it.
        Assert.Contains(Percent(Values.HuntersBootsSpeed), Items.Info(ItemKind.HuntersBoots).Effect, StringComparison.Ordinal);
        Assert.Contains(Percent(Values.IronFlaskHeal), Items.Info(ItemKind.IronFlask).Effect, StringComparison.Ordinal);
        Assert.Contains($"heals {Values.SilverRosaryHeal}", Items.Info(ItemKind.SilverRosary).Effect, StringComparison.Ordinal);
        Assert.Contains(Percent(Values.QuickdrawHolsterReload), Items.Info(ItemKind.QuickdrawHolster).Effect, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("huntersBootsSpeed")]
    [InlineData("ironFlaskHeal")]
    [InlineData("silverRosaryHeal")]
    [InlineData("quickdrawHolsterReload")]
    public void An_item_may_be_set_to_do_nothing_but_not_to_work_backwards(string name)
    {
        var zero = BalanceFile.Parse($$"""{ "items": { "{{name}}": 0 } }""");
        var negative = BalanceFile.Parse($$"""{ "items": { "{{name}}": -1 } }""");

        Assert.Empty(zero.Warnings);
        Assert.Equal(Balance.Default, negative.Balance);
        Assert.Contains(negative.Warnings, w => w.Contains($"items.{name}", StringComparison.Ordinal));
    }

    [Fact]
    public void A_holster_that_would_make_reloading_take_no_time_is_refused()
    {
        var result = BalanceFile.Parse("""{ "items": { "quickdrawHolsterReload": 1 } }""");

        Assert.Equal(Values.QuickdrawHolsterReload, result.Balance.Items.QuickdrawHolsterReload);
        Assert.Contains(result.Warnings, w => w.Contains("items.quickdrawHolsterReload", StringComparison.Ordinal));
    }

    // ---- Helpers ---------------------------------------------------------------------------------------------------

    private static Player PlayerInCorridor() =>
        new(LevelParser.Parse(string.Join('\n', Enumerable.Repeat(".", 40).Prepend("P"))));

    private static string Percent(float share) =>
        string.Create(CultureInfo.InvariantCulture, $"{Math.Round(share * 100):0}%");

    /// <summary>A player with <paramref name="health"/> (and <paramref name="item"/>, if any) walks over a dropped blood vial.</summary>
    private static (GameWorld World, List<GameEvent> Seen) WalkOverAVial(int health, ItemKind? item)
    {
        var world = new GameWorld(GhoulAhead, balance: AlwaysDrop);
        var player = world.Player;
        if (item is { } kind)
        {
            player.Take(kind);
        }

        player.TakeDamage(player.Health - health, []);
        world.Ghouls[0].TakeDamage(10_000, []);
        world.Tick(PlayerInput.None);
        Assert.Single(world.Pickups);

        var seen = Run(world, Forward, Second);

        Assert.Empty(world.Pickups);
        return (world, seen);
    }

    /// <summary>Turns the player towards the body of <paramref name="ghoul"/> and fires once (one tick).</summary>
    private static List<GameEvent> ShootAt(GameWorld world, Ghoul ghoul)
    {
        var player = world.Player;
        var direction = Vector3.Normalize(ghoul.Position + new Vector3(0f, 0.5f, 0f) - player.EyePosition);
        player.Look(PlayerInput.None with
        {
            Look = new Vector2(
                -(MathF.Atan2(direction.X, direction.Z) - player.View.YawRadians) / ControlSettings.MouseSensitivity,
                -(MathF.Asin(direction.Y) - player.View.PitchRadians) / ControlSettings.MouseSensitivity),
        });
        return Run(world, Shoot, 1);
    }

    private static void Tick(Player player, PlayerInput input) => player.Tick(input);

    /// <summary>Fires one round, presses R and counts the ticks after the R tick until the magazine is full again.</summary>
    private static int TicksToReload(Player player)
    {
        Tick(player, Shoot);
        RunTicks(player, PlayerInput.None, SimulationClock.SecondsToTicks(Balance.Default.Ranged(player.Ranged.Kind).Cooldown) + 1);
        Tick(player, Reload);
        Assert.True(player.Ranged.Magazine.IsReloading, "The reload did not start - the test would prove nothing.");
        return TicksUntilReloaded(player);
    }

    /// <summary>Ticks the player until the running reload is done; returns how many ticks that took.</summary>
    private static int TicksUntilReloaded(Player player)
    {
        for (var ticks = 1; ticks <= 10 * Second; ticks++)
        {
            player.Tick(PlayerInput.None);
            if (player.Events.Any(e => e is ReloadFinished))
            {
                return ticks;
            }
        }

        throw new InvalidOperationException("The reload never finished.");
    }
}
