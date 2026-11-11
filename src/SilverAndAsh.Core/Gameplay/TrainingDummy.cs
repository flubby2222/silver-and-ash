using System.Numerics;
using SilverAndAsh.Core.Physics;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// A training dummy: something to hit before there are enemies (those come in v0.5 and reuse all of this).
/// It has health, falls over at 0 and stands up again with full health after a short time.
/// </summary>
public sealed class TrainingDummy
{
    private static readonly int DownTicks = SimulationClock.SecondsToTicks(Tuning.Combat.DummyDownTime);

    private int _downTicksLeft;

    /// <summary>Creates a dummy standing at <paramref name="position"/> (centre of its feet).</summary>
    public TrainingDummy(int id, Vector3 position)
    {
        Id = id;
        Position = position;
    }

    /// <summary>
    /// Number of this dummy within its world. Events name the dummy by this number instead of the object,
    /// so they could be sent to another computer later (online co-op).
    /// </summary>
    public int Id { get; }

    /// <summary>Centre of the feet in world coordinates.</summary>
    public Vector3 Position { get; }

    /// <summary>Current health; 0 while lying on the ground.</summary>
    public int Health { get; private set; } = Tuning.Combat.DummyMaxHealth;

    /// <summary>True while the dummy lies on the ground. It cannot be hit then and does not block the way.</summary>
    public bool IsDown => _downTicksLeft > 0;

    /// <summary>The body for collisions and hits.</summary>
    public CylinderBody Body => new(Position, Tuning.Combat.DummyRadius, Tuning.Combat.DummyHeight);

    /// <summary>
    /// Takes <paramref name="damage"/>. Reports <see cref="DummyHit"/> and, at 0 health, <see cref="DummyKnockedDown"/>
    /// into <paramref name="events"/>. A dummy that is already down ignores hits.
    /// </summary>
    public void TakeDamage(int damage, ICollection<GameEvent> events)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(damage);
        ArgumentNullException.ThrowIfNull(events);
        if (IsDown)
        {
            return;
        }

        Health = Math.Max(0, Health - damage);
        events.Add(new DummyHit(Id, damage));

        if (Health == 0)
        {
            _downTicksLeft = DownTicks;
            events.Add(new DummyKnockedDown(Id));
        }
    }

    /// <summary>Advances by one tick: a dummy that lies down long enough stands up with full health.</summary>
    public void Tick(ICollection<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (!IsDown)
        {
            return;
        }

        _downTicksLeft--;
        if (!IsDown)
        {
            Health = Tuning.Combat.DummyMaxHealth;
            events.Add(new DummyStoodUp(Id));
        }
    }
}
