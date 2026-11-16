namespace SilverAndAsh.Core.Gameplay;

/// <summary>Where a place of the inventory window is: the bag of weapons not in use, the ranged places or the melee hand.</summary>
public enum InventoryPlace
{
    /// <summary>One of the <see cref="Player.InventorySlotCount"/> places for weapons that are not equipped.</summary>
    Bag,

    /// <summary>One of the <see cref="Player.RangedSlotCount"/> ranged places (right mouse button).</summary>
    Ranged,

    /// <summary>The melee hand (left mouse button); there is only one, index 0.</summary>
    Melee,
}

/// <summary>
/// A click into the open inventory (G, decision record 0008): <paramref name="Place"/> and <paramref name="Index"/> name the
/// place, <paramref name="Drop"/> tells a right click (drop it) from a left click (equip it). It comes into the tick as input
/// (online co-op rule 2), see <see cref="Input.PlayerInput.InventoryClick"/>.
/// </summary>
public readonly record struct InventoryClick(InventoryPlace Place, int Index, bool Drop = false);

/// <summary>The inventory of a player (decision record 0008, issue #193).</summary>
public sealed partial class Player
{
    /// <summary>Places for weapons that are carried but not equipped (decision record 0008).</summary>
    public const int InventorySlotCount = 6;

    private readonly WeaponKind?[] _bag = new WeaponKind?[InventorySlotCount];

    /// <summary>The weapons carried but not equipped: <see cref="InventorySlotCount"/> places, empty ones null.</summary>
    public IReadOnlyList<WeaponKind?> Bag => _bag;

    /// <summary>
    /// True while the inventory window is open (G). The world goes on meanwhile; walking, jumping, Shift or a hit close it.
    /// </summary>
    public bool IsInventoryOpen { get; private set; }

    /// <summary>
    /// Puts <paramref name="kind"/> into the first free place of the <see cref="Bag"/>. Returns false (and keeps nothing)
    /// if all places are taken - loot (#194) then leaves the weapon lying ("Inventory full"). Tests use this, too.
    /// </summary>
    public bool Stow(WeaponKind kind)
    {
        _ = Weapons.Info(kind);
        var free = Array.IndexOf(_bag, null);
        if (free < 0)
        {
            return false;
        }

        _bag[free] = kind;
        return true;
    }

    /// <summary>
    /// Opens and closes the window (G), closes it when the player moves, and carries out a click while it is open. Comes
    /// first in every tick, so a click acts on the weapons before this tick's attacks.
    /// </summary>
    private void UseInventory(Input.PlayerInput input)
    {
        if (IsDead)
        {
            IsInventoryOpen = false;
            return;
        }

        if (input.ToggleInventory)
        {
            IsInventoryOpen = !IsInventoryOpen;
        }

        // Moving closes it (decision record 0008): walking, jumping, Shift. Being pulled by Gorm's chain is no own move.
        if (input.Move != System.Numerics.Vector2.Zero || input.Jump || input.Dash)
        {
            IsInventoryOpen = false;
        }

        if (IsInventoryOpen && input.InventoryClick is { } click)
        {
            Click(click);
        }
    }

    /// <summary>A hit closes the window (decision record 0008).</summary>
    private void CloseInventoryOnHit() => IsInventoryOpen = false;

    private void Click(InventoryClick click)
    {
        switch (click.Place)
        {
            case InventoryPlace.Bag when click.Index >= 0 && click.Index < InventorySlotCount && _bag[click.Index] is { } kind:
                if (click.Drop)
                {
                    _bag[click.Index] = null;
                    _events.Add(new WeaponDropped(Id, kind, Position));
                }
                else
                {
                    EquipFromBag(click.Index, kind);
                }

                break;
            case InventoryPlace.Ranged when click.Drop && click.Index >= 0 && click.Index < RangedSlotCount:
                DropRanged(click.Index);
                break;

            // A left click on an equipped weapon, and a right click on the melee hand, do nothing: the hand is never empty.
        }
    }

    /// <summary>
    /// Left click on a weapon in the bag: a melee weapon swaps with the one in the hand; a ranged one goes into the first
    /// free ranged place, or - all taken - swaps with the one in the hand.
    /// </summary>
    private void EquipFromBag(int index, WeaponKind kind)
    {
        if (Weapons.Info(kind).Hand == WeaponHand.Melee)
        {
            _bag[index] = Melee;
            EquipMelee(kind);
            return;
        }

        var free = Array.IndexOf(_rangedSlots, null);
        if (free >= 0)
        {
            _bag[index] = null;
            Equip(free, kind);
            return;
        }

        // The weapon put away keeps only its kind: taken out again later it comes full (see the pull request of #193).
        _bag[index] = Ranged.Kind;
        Equip(SelectedSlot, kind);
        _events.Add(new WeaponSwitched(Id, SelectedSlot, kind));
    }

    /// <summary>Right click on a ranged place: drops that weapon - never the last one (decision record 0008).</summary>
    private void DropRanged(int slot)
    {
        if (_rangedSlots[slot] is not { } weapon || _rangedSlots.Count(place => place is not null) <= 1)
        {
            return;
        }

        _rangedSlots[slot] = null;
        _events.Add(new WeaponDropped(Id, weapon.Kind, Position));
        if (slot != SelectedSlot)
        {
            return;
        }

        // The weapon in the hand is gone: the first other one comes into the hand.
        SelectedSlot = Array.FindIndex(_rangedSlots, place => place is not null);
        _events.Add(new WeaponSwitched(Id, SelectedSlot, Ranged.Kind));
    }
}
