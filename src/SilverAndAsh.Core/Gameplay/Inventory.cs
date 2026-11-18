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

    // A place of the bag: the kind, and for a ranged weapon the weapon itself - it keeps its magazine in the bag.
    private readonly record struct BagEntry(WeaponKind Kind, RangedWeapon? Ranged);

    private readonly BagEntry?[] _bag = new BagEntry?[InventorySlotCount];

    private readonly List<ItemKind> _items = [];

    /// <summary>The weapons carried but not equipped: <see cref="InventorySlotCount"/> places, empty ones null.</summary>
    public IReadOnlyList<WeaponKind?> Bag => _bag.Select(entry => entry?.Kind).ToList();

    /// <summary>The ranged weapon in bag place <paramref name="index"/> with its magazine, or null (empty, or a melee weapon).</summary>
    public RangedWeapon? BagRanged(int index) => _bag[index]?.Ranged;

    /// <summary>True while a place of the <see cref="Bag"/> is free - otherwise a weapon on the floor stays lying ("Inventory full").</summary>
    public bool HasBagRoom => Array.IndexOf(_bag, null) >= 0;

    /// <summary>
    /// The items this player has found, in the order taken (decision record 0008): passive for the whole run, no place
    /// needed. Placeholders for now - their effects come with #195 and #196.
    /// </summary>
    public IReadOnlyList<ItemKind> Items => _items;

    /// <summary>
    /// True while the inventory window is open (G). The world goes on meanwhile; only the player closes it - neither
    /// moving nor a hit does (issue #346).
    /// </summary>
    public bool IsInventoryOpen { get; private set; }

    /// <summary>
    /// Puts <paramref name="kind"/> (a ranged one ready and full) into the first free place of the <see cref="Bag"/>.
    /// Returns false (and keeps nothing) if all places are taken - loot (#194) then leaves the weapon lying
    /// ("Inventory full"). Tests use this, too.
    /// </summary>
    public bool Stow(WeaponKind kind) => Stow(kind, null);

    /// <summary>
    /// Puts <paramref name="kind"/> into the first free place of the <see cref="Bag"/> as it was taken from the floor:
    /// a ranged one as <paramref name="ranged"/>, with the magazine it was dropped with - or ready and full if it comes
    /// without one (from a chest). Returns false (and keeps nothing) if all places are taken.
    /// </summary>
    public bool Stow(WeaponKind kind, RangedWeapon? ranged)
    {
        if (ranged is not null && ranged.Kind != kind)
        {
            throw new ArgumentException($"The weapon {ranged.Kind} is no {kind}.", nameof(ranged));
        }

        if (ranged is null && Weapons.Info(kind).Hand == WeaponHand.Ranged)
        {
            ranged = new RangedWeapon(kind, _balance);
        }

        var free = Array.IndexOf(_bag, null);
        if (free < 0)
        {
            return false;
        }

        _bag[free] = new BagEntry(kind, ranged);
        return true;
    }

    /// <summary>Adds <paramref name="kind"/> to the <see cref="Items"/> - an item needs no place and is always taken.</summary>
    internal void Take(ItemKind kind) => _items.Add(kind);

    /// <summary>
    /// Opens and closes the window (G) and carries out a click while it is open. Comes first in every tick, so a click acts
    /// on the weapons before this tick's attacks. Only the player closes it - neither moving nor a hit does (issue #346).
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

        if (IsInventoryOpen && input.InventoryClick is { } click)
        {
            Click(click);
        }
    }

    private void Click(InventoryClick click)
    {
        switch (click.Place)
        {
            case InventoryPlace.Bag when click.Index >= 0 && click.Index < InventorySlotCount && _bag[click.Index] is { } entry:
                if (click.Drop)
                {
                    _bag[click.Index] = null;
                    _events.Add(new WeaponDropped(Id, entry.Kind, Position, entry.Ranged));
                }
                else
                {
                    EquipFromBag(click.Index, entry);
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
    /// free ranged place, or - all taken - swaps with the one in the hand. Ranged weapons keep their magazine; the one put
    /// away stops reloading, like on a weapon switch.
    /// </summary>
    private void EquipFromBag(int index, BagEntry entry)
    {
        if (entry.Ranged is not { } weapon)
        {
            _bag[index] = new BagEntry(Melee, null);
            EquipMelee(entry.Kind);
            return;
        }

        var free = Array.IndexOf(_rangedSlots, null);
        if (free >= 0)
        {
            _bag[index] = null;
            _rangedSlots[free] = weapon;
            return;
        }

        var putAway = Ranged;
        putAway.Magazine.CancelReload();
        _bag[index] = new BagEntry(putAway.Kind, putAway);
        _rangedSlots[SelectedSlot] = weapon;
        _events.Add(new WeaponSwitched(Id, SelectedSlot, weapon.Kind));
    }

    /// <summary>Right click on a ranged place: drops that weapon with its magazine - never the last one (decision record 0008).</summary>
    private void DropRanged(int slot)
    {
        if (_rangedSlots[slot] is not { } weapon || _rangedSlots.Count(place => place is not null) <= 1)
        {
            return;
        }

        weapon.Magazine.CancelReload();
        _rangedSlots[slot] = null;
        _events.Add(new WeaponDropped(Id, weapon.Kind, Position, weapon));
        if (slot != SelectedSlot)
        {
            return;
        }

        // The weapon in the hand is gone: the first other one comes into the hand.
        SelectedSlot = Array.FindIndex(_rangedSlots, place => place is not null);
        _events.Add(new WeaponSwitched(Id, SelectedSlot, Ranged.Kind));
    }
}
