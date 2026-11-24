using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>Sirius' Relief in the world (issue #438): it blocks the way, is offered with F and turns spent.</summary>
public class WellOfSiriusTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };

    // A corridor along +Z, one cell wide: the player in row 0, the well in row 3.
    private static GameWorld Corridor(Balance? balance = null) =>
        new(LevelParser.Parse("#P#\n#.#\n#.#\n#.#\n#.#\n#.#"), 1, balance ?? Balance.Default, well: new WellPlan(1, 3, 0));

    private static List<GameEvent> Run(GameWorld world, PlayerInput input, int ticks, Func<bool>? until = null)
    {
        var events = new List<GameEvent>();
        for (var tick = 0; tick < ticks && until?.Invoke() != true; tick++)
        {
            world.Tick(input);
            events.AddRange(world.Events);
        }

        return events;
    }

    /// <summary>Walks into the well until it stops the player, then looks down at it a little.</summary>
    private static void StandBeforeWell(GameWorld world)
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
