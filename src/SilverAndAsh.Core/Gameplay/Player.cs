using System.Numerics;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>The player character: where it stands, how it moves and where it looks.</summary>
public sealed class Player
{
    private readonly Level _level;

    /// <summary>Creates the player standing on the start cell of <paramref name="level"/>.</summary>
    public Player(Level level)
    {
        ArgumentNullException.ThrowIfNull(level);
        _level = level;
        Position = level.PlayerStartPosition;
        PreviousPosition = Position;
    }

    /// <summary>Position of the feet in world coordinates (metres) after the latest tick.</summary>
    public Vector3 Position { get; private set; }

    /// <summary>Position of the feet one tick earlier. Needed for smooth drawing between ticks.</summary>
    public Vector3 PreviousPosition { get; private set; }

    /// <summary>Current movement in metres per second.</summary>
    public Vector3 Velocity { get; private set; }

    /// <summary>Speed over the ground in metres per second (without up and down).</summary>
    public float HorizontalSpeed => new Vector2(Velocity.X, Velocity.Z).Length();

    /// <summary>Where the player is looking.</summary>
    public FirstPersonView View { get; } = new();

    /// <summary>The camera "nod" after landing.</summary>
    public LandingDip LandingDip { get; } = new();

    /// <summary>Position of the eyes after the latest tick.</summary>
    public Vector3 EyePosition => Position + EyeOffset;

    private static Vector3 EyeOffset => new(0f, Tuning.Player.EyeHeight, 0f);

    /// <summary>
    /// Applies the look part of the input. Called once per drawn frame (not per tick),
    /// so turning the view feels immediate even at high frame rates.
    /// </summary>
    public void Look(PlayerInput input) => View.ApplyMouse(input.Look);

    /// <summary>True while the feet are on the ground. Jumping is only possible from here.</summary>
    public bool IsGrounded { get; private set; } = true;

    /// <summary>
    /// True while the speed bonus of a perfect jump is active: from a perfectly timed jump
    /// until the next landing that is NOT followed by another perfect jump.
    /// </summary>
    public bool HasPerfectJumpBonus { get; private set; }

    // Was the jump button already held in the previous tick? A jump needs a fresh press,
    // so holding the button down does not make the player hop again and again.
    private bool _jumpWasHeld;

    // Perfect jump timing, counted in ticks. "Never" is a large number so the first jump is never perfect.
    private const int Never = int.MaxValue / 2;
    private static readonly int PerfectWindowTicks = SimulationClock.SecondsToTicks(Tuning.Player.PerfectJumpWindow);
    private int _ticksSinceLanding = Never;
    private int _ticksSinceJumpPressedInAir = Never;
    private int _ticksInAir;
    private bool _perfectJumpQueued;

    // A drop must last at least this long to count as a "landing" for the perfect window -
    // stepping down a small step is not a landing (code review finding). 0.25 s rules out drops up to
    // about 0.5 m; a real jump (0.8 s) or falling into a pit still counts.
    private static readonly int MinimumAirTicksForLanding = SimulationClock.SecondsToTicks(0.25f);

    /// <summary>
    /// Advances the player by one simulation tick: walking (with less control in the air), jumping,
    /// perfect jumps, gravity, walls, floor and ceiling.
    /// </summary>
    public void Tick(PlayerInput input)
    {
        const float Dt = SimulationClock.SecondsPerTick;
        PreviousPosition = Position;
        LandingDip.Tick();
        var wasGrounded = IsGrounded;
        _ticksSinceLanding = Math.Min(_ticksSinceLanding + 1, Never);
        _ticksSinceJumpPressedInAir = Math.Min(_ticksSinceJumpPressedInAir + 1, Never);

        // Horizontal: accelerate towards the wished velocity. In the air: less steering strength,
        // but a slightly higher top speed - and even more with the perfect jump bonus.
        var acceleration = Tuning.Player.Acceleration * (IsGrounded ? 1f : Tuning.Player.AirControl);
        var topSpeed = Tuning.Player.MaxSpeed * (IsGrounded ? 1f : Tuning.Player.AirSpeedMultiplier) * BonusFactor;
        var targetVelocity = WishDirection(input) * topSpeed;
        var horizontal = MoveTowards(new Vector3(Velocity.X, 0f, Velocity.Z), targetVelocity, acceleration * Dt);

        // Vertical: jump on a fresh press while standing. Gravity is applied in MoveVertically.
        var vertical = Velocity.Y;
        var jumpPressed = input.Jump && !_jumpWasHeld;
        _jumpWasHeld = input.Jump;

        if (jumpPressed && !IsGrounded)
        {
            // Pressed in the air: remember when - if we land soon, the jump happens then ("buffered").
            _ticksSinceJumpPressedInAir = 0;
        }

        var jumpNow = IsGrounded && (jumpPressed || _perfectJumpQueued);
        if (jumpNow)
        {
            // Perfect: either pressed shortly before landing (queued) or shortly after landing.
            var wasQueued = _perfectJumpQueued;
            var perfect = wasQueued || _ticksSinceLanding <= PerfectWindowTicks;
            _perfectJumpQueued = false;
            vertical = Tuning.Player.JumpSpeed;

            if (perfect)
            {
                if (!HasPerfectJumpBonus)
                {
                    // Noticeable right away: the current speed gets the bonus, too (it does not stack).
                    horizontal *= 1f + Tuning.Player.PerfectJumpSpeedBonus;
                }

                HasPerfectJumpBonus = true;

                // A queued jump already softened the dip when it landed - softening twice would make it 0.4 x 0.4.
                if (!wasQueued)
                {
                    LandingDip.Soften(Tuning.Player.PerfectJumpDipFactor);
                }
            }
        }

        Velocity = horizontal with { Y = vertical };

        // Walls first (sideways), then gravity, floor and ceiling (up and down).
        (Position, Velocity) = CharacterCollision.Move(_level, Position, Velocity, Tuning.Player.Radius, Dt);

        // Falling speed at the end of this tick - needed for the landing dip, because landing sets it to 0.
        var fallSpeed = -(Velocity.Y - Tuning.Player.Gravity * Dt);

        var airTicksBeforeLanding = _ticksInAir;
        (Position, Velocity, var grounded) = CharacterCollision.MoveVertically(
            _level, Position, Velocity, Tuning.Player.Radius, Tuning.Player.Height, Tuning.Player.Gravity, Dt);
        IsGrounded = grounded;
        _ticksInAir = grounded ? 0 : _ticksInAir + 1;

        if (!wasGrounded && grounded)
        {
            LandingDip.Land(fallSpeed);

            // Only real drops open the perfect window (see MinimumAirTicksForLanding).
            var realLanding = airTicksBeforeLanding >= MinimumAirTicksForLanding;
            if (realLanding)
            {
                _ticksSinceLanding = 0;
            }

            if (realLanding && _ticksSinceJumpPressedInAir <= PerfectWindowTicks)
            {
                // Pressed just before landing: jump again right away in the next tick, as a perfect jump.
                _perfectJumpQueued = true;
                _ticksSinceJumpPressedInAir = Never;
                LandingDip.Soften(Tuning.Player.PerfectJumpDipFactor);
            }
            else
            {
                // A normal landing ends the bonus - a perfect press within the window after it brings it back.
                HasPerfectJumpBonus = false;
            }
        }
    }

    // 1 normally, e.g. 1.2 with the perfect jump bonus.
    private float BonusFactor => HasPerfectJumpBonus ? 1f + Tuning.Player.PerfectJumpSpeedBonus : 1f;

    /// <summary>
    /// Eye position for drawing, blended between the previous and the latest tick, lowered by the landing dip.
    /// <paramref name="alpha"/> 0 = previous tick, 1 = latest tick.
    /// </summary>
    public Vector3 InterpolatedEyePosition(float alpha) =>
        Vector3.Lerp(PreviousPosition, Position, Math.Clamp(alpha, 0f, 1f))
        + EyeOffset
        - new Vector3(0f, LandingDip.Interpolated(alpha), 0f);

    /// <summary>How far the view tilts down because of the landing dip, in radians (for drawing).</summary>
    public float InterpolatedViewTilt(float alpha) => LandingDip.Interpolated(alpha) * Tuning.Player.LandingTiltPerMetre;

    /// <summary>Desired walking direction on the ground, relative to the view. Length 1 at most.</summary>
    private Vector3 WishDirection(PlayerInput input)
    {
        var direction = View.FlatForward * input.Move.Y + View.FlatRight * input.Move.X;

        // Without this, walking diagonally (W + D) would be about 41 % faster than straight.
        return direction.LengthSquared() > 1f ? Vector3.Normalize(direction) : direction;
    }

    /// <summary>Changes <paramref name="current"/> towards <paramref name="target"/> by at most <paramref name="maxChange"/>.</summary>
    private static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxChange)
    {
        var difference = target - current;
        var distance = difference.Length();
        return distance <= maxChange ? target : current + difference / distance * maxChange;
    }
}
