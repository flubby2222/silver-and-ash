using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>Sirius' Relief in the world (issue #438): it blocks the way, is offered with F and turns spent.</summary>
public class WellOfSiriusTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    internal static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };

    // With the standard values the throw of these two worlds goes this way (issue #815). A balancing round that changes
    // well.luckChance may need other seeds; the two tests that use them then say so.
    internal const int LuckySeed = 1;
    internal const int UnluckySeed = 2;

    // A corridor along +Z, one cell wide: the player in row 0, the well in row 3.
    internal static GameWorld Corridor(Balance? balance = null, int seed = 1) =>
        new(LevelParser.Parse("#P#\n#.#\n#.#\n#.#\n#.#\n#.#"), seed, balance ?? Balance.Default, well: new WellPlan(1, 3, 0));

    /// <summary>Walks into the well until it stops the player, then looks down at it a little.</summary>
    internal static void StandBeforeWell(GameWorld world)
    {
        Run(world, Forward, 3 * Second);
        Run(world, PlayerInput.None, Second / 2);
        world.Player.Look(PlayerInput.None with { Look = new Vector2(0f, 20f * MathF.PI / 180f / ControlSettings.MouseSensitivity) });
    }

    private static float Flat(Vector3 v) => new Vector2(v.X, v.Z).Length();

    [Fact]
    public void The_well_stands_in_the_middle_of_its_cell_on_the_floor()
    {
        var well = Corridor().Well!;

        Assert.Equal(new Vector3(3f, 0f, 7f), well.Position);
        Assert.Equal(Balance.Default.Well.CollisionRadius, well.Radius);
        Assert.False(well.IsSpent);
    }

    [Fact]
    public void A_ready_well_stops_the_player_at_its_circle()
    {
        var world = Corridor();
        var well = world.Well!;

        Run(world, Forward, 3 * Second);

        Assert.Equal(well.Position.Z - well.Radius - BodySizes.Player.Radius, world.Player.Position.Z, 0.02f);
    }

    [Fact]
    public void The_circle_comes_from_the_balancing_values()
    {
        var narrow = Balance.Default with { Well = Balance.Default.Well with { CollisionRadius = 0.5f } };
        var world = Corridor(narrow);

        Run(world, Forward, 3 * Second);

        Assert.Equal(7f - 0.5f - BodySizes.Player.Radius, world.Player.Position.Z, 0.02f);
    }

    [Fact]
    public void A_spent_well_still_stops_the_player()
    {
        var world = Corridor();
        StandBeforeWell(world);
        Run(world, PressF, 1);
        Assert.True(world.Well!.IsSpent);

        Run(world, Forward, 2 * Second);

        Assert.Equal(world.Well.Position.Z - world.Well.Radius - BodySizes.Player.Radius, world.Player.Position.Z, 0.02f);
    }

    [Fact]
    public void A_ready_well_is_offered_by_its_name()
    {
        var world = Corridor();
        StandBeforeWell(world);

        var target = world.InteractionTarget(world.Player);

        Assert.Same(world.Well, target);
        Assert.Equal(new InteractionOffer("Use Sirius' Relief", true), target!.OfferFor(world.Player));
    }

    [Fact]
    public void F_spends_the_well_once_and_tells_it()
    {
        var world = Corridor();
        StandBeforeWell(world);
        var silver = world.Player.Silver;

        var events = Run(world, PressF, 1);

        Assert.True(world.Well!.IsSpent);
        Assert.Contains(new WellOfSiriusUsed(world.Player.Id), events);
        Assert.Equal(world.Well.Position, events.OfType<WellOfSiriusUsed>().Single().Place); // heard from the well (#491)

        // Provisional (issue #438): nothing paid, nothing given - the rules are not decided yet.
        Assert.Equal(silver, world.Player.Silver);
        Assert.Empty(world.EquipmentDrops);
        Assert.Empty(world.Loot);
        Assert.Empty(world.Pickups);
    }

    [Fact]
    public void A_lucky_throw_tells_it_right_after_the_throw_from_the_well()
    {
        var world = Corridor(seed: LuckySeed);
        StandBeforeWell(world);

        var events = Run(world, PressF, 1);

        var used = events.FindIndex(e => e is WellOfSiriusUsed);
        var gave = events.FindIndex(e => e is WellOfSiriusGave);
        Assert.True(used >= 0 && gave == used + 1, $"used at {used}, gave at {gave}");
        Assert.Equal(new WellOfSiriusGave(world.Player.Id), events[gave]);
        Assert.Equal(world.Well!.Position, events[gave].Place);
        Assert.Single(events, e => e is WellOfSiriusGave);
    }

    [Fact]
    public void An_unlucky_throw_tells_only_the_throw()
    {
        var world = Corridor(seed: UnluckySeed);
        StandBeforeWell(world);

        var events = Run(world, PressF, 1);

        Assert.True(world.Well!.IsSpent);
        Assert.Single(events, e => e is WellOfSiriusUsed);
        Assert.DoesNotContain(events, e => e is WellOfSiriusGave);
    }

    [Fact]
    public void The_same_seed_throws_the_same_way_and_both_ways_happen()
    {
        static bool Lucky(int seed)
        {
            var world = Corridor(seed: seed);
            StandBeforeWell(world);
            return Run(world, PressF, 1).Any(e => e is WellOfSiriusGave);
        }

        var first = Enumerable.Range(1, 40).Select(Lucky).ToArray();
        var second = Enumerable.Range(1, 40).Select(Lucky).ToArray();

        Assert.Equal(first, second);
        Assert.Contains(true, first);
        Assert.Contains(false, first);
    }

    [Theory]
    [InlineData(0f, false)]
    [InlineData(1f, true)]
    public void The_luck_comes_from_the_balancing_values(float chance, bool lucky)
    {
        var balance = Balance.Default with { Well = Balance.Default.Well with { LuckChance = chance } };
        foreach (var seed in Enumerable.Range(1, 20))
        {
            var world = Corridor(balance, seed);
            StandBeforeWell(world);

            Assert.Equal(lucky, Run(world, PressF, 1).Any(e => e is WellOfSiriusGave));
        }
    }

    [Fact]
    public void Without_a_throw_there_is_no_luck_either()
    {
        // Looking away or at a spent well: no throw, so no luck - even at a chance of 1.
        var always = Balance.Default with { Well = Balance.Default.Well with { LuckChance = 1f } };
        var world = Corridor(always);
        Run(world, Forward, 3 * Second);
        world.Player.Look(PlayerInput.None with { Look = new Vector2(MathF.PI / ControlSettings.MouseSensitivity, 0f) });
        Assert.DoesNotContain(Run(world, PressF, 1), e => e is WellOfSiriusGave);

        var spent = Corridor(always);
        StandBeforeWell(spent);
        Assert.Single(Run(spent, PressF, 1), e => e is WellOfSiriusGave);
        Run(spent, PlayerInput.None, 1);
        Assert.DoesNotContain(Run(spent, PressF, 1), e => e is WellOfSiriusGave);
    }

    // One run in an open room with two bats and the well two cells ahead: walk up, look down at the well, press F at
    // tick 100 (or not), then face the nearest bat, strike and shoot until the end. Every tick says where the bats are, how they are
    // and what lies on the floor.
    private static (List<string> Trace, bool Thrown, int BatsKilled, int Drops) FightAfterTheWell(int seed, bool throwCoin)
    {
        var world = new GameWorld(
            LevelParser.Parse("..P..\n.....\n.....\n.....\nB...B"), seed, Balance.Default, well: new WellPlan(2, 2, 0));
        var trace = new List<string>();
        var thrown = false;
        var drops = 0;
        for (var tick = 0; tick < 40 * Second; tick++)
        {
            if (tick == 90)
            {
                world.Player.Look(PlayerInput.None with { Look = new Vector2(0f, 20f * MathF.PI / 180f / ControlSettings.MouseSensitivity) });
            }

            var input = tick switch
            {
                < 60 => Forward,
                < 100 => PlayerInput.None,
                100 => throwCoin ? PressF : PlayerInput.None,
                _ => PlayerInput.None with { Melee = tick % 30 == 0, Shoot = tick % 30 == 15 }, // a blow and a shot every half second
            };
            if (tick > 100 && world.Bats.Where(bat => bat.IsHittable).MinBy(bat => Vector3.Distance(bat.Centre, world.Player.EyePosition)) is { } nearest)
            {
                // Fight as a player would: face the nearest bat.
                var direction = Vector3.Normalize(nearest.Centre - world.Player.EyePosition);
                world.Player.Look(PlayerInput.None with
                {
                    Look = new Vector2(
                        -(MathF.Atan2(direction.X, direction.Z) - world.Player.View.YawRadians) / ControlSettings.MouseSensitivity,
                        -(MathF.Asin(direction.Y) - world.Player.View.PitchRadians) / ControlSettings.MouseSensitivity),
                });
            }

            world.Tick(input);
            thrown |= world.Events.Any(e => e is WellOfSiriusUsed);
            drops = Math.Max(drops, world.Pickups.Count + world.Loot.Count);
            trace.Add(string.Join(
                " | ",
                world.Bats.Select(bat => $"{bat.Position} {bat.Health}")
                    .Concat(world.Pickups.Select(pickup => $"{pickup.Kind} {pickup.Position}"))
                    .Concat(world.Loot.Select(loot => $"{loot.Kind} {loot.Amount} {loot.Position}"))
                    .Append($"{world.Player.Position} {world.Player.Health}")));
        }

        return (trace, thrown, world.Bats.Count(bat => bat.IsDead), drops);
    }

    [Fact]
    public void A_throw_changes_neither_the_flight_of_the_bats_nor_what_they_drop()
    {
        // Review of PR #817: the luck of a throw has random numbers of its own, so the fight after a throw is the one
        // there would have been without it - the same world with and without the throw, compared tick by tick.
        var killed = 0;
        var drops = 0;
        var lucky = 0;
        foreach (var seed in Enumerable.Range(1, 12))
        {
            var with = FightAfterTheWell(seed, throwCoin: true);
            var without = FightAfterTheWell(seed, throwCoin: false);
            Assert.True(with.Thrown, $"Seed {seed}: the coin was never thrown - the test would prove nothing.");
            Assert.False(without.Thrown);

            var firstDifference = Enumerable.Range(0, with.Trace.Count).FirstOrDefault(tick => with.Trace[tick] != without.Trace[tick], -1);
            Assert.True(
                firstDifference < 0,
                $"Seed {seed}: the throw changed the fight from tick {firstDifference} on.");
            Assert.NotEqual(with.Trace[100], with.Trace[^1]); // the bats did fly after the throw
            killed += with.BatsKilled;
            drops += with.Drops;
            lucky += Corridor(seed: seed) is var corridor && LuckyIn(corridor) ? 1 : 0;
        }

        // The comparison covered what it names: bats died and left something, and both kinds of throw were among the seeds.
        Assert.True(killed > 0, "No bat died in any run.");
        Assert.True(drops > 0, "No bat dropped anything in any run.");
        Assert.InRange(lucky, 1, 11);
    }

    private static bool LuckyIn(GameWorld corridor)
    {
        StandBeforeWell(corridor);
        return Run(corridor, PressF, 1).Any(e => e is WellOfSiriusGave);
    }

    [Fact]
    public void A_spent_well_is_no_longer_offered_and_F_does_nothing_more()
    {
        var world = Corridor();
        StandBeforeWell(world);
        Run(world, PressF, 1);
        Run(world, PlayerInput.None, 1);

        Assert.Null(world.InteractionTarget(world.Player));
        Assert.Equal(new InteractionOffer("Use Sirius' Relief", false), world.Well!.OfferFor(world.Player));
        Assert.DoesNotContain(Run(world, PressF, 1), e => e is WellOfSiriusUsed);
        Assert.True(world.Well.IsSpent);
    }

    [Fact]
    public void Looking_away_F_leaves_the_well_ready()
    {
        var world = Corridor();
        Run(world, Forward, 3 * Second);
        world.Player.Look(PlayerInput.None with { Look = new Vector2(MathF.PI / ControlSettings.MouseSensitivity, 0f) });

        var events = Run(world, PressF, 1);

        Assert.False(world.Well!.IsSpent);
        Assert.DoesNotContain(events, e => e is WellOfSiriusUsed);
    }

    [Fact]
    public void A_world_without_a_well_has_none_and_F_spends_nothing()
    {
        var world = new GameWorld(LevelParser.Parse("#P#\n#.#\n#.#\n#.#"), 1, Balance.Default);

        var events = Run(world, PressF, 1);

        Assert.Null(world.Well);
        Assert.DoesNotContain(events, e => e is WellOfSiriusUsed);
    }

    [Fact]
    public void A_restarted_world_has_the_same_well_ready_again()
    {
        var world = Corridor();
        StandBeforeWell(world);
        Run(world, PressF, 1);

        var restarted = world.Restarted();

        Assert.Equal(world.Well!.Plan, restarted.Well!.Plan);
        Assert.False(restarted.Well.IsSpent);
    }

    [Fact]
    public void A_ghoul_walks_round_the_well_not_through_it()
    {
        // The ghoul straight behind the well; it must go round to reach the player.
        var level = LevelParser.Parse("#######\n#..P..#\n#.....#\n#.....#\n#.....#\n#..G..#\n#######");
        var world = new GameWorld(level, 1, Balance.Default, well: new WellPlan(3, 3, 0));
        var well = world.Well!;
        var ghoul = world.Ghouls[0];
        var closest = float.MaxValue;

        Run(world, PlayerInput.None, 6 * Second, () =>
        {
            closest = MathF.Min(closest, Flat(ghoul.Position - well.Position));
            return world.Player.Health < world.Player.MaxHealth;
        });

        Assert.True(world.Player.Health < world.Player.MaxHealth, "The ghoul never reached the player.");
        Assert.True(closest >= well.Radius + BodySizes.Ghoul.Radius - 0.02f, $"The ghoul came {closest} m close to the well's middle.");
    }

    [Fact]
    public void Enemies_still_see_past_the_well()
    {
        var world = Corridor();

        Assert.False(world.EnemyLevel[1, 3].IsSolid);
        Assert.False(world.Level[1, 3].IsSolid);
    }
}
