using System.Numerics;
using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.Gameplay.JumpTestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// The events of issue #291 for the sounds: jumping, dashing, dodging, landing (with the fall height), the end of the
/// consecration and a ghoul noticing a player - when they come and when they do not.
/// </summary>
public class MovementEventTests
{
    private static readonly PlayerInput Shift = PlayerInput.None with { Dash = true };
    private static readonly PlayerInput JumpOnly = PlayerInput.None with { Jump = true };
    private static readonly float JumpHeight = Balance.Default.Player.JumpHeight;
    private static readonly int Second = SimulationClock.TicksPerSecond;

    [Fact]
    public void A_jump_reports_one_jump_in_the_tick_it_takes_off()
    {
        var player = new Player(Hall());

        player.Tick(JumpOnly);

        var jump = Assert.Single(player.Events.OfType<PlayerJumped>());
        Assert.Equal(player.Id, jump.PlayerId);
        Assert.False(player.IsGrounded);
    }

    [Fact]
    public void Holding_jump_jumps_and_reports_only_once()
    {
        var player = new Player(Hall());

        var events = Run(player, JumpOnly, AirTicks + 30);

        Assert.Single(events.OfType<PlayerJumped>());
    }

    [Fact]
    public void Walking_and_standing_report_no_jump_dash_or_landing()
    {
        var player = new Player(Hall());

        var events = Run(player, Forward, 3 * Second).Concat(Run(player, PlayerInput.None, Second)).ToList();

        Assert.DoesNotContain(events, e => e is PlayerJumped or PlayerDashed or PlayerDodged or PlayerLanded);
    }

    [Fact]
    public void A_jump_on_flat_ground_lands_once_from_the_jump_height()
    {
        var player = new Player(Hall());
        player.Tick(JumpOnly);

        var events = Run(player, PlayerInput.None, AirTicks + 10);

        var landing = Assert.Single(events.OfType<PlayerLanded>());
        Assert.Equal(player.Id, landing.PlayerId);
        Assert.Equal(JumpHeight, landing.FallHeight, 0.1f);
        Assert.True(landing.FallHeight >= SoundBoard.LandingSoundMinimumFallHeight, "A normal jump is heard landing.");
    }

    [Fact]
    public void Walking_into_a_pit_lands_from_the_depth_of_the_pit()
    {
        var player = new Player(LevelParser.Parse("P\n_\n_\n_\n_\n."));

        var events = Run(player, Forward, 2 * Second);

        var landing = Assert.Single(events.OfType<PlayerLanded>());
        Assert.Equal(-LevelParser.PitFloorHeight, landing.FallHeight, 0.1f);
        Assert.Empty(events.OfType<PlayerJumped>());
    }

    [Fact]
    public void Walking_over_low_steps_is_never_a_landing_loud_enough_to_hear()
    {
        // Up onto a platform of 0.25 m, up to 0.5 m, then down again step by step to the floor.
        var player = new Player(LevelParser.Parse("P\n.\n1\n2\n2\n1\n.\n.\n.\n."));

        var events = Run(player, Forward, 3 * Second);

        Assert.True(player.Position.Z > 8 * Level.CellSize, "The player walked over all the steps.");
        Assert.All(events.OfType<PlayerLanded>(), landing =>
            Assert.True(landing.FallHeight < SoundBoard.LandingSoundMinimumFallHeight, $"Step landing of {landing.FallHeight} m."));
    }

    [Fact]
    public void A_dash_in_the_air_reports_one_dash_and_no_dodge()
    {
        var player = new Player(Hall());
        player.Tick(JumpOnly);
        Run(player, PlayerInput.None, 5);

        player.Tick(Shift);

        var dash = Assert.Single(player.Events.OfType<PlayerDashed>());
        Assert.Equal(player.Id, dash.PlayerId);
        Assert.Empty(player.Events.OfType<PlayerDodged>());
    }

    [Fact]
    public void A_second_dash_in_the_same_stay_in_the_air_reports_nothing()
    {
        var player = new Player(Hall());
        player.Tick(JumpOnly);
        Run(player, PlayerInput.None, 5);
        player.Tick(Shift);
        Run(player, PlayerInput.None, 2);

        var events = new List<GameEvent>();
        while (!player.IsGrounded)
        {
            player.Tick(Shift);
            events.AddRange(player.Events);
            player.Tick(PlayerInput.None);
            events.AddRange(player.Events);
        }

        Assert.Empty(events.OfType<PlayerDashed>());
    }

    [Fact]
    public void A_dodge_on_the_ground_reports_one_dodge_and_no_dash_even_with_shift_held()
    {
        var player = new Player(Hall());

        var events = Run(player, Shift, Second / 2);

        var dodge = Assert.Single(events.OfType<PlayerDodged>());
        Assert.Equal(player.Id, dodge.PlayerId);
        Assert.Empty(events.OfType<PlayerDashed>());
    }

    [Fact]
    public void A_dodge_the_stamina_cannot_pay_reports_nothing()
    {
        var player = new Player(Hall());
        var events = new List<GameEvent>();

        // Dodge again and again until the bar is empty: each dodge that happens reports itself, a refused one does not.
        for (var i = 0; i < 20; i++)
        {
            var dodging = player.IsDodging;
            player.Tick(Shift);
            var started = !dodging && player.IsDodging;
            Assert.Equal(started ? 1 : 0, player.Events.OfType<PlayerDodged>().Count());
            events.AddRange(player.Events);
            while (player.IsDodging)
            {
                player.Tick(PlayerInput.None);
            }

            player.Tick(PlayerInput.None);
        }

        Assert.NotEmpty(events.OfType<PlayerDodged>());
        Assert.True(events.OfType<PlayerDodged>().Count() < 20, "The bar ran empty, some dodges were refused.");
    }

    [Fact]
    public void The_consecration_reports_its_end_once_in_the_tick_it_runs_out()
    {
        var world = new GameWorld(Hall());
        var player = world.Player;
        player.Consecrate(1f);

        var endedAt = new List<int>();
        for (var tick = 0; tick < 2 * Second; tick++)
        {
            world.Tick(PlayerInput.None);
            if (world.Events.OfType<ConsecrationEnded>().Any(e => e.PlayerId == player.Id))
            {
                endedAt.Add(tick);
                Assert.False(player.IsConsecrated);
            }
            else if (endedAt.Count == 0)
            {
                Assert.True(player.IsConsecrated);
            }
        }

        Assert.Single(endedAt);
    }

    [Fact]
    public void A_new_consecration_before_the_end_postpones_the_end()
    {
        var player = new Player(Hall());
        player.Consecrate(1f);
        Run(player, PlayerInput.None, Second / 2);
        player.Consecrate(1f);

        var events = Run(player, PlayerInput.None, Second - 2);

        Assert.Empty(events.OfType<ConsecrationEnded>());
        Assert.Single(Run(player, PlayerInput.None, 4).OfType<ConsecrationEnded>());
    }

    [Fact]
    public void Without_a_consecration_or_after_death_no_end_is_reported()
    {
        var unconsecrated = new Player(Hall());
        Assert.Empty(Run(unconsecrated, PlayerInput.None, 2 * Second).OfType<ConsecrationEnded>());

        var dying = new Player(Hall());
        dying.Consecrate(1f);
        dying.TakeDamage(dying.Health, []);
        Assert.Empty(Run(dying, PlayerInput.None, 2 * Second).OfType<ConsecrationEnded>());
    }

    [Fact]
    public void A_ghoul_reports_noticing_a_player_once_not_every_tick()
    {
        var world = new GameWorld(LevelParser.Parse("P.....G"));        // 12 m, nothing in between
        var noticed = new List<GhoulNoticed>();

        for (var i = 0; i < 3 * Second; i++)
        {
            world.Tick(PlayerInput.None);
            noticed.AddRange(world.Events.OfType<GhoulNoticed>());
        }

        var notice = Assert.Single(noticed);
        Assert.Equal(world.Ghouls[0].Id, notice.GhoulId);
        Assert.Equal(world.Player.Id, notice.PlayerId);
    }

    [Fact]
    public void A_ghoul_that_cannot_see_the_player_reports_nothing()
    {
        var world = new GameWorld(LevelParser.Parse("P.#.G"));

        for (var i = 0; i < 2 * Second; i++)
        {
            world.Tick(PlayerInput.None);
            Assert.Empty(world.Events.OfType<GhoulNoticed>());
        }
    }

    [Fact]
    public void A_ghoul_woken_by_a_hit_reports_noticing_the_player()
    {
        var world = new GameWorld(LevelParser.Parse("P.......G"));      // too far to be seen
        world.Ghouls[0].TakeDamage(10, []);

        world.Tick(PlayerInput.None);

        Assert.Single(world.Events.OfType<GhoulNoticed>());
    }

    [Fact]
    public void The_events_are_the_same_in_two_runs_with_the_same_input()
    {
        static List<string> Record()
        {
            var player = new Player(LevelParser.Parse("P\n.\n1\n2\n.\n_\n_\n_\n.\n.\n."));
            var log = new List<string>();
            for (var i = 0; i < 4 * Second; i++)
            {
                var input = (i % 50) switch
                {
                    0 => ForwardJump,
                    10 => ForwardDash,
                    30 => ForwardDash,
                    _ => Forward,
                };
                player.Tick(input);
                log.AddRange(player.Events.Select(e => $"{i}:{e}"));
            }

            return log;
        }

        var first = Record();

        Assert.Contains(first, line => line.Contains(nameof(PlayerJumped), StringComparison.Ordinal));
        Assert.Equal(first, Record());
    }

    private static List<GameEvent> Run(Player player, PlayerInput input, int ticks)
    {
        var events = new List<GameEvent>();
        for (var i = 0; i < ticks; i++)
        {
            player.Tick(input);
            events.AddRange(player.Events);
        }

        return events;
    }

    // An open hall of 11 x 11 cells (22 m), the player in the middle: no walls in the way.
    private static Level Hall()
    {
        var rows = Enumerable.Range(0, 11)
            .Select(row => new string('.', 5) + (row == 5 ? "P" : ".") + new string('.', 5));
        return LevelParser.Parse(string.Join('\n', rows));
    }
}
