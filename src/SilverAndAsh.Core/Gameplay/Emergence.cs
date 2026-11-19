using System.Numerics;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// How far an enemy of a combat room has come out (issue #424). Such enemies wait hidden - inside the floor or the
/// ceiling - until their room seals; then they rise out of the floor (ghouls) or sink out of the ceiling (bats). While
/// hidden or coming out they neither act nor can be hit. Every other enemy is there from the start.
/// </summary>
public sealed class Emergence
{
    private Vector3 _hiddenOffset;
    private int _ticks;
    private int _ticksLeft;

    /// <summary>True while it waits inside the floor or the ceiling (before its room seals).</summary>
    public bool IsHidden { get; private set; }

    /// <summary>True while it comes out.</summary>
    public bool IsEmerging => !IsHidden && _ticksLeft > 0;

    /// <summary>True once it is fully out: it acts and can be hit.</summary>
    public bool IsPresent => !IsHidden && _ticksLeft == 0;

    /// <summary>How far it has come out: 0 hidden, 1 fully out. For drawing.</summary>
    public float Fraction => IsHidden ? 0f : _ticks == 0 ? 1f : 1f - (float)_ticksLeft / _ticks;

    /// <summary>
    /// Where it is drawn, seen from its real position: <c>hidden offset * (1 - Fraction)</c> - all the way in the floor
    /// or the ceiling while hidden, nothing once it is out.
    /// </summary>
    public Vector3 Offset => _hiddenOffset * (1f - Fraction);

    /// <summary>Hides it; <paramref name="hiddenOffset"/> is where it waits, seen from its real position.</summary>
    internal void Hide(Vector3 hiddenOffset)
    {
        _hiddenOffset = hiddenOffset;
        IsHidden = true;
    }

    /// <summary>Lets it come out over <paramref name="ticks"/> ticks (at once with 0).</summary>
    internal void Start(int ticks)
    {
        IsHidden = false;
        _ticks = Math.Max(0, ticks);
        _ticksLeft = _ticks;
    }

    /// <summary>One tick has passed.</summary>
    internal void Tick()
    {
        if (!IsHidden && _ticksLeft > 0)
        {
            _ticksLeft--;
        }
    }
}
