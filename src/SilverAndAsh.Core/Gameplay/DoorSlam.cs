using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// The boss door's own clock for swinging shut (issue #566): <see cref="DoorsBalance.BossCloseTicks"/> ticks with the
/// hard stop at <see cref="DoorsBalance.BossShutTick"/> - from that tick on the door blocks. Counted in the door's own
/// ticks, never the world's clock. Opening again is the door's ordinary even swing (<see cref="DoorsBalance.BossOpenTime"/>).
/// </summary>
internal sealed class DoorSlam
{
    private readonly int _closeTicks;
    private readonly int _shutTick;

    // Ticks since the door began to close; -1 while it does not close.
    private int _ticks = -1;

    // How far the leaves stood open when the closing began.
    private float _from;

    private DoorSlam(DoorsBalance balance)
    {
        // A hard stop after the end would never come: the slam then lasts until it.
        _closeTicks = Math.Max(balance.BossCloseTicks, balance.BossShutTick);
        _shutTick = balance.BossShutTick;
    }

    /// <summary>The slam of a door of <paramref name="kind"/>: only a boss door has one.</summary>
    public static DoorSlam? For(DoorKind kind, DoorsBalance balance) => kind == DoorKind.Boss ? new DoorSlam(balance) : null;

    /// <summary>True from the tick the door began to close until it opens again.</summary>
    public bool IsClosing => _ticks >= 0;

    /// <summary>True from the hard stop on: the leaves have met, nothing passes.</summary>
    public bool IsShut => IsClosing && _ticks >= _shutTick;

    /// <summary>
    /// How far the leaves are still open while closing: evenly from where they stood down to 0 at the hard stop (the
    /// run-up and the bounce are for the picture, issue #568).
    /// </summary>
    public float OpenFraction => _shutTick <= 0 ? 0f : _from * MathF.Max(0f, 1f - ((float)_ticks / _shutTick));

    /// <summary>Begins to close with the leaves <paramref name="from"/> open. False if it is closing already.</summary>
    public bool Close(float from)
    {
        if (IsClosing)
        {
            return false;
        }

        _from = from;
        _ticks = 0;
        return true;
    }

    /// <summary>The closing is over: the door opens again.</summary>
    public void Stop() => _ticks = -1;

    /// <summary>One tick of closing; nothing once the slam is over or while the door does not close.</summary>
    public void Tick()
    {
        if (IsClosing && _ticks < _closeTicks)
        {
            _ticks++;
        }
    }
}
