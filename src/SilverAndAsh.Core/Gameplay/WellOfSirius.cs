using System.Numerics;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Sirius' Relief (issue #438, named so since #716; the code still says well): a three-tier fountain in the middle of its cell. Nobody walks through it - players,
/// ghouls and Gorm bump into a circle of <see cref="WellBalance.CollisionRadius"/>, and ghouls and Gorm plan their way
/// round its cell as round a standing chest (they still see past it; bats fly over it). It is
/// ready until a player uses it with F, then it is spent and stays dry.
/// <para>
/// Provisional: what a throw of silver costs, what it may give and how often one may throw are not decided yet. Until
/// then using it costs nothing and hands out nothing - it turns from ready to spent, and the world rolls whether the
/// throw was lucky (<see cref="WellBalance.LuckChance"/>, <see cref="WellOfSiriusGave"/>), so the luck can be heard.
/// </para>
/// <para>
/// Heard (issue #815): the coin at every throw, the luck only when it gives, and the water as a loop while the well is
/// ready (<see cref="Audio.SoundLoops"/>).
/// </para>
/// </summary>
public sealed class WellOfSirius : IInteractable
{
    /// <summary>Name shown in the game.</summary>
    public const string Name = "Sirius' Relief";

    /// <summary>Height of the well with its top bowl, in metres (from the model).</summary>
    public const float Height = 2.3f;

    internal WellOfSirius(WellPlan plan, Level level, WellBalance balance)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(balance);
        Plan = plan;
        Radius = balance.CollisionRadius;
        Position = level.CellCenter(plan.X, plan.Z);
    }

    /// <summary>Place as the level was made.</summary>
    public WellPlan Plan { get; }

    /// <summary>Bottom centre on the floor of its cell.</summary>
    public Vector3 Position { get; }

    /// <summary>Radius of its solid circle, in metres.</summary>
    public float Radius { get; }

    /// <summary>True once a player has used it; then it stays dry.</summary>
    public bool IsSpent { get; private set; }

    /// <summary>Offered while it is ready.</summary>
    public bool CanInteract => !IsSpent;

    public Vector3 BoxMin => Position - new Vector3(Radius, 0f, Radius);

    public Vector3 BoxMax => Position + new Vector3(Radius, Height, Radius);

    /// <summary>Its solid body: a standing cylinder, as high as the well.</summary>
    public CylinderBody Body => new(Position, Radius, Height);

    /// <summary>"Use Sirius' Relief" while it is ready.</summary>
    public InteractionOffer OfferFor(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return new($"Use {Name}", !IsSpent);
    }

    /// <summary>Turns it from ready to spent. False if it was spent already.</summary>
    internal bool Use()
    {
        if (IsSpent)
        {
            return false;
        }

        IsSpent = true;
        return true;
    }
}
