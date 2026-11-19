using System.Numerics;
using SilverAndAsh.Core.Physics;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// A piece of equipment lying in the level (decision record 0008, issue #317): a weapon (<see cref="WeaponDrop"/>) or
/// an item (<see cref="ItemDrop"/>). It falls to the floor and stays there. A player takes it with F - walking over it
/// does nothing. A weapon needs a free place - a ranged one goes into a free ranged place first, then into the bag
/// (issue #434); an item needs none and is always taken.
/// </summary>
public abstract class EquipmentDrop : IInteractable
{
    /// <summary>Half the width of the box a player has to look at to take it - a little wider than the piece itself.</summary>
    public const float BoxRadius = 0.35f;

    /// <summary>Height of that box above the floor.</summary>
    public const float BoxHeight = 0.5f;

    private Vector3 _velocity;

    /// <summary>Creates it lying at <paramref name="position"/> (the bottom of its body; it falls from there).</summary>
    protected EquipmentDrop(int id, Vector3 position)
    {
        Id = id;
        Position = position;
        PreviousPosition = position;
    }

    public int Id { get; }

    /// <summary>What lies there: its name and tier.</summary>
    public abstract Equipment Piece { get; }

    /// <summary>Bottom centre of its body in world coordinates.</summary>
    public Vector3 Position { get; private set; }

    /// <summary>The position one tick earlier, for smooth drawing between ticks.</summary>
    public Vector3 PreviousPosition { get; private set; }

    /// <summary>Always true: as long as it lies there, it is offered - "Inventory full" says why F does nothing.</summary>
    public bool CanInteract => true;

    public Vector3 BoxMin => Position - new Vector3(BoxRadius, 0f, BoxRadius);

    public Vector3 BoxMax => Position + new Vector3(BoxRadius, BoxHeight, BoxRadius);

    /// <summary>
    /// "Take Rifle (Forged)" in the colour of its tier; for a weapon "Inventory full" (F does nothing, it stays lying)
    /// when <paramref name="player"/> has no room for it (<see cref="Player.HasRoomFor"/>). An item is always taken.
    /// </summary>
    public InteractionOffer OfferFor(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return this is WeaponDrop weapon && !player.HasRoomFor(weapon.Kind)
            ? new InteractionOffer("Inventory full", false, Piece.Tier)
            : new InteractionOffer($"Take {Piece.Name} ({Piece.Tier})", true, Piece.Tier);
    }

    /// <summary>One tick of falling (with <paramref name="gravity"/>) until it lies on the floor.</summary>
    public void Fall(World.Level level, float gravity)
    {
        ArgumentNullException.ThrowIfNull(level);
        PreviousPosition = Position;
        (Position, _velocity, _) = CharacterCollision.MoveVertically(
            level, Position, _velocity, BodySizes.Pickup.Radius, BodySizes.Pickup.Height, gravity, SimulationClock.SecondsPerTick);
    }
}

/// <summary>An item lying in the level, from a chest or Gorm (#318) or put there by the showroom.</summary>
public sealed class ItemDrop : EquipmentDrop
{
    /// <summary>Creates <paramref name="kind"/> lying at <paramref name="position"/>.</summary>
    public ItemDrop(int id, ItemKind kind, Vector3 position)
        : base(id, position)
    {
        Piece = Equipment.Of(kind);
        Kind = kind;
    }

    public ItemKind Kind { get; }

    public override Equipment Piece { get; }
}
