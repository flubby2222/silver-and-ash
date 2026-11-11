using System.Numerics;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// The vampire bat: a small flying enemy. It hovers in the air (no gravity while alive), can be hit like every target
/// and dies at 0 health - then it tumbles to the floor, lies there for a moment and is gone.
/// (Circling, spitting and diving come with its behaviour.)
/// </summary>
public sealed class Bat : ITarget
{
    private static readonly int CorpseTicks = SimulationClock.SecondsToTicks(Tuning.Bat.CorpseTime);

    private int _corpseTicksLeft;
    private Vector3 _velocity;

    /// <summary>Creates a bat whose body starts at <paramref name="position"/> (the bottom of its body, in the air).</summary>
    public Bat(int id, Vector3 position)
    {
        Id = id;
        Position = position;
    }

    public int Id { get; }

    /// <summary>Bottom centre of its body in world coordinates (it flies, so this is in the air).</summary>
    public Vector3 Position { get; private set; }

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
        if (IsDead)
        {
            _corpseTicksLeft = CorpseTicks;
            events.Add(new BatDied(Id));
        }
    }

    /// <summary>One tick: alive it stays in the air; dead it falls to the floor and disappears after a while.</summary>
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
