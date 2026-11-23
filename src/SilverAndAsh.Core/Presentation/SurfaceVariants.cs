using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Presentation;

/// <summary>One of the three textures a kind of surface has (issue #244). A is the first one; B and C were added later.</summary>
public enum TextureVariant
{
    /// <summary>The first texture (e.g. catacomb_wall.png). Also what every surface falls back to.</summary>
    A,

    /// <summary>The second texture (e.g. catacomb_wall_b.png).</summary>
    B,

    /// <summary>The third texture (e.g. catacomb_wall_c.png). On the floor these are cobbles - see <see cref="SurfaceVariants"/>.</summary>
    C,
}

/// <summary>The textures chosen for one room: one variant each for its walls, floor and ceiling.</summary>
public readonly record struct RoomLook(TextureVariant Wall, TextureVariant Floor, TextureVariant Ceiling)
{
    /// <summary>Variant A everywhere - the look of hand-made levels and of cells that belong to no room.</summary>
    public static RoomLook Plain => default;

    /// <summary>The variant of surfaces of kind <paramref name="kind"/>.</summary>
    public TextureVariant For(SurfaceKind kind) => kind switch
    {
        SurfaceKind.Floor => Floor,
        SurfaceKind.Ceiling => Ceiling,
        SurfaceKind.Wall => Wall,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown kind of surface."),
    };
}

/// <summary>
/// Which texture variant each surface of a generated level shows (issue #244). The rule:
/// <list type="bullet">
/// <item>Each room picks one variant for its walls, one for its floor and one for its ceiling, independently, from the
/// seed of the level and its room number. The same seed always gives the same picture.</item>
/// <item>Within one surface of a room nothing is mixed: all its walls show the same variant, and so on.</item>
/// <item>A corridor takes the choice of the room it begins at (the room it was joined to first).</item>
/// <item>Floor C (cobbles) is the busiest picture: it only lies in start, treasure and secret rooms - never in combat
/// and boss rooms, where the floor is the background of the enemies, and never in corridors. Where the roll says C
/// but C is not allowed, a second roll from the same number picks A or B.</item>
/// <item>A door cell looks like the room on its inner side (<see cref="LevelDoor.RoomA"/>). Cells of no room, and every
/// cell of a hand-made level, show A.</item>
/// </list>
/// The choice is worked out from the seed with its own mixing function, not drawn from the level generator's random
/// numbers - so it never shifts the layouts that existing seeds give.
/// </summary>
public sealed class SurfaceVariants
{
    private readonly IReadOnlyList<RoomLook> _rooms;
    private readonly Dictionary<(int X, int Z), int> _roomOfCell;

    private SurfaceVariants(IReadOnlyList<RoomLook> rooms, Dictionary<(int X, int Z), int> roomOfCell)
    {
        _rooms = rooms;
        _roomOfCell = roomOfCell;
    }

    /// <summary>Variant A on every surface: for hand-made levels.</summary>
    public static SurfaceVariants Plain { get; } = new([], new());

    /// <summary>The look of each room, by room number (<see cref="PlacedRoom.Index"/>).</summary>
    public IReadOnlyList<RoomLook> Rooms => _rooms;

    /// <summary>The variants of <paramref name="generated"/>, by the rule above.</summary>
    public static SurfaceVariants For(GeneratedLevel generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        var rooms = generated.Rooms;
        var looks = new RoomLook[rooms.Count];
        for (var index = 0; index < rooms.Count; index++)
        {
            var room = rooms[index];
            if (room.Kind == RoomKind.Corridor)
            {
                var origin = Origin(generated, index);
                looks[index] = Roll(generated.Seed, origin, allowCobbles: false);
            }
            else
            {
                looks[index] = Roll(generated.Seed, index, AllowsCobbles(room.Kind));
            }
        }

        var roomOfCell = new Dictionary<(int X, int Z), int>();
        foreach (var room in rooms)
        {
            foreach (var cell in room.FloorCells)
            {
                roomOfCell[cell] = room.Index;
            }
        }

        foreach (var door in generated.Doors)
        {
            foreach (var cell in door.Cells)
            {
                roomOfCell.TryAdd(cell, door.RoomA);
            }
        }

        return new SurfaceVariants(looks, roomOfCell);
    }

    /// <summary>
    /// Chosen looks instead of rolled ones (the showroom, issue #283): cell (x, z) shows <paramref name="looks"/>[n] if
    /// <paramref name="lookOfCell"/> gives it n, otherwise variant A.
    /// </summary>
    public static SurfaceVariants Of(IReadOnlyList<RoomLook> looks, IReadOnlyDictionary<(int X, int Z), int> lookOfCell)
    {
        ArgumentNullException.ThrowIfNull(looks);
        ArgumentNullException.ThrowIfNull(lookOfCell);
        return new SurfaceVariants(looks.ToList(), new Dictionary<(int X, int Z), int>(lookOfCell));
    }

    /// <summary>True for the kinds of room whose floor may be cobbles (floor variant C).</summary>
    public static bool AllowsCobbles(RoomKind kind) => kind is RoomKind.Start or RoomKind.Treasure or RoomKind.Secret;

    /// <summary>The variant of the surface of kind <paramref name="kind"/> seen from walkable cell (x, z).</summary>
    public TextureVariant At(int x, int z, SurfaceKind kind) =>
        _roomOfCell.TryGetValue((x, z), out var room) && room < _rooms.Count ? _rooms[room].For(kind) : TextureVariant.A;

    // The room a corridor begins at: the other room of the first door that joins it. (The generator adds the door
    // to the room a corridor was placed from before any other door of it.) Corridors never begin at corridors; should
    // one ever do, the corridor rolls for itself.
    private static int Origin(GeneratedLevel generated, int corridor)
    {
        foreach (var door in generated.Doors)
        {
            var other = door.RoomB == corridor ? door.RoomA : door.RoomA == corridor ? door.RoomB : -1;
            if (other >= 0)
            {
                return generated.Rooms[other].Kind == RoomKind.Corridor ? corridor : other;
            }
        }

        return corridor;
    }

    private static RoomLook Roll(int seed, int room, bool allowCobbles) => new(
        Pick(Mix(seed, room, SurfaceKind.Wall), allowC: true),
        Pick(Mix(seed, room, SurfaceKind.Floor), allowCobbles),
        Pick(Mix(seed, room, SurfaceKind.Ceiling), allowC: true));

    // One of three; if that is C but C is not allowed, the rest of the number decides between A and B. So a room and a
    // corridor that begins there agree on every variant except cobbles.
    private static TextureVariant Pick(ulong number, bool allowC)
    {
        var variant = (TextureVariant)(number % 3);
        return variant != TextureVariant.C || allowC ? variant : (TextureVariant)(number / 3 % 2);
    }

    // SplitMix64's finaliser over seed, room and surface: well-spread numbers from neighbouring inputs, with no state.
    private static ulong Mix(int seed, int room, SurfaceKind kind)
    {
        var z = ((ulong)(uint)seed << 32) | ((uint)room * 4u) | (uint)kind;
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
