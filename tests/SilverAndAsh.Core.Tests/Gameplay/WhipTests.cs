using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// The whip (issue #202): a long, narrow reach, medium damage, chargeable - and a hit pulls ghouls and bats in towards
/// the one who struck, Gorm not.
/// </summary>
public class WhipTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly MeleeBalance Whip = Balance.Default.Whip;
    private static readonly int FullCharge = SimulationClock.SecondsToTicks(Whip.ChargeTime);
    private static readonly int PullTicks = SimulationClock.SecondsToTicks(Whip.KnockbackTime);
    private static readonly PlayerInput Melee = PlayerInput.None with { Melee = true };

    // How far inside and outside a limit of the whip a ghoul is put: degrees for the cone's edge, metres for its reach.
    private const float EdgeDegrees = 3f;
    private const float EdgeMetres = 0.1f;

    /// <summary>Ghouls that survive even a charged whip, so every hit can be read off their health.</summary>
    private static readonly Balance ToughGhouls = Balance.Default with { Ghoul = Balance.Default.Ghoul with { MaxHealth = 10_000 } };

    /// <summary>Tough ghouls that neither see nor walk (sight range 0), so they stand where they were put until the swing.</summary>
    private static readonly Balance StillGhouls = ToughGhouls with { Ghoul = ToughGhouls.Ghoul with { SightRange = 0f } };

    /// <summary>Holds the melee button <paramref name="ticks"/> ticks and lets go (the swing happens on letting go).</summary>
    private static List<GameEvent> Swing(GameWorld world, int ticks = 1)
    {
        var events = Run(world, Melee, ticks);
        events.AddRange(Run(world, PlayerInput.None, 1));
        return events;
    }

    /// <summary>A world with the player looking along +Z and <paramref name="weapon"/> in the hand.</summary>
    private static GameWorld With(WeaponKind weapon, string map, Balance? balance = null)
    {
        var world = new GameWorld(LevelParser.Parse(map), 1, balance ?? Balance.Default);
        world.Player.EquipMelee(weapon);
        return world;
    }

    /// <summary>
    /// Swings once (a tap) and lets the pull run to its end - the swing's tick is the pull's first. Returns everything
    /// that happened.
    /// </summary>
    private static List<GameEvent> SwingAndPull(GameWorld world, Knockback knockback)
    {
        var events = Swing(world);
        Assert.True(knockback.IsActive, "The whip did not pull - the test would prove nothing.");
        events.AddRange(Run(world, PlayerInput.None, PullTicks - 1));
        Assert.False(knockback.IsActive);
        return events;
    }

    /// <summary>
    /// Puts <paramref name="ghoul"/> straight ahead so that its surface is <paramref name="surface"/> metres from the eyes
    /// (the reach of a swing counts from the eyes to the body's surface).
    /// </summary>
    private static void PutAhead(GameWorld world, Ghoul ghoul, float surface)
    {
        var player = world.Player;
        var forward = Vector3.Normalize(player.View.Forward with { Y = 0f });
        var drop = player.EyePosition.Y - (player.Position.Y + BodySizes.Ghoul.Height);
        var flat = MathF.Sqrt(MathF.Pow(surface + BodySizes.Ghoul.Radius, 2) - drop * drop);
        ghoul.PlaceAt((player.EyePosition + forward * flat) with { Y = player.Position.Y });
    }

    /// <summary>
    /// Puts <paramref name="ghoul"/> on the floor so that the top of its axis lies <paramref name="fromTip"/> metres from
    /// the tip of the whip's cone, at <paramref name="degrees"/> to the right of the looking direction.
    /// </summary>
    private static void PutAtAngle(GameWorld world, Ghoul ghoul, float degrees, float fromTip)
    {
        var player = world.Player;
        var forward = Vector3.Normalize(player.View.Forward with { Y = 0f });
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var drop = player.EyePosition.Y - (player.Position.Y + BodySizes.Ghoul.Height);
        var tip = player.EyePosition - forward * Whip.ConeStartBehind;
        var radians = degrees * MathF.PI / 180f;
        var along = fromTip * MathF.Cos(radians);
        var aside = MathF.Sqrt(MathF.Pow(fromTip * MathF.Sin(radians), 2) - drop * drop);
        ghoul.PlaceAt((tip + forward * along + right * aside) with { Y = player.Position.Y });
    }

    // ---- The weapon ------------------------------------------------------------------------------------------------

    [Fact]
    public void The_whip_is_a_royal_melee_weapon_with_its_own_values()
    {
        Assert.Equal(new WeaponInfo("Whip", WeaponHand.Melee, ItemTier.Royal), Weapons.Info(WeaponKind.Whip));
        Assert.Same(Balance.Default.Whip, Balance.Default.Melee(WeaponKind.Whip));
        Assert.Equal(Weapon.Whip, Weapons.ForDamage(WeaponKind.Whip));
    }

    [Fact]
    public void The_whip_reaches_far_and_does_medium_damage()
    {
        Assert.True(Whip.Range > Balance.Default.HuntersAxe.Range, "The whip must reach further than every other melee weapon.");
        Assert.True(Whip.Range > Balance.Default.Club.Range);
        Assert.InRange(Whip.Damage, Balance.Default.Blade.Damage, Balance.Default.HuntersAxe.Damage);
        Assert.True(Whip.PullTo > 0f, "The whip must pull.");
    }

    // ---- Reach and cone --------------------------------------------------------------------------------------------

    // A long hall (cells of 2 m); the player stands at one end looking along +Z.
    private const string Hall = ".P.\n...\n...\n...\n...\n.G.";

    [Fact]
    public void The_whip_reaches_a_ghoul_just_inside_its_range_but_not_one_just_beyond()
    {
        var near = With(WeaponKind.Whip, Hall, StillGhouls);
        var far = With(WeaponKind.Whip, Hall, StillGhouls);
        PutAhead(near, near.Ghouls[0], Whip.Range - EdgeMetres);
        PutAhead(far, far.Ghouls[0], Whip.Range + EdgeMetres);
        var full = StillGhouls.Ghoul.MaxHealth;

        Swing(near);
        var events = Swing(far);

        Assert.Equal(full - Whip.Damage, near.Ghouls[0].Health);
        Assert.Equal(full, far.Ghouls[0].Health);
        Assert.DoesNotContain(events, e => e is MeleePulled);
    }

    [Fact]
    public void The_whip_s_cone_is_narrow()
    {
        var inside = With(WeaponKind.Whip, Hall, StillGhouls);
        var outside = With(WeaponKind.Whip, Hall, StillGhouls);
        PutAtAngle(inside, inside.Ghouls[0], Whip.Angle / 2f - EdgeDegrees, 3f);
        PutAtAngle(outside, outside.Ghouls[0], Whip.Angle / 2f + EdgeDegrees, 3f);
        var full = StillGhouls.Ghoul.MaxHealth;

        Swing(inside);
        Swing(outside);

        Assert.Equal(full - Whip.Damage, inside.Ghouls[0].Health);
        Assert.Equal(full, outside.Ghouls[0].Health);
    }

    // ---- Damage and charging ---------------------------------------------------------------------------------------

    [Fact]
    public void The_whip_does_its_own_damage()
    {
        var world = With(WeaponKind.Whip, "P\nG", ToughGhouls);

        Swing(world);

        Assert.Equal(ToughGhouls.Ghoul.MaxHealth - Whip.Damage, world.Ghouls[0].Health);
    }

    [Fact]
    public void A_fully_charged_whip_hits_harder()
    {
        var world = With(WeaponKind.Whip, "P\nG", StillGhouls);

        Swing(world, FullCharge + 2);

        Assert.Equal(StillGhouls.Ghoul.MaxHealth - (int)MathF.Round(Whip.Damage * Whip.ChargeMaxMultiplier), world.Ghouls[0].Health);
    }

    [Fact]
    public void Charging_the_whip_reports_its_start_and_its_own_charge_time()
    {
        var world = With(WeaponKind.Whip, "P\n.\n.");

        var events = Swing(world, FullCharge + 2);

        var started = Assert.Single(events.OfType<MeleeChargeStarted>());
        Assert.Equal(new MeleeChargeStarted(world.Player.Id, WeaponKind.Whip, FullCharge), started);
        var swung = Assert.Single(events.OfType<MeleeSwung>());
        Assert.Equal(WeaponKind.Whip, swung.Weapon);
        Assert.Equal(1f, swung.Charge);
    }

    // ---- Pulling ---------------------------------------------------------------------------------------------------

    [Fact]
    public void A_hit_pulls_a_ghoul_in_to_the_pull_distance()
    {
        var world = With(WeaponKind.Whip, "P\n.\nG\n.", StillGhouls);
        var ghoul = world.Ghouls[0];
        var player = world.Player;
        var before = ghoul.Position;

        var events = SwingAndPull(world, ghoul.Knockback);

        Assert.Equal(Whip.PullTo, FlatDistance(ghoul.Position, player.Position), 0.01f);
        Assert.Equal(before.X, ghoul.Position.X, 0.01f);
        Assert.Equal(before.Y, ghoul.Position.Y, 0.001f);
        var pulled = Assert.Single(events.OfType<MeleePulled>());
        Assert.Equal((ghoul.Id, player.Id, WeaponKind.Whip), (pulled.TargetId, pulled.PlayerId, pulled.Weapon));
        Assert.Equal(FlatDistance(before, ghoul.Position), pulled.Distance, 0.01f);
        Assert.Equal(before, pulled.Place);
    }

    [Fact]
    public void The_pull_comes_closer_tick_by_tick_and_then_stops()
    {
        var world = With(WeaponKind.Whip, "P\n.\nG\n.", StillGhouls);
        var ghoul = world.Ghouls[0];
        var start = FlatDistance(ghoul.Position, world.Player.Position);

        Swing(world);
        var distances = new List<float> { FlatDistance(ghoul.Position, world.Player.Position) };
        for (var tick = 1; tick < PullTicks; tick++)
        {
            Run(world, PlayerInput.None, 1);
            distances.Add(FlatDistance(ghoul.Position, world.Player.Position));
        }

        var step = (start - Whip.PullTo) / PullTicks;
        Assert.Equal(start - step, distances[0], 0.01f);
        Assert.All(distances.Zip(distances.Skip(1)), pair => Assert.Equal(step, pair.First - pair.Second, 0.01f));
        Assert.Equal(Whip.PullTo, distances[^1], 0.01f);
        Run(world, PlayerInput.None, Second);
        Assert.Equal(Whip.PullTo, FlatDistance(ghoul.Position, world.Player.Position), 0.01f);
    }

    [Fact]
    public void A_charged_hit_pulls_just_as_far()
    {
        var world = With(WeaponKind.Whip, "P\n.\nG\n.", StillGhouls);
        var ghoul = world.Ghouls[0];

        Swing(world, FullCharge + 2);
        Run(world, PlayerInput.None, PullTicks - 1);

        Assert.False(ghoul.Knockback.IsActive);
        Assert.Equal(Whip.PullTo, FlatDistance(ghoul.Position, world.Player.Position), 0.01f);
    }

    /// <summary>Bats that survive the whip and do not see the player, so they hang still where they spawned until hit.</summary>
    private static readonly Balance StillBats =
        Balance.Default with { Bat = Balance.Default.Bat with { MaxHealth = 10_000, SightRange = 0f } };

    [Fact]
    public void A_hit_pulls_a_bat_out_of_the_air_flat_keeping_its_height()
    {
        var world = With(WeaponKind.Whip, "P\n.\nB\n.", StillBats);
        var bat = world.Bats[0];
        var player = world.Player;
        var before = bat.Position;
        Assert.True(before.Y > BodySizes.Player.Height, $"The bat hangs at {before.Y} m - not in the air, the test would prove nothing.");

        var events = SwingAndPull(world, bat.Knockback);

        Assert.True(bat.Health < StillBats.Bat.MaxHealth, "The whip missed the bat.");
        Assert.Equal(Whip.PullTo, FlatDistance(bat.Position, player.Position), 0.01f);
        Assert.Equal(before.Y, bat.Position.Y, 0.001f);
        var pulled = Assert.Single(events.OfType<MeleePulled>());
        Assert.Equal((bat.Id, player.Id), (pulled.TargetId, pulled.PlayerId));
        Assert.Equal(FlatDistance(before, bat.Position), pulled.Distance, 0.01f);
    }

    [Fact]
    public void Gorm_is_not_pulled()
    {
        // A Gorm that neither attacks nor walks, so any move of his could only be a pull.
        var still = Balance.Default with { Gorm = Balance.Default.Gorm with { NearRange = 0f, FarRange = 0f, Speed = 0f } };
        var world = With(WeaponKind.Whip, "P\n.\nT\n.\n.\n.", still);
        var gorm = world.Gorm!;
        var before = gorm.Position;
        Assert.True(FlatDistance(before, world.Player.Position) > Whip.PullTo + 1f, "Gorm stands too near - a pull would not show.");

        var events = Swing(world, FullCharge + 2);
        events.AddRange(Run(world, PlayerInput.None, PullTicks));

        Assert.True(gorm.Health < gorm.MaxHealth, "The whip missed Gorm - the test would prove nothing.");
        Assert.DoesNotContain(events, e => e is MeleePulled);
        Assert.Equal(before, gorm.Position);
    }

    [Fact]
    public void A_ghoul_nearer_than_the_pull_distance_stays_where_it_is()
    {
        var world = With(WeaponKind.Whip, "P\n.\nG", StillGhouls);
        var ghoul = world.Ghouls[0];
        var player = world.Player;
        ghoul.PlaceAt(player.Position + new Vector3(0f, 0f, Whip.PullTo - 0.2f));
        var before = ghoul.Position;

        // A hit wakes the ghoul and it walks on afterwards - so the pull is read off the swing's own tick.
        var events = Swing(world);

        Assert.True(ghoul.Health < StillGhouls.Ghoul.MaxHealth, "The whip missed the ghoul - the test would prove nothing.");
        Assert.False(ghoul.Knockback.IsActive);
        Assert.DoesNotContain(events, e => e is MeleePulled);
        Assert.True(FlatDistance(ghoul.Position, before) < 0.1f, $"The ghoul moved {FlatDistance(ghoul.Position, before)} m.");
    }

    [Fact]
    public void The_whip_hits_a_ghoul_right_at_your_feet()
    {
        // The cone is narrow, but a ghoul is lower than the eyes: up close the whip must still reach down to it.
        var world = With(WeaponKind.Whip, "P\n.\nG", StillGhouls);
        var ghoul = world.Ghouls[0];
        var touching = BodySizes.Player.Radius + BodySizes.Ghoul.Radius + 0.01f;
        ghoul.PlaceAt(world.Player.Position + new Vector3(0f, 0f, touching));

        Swing(world);

        Assert.Equal(StillGhouls.Ghoul.MaxHealth - Whip.Damage, ghoul.Health);
    }

    [Fact]
    public void A_ghoul_the_whip_kills_is_not_pulled()
    {
        var weak = Balance.Default with { Ghoul = Balance.Default.Ghoul with { MaxHealth = 1, SightRange = 0f } };
        var world = With(WeaponKind.Whip, "P\n.\nG\n.", weak);
        var ghoul = world.Ghouls[0];
        var before = ghoul.Position;

        var events = Swing(world);
        events.AddRange(Run(world, PlayerInput.None, PullTicks));

        Assert.True(ghoul.IsDead, "The whip did not kill the ghoul - the test would prove nothing.");
        Assert.False(ghoul.Knockback.IsActive);
        Assert.DoesNotContain(events, e => e is MeleePulled);
        Assert.Equal(before.Z, ghoul.Position.Z, 0.001f);
    }

    [Fact]
    public void No_other_melee_weapon_pulls()
    {
        foreach (var weapon in new[] { WeaponKind.SilverBlade, WeaponKind.Club, WeaponKind.HuntersAxe })
        {
            var world = With(weapon, "P\nG\n.", StillGhouls);

            var events = Swing(world, SimulationClock.SecondsToTicks(Balance.Default.Melee(weapon).ChargeTime) + 2);
            events.AddRange(Run(world, PlayerInput.None, Second));

            Assert.True(world.Ghouls[0].Health < StillGhouls.Ghoul.MaxHealth, $"The {weapon} missed - the test would prove nothing.");
            Assert.DoesNotContain(events, e => e is MeleePulled);
        }
    }

    // ---- What stops a pull -----------------------------------------------------------------------------------------

    [Fact]
    public void A_wall_between_shields_the_ghoul_from_hit_and_pull()
    {
        var world = With(WeaponKind.Whip, "P\n#\nG", StillGhouls);
        var ghoul = world.Ghouls[0];
        var before = ghoul.Position;

        var events = Swing(world, FullCharge + 2);
        events.AddRange(Run(world, PlayerInput.None, PullTicks));

        Assert.Equal(StillGhouls.Ghoul.MaxHealth, ghoul.Health);
        Assert.DoesNotContain(events, e => e is MeleePulled);
        Assert.Equal(before, ghoul.Position);
    }

    [Fact]
    public void A_ledge_too_high_to_step_up_ends_the_pull_at_its_foot()
    {
        // The cell between is 0.75 m high - a ghoul cannot step up that far (it steps 0.4 m).
        var world = With(WeaponKind.Whip, "P\n.\n3\nG", StillGhouls);
        var ghoul = world.Ghouls[0];
        var before = ghoul.Position;
        var ledgeFar = 6f;

        var events = SwingAndPull(world, ghoul.Knockback);

        Assert.True(ghoul.Health < StillGhouls.Ghoul.MaxHealth, "The whip missed the ghoul - the test would prove nothing.");
        Assert.True(ghoul.Position.Z >= ledgeFar + BodySizes.Ghoul.Radius - 0.06f, $"The ghoul was pulled onto the ledge (z = {ghoul.Position.Z}).");
        Assert.True(ghoul.Position.Z < before.Z - 0.3f, $"The ghoul was hardly pulled (z = {ghoul.Position.Z}) - the test would prove nothing.");
        Assert.Equal(before.Y, ghoul.Position.Y, 0.001f);
        var pulled = Assert.Single(events.OfType<MeleePulled>());
        Assert.Equal(FlatDistance(before, ghoul.Position), pulled.Distance, 0.01f);
    }

    [Fact]
    public void A_ghoul_is_not_dragged_into_a_pit()
    {
        var world = With(WeaponKind.Whip, "P\n.\n_\nG", StillGhouls);
        var ghoul = world.Ghouls[0];
        var before = ghoul.Position;
        var pitFar = 6f;

        var events = SwingAndPull(world, ghoul.Knockback);

        Assert.True(ghoul.Position.Z > pitFar - BodySizes.Ghoul.Radius, $"The ghoul hangs over the pit (z = {ghoul.Position.Z}).");
        Assert.True(ghoul.Position.Z < pitFar, $"The ghoul was not pulled to the edge (z = {ghoul.Position.Z}).");
        var pulled = Assert.Single(events.OfType<MeleePulled>());
        Assert.Equal(FlatDistance(before, ghoul.Position), pulled.Distance, 0.01f);

        Run(world, PlayerInput.None, Second);
        Assert.Equal(before.Y, ghoul.Position.Y, 0.001f);
    }

    [Fact]
    public void A_ghoul_at_the_edge_of_a_pit_is_not_pulled_at_all()
    {
        var world = With(WeaponKind.Whip, "P\n.\n_\nG", StillGhouls);
        var ghoul = world.Ghouls[0];
        // Its circle still just touches the floor beyond the pit (0.03 m) - one step more and it would stand over it.
        ghoul.PlaceAt(ghoul.Position with { Z = 6f - BodySizes.Ghoul.Radius + 0.03f });
        var before = ghoul.Position;

        var events = Swing(world);

        Assert.True(ghoul.Health < StillGhouls.Ghoul.MaxHealth, "The whip missed the ghoul - the test would prove nothing.");
        Assert.False(ghoul.Knockback.IsActive);
        Assert.DoesNotContain(events, e => e is MeleePulled);
        Assert.True(ghoul.Position.Z >= before.Z - 0.1f, $"The ghoul was pulled to z = {ghoul.Position.Z}.");
    }

    [Fact]
    public void A_bat_is_pulled_over_a_pit()
    {
        var world = With(WeaponKind.Whip, "P\n.\n_\nB", StillBats);
        var bat = world.Bats[0];

        SwingAndPull(world, bat.Knockback);

        Assert.Equal(Whip.PullTo, FlatDistance(bat.Position, world.Player.Position), 0.01f);
    }

    // ---- Invalid values in balance.json ----------------------------------------------------------------------------

    [Fact]
    public void A_cone_wider_than_180_degrees_falls_back_to_the_standard()
    {
        var result = BalanceFile.Parse("""{ "whip": { "angle": 200 } }""");

        Assert.Equal(Balance.Default.Whip, result.Balance.Whip);
        Assert.Contains(result.Warnings, w => w.Contains("whip.angle"));
    }

    [Fact]
    public void A_cooldown_of_zero_falls_back_to_the_standard()
    {
        var result = BalanceFile.Parse("""{ "whip": { "cooldown": 0 } }""");

        Assert.Equal(Balance.Default.Whip, result.Balance.Whip);
        Assert.Contains(result.Warnings, w => w.Contains("whip.cooldown"));
    }

    [Fact]
    public void A_negative_damage_falls_back_to_the_standard()
    {
        var result = BalanceFile.Parse("""{ "whip": { "damage": -5 } }""");

        Assert.Equal(Balance.Default.Whip, result.Balance.Whip);
        Assert.Contains(result.Warnings, w => w.Contains("whip.damage"));
    }

    [Fact]
    public void A_negative_pull_distance_falls_back_to_the_standard()
    {
        var result = BalanceFile.Parse("""{ "whip": { "pullTo": -1 } }""");

        Assert.Equal(Balance.Default.Whip, result.Balance.Whip);
        Assert.Contains(result.Warnings, w => w.Contains("whip.pullTo"));
    }

    [Fact]
    public void A_charged_knockback_shorter_than_the_plain_one_falls_back_to_both_standards()
    {
        var result = BalanceFile.Parse("""{ "whip": { "knockback": 8, "chargedKnockback": 2 } }""");

        Assert.Equal(Balance.Default.Whip, result.Balance.Whip);
        Assert.Contains(result.Warnings, w => w.Contains("whip.knockback") && w.Contains("whip.chargedKnockback"));
    }

    [Fact]
    public void Valid_whip_values_are_taken_over()
    {
        var result = BalanceFile.Parse("""{ "whip": { "range": 7, "pullTo": 0, "angle": 20 } }""");

        Assert.Empty(result.Warnings);
        Assert.Equal(7f, result.Balance.Whip.Range);
        Assert.Equal(0f, result.Balance.Whip.PullTo);
        Assert.Equal(20f, result.Balance.Whip.Angle);
    }

    [Fact]
    public void A_whip_without_a_pull_distance_pulls_nobody()
    {
        var noPull = StillGhouls with { Whip = Whip with { PullTo = 0f } };
        var world = With(WeaponKind.Whip, "P\n.\nG\n.", noPull);
        var before = world.Ghouls[0].Position;

        var events = Swing(world);

        Assert.True(world.Ghouls[0].Health < noPull.Ghoul.MaxHealth, "The whip missed the ghoul - the test would prove nothing.");
        Assert.False(world.Ghouls[0].Knockback.IsActive);
        Assert.DoesNotContain(events, e => e is MeleePulled);
        Assert.True(FlatDistance(world.Ghouls[0].Position, before) < 0.1f, "The ghoul was pulled.");
    }
}
