using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>Charging for every melee weapon, and the club with its knockback (decision record 0008, issue #190).</summary>
public class ClubTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly MeleeBalance Club = Balance.Default.Club;
    private static readonly int FullCharge = SimulationClock.SecondsToTicks(Club.ChargeTime);
    private static readonly int KnockTicks = SimulationClock.SecondsToTicks(Club.KnockbackTime);
    private static readonly PlayerInput Melee = PlayerInput.None with { Melee = true };

    private static List<GameEvent> Run(GameWorld world, PlayerInput input, int ticks)
    {
        var events = new List<GameEvent>();
        for (var tick = 0; tick < ticks; tick++)
        {
            world.Tick(input);
            events.AddRange(world.Events);
        }

        return events;
    }

    /// <summary>Holds the melee button <paramref name="ticks"/> ticks and lets go (the swing happens on letting go).</summary>
    private static List<GameEvent> Swing(GameWorld world, int ticks = 1)
    {
        var events = Run(world, Melee, ticks);
        events.AddRange(Run(world, PlayerInput.None, 1));
        return events;
    }

    /// <summary>A world with the player looking along +Z and the club in the hand.</summary>
    private static GameWorld WithClub(string map, Balance? balance = null)
    {
        var world = new GameWorld(LevelParser.Parse(map), 1, balance ?? Balance.Default);
        world.Player.EquipMelee(WeaponKind.Club);
        return world;
    }

    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    // ---- The weapon ------------------------------------------------------------------------------------------------

    [Fact]
    public void The_club_is_a_worn_melee_weapon()
    {
        Assert.Equal(new WeaponInfo("Club", WeaponHand.Melee, ItemTier.Worn), Weapons.Info(WeaponKind.Club));
        Assert.Same(Balance.Default.Club, Balance.Default.Melee(WeaponKind.Club));
        Assert.Same(Balance.Default.Blade, Balance.Default.Melee(WeaponKind.SilverBlade));
    }

    [Fact]
    public void Taking_the_club_changes_the_hand_and_its_cooldown()
    {
        var player = new Player(LevelParser.Parse("P"));

        player.EquipMelee(WeaponKind.Club);

        Assert.Equal(WeaponKind.Club, player.Melee);
        Assert.Equal(SimulationClock.SecondsToTicks(Club.Cooldown), player.MeleeCooldown.DurationTicks);
        Assert.False(player.MeleeCharge.IsCharging);
    }

    [Fact]
    public void A_ranged_weapon_cannot_go_into_the_melee_hand()
    {
        var player = new Player(LevelParser.Parse("P"));

        Assert.Throws<ArgumentException>(() => player.EquipMelee(WeaponKind.Pistol));
        Assert.Equal(WeaponKind.SilverBlade, player.Melee);
    }

    [Fact]
    public void The_club_charges_like_the_blade_and_says_which_weapon_swung()
    {
        var world = WithClub("P\n.\n.");

        var events = Swing(world, FullCharge + 2);

        var swing = Assert.Single(events.OfType<MeleeSwung>());
        Assert.Equal(WeaponKind.Club, swing.Weapon);
        Assert.Equal(1f, swing.Charge);
    }

    [Fact]
    public void A_full_charge_makes_the_club_hit_harder()
    {
        var rules = DamageRules.Standard();

        var plain = rules.Calculate(new Hit(Weapon.Club, HitZone.Body, Club.Damage, 0f));
        var charged = rules.Calculate(new Hit(Weapon.Club, HitZone.Body, Club.Damage, 1f));

        Assert.Equal(Club.Damage, plain.Amount);
        Assert.Equal((int)MathF.Round(Club.Damage * Club.ChargeMaxMultiplier), charged.Amount);
    }

    // ---- Hitting ---------------------------------------------------------------------------------------------------

    [Fact]
    public void The_club_does_its_own_damage()
    {
        var world = WithClub("P\nG");
        var ghoul = world.Ghouls[0];

        Swing(world);

        Assert.Equal(Balance.Default.Ghoul.MaxHealth - Club.Damage, ghoul.Health);
    }

    /// <summary>Ghouls that survive a charged club, so the push can be measured.</summary>
    private static readonly Balance ToughGhouls = Balance.Default with { Ghoul = Balance.Default.Ghoul with { MaxHealth = 10_000 } };

    /// <summary>
    /// Holds the club <paramref name="holdTicks"/> ticks; then the ghoul's position is taken, the club let go (the swing
    /// and the first tick of the push) and the rest of the push run. Returns how far the ghoul moved along +Z.
    /// </summary>
    private static float PushOf(GameWorld world, Ghoul ghoul, int holdTicks)
    {
        Run(world, Melee, holdTicks);
        var before = ghoul.Position;
        Run(world, PlayerInput.None, 1);
        Assert.True(ghoul.Knockback.IsActive || KnockTicks == 1, "The club missed the ghoul - the test would prove nothing.");
        Run(world, PlayerInput.None, KnockTicks - 1);
        Assert.False(ghoul.Knockback.IsActive);
        Assert.Equal(before.X, ghoul.Position.X, 0.01f);
        return ghoul.Position.Z - before.Z;
    }

    [Fact]
    public void A_charged_club_uses_its_own_charge_bonus()
    {
        // The club's bonus differs from the blade's here - so a mix-up would show.
        var balance = ToughGhouls with { Club = Club with { ChargeMaxMultiplier = 3f } };
        var world = WithClub("P\nG\n.\n.\n.\n.\n.\n.\n.", balance);
        var ghoul = world.Ghouls[0];

        Swing(world, FullCharge + 2);

        Assert.Equal(balance.Ghoul.MaxHealth - (Club.Damage * 3), ghoul.Health);
    }

    [Fact]
    public void A_club_hit_knocks_a_ghoul_back_by_its_distance()
    {
        var world = WithClub("P\nG\n.\n.\n.\n.", ToughGhouls);

        var pushed = PushOf(world, world.Ghouls[0], 1);

        Assert.Equal(Club.Knockback, pushed, 0.05f);
    }

    [Fact]
    public void A_charged_club_hit_knocks_it_further()
    {
        var world = WithClub("P\nG\n.\n.\n.\n.\n.", ToughGhouls);

        var pushed = PushOf(world, world.Ghouls[0], FullCharge + 2);

        Assert.Equal(Club.ChargedKnockback, pushed, 0.05f);
    }

    [Fact]
    public void A_wall_stops_the_push()
    {
        // Only one free cell behind the ghoul.
        var world = WithClub("P\nG\n.\n#", ToughGhouls);
        var ghoul = world.Ghouls[0];

        var pushed = PushOf(world, ghoul, FullCharge + 2);

        Assert.True(pushed > 0.5f, $"The ghoul was hardly pushed ({pushed} m) - the test would prove nothing.");
        Assert.True(pushed < Club.ChargedKnockback - 1f, $"The wall did not stop the push ({pushed} m).");
        Assert.True(ghoul.Position.Z + BodySizes.Ghoul.Radius <= 3f * Level.CellSize + 0.01f, $"Pushed into the wall to z {ghoul.Position.Z}.");
    }

    [Fact]
    public void A_bat_is_knocked_back_too()
    {
        // A bat that survives the club (a normal one dies at once - and the dead are not pushed).
        var toughBats = Balance.Default with { Bat = Balance.Default.Bat with { MaxHealth = 10_000 } };
        var world = WithClub("P\nB\n.\n.\n.\n.", toughBats);
        var bat = world.Bats[0];
        Run(world, Melee, 1);
        var before = bat.Position;

        Run(world, PlayerInput.None, 1);
        Assert.True(bat.Knockback.IsActive, "The club missed the bat - the test would prove nothing.");
        Run(world, PlayerInput.None, KnockTicks - 1);

        Assert.False(bat.Knockback.IsActive);
        Assert.Equal(Club.Knockback, FlatDistance(bat.Position, before), 0.1f);
    }

    [Fact]
    public void Gorm_stands_firm()
    {
        var noAttacks = Balance.Default with { Gorm = Balance.Default.Gorm with { NearRange = 0f, FarRange = 0f, StopDistance = 0.5f } };
        var world = WithClub("P\n.\nT\n.\n.\n.", noAttacks);
        var gorm = world.Gorm!;
        Run(world, PlayerInput.None, Second, () => FlatDistance(gorm.Position, world.Player.Position) < 2.5f);
        var before = FlatDistance(gorm.Position, world.Player.Position);

        Swing(world, FullCharge + 2);
        Run(world, PlayerInput.None, KnockTicks);

        Assert.True(gorm.Health < gorm.MaxHealth, "The club missed Gorm - the test would prove nothing.");
        Assert.True(FlatDistance(gorm.Position, world.Player.Position) <= before + 0.05f, "Gorm was pushed away.");
    }

    [Fact]
    public void The_blade_does_not_knock_back()
    {
        var world = new GameWorld(LevelParser.Parse("P\nG\n.\n.\n."));
        var ghoul = world.Ghouls[0];

        Swing(world, SimulationClock.SecondsToTicks(Balance.Default.Blade.ChargeTime) + 2);

        Assert.False(ghoul.Knockback.IsActive);
    }

    [Fact]
    public void A_dead_ghoul_is_not_knocked_back()
    {
        var strong = Balance.Default with { Club = Club with { Damage = 10_000 } };
        var world = WithClub("P\nG\n.\n.\n.", strong);
        var ghoul = world.Ghouls[0];

        Swing(world);

        Assert.True(ghoul.IsDead);
        Assert.False(ghoul.Knockback.IsActive);
    }

    private static void Run(GameWorld world, PlayerInput input, int ticks, Func<bool> until)
    {
        for (var tick = 0; tick < ticks && !until(); tick++)
        {
            world.Tick(input);
        }
    }
}
