using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// A chest (decision record 0006): Beggar's, Guard's, King's or Vampire's. F opens it - a locked one only with a key,
/// which is used up. Its contents (silver, maybe a blood vial) were decided when the level was made and jump out
/// towards the player who opens it; the open chest stays standing, empty. The chest of a combat room is hidden in the
/// floor until the room is cleared, then it rises. It does not block walking (a placeholder for now).
/// </summary>
public sealed class Chest : IInteractable
{
    /// <summary>Seconds a chest takes to rise from the floor.</summary>
    public const float RiseSeconds = 1f;

    /// <summary>Width of the chest (along X), in metres.</summary>
    public const float Width = 1f;

    /// <summary>Height of the closed chest, in metres.</summary>
    public const float Height = 0.7f;

    /// <summary>Depth of the chest (along Z), in metres.</summary>
    public const float Depth = 0.7f;

    private static readonly int RiseTicks = SimulationClock.SecondsToTicks(RiseSeconds);
    private int _risenTicks;

    internal Chest(int id, ChestPlan plan, Level level)
    {
        Id = id;
        Plan = plan;
        var floor = level[plan.X, plan.Z].FloorHeight;
        Position = new Vector3((plan.X + 0.5f) * Level.CellSize, floor, (plan.Z + 0.5f) * Level.CellSize);
        _risenTicks = plan.RisesWhenCleared ? 0 : RiseTicks;
        IsLocked = plan.IsLocked;
    }

    /// <summary>Number of the chest in its world (position in <see cref="GameWorld.Chests"/>).</summary>
    public int Id { get; }

    /// <summary>Place, tier, lock and contents as the level was made.</summary>
    public ChestPlan Plan { get; }

    public ChestTier Tier => Plan.Tier;

    /// <summary>Bottom centre on the floor of its cell.</summary>
    public Vector3 Position { get; }

    /// <summary>True while it still needs a key.</summary>
    public bool IsLocked { get; private set; }

    /// <summary>True once opened; then it stays open and empty.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>True once it has started to rise (or was never hidden).</summary>
    public bool IsRising { get; private set; }

    /// <summary>How far it has risen: 0 = still in the floor, 1 = standing. Chests outside combat rooms stand from the start.</summary>
    public float RiseFraction => (float)_risenTicks / RiseTicks;

    /// <summary>Offered while it stands (fully risen) and is not open yet.</summary>
    public bool CanInteract => RiseFraction >= 1f && !IsOpen;

    public Vector3 BoxMin => Position - new Vector3(Width / 2f, 0f, Depth / 2f);

    public Vector3 BoxMax => Position + new Vector3(Width / 2f, Height, Depth / 2f);

    /// <summary>"Open Guard's Chest", or for a locked chest "Unlock (1 key)" / "Locked - needs a key".</summary>
    public InteractionOffer OfferFor(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!IsLocked)
        {
            return new($"Open {Tier.ChestName()}", true);
        }

        return player.Keys > 0 ? new("Unlock (1 key)", true) : new("Locked - needs a key", false);
    }

    /// <summary>Starts rising from the floor (only a hidden chest; once).</summary>
    internal bool StartRising()
    {
        if (IsRising || !Plan.RisesWhenCleared)
        {
            return false;
        }

        IsRising = true;
        return true;
    }

    /// <summary>One tick of rising.</summary>
    internal void Tick()
    {
        if (IsRising && _risenTicks < RiseTicks)
        {
            _risenTicks++;
        }
    }

    internal void Unlock() => IsLocked = false;

    internal void Open() => IsOpen = true;
}
