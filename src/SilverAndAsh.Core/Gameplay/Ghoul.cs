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
    private static readonly int CorpseTicks = SimulationClock.SecondsToTicks(Tuning.Ghoul.CorpseTime);
    private static readonly int WindUpTicks = SimulationClock.SecondsToTicks(Tuning.Ghoul.WindUpTime);
    private static readonly int RecoverTicks = SimulationClock.SecondsToTicks(Tuning.Ghoul.RecoverTime);
    private static readonly int RepathTicks = SimulationClock.SecondsToTicks(Tuning.Ghoul.RepathTime);
    private static readonly float AttackCosine = MathF.Cos(Tuning.Ghoul.AttackHalfAngle * MathF.PI / 180f);

    private int _corpseTicksLeft;
    private int _stateTicksLeft;
    private bool _alerted;
    private Player? _target;
    private List<Vector3> _waypoints = [];
    private int _repathTicksLeft;
    private (int X, int Z) _pathGoal;

    /// <summary>Creates a ghoul at <paramref name="position"/> (centre of its feet), looking along <paramref name="facing"/>.</summary>
    public Ghoul(int id, Vector3 position, Vector3? facing = null)
    {
        Id = id;
        Position = position;
        Facing = facing ?? Vector3.UnitZ;
    }

    public int Id { get; }

    /// <summary>Centre of its feet in world coordinates.</summary>
    public Vector3 Position { get; private set; }

    /// <summary>Its movement in metres per second.</summary>
    public Vector3 Velocity { get; private set; }

    /// <summary>The flat direction it looks in (length 1). Its head is at the front.</summary>
    public Vector3 Facing { get; private set; }

    /// <summary>What it is doing right now.</summary>
    public GhoulState State { get; private set; } = GhoulState.Lurking;

    /// <summary>How far the wind-up has come: 0 at its start, 1 at the blow. 0 in every other state. For drawing.</summary>
    public float WindUpProgress => State == GhoulState.WindingUp ? 1f - (float)_stateTicksLeft / WindUpTicks : 0f;

    /// <summary>Current health; 0 when dead.</summary>
    public int Health { get; private set; } = Tuning.Ghoul.MaxHealth;

    /// <summary>True once its health reached 0.</summary>
    public bool IsDead => Health == 0;

    /// <summary>True when the dead body has disappeared - it is no longer drawn.</summary>
    public bool IsGone => IsDead && _corpseTicksLeft == 0;

    public bool IsHittable => !IsDead;

    public CylinderBody Body => new(Position, Tuning.Ghoul.Radius, Tuning.Ghoul.Height);

    /// <summary>The head: the top part of the body, in its front half.</summary>
    public HitZone ZoneAt(Vector3 point)
    {
        var high = point.Y >= Position.Y + Tuning.Ghoul.Height - Tuning.Ghoul.HeadHeight;
        var flatOffset = (point - Position) with { Y = 0f };
        var inFront = Vector3.Dot(flatOffset, Facing) > 0f;
        return high && inFront ? HitZone.Head : HitZone.Body;
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
            _corpseTicksLeft = CorpseTicks;
            events.Add(new GhoulDied(Id));
        }
    }

    /// <summary>
    /// One tick of behaviour: notice players, crawl, wind up, strike. <paramref name="obstacles"/> are the bodies it
    /// cannot crawl through (players, other ghouls, standing dummies - not itself).
    /// </summary>
    public void Act(Level level, IReadOnlyList<Player> players, IReadOnlyList<CylinderBody> obstacles, ICollection<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(obstacles);
        ArgumentNullException.ThrowIfNull(events);
        if (IsDead)
        {
            Velocity = Vector3.Zero;
            return;
        }

        if (State == GhoulState.Lurking)
        {
            // Online co-op rule 1: choose from THE players, not "the player".
            _target = players.FirstOrDefault(player => CanSee(level, player)) ?? (_alerted ? Nearest(players) : null);
            if (_target is null)
            {
                Stand(level, obstacles);
                return;
            }

            State = GhoulState.Chasing;
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
                    _stateTicksLeft = WindUpTicks;
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
                    var hit = InReach(target) && InFront(target);
                    if (hit)
                    {
                        target.TakeDamage(Tuning.Ghoul.AttackDamage, events);
                    }

                    events.Add(new GhoulStruck(Id, hit));
                    State = GhoulState.Recovering;
                    _stateTicksLeft = RecoverTicks;
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
        var eye = Position + new Vector3(0f, Tuning.Ghoul.EyeHeight, 0f);
        return FlatDistance(Position, player.Position) <= Tuning.Ghoul.SightRange
            && LineOfSight.IsClear(level, eye, player.EyePosition);
    }

    private Player? Nearest(IReadOnlyList<Player> players) =>
        players.OrderBy(player => FlatDistance(Position, player.Position)).FirstOrDefault();

    private bool InReach(Player player) =>
        FlatDistance(Position, player.Position) <= Tuning.Ghoul.AttackReach
        && MathF.Abs(player.Position.Y - Position.Y) < Tuning.Ghoul.Height;

    private bool InFront(Player player)
    {
        var toPlayer = (player.Position - Position) with { Y = 0f };
        return toPlayer.LengthSquared() < 0.0001f || Vector3.Dot(Vector3.Normalize(toPlayer), Facing) >= AttackCosine;
    }

    /// <summary>
    /// One tick of crawling towards the target: straight at them if nothing is in the way, otherwise along the
    /// smoothed path from the grid search - searched again now and then, or when the target changed its cell.
    /// </summary>
    private void Crawl(Level level, Player target, IReadOnlyList<CylinderBody> obstacles)
    {
        Vector3? next = null;
        if (Pathfinder.CanWalkStraight(level, Position, target.Position, Tuning.Ghoul.Radius))
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
                var path = Pathfinder.FindPath(level, start, goal);
                _waypoints = path is null ? [] : Pathfinder.Smooth(level, path, Tuning.Ghoul.Radius).ToList();
                _repathTicksLeft = RepathTicks;
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
        Move(level, Facing * Tuning.Ghoul.Speed, obstacles);
    }

    private void Stand(Level level, IReadOnlyList<CylinderBody> obstacles) => Move(level, Vector3.Zero, obstacles);

    /// <summary>Moves with the same collision rules as the player: walls, steps, other bodies, gravity.</summary>
    private void Move(Level level, Vector3 flatVelocity, IReadOnlyList<CylinderBody> obstacles)
    {
        const float Dt = SimulationClock.SecondsPerTick;
        var velocity = flatVelocity with { Y = Velocity.Y };
        (var position, velocity) = CharacterCollision.Move(
            level, Position, velocity, Tuning.Ghoul.Radius, Dt, Tuning.Ghoul.Height, bodies: obstacles);
        (position, velocity, _) = CharacterCollision.MoveVertically(
            level, position, velocity, Tuning.Ghoul.Radius, Tuning.Ghoul.Height, Tuning.Player.Gravity, Dt);
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

    /// <summary>Advances the timers by one tick: a dead body disappears after a while.</summary>
    public void Tick(ICollection<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (_corpseTicksLeft > 0)
        {
            _corpseTicksLeft--;
        }
    }
}
