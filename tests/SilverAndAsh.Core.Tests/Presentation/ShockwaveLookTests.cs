using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

// Issue #299: the shock wave drawn as breaking stone must run exactly where the danger runs.
public class ShockwaveLookTests
{
    private const float Tolerance = 0.0001f;

    // The old ring of boxes stood 0.06 m beyond the radius (boxes 0.12 m wide, centred on it); the stones may not more.
    private const float Overhang = 0.06f;

    private static readonly IReadOnlyList<RoomTemplate> Templates = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    private static readonly Lazy<IReadOnlyList<GlbPart>> File = new(
        () => GlbScene.Read(System.IO.File.ReadAllBytes(RepositoryFiles.Find($"assets/models/{ShockwaveLook.FileName}"))));

    private static PhotoStudio Studio() => new(Templates, Balance.Default);

    private static GlbPart Part(string name) => File.Value.Single(part => part.Name == name);

    private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));

    private static readonly float[] SomeRadii = [0f, 0.05f, 0.3f, 0.31f, 0.9f, 1.6f, 3.3f, 6f, 8.03f, 11.99f, 12f];

    public static TheoryData<float> Radii() => new(SomeRadii);

    [Fact]
    public void The_file_has_the_shard_and_the_dust_with_their_pivots_at_the_origin()
    {
        Assert.Equal([new RigPart(ShockwaveLook.Shard, null), new RigPart(ShockwaveLook.Puff, null)], ShockwaveLook.Parts);
        Assert.Equal(ShockwaveLook.Parts.Select(part => part.Name).Order(), File.Value.Select(part => part.Name).Order());
        Assert.All(File.Value, part => Assert.Equal(Vector3.Zero, part.Pivot));
    }

    [Fact]
    public void The_shard_reaches_outwards_exactly_as_far_as_the_look_says_and_the_dust_less()
    {
        var shard = Part(ShockwaveLook.Shard);

        Assert.Equal(ShockwaveLook.ShardReach, shard.PositionMax.X, 0.002f);
        Assert.InRange(shard.PositionMax.Z - shard.PositionMin.Z, ShockwaveLook.ShardLength, ShockwaveLook.ShardLength + 0.1f);

        // The first row of dust lies 0.55 m behind the shard: it must not stick out in front of it.
        Assert.True(Part(ShockwaveLook.Puff).PositionMax.X < 0.55f + ShockwaveLook.ShardReach);
    }

    // Issue #314: no red crack any more - the front of the shards (their broken lip, the outermost point of the file) is
    // what marks the danger now, so it is the front that must lie exactly on the edge of the wave.
    [Theory]
    [MemberData(nameof(Radii))]
    public void The_front_of_every_shard_lies_exactly_on_the_edge_of_the_wave(float radius)
    {
        var origin = new Vector3(13f, 0f, 21f);
        var pieces = new List<ShockwavePiece>();

        ShockwaveLook.Place(origin, radius, pieces);

        var shards = pieces.Where(piece => piece.Part == 0).ToList();
        Assert.Equal(ShockwaveLook.PieceCount(radius), shards.Count);
        Assert.All(shards, shard =>
            Assert.Equal(ShockwaveLook.Edge(radius), Flat(origin, Vector3.Transform(new Vector3(ShockwaveLook.ShardReach, 0f, 0f), shard.Frame)), Tolerance));
    }

    [Theory]
    [MemberData(nameof(Radii))]
    public void No_stone_lies_beyond_the_edge_and_the_edge_is_the_radius(float radius)
    {
        var origin = new Vector3(-4f, 1.5f, 7f);
        var pieces = new List<ShockwavePiece>();

        ShockwaveLook.Place(origin, radius, pieces);

        var farthest = pieces.SelectMany(piece => Corners(File.Value[piece.Part]).Select(corner => Flat(origin, Vector3.Transform(corner, piece.Frame)))).Max();
        Assert.InRange(farthest, ShockwaveLook.Edge(radius), ShockwaveLook.Edge(radius) + Overhang);
        Assert.Equal(radius >= ShockwaveLook.MinRadius ? radius : ShockwaveLook.MinRadius, ShockwaveLook.Edge(radius));

        // Nothing sinks into the floor or flies off: the stones stand on the floor the wave started on.
        var heights = pieces.SelectMany(piece => Corners(File.Value[piece.Part]).Select(corner => Vector3.Transform(corner, piece.Frame).Y - origin.Y)).ToList();
        Assert.InRange(heights.Min(), -0.1f, 0.05f);
        Assert.InRange(heights.Max(), 0.3f, 1f);
    }

    [Fact]
    public void The_same_wave_gives_the_same_stones_and_the_list_is_reused()
    {
        var first = new List<ShockwavePiece>();
        var second = new List<ShockwavePiece> { new(1, Matrix4x4.Identity) };

        ShockwaveLook.Place(new Vector3(2f, 0f, 3f), 5.5f, first);
        ShockwaveLook.Place(new Vector3(2f, 0f, 3f), 5.5f, second);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Dust_never_lies_closer_to_the_middle_than_the_smallest_ring()
    {
        var origin = Vector3.Zero;
        foreach (var radius in SomeRadii)
        {
            var pieces = new List<ShockwavePiece>();
            ShockwaveLook.Place(origin, radius, pieces);
            Assert.All(pieces.Where(piece => piece.Part == 1), dust =>
                Assert.True(Flat(origin, dust.Frame.Translation) >= ShockwaveLook.MinRadius - Tolerance));
        }
    }

    // In the clip from close up the player stands still: the tick he is hit is the first tick the drawn edge reaches him.
    [Fact]
    public void Who_stands_still_is_hit_in_the_very_tick_the_drawn_edge_reaches_him()
    {
        var setup = MotionScenes.Find("gorm-slam")!.Build(Studio());
        var world = setup.World;
        var reached = new List<bool>();
        var hit = new List<bool>();

        for (var tick = 0; tick < setup.Ticks; tick++)
        {
            world.Tick(PlayerInput.None);
            hit.Add(world.Events.OfType<ShockwaveHit>().Any());
            reached.Add(world.Gorm!.Shockwave is { } wave && ShockwaveLook.Edge(wave.Radius) >= Flat(wave.Origin, world.Player.Position));
        }

        var hitAt = hit.IndexOf(true);
        Assert.True(hitAt > 0, "The wave never hit the player standing 8 m away.");
        Assert.Equal(1, hit.Count(h => h));
        Assert.Equal(reached.IndexOf(true), hitAt);
    }

    [Fact]
    public void Who_stands_outside_the_reach_is_never_reached_by_the_drawn_edge_nor_hit()
    {
        var setup = MotionScenes.Find("gorm-slam-far")!.Build(Studio());
        var world = setup.World;
        var waveSeen = false;

        for (var tick = 0; tick < setup.Ticks; tick++)
        {
            world.Tick(PlayerInput.None);
            Assert.Empty(world.Events.OfType<ShockwaveHit>());
            if (world.Gorm!.Shockwave is { } wave)
            {
                waveSeen = true;
                Assert.True(ShockwaveLook.Edge(wave.Radius) < Flat(wave.Origin, world.Player.Position));
            }
        }

        Assert.True(waveSeen, "Gorm never struck in the clip from afar.");
    }

    // Jumping over the drawn edge: in the air (higher than the wave) when it passes, the player is not hit - and once it
    // has passed, standing behind it inside the disc does not hurt either. Tried with the jump started a little earlier
    // or later; at least one start clears the wave, and every start that does was in the air when the edge reached him.
    [Fact]
    public void Who_jumps_over_the_drawn_edge_is_not_hit_and_landing_behind_it_does_not_hurt()
    {
        var reachedAt = TickTheEdgeReaches("gorm-slam");
        var height = Balance.Default.Gorm.ShockwaveHeight;
        var cleared = 0;

        for (var lead = 1; lead <= 30; lead++)
        {
            var setup = MotionScenes.Find("gorm-slam")!.Build(Studio());
            var world = setup.World;
            var hit = false;
            var airborneWhenReached = false;
            for (var tick = 0; tick < setup.Ticks; tick++)
            {
                var jump = tick >= reachedAt - lead && tick < reachedAt - lead + 3;
                world.Tick(PlayerInput.None with { Jump = jump });
                hit |= world.Events.OfType<ShockwaveHit>().Any();
                if (tick == reachedAt)
                {
                    airborneWhenReached = world.Player.Position.Y - world.Level.CellAt(world.Player.Position).FloorHeight >= height;
                }
            }

            Assert.Equal(!airborneWhenReached, hit);
            cleared += hit ? 0 : 1;
        }

        Assert.True(cleared > 0, "No jump cleared the wave.");
    }

    private static int TickTheEdgeReaches(string scene)
    {
        var setup = MotionScenes.Find(scene)!.Build(Studio());
        var world = setup.World;
        for (var tick = 0; tick < setup.Ticks; tick++)
        {
            world.Tick(PlayerInput.None);
            if (world.Gorm!.Shockwave is { } wave && ShockwaveLook.Edge(wave.Radius) >= Flat(wave.Origin, world.Player.Position))
            {
                return tick;
            }
        }

        throw new InvalidOperationException("The edge never reached the player.");
    }

    private static IEnumerable<Vector3> Corners(GlbPart part)
    {
        foreach (var x in new[] { part.PositionMin.X, part.PositionMax.X })
        {
            foreach (var y in new[] { part.PositionMin.Y, part.PositionMax.Y })
            {
                foreach (var z in new[] { part.PositionMin.Z, part.PositionMax.Z })
                {
                    yield return new Vector3(x, y, z);
                }
            }
        }
    }
}
