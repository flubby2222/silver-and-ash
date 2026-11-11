using System.Numerics;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Everything that is simulated together: the level, the player and the training dummies.
/// One tick of the world advances all of them and collects their events in one list.
/// <para>
/// The whole game state lives in this object, not in static fields - a requirement for online co-op later
/// (decision record 0003). There is one player for now; the name "Player" may become a list then.
/// </para>
/// </summary>
public sealed class GameWorld
{
    private readonly List<GameEvent> _events = [];
    private readonly List<TrainingDummy> _dummies;

    /// <summary>Creates the world for <paramref name="level"/>: the player at its start, dummies at their cells.</summary>
    public GameWorld(Level level)
    {
        ArgumentNullException.ThrowIfNull(level);
        Level = level;
        _dummies = level.DummyPositions.Select((position, index) => new TrainingDummy(index, position)).ToList();

        // The player cannot walk through standing dummies; lying ones are stepped over.
        Player = new Player(level, () => _dummies.Where(dummy => !dummy.IsDown).Select(dummy => dummy.Body));
    }

    public Level Level { get; }

    public Player Player { get; }

    public IReadOnlyList<TrainingDummy> Dummies => _dummies;

    /// <summary>
    /// The rules that turn a hit into damage (headshots, later items, ...). Part of the world, not static:
    /// adding a rule here changes every following hit.
    /// </summary>
    public DamageRules DamageRules { get; } = DamageRules.Standard();

    /// <summary>Everything that happened in the latest tick (player and dummies). Read it after every tick.</summary>
    public IReadOnlyList<GameEvent> Events => _events;

    /// <summary>A swing hits every standing dummy in the blade's cone (see <see cref="BladeStrike"/>).</summary>
    private void StrikeWithBlade(BladeSwung swing)
    {
        foreach (var dummy in _dummies)
        {
            if (BladeStrike.Hits(Level, swing.Origin, swing.Direction, dummy.Body))
            {
                // The blade hits the whole body at once - it has no hit zone (and so no headshot bonus).
                Damage(dummy, new Hit(Weapon.Blade, HitZone.Body, Tuning.Combat.BladeDamage));
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

        TrainingDummy? target = null;
        foreach (var dummy in _dummies.Where(dummy => !dummy.IsDown))
        {
            if (Raycast.AgainstCylinder(shot.Origin, direction, dummy.Body) is { } distance && distance <= nearest)
            {
                nearest = distance;
                target = dummy;
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

    /// <summary>Turns a hit into damage with the world's rules and applies it.</summary>
    private void Damage(TrainingDummy dummy, Hit hit)
    {
        var result = DamageRules.Calculate(hit);
        dummy.TakeDamage(result.Amount, _events, result.IsCritical);
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
    }
}
