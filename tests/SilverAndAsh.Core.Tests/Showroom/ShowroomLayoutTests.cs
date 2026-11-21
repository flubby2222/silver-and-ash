using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.Showroom;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Showroom;

public class ShowroomLayoutTests
{
    private static readonly ShowroomLayout Layout = ShowroomLayout.Build();

    private static IEnumerable<PlacedPiece> FloorPieces(ShowroomLayout layout) => layout.Placed.Where(place => place.Piece is not WallPiece);

    // The wall pillars standing in `part` of `section`, west to east.
    private static List<ExhibitWallPillar> PillarsOf(ShowroomSection section, ShowroomPart part) => Layout.Exhibits.WallPillars
        .Where(pillar => pillar.Variant == section.Variant && pillar.CeilingHeight == ShowroomSection.CeilingOf(part))
        .OrderBy(pillar => pillar.Position.X)
        .ToList();

    // The ceiling arches standing in the arch corridor of `section`, west to east.
    private static List<ExhibitCeilingArch> ArchesOf(ShowroomSection section) => Layout.Exhibits.CeilingArches
        .Where(arch => arch.Variant == section.Variant)
        .OrderBy(arch => arch.Position.X)
        .ToList();

    // The wall niches of `size` and `form` (the flat ones of issue #538 unless named) set on the walls of `section`, west to east.
    private static List<ExhibitWallNiche> NichesOf(ShowroomSection section, WallNicheSize size, WallNicheForm form = WallNicheForm.Flat) =>
        Layout.Exhibits.WallNiches
        .Where(niche => niche.Variant == section.Variant && niche.Size == size && niche.Form == form)
        .OrderBy(niche => niche.Position.X)
        .ToList();

    // A player's position tick by tick, walking from the dead end of the arch corridor of `section` east (forward) and
    // `sideways` to the right, in a world of the showroom's cells with `exhibits`, until it is two cells into the room.
    // The player starts at the dead end: the same cells, only the start moved there.
    private static List<Vector3> Walk(ShowroomExhibits exhibits, ShowroomSection section, float sideways)
    {
        var cells = new Cell[Layout.Level.Width, Layout.Level.Depth];
        for (var x = 0; x < Layout.Level.Width; x++)
        {
            for (var z = 0; z < Layout.Level.Depth; z++)
            {
                cells[x, z] = Layout.Level[x, z];
            }
        }

        var level = new Level(cells, section.ArchCorridorX, section.ArchCorridorRow);
        var world = new GameWorld(level, 0, null, Layout.Doors, null, Layout.Chests)
        {
            Surfaces = Layout.Surfaces,
            Showroom = exhibits,
        };
        PhotoStudio.Face(world.Player, world.Player.EyePosition + Vector3.UnitX);

        var path = new List<Vector3>();
        var end = (section.RoomX + 2) * Level.CellSize;
        for (var tick = 0; tick < 1800 && world.Player.Position.X < end; tick++)
        {
            world.Tick(PlayerInput.None with { Move = new Vector2(sideways, 1f) });
            path.Add(world.Player.Position);
        }

        return path;
    }

    // Walkable cells reachable from the start; `blocked` cells count as wall.
    private static HashSet<(int X, int Z)> Reachable(Level level, ISet<(int X, int Z)>? blocked = null)
    {
        var start = (level.PlayerStartX, level.PlayerStartZ);
        var seen = new HashSet<(int X, int Z)> { start };
        var queue = new Queue<(int X, int Z)>([start]);
        while (queue.Count > 0)
        {
            var (x, z) = queue.Dequeue();
            foreach (var next in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
            {
                if (!level[next.Item1, next.Item2].IsSolid && blocked?.Contains(next) != true && seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return seen;
    }

    [Fact]
    public void Every_wall_piece_stands_once_per_section_in_the_corridor_part_and_every_other_piece_once()
    {
        foreach (var piece in ShowroomPieces.All)
        {
            var places = Layout.Placed.Where(place => ReferenceEquals(place.Piece, piece)).ToList();
            if (piece is WallPiece)
            {
                Assert.Equal(3, places.Count);
                Assert.Equal([0, 1, 2], places.Select(place => place.Section).Order());
                Assert.All(places, place => Assert.Equal(ShowroomPart.Corridor, place.Part));
            }
            else
            {
                Assert.Single(places);
            }
        }

        Assert.Equal(ShowroomPieces.All.OfType<WallPiece>().Count() * 3, Layout.Doors.Count);
        Assert.Equal(ShowroomPieces.All.OfType<ChestPiece>().Count(), Layout.Chests.Count);
    }

    [Fact]
    public void Every_kind_of_door_chest_enemy_and_weapon_of_the_game_is_shown()
    {
        var pieces = ShowroomPieces.All;
        Assert.Equal(Enum.GetValues<DoorKind>().Order(), pieces.OfType<WallPiece>().Select(piece => piece.Kind).Order());
        Assert.Equal(Enum.GetValues<ChestTier>().Order(), pieces.OfType<ChestPiece>().Select(piece => piece.Tier).Order());
        Assert.Equal(Enum.GetValues<ShowroomEnemy>().Order(), pieces.OfType<EnemyPiece>().Select(piece => piece.Enemy).Order());
        Assert.Equal(Enum.GetValues<WeaponKind>().Order(), pieces.OfType<WeaponPiece>().Select(piece => piece.Weapon).Order());
        Assert.Equal(Enum.GetValues<ConsumableKind>().Order(), pieces.OfType<ConsumablePiece>().Select(piece => piece.Consumable).Order());
        Assert.Single(pieces.OfType<StairsPiece>());

        Assert.Single(Layout.Level.GhoulPositions);
        Assert.Single(Layout.Level.BatPositions);
        Assert.Single(Layout.Level.BossPositions);
        Assert.NotNull(Layout.Exhibits.Stairs);
        Assert.Equal(Enum.GetValues<WeaponKind>().Length, Layout.Exhibits.Weapons.Count);
    }

    [Fact]
    public void The_crossbow_skeleton_stands_once_per_pose_where_its_piece_is_looking_south()
    {
        // Issue #497: a figure only, on the floor in the middle of its cell, looking at the south walkway.
        var places = Layout.Placed.Where(place => place.Piece is CrossbowSkeletonPiece).ToList();
        var exhibits = Layout.Exhibits.CrossbowSkeletons;

        Assert.Equal(Enum.GetValues<CrossbowSkeletonStance>().Order(), places.Select(place => ((CrossbowSkeletonPiece)place.Piece).Stance).Order());
        Assert.Equal(places.Count, exhibits.Count);
        foreach (var place in places)
        {
            var exhibit = Assert.Single(exhibits, exhibit => exhibit.Stance == ((CrossbowSkeletonPiece)place.Piece).Stance);
            Assert.Equal(Layout.Level.CellCenter(place.X, place.Z), exhibit.Position);
            Assert.Equal(Vector3.UnitZ, exhibit.Facing);
        }

        var label = Assert.Single(Layout.Labels, label => label.Text == "Crossbow skeleton (announce)");
        Assert.True(label.Position.Y > CrossbowSkeletonLook.Height);
        Assert.Empty(new ShowroomExhibits(null, [], [], [], []).CrossbowSkeletons);
    }

    [Fact]
    public void The_crypt_warden_stands_once_per_pose_where_its_piece_is_looking_south()
    {
        // Issue #530: a figure only, on the floor in the middle of its cell, looking at the south walkway.
        var places = Layout.Placed.Where(place => place.Piece is CryptWardenPiece).ToList();
        var exhibits = Layout.Exhibits.CryptWardens;

        Assert.Equal(Enum.GetValues<CryptWardenStance>().Order(), places.Select(place => ((CryptWardenPiece)place.Piece).Stance).Order());
        Assert.Equal(places.Count, exhibits.Count);
        foreach (var place in places)
        {
            var exhibit = Assert.Single(exhibits, exhibit => exhibit.Stance == ((CryptWardenPiece)place.Piece).Stance);
            Assert.Equal(Layout.Level.CellCenter(place.X, place.Z), exhibit.Position);
            Assert.Equal(Vector3.UnitZ, exhibit.Facing);
        }

        var label = Assert.Single(Layout.Labels, label => label.Text == "Crypt warden (announce)");
        Assert.True(label.Position.Y > CryptWardenLook.Height);
        Assert.Empty(new ShowroomExhibits(null, [], [], [], []).CryptWardens);
    }

    [Fact]
    public void Equipment_of_all_four_tiers_lies_in_the_showroom_and_can_be_taken()
    {
        // Issue #317: an item of each tier besides the weapons (Worn and Forged only so far), all taken with F.
        var pieces = ShowroomPieces.All;
        Assert.Equal(Enum.GetValues<ItemTier>().Order(), pieces.OfType<ItemPiece>().Select(piece => Items.Info(piece.Item).Tier).Order());
        Assert.Equal(pieces.OfType<ItemPiece>().Count(), Layout.Exhibits.Items.Count);

        var world = Layout.NewWorld();
        Assert.Equal(Enum.GetValues<ItemTier>().Order(), world.EquipmentDrops.Select(drop => drop.Piece.Tier).Distinct().Order());
        Assert.All(world.EquipmentDrops, drop =>
        {
            var offer = drop.OfferFor(world.Player);
            Assert.True(offer.IsPossible);
            Assert.Equal($"Take {drop.Piece.Name} ({drop.Piece.Tier})", offer.Text);
            Assert.Equal(drop.Piece.Tier, offer.Tier);
        });
    }

    [Fact]
    public void Three_sections_show_the_variants_A_B_and_C_at_corridor_and_room_height()
    {
        Assert.Equal([TextureVariant.A, TextureVariant.B, TextureVariant.C], Layout.Sections.Select(section => section.Variant));
        foreach (var section in Layout.Sections)
        {
            foreach (var part in Enum.GetValues<ShowroomPart>())
            {
                var start = section.StartOf(part);
                for (var x = start; x < start + section.Length; x++)
                {
                    for (var z = section.NorthRow; z <= section.SouthRowOf(part); z++)
                    {
                        Assert.False(Layout.Level[x, z].IsSolid);
                        Assert.Equal(ShowroomSection.CeilingOf(part), Layout.Level[x, z].CeilingHeight);
                        foreach (var kind in Enum.GetValues<SurfaceKind>())
                        {
                            Assert.Equal(section.Variant, Layout.Surfaces.At(x, z, kind));
                        }
                    }
                }
            }
        }

        Assert.Equal(Cell.DefaultCeilingHeight, ShowroomSection.CeilingOf(ShowroomPart.Corridor));
        Assert.Equal(RoomTemplate.RoomCeilingHeight, ShowroomSection.CeilingOf(ShowroomPart.Room));
    }

    [Fact]
    public void Doors_sit_in_the_north_wall_of_their_part_with_a_closet_behind_in_the_same_variant()
    {
        foreach (var place in Layout.Placed.Where(place => place.Piece is WallPiece))
        {
            var section = Layout.Sections[place.Section];
            var door = Layout.Doors.Single(door => door.X == place.X && door.Z == place.Z);
            Assert.Equal(((WallPiece)place.Piece).Kind, door.Kind);
            Assert.Equal(RoomSide.North, door.Side);
            Assert.Equal(section.DoorRow, door.Z);
            Assert.InRange(door.X, section.StartOf(place.Part), section.StartOf(place.Part) + section.Length - 1);

            // Wall either side of the doorway, the closet behind it.
            Assert.True(Layout.Level[door.X - 1, door.Z].IsSolid);
            Assert.True(Layout.Level[door.X + 1, door.Z].IsSolid);
            Assert.False(Layout.Level[door.X, door.Z - 1].IsSolid);
            Assert.Equal(section.Variant, Layout.Surfaces.At(door.X, door.Z - 1, SurfaceKind.Wall));
            Assert.Equal(section.Variant, Layout.Surfaces.At(door.X, door.Z, SurfaceKind.Wall));

            // As in the game, where every door cell - the boss gate's too - has the corridor ceiling.
            Assert.Equal(Cell.DefaultCeilingHeight, Layout.Level[door.X, door.Z].CeilingHeight);
        }
    }

    [Fact]
    public void Walking_along_a_corridor_one_passes_every_door_within_lantern_reach()
    {
        // The lantern is the only light and the fog starts a few metres out (issue #302): from the middle row of the
        // corridor, every door in its north wall is at most two cells away, and the corridor is narrower than the room.
        foreach (var place in Layout.Placed.Where(place => place.Piece is WallPiece))
        {
            var section = Layout.Sections[place.Section];
            var walkway = Layout.Level.CellCenter(place.X, section.MiddleRowOf(ShowroomPart.Corridor));
            Assert.True(Vector3.Distance(walkway, Layout.Level.CellCenter(place.X, place.Z)) <= 2 * Level.CellSize);
            Assert.True(Layout.Level[place.X, section.SouthRowOf(ShowroomPart.Corridor) + 1].IsSolid);
        }

        Assert.All(Layout.Sections, section =>
            Assert.True(section.SouthRowOf(ShowroomPart.Corridor) < section.SouthRowOf(ShowroomPart.Room)));
    }

    [Fact]
    public void Every_locked_door_has_a_key_on_the_floor_in_front_of_it()
    {
        var keys = Layout.Exhibits.Loot.Where(loot => loot.Kind == LootKind.Key).ToList();
        var locked = Layout.Doors.Where(door => door.Kind == DoorKind.Locked).ToList();

        Assert.Equal(locked.Count, keys.Count);
        foreach (var door in locked)
        {
            var front = Layout.Level.CellCenter(door.X, door.Z + 1);
            Assert.Contains(keys, key => Vector3.Distance(key.Position, front) <= Level.CellSize + 0.01f);
        }
    }

    [Fact]
    public void No_two_pieces_overlap()
    {
        var cells = Layout.Placed.Select(place => (place.X, place.Z))
            .Concat(Layout.Exhibits.Loot.Where(loot => loot.Kind == LootKind.Key).Select(loot => Level.ToGrid(loot.Position.X, loot.Position.Z)))
            .ToList();
        Assert.Equal(cells.Count, cells.Distinct().Count());

        // Floor pieces stand on the floor of a room part, well apart: the widest (the stairs, 1.2 m, and Gorm, 0.9 m)
        // would still leave room to walk between them.
        var floor = FloorPieces(Layout).ToList();
        foreach (var place in floor)
        {
            Assert.Equal(ShowroomPart.Room, place.Part);
            Assert.False(Layout.Level[place.X, place.Z].IsSolid);
            Assert.DoesNotContain(Layout.Doors, door => door.X == place.X && door.Z == place.Z);
            foreach (var other in floor.Where(other => !ReferenceEquals(other, place)))
            {
                var metres = Vector3.Distance(Layout.Level.CellCenter(place.X, place.Z), Layout.Level.CellCenter(other.X, other.Z));
                Assert.True(metres >= 3 * Level.CellSize, $"{place.Piece.Label} and {other.Piece.Label} are {metres} m apart.");
            }
        }
    }

    [Fact]
    public void Every_piece_and_every_closet_can_be_reached_from_the_start()
    {
        // With all doors open: every door cell and the closet behind it.
        var open = Reachable(Layout.Level);
        foreach (var door in Layout.Doors)
        {
            Assert.Contains((door.X, door.Z), open);
            Assert.Contains((door.X, door.Z - 1), open);
        }

        // With all doors closed: every floor piece, every key and the walkway in front of every door - nothing needs
        // a door to be opened first.
        var closed = Reachable(Layout.NewWorld().Level);
        foreach (var place in FloorPieces(Layout))
        {
            Assert.Contains((place.X, place.Z), closed);
        }

        foreach (var key in Layout.Exhibits.Loot)
        {
            Assert.Contains(Level.ToGrid(key.Position.X, key.Position.Z), closed);
        }

        foreach (var door in Layout.Doors)
        {
            Assert.Contains((door.X, door.Z + 1), closed);
            Assert.DoesNotContain((door.X, door.Z - 1), closed);
        }
    }

    [Fact]
    public void The_sections_form_a_loop()
    {
        // Wall off the step from the corridor part to the room part of one section: both parts can still be reached,
        // the long way round. So every section lies on a round trip.
        foreach (var section in Layout.Sections)
        {
            var wall = Enumerable.Range(section.NorthRow, section.SouthRow - section.NorthRow + 1)
                .Select(z => (section.RoomX, z))
                .ToHashSet();
            var reachable = Reachable(Layout.NewWorld().Level, wall);

            Assert.Contains((section.CorridorX + 1, section.SouthRowOf(ShowroomPart.Corridor)), reachable);
            Assert.Contains((section.RoomX + 1, section.SouthRow), reachable);
        }
    }

    [Fact]
    public void The_same_list_always_gives_the_same_showroom()
    {
        var again = ShowroomLayout.Build();

        for (var x = 0; x < Layout.Level.Width; x++)
        {
            for (var z = 0; z < Layout.Level.Depth; z++)
            {
                Assert.Equal(Layout.Level[x, z], again.Level[x, z]);
            }
        }

        Assert.Equal(Layout.Doors, again.Doors);
        Assert.Equal(Layout.Chests, again.Chests);
        Assert.Equal(Layout.Placed, again.Placed);
        Assert.Equal(Layout.Labels, again.Labels);
        Assert.Equal(Layout.Exhibits.Loot, again.Exhibits.Loot);
        Assert.Equal(Layout.Exhibits.Weapons, again.Exhibits.Weapons);
        Assert.Equal(Layout.Exhibits.Consumables, again.Exhibits.Consumables);
        Assert.Equal(Layout.Exhibits.WallPillars, again.Exhibits.WallPillars);
        Assert.Equal(Layout.Exhibits.CeilingArches, again.Exhibits.CeilingArches);
        Assert.Equal(Layout.Exhibits.WallNiches, again.Exhibits.WallNiches);
    }

    [Fact]
    public void A_sequence_of_three_ceiling_arches_spans_the_arch_corridor_of_every_section_in_its_variant()
    {
        foreach (var section in Layout.Sections)
        {
            var arches = ArchesOf(section);
            var row = section.ArchCorridorRow;
            Assert.Equal(3, arches.Count);
            Assert.All(arches, arch => Assert.Equal(section.Variant, arch.Variant));
            Assert.NotNull(CeilingArchLook.FileName(section.Variant));

            // On the grid: through the middle of a corridor cell, looking along it, one every 4 m as in the probe, the
            // first a cell in from the corridor's mouth.
            Assert.Equal([.. ShowroomLayout.ArchColumns(section)], arches.Select(arch => Level.ToGrid(arch.Position.X, arch.Position.Z).X).Reverse());
            Assert.All(arches, arch => Assert.Equal(row, Level.ToGrid(arch.Position.X, arch.Position.Z).Z));
            Assert.Equal([4f, 4f], arches.Zip(arches.Skip(1), (west, east) => east.Position.X - west.Position.X));
            Assert.All(arches, arch =>
            {
                var (x, z) = Level.ToGrid(arch.Position.X, arch.Position.Z);
                Assert.Equal(CeilingArchLook.Place(Layout.Level, x, z), (arch.Position, arch.Facing));
                Assert.Equal(Vector3.UnitX, arch.Facing);
            });
            Assert.InRange(ShowroomLayout.ArchColumns(section).Min(), section.ArchCorridorX + 1, section.RoomX - 2);

            // The corridor: one cell wide (2 m) under the corridor ceiling in the section's variant, walls along both
            // sides from its dead end in the west to its mouth into the room - and no doorway in them.
            var doorCells = Layout.Doors.Select(door => (door.X, door.Z)).ToHashSet();
            Assert.True(Layout.Level[section.ArchCorridorX - 1, row].IsSolid);
            for (var x = section.ArchCorridorX; x < section.RoomX; x++)
            {
                Assert.False(Layout.Level[x, row].IsSolid);
                Assert.Equal(CeilingArchLook.CeilingHeight, Layout.Level[x, row].CeilingHeight);
                Assert.Equal(section.Variant, Layout.Surfaces.At(x, row, SurfaceKind.Wall));
                foreach (var side in new[] { row - 1, row + 1 })
                {
                    Assert.True(Layout.Level[x, side].IsSolid, $"Arch corridor of {section.Variant}: no wall at ({x}, {side}).");
                    Assert.DoesNotContain((x, side), doorCells);
                }
            }

            Assert.False(Layout.Level[section.RoomX, row].IsSolid);
            Assert.Contains((section.ArchCorridorX, row), Reachable(Layout.NewWorld().Level));
            Assert.Contains(Layout.Labels, label => label.Text == ShowroomLayout.ArchLabel
                && Level.ToGrid(label.Position.X, label.Position.Z) == (section.RoomX - 1, row));
        }
    }

    [Theory]
    [InlineData(-0.5f)]
    [InlineData(0f)]
    [InlineData(0.5f)]
    public void A_ceiling_arch_is_only_decoration_one_walks_the_same_path_under_it_as_without_it(float sideways)
    {
        // Tick by tick from the dead end of every arch corridor east into the room: along the north wall (sideways -0.5,
        // to the left), down the middle (0) and along the south wall (+0.5) - under every arch and past its pillars. The
        // same walk in a world without the arches takes the same steps.
        var without = Layout.Exhibits with { CeilingArches = [] };
        foreach (var section in Layout.Sections)
        {
            var arches = ArchesOf(section);
            var withArches = Walk(Layout.Exhibits, section, sideways);
            var withoutArches = Walk(without, section, sideways);
            Assert.Equal(withoutArches, withArches);

            // The walk really passed under all three arches and out of the corridor, and along a wall it really kept to
            // that wall: a pillar with a body would push it off a pillar's depth, an arch with one would stop it.
            Assert.True(withArches[^1].X > section.RoomX * Level.CellSize, $"Section {section.Variant}: the walk ended at {withArches[^1]}.");
            // Along a wall the player's middle keeps its radius off the wall's face; down the middle, the corridor's middle.
            var lane = arches[0].Position.Z + (MathF.Sign(sideways) * ((CeilingArchLook.Width / 2f) - BodySizes.Player.Radius));
            foreach (var arch in arches)
            {
                var under = withArches.Where(step => MathF.Abs(step.X - arch.Position.X) < 0.3f).ToList();
                Assert.NotEmpty(under);
                Assert.All(under, step => Assert.InRange(step.Z, lane - 0.05f, lane + 0.05f));
            }
        }
    }

    [Fact]
    public void Wall_pillars_stand_at_every_wall_variant_at_corridor_and_room_height()
    {
        foreach (var section in Layout.Sections)
        {
            foreach (var part in Enum.GetValues<ShowroomPart>())
            {
                var ceiling = ShowroomSection.CeilingOf(part);
                var pillars = PillarsOf(section, part);
                Assert.True(pillars.Count >= 2, $"Section {section.Variant}, {part}: only {pillars.Count} pillar(s).");
                Assert.All(pillars, pillar =>
                {
                    Assert.Equal(section.Variant, pillar.Variant);
                    Assert.Equal(ceiling, pillar.CeilingHeight);
                    Assert.NotNull(WallPillarLook.FileName(pillar.Variant, pillar.CeilingHeight));
                });
                Assert.Contains(Layout.Labels, label => label.Text == ShowroomLayout.PillarLabel(part)
                    && Level.ToGrid(label.Position.X, label.Position.Z) == Level.ToGrid(pillars[0].Position.X, pillars[0].Position.Z + 0.01f));
            }
        }
    }

    [Fact]
    public void Every_wall_pillar_stands_in_the_grid_against_the_bare_north_wall_of_its_part()
    {
        var doorCells = Layout.Doors.Select(door => (door.X, door.Z)).ToHashSet();
        foreach (var pillar in Layout.Exhibits.WallPillars)
        {
            // Placed on the grid: in the middle of a cell's north wall, on its floor, looking south into the cell. The
            // middle of a cell's wall always lies on a whole metre, so the pillar's texture runs on with the wall's.
            var section = Layout.Sections.Single(section => section.Variant == pillar.Variant);
            var part = pillar.CeilingHeight == ShowroomSection.CeilingOf(ShowroomPart.Corridor) ? ShowroomPart.Corridor : ShowroomPart.Room;
            var (x, z) = Level.ToGrid(pillar.Position.X, pillar.Position.Z + 0.01f);
            Assert.Equal(section.NorthRow, z);
            Assert.InRange(x, section.StartOf(part) + 1, section.StartOf(part) + section.Length - 2);
            Assert.Equal(WallPillarLook.Place(Layout.Level, x, z, RoomSide.North), (pillar.Position, pillar.Facing));
            Assert.Equal(MathF.Round(pillar.Position.X), pillar.Position.X);

            // The cell in front is open floor under the pillar's ceiling; the wall behind it is solid and no doorway,
            // and neither is any cell beside it - the pillar never stands in front of a door.
            Assert.False(Layout.Level[x, z].IsSolid);
            Assert.Equal(pillar.CeilingHeight, Layout.Level[x, z].CeilingHeight);
            foreach (var beside in new[] { x - 1, x, x + 1 })
            {
                Assert.True(Layout.Level[beside, z - 1].IsSolid, $"Pillar at ({x}, {z}): no wall behind it at {beside}.");
                Assert.DoesNotContain((beside, z - 1), doorCells);
            }
        }

        var cells = Layout.Exhibits.WallPillars.Select(pillar => (pillar.Position.X, pillar.Position.Z)).ToList();
        Assert.Equal(cells.Count, cells.Distinct().Count());
    }

    [Fact]
    public void A_wall_pillar_is_only_decoration_one_walks_right_up_to_the_wall_behind_it()
    {
        // No body, no collision: walking north at a pillar ends at the wall, not in front of the pillar.
        foreach (var pillar in Layout.Exhibits.WallPillars)
        {
            var (x, z) = Level.ToGrid(pillar.Position.X, pillar.Position.Z + 0.01f);
            var world = Layout.NewWorldAt(x, z + 1, pillar.Position + new Vector3(0f, 1.5f, 0f));
            for (var tick = 0; tick < 120; tick++)
            {
                world.Tick(PlayerInput.None with { Move = new Vector2(0f, 1f) });
            }

            // The wall stops the player at its radius; a pillar with a body would stop it a pillar's depth earlier.
            Assert.InRange(world.Player.Position.Z, pillar.Position.Z, pillar.Position.Z + BodySizes.Player.Radius + 0.05f);
        }
    }

    [Fact]
    public void Wall_niches_stand_at_every_wall_variant_in_both_sizes()
    {
        foreach (var section in Layout.Sections)
        {
            foreach (var (part, size) in new[] { (ShowroomPart.Corridor, WallNicheSize.Corridor), (ShowroomPart.Room, WallNicheSize.Room) })
            {
                var niches = NichesOf(section, size);
                Assert.True(niches.Count >= 2, $"Section {section.Variant}, {part}: only {niches.Count} niche(s).");
                Assert.All(niches, niche => Assert.NotNull(WallNicheLook.FileName(niche.Size, niche.Variant)));
                Assert.Equal([.. ShowroomLayout.NicheColumns(section, part)], niches.Select(niche => Level.ToGrid(niche.Position.X, niche.Position.Z - 0.01f).X));
                Assert.Contains(Layout.Labels, label => label.Text == ShowroomLayout.NicheLabel(size)
                    && Level.ToGrid(label.Position.X, label.Position.Z) == Level.ToGrid(niches[0].Position.X, niches[0].Position.Z - 0.01f));
            }
        }
    }

    [Fact]
    public void Deep_niches_with_and_without_shelf_stand_at_every_wall_variant_in_both_sizes()
    {
        foreach (var section in Layout.Sections)
        {
            foreach (var (part, size) in new[] { (ShowroomPart.Corridor, WallNicheSize.Corridor), (ShowroomPart.Room, WallNicheSize.Room) })
            {
                // Between the flat niches, taking turns: without shelf, with shelf, without ...
                var deep = Layout.Exhibits.WallNiches
                    .Where(niche => niche.Variant == section.Variant && niche.Size == size && niche.Form != WallNicheForm.Flat)
                    .OrderBy(niche => niche.Position.X)
                    .ToList();
                Assert.Equal([.. ShowroomLayout.DeepNicheColumns(section, part)], deep.Select(niche => Level.ToGrid(niche.Position.X, niche.Position.Z - 0.01f).X));
                Assert.Equal(
                    deep.Select((_, index) => index % 2 == 0 ? WallNicheForm.Deep : WallNicheForm.DeepWithShelf),
                    deep.Select(niche => niche.Form));

                foreach (var form in new[] { WallNicheForm.Deep, WallNicheForm.DeepWithShelf })
                {
                    var first = NichesOf(section, size, form);
                    Assert.NotEmpty(first);
                    Assert.Contains(Layout.Labels, label => label.Text == ShowroomLayout.DeepNicheLabel(form)
                        && Level.ToGrid(label.Position.X, label.Position.Z) == Level.ToGrid(first[0].Position.X, first[0].Position.Z - 0.01f));
                }
            }
        }
    }

    [Fact]
    public void Every_deep_niche_and_only_a_deep_niche_leaves_its_hole_in_the_wall()
    {
        var holes = Layout.Exhibits.WallNiches
            .Select(niche =>
            {
                var (x, z) = Level.ToGrid(niche.Position.X, niche.Position.Z - 0.01f);
                return WallNicheLook.Cutout(niche.Form, x, z, RoomSide.South);
            })
            .OfType<WallCutout>()
            .ToHashSet();

        Assert.NotEmpty(holes);
        Assert.Equal(holes, Layout.Level.WallCutouts.ToHashSet());
        Assert.Equal(holes.Count, Layout.Level.WallCutouts.Count);

        // The holes reach the picture of the world the showroom starts (drawn from the level as built, doors open) and
        // the level that blocks (doors closed) alike.
        var world = Layout.NewWorld();
        Assert.Equal(Layout.Level.WallCutouts, world.LevelAsBuilt.WallCutouts);
        Assert.Equal(Layout.Level.WallCutouts, world.Level.WallCutouts);
    }

    [Fact]
    public void Every_wall_niche_stands_in_the_grid_on_the_bare_south_wall_of_its_part()
    {
        var doorCells = Layout.Doors.Select(door => (door.X, door.Z)).ToHashSet();
        var pieceCells = FloorPieces(Layout).Select(place => (place.X, place.Z)).ToHashSet();
        foreach (var niche in Layout.Exhibits.WallNiches)
        {
            // Placed by the rule: in the middle of a cell's south wall, on its floor, looking north into the cell, the
            // size taken from the cell's clear height.
            var section = Layout.Sections.Single(section => section.Variant == niche.Variant);
            var part = niche.Size == WallNicheSize.Corridor ? ShowroomPart.Corridor : ShowroomPart.Room;
            var (x, z) = Level.ToGrid(niche.Position.X, niche.Position.Z - 0.01f);
            Assert.Equal(section.SouthRowOf(part), z);
            Assert.InRange(x, section.StartOf(part) + 1, section.StartOf(part) + section.Length - 2);
            Assert.Equal(WallNicheLook.Place(Layout.Level, x, z, RoomSide.South), (niche.Position, niche.Facing, niche.Size));
            Assert.Equal(-Vector3.UnitZ, niche.Facing);
            var cell = Layout.Level[x, z];
            Assert.Equal(ShowroomSection.CeilingOf(part), cell.CeilingHeight - cell.FloorHeight);

            // No door behind it or beside it, and no piece on the floor in front of it or beside it to hide it.
            foreach (var beside in new[] { x - 1, x, x + 1 })
            {
                Assert.DoesNotContain((beside, z + 1), doorCells);
                Assert.DoesNotContain((beside, z), pieceCells);
            }
        }

        var cells = Layout.Exhibits.WallNiches.Select(niche => (niche.Position.X, niche.Position.Z)).ToList();
        Assert.Equal(cells.Count, cells.Distinct().Count());
    }

    [Fact]
    public void A_wall_niche_is_only_decoration_one_walks_right_up_to_the_wall_behind_it()
    {
        // No body, no collision: walking south at a niche, tick by tick, ends at the wall, not in front of the niche.
        foreach (var niche in Layout.Exhibits.WallNiches)
        {
            var (x, z) = Level.ToGrid(niche.Position.X, niche.Position.Z - 0.01f);
            var world = Layout.NewWorldAt(x, z - 1, niche.Position + new Vector3(0f, 1.5f, 0f));
            for (var tick = 0; tick < 120; tick++)
            {
                world.Tick(PlayerInput.None with { Move = new Vector2(0f, 1f) });
            }

            // The wall stops the player at its radius; a niche with a body would stop it earlier. Nothing here depends on
            // how far the model stands out, so a model set back into the wall keeps this rule.
            Assert.InRange(world.Player.Position.Z, niche.Position.Z - BodySizes.Player.Radius - 0.05f, niche.Position.Z);
        }
    }

    [Fact]
    public void One_portcullis_per_section_stands_in_the_north_wall_of_the_room_part_with_a_closet_behind()
    {
        Assert.Equal(Layout.Sections.Count, Layout.Exhibits.Portcullises.Count);
        foreach (var section in Layout.Sections)
        {
            var portcullis = Assert.Single(Layout.Exhibits.Portcullises, portcullis => portcullis.Variant == section.Variant);
            var (x, z) = (portcullis.X, portcullis.Z);
            Assert.Equal(ShowroomLayout.PortcullisCell(section), (x, z));
            Assert.Equal(RoomSide.North, portcullis.Side);
            Assert.Equal(Layout.Level.CellCenter(x, z), portcullis.Position);
            Assert.Equal(section.DoorRow, z);
            Assert.InRange(x, section.RoomX + 1, section.RoomX + section.Length - 2);

            // Wall either side of the doorway; in front the 6 m room, behind a closet of corridor height in the same
            // variant - so the photos show it from the room and from the 3.5 m side.
            Assert.True(Layout.Level[x - 1, z].IsSolid && Layout.Level[x + 1, z].IsSolid);
            Assert.Equal(RoomTemplate.RoomCeilingHeight, Layout.Level[x, z + 1].CeilingHeight);
            Assert.Equal(Cell.DefaultCeilingHeight, Layout.Level[x, z].CeilingHeight);
            Assert.Equal(Cell.DefaultCeilingHeight, Layout.Level[x, z - 1].CeilingHeight);
            Assert.Equal(section.Variant, Layout.Surfaces.At(x, z, SurfaceKind.Wall));
            Assert.Equal(section.Variant, Layout.Surfaces.At(x, z - 1, SurfaceKind.Wall));

            // No door of the corridor part and no wall pillar beside it.
            Assert.DoesNotContain(Layout.Doors, door => door.X == x && door.Z == z);
            Assert.All(PillarsOf(section, ShowroomPart.Room), pillar =>
                Assert.True(MathF.Abs(pillar.Position.X - portcullis.Position.X) >= 2 * Level.CellSize));
            Assert.Contains(Layout.Labels, label => label.Text == ShowroomLayout.PortcullisLabel
                && Level.ToGrid(label.Position.X, label.Position.Z) == (x, z + 1));
        }
    }

    [Fact]
    public void For_now_one_walks_through_the_portcullis_into_its_closet()
    {
        // Part 1 of issue #540 is only the picture: the doorway stays open, nothing blocks it (part 2 brings that).
        var open = Reachable(Layout.NewWorld().Level);
        foreach (var portcullis in Layout.Exhibits.Portcullises)
        {
            Assert.Contains((portcullis.X, portcullis.Z - 1), open);
            var world = Layout.NewWorldAt(portcullis.X, portcullis.Z + 1, portcullis.Position - new Vector3(0f, -1.5f, Level.CellSize));
            for (var tick = 0; tick < 120; tick++)
            {
                world.Tick(PlayerInput.None with { Move = new Vector2(0f, 1f) });
            }

            // Through the doorway and into the closet (its two rows lie north of it).
            var (x, z) = Level.ToGrid(world.Player.Position.X, world.Player.Position.Z);
            Assert.Equal(portcullis.X, x);
            Assert.True(z < portcullis.Z, $"The player stopped in row {z}, not behind the portcullis in row {portcullis.Z}.");
        }
    }

    [Fact]
    public void Every_piece_and_every_part_has_a_label()
    {
        foreach (var place in Layout.Placed)
        {
            Assert.Contains(Layout.Labels, label => label.Text == place.Piece.Label
                && Level.ToGrid(label.Position.X, label.Position.Z).X == place.X);
        }

        Assert.Equal(6, Layout.Labels.Count(label => label.Text.StartsWith("Wall ", StringComparison.Ordinal)));
        Assert.Equal(Layout.Doors.Count(door => door.Kind == DoorKind.Locked), Layout.Labels.Count(label => label.Text == "Key"));
    }

    [Fact]
    public void A_new_piece_is_one_more_entry_in_the_list()
    {
        var extra = new ChestPiece("Another chest", ChestTier.Guard);
        var layout = ShowroomLayout.Build([.. ShowroomPieces.All, extra]);

        Assert.Single(layout.Placed, place => ReferenceEquals(place.Piece, extra));
        Assert.Equal(Layout.Chests.Count + 1, layout.Chests.Count);
        Assert.Contains(layout.Labels, label => label.Text == "Another chest");
    }

    [Fact]
    public void A_second_gorm_is_refused()
    {
        Assert.Throws<ArgumentException>(() => ShowroomLayout.Build(
            [.. ShowroomPieces.All, new EnemyPiece("Another Gorm", ShowroomEnemy.Gorm)]));
    }

    [Fact]
    public void The_player_has_every_weapon()
    {
        var player = Layout.NewWorld().Player;

        Assert.Equal(WeaponKind.SilverBlade, player.Melee);
        Assert.Equal([WeaponKind.Pistol, WeaponKind.Rifle, WeaponKind.Blunderbuss], player.RangedSlots.Select(weapon => weapon!.Kind));
        Assert.Contains(WeaponKind.Club, player.Bag);
    }

    [Fact]
    public void The_enemies_are_harmless_exhibits_and_nobody_dies()
    {
        // Stand next to each enemy for ten seconds, striking and shooting at it all the time.
        foreach (var place in Layout.Placed.Where(place => place.Piece is EnemyPiece))
        {
            var aim = Layout.Level.CellCenter(place.X, place.Z) + new Vector3(0f, 1.2f, 0f);
            var world = Layout.NewWorldAt(place.X - 1, place.Z, aim);
            var enemies = world.Targets.ToList();
            var before = enemies.Select(enemy => (enemy.Body.Position, enemy.IsHittable)).ToList();
            var health = world.Player.Health;
            for (var tick = 0; tick < 600; tick++)
            {
                world.Tick(PlayerInput.None with { Melee = tick % 20 < 10, Shoot = tick % 2 == 0, Reload = tick % 120 == 119 });
                Assert.DoesNotContain(world.Events, happened => happened is RoomSealed or BossFightStarted or RunLost);
            }

            Assert.Equal(before, enemies.Select(enemy => (enemy.Body.Position, enemy.IsHittable)).ToList());
            Assert.Equal(health, world.Player.Health);
            Assert.Equal(world.Balance.Ghoul.MaxHealth, world.Ghouls.Single().Health);
            Assert.False(world.Player.IsDead);
            Assert.False(world.IsBossFightOn);
            Assert.Empty(world.Rooms);
        }
    }

    [Fact]
    public void The_stairs_stand_but_cannot_be_climbed()
    {
        var place = Layout.Placed.Single(place => place.Piece is StairsPiece);
        var world = Layout.NewWorldAt(place.X - 1, place.Z, Layout.Level.CellCenter(place.X, place.Z) + new Vector3(0f, 1f, 0f));
        var stairs = world.Stairs!;
        Assert.Equal(1f, stairs.RiseFraction);

        for (var tick = 0; tick < 60; tick++)
        {
            world.Tick(PlayerInput.None with { Interact = true });
            Assert.IsNotType<Stairs>(world.InteractionTarget(world.Player));
        }

        Assert.False(world.IsWon);
    }

    [Fact]
    public void The_key_next_to_a_locked_door_opens_it()
    {
        var locked = Layout.Doors.First(door => door.Kind == DoorKind.Locked);
        var doorway = Layout.Level.CellCenter(locked.X, locked.Z) + new Vector3(0f, 1.5f, 0f);

        // The key lies one cell east of the walkway in front of the door: one step onto it, then turn to the door.
        var world = Layout.NewWorldAt(locked.X + 1, locked.Z + 1, doorway);
        world.Tick(PlayerInput.None);
        Assert.Equal(1, world.Player.Keys);

        var door = world.Doors.Single(door => door.X == locked.X && door.Z == locked.Z);
        for (var tick = 0; tick < 60 && !door.IsOpen; tick++)
        {
            world.Tick(PlayerInput.None with { Move = new Vector2(0f, 1f), Interact = tick % 2 == 0 });
        }

        Assert.True(door.IsOpen);
        Assert.Equal(0, world.Player.Keys);
    }

    [Fact]
    public void A_normal_world_is_no_showroom()
    {
        var world = new GameWorld(Layout.Level, 0, null, Layout.Doors, null, Layout.Chests);

        Assert.False(world.IsShowroom);
        Assert.Null(world.Stairs);
        Assert.Empty(world.Loot);
        Assert.Empty(world.WeaponDrops);
        Assert.Empty(world.EquipmentDrops);
        Assert.True(Layout.NewWorld().IsShowroom);
    }
}
