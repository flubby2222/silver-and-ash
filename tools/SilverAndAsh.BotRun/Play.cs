using System.Numerics;
using System.Reflection;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.BotRun;

/// <summary>
/// One world played by the bot, tick by tick, exactly as the game does it (<see cref="GameWorld.Tick"/> with an input).
/// After every tick it checks what must always hold: nobody stands inside a blocking door or a solid cell.
/// </summary>
internal sealed class Play(GameWorld world, Report report, string context)
{
    private readonly HashSet<string> _reported = [];

    public static PlayerInput Forward { get; } = new(new Vector2(0, 1), Vector2.Zero, false);

    public static PlayerInput Interact { get; } = PlayerInput.None with { Interact = true };

    /// <summary>The balancing values of the game, with a player who cannot die by accident (the bot kills him on purpose).</summary>
    public static Balance Balance { get; } = Balance.Default with { Player = Balance.Default.Player with { MaxHealth = 1_000_000 } };

    public GameWorld World => world;

    public string Context => context;

    /// <summary>Every event of every tick played so far.</summary>
    public List<GameEvent> Events { get; } = [];

    public static (int X, int Z) Cell(Vector3 position) => Level.ToGrid(position.X, position.Z);

    /// <summary>The cell next to <paramref name="door"/> that belongs to <paramref name="room"/>, if any.</summary>
    public static (int X, int Z)? NeighbourIn(Door door, RoomState room)
    {
        foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            if (room.Cells.Contains((door.X + dx, door.Z + dz)))
            {
                return (door.X + dx, door.Z + dz);
            }
        }

        return null;
    }

    public static RoomState OtherRoom(GameWorld world, Door door, RoomState room) => world.Rooms[door.RoomA == room.Index ? door.RoomB : door.RoomA];

    /// <summary>Kills every living ghoul and bat that stands in <paramref name="room"/> (all of them if null).</summary>
    public static void KillEnemies(GameWorld world, RoomState? room)
    {
        foreach (var target in world.Targets.ToList())
        {
            if (target is Ghoul { IsDead: false } or Bat { IsDead: false } && (room is null || room.Cells.Contains(Cell(target.Body.Position))))
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
        }

        var (x, z) = Cell(player.Position);
        if (!player.IsDead && world.Level[x, z].IsSolid)
        {
            Once($"player stands in solid cell {x},{z}");
        }
    }

    private void Once(string what)
    {
        if (_reported.Add(what))
        {
            report.Finding($"{context}: {what}");
        }
    }
}

/// <summary>Thrown when the bot cannot reach a member of the core any more: the bot needs updating, the game is not at fault.</summary>
internal sealed class MissingCoreMember(string member)
    : Exception($"{member} is not in SilverAndAsh.Core any more - update tools/SilverAndAsh.BotRun/Play.cs (CoreInternals).");

/// <summary>
/// The two internal members of the core the bot needs (placing the player, giving him keys). The bot reaches them by
/// reflection instead of opening the core (no InternalsVisibleTo for a tool); a renamed member stops the run at once.
/// </summary>
internal static class CoreInternals
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly MethodInfo? Place = typeof(Player).GetMethod("PlaceAt", Any, [typeof(Vector3)]);

    private static readonly MethodInfo? Keys = typeof(Player).GetMethod("AddKeys", Any, [typeof(int)]);

    public static void PlaceAt(Player player, Vector3 feet) =>
        (Place ?? throw new MissingCoreMember("Player.PlaceAt(Vector3)")).Invoke(player, [feet]);

    public static void AddKeys(Player player, int amount) =>
        (Keys ?? throw new MissingCoreMember("Player.AddKeys(int)")).Invoke(player, [amount]);
}
