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

    /// <summary>Weapons and health.</summary>
    public static class Combat
    {
        /// <summary>The player's health at the start (and the most it can be).</summary>
        public const int PlayerMaxHealth = 100;

        /// <summary>Seconds between the player's death and the restart of the level.</summary>
        public const float RestartDelay = 2f;

        /// <summary>Silver blade: seconds before the next swing.</summary>
        public const float BladeCooldown = 0.4f;

        /// <summary>Silver blade: damage per hit.</summary>
        public const int BladeDamage = 40;

        /// <summary>Silver blade: seconds of holding the button until the blade is fully charged.</summary>
        public const float BladeChargeTime = 1f;

        /// <summary>Silver blade: damage multiplier when fully charged (2 = 200 %), linear from 1 when not charged.</summary>
        public const float BladeChargeMaxMultiplier = 2f;

        /// <summary>Silver blade: stamina segments a full charge costs (spent linearly while charging).</summary>
        public const int BladeChargeStaminaSegments = 1;

        /// <summary>Silver blade: how far it reaches from the eyes, in metres (to the surface of the target).</summary>
        public const float BladeRange = 2f;

        /// <summary>Silver blade: full width of the cone in degrees (90 = 45 to each side, also up and down).</summary>
        public const float BladeAngle = 90f;

        /// <summary>Pistol: damage per hit. A bit less than the blade, but with its slow rate every shot must count.</summary>
        public const int PistolDamage = 35;

        /// <summary>Pistol: damage multiplier for a hit on the head (critical hit), e.g. 3 = 300 %.</summary>
        public const float PistolHeadshotMultiplier = 3f;

        /// <summary>Pistol: how far a shot reaches, in metres.</summary>
        public const float PistolRange = 50f;

        /// <summary>Pistol: seconds before the next shot.</summary>
        public const float PistolCooldown = 0.8f;

        /// <summary>Training dummy: health (and the most it can be).</summary>
        public const int DummyMaxHealth = 100;

        /// <summary>Training dummy: seconds it lies on the ground before it stands up again.</summary>
        public const float DummyDownTime = 3f;

        /// <summary>Training dummy: radius of its body in metres (a bit slimmer than the player).</summary>
        public const float DummyRadius = 0.3f;

        /// <summary>Training dummy: height of its body in metres.</summary>
        public const float DummyHeight = 1.8f;

        /// <summary>Training dummy: the top part of this height (in metres) counts as the head.</summary>
        public const float DummyHeadHeight = 0.35f;
    }

    /// <summary>The bat's blood bolt: a flying projectile you can dodge.</summary>
    public static class BloodBolt
    {
        /// <summary>Speed in m/s - fast, but you see it coming.</summary>
        public const float Speed = 12f;

        /// <summary>Damage to the player.</summary>
        public const int Damage = 15;

        /// <summary>Radius of the ball in metres.</summary>
        public const float Radius = 0.15f;

        /// <summary>Seconds until it disappears if it hits nothing.</summary>
        public const float LifeTime = 5f;
    }

    /// <summary>The vampire bat: a small flying enemy.</summary>
    public static class Bat
    {
        /// <summary>Health: one blade hit or one pistol shot.</summary>
        public const int MaxHealth = 30;

        /// <summary>Radius of its body in metres (wings spread).</summary>
        public const float Radius = 0.35f;

        /// <summary>Height of its body in metres.</summary>
        public const float Height = 0.4f;

        /// <summary>How high above the floor it hovers (bottom of its body), in metres. Rooms are 3.5 m high.</summary>
        public const float HoverHeight = 2.2f;

        /// <summary>It keeps at least this much room between the top of its body and the ceiling, in metres.</summary>
        public const float CeilingGap = 0.3f;

        /// <summary>Seconds the dead body lies on the floor before it disappears (counted from its death).</summary>
        public const float CorpseTime = 2.5f;
    }

    /// <summary>The ghoul: a crawling enemy.</summary>
    public static class Ghoul
    {
        /// <summary>Health (2 blade hits, 1 pistol headshot, 3 pistol body shots).</summary>
        public const int MaxHealth = 80;

        /// <summary>Radius of its body in metres - a bit wider than the player, it crawls on all fours.</summary>
        public const float Radius = 0.4f;

        /// <summary>Height of its body in metres - crawling, so much lower than the player.</summary>
        public const float Height = 1f;

        /// <summary>The top part of this height (in metres), in its front half, counts as the head.</summary>
        public const float HeadHeight = 0.35f;

        /// <summary>Seconds the dead body lies on the ground before it disappears.</summary>
        public const float CorpseTime = 2f;

        /// <summary>It notices a player it can see who is closer than this, in metres.</summary>
        public const float SightRange = 12f;

        /// <summary>Height of its eyes above its feet (for seeing players), in metres.</summary>
        public const float EyeHeight = 0.8f;

        /// <summary>Crawling speed in m/s - three quarters of the player's 6 m/s: you can get away, but not by strolling.</summary>
        public const float Speed = 4.5f;

        /// <summary>It starts its blow when a player is this close (centre to centre, flat), in metres.</summary>
        public const float AttackReach = 1.6f;

        /// <summary>
        /// The blow hits a player up to this far away (centre to centre, flat) - farther than <see cref="AttackReach"/>,
        /// so one short step back is not enough; you have to really get away or dodge.
        /// </summary>
        public const float HitReach = 2.4f;

        /// <summary>Seconds of drawing back before the blow - the time to step away or dodge.</summary>
        public const float WindUpTime = 0.5f;

        /// <summary>The blow only hits a player at most this many degrees to the side of where it faced at the start.</summary>
        public const float AttackHalfAngle = 60f;

        /// <summary>Damage of a blow.</summary>
        public const int AttackDamage = 20;

        /// <summary>Seconds of rest after a blow.</summary>
        public const float RecoverTime = 0.8f;

        /// <summary>The way to the target is searched again at most this often, in seconds.</summary>
        public const float RepathTime = 0.5f;
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
