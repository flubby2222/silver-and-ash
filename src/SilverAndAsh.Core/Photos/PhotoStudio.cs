using System.Globalization;
using System.Numerics;
using System.Text;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Photos;

/// <summary>
/// The building blocks of the photo scenes: level 0 with a fixed seed, the player put into a room of a kind, a bare
/// arena with one figure at a chosen distance. Everything is built with the normal world - only where the player
/// stands and where they look are chosen. The same studio always builds the same scenes.
/// </summary>
public sealed class PhotoStudio
{
    /// <summary>The seed of every scene on level 0 (the room scenes take the first seed from here that has their room).</summary>
    public const int Seed = 1;

    // Seeds tried for a room of a kind before giving up - the generator places every kind long before that.
    private const int SeedsToTry = 1000;

    // Height above the floor the room scenes look at: a little below the eyes.
    private const float LookHeight = 1f;

    private readonly LevelZero _levelZero;

    /// <summary>A studio for level 0 from <paramref name="templates"/>, with <paramref name="balance"/> (standard values if null).</summary>
    public PhotoStudio(IReadOnlyList<RoomTemplate> templates, Balance? balance = null)
    {
        _levelZero = new LevelZero(templates);
        Balance = balance ?? Balance.Default;
    }

    /// <summary>The balancing values of every world built here.</summary>
    public Balance Balance { get; }

    /// <summary>A run of level 0 as it begins: start room, level name, standard weapons.</summary>
    public GameWorld StartRun() => _levelZero.NewRun(Seed, Balance);

    /// <summary>
    /// Level 0 with the player in a room of <paramref name="kind"/>: on the floor cell farthest from its middle that
    /// still sees the middle (past pillars and corners), looking there a little downwards, so the floor and what stands
    /// on it show. Arenas seal and wake up on entry, as in the game.
    /// </summary>
    /// <exception cref="InvalidOperationException">If no seed gives such a room.</exception>
    public GameWorld InRoom(RoomKind kind)
    {
        var (generated, room) = FirstRoom(kind);
        var level = generated.Level;
        var averageX = (float)room.FloorCells.Average(cell => cell.X);
        var averageZ = (float)room.FloorCells.Average(cell => cell.Z);
        var middle = room.FloorCells
            .OrderBy(cell => ((cell.X - averageX) * (cell.X - averageX)) + ((cell.Z - averageZ) * (cell.Z - averageZ)))
            .ThenBy(cell => cell.Z)
            .ThenBy(cell => cell.X)
            .Select(cell => level.CellCenter(cell.X, cell.Z) + new Vector3(0f, LookHeight, 0f))
            .First();

        var world = World(generated, BestPlace(level, room, middle, cell => -Vector3.Distance(level.CellCenter(cell.X, cell.Z), middle)));
        Face(world.Player, middle);
        return world;
    }

    /// <summary>
    /// Level 0 with the player in the boss room, on the floor cell nearest to <paramref name="metres"/> from Gorm that
    /// sees him, looking at him.
    /// </summary>
    public GameWorld BeforeGorm(float metres)
    {
        var (generated, room) = FirstRoom(RoomKind.Boss);
        var level = generated.Level;
        var gorm = level.BossPositions.Single();
        var head = gorm + new Vector3(0f, LookHeight, 0f);
        var cell = BestPlace(level, room, head, cell => MathF.Abs(Vector3.Distance(level.CellCenter(cell.X, cell.Z), gorm) - metres));

        var world = World(generated, cell);
        Face(world.Player, Middle(world.Gorm!.Body));
        return world;
    }

    /// <summary>
    /// A bare hall (hand-made, so its enemies are awake) with one figure - 'G' ghoul, 'B' bat, 'T' Gorm, 'D' dummy -
    /// <paramref name="metres"/> in front of the player (a multiple of 2, the cell size), looking at it. The ceiling
    /// lies at <paramref name="ceiling"/> (default: as high as a corridor; Gorm's boss room: <see cref="RoomTemplate.RoomCeilingHeight"/>).
    /// </summary>
    public GameWorld Arena(char figure, int metres, float ceiling = Cell.DefaultCeilingHeight)
    {
        if (metres <= 0 || metres % (int)Level.CellSize != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(metres), metres, "The distance must be a positive multiple of the cell size (2 m).");
        }

        // Room to all sides: the walls stay out of the way of the figure, and the fog shows how far the light reaches.
        const int Width = 9;
        const int PlayerRow = 2;
        var figureRow = PlayerRow + (metres / (int)Level.CellSize);
        var depth = figureRow + 4;
        var text = new StringBuilder();
        for (var z = 0; z < depth; z++)
        {
            for (var x = 0; x < Width; x++)
            {
                var wall = x == 0 || z == 0 || x == Width - 1 || z == depth - 1;
                text.Append(wall ? '#'
                    : x == Width / 2 && z == PlayerRow ? 'P'
                    : x == Width / 2 && z == figureRow ? figure
                    : '.');
            }

            text.Append('\n');
        }

        var level = LevelParser.Parse(text.ToString());
        if (ceiling != Cell.DefaultCeilingHeight)
        {
            level = level.WithCells(
                from x in Enumerable.Range(0, level.Width)
                from z in Enumerable.Range(0, level.Depth)
                where !level[x, z].IsSolid
                select (x, z, level[x, z] with { CeilingHeight = ceiling }));
        }

        var world = new GameWorld(level, Seed, Balance);
        var target = world.Targets.Single();
        Face(world.Player, Middle(target.Body));
        return world;
    }

    /// <summary>
    /// A bare hall with the chests of all four tiers side by side, closed and locked, 4 m in front of the player, their
    /// fronts towards them - and a fifth one, a Beggar's chest, unlocked and open (issue #249).
    /// </summary>
    public GameWorld ChestRow()
    {
        const int Width = 9;
        const int Depth = 8;
        const int PlayerRow = 2;
        const int ChestRow = PlayerRow + 2;
        var text = new StringBuilder();
        for (var z = 0; z < Depth; z++)
        {
            for (var x = 0; x < Width; x++)
            {
                var wall = x == 0 || z == 0 || x == Width - 1 || z == Depth - 1;
                text.Append(wall ? '#' : x == Width / 2 && z == PlayerRow ? 'P' : '.');
            }

            text.Append('\n');
        }

        // Looking towards +z, +x lies on the left: the row reads Beggar's .. Vampire's from left to right, the open one last.
        ChestTier[] tiers = [ChestTier.Beggar, ChestTier.Vampire, ChestTier.King, ChestTier.Guard, ChestTier.Beggar];
        var chests = tiers
            .Select((tier, index) => new ChestPlan(2 + index, ChestRow, 0, tier, IsLocked: true, Silver: 0,
                HasBloodVial: false, RisesWhenCleared: false, ChestFront.MinusZ))
            .ToList();
        var world = new GameWorld(LevelParser.Parse(text.ToString()), Seed, Balance, chests: chests);
        var open = world.Chests[0];
        open.Unlock();
        open.Open();
        Face(world.Player, world.Chests[2].Position + new Vector3(0f, Chest.Height / 2f, 0f));
        return world;
    }

    /// <summary>
    /// Turns <paramref name="player"/> to look at <paramref name="point"/> - through the normal look input (as if the
    /// mouse had moved exactly so far).
    /// </summary>
    public static void Face(Player player, Vector3 point)
    {
        ArgumentNullException.ThrowIfNull(player);
        var to = point - player.EyePosition;
        var yaw = MathF.Atan2(to.X, to.Z);
        var pitch = MathF.Atan2(to.Y, new Vector2(to.X, to.Z).Length());
        var turn = MathF.IEEERemainder(player.View.YawRadians - yaw, MathF.Tau);
        var nod = player.View.PitchRadians - pitch;
        player.Look(new PlayerInput(Vector2.Zero, new Vector2(turn, nod) / ControlSettings.MouseSensitivity, false));
    }

    private static Vector3 Middle(CylinderBody body) => body.Position + new Vector3(0f, body.Height / 2f, 0f);

    // A run of level 0 on this level (without the level name banner), the player moved onto cell `cell` before the
    // first tick. Moved, not started there: treasure and secret rooms are wall for enemies, so no level may start a
    // player inside them.
    private GameWorld World(GeneratedLevel generated, (int X, int Z) cell)
    {
        var world = new GameWorld(generated.Level, generated.Seed, Balance, generated.Doors, generated.Rooms, generated.Chests)
        {
            Surfaces = SurfaceVariants.For(generated),
        };
        world.Player.PlaceAt(generated.Level.CellCenter(cell.X, cell.Z));
        return world;
    }

    // The floor cell of `room` with the lowest `cost` from whose eye height `target` is in sight (ties: north, then
    // west first) - or simply the cheapest if none sees it.
    private static (int X, int Z) BestPlace(Level level, PlacedRoom room, Vector3 target, Func<(int X, int Z), float> cost)
    {
        var places = room.FloorCells.OrderBy(cost).ThenBy(cell => cell.Z).ThenBy(cell => cell.X).ToList();
        var eye = new Vector3(0f, BodySizes.Player.EyeHeight, 0f);
        return places.FirstOrDefault(cell => LineOfSight.IsClear(level, level.CellCenter(cell.X, cell.Z) + eye, target), places[0]);
    }

    private (GeneratedLevel Level, PlacedRoom Room) FirstRoom(RoomKind kind)
    {
        for (var seed = Seed; seed < Seed + SeedsToTry; seed++)
        {
            var generated = _levelZero.Build(seed, Balance);
            if (generated.Rooms.FirstOrDefault(room => room.Kind == kind) is { } room)
            {
                return (generated, room);
            }
        }

        throw new InvalidOperationException(string.Create(
            CultureInfo.InvariantCulture, $"No level 0 with seeds {Seed} to {Seed + SeedsToTry - 1} has a {kind} room."));
    }
}
