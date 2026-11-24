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
public sealed class Bat : IEnemy
{
    private const float HalfHeight = BodySizes.Bat.Height / 2f;

    private readonly BatBalance _bat;
    private readonly float _gravity;
    private readonly int _corpseTicks;
    private readonly int _spitTicks;
    private readonly int _spitWindUpTicks;
    private readonly int _diveTicks;
    private readonly int _screechTicks;
    private readonly int _repathTicks;
    private readonly int _bobTicks;

    /// <summary>Pressed against a wall for this many ticks, it turns round and circles the other way.</summary>
    private static readonly int TurnRoundTicks = SimulationClock.SecondsToTicks(0.25f);

    private readonly int _turnRoundMinTicks;
    private readonly int _turnRoundMaxTicks;

    /// <summary>
    /// Seconds between two wing beats that are reported (<see cref="BatFlapped"/>, issue #297) - the rhythm of the
    /// flapping sound, within the 0.3 to 0.5 s the audio designer asked for. Presentation only, not a game rule: no
    /// value of balance.json, and nothing the bat does depends on it.
    /// </summary>
    public const float FlapInterval = 0.4f;

    private static readonly int FlapTicks = SimulationClock.SecondsToTicks(FlapInterval);

    private readonly Random _random;
    private int _corpseTicksLeft;
    private Vector3 _velocity;
    private int _turnTicksLeft;

    // Ticks to the next reported wing beat. Starts from the bat's number, not from the world's random numbers (those
    // would change the run): several bats flap out of step with each other.
    private int _flapTicksLeft;

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

    /// <summary>
    /// Creates a bat whose body starts at <paramref name="position"/> (the bottom of its body, in the air).
    /// <paramref name="random"/> is the world's random generator (online co-op rule 3: no chance of its own); from it
    /// every bat gets its own circle - distance, direction and the beat of its rising and sinking - and the moments
    /// it turns round. Without one (single bats in tests) it uses a generator started from its number.
    /// <paramref name="balance"/> sets its health, speeds and attack rhythm (the standard values if none is given).
    /// </summary>
    public Bat(int id, Vector3 position, Random? random = null, Balance? balance = null)
    {
        Id = id;
        Position = position;

        balance ??= Balance.Default;
        _bat = balance.Bat;
        _gravity = balance.Player.Gravity;
        _corpseTicks = SimulationClock.SecondsToTicks(_bat.CorpseTime);
        _spitTicks = SimulationClock.SecondsToTicks(_bat.SpitInterval);
        _spitWindUpTicks = SimulationClock.SecondsToTicks(_bat.SpitWindUpTime);
        _diveTicks = SimulationClock.SecondsToTicks(_bat.DiveInterval);
        _screechTicks = SimulationClock.SecondsToTicks(_bat.ScreechTime);
        _repathTicks = SimulationClock.SecondsToTicks(_bat.RepathTime);
        _bobTicks = SimulationClock.SecondsToTicks(_bat.BobTime);
        _turnRoundMinTicks = SimulationClock.SecondsToTicks(_bat.TurnRoundMin);
        _turnRoundMaxTicks = SimulationClock.SecondsToTicks(_bat.TurnRoundMax);
        Health = _bat.MaxHealth;
        _flapTicksLeft = 1 + Math.Abs(id) % FlapTicks;

        // Always drawn in this order - the same seed must give the same bats.
        _random = random ?? new Random(id);
        CircleRadius = _bat.CircleRadiusMin + (float)_random.NextDouble() * (_bat.CircleRadiusMax - _bat.CircleRadiusMin);
        _circleDirection = _random.Next(2) == 0 ? 1f : -1f;
        _bobTick = _random.Next(_bobTicks);
        _turnTicksLeft = NextTurnTicks();
    }

    public int Id { get; }

    /// <summary>What it is doing right now.</summary>
    public BatState State { get; private set; } = BatState.Lurking;

    /// <summary>Its own distance to the player while circling, in metres (chosen at random when it is created).</summary>
    public float CircleRadius { get; }

    /// <summary>Which way round it circles: +1 or -1. Changes now and then.</summary>
    public float CircleDirection => _circleDirection;

    /// <summary>How far the spit warning has come: 0 before it, 1 at the spit. For drawing.</summary>
    public float SpitWindUpProgress => _spitWarned ? 1f - (float)_spitTicksLeft / _spitWindUpTicks : 0f;

    /// <summary>How far the screech has come: 0 at its start, 1 at the dive. 0 in every other state. For drawing.</summary>
    public float ScreechProgress => State == BatState.Screeching ? 1f - (float)_stateTicksLeft / _screechTicks : 0f;

    /// <summary>Bottom centre of its body in world coordinates (it flies, so this is in the air).</summary>
    public Vector3 Position { get; private set; }

    /// <summary>A push from a heavy blow (the club) - while it lasts the bat is flung away and does nothing else.</summary>
    public Knockback Knockback { get; } = new();

    /// <summary>Middle of its body - where it spits from.</summary>
    public Vector3 Centre => Position + new Vector3(0f, HalfHeight, 0f);

    /// <summary>Current health; 0 when dead.</summary>
    public int Health { get; private set; }

    /// <summary>True once its health reached 0.</summary>
    public bool IsDead => Health == 0;

    /// <summary>True when the dead body has disappeared - it is no longer drawn.</summary>
    public bool IsGone => IsDead && _corpseTicksLeft == 0;

    /// <summary>
    /// Coming out of the ceiling (issue #424): in a combat room it waits hidden until the room seals. While hidden or
    /// sinking it cannot be hit (and the world does not let it act).
    /// </summary>
    public Emergence Emergence { get; } = new();

    /// <summary>Where it is drawn: its position, pulled up into the ceiling while it is hidden or sinking.</summary>
    public Vector3 ShownPosition => Position + Emergence.Offset;

    /// <summary>
    /// Moves it while it is still hidden (issue #445: it comes out elsewhere, away from the players) or out of a doorway
    /// that shuts (issue #558).
    /// </summary>
    internal void PlaceAt(Vector3 position) => Position = position;

    void IEnemy.PlaceAt(Vector3 position) => PlaceAt(position);

    bool IEnemy.IsHidden => Emergence.IsHidden;

    bool IEnemy.IsOut => Emergence.IsPresent;

    public bool IsHittable => !IsDead && Emergence.IsPresent;

    public CylinderBody Body => new(Position, BodySizes.Bat.Radius, BodySizes.Bat.Height);

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
        events.Add(new BatHit(Id, taken) { Place = Centre });

        // Being hit wakes it up, even if it never saw the attacker.
        _alerted = true;
        if (IsDead)
        {
            _corpseTicksLeft = _corpseTicks;
            events.Add(new BatDied(Id) { Place = Centre });
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

        Flap(events);

        // Flung away by a heavy blow: it flies with the push (walls stop it) and does nothing else meanwhile.
        if (Knockback.IsActive)
        {
            (Position, _) = CharacterCollision.Move(
                level, Position, Knockback.Velocity, BodySizes.Bat.Radius, SimulationClock.SecondsPerTick, BodySizes.Bat.Height, BodySizes.Bat.StepHeight);
            Knockback.Tick();
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
            _spitTicksLeft = _spitTicks;
            _diveTicksLeft = _diveTicks;
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
                Rise(level);
                break;
        }
    }

    /// <summary>
    /// One tick of rising after a dive, up to its circling height. Stone of a boss portal overhead (the impost) ends the
    /// climb early: it circles on from where it is and climbs once it is clear of the stone (issue #627).
    /// </summary>
    private void Rise(Level level)
    {
        var height = CircleHeight(level);
        var climbed = ClimbClearOfStone(level, MathF.Min(height, Position.Y + _bat.ClimbSpeed * SimulationClock.SecondsPerTick));
        if (Position.Y >= height || !climbed)
        {
            State = BatState.Circling;
            _diveTicksLeft = _diveTicks;
        }
    }

    /// <summary>
    /// Climbs or sinks to <paramref name="height"/> (the bottom of its body) - unless its body would reach into stone
    /// there: the walls only stop it on the ground plane at its present height (<see cref="CharacterCollision.Move"/>),
    /// so a bat beside a boss portal would rise into the impost that juts out above it (issue #627). Then it keeps its
    /// height for this tick. False if it was held. A column or a wall it is pressed against does not hold it: they
    /// stand in its way at every height alike (issue #635).
    /// </summary>
    private bool ClimbClearOfStone(Level level, float height)
    {
        var there = Position with { Y = height };
        if (height != Position.Y
            && CharacterCollision.BlockedAtHeight(level, there, BodySizes.Bat.Radius, BodySizes.Bat.Height, BodySizes.Bat.StepHeight))
        {
            return false;
        }

        Position = there;
        return true;
    }

    /// <summary>
    /// Reports a wing beat every <see cref="FlapInterval"/> while it is awake (anything but hanging still unnoticed) -
    /// only for the sound; the counter keeps running while it lurks, so waking up does not flap at once.
    /// </summary>
    private void Flap(ICollection<GameEvent> events)
    {
        if (--_flapTicksLeft > 0)
        {
            return;
        }

        _flapTicksLeft = FlapTicks;
        if (State != BatState.Lurking)
        {
            events.Add(new BatFlapped(Id, Centre) { Place = Centre });
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
            if (_spitTicksLeft > _spitWindUpTicks)
            {
                _spitTicksLeft--;
            }

            if (_spitTicksLeft <= _spitWindUpTicks && CanSee(level, target))
            {
                _spitWarned = true;
                events.Add(new BatSpitWindUp(Id) { Place = Centre });
            }

            return;
        }

        if (--_spitTicksLeft > 0)
        {
            return;
        }

        var aim = target.EyePosition - new Vector3(0f, _bat.SpitAimBelowEyes, 0f);
        events.Add(new BatSpat(Id, Centre, Vector3.Normalize(aim - Centre)) { Place = Centre });
        _spitWarned = false;
        _spitTicksLeft = _spitTicks;
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
        _stateTicksLeft = _screechTicks;
        _diveGoal = target.Position + new Vector3(0f, target.Body.Height / 2f, 0f);
        events.Add(new BatScreeched(Id) { Place = Centre });
    }

    /// <summary>
    /// One tick of the dive: straight at the fixed spot. Touching a living player is a bite; reaching the spot (or a
    /// wall or the floor) without one is a miss. Either way it rises again afterwards.
    /// </summary>
    private void Dive(Level level, IReadOnlyList<Player> players, ICollection<GameEvent> events)
    {
        var toGoal = _diveGoal - Centre;
        var distance = toGoal.Length();
        var step = MathF.Min(_bat.DiveSpeed * SimulationClock.SecondsPerTick, distance);
        var next = distance > 0.0001f ? Position + toGoal / distance * step : Position;

        var floor = level.CellAt(next).FloorHeight;
        var blocked = next.Y < floor
            || CharacterCollision.Overlaps(level, next, BodySizes.Bat.Radius, BodySizes.Bat.Height, BodySizes.Bat.StepHeight);
        if (!blocked)
        {
            Position = next;
        }

        var victim = players.FirstOrDefault(player => !player.IsDead && Touches(player.Body));
        if (victim is not null)
        {
            victim.TakeDamage(_bat.BiteDamage, events);
            events.Add(new BatBit(Id, victim.Id));
            State = BatState.Rising;
        }
        else if (blocked || step >= distance)
        {
            events.Add(new BatDiveMissed(Id) { Place = Centre });
            State = BatState.Rising;
        }
    }

    /// <summary>
    /// One tick of circling: flies round the target at its own <see cref="CircleRadius"/>, rising and sinking a
    /// little. If a wall is between them, it first finds its way round. It turns round now and then - and whenever it
    /// is pressed against a wall.
    /// </summary>
    private void Circle(Level level, Player target)
    {
        // Now and then it turns round, at random - harder to predict, harder to hit.
        if (--_turnTicksLeft <= 0)
        {
            _circleDirection = -_circleDirection;
            _turnTicksLeft = NextTurnTicks();
        }

        Vector3 velocity;
        if (Pathfinder.CanWalkStraight(level, Position, target.Position, BodySizes.Bat.Radius, BodySizes.Bat.StepHeight))
        {
            _waypoints.Clear();
            var offset = (Position - target.Position) with { Y = 0f };
            var distance = offset.Length();
            var outward = distance > 0.01f ? offset / distance : Vector3.UnitX;
            var along = new Vector3(-outward.Z, 0f, outward.X) * _circleDirection;

            // Along the circle, plus a pull back onto it when too close or too far.
            velocity = along * _bat.CircleSpeed + outward * ((CircleRadius - distance) * 2f);
        }
        else
        {
            velocity = AlongPath(level, target);
        }

        if (velocity.Length() > _bat.FlySpeed)
        {
            velocity = Vector3.Normalize(velocity) * _bat.FlySpeed;
        }

        var (position, slid) = CharacterCollision.Move(
            level, Position, velocity, BodySizes.Bat.Radius, SimulationClock.SecondsPerTick, BodySizes.Bat.Height, BodySizes.Bat.StepHeight);

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
        var maxClimb = _bat.ClimbSpeed * SimulationClock.SecondsPerTick;
        Position = position;
        ClimbClearOfStone(level, Position.Y + Math.Clamp(height - Position.Y, -maxClimb, maxClimb));
    }

    /// <summary>A random time until it turns round next, between <see cref="BatBalance.TurnRoundMin"/> and <see cref="BatBalance.TurnRoundMax"/>.</summary>
    private int NextTurnTicks() => _random.Next(_turnRoundMinTicks, _turnRoundMaxTicks + 1);

    /// <summary>The velocity towards the next point of the way round the walls (searched again now and then).</summary>
    private Vector3 AlongPath(Level level, Player target)
    {
        var goal = Level.ToGrid(target.Position.X, target.Position.Z);
        if (--_repathTicksLeft <= 0 || goal != _pathGoal || _waypoints.Count == 0)
        {
            var start = Level.ToGrid(Position.X, Position.Z);
            var path = Pathfinder.FindPath(level, start, goal, BodySizes.Bat.StepHeight);
            _waypoints = path is null ? [] : Pathfinder.Smooth(level, path, BodySizes.Bat.Radius, BodySizes.Bat.StepHeight).ToList();
            _repathTicksLeft = _repathTicks;
            _pathGoal = goal;
        }

        while (_waypoints.Count > 0 && FlatDistance(Position, _waypoints[0]) < 0.25f)
        {
            _waypoints.RemoveAt(0);
        }

        ColumnDetour.SkipUnreachable(_waypoints, level, BodySizes.Bat.Radius);
        if (_waypoints.Count == 0)
        {
            return Vector3.Zero;
        }

        var toNext = (ColumnDetour.Around(level, Position, _waypoints[0], BodySizes.Bat.Radius) - Position) with { Y = 0f };
        return Vector3.Normalize(toNext) * _bat.FlySpeed;
    }

    /// <summary>
    /// The height it wants to be at while circling: a slow wave between <see cref="BatBalance.CircleHeightMin"/> and
    /// <see cref="BatBalance.CircleHeightMax"/> above the floor below it - but never too close to the ceiling.
    /// </summary>
    private float CircleHeight(Level level)
    {
        var cell = level.CellAt(Position);
        var middle = (_bat.CircleHeightMin + _bat.CircleHeightMax) / 2f;
        var swing = (_bat.CircleHeightMax - _bat.CircleHeightMin) / 2f;
        var wave = MathF.Sin(2f * MathF.PI * _bobTick / _bobTicks);
        var highest = cell.CeilingHeight - BodySizes.Bat.CeilingGap - BodySizes.Bat.Height;
        return MathF.Min(cell.FloorHeight + middle + swing * wave, highest);
    }

    /// <summary>A player it can see: closer than its sight range and nothing in between (from its body to their eyes).</summary>
    private bool CanSee(Level level, Player player) =>
        Vector3.Distance(Centre, player.EyePosition) <= _bat.SightRange
        && LineOfSight.IsClear(level, Centre, player.EyePosition);

    private Player? Nearest(IReadOnlyList<Player> players) =>
        players.Where(player => !player.IsDead).OrderBy(player => FlatDistance(Position, player.Position)).FirstOrDefault();

    /// <summary>Does its body touch <paramref name="body"/>? (Two upright cylinders.)</summary>
    private bool Touches(CylinderBody body) =>
        FlatDistance(Position, body.Position) <= BodySizes.Bat.Radius + body.Radius
        && Position.Y < body.Position.Y + body.Height
        && body.Position.Y < Position.Y + BodySizes.Bat.Height;

    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    /// <summary>One tick of the body: it sinks further out of the ceiling; dead it falls to the floor and disappears after a while.</summary>
    public void Tick(Level level, ICollection<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(events);
        Emergence.Tick();
        if (!IsDead)
        {
            return;
        }

        // Dead: gravity takes over until it lies on the floor.
        (Position, _velocity, _) = CharacterCollision.MoveVertically(
            level, Position, _velocity, BodySizes.Bat.Radius, BodySizes.Bat.Height, _gravity, SimulationClock.SecondsPerTick);

        if (_corpseTicksLeft > 0)
        {
            _corpseTicksLeft--;
        }
    }

    /// <summary>
    /// Where a bat starting in a cell hovers: <see cref="BodySizes.Bat.HoverHeight"/> above the floor, but always
    /// with some room below the ceiling.
    /// </summary>
    public static Vector3 HoverPosition(Level level, Vector3 floorPosition)
    {
        ArgumentNullException.ThrowIfNull(level);
        var ceiling = level.CellAt(floorPosition).CeilingHeight;
        var highest = ceiling - BodySizes.Bat.CeilingGap - BodySizes.Bat.Height;
        return floorPosition with { Y = MathF.Min(floorPosition.Y + BodySizes.Bat.HoverHeight, highest) };
    }
}
