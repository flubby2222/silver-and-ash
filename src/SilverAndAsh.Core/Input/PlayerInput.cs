using System.Numerics;

namespace SilverAndAsh.Core.Input;

/// <summary>
/// What the player wants to do, independent of keyboard, mouse or gamepad.
/// The Game project fills this from Raylib; Core only ever sees this struct.
/// </summary>
/// <param name="Move">
/// Desired walking direction relative to the view: X = right (+) / left (-), Y = forward (+) / back (-).
/// Each component between -1 and 1.
/// </param>
/// <param name="Look">Mouse movement since the last frame in pixels: X = right (+), Y = down (+).</param>
/// <param name="Jump">True while the jump button is held.</param>
/// <param name="Dash">True while the dash button is held (Shift): dash in the air, dodge on the ground.</param>
/// <param name="Melee">True while the button for the silver blade is held (left mouse button).</param>
/// <param name="Shoot">True while the button for the pistol is held (right mouse button).</param>
/// <param name="ChooseBoon">The boon chosen for a waiting level up (X, then 1 - 4 or a click), or null for none.</param>
/// <param name="Interact">
/// True in the one tick after the interaction key (F) was pressed: open the door (later: chest, lock, person) in front.
/// </param>
/// <param name="Reload">True while the reload key (R) is held - pressing it reloads the ranged weapon.</param>
/// <param name="SelectSlot">The ranged weapon place chosen with 1 / 2 / 3 (0, 1, 2), or null.</param>
/// <param name="CycleSlot">The mouse wheel: +1 the next ranged weapon, -1 the one before, 0 none.</param>
/// <param name="Paused">
/// True while the player has the Esc menu open. The world only stands still while ALL players have paused - alone
/// that is this one; in online co-op it would go on.
/// </param>
public readonly record struct PlayerInput(
    Vector2 Move, Vector2 Look, bool Jump, bool Dash = false, bool Melee = false, bool Shoot = false,
    Gameplay.Boon? ChooseBoon = null, bool Interact = false, bool Paused = false, bool Reload = false,
    int? SelectSlot = null, int CycleSlot = 0)
{
    /// <summary>No input at all.</summary>
    public static PlayerInput None { get; } = new(Vector2.Zero, Vector2.Zero, false);
}
