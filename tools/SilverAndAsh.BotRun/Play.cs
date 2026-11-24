using System.Numerics;
using System.Reflection;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.BotRun;

/// <summary>
/// One world played by the bot, tick by tick, exactly as the game does it (<see cref="GameWorld.Tick"/> with an input).
/// After every tick it checks what must always hold: nobody stands inside a blocking door, a solid cell or the stone of
/// a boss portal.
/// </summary>
internal sealed class Play(GameWorld world, Report report, string context)
{
    private readonly HashSet<string> _reported = [];

    public static PlayerInput Forward { get; } = new(new Vector2(0, 1), Vector2.Zero, false);

    public static PlayerInput Interact { get; } = PlayerInput.None with { Interact = true };

    /// <summary>
    /// The balancing values of the run (<see cref="Use"/>, from assets/balance.json), with a player who cannot die by
    /// accident (the bot kills him on purpose). Set once at the start, before the first world is built.
    /// </summary>
    public static Balance Balance { get; private set; } = Immortal(Balance.Default);

    public static void Use(Balance balance) => Balance = Immortal(balance);

    private static Balance Immortal(Balance balance) => balance with { Player = balance.Player with { MaxHealth = 1_000_000 } };

    public GameWorld World => world;

    public string Context => context;

    /// <summary>Every event of every tick played so far.</summary>
    public List<GameEvent> Events { get; } = [];

    public static (int X, int Z) Cell(Vector3 position) => Level.ToGrid(position.X, position.Z);

    /// <summary>A cell next to <paramref name="door"/> (any of its cells) that belongs to <paramref name="room"/>, if any.</summary>
    public static (int X, int Z)? NeighbourIn(Door door, RoomState room)
    {
        foreach (var (x, z) in door.Cells)
        {
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                if (room.Cells.Contains((x + dx, z + dz)))
                {
                    return (x + dx, z + dz);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Ticks after the boss room seals by which its door must block: the hard stop of the slam
    /// (<see cref="DoorsBalance.BossShutTick"/>, issue #566), plus two ticks for the order inside one tick.
    /// </summary>
    public static int BossDoorShutTicks(GameWorld world) => world.Balance.Doors.BossShutTick + 2;

    public static RoomState OtherRoom(GameWorld world, Door door, RoomState room) => world.Rooms[door.RoomA == room.Index ? door.RoomB : door.RoomA];

    /// <summary>
    /// Kills every living ghoul and bat that stands in <paramref name="room"/> (all of them if null), except
    /// <paramref name="spare"/>.
    /// </summary>
    public static void KillEnemies(GameWorld world, RoomState? room, ITarget? spare = null)
    {
        foreach (var target in world.Targets.ToList())
        {
            if (target is Ghoul { IsDead: false } or Bat { IsDead: false } && target != spare
                && (room is null || room.Cells.Contains(Cell(target.Body.Position))))
            {
                target.TakeDamage(1_000_000, []);
            }
        }
    }

    public void Tick(PlayerInput input)
    {
        world.Tick(input);
        Events.AddRange(world.Events);
        CheckInvariants();
    }

    public void Ticks(int count, PlayerInput input)
    {
        for (var i = 0; i < count; i++)
        {
            Tick(input);
        }
    }

    /// <summary>Plays up to <paramref name="max"/> ticks until <paramref name="done"/> holds; returns whether it does.</summary>
    public bool TicksUntil(int max, PlayerInput input, Func<bool> done)
    {
        for (var i = 0; i < max && !done(); i++)
        {
            Tick(input);
        }

        return done();
    }

    /// <summary>Puts the player in the middle of <paramref name="cell"/> (the bot skips the way there).</summary>
    public void PlaceAt((int X, int Z) cell) => CoreInternals.PlaceAt(world.Player, world.Level.CellCenter(cell.X, cell.Z));

    public void PlaceAt(Vector3 feet) => CoreInternals.PlaceAt(world.Player, feet);

    /// <summary>Puts an enemy (ghoul, bat or Gorm) at <paramref name="position"/> at once.</summary>
    public static void PlaceEnemyAt(ITarget enemy, Vector3 position) => CoreInternals.PlaceEnemyAt(enemy, position);

    public void GiveKeys() => CoreInternals.AddKeys(world.Player, 5);

    /// <summary>Turns the player towards the middle of <paramref name="cell"/>, at eye height.</summary>
    public void Face((int X, int Z) cell) => Face(world.Level.CellCenter(cell.X, cell.Z));

    public void Face(Vector3 point) => PhotoStudio.Face(world.Player, new Vector3(point.X, world.Player.Position.Y + 1.6f, point.Z));

    /// <summary>Walks straight towards <paramref name="target"/>, turning every tick; true once within a quarter metre.</summary>
    public bool WalkTo(Vector3 target, int max = 900)
    {
        for (var i = 0; i < max; i++)
        {
            if (Distance(world.Player.Position, target) < 0.25f)
            {
                return true;
            }

            Face(target);
            Tick(Forward);
        }

        return false;
    }

    public static float Distance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    /// <summary>
    /// The heights <see cref="InPortalStone"/> tests at lie at most this far apart: four fifths of the thinnest block of
    /// the portal (<see cref="BossPortalShape.Blocks"/>, now the impost: 0.125 m), so none slips between two of them.
    /// </summary>
    public static float PortalStoneStep { get; } = BossPortalShape.Blocks.Min(block => block.Top - block.Bottom) * 0.8f;

    // The bottom and top of a body are tested this far inside it: a body standing on a plinth or touching the underside
    // of an impost is not in the stone.
    private const float PortalStoneInset = 0.01f;

    /// <summary>
    /// True if a body (a standing cylinder: feet, radius, height) reaches into the stone of a boss portal, by
    /// <see cref="BossPortal.Contains"/>: its axis and 24 points round its rim, at heights at most
    /// <see cref="PortalStoneStep"/> apart from the feet to the top. The rim is taken 5 mm inside (the same for every
    /// body, also for big Gorm), so a body that only touches the stone, as the collision leaves it, is no finding.
    /// </summary>
    public static bool InPortalStone(Level level, CylinderBody body)
    {
        const int RimPoints = 24;
        const float Touch = 0.005f;
        foreach (var portal in level.Portals.Where(portal => Distance(body.Position, new Vector3(portal.X, 0f, portal.Z)) < body.Radius + 3f))
        {
            foreach (var up in PortalStoneHeights(body.Height))
            {
                var y = body.Position.Y + PortalStoneInset + up;
                if (portal.Contains(body.Position with { Y = y }))
                {
                    return true;
                }

                var rim = MathF.Max(0f, body.Radius - Touch);
                for (var i = 0; i < RimPoints; i++)
                {
                    var angle = MathF.Tau * i / RimPoints;
                    if (portal.Contains(new Vector3(body.Position.X + (rim * MathF.Cos(angle)), y, body.Position.Z + (rim * MathF.Sin(angle)))))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>The heights at which <see cref="InPortalStone"/> tests a body this high, above its feet and the inset.</summary>
    public static IEnumerable<float> PortalStoneHeights(float height)
    {
        var heights = Math.Max(2, (int)MathF.Ceiling(height / PortalStoneStep) + 1);
        for (var h = 0; h < heights; h++)
        {
            yield return (height - (2f * PortalStoneInset)) * h / (heights - 1);
        }
    }

    /// <summary>
    /// A finding if a block of the portal could slip between the heights <see cref="InPortalStone"/> tests: for every
    /// body height and every block, with the feet every centimetre from below the block to above it, some tested height
    /// lies in the block whenever the block lies between the lowest and the highest tested height (issue #647).
    /// </summary>
    public static void CheckPortalStoneHeights(Report report, string context, IEnumerable<float> bodyHeights)
    {
        foreach (var height in bodyHeights)
        {
            var ups = PortalStoneHeights(height).ToList();
            foreach (var block in BossPortalShape.Blocks)
            {
                for (var feet = block.Bottom - height; feet <= block.Top; feet += 0.01f)
                {
                    var tested = ups.Select(rise => feet + PortalStoneInset + rise).ToList();
                    var between = block.Bottom >= tested[0] && block.Top <= tested[^1];
                    if (between && !tested.Any(y => y >= block.Bottom && y <= block.Top))
                    {
                        report.Finding(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                            $"{context}: the stone check misses the block {block.Bottom:0.000}..{block.Top:0.000} m for a body {height:0.00} m high with its feet at {feet:0.00} m (heights {PortalStoneStep:0.000} m apart)"));
                        break;
                    }
                }
            }
        }
    }

    private static bool Overlaps(Door door, Vector3 at, float radius)
    {
        var dx = at.X - Math.Clamp(at.X, door.BoxMin.X, door.BoxMax.X);
        var dz = at.Z - Math.Clamp(at.Z, door.BoxMin.Z, door.BoxMax.Z);
        return dx * dx + dz * dz < radius * radius;
    }

    private void CheckInvariants()
    {
        var player = world.Player;
        foreach (var door in world.Doors.Where(door => door.Blocks))
        {
            if (!player.IsDead && Overlaps(door, player.Position, 0.3f))
            {
                Once($"player stands in blocking door {door.Id}");
            }

            if (world.Ghouls.Any(ghoul => !ghoul.IsDead && Overlaps(door, ghoul.Position, 0.3f)))
            {
                Once($"ghoul stands in blocking door {door.Id}");
            }

            if (world.Bats.Any(bat => !bat.IsDead && Overlaps(door, bat.Position, 0.2f)))
            {
                Once($"bat hangs in blocking door {door.Id}");
            }

            if (world.Gorm is { IsDead: false } gorm && Overlaps(door, gorm.Position, 0.3f))
            {
                Once($"Gorm stands in blocking door {door.Id}");
            }
        }

        var (x, z) = Cell(player.Position);
        if (!player.IsDead && world.Level[x, z].IsSolid)
        {
            Once($"player stands in solid cell {x},{z}");
        }

        CheckPortalStone();
    }

    // Issues #598, #627, #629: no body ends up in the stone of a boss portal - walking (player, ghoul, Gorm) or flying
    // (a bat, also its corpse while it falls and lies there).
    private void CheckPortalStone()
    {
        if (world.Level.Portals.Count == 0)
        {
            return;
        }

        var bodies = new List<(string What, CylinderBody Body)>();
        if (!world.Player.IsDead)
        {
            bodies.Add(("player", world.Player.Body));
        }

        bodies.AddRange(world.Ghouls.Where(ghoul => !ghoul.IsDead).Select(ghoul => ("ghoul", ghoul.Body)));
        bodies.AddRange(world.Bats.Where(bat => !bat.IsGone).Select(bat => (bat.IsDead ? "dead bat" : "bat", bat.Body)));
        if (world.Gorm is { IsDead: false } gorm)
        {
            bodies.Add(("Gorm", gorm.Body));
        }

        foreach (var (what, body) in bodies.Where(body => InPortalStone(world.Level, body.Body)))
        {
            Once($"{what} in the stone of the boss portal", $" at {body.Position}");
        }
    }

    // One finding per kind of fault and world; the detail (where) is that of the first time.
    private void Once(string what, string detail = "")
    {
        if (_reported.Add(what))
        {
            report.Finding($"{context}: {what}{detail}");
        }
    }
}

/// <summary>Thrown when the bot cannot reach a member of the core any more: the bot needs updating, the game is not at fault.</summary>
internal sealed class MissingCoreMember(string member)
    : Exception($"{member} is not in SilverAndAsh.Core any more - update tools/SilverAndAsh.BotRun/Play.cs (CoreInternals).");

/// <summary>
/// The internal members of the core the bot needs (placing the player, giving him keys, placing an enemy). The bot
/// reaches them by reflection instead of opening the core (no InternalsVisibleTo for a tool); a renamed member stops the
/// run at once.
/// </summary>
internal static class CoreInternals
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly MethodInfo? Place = typeof(Player).GetMethod("PlaceAt", Any, [typeof(Vector3)]);

    private static readonly MethodInfo? Keys = typeof(Player).GetMethod("AddKeys", Any, [typeof(int)]);

    // The one list of every kind of enemy (issue #572): one internal interface for ghoul, bat and Gorm.
    private static readonly MethodInfo? PlaceEnemy = typeof(Player).Assembly
        .GetType("SilverAndAsh.Core.Gameplay.IEnemy")?.GetMethod("PlaceAt", [typeof(Vector3)]);

    public static void PlaceAt(Player player, Vector3 feet) =>
        (Place ?? throw new MissingCoreMember("Player.PlaceAt(Vector3)")).Invoke(player, [feet]);

    public static void AddKeys(Player player, int amount) =>
        (Keys ?? throw new MissingCoreMember("Player.AddKeys(int)")).Invoke(player, [amount]);

    public static void PlaceEnemyAt(ITarget enemy, Vector3 position) =>
        (PlaceEnemy ?? throw new MissingCoreMember("IEnemy.PlaceAt(Vector3)")).Invoke(enemy, [position]);
}
