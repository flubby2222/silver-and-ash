using System.Numerics;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>What a bat is doing right now.</summary>
public enum BatState
{
    /// <summary>Hanging still in the air until it notices a player.</summary>
    Lurking,

    /// <summary>Flying round its target (or finding its way to it), spitting now and then.</summary>
    Circling,

    /// <summary>Hovering and screeching before a dive - the moment to step aside.</summary>
    Screeching,

    /// <summary>Diving straight at the spot where its target was when it screeched.</summary>
    Diving,

    /// <summary>Flying back up to its circling height after a dive.</summary>
    Rising,
}

/// <summary>
/// The vampire bat: a small flying enemy. It hangs still until it sees a player (or is hit), then circles them,
/// spits blood bolts now and then and every few seconds dives at them for a bite. Every attack has a warning: it glows
/// before a spit and screeches before a dive. At 0 health it tumbles to the floor, lies there for a moment and is gone.
/// <para>
/// It flies: no gravity while alive, floor heights do not matter, only walls (and the ceiling) stop it. Around
/// corners it finds its way with the same grid search as the ghoul (<see cref="Pathfinder"/>), in "flying mode".
/// </para>
/// </summary>
public sealed class Bat : ITarget
{
    /// <summary>Flying: any floor height can be passed, only walls block.</summary>
    private const float FlyingStepHeight = float.PositiveInfinity;

    private const float HalfHeight = Tuning.Bat.Height / 2f;

    private static readonly int CorpseTicks = SimulationClock.SecondsToTicks(Tuning.Bat.CorpseTime);
    private static readonly int SpitTicks = SimulationClock.SecondsToTicks(Tuning.Bat.SpitInterval);
    private static readonly int SpitWindUpTicks = SimulationClock.SecondsToTicks(Tuning.Bat.SpitWindUpTime);
    private static readonly int DiveTicks = SimulationClock.SecondsToTicks(Tuning.Bat.DiveInterval);
    private static readonly int ScreechTicks = SimulationClock.SecondsToTicks(Tuning.Bat.ScreechTime);
    private static readonly int RepathTicks = SimulationClock.SecondsToTicks(Tuning.Bat.RepathTime);
    private static readonly int BobTicks = SimulationClock.SecondsToTicks(Tuning.Bat.BobTime);

    /// <summary>Pressed against a wall for this many ticks, it turns round and circles the other way.</summary>
    private static readonly int TurnRoundTicks = SimulationClock.SecondsToTicks(0.25f);

    private int _corpseTicksLeft;
    private Vector3 _velocity;

    private bool _alerted;
    private Player? _target;
    private int _spitTicksLeft;
    private bool _spitWarned;
    private int _diveTicksLeft;
    private int _stateTicksLeft;
    private Vector3 _diveGoal;
    private float _circleDirection = 1f;
    private int _blockedTicks;
    private int _bobTick;
    private List<Vector3> _waypoints = [];
    private int _repathTicksLeft;
    private (int X, int Z) _pathGoal;

    /// <summary>Creates a bat whose body starts at <paramref name="position"/> (the bottom of its body, in the air).</summary>
    public Bat(int id, Vector3 position)
    {
        Id = id;
        Position = position;
    }

    public int Id { get; }

    /// <summary>What it is doing right now.</summary>
    public BatState State { get; private set; } = BatState.Lurking;

    /// <summary>How far the spit warning has come: 0 before it, 1 at the spit. For drawing.</summary>
    public float SpitWindUpProgress => _spitWarned ? 1f - (float)_spitTicksLeft / SpitWindUpTicks : 0f;

    /// <summary>How far the screech has come: 0 at its start, 1 at the dive. 0 in every other state. For drawing.</summary>
    public float ScreechProgress => State == BatState.Screeching ? 1f - (float)_stateTicksLeft / ScreechTicks : 0f;

    /// <summary>Bottom centre of its body in world coordinates (it flies, so this is in the air).</summary>
    public Vector3 Position { get; private set; }

    /// <summary>Middle of its body - where it spits from.</summary>
    public Vector3 Centre => Position + new Vector3(0f, HalfHeight, 0f);

    /// <summary>Current health; 0 when dead.</summary>
    public int Health { get; private set; } = Tuning.Bat.MaxHealth;

    /// <summary>True once its health reached 0.</summary>
    public bool IsDead => Health == 0;

    /// <summary>True when the dead body has disappeared - it is no longer drawn.</summary>
    public bool IsGone => IsDead && _corpseTicksLeft == 0;

    public bool IsHittable => !IsDead;

    public CylinderBody Body => new(Position, Tuning.Bat.Radius, Tuning.Bat.Height);

    /// <summary>A bat is too small for a head zone: everything is body.</summary>
    public HitZone ZoneAt(Vector3 point) => HitZone.Body;

    public void TakeDamage(int damage, ICollection<GameEvent> events, bool critical = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(damage);
        ArgumentNullException.ThrowIfNull(events);
        if (IsDead)
        {
            return;
        }

        var taken = Math.Min(damage, Health);
        Health -= taken;
        events.Add(new BatHit(Id, taken));

        // Being hit wakes it up, even if it never saw the attacker.
        _alerted = true;
        if (IsDead)
        {
            _corpseTicksLeft = CorpseTicks;
            events.Add(new BatDied(Id));
        }
    }

    /// <summary>
    /// One tick of behaviour: notice players, circle, spit, screech, dive, bite, rise. A spit is only reported
    /// (<see cref="BatSpat"/>) - the world turns it into a blood bolt.
    /// </summary>
    public void Act(Level level, IReadOnlyList<Player> players, ICollection<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(events);
        if (IsDead)
        {
            return;
        }

        if (_target is { IsDead: true })
        {
            // Its prey is dead: it hangs still where it is (with online co-op it would look for the next player).
            State = BatState.Lurking;
            _target = null;
            _alerted = false;
            _spitWarned = false;
        }

        if (State == BatState.Lurking)
        {
            // Online co-op rule 1: choose from THE players, not "the player" - and only living ones.
            _target = players.FirstOrDefault(player => !player.IsDead && CanSee(level, player))
                ?? (_alerted ? Nearest(players) : null);
            if (_target is null)
            {
                return;
            }

            State = BatState.Circling;
            _spitTicksLeft = SpitTicks;
            _diveTicksLeft = DiveTicks;
        }

        var target = _target!;
        switch (State)
        {
            case BatState.Circling:
                Circle(level, target);
                Spit(level, target, events);
                PrepareDive(level, target, events);
                break;

            case BatState.Screeching:
                if (--_stateTicksLeft == 0)
                {
                    State = BatState.Diving;
                }

                break;

            case BatState.Diving:
                Dive(level, players, events);
                break;

            case BatState.Rising:
                var height = CircleHeight(level);
                Position = Position with { Y = MathF.Min(height, Position.Y + Tuning.Bat.ClimbSpeed * SimulationClock.SecondsPerTick) };
                if (Position.Y >= height)
                {
                    State = BatState.Circling;
                    _diveTicksLeft = DiveTicks;
                }

                break;
        }
    }

    /// <summary>
    /// Counts down to the next spit. When it is near, it starts glowing - but only while it can see its target;
    /// otherwise it waits. After the glow it spits at the target's chest (a bit below the eyes).
    /// </summary>
    private void Spit(Level level, Player target, ICollection<GameEvent> events)
    {
        if (!_spitWarned)
        {
            if (_spitTicksLeft > SpitWindUpTicks)
            {
                _spitTicksLeft--;
            }

            if (_spitTicksLeft <= SpitWindUpTicks && CanSee(level, target))
            {
                _spitWarned = true;
                events.Add(new BatSpitWindUp(Id));
            }

            return;
        }

        if (--_spitTicksLeft > 0)
        {
            return;
        }

        var aim = target.EyePosition - new Vector3(0f, Tuning.Bat.SpitAimBelowEyes, 0f);
        events.Add(new BatSpat(Id, Centre, Vector3.Normalize(aim - Centre)));
        _spitWarned = false;
        _spitTicksLeft = SpitTicks;
    }

    /// <summary>
    /// Counts down to the next dive. When it is due and the target is in sight, the bat stops and screeches - and
    /// fixes the spot it will dive at: where the target is NOW. Whoever moves away during the screech is missed.
    /// While it glows for a spit, the dive waits until the spit is out.
    /// </summary>
    private void PrepareDive(Level level, Player target, ICollection<GameEvent> events)
    {
        if (_diveTicksLeft > 0)
        {
            _diveTicksLeft--;
        }

        if (_diveTicksLeft > 0 || _spitWarned || !CanSee(level, target))
        {
            return;
        }

        State = BatState.Screeching;
        _stateTicksLeft = ScreechTicks;
        _diveGoal = target.Position + new Vector3(0f, target.Body.Height / 2f, 0f);
        events.Add(new BatScreeched(Id));
    }

    /// <summary>
    /// One tick of the dive: straight at the fixed spot. Touching a living player is a bite; reaching the spot (or a
    /// wall or the floor) without one is a miss. Either way it rises again afterwards.
    /// </summary>
    private void Dive(Level level, IReadOnlyList<Player> players, ICollection<GameEvent> events)
    {
        var toGoal = _diveGoal - Centre;
        var distance = toGoal.Length();
        var step = MathF.Min(Tuning.Bat.DiveSpeed * SimulationClock.SecondsPerTick, distance);
        var next = distance > 0.0001f ? Position + toGoal / distance * step : Position;

        var floor = level.CellAt(next).FloorHeight;
        var blocked = next.Y < floor
            || CharacterCollision.Overlaps(level, next, Tuning.Bat.Radius, Tuning.Bat.Height, FlyingStepHeight);
        if (!blocked)
        {
            Position = next;
        }

        var victim = players.FirstOrDefault(player => !player.IsDead && Touches(player.Body));
        if (victim is not null)
        {
            victim.TakeDamage(Tuning.Bat.BiteDamage, events);
            events.Add(new BatBit(Id, victim.Id));
            State = BatState.Rising;
        }
        else if (blocked || step >= distance)
        {
            events.Add(new BatDiveMissed(Id));
            State = BatState.Rising;
        }
    }

    /// <summary>
    /// One tick of circling: flies round the target at <see cref="Tuning.Bat.CircleRadius"/>, rising and sinking a
    /// little. If a wall is between them, it first finds its way round. Pressed against a wall, it turns round.
    /// </summary>
    private void Circle(Level level, Player target)
    {
        Vector3 velocity;
        if (Pathfinder.CanWalkStraight(level, Position, target.Position, Tuning.Bat.Radius, FlyingStepHeight))
        {
            _waypoints.Clear();
            var offset = (Position - target.Position) with { Y = 0f };
            var distance = offset.Length();
            var outward = distance > 0.01f ? offset / distance : Vector3.UnitX;
            var along = new Vector3(-outward.Z, 0f, outward.X) * _circleDirection;

            // Along the circle, plus a pull back onto it when too close or too far.
            velocity = along * Tuning.Bat.CircleSpeed + outward * ((Tuning.Bat.CircleRadius - distance) * 2f);
        }
        else
        {
            velocity = AlongPath(level, target);
        }

        if (velocity.Length() > Tuning.Bat.FlySpeed)
        {
            velocity = Vector3.Normalize(velocity) * Tuning.Bat.FlySpeed;
        }

        var (position, slid) = CharacterCollision.Move(
            level, Position, velocity, Tuning.Bat.Radius, SimulationClock.SecondsPerTick, Tuning.Bat.Height, FlyingStepHeight);

        // A wall took most of the movement away for a while: circle the other way round.
        _blockedTicks = velocity.Length() > 0.5f && slid.Length() < velocity.Length() / 2f ? _blockedTicks + 1 : 0;
        if (_blockedTicks >= TurnRoundTicks)
        {
            _circleDirection = -_circleDirection;
            _blockedTicks = 0;
        }

        // Up and down: a slow wave between the lowest and the highest circling height.
        _bobTick++;
        var height = CircleHeight(level);
        var maxClimb = Tuning.Bat.ClimbSpeed * SimulationClock.SecondsPerTick;
        Position = position with { Y = Position.Y + Math.Clamp(height - Position.Y, -maxClimb, maxClimb) };
    }

    /// <summary>The velocity towards the next point of the way round the walls (searched again now and then).</summary>
    private Vector3 AlongPath(Level level, Player target)
    {
        var goal = Level.ToGrid(target.Position.X, target.Position.Z);
        if (--_repathTicksLeft <= 0 || goal != _pathGoal || _waypoints.Count == 0)
        {
            var start = Level.ToGrid(Position.X, Position.Z);
            var path = Pathfinder.FindPath(level, start, goal, FlyingStepHeight);
            _waypoints = path is null ? [] : Pathfinder.Smooth(level, path, Tuning.Bat.Radius, FlyingStepHeight).ToList();
            _repathTicksLeft = RepathTicks;
            _pathGoal = goal;
        }

        while (_waypoints.Count > 0 && FlatDistance(Position, _waypoints[0]) < 0.25f)
        {
            _waypoints.RemoveAt(0);
        }

        if (_waypoints.Count == 0)
        {
            return Vector3.Zero;
        }

        var toNext = (_waypoints[0] - Position) with { Y = 0f };
        return Vector3.Normalize(toNext) * Tuning.Bat.FlySpeed;
    }

    /// <summary>
    /// The height it wants to be at while circling: a slow wave between <see cref="Tuning.Bat.CircleHeightMin"/> and
    /// <see cref="Tuning.Bat.CircleHeightMax"/> above the floor below it - but never too close to the ceiling.
    /// </summary>
    private float CircleHeight(Level level)
    {
        var cell = level.CellAt(Position);
        var middle = (Tuning.Bat.CircleHeightMin + Tuning.Bat.CircleHeightMax) / 2f;
        var swing = (Tuning.Bat.CircleHeightMax - Tuning.Bat.CircleHeightMin) / 2f;
        var wave = MathF.Sin(2f * MathF.PI * _bobTick / BobTicks);
        var highest = cell.CeilingHeight - Tuning.Bat.CeilingGap - Tuning.Bat.Height;
        return MathF.Min(cell.FloorHeight + middle + swing * wave, highest);
    }

    /// <summary>A player it can see: closer than its sight range and nothing in between (from its body to their eyes).</summary>
    private bool CanSee(Level level, Player player) =>
        Vector3.Distance(Centre, player.EyePosition) <= Tuning.Bat.SightRange
        && LineOfSight.IsClear(level, Centre, player.EyePosition);

    private Player? Nearest(IReadOnlyList<Player> players) =>
        players.Where(player => !player.IsDead).OrderBy(player => FlatDistance(Position, player.Position)).FirstOrDefault();

    /// <summary>Does its body touch <paramref name="body"/>? (Two upright cylinders.)</summary>
    private bool Touches(CylinderBody body) =>
        FlatDistance(Position, body.Position) <= Tuning.Bat.Radius + body.Radius
        && Position.Y < body.Position.Y + body.Height
        && body.Position.Y < Position.Y + Tuning.Bat.Height;

    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    /// <summary>One tick of the body: dead it falls to the floor and disappears after a while.</summary>
    public void Tick(Level level, ICollection<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(events);
        if (!IsDead)
        {
            return;
        }

        // Dead: gravity takes over until it lies on the floor.
        (Position, _velocity, _) = CharacterCollision.MoveVertically(
            level, Position, _velocity, Tuning.Bat.Radius, Tuning.Bat.Height, Tuning.Player.Gravity, SimulationClock.SecondsPerTick);

        if (_corpseTicksLeft > 0)
        {
            _corpseTicksLeft--;
        }
    }

    /// <summary>
    /// Where a bat starting in a cell hovers: <see cref="Tuning.Bat.HoverHeight"/> above the floor, but always
    /// with some room below the ceiling.
    /// </summary>
    public static Vector3 HoverPosition(Level level, Vector3 floorPosition)
    {
        ArgumentNullException.ThrowIfNull(level);
        var ceiling = level.CellAt(floorPosition).CeilingHeight;
        var highest = ceiling - Tuning.Bat.CeilingGap - Tuning.Bat.Height;
        return floorPosition with { Y = MathF.Min(floorPosition.Y + Tuning.Bat.HoverHeight, highest) };
    }
}
