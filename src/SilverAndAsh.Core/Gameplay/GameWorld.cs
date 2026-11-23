using System.Numerics;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Everything that is simulated together: the level, the player, the training dummies and the ghouls.
/// One tick of the world advances all of them and collects their events in one list.
/// <para>
/// The whole game state lives in this object, not in static fields - a requirement for online co-op later
/// (decision record 0003). There is one player for now; the name "Player" may become a list then.
/// </para>
/// </summary>
public sealed class GameWorld
{
    private readonly int _restartTicks;
    private readonly List<GameEvent> _events = [];
    private readonly Random _random;
    private readonly Random _lootRandom;

    /// <summary>Only for the spread of pellets: so shooting does not change what the other random rolls give.</summary>
    private readonly Random _shotRandom;
    private int _ticksAllPlayersDead;
    private readonly List<TrainingDummy> _dummies;
    private readonly List<Ghoul> _ghouls;
    private readonly List<Bat> _bats;
    private readonly Gorm? _gorm;
    private bool _gormLootDropped;
    private Stairs? _stairs;
    private readonly List<Projectile> _projectiles = [];
    private readonly List<Pickup> _pickups = [];
    private readonly HashSet<int> _dropsRolled = [];
    private int _nextPickupId;
    private readonly List<Loot> _loot = [];
    private int _nextLootId;
    private readonly List<EquipmentDrop> _equipmentDrops = [];
    private int _nextEquipmentDropId;
    private int _nextProjectileId;
    private readonly Level _levelWithOpenDoors;

    // Issue #590: what Gorm's blows did to the columns, and the level with open doors without the broken ones - the
    // base of Level and LevelAsBuilt. A restart starts again from _levelWithOpenDoors, all columns standing.
    private readonly ColumnWear _columnWear;
    private Level _levelWithStandingColumns;
    private readonly IReadOnlyList<LevelDoor> _doorPlan;
    private readonly List<Door> _doors;
    private readonly IReadOnlyList<PlacedRoom> _roomPlan;
    private readonly List<RoomState> _rooms;
    private readonly Dictionary<(int X, int Z), RoomState> _roomOfCell = [];
    private readonly HashSet<int> _asleep = [];
    private readonly IReadOnlyList<ChestPlan> _chestPlan;
    private readonly List<(int X, int Z)> _safeCells = [];
    private Level? _enemyPaths;
    private List<(int X, int Z)> _enemyPathWalls = [];
    private Level _enemyLevel;
    private readonly List<Chest> _chests;
    private readonly IReadOnlyList<TeleporterPlan> _teleporterPlan;
    private readonly List<Teleporter> _teleporters;
    private bool _lossReported;
    private bool _endSummaryTaken;
    private ShowroomExhibits? _showroom;

    /// <summary>
    /// Creates the world for <paramref name="level"/>: the player at its start, dummies at their cells.
    /// <paramref name="seed"/> starts the world's random numbers: the same seed always gives the same game.
    /// <paramref name="balance"/> holds all balancing values; every figure of this world gets them (the standard
    /// values if none is given). <paramref name="doors"/> (optional) are the doors of a generated level; they all
    /// start closed. Their cells must be walkable in <paramref name="level"/>. <paramref name="rooms"/> (optional) are
    /// the rooms of a generated level: enemies in combat and boss rooms sleep until a player enters, then the room
    /// becomes an arena (see <see cref="RoomState"/>). <paramref name="chests"/> (optional) are the chests of a generated
    /// level; a combat room's chest rises once its room is cleared. <paramref name="well"/> (optional) is its Well of
    /// Sirius. <paramref name="teleporters"/> (optional) are the teleporters of a generated level; they all start
    /// locked (issue #436).
    /// </summary>
    public GameWorld(
        Level level, int seed = 0, Balance? balance = null, IReadOnlyList<LevelDoor>? doors = null,
        IReadOnlyList<PlacedRoom>? rooms = null, IReadOnlyList<ChestPlan>? chests = null, WellPlan? well = null,
        IReadOnlyList<TeleporterPlan>? teleporters = null)
    {
        ArgumentNullException.ThrowIfNull(level);
        _doorPlan = doors ?? [];
        _levelWithOpenDoors = WithBossPortals(level, _doorPlan);
        _levelWithStandingColumns = _levelWithOpenDoors;
        Balance = balance ?? Balance.Default;
        _columnWear = new ColumnWear(Balance.Columns.HitsToBreak);
        _doors = _doorPlan.Select((plan, index) => new Door(index, plan, level, Balance.Doors, Balance.Grates)).ToList();
        Level = WithClosedDoors();
        LevelAsBuilt = WithUnbrokenWalls();
        Seed = seed;
        _restartTicks = SimulationClock.SecondsToTicks(Balance.RestartDelay);
        DamageRules = DamageRules.Standard(Balance);

        // Online co-op rule 3: all chance comes from the seed. Loot (silver) has a generator of its own, so the
        // amounts never shift what the fights roll - and the fights never shift the loot.
        _random = new Random(seed);
        _lootRandom = new Random(unchecked((seed * 397) ^ 0x5117E2));
        _shotRandom = new Random(unchecked((seed * 7919) ^ 0x5407));
        _dummies = level.DummyPositions.Select((position, index) => new TrainingDummy(index, position, Balance)).ToList();

        // Ghoul numbers continue after the dummies': every target has its own number.
        // They start looking towards the player's start (their head is at the front).
        _ghouls = level.GhoulPositions
            .Select((position, index) => new Ghoul(
                _dummies.Count + index, position, FacingTowards(position, level.PlayerStartPosition), Balance))
            .ToList();

        // Bat numbers continue after the ghouls'. They start hovering above their cells.
        _bats = level.BatPositions
            .Select((position, index) => new Bat(
                _dummies.Count + _ghouls.Count + index, Bat.HoverPosition(level, position), _random, Balance))
            .ToList();

        // Gorm (at most one, 'T') comes after the bats; he starts looking towards the player's start.
        _gorm = level.BossPositions
            .Select(position => new Gorm(
                _dummies.Count + _ghouls.Count + _bats.Count, position, FacingTowards(position, level.PlayerStartPosition), Balance, _random,
                rooms?.FirstOrDefault(room => room.Kind == RoomKind.Boss)?.FloorCells.ToHashSet()))
            .FirstOrDefault();

        // The player cannot walk through standing dummies or living ghouls; lying or dead ones are stepped over.
        // ... nor through chests that stand (or are rising) - but they can jump onto them - nor through the well.
        Well = well is null ? null : new WellOfSirius(well, level, Balance.Well);
        Player = new Player(
            Level, () => Targets.Where(target => target.IsHittable).Select(target => target.Body).Concat(WellBodies), Balance, _random,
            () => StandingChests.Select(chest => chest.Body));

        // The equipment still to be found, shared by all players. What they start with is not in it (no doubles).
        // It rolls with its own generator, so having it changes no other roll of the world.
        LootPool = new LootPool(seed, Balance.Loot, [Equipment.Of(Player.Melee), .. Player.RangedSlots.OfType<RangedWeapon>().Select(weapon => Equipment.Of(weapon.Kind))]);

        _chestPlan = chests ?? [];
        _chests = _chestPlan.Select((plan, index) => new Chest(index, plan, level)).ToList();

        _teleporterPlan = teleporters ?? [];
        _teleporters = _teleporterPlan.Select((plan, index) => new Teleporter(index, plan, level)).ToList();

        _roomPlan = rooms ?? [];

        // Treasure and secret rooms are safe: their floor and doorways are wall for enemies (see EnemyLevel).
        var safeRooms = _roomPlan.Where(room => room.Kind.IsHoard()).Select(room => room.Index).ToHashSet();
        _safeCells.AddRange(_roomPlan.Where(room => safeRooms.Contains(room.Index)).SelectMany(room => room.FloorCells));
        _safeCells.AddRange(_doorPlan.Where(door => safeRooms.Contains(door.RoomA) || safeRooms.Contains(door.RoomB)).SelectMany(door => door.Cells));
        _enemyLevel = WithSafeRoomsWalled();
        _rooms = _roomPlan.Select(room => new RoomState(room, _doorPlan
                .Select((door, index) => (door, index))
                .Where(pair => pair.door.RoomA == room.Index || pair.door.RoomB == room.Index)
                .Select(pair => pair.index)))
            .ToList();
        foreach (var room in _rooms)
        {
            foreach (var cell in room.Cells)
            {
                _roomOfCell[cell] = room;
            }
        }

        // Every enemy belongs to the room it starts in; in an arena it sleeps until the room is entered.
        foreach (var enemy in Enemies)
        {
            var position = enemy.Body.Position;
            if (_roomOfCell.TryGetValue(Level.ToGrid(position.X, position.Z), out var room))
            {
                room.Enemies.Add(enemy);
                if (room.IsArena)
                {
                    _asleep.Add(enemy.Id);
                }

                // Issue #424: in a combat room it is not even there yet - it waits inside the floor (a ghoul) or the
                // ceiling (a bat) on its start cell, which the level generator keeps clear of the doors
                // (DoorClearance), and comes out when the room seals. Nobody can lure it out through an
                // open door. The boss room stays as it was: Gorm stands there from the start.
                if (room.Kind == RoomKind.Combat)
                {
                    Hide(enemy);
                }
            }
        }

        // Today one wave per combat room: all its enemies. More waves would come out one after the other.
        foreach (var room in _rooms.Where(room => room.Kind == RoomKind.Combat && room.Enemies.Count > 0))
        {
            room.Waves.Add([.. room.Enemies]);
        }
    }

    // Puts an enemy of a combat room inside the floor (ghoul) or the ceiling (bat) of its start cell.
    private void Hide(ITarget enemy)
    {
        switch (enemy)
        {
            case Ghoul ghoul:
                ghoul.Emergence.Hide(new Vector3(0f, -BodySizes.Ghoul.Height, 0f));
                break;
            case Bat bat:
                var ceiling = Level.CellAt(bat.Position).CeilingHeight;
                bat.Emergence.Hide(new Vector3(0f, ceiling - bat.Position.Y, 0f));
                break;
        }
    }

    // A wave comes out: its enemies wake up and rise out of the floor or sink out of the ceiling. Nothing to hear yet
    // (a new kind of sound needs a listening test first) - only the event, for effects later.
    private void Release(RoomState room, IReadOnlyList<ITarget> wave)
    {
        KeepAwayFromPlayers(room, wave);
        var emergeTicks = SimulationClock.SecondsToTicks(Balance.Difficulty.EmergeTime);
        foreach (var enemy in wave)
        {
            _asleep.Remove(enemy.Id);
            switch (enemy)
            {
                case Ghoul ghoul:
                    ghoul.Emergence.Start(emergeTicks);
                    break;
                case Bat bat:
                    bat.Emergence.Start(emergeTicks);
                    break;
            }
        }

        _events.Add(new WaveEmerged(room.Index, room.WavesReleased - 1, wave.Select(enemy => enemy.Id).ToList()));
    }

    // Issue #445: nobody comes out right in front of a player. The start cells were chosen with the level, before
    // anybody stood in the room; now the players' places are known. An enemy whose start cell is nearer than
    // difficulty.playerDistance to any player comes out on the nearest free cell of the room that keeps that distance
    // to every player and keeps clear of every door (DoorClearance, issue #490: straight in difficulty.doorDistance,
    // beside the door difficulty.sideDoorDistance) - or, if there is none, on the free cell farthest from all players
    // (clear of the doors if possible). No chance involved: the same places give the same cells.
    private void KeepAwayFromPlayers(RoomState room, IReadOnlyList<ITarget> wave)
    {
        var difficulty = Balance.Difficulty;
        var players = Players.Select(player => new Vector2(player.Position.X, player.Position.Z)).ToList();
        var doors = room.DoorIds.SelectMany(id => _doors[id].Cells).Select(cell => DoorClearance.Of(cell, room.Cells)).ToList();

        // Metres from the middle of a cell to the nearest player.
        static float Nearest(List<Vector2> places, (int X, int Z) cell) =>
            places.Count == 0 ? float.MaxValue : places.Min(place => Vector2.Distance(Middle(cell), place));

        // Enemies that stay - all others of the room, and those of the wave that are far enough - keep their cells.
        var movers = wave.Where(enemy => Nearest(players, CellOf(enemy)) < difficulty.PlayerDistance).ToList();
        var taken = room.Enemies.Where(enemy => !IsDead(enemy) && !movers.Contains(enemy)).Select(CellOf).ToHashSet();
        var cells = room.Cells.OrderBy(cell => cell.Z).ThenBy(cell => cell.X).ToList();

        foreach (var enemy in movers)
        {
            var start = CellOf(enemy);
            var free = cells.Where(cell => !taken.Contains(cell)).ToList();
            if (free.Count == 0)
            {
                continue;
            }

            var awayFromDoors = free.Where(cell => DoorClearance.Keeps(cell, doors, difficulty)).ToList();
            var allowed = awayFromDoors.Where(cell => Nearest(players, cell) >= difficulty.PlayerDistance).ToList();
            var choice = allowed.Count > 0
                ? allowed.MinBy(cell => Vector2.Distance(Middle(cell), Middle(start)))
                : (awayFromDoors.Count > 0 ? awayFromDoors : free).MaxBy(cell => Nearest(players, cell));
            taken.Add(choice);
            MoveHidden(enemy, choice);
        }
    }

    private static readonly Vector2 Half = new(Level.CellSize / 2f, Level.CellSize / 2f);

    private static Vector2 Middle((int X, int Z) cell) => (new Vector2(cell.X, cell.Z) * Level.CellSize) + Half;

    private (int X, int Z) CellOf(ITarget enemy) => Level.ToGrid(enemy.Body.Position.X, enemy.Body.Position.Z);

    // Puts a hidden enemy onto another cell, still hidden there (a bat in that cell's ceiling).
    private void MoveHidden(ITarget enemy, (int X, int Z) cell)
    {
        var floor = Level.CellCenter(cell.X, cell.Z);
        switch (enemy)
        {
            case Ghoul ghoul:
                ghoul.PlaceAt(floor);
                break;
            case Bat bat:
                bat.PlaceAt(Bat.HoverPosition(Level, floor));
                break;
            default:
                return;
        }

        Hide(enemy);
    }

    /// <summary>
    /// The weapons and items still to be found in this run (decision records 0006 and 0008) - one pool for the whole
    /// world, not one per player. Chests and the boss will draw from it (#318).
    /// </summary>
    public LootPool LootPool { get; }

    /// <summary>Name of the level, shown briefly at the start (e.g. "THE CATACOMBS"), or null for hand-made levels.</summary>
    public string? LevelName { get; init; }

    /// <summary>
    /// Which texture variant each wall, floor and ceiling shows (issue #244) - only how the level looks, never a rule.
    /// Variant A everywhere unless the level was generated (see <see cref="SurfaceVariants.For"/>).
    /// </summary>
    public SurfaceVariants Surfaces { get; init; } = SurfaceVariants.Plain;

    /// <summary>
    /// The exhibits of the showroom (issue #283, see <see cref="ShowroomExhibits"/>), placed when set - or null in a
    /// normal game, which this never changes.
    /// </summary>
    public ShowroomExhibits? Showroom
    {
        get => _showroom;
        init
        {
            _showroom = value;
            if (value is null)
            {
                return;
            }

            if (value.Stairs is { } stairs)
            {
                // It stands from the start: one tick of rising is all it needs.
                _stairs = new Stairs(stairs, 0f);
                _stairs.Tick();
            }

            foreach (var loot in value.Loot)
            {
                _loot.Add(new Loot(_nextLootId++, loot.Kind, loot.Amount, loot.Position));
            }

            foreach (var consumable in value.Consumables)
            {
                _pickups.Add(new Pickup(_nextPickupId++, consumable.Kind, consumable.Position));
            }

            foreach (var weapon in value.Weapons)
            {
                _equipmentDrops.Add(new WeaponDrop(_nextEquipmentDropId++, weapon.Kind, weapon.Position));
            }

            foreach (var item in value.Items)
            {
                _equipmentDrops.Add(new ItemDrop(_nextEquipmentDropId++, item.Kind, item.Position));
            }
        }
    }

    /// <summary>
    /// True in the showroom: enemies are exhibits that neither move, attack nor take damage, and the staircase cannot be
    /// climbed - so nobody dies and the level never changes there.
    /// </summary>
    public bool IsShowroom => _showroom is not null;

    /// <summary>
    /// The level as it was built, all doors open - for drawing - except unbroken cracked walls, which are drawn as
    /// wall. A new object once a wall breaks (so the picture is rebuilt). <see cref="Level"/> is what blocks.
    /// </summary>
    public Level LevelAsBuilt { get; private set; }

    /// <summary>True once a player climbed the spiral staircase that rises after the boss room is cleared: the run is won.</summary>
    public bool IsWon { get; private set; }

    /// <summary>
    /// The ticks the world has run since it was made; paused ticks do not count. A clock for things that move by the
    /// tick alone, such as the showroom's portcullis (issue #540).
    /// </summary>
    public long Ticks { get; private set; }

    /// <summary>True when every player is dead before the run was won.</summary>
    public bool IsLost => !IsWon && Players.All(player => player.IsDead);

    /// <summary>The numbers of this run so far (a new world starts at 0).</summary>
    public RunStatistics Statistics { get; } = new();

    /// <summary>
    /// The summary of the finished run (won or lost) - handed out exactly once, for the end screen and the run log.
    /// Null while the run is still on and every time after it was taken, so a run is never logged twice.
    /// </summary>
    public RunSummary? TakeEndSummary()
    {
        if (_endSummaryTaken || !(IsWon || IsLost))
        {
            return null;
        }

        _endSummaryTaken = true;
        return Summary();
    }

    /// <summary>The run so far in one record - for the end screen and the run log.</summary>
    public RunSummary Summary() => new(
        Seed,
        IsWon ? RunOutcome.Won : IsLost ? RunOutcome.Lost : RunOutcome.Running,
        Statistics.Seconds,
        Statistics.GhoulsKilled,
        Statistics.BatsKilled,
        Statistics.DamageDealt,
        Statistics.DamageTaken,
        Player.Level,
        Statistics.RoomsCleared,
        _rooms.Count(room => room.IsArena),
        Statistics.SilverCollected,
        Statistics.GormSlain,
        Statistics.BossFightSeconds);

    /// <summary>The rooms of a generated level and what happened in them (empty for hand-made levels).</summary>
    public IReadOnlyList<RoomState> Rooms => _rooms;

    /// <summary>True if <paramref name="door"/> belongs to a discovered room - then it shows on the map (a cracked wall only once broken).</summary>
    public bool IsKnown(Door door)
    {
        ArgumentNullException.ThrowIfNull(door);

        // Doors without rooms (a hand-made test level) never show on a map, an unbroken cracked wall neither.
        return _rooms.Count > 0 && !IsUnbrokenWall(door) && (_rooms[door.RoomA].IsDiscovered || _rooms[door.RoomB].IsDiscovered);
    }

    /// <summary>True while <paramref name="enemy"/> sleeps: it waits motionless until its room is entered (or it is hit).</summary>
    public bool IsAsleep(ITarget enemy)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        return _asleep.Contains(enemy.Id);
    }

    /// <summary>
    /// The level as everything in the world sees it: closed doors are walls here. Opening a door replaces it
    /// with a new level in which that cell is free.
    /// </summary>
    public Level Level { get; private set; }

    /// <summary>The doors of the level, in the order they were given.</summary>
    public IReadOnlyList<Door> Doors => _doors;

    /// <summary>Gorm, the Chained - the boss of the level ('T'), or null in a level without one.</summary>
    public Gorm? Gorm => _gorm;

    /// <summary>The spiral staircase - null until the boss room is cleared, then rising in its middle.</summary>
    public Stairs? Stairs => _stairs;

    /// <summary>True while the boss room is sealed and Gorm lives: his health bar shows.</summary>
    public bool IsBossFightOn => _gorm is { IsDead: false } && _rooms.Any(room => room.Kind == RoomKind.Boss && room.IsSealed);

    /// <summary>The Well of Sirius of the level, or null if it has none (issue #438).</summary>
    public WellOfSirius? Well { get; }

    // The well's solid circle for players, if the level has one.
    private IEnumerable<CylinderBody> WellBodies => Well is null ? [] : [Well.Body];

    /// <summary>The chests of the level, in the order they were given (hidden ones included).</summary>
    public IReadOnlyList<Chest> Chests => _chests;

    /// <summary>The teleporters of the level, in the order they were given (their number is their place here).</summary>
    public IReadOnlyList<Teleporter> Teleporters => _teleporters;

    // Issue #436: a living player whose feet come within the radius of a locked teleporter unlocks it - for everyone.
    private void UnlockTeleporters(Player player)
    {
        if (player.IsDead)
        {
            return;
        }

        foreach (var teleporter in _teleporters.Where(teleporter => !teleporter.IsUnlocked && StandsOn(player, teleporter)))
        {
            teleporter.Unlock();
            _events.Add(new TeleporterUnlocked(teleporter.Id, player.Id, teleporter.Position));
        }
    }

    /// <summary>
    /// The unlocked teleporter <paramref name="player"/> stands on and could travel from right now - or null: when they
    /// are dead, stand on none (or only on a locked one), or are in a sealed room (there it rests until the fight ends).
    /// </summary>
    public Teleporter? TeleporterUnder(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.IsDead || IsInSealedRoom(player))
        {
            return null;
        }

        return _teleporters.FirstOrDefault(teleporter => teleporter.IsUnlocked && StandsOn(player, teleporter));
    }

    /// <summary>
    /// The destinations <paramref name="player"/> can see in the travel choice (issue #480): every unlocked teleporter in
    /// number order, each marked as the one they stand on, ready, or in a sealed room. Locked ones are left out. Empty
    /// when they could not travel at all (see <see cref="TeleporterUnder"/>).
    /// </summary>
    public IReadOnlyList<TravelChoice> TravelChoices(Player player)
    {
        if (TeleporterUnder(player) is not { } from)
        {
            return [];
        }

        return _teleporters
            .Where(teleporter => teleporter.IsUnlocked)
            .Select(teleporter => new TravelChoice(teleporter, TravelStateOf(teleporter, from)))
            .ToList();
    }

    private TravelState TravelStateOf(Teleporter teleporter, Teleporter from) =>
        ReferenceEquals(teleporter, from) ? TravelState.Here
        : IsInSealedRoom(teleporter) ? TravelState.Sealed
        : TravelState.Ready;

    private bool StandsOn(Player player, Teleporter teleporter) =>
        teleporter.Reaches(player.Position, Balance.Teleporters.Radius, Balance.Teleporters.Height);

    private bool IsInSealedRoom(Player player) => SealedRoomOf(player) is not null;

    // Issue #480, trap 3: in co-op another player's fight may have sealed the room a teleporter stands in.
    private bool IsInSealedRoom(Teleporter teleporter) =>
        _roomOfCell.TryGetValue((teleporter.Plan.X, teleporter.Plan.Z), out var room) && room.IsSealed;

    // Issue #480, trap 2: a strike or shot of this tick happens where the player stood; travelling in the same tick
    // would hit from the old place but knock back from the new one. The attack wins, the travel is refused.
    private static bool AttacksThisTick(Player player) =>
        player.Events.Any(e => e is MeleeSwung or ShotFired);

    // Issue #436, input TeleportTo: from the unlocked teleporter the player stands on to another unlocked one. Anything
    // else - no choice, an unknown number, a locked or the same teleporter, not standing on one, a sealed room here or
    // there, an attack in the same tick - does nothing.
    private void Teleport(Player player, int? target)
    {
        if (target is not { } id || id < 0 || id >= _teleporters.Count || TeleporterUnder(player) is not { } from)
        {
            return;
        }

        var to = _teleporters[id];
        if (!to.IsUnlocked || TravelStateOf(to, from) != TravelState.Ready || AttacksThisTick(player))
        {
            return;
        }

        player.TeleportTo(to.Position);
        _events.Add(new PlayerTeleported(player.Id, from.Id, to.Id, from.Position, to.Position));
    }

    // The chests that block the way: standing or rising, not those still waiting under the floor (decision record 0008).
    private IEnumerable<Chest> StandingChests => _chests.Where(chest => chest.RiseFraction > 0f);

    // The level ghouls and Gorm find their way in: EnemyLevel with the cells of standing chests (and the well) as walls, so they plan
    // a way round a chest instead of pushing against it. Not the cell a player stands in (on a chest) - the way to
    // them must stay findable. Built anew only when those cells change. It is only a map for planning: it has no
    // enemy starts (a chest may stand where an enemy started), and the player's start and the dummies' cells stay open.
    private Level EnemyPaths()
    {
        var open = Players.Select(player => Level.ToGrid(player.Position.X, player.Position.Z))
            .Append((Level.PlayerStartX, Level.PlayerStartZ))
            .Concat(Level.DummyPositions.Select(position => Level.ToGrid(position.X, position.Z)))
            .ToHashSet();
        var walls = StandingChests
            .Select(chest => Level.ToGrid(chest.Position.X, chest.Position.Z))
            .Concat(Well is null ? [] : [(Well.Plan.X, Well.Plan.Z)])
            .Where(cell => !open.Contains(cell))
            .ToList();
        if (_enemyPaths is null || !walls.SequenceEqual(_enemyPathWalls))
        {
            _enemyPathWalls = walls;
            _enemyPaths = walls.Count == 0
                ? _enemyLevel
                : _enemyLevel.WithEnemies([], []).WithCells(walls.Select(cell => (cell.X, cell.Z, Cell.Wall)));
        }

        return _enemyPaths;
    }

    /// <summary>All balancing values of this world. They never change while it runs.</summary>
    public Balance Balance { get; }

    public Player Player { get; }

    /// <summary>All players. One for now - enemies already pick their target from this list (online co-op later).</summary>
    public IReadOnlyList<Player> Players => [Player];

    /// <summary>True when every player is dead and the restart delay has passed: time to start the level again.</summary>
    public bool ShouldRestart => _ticksAllPlayersDead >= _restartTicks;

    /// <summary>The start value of this world's random numbers.</summary>
    public int Seed { get; }

    /// <summary>
    /// A fresh world for the same level: full health, every enemy back at its start - but with a new seed (taken
    /// from this one), so the bats fly differently than last time. The balancing values stay the same.
    /// </summary>
    public GameWorld Restarted() => new(_levelWithOpenDoors, new Random(Seed).Next(), Balance, _doorPlan, _roomPlan, _chestPlan, Well?.Plan, _teleporterPlan)
    {
        Surfaces = Surfaces,
    };

    /// <summary>
    /// What <paramref name="player"/> would use with F right now: the nearest usable thing they look at, within
    /// <see cref="ControlSettings.InteractionReach"/> and not behind a wall - or null. The HUD shows its prompt.
    /// </summary>
    public IInteractable? InteractionTarget(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.IsDead)
        {
            return null;
        }

        var eye = player.EyePosition;
        var direction = player.View.Forward;
        var reach = ControlSettings.InteractionReach;

        // A closed door is wall in Level, so the wall hit is its own front - a small margin lets it count.
        var wall = Raycast.AgainstLevel(Level, eye, direction, reach)?.Distance ?? float.MaxValue;
        IInteractable? nearest = null;
        var nearestDistance = float.MaxValue;
        foreach (var thing in Interactables.Where(thing => thing.CanInteract))
        {
            if (Raycast.AgainstBox(eye, direction, thing.BoxMin, thing.BoxMax) is { } distance
                && distance <= reach && distance <= wall + 0.01f && distance < nearestDistance)
            {
                nearest = thing;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    /// <summary>Everything that can be used with F: doors, chests, the stairs and equipment on the floor; people later.</summary>
    // The showroom's staircase is only on display: it is never offered (no level change there).
    private IEnumerable<IInteractable> Interactables =>
        _doors.Concat<IInteractable>(_chests).Concat(_equipmentDrops).Concat(_stairs is null || IsShowroom ? [] : [_stairs])
            .Concat(Well is null ? [] : [Well]);

    private void Interact(Player player)
    {
        switch (InteractionTarget(player))
        {
            case Door door when door.OfferFor(player).IsPossible:
                if (door.Kind == DoorKind.Locked)
                {
                    player.UseKey();
                    _events.Add(new DoorUnlocked(door.Id, player.Id));
                }

                OpenDoor(door, player);
                break;
            case Stairs stairs when stairs.CanInteract && !IsWon:
                IsWon = true;
                _events.Add(new StairsClimbed(player.Id));
                _events.Add(new RunWon());
                break;
            case Chest chest when chest.OfferFor(player).IsPossible:
                if (chest.IsLocked)
                {
                    player.UseKey();
                    chest.Unlock();
                    _events.Add(new ChestUnlocked(chest.Id, player.Id));
                }

                OpenChest(chest, player);
                break;
            case EquipmentDrop drop when drop.OfferFor(player).IsPossible:
                Take(drop, player);
                break;
            case WellOfSirius well:
                UseWell(well, player);
                break;
        }
    }

    // Provisional (issue #438): price, chances and the most throws are not decided - using the well only spends it.
    private void UseWell(WellOfSirius well, Player player)
    {
        if (well.Use())
        {
            _events.Add(new WellOfSiriusUsed(player.Id) { Place = well.Position });
        }
    }

    // Whoever takes a piece first gets it: it leaves the floor at once, so in online co-op a second player pressing F
    // in the same tick (later in the order of Players) finds nothing there any more. A weapon goes with its magazine into
    // a free ranged place, otherwise into the bag (issue #434); an item needs no place.
    private void Take(EquipmentDrop drop, Player taker)
    {
        switch (drop)
        {
            case WeaponDrop weapon when !taker.PickUp(weapon.Kind, weapon.Ranged):
                return;
            case ItemDrop item:
                taker.Take(item.Kind);
                break;
        }

        _equipmentDrops.Remove(drop);
        _events.Add(new EquipmentTaken(drop.Id, taker.Id, drop.Piece, drop.Position));
    }

    // The contents jump out towards the opener and fall to the floor in front of the chest - still in its cell, so
    // never into a wall: silver straight towards them, a blood vial a little to one side, equipment to the other.
    private void OpenChest(Chest chest, Player opener)
    {
        chest.Open();
        _events.Add(new ChestOpened(chest.Id, opener.Id, chest.Tier) { Place = chest.Position });

        var towards = FacingTowards(chest.Position, opener.Position);
        var lid = new Vector3(0f, Chest.Height, 0f);
        const float Out = 0.8f;
        if (chest.Plan.Silver > 0)
        {
            var place = chest.Position + (towards * Out) + lid;
            var pile = new Loot(_nextLootId++, LootKind.Silver, chest.Plan.Silver, place);
            _loot.Add(pile);
            _events.Add(new SilverDropped(pile.Id, pile.Amount, place));
        }

        if (chest.Plan.HasBloodVial)
        {
            var side = Vector3.Transform(towards, Matrix4x4.CreateRotationY(MathF.PI / 5f));
            var place = chest.Position + (side * Out) + lid;
            var vial = new Pickup(_nextPickupId++, ConsumableKind.BloodVial, place);
            _pickups.Add(vial);
            _events.Add(new PickupDropped(vial.Id, vial.Kind, place));
        }

        // Equipment from the loot pool (decision record 0008), at least of the chest's tier. Both the chance and the
        // draw come from the pool's own generator, so silver, keys and fights roll as before.
        if (LootPool.Roll(Balance.Chests.Of(chest.Tier).EquipmentChance))
        {
            var side = Vector3.Transform(towards, Matrix4x4.CreateRotationY(-MathF.PI / 5f));
            DropEquipment(LootPool.Draw(chest.Tier.LootTier()), chest.Plan.Silver, chest.Position + (side * Out) + lid);
        }
    }

    /// <summary>
    /// Lays <paramref name="piece"/> at <paramref name="place"/> - or, if the loot pool had nothing left
    /// (<paramref name="piece"/> is null), <paramref name="extraSilver"/> instead (decision record 0006).
    /// </summary>
    private void DropEquipment(Equipment? piece, int extraSilver, Vector3 place)
    {
        if (piece is null)
        {
            if (extraSilver > 0)
            {
                var pile = new Loot(_nextLootId++, LootKind.Silver, extraSilver, place);
                _loot.Add(pile);
                _events.Add(new SilverDropped(pile.Id, extraSilver, place));
            }

            return;
        }

        EquipmentDrop drop = piece.Weapon is { } weapon
            ? new WeaponDrop(_nextEquipmentDropId++, weapon, place)
            : new ItemDrop(_nextEquipmentDropId++, piece.Item!.Value, place);
        _equipmentDrops.Add(drop);
        _events.Add(new EquipmentDropped(drop.Id, piece, place));
    }

    // A cleared combat room lets its chest rise from the floor.
    private void RaiseChestsIn(RoomState room)
    {
        foreach (var chest in _chests.Where(chest => chest.Plan.Room == room.Index && chest.StartRising()))
        {
            _events.Add(new ChestRose(chest.Id));
        }
    }

    private void OpenDoor(Door door, Player player)
    {
        door.Open(player.Position);
        UpdateLevel();
        _events.Add(new DoorOpened(door.Id) { Place = (door.BoxMin + door.BoxMax) / 2f });
        ReportMotion(door, door.SlamsShut, closing: false);
    }

    // After doors changed: a new level in which exactly the blocking doors are walls, for everybody.
    private void UpdateLevel()
    {
        Level = WithClosedDoors();
        _enemyLevel = WithSafeRoomsWalled();
        _enemyPaths = null;
        foreach (var player in Players)
        {
            player.ChangeLevel(Level);
        }
    }

    /// <summary>
    /// The level as enemies know it: <see cref="Level"/>, but the floor and doorways of treasure and secret rooms are
    /// wall. Enemies walk, fly, look and find their way in this one - so they never go into a safe room, nor see (or
    /// spit at) a player inside, however they were woken.
    /// </summary>
    public Level EnemyLevel => _enemyLevel;

    private Level WithSafeRoomsWalled() => _safeCells.Count == 0
        ? Level
        : Level.WithCells(_safeCells.Select(cell => (cell.X, cell.Z, Cell.Wall)));

    private Level WithUnbrokenWalls() => _doors.Any(IsUnbrokenWall)
        ? _levelWithStandingColumns.WithCells(_doors.Where(IsUnbrokenWall).SelectMany(WallCells))
        : _levelWithStandingColumns;

    private static bool IsUnbrokenWall(Door door) => door.Kind == DoorKind.Cracked && !door.IsOpen;

    private Level WithClosedDoors() => _doors.Count == 0
        ? _levelWithStandingColumns
        : _levelWithStandingColumns.WithCells(_doors.Where(door => door.Blocks).SelectMany(WallCells));

    /// <summary>
    /// Issue #590: once a column has broken under Gorm's blows, it leaves the level everybody plays in - it no longer
    /// blocks the way, sight or shots, and is no longer drawn (a new <see cref="LevelAsBuilt"/>).
    /// </summary>
    private void RemoveBrokenColumns()
    {
        if (!_levelWithStandingColumns.Columns.Any(_columnWear.IsBroken))
        {
            return;
        }

        _levelWithStandingColumns = _levelWithOpenDoors.WithColumns(
            _levelWithOpenDoors.Columns.Where(column => !_columnWear.IsBroken(column)));
        UpdateLevel();
        LevelAsBuilt = WithUnbrokenWalls();
    }

    // Every cell of a door that blocks is wall (issue #566: the boss door has two).
    private static IEnumerable<(int X, int Z, Cell Cell)> WallCells(Door door) => door.Cells.Select(cell => (cell.X, cell.Z, Cell.Wall));

    // A player who stands in a room with the whole body enters it. A combat or boss room with living enemies
    // becomes an arena: grates fall at all its doors and the enemies wake up.
    private void EnterRooms(Player player)
    {
        var cell = Level.ToGrid(player.Position.X, player.Position.Z);
        if (!_roomOfCell.TryGetValue(cell, out var room))
        {
            return;
        }

        // On the map from the first step onto its floor; entered (and maybe sealed) only with the whole body inside.
        room.Discover();
        if (room.IsEntered || TouchesDoorOf(room, player))
        {
            return;
        }

        room.Enter();
        if (!room.IsArena)
        {
            return;
        }

        WakeUp(room);
        if (room.Enemies.All(IsDead))
        {
            room.Clear();
            _events.Add(new RoomCleared(room.Index));
            MaybeDropKeyIn(room);
            RaiseChestsIn(room);
            RaiseStairsIfBoss(room);
            return;
        }

        // Issue #558, decision of the human: the doors shut in this tick - the grates only start to fall, but nobody gets
        // out any more. An enemy in a doorway (one that followed the player) is pushed into the arena. An open boss door
        // only starts to swing shut here and blocks from its hard stop on (issue #566, see TickDoors).
        foreach (var id in room.DoorIds)
        {
            ReportMotion(_doors[id], _doors[id].Bar(), closing: true);
            PushOutOf(_doors[id], room);
        }

        room.Seal();

        UpdateLevel();
        _events.Add(new RoomSealed(room.Index));
        if (room.ReleaseNextWave() is { } wave)
        {
            Release(room, wave);
        }

        if (room.Kind == RoomKind.Boss && _gorm is { IsDead: false })
        {
            _events.Add(new BossFightStarted());
        }
    }

    // When the last enemy of an arena is dead, its grates rise. The door the player came through is open (they walked
    // through it), but it stays shut until its grate hangs above grates.blockHeight again (issue #558); a door without
    // a grate (the boss room) is free at once. The others open with F as before. While a combat room still has a
    // hidden wave, the next one comes out instead once all that came out before are dead (today there is only one).
    private void ClearRooms()
    {
        foreach (var room in _rooms.Where(room => room.IsSealed))
        {
            var released = room.Waves.Take(room.WavesReleased).SelectMany(wave => wave);
            if (room.WavesReleased < room.WaveCount && released.All(IsDead))
            {
                Release(room, room.ReleaseNextWave()!);
                continue;
            }

            if (!room.Enemies.All(IsDead))
            {
                continue;
            }

            LiftGrates(room);
            room.Clear();
            UpdateLevel();
            _events.Add(new RoomCleared(room.Index));
            MaybeDropKeyIn(room);
            RaiseChestsIn(room);
            RaiseStairsIfBoss(room);
        }
    }

    // The grates of a room rise - one event per grate that starts to move. (A door shared with a second sealed room
    // cannot occur: it shuts at once, so no player gets through to seal the other room while this one is sealed.)
    private void LiftGrates(RoomState room)
    {
        foreach (var id in room.DoorIds)
        {
            ReportMotion(_doors[id], _doors[id].Lift(), closing: false);
        }
    }

    // One event per door whose grate or boss door (issue #566) started to move.
    private void ReportMotion(Door door, bool started, bool closing)
    {
        if (started)
        {
            _events.Add(MotionStarted(door, closing) with { Place = (door.BoxMin + door.BoxMax) / 2f });
        }
    }

    private static GameEvent MotionStarted(Door door, bool closing) => door.SlamsShut
        ? new DoorSwingStarted(door.Id, closing)
        : new GrateStarted(door.Id, closing);

    // Issue #558: every door runs its own clock. A rising grate frees the way once it hangs high enough - then the
    // level changes for everybody. A boss door that reaches the hard stop of its slam (issue #566) shuts the way.
    private void TickDoors()
    {
        var changed = false;
        foreach (var door in _doors)
        {
            if (door.Tick())
            {
                changed = true;
                ShutBehind(door);
            }
        }

        if (changed)
        {
            UpdateLevel();
        }
    }

    // Issue #598: the stone portal of every boss door its model fits (DoorFrameLook.Fits - the same rule that decides
    // whether the portal is drawn, so stone that blocks is always stone that is seen). Its middle is the middle of the
    // door's cells, as for the drawing. A boss door of one cell (the showroom, a hand-made level) gets the placeholder
    // and no stone. A level that has these portals already (a restart passes the level on) stays the same level.
    private static Level WithBossPortals(Level level, IReadOnlyList<LevelDoor> doors)
    {
        var portals = doors
            .Where(door => door.Kind == DoorKind.Boss && DoorFrameLook.Fits(door.Kind, door.Cells.Count))
            .Select(door =>
            {
                var middle = door.Cells.Aggregate(Vector3.Zero, (sum, cell) => sum + level.CellCenter(cell.X, cell.Z)) / door.Cells.Count;
                return new BossPortal(middle.X, middle.Z, middle.Y, door.Side is RoomSide.North or RoomSide.South);
            })
            .ToList();
        return portals.SequenceEqual(level.Portals) ? level : level.WithPortals(portals);
    }

    // A door that has just begun to block (the boss door at the hard stop of its slam, issue #566) pushes everybody in
    // its doorway into the sealed room behind it - nobody is caught in it.
    private void ShutBehind(Door door)
    {
        if (door.Blocks && _rooms.FirstOrDefault(room => room.IsSealed && room.DoorIds.Contains(door.Id)) is { } arena)
        {
            PushOutOf(door, arena);
        }
    }

    // The sealed room a player stands in (by the cell under their feet), or null.
    private RoomState? SealedRoomOf(Player player) =>
        _roomOfCell.TryGetValue(Level.ToGrid(player.Position.X, player.Position.Z), out var room) && room.IsSealed ? room : null;

    // Issue #567, decision of the human: whoever is in the boss room when it seals is in - no way back until the fight
    // is won, also not in the ticks before the hard stop of the slam, while the leaves still swing and the door does
    // not block yet (it blocks only from the stop on, issue #566 - for sight, shots and enemies that stays so). A
    // player who stood in a sealed room at the start of the tick and now reaches into one of its doorways that does
    // not block yet is held back on the threshold, as by a wall - walking, jumping or dashing alike. Only a player who
    // was inside is held: one who comes from outside (later in co-op, ADR 0003) is not. Nothing moves a body as far
    // as through a whole door cell (2 m) in one tick - a dash covers well under half a metre - so touching the doorway
    // is caught before passing it. Grated doors block from the sealing tick on, so for them this never does anything.
    private void KeepInSealedRoom(Player player, RoomState? room)
    {
        if (room is null)
        {
            return;
        }

        var radius = BodySizes.Player.Radius;
        foreach (var door in room.DoorIds.Select(id => _doors[id]).Where(door => !door.Blocks && Overlaps(door, player.Position, radius)))
        {
            player.HoldAt(OnThreshold(room, door, player.Position, radius));
        }
    }

    // A door that shuts pushes every enemy in its cell into the arena - nobody is hurt or caught in it. A player who
    // seals the room is out of its doorways (a player enters a room only with the whole body out of them) and is kept
    // out of them until the fight ends (issue #567, KeepInSealedRoom). Every kind of enemy, through the one list of
    // them (issue #572).
    //
    // PROVISIONAL (decision of the lead, issue #572; the human judges it at the play test): this pulls in strangers,
    // too - an enemy from the corridor or the start room that followed a player into the doorway ends up in the arena.
    // It does not count for the arena (not in room.Enemies), so it does not hold up the clearing; but it fights in there.
    // The other way (pushed back to where it came from) would leave it waiting outside the grate.
    //
    // Issue #566: the boss door shuts only at the hard stop of its slam. A player who was inside cannot step back into its
    // doorway (issue #567); every other player there by then is pushed into the boss room the same way (co-op, #584).
    private void PushOutOf(Door door, RoomState arena)
    {
        foreach (var enemy in Enemies.Where(enemy => Overlaps(door, enemy.Body.Position, enemy.Body.Radius)))
        {
            enemy.PlaceAt(PushedInto(arena, door, enemy.Body.Position, enemy.Body.Radius));
        }

        foreach (var player in Players.Where(player => Overlaps(door, player.Position, BodySizes.Player.Radius)))
        {
            player.PlaceAt(PushedInto(arena, door, player.Position, BodySizes.Player.Radius));
        }
    }

    // Just out of the door cell along the passage, into the arena. Across the passage it stays where it was, but inside
    // the doorway's width, so it never ends up in a wall.
    private Vector3 PushedInto(RoomState arena, Door door, Vector3 at, float radius)
    {
        var threshold = OnThreshold(arena, door, at, radius);
        var across = PushedAcross(door, PassesAlongZ(door) ? at.X : at.Z, radius);
        return PassesAlongZ(door) ? threshold with { X = across } : threshold with { Z = across };
    }

    // Across the passage a pushed body stays inside the doorway's width - or, where a boss portal frames the doorway
    // (issue #598), inside its opening, clear of the plinths and buttresses beside it (BossPortal.PushedAcross).
    private float PushedAcross(Door door, float across, float radius)
    {
        var (x, z) = door.Cells[0];
        foreach (var portal in Level.Portals.Where(portal => portal.Frames(x, z)))
        {
            return portal.PushedAcross(across, radius);
        }

        return PassesAlongZ(door)
            ? Math.Clamp(across, door.BoxMin.X + radius, door.BoxMax.X - radius)
            : Math.Clamp(across, door.BoxMin.Z + radius, door.BoxMax.Z - radius);
    }

    // Just out of the door cell along the passage, on the arena's side; across the passage it stays where it was - as a
    // wall stops a body, which slides along it (issue #567: past the edge of the doorway the threshold goes on as wall).
    private static Vector3 OnThreshold(RoomState arena, Door door, Vector3 at, float radius)
    {
        const float Gap = 0.01f;
        var towardsB = door.Side is RoomSide.South or RoomSide.East;
        var high = (door.RoomB == arena.Index) == towardsB;
        return PassesAlongZ(door)
            ? at with { Z = high ? door.BoxMax.Z + radius + Gap : door.BoxMin.Z - radius - Gap }
            : at with { X = high ? door.BoxMax.X + radius + Gap : door.BoxMin.X - radius - Gap };
    }

    // True if the passage through the door runs along Z (a door in a north or south wall).
    private static bool PassesAlongZ(Door door) => door.Side is RoomSide.North or RoomSide.South;

    // A cleared boss room lets the spiral staircase rise in its middle (decision record 0007). A room is cleared only
    // once, and there is one boss room - so it rises only once. Climbing it (F) wins the run.
    private void RaiseStairsIfBoss(RoomState room)
    {
        if (room.Kind != RoomKind.Boss)
        {
            return;
        }

        var (cellX, cellZ) = Stairs.CellIn(room.Cells);
        _stairs = new Stairs(Level.CellCenter(cellX, cellZ), Balance.Gorm.StairsRiseTime);
        _events.Add(new StairsRose());
    }

    private void ReportLoss()
    {
        if (IsLost && !_lossReported)
        {
            _lossReported = true;
            _events.Add(new RunLost());
        }
    }

    // Wakes the enemies of a room that are there - not those still hidden in the floor or the ceiling (they wake when
    // their wave comes out).
    private void WakeUp(RoomState room)
    {
        foreach (var enemy in room.Enemies.Where(enemy => !IsHidden(enemy)))
        {
            _asleep.Remove(enemy.Id);
        }
    }

    private static bool IsHidden(ITarget enemy) => enemy switch
    {
        Ghoul ghoul => ghoul.Emergence.IsHidden,
        Bat bat => bat.Emergence.IsHidden,
        _ => false,
    };

    // Only an enemy that is fully out acts (issue #424: while it comes out it does not attack).
    private static bool IsOut(ITarget enemy) => enemy switch
    {
        Ghoul ghoul => ghoul.Emergence.IsPresent,
        Bat bat => bat.Emergence.IsPresent,
        _ => true,
    };

    private bool TouchesDoorOf(RoomState room, Player player) =>
        room.DoorIds.Any(id => Overlaps(_doors[id], player.Position, BodySizes.Player.Radius));

    // Whether a round body of radius at a place reaches into the door's cell (seen from above).
    private static bool Overlaps(Door door, Vector3 at, float radius)
    {
        var dx = at.X - Math.Clamp(at.X, door.BoxMin.X, door.BoxMax.X);
        var dz = at.Z - Math.Clamp(at.Z, door.BoxMin.Z, door.BoxMax.Z);
        return dx * dx + dz * dz < radius * radius;
    }

    private static bool IsDead(ITarget enemy) => enemy switch
    {
        Ghoul ghoul => ghoul.IsDead,
        Bat bat => bat.IsDead,
        _ => !enemy.IsHittable,
    };

    public IReadOnlyList<TrainingDummy> Dummies => _dummies;

    /// <summary>The ghouls, dead ones included (until the end of the level).</summary>
    public IReadOnlyList<Ghoul> Ghouls => _ghouls;

    /// <summary>Consumables lying in the level (taken ones are removed after each tick).</summary>
    public IReadOnlyList<Pickup> Pickups => _pickups;

    /// <summary>
    /// Blood vials lying in rooms already discovered - the map shows them, so a vial left behind is easy to find again.
    /// Hand-made levels have no rooms and so no map.
    /// </summary>
    public IEnumerable<Pickup> KnownBloodVials => _pickups.Where(pickup =>
        pickup.Kind == ConsumableKind.BloodVial
        && _roomOfCell.TryGetValue(Level.ToGrid(pickup.Position.X, pickup.Position.Z), out var room)
        && room.IsDiscovered);

    /// <summary>
    /// Unopened chests in rooms already discovered that are there to see - one that still waits under the floor for
    /// its room to be cleared is not (yet), an opened one no longer. The map shows them with their tier and lock.
    /// </summary>
    public IEnumerable<Chest> KnownChests => _chests.Where(chest =>
        !chest.IsOpen
        && chest.RiseFraction > 0f
        && _roomOfCell.TryGetValue(Level.ToGrid(chest.Position.X, chest.Position.Z), out var room)
        && room.IsDiscovered);

    /// <summary>Silver and keys lying in the level (taken ones are removed after each tick).</summary>
    public IReadOnlyList<Loot> Loot => _loot;

    /// <summary>Equipment lying in the level - weapons and items, in the order they came there. Taken with F (#317).</summary>
    public IReadOnlyList<EquipmentDrop> EquipmentDrops => _equipmentDrops;

    /// <summary>The weapons among <see cref="EquipmentDrops"/> - e.g. dropped from an inventory (#193).</summary>
    public IReadOnlyList<WeaponDrop> WeaponDrops => _equipmentDrops.OfType<WeaponDrop>().ToList();

    /// <summary>The silver among <see cref="Loot"/>.</summary>
    public IReadOnlyList<Loot> SilverPiles => _loot.Where(loot => loot.Kind == LootKind.Silver).ToList();

    /// <summary>
    /// Every enemy that died since the last tick gets exactly one chance to drop a consumable (its own chance from
    /// <see cref="DropsBalance"/>, rolled with the world's random generator) and may drop silver
    /// (<see cref="SilverBalance"/>) and a key (<see cref="KeysBalance"/>), both rolled with the loot's own random
    /// generator. Everything appears where it died.
    /// </summary>
    private void DropFromTheDead()
    {
        foreach (var ghoul in _ghouls.Where(ghoul => ghoul.IsDead && _dropsRolled.Add(ghoul.Id)))
        {
            MaybeDrop(Balance.Drops.GhoulChance, ghoul.Position);
            DropSilver(Balance.Silver.GhoulChance, Balance.Silver.GhoulMin, Balance.Silver.GhoulMax, ghoul.Position);
            MaybeDropKey(Balance.Keys.EnemyChance, ghoul.Position);
        }

        foreach (var bat in _bats.Where(bat => bat.IsDead && _dropsRolled.Add(bat.Id)))
        {
            MaybeDrop(Balance.Drops.BatChance, bat.Position);
            DropSilver(Balance.Silver.BatChance, Balance.Silver.BatMin, Balance.Silver.BatMax, bat.Position);
            MaybeDropKey(Balance.Keys.EnemyChance, bat.Position);
        }
    }

    /// <summary>
    /// Once Gorm is dead: a big silver pile, a blood vial and a piece of equipment (Royal or Vampiric, decision record
    /// 0008) where he fell (decision record 0007).
    /// </summary>
    private void DropGormsLoot()
    {
        if (_gorm is not { IsDead: true } || _gormLootDropped)
        {
            return;
        }

        _gormLootDropped = true;
        var silver = _lootRandom.Next(Balance.Gorm.SilverMin, Balance.Gorm.SilverMax + 1);
        if (silver > 0)
        {
            var pile = new Loot(_nextLootId++, LootKind.Silver, silver, _gorm.Position);
            _loot.Add(pile);
            _events.Add(new SilverDropped(pile.Id, silver, _gorm.Position));
        }

        var vialPlace = FreePlace(_gorm.Position + (_gorm.Facing * 1.2f), BodySizes.Pickup.Radius, _gorm.Position);
        var vial = new Pickup(_nextPickupId++, ConsumableKind.BloodVial, vialPlace);
        _pickups.Add(vial);
        _events.Add(new PickupDropped(vial.Id, vial.Kind, vialPlace));

        // Behind him and closer than his own radius: his body kept that spot clear of walls, so the piece never lies in
        // one. The tier and the draw come from the loot pool's own generator; the tier is already rolled, so no bonus.
        var tier = LootPool.Roll(Balance.Gorm.EquipmentVampiricChance) ? ItemTier.Vampiric : ItemTier.Royal;
        var behindHim = OutsideTheStairs(_gorm.Position - (_gorm.Facing * GormEquipmentDistance), _gorm.Position, -_gorm.Facing);
        var equipmentPlace = FreePlace(behindHim, EquipmentDrop.BoxRadius, _gorm.Position);
        DropEquipment(LootPool.Draw(tier, mayRise: false), Balance.Gorm.ExtraSilver, equipmentPlace);
    }

    /// <summary>How far behind Gorm his equipment lies (metres) - less than his body's radius, so never in a wall.</summary>
    public const float GormEquipmentDistance = BodySizes.Gorm.Radius * 0.7f;

    /// <summary>
    /// The gap (metres) between the box of Gorm's piece and the box of the staircase, when he died next to where it
    /// rises. With it, F never finds the stairs nearer than the piece for a player looking at it from outside.
    /// </summary>
    public const float StairsClearance = 0.1f;

    // Interaction picks the nearest box a player looks at, and inside the staircase's box that is the stairs (issue
    // #338). So a piece whose box would touch the staircase's box moves out: away from the stairs' middle (straight
    // behind Gorm if he died right on it), just far enough, and only onto the boss room's own floor at Gorm's height -
    // every corner of its box on such a cell, so never in a wall. If no direction fits (a room too small), it stays.
    private Vector3 OutsideTheStairs(Vector3 place, Vector3 gormPosition, Vector3 behind)
    {
        if (_rooms.FirstOrDefault(room => room.Kind == RoomKind.Boss) is not { } boss)
        {
            return place;
        }

        var (stairsX, stairsZ) = Stairs.CellIn(boss.Cells);
        var stairs = Level.CellCenter(stairsX, stairsZ);
        var reach = Stairs.Radius + EquipmentDrop.BoxRadius + StairsClearance;
        if (MathF.Max(MathF.Abs(place.X - stairs.X), MathF.Abs(place.Z - stairs.Z)) >= reach)
        {
            return place;
        }

        var away = new Vector2(place.X - stairs.X, place.Z - stairs.Z);
        if (away.LengthSquared() < 1e-6f)
        {
            away = new Vector2(behind.X, behind.Z);
        }

        var start = MathF.Atan2(away.X, away.Y);
        for (var step = 0; step < 16; step++)
        {
            // 0, +22.5°, -22.5°, +45°, ... - the nearest directions to "away" first.
            var angle = start + ((step + 1) / 2 * (step % 2 == 1 ? 1 : -1) * MathF.PI / 8f);
            var direction = new Vector2(MathF.Sin(angle), MathF.Cos(angle));
            var distance = reach / MathF.Max(MathF.Abs(direction.X), MathF.Abs(direction.Y));
            var candidate = new Vector3(stairs.X + (direction.X * distance), gormPosition.Y, stairs.Z + (direction.Y * distance));
            if (OnFloorOf(boss, candidate, EquipmentDrop.BoxRadius, gormPosition.Y))
            {
                return candidate;
            }
        }

        return place;
    }

    /// <summary>
    /// The gap (metres) between a piece of Gorm's loot and a standing column (issue #608): a piece never touches one, so
    /// a player walking round the column reaches it and F finds it.
    /// </summary>
    public const float ColumnClearance = 0.1f;

    // A piece of Gorm's loot (a square of half-width halfWidth) must lie where it can be taken: on open floor at his
    // height, clear of every standing column and of the staircase (issue #608). Where it would not, it turns round him
    // at the same distance - the nearest directions first - to the first place that is free. His own body kept the
    // ground under him clear, so turning stays close to where he fell. If no direction is free, the piece stays where
    // it would have lain, even in a column (in generated boss rooms this never happened: review of PR #613).
    private Vector3 FreePlace(Vector3 place, float halfWidth, Vector3 gormPosition)
    {
        if (IsFreeFor(place, halfWidth, gormPosition.Y))
        {
            return place;
        }

        var offset = new Vector2(place.X - gormPosition.X, place.Z - gormPosition.Z);
        var distance = offset.Length();
        if (distance < 1e-4f)
        {
            return place;
        }

        var start = MathF.Atan2(offset.X, offset.Y);
        for (var step = 1; step < 16; step++)
        {
            // +22.5°, -22.5°, +45°, ... - the nearest directions to where it should have lain first.
            var angle = start + ((step + 1) / 2 * (step % 2 == 1 ? 1 : -1) * MathF.PI / 8f);
            var candidate = new Vector3(
                gormPosition.X + (MathF.Sin(angle) * distance), place.Y, gormPosition.Z + (MathF.Cos(angle) * distance));
            if (IsFreeFor(candidate, halfWidth, gormPosition.Y))
            {
                return candidate;
            }
        }

        return place;
    }

    // True if a square of half-width halfWidth around position lies on open floor at floorHeight (in the boss room, if
    // there is one), keeps ColumnClearance from every standing column and does not reach the staircase's box.
    private bool IsFreeFor(Vector3 position, float halfWidth, float floorHeight)
    {
        var flat = new Vector2(position.X, position.Z);
        foreach (var column in Level.Columns)
        {
            var nearest = Vector2.Clamp(column.Centre, flat - new Vector2(halfWidth), flat + new Vector2(halfWidth));
            if (Vector2.Distance(nearest, column.Centre) < Column.Radius + ColumnClearance)
            {
                return false;
            }
        }

        if (InPortalStone(flat, halfWidth))
        {
            return false;
        }

        if (_rooms.FirstOrDefault(room => room.Kind == RoomKind.Boss) is not { } boss)
        {
            return OnFloorOf(null, position, halfWidth, floorHeight);
        }

        var (stairsX, stairsZ) = Stairs.CellIn(boss.Cells);
        var stairs = Level.CellCenter(stairsX, stairsZ);
        var reach = Stairs.Radius + halfWidth + StairsClearance;
        return MathF.Max(MathF.Abs(position.X - stairs.X), MathF.Abs(position.Z - stairs.Z)) >= reach
            && OnFloorOf(boss, position, halfWidth, floorHeight);
    }

    // True if the square of half-width halfWidth around flat reaches into the stone of a boss door's portal: loot there
    // would lie in the jamb or on the plinth (issue #627).
    private bool InPortalStone(Vector2 flat, float halfWidth) =>
        Level.Portals.Any(portal => portal.Covers(flat, halfWidth));

    // True if every corner of the square of half-width radius around position lies on an open cell whose floor is at
    // floorHeight - a cell of the room, or in a level without a boss room (room null) any cell of the level.
    private bool OnFloorOf(RoomState? room, Vector3 position, float radius, float floorHeight)
    {
        foreach (var (dx, dz) in new[] { (-1f, -1f), (-1f, 1f), (1f, -1f), (1f, 1f) })
        {
            var cell = Level.ToGrid(position.X + (dx * radius), position.Z + (dz * radius));
            var inside = room?.Cells.Contains(cell) ?? Level.IsInside(cell.X, cell.Z);
            if (!inside || Level[cell.X, cell.Z].IsSolid
                || MathF.Abs(Level[cell.X, cell.Z].FloorHeight - floorHeight) > 0.01f)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// With <paramref name="chance"/>, drops a pile worth between <paramref name="min"/> and <paramref name="max"/>
    /// (both included) - nothing for 0.
    /// </summary>
    private void DropSilver(float chance, int min, int max, Vector3 position)
    {
        if (_lootRandom.NextDouble() >= chance)
        {
            return;
        }

        var amount = _lootRandom.Next(min, max + 1);
        if (amount <= 0)
        {
            return;
        }

        var pile = new Loot(_nextLootId++, LootKind.Silver, amount, position);
        _loot.Add(pile);
        _events.Add(new SilverDropped(pile.Id, amount, position));
    }

    /// <summary>With <paramref name="chance"/>, drops one key at <paramref name="position"/>.</summary>
    private void MaybeDropKey(float chance, Vector3 position)
    {
        if (_lootRandom.NextDouble() >= chance)
        {
            return;
        }

        var key = new Loot(_nextLootId++, LootKind.Key, 1, position);
        _loot.Add(key);
        _events.Add(new KeyDropped(key.Id, position));
    }

    /// <summary>A cleared room may leave a key, on the floor cell nearest to its middle.</summary>
    private void MaybeDropKeyIn(RoomState room)
    {
        var middleX = (float)room.Cells.Average(cell => cell.X);
        var middleZ = (float)room.Cells.Average(cell => cell.Z);
        var (x, z) = room.Cells
            .OrderBy(cell => ((cell.X - middleX) * (cell.X - middleX)) + ((cell.Z - middleZ) * (cell.Z - middleZ)))
            .ThenBy(cell => cell.Z)
            .ThenBy(cell => cell.X)
            .First();
        var floor = new Vector3((x + 0.5f) * Level.CellSize, Level[x, z].FloorHeight, (z + 0.5f) * Level.CellSize);
        MaybeDropKey(Balance.Keys.RoomChance, floor);
    }

    /// <summary>Loot falls to the floor; a living player who touches it takes it (always - nothing of value is wasted).</summary>
    private void MoveLoot()
    {
        foreach (var loot in _loot)
        {
            loot.Fall(Level, Balance.Player.Gravity);
            var taker = Players.FirstOrDefault(player => !player.IsDead && BodiesTouch(player.Body, loot.Body));
            if (taker is null)
            {
                continue;
            }

            loot.IsTaken = true;
            switch (loot.Kind)
            {
                case LootKind.Silver:
                    taker.AddSilver(loot.Amount);
                    _events.Add(new SilverTaken(loot.Id, taker.Id, loot.Amount));
                    break;
                case LootKind.Key:
                    taker.AddKeys(loot.Amount);
                    _events.Add(new KeyTaken(loot.Id, taker.Id));
                    break;
            }
        }

        _loot.RemoveAll(loot => loot.IsTaken);
    }

    private void MaybeDrop(float chance, Vector3 position)
    {
        if (_random.NextDouble() >= chance)
        {
            return;
        }

        // Which consumable: a second roll, only for a drop (so a kill without a drop costs one number, as before).
        var kind = _random.NextDouble() < Balance.Drops.ConsecratedAmmoShare ? ConsumableKind.ConsecratedAmmo : ConsumableKind.BloodVial;
        var pickup = new Pickup(_nextPickupId++, kind, position);
        _pickups.Add(pickup);
        _events.Add(new PickupDropped(pickup.Id, pickup.Kind, position));
    }

    /// <summary>
    /// Pickups fall to the floor; a living player who touches one takes it - if it does them any good
    /// (a blood vial stays lying while the player's health is full; consecrated ammunition is always taken).
    /// </summary>
    private void MovePickups()
    {
        foreach (var pickup in _pickups)
        {
            pickup.Fall(Level, Balance.Player.Gravity);
            var taker = Players.FirstOrDefault(player => !player.IsDead && BodiesTouch(player.Body, pickup.Body) && CanUse(player, pickup));
            if (taker is null)
            {
                continue;
            }

            pickup.IsTaken = true;
            _events.Add(new PickupTaken(pickup.Id, taker.Id, pickup.Kind));
            Use(taker, pickup);
        }

        _pickups.RemoveAll(pickup => pickup.IsTaken);
    }

    private static bool CanUse(Player player, Pickup pickup) => pickup.Kind switch
    {
        ConsumableKind.BloodVial => player.Health < player.MaxHealth,
        _ => true,
    };

    private void Use(Player player, Pickup pickup)
    {
        switch (pickup.Kind)
        {
            case ConsumableKind.BloodVial:
                player.Heal(Balance.BloodVial.Heal, _events);
                break;
            case ConsumableKind.ConsecratedAmmo:
                player.Consecrate(Balance.ConsecratedAmmo.Duration);
                break;
        }
    }

    /// <summary>Do two upright cylinders touch? (Close enough on the ground and overlapping in height.)</summary>
    private static bool BodiesTouch(CylinderBody a, CylinderBody b) =>
        new Vector2(a.Position.X - b.Position.X, a.Position.Z - b.Position.Z).Length() <= a.Radius + b.Radius
        && a.Position.Y <= b.Position.Y + b.Height
        && b.Position.Y <= a.Position.Y + a.Height;

    /// <summary>The projectiles in flight (gone ones are removed after each tick).</summary>
    public IReadOnlyList<Projectile> Projectiles => _projectiles;

    /// <summary>
    /// Fires a blood bolt (speed, damage, size and lifetime from this world's <see cref="Balance"/>) from
    /// <paramref name="origin"/> in <paramref name="direction"/>. It hits only players, never other enemies or dummies.
    /// </summary>
    public Projectile Fire(Vector3 origin, Vector3 direction)
    {
        var bolt = Balance.BloodBolt;
        return Fire(origin, direction, bolt.Speed, bolt.Damage, bolt.Radius, bolt.LifeTime);
    }

    /// <summary>
    /// Fires a projectile from <paramref name="origin"/> in <paramref name="direction"/> - for enemies (and tests).
    /// It hits only players, never other enemies or dummies.
    /// </summary>
    public Projectile Fire(Vector3 origin, Vector3 direction, float speed, int damage, float radius, float lifeSeconds)
    {
        var projectile = new Projectile(_nextProjectileId++, origin, Vector3.Normalize(direction) * speed, damage, radius, lifeSeconds);
        _projectiles.Add(projectile);
        _events.Add(new ProjectileFired(projectile.Id, origin));
        return projectile;
    }

    /// <summary>
    /// Moves every projectile one tick, in small steps so it cannot jump through a thin target: the first living
    /// player it touches takes its damage; a wall, the floor or the ceiling stops it; its lifetime runs out.
    /// </summary>
    private void MoveProjectiles()
    {
        const float SubStep = 0.05f;
        foreach (var projectile in _projectiles)
        {
            projectile.PreviousPosition = projectile.Position;
            var travel = projectile.Velocity * SimulationClock.SecondsPerTick;
            var steps = Math.Max(1, (int)MathF.Ceiling(travel.Length() / SubStep));
            var lastFree = projectile.Position;

            for (var step = 1; step <= steps && !projectile.IsGone; step++)
            {
                var point = projectile.PreviousPosition + travel * ((float)step / steps);

                var victim = Players.FirstOrDefault(player => !player.IsDead && Touches(point, projectile.Radius, player.Body));
                if (victim is not null)
                {
                    victim.TakeDamage(projectile.Damage, _events);
                    _events.Add(new ProjectileHitPlayer(projectile.Id, victim.Id, point));
                    projectile.Position = point;
                    projectile.IsGone = true;
                }
                else if (LineOfSight.IsInsideGeometry(Level, point))
                {
                    _events.Add(new ProjectileImpact(projectile.Id, lastFree));
                    projectile.Position = lastFree;
                    projectile.IsGone = true;
                }
                else
                {
                    projectile.Position = point;
                    lastFree = point;
                }
            }

            if (!projectile.IsGone && --projectile.TicksLeft <= 0)
            {
                projectile.IsGone = true;
            }
        }

        _projectiles.RemoveAll(projectile => projectile.IsGone);
    }

    /// <summary>Does a ball at <paramref name="centre"/> touch the upright cylinder <paramref name="body"/>?</summary>
    private static bool Touches(Vector3 centre, float radius, CylinderBody body)
    {
        var flat = new Vector2(centre.X - body.Position.X, centre.Z - body.Position.Z).Length();
        return flat <= body.Radius + radius
            && centre.Y + radius >= body.Position.Y
            && centre.Y - radius <= body.Position.Y + body.Height;
    }

    /// <summary>The bats, dead ones included (until the end of the level).</summary>
    public IReadOnlyList<Bat> Bats => _bats;

    /// <summary>Everything the weapons can hit: dummies and ghouls.</summary>
    public IEnumerable<ITarget> Targets => _dummies.Cast<ITarget>().Concat(Enemies);

    // Every enemy of every kind, once (issue #572): a new kind of enemy goes here, and with it into the rooms and out of
    // doorways that shut.
    private IEnumerable<IEnemy> Enemies => _ghouls.Cast<IEnemy>().Concat(_bats).Concat(_gorm is null ? [] : [_gorm]);

    /// <summary>
    /// The rules that turn a hit into damage (headshots, later items, ...). Part of the world, not static:
    /// adding a rule here changes every following hit.
    /// </summary>
    public DamageRules DamageRules { get; }

    /// <summary>Everything that happened in the latest tick (player and dummies). Read it after every tick.</summary>
    public IReadOnlyList<GameEvent> Events => _events;

    /// <summary>
    /// A swing of <paramref name="attacker"/> hits every target in the weapon's cone (see <see cref="MeleeStrike"/>).
    /// A heavy weapon (the club) also knocks living ghouls and bats back - further when charged; Gorm stands firm.
    /// </summary>
    private void StrikeWithMelee(MeleeSwung swing, Player attacker)
    {
        var values = Balance.Melee(swing.Weapon);
        BreakWalls(swing, attacker, values);

        foreach (var target in Targets.Where(target => target.IsHittable).ToList())
        {
            if (MeleeStrike.Hits(Level, swing.Origin, swing.Direction, target.Body, values))
            {
                // A melee weapon hits the whole body at once - it has no hit zone (and so no headshot bonus).
                var weapon = swing.Weapon == WeaponKind.Club ? Weapon.Club : Weapon.Blade;
                Damage(target, new Hit(weapon, HitZone.Body, values.Damage, swing.Charge), attacker);
                KnockBack(target, attacker, values, swing.Charge);
            }
        }
    }

    private static void KnockBack(ITarget target, Player attacker, MeleeBalance values, float charge)
    {
        var distance = values.Knockback + (charge * (values.ChargedKnockback - values.Knockback));
        var away = target.Body.Position - attacker.Position;
        var knockback = target switch
        {
            Ghoul { IsDead: false } ghoul => ghoul.Knockback,
            Bat { IsDead: false } bat => bat.Knockback,
            _ => null,
        };
        knockback?.Start(away, distance, values.KnockbackTime);
    }

    /// <summary>
    /// A fully charged swing of any melee weapon breaks the cracked wall it is aimed at, within the weapon's range.
    /// Uncharged swings and shots leave it as it is.
    /// </summary>
    private void BreakWalls(MeleeSwung swing, Player attacker, MeleeBalance values)
    {
        if (swing.Charge < 1f)
        {
            return;
        }

        foreach (var wall in _doors.Where(IsUnbrokenWall))
        {
            if (Raycast.AgainstBox(swing.Origin, swing.Direction, wall.BoxMin, wall.BoxMax) is { } distance && distance <= values.Range)
            {
                wall.Break();
                UpdateLevel();
                LevelAsBuilt = WithUnbrokenWalls();
                _events.Add(new WallBroken(wall.Id, attacker.Id));
            }
        }
    }

    /// <summary>
    /// A shot hits the first thing on its ray within the weapon's range: a target (damage) or a wall, floor or
    /// ceiling (an impact). A piercing weapon (the rifle) goes on through as many more targets as its
    /// <see cref="RangedBalance.Pierce"/> says - walls always stop it. Instant - no flying bullet ("hitscan").
    /// </summary>
    private void FireShot(ShotFired shot, Player attacker)
    {
        var values = Balance.Ranged(shot.Weapon);
        var weapon = shot.Weapon switch
        {
            WeaponKind.Rifle => Weapon.Rifle,
            WeaponKind.Blunderbuss => Weapon.Blunderbuss,
            _ => Weapon.Pistol,
        };
        var forward = Vector3.Normalize(shot.Direction);

        for (var pellet = 0; pellet < values.Pellets; pellet++)
        {
            FirePellet(shot.Origin, Spread(forward, values.Spread), values, weapon, attacker);
        }
    }

    /// <summary>
    /// A direction in the cone <paramref name="degrees"/> wide around <paramref name="forward"/>, evenly spread over
    /// the cone's cross-section. The roll comes from the seed (coop rule: the same seed gives the same spread).
    /// </summary>
    private Vector3 Spread(Vector3 forward, float degrees)
    {
        if (degrees <= 0f)
        {
            return forward;
        }

        var halfAngle = degrees / 2f * MathF.PI / 180f;
        var tilt = halfAngle * MathF.Sqrt((float)_shotRandom.NextDouble());
        var around = 2f * MathF.PI * (float)_shotRandom.NextDouble();

        // Two directions across the shot: sideways and (roughly) up.
        var helper = MathF.Abs(forward.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
        var side = Vector3.Normalize(Vector3.Cross(helper, forward));
        var up = Vector3.Cross(forward, side);
        var across = side * MathF.Cos(around) + up * MathF.Sin(around);
        return Vector3.Normalize(forward * MathF.Cos(tilt) + across * MathF.Sin(tilt));
    }

    /// <summary>How far along the ray it meets the nearest of the target's shot bodies, or null if it misses them all.</summary>
    private static float? NearestShotBody(Vector3 origin, Vector3 direction, ITarget target) =>
        target.ShotBodies
            .Select(body => Raycast.AgainstCylinder(origin, direction, body))
            .Min(); // Min of float? skips the nulls, and is null if all are null.

    /// <summary>One bullet (or pellet) along <paramref name="direction"/>: hits up to 1 + pierce targets before the first wall.</summary>
    private void FirePellet(Vector3 origin, Vector3 direction, RangedBalance values, Weapon weapon, Player attacker)
    {
        var wall = Raycast.AgainstLevel(Level, origin, direction, values.Range);
        var reach = wall?.Distance ?? values.Range;

        // Every target on the ray before the wall, nearest first; the shot takes the first one and pierces on.
        var hits = Targets.Where(candidate => candidate.IsHittable)
            .Select(candidate => (Target: candidate, Distance: NearestShotBody(origin, direction, candidate)))
            .Where(hit => hit.Distance is { } distance && distance <= reach)
            .OrderBy(hit => hit.Distance)
            .Take(1 + values.Pierce)
            .ToList();

        foreach (var (target, distance) in hits)
        {
            var zone = target.ZoneAt(origin + direction * distance!.Value);
            Damage(target, new Hit(weapon, zone, values.Damage), attacker);
        }

        // Not stopped by enough targets: the bullet ends in the wall.
        if (hits.Count < 1 + values.Pierce && wall is { } impact)
        {
            _events.Add(new BulletImpact(impact.Point, impact.Normal));
        }
    }

    /// <summary>The flat direction from <paramref name="from"/> to <paramref name="to"/> (length 1), or +Z if they are on top of each other.</summary>
    private static Vector3 FacingTowards(Vector3 from, Vector3 to)
    {
        var flat = (to - from) with { Y = 0f };
        return flat.LengthSquared() > 0.0001f ? Vector3.Normalize(flat) : Vector3.UnitZ;
    }

    /// <summary>
    /// Turns a hit of <paramref name="attacker"/> into damage with the world's rules and applies it. If it kills an
    /// enemy, the attacker gets that enemy's experience - only the killing hit counts (online co-op: the one who
    /// finishes it off, not everyone who hit it).
    /// </summary>
    private void Damage(ITarget target, Hit hit, Player attacker)
    {
        // The showroom's enemies are exhibits: hits do nothing to them.
        if (IsShowroom)
        {
            return;
        }

        var result = DamageRules.Calculate(hit with { DamageBoons = attacker.BoonCount(Boon.Damage) });
        var wasAlive = target.IsHittable;
        var eventsBefore = _events.Count;
        target.TakeDamage(result.Amount, _events, result.IsCritical);

        // The target's own hit event does not say what hit it; this one does (issue #267: the blade sound only for the
        // blade). Only if the hit counted - a target that ignores it (already down) reports nothing.
        if (_events.Count > eventsBefore)
        {
            _events.Insert(eventsBefore + 1, new TargetHitBy(target.Id, attacker.Id, hit.Weapon));
        }

        // A sleeping enemy that is hit wakes up - together with its whole room (the room is not sealed by that). Since
        // issue #424 this is only the boss room: a combat room's enemies are hidden (not hittable) until it seals.
        if (_asleep.Contains(target.Id))
        {
            foreach (var room in _rooms.Where(room => room.Enemies.Contains(target)))
            {
                WakeUp(room);
            }
        }

        var experience = target switch
        {
            Ghoul { IsDead: true } => Balance.Experience.Ghoul,
            Bat { IsDead: true } => Balance.Experience.Bat,
            _ => 0,
        };
        if (wasAlive && experience > 0)
        {
            attacker.AddExperience(experience, _events);
        }
    }

    /// <summary>Advances the whole world by one simulation tick.</summary>
    public void Tick(PlayerInput input)
    {
        _events.Clear();

        // Esc menu: the world stands still while every player has paused (online co-op rule 2: the pause is input
        // like everything else). There is one player for now, so their input decides.
        if (input.Paused)
        {
            return;
        }

        Ticks++;

        // A level up waits for its bonus - but the world goes on (decision record 0008): the choice comes in as input
        // whenever the player makes it (online co-op rule 2 - everything that changes the game is tick input).
        // Several level ups wait in line and are chosen one after the other.
        if (input.ChooseBoon is { } boon && Players.FirstOrDefault(player => player.PendingBoons > 0) is { } choosing)
        {
            choosing.Choose(boon, _events);
        }

        var sealedIn = SealedRoomOf(Player);
        Player.Tick(input);
        _events.AddRange(Player.Events);
        KeepInSealedRoom(Player, sealedIn);

        // A weapon dropped from the inventory lies where the player stood.
        foreach (var dropped in Player.Events.OfType<WeaponDropped>())
        {
            _equipmentDrops.Add(new WeaponDrop(_nextEquipmentDropId++, dropped.Kind, dropped.Position, dropped.Ranged));
        }

        if (input.Interact)
        {
            Interact(Player);
        }

        Teleport(Player, input.TeleportTo);
        EnterRooms(Player);
        UnlockTeleporters(Player);

        foreach (var swing in Player.Events.OfType<MeleeSwung>())
        {
            StrikeWithMelee(swing, Player);
        }

        foreach (var shot in Player.Events.OfType<ShotFired>())
        {
            FireShot(shot, Player);
        }

        foreach (var dummy in _dummies)
        {
            dummy.Tick(_events);
        }

        foreach (var ghoul in _ghouls)
        {
            // Every ghoul is blocked by the players, the other living ghouls, the standing dummies, the chests and the well.
            var obstacles = Players.Select(player => player.Body)
                .Concat(Targets.Where(target => target.IsHittable && !ReferenceEquals(target, ghoul)).Select(target => target.Body))
                .Concat(StandingChests.Select(chest => chest.RoundBody))
                .Concat(WellBodies)
                .ToList();
            if (!_asleep.Contains(ghoul.Id) && IsOut(ghoul) && !IsShowroom)
            {
                ghoul.Act(_enemyLevel, Players, obstacles, _events, EnemyPaths());
            }

            ghoul.Tick(_events);
        }

        if (_gorm is not null && !_asleep.Contains(_gorm.Id) && !IsShowroom)
        {
            var obstacles = Players.Select(player => player.Body)
                .Concat(Targets.Where(target => target.IsHittable && !ReferenceEquals(target, _gorm)).Select(target => target.Body))
                .Concat(StandingChests.Select(chest => chest.RoundBody))
                .Concat(WellBodies)
                .ToList();
            _gorm.Act(_enemyLevel, Players, obstacles, _events, EnemyPaths(), _columnWear);
            RemoveBrokenColumns();
        }

        var batEventsFrom = _events.Count;
        foreach (var bat in _bats)
        {
            if (!_asleep.Contains(bat.Id) && IsOut(bat) && !IsShowroom)
            {
                bat.Act(_enemyLevel, Players, _events);
            }

            bat.Tick(_enemyLevel, _events);
        }

        // Every spit of this tick becomes a blood bolt.
        foreach (var spit in _events.Skip(batEventsFrom).OfType<BatSpat>().ToList())
        {
            Fire(spit.Origin, spit.Direction);
        }

        MoveProjectiles();
        DropFromTheDead();
        DropGormsLoot();
        MovePickups();
        MoveLoot();
        foreach (var drop in _equipmentDrops)
        {
            drop.Fall(Level, Balance.Player.Gravity);
        }

        foreach (var chest in _chests)
        {
            chest.Tick();
        }

        TickDoors();
        _stairs?.Tick();
        ClearRooms();

        // The clock of the run stops with victory or defeat; this tick still counts.
        var wasOver = IsWon || _lossReported;
        ReportLoss();
        Statistics.Count(_events);
        if (!wasOver)
        {
            Statistics.AddTick();
        }

        _ticksAllPlayersDead = Players.All(player => player.IsDead) ? _ticksAllPlayersDead + 1 : 0;
    }
}
