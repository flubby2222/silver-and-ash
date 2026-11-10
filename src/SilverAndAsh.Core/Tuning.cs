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

        /// <summary>Highest floor step the player walks up without jumping, in metres. Higher steps block like walls.</summary>
        public const float StepHeight = 0.4f;

        /// <summary>Top walking speed in metres per second.</summary>
        public const float MaxSpeed = 6f;

        /// <summary>Time in seconds to go from standing to top speed (and from top speed to standing).</summary>
        public const float AccelerationTime = 0.1f;

        /// <summary>Speed change per second, derived from the two values above.</summary>
        public const float Acceleration = MaxSpeed / AccelerationTime;

        /// <summary>How much of the ground acceleration is available in the air (1 = full control, 0 = none).</summary>
        public const float AirControl = 0.7f;

        /// <summary>Top speed in the air as a multiple of <see cref="MaxSpeed"/> - a small boost while jumping.</summary>
        public const float AirSpeedMultiplier = 1.15f;

        /// <summary>How high a jump lifts the feet, in metres. Must stay below ceiling minus <see cref="Height"/>.</summary>
        public const float JumpHeight = 1.3f;

        /// <summary>Time from take-off to landing on flat ground, in seconds. Longer = floatier.</summary>
        public const float JumpDuration = 0.8f;

        /// <summary>
        /// Landing dip: how deep the eyes sink per m/s of falling speed, in metres.
        /// A normal jump landing (about 6.5 m/s) gives about 8 cm.
        /// </summary>
        public const float LandingDipPerSpeed = 0.0125f;

        /// <summary>Landing dip: the eyes never sink further than this, in metres.</summary>
        public const float LandingDipMax = 0.15f;

        /// <summary>Landing dip: how long the whole nod (down and back up) takes, in seconds. Longer = softer.</summary>
        public const float LandingDipDuration = 0.45f;

        /// <summary>Landing dip: how much the view tilts down per metre of dip, in radians (the "nod").</summary>
        public const float LandingTiltPerMetre = 0.35f;

        // Derived from height and duration (physics of a throw straight up):
        // time to the top t = duration / 2, gravity g = 2h / t², take-off speed v = 2h / t.
        private const float TimeToApex = JumpDuration / 2f;

        /// <summary>Downward acceleration in m/s² (derived - about 16, still stronger than on Earth's 9.81).</summary>
        public const float Gravity = 2f * JumpHeight / (TimeToApex * TimeToApex);

        /// <summary>Upward speed at take-off in m/s (derived).</summary>
        public const float JumpSpeed = 2f * JumpHeight / TimeToApex;
    }
}
