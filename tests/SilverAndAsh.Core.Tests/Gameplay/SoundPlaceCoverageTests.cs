using System.Numerics;
using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Every event with a sound that a ghoul, a bat, Gorm or a player makes says where it happened, at the body that made it
/// (issue #501, follow-up of #491). Each test plays one fight tick by tick until every kind of event of its table has
/// come at least once, then checks all of them: an event that loses its place sounds in the middle again, and the test
/// turns red. A new kind of sound of that body that is not in the table turns it red, too.
/// <para>
/// Heard in the middle on purpose, so not in the tables: a hit on a player (<see cref="BatBit"/>, like
/// <see cref="PlayerHit"/> and Gorm's <see cref="HookHit"/>, PR #496).
/// </para>
/// </summary>
public class SoundPlaceCoverageTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;

    // Rounding of float positions; far below anything that could be heard.
    private const float Slack = 1e-3f;

    [Fact]
    public void Every_sound_of_a_ghoul_comes_from_its_body()
    {
        // The ghoul down the corridor notices the player, comes and winds up; only then the blade strikes back until it dies.
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\n.\n.\nG"), balance: Tough);
        var ghoul = world.Ghouls[0];
        var fight = new Fight(world, () => ghoul.Body, MadeBy(nameof(GhoulDied.GhoulId), ghoul.Id));

        fight.Run(20 * Second, () => ghoul.IsDead, tick =>
            Aim(world.Player, ghoul.Body) with { Melee = fight.Saw<GhoulWindUp>() && tick % 20 < 10 });

        fight.AssertEveryKindIsPlacedInTheBody(typeof(GhoulNoticed), typeof(GhoulWindUp), typeof(GhoulDied));
    }

    [Fact]
    public void Every_sound_of_a_bat_comes_from_its_body()
    {
        // The player walks from side to side, so the bat's dives miss; once it has spat, screeched and missed, the pistol
        // brings it down.
        var world = new GameWorld(LevelParser.Parse(Hall(11, 11, player: (5, 3), ('B', 5, 6))), balance: Tough);
        var bat = world.Bats[0];
        var fight = new Fight(world, () => bat.Body, MadeBy(nameof(BatDied.BatId), bat.Id, except: typeof(BatBit)));

        fight.Run(40 * Second, () => bat.IsDead, tick =>
            Aim(world.Player, bat.Body) with
            {
                Move = new Vector2(tick / Second % 2 == 0 ? 1f : -1f, 0f),
                Shoot = fight.Saw<BatSpat>() && fight.Saw<BatDiveMissed>() && tick % 10 < 5,
                Reload = world.Player.Ranged.Magazine.Rounds == 0,
            });

        fight.AssertEveryKindIsPlacedInTheBody(
            typeof(BatFlapped), typeof(BatSpitWindUp), typeof(BatSpat), typeof(BatScreeched), typeof(BatDiveMissed),
            typeof(BatHit), typeof(BatDied));
    }

    [Fact]
    public void Every_sound_of_gorm_comes_from_his_body()
    {
        // Gorm starts farther than his near range: he throws first, pulls the player in, then hammers and swings. Once all
        // three attacks have come, the pistol fights him down through his roar to his death. Less health than the standard
        // only so the test does not take minutes.
        var balance = Tough with { Gorm = Tough.Gorm with { MaxHealth = Tough.Pistol.Damage * 12 } };
        var world = new GameWorld(LevelParser.Parse(Hall(15, 25, player: (7, 12), ('T', 7, 18))), balance: balance);
        var gorm = world.Gorm!;
        var fight = new Fight(world, () => gorm.Body, MadeBy(nameof(GormDied.GormId), gorm.Id));

        // The player backs away until Gorm has walked after him; then stands.
        fight.Run(120 * Second, () => gorm.IsDead, tick =>
            Aim(world.Player, gorm.Body) with
            {
                Move = new Vector2(0f, fight.Saw<GormStepped>() ? 0f : -1f),
                Shoot = fight.Saw<GormStepped>() && fight.Saw<GormSlammed>() && fight.Saw<GormSwingStarted>()
                    && fight.Saw<GormThrew>() && tick % 10 < 5,
                Reload = world.Player.Ranged.Magazine.Rounds == 0,
            });

        fight.AssertEveryKindIsPlacedInTheBody(
            typeof(GormStepped), typeof(GormWindUp), typeof(GormSlammed), typeof(GormSwingStarted), typeof(GormThrew),
            typeof(GormHit), typeof(GormRoared), typeof(GormDied));
        Assert.Equal(
            [GormAttack.Hammer, GormAttack.Swing, GormAttack.Throw],
            fight.Heard.Select(h => h.Event).OfType<GormWindUp>().Select(w => w.Attack).Distinct().Order());
    }

    [Fact]
    public void Every_sound_of_a_player_comes_from_their_body()
    {
        // Down a long corridor: walk, jump, jump and dash in the air, dodge on the ground, shoot, reload, swing the blade.
        var world = new GameWorld(LevelParser.Parse("P" + string.Concat(Enumerable.Repeat("\n.", 60))), balance: Tough);
        var player = world.Player;
        var fight = new Fight(world, () => player.Body, e => e is IPlayerSound own && own.PlayerId == player.Id);
        var forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };

        fight.Run(8 * Second, () => false, tick => (tick / (Second / 2)) switch
        {
            3 => forward with { Jump = tick == 3 * Second / 2 },
            5 => forward with { Jump = tick == 5 * Second / 2, Dash = tick == (5 * Second / 2) + 10 },
            8 => PlayerInput.None with { Dash = tick == 4 * Second },
            10 => PlayerInput.None with { Shoot = tick == 5 * Second },
            11 => PlayerInput.None with { Reload = tick == 11 * Second / 2 },
            13 => PlayerInput.None with { Melee = tick < (13 * Second / 2) + 5 },
            < 3 => forward,
            _ => PlayerInput.None,
        });

        fight.AssertEveryKindIsPlacedInTheBody(
            typeof(PlayerStepped), typeof(PlayerJumped), typeof(PlayerLanded), typeof(PlayerDashed), typeof(PlayerDodged),
            typeof(ShotFired), typeof(ReloadStarted), typeof(ReloadFinished), typeof(MeleeSwung));
    }

    /// <summary>The events of a body: those whose <paramref name="idName"/> (GhoulId, BatId, GormId) is <paramref name="id"/>.</summary>
    private static Func<GameEvent, bool> MadeBy(string idName, int id, Type? except = null) =>
        gameEvent => gameEvent.GetType() != except && gameEvent.GetType().GetProperty(idName)?.GetValue(gameEvent) is int made && made == id;

    /// <summary>
    /// Turns <paramref name="player"/>'s view to the middle of <paramref name="body"/> before the tick, as the game turns
    /// it with the mouse every frame, and gives the input for the tick (nothing pressed yet).
    /// </summary>
    private static PlayerInput Aim(Player player, CylinderBody body)
    {
        var direction = Vector3.Normalize(body.Position + new Vector3(0f, body.Height / 2f, 0f) - player.EyePosition);
        player.Look(PlayerInput.None with
        {
            Look = new Vector2(
                -(MathF.Atan2(direction.X, direction.Z) - player.View.YawRadians) / ControlSettings.MouseSensitivity,
                -(MathF.Asin(direction.Y) - player.View.PitchRadians) / ControlSettings.MouseSensitivity),
        });
        return PlayerInput.None;
    }

    /// <summary>An open hall with walls round it and the player and one figure (symbol, column, row) in it.</summary>
    private static string Hall(int width, int depth, (int X, int Z) player, (char Symbol, int X, int Z) figure) =>
        string.Join('\n', Enumerable.Range(0, depth).Select(z => new string(Enumerable.Range(0, width).Select(x =>
            x == 0 || z == 0 || x == width - 1 || z == depth - 1 ? '#'
            : (x, z) == player ? 'P'
            : (x, z) == (figure.X, figure.Z) ? figure.Symbol
            : '.').ToArray())));

    /// <summary>One heard event and the body that made it, as it stood before and after the tick of the event.</summary>
    private sealed record Heard(GameEvent Event, CylinderBody Before, CylinderBody After);

    /// <summary>Ticks a world and keeps every event with a sound that the watched body makes.</summary>
    private sealed class Fight(GameWorld world, Func<CylinderBody> body, Func<GameEvent, bool> makes)
    {
        public List<Heard> Heard { get; } = [];

        public bool Saw<T>()
            where T : GameEvent => Heard.Any(h => h.Event is T);

        /// <summary>Ticks with the input <paramref name="inputFor"/> gives for the tick until <paramref name="done"/> (and one more), at most <paramref name="ticks"/>.</summary>
        public void Run(int ticks, Func<bool> done, Func<int, PlayerInput> inputFor)
        {
            for (var tick = 0; tick < ticks; tick++)
            {
                var before = body();
                world.Tick(inputFor(tick));
                var after = body();
                Heard.AddRange(world.Events.Where(e => SoundBoard.KindFor(e) is not null && makes(e)).Select(e => new Heard(e, before, after)));
                if (done())
                {
                    return;
                }
            }
        }

        /// <summary>Every kind of <paramref name="expected"/> came, no other one, and each event lies in the body of its tick.</summary>
        public void AssertEveryKindIsPlacedInTheBody(params Type[] expected)
        {
            Assert.Equal(expected.Select(type => type.Name).Order(), Heard.Select(h => h.Event.GetType().Name).Distinct().Order());
            Assert.All(Heard, h => Assert.True(
                h.Event.Place is { } place && IsInBody(place, h.Before, h.After),
                $"{h.Event} at {h.Event.Place?.ToString() ?? "no place"}, the body from {h.Before.Position} to {h.After.Position}"));
        }

        /// <summary>
        /// Whether <paramref name="place"/> lies in the body somewhere on its way through the tick (straight from
        /// <paramref name="before"/> to <paramref name="after"/>): the event came at some moment of the tick.
        /// </summary>
        private static bool IsInBody(Vector3 place, CylinderBody before, CylinderBody after)
        {
            var way = after.Position - before.Position;
            var flatWay = new Vector2(way.X, way.Z);
            var t = flatWay.LengthSquared() <= 1e-12f
                ? 0f
                : Math.Clamp(Vector2.Dot(new Vector2(place.X - before.Position.X, place.Z - before.Position.Z), flatWay) / flatWay.LengthSquared(), 0f, 1f);
            var feet = before.Position + (way * t);
            var radius = MathF.Max(before.Radius, after.Radius);
            var height = MathF.Max(before.Height, after.Height);
            var flat = new Vector2(place.X - feet.X, place.Z - feet.Z).Length();
            var up = place.Y - MathF.Min(feet.Y, MathF.Min(before.Position.Y, after.Position.Y));
            return flat <= radius + Slack && up >= -Slack && up <= height + MathF.Abs(way.Y) + Slack;
        }
    }
}
