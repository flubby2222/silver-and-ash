namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// The magazine of a ranged weapon (decision record 0008): so many rounds, then it has to be reloaded. The supply is
/// unlimited - reloading only takes time. Counts in simulation ticks, like <see cref="Cooldown"/>.
/// </summary>
public sealed class Magazine
{
    private readonly int _reloadTicks;
    private int _reloadTicksLeft;

    /// <summary>A full magazine of <paramref name="size"/> rounds that takes <paramref name="reloadSeconds"/> to reload.</summary>
    public Magazine(int size, float reloadSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        ArgumentOutOfRangeException.ThrowIfNegative(reloadSeconds);
        Size = size;
        Rounds = size;
        _reloadTicks = Math.Max(1, SimulationClock.SecondsToTicks(reloadSeconds));
    }

    /// <summary>How many rounds a full magazine holds.</summary>
    public int Size { get; }

    /// <summary>Rounds left in it.</summary>
    public int Rounds { get; private set; }

    /// <summary>True while it is being reloaded - no shot meanwhile.</summary>
    public bool IsReloading => _reloadTicksLeft > 0;

    /// <summary>How far the reload has come: 0 at its start, 1 when done (0 while not reloading).</summary>
    public float ReloadProgress => IsReloading ? 1f - ((float)_reloadTicksLeft / _reloadTicks) : 0f;

    /// <summary>Takes one round for a shot - false (and nothing happens) if it is empty or being reloaded.</summary>
    public bool TryTake()
    {
        if (IsReloading || Rounds == 0)
        {
            return false;
        }

        Rounds--;
        return true;
    }

    /// <summary>Starts reloading - false if it is full already or reloading anyway.</summary>
    public bool StartReload()
    {
        if (IsReloading || Rounds == Size)
        {
            return false;
        }

        _reloadTicksLeft = _reloadTicks;
        return true;
    }

    /// <summary>Breaks a reload off (e.g. when the weapon is put away); the rounds stay as they were.</summary>
    public void CancelReload() => _reloadTicksLeft = 0;

    /// <summary>One tick: a reload counts down; true in the tick it is done (the magazine is full again).</summary>
    public bool Tick()
    {
        if (!IsReloading || --_reloadTicksLeft > 0)
        {
            return false;
        }

        Rounds = Size;
        return true;
    }
}
