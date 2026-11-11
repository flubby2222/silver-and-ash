using System.Numerics;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>The player character: where it stands, how it moves and where it looks.</summary>
public sealed class Player
{
    private readonly Level _level;
    private readonly Func<IEnumerable<CylinderBody>> _bodies;

    /// <summary>
    /// Creates the player standing on the start cell of <paramref name="level"/>. <paramref name="bodies"/> gives the
    /// bodies the player cannot walk through (training dummies, later enemies); asked anew every tick.
    /// </summary>
    public Player(Level level, Func<IEnumerable<CylinderBody>>? bodies = null)
    {
        ArgumentNullException.ThrowIfNull(level);
        _level = level;
        _bodies = bodies ?? (() => []);
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

    /// <summary>Position of the eyes after the latest tick (lower while ducked).</summary>
    public Vector3 EyePosition => Position + new Vector3(0f, Crouch.EyeHeight(Crouch.Amount), 0f);

    /// <summary>How far the player is ducked (for the camera).</summary>
    public Crouch Crouch { get; } = new();

    /// <summary>
    /// Height of the body for collisions (ceiling now, enemies and projectiles later): smaller while dodging.
    /// </summary>
    public float CollisionHeight => IsDodging ? Tuning.Player.DodgeHeight : Tuning.Player.Height;

    /// <summary>
    /// Applies the look part of the input. Called once per drawn frame (not per tick),
    /// so turning the view feels immediate even at high frame rates.
    /// </summary>
    public void Look(PlayerInput input) => View.ApplyMouse(input.Look);

    /// <summary>True while the feet are on the ground. Jumping is only possible from here.</summary>
    public bool IsGrounded { get; private set; } = true;

    /// <summary>
    /// True while the speed bonus of a perfect jump is active: from a perfectly timed jump
    /// until the chain ends (see <see cref="Tick"/>).
    /// </summary>
    public bool HasPerfectJumpBonus { get; private set; }

    /// <summary>
    /// True after a timed jump with an empty stamina bar: slower than normal until the chain ends.
    /// </summary>
    public bool IsExhausted { get; private set; }

    /// <summary>Pays for jumps and dashes.</summary>
    public Stamina Stamina { get; } = new();

    /// <summary>Health of the player. Nothing does damage yet - enemies come in v0.5.</summary>
    public int Health { get; } = Tuning.Combat.PlayerMaxHealth;

    /// <summary>Waiting time of the silver blade.</summary>
    public Cooldown BladeCooldown { get; } = new(Tuning.Combat.BladeCooldown);

    /// <summary>Waiting time of the pistol.</summary>
    public Cooldown PistolCooldown { get; } = new(Tuning.Combat.PistolCooldown);

    /// <summary>
    /// What happened in the latest tick (swings, shots, ...), for the presentation.
    /// Emptied at the start of every tick - read it after each tick.
    /// </summary>
    public IReadOnlyList<GameEvent> Events => _events;

    private readonly List<GameEvent> _events = [];

    /// <summary>True during a dash: fast, flat, no gravity, no jumping.</summary>
    public bool IsDashing => _dashTicksLeft > 0;

    // Dash speed chosen so that the dash covers exactly DashDistance in its whole number of ticks.
    private static readonly int DashTicks = SimulationClock.SecondsToTicks(Tuning.Player.DashDuration);
    private static readonly float DashSpeed = Tuning.Player.DashDistance / (DashTicks * SimulationClock.SecondsPerTick);
    private int _dashTicksLeft;
    private Vector3 _dashDirection;
    private bool _airDashUsed;

    // Jump pressed at the end of a dash: kept until the landing, however long the fall after the dash takes.
    private bool _jumpPressedAtDashEnd;

    /// <summary>
    /// True during a dodge: ducked, fast, fully steerable, no refilling. No jumping either - except a jump
    /// pressed at its end, which comes as a perfect jump when the dodge is over.
    /// </summary>
    public bool IsDodging => _dodgeTicksLeft > 0;

    private static readonly int DodgeTicks = SimulationClock.SecondsToTicks(Tuning.Player.DodgeDuration);
    private int _dodgeTicksLeft;
    private Vector3 _dodgeDirection;

    // Shift is one button for two moves: dash in the air, dodge on the ground. Needs a fresh press.
    private bool _evadeWasHeld;

    // Attacks need a fresh press, too: holding the button swings or shoots only once.
    private bool _meleeWasHeld;
    private bool _shootWasHeld;

    // How long the feet have been on the ground without a break.
    private int _ticksOnGround;

    // Was the jump button already held in the previous tick? A jump needs a fresh press,
    // so holding the button down does not make the player hop again and again.
    private bool _jumpWasHeld;

    // Perfect jump timing, counted in ticks. "Never" is a large number so the first jump is never perfect.
    private const int Never = int.MaxValue / 2;
    private static readonly int PerfectWindowTicks = SimulationClock.SecondsToTicks(Tuning.Player.PerfectJumpWindow);
    private int _ticksSinceLanding = Never;
    private int _ticksSinceJumpPressedInAir = Never;
    private int _ticksSinceDodgeEnded = Never;
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
        _events.Clear();
        BladeCooldown.Tick();
        PistolCooldown.Tick();
        LandingDip.Tick();
        var wasGrounded = IsGrounded;
        _ticksSinceLanding = Math.Min(_ticksSinceLanding + 1, Never);
        _ticksSinceJumpPressedInAir = Math.Min(_ticksSinceJumpPressedInAir + 1, Never);
        _ticksSinceDodgeEnded = Math.Min(_ticksSinceDodgeEnded + 1, Never);

        // A chain of jumps ends when the player stays on the ground longer than the perfect window:
        // bonus and exhaustion are over. It ends on the ground, not at the moment of landing - a timed press
        // shortly after landing still belongs to the chain, and so does brushing the edge of a platform for a
        // few ticks while still rising (code review finding). The bar refills only while standing like this.
        var chainOver = IsGrounded && _ticksOnGround > PerfectWindowTicks;
        if (chainOver)
        {
            HasPerfectJumpBonus = false;
            IsExhausted = false;
        }

        // No refilling during a dodge either - otherwise standing on the ground would pay part of it back.
        Stamina.SetRecharging(chainOver && !IsDodging);
        Stamina.Tick();

        // Horizontal: accelerate towards the wished velocity. In the air: less steering strength,
        // but a slightly higher top speed - more with the perfect jump bonus, less when exhausted.
        var acceleration = Tuning.Player.Acceleration * (IsGrounded ? 1f : Tuning.Player.AirControl);
        var targetVelocity = WishDirection(input) * TopSpeed(IsGrounded);
        var horizontal = MoveTowards(new Vector3(Velocity.X, 0f, Velocity.Z), targetVelocity, acceleration * Dt);

        // Vertical: jump on a fresh press while standing. Gravity is applied in MoveVertically.
        var vertical = Velocity.Y;
        var jumpPressed = input.Jump && !_jumpWasHeld;
        _jumpWasHeld = input.Jump;

        if (jumpPressed && !IsGrounded && !IsDashing && !IsDodging)
        {
            // Pressed in the air: remember when - if we land soon, the jump happens then ("buffered").
            _ticksSinceJumpPressedInAir = 0;
        }

        if (jumpPressed && IsDashing && _dashTicksLeft <= PerfectWindowTicks)
        {
            // Pressed at the end of a dash: a perfect jump on landing. Earlier in the dash the press is ignored -
            // otherwise a dash that lands on a low step would fire a late jump (code review finding).
            // Already on the ground (the dash landed on a step): no landing will follow, so the jump comes
            // right after the dash instead of waiting for some much later landing.
            if (IsGrounded)
            {
                _perfectJumpQueued = true;
            }
            else
            {
                _jumpPressedAtDashEnd = true;
            }
        }

        if (jumpPressed && IsDodging && _dodgeTicksLeft <= PerfectWindowTicks)
        {
            // Pressed at the end of a dodge: jump as soon as it is over, as a timed jump - the same rhythm
            // as pressing shortly before landing. Pressed earlier in the dodge, the press is simply ignored.
            _perfectJumpQueued = true;
        }

        // Shift on a fresh press: dodge on the ground, dash in the air. Both override walking and jumping.
        var evadePressed = input.Dash && !_evadeWasHeld;
        _evadeWasHeld = input.Dash;
        if (evadePressed)
        {
            if (IsGrounded)
            {
                TryStartDodge(input);
            }
            else
            {
                TryStartDash(input);
            }
        }

        if (IsDashing)
        {
            horizontal = _dashDirection * DashSpeed;
            vertical = 0f;
        }

        if (IsDodging)
        {
            // Fully steerable: the dodge always goes where the player walks. Without keys it keeps its last direction.
            var wish = WishDirection(input);
            if (wish.LengthSquared() > 0f)
            {
                _dodgeDirection = Vector3.Normalize(wish);
            }

            horizontal = _dodgeDirection * Tuning.Player.DodgeSpeed;
        }

        var jumpNow = !IsDashing && !IsDodging && IsGrounded && (jumpPressed || _perfectJumpQueued);
        if (jumpNow)
        {
            // Timed: pressed shortly before landing or at the end of a dodge (queued),
            // or shortly after landing or after a dodge.
            var wasQueued = _perfectJumpQueued;
            var afterDodge = _ticksSinceDodgeEnded <= PerfectWindowTicks;
            var timed = wasQueued || afterDodge || _ticksSinceLanding <= PerfectWindowTicks;
            _perfectJumpQueued = false;
            vertical = Tuning.Player.JumpSpeed;

            // Every jump costs stamina. Timed and paid = perfect (bonus). Not paid = exhausted (slower).
            // A normal, paid jump changes nothing about bonus or exhaustion.
            var factorBefore = SpeedFactor;
            var paid = Stamina.TryPay();
            if (!paid)
            {
                HasPerfectJumpBonus = false;
                IsExhausted = true;
            }
            else if (timed)
            {
                HasPerfectJumpBonus = true;
                IsExhausted = false;
            }

            // Noticeable right away: the current speed changes with the factor, too. Same factor
            // as before (perfect after perfect) = no change, so the bonus does not stack.
            horizontal *= SpeedFactor / factorBefore;

            // A queued jump already softened the dip when it landed - softening twice would make it 0.4 x 0.4.
            if (timed && paid && !wasQueued)
            {
                LandingDip.Soften(Tuning.Player.PerfectJumpDipFactor);
            }
        }

        Velocity = horizontal with { Y = vertical };

        // Walls first (sideways), then gravity, floor and ceiling (up and down).
        (Position, Velocity) = CharacterCollision.Move(
            _level, Position, Velocity, Tuning.Player.Radius, Dt, CollisionHeight, bodies: _bodies().ToList());

        // Falling speed at the end of this tick - needed for the landing dip, because landing sets it to 0.
        var fallSpeed = -(Velocity.Y - Tuning.Player.Gravity * Dt);

        var airTicksBeforeLanding = _ticksInAir;
        var gravity = IsDashing ? 0f : Tuning.Player.Gravity;
        (Position, Velocity, var grounded) = CharacterCollision.MoveVertically(
            _level, Position, Velocity, Tuning.Player.Radius, CollisionHeight, gravity, Dt);
        IsGrounded = grounded;
        _ticksInAir = grounded ? 0 : _ticksInAir + 1;
        _ticksOnGround = grounded ? _ticksOnGround + 1 : 0;

        // Back on the ground (and not in the middle of a dash or dodge): the air dash is available again.
        // Not during them - touching a low step mid-dash must not hand out a second one.
        if (grounded && !IsDashing && !IsDodging)
        {
            _airDashUsed = false;
        }

        if (IsDashing)
        {
            _dashTicksLeft--;
            if (!IsDashing)
            {
                FallBackToTopSpeed();
            }
        }

        if (IsDodging)
        {
            _dodgeTicksLeft--;
            if (!IsDodging)
            {
                FallBackToTopSpeed();

                // The window for a perfect jump out of the dodge - only on the ground. A dodge that ended in the
                // air (off a ledge) must not keep a queued jump for a much later landing.
                if (grounded)
                {
                    _ticksSinceDodgeEnded = 0;

                    // The dodge used up the air dash (for dodges off a ledge). It ended on the ground, so the
                    // next stay in the air gets its own - a jump straight out of the dodge never touches the
                    // ground long enough to get it back otherwise (found while playing).
                    _airDashUsed = false;
                }
                else
                {
                    _perfectJumpQueued = false;
                }
            }
        }

        Crouch.Tick(IsDodging);

        if (!wasGrounded && grounded)
        {
            LandingDip.Land(fallSpeed);

            // Only real drops open the perfect window (see MinimumAirTicksForLanding).
            var realLanding = airTicksBeforeLanding >= MinimumAirTicksForLanding;
            if (realLanding)
            {
                _ticksSinceLanding = 0;
            }

            var pressedAtDashEnd = _jumpPressedAtDashEnd;
            _jumpPressedAtDashEnd = false;
            if ((realLanding && _ticksSinceJumpPressedInAir <= PerfectWindowTicks) || pressedAtDashEnd)
            {
                // Pressed just before landing (or at the end of the dash): jump again right away in the next tick
                // (after a dash that landed on a step: when the dash is over), as a timed jump.
                // Only a jump the stamina can pay for lands softly.
                _perfectJumpQueued = true;
                _ticksSinceJumpPressedInAir = Never;
                if (Stamina.CanPay)
                {
                    LandingDip.Soften(Tuning.Player.PerfectJumpDipFactor);
                }
            }
        }

        Attack(input);
    }

    /// <summary>
    /// Blade and pistol: each needs a fresh press and a finished cooldown. They work in every movement
    /// (jumping, dash, dodge) and cost no stamina. Done after moving, so the events start at the eyes'
    /// position of this tick.
    /// </summary>
    private void Attack(PlayerInput input)
    {
        var meleePressed = input.Melee && !_meleeWasHeld;
        var shootPressed = input.Shoot && !_shootWasHeld;
        _meleeWasHeld = input.Melee;
        _shootWasHeld = input.Shoot;

        if (meleePressed && BladeCooldown.TryUse())
        {
            _events.Add(new BladeSwung(EyePosition, View.Forward));
        }

        if (shootPressed && PistolCooldown.TryUse())
        {
            _events.Add(new ShotFired(EyePosition, View.Forward));
        }
    }

    /// <summary>
    /// Starts a dash if allowed: only in the air (on the ground the bar refills - dashing there could be
    /// repeated again and again, found while playing), once per stay in the air, not already dashing, and the
    /// stamina pays one segment (empty bar = no dash). Direction: where the player walks, or forward if not walking.
    /// </summary>
    private void TryStartDash(PlayerInput input)
    {
        var allowed = !IsDashing && !IsDodging && !IsGrounded && !_airDashUsed;
        if (!allowed || !Stamina.TryPay())
        {
            return;
        }

        var wish = WishDirection(input);
        _dashDirection = wish.LengthSquared() > 0f ? Vector3.Normalize(wish) : View.FlatForward;
        _dashTicksLeft = DashTicks;
        _perfectJumpQueued = false;
        _ticksSinceJumpPressedInAir = Never;

        _airDashUsed = true;
    }

    /// <summary>
    /// Starts a dodge if allowed: on the ground, not already dodging, and the stamina pays
    /// <see cref="Tuning.Player.DodgeStaminaSegments"/> segments. Direction: where the player walks, or forward.
    /// </summary>
    private void TryStartDodge(PlayerInput input)
    {
        if (IsDodging || !Stamina.TryPay(Tuning.Player.DodgeStaminaSegments))
        {
            return;
        }

        var wish = WishDirection(input);
        _dodgeDirection = wish.LengthSquared() > 0f ? Vector3.Normalize(wish) : View.FlatForward;
        _dodgeTicksLeft = DodgeTicks;
        _perfectJumpQueued = false;

        // If the dodge runs off a ledge, that stay in the air already had its move - no dash on top
        // (code review finding: dodge + dash = about 8 m of evading).
        _airDashUsed = true;
    }

    /// <summary>After a dash or dodge: the speed falls back to the normal top speed (if it is above it).</summary>
    private void FallBackToTopSpeed()
    {
        var horizontal = new Vector3(Velocity.X, 0f, Velocity.Z);
        var topSpeed = TopSpeed(IsGrounded);
        if (horizontal.Length() > topSpeed)
        {
            Velocity = (Vector3.Normalize(horizontal) * topSpeed) with { Y = Velocity.Y };
        }
    }

    /// <summary>
    /// Top speed on the ground or in the air. In the air a little faster (air speed multiplier) -
    /// except when exhausted: then the penalty applies to the walking speed (6 m/s x 0.8 = 4.8 m/s),
    /// otherwise air boost and penalty would almost cancel out (6.9 x 0.85 = 5.87, found while playing).
    /// </summary>
    private float TopSpeed(bool grounded)
    {
        var air = grounded || IsExhausted ? 1f : Tuning.Player.AirSpeedMultiplier;
        return Tuning.Player.MaxSpeed * air * SpeedFactor;
    }

    // 1 normally, e.g. 1.2 with the perfect jump bonus or 0.8 when exhausted.
    private float SpeedFactor =>
        HasPerfectJumpBonus ? 1f + Tuning.Player.PerfectJumpSpeedBonus
        : IsExhausted ? 1f - Tuning.Player.ExhaustedJumpSpeedPenalty
        : 1f;

    /// <summary>
    /// Eye position for drawing, blended between the previous and the latest tick, lowered by the landing dip.
    /// <paramref name="alpha"/> 0 = previous tick, 1 = latest tick.
    /// </summary>
    public Vector3 InterpolatedEyePosition(float alpha) =>
        Vector3.Lerp(PreviousPosition, Position, Math.Clamp(alpha, 0f, 1f))
        + new Vector3(0f, Crouch.EyeHeight(Crouch.Interpolated(alpha)) - LandingDip.Interpolated(alpha), 0f);

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
