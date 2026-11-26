using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Issue #785: the rime flecks of Grave Chill only sit where the eye sees body - on the rods of <see cref="FrostCore"/>,
/// on the side facing the eye and moved towards it along the line of sight. The enemies are chilled through the world
/// and run tick by tick; the eyes stand where a player can stand (level, above, below, from the side).
/// </summary>
public class FrostFleckTests
{
    [Fact]
    public void Seen_from_anywhere_every_fleck_of_a_crawling_ghoul_lies_over_its_body()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\n.\nG"));
        var ghoul = world.Ghouls[0];
        Chill(world, ghoul);
        RunTicks(world, PlayerInput.None, 20);
        Assert.Equal(0f, ghoul.WindUpProgress);

        AssertEveryFleckOverABody(world);
    }

    [Fact]
    public void Seen_from_anywhere_every_fleck_of_a_ghoul_rearing_up_for_a_blow_lies_over_its_body()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nG"));
        var ghoul = world.Ghouls[0];
        Assert.True(RunUntil(world, PlayerInput.None, 600, _ => ghoul is { State: GhoulState.WindingUp, WindUpProgress: >= 0.9f }));
        Chill(world, ghoul);

        var readout = AssertEveryFleckOverABody(world);

        // Reared up, its torso stands higher than when it crawls: the rods turn with it.
        var crawling = FrostCore.Of(new GameWorld(LevelParser.Parse("P\n.\nG")).Ghouls[0]);
        Assert.True(readout.Cores.Max(core => core.To.Y) - readout.Feet.Y > crawling.Max(core => core.To.Y) + 0.1f);
    }

    [Fact]
    public void Seen_from_anywhere_every_fleck_of_a_bat_lies_over_its_body()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nB"));
        Chill(world, world.Bats[0]);
        RunTicks(world, PlayerInput.None, 20);

        AssertEveryFleckOverABody(world);
    }

    [Fact]
    public void Seen_from_anywhere_every_fleck_of_gorm_lies_over_his_body()
    {
        var world = GormHallWorld();
        Chill(world, world.Gorm!);
        RunTicks(world, PlayerInput.None, 20);

        AssertEveryFleckOverABody(world);
    }

    [Fact]
    public void The_rods_of_a_crawling_ghoul_a_bat_and_gorm_lie_inside_what_a_shot_hits()
    {
        var ghoulWorld = new GameWorld(LevelParser.Parse("P\n.\n.\n.\nG"));
        var batWorld = new GameWorld(LevelParser.Parse("P\n.\n.\nB"));
        var gormWorld = GormHallWorld();
        RunTicks(ghoulWorld, PlayerInput.None, 20);
        RunTicks(batWorld, PlayerInput.None, 20);
        RunTicks(gormWorld, PlayerInput.None, 20);

        foreach (IEnemy enemy in new IEnemy[] { ghoulWorld.Ghouls[0], batWorld.Bats[0], gormWorld.Gorm! })
        {
            // The bat is drawn bobbing above its body; its shot body is the body (one per figure, a little wider).
            var shot = enemy.ShotBodies.Select(body => body with { Position = body.Position + (enemy.ShownPosition - enemy.Body.Position) });
            foreach (var core in FrostCore.Of(enemy))
            {
                foreach (var end in new[] { core.From, core.To })
                {
                    Assert.Contains(shot, body => Holds(body, end, core.Radius));
                }
            }
        }
    }

    [Fact]
    public void A_readout_without_rods_puts_the_flecks_on_the_middle_of_its_body()
    {
        var readout = new EnemyStatusReadout(1, new Vector3(3f, 0f, 5f), 1.2f, 0.4f, []);
        var middle = FrostCore.Middle(readout.Feet, readout.Height, readout.Radius);

        foreach (var eye in Eyes(readout))
        {
            foreach (var fleck in FrostLook.Flecks)
            {
                AssertOverARod([middle], eye, FrostLook.FleckAt(fleck, readout, eye));
            }
        }
    }

    [Fact]
    public void An_eye_straight_above_a_rod_or_on_it_gives_a_fleck_and_never_nan()
    {
        var core = new FrostCore(new Vector3(1f, 0.4f, 1f), new Vector3(1f, 0.8f, 1f), 0.1f);
        var readout = new EnemyStatusReadout(1, new Vector3(1f, 0f, 1f), 1f, 0.3f, []) { Cores = [core] };

        foreach (var eye in new[] { new Vector3(1f, 3f, 1f), new Vector3(1f, 0.6f, 1f), core.From, core.To })
        {
            foreach (var fleck in FrostLook.Flecks)
            {
                var at = FrostLook.FleckAt(fleck, readout, eye);
                Assert.False(float.IsNaN(at.X) || float.IsNaN(at.Y) || float.IsNaN(at.Z), $"NaN seen from {eye}");
                Assert.True(DistanceToRod(core, at) <= core.Radius + readout.Radius + 1e-3f, $"{at} far from the rod, seen from {eye}");
            }
        }
    }

    [Fact]
    public void An_enemy_without_a_table_of_its_own_gets_the_rod_in_the_middle_of_its_body()
    {
        var enemy = new Unknown(new CylinderBody(new Vector3(2f, 0.5f, 4f), 0.4f, 1.5f));

        var core = Assert.Single(FrostCore.Of(enemy));

        Assert.Equal(FrostCore.Middle(enemy.ShownPosition, 1.5f, 0.4f), core);
        Assert.Equal(1f, core.From.Y, 4);
        Assert.Equal(1.5f, core.To.Y, 4);
        Assert.Equal(0.1f, core.Radius, 4);
    }

    [Fact]
    public void An_enemy_is_needed_for_its_rods()
    {
        Assert.Throws<ArgumentNullException>(() => FrostCore.Of(null!));
        Assert.Throws<ArgumentNullException>(() => FrostLook.FleckAt(FrostLook.Flecks[0], null!, Vector3.Zero));
    }

    /// <summary>
    /// Seen from <paramref name="eye"/>, <paramref name="at"/> lies over one of the rods of <paramref name="readout"/>
    /// and in front of it: the line from the eye through the fleck meets a rod behind the fleck. (Where the line grazes
    /// along a slanted rod, it may also touch it before the fleck - the fleck still lies over the body.)
    /// </summary>
    internal static void AssertOverARod(EnemyStatusReadout readout, Vector3 eye, Vector3 at) =>
        AssertOverARod(readout.Cores.Count > 0 ? readout.Cores : [FrostCore.Middle(readout.Feet, readout.Height, readout.Radius)], eye, at);

    private static void AssertOverARod(IReadOnlyList<FrostCore> cores, Vector3 eye, Vector3 at)
    {
        const float Step = 0.002f;
        var toFleck = at - eye;
        var direction = Vector3.Normalize(toFleck);
        for (var along = toFleck.Length(); along < toFleck.Length() + 3f; along += Step)
        {
            if (cores.Any(core => DistanceToRod(core, eye + (direction * along)) <= core.Radius + Step))
            {
                return;
            }
        }

        Assert.Fail($"The fleck at {at} lies beside the body, seen from {eye}.");
    }

    private static EnemyStatusReadout AssertEveryFleckOverABody(GameWorld world)
    {
        var readout = Assert.Single(EnemyStatusReadout.Of(world));
        Assert.NotEmpty(readout.Cores);
        foreach (var eye in Eyes(readout).Append(world.Player.EyePosition))
        {
            foreach (var fleck in FrostLook.Flecks)
            {
                AssertOverARod(readout, eye, FrostLook.FleckAt(fleck, readout, eye));
            }
        }

        return readout;
    }

    // Eyes round the enemy: level with it from four sides, from high above at 4 m and from below (a player on lower ground).
    private static IEnumerable<Vector3> Eyes(EnemyStatusReadout readout)
    {
        foreach (var angle in new[] { 0f, 1.3f, 2.6f, 4.4f })
        {
            var flat = new Vector3(MathF.Sin(angle), 0f, MathF.Cos(angle));
            yield return readout.Feet + (flat * 4f) + new Vector3(0f, 1.6f, 0f);
            yield return readout.Feet + (flat * 4f) + new Vector3(0f, 4f, 0f);
            yield return readout.Feet + (flat * 3f) - new Vector3(0f, 0.8f, 0f);
            yield return readout.Feet + (flat * 10f) + new Vector3(0f, 1.6f, 0f);
        }
    }

    private static void Chill(GameWorld world, IEnemy enemy) =>
        Assert.True(world.ApplyStatus(enemy, StatusKind.GraveChill, world.Player));

    private static float DistanceToRod(FrostCore core, Vector3 point)
    {
        var axis = core.To - core.From;
        var share = axis.LengthSquared() < 1e-12f ? 0f : Math.Clamp(Vector3.Dot(point - core.From, axis) / axis.LengthSquared(), 0f, 1f);
        return Vector3.Distance(point, core.From + (axis * share));
    }

    // True if a ball of radius around the point lies inside the body across, and the point between its bottom and top.
    private static bool Holds(CylinderBody body, Vector3 point, float radius)
    {
        var across = Vector2.Distance(new Vector2(point.X, point.Z), new Vector2(body.Position.X, body.Position.Z));
        return across + radius <= body.Radius + 1e-4f
            && point.Y >= body.Position.Y - 1e-4f && point.Y <= body.Position.Y + body.Height + 1e-4f;
    }

    // An enemy the frost has no table for (issue #785: the fallback of FrostCore.Of).
    private sealed class Unknown(CylinderBody body) : IEnemy
    {
        public int Id => 99;

        public bool IsHittable => true;

        public CylinderBody Body => body;

        public bool IsDead => false;

        public bool IsHidden => false;

        public bool IsOut => true;

        public StatusEffects Status { get; } = new();

        public Vector3 ShownPosition => body.Position;

        public HitZone ZoneAt(Vector3 point) => default;

        public int TakeDamage(int damage, ICollection<GameEvent> events, bool critical = false, DamageKind kind = DamageKind.Strike) => 0;

        public void PlaceAt(Vector3 position)
        {
        }
    }
}
