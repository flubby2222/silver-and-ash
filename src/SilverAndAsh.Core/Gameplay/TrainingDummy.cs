using System.Numerics;
using SilverAndAsh.Core.Physics;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// A training dummy: something to hit before there are enemies (those come in v0.5 and reuse all of this).
/// It has health, falls over at 0 and stands up again with full health after a short time.
/// </summary>
public sealed class TrainingDummy : ITarget
{
    private readonly DummyBalance _balance;
    private readonly int _downTicks;

    private int _downTicksLeft;

    /// <summary>
    /// Creates a dummy standing at <paramref name="position"/> (centre of its feet), with the health and down time
    /// from <paramref name="balance"/> (the standard values if none is given).
    /// </summary>
    public TrainingDummy(int id, Vector3 position, Balance? balance = null)
    {
        Id = id;
        Position = position;
        _balance = (balance ?? Balance.Default).Dummy;
        _downTicks = SimulationClock.SecondsToTicks(_balance.DownTime);
        Health = _balance.MaxHealth;
    }

    /// <summary>
    /// Number of this dummy within its world. Events name the dummy by this number instead of the object,
    /// so they could be sent to another computer later (online co-op).
    /// </summary>
    public int Id { get; }

    /// <summary>Centre of the feet in world coordinates.</summary>
    public Vector3 Position { get; }

    /// <summary>Current health; 0 while lying on the ground.</summary>
    public int Health { get; private set; }

    /// <summary>True while the dummy lies on the ground. It cannot be hit then and does not block the way.</summary>
    public bool IsDown => _downTicksLeft > 0;

    public bool IsHittable => !IsDown;

    /// <summary>The body for collisions and hits.</summary>
    public CylinderBody Body => new(Position, BodySizes.Dummy.Radius, BodySizes.Dummy.Height);

    /// <summary>
    /// Takes <paramref name="damage"/>. Reports <see cref="DummyHit"/> and, at 0 health, <see cref="DummyKnockedDown"/>
    /// into <paramref name="events"/>. A dummy that is already down ignores hits.
    /// </summary>
    public void TakeDamage(int damage, ICollection<GameEvent> events, bool critical = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(damage);
        ArgumentNullException.ThrowIfNull(events);
        if (IsDown)
        {
            return;
        }

        Health = Math.Max(0, Health - damage);
        events.Add(new DummyHit(Id, damage, critical));

        if (Health == 0)
        {
            _downTicksLeft = _downTicks;
            events.Add(new DummyKnockedDown(Id));
        }
    }

    /// <summary>Which zone a hit at <paramref name="point"/> (on the body's surface) belongs to.</summary>
    public HitZone ZoneAt(Vector3 point) =>
        point.Y >= Position.Y + BodySizes.Dummy.Height - BodySizes.Dummy.HeadHeight ? HitZone.Head : HitZone.Body;

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
            Health = _balance.MaxHealth;
            events.Add(new DummyStoodUp(Id));
        }
    }
}
