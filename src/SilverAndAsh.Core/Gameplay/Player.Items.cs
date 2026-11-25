namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// The effects of the items a player carries (decision record 0008, issue #195). The rules are in
/// <see cref="ItemEffects"/>; here they meet this player's <see cref="Items"/> and the world's balance values.
/// </summary>
public sealed partial class Player
{
    /// <summary>
    /// What a blood vial that heals <paramref name="heal"/> heals this player: more with the Iron Flask. The world heals
    /// with it when the player walks over a vial.
    /// </summary>
    internal int VialHeal(int heal) => ItemEffects.VialHeal(heal, _items, _balance.Items);

    /// <summary>
    /// This player made a kill: the Silver Rosary gives back its health through <see cref="Heal"/> - never above
    /// <see cref="MaxHealth"/>, nothing for the dead, nothing without the rosary. Returns how much was really healed.
    /// </summary>
    internal int HealByKill(ICollection<GameEvent> events) => Heal(ItemEffects.KillHeal(_items, _balance.Items), events);

    /// <summary>Ticks a reload of <paramref name="magazine"/> takes for this player: fewer with the Quickdraw Holster.</summary>
    private int ReloadTicks(Magazine magazine) => ItemEffects.ReloadTicks(magazine.ReloadTicks, _items, _balance.Items);
}
