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
        /// A normal jump landing (about 6.5 m/s) gives about 12 cm.
        /// </summary>
        public const float LandingDipPerSpeed = 0.01875f;

        /// <summary>Landing dip: the eyes never sink further than this, in metres.</summary>
        public const float LandingDipMax = 0.22f;

        /// <summary>Landing dip: how long the whole nod (down and back up) takes, in seconds. Longer = softer.</summary>
        public const float LandingDipDuration = 0.3f;

        /// <summary>
        /// Perfect jump: how close to the landing (before OR after) the jump must be pressed, in seconds.
        /// Bigger = easier.
        /// </summary>
        public const float PerfectJumpWindow = 0.12f;

        /// <summary>Perfect jump: extra top speed, e.g. 0.2 = +20 %. Does not stack.</summary>
        public const float PerfectJumpSpeedBonus = 0.2f;

        /// <summary>Perfect jump: the landing nod is scaled by this (0.4 = only 40 % as deep - "cleanly absorbed").</summary>
        public const float PerfectJumpDipFactor = 0.4f;

        /// <summary>Stamina: how many jumps a full bar pays for. Every jump costs one, normal or perfect.</summary>
        public const int StaminaJumps = 5;

        /// <summary>
        /// Stamina: seconds from empty to full. Refills only while standing on the ground
        /// (longer than the perfect window, so a chain of jumps never refills it).
        /// </summary>
        public const float StaminaRechargeTime = 3f;

        /// <summary>
        /// Jump with an empty bar ("exhausted"): walking speed is lowered by this, e.g. 0.2 = -20 %,
        /// until the chain of jumps ends.
        /// </summary>
        public const float ExhaustedJumpSpeedPenalty = 0.2f;

        /// <summary>Dash: how far it goes, in metres (flat, in the walking direction).</summary>
        public const float DashDistance = 4f;

        /// <summary>Dash: how long it takes, in seconds. No gravity during this time.</summary>
        public const float DashDuration = 0.15f;

        /// <summary>Dodge (on the ground): how long it lasts, in seconds.</summary>
        public const float DodgeDuration = 0.4f;

        /// <summary>Dodge: speed in m/s (0.4 s x 10 m/s = about 4 m).</summary>
        public const float DodgeSpeed = 10f;

        /// <summary>Dodge: stamina cost in segments.</summary>
        public const int DodgeStaminaSegments = 2;

        /// <summary>Dodge: height of the ducked body (hitbox) in metres, instead of <see cref="Height"/>.</summary>
        public const float DodgeHeight = 1f;

        /// <summary>Dodge: eye height while ducked, instead of <see cref="EyeHeight"/>.</summary>
        public const float DodgeEyeHeight = 0.85f;

        /// <summary>Ducking and standing up take this long, in seconds (smooth, no jerk).</summary>
        public const float CrouchTransitionTime = 0.1f;

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
