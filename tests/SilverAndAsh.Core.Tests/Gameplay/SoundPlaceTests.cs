using System.Numerics;
using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Events with a sound say where they happened (issue #491): at the body of the enemy or player that makes them. Doors,
/// chests and the well are checked in their own tests.
/// </summary>
public class SoundPlaceTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };

    // Where a body was in the tick of its event and where it is after the tick lie at most one tick's move apart.
    private const float OneTickMove = 0.25f;

    [Fact]
    public void A_ghouls_sounds_come_from_the_ghoul()
    {
        var world = new GameWorld(LevelParser.Parse("P..G"));
        var ghoul = world.Ghouls[0];
        var heard = new List<(GameEvent Event, Vector3 Body)>();

        for (var tick = 0; tick < 3 * Second; tick++)
        {
            world.Tick(PlayerInput.None);
            heard.AddRange(world.Events.Where(e => e is GhoulNoticed or GhoulWindUp or GhoulStruck).Select(e => (e, ghoul.Position)));
        }

        Assert.Contains(heard, h => h.Event is GhoulWindUp);
        Assert.All(heard, h => AssertNear(h.Body, h.Event));
    }

    [Fact]
    public void A_ghoul_dies_where_it_stands()
    {
        var world = new GameWorld(LevelParser.Parse("P....G"));
        var ghoul = world.Ghouls[0];
        var events = new List<GameEvent>();

        ghoul.TakeDamage(ghoul.Health, events);

        Assert.Equal(ghoul.Position, events.OfType<GhoulHit>().Single().Place);
        Assert.Equal(ghoul.Position, events.OfType<GhoulDied>().Single().Place);
    }

    [Fact]
    public void A_bats_wing_beat_comes_from_the_bat()
    {
        var world = new GameWorld(LevelParser.Parse("P...B"));
        var flaps = new List<BatFlapped>();

        for (var tick = 0; tick < 2 * Second; tick++)
        {
            world.Tick(PlayerInput.None);
            flaps.AddRange(world.Events.OfType<BatFlapped>());
        }

        Assert.NotEmpty(flaps);
        Assert.All(flaps, flap => Assert.Equal(flap.At, flap.Place));
    }

    [Fact]
    public void A_bat_is_hit_and_dies_where_it_flies()
    {
        var world = new GameWorld(LevelParser.Parse("P...B"));
        var bat = world.Bats[0];
        var events = new List<GameEvent>();

        bat.TakeDamage(bat.Health, events);

        Assert.Equal(bat.Centre, events.OfType<BatHit>().Single().Place);
        Assert.Equal(bat.Centre, events.OfType<BatDied>().Single().Place);
    }

    [Fact]
    public void Gorms_steps_come_from_gorm()
    {
        var noAttacks = Balance.Default with { Gorm = Balance.Default.Gorm with { NearRange = 0f, FarRange = 0f } };
        var world = new GameWorld(LevelParser.Parse("P" + new string('.', 30) + "T"), 1, noAttacks);
        var gorm = world.Gorm!;
        var steps = new List<(GormStepped Step, Vector3 Body)>();

        for (var tick = 0; tick < 3 * Second; tick++)
        {
            world.Tick(PlayerInput.None);
            steps.AddRange(world.Events.OfType<GormStepped>().Select(step => (step, gorm.Position)));
        }

        Assert.NotEmpty(steps);
        Assert.All(steps, s => AssertNear(s.Body, s.Step));
    }

    [Fact]
    public void A_players_steps_and_jump_come_from_their_feet()
    {
        // A long corridor along +Z: walking 1.5 s gives steps (one per step length), then a jump and its landing.
        var world = new GameWorld(LevelParser.Parse("P" + string.Concat(Enumerable.Repeat("\n.", 20))));
        var player = world.Player;
        var heard = new List<(GameEvent Event, Vector3 Body)>();

        for (var tick = 0; tick < 3 * Second; tick++)
        {
            world.Tick(tick == 3 * Second / 2 ? Forward with { Jump = true } : Forward);
            heard.AddRange(world.Events.Where(e => e is IPlayerSound).Select(e => (e, player.Position)));
        }

        Assert.Contains(heard, h => h.Event is PlayerStepped);
        Assert.Contains(heard, h => h.Event is PlayerJumped);
        Assert.Contains(heard, h => h.Event is PlayerLanded);
        Assert.All(heard, h => AssertNear(h.Body, h.Event));
    }

    [Fact]
    public void A_players_own_sounds_are_theirs()
    {
        // Steps, jumps and reloading are IPlayerSound - the listener hears them in the middle (Hearing.PlaceFor).
        GameEvent[] own =
        [
            new PlayerStepped(0, Foot.Left), new PlayerJumped(0), new PlayerLanded(0, 2f), new PlayerDashed(0),
            new PlayerDodged(0), new ReloadStarted(0), new ReloadFinished(0),
        ];

        Assert.All(own, e => Assert.Null(Hearing.PlaceFor(e with { Place = Vector3.One }, 0)));
        Assert.All(own, e => Assert.Equal(Vector3.One, Hearing.PlaceFor(e with { Place = Vector3.One }, 1)));
    }

    private static void AssertNear(Vector3 body, GameEvent gameEvent)
    {
        Assert.True(gameEvent.Place is { } place && Vector3.Distance(place, body) < OneTickMove, $"{gameEvent} at {gameEvent.Place}, body at {body}");
    }
}
