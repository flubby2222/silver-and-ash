using System.Numerics;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// A knife in flight (issue #204): thrown by a player, it flies in a straight line at its speed until it meets a target
/// or a wall, a column, the floor or the ceiling - or until it has flown its weapon's range. It has no width: it hits
/// what its line crosses.
/// </summary>
public sealed class ThrownKnife
{
    internal ThrownKnife(int id, Player thrower, WeaponKind weapon, Vector3 position, Vector3 direction, ThrownBalance flight, float range)
    {
        Id = id;
        Thrower = thrower;
        Weapon = weapon;
        Position = position;
        PreviousPosition = position;
        Direction = direction;
        Flight = flight;
        RangeLeft = range;
    }

    /// <summary>Number within its world (events and the stuck knife carry the same one).</summary>
    public int Id { get; }

    /// <summary>The weapon it was thrown from.</summary>
    public WeaponKind Weapon { get; }

    /// <summary>The player who threw it.</summary>
    public int ThrowerId => Thrower.Id;

    /// <summary>Where its point is.</summary>
    public Vector3 Position { get; internal set; }

    /// <summary>Where it was one tick earlier, for smooth drawing between ticks.</summary>
    public Vector3 PreviousPosition { get; internal set; }

    /// <summary>Where it flies, length 1.</summary>
    public Vector3 Direction { get; }

    /// <summary>Metres per second.</summary>
    public float Speed => Flight.Speed;

    /// <summary>Metres it may still fly.</summary>
    public float RangeLeft { get; internal set; }

    internal Player Thrower { get; }

    internal ThrownBalance Flight { get; }
}

/// <summary>
/// A knife that sticks where it hit (issue #204): in a wall (then it stays where it is) or in a target (then it moves
/// along with it, and is gone when the target can no longer be hit).
/// </summary>
public sealed class StuckKnife
{
    private readonly Vector3 _offset;

    internal StuckKnife(int id, WeaponKind weapon, ITarget? target, Vector3 point, Vector3 direction)
    {
        Id = id;
        Weapon = weapon;
        Target = target;
        Direction = direction;
        _offset = target is null ? point : point - target.Body.Position;
    }

    /// <summary>The number it had in flight (<see cref="ThrownKnife.Id"/>).</summary>
    public int Id { get; }

    /// <summary>The weapon it was thrown from.</summary>
    public WeaponKind Weapon { get; }

    /// <summary>The target it sticks in, or null in a wall, a column, the floor or the ceiling.</summary>
    public int? TargetId => Target?.Id;

    /// <summary>Where its point is now: fixed in a wall, carried along in a target.</summary>
    public Vector3 Point => Target is null ? _offset : Target.Body.Position + _offset;

    /// <summary>The way it flew in, length 1 - the handle points back along it.</summary>
    public Vector3 Direction { get; }

    internal ITarget? Target { get; }
}

/// <summary>
/// The knives of one world (issue #204): those in flight and those that stick. The world throws them, moves them once
/// a tick and says what a hit on a target does; everything about the flight is here.
/// </summary>
internal sealed class ThrownKnives
{
    private readonly List<ThrownKnife> _flying = [];
    private readonly List<StuckKnife> _stuck = [];
    private int _nextId;

    /// <summary>The knives in flight, oldest first.</summary>
    public IReadOnlyList<ThrownKnife> Flying => _flying;

    /// <summary>The knives that stick, oldest first.</summary>
    public IReadOnlyList<StuckKnife> Stuck => _stuck;

    /// <summary>
    /// <paramref name="thrower"/> throws a knife of <paramref name="weapon"/> from <paramref name="origin"/> in
    /// <paramref name="direction"/>; it flies as <paramref name="flight"/> says, <paramref name="range"/> metres at most.
    /// </summary>
    public ThrownKnife Throw(Player thrower, WeaponKind weapon, Vector3 origin, Vector3 direction, ThrownBalance flight, float range)
    {
        var knife = new ThrownKnife(_nextId++, thrower, weapon, origin, Vector3.Normalize(direction), flight, range);
        _flying.Add(knife);
        return knife;
    }

    /// <summary>
    /// One tick of flight for every knife. Each flies its stretch of this tick as one line: the nearest thing on it stops
    /// the knife - a target of <paramref name="targets"/> that can be hit (then <paramref name="strike"/> deals the
    /// damage; if it says the hit did not count, the knife flies on through) or the stone of <paramref name="level"/>.
    /// What sticks is reported as <see cref="KnifeStuck"/>, in stone also as <see cref="BulletImpact"/>. Afterwards the
    /// knives in targets that can no longer be hit are gone.
    /// </summary>
    public void Move(Level level, IReadOnlyList<ITarget> targets, Func<ThrownKnife, ITarget, HitZone, bool> strike, ICollection<GameEvent> events)
    {
        foreach (var knife in _flying)
        {
            if (Fly(knife, level, targets, strike) is not var (stuck, stone))
            {
                continue;
            }

            knife.RangeLeft = 0f;
            Stick(stuck, knife.Flight.StuckMax);
            events.Add(new KnifeStuck(stuck.Id, knife.ThrowerId, stuck.Weapon, stuck.Point, stuck.Direction, stuck.TargetId));
            if (stone is { } wall)
            {
                events.Add(new BulletImpact(wall.Point, wall.Normal, stuck.Weapon));
            }
        }

        _flying.RemoveAll(knife => knife.RangeLeft <= 0f);
        _stuck.RemoveAll(knife => knife.Target is { IsHittable: false });
    }

    /// <summary>
    /// How far along the line from <paramref name="origin"/> in <paramref name="direction"/> a knife meets
    /// <paramref name="target"/>, or null if it misses. 0 if it starts inside one of its bodies: a target that moved
    /// onto the knife between two ticks is hit, not flown through.
    /// </summary>
    internal static float? DistanceTo(ITarget target, Vector3 origin, Vector3 direction)
    {
        float? nearest = null;
        foreach (var body in target.ShotBodies)
        {
            if (IsInside(origin, body))
            {
                return 0f;
            }

            if (Raycast.AgainstCylinder(origin, direction, body) is { } distance && (nearest is null || distance < nearest))
            {
                nearest = distance;
            }
        }

        return nearest;
    }

    private static bool IsInside(Vector3 point, CylinderBody body)
    {
        var flat = new Vector2(point.X - body.Position.X, point.Z - body.Position.Z);
        return flat.LengthSquared() <= body.Radius * body.Radius
            && point.Y >= body.Position.Y
            && point.Y <= body.Position.Y + body.Height;
    }

    // Moves the knife its stretch of this tick. Returns the knife as it sticks - with the stone it sticks in, if it is
    // no target - or null if it still flies (or its range is used up).
    private static (StuckKnife Knife, RayHit? Stone)? Fly(ThrownKnife knife, Level level, IReadOnlyList<ITarget> targets, Func<ThrownKnife, ITarget, HitZone, bool> strike)
    {
        knife.PreviousPosition = knife.Position;
        var origin = knife.Position;
        var stretch = MathF.Min(knife.Speed * SimulationClock.SecondsPerTick, knife.RangeLeft);
        var stone = Raycast.AgainstLevel(level, origin, knife.Direction, stretch);
        var free = stone?.Distance ?? stretch;

        // Every target on this stretch before the stone, nearest first: the first whose hit counts keeps the knife.
        foreach (var (target, distance) in targets.Where(candidate => candidate.IsHittable)
            .Select(candidate => (Target: candidate, Distance: DistanceTo(candidate, origin, knife.Direction)))
            .Where(hit => hit.Distance <= free)
            .OrderBy(hit => hit.Distance)
            .Select(hit => (hit.Target, hit.Distance!.Value))
            .ToList())
        {
            var point = origin + knife.Direction * distance;
            if (strike(knife, target, target.ZoneAt(point)))
            {
                knife.Position = point;
                return (new StuckKnife(knife.Id, knife.Weapon, target, point, knife.Direction), null);
            }
        }

        if (stone is { } wall)
        {
            knife.Position = wall.Point;
            return (new StuckKnife(knife.Id, knife.Weapon, null, wall.Point, knife.Direction), wall);
        }

        knife.Position = origin + knife.Direction * stretch;
        knife.RangeLeft -= stretch;
        return null;
    }

    private void Stick(StuckKnife knife, int most)
    {
        _stuck.Add(knife);
        if (_stuck.Count > most)
        {
            _stuck.RemoveRange(0, _stuck.Count - most);
        }
    }
}
