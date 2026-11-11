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
    private static readonly int RestartTicks = SimulationClock.SecondsToTicks(Tuning.Combat.RestartDelay);

    private readonly List<GameEvent> _events = [];
    private int _ticksAllPlayersDead;
    private readonly List<TrainingDummy> _dummies;
    private readonly List<Ghoul> _ghouls;

    /// <summary>Creates the world for <paramref name="level"/>: the player at its start, dummies at their cells.</summary>
    public GameWorld(Level level)
    {
        ArgumentNullException.ThrowIfNull(level);
        Level = level;
        _dummies = level.DummyPositions.Select((position, index) => new TrainingDummy(index, position)).ToList();

        // Ghoul numbers continue after the dummies': every target has its own number.
        // They start looking towards the player's start (their head is at the front).
        _ghouls = level.GhoulPositions
            .Select((position, index) => new Ghoul(_dummies.Count + index, position, FacingTowards(position, level.PlayerStartPosition)))
            .ToList();

        // The player cannot walk through standing dummies or living ghouls; lying or dead ones are stepped over.
        Player = new Player(level, () => Targets.Where(target => target.IsHittable).Select(target => target.Body));
    }

    public Level Level { get; }

    public Player Player { get; }

    /// <summary>All players. One for now - enemies already pick their target from this list (online co-op later).</summary>
    public IReadOnlyList<Player> Players => [Player];

    /// <summary>True when every player is dead and the restart delay has passed: time to start the level again.</summary>
    public bool ShouldRestart => _ticksAllPlayersDead >= RestartTicks;

    /// <summary>A fresh world for the same level: full health, every dummy and ghoul as at the start.</summary>
    public GameWorld Restarted() => new(Level);

    public IReadOnlyList<TrainingDummy> Dummies => _dummies;

    /// <summary>The ghouls, dead ones included (until the end of the level).</summary>
    public IReadOnlyList<Ghoul> Ghouls => _ghouls;

    /// <summary>Everything the weapons can hit: dummies and ghouls.</summary>
    public IEnumerable<ITarget> Targets => _dummies.Cast<ITarget>().Concat(_ghouls);

    /// <summary>
    /// The rules that turn a hit into damage (headshots, later items, ...). Part of the world, not static:
    /// adding a rule here changes every following hit.
    /// </summary>
    public DamageRules DamageRules { get; } = DamageRules.Standard();

    /// <summary>Everything that happened in the latest tick (player and dummies). Read it after every tick.</summary>
    public IReadOnlyList<GameEvent> Events => _events;

    /// <summary>A swing hits every target in the blade's cone (see <see cref="BladeStrike"/>).</summary>
    private void StrikeWithBlade(BladeSwung swing)
    {
        foreach (var target in Targets.Where(target => target.IsHittable).ToList())
        {
            if (BladeStrike.Hits(Level, swing.Origin, swing.Direction, target.Body))
            {
                // The blade hits the whole body at once - it has no hit zone (and so no headshot bonus).
                Damage(target, new Hit(Weapon.Blade, HitZone.Body, Tuning.Combat.BladeDamage, swing.Charge));
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
        var wall = Raycast.AgainstLevel(Level, shot.Origin, direction, Tuning.Combat.PistolRange);
        var nearest = wall?.Distance ?? Tuning.Combat.PistolRange;

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
            Damage(target, new Hit(Weapon.Pistol, zone, Tuning.Combat.PistolDamage));
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

        _ticksAllPlayersDead = Players.All(player => player.IsDead) ? _ticksAllPlayersDead + 1 : 0;
    }
}
