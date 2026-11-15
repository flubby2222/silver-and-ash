using System.Numerics;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
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
    private int _ticksAllPlayersDead;
    private readonly List<TrainingDummy> _dummies;
    private readonly List<Ghoul> _ghouls;
    private readonly List<Bat> _bats;
    private readonly List<Projectile> _projectiles = [];
    private readonly List<Pickup> _pickups = [];
    private readonly HashSet<int> _dropsRolled = [];
    private int _nextPickupId;
    private int _nextProjectileId;
    private readonly Level _levelWithOpenDoors;
    private readonly IReadOnlyList<LevelDoor> _doorPlan;
    private readonly List<Door> _doors;
    private readonly IReadOnlyList<PlacedRoom> _roomPlan;
    private readonly List<RoomState> _rooms;
    private readonly Dictionary<(int X, int Z), RoomState> _roomOfCell = [];
    private readonly HashSet<int> _asleep = [];
    private bool _lossReported;

    /// <summary>
    /// Creates the world for <paramref name="level"/>: the player at its start, dummies at their cells.
    /// <paramref name="seed"/> starts the world's random numbers: the same seed always gives the same game.
    /// <paramref name="balance"/> holds all balancing values; every figure of this world gets them (the standard
    /// values if none is given). <paramref name="doors"/> (optional) are the doors of a generated level; they all
    /// start closed. Their cells must be walkable in <paramref name="level"/>. <paramref name="rooms"/> (optional) are
    /// the rooms of a generated level: enemies in combat and boss rooms sleep until a player enters, then the room
    /// becomes an arena (see <see cref="RoomState"/>).
    /// </summary>
    public GameWorld(
        Level level, int seed = 0, Balance? balance = null, IReadOnlyList<LevelDoor>? doors = null,
        IReadOnlyList<PlacedRoom>? rooms = null)
    {
        ArgumentNullException.ThrowIfNull(level);
        _levelWithOpenDoors = level;
        _doorPlan = doors ?? [];
        _doors = _doorPlan.Select((plan, index) => new Door(index, plan, level)).ToList();
        Level = WithClosedDoors();
        Seed = seed;
        Balance = balance ?? Balance.Default;
        _restartTicks = SimulationClock.SecondsToTicks(Balance.RestartDelay);
        DamageRules = DamageRules.Standard(Balance);

        // Online co-op rule 3: all chance comes from this one generator, started from the seed.
        _random = new Random(seed);
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

        // The player cannot walk through standing dummies or living ghouls; lying or dead ones are stepped over.
        Player = new Player(Level, () => Targets.Where(target => target.IsHittable).Select(target => target.Body), Balance, _random);

        _roomPlan = rooms ?? [];
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
        foreach (var enemy in _ghouls.Cast<ITarget>().Concat(_bats))
        {
            var position = enemy.Body.Position;
            if (_roomOfCell.TryGetValue(Level.ToGrid(position.X, position.Z), out var room))
            {
                room.Enemies.Add(enemy);
                if (room.IsArena)
                {
                    _asleep.Add(enemy.Id);
                }
            }
        }
    }

    /// <summary>Name of the level, shown briefly at the start (e.g. "THE CATACOMBS"), or null for hand-made levels.</summary>
    public string? LevelName { get; init; }

    /// <summary>The level as it was built, all doors open - for drawing. <see cref="Level"/> is what blocks.</summary>
    public Level LevelAsBuilt => _levelWithOpenDoors;

    /// <summary>True once the boss room is cleared: the run is won (for now; the troll and the stairs come in v0.10).</summary>
    public bool IsWon { get; private set; }

    /// <summary>True when every player is dead before the run was won.</summary>
    public bool IsLost => !IsWon && Players.All(player => player.IsDead);

    /// <summary>The rooms of a generated level and what happened in them (empty for hand-made levels).</summary>
    public IReadOnlyList<RoomState> Rooms => _rooms;

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
    public GameWorld Restarted() => new(_levelWithOpenDoors, new Random(Seed).Next(), Balance, _doorPlan, _roomPlan);

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

    /// <summary>Everything that can be used with F. Doors today; chests, locks and people later.</summary>
    private IEnumerable<IInteractable> Interactables => _doors;

    private void Interact(Player player)
    {
        switch (InteractionTarget(player))
        {
            case Door door:
                OpenDoor(door);
                break;
        }
    }

    private void OpenDoor(Door door)
    {
        door.Open();
        UpdateLevel();
        _events.Add(new DoorOpened(door.Id));
    }

    // After doors changed: a new level in which exactly the blocking doors are walls, for everybody.
    private void UpdateLevel()
    {
        Level = WithClosedDoors();
        foreach (var player in Players)
        {
            player.ChangeLevel(Level);
        }
    }

    private Level WithClosedDoors() => _doors.Count == 0
        ? _levelWithOpenDoors
        : _levelWithOpenDoors.WithCells(_doors.Where(door => door.Blocks).Select(door => (door.X, door.Z, Cell.Wall)));

    // A player who stands in a room with the whole body enters it. A combat or boss room with living enemies
    // becomes an arena: grates fall at all its doors and the enemies wake up.
    private void EnterRooms(Player player)
    {
        var cell = Level.ToGrid(player.Position.X, player.Position.Z);
        if (!_roomOfCell.TryGetValue(cell, out var room) || room.IsEntered || TouchesDoorOf(room, player))
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
            WinIfBoss(room);
            return;
        }

        foreach (var id in room.DoorIds)
        {
            _doors[id].Bar();
        }

        room.Seal();
        UpdateLevel();
        _events.Add(new RoomSealed(room.Index));
    }

    // When the last enemy of an arena is dead, its grates rise. The door the player came through is open (they walked
    // through it), so it is free again at once; the others open with F as before.
    private void ClearRooms()
    {
        foreach (var room in _rooms.Where(room => room.IsSealed && room.Enemies.All(IsDead)))
        {
            foreach (var id in room.DoorIds)
            {
                _doors[id].Lift();
            }

            room.Clear();
            UpdateLevel();
            _events.Add(new RoomCleared(room.Index));
            WinIfBoss(room);
        }
    }

    // A room is cleared only once, and there is one boss room - so the run is won only once.
    private void WinIfBoss(RoomState room)
    {
        if (room.Kind == RoomKind.Boss)
        {
            IsWon = true;
            _events.Add(new RunWon());
        }
    }

    private void ReportLoss()
    {
        if (IsLost && !_lossReported)
        {
            _lossReported = true;
            _events.Add(new RunLost());
        }
    }

    private void WakeUp(RoomState room)
    {
        foreach (var enemy in room.Enemies)
        {
            _asleep.Remove(enemy.Id);
        }
    }

    private bool TouchesDoorOf(RoomState room, Player player)
    {
        foreach (var id in room.DoorIds)
        {
            var door = _doors[id];
            var nearestX = Math.Clamp(player.Position.X, door.BoxMin.X, door.BoxMax.X);
            var nearestZ = Math.Clamp(player.Position.Z, door.BoxMin.Z, door.BoxMax.Z);
            var dx = player.Position.X - nearestX;
            var dz = player.Position.Z - nearestZ;
            if (dx * dx + dz * dz < BodySizes.Player.Radius * BodySizes.Player.Radius)
            {
                return true;
            }
        }

        return false;
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
    /// Every enemy that died since the last tick gets exactly one chance to drop a consumable (its own chance from
    /// <see cref="DropsBalance"/>, rolled with the world's random generator). The drop appears where it died.
    /// </summary>
    private void DropFromTheDead()
    {
        foreach (var ghoul in _ghouls.Where(ghoul => ghoul.IsDead && _dropsRolled.Add(ghoul.Id)))
        {
            MaybeDrop(Balance.Drops.GhoulChance, ghoul.Position);
        }

        foreach (var bat in _bats.Where(bat => bat.IsDead && _dropsRolled.Add(bat.Id)))
        {
            MaybeDrop(Balance.Drops.BatChance, bat.Position);
        }
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
    public IEnumerable<ITarget> Targets => _dummies.Cast<ITarget>().Concat(_ghouls).Concat(_bats);

    /// <summary>
    /// The rules that turn a hit into damage (headshots, later items, ...). Part of the world, not static:
    /// adding a rule here changes every following hit.
    /// </summary>
    public DamageRules DamageRules { get; }

    /// <summary>Everything that happened in the latest tick (player and dummies). Read it after every tick.</summary>
    public IReadOnlyList<GameEvent> Events => _events;

    /// <summary>A swing of <paramref name="attacker"/> hits every target in the blade's cone (see <see cref="BladeStrike"/>).</summary>
    private void StrikeWithBlade(BladeSwung swing, Player attacker)
    {
        foreach (var target in Targets.Where(target => target.IsHittable).ToList())
        {
            if (BladeStrike.Hits(Level, swing.Origin, swing.Direction, target.Body, Balance.Blade))
            {
                // The blade hits the whole body at once - it has no hit zone (and so no headshot bonus).
                Damage(target, new Hit(Weapon.Blade, HitZone.Body, Balance.Blade.Damage, swing.Charge), attacker);
            }
        }
    }

    /// <summary>
    /// A shot hits the first thing on its ray within the pistol's range: a standing dummy (damage) or a wall,
    /// floor or ceiling (an impact). Instant - no flying bullet ("hitscan").
    /// </summary>
    private void FirePistol(ShotFired shot, Player attacker)
    {
        var direction = Vector3.Normalize(shot.Direction);
        var wall = Raycast.AgainstLevel(Level, shot.Origin, direction, Balance.Pistol.Range);
        var nearest = wall?.Distance ?? Balance.Pistol.Range;

        ITarget? target = null;
        foreach (var candidate in Targets.Where(candidate => candidate.IsHittable))
        {
            if (Raycast.AgainstCylinder(shot.Origin, direction, candidate.Body) is { } distance && distance <= nearest)
            {
                nearest = distance;
                target = candidate;
            }
        }

        if (target is not null)
        {
            var zone = target.ZoneAt(shot.Origin + direction * nearest);
            Damage(target, new Hit(Weapon.Pistol, zone, Balance.Pistol.Damage), attacker);
        }
        else if (wall is { } impact)
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
        var result = DamageRules.Calculate(hit with { DamageBoons = attacker.BoonCount(Boon.Damage) });
        var wasAlive = target.IsHittable;
        target.TakeDamage(result.Amount, _events, result.IsCritical);

        // A sleeping enemy that is hit wakes up - together with its whole room (the room is not sealed by that).
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

        // A level up waits for its bonus: the world stands still until the choice comes in as input
        // (online co-op rule 2 - everything that changes the game is tick input).
        var choosing = Players.FirstOrDefault(player => player.PendingBoons > 0);
        if (choosing is not null)
        {
            if (input.ChooseBoon is { } boon)
            {
                choosing.Choose(boon, _events);
            }

            return;
        }

        Player.Tick(input);
        _events.AddRange(Player.Events);

        if (input.Interact)
        {
            Interact(Player);
        }

        EnterRooms(Player);

        foreach (var swing in Player.Events.OfType<BladeSwung>())
        {
            StrikeWithBlade(swing, Player);
        }

        foreach (var shot in Player.Events.OfType<ShotFired>())
        {
            FirePistol(shot, Player);
        }

        foreach (var dummy in _dummies)
        {
            dummy.Tick(_events);
        }

        foreach (var ghoul in _ghouls)
        {
            // Every ghoul is blocked by the players, the other living ghouls and the standing dummies.
            var obstacles = Players.Select(player => player.Body)
                .Concat(Targets.Where(target => target.IsHittable && !ReferenceEquals(target, ghoul)).Select(target => target.Body))
                .ToList();
            if (!_asleep.Contains(ghoul.Id))
            {
                ghoul.Act(Level, Players, obstacles, _events);
            }

            ghoul.Tick(_events);
        }

        var batEventsFrom = _events.Count;
        foreach (var bat in _bats)
        {
            if (!_asleep.Contains(bat.Id))
            {
                bat.Act(Level, Players, _events);
            }

            bat.Tick(Level, _events);
        }

        // Every spit of this tick becomes a blood bolt.
        foreach (var spit in _events.Skip(batEventsFrom).OfType<BatSpat>().ToList())
        {
            Fire(spit.Origin, spit.Direction);
        }

        MoveProjectiles();
        DropFromTheDead();
        MovePickups();
        ClearRooms();
        ReportLoss();

        _ticksAllPlayersDead = Players.All(player => player.IsDead) ? _ticksAllPlayersDead + 1 : 0;
    }
}
