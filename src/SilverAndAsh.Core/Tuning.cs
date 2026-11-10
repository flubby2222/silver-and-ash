namespace SilverAndAsh.Core;

/// <summary>
/// All values that shape how the game feels, in one place.
/// They move into a JSON balancing file in milestone v0.7.
/// </summary>
public static class Tuning
{
    /// <summary>Mouse and keyboard.</summary>
    public static class Controls
    {
        /// <summary>
        /// How far the view turns per pixel of mouse movement, in radians.
        /// 0.0025 rad = about 0.14 degrees - a medium sensitivity.
        /// </summary>
        public const float MouseSensitivity = 0.0025f;

        /// <summary>Maximum look up/down angle: just under 90 degrees, so the view never flips over.</summary>
        public const float MaxPitchRadians = 89f * MathF.PI / 180f;
    }

    /// <summary>The player character.</summary>
    public static class Player
    {
        /// <summary>Height of the eyes above the feet, in metres.</summary>
        public const float EyeHeight = 1.6f;

        /// <summary>Radius of the player's collision capsule in metres (how close the player gets to walls).</summary>
        public const float Radius = 0.35f;

        /// <summary>Total height of the player's collision capsule in metres.</summary>
        public const float Height = 1.8f;

        /// <summary>Top walking speed in metres per second.</summary>
        public const float MaxSpeed = 6f;

        /// <summary>Time in seconds to go from standing to top speed (and from top speed to standing).</summary>
        public const float AccelerationTime = 0.1f;

        /// <summary>Speed change per second, derived from the two values above.</summary>
        public const float Acceleration = MaxSpeed / AccelerationTime;
    }
}
