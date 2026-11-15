namespace SilverAndAsh.Core;

/// <summary>
/// All balancing values - health, damage, cooldowns, speeds, times, ranges - in one immutable object.
/// The game world gets one and hands its parts to everything it creates. There is no static, changeable copy:
/// every world can have its own values (online co-op rule 4), and tests can try other values easily:
/// <c>Balance.Default with { Ghoul = Balance.Default.Ghoul with { Speed = 2f } }</c>.
/// <para>Body sizes and controls are not balancing - see <see cref="BodySizes"/> and <see cref="ControlSettings"/>.</para>
/// </summary>
public sealed record Balance
{
    /// <summary>The standard values of the game.</summary>
    public static Balance Default { get; } = new();

    /// <summary>The player character.</summary>
    public PlayerBalance Player { get; init; } = new();

    /// <summary>The silver blade.</summary>
    public BladeBalance Blade { get; init; } = new();

    /// <summary>The pistol with silver bullets.</summary>
    public PistolBalance Pistol { get; init; } = new();

    /// <summary>The training dummy.</summary>
    public DummyBalance Dummy { get; init; } = new();

    /// <summary>The ghoul: a crawling enemy.</summary>
    public GhoulBalance Ghoul { get; init; } = new();

    /// <summary>The vampire bat: a small flying enemy.</summary>
    public BatBalance Bat { get; init; } = new();

    /// <summary>The bat's blood bolt: a flying projectile you can dodge.</summary>
    public BloodBoltBalance BloodBolt { get; init; } = new();

    /// <summary>What killed enemies leave behind.</summary>
    public DropsBalance Drops { get; init; } = new();

    /// <summary>The blood vial: a consumable that heals.</summary>
    public BloodVialBalance BloodVial { get; init; } = new();

    /// <summary>Consecrated ammunition: a consumable that makes the pistol stronger for a while.</summary>
    public ConsecratedAmmoBalance ConsecratedAmmo { get; init; } = new();

    /// <summary>Seconds between the death of all players and the restart of the level.</summary>
    public float RestartDelay { get; init; } = 2f;
}

/// <summary>How often killed enemies drop a consumable.</summary>
public sealed record DropsBalance
{
    /// <summary>Chance that a killed ghoul drops something: 0 = never, 1 = always.</summary>
    public float GhoulChance { get; init; } = 0.3f;

    /// <summary>Chance that a killed bat drops something: 0 = never, 1 = always.</summary>
    public float BatChance { get; init; } = 0.15f;

    /// <summary>Of all drops, this share is consecrated ammunition (0.3 = 30 %); the rest are blood vials.</summary>
    public float ConsecratedAmmoShare { get; init; } = 0.3f;
}

/// <summary>Balancing values of consecrated ammunition.</summary>
public sealed record ConsecratedAmmoBalance
{
    /// <summary>The pistol's damage is multiplied by this while it lasts (2 = double).</summary>
    public float DamageMultiplier { get; init; } = 2f;

    /// <summary>Seconds it lasts - starting at once when picked up. Picking up another starts the time again.</summary>
    public float Duration { get; init; } = 15f;
}

/// <summary>Balancing values of the blood vial.</summary>
public sealed record BloodVialBalance
{
    /// <summary>Health it gives back - never above the maximum.</summary>
    public int Heal { get; init; } = 50;
}

/// <summary>Balancing values of the player character.</summary>
public sealed record PlayerBalance
{
    /// <summary>The player's health at the start (and the most it can be).</summary>
    public int MaxHealth { get; init; } = 100;

    /// <summary>Top walking speed in metres per second.</summary>
    public float MaxSpeed { get; init; } = 6f;

    /// <summary>Time in seconds to go from standing to top speed (and from top speed to standing).</summary>
    public float AccelerationTime { get; init; } = 0.1f;

    /// <summary>Speed change per second, derived from the two values above.</summary>
    public float Acceleration => MaxSpeed / AccelerationTime;

    /// <summary>How much of the ground acceleration is available in the air (1 = full control, 0 = none).</summary>
    public float AirControl { get; init; } = 0.7f;

    /// <summary>Top speed in the air as a multiple of <see cref="MaxSpeed"/> - a small boost while jumping.</summary>
    public float AirSpeedMultiplier { get; init; } = 1.15f;

    /// <summary>How high a jump lifts the feet, in metres. Must stay below ceiling minus the player's height.</summary>
    public float JumpHeight { get; init; } = 1.3f;

    /// <summary>Time from take-off to landing on flat ground, in seconds. Longer = floatier.</summary>
    public float JumpDuration { get; init; } = 0.8f;

    // Derived from height and duration (physics of a throw straight up):
    // time to the top t = duration / 2, gravity g = 2h / t², take-off speed v = 2h / t.
    private float TimeToApex => JumpDuration / 2f;

    /// <summary>Downward acceleration in m/s² (derived - about 16, still stronger than on Earth's 9.81).</summary>
    public float Gravity => 2f * JumpHeight / (TimeToApex * TimeToApex);

    /// <summary>Upward speed at take-off in m/s (derived).</summary>
    public float JumpSpeed => 2f * JumpHeight / TimeToApex;

    /// <summary>
    /// Landing dip: how deep the eyes sink per m/s of falling speed, in metres.
    /// A normal jump landing (about 6.5 m/s) gives about 12 cm.
    /// </summary>
    public float LandingDipPerSpeed { get; init; } = 0.01875f;

    /// <summary>Landing dip: the eyes never sink further than this, in metres.</summary>
    public float LandingDipMax { get; init; } = 0.22f;

    /// <summary>Landing dip: how long the whole nod (down and back up) takes, in seconds. Longer = softer.</summary>
    public float LandingDipDuration { get; init; } = 0.3f;

    /// <summary>Landing dip: how much the view tilts down per metre of dip, in radians (the "nod").</summary>
    public float LandingTiltPerMetre { get; init; } = 0.35f;

    /// <summary>Perfect jump: how close to the landing (before OR after) the jump must be pressed, in seconds. Bigger = easier.</summary>
    public float PerfectJumpWindow { get; init; } = 0.12f;

    /// <summary>Perfect jump: extra top speed, e.g. 0.2 = +20 %. Does not stack.</summary>
    public float PerfectJumpSpeedBonus { get; init; } = 0.2f;

    /// <summary>Perfect jump: the landing nod is scaled by this (0.4 = only 40 % as deep - "cleanly absorbed").</summary>
    public float PerfectJumpDipFactor { get; init; } = 0.4f;

    /// <summary>Stamina: how many jumps a full bar pays for. Every jump costs one, normal or perfect.</summary>
    public int StaminaJumps { get; init; } = 5;

    /// <summary>
    /// Stamina: seconds from empty to full. Refills only while standing on the ground
    /// (longer than the perfect window, so a chain of jumps never refills it).
    /// </summary>
    public float StaminaRechargeTime { get; init; } = 3f;

    /// <summary>
    /// Jump with an empty bar ("exhausted"): walking speed is lowered by this, e.g. 0.2 = -20 %,
    /// until the chain of jumps ends.
    /// </summary>
    public float ExhaustedJumpSpeedPenalty { get; init; } = 0.2f;

    /// <summary>Dash: how far it goes, in metres (flat, in the walking direction).</summary>
    public float DashDistance { get; init; } = 4f;

    /// <summary>Dash: how long it takes, in seconds. No gravity during this time.</summary>
    public float DashDuration { get; init; } = 0.15f;

    /// <summary>Dodge (on the ground): how long it lasts, in seconds.</summary>
    public float DodgeDuration { get; init; } = 0.4f;

    /// <summary>Dodge: speed in m/s (0.4 s x 10 m/s = about 4 m).</summary>
    public float DodgeSpeed { get; init; } = 10f;

    /// <summary>Dodge: stamina cost in segments.</summary>
    public int DodgeStaminaSegments { get; init; } = 2;

    /// <summary>Ducking and standing up take this long, in seconds (smooth, no jerk).</summary>
    public float CrouchTransitionTime { get; init; } = 0.1f;
}

/// <summary>Balancing values of the silver blade.</summary>
public sealed record BladeBalance
{
    /// <summary>Seconds before the next swing.</summary>
    public float Cooldown { get; init; } = 0.4f;

    /// <summary>Damage per hit.</summary>
    public int Damage { get; init; } = 40;

    /// <summary>Seconds of holding the button until the blade is fully charged.</summary>
    public float ChargeTime { get; init; } = 1f;

    /// <summary>Damage multiplier when fully charged (2 = 200 %), linear from 1 when not charged.</summary>
    public float ChargeMaxMultiplier { get; init; } = 2f;

    /// <summary>Stamina segments a full charge costs (spent linearly while charging).</summary>
    public int ChargeStaminaSegments { get; init; } = 1;

    /// <summary>How far it reaches from the eyes, in metres (to the surface of the target).</summary>
    public float Range { get; init; } = 2f;

    /// <summary>Full width of the cone in degrees (90 = 45 to each side, also up and down).</summary>
    public float Angle { get; init; } = 90f;

    /// <summary>
    /// The tip of the cone lies this far behind the eyes, in metres - so right in front of you the cone is already
    /// about a metre wide and close enemies at the side are hit. The reach still counts from the eyes.
    /// </summary>
    public float ConeStartBehind { get; init; } = 0.6f;
}

/// <summary>Balancing values of the pistol.</summary>
public sealed record PistolBalance
{
    /// <summary>Damage per hit. A bit less than the blade, but with its slow rate every shot must count.</summary>
    public int Damage { get; init; } = 35;

    /// <summary>Damage multiplier for a hit on the head (critical hit), e.g. 3 = 300 %.</summary>
    public float HeadshotMultiplier { get; init; } = 3f;

    /// <summary>How far a shot reaches, in metres.</summary>
    public float Range { get; init; } = 50f;

    /// <summary>Seconds before the next shot.</summary>
    public float Cooldown { get; init; } = 0.8f;
}

/// <summary>Balancing values of the training dummy.</summary>
public sealed record DummyBalance
{
    /// <summary>Health (and the most it can be).</summary>
    public int MaxHealth { get; init; } = 100;

    /// <summary>Seconds it lies on the ground before it stands up again.</summary>
    public float DownTime { get; init; } = 3f;
}

/// <summary>Balancing values of the ghoul.</summary>
public sealed record GhoulBalance
{
    /// <summary>Health (2 blade hits, 1 pistol headshot, 3 pistol body shots).</summary>
    public int MaxHealth { get; init; } = 80;

    /// <summary>Seconds the dead body lies on the ground before it disappears.</summary>
    public float CorpseTime { get; init; } = 2f;

    /// <summary>It notices a player it can see who is closer than this, in metres.</summary>
    public float SightRange { get; init; } = 12f;

    /// <summary>Crawling speed in m/s - three quarters of the player's 6 m/s: you can get away, but not by strolling.</summary>
    public float Speed { get; init; } = 4.5f;

    /// <summary>It starts its blow when a player is this close (centre to centre, flat), in metres.</summary>
    public float AttackReach { get; init; } = 1.6f;

    /// <summary>
    /// The blow hits a player up to this far away (centre to centre, flat) - farther than <see cref="AttackReach"/>,
    /// so one short step back is not enough; you have to really get away or dodge.
    /// </summary>
    public float HitReach { get; init; } = 2.4f;

    /// <summary>Seconds of drawing back before the blow - the time to step away or dodge.</summary>
    public float WindUpTime { get; init; } = 0.5f;

    /// <summary>The blow only hits a player at most this many degrees to the side of where it faced at the start.</summary>
    public float AttackHalfAngle { get; init; } = 60f;

    /// <summary>Damage of a blow.</summary>
    public int AttackDamage { get; init; } = 20;

    /// <summary>Seconds of rest after a blow.</summary>
    public float RecoverTime { get; init; } = 0.8f;

    /// <summary>The way to the target is searched again at most this often, in seconds.</summary>
    public float RepathTime { get; init; } = 0.5f;
}

/// <summary>Balancing values of the vampire bat.</summary>
public sealed record BatBalance
{
    /// <summary>Health: one blade hit or one pistol shot.</summary>
    public int MaxHealth { get; init; } = 30;

    /// <summary>Seconds the dead body lies on the floor before it disappears (counted from its death).</summary>
    public float CorpseTime { get; init; } = 2.5f;

    /// <summary>It notices a player it can see who is closer than this, in metres.</summary>
    public float SightRange { get; init; } = 15f;

    /// <summary>Smallest distance (flat) at which a bat circles the player, in metres. Each bat gets its own.</summary>
    public float CircleRadiusMin { get; init; } = 4f;

    /// <summary>Largest distance (flat) at which a bat circles the player, in metres.</summary>
    public float CircleRadiusMax { get; init; } = 6f;

    /// <summary>While circling it turns round after a random time between this ...</summary>
    public float TurnRoundMin { get; init; } = 2f;

    /// <summary>... and this, in seconds.</summary>
    public float TurnRoundMax { get; init; } = 4f;

    /// <summary>Speed along the circle in m/s - slow enough to follow it with your eyes.</summary>
    public float CircleSpeed { get; init; } = 3f;

    /// <summary>Highest flying speed in m/s (getting to the circle, following a path).</summary>
    public float FlySpeed { get; init; } = 6f;

    /// <summary>Speed up and down in m/s (rising, sinking, bobbing).</summary>
    public float ClimbSpeed { get; init; } = 3f;

    /// <summary>Lowest height above the floor while circling (bottom of its body), in metres.</summary>
    public float CircleHeightMin { get; init; } = 2f;

    /// <summary>Highest height above the floor while circling (bottom of its body), in metres.</summary>
    public float CircleHeightMax { get; init; } = 2.8f;

    /// <summary>Seconds for one rise and sink while circling.</summary>
    public float BobTime { get; init; } = 2f;

    /// <summary>Seconds between two spits.</summary>
    public float SpitInterval { get; init; } = 2f;

    /// <summary>Seconds it glows before it spits - the warning.</summary>
    public float SpitWindUpTime { get; init; } = 0.4f;

    /// <summary>It aims this far below the player's eyes, in metres: a ducked (dodging) player is under the bolt.</summary>
    public float SpitAimBelowEyes { get; init; } = 0.2f;

    /// <summary>Seconds between two dives (counted while circling).</summary>
    public float DiveInterval { get; init; } = 6f;

    /// <summary>Seconds of screeching before a dive - the warning; stepping aside now makes it miss.</summary>
    public float ScreechTime { get; init; } = 0.5f;

    /// <summary>Diving speed in m/s.</summary>
    public float DiveSpeed { get; init; } = 10f;

    /// <summary>Damage of its bite at the end of a dive.</summary>
    public int BiteDamage { get; init; } = 15;

    /// <summary>The way to its target is searched again at most this often, in seconds.</summary>
    public float RepathTime { get; init; } = 0.5f;
}

/// <summary>Balancing values of the bat's blood bolt.</summary>
public sealed record BloodBoltBalance
{
    /// <summary>Speed in m/s - fast, but you see it coming.</summary>
    public float Speed { get; init; } = 12f;

    /// <summary>Damage to the player.</summary>
    public int Damage { get; init; } = 15;

    /// <summary>Radius of the ball in metres.</summary>
    public float Radius { get; init; } = 0.15f;

    /// <summary>Seconds until it disappears if it hits nothing.</summary>
    public float LifeTime { get; init; } = 5f;
}
