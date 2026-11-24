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

    /// <summary>
    /// The row of the arch corridor (issue #537): one cell wide under the corridor ceiling, south of the corridor part
    /// with a wall between, from <see cref="ArchCorridorX"/> east to the room part, where it opens into the room's
    /// south-west corner. Ceiling arches span it (<see cref="ShowroomLayout.ArchColumns"/>).
    /// </summary>
    public int ArchCorridorRow => SouthRow;

    /// <summary>The first (west) column of the arch corridor; it is a dead end there.</summary>
    public int ArchCorridorX => CorridorX + 1;

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
/// (issue #536); ceiling arches span a corridor one cell wide south of every corridor part (issue #537). South of the
/// passage along the bottom of the band lies a hall with a free-standing column under each ceiling height (issue #539). Built
/// from <see cref="ShowroomPieces.All"/> with a fixed rule - the same list always gives the same showroom.
/// </summary>
public sealed partial class ShowroomLayout
{
    /// <summary>Name shown briefly at the start.</summary>
    public const string Name = "THE SHOWROOM";

    /// <summary>The label at the mouth of every arch corridor (issue #537).</summary>
    public const string ArchLabel = "Ceiling arch";

    // Number of ceiling arches in a sequence and the cells from one to the next.
    private const int ArchCount = 3;
    private const int ArchStep = 2;

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
    /// Builds the showroom of <paramref name="pieces"/> (<see cref="ShowroomPieces.All"/> if null): first the grid of
    /// the sections, passages, doors and closets, then every piece where it stands (<see cref="Placers"/>), then the
    /// building blocks in their order (<see cref="Fixtures"/>).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// If the list has more than one Gorm (a world has at most one) or a piece no entry of <see cref="Placers"/> takes.
    /// </exception>
    public static ShowroomLayout Build(IReadOnlyList<ShowroomPiece>? pieces = null)
    {
        pieces ??= ShowroomPieces.All;
        if (pieces.OfType<EnemyPiece>().Count(piece => piece.Enemy == ShowroomEnemy.Gorm) > 1)
        {
            throw new ArgumentException("The showroom can show at most one Gorm.", nameof(pieces));
        }

        var draft = new Draft(pieces);
        draft.CarveSections();
        draft.CarvePassages();
        draft.PlaceWallPieces();
        draft.CarvePortcullisClosets();
        draft.PlaceFloorPieces();
        foreach (var place in draft.Placed)
        {
            draft.Labels.Add(PlacerOf(place.Piece, nameof(pieces))(draft, place));
        }

        draft.LayKeys();
        foreach (var fixture in Fixtures)
        {
            fixture(draft);
        }

        return draft.Finish();
    }

    /// <summary>
    /// Kind of piece -> how it stands in the showroom: what it adds to the draft and the label it gets. The first entry
    /// whose kind the piece is takes it. A new kind of piece is one more entry here.
    /// </summary>
    private static readonly (Type Kind, Func<Draft, PlacedPiece, ShowroomLabel> Place)[] Placers =
    [
        // A door's label hangs on the wall above it, on the side of the walkway.
        (typeof(WallPiece), static (_, place) => new ShowroomLabel(
            place.Piece.Label, new Vector3((place.X + 0.5f) * Level.CellSize, 3.1f, ((place.Z + 1) * Level.CellSize) + 0.3f))),
        Placer<ChestPiece>(static (draft, chest, place) => AddChest(draft.Chests, place.X, place.Z, place.Section, chest.Tier)),
        Placer<StairsPiece>(static (draft, _, place) => SetStairs(draft, draft.At(place))),
        Placer<EnemyPiece>(static (_, enemy, _) => EnemyKindOf(enemy.Enemy).LabelHeight),
        Placer<CrossbowSkeletonPiece>(static (draft, skeleton, place) =>
            AddSkeleton(draft.Skeletons, new ExhibitCrossbowSkeleton(skeleton.Stance, draft.At(place), Vector3.UnitZ))),
        Placer<CryptWardenPiece>(static (draft, warden, place) =>
            AddWarden(draft.Wardens, new ExhibitCryptWarden(warden.Stance, draft.At(place), Vector3.UnitZ))),
        Placer<WeaponPiece>(static (draft, weapon, place) => Add(draft.Weapons, new ExhibitWeapon(weapon.Weapon, draft.At(place)))),
        Placer<ItemPiece>(static (draft, item, place) => Add(draft.Items, new ExhibitItem(item.Item, draft.At(place)))),
        Placer<LootPiece>(static (draft, pile, place) => Add(draft.Loot, new ExhibitLoot(pile.Loot, pile.Amount, draft.At(place)))),
        Placer<ConsumablePiece>(static (draft, consumable, place) =>
            Add(draft.Consumables, new ExhibitConsumable(consumable.Consumable, draft.At(place)))),
    ];

    /// <summary>
    /// The building blocks of the showroom beyond its pieces, in the order they add their exhibits and labels: the labels
    /// of the parts, wall pillars, ceiling arches, wall niches, portcullises, deep niches, the hall's columns and the
    /// breaking column. A new building block is one more entry here.
    /// </summary>
    private static readonly Action<Draft>[] Fixtures =
    [
        AddPartLabels,
        AddWallPillars,
        AddCeilingArches,
        AddWallNiches,
        AddPortcullises,
        AddDeepWallNiches,
        AddColumns,
        AddBreakingColumns,
    ];

    /// <summary>
    /// Kind of enemy -> its slot in the level (ghouls, bats, bosses) and the height of its label. An enemy missing here
    /// stands as the last one, the boss.
    /// </summary>
    private static readonly (ShowroomEnemy Enemy, int Slot, float LabelHeight)[] EnemyKinds =
    [
        (ShowroomEnemy.Ghoul, 0, BodySizes.Ghoul.Height + 0.5f),
        (ShowroomEnemy.Bat, 1, BodySizes.Bat.HoverHeight + 1f),
        (ShowroomEnemy.Gorm, 2, BodySizes.Gorm.Height + 0.6f),
    ];

    private static (ShowroomEnemy Enemy, int Slot, float LabelHeight) EnemyKindOf(ShowroomEnemy enemy)
    {
        foreach (var kind in EnemyKinds)
        {
            if (kind.Enemy == enemy)
            {
                return kind;
            }
        }

        return EnemyKinds[^1];
    }

    // The entry of Placers for pieces of kind T: `place` adds it to the draft and gives the height of its label above its cell.
    private static (Type Kind, Func<Draft, PlacedPiece, ShowroomLabel> Place) Placer<T>(Func<Draft, T, PlacedPiece, float> place)
        where T : ShowroomPiece =>
        (typeof(T), (draft, placed) => new ShowroomLabel(placed.Piece.Label, draft.Above(placed.X, placed.Z, place(draft, (T)placed.Piece, placed))));

    // The entry of Placers that takes `piece`; `parameter` names the list it came in.
    private static Func<Draft, PlacedPiece, ShowroomLabel> PlacerOf(ShowroomPiece piece, string parameter)
    {
        foreach (var (kind, place) in Placers)
        {
            if (kind.IsInstanceOfType(piece))
            {
                return place;
            }
        }

        throw new ArgumentException($"The showroom cannot place {piece.GetType().Name}.", parameter);
    }

    private static void AddPartLabels(Draft draft)
    {
        foreach (var section in draft.Sections)
        {
            foreach (var part in Enum.GetValues<ShowroomPart>())
            {
                var text = part == ShowroomPart.Corridor ? "corridor 3.5 m" : "room 6 m";
                draft.Labels.Add(new ShowroomLabel($"Wall {section.Variant} - {text}", draft.Above(section.StartOf(part), section.MiddleRowOf(part), 2.6f)));
            }
        }
    }

    private static void AddWallPillars(Draft draft)
    {
        draft.Pillars = WallPillars(draft.Level, draft.Sections, draft.WallPieces.Count);
        draft.Labels.AddRange(PillarLabels(draft.Sections, draft.Pillars));
    }

    private static void AddCeilingArches(Draft draft)
    {
        draft.Arches = CeilingArches(draft.Level, draft.Sections);
        draft.Labels.AddRange(draft.Sections.Select(section => new ShowroomLabel(ArchLabel, draft.Above(section.RoomX - 1, section.ArchCorridorRow, 2.6f))));
    }

    private static void AddWallNiches(Draft draft)
    {
        draft.Niches.AddRange(WallNiches(draft.Level, draft.Sections));
        draft.Labels.AddRange(NicheLabels(draft.Niches));
    }

    private static void AddPortcullises(Draft draft)
    {
        draft.Portcullises = Portcullises(draft.Level, draft.Sections);
        draft.Labels.AddRange(PortcullisLabels(draft.Portcullises));
    }

    // The deep niches join the flat ones and cut their holes into the level's walls.
    private static void AddDeepWallNiches(Draft draft)
    {
        var deepNiches = DeepWallNiches(draft.Level, draft.Sections);
        draft.Niches.AddRange(deepNiches.Select(deep => deep.Niche));
        draft.Labels.AddRange(DeepNicheLabels(deepNiches));
        draft.Labels.AddRange(FillingLabels(deepNiches));
        draft.Change(level => level.WithWallCutouts(deepNiches.Select(deep => deep.Cutout)));
    }

    private static void AddColumns(Draft draft)
    {
        draft.Change(level => level.WithColumns(Enum.GetValues<ShowroomPart>().Select(part => ColumnOf(part, draft.Bottom))));
        draft.Labels.AddRange(Enum.GetValues<ShowroomPart>().Select(part => ColumnLabelOf(part, draft.Bottom)));
    }

    private static void AddBreakingColumns(Draft draft)
    {
        draft.Columns = BreakingColumns(draft.Bottom);
        draft.Labels.AddRange(draft.Columns.Select(BreakingColumnLabel));
    }

    // The column hall (issue #539): south of the passage along the bottom of the band and open to it along its whole
    // length, HallRows deep. Its west half has the corridor ceiling, its east half the room's, HallLength cells each.
    private const int HallRows = 5;
    private const int HallLength = 5;
    private const int HallX = BandX + 2;

    /// <summary>The label in front of the column of <paramref name="part"/>'s height (issue #539).</summary>
    public static string ColumnLabel(ShowroomPart part) => part == ShowroomPart.Corridor ? "Column 3.5 m" : "Column 6 m";

    // The rectangle of the hall's half under the ceiling of `part`; `bottom` is the first row of the south passage.
    private static (int X0, int Z0, int X1, int Z1) HallOf(ShowroomPart part, int bottom)
    {
        var x0 = HallX + (part == ShowroomPart.Corridor ? 0 : HallLength);
        var z0 = bottom + PassageWidth;
        return (x0, z0, x0 + HallLength - 1, z0 + HallRows - 1);
    }

    // The free-standing column of the hall's half under the ceiling of `part`: in the middle of its middle cell, so it
    // leaves every figure room on all sides (ColumnPlacement).
    private static Column ColumnOf(ShowroomPart part, int bottom)
    {
        var (x0, z0, x1, z1) = HallOf(part, bottom);
        return Column.AtCell((x0 + x1) / 2, (z0 + z1) / 2);
    }

    // On the side of the passage, where one comes from, below the corridor ceiling.
    private static ShowroomLabel ColumnLabelOf(ShowroomPart part, int bottom)
    {
        var column = ColumnOf(part, bottom);
        return new ShowroomLabel(ColumnLabel(part), new Vector3(column.X, 2.6f, column.Z - Column.Radius - 0.6f));
    }

    /// <summary>The label in front of a breaking column of the hall in <paramref name="stage"/> (issue #601).</summary>
    public static string BreakingColumnLabel(ColumnStage stage) => stage switch
    {
        ColumnStage.Intact => "Intact",
        ColumnStage.Hit1 => "1 blow",
        ColumnStage.Hit2 => "2 blows",
        ColumnStage.Broken => "Broken",
        _ => "Breaking",
    };

    // The breaking column (issue #601) in the hall's room half: its four stages along the back row, the side of the blows
    // towards the passage one comes from, and one breaking in a loop in the front row, falling east into the free cells.
    // Only pictures (ExhibitColumn): the level's column in the middle stays the only real one.
    private static List<ExhibitColumn> BreakingColumns(int bottom)
    {
        var (x0, z0, _, z1) = HallOf(ShowroomPart.Room, bottom);
        ColumnStage[] stages = [ColumnStage.Intact, ColumnStage.Hit1, ColumnStage.Hit2, ColumnStage.Broken];
        int[] cells = [x0, x0 + 1, x0 + 3, x0 + 4];
        var columns = new List<ExhibitColumn>();
        for (var i = 0; i < stages.Length; i++)
        {
            var spot = Column.AtCell(cells[i], z1);
            var passage = new Vector3(spot.X, 0f, spot.Z - 10f);
            columns.Add(new ExhibitColumn(stages[i], new Vector3(spot.X, 0f, spot.Z), ColumnBreakLook.YawToward(spot.Centre, passage)));
        }

        var looping = Column.AtCell(x0 + 2, z0);
        columns.Add(new ExhibitColumn(ColumnStage.Falling, new Vector3(looping.X, 0f, looping.Z), ColumnBreakLook.YawToward(looping.Centre, new Vector3(looping.X + 10f, 0f, looping.Z))));
        return columns;
    }

    // On the side of the passage, low enough to stay under the hall's ceiling.
    private static ShowroomLabel BreakingColumnLabel(ExhibitColumn column) =>
        new(BreakingColumnLabel(column.Stage), column.Position + new Vector3(0f, 2.6f, -Column.Radius - 0.6f));

    /// <summary>
    /// The door cell of the portcullis of <paramref name="section"/> (issue #540): in the north wall of the room part,
    /// between its first two wall pillars.
    /// </summary>
    public static (int X, int Z) PortcullisCell(ShowroomSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        return (section.RoomX + 2 + (WallStep / 2), section.DoorRow);
    }

    /// <summary>The label above the lintel of every portcullis, on the side of the room.</summary>
    public const string PortcullisLabel = "Portcullis";

    // Above the lintel (3.5 m), so the falling grate does not run through the label.
    private const float PortcullisLabelHeight = 4.1f;

    // One portcullis per section in the north wall of the room part (issue #540), a closet of corridor height behind
    // it: from the front the 6 m room, from the back the 3.5 m closet. Each rectangle with the section whose look it takes.
    private static IEnumerable<(int X0, int Z0, int X1, int Z1, int Section)> PortcullisCarvings(IReadOnlyList<ShowroomSection> sections)
    {
        foreach (var section in sections)
        {
            var (x, z) = PortcullisCell(section);
            yield return (x, z, x, z, section.Index);
            yield return (x - 1, z - DoorRowOffset + ClosetRow, x + 1, z - 1, section.Index);
        }
    }

    private static List<ExhibitPortcullis> Portcullises(Level level, IReadOnlyList<ShowroomSection> sections) =>
        [.. sections.Select(section =>
        {
            var (x, z) = PortcullisCell(section);
            return new ExhibitPortcullis(section.Variant, x, z, RoomSide.North, level.CellCenter(x, z));
        })];

    // Above the lintel, on the side of the room.
    private static IEnumerable<ShowroomLabel> PortcullisLabels(List<ExhibitPortcullis> portcullises) =>
        portcullises.Select(portcullis => new ShowroomLabel(
            PortcullisLabel, new Vector3(portcullis.Position.X, PortcullisLabelHeight, ((portcullis.Z + 1) * Level.CellSize) + 0.3f)));

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
    /// The columns of the ceiling arches in the arch corridor of <paramref name="section"/>, east to west: a sequence of
    /// <see cref="ArchCount"/>, one every <see cref="ArchStep"/> cells (4 m, as in the probe), the first a cell into the
    /// corridor from its mouth.
    /// </summary>
    public static IEnumerable<int> ArchColumns(ShowroomSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        return Enumerable.Range(0, ArchCount).Select(step => section.RoomX - 3 - (ArchStep * step));
    }

    /// <summary>
    /// The ceiling arches (issue #537): a sequence across the arch corridor of every section, in its variant, placed
    /// on the grid by <see cref="CeilingArchLook.Place"/>.
    /// </summary>
    private static List<ExhibitCeilingArch> CeilingArches(Level level, IReadOnlyList<ShowroomSection> sections)
    {
        var arches = new List<ExhibitCeilingArch>();
        foreach (var section in sections)
        {
            foreach (var x in ArchColumns(section))
            {
                var (position, facing) = CeilingArchLook.Place(level, x, section.ArchCorridorRow);
                arches.Add(new ExhibitCeilingArch(section.Variant, position, facing));
            }
        }

        return arches;
    }

    /// <summary>The label above the first wall niche of <paramref name="size"/> in every section.</summary>
    public static string NicheLabel(WallNicheSize size) => size == WallNicheSize.Corridor ? "Niche 3.5 m" : "Niche 6 m";

    /// <summary>
    /// The columns of the wall niches in <paramref name="part"/> of <paramref name="section"/>, west to east: one every
    /// <see cref="WallStep"/> cells, keeping a cell of bare wall at each end - away from the part's ends, where the
    /// passages, the arch corridor's mouth and the other part open the south wall.
    /// </summary>
    public static IEnumerable<int> NicheColumns(ShowroomSection section, ShowroomPart part)
    {
        ArgumentNullException.ThrowIfNull(section);
        var start = section.StartOf(part);
        return Enumerable.Range(0, ((section.Length - 4) / WallStep) + 1).Select(step => start + 2 + (WallStep * step));
    }

    /// <summary>
    /// The wall niches (issue #538): on the south wall of every part, in the variant of that part. The north walls carry
    /// the doors and the wall pillars; a niche must stand neither beside a door nor behind a pillar, so the niches take
    /// the bare wall opposite. <see cref="WallNicheLook.Place"/> checks each place on the grid and picks the size from the
    /// clear height.
    /// </summary>
    private static List<ExhibitWallNiche> WallNiches(Level level, IReadOnlyList<ShowroomSection> sections)
    {
        var niches = new List<ExhibitWallNiche>();
        foreach (var section in sections)
        {
            foreach (var part in Enum.GetValues<ShowroomPart>())
            {
                foreach (var x in NicheColumns(section, part))
                {
                    var (position, facing, size) = WallNicheLook.Place(level, x, section.SouthRowOf(part), RoomSide.South);
                    niches.Add(new ExhibitWallNiche(section.Variant, size, position, facing));
                }
            }
        }

        return niches;
    }

    /// <summary>One label per size and section, in front of and above the westmost niche of that size.</summary>
    private static IEnumerable<ShowroomLabel> NicheLabels(List<ExhibitWallNiche> niches) =>
        niches
            .GroupBy(niche => (niche.Variant, niche.Size))
            .Select(group => group.OrderBy(niche => niche.Position.X).First())
            .Select(first => new ShowroomLabel(NicheLabel(first.Size), first.Position + new Vector3(0f, 2.6f, 0f) + (first.Facing * 0.6f)));

    /// <summary>The label above the first deep wall niche of <paramref name="form"/> in every part (issue #553).</summary>
    public static string DeepNicheLabel(WallNicheForm form) => form == WallNicheForm.DeepWithShelf ? "Deep niche, shelf" : "Deep niche";

    /// <summary>
    /// The columns of the deep wall niches in <paramref name="part"/> of <paramref name="section"/>, west to east: one in
    /// every gap between two flat niches (<see cref="NicheColumns"/>), so each stands between bare wall cells.
    /// </summary>
    public static IEnumerable<int> DeepNicheColumns(ShowroomSection section, ShowroomPart part) =>
        NicheColumns(section, part).SkipLast(1).Select(x => x + (WallStep / 2));

    /// <summary>
    /// The deep wall niches (issue #553): on the south wall of every part between the flat niches, taking turns without
    /// and with a shelf, each with the hole it leaves in the wall face.
    /// </summary>
    private static List<(ExhibitWallNiche Niche, WallCutout Cutout)> DeepWallNiches(Level level, IReadOnlyList<ShowroomSection> sections)
    {
        WallNicheForm[] forms = [WallNicheForm.Deep, WallNicheForm.DeepWithShelf];
        var niches = new List<(ExhibitWallNiche, WallCutout)>();
        foreach (var section in sections)
        {
            foreach (var part in Enum.GetValues<ShowroomPart>())
            {
                var z = section.SouthRowOf(part);
                foreach (var (x, index) in DeepNicheColumns(section, part).Select((x, index) => (x, index)))
                {
                    var form = forms[index % forms.Length];
                    var filling = DeepNicheFilling(part, index);
                    if (!WallNicheFillingLook.Fits(form, filling))
                    {
                        throw new InvalidOperationException($"Deep niche {index} of the {part} takes no {filling}.");
                    }

                    var (position, facing, size) = WallNicheLook.Place(level, x, z, RoomSide.South);
                    var cutout = WallNicheLook.Cutout(form, x, z, RoomSide.South)!.Value;
                    niches.Add((new ExhibitWallNiche(section.Variant, size, position, facing) { Form = form, Filling = filling }, cutout));
                }
            }
        }

        return niches;
    }

    // Part -> what stands in each of its deep niches, west to east (issue #553). The niches take turns without and with a
    // shelf, so the second and fourth have one: candles and skulls need it, bottles lie on the sill of either. Each part
    // keeps empty niches; the room shows the bottles under a shelf and an empty shelf. A new filling is one more entry here.
    private static readonly (ShowroomPart Part, WallNicheFilling[] Fillings)[] DeepNicheFillings =
    [
        (ShowroomPart.Corridor,
            [WallNicheFilling.None, WallNicheFilling.Candles, WallNicheFilling.Bottles, WallNicheFilling.Skulls, WallNicheFilling.None]),
        (ShowroomPart.Room,
            [WallNicheFilling.None, WallNicheFilling.Bottles, WallNicheFilling.Bottles, WallNicheFilling.None, WallNicheFilling.None]),
    ];

    /// <summary>
    /// What stands in the deep niche <paramref name="index"/> (west to east, <see cref="DeepNicheColumns"/>) of
    /// <paramref name="part"/>: nothing beyond the table.
    /// </summary>
    public static WallNicheFilling DeepNicheFilling(ShowroomPart part, int index)
    {
        foreach (var (each, fillings) in DeepNicheFillings)
        {
            if (each == part)
            {
                return index >= 0 && index < fillings.Length ? fillings[index] : WallNicheFilling.None;
            }
        }

        return WallNicheFilling.None;
    }

    /// <summary>The label in front of every deep niche with <paramref name="filling"/> (issue #553), or null for an empty one.</summary>
    public static string? FillingLabel(WallNicheFilling filling) => filling switch
    {
        WallNicheFilling.None => null,
        _ => filling.ToString(),
    };

    // Low in front of the sill, below what the niche holds, so it hides neither the filling nor the label above the niche.
    private const float FillingLabelHeight = 0.25f;

    // One label in front of every deep niche with a filling.
    private static IEnumerable<ShowroomLabel> FillingLabels(List<(ExhibitWallNiche Niche, WallCutout Cutout)> niches) =>
        niches
            .Select(deep => deep.Niche)
            .Where(niche => FillingLabel(niche.Filling) is not null)
            .Select(niche => new ShowroomLabel(
                FillingLabel(niche.Filling)!, niche.Position + new Vector3(0f, FillingLabelHeight, 0f) + (niche.Facing * 0.6f)));

    /// <summary>One label per form, size and section, in front of and above the westmost deep niche of that form.</summary>
    private static IEnumerable<ShowroomLabel> DeepNicheLabels(List<(ExhibitWallNiche Niche, WallCutout Cutout)> niches) =>
        niches
            .Select(deep => deep.Niche)
            .GroupBy(niche => (niche.Variant, niche.Size, niche.Form))
            .Select(group => group.OrderBy(niche => niche.Position.X).First())
            .Select(first => new ShowroomLabel(DeepNicheLabel(first.Form), first.Position + new Vector3(0f, 2.6f, 0f) + (first.Facing * 0.6f)));

    /// <summary>One label per part of every section, above the first wall pillar of that part.</summary>
    private static IEnumerable<ShowroomLabel> PillarLabels(
        IReadOnlyList<ShowroomSection> sections, List<(int Section, ShowroomPart Part, ExhibitWallPillar Pillar)> pillars)
    {
        foreach (var section in sections)
        {
            foreach (var part in Enum.GetValues<ShowroomPart>())
            {
                var first = pillars.First(pillar => pillar.Section == section.Index && pillar.Part == part).Pillar;
                yield return new ShowroomLabel(PillarLabel(part), first.Position + new Vector3(0f, 2.4f, 0.6f));
            }
        }
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
        ArmFully(world.Player);
        PhotoStudio.Face(world.Player, world.Player.EyePosition + Vector3.UnitX);
        return world;
    }

    /// <summary>
    /// Gives <paramref name="player"/> every weapon: the blade stays in the hand, the club goes in the bag, pistol, rifle
    /// and blunderbuss on 1, 2, 3 - as in the showroom and on the test course (issue #718).
    /// </summary>
    public static void ArmFully(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.Equip(0, WeaponKind.Pistol);
        player.Equip(1, WeaponKind.Rifle);
        player.Equip(2, WeaponKind.Blunderbuss);
        player.Stow(WeaponKind.Club);
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

    private static float SetStairs(Draft draft, Vector3 position)
    {
        draft.Stairs = position;
        return Stairs.Height - 0.4f;                                        // the staircase reaches the ceiling: its label hangs just under it
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
