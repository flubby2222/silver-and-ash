using System.Numerics;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// A weapon lying in the level - dropped from the inventory (right click, issue #193), from a chest or Gorm (#318), or
/// put there by the showroom. A player takes it up again with F (#317); a ranged one keeps its magazine.
/// </summary>
public sealed class WeaponDrop : EquipmentDrop
{
    /// <summary>Creates <paramref name="kind"/> lying at <paramref name="position"/> (the bottom of its body; it falls from there); a ranged one as <paramref name="ranged"/>, with its magazine.</summary>
    public WeaponDrop(int id, WeaponKind kind, Vector3 position, RangedWeapon? ranged = null)
        : base(id, position)
    {
        if (ranged is not null && ranged.Kind != kind)
        {
            throw new ArgumentException($"The weapon {ranged.Kind} is no {kind}.", nameof(ranged));
        }

        Piece = Equipment.Of(kind);
        Ranged = ranged;
        Kind = kind;
    }

    public WeaponKind Kind { get; }

    /// <summary>A ranged weapon as it was dropped, with its magazine (null for a melee weapon or a new one).</summary>
    public RangedWeapon? Ranged { get; }

    /// <summary>Its name, hand and tier.</summary>
    public WeaponInfo Info => Weapons.Info(Kind);

    public override Equipment Piece { get; }
}
