using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>Chests and Gorm give equipment from the loot pool (decision records 0006 and 0008, issue #318).</summary>
public class ChestAndBossLootTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };

    /// <summary>
    /// Worlds per statistical test. With the tolerance of 5 standard deviations of a binomial share
    /// (5 * sqrt(p(1-p)/n)) this gives at most +-0.11 at p = 0.5 - narrower than the gap between neighbouring chest
    /// tiers (at least 0.2), so a tier with the wrong chance fails. The seeds are fixed (0 .. n-1), so the test never
    /// flickers; the wide tolerance only keeps it from depending on how many numbers a tick happens to use.
    /// </summary>
    private const int Worlds = 500;

    /// <summary>Nothing else drops: all loot comes from the chest or Gorm.</summary>
    private static readonly Balance NoOtherLoot = Balance.Default with
    {
        Drops = Balance.Default.Drops with { GhoulChance = 0f, BatChance = 0f },
        Silver = Balance.Default.Silver with { GhoulChance = 0f, BatChance = 0f },
        Keys = new KeysBalance { RoomChance = 0f, EnemyChance = 0f },
    };

    /// <summary>The chest of <paramref name="tier"/> with its equipment chance set to 1 - it always holds a piece (or extra silver).</summary>
    private static Balance AlwaysEquipment(ChestTier tier) => NoOtherLoot with
    {
        Chests = tier switch
        {
            ChestTier.Beggar => NoOtherLoot.Chests with { Beggar = NoOtherLoot.Chests.Beggar with { EquipmentChance = 1f } },
            ChestTier.Guard => NoOtherLoot.Chests with { Guard = NoOtherLoot.Chests.Guard with { EquipmentChance = 1f } },
            ChestTier.King => NoOtherLoot.Chests with { King = NoOtherLoot.Chests.King with { EquipmentChance = 1f } },
            _ => NoOtherLoot.Chests with { Vampire = NoOtherLoot.Chests.Vampire with { EquipmentChance = 1f } },
        },
    };

    // A corridor along +Z: the player in row 0, the chest in row 3.
    private const string ChestMap = "#P#\n#.#\n#.#\n#.#\n#.#";

    private static GameWorld ChestWorld(int seed, ChestTier tier, Balance? balance = null, int silver = 25) =>
        new(LevelParser.Parse(ChestMap), seed, balance ?? NoOtherLoot, chests: [new ChestPlan(1, 3, 0, tier, false, silver, false, false)]);

    /// <summary>Walks up to the chest, looks down at it and opens it with F; returns the events of opening.</summary>
    private static List<GameEvent> OpenChest(GameWorld world)
    {
        var events = WalkUpAndPressF(world);
        Assert.True(world.Chests[0].IsOpen, "The chest never opened - the test would prove nothing.");
        return events;
    }

    /// <summary>Walks up to the chest, looks down at it and presses F - or, without <paramref name="press"/>, does not.</summary>
    private static List<GameEvent> WalkUpAndPressF(GameWorld world, bool press = true)
    {
        var player = world.Player;
        for (var tick = 0; player.Position.Z < 5f; tick++)
        {
            Assert.True(tick < 3 * Second, "The player never got close to the chest.");
            world.Tick(Forward);
        }

        Run(world, PlayerInput.None, Second / 2);
        player.Look(PlayerInput.None with { Look = new Vector2(0f, 35f * MathF.PI / 180f / ControlSettings.MouseSensitivity) });
        return Run(world, press ? PressF : PlayerInput.None, 1);
    }

    /// <summary>
    /// Room 0 (start) in row 0, door in row 1, room 1 (the boss room) in rows 2-11 with Gorm ('T') in
    /// <paramref name="gormRow"/>, column 3. The staircase rises in cell (3, 6). <paramref name="below"/> adds rows
    /// after the hall, outside every room.
    /// </summary>
    private static string BossMap(int gormRow = 10, string below = "") =>
        "##.P.##\n###.###\n" + string.Join("\n", Enumerable.Range(2, 10).Select(z => z == gormRow ? "#..T..#" : "#.....#")) + below;

    private static GameWorld BossWorld(int seed, Balance? balance = null, int gormRow = 10, string below = "", Column[]? columns = null)
    {
        var hall = Enumerable.Range(2, 10).SelectMany(z => Enumerable.Range(1, 5).Select(x => (x, z))).ToList();
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 7, 1, 0, [(2, 0), (3, 0), (4, 0)], [], []),
            new(1, RoomKind.Boss, "hall", 0, 2, 7, 10, 1, hall, [], []),
        ];
        LevelDoor[] doors = [new(3, 1, 0, 1, RoomSide.South, DoorKind.Boss)];
        var level = LevelParser.Parse(BossMap(gormRow, below)).WithColumns(columns ?? []);
        return new GameWorld(level, seed, balance ?? NoOtherLoot, doors, rooms);
    }

    /// <summary>Turns the player's view (through the mouse, as in the game) to look at <paramref name="point"/>.</summary>
    private static void LookAt(Player player, Vector3 point)
    {
        var to = point - player.EyePosition;
        var yaw = MathF.Atan2(to.X, to.Z);
        var pitch = MathF.Atan2(to.Y, new Vector2(to.X, to.Z).Length());
        var turn = MathF.IEEERemainder(player.View.YawRadians - yaw, MathF.Tau);
        player.Look(PlayerInput.None with
        {
            Look = new Vector2(turn, player.View.PitchRadians - pitch) / ControlSettings.MouseSensitivity,
        });
    }

    /// <summary>Walks the player (turning towards it each tick) until they stand on <paramref name="spot"/>, then lets them stop.</summary>
    private static void WalkTo(GameWorld world, Vector3 spot)
    {
        var player = world.Player;
        for (var tick = 0; new Vector2(spot.X - player.Position.X, spot.Z - player.Position.Z).Length() > 0.15f; tick++)
        {
            Assert.True(tick < 10 * Second, $"The player never got to {spot} (stuck at {player.Position}).");
            LookAt(player, spot with { Y = player.EyePosition.Y });
            world.Tick(Forward);
        }

        Run(world, PlayerInput.None, Second / 2);
    }

    /// <summary>Kills Gorm where he stands and lets his loot fall; returns the events.</summary>
    private static List<GameEvent> KillGorm(GameWorld world)
    {
        world.Gorm!.TakeDamage(10_000, []);
        return Run(world, PlayerInput.None, 2);
    }

    private static List<GameEvent> Run(GameWorld world, PlayerInput input, int ticks)
    {
        var events = new List<GameEvent>();
        for (var tick = 0; tick < ticks; tick++)
        {
            world.Tick(input);
            events.AddRange(world.Events);
        }

        return events;
    }

    private static void EmptyThePool(GameWorld world)
    {
        foreach (var piece in world.LootPool.Left.ToList())
        {
            world.LootPool.Take(piece);
        }

        Assert.Empty(world.LootPool.Left);
    }

    private static double Tolerance(double chance) => 5 * Math.Sqrt(chance * (1 - chance) / Worlds);

    [Theory]
    [InlineData(ChestTier.Beggar)]
    [InlineData(ChestTier.Guard)]
    [InlineData(ChestTier.King)]
    [InlineData(ChestTier.Vampire)]
    public void A_chest_holds_equipment_with_the_chance_of_its_tier(ChestTier tier)
    {
        var chance = (double)Balance.Default.Chests.Of(tier).EquipmentChance;

        var withEquipment = 0;
        for (var seed = 0; seed < Worlds; seed++)
        {
            var world = ChestWorld(seed, tier);
            var dropped = OpenChest(world).OfType<EquipmentDropped>().ToList();
            Assert.True(dropped.Count <= 1, "A chest gave more than one piece.");
            withEquipment += dropped.Count;
        }

        Assert.InRange(withEquipment / (double)Worlds, chance - Tolerance(chance), chance + Tolerance(chance));
    }

    [Theory]
    [InlineData(ChestTier.Beggar, ItemTier.Worn)]
    [InlineData(ChestTier.Guard, ItemTier.Forged)]
    [InlineData(ChestTier.King, ItemTier.Royal)]
    [InlineData(ChestTier.Vampire, ItemTier.Vampiric)]
    public void A_chest_s_equipment_is_at_least_of_its_tier_and_lies_glowing_in_its_cell(ChestTier tier, ItemTier least)
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var world = ChestWorld(seed, tier, AlwaysEquipment(tier));

            var events = OpenChest(world);

            var drop = Assert.Single(world.EquipmentDrops);
            Assert.True(drop.Piece.Tier >= least, $"Seed {seed}: {drop.Piece} ({drop.Piece.Tier}) from a {tier.ChestName()}.");
            Assert.False(world.LootPool.Contains(drop.Piece));
            var dropped = Assert.Single(events.OfType<EquipmentDropped>());
            Assert.Equal((drop.Id, drop.Piece), (dropped.DropId, dropped.Piece));
            Assert.Equal((1, 3), Level.ToGrid(dropped.Position.X, dropped.Position.Z));

            // It jumped out of the chest, but stays in the chest's cell - never in a wall.
            Assert.Equal((1, 3), Level.ToGrid(drop.Position.X, drop.Position.Z));
            Run(world, PlayerInput.None, Second);
            Assert.Equal((1, 3), Level.ToGrid(drop.Position.X, drop.Position.Z));
        }
    }

    [Fact]
    public void An_empty_pool_gives_the_chest_s_silver_once_more()
    {
        var world = ChestWorld(1, ChestTier.Guard, AlwaysEquipment(ChestTier.Guard), silver: 27);
        EmptyThePool(world);

        var events = OpenChest(world);

        Assert.Empty(world.EquipmentDrops);
        Assert.DoesNotContain(events, e => e is EquipmentDropped);
        Assert.Equal([27, 27], world.Loot.Where(loot => loot.Kind == LootKind.Silver).Select(loot => loot.Amount));
        Assert.All(world.Loot, loot => Assert.Equal((1, 3), Level.ToGrid(loot.Position.X, loot.Position.Z)));
    }

    [Fact]
    public void A_chest_without_equipment_gives_no_extra_silver()
    {
        var balance = NoOtherLoot with { Chests = NoOtherLoot.Chests with { Guard = NoOtherLoot.Chests.Guard with { EquipmentChance = 0f } } };
        var world = ChestWorld(1, ChestTier.Guard, balance, silver: 27);
        EmptyThePool(world);

        OpenChest(world);

        Assert.Empty(world.EquipmentDrops);
        Assert.Equal([27], world.Loot.Select(loot => loot.Amount));
    }

    [Fact]
    public void Gorm_always_leaves_equipment_royal_or_vampiric_by_their_shares()
    {
        var vampiricChance = (double)Balance.Default.Gorm.EquipmentVampiricChance;

        var vampiric = 0;
        for (var seed = 0; seed < Worlds; seed++)
        {
            var world = BossWorld(seed);
            var events = KillGorm(world);

            var dropped = Assert.Single(events.OfType<EquipmentDropped>());
            Assert.Contains(dropped.Piece.Tier, new[] { ItemTier.Royal, ItemTier.Vampiric });
            Assert.Single(world.EquipmentDrops);
            vampiric += dropped.Piece.Tier == ItemTier.Vampiric ? 1 : 0;
        }

        Assert.InRange(vampiric / (double)Worlds, vampiricChance - Tolerance(vampiricChance), vampiricChance + Tolerance(vampiricChance));
    }

    [Fact]
    public void Gorm_s_equipment_lies_behind_him_on_his_floor_and_never_in_a_wall()
    {
        var world = BossWorld(3);
        var gorm = world.Gorm!;

        KillGorm(world);

        var drop = Assert.Single(world.EquipmentDrops);
        var flat = new Vector2(drop.Position.X - gorm.Position.X, drop.Position.Z - gorm.Position.Z);
        Assert.Equal(GameWorld.GormEquipmentDistance, flat.Length(), 0.01f);
        Assert.True(GameWorld.GormEquipmentDistance < BodySizes.Gorm.Radius, "Only within his body's radius is the floor sure to be free of walls.");
        Assert.True(Vector2.Dot(flat, new Vector2(gorm.Facing.X, gorm.Facing.Z)) < 0f, "The piece lay in front of him, where the blood vial lies.");
        var (x, z) = Level.ToGrid(drop.Position.X, drop.Position.Z);
        Assert.Contains((x, z), world.Rooms[1].Cells);
    }

    /// <summary>
    /// Issue #338: F takes the nearest box a player looks at, and inside the staircase's box that is the stairs. If Gorm
    /// dies right where the staircase will rise, his piece still lies outside its box, on his floor - and once the
    /// stairs stand, a player who walks up to the piece and looks at it gets "Take ...", not "Climb the stairs".
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Gorm_s_equipment_never_lies_under_the_stairs_and_can_be_taken_when_they_stand(int seed)
    {
        var world = BossWorld(seed, gormRow: 6);
        Assert.Equal((3, 6), Stairs.CellIn(world.Rooms[1].Cells));
        Assert.Equal(world.Level.CellCenter(3, 6), world.Gorm!.Position);

        KillGorm(world);

        var drop = Assert.Single(world.EquipmentDrops);
        var stairsMiddle = world.Level.CellCenter(3, 6);
        var apart = MathF.Max(MathF.Abs(drop.Position.X - stairsMiddle.X), MathF.Abs(drop.Position.Z - stairsMiddle.Z));
        Assert.True(apart >= Stairs.Radius + EquipmentDrop.BoxRadius, $"The piece's box at {drop.Position} reaches into the staircase's box.");
        foreach (var (dx, dz) in new[] { (-1f, -1f), (-1f, 1f), (1f, -1f), (1f, 1f) })
        {
            var corner = Level.ToGrid(drop.Position.X + (dx * EquipmentDrop.BoxRadius), drop.Position.Z + (dz * EquipmentDrop.BoxRadius));
            Assert.Contains(corner, world.Rooms[1].Cells);
        }

        Assert.Equal(world.Gorm.Position.Y, drop.Position.Y, 0.01f);

        // In through the door: the dead boss's room is cleared at once, and the stairs rise.
        Run(world, PressF, 1);
        for (var tick = 0; world.Stairs is null; tick++)
        {
            Assert.True(tick < 3 * Second, "The stairs never rose - the test would prove nothing.");
            world.Tick(Forward);
        }

        Run(world, PlayerInput.None, SimulationClock.SecondsToTicks(Balance.Default.Gorm.StairsRiseTime) + 1);
        var stairs = world.Stairs!;
        Assert.True(stairs.CanInteract, "The stairs do not stand - the test would prove nothing.");

        // Walk up to the piece from the side away from the stairs and look at it.
        var outwards = Vector3.Normalize((drop.Position - stairs.Position) with { Y = 0f });
        WalkTo(world, drop.Position + (outwards * 1.2f));
        LookAt(world.Player, drop.Position + new Vector3(0f, EquipmentDrop.BoxHeight / 2f, 0f));

        Assert.Same(drop, world.InteractionTarget(world.Player));
        var taken = Assert.Single(Run(world, PressF, 1).OfType<EquipmentTaken>());
        Assert.Equal((drop.Id, drop.Piece), (taken.DropId, taken.Piece));
        Assert.Empty(world.EquipmentDrops);
        Assert.False(world.IsWon);
    }

    /// <summary>
    /// Issue #608: Gorm dies edge to edge with a standing column - in front of him (where the vial falls), behind him
    /// (where the piece falls) or to a side. None of his loot lies in the column, another column, a wall or the
    /// staircase, and a player who walks into the hall takes every piece: the piece with F, the vial and the silver by
    /// walking over them.
    /// </summary>
    [Theory]
    [MemberData(nameof(ColumnsAtGorm))]
    public void Gorm_s_loot_never_lies_in_a_column_he_died_against_and_every_piece_can_be_taken(int seed, int degrees, int gormRow, float farther)
    {
        // Balance.Default without other drops (NoOtherLoot): a key for the cleared hall would be one more pile.
        var plain = BossWorld(seed, NoOtherLoot, gormRow);
        var (at, facing) = (plain.Gorm!.Position, plain.Gorm.Facing);
        var turn = degrees * MathF.PI / 180f;
        var towards = new Vector2(
            (facing.X * MathF.Cos(turn)) - (facing.Z * MathF.Sin(turn)), (facing.X * MathF.Sin(turn)) + (facing.Z * MathF.Cos(turn)));
        var apartFromHim = BodySizes.Gorm.Radius + Column.Radius + 0.02f + farther;
        var column = new Column(at.X + (towards.X * apartFromHim), at.Z + (towards.Y * apartFromHim));
        var world = BossWorld(seed, NoOtherLoot, gormRow, columns: [column]);
        Assert.Equal((at, facing), (world.Gorm!.Position, world.Gorm.Facing));
        world.Player.TakeDamage(Balance.Default.BloodVial.Heal, []);

        KillGorm(world);
        Run(world, PlayerInput.None, Second);

        var drop = Assert.Single(world.EquipmentDrops);
        var vial = Assert.Single(world.Pickups);
        var silver = Assert.Single(world.Loot);
        var stairs = world.Level.CellCenter(3, 6);
        foreach (var (name, position, halfWidth) in new[]
        {
            ("piece", drop.Position, EquipmentDrop.BoxRadius),
            ("vial", vial.Position, BodySizes.Pickup.Radius),
            ("silver", silver.Position, BodySizes.Pickup.Radius),
        })
        {
            var flat = new Vector2(position.X, position.Z);
            var nearest = Vector2.Clamp(column.Centre, flat - new Vector2(halfWidth), flat + new Vector2(halfWidth));
            Assert.True(
                Vector2.Distance(nearest, column.Centre) >= Column.Radius + GameWorld.ColumnClearance - 0.001f,
                $"The {name} at {position} lies in the column at {column.Centre}, or closer to it than {GameWorld.ColumnClearance} m.");
            var apart = MathF.Max(MathF.Abs(position.X - stairs.X), MathF.Abs(position.Z - stairs.Z));
            Assert.True(apart >= Stairs.Radius + halfWidth, $"The {name} at {position} reaches into the staircase's box.");
            foreach (var (dx, dz) in new[] { (-1f, -1f), (-1f, 1f), (1f, -1f), (1f, 1f) })
            {
                var corner = Level.ToGrid(position.X + (dx * halfWidth), position.Z + (dz * halfWidth));
                Assert.Contains(corner, world.Rooms[1].Cells);
            }

            Assert.Equal(at.Y, position.Y, 0.01f);
        }

        // In through the door, then round the column to each piece - from the side away from Gorm.
        Run(world, PressF, 1);
        WalkTo(world, world.Level.CellCenter(3, 2));
        var taken = new List<GameEvent>();
        VisitFrom(world, drop.Position, at);
        LookAt(world.Player, drop.Position + new Vector3(0f, EquipmentDrop.BoxHeight / 2f, 0f));
        Assert.Same(drop, world.InteractionTarget(world.Player));
        taken.AddRange(Run(world, PressF, 1));
        taken.AddRange(VisitFrom(world, vial.Position, at, onto: true));
        taken.AddRange(VisitFrom(world, silver.Position, at, onto: true));

        Assert.Contains(taken, e => e is EquipmentTaken { DropId: var id } && id == drop.Id);
        Assert.Contains(taken, e => e is PickupTaken { PickupId: var id } && id == vial.Id);
        Assert.Contains(taken, e => e is SilverTaken { PileId: var id } && id == silver.Id);
        Assert.Empty(world.EquipmentDrops);
        Assert.Empty(world.Pickups);
        Assert.Empty(world.Loot);
    }

    /// <summary>
    /// Seeds, the column's direction from Gorm's facing (degrees), his row and how much farther than edge to edge the
    /// column stands (metres): every side of him, nearer the stairs, and a column the vial would not quite touch.
    /// </summary>
    public static TheoryData<int, int, int, float> ColumnsAtGorm()
    {
        var data = new TheoryData<int, int, int, float>();
        foreach (var degrees in new[] { 0, 45, 90, 135, 180, 225, 270, 315 })
        {
            data.Add(degrees / 45 % 3, degrees, 10, 0f);
        }

        data.Add(7, 0, 8, 0f);
        data.Add(8, 180, 8, 0f);
        data.Add(9, 90, 8, 0f);

        // Row 7: in front of him, the vial would fall into the staircase's box (the stairs rise in row 6).
        data.Add(10, 90, 7, 0f);
        data.Add(11, 270, 7, 0f);

        // In front of him, 5 cm beyond where the vial's box would end: less than ColumnClearance.
        data.Add(12, 0, 10, BodySizes.Pickup.Radius + 1.2f - BodySizes.Gorm.Radius - 0.02f + 0.05f);
        return data;
    }

    /// <summary>
    /// Walks into the hall along its west or east cell row (whichever is nearer), then to a spot 1.2 m out from
    /// <paramref name="spot"/>, seen from <paramref name="gorm"/> - and, with <paramref name="onto"/>, onto the spot.
    /// </summary>
    private static List<GameEvent> VisitFrom(GameWorld world, Vector3 spot, Vector3 gorm, bool onto = false)
    {
        // Right where he fell (the silver), "out" is towards where the player stands.
        var outwards = (spot - gorm) with { Y = 0f };
        outwards = Vector3.Normalize(outwards.LengthSquared() < 1e-4f ? (world.Player.Position - gorm) with { Y = 0f } : outwards);
        var near = spot + (outwards * 1.2f);
        var lane = world.Level.CellCenter(near.X < gorm.X ? 1 : 5, 2);
        var events = new List<GameEvent>();
        foreach (var waypoint in new[] { lane with { Z = world.Player.Position.Z }, lane with { Z = near.Z }, near })
        {
            events.AddRange(WalkCounting(world, waypoint));
        }

        if (onto)
        {
            events.AddRange(WalkCounting(world, spot));
        }

        return events;
    }

    /// <summary>As <see cref="WalkTo"/>, but returns the events on the way.</summary>
    private static List<GameEvent> WalkCounting(GameWorld world, Vector3 spot)
    {
        var player = world.Player;
        var events = new List<GameEvent>();
        for (var tick = 0; new Vector2(spot.X - player.Position.X, spot.Z - player.Position.Z).Length() > 0.15f; tick++)
        {
            Assert.True(tick < 10 * Second, $"The player never got to {spot} (stuck at {player.Position}).");
            LookAt(player, spot with { Y = player.EyePosition.Y });
            world.Tick(Forward);
            events.AddRange(world.Events);
        }

        return events;
    }

    /// <summary>
    /// Gorm's tier roll and his draw come from the loot pool's own generator - exactly what a fresh pool of the same
    /// seed rolls and draws. And whichever tier he rolls, or whether the pool is empty: the ghouls that die after him
    /// leave the same silver, keys (the world's loot generator) and drops (its combat generator).
    /// </summary>
    [Fact]
    public void Gorm_s_tier_roll_comes_from_the_loot_pool_and_never_shifts_what_silver_keys_and_drops_roll()
    {
        var vampiricChance = Balance.Default.Gorm.EquipmentVampiricChance;
        var tiers = new HashSet<ItemTier>();
        for (var seed = 0; seed < 20; seed++)
        {
            var twin = BossWorld(seed);
            var expectedTier = twin.LootPool.Roll(vampiricChance) ? ItemTier.Vampiric : ItemTier.Royal;
            var expected = twin.LootPool.Draw(expectedTier, mayRise: false);

            var world = BossWorld(seed);
            KillGorm(world);

            Assert.Equal(expected, Assert.Single(world.EquipmentDrops).Piece);
            tiers.Add(expectedTier);
        }

        Assert.Equal([ItemTier.Royal, ItemTier.Vampiric], tiers.Order());

        // Five ghouls in a closed pocket below the hall: asleep, outside every room.
        const string Pocket = "\n#######\n#GGGGG#\n#######";
        var royal = Balance.Default with
        {
            Drops = Balance.Default.Drops with { GhoulChance = 0.5f },
            Keys = new KeysBalance { RoomChance = 0f, EnemyChance = 0.5f },
            Gorm = Balance.Default.Gorm with { EquipmentVampiricChance = 0f },
        };
        var vampiric = royal with { Gorm = royal.Gorm with { EquipmentVampiricChance = 1f } };

        static List<string> GhoulDropsAfterGorm(Balance balance, int seed, bool emptyPool)
        {
            var world = BossWorld(seed, balance, below: Pocket);
            if (emptyPool)
            {
                EmptyThePool(world);
            }

            KillGorm(world);
            Assert.Equal(!emptyPool, world.EquipmentDrops.Count == 1);
            foreach (var ghoul in world.Ghouls)
            {
                ghoul.TakeDamage(10_000, []);
            }

            return Run(world, PlayerInput.None, 2).Select(e => e switch
            {
                SilverDropped silver => $"{silver.Amount} silver at {silver.Position}",
                KeyDropped key => $"key at {key.Position}",
                PickupDropped pickup => $"{pickup.Kind} at {pickup.Position}",
                _ => null,
            }).OfType<string>().ToList();
        }

        for (var seed = 0; seed < 10; seed++)
        {
            var asRoyal = GhoulDropsAfterGorm(royal, seed, emptyPool: false);
            Assert.True(asRoyal.Count > 1, "The ghouls left almost nothing - the test would prove nothing.");
            Assert.Equal(asRoyal, GhoulDropsAfterGorm(vampiric, seed, emptyPool: false));
            Assert.Equal(asRoyal, GhoulDropsAfterGorm(royal, seed, emptyPool: true));
        }
    }

    [Fact]
    public void An_empty_pool_gives_gorm_s_extra_silver_instead()
    {
        var world = BossWorld(1);
        EmptyThePool(world);

        var events = KillGorm(world);

        Assert.Empty(world.EquipmentDrops);
        Assert.DoesNotContain(events, e => e is EquipmentDropped);
        Assert.Contains(world.Loot, loot => loot.Kind == LootKind.Silver && loot.Amount == Balance.Default.Gorm.ExtraSilver);
        Assert.Equal(2, world.Loot.Count(loot => loot.Kind == LootKind.Silver));
    }

    [Fact]
    public void Gorm_never_rises_above_the_tier_he_rolled()
    {
        // Every draw would rise if it could: Gorm's tier is already rolled, so he still gives Royal ...
        var balance = NoOtherLoot with
        {
            Loot = NoOtherLoot.Loot with { HigherTierChance = 1f },
            Gorm = NoOtherLoot.Gorm with { EquipmentVampiricChance = 0f },
        };

        for (var seed = 0; seed < 20; seed++)
        {
            var world = BossWorld(seed, balance);
            KillGorm(world);
            Assert.Equal(ItemTier.Royal, Assert.Single(world.EquipmentDrops).Piece.Tier);
        }
    }

    [Fact]
    public void Gorm_s_vampiric_chance_comes_from_the_balancing()
    {
        var balance = NoOtherLoot with { Gorm = NoOtherLoot.Gorm with { EquipmentVampiricChance = 1f, ExtraSilver = 9 } };

        var world = BossWorld(1, balance);
        KillGorm(world);
        Assert.Equal(ItemTier.Vampiric, Assert.Single(world.EquipmentDrops).Piece.Tier);

        var empty = BossWorld(1, balance);
        EmptyThePool(empty);
        KillGorm(empty);
        Assert.Contains(empty.Loot, loot => loot.Amount == 9);
    }

    [Fact]
    public void The_same_seed_gives_the_same_loot()
    {
        static List<Equipment> Loot(int seed)
        {
            var found = new List<Equipment>();
            var chest = ChestWorld(seed, ChestTier.King, AlwaysEquipment(ChestTier.King));
            OpenChest(chest);
            found.AddRange(chest.EquipmentDrops.Select(drop => drop.Piece));
            var boss = BossWorld(seed);
            KillGorm(boss);
            found.AddRange(boss.EquipmentDrops.Select(drop => drop.Piece));
            return found;
        }

        for (var seed = 0; seed < 10; seed++)
        {
            var first = Loot(seed);
            Assert.Equal(2, first.Count);
            Assert.Equal(first, Loot(seed));
        }

        // ... and the seed matters: not every seed gives the same pieces.
        Assert.True(Enumerable.Range(0, 10).Select(seed => string.Join(",", Loot(seed))).Distinct().Count() > 1);
    }

    /// <summary>
    /// The player's corridor next to a closed pocket of ghouls that cannot reach the player. The player walks up to the
    /// chest and opens it - or leaves it shut -, then the ghouls are killed. Silver and keys come from the world's loot
    /// generator, drops from its combat generator: they must roll exactly as if the chest had stayed shut, whether it
    /// holds equipment or not. Before equipment, opening a chest rolled nothing at all.
    /// </summary>
    [Fact]
    public void A_chest_s_equipment_never_shifts_what_silver_keys_and_drops_roll()
    {
        const string Map = "#P####\n#.##G#\n#.##G#\n#.##G#\n#.##G#\n####G#\n####G#\n######";
        var withoutEquipment = Balance.Default with
        {
            Drops = Balance.Default.Drops with { GhoulChance = 0.5f },
            Keys = new KeysBalance { RoomChance = 0f, EnemyChance = 0.5f },
            Chests = Balance.Default.Chests with { King = Balance.Default.Chests.King with { EquipmentChance = 0f } },
        };
        var withEquipment = withoutEquipment with
        {
            Chests = withoutEquipment.Chests with { King = withoutEquipment.Chests.King with { EquipmentChance = 1f } },
        };

        // Only what the ghouls leave, without ids: the chest's own silver pile (no roll) takes the first loot id.
        static List<string> GhoulDrops(Balance balance, int seed, bool open)
        {
            var world = new GameWorld(LevelParser.Parse(Map), seed, balance, chests: [new ChestPlan(1, 3, 0, ChestTier.King, false, 25, false, false)]);
            WalkUpAndPressF(world, open);
            Assert.Equal(open, world.Chests[0].IsOpen);
            foreach (var ghoul in world.Ghouls)
            {
                ghoul.TakeDamage(10_000, []);
            }

            return Run(world, PlayerInput.None, 2).Select(e => e switch
            {
                SilverDropped silver => $"{silver.Amount} silver at {silver.Position}",
                KeyDropped key => $"key at {key.Position}",
                PickupDropped pickup => $"{pickup.Kind} at {pickup.Position}",
                _ => null,
            }).OfType<string>().ToList();
        }

        for (var seed = 0; seed < 10; seed++)
        {
            var shut = GhoulDrops(withoutEquipment, seed, open: false);
            Assert.True(shut.Count > 1, "The ghouls left almost nothing - the test would prove nothing.");
            Assert.Equal(shut, GhoulDrops(withoutEquipment, seed, open: true));
            Assert.Equal(shut, GhoulDrops(withEquipment, seed, open: true));
        }
    }
}
