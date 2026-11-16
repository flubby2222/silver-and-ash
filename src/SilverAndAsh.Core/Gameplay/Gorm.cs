using System.Numerics;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>Gorm's three attacks (decision record 0007) - each has its one answer.</summary>
public enum GormAttack
{
    /// <summary>Hammer blow: a shock wave along the floor - jump.</summary>
    Hammer,

    /// <summary>Chain swing: the chain round at chest height - duck in a dodge.</summary>
    Swing,

    /// <summary>Chain throw: a hook straight at the player, pulls them close - step aside or behind a pillar.</summary>
    Throw,
}

/// <summary>What Gorm is doing right now.</summary>
public enum GormState
{
    /// <summary>Walking after the player (or standing close in front of them), choosing his next attack.</summary>
    Walking,

    /// <summary>Warning before an attack (<see cref="Gorm.Attack"/>): club raised, chain spun up or hook glowing.</summary>
    WindingUp,

    /// <summary>The hammer struck: its shock wave runs along the floor.</summary>
    Slamming,

    /// <summary>The hook flies.</summary>
    Throwing,

    /// <summary>A hammer blow hit nobody: the club is stuck, he bends forward and takes double damage.</summary>
    Stuck,

    /// <summary>Resting after an attack - the moment to strike back.</summary>
    Recovering,

    /// <summary>At half health he roars, head thrown back (no attack meanwhile) - then phase 2 begins.</summary>
    Roaring,
}

/// <summary>A stone falling from the ceiling: its shadow lies at <paramref name="At"/> on the floor; <paramref name="Progress"/> runs from 0 (just loose) to 1 (it lands).</summary>
public readonly record struct FallingStone(Vector3 At, float Progress);

/// <summary>
/// Gorm, the Chained: the troll, boss of level 0 (decision record 0007). 3.2 m tall, slow, much health. He sleeps in
/// the boss room until a player enters it, then walks after the nearest living player and attacks: close up hammer or
/// chain swing (50/50), farther away the chain throw - never the same attack twice in a row, and after every attack
/// he rests a moment. Every attack is announced; each has its one answer (jump, duck in a dodge, step aside). A hammer
/// blow that hits nobody gets his club stuck: he bends forward (head lower) and takes double damage. His head is a
/// weak spot (<see cref="HitZone.BossHead"/>). At half health he roars once and phase 2 begins: walking, warnings
/// and pauses get faster, and every hammer blow shakes stones from the ceiling - each announced by its shadow on the
/// floor. When he dies, his body stays where it fell.
/// </summary>
public sealed class Gorm : ITarget
{
    /// <summary>The name shown above his health bar.</summary>
    public const string Name = "GORM, THE CHAINED";

    /// <summary>How far his head comes down while the club is stuck (metres).</summary>
    public const float StuckBend = 0.8f;

    /// <summary>The hook is thrown from this high above his feet (metres) ...</summary>
    public const float HookHeight = 1.6f;

    /// <summary>... and this radius (metres) touches a player.</summary>
    public const float HookRadius = 0.3f;

    private readonly GormBalance _gorm;
    private readonly float _gravity;
    private readonly int _repathTicks;
    private readonly Random _random;
    private List<Vector3> _waypoints = [];
    private int _repathTicksLeft;
    private (int X, int Z) _pathGoal;

    private int _stateTicks;
    private int _stateTicksLeft;
    private GormAttack? _lastAttack;
    private readonly HashSet<int> _reachedByWave = [];
    private bool _waveHit;
    private Vector3 _hookDirection;
    private float _hookTravelled;

    private readonly IReadOnlySet<(int X, int Z)>? _arena;
    private readonly int _stoneTicks;
    private readonly List<(Vector3 At, int TicksLeft)> _stones = [];

    /// <summary>
    /// Creates Gorm at <paramref name="position"/> (centre of his feet), looking along <paramref name="facing"/>.
    /// Stones fall only on the cells of <paramref name="arena"/> (his boss room) - without one, on any open cell.
    /// </summary>
    public Gorm(
        int id, Vector3 position, Vector3? facing = null, Balance? balance = null, Random? random = null,
        IReadOnlySet<(int X, int Z)>? arena = null)
    {
        Id = id;
        Position = position;
        Facing = facing ?? Vector3.UnitZ;

        balance ??= Balance.Default;
        _gorm = balance.Gorm;
        _gravity = balance.Player.Gravity;
        _repathTicks = SimulationClock.SecondsToTicks(_gorm.RepathTime);
        _stoneTicks = Math.Max(1, SimulationClock.SecondsToTicks(_gorm.StoneWarning));
        _random = random ?? new Random(id);
        _arena = arena;
        Health = _gorm.MaxHealth;
    }

    /// <summary>True once he has fallen to half health and roared: faster, and his hammer brings down stones.</summary>
    public bool IsInPhase2 { get; private set; }

    /// <summary>How far round its spot a stone hits (metres) - for the size of its shadow.</summary>
    public float StoneReach => _gorm.StoneRadius;

    /// <summary>The stones falling right now - their shadows on the floor.</summary>
    public IEnumerable<FallingStone> Stones =>
        _stones.Select(stone => new FallingStone(stone.At, 1f - ((float)stone.TicksLeft / _stoneTicks)));

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

    /// <summary>What he is doing right now.</summary>
    public GormState State { get; private set; } = GormState.Walking;

    /// <summary>The attack he is winding up or carrying out (null while walking).</summary>
    public GormAttack? Attack { get; private set; }

    /// <summary>How far the current state has come: 0 at its start, 1 at its end. For drawing (wind-up, stuck club).</summary>
    public float StateProgress => _stateTicks == 0 ? 0f : 1f - ((float)_stateTicksLeft / _stateTicks);

    /// <summary>Where the shock wave started and how far it has run (metres) - null while there is none.</summary>
    public (Vector3 Origin, float Radius)? Shockwave { get; private set; }

    /// <summary>How far his shock wave reaches (metres) - for the warning circle before the hammer.</summary>
    public float ShockwaveReach => _gorm.ShockwaveRadius;

    /// <summary>How far his chain swing reaches (metres) and at which heights above his feet it passes - for drawing.</summary>
    public (float Reach, float Low, float High) SwingArea => (_gorm.SwingReach, _gorm.SwingLow, _gorm.SwingHigh);

    /// <summary>Where the hook is while it flies - null otherwise. The chain runs from his hand to it.</summary>
    public Vector3? Hook { get; private set; }

    /// <summary>Where the hook leaves his hand.</summary>
    public Vector3 Hand => Position + new Vector3(0f, HookHeight, 0f) + (Facing * BodySizes.Gorm.Radius);

    public CylinderBody Body => new(Position, BodySizes.Gorm.Radius, BodySizes.Gorm.Height);

    /// <summary>
    /// His head: the top of his body, at the front, as wide as it is drawn - lower while the club is stuck (he bends
    /// forward). The rest is body.
    /// </summary>
    public HitZone ZoneAt(Vector3 point)
    {
        var top = Position.Y + BodySizes.Gorm.Height - (State == GormState.Stuck ? StuckBend : 0f);
        var onHeadHeight = point.Y >= top - BodySizes.Gorm.HeadHeight && point.Y <= top;
        var flatOffset = (point - Position) with { Y = 0f };
        var forward = Vector3.Dot(flatOffset, Facing);
        var side = MathF.Abs(Vector3.Dot(flatOffset, new Vector3(Facing.Z, 0f, -Facing.X)));
        return onHeadHeight && forward > 0f && side <= BodySizes.Gorm.HeadWidth / 2f ? HitZone.BossHead : HitZone.Body;
    }

    public void TakeDamage(int damage, ICollection<GameEvent> events, bool critical = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(damage);
        ArgumentNullException.ThrowIfNull(events);
        if (IsDead)
        {
            return;
        }

        // The club is stuck: every hit counts double (or what the balancing says).
        if (State == GormState.Stuck)
        {
            damage = (int)MathF.Round(damage * _gorm.StuckDamageMultiplier, MidpointRounding.AwayFromZero);
        }

        // Reported is what he really lost - a deadly hit counts only his last health.
        var taken = Math.Min(damage, Health);
        Health -= taken;
        events.Add(new GormHit(Id, taken, critical));
        if (IsDead)
        {
            Velocity = Vector3.Zero;
            Shockwave = null;
            Hook = null;
            _stones.Clear();
            events.Add(new GormDied(Id));
            return;
        }

        // Half his health gone: he breaks off whatever he is doing and roars - once.
        if (!IsInPhase2 && Health <= MaxHealth * _gorm.Phase2Health)
        {
            IsInPhase2 = true;
            Shockwave = null;
            Hook = null;
            Attack = null;
            Begin(GormState.Roaring, _gorm.RoarTime);
            events.Add(new GormRoared(Id));
        }
    }

    /// <summary>
    /// One tick of behaviour: walk, choose and carry out an attack, rest. <paramref name="obstacles"/> are the bodies
    /// he cannot walk through.
    /// </summary>
    public void Act(Level level, IReadOnlyList<Player> players, IReadOnlyList<CylinderBody> obstacles, ICollection<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(obstacles);
        ArgumentNullException.ThrowIfNull(events);
        if (IsDead)
        {
            return;
        }

        // Online co-op rule 1: the nearest of THE living players.
        var target = players.Where(player => !player.IsDead).MinBy(player => FlatDistance(Position, player.Position));
        DropStones(players, events);
        switch (State)
        {
            case GormState.Walking:
                Walk(level, target, obstacles, events);
                break;

            case GormState.WindingUp:
                if (target is not null)
                {
                    FaceTowards(target.Position);
                }

                Stand(level, obstacles);
                if (--_stateTicksLeft == 0)
                {
                    Release(level, target, players, events);
                }

                break;

            case GormState.Slamming:
                Stand(level, obstacles);
                RunShockwave(level, players, events);
                break;

            case GormState.Throwing:
                Stand(level, obstacles);
                FlyHook(level, players, events);
                break;

            case GormState.Stuck:
                Stand(level, obstacles);
                if (--_stateTicksLeft == 0)
                {
                    events.Add(new ClubFreed(Id));
                    Rest();
                }

                break;

            case GormState.Recovering:
            case GormState.Roaring:
                Stand(level, obstacles);
                if (--_stateTicksLeft == 0)
                {
                    State = GormState.Walking;
                    Attack = null;
                }

                break;
        }
    }

    // In phase 2 walking, warnings and pauses take less time.
    private float Quicker(float seconds) => IsInPhase2 ? seconds / _gorm.Phase2SpeedUp : seconds;

    // Each stone counts down its shadow, then lands: whoever stands within its radius is hit.
    private void DropStones(IReadOnlyList<Player> players, ICollection<GameEvent> events)
    {
        for (var index = _stones.Count - 1; index >= 0; index--)
        {
            var (at, ticksLeft) = _stones[index];
            if (--ticksLeft > 0)
            {
                _stones[index] = (at, ticksLeft);
                continue;
            }

            _stones.RemoveAt(index);
            events.Add(new StoneLanded(at));
            foreach (var player in players.Where(player => !player.IsDead && FlatDistance(at, player.Position) <= _gorm.StoneRadius))
            {
                player.TakeDamage(_gorm.StoneDamage, events);
                events.Add(new StoneHit(player.Id));
            }
        }
    }

    // Phase 2: the hammer blow shakes stones loose, each at a random spot of open floor in his room near the player.
    private void LoosenStones(Level level, Player target, ICollection<GameEvent> events)
    {
        const int Tries = 30;
        for (var stone = 0; stone < _gorm.StoneCount; stone++)
        {
            for (var attempt = 0; attempt < Tries; attempt++)
            {
                var angle = _random.NextSingle() * 2f * MathF.PI;
                var distance = _gorm.StoneSpread * MathF.Sqrt(_random.NextSingle());
                var x = target.Position.X + (MathF.Cos(angle) * distance);
                var z = target.Position.Z + (MathF.Sin(angle) * distance);
                // Outside the level counts as wall (see Level's indexer).
                var cell = Level.ToGrid(x, z);
                if (level[cell.X, cell.Z].IsSolid || _arena?.Contains(cell) == false)
                {
                    continue;
                }

                var at = new Vector3(x, level[cell.X, cell.Z].FloorHeight, z);
                _stones.Add((at, _stoneTicks));
                events.Add(new StoneFalling(at));
                break;
            }
        }
    }

    // Walking: close enough for an attack? Otherwise on towards the target (stopping close in front of them).
    private void Walk(Level level, Player? target, IReadOnlyList<CylinderBody> obstacles, ICollection<GameEvent> events)
    {
        if (target is null)
        {
            Stand(level, obstacles);
            return;
        }

        var distance = FlatDistance(Position, target.Position);
        var attack = ChooseAttack(level, target, distance);
        if (attack is { } chosen)
        {
            FaceTowards(target.Position);
            Begin(GormState.WindingUp, chosen switch
            {
                GormAttack.Hammer => Quicker(_gorm.HammerWindUp),
                GormAttack.Swing => Quicker(_gorm.SwingWindUp),
                _ => Quicker(_gorm.ThrowWindUp),
            });
            Attack = chosen;
            _lastAttack = chosen;
            events.Add(new GormWindUp(Id, chosen));
            Stand(level, obstacles);
            return;
        }

        if (distance <= _gorm.StopDistance)
        {
            FaceTowards(target.Position);
            Stand(level, obstacles);
            return;
        }

        var next = NextPoint(level, target);
        if (next is not { } point || FlatDistance(Position, point) < 0.001f)
        {
            Stand(level, obstacles);
            return;
        }

        FaceTowards(point);
        Move(level, Facing * (IsInPhase2 ? _gorm.Speed * _gorm.Phase2SpeedUp : _gorm.Speed), obstacles);
    }

    // Close up hammer or chain swing (50/50), farther away the throw - never the same twice in a row.
    private GormAttack? ChooseAttack(Level level, Player target, float distance)
    {
        if (distance <= _gorm.NearRange)
        {
            var options = new[] { GormAttack.Hammer, GormAttack.Swing }.Where(attack => attack != _lastAttack).ToList();
            return options[_random.Next(options.Count)];
        }

        var clear = LineOfSight.IsClear(level, Hand, target.Position + new Vector3(0f, BodySizes.Player.Height / 2f, 0f));
        return distance <= _gorm.FarRange && _lastAttack != GormAttack.Throw && clear ? GormAttack.Throw : null;
    }

    // The end of the wind-up: the blow, the swing or the throw.
    private void Release(Level level, Player? target, IReadOnlyList<Player> players, ICollection<GameEvent> events)
    {
        switch (Attack)
        {
            case GormAttack.Hammer:
                Shockwave = (Position, 0f);
                _reachedByWave.Clear();
                _waveHit = false;
                State = GormState.Slamming;
                events.Add(new GormSlammed(Id));
                if (IsInPhase2 && target is not null)
                {
                    LoosenStones(level, target, events);
                }

                break;

            case GormAttack.Swing:
                var hit = false;
                foreach (var player in players.Where(player => !player.IsDead && FlatDistance(Position, player.Position) <= _gorm.SwingReach))
                {
                    var low = Position.Y + _gorm.SwingLow;
                    var high = Position.Y + _gorm.SwingHigh;
                    if (player.Position.Y < high && player.Position.Y + player.CollisionHeight > low)
                    {
                        player.TakeDamage(_gorm.SwingDamage, events);
                        hit = true;
                    }
                }

                events.Add(new GormSwung(Id, hit));
                Rest();
                break;

            default:
                // Straight ahead at hand height: he has turned to where the player stands now while winding up.
                _hookDirection = Facing;
                _hookTravelled = 0f;
                Hook = Hand;
                State = GormState.Throwing;
                events.Add(new GormThrew(Id, Hand, _hookDirection));
                break;
        }
    }

    // The shock wave runs outwards; every living player it reaches who is not high enough in the air is hit once.
    private void RunShockwave(Level level, IReadOnlyList<Player> players, ICollection<GameEvent> events)
    {
        var (origin, radius) = Shockwave!.Value;
        radius = MathF.Min(_gorm.ShockwaveRadius, radius + (_gorm.ShockwaveSpeed * SimulationClock.SecondsPerTick));
        Shockwave = (origin, radius);

        foreach (var player in players.Where(player => !player.IsDead && !_reachedByWave.Contains(player.Id)))
        {
            if (FlatDistance(origin, player.Position) > radius)
            {
                continue;
            }

            _reachedByWave.Add(player.Id);
            var aboveFloor = player.Position.Y - level.CellAt(player.Position).FloorHeight;
            if (aboveFloor < _gorm.ShockwaveHeight)
            {
                player.TakeDamage(_gorm.HammerDamage, events);
                events.Add(new ShockwaveHit(player.Id));
                _waveHit = true;
            }
        }

        if (radius < _gorm.ShockwaveRadius)
        {
            return;
        }

        Shockwave = null;
        if (_waveHit)
        {
            Rest();
            return;
        }

        // Nobody hit: the club is stuck.
        Begin(GormState.Stuck, _gorm.StuckTime);
        events.Add(new ClubStuck(Id));
    }

    // The hook flies straight on until it touches a player (damage, pull), a wall or pillar, or has flown its range.
    private void FlyHook(Level level, IReadOnlyList<Player> players, ICollection<GameEvent> events)
    {
        var from = Hook!.Value;
        var step = _gorm.HookSpeed * SimulationClock.SecondsPerTick;
        var to = from + (_hookDirection * step);
        _hookTravelled += step;

        var victim = players.FirstOrDefault(player => !player.IsDead && Touches(player, to));
        if (victim is not null)
        {
            victim.TakeDamage(_gorm.ThrowDamage, events);
            var towards = (victim.Position - Position) with { Y = 0f };
            var away = towards.LengthSquared() > 0.0001f ? Vector3.Normalize(towards) : Facing;
            victim.PullTo(Position + (away * _gorm.PullDistance), _gorm.PullTime);
            events.Add(new HookHit(victim.Id));
            Hook = null;
            Rest();
            return;
        }

        if (!LineOfSight.IsClear(level, from, to) || _hookTravelled >= _gorm.FarRange + 3f)
        {
            events.Add(new HookStopped(to));
            Hook = null;
            Rest();
            return;
        }

        Hook = to;
    }

    private static bool Touches(Player player, Vector3 point) =>
        FlatDistance(player.Position, point) <= BodySizes.Player.Radius + HookRadius
        && point.Y >= player.Position.Y - HookRadius
        && point.Y <= player.Position.Y + player.CollisionHeight + HookRadius;

    private void Rest()
    {
        Begin(GormState.Recovering, Quicker(_gorm.PauseTime));
    }

    private void Begin(GormState state, float seconds)
    {
        State = state;
        _stateTicks = Math.Max(1, SimulationClock.SecondsToTicks(seconds));
        _stateTicksLeft = _stateTicks;
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

    private void Stand(Level level, IReadOnlyList<CylinderBody> obstacles) => Move(level, Vector3.Zero, obstacles);

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
