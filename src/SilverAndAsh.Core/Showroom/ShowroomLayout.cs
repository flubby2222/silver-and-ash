using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Showroom;

/// <summary>A small text in the picture: <paramref name="Text"/> at <paramref name="Position"/> (world coordinates).</summary>
public sealed record ShowroomLabel(string Text, Vector3 Position);

/// <summary>The two parts of a section: one at corridor height (3.5 m), one at room height (6 m).</summary>
public enum ShowroomPart
{
    Corridor,
    Room,
}

/// <summary>Where a piece stands: cell (x, z) of part <paramref name="Part"/> of section <paramref name="Section"/>.</summary>
public sealed record PlacedPiece(ShowroomPiece Piece, int X, int Z, int Section, ShowroomPart Part);

/// <summary>
/// One section of the tour: walls, floor and ceiling all in <paramref name="Variant"/>. Its band runs west to east -
/// first the corridor part, then the room part, each <paramref name="Length"/> cells long - with the doors in the north
/// wall of the corridor part (row <paramref name="DoorRow"/>). The room's floor runs from row <paramref name="NorthRow"/>
/// to <paramref name="SouthRow"/>; the corridor is narrower (see <see cref="SouthRowOf"/>), so the lantern reaches its
/// doors from every step.
/// </summary>
public sealed record ShowroomSection(int Index, TextureVariant Variant, int CorridorX, int RoomX, int Length, int DoorRow, int NorthRow, int SouthRow)
{
    /// <summary>The first column of <paramref name="part"/>.</summary>
    public int StartOf(ShowroomPart part) => part == ShowroomPart.Corridor ? CorridorX : RoomX;

    /// <summary>The southmost floor row of <paramref name="part"/>: the corridor is 3 rows wide, the room 5.</summary>
    public int SouthRowOf(ShowroomPart part) => part == ShowroomPart.Corridor ? NorthRow + CorridorRows - 1 : SouthRow;

    /// <summary>The middle floor row of <paramref name="part"/>, where one walks along.</summary>
    public int MiddleRowOf(ShowroomPart part) => (NorthRow + SouthRowOf(part)) / 2;

    /// <summary>Floor rows of the corridor part.</summary>
    public const int CorridorRows = 3;

    /// <summary>The ceiling height of <paramref name="part"/>, in metres.</summary>
    public static float CeilingOf(ShowroomPart part) => part == ShowroomPart.Corridor ? Cell.DefaultCeilingHeight : RoomTemplate.RoomCeilingHeight;
}

/// <summary>
/// The showroom (issue #283, docs/musterraum.md): a level that shows every model of the game side by side under game
/// conditions. A tour of three sections, one per texture variant (A, B, C), each with a part at corridor and at room
/// height. Every wall piece (doors, cracked wall) stands once per section, in the north wall of the corridor part with a
/// closet behind - where doors stand in the game too: every door cell of a generated level has the corridor ceiling,
/// the boss gate's as well. Every other piece stands once, in a row through the middle of the room parts (taking turns
/// between the sections), where there is room for its height. Wall pillars stand against the north wall of every part
/// (issue #536). Built
/// from <see cref="ShowroomPieces.All"/> with a fixed rule - the same list always gives the same showroom.
/// </summary>
public sealed class ShowroomLayout
{
    /// <summary>Name shown briefly at the start.</summary>
    public const string Name = "THE SHOWROOM";

    // Cells from one door to the next in a wall (a closet of 3 and a wall between), and from one floor piece to the next.
    private const int WallStep = 4;
    private const int FloorStep = 3;

    // Rows of a section, counted from its first row: closet back wall, 2 closet rows, the door row (the band's north
    // wall), 5 floor rows (pieces in the middle one, walkways either side) and the south wall.
    private const int SectionRows = 10;
    private const int ClosetRow = 1;
    private const int DoorRowOffset = 3;
    private const int FloorRows = 5;

    // Width of the passages joining the sections into a loop; columns of the band's west end and of the passages.
    private const int PassageWidth = 3;
    private const int BandX = 8;

    private static readonly TextureVariant[] Variants = [TextureVariant.A, TextureVariant.B, TextureVariant.C];

    private ShowroomLayout(
        Level level, IReadOnlyList<LevelDoor> doors, IReadOnlyList<ChestPlan> chests, SurfaceVariants surfaces,
        ShowroomExhibits exhibits, IReadOnlyList<ShowroomLabel> labels, IReadOnlyList<ShowroomSection> sections,
        IReadOnlyList<PlacedPiece> placed)
    {
        Level = level;
        Doors = doors;
        Chests = chests;
        Surfaces = surfaces;
        Exhibits = exhibits;
        Labels = labels;
        Sections = sections;
        Placed = placed;
    }

    /// <summary>The level: the tour and its closets, the enemies on their cells, the player at the start of section A.</summary>
    public Level Level { get; }

    /// <summary>The doors and cracked walls, all closed.</summary>
    public IReadOnlyList<LevelDoor> Doors { get; }

    /// <summary>The chests, standing and unlocked.</summary>
    public IReadOnlyList<ChestPlan> Chests { get; }

    /// <summary>The texture variants: each section in its own, its closets and doors too; the passages in A.</summary>
    public SurfaceVariants Surfaces { get; }

    /// <summary>Stairs, loot, consumables and weapons on the floor, figures and wall pillars.</summary>
    public ShowroomExhibits Exhibits { get; }

    /// <summary>A label above every piece and at the start of every part.</summary>
    public IReadOnlyList<ShowroomLabel> Labels { get; }

    /// <summary>The sections in the order of the tour (A, B, C).</summary>
    public IReadOnlyList<ShowroomSection> Sections { get; }

    /// <summary>Every piece where it stands: wall pieces once per section, the others once.</summary>
    public IReadOnlyList<PlacedPiece> Placed { get; }

    /// <summary>
    /// Builds the showroom of <paramref name="pieces"/> (<see cref="ShowroomPieces.All"/> if null).
    /// </summary>
    /// <exception cref="ArgumentException">If the list has more than one Gorm (a world has at most one).</exception>
    public static ShowroomLayout Build(IReadOnlyList<ShowroomPiece>? pieces = null)
    {
        pieces ??= ShowroomPieces.All;
        if (pieces.OfType<EnemyPiece>().Count(piece => piece.Enemy == ShowroomEnemy.Gorm) > 1)
        {
            throw new ArgumentException("The showroom can show at most one Gorm.", nameof(pieces));
        }

        var wallPieces = pieces.OfType<WallPiece>().ToList();
        var floorPieces = pieces.Where(piece => piece is not WallPiece).ToList();
        var perRoom = (floorPieces.Count + Variants.Length - 1) / Variants.Length;

        // Each part is as long as the corridor needs for its doors and the room for its share of the floor pieces, with
        // a walkway at each end.
        var length = Math.Max(Math.Max(WallStep * wallPieces.Count, (FloorStep * perRoom) + 1), 6);
        var east = BandX + (2 * length) - 1;
        var bottom = Variants.Length * SectionRows;
        var width = east + PassageWidth + 2;
        var depth = bottom + PassageWidth + 1;

        var cells = new Cell[width, depth];
        for (var x = 0; x < width; x++)
        {
            for (var z = 0; z < depth; z++)
            {
                cells[x, z] = Cell.Wall;
            }
        }

        var lookOfCell = new Dictionary<(int X, int Z), int>();
        void Carve(int x0, int z0, int x1, int z1, float ceiling, int? look)
        {
            for (var x = x0; x <= x1; x++)
            {
                for (var z = z0; z <= z1; z++)
                {
                    cells[x, z] = Cell.Floor with { CeilingHeight = ceiling };
                    if (look is { } section)
                    {
                        lookOfCell[(x, z)] = section;
                    }
                }
            }
        }

        var sections = new List<ShowroomSection>();
        for (var index = 0; index < Variants.Length; index++)
        {
            var top = index * SectionRows;
            var section = new ShowroomSection(
                index, Variants[index], BandX, BandX + length, length, top + DoorRowOffset, top + DoorRowOffset + 1, top + DoorRowOffset + FloorRows);
            sections.Add(section);
            foreach (var part in Enum.GetValues<ShowroomPart>())
            {
                var start = section.StartOf(part);
                Carve(start, section.NorthRow, start + length - 1, section.SouthRowOf(part), ShowroomSection.CeilingOf(part), index);
            }
        }

        // The passages that join the sections into a loop: A's west end to the way back, A to B in the east, B to C in
        // the west, C in the east down to the way back along the south and up the west side. Walls stand between.
        var a = sections[0];
        var b = sections[1];
        var c = sections[2];
        var corridor = Cell.DefaultCeilingHeight;
        Carve(1, a.NorthRow, BandX - 1, a.SouthRow, corridor, null);
        Carve(1, a.NorthRow, PassageWidth, bottom + PassageWidth - 1, corridor, null);
        Carve(1, bottom, east + PassageWidth, bottom + PassageWidth - 1, corridor, null);
        Carve(east + 1, a.NorthRow, east + PassageWidth, b.SouthRow, corridor, null);
        Carve(BandX - PassageWidth, b.NorthRow, BandX - 1, c.SouthRow, corridor, null);
        Carve(east + 1, c.NorthRow, east + PassageWidth, bottom + PassageWidth - 1, corridor, null);

        var doors = new List<LevelDoor>();
        var placed = new List<PlacedPiece>();
        var labels = new List<ShowroomLabel>();
        var keys = new List<(int X, int Z)>();
        var closets = 0;
        foreach (var section in sections)
        {
            var start = section.CorridorX;
            for (var slot = 0; slot < wallPieces.Count; slot++)
            {
                var piece = wallPieces[slot];
                var x = start + 2 + (WallStep * slot);
                var z = section.DoorRow;
                Carve(x, z, x, z, Cell.DefaultCeilingHeight, section.Index);
                Carve(x - 1, z - DoorRowOffset + ClosetRow, x + 1, z - 1, Cell.DefaultCeilingHeight, section.Index);
                doors.Add(new LevelDoor(x, z, section.Index * 2, (Variants.Length * 2) + closets++, RoomSide.North, piece.Kind));
                placed.Add(new PlacedPiece(piece, x, z, section.Index, ShowroomPart.Corridor));
                if (piece.Kind == DoorKind.Locked)
                {
                    keys.Add((x + 1, section.NorthRow));
                }
            }
        }

        // Floor pieces take turns between the room parts of the sections, in the middle row, west to east.
        var ghouls = new List<(int X, int Z)>();
        var bats = new List<(int X, int Z)>();
        var bosses = new List<(int X, int Z)>();
        var floorPlaces = floorPieces.Select((piece, index) =>
        {
            var section = sections[index % sections.Count];
            var x = section.RoomX + 2 + (FloorStep * (index / sections.Count));
            return new PlacedPiece(piece, x, section.MiddleRowOf(ShowroomPart.Room), section.Index, ShowroomPart.Room);
        }).ToList();
        placed.AddRange(floorPlaces);
        foreach (var place in floorPlaces)
        {
            if (place.Piece is EnemyPiece enemy)
            {
                (enemy.Enemy switch { ShowroomEnemy.Ghoul => ghouls, ShowroomEnemy.Bat => bats, _ => bosses }).Add((place.X, place.Z));
            }
        }

        var startSection = sections[0];
        var level = new Level(cells, startSection.CorridorX + 1, startSection.MiddleRowOf(ShowroomPart.Corridor), null, ghouls, bats, bosses);

        Vector3 Above(int x, int z, float height) => level.CellCenter(x, z) + new Vector3(0f, height, 0f);

        var chests = new List<ChestPlan>();
        Vector3? stairs = null;
        var loot = new List<ExhibitLoot>();
        var consumables = new List<ExhibitConsumable>();
        var weapons = new List<ExhibitWeapon>();
        var items = new List<ExhibitItem>();
        var skeletons = new List<ExhibitCrossbowSkeleton>();
        var wardens = new List<ExhibitCryptWarden>();
        foreach (var place in placed)
        {
            var (x, z) = (place.X, place.Z);
            var labelHeight = place.Piece switch
            {
                WallPiece => 0f,
                ChestPiece chest => AddChest(chests, x, z, place.Section, chest.Tier),
                StairsPiece => SetStairs(ref stairs, level.CellCenter(x, z)),
                EnemyPiece { Enemy: ShowroomEnemy.Ghoul } => BodySizes.Ghoul.Height + 0.5f,
                EnemyPiece { Enemy: ShowroomEnemy.Bat } => BodySizes.Bat.HoverHeight + 1f,
                EnemyPiece => BodySizes.Gorm.Height + 0.6f,
                CrossbowSkeletonPiece skeleton => AddSkeleton(skeletons, new ExhibitCrossbowSkeleton(skeleton.Stance, level.CellCenter(x, z), Vector3.UnitZ)),
                CryptWardenPiece warden => AddWarden(wardens, new ExhibitCryptWarden(warden.Stance, level.CellCenter(x, z), Vector3.UnitZ)),
                WeaponPiece weapon => Add(weapons, new ExhibitWeapon(weapon.Weapon, level.CellCenter(x, z))),
                ItemPiece item => Add(items, new ExhibitItem(item.Item, level.CellCenter(x, z))),
                LootPiece pile => Add(loot, new ExhibitLoot(pile.Loot, pile.Amount, level.CellCenter(x, z))),
                ConsumablePiece consumable => Add(consumables, new ExhibitConsumable(consumable.Consumable, level.CellCenter(x, z))),
                _ => throw new ArgumentException($"The showroom cannot place {place.Piece.GetType().Name}.", nameof(pieces)),
            };

            // A door's label hangs on the wall above it, on the side of the walkway.
            labels.Add(place.Piece is WallPiece
                ? new ShowroomLabel(place.Piece.Label, new Vector3((x + 0.5f) * Level.CellSize, 3.1f, ((z + 1) * Level.CellSize) + 0.3f))
                : new ShowroomLabel(place.Piece.Label, Above(x, z, labelHeight)));
        }

        foreach (var (x, z) in keys)
        {
            loot.Add(new ExhibitLoot(LootKind.Key, 1, level.CellCenter(x, z)));
            labels.Add(new ShowroomLabel("Key", Above(x, z, 0.9f)));
        }

        foreach (var section in sections)
        {
            foreach (var part in Enum.GetValues<ShowroomPart>())
            {
                var text = part == ShowroomPart.Corridor ? "corridor 3.5 m" : "room 6 m";
                labels.Add(new ShowroomLabel($"Wall {section.Variant} - {text}", Above(section.StartOf(part), section.MiddleRowOf(part), 2.6f)));
            }
        }

        var pillars = WallPillars(level, sections, wallPieces.Count);
        foreach (var section in sections)
        {
            foreach (var part in Enum.GetValues<ShowroomPart>())
            {
                var first = pillars.First(pillar => pillar.Section == section.Index && pillar.Part == part).Pillar;
                labels.Add(new ShowroomLabel(PillarLabel(part), first.Position + new Vector3(0f, 2.4f, 0.6f)));
            }
        }

        var looks = Variants.Select(variant => new RoomLook(variant, variant, variant)).ToList();
        return new ShowroomLayout(
            level, doors, chests, SurfaceVariants.Of(looks, lookOfCell),
            new ShowroomExhibits(stairs, loot, consumables, weapons, items)
            {
                CrossbowSkeletons = skeletons,
                CryptWardens = wardens,
                WallPillars = [.. pillars.Select(pillar => pillar.Pillar)],
            },
            labels, sections, placed);
    }

    /// <summary>The label above the first wall pillar of <paramref name="part"/>.</summary>
    public static string PillarLabel(ShowroomPart part) => part == ShowroomPart.Corridor ? "Pillar 3.5 m" : "Pillar 6 m";

    /// <summary>
    /// The wall pillars (issue #536): against the north wall of every part, in the variant and under the ceiling of
    /// that part. In the corridor one between each two doors, in the room one every <see cref="WallStep"/> cells, keeping
    /// a cell of bare wall at each end.
    /// </summary>
    private static List<(int Section, ShowroomPart Part, ExhibitWallPillar Pillar)> WallPillars(
        Level level, IReadOnlyList<ShowroomSection> sections, int doorCount)
    {
        var pillars = new List<(int, ShowroomPart, ExhibitWallPillar)>();
        foreach (var section in sections)
        {
            foreach (var part in Enum.GetValues<ShowroomPart>())
            {
                var start = section.StartOf(part);
                var columns = part == ShowroomPart.Corridor
                    ? Enumerable.Range(0, Math.Max(doorCount - 1, 1)).Select(gap => start + 2 + (WallStep / 2) + (WallStep * gap))
                    : Enumerable.Range(0, ((section.Length - 4) / WallStep) + 1).Select(step => start + 2 + (WallStep * step));
                foreach (var x in columns)
                {
                    var (position, facing) = WallPillarLook.Place(level, x, section.NorthRow, RoomSide.North);
                    pillars.Add((section.Index, part, new ExhibitWallPillar(section.Variant, ShowroomSection.CeilingOf(part), position, facing)));
                }
            }
        }

        return pillars;
    }

    /// <summary>
    /// A new world of this showroom: a player who cannot die (the enemies are exhibits), with every weapon - the blade
    /// in the hand, the club in the bag, pistol, rifle and blunderbuss on 1, 2, 3 - looking east along section A. No
    /// rooms, so no arena ever seals and no boss fight starts.
    /// </summary>
    public GameWorld NewWorld(Balance? balance = null)
    {
        var world = new GameWorld(Level, 0, balance, Doors, null, Chests)
        {
            LevelName = Name,
            Surfaces = Surfaces,
            Showroom = Exhibits,
        };
        var player = world.Player;
        player.Equip(0, WeaponKind.Pistol);
        player.Equip(1, WeaponKind.Rifle);
        player.Equip(2, WeaponKind.Blunderbuss);
        player.Stow(WeaponKind.Club);
        PhotoStudio.Face(player, player.EyePosition + Vector3.UnitX);
        return world;
    }

    /// <summary>
    /// A new world with the player standing at the west end of <paramref name="part"/> of <paramref name="section"/> on
    /// its south walkway, looking across it at its north wall and pieces - the view of the showroom photos.
    /// </summary>
    public GameWorld ViewOf(int section, ShowroomPart part, Balance? balance = null)
    {
        var shown = Sections[section];
        var start = shown.StartOf(part);
        var target = Level.CellCenter(start + (shown.Length / 3), shown.DoorRow) + new Vector3(0f, 2f, Level.CellSize);
        return NewWorldAt(start, shown.SouthRowOf(part), target, balance);
    }

    /// <summary>
    /// A new world (<see cref="NewWorld"/>) with the player standing in cell (<paramref name="x"/>, <paramref name="z"/>),
    /// looking at <paramref name="lookAt"/> - for photos and tests of single pieces.
    /// </summary>
    public GameWorld NewWorldAt(int x, int z, Vector3 lookAt, Balance? balance = null)
    {
        var world = NewWorld(balance);
        world.Player.PlaceAt(Level.CellCenter(x, z));
        PhotoStudio.Face(world.Player, lookAt);
        return world;
    }

    private static float AddChest(List<ChestPlan> chests, int x, int z, int section, ChestTier tier)
    {
        chests.Add(new ChestPlan(x, z, (section * 2) + 1, tier, false, 5, false, false));
        return Chest.Height + 0.5f;
    }

    private static float SetStairs(ref Vector3? stairs, Vector3 position)
    {
        stairs = position;
        return Stairs.Height + 0.4f;
    }

    private static float AddSkeleton(List<ExhibitCrossbowSkeleton> skeletons, ExhibitCrossbowSkeleton skeleton)
    {
        skeletons.Add(skeleton);
        return CrossbowSkeletonLook.Height + 0.5f;
    }

    private static float AddWarden(List<ExhibitCryptWarden> wardens, ExhibitCryptWarden warden)
    {
        wardens.Add(warden);
        return CryptWardenLook.Height + 0.5f;
    }

    private static float Add<T>(List<T> list, T item)
    {
        list.Add(item);
        return 0.9f;
    }
}
