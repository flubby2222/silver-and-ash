using System.Numerics;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Gorm, the Chained: the troll, boss of level 0 (decision record 0007). 3.2 m tall, slow, much health. He sleeps in
/// the boss room until a player enters it, then walks after the nearest living player along the shortest way and
/// stops close in front of them. His head is a weak spot (<see cref="HitZone.BossHead"/>). His attacks (hammer, chain
/// swing, chain throw) come with issue #165 - for now he only walks. When he dies, his body stays where it fell.
/// </summary>
public sealed class Gorm : ITarget
{
    /// <summary>The name shown above his health bar.</summary>
    public const string Name = "GORM, THE CHAINED";

    private readonly GormBalance _gorm;
    private readonly float _gravity;
    private readonly int _repathTicks;
    private List<Vector3> _waypoints = [];
    private int _repathTicksLeft;
    private (int X, int Z) _pathGoal;

    /// <summary>Creates Gorm at <paramref name="position"/> (centre of his feet), looking along <paramref name="facing"/>.</summary>
    public Gorm(int id, Vector3 position, Vector3? facing = null, Balance? balance = null)
    {
        Id = id;
        Position = position;
        Facing = facing ?? Vector3.UnitZ;

        balance ??= Balance.Default;
        _gorm = balance.Gorm;
        _gravity = balance.Player.Gravity;
        _repathTicks = SimulationClock.SecondsToTicks(_gorm.RepathTime);
        Health = _gorm.MaxHealth;
    }

    public int Id { get; }

    /// <summary>Centre of his feet in world coordinates.</summary>
    public Vector3 Position { get; private set; }

    /// <summary>His movement in metres per second.</summary>
    public Vector3 Velocity { get; private set; }

    /// <summary>The flat direction he looks in (length 1).</summary>
    public Vector3 Facing { get; private set; }

    public int Health { get; private set; }

    public int MaxHealth => _gorm.MaxHealth;

    public bool IsDead => Health == 0;

    public bool IsHittable => !IsDead;

    public CylinderBody Body => new(Position, BodySizes.Gorm.Radius, BodySizes.Gorm.Height);

    /// <summary>His head: the top of his body, at the front, as wide as it is drawn. The rest is body.</summary>
    public HitZone ZoneAt(Vector3 point)
    {
        var high = point.Y >= Position.Y + BodySizes.Gorm.Height - BodySizes.Gorm.HeadHeight;
        var flatOffset = (point - Position) with { Y = 0f };
        var forward = Vector3.Dot(flatOffset, Facing);
        var side = MathF.Abs(Vector3.Dot(flatOffset, new Vector3(Facing.Z, 0f, -Facing.X)));
        return high && forward > 0f && side <= BodySizes.Gorm.HeadWidth / 2f ? HitZone.BossHead : HitZone.Body;
    }

    public void TakeDamage(int damage, ICollection<GameEvent> events, bool critical = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(damage);
        ArgumentNullException.ThrowIfNull(events);
        if (IsDead)
        {
            return;
        }

        // Reported is what he really lost - a deadly hit counts only his last health.
        var taken = Math.Min(damage, Health);
        Health -= taken;
        events.Add(new GormHit(Id, taken, critical));
        if (IsDead)
        {
            Velocity = Vector3.Zero;
            events.Add(new GormDied(Id));
        }
    }

    /// <summary>
    /// One tick of behaviour: walk after the nearest living player and stop <see cref="GormBalance.StopDistance"/>
    /// in front of them. <paramref name="obstacles"/> are the bodies he cannot walk through.
    /// </summary>
    public void Act(Level level, IReadOnlyList<Player> players, IReadOnlyList<CylinderBody> obstacles)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(obstacles);
        if (IsDead)
        {
            return;
        }

        // Online co-op rule 1: the nearest of THE living players.
        var target = players.Where(player => !player.IsDead).MinBy(player => FlatDistance(Position, player.Position));
        if (target is null || FlatDistance(Position, target.Position) <= _gorm.StopDistance)
        {
            if (target is not null)
            {
                FaceTowards(target.Position);
            }

            Move(level, Vector3.Zero, obstacles);
            return;
        }

        var next = NextPoint(level, target);
        if (next is not { } point || FlatDistance(Position, point) < 0.001f)
        {
            Move(level, Vector3.Zero, obstacles);
            return;
        }

        FaceTowards(point);
        Move(level, Facing * _gorm.Speed, obstacles);
    }

    // Straight at the target if nothing is in the way, otherwise along the smoothed path of the grid search.
    private Vector3? NextPoint(Level level, Player target)
    {
        if (Pathfinder.CanWalkStraight(level, Position, target.Position, BodySizes.Gorm.Radius))
        {
            _waypoints.Clear();
            return target.Position;
        }

        var goal = Level.ToGrid(target.Position.X, target.Position.Z);
        if (--_repathTicksLeft <= 0 || goal != _pathGoal || _waypoints.Count == 0)
        {
            var path = Pathfinder.FindPath(level, Level.ToGrid(Position.X, Position.Z), goal);
            _waypoints = path is null ? [] : Pathfinder.Smooth(level, path, BodySizes.Gorm.Radius).ToList();
            _repathTicksLeft = _repathTicks;
            _pathGoal = goal;
        }

        while (_waypoints.Count > 0 && FlatDistance(Position, _waypoints[0]) < 0.25f)
        {
            _waypoints.RemoveAt(0);
        }

        return _waypoints.Count > 0 ? _waypoints[0] : null;
    }

    private void Move(Level level, Vector3 flatVelocity, IReadOnlyList<CylinderBody> obstacles)
    {
        const float Dt = SimulationClock.SecondsPerTick;
        var velocity = flatVelocity with { Y = Velocity.Y };
        (var position, velocity) = CharacterCollision.Move(
            level, Position, velocity, BodySizes.Gorm.Radius, Dt, BodySizes.Gorm.Height, bodies: obstacles);
        (position, velocity, _) = CharacterCollision.MoveVertically(
            level, position, velocity, BodySizes.Gorm.Radius, BodySizes.Gorm.Height, _gravity, Dt);
        Position = position;
        Velocity = velocity;
    }

    private void FaceTowards(Vector3 point)
    {
        var flat = (point - Position) with { Y = 0f };
        if (flat.LengthSquared() > 0.0001f)
        {
            Facing = Vector3.Normalize(flat);
        }
    }

    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();
}
