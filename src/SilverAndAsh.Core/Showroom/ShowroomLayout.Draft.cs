using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Showroom;

public sealed partial class ShowroomLayout
{
    /// <summary>
    /// The showroom while <see cref="Build"/> puts it together (issue #588): the cell grid and what the building blocks
    /// add to it, step by step. One per build, never shared - the steps of <see cref="Build"/>, <see cref="Placers"/>
    /// and <see cref="Fixtures"/> fill it in turn, <see cref="Finish"/> makes the showroom of it.
    /// </summary>
    private sealed class Draft
    {
        private readonly Cell[,] cells;
        private readonly Dictionary<(int X, int Z), int> lookOfCell = [];
        private readonly List<(int X, int Z)> keys = [];

        // The enemies on their cells, one list per slot of the level (EnemyKinds).
        private readonly List<(int X, int Z)>[] enemies = [[], [], []];
        private Level? level;

        public Draft(IReadOnlyList<ShowroomPiece> pieces)
        {
            WallPieces = [.. pieces.OfType<WallPiece>()];
            FloorPieces = [.. pieces.Where(piece => piece is not WallPiece)];
            var perRoom = (FloorPieces.Count + Variants.Length - 1) / Variants.Length;

            // Each part is as long as the corridor needs for its doors and the room for its share of the floor pieces,
            // with a walkway at each end.
            Length = Math.Max(Math.Max(WallStep * WallPieces.Count, (FloorStep * perRoom) + 1), 6);
            East = BandX + (2 * Length) - 1;
            Bottom = Variants.Length * SectionRows;
            var width = East + PassageWidth + 2;
            var depth = Bottom + PassageWidth + HallRows + 1;
            cells = new Cell[width, depth];
            for (var x = 0; x < width; x++)
            {
                for (var z = 0; z < depth; z++)
                {
                    cells[x, z] = Cell.Wall;
                }
            }
        }

        public List<WallPiece> WallPieces { get; }

        public List<ShowroomPiece> FloorPieces { get; }

        /// <summary>The cells of each part, west to east.</summary>
        public int Length { get; }

        /// <summary>The east end of the band: the last column of the room parts.</summary>
        public int East { get; }

        /// <summary>The first row of the passage along the bottom of the band.</summary>
        public int Bottom { get; }

        /// <summary>The level, once the grid is carved (<see cref="PlaceFloorPieces"/>).</summary>
        public Level Level => level ?? throw new InvalidOperationException("The showroom's level is not made yet.");

        public List<ShowroomSection> Sections { get; } = [];

        public List<LevelDoor> Doors { get; } = [];

        public List<PlacedPiece> Placed { get; } = [];

        public List<ShowroomLabel> Labels { get; } = [];

        public List<ChestPlan> Chests { get; } = [];

        public Vector3? Stairs { get; set; }

        public List<ExhibitLoot> Loot { get; } = [];

        public List<ExhibitConsumable> Consumables { get; } = [];

        public List<ExhibitWeapon> Weapons { get; } = [];

        public List<ExhibitItem> Items { get; } = [];

        public List<ExhibitCrossbowSkeleton> Skeletons { get; } = [];

        public List<ExhibitCryptWarden> Wardens { get; } = [];

        public List<(int Section, ShowroomPart Part, ExhibitWallPillar Pillar)> Pillars { get; set; } = [];

        public List<ExhibitCeilingArch> Arches { get; set; } = [];

        public List<ExhibitWallNiche> Niches { get; set; } = [];

        public List<ExhibitPortcullis> Portcullises { get; set; } = [];

        public List<ExhibitColumn> Columns { get; set; } = [];

        /// <summary>Makes the rectangle (x0, z0)-(x1, z1) floor under <paramref name="ceiling"/>, in the look of section <paramref name="look"/>.</summary>
        public void Carve(int x0, int z0, int x1, int z1, float ceiling, int? look)
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

        /// <summary>The sections, each with both parts and its arch corridor.</summary>
        public void CarveSections()
        {
            for (var index = 0; index < Variants.Length; index++)
            {
                var top = index * SectionRows;
                var section = new ShowroomSection(
                    index, Variants[index], BandX, BandX + Length, Length, top + DoorRowOffset, top + DoorRowOffset + 1, top + DoorRowOffset + FloorRows);
                Sections.Add(section);
                foreach (var part in Enum.GetValues<ShowroomPart>())
                {
                    var start = section.StartOf(part);
                    Carve(start, section.NorthRow, start + Length - 1, section.SouthRowOf(part), ShowroomSection.CeilingOf(part), index);
                }

                Carve(section.ArchCorridorX, section.ArchCorridorRow, section.RoomX - 1, section.ArchCorridorRow, Cell.DefaultCeilingHeight, index);
            }
        }

        /// <summary>
        /// The passages that join the sections into a loop: A's west end to the way back, A to B in the east, B to C in
        /// the west, C in the east down to the way back along the south and up the west side. Walls stand between. South
        /// of the way back the column hall.
        /// </summary>
        public void CarvePassages()
        {
            var a = Sections[0];
            var b = Sections[1];
            var c = Sections[2];
            var corridor = Cell.DefaultCeilingHeight;
            Carve(1, a.NorthRow, BandX - 1, a.SouthRow, corridor, null);
            Carve(1, a.NorthRow, PassageWidth, Bottom + PassageWidth - 1, corridor, null);
            Carve(1, Bottom, East + PassageWidth, Bottom + PassageWidth - 1, corridor, null);
            Carve(East + 1, a.NorthRow, East + PassageWidth, b.SouthRow, corridor, null);
            Carve(BandX - PassageWidth, b.NorthRow, BandX - 1, c.SouthRow, corridor, null);
            Carve(East + 1, c.NorthRow, East + PassageWidth, Bottom + PassageWidth - 1, corridor, null);
            foreach (var part in Enum.GetValues<ShowroomPart>())
            {
                var (x0, z0, x1, z1) = HallOf(part, Bottom);
                Carve(x0, z0, x1, z1, ShowroomSection.CeilingOf(part), null);
            }
        }

        /// <summary>
        /// Every wall piece once per section in the north wall of the part it stands in (<see cref="PartOf"/>), a closet
        /// behind; a key before each locked door. A door is as many cells wide as in the game
        /// (<see cref="DoorFrameLook.CellsWide"/>, the further cells as <see cref="LevelDoor.MoreCells"/>, as
        /// <c>LevelGenerator</c> widens the boss door), its cells and closet under the ceiling of a door cell of its kind
        /// (<see cref="LevelGenerator.DoorCell"/>) - so the boss door is the portal over two 6 m cells (issue #599).
        /// </summary>
        public void PlaceWallPieces()
        {
            var closets = 0;
            foreach (var section in Sections)
            {
                var slots = new Dictionary<ShowroomPart, int>();
                foreach (var piece in WallPieces)
                {
                    var part = PartOf(piece.Kind);
                    var slot = slots.GetValueOrDefault(part);
                    slots[part] = slot + 1;
                    var x = WallSlotColumn(section, part, slot);
                    var z = section.DoorRow;
                    var wide = DoorFrameLook.CellsWide(piece.Kind);
                    var ceiling = LevelGenerator.DoorCell(piece.Kind).CeilingHeight;
                    Carve(x, z, x + wide - 1, z, ceiling, section.Index);
                    Carve(x - 1, z - DoorRowOffset + ClosetRow, x + wide, z - 1, ceiling, section.Index);
                    Doors.Add(new LevelDoor(x, z, section.Index * 2, (Variants.Length * 2) + closets++, RoomSide.North, piece.Kind)
                    {
                        MoreCells = [.. Enumerable.Range(x + 1, wide - 1).Select(more => (more, z))],
                    });
                    Placed.Add(new PlacedPiece(piece, x, z, section.Index, part));
                    if (piece.Kind == DoorKind.Locked)
                    {
                        keys.Add((x + 1, section.NorthRow));
                    }
                }
            }
        }

        /// <summary>The door cell and closet of every portcullis.</summary>
        public void CarvePortcullisClosets()
        {
            foreach (var (x0, z0, x1, z1, section) in PortcullisCarvings(Sections))
            {
                Carve(x0, z0, x1, z1, Cell.DefaultCeilingHeight, section);
            }
        }

        /// <summary>
        /// Floor pieces take turns between the room parts of the sections, in the middle row, west to east. The enemies
        /// among them go into the level, which is made here: the grid is carved.
        /// </summary>
        public void PlaceFloorPieces()
        {
            for (var index = 0; index < FloorPieces.Count; index++)
            {
                var section = Sections[index % Sections.Count];
                var x = section.RoomX + 2 + (FloorStep * (index / Sections.Count));
                Placed.Add(new PlacedPiece(FloorPieces[index], x, section.MiddleRowOf(ShowroomPart.Room), section.Index, ShowroomPart.Room));
            }

            foreach (var place in Placed.Where(place => place.Piece is EnemyPiece))
            {
                enemies[EnemyKindOf(((EnemyPiece)place.Piece).Enemy).Slot].Add((place.X, place.Z));
            }

            var start = Sections[0];
            level = new Level(cells, start.CorridorX + 1, start.MiddleRowOf(ShowroomPart.Corridor), null, enemies[0], enemies[1], enemies[2]);
        }

        /// <summary>The key before every locked door, with its label.</summary>
        public void LayKeys()
        {
            foreach (var (x, z) in keys)
            {
                Loot.Add(new ExhibitLoot(LootKind.Key, 1, Level.CellCenter(x, z)));
                Labels.Add(new ShowroomLabel("Key", Above(x, z, 0.9f)));
            }
        }

        /// <summary>The middle of cell (<paramref name="x"/>, <paramref name="z"/>), <paramref name="height"/> metres up.</summary>
        public Vector3 Above(int x, int z, float height) => Level.CellCenter(x, z) + new Vector3(0f, height, 0f);

        /// <summary>The middle of the cell <paramref name="place"/> stands on, on the floor.</summary>
        public Vector3 At(PlacedPiece place) => Level.CellCenter(place.X, place.Z);

        /// <summary>Changes the level (wall cutouts, columns) after it is made.</summary>
        public void Change(Func<Level, Level> change) => level = change(Level);

        public ShowroomLayout Finish()
        {
            var looks = Variants.Select(variant => new RoomLook(variant, variant, variant)).ToList();
            return new ShowroomLayout(
                Level, Doors, Chests, SurfaceVariants.Of(looks, lookOfCell),
                new ShowroomExhibits(Stairs, Loot, Consumables, Weapons, Items)
                {
                    CrossbowSkeletons = Skeletons,
                    CryptWardens = Wardens,
                    WallPillars = [.. Pillars.Select(pillar => pillar.Pillar)],
                    CeilingArches = Arches,
                    WallNiches = Niches,
                    Portcullises = Portcullises,
                    Columns = Columns,
                },
                Labels, Sections, Placed);
        }
    }
}
