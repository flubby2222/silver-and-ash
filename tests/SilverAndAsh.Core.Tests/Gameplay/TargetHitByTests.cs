using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>Which weapon a hit came from (issue #267: the blade sound plays only on blade hits).</summary>
public class TargetHitByTests
{
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly PlayerInput Melee = PlayerInput.None with { Melee = true };

    [Fact]
    public void A_blade_hit_names_the_blade_right_after_the_hit()
    {
        var world = InFrontOfADummy();

        var events = Swing(world);

        var hit = events.FindIndex(e => e is DummyHit);
        Assert.True(hit >= 0);
        // Hard: the target is the wooden dummy (issue #776, the axe's hard hit sound).
        Assert.Equal(new TargetHitBy(0, world.Player.Id, Weapon.Blade) { Hard = true }, events[hit + 1]);
    }

    [Fact]
    public void A_club_hit_names_the_club()
    {
        var world = InFrontOfADummy();
        world.Player.EquipMelee(WeaponKind.Club);

        var events = Swing(world);

        Assert.Contains(events, e => e is DummyHit);
        Assert.Contains(new TargetHitBy(0, world.Player.Id, Weapon.Club) { Hard = true }, events);
        Assert.DoesNotContain(events, e => e is TargetHitBy { Weapon: Weapon.Blade });
    }

    [Fact]
    public void A_pistol_hit_names_the_pistol()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nD\n."));   // 6 m ahead: far beyond the blade

        world.Tick(PlayerInput.None with { Shoot = true });

        Assert.Contains(world.Events, e => e is DummyHit);
        Assert.Contains(new TargetHitBy(0, world.Player.Id, Weapon.Pistol) { Hard = true }, world.Events);
    }

    [Fact]
    public void A_swing_that_hits_nothing_names_no_weapon()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nD"));   // dummy 4 m ahead

        Assert.DoesNotContain(Swing(world), e => e is TargetHitBy);
    }

    private static List<GameEvent> Swing(GameWorld world)
    {
        var events = new List<GameEvent>();
        world.Tick(Melee);
        events.AddRange(world.Events);
        world.Tick(PlayerInput.None);
        events.AddRange(world.Events);
        return events;
    }

    private static GameWorld InFrontOfADummy()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nD"));
        for (var i = 0; i < SimulationClock.TicksPerSecond; i++)
        {
            world.Tick(Forward);
        }

        for (var i = 0; i < 10; i++)
        {
            world.Tick(PlayerInput.None);
        }

        return world;
    }
}
