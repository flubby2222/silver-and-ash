using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// The throwing knives (Vampiric, issue #204): heavy damage at a low rate of fire, and the only ranged weapon whose shot
/// really flies - fast and straight, so a moving target must be led - and sticks in what it hits.
/// </summary>
public class ThrowingKnivesTests
{
    private static readonly RangedBalance Knives = Balance.Default.ThrowingKnives;
    private static readonly ThrownBalance Flight = Balance.Default.KnifeFlight;
    private static readonly int CooldownTicks = SimulationClock.SecondsToTicks(Knives.Cooldown);
    private static readonly int ReloadTicks = SimulationClock.SecondsToTicks(Knives.ReloadTime);

    /// <summary>Metres a knife flies in one tick.</summary>
    private static readonly float Step = Flight.Speed * SimulationClock.SecondsPerTick;

    /// <summary>Dummies that never fall over: so every hit can be counted in their health.</summary>
    private static readonly Balance Sturdy = Balance.Default with { Dummy = Balance.Default.Dummy with { MaxHealth = 100_000 } };

    /// <summary>A bat that survives a knife, so the knife can be seen sticking in it. It flies as every bat does.</summary>
    private static readonly Balance SturdyBat = Balance.Default with { Bat = Balance.Default.Bat with { MaxHealth = 100_000 } };

    /// <summary>A 14 x 14 m room: the player in the middle, the bat in a corner - it sees the player and comes to circle.</summary>
    private const string BatRoom = "B......\n.......\n.......\n...P...\n.......\n.......\n.......";

    /// <summary>A one-cell corridor along +Z from the given rows; the player looks along it, the knives in hand.</summary>
    private static GameWorld World(string map, Balance? balance = null) => World(LevelParser.Parse(map), balance);

    private static GameWorld World(Level level, Balance? balance = null)
    {
        var world = new GameWorld(level, 1, balance ?? Sturdy);
        world.Player.Equip(0, WeaponKind.ThrowingKnives);
        return world;
    }

    /// <summary>The corridor with <paramref name="free"/> empty cells between the player and <paramref name="end"/>.</summary>
    private static string Corridor(int free, string end) => "P\n" + string.Concat(Enumerable.Repeat(".\n", free)) + end;

    /// <summary>One press of the trigger: the events of that tick. The knife has flown its first stretch by then.</summary>
    private static List<GameEvent> Throw(GameWorld world)
    {
        world.Tick(PlayerInput.None with { Shoot = true });
        return world.Events.ToList();
    }

    private static List<GameEvent> Run(GameWorld world, PlayerInput input, int ticks = 1)
    {
        var events = new List<GameEvent>();
        for (var tick = 0; tick < ticks; tick++)
        {
            world.Tick(input);
            events.AddRange(world.Events);
        }

        return events;
    }

    /// <summary>
    /// Throws and lets the knife fly until it sticks (or <paramref name="maxTicks"/> are over). Returns every event and
    /// the tick the knife stuck in, the tick of the throw counted as 1 - or 0 if it never did.
    /// </summary>
    private static (List<GameEvent> Events, int StuckInTick) ThrowAndWatch(GameWorld world, int maxTicks = 240)
    {
        var events = Throw(world);
        var tick = 1;
        while (!events.Any(e => e is KnifeStuck) && tick < maxTicks)
        {
            world.Tick(PlayerInput.None);
            events.AddRange(world.Events);
            tick++;
        }

        return (events, events.Any(e => e is KnifeStuck) ? tick : 0);
    }

    /// <summary>In which tick a knife that flies <paramref name="distance"/> metres arrives, the tick of the throw counted as 1.</summary>
    private static int TicksFor(float distance) => (int)MathF.Ceiling(distance / Step);

    private static int Lost(TrainingDummy dummy) => Sturdy.Dummy.MaxHealth - dummy.Health;

    /// <summary>Turns the view (like the mouse would) so it looks at <paramref name="point"/>.</summary>
    private static void AimAt(GameWorld world, Vector3 point)
    {
        var player = world.Player;
        var direction = Vector3.Normalize(point - player.EyePosition);
        var pixels = new Vector2(
            -(MathF.Atan2(direction.X, direction.Z) - player.View.YawRadians) / ControlSettings.MouseSensitivity,
            -(MathF.Asin(direction.Y) - player.View.PitchRadians) / ControlSettings.MouseSensitivity);
        player.Look(PlayerInput.None with { Look = pixels });
    }

    // ---- The weapon ------------------------------------------------------------------------------------------------

    [Fact]
    public void The_throwing_knives_are_a_vampiric_ranged_weapon_with_their_own_values()
    {
        Assert.Equal(new WeaponInfo("Knives", WeaponHand.Ranged, ItemTier.Vampiric), Weapons.Info(WeaponKind.ThrowingKnives));
        Assert.Same(Balance.Default.ThrowingKnives, Balance.Default.Ranged(WeaponKind.ThrowingKnives));
        Assert.Equal(Weapon.ThrowingKnives, Weapons.ForDamage(WeaponKind.ThrowingKnives));
    }

    [Fact]
    public void Only_the_throwing_knives_fly_every_other_ranged_weapon_hits_at_once()
    {
        Assert.Same(Balance.Default.KnifeFlight, Balance.Default.Thrown(WeaponKind.ThrowingKnives));
        var others = Enum.GetValues<WeaponKind>()
            .Where(kind => kind != WeaponKind.ThrowingKnives && Weapons.Info(kind).Hand == WeaponHand.Ranged);
        Assert.All(others, kind => Assert.Null(Balance.Default.Thrown(kind)));
    }

    [Fact]
    public void The_throwing_knives_can_be_found_as_loot()
    {
        Assert.Contains(Equipment.Of(WeaponKind.ThrowingKnives), Equipment.All);
    }

    [Fact]
    public void Give_throwingknives_on_the_command_line_puts_the_knives_into_a_free_place()
    {
        var give = LaunchOptions.Parse(["--give", "throwingknives"]).Give!;
        var world = new GameWorld(LevelParser.Parse("P\n."));
        var free = world.Player.RangedSlots.ToList().FindIndex(weapon => weapon is null);

        GiveOptions.GiveTo(world, give);

        Assert.Equal([Equipment.Of(WeaponKind.ThrowingKnives)], give);
        var knives = world.Player.RangedSlots[free]!;
        Assert.Equal(WeaponKind.ThrowingKnives, knives.Kind);
        Assert.Equal(Knives.MagazineSize, knives.Magazine.Size);
    }

    [Fact]
    public void Knives_in_a_place_have_their_magazine_and_cooldown()
    {
        var player = new Player(LevelParser.Parse("P"));

        player.Equip(1, WeaponKind.ThrowingKnives);

        var knives = player.RangedSlots[1]!;
        Assert.Equal(Knives.MagazineSize, knives.Magazine.Size);
        Assert.Equal(CooldownTicks, knives.Cooldown.DurationTicks);
    }

    [Fact]
    public void A_throw_says_it_came_from_the_knives()
    {
        var world = World("P\n.");

        var events = Throw(world);

        Assert.Equal(WeaponKind.ThrowingKnives, Assert.Single(events.OfType<ShotFired>()).Weapon);
    }

    // ---- The flight ------------------------------------------------------------------------------------------------

    [Fact]
    public void A_knife_is_clearly_faster_than_a_blood_bolt()
    {
        Assert.True(Flight.Speed >= 2f * Balance.Default.BloodBolt.Speed, "A knife must be at least twice as fast as a blood bolt.");
    }

    [Fact]
    public void A_throw_hits_nothing_at_once_the_knife_is_in_the_air()
    {
        var world = World(Corridor(4, "D\n."));

        var events = Throw(world);

        Assert.DoesNotContain(events, e => e is DummyHit or TargetHitBy or KnifeStuck);
        Assert.Equal(0, Lost(world.Dummies[0]));
        var knife = Assert.Single(world.FlyingKnives);
        Assert.Equal(WeaponKind.ThrowingKnives, knife.Weapon);
        Assert.Equal(world.Player.Id, knife.ThrowerId);
        Assert.Equal(Flight.Speed, knife.Speed);

        // It has flown its first stretch in the tick of the throw, straight along the view.
        var eye = world.Player.EyePosition;
        Assert.Equal(Step, Vector3.Distance(eye, knife.Position), 3);
        Assert.Equal(eye, knife.PreviousPosition);
        Assert.Equal(world.Player.View.Forward, knife.Direction);
    }

    [Fact]
    public void The_pistol_in_the_same_corridor_hits_in_the_tick_of_the_shot()
    {
        // The counterpart: a bullet needs no flight. Only the weapon differs from the test above.
        var world = new GameWorld(LevelParser.Parse(Corridor(4, "D\n.")), 1, Sturdy);

        world.Tick(PlayerInput.None with { Shoot = true });

        Assert.Contains(world.Events, e => e is DummyHit);
        Assert.Empty(world.FlyingKnives);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(9)]
    public void The_knife_needs_the_time_its_speed_gives_for_the_way_to_the_target(int freeCells)
    {
        var world = World(Corridor(freeCells, "D\n."));
        var dummy = world.Dummies[0];
        var distance = Raycast.AgainstCylinder(world.Player.EyePosition, world.Player.View.Forward, dummy.Body)!.Value;
        var expected = TicksFor(distance);
        Assert.True(expected > 1, "The target is so near that the knife arrives in the tick of the throw - the test would prove nothing.");

        var (events, stuckInTick) = ThrowAndWatch(world);

        Assert.Equal(expected, stuckInTick);
        Assert.Single(events.OfType<DummyHit>());
        Assert.True(Lost(dummy) > 0);
        Assert.Empty(world.FlyingKnives);
    }

    [Fact]
    public void A_knife_that_flies_straight_keeps_its_height_and_its_line()
    {
        // No gravity, no drift: tick by tick the knife is one stretch further along the view.
        var world = World(Corridor(9, "."));
        var eye = world.Player.EyePosition;
        var forward = world.Player.View.Forward;
        Throw(world);

        for (var tick = 2; tick <= 20; tick++)
        {
            world.Tick(PlayerInput.None);
            var knife = Assert.Single(world.FlyingKnives);
            var expected = eye + forward * (Step * tick);
            Assert.True(Vector3.Distance(expected, knife.Position) < 0.001f, $"Tick {tick}: the knife is at {knife.Position}, not at {expected}.");
        }
    }

    [Fact]
    public void A_knife_that_has_flown_its_range_is_gone_without_a_trace()
    {
        var range = 3f;
        var world = World(Corridor(9, "."), Sturdy with { ThrowingKnives = Knives with { Range = range } });
        var eye = world.Player.EyePosition;
        var events = Throw(world);
        var farthest = 0f;

        for (var tick = 1; tick < 60 && world.FlyingKnives.Count > 0; tick++)
        {
            farthest = MathF.Max(farthest, Vector3.Distance(eye, world.FlyingKnives[0].Position));
            events.AddRange(Run(world, PlayerInput.None));
        }

        Assert.Empty(world.FlyingKnives);
        Assert.Empty(world.StuckKnives);
        Assert.DoesNotContain(events, e => e is KnifeStuck or BulletImpact);
        Assert.True(farthest <= range + 0.001f, $"The knife flew {farthest} m, further than its range of {range} m.");
        // Seen last one tick before the end of its range: in the tick it gets there it is gone.
        Assert.True(farthest > range - Step - 0.001f, $"The knife flew only {farthest} m of its range of {range} m.");
    }

    // ---- A moving target -------------------------------------------------------------------------------------------

    /// <summary>
    /// Where and in which tick (the throw counted as 1) a knife thrown from <paramref name="eye"/> along
    /// <paramref name="direction"/> meets a body that is at <paramref name="bodies"/>[tick] in each tick - computed
    /// from the knife's speed alone, not by the game. Null if it never does.
    /// </summary>
    private static (int Tick, Vector3 Point)? Meeting(Vector3 eye, Vector3 direction, IReadOnlyList<CylinderBody> bodies)
    {
        for (var tick = 1; tick < bodies.Count; tick++)
        {
            var from = eye + direction * (Step * (tick - 1));
            if (Raycast.AgainstCylinder(from, direction, bodies[tick]) is { } distance && distance <= Step)
            {
                return (tick, from + direction * distance);
            }
        }

        return null;
    }

    /// <summary>
    /// Two worlds with the same seed in which a bat circles a player who stands still: the first to throw in, the second
    /// its twin. Returns the first, and where the twin's bat really is after each of the next ticks ([0]: now).
    /// </summary>
    private static (GameWorld World, List<CylinderBody> Bodies) CirclingBat(int ticksAhead = 60)
    {
        var world = World(BatRoom, SturdyBat);
        var twin = World(BatRoom, SturdyBat);
        foreach (var each in new[] { world, twin })
        {
            Run(each, PlayerInput.None, 3 * SimulationClock.TicksPerSecond);
            Assert.Equal(BatState.Circling, each.Bats[0].State);
        }

        Assert.Equal(twin.Bats[0].Position, world.Bats[0].Position);
        var bodies = new List<CylinderBody> { twin.Bats[0].Body };
        for (var tick = 1; tick <= ticksAhead; tick++)
        {
            twin.Tick(PlayerInput.None);
            bodies.Add(twin.Bats[0].Body);
        }

        return (world, bodies);
    }

    private static Vector3 Centre(CylinderBody body) => body.Position + new Vector3(0f, body.Height / 2f, 0f);

    [Fact]
    public void A_knife_thrown_ahead_of_a_flying_bat_hits_it_where_and_when_the_flight_says()
    {
        var (world, bodies) = CirclingBat();
        var bat = world.Bats[0];
        var eye = world.Player.EyePosition;

        // Lead: aim at where the bat will be in the tick the knife gets that far.
        var lead = Enumerable.Range(1, bodies.Count - 1)
            .First(tick => Meeting(eye, Vector3.Normalize(Centre(bodies[tick]) - eye), bodies)?.Tick == tick);
        AimAt(world, Centre(bodies[lead]));
        var expected = Meeting(eye, world.Player.View.Forward, bodies)!.Value;
        Assert.True(expected.Tick > 3, "The bat is so near that there is nothing to lead.");
        Assert.True(Vector3.Distance(Centre(bodies[0]), Centre(bodies[expected.Tick])) > bodies[0].Radius,
            "The bat hardly moves during the flight - the test would not need a lead.");

        var (events, stuckInTick) = ThrowAndWatch(world);

        Assert.Equal(expected.Tick, stuckInTick);
        var stuck = Assert.Single(events.OfType<KnifeStuck>());
        Assert.Equal(bat.Id, stuck.TargetId);
        Assert.True(Vector3.Distance(expected.Point, stuck.Point) < 0.001f, $"The knife stuck at {stuck.Point}, computed was {expected.Point}.");
        Assert.Equal(new BatHit(bat.Id, Knives.Damage), Assert.Single(events.OfType<BatHit>()));
        Assert.Equal(SturdyBat.Bat.MaxHealth - Knives.Damage, bat.Health);
    }

    [Fact]
    public void A_knife_thrown_at_where_the_bat_is_now_misses_it()
    {
        // The counterpart to the test above, same bat, same moment: without a lead the bat has flown on.
        var (world, bodies) = CirclingBat();
        var bat = world.Bats[0];
        AimAt(world, Centre(bodies[0]));
        Assert.Null(Meeting(world.Player.EyePosition, world.Player.View.Forward, bodies));

        var events = Throw(world);
        events.AddRange(Run(world, PlayerInput.None, bodies.Count - 2));

        Assert.DoesNotContain(events, e => e is BatHit);
        Assert.Equal(SturdyBat.Bat.MaxHealth, bat.Health);
    }

    [Fact]
    public void A_knife_in_a_bat_flies_along_with_it()
    {
        var (world, bodies) = CirclingBat();
        var bat = world.Bats[0];
        var eye = world.Player.EyePosition;
        var lead = Enumerable.Range(1, bodies.Count - 1)
            .First(tick => Meeting(eye, Vector3.Normalize(Centre(bodies[tick]) - eye), bodies)?.Tick == tick);
        AimAt(world, Centre(bodies[lead]));
        ThrowAndWatch(world);
        var knife = Assert.Single(world.StuckKnives);
        Assert.Equal(bat.Id, knife.TargetId);
        var offset = knife.Point - bat.Position;
        var hitAt = bat.Position;

        Run(world, PlayerInput.None, SimulationClock.TicksPerSecond);

        Assert.True(Vector3.Distance(hitAt, bat.Position) > 0.5f, "The bat did not fly on - the test would prove nothing.");
        Assert.Same(knife, Assert.Single(world.StuckKnives));
        Assert.True(Vector3.Distance(offset, knife.Point - bat.Position) < 0.0001f, "The knife did not stay in its place in the bat.");
    }

    // ---- What it sticks in -----------------------------------------------------------------------------------------

    [Fact]
    public void A_wall_stops_the_knife_and_it_sticks_there()
    {
        var world = World("P\n.\n#\nD");
        var wall = Raycast.AgainstLevel(world.Level, world.Player.EyePosition, world.Player.View.Forward, Knives.Range)!.Value;

        var (events, stuckInTick) = ThrowAndWatch(world);

        Assert.Equal(TicksFor(wall.Distance), stuckInTick);
        var stuck = Assert.Single(events.OfType<KnifeStuck>());
        Assert.Null(stuck.TargetId);
        Assert.Equal(WeaponKind.ThrowingKnives, stuck.Weapon);
        Assert.Equal(world.Player.Id, stuck.PlayerId);
        Assert.True(Vector3.Distance(wall.Point, stuck.Point) < 0.001f, $"The knife stuck at {stuck.Point}, the wall is at {wall.Point}.");
        Assert.Equal(world.Player.View.Forward, stuck.Direction);
        var impact = Assert.Single(events.OfType<BulletImpact>());
        Assert.Equal(WeaponKind.ThrowingKnives, impact.Weapon);
        Assert.Equal(wall.Normal, impact.Normal);
        Assert.Empty(world.FlyingKnives);

        // It stays: a second later it is still there, and the dummy behind the wall was never touched.
        events.AddRange(Run(world, PlayerInput.None, SimulationClock.TicksPerSecond));
        var knife = Assert.Single(world.StuckKnives);
        Assert.Equal(stuck.KnifeId, knife.Id);
        Assert.Null(knife.TargetId);
        Assert.Equal(stuck.Point, knife.Point);
        Assert.Equal(0, Lost(world.Dummies[0]));
        Assert.DoesNotContain(events, e => e is DummyHit or TargetHitBy);
    }

    [Fact]
    public void A_column_stops_the_knife()
    {
        var level = LevelParser.Parse("P\n.\n.\nD\n.").WithColumns([Column.AtCell(0, 2)]);
        var world = World(level);

        var (events, _) = ThrowAndWatch(world);
        events.AddRange(Run(world, PlayerInput.None, SimulationClock.TicksPerSecond));

        var stuck = Assert.Single(events.OfType<KnifeStuck>());
        Assert.Null(stuck.TargetId);
        Assert.True(stuck.Point.Z < world.Dummies[0].Position.Z - BodySizes.Dummy.Radius, "The knife did not end in the column.");
        Assert.Equal(WeaponKind.ThrowingKnives, Assert.Single(events.OfType<BulletImpact>()).Weapon);
        Assert.Equal(0, Lost(world.Dummies[0]));
    }

    [Fact]
    public void A_knife_sticks_in_the_first_target_and_does_not_reach_the_one_behind()
    {
        var world = World("P\n.\nD\nD\n.");

        var (events, _) = ThrowAndWatch(world);
        events.AddRange(Run(world, PlayerInput.None, SimulationClock.TicksPerSecond));

        var stuck = Assert.Single(events.OfType<KnifeStuck>());
        Assert.Equal(world.Dummies[0].Id, stuck.TargetId);
        Assert.DoesNotContain(events, e => e is BulletImpact);
        var hit = Assert.Single(events.OfType<TargetHitBy>());
        Assert.Equal(new TargetHitBy(world.Dummies[0].Id, world.Player.Id, Weapon.ThrowingKnives) { Hard = hit.Hard, Place = hit.Place }, hit);
        Assert.True(Lost(world.Dummies[0]) > 0);
        Assert.Equal(0, Lost(world.Dummies[1]));
        var knife = Assert.Single(world.StuckKnives);
        Assert.Equal(world.Dummies[0].Id, knife.TargetId);
        Assert.Equal(stuck.Point, knife.Point);
    }

    [Fact]
    public void A_knife_in_a_target_that_can_no_longer_be_hit_is_gone()
    {
        // A dummy with little health falls over from the knife: the hit is reported, but no knife stays in the air above it.
        var world = World("P\n.\nD\n.", Balance.Default with { Dummy = Balance.Default.Dummy with { MaxHealth = 1 } });

        var (events, _) = ThrowAndWatch(world);

        Assert.True(world.Dummies[0].IsDown, "The dummy did not fall - the test would prove nothing.");
        Assert.Equal(world.Dummies[0].Id, Assert.Single(events.OfType<KnifeStuck>()).TargetId);
        Assert.Empty(world.StuckKnives);
        Assert.Empty(world.FlyingKnives);
    }

    [Fact]
    public void A_knife_in_a_wall_stays_when_a_target_falls()
    {
        var balance = Balance.Default with { Dummy = Balance.Default.Dummy with { MaxHealth = 1 } };
        var world = World("P\n.\nD\n.\n#", balance);
        AimAt(world, world.Player.EyePosition + new Vector3(0f, 1.5f, 3f));   // over the dummy's head, into stone
        var (first, _) = ThrowAndWatch(world);
        Assert.Null(Assert.Single(first.OfType<KnifeStuck>()).TargetId);
        Run(world, PlayerInput.None, CooldownTicks);

        AimAt(world, world.Dummies[0].Body.Position + new Vector3(0f, 0.5f, 0f));
        ThrowAndWatch(world);

        Assert.True(world.Dummies[0].IsDown);
        Assert.Null(Assert.Single(world.StuckKnives).TargetId);
    }

    [Fact]
    public void Only_so_many_knives_stay_stuck_the_oldest_goes_first()
    {
        var balance = Sturdy with
        {
            ThrowingKnives = Knives with { MagazineSize = 4 },
            KnifeFlight = Flight with { StuckMax = 2 },
        };
        var world = World("P\n.\n#", balance);
        var ids = new List<int>();

        for (var knife = 0; knife < 4; knife++)
        {
            var (events, _) = ThrowAndWatch(world);
            ids.Add(Assert.Single(events.OfType<KnifeStuck>()).KnifeId);
            Run(world, PlayerInput.None, CooldownTicks);
        }

        Assert.Equal(4, ids.Distinct().Count());
        Assert.Equal(ids.Skip(2), world.StuckKnives.Select(knife => knife.Id));
    }

    [Fact]
    public void A_target_that_ignores_the_hit_does_not_keep_the_knife()
    {
        // As for the stake (issue #793): the showroom's exhibits take no damage, so the knife flies on into the wall.
        var world = new GameWorld(LevelParser.Parse("P\n.\nD\n.\n#"), 1, Sturdy) { Showroom = new ShowroomExhibits(null, [], [], [], []) };
        world.Player.Equip(0, WeaponKind.ThrowingKnives);

        var (events, _) = ThrowAndWatch(world);

        Assert.Empty(events.OfType<TargetHitBy>());
        Assert.Null(Assert.Single(events.OfType<KnifeStuck>()).TargetId);
        Assert.Equal(WeaponKind.ThrowingKnives, Assert.Single(events.OfType<BulletImpact>()).Weapon);
        Assert.Equal(0, Lost(world.Dummies[0]));
    }

    [Fact]
    public void A_knife_that_starts_inside_a_body_hits_it_at_once()
    {
        // A target can move onto a knife between two ticks; then the knife's stretch starts inside it.
        var dummy = new TrainingDummy(0, new Vector3(1f, 0f, 3f));
        var inside = dummy.Body.Position + new Vector3(0f, dummy.Body.Height / 2f, 0f);
        var above = dummy.Body.Position + new Vector3(0f, dummy.Body.Height + 0.5f, 0f);
        var beside = dummy.Body.Position + new Vector3(dummy.Body.Radius + 0.5f, dummy.Body.Height / 2f, 0f);

        Assert.Equal(0f, ThrownKnives.DistanceTo(dummy, inside, Vector3.UnitZ));
        Assert.Null(ThrownKnives.DistanceTo(dummy, above, Vector3.UnitZ));
        Assert.Null(ThrownKnives.DistanceTo(dummy, beside, Vector3.UnitZ));
        Assert.Equal(0.5f, ThrownKnives.DistanceTo(dummy, beside, -Vector3.UnitX)!.Value, 4);
    }

    // ---- Damage ----------------------------------------------------------------------------------------------------

    [Fact]
    public void Headshots_and_Gorm_s_head_count_for_the_knives_too()
    {
        var rules = DamageRules.Standard();

        Assert.Equal(new DamageResult((int)MathF.Round(Knives.Damage * Knives.HeadshotMultiplier), true), rules.Calculate(new Hit(Weapon.ThrowingKnives, HitZone.Head, Knives.Damage)));
        Assert.Equal(Knives.Damage, rules.Calculate(new Hit(Weapon.ThrowingKnives, HitZone.Body, Knives.Damage)).Amount);
        Assert.True(rules.Calculate(new Hit(Weapon.ThrowingKnives, HitZone.BossHead, Knives.Damage)).Amount > Knives.Damage);
    }

    [Fact]
    public void A_knife_in_the_head_uses_the_knives_multiplier_one_in_the_body_does_not()
    {
        // The pistol gets no head bonus here, so a knife counted as a pistol shot would show.
        var balance = Sturdy with { Pistol = Sturdy.Pistol with { HeadshotMultiplier = 1f } };
        var world = World("P\n.\n.\nD\n.", balance);
        var dummy = world.Dummies[0];
        AimAt(world, dummy.Position with { Y = dummy.Position.Y + BodySizes.Dummy.Height - BodySizes.Dummy.HeadHeight / 2f });

        var (head, _) = ThrowAndWatch(world);

        var damage = (int)MathF.Round(Knives.Damage * Knives.HeadshotMultiplier, MidpointRounding.AwayFromZero);
        Assert.Contains(new DummyHit(0, damage, Critical: true), head);

        Run(world, PlayerInput.None, CooldownTicks);
        AimAt(world, dummy.Position with { Y = dummy.Position.Y + (BodySizes.Dummy.Height - BodySizes.Dummy.HeadHeight) / 2f });

        var (body, _) = ThrowAndWatch(world);

        Assert.Contains(new DummyHit(0, Knives.Damage), body);
        Assert.Equal(damage + Knives.Damage, Lost(dummy));
    }

    // ---- Rate of fire and reloading --------------------------------------------------------------------------------

    [Fact]
    public void Between_two_throws_the_knives_need_their_cooldown()
    {
        var world = World(Corridor(9, "."));
        var events = Throw(world);
        world.Tick(PlayerInput.None);

        // Held down for the rest of the cooldown: nothing. The next tick throws.
        events.AddRange(Run(world, PlayerInput.None with { Shoot = true }, CooldownTicks - 2));
        Assert.Single(events.OfType<ShotFired>());

        events.AddRange(Run(world, PlayerInput.None, 1));
        events.AddRange(Run(world, PlayerInput.None with { Shoot = true }, 1));

        Assert.Equal(2, events.OfType<ShotFired>().Count());
    }

    [Fact]
    public void After_a_hand_of_knives_the_next_press_reloads()
    {
        var world = World(Corridor(9, "."));
        var events = new List<GameEvent>();
        for (var knife = 0; knife < Knives.MagazineSize; knife++)
        {
            events.AddRange(Throw(world));
            events.AddRange(Run(world, PlayerInput.None, CooldownTicks));
        }

        Assert.Equal(Knives.MagazineSize, events.OfType<ShotFired>().Count());

        var press = Run(world, PlayerInput.None with { Shoot = true });

        Assert.DoesNotContain(press, e => e is ShotFired);
        Assert.Equal(WeaponKind.ThrowingKnives, Assert.Single(press.OfType<ReloadStarted>()).Weapon);
        Assert.True(world.Player.Ranged.Magazine.IsReloading);
    }

    [Fact]
    public void Reloading_takes_the_knives_time_and_says_it_was_the_knives()
    {
        var world = World(Corridor(9, "."));
        Throw(world);
        world.Tick(PlayerInput.None);
        var started = Run(world, PlayerInput.None with { Reload = true });
        Assert.Equal(WeaponKind.ThrowingKnives, Assert.Single(started.OfType<ReloadStarted>()).Weapon);

        var waiting = Run(world, PlayerInput.None, ReloadTicks - 1);
        Assert.DoesNotContain(waiting, e => e is ReloadFinished);
        Assert.True(world.Player.Ranged.Magazine.IsReloading);

        var done = Run(world, PlayerInput.None);
        Assert.Equal(WeaponKind.ThrowingKnives, Assert.Single(done.OfType<ReloadFinished>()).Weapon);
        Assert.Equal(Knives.MagazineSize, world.Player.Ranged.Magazine.Rounds);
    }

    // ---- Invalid values in balance.json ----------------------------------------------------------------------------

    [Theory]
    [InlineData("damage", "-5")]
    [InlineData("pierce", "1")]
    [InlineData("pierce", "-1")]
    [InlineData("spread", "100")]
    [InlineData("range", "0")]
    [InlineData("cooldown", "0")]
    [InlineData("magazineSize", "0")]
    [InlineData("reloadTime", "0")]
    [InlineData("pellets", "0")]
    public void An_invalid_value_of_the_knives_falls_back_to_the_standard(string name, string value)
    {
        var result = BalanceFile.Parse($$"""{ "throwingKnives": { "{{name}}": {{value}} } }""");

        Assert.Equal(Knives, result.Balance.ThrowingKnives);
        Assert.Contains(result.Warnings, w => w.Contains($"throwingKnives.{name}"));
    }

    [Theory]
    [InlineData("speed", "0")]
    [InlineData("speed", "-30")]
    [InlineData("stuckMax", "0")]
    public void An_invalid_value_of_the_flight_falls_back_to_the_standard(string name, string value)
    {
        var result = BalanceFile.Parse($$"""{ "knifeFlight": { "{{name}}": {{value}} } }""");

        Assert.Equal(Flight, result.Balance.KnifeFlight);
        Assert.Contains(result.Warnings, w => w.Contains($"knifeFlight.{name}"));
    }

    [Fact]
    public void Valid_knife_values_are_taken_over_zero_damage_and_spread_among_them()
    {
        var result = BalanceFile.Parse("""{ "throwingKnives": { "damage": 0, "pierce": 0, "spread": 0, "reloadTime": 4 }, "knifeFlight": { "speed": 45, "stuckMax": 3 } }""");

        Assert.Empty(result.Warnings);
        Assert.Equal(0, result.Balance.ThrowingKnives.Damage);
        Assert.Equal(0f, result.Balance.ThrowingKnives.Spread);
        Assert.Equal(4f, result.Balance.ThrowingKnives.ReloadTime);
        Assert.Equal(45f, result.Balance.KnifeFlight.Speed);
        Assert.Equal(3, result.Balance.KnifeFlight.StuckMax);
    }

    [Fact]
    public void The_file_in_the_repository_has_the_standard_values_of_the_knives()
    {
        var result = BalanceFile.Load(RepositoryFiles.Find("assets/balance.json"));

        Assert.Empty(result.Warnings);
        Assert.Equal(Knives, result.Balance.ThrowingKnives);
        Assert.Equal(Flight, result.Balance.KnifeFlight);
    }
}
