using System.Numerics;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>What a ghoul is doing right now.</summary>
public enum GhoulState
{
    /// <summary>Crouching still until it notices a player.</summary>
    Lurking,

    /// <summary>Crawling after its target.</summary>
    Chasing,

    /// <summary>Drawing back for a blow - the moment to step away or dodge.</summary>
    WindingUp,

    /// <summary>Catching its breath after a blow.</summary>
    Recovering,
}

/// <summary>
/// The ghoul: a crawling enemy, lower than the player, with its head at the front. It lurks until it sees a player
/// (or is hit), then crawls after them along the shortest way (<see cref="Pathfinder"/>), draws back and strikes.
/// At 0 health it dies - its body lies on the ground for a moment, then it is gone.
/// </summary>
public sealed class Ghoul : ITarget
{
    private readonly GhoulBalance _ghoul;
    private readonly float _gravity;
    private readonly int _corpseTicks;
    private readonly int _windUpTicks;
    private readonly int _recoverTicks;
    private readonly int _repathTicks;
    private readonly float _attackCosine;

    private int _corpseTicksLeft;
    private int _stateTicksLeft;
    private bool _alerted;
    private Player? _target;
    private List<Vector3> _waypoints = [];
    private Level? _paths;
    private int _repathTicksLeft;
    private (int X, int Z) _pathGoal;

    /// <summary>
    /// Creates a ghoul at <paramref name="position"/> (centre of its feet), looking along <paramref name="facing"/>,
    /// as strong and fast as <paramref name="balance"/> says (the standard values if none is given).
    /// </summary>
    public Ghoul(int id, Vector3 position, Vector3? facing = null, Balance? balance = null)
    {
        Id = id;
        Position = position;
        Facing = facing ?? Vector3.UnitZ;

        balance ??= Balance.Default;
        _ghoul = balance.Ghoul;
        _gravity = balance.Player.Gravity;
        _corpseTicks = SimulationClock.SecondsToTicks(_ghoul.CorpseTime);
        _windUpTicks = SimulationClock.SecondsToTicks(_ghoul.WindUpTime);
        _recoverTicks = SimulationClock.SecondsToTicks(_ghoul.RecoverTime);
        _repathTicks = SimulationClock.SecondsToTicks(_ghoul.RepathTime);
        _attackCosine = MathF.Cos(_ghoul.AttackHalfAngle * MathF.PI / 180f);
        Health = _ghoul.MaxHealth;
    }

    public int Id { get; }

    /// <summary>Centre of its feet in world coordinates.</summary>
    public Vector3 Position { get; private set; }

    /// <summary>Its movement in metres per second.</summary>
    public Vector3 Velocity { get; private set; }

    /// <summary>A push from a heavy blow (the club) - while it lasts the ghoul slides away and does nothing else.</summary>
    public Knockback Knockback { get; } = new();

    /// <summary>Its crawling speed in m/s (for drawing: how fast the arms move).</summary>
    public float Speed => _ghoul.Speed;

    /// <summary>The flat direction it looks in (length 1). Its head is at the front.</summary>
    public Vector3 Facing { get; private set; }

    /// <summary>What it is doing right now.</summary>
    public GhoulState State { get; private set; } = GhoulState.Lurking;

    /// <summary>How far the wind-up has come: 0 at its start, 1 at the blow. 0 in every other state. For drawing.</summary>
    public float WindUpProgress => State == GhoulState.WindingUp ? 1f - (float)_stateTicksLeft / _windUpTicks : 0f;

    /// <summary>Current health; 0 when dead.</summary>
    public int Health { get; private set; }

    /// <summary>True once its health reached 0.</summary>
    public bool IsDead => Health == 0;

    /// <summary>True when the dead body has disappeared - it is no longer drawn.</summary>
    public bool IsGone => IsDead && _corpseTicksLeft == 0;

    /// <summary>
    /// Coming out of the floor (issue #424): in a combat room it waits hidden until the room seals. While hidden or
    /// rising it cannot be hit (and the world does not let it act).
    /// </summary>
    public Emergence Emergence { get; } = new();

    /// <summary>Where it is drawn: its position, sunk into the floor while it is hidden or rising.</summary>
    public Vector3 ShownPosition => Position + Emergence.Offset;

    public bool IsHittable => !IsDead && Emergence.IsPresent;

    public CylinderBody Body => new(Position, BodySizes.Ghoul.Radius, BodySizes.Ghoul.Height);

    /// <summary>
    /// The head: where it is drawn - pushed forward, in the top part of the body, narrow
    /// (<see cref="BodySizes.Ghoul"/>). The rest of the body, also its upper front beside the head, is body.
    /// </summary>
    public HitZone ZoneAt(Vector3 point)
    {
        var high = point.Y >= Position.Y + BodySizes.Ghoul.Height - BodySizes.Ghoul.HeadHeight;
        var flatOffset = (point - Position) with { Y = 0f };
        var forward = Vector3.Dot(flatOffset, Facing);
        var side = MathF.Abs(Vector3.Dot(flatOffset, new Vector3(Facing.Z, 0f, -Facing.X)));
        var onHead = forward >= BodySizes.Ghoul.HeadFrontStart && side <= BodySizes.Ghoul.HeadWidth / 2f;
        return high && onHead ? HitZone.Head : HitZone.Body;
    }

    public void TakeDamage(int damage, ICollection<GameEvent> events, bool critical = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(damage);
        ArgumentNullException.ThrowIfNull(events);
        if (IsDead)
        {
            return;
        }

        Health = Math.Max(0, Health - damage);
        events.Add(new GhoulHit(Id, damage, critical));

        // Being hit wakes it up, even if it never saw the attacker.
        _alerted = true;
        if (IsDead)
        {
            _corpseTicksLeft = _corpseTicks;
            events.Add(new GhoulDied(Id));
        }
    }

    /// <summary>
    /// One tick of behaviour: notice players, crawl, wind up, strike. <paramref name="obstacles"/> are the bodies it
    /// cannot crawl through (players, other ghouls, standing dummies, chests - not itself). <paramref name="paths"/>
    /// is the level it finds its way in, if that differs (chest cells are wall there, so it plans a way round them).
    /// </summary>
    public void Act(
        Level level, IReadOnlyList<Player> players, IReadOnlyList<CylinderBody> obstacles, ICollection<GameEvent> events,
        Level? paths = null)
    {
        _paths = paths;
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(obstacles);
        ArgumentNullException.ThrowIfNull(events);
        if (IsDead)
        {
            Velocity = Vector3.Zero;
            return;
        }

        // Knocked back by a heavy blow: it slides away and does nothing else meanwhile (its wind-up waits).
        if (Knockback.IsActive)
        {
            Move(level, Knockback.Velocity, obstacles);
            Knockback.Tick();
            return;
        }

        if (_target is { IsDead: true })
        {
            // Its prey is dead: back to lurking (with online co-op it would look for the next player).
            State = GhoulState.Lurking;
            _target = null;
            _alerted = false;
        }

        if (State == GhoulState.Lurking)
        {
            // Online co-op rule 1: choose from THE players, not "the player" - and only living ones.
            _target = players.FirstOrDefault(player => !player.IsDead && CanSee(level, player))
                ?? (_alerted ? Nearest(players) : null);
            if (_target is null)
            {
                Stand(level, obstacles);
                return;
            }

            State = GhoulState.Chasing;
            events.Add(new GhoulNoticed(Id, _target.Id));
        }

        var target = _target!;
        switch (State)
        {
            case GhoulState.Chasing:
                if (InReach(target))
                {
                    // Commit to a direction: the blow goes where it faces now. Stepping aside or away helps.
                    FaceTowards(target.Position);
                    State = GhoulState.WindingUp;
                    _stateTicksLeft = _windUpTicks;
                    events.Add(new GhoulWindUp(Id));
                    Stand(level, obstacles);
                }
                else
                {
                    Crawl(level, target, obstacles);
                }

                break;

            case GhoulState.WindingUp:
                Stand(level, obstacles);
                if (--_stateTicksLeft == 0)
                {
                    var hit = InHitReach(target) && InFront(target);
                    if (hit)
                    {
                        target.TakeDamage(_ghoul.AttackDamage, events);
                    }

                    events.Add(new GhoulStruck(Id, hit));
                    State = GhoulState.Recovering;
                    _stateTicksLeft = _recoverTicks;
                }

                break;

            case GhoulState.Recovering:
                if (--_stateTicksLeft > 0)
                {
                    Stand(level, obstacles);
                    break;
                }

                // Rested: carry on in the same tick (strike again at once if the player is still close).
                State = GhoulState.Chasing;
                goto case GhoulState.Chasing;
        }
    }

    /// <summary>A player it can see: closer than its sight range and nothing in between (from eye to eye).</summary>
    private bool CanSee(Level level, Player player)
    {
        var eye = Position + new Vector3(0f, BodySizes.Ghoul.EyeHeight, 0f);
        return FlatDistance(Position, player.Position) <= _ghoul.SightRange
            && LineOfSight.IsClear(level, eye, player.EyePosition);
    }

    private Player? Nearest(IReadOnlyList<Player> players) =>
        players.Where(player => !player.IsDead).OrderBy(player => FlatDistance(Position, player.Position)).FirstOrDefault();

    /// <summary>Close enough to start a blow.</summary>
    private bool InReach(Player player) => IsWithin(player, _ghoul.AttackReach);

    /// <summary>Close enough to be hit by the blow - a bit farther than where it starts.</summary>
    private bool InHitReach(Player player) => IsWithin(player, _ghoul.HitReach);

    private bool IsWithin(Player player, float reach) =>
        FlatDistance(Position, player.Position) <= reach
        && MathF.Abs(player.Position.Y - Position.Y) < BodySizes.Ghoul.Height;

    private bool InFront(Player player)
    {
        var toPlayer = (player.Position - Position) with { Y = 0f };
        return toPlayer.LengthSquared() < 0.0001f || Vector3.Dot(Vector3.Normalize(toPlayer), Facing) >= _attackCosine;
    }

    // The level to find its way in: the one with chests as walls - unless it stands in such a cell itself (sliding
    // round a chest), where a search could not even start.
    private Level WaysIn(Level level) => _paths is { } paths && !paths.CellAt(Position).IsSolid ? paths : level;

    /// <summary>
    /// One tick of crawling towards the target: straight at them if nothing is in the way, otherwise along the
    /// smoothed path from the grid search - searched again now and then, or when the target changed its cell.
    /// </summary>
    private void Crawl(Level level, Player target, IReadOnlyList<CylinderBody> obstacles)
    {
        var ways = WaysIn(level);
        Vector3? next = null;
        if (Pathfinder.CanWalkStraight(ways, Position, target.Position, BodySizes.Ghoul.Radius))
        {
            next = target.Position;
            _waypoints.Clear();
        }
        else
        {
            var goal = Level.ToGrid(target.Position.X, target.Position.Z);
            if (--_repathTicksLeft <= 0 || goal != _pathGoal || _waypoints.Count == 0)
            {
                var start = Level.ToGrid(Position.X, Position.Z);
                var path = Pathfinder.FindPath(ways, start, goal);
                _waypoints = path is null ? [] : Pathfinder.Smooth(ways, path, BodySizes.Ghoul.Radius).ToList();
                _repathTicksLeft = _repathTicks;
                _pathGoal = goal;
            }

            while (_waypoints.Count > 0 && FlatDistance(Position, _waypoints[0]) < 0.25f)
            {
                _waypoints.RemoveAt(0);
            }

            if (_waypoints.Count > 0)
            {
                next = _waypoints[0];
            }
        }

        if (next is not { } point || FlatDistance(Position, point) < 0.001f)
        {
            Stand(level, obstacles);
            return;
        }

        FaceTowards(point);
        Move(level, Facing * _ghoul.Speed, obstacles);
    }

    private void Stand(Level level, IReadOnlyList<CylinderBody> obstacles) => Move(level, Vector3.Zero, obstacles);

    /// <summary>Moves with the same collision rules as the player: walls, steps, other bodies, gravity.</summary>
    private void Move(Level level, Vector3 flatVelocity, IReadOnlyList<CylinderBody> obstacles)
    {
        const float Dt = SimulationClock.SecondsPerTick;
        var velocity = flatVelocity with { Y = Velocity.Y };
        (var position, velocity) = CharacterCollision.Move(
            level, Position, velocity, BodySizes.Ghoul.Radius, Dt, BodySizes.Ghoul.Height, bodies: obstacles);
        (position, velocity, _) = CharacterCollision.MoveVertically(
            level, position, velocity, BodySizes.Ghoul.Radius, BodySizes.Ghoul.Height, _gravity, Dt);
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

    /// <summary>Advances the timers by one tick: it rises further out of the floor; a dead body disappears after a while.</summary>
    public void Tick(ICollection<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        Emergence.Tick();
        if (_corpseTicksLeft > 0)
        {
            _corpseTicksLeft--;
        }
    }
}
