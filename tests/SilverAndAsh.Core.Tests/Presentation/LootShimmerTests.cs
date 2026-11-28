using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Issue #825: the shimmer of loot with a tier - the flash in steps, the floor glow with a factor per tier, the sparks,
/// the place of each piece in the pulse from its id, and the three knives of the throwing knives' loot. Time runs as in
/// the game, tick by tick (<see cref="SimulationClock.SecondsPerTick"/>); where a piece lies comes from a world that
/// lets it fall. Every number is read from <see cref="LootShimmer"/>.
/// </summary>
public class LootShimmerTests
{
    // Ticks of two whole pulses of the slowest part.
    private static readonly int Ticks = SimulationClock.SecondsToTicks(2f * LootShimmer.TurnPeriod);

    private static double Seconds(int tick) => tick * (double)SimulationClock.SecondsPerTick;

    private static IEnumerable<int> EveryTick => Enumerable.Range(0, Ticks);

    public static TheoryData<ItemTier> Tiers => [.. Enum.GetValues<ItemTier>()];

    [Fact]
    public void The_flash_goes_through_every_step_from_dark_to_bright_and_never_jumps_one()
    {
        var levels = EveryTick.Select(tick => LootShimmer.FlashLevel(Seconds(tick), id: 0)).ToList();

        var steps = LootShimmer.FlashSteps - 1;
        var wanted = Enumerable.Range(0, LootShimmer.FlashSteps).Select(step => (float)step / steps).ToList();
        Assert.Equal(wanted, levels.Distinct().Order().ToList());
        Assert.All(levels.Zip(levels.Skip(1)), pair =>
            Assert.True(MathF.Abs(pair.First - pair.Second) < 1.5f / steps, $"The flash jumped from {pair.First} to {pair.Second}."));
    }

    [Fact]
    public void The_flash_is_dark_and_bright_once_every_period()
    {
        var period = SimulationClock.SecondsToTicks(LootShimmer.FlashPeriod);
        var levels = EveryTick.Select(tick => LootShimmer.FlashLevel(Seconds(tick), id: 0)).ToList();

        // Counted as the game sees it: the ticks at which the flash arrives at its brightest step.
        var arrivals = Enumerable.Range(1, Ticks - 1).Count(tick => levels[tick] == 1f && levels[tick - 1] < 1f);
        Assert.InRange(arrivals, (Ticks / period) - 1, (Ticks / period) + 1);
        Assert.Equal(0f, levels[0]);
    }

    [Fact]
    public void A_model_and_a_box_light_up_between_their_two_ends()
    {
        Assert.Equal(LootShimmer.FlashLow, LootShimmer.FlashAmount(0f), 5);
        Assert.Equal(LootShimmer.FlashHigh, LootShimmer.FlashAmount(1f), 5);
        Assert.Equal(LootShimmer.BoxLow, LootShimmer.BoxBrightness(0f), 5);
        Assert.Equal(1f, LootShimmer.BoxBrightness(1f), 5);
        Assert.True(LootShimmer.FlashLow < LootShimmer.FlashHigh);
        Assert.InRange(LootShimmer.BoxLow, 0.01f, 0.99f);
    }

    [Theory]
    [MemberData(nameof(Tiers))]
    public void The_floor_glow_of_every_tier_pulses_between_its_ends_times_the_factor_of_the_tier(ItemTier tier)
    {
        var shares = EveryTick.Select(tick => LootShimmer.GlowShare(tier, Seconds(tick), id: 0)).ToList();

        var factor = LootShimmer.GlowFactor(tier);
        var low = LootShimmer.GlowLowSixteenths / 16f * factor;
        var high = MathF.Min(LootShimmer.GlowHighSixteenths / 16f * factor, LootShimmer.GlowShareLimit);
        Assert.Equal(low, shares.Min(), 5);
        Assert.Equal(high, shares.Max(), 5);
        Assert.Equal(low, shares[0], 5);
        Assert.All(shares, share => Assert.InRange(share, 0.001f, LootShimmer.GlowShareLimit));

        // In whole sixteenths before the factor: no step in between.
        Assert.All(shares.Where(share => share < LootShimmer.GlowShareLimit), share =>
        {
            var sixteenths = share / factor * 16f;
            Assert.Equal(MathF.Round(sixteenths), sixteenths, 3);
        });
    }

    [Fact]
    public void Gold_glows_weakest_on_the_floor_and_no_two_tiers_alike()
    {
        var factors = Enum.GetValues<ItemTier>().ToDictionary(tier => tier, LootShimmer.GlowFactor);

        Assert.Equal(factors.Values.Min(), factors[ItemTier.Royal]);
        Assert.Equal(factors.Count, factors.Values.Distinct().Count());

        // At every tick the glow of two tiers differs by exactly their factors (below the limit).
        foreach (var tick in EveryTick)
        {
            var royal = LootShimmer.GlowShare(ItemTier.Royal, Seconds(tick), id: 0);
            var forged = LootShimmer.GlowShare(ItemTier.Forged, Seconds(tick), id: 0);
            Assert.Equal(factors[ItemTier.Royal] / factors[ItemTier.Forged], royal / forged, 4);
        }
    }

    [Fact]
    public void The_glow_never_fills_the_floor_even_where_the_factor_would_ask_for_it()
    {
        var strongest = Enum.GetValues<ItemTier>().MaxBy(LootShimmer.GlowFactor);
        Assert.True(
            LootShimmer.GlowHighSixteenths / 16f * LootShimmer.GlowFactor(strongest) >= LootShimmer.GlowShareLimit,
            "This test needs a tier whose factor reaches the limit.");

        Assert.Equal(LootShimmer.GlowShareLimit, EveryTick.Max(tick => LootShimmer.GlowShare(strongest, Seconds(tick), id: 0)), 5);
    }

    [Fact]
    public void The_rings_of_the_glow_grow_outward_and_thin_out()
    {
        var rings = LootShimmer.Rings;

        Assert.True(rings.Count >= 2);
        Assert.Equal(1f, rings[0].Part);
        Assert.All(rings.Zip(rings.Skip(1)), pair =>
        {
            Assert.True(pair.First.Radius < pair.Second.Radius);
            Assert.True(pair.First.Part > pair.Second.Part);
            Assert.True(pair.Second.Part > 0f);
        });
    }

    [Fact]
    public void Each_piece_has_its_own_place_in_the_pulse_by_its_id()
    {
        var phases = Enumerable.Range(0, 30).Select(LootShimmer.Phase).ToList();

        Assert.All(phases, phase => Assert.InRange(phase, 0f, 0.9999f));
        Assert.Equal(0f, phases[0]);
        Assert.Equal(phases.Count, phases.Distinct().Count());

        // Two pieces dropped one after the other (ids 0 and 1) do not blink in step: at most ticks they show another step,
        // and the second one runs ahead by its phase of a period.
        var shift = phases[1] * LootShimmer.FlashPeriod;
        var apart = 0;
        foreach (var tick in EveryTick)
        {
            var first = LootShimmer.FlashLevel(Seconds(tick), id: 0);
            var second = LootShimmer.FlashLevel(Seconds(tick), id: 1);
            apart += first == second ? 0 : 1;
            Assert.Equal(LootShimmer.FlashLevel(Seconds(tick) + shift, id: 0), second, 3);
            Assert.Equal(
                LootShimmer.GlowShare(ItemTier.Worn, Seconds(tick) + (phases[1] * LootShimmer.GlowPeriod), id: 0),
                LootShimmer.GlowShare(ItemTier.Worn, Seconds(tick), id: 1), 3);
        }

        Assert.True(apart > Ticks / 2, $"Only at {apart} of {Ticks} ticks the two pieces showed different steps.");
    }

    [Fact]
    public void The_sparks_rise_round_the_piece_shrink_half_way_up_and_go_out()
    {
        var top = LootShimmer.SparkStart + (LootShimmer.SparkRise * LootShimmer.SparkLife);
        var outermost = LootShimmer.SparkRadius + (4 * LootShimmer.SparkRadiusStep);
        var half = LootShimmer.SparkStart + (LootShimmer.SparkRise * LootShimmer.SparkLife / 2f);
        var (small, large, shown) = (0, 0, 0);

        foreach (var tick in EveryTick)
        {
            var sparks = LootShimmer.Sparks(Seconds(tick), id: 3);
            shown += sparks.Count;
            Assert.InRange(sparks.Count, 1, LootShimmer.SparkCount - 1);
            foreach (var spark in sparks)
            {
                Assert.InRange(spark.Offset.Y, LootShimmer.SparkStart, top);
                var distance = (spark.Offset with { Y = 0f }).Length();
                Assert.InRange(distance, LootShimmer.SparkRadius - 1e-4f, outermost + 1e-4f);
                var edge = spark.Offset.Y < half ? LootShimmer.SparkEdge : LootShimmer.SparkEdgeHigh;
                Assert.Equal(edge, spark.Edge);
                (small, large) = spark.Edge == LootShimmer.SparkEdge ? (small, large + 1) : (small + 1, large);
            }
        }

        Assert.True(LootShimmer.SparkEdgeHigh < LootShimmer.SparkEdge);
        Assert.True(small > 0 && large > 0);

        // On average the share of the way a spark shows, of all sparks.
        Assert.Equal(LootShimmer.SparkCount * LootShimmer.SparkLife, (float)shown / Ticks, 0.05f);
    }

    [Fact]
    public void A_spark_rises_evenly_from_tick_to_tick_until_it_goes_out()
    {
        // The sparks of one piece by their place round it: a spark keeps its ring, so the nearest one of the next tick
        // in the same ring is the same spark.
        var perTick = LootShimmer.SparkRise / LootShimmer.FlashPeriod * SimulationClock.SecondsPerTick;
        var followed = 0;
        for (var tick = 0; tick + 1 < Ticks; tick++)
        {
            var now = LootShimmer.Sparks(Seconds(tick), id: 0);
            var next = LootShimmer.Sparks(Seconds(tick + 1), id: 0);
            foreach (var spark in now)
            {
                var same = next.Where(other => Vector3.Distance(other.Offset, spark.Offset) < 0.02f).ToList();
                if (same.Count == 1)
                {
                    Assert.Equal(perTick, same[0].Offset.Y - spark.Offset.Y, 4);
                    followed++;
                }
            }
        }

        Assert.True(followed > Ticks * 3, $"Only {followed} steps of sparks could be followed.");
    }

    [Fact]
    public void A_piece_bobs_round_its_height_and_turns_once_every_turn_period()
    {
        var bobs = EveryTick.Select(tick => LootShimmer.BobOf(Seconds(tick), id: 0)).ToList();
        Assert.Equal(LootShimmer.Bob, bobs.Max(), 3);
        Assert.Equal(-LootShimmer.Bob, bobs.Min(), 3);

        var turns = EveryTick.Select(tick => LootShimmer.TurnOf(Seconds(tick), id: 0)).ToList();
        Assert.All(turns, turn => Assert.InRange(turn, 0f, MathF.Tau));
        var perTick = MathF.Tau / LootShimmer.TurnPeriod * SimulationClock.SecondsPerTick;
        var wraps = 0;
        foreach (var (before, after) in turns.Zip(turns.Skip(1)))
        {
            if (after < before)
            {
                wraps++;
                continue;
            }

            Assert.Equal(perTick, after - before, 3);
        }

        Assert.InRange(wraps, 1, 2);
    }

    [Fact]
    public void The_three_knives_of_the_loot_meet_at_their_tips_and_lean_apart_evenly()
    {
        // In the knife's file: the base of the blade at the origin, the tip along +z.
        var fileTip = new Vector3(0f, 0f, ThrowingKnivesPose.BladeLength * ThrowingKnivesPose.FileScale);
        var length = ThrowingKnivesPose.BladeLength * LootShimmer.KnivesScale;
        var lean = LootShimmer.KnivesLeanDegrees * MathF.PI / 180f;
        var bases = new List<Vector3>();

        for (var knife = 0; knife < ThrowingKnivesPose.Count; knife++)
        {
            var pose = LootShimmer.KnifeInPiece(knife);
            var tip = Vector3.Transform(fileTip, pose);
            var bladeBase = Vector3.Transform(Vector3.Zero, pose);
            Assert.True(tip.Length() < 1e-4f, $"The tip of knife {knife} is {tip.Length()} m off the lowest point.");
            Assert.Equal(length, bladeBase.Length(), 4);
            Assert.Equal(length * MathF.Cos(lean), bladeBase.Y, 4);
            bases.Add(bladeBase);
        }

        // Evenly round the upright: every two bases equally far apart, none on another.
        var apart = length * MathF.Sin(lean) * MathF.Sqrt(3f);
        Assert.Equal(apart, Vector3.Distance(bases[0], bases[1]), 4);
        Assert.Equal(apart, Vector3.Distance(bases[1], bases[2]), 4);
        Assert.Equal(apart, Vector3.Distance(bases[2], bases[0]), 4);

        Assert.Throws<ArgumentOutOfRangeException>(() => LootShimmer.KnifeInPiece(ThrowingKnivesPose.Count));
        Assert.Throws<ArgumentOutOfRangeException>(() => LootShimmer.KnifeInPiece(-1));
    }

    // A world with one item lying at the middle of cell (x, z), fallen to the floor tick by tick.
    private static (GameWorld World, EquipmentDrop Drop) Lying(string map, int x, int z, Vector3 aside = default)
    {
        var level = LevelParser.Parse(map);
        var place = level.CellCenter(x, z) + aside + new Vector3(0f, 1f, 0f);
        var world = new GameWorld(level)
        {
            Finds = new GroundFinds([], [], [], [new ExhibitItem(ItemKind.HuntersEye, place)]),
        };
        var drop = Assert.Single(world.EquipmentDrops);
        for (var tick = 0; tick < 240 && (tick < 2 || drop.Position != drop.PreviousPosition); tick++)
        {
            world.Tick(PlayerInput.None);
        }

        Assert.Equal(drop.PreviousPosition, drop.Position);
        return (world, drop);
    }

    private static float Area(GlowDisc disc) =>
        disc.Triangles.Sum(triangle => 0.5f * Vector3.Cross(triangle.B - triangle.A, triangle.C - triangle.A).Length());

    private static float WholeArea(float radius) =>
        0.5f * FrostLook.DiscSegments * radius * radius * MathF.Sin(MathF.Tau / FrostLook.DiscSegments);

    private static IEnumerable<Vector3> Corners(GlowDisc disc) =>
        disc.Triangles.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C });

    [Fact]
    public void On_a_flat_floor_the_glow_is_one_whole_disc_per_ring_at_the_floor_below_the_piece()
    {
        var (world, drop) = Lying("P..\n...\n...", 1, 1);

        var discs = LootShimmer.GlowDiscs(world.Level, drop.Position, drop.Position.Y);

        Assert.Equal(LootShimmer.Rings.Count, discs.Count);
        for (var ring = 0; ring < discs.Count; ring++)
        {
            Assert.Equal(LootShimmer.Rings[ring].Part, discs[ring].Part);
            Assert.Equal(WholeArea(LootShimmer.Rings[ring].Radius), Area(discs[ring]), 3);
            Assert.All(Corners(discs[ring]), corner =>
            {
                Assert.Equal(drop.Position.Y, corner.Y);
                var distance = ((corner - drop.Position) with { Y = 0f }).Length();
                Assert.InRange(distance, 0f, LootShimmer.Rings[ring].Radius + 1e-4f);
            });
        }
    }

    [Fact]
    public void At_a_wall_the_glow_is_cut_off_and_at_a_step_it_stays_on_the_floor_of_the_piece()
    {
        // The piece lies in front of the wall to the east ('#'): its body clear of it, its widest ring reaching into it ...
        var widest = LootShimmer.Rings[^1].Radius;
        var gap = (BodySizes.Pickup.Radius + widest) / 2f;
        Assert.True(gap > LootShimmer.Rings[0].Radius, "This test needs an innermost ring that stays clear of the wall.");
        var aside = new Vector3((Level.CellSize / 2f) - gap, 0f, 0f);
        var edge = 2 * Level.CellSize;

        var (walled, atWall) = Lying("P.#\n..#\n..#", 1, 1, aside);
        Assert.Equal(edge - gap, atWall.Position.X, 3);
        AssertCutAt(edge, atWall, LootShimmer.GlowDiscs(walled.Level, atWall.Position, atWall.Position.Y));

        // ... and here in front of a step up ('1'): the glow does not climb it.
        var (stepped, atStep) = Lying("P.1\n..1\n..1", 1, 1, aside);
        Assert.True(stepped.Level[2, 1].FloorHeight > atStep.Position.Y, "The piece lies on the step, not in front of it.");
        AssertCutAt(edge, atStep, LootShimmer.GlowDiscs(stepped.Level, atStep.Position, atStep.Position.Y));
    }

    // The innermost disc whole, the widest one cut off at x = edge, everything at the height of the piece.
    private static void AssertCutAt(float edge, EquipmentDrop drop, IReadOnlyList<GlowDisc> discs)
    {
        Assert.Equal(WholeArea(LootShimmer.Rings[0].Radius), Area(discs[0]), 3);
        var whole = WholeArea(LootShimmer.Rings[^1].Radius);
        Assert.True(Area(discs[^1]) < whole * 0.99f, $"Nothing was cut off the widest disc: {Area(discs[^1])} of {whole} m2.");
        Assert.True(Area(discs[^1]) > whole * 0.5f);
        Assert.All(discs.SelectMany(Corners), corner =>
        {
            Assert.Equal(drop.Position.Y, corner.Y);
            Assert.True(corner.X <= edge + 1e-4f, $"A corner at x {corner.X} lies beyond the edge at {edge}.");
        });
    }

    [Fact]
    public void Glows_of_pieces_dropped_one_after_the_other_lie_at_different_heights_above_the_frost_disc()
    {
        var lifts = Enumerable.Range(0, LootShimmer.GlowLiftSteps).Select(LootShimmer.GlowLiftOf).ToList();

        Assert.Equal(lifts.Count, lifts.Distinct().Count());
        Assert.Equal(LootShimmer.GlowLift, lifts.Min());
        Assert.All(lifts.Zip(lifts.Skip(1)), pair => Assert.Equal(LootShimmer.GlowLiftStep, pair.Second - pair.First, 5));
        Assert.Equal(LootShimmer.GlowLiftOf(0), LootShimmer.GlowLiftOf(LootShimmer.GlowLiftSteps));
    }
}
