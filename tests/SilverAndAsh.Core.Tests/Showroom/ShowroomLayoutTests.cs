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
            Assert.InRange(world.Player.Position.Z, pillar.Position.Z, pillar.Position.Z + BodySizes.Player.Radius + 0.05f);        }
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
