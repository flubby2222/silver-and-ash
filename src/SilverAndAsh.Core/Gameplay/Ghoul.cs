using System.Numerics;
using SilverAndAsh.Core.Physics;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// The ghoul: a crawling enemy, lower than the player, with its head at the front. It has health and dies at 0 -
/// its body lies on the ground for a moment, then it is gone. (Moving and attacking come with its behaviour.)
/// </summary>
public sealed class Ghoul : ITarget
{
    private static readonly int CorpseTicks = SimulationClock.SecondsToTicks(Tuning.Ghoul.CorpseTime);

    private int _corpseTicksLeft;

    /// <summary>Creates a ghoul at <paramref name="position"/> (centre of its feet), looking along <paramref name="facing"/>.</summary>
    public Ghoul(int id, Vector3 position, Vector3? facing = null)
    {
        Id = id;
        Position = position;
        Facing = facing ?? Vector3.UnitZ;
    }

    public int Id { get; }

    /// <summary>Centre of its feet in world coordinates.</summary>
    public Vector3 Position { get; }

    /// <summary>The flat direction it looks in (length 1). Its head is at the front.</summary>
    public Vector3 Facing { get; }

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
        if (IsDead)
        {
            _corpseTicksLeft = CorpseTicks;
            events.Add(new GhoulDied(Id));
        }
    }

    /// <summary>Advances by one tick: a dead body disappears after a while.</summary>
    public void Tick(ICollection<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (_corpseTicksLeft > 0)
        {
            _corpseTicksLeft--;
        }
    }
}
