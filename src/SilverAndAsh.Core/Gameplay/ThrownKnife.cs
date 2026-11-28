using System.Numerics;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// A knife in flight (issue #204): thrown by a player, it flies in a straight line at its speed until it meets a target
/// or a wall, a column, the floor or the ceiling - or until it has flown its weapon's range. It has no width: it hits
/// what its line crosses. Off stone it glances a few times before it sticks (issue #822): <see cref="Ricochets"/>.
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

    /// <summary>Where it flies, length 1. It changes when the knife glances off stone (issue #822).</summary>
    public Vector3 Direction { get; internal set; }

    /// <summary>
    /// How often it has glanced off stone so far (issue #822). At <see cref="ThrownBalance.Ricochets"/> the next stone
    /// keeps it.
    /// </summary>
    public int Ricochets { get; internal set; }

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
    /// One tick of flight for every knife. Each flies its stretch of this tick as a line: the nearest thing on it stops
    /// the knife - a target of <paramref name="targets"/> that can be hit (then <paramref name="strike"/> deals the
    /// damage; if it says the hit did not count, the knife flies on through) or the stone of <paramref name="level"/>.
    /// A knife that may still glance off (<see cref="ThrownBalance.Ricochets"/>, issue #822) does not stick in stone: it
    /// flies the rest of its stretch mirrored, reported as <see cref="KnifeRicocheted"/>.
    /// What sticks is reported as <see cref="KnifeStuck"/>, in stone also as <see cref="BulletImpact"/>. Afterwards the
    /// knives in targets that can no longer be hit are gone.
    /// </summary>
    public void Move(Level level, IReadOnlyList<ITarget> targets, Func<ThrownKnife, ITarget, HitZone, bool> strike, ICollection<GameEvent> events)
    {
        foreach (var knife in _flying)
        {
            if (Fly(knife, level, targets, strike, events) is not var (stuck, stone))
            {
                continue;
            }

            knife.RangeLeft = 0f;
            Stick(stuck, knife.Flight.StuckMax);
            events.Add(new KnifeStuck(stuck.Id, knife.ThrowerId, stuck.Weapon, stuck.Point, stuck.Direction, stuck.TargetId) { Place = stuck.Point });
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

    // Moves the knife its stretch of this tick: one line, or several if it glances off stone on the way (each of them
    // reported in `events`). Returns the knife as it sticks - with the stone it sticks in, if it is no target - or null
    // if it still flies (or its range is used up).
    private static (StuckKnife Knife, RayHit? Stone)? Fly(
        ThrownKnife knife, Level level, IReadOnlyList<ITarget> targets, Func<ThrownKnife, ITarget, HitZone, bool> strike, ICollection<GameEvent> events)
    {
        knife.PreviousPosition = knife.Position;
        var stretch = MathF.Min(knife.Speed * SimulationClock.SecondsPerTick, knife.RangeLeft);

        // Ends at the latest with the stone after the last ricochet: every round but the last uses one of them up.
        while (true)
        {
            var origin = knife.Position;
            var stone = Raycast.AgainstLevel(level, origin, knife.Direction, stretch);
            var free = stone?.Distance ?? stretch;
            if (StrikeFirst(knife, free, targets, strike) is { } inTarget)
            {
                return (inTarget, null);
            }

            if (stone is not { } wall)
            {
                knife.Position = origin + knife.Direction * stretch;
                knife.RangeLeft -= stretch;
                return null;
            }

            knife.Position = wall.Point;
            if (!GlancesOff(knife, wall))
            {
                return (new StuckKnife(knife.Id, knife.Weapon, null, wall.Point, knife.Direction), wall);
            }

            stretch -= wall.Distance;
            knife.RangeLeft -= wall.Distance;
            events.Add(Ricochet(knife, wall));
        }
    }

    // Every target on the next `free` metres of the knife's line, nearest first: the first whose hit counts keeps the
    // knife - it is returned as it sticks there. Null if none does.
    private static StuckKnife? StrikeFirst(ThrownKnife knife, float free, IReadOnlyList<ITarget> targets, Func<ThrownKnife, ITarget, HitZone, bool> strike)
    {
        var origin = knife.Position;
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
                return new StuckKnife(knife.Id, knife.Weapon, target, point, knife.Direction);
            }
        }

        return null;
    }

    /// <summary>
    /// Whether the knife glances off the stone it met (issue #822): as long as it has ricochets left. A knife that
    /// starts inside the stone (distance 0) has no surface to glance off - it sticks where it is.
    /// </summary>
    private static bool GlancesOff(ThrownKnife knife, RayHit stone) =>
        knife.Ricochets < knife.Flight.Ricochets && stone.Distance > 0f;

    /// <summary>
    /// The knife glances off <paramref name="stone"/> (issue #822): its direction is mirrored at the surface, it keeps
    /// its speed, and the ricochet is counted. Returns the event that reports it.
    /// </summary>
    private static KnifeRicocheted Ricochet(ThrownKnife knife, RayHit stone)
    {
        knife.Direction = Vector3.Normalize(Vector3.Reflect(knife.Direction, stone.Normal));
        knife.Ricochets++;
        return new KnifeRicocheted(knife.Id, knife.ThrowerId, knife.Weapon, stone.Point, stone.Normal, knife.Direction, knife.Ricochets) { Place = stone.Point };
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
