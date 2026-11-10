using System.Numerics;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>The player character: where it stands and where it looks.</summary>
public sealed class Player
{
    /// <summary>Creates the player standing on the start cell of <paramref name="level"/>.</summary>
    public Player(Level level)
    {
        ArgumentNullException.ThrowIfNull(level);
        Position = level.PlayerStartPosition;
    }

    /// <summary>Position of the feet in world coordinates (metres).</summary>
    public Vector3 Position { get; private set; }

    /// <summary>Where the player is looking.</summary>
    public FirstPersonView View { get; } = new();

    /// <summary>Position of the eyes - where the camera sits.</summary>
    public Vector3 EyePosition => Position + new Vector3(0f, Tuning.Player.EyeHeight, 0f);

    /// <summary>
    /// Applies the look part of the input. Called once per drawn frame (not per tick),
    /// so turning the view feels immediate even at high frame rates.
    /// </summary>
    public void Look(PlayerInput input) => View.ApplyMouse(input.Look);
}
