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
    /// Level 0 with the player in the doorway of the antechamber (issue #565), looking at the middle of the room: the boss
    /// door on one side, the boss-gate teleporter (found, so its crystal shines) in the far corner.
    /// </summary>
    public GameWorld AtAntechamberEntrance()
    {
        var (generated, room) = FirstRoom(RoomKind.Antechamber);
        var level = generated.Level;
        var entrance = generated.Doors.Single(door => door.Kind != DoorKind.Boss && (door.RoomA == room.Index || door.RoomB == room.Index));
        var middle = new Vector3(
            ((float)room.FloorCells.Average(cell => cell.X) + 0.5f) * Level.CellSize,
            level[room.FloorCells[0].X, room.FloorCells[0].Z].FloorHeight + LookHeight,
            ((float)room.FloorCells.Average(cell => cell.Z) + 0.5f) * Level.CellSize);

        var world = new GameWorld(level, generated.Seed, Balance, generated.Doors, generated.Rooms, generated.Chests, generated.Well, generated.Teleporters)
        {
            LevelName = LevelZero.Name,
            Surfaces = SurfaceVariants.For(generated),
        };
        foreach (var teleporter in world.Teleporters)
        {
            teleporter.Unlock();
        }

        world.Player.PlaceAt(level.CellCenter(entrance.X, entrance.Z));
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
    /// Level 0 with the player in the boss room after the fight (issue #300): Gorm is dead before the first tick, so the
    /// room is cleared on the first tick and the spiral staircase begins to rise in its middle, as in the game. The
    /// player stands on the floor cell nearest to <paramref name="metres"/> from the foot of the staircase that sees its
    /// middle, looking there (half its height above the floor).
    /// </summary>
    public GameWorld AfterGorm(float metres)
    {
        var (generated, room) = FirstRoom(RoomKind.Boss);
        var level = generated.Level;
        var (stairsX, stairsZ) = Stairs.CellIn(room.FloorCells);
        var foot = level.CellCenter(stairsX, stairsZ);
        var middle = foot + new Vector3(0f, Stairs.Height / 2f, 0f);
        var cell = BestPlace(level, room, middle, cell => MathF.Abs(Vector3.Distance(level.CellCenter(cell.X, cell.Z), foot) - metres));

        var world = World(generated, cell);
        world.Gorm!.TakeDamage(world.Gorm.Health, new List<GameEvent>());
        Face(world.Player, middle);
        return world;
    }

    /// <summary>
    /// A bare hall (hand-made, so its enemies are awake) with one figure - 'G' ghoul, 'B' bat, 'T' Gorm, 'D' dummy -
    /// <paramref name="metres"/> in front of the player (a multiple of 2, the cell size), looking at it. The ceiling
    /// lies at <paramref name="ceiling"/> (default: as high as a corridor; Gorm's boss room: <see cref="RoomTemplate.RoomCeilingHeight"/>).
    /// The floor shows texture <paramref name="floor"/> (default A, like every hand-made level); walls and ceiling stay A.
    /// </summary>
    public GameWorld Arena(char figure, int metres, float ceiling = Cell.DefaultCeilingHeight, TextureVariant floor = TextureVariant.A)
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

        var world = new GameWorld(level, Seed, Balance)
        {
            Surfaces = floor == TextureVariant.A ? SurfaceVariants.Plain : SurfaceVariants.Of(
                [new RoomLook(TextureVariant.A, floor, TextureVariant.A)],
                (from x in Enumerable.Range(0, level.Width) from z in Enumerable.Range(0, level.Depth) select (x, z)).ToDictionary(cell => cell, _ => 0)),
        };
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
    /// A bare hall with the Well of Sirius in its middle, 4 m in front of the player, who looks at its middle (issue #438).
    /// <paramref name="spent"/>: it has been used already and stands dry.
    /// </summary>
    public GameWorld Well(bool spent)
    {
        const int Width = 9;
        const int Depth = 8;
        const int PlayerRow = 2;
        const int WellRow = PlayerRow + 2;
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

        var world = new GameWorld(LevelParser.Parse(text.ToString()), Seed, Balance, well: new WellPlan(Width / 2, WellRow, 0));
        if (spent)
        {
            world.Well!.Use();
        }

        Face(world.Player, world.Well!.Position + new Vector3(0f, WellOfSirius.Height / 2f, 0f));
        return world;
    }

    /// <summary>
    /// A long bare hall with a teleporter <paramref name="metres"/> in front of the player, who looks at its crystal
    /// (issue #480). <paramref name="unlocked"/>: it has been found already and its crystal shines.
    /// </summary>
    public GameWorld TeleporterHall(float metres, bool unlocked)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(metres, 2f);
        var cells = (int)MathF.Ceiling(metres / Level.CellSize);
        const int Width = 9;
        const int PlayerRow = 2;
        var depth = PlayerRow + cells + 3;
        var text = new StringBuilder();
        for (var z = 0; z < depth; z++)
        {
            for (var x = 0; x < Width; x++)
            {
                var wall = x == 0 || z == 0 || x == Width - 1 || z == depth - 1;
                text.Append(wall ? '#' : x == Width / 2 && z == PlayerRow ? 'P' : '.');
            }

            text.Append('\n');
        }

        var world = new GameWorld(LevelParser.Parse(text.ToString()), Seed, Balance,
            teleporters: [new TeleporterPlan(Width / 2, PlayerRow + cells, 0, TeleporterSite.Start)]);
        var teleporter = world.Teleporters[0];
        if (unlocked)
        {
            teleporter.Unlock();
        }

        // The player's cell is the nearest whole one; the last bit forward makes the distance exact.
        world.Player.PlaceAt(teleporter.Position - new Vector3(0f, 0f, metres));
        Face(world.Player, teleporter.Position + new Vector3(0f, TeleporterLook.CrystalHeight, 0f));
        return world;
    }

    /// <summary>
    /// Level 0 with every room on the map and every teleporter found, the player on the one in the start hall (issue
    /// #480: the destination choice). <paramref name="sealBossGate"/>: the boss-gate teleporter stands in a combat room,
    /// that room sealed with its enemies alive, as if another player fought there (co-op) - so the picture shows a sealed
    /// destination (see <see cref="WithBossGateInCombatRoom"/>).
    /// </summary>
    public GameWorld AtStartTeleporter(bool sealBossGate)
    {
        var world = sealBossGate
            ? Enumerable.Range(Seed, SeedsToTry).Select(WithBossGateInCombatRoom).FirstOrDefault(world => world is not null && BossGateInCombatRoom(world))
                ?? throw new InvalidOperationException("No seed has a combat room with enemies in front of the antechamber.")
            : StartRun();
        foreach (var room in world.Rooms)
        {
            room.Discover();
        }

        foreach (var teleporter in world.Teleporters)
        {
            teleporter.Unlock();
        }

        var start = world.Teleporters.First(teleporter => teleporter.Plan.Site == TeleporterSite.Start);
        world.Player.PlaceAt(start.Position);
        if (sealBossGate)
        {
            BossGateRoom(world)!.Seal();
        }

        return world;
    }

    private static bool BossGateInCombatRoom(GameWorld world) => BossGateRoom(world) is { Kind: RoomKind.Combat, WaveCount: > 0 };

    /// <summary>
    /// The run of <paramref name="seed"/> with its boss-gate teleporter in the combat room in front of the antechamber, at
    /// that room's floor cell nearest to the antechamber's door that keeps two cells from every door - or null if a
    /// corridor lies in front of the antechamber. Since issue #565 the teleporter stands in the antechamber, which never
    /// seals; a sealed destination needs a teleporter in an arena, so the scene sets it where it stood before #565.
    /// </summary>
    private GameWorld? WithBossGateInCombatRoom(int seed)
    {
        var generated = _levelZero.Build(seed, Balance);
        var antechamber = generated.Rooms.Single(room => room.Kind == RoomKind.Antechamber);
        var entrance = generated.Doors.Single(door => door.Kind != DoorKind.Boss && (door.RoomA == antechamber.Index || door.RoomB == antechamber.Index));
        var before = generated.Rooms[entrance.RoomA == antechamber.Index ? entrance.RoomB : entrance.RoomA];
        if (before.Kind != RoomKind.Combat)
        {
            return null;
        }

        var doors = generated.Doors.Where(door => door.RoomA == before.Index || door.RoomB == before.Index).ToList();
        var (x, z) = before.FloorCells
            .Where(cell => doors.All(door => Math.Max(Math.Abs(cell.X - door.X), Math.Abs(cell.Z - door.Z)) >= 2))
            .OrderBy(cell => ((cell.X - entrance.X) * (cell.X - entrance.X)) + ((cell.Z - entrance.Z) * (cell.Z - entrance.Z)))
            .ThenBy(cell => cell.Z)
            .ThenBy(cell => cell.X)
            .First();
        var teleporters = generated.Teleporters!
            .Select(plan => plan.Site == TeleporterSite.BossGate ? new TeleporterPlan(x, z, before.Index, TeleporterSite.BossGate) : plan)
            .ToList();
        return new GameWorld(generated.Level, seed, Balance, generated.Doors, generated.Rooms, generated.Chests, generated.Well, teleporters)
        {
            LevelName = LevelZero.Name,
            Surfaces = SurfaceVariants.For(generated),
        };
    }

    private static RoomState? BossGateRoom(GameWorld world) =>
        world.Teleporters.FirstOrDefault(teleporter => teleporter.Plan.Site == TeleporterSite.BossGate) is { } gate
            ? world.Rooms.FirstOrDefault(room => room.Cells.Contains((gate.Plan.X, gate.Plan.Z)))
            : null;

    /// <summary>
    /// A bare hall cut in two by a wall with one door of <paramref name="kind"/> in it, <paramref name="cells"/> cells
    /// (2 m each, so 6 m by default, at most 5 cells) in front of the player, who looks at its middle (issue #245).
    /// <paramref name="sideways"/>: the wall runs North/South and the passage along X (the door model turned a quarter).
    /// <paramref name="open"/>: the door is opened by the player before the first tick - its leaves swing away from them
    /// while the scene's ticks run.
    /// </summary>
    public GameWorld DoorHall(DoorKind kind, bool sideways = false, bool open = false, int cells = 3)
    {
        const int Width = 9;
        const int Depth = 10;
        const int Middle = Width / 2;
        const int DoorRow = 6;
        ArgumentOutOfRangeException.ThrowIfLessThan(cells, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(cells, DoorRow - 1);
        var playerRow = DoorRow - cells;
        var text = new StringBuilder();
        for (var row = 0; row < Depth; row++)
        {
            for (var column = 0; column < Width; column++)
            {
                // Built along Z; sideways the map is mirrored on its diagonal afterwards (rows become columns).
                var outer = row == 0 || column == 0 || row == Depth - 1 || column == Width - 1;
                var wall = outer || (row == DoorRow && column != Middle);
                text.Append(wall ? '#' : row == playerRow && column == Middle ? 'P' : '.');
            }

            text.Append('\n');
        }

        var map = sideways ? Transposed(text.ToString()) : text.ToString();
        var (x, z) = sideways ? (DoorRow, Middle) : (Middle, DoorRow);
        LevelDoor[] doors = [new(x, z, 0, 1, sideways ? RoomSide.East : RoomSide.South, kind)];
        var world = new GameWorld(LevelParser.Parse(map), Seed, Balance, doors);
        var door = world.Doors[0];
        var doorMiddle = (door.BoxMin + door.BoxMax) / 2f;
        if (open)
        {
            // The regular way, as in the game: one cell before the door, a key in hand for a locked one, F for one
            // tick - so the level, the events and the key are exactly as after a real opening. Then back to the start.
            var start = world.Player.Position;
            var (beforeX, beforeZ) = sideways ? (DoorRow - 1, Middle) : (Middle, DoorRow - 1);
            world.Player.PlaceAt(world.Level.CellCenter(beforeX, beforeZ));
            Face(world.Player, doorMiddle);
            if (kind == DoorKind.Locked)
            {
                world.Player.AddKeys(1);
            }

            world.Tick(PlayerInput.None with { Interact = true });
            if (!door.IsOpen)
            {
                throw new InvalidOperationException($"The {kind} door of the door hall did not open on F.");
            }

            world.Player.PlaceAt(start);
        }

        Face(world.Player, doorMiddle);
        return world;
    }

    /// <summary>
    /// Level 0 from <paramref name="seed"/> with the player before its first door of <paramref name="kind"/>, on the
    /// floor cell 4 to 6 m in front of it (along its passage) that sees it, looking at it - a door as the generator
    /// placed it, between its rooms (issue #245).
    /// </summary>
    /// <exception cref="InvalidOperationException">If the level has no such door or no place before it.</exception>
    public GameWorld BeforeDoor(int seed, DoorKind kind)
    {
        var generated = _levelZero.Build(seed, Balance);
        var plan = generated.Doors.FirstOrDefault(door => door.Kind == kind)
            ?? throw new InvalidOperationException($"Level 0 with seed {seed} has no {kind} door.");
        var level = generated.Level;
        var target = level.CellCenter(plan.X, plan.Z) + new Vector3(0f, LookHeight, 0f);
        var alongX = plan.Side is RoomSide.East or RoomSide.West;
        var eye = new Vector3(0f, BodySizes.Player.EyeHeight, 0f);
        var place = new[] { 2, 3, -2, -3 }
            .Select(step => alongX ? (X: plan.X + step, Z: plan.Z) : (X: plan.X, Z: plan.Z + step))
            .Where(cell => cell.X >= 0 && cell.Z >= 0 && cell.X < level.Width && cell.Z < level.Depth && !level[cell.X, cell.Z].IsSolid)
            .Where(cell => !generated.Doors.Any(door => door.Cells.Contains((cell.X, cell.Z))))
            .Where(cell => LineOfSight.IsClear(level, level.CellCenter(cell.X, cell.Z) + eye, target))
            .Select(cell => ((int X, int Z)?)cell)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"No place before the first {kind} door of level 0 with seed {seed}.");

        var world = World(generated, place);
        var door = world.Doors.First(door => door.X == plan.X && door.Z == plan.Z);
        Face(world.Player, (door.BoxMin + door.BoxMax) / 2f);
        return world;
    }

    /// <summary>
    /// Level 0 with the player before a cracked wall as the generator placed it (issue #246): the first one, from seed
    /// <see cref="Seed"/> on, that fits. The player stands <paramref name="cells"/> cells (2 m each) straight out from the
    /// wall's middle and <paramref name="along"/> cells along it (a flat angle onto the face), on the side away from the
    /// secret room - or inside it with <paramref name="fromSecretRoom"/> - and looks at the middle of the face before them.
    /// <paramref name="variant"/>: only a face of that wall variant. <paramref name="sideways"/>: only a wall of an
    /// East/West door (its plates turned a quarter).
    /// </summary>
    /// <exception cref="InvalidOperationException">If no seed gives such a wall with such a place before it.</exception>
    public GameWorld BeforeCrackedWall(
        int cells, int along = 0, bool fromSecretRoom = false, TextureVariant? variant = null, bool sideways = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(cells, 1);
        for (var seed = Seed; seed < Seed + SeedsToTry; seed++)
        {
            var generated = _levelZero.Build(seed, Balance);
            var surfaces = SurfaceVariants.For(generated);
            var secretCells = generated.Rooms.Where(room => room.Kind == RoomKind.Secret).SelectMany(room => room.FloorCells).ToHashSet();
            foreach (var plan in generated.Doors.Where(door => door.Kind == DoorKind.Cracked))
            {
                if (sideways != (plan.Side is RoomSide.East or RoomSide.West))
                {
                    continue;
                }

                var (front, back) = CrackedWallLook.FaceCells(plan.X, plan.Z, plan.Side);
                var (frontVariant, backVariant) = CrackedWallLook.Variants(surfaces, plan.X, plan.Z, plan.Side);
                var faceFront = secretCells.Contains(front) == fromSecretRoom;
                var (face, faceVariant) = faceFront ? (front, frontVariant) : (back, backVariant);
                if (variant is { } wanted && faceVariant != wanted)
                {
                    continue;
                }

                if (Place(generated, plan, face, cells, along) is { } place)
                {
                    var world = World(generated, place.Cell);
                    Face(world.Player, place.Target);
                    return world;
                }
            }
        }

        throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
            $"No level 0 with seeds {Seed} to {Seed + SeedsToTry - 1} has a cracked wall with a place {cells} cells before it and {along} along it."));
    }

    // The cell `cells` out from the cracked wall `plan` on the side of its face cell `face` and `along` cells along the
    // wall, and the middle of that face at look height - if the cell is floor, no door, and sees the face.
    private static ((int X, int Z) Cell, Vector3 Target)? Place(GeneratedLevel generated, LevelDoor plan, (int X, int Z) face, int cells, int along)
    {
        var level = generated.Level;
        var (outX, outZ) = (face.X - plan.X, face.Z - plan.Z);
        var cell = (X: plan.X + (outX * cells) + (outZ * along), Z: plan.Z + (outZ * cells) + (outX * along));
        if (cell.X < 0 || cell.Z < 0 || cell.X >= level.Width || cell.Z >= level.Depth || level[cell.X, cell.Z].IsSolid
            || generated.Doors.Any(door => door.Cells.Contains((cell.X, cell.Z))))
        {
            return null;
        }

        // A hand's breadth in front of the face, so the line of sight does not end inside the wall.
        var target = level.CellCenter(plan.X, plan.Z) + (new Vector3(outX, 0f, outZ) * ((Level.CellSize / 2f) + 0.1f))
            + new Vector3(0f, LookHeight, 0f);
        var eye = level.CellCenter(cell.X, cell.Z) + new Vector3(0f, BodySizes.Player.EyeHeight, 0f);
        return LineOfSight.IsClear(level, eye, target) ? (cell, target) : null;
    }

    // The map with rows and columns swapped.
    private static string Transposed(string map)
    {
        var lines = map.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var text = new StringBuilder();
        for (var column = 0; column < lines[0].Length; column++)
        {
            text.Append(string.Concat(lines.Select(line => line[column]))).Append('\n');
        }

        return text.ToString();
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
