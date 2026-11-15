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

    /// <summary>
    /// Creates the world for <paramref name="level"/>: the player at its start, dummies at their cells.
    /// <paramref name="seed"/> starts the world's random numbers: the same seed always gives the same game.
    /// <paramref name="balance"/> holds all balancing values; every figure of this world gets them (the standard
    /// values if none is given).
    /// </summary>
    public GameWorld(Level level, int seed = 0, Balance? balance = null)
    {
        ArgumentNullException.ThrowIfNull(level);
        Level = level;
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
        Player = new Player(level, () => Targets.Where(target => target.IsHittable).Select(target => target.Body), Balance);
    }

    public Level Level { get; }

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
    public GameWorld Restarted() => new(Level, new Random(Seed).Next(), Balance);

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

    /// <summary>A swing hits every target in the blade's cone (see <see cref="BladeStrike"/>).</summary>
    private void StrikeWithBlade(BladeSwung swing)
    {
        foreach (var target in Targets.Where(target => target.IsHittable).ToList())
        {
            if (BladeStrike.Hits(Level, swing.Origin, swing.Direction, target.Body, Balance.Blade))
            {
                // The blade hits the whole body at once - it has no hit zone (and so no headshot bonus).
                Damage(target, new Hit(Weapon.Blade, HitZone.Body, Balance.Blade.Damage, swing.Charge));
            }
        }
    }

    /// <summary>
    /// A shot hits the first thing on its ray within the pistol's range: a standing dummy (damage) or a wall,
    /// floor or ceiling (an impact). Instant - no flying bullet ("hitscan").
    /// </summary>
    private void FirePistol(ShotFired shot)
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
            Damage(target, new Hit(Weapon.Pistol, zone, Balance.Pistol.Damage, Consecrated: shot.Consecrated));
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

    /// <summary>Turns a hit into damage with the world's rules and applies it.</summary>
    private void Damage(ITarget target, Hit hit)
    {
        var result = DamageRules.Calculate(hit);
        target.TakeDamage(result.Amount, _events, result.IsCritical);
    }

    /// <summary>Advances the whole world by one simulation tick.</summary>
    public void Tick(PlayerInput input)
    {
        _events.Clear();
        Player.Tick(input);
        _events.AddRange(Player.Events);

        foreach (var swing in Player.Events.OfType<BladeSwung>())
        {
            StrikeWithBlade(swing);
        }

        foreach (var shot in Player.Events.OfType<ShotFired>())
        {
            FirePistol(shot);
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
            ghoul.Act(Level, Players, obstacles, _events);
            ghoul.Tick(_events);
        }

        var batEventsFrom = _events.Count;
        foreach (var bat in _bats)
        {
            bat.Act(Level, Players, _events);
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

        _ticksAllPlayersDead = Players.All(player => player.IsDead) ? _ticksAllPlayersDead + 1 : 0;
    }
}
