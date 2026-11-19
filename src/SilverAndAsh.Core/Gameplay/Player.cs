using System.Numerics;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>The player character: where it stands, how it moves and where it looks.</summary>
public sealed partial class Player
{
    private Level _level;
    private readonly Func<IEnumerable<CylinderBody>> _bodies;
    private readonly Func<IEnumerable<BoxBody>> _boxes;
    private readonly PlayerBalance _player;

    /// <summary>
    /// Creates the player standing on the start cell of <paramref name="level"/>. <paramref name="bodies"/> gives the
    /// bodies the player cannot walk through (training dummies, enemies), <paramref name="boxes"/> the solid boxes
    /// (chests) they cannot walk through but can jump onto; both asked anew every tick.
    /// <paramref name="balance"/> sets health, speeds, stamina and weapons (the standard values if none is given).
    /// <paramref name="random"/> is the world's random generator - it draws the boons a level up offers (online co-op
    /// rule 3). Without one (a player alone in a test) it uses one started from 0.
    /// </summary>
    public Player(
        Level level, Func<IEnumerable<CylinderBody>>? bodies = null, Balance? balance = null, Random? random = null,
        Func<IEnumerable<BoxBody>>? boxes = null)
    {
        ArgumentNullException.ThrowIfNull(level);
        balance ??= Balance.Default;
        _random = random ?? new Random(0);
        _level = level;
        _bodies = bodies ?? (() => []);
        _boxes = boxes ?? (() => []);
        _player = balance.Player;
        _experience = balance.Experience;
        _boons = balance.Boons;
        _consecratedFireRate = balance.ConsecratedAmmo.FireRateMultiplier;
        Position = level.PlayerStartPosition;
        PreviousPosition = Position;

        Health = _player.MaxHealth;
        LandingDip = new LandingDip(_player);
        Crouch = new Crouch(_player);
        Stamina = new Stamina(_player);
        MeleeCooldown = new Cooldown(balance.Blade.Cooldown);
        MeleeCharge = new MeleeCharge(balance.Blade);

        // Every run starts with the silver blade and the pistol (decision record 0008).
        _balance = balance;
        _rangedSlots[0] = new RangedWeapon(WeaponKind.Pistol, balance);

        // Dash speed chosen so that the dash covers exactly DashDistance in its whole number of ticks.
        _dashTicks = SimulationClock.SecondsToTicks(_player.DashDuration);
        _dashSpeed = _player.DashDistance / (_dashTicks * SimulationClock.SecondsPerTick);
        _dodgeTicks = SimulationClock.SecondsToTicks(_player.DodgeDuration);
        _perfectWindowTicks = SimulationClock.SecondsToTicks(_player.PerfectJumpWindow);
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

    /// <summary>
    /// From now on the player moves in <paramref name="level"/> - the same level with some cells changed
    /// (a door opened). Called by the world.
    /// </summary>
    private int _pullTicksLeft;
    private Vector3 _pullVelocity;

    /// <summary>True while Gorm's chain pulls the player: no control over walking for that time.</summary>
    public bool IsPulled => _pullTicksLeft > 0;

    /// <summary>
    /// Pulls the player (flat, walls still stop them) to <paramref name="to"/> within <paramref name="seconds"/>,
    /// without control - Gorm's chain throw (decision record 0007).
    /// </summary>
    internal void PullTo(Vector3 to, float seconds)
    {
        _pullTicksLeft = Math.Max(1, SimulationClock.SecondsToTicks(seconds));
        var flat = (to - Position) with { Y = 0f };
        _pullVelocity = flat / (_pullTicksLeft * SimulationClock.SecondsPerTick);
    }

    /// <summary>
    /// Puts the player at rest with their feet on <paramref name="feet"/> - only for building photo scenes (issue #224),
    /// which is why it is internal: no game rule moves a player like this.
    /// </summary>
    internal void PlaceAt(Vector3 feet)
    {
        Position = feet;
        PreviousPosition = feet;
        Velocity = Vector3.Zero;
    }

    internal void ChangeLevel(Level level) => _level = level ?? throw new ArgumentNullException(nameof(level));

    /// <summary>The camera "nod" after landing.</summary>
    public LandingDip LandingDip { get; }

    /// <summary>Position of the eyes after the latest tick (lower while ducked).</summary>
    public Vector3 EyePosition => Position + new Vector3(0f, Crouch.EyeHeight(Crouch.Amount), 0f);

    /// <summary>How far the player is ducked (for the camera).</summary>
    public Crouch Crouch { get; }

    /// <summary>
    /// Height of the body for collisions (ceiling now, enemies and projectiles later): smaller while dodging.
    /// </summary>
    public float CollisionHeight => IsDodging ? BodySizes.Player.DodgeHeight : BodySizes.Player.Height;

    /// <summary>
    /// Applies the look part of the input. Called once per drawn frame (not per tick),
    /// so turning the view feels immediate even at high frame rates.
    /// </summary>
    /// <param name="input">The mouse movement of this frame.</param>
    /// <param name="sensitivity">The player's own factor from the settings (1 = standard, 2 = twice as fast).</param>
    public void Look(PlayerInput input, float sensitivity = 1f)
    {
        // The dead do not look around. A waiting bonus no longer holds the view (decision record 0008).
        if (!IsDead)
        {
            View.ApplyMouse(input.Look, ControlSettings.MouseSensitivity * sensitivity);
        }
    }

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
    public Stamina Stamina { get; }

    /// <summary>Number of this player (0 for now; with online co-op every player gets their own).</summary>
    public int Id { get; }

    /// <summary>Health of the player.</summary>
    public int Health { get; private set; }

    /// <summary>True while consecrated ammunition makes the pistol fire faster.</summary>
    public bool IsConsecrated => _consecratedTicksLeft > 0;

    /// <summary>Seconds of consecrated ammunition left (0 if none).</summary>
    public float ConsecratedSecondsLeft => _consecratedTicksLeft * SimulationClock.SecondsPerTick;

    /// <summary>
    /// Loads consecrated ammunition for <paramref name="seconds"/> - starting now. Any time left is replaced, not added:
    /// a second pickup restarts the time, it does not make the pistol any faster.
    /// </summary>
    public void Consecrate(float seconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);
        if (!IsDead)
        {
            _consecratedTicksLeft = SimulationClock.SecondsToTicks(seconds);
        }
    }

    private int _consecratedTicksLeft;
    private readonly float _consecratedFireRate;
    private readonly Balance _balance;
    private readonly RangedWeapon?[] _rangedSlots = new RangedWeapon?[RangedSlotCount];

    /// <summary>Silver collected in this run - 0 at the start, back to 0 after death (a new world).</summary>
    public int Silver { get; private set; }

    /// <summary>Adds <paramref name="amount"/> picked-up silver.</summary>
    internal void AddSilver(int amount) => Silver += amount;

    /// <summary>Keys in hand in this run - each fits every lock and is used up. 0 at the start and after death.</summary>
    public int Keys { get; private set; }

    /// <summary>Adds <paramref name="amount"/> picked-up keys.</summary>
    internal void AddKeys(int amount) => Keys += amount;

    /// <summary>Uses up one key (to unlock a door). There must be one.</summary>
    internal void UseKey()
    {
        if (Keys <= 0)
        {
            throw new InvalidOperationException("The player has no key to use.");
        }

        Keys--;
    }

    /// <summary>The player's level in this run - 0 at the start (issue #391), back to 0 after death (a new world).</summary>
    public int Level { get; private set; }

    /// <summary>Experience gathered towards the next level.</summary>
    public int Experience { get; private set; }

    /// <summary>
    /// Experience needed for the next level: the first level's amount, then <see cref="ExperienceBalance.Growth"/>
    /// times as much for each level (100, 125, 156, 195 ...), rounded.
    /// </summary>
    public int ExperienceToNextLevel =>
        (int)MathF.Round(_experience.FirstLevel * MathF.Pow(_experience.Growth, Level), MidpointRounding.AwayFromZero);

    /// <summary>
    /// Adds <paramref name="amount"/> experience - plus the <see cref="Boon.Experience"/> bonus, counted from
    /// <paramref name="amount"/> - and climbs as many levels as it pays for; what is left over counts towards the next.
    /// Every level up draws an offer of boons. Reports <see cref="ExperienceGained"/> and every <see cref="LevelUp"/>.
    /// The dead gain nothing.
    /// </summary>
    public void AddExperience(int amount, ICollection<GameEvent> events)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        ArgumentNullException.ThrowIfNull(events);
        if (IsDead || amount == 0)
        {
            return;
        }

        var gained = (int)MathF.Round(amount * ExperienceMultiplier, MidpointRounding.AwayFromZero);
        Experience += gained;
        events.Add(new ExperienceGained(Id, gained));

        // A level that needs nothing (values set to 0 in code) would be climbed forever - stop there.
        while (ExperienceToNextLevel > 0 && Experience >= ExperienceToNextLevel)
        {
            Experience -= ExperienceToNextLevel;
            Level++;
            // An empty offer (0 boons offered, set in code) would leave the world waiting forever - skip it.
            var offer = DrawOffer();
            if (offer.Count > 0)
            {
                _offers.Enqueue(offer);
            }

            events.Add(new LevelUp(Id, Level));
        }
    }

    private readonly ExperienceBalance _experience;

    /// <summary>Every kill's experience is multiplied by this (1 = base; every Experience boon adds a bit, counted from the base).</summary>
    public float ExperienceMultiplier => 1f + (_boons.Experience * BoonCount(Boon.Experience));

    /// <summary>Level ups whose bonus has not been chosen yet. While above 0, the world waits.</summary>
    public int PendingBoons => _offers.Count;

    /// <summary>The boons offered for the next waiting level up (empty if none is waiting).</summary>
    public IReadOnlyList<Boon> CurrentOffer => _offers.Count > 0 ? _offers.Peek() : [];

    /// <summary>How often <paramref name="boon"/> was chosen in this run.</summary>
    public int BoonCount(Boon boon) => _boonCounts.GetValueOrDefault(boon);

    /// <summary>
    /// Takes the waiting bonus: applies <paramref name="boon"/> and reports <see cref="BoonChosen"/>. Returns false
    /// (and does nothing) if no level up is waiting, <paramref name="boon"/> is not in its offer, or this player is
    /// dead - a dead player takes no boon, so "Health" never brings them back (the bonus choice stays open while hit,
    /// issue #346).
    /// </summary>
    public bool Choose(Boon boon, ICollection<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (IsDead || !CurrentOffer.Contains(boon))
        {
            return false;
        }

        _offers.Dequeue();
        _boonCounts[boon] = BoonCount(boon) + 1;
        switch (boon)
        {
            case Boon.Health:
                Health += _boons.Health;
                break;
            case Boon.StaminaRegen:
                Stamina.RegenMultiplier = 1f + _boons.StaminaRegen * BoonCount(Boon.StaminaRegen);
                break;
        }

        // Damage and experience work through their counts (the damage rule, AddExperience).
        events.Add(new BoonChosen(Id, boon));
        return true;
    }

    /// <summary>
    /// Draws <see cref="BoonBalance.Offered"/> different boons from the pool (every <see cref="Boon"/>) with the
    /// world's random generator - the same seed always offers the same.
    /// </summary>
    private List<Boon> DrawOffer()
    {
        var pool = Enum.GetValues<Boon>().ToList();
        var offer = new List<Boon>();
        while (offer.Count < _boons.Offered && pool.Count > 0)
        {
            var index = _random.Next(pool.Count);
            offer.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return offer;
    }

    private readonly BoonBalance _boons;
    private readonly Random _random;
    private readonly Queue<List<Boon>> _offers = new();
    private readonly Dictionary<Boon, int> _boonCounts = [];
    private int BonusMaxHealth => BoonCount(Boon.Health) * _boons.Health;

    /// <summary>The most health the player can have.</summary>
    public int MaxHealth => _player.MaxHealth + BonusMaxHealth;

    /// <summary>Top walking speed on the ground in m/s (for drawing, e.g. how much the weapons bob).</summary>
    public float WalkSpeed => _player.MaxSpeed;

    /// <summary>True at 0 health: the player can no longer move, look around or attack.</summary>
    public bool IsDead => Health == 0;

    /// <summary>The player's body, for enemies that must not crawl through it.</summary>
    public CylinderBody Body => new(Position, BodySizes.Player.Radius, CollisionHeight);

    /// <summary>
    /// Gives back up to <paramref name="amount"/> health, never above <see cref="MaxHealth"/>, and reports it into
    /// <paramref name="events"/>. The dead are not healed. Returns how much was really healed.
    /// </summary>
    public int Heal(int amount, ICollection<GameEvent> events)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        ArgumentNullException.ThrowIfNull(events);
        var healed = IsDead ? 0 : Math.Min(amount, MaxHealth - Health);
        if (healed > 0)
        {
            Health += healed;
            events.Add(new PlayerHealed(Id, healed));
        }

        return healed;
    }

    /// <summary>Takes <paramref name="damage"/> (never below 0) and reports it into <paramref name="events"/>.</summary>
    public void TakeDamage(int damage, ICollection<GameEvent> events)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(damage);
        ArgumentNullException.ThrowIfNull(events);
        if (IsDead)
        {
            return;
        }

        var taken = Math.Min(damage, Health);
        Health -= taken;
        events.Add(new PlayerHit(Id, taken));
        if (IsDead)
        {
            events.Add(new PlayerDied(Id));
            _consecratedTicksLeft = 0;
        }
    }

    /// <summary>Waiting time of the melee weapon in the hand.</summary>
    public Cooldown MeleeCooldown { get; private set; }

    /// <summary>Most ranged weapons a player carries at once (decision record 0008).</summary>
    public const int RangedSlotCount = 3;

    /// <summary>The melee weapon in the left hand: the silver blade at the start, later e.g. the club.</summary>
    public WeaponKind Melee { get; private set; } = WeaponKind.SilverBlade;

    /// <summary>
    /// Takes melee weapon <paramref name="kind"/> into the left hand (ready, not charging) - there is only one. The
    /// inventory (#193) and loot use this; so do tests.
    /// </summary>
    public void EquipMelee(WeaponKind kind)
    {
        if (Weapons.Info(kind).Hand != WeaponHand.Melee)
        {
            throw new ArgumentException($"{kind} is no melee weapon.", nameof(kind));
        }

        var values = _balance.Melee(kind);
        Melee = kind;
        MeleeCooldown = new Cooldown(values.Cooldown);
        MeleeCharge = new MeleeCharge(values);
    }

    /// <summary>The ranged weapons carried: <see cref="RangedSlotCount"/> places, empty ones null.</summary>
    public IReadOnlyList<RangedWeapon?> RangedSlots => _rangedSlots;

    /// <summary>Which of <see cref="RangedSlots"/> is in the hand (0, 1 or 2).</summary>
    public int SelectedSlot { get; private set; }

    /// <summary>The ranged weapon in the hand - never null: there is always at least one.</summary>
    public RangedWeapon Ranged => _rangedSlots[SelectedSlot]!;

    /// <summary>
    /// The hint under the crosshair (issue #259): <see cref="AmmoHint.Reload"/> while the ranged weapon in the hand is
    /// empty and not being reloaded. The melee weapon needs no ammunition and never brings a hint.
    /// </summary>
    public AmmoHint AmmoHint =>
        !IsDead && Ranged.Magazine is { Rounds: 0, IsReloading: false } ? AmmoHint.Reload : AmmoHint.None;

    /// <summary>
    /// Puts <paramref name="kind"/> into ranged place <paramref name="slot"/> (ready and full), replacing what was there.
    /// The inventory (#193) and loot use this; so do tests.
    /// </summary>
    public void Equip(int slot, WeaponKind kind)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, RangedSlotCount);
        _rangedSlots[slot] = new RangedWeapon(kind, _balance);
    }

    /// <summary>Charging the melee weapon in the hand (hold the button, swing on release) - every melee weapon can.</summary>
    public MeleeCharge MeleeCharge { get; private set; }

    /// <summary>
    /// What happened in the latest tick (swings, shots, ...), for the presentation.
    /// Emptied at the start of every tick - read it after each tick.
    /// </summary>
    public IReadOnlyList<GameEvent> Events => _events;

    private readonly List<GameEvent> _events = [];

    /// <summary>True during a dash: fast, no gravity, no jumping - flat, or in the looking direction with W.</summary>
    public bool IsDashing => _dashTicksLeft > 0;

    // Dash and dodge timing, from the balance (see the constructor).
    private readonly int _dashTicks;
    private readonly float _dashSpeed;
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

    private readonly int _dodgeTicks;
    private int _dodgeTicksLeft;
    private Vector3 _dodgeDirection;

    // Shift is one button for two moves: dash in the air, dodge on the ground. Needs a fresh press.
    private bool _evadeWasHeld;

    // Attacks need a fresh press, too: holding the button swings or shoots only once.
    private bool _meleeWasHeld;
    private bool _shootWasHeld;
    private bool _reloadWasHeld;

    // How long the feet have been on the ground without a break.
    private int _ticksOnGround;

    // Was the jump button already held in the previous tick? A jump needs a fresh press,
    // so holding the button down does not make the player hop again and again.
    private bool _jumpWasHeld;

    // Perfect jump timing, counted in ticks. "Never" is a large number so the first jump is never perfect.
    private const int Never = int.MaxValue / 2;
    private readonly int _perfectWindowTicks;
    private int _ticksSinceLanding = Never;
    private int _ticksSinceJumpPressedInAir = Never;
    private int _ticksSinceDodgeEnded = Never;
    private int _ticksInAir;
    private bool _perfectJumpQueued;

    // The highest point (feet) of the current stay in the air - for the fall height of PlayerLanded (issue #291).
    // NaN = no stay in the air measured yet (spawned in the air): then the height of the last tick counts.
    private float _airPeakY = float.NaN;

    // A drop must last at least this long to count as a "landing" for the perfect window -
    // stepping down a small step is not a landing (code review finding). 0.25 s rules out drops up to
    // about 0.5 m; a real jump (0.8 s) or falling into a pit still counts.
    private static readonly int MinimumAirTicksForLanding = SimulationClock.SecondsToTicks(0.25f);

    /// <summary>
    /// Advances the player by one simulation tick: walking (with less control in the air), jumping,
    /// perfect jumps, gravity, walls, floor and ceiling.
    /// </summary>
    /// <remarks>
    /// The phases run in this order because most of them read what the ones before it left behind (issue #325).
    /// Some neighbours share nothing and could swap without any effect - the timers and the jump chain, counting down
    /// dash and dodge and the landing, crouch and steps; they keep the order they always had:
    /// <list type="number">
    ///   <item>Death and Gorm's chain first take away what the input may do - every later phase sees the cut input.</item>
    ///   <item>Inventory and timers (cooldowns, consecration, reloads) before the attack, so a weapon that just became
    ///     ready fires in this very tick.</item>
    ///   <item>The jump chain (counters, bonus, exhaustion, refilling) before steering: the top speed and whether a jump
    ///     is timed depend on it.</item>
    ///   <item>Steering (walking, jump presses, dash and dodge, the jump itself, the pull) only sets the velocity;
    ///     a dash or dodge started here already overrides walking and blocks the jump of the same tick.</item>
    ///   <item>Moving through the level, then counting down dash and dodge: their end looks at where the move left the
    ///     feet (on the ground or not).</item>
    ///   <item>Crouch, steps, fall height and landing after the move, because they need the new position.</item>
    ///   <item>The attack last, so blade and shot start at the eyes' position of this tick.</item>
    /// </list>
    /// </remarks>
    public void Tick(PlayerInput input)
    {
        PreviousPosition = Position;
        input = LimitInput(input, out var pulled);

        _events.Clear();
        UseInventory(input);
        TickWeaponTimers();

        var wasGrounded = IsGrounded;
        TickJumpChain();

        var (horizontal, vertical) = Steer(input);
        Velocity = horizontal with { Y = vertical };

        var (grounded, fallSpeed, airTicksBeforeLanding) = MoveThroughLevel();
        CountDownDashAndDodge(grounded);

        Crouch.Tick(IsDodging);
        CountSteps(wasGrounded && grounded, pulled);
        TrackFallAndLanding(wasGrounded, grounded, fallSpeed, airTicksBeforeLanding);

        Attack(input);
    }

    /// <summary>
    /// What the input may still do: a dead player does nothing any more (gravity still works); a player pulled by
    /// Gorm's chain cannot walk, jump, dash or dodge - looking and fighting still work. <paramref name="pulled"/>
    /// tells whether the chain pulls at the start of this tick.
    /// </summary>
    private PlayerInput LimitInput(PlayerInput input, out bool pulled)
    {
        if (IsDead)
        {
            input = PlayerInput.None;
        }

        pulled = IsPulled;
        if (pulled)
        {
            input = input with { Move = Vector2.Zero, Jump = false, Dash = false };
            _dashTicksLeft = 0;
            _dodgeTicksLeft = 0;
        }

        return input;
    }

    /// <summary>Blade cooldown, consecrated ammunition, and cooldown and reload of every ranged weapon count down.</summary>
    private void TickWeaponTimers()
    {
        MeleeCooldown.Tick();
        if (_consecratedTicksLeft > 0 && --_consecratedTicksLeft == 0)
        {
            // Ran out by itself (death sets it to 0 in TakeDamage, without this event) - issue #291.
            _events.Add(new ConsecrationEnded(Id));
        }
        foreach (var weapon in _rangedSlots.OfType<RangedWeapon>())
        {
            weapon.Cooldown.Tick();
            if (weapon.Magazine.Tick())
            {
                _events.Add(new ReloadFinished(Id));
            }
        }
    }

    /// <summary>
    /// The landing dip and the perfect-jump counters move on; a chain of jumps that is over ends bonus and exhaustion;
    /// the stamina refills (or not) and ticks.
    /// </summary>
    private void TickJumpChain()
    {
        LandingDip.Tick();
        _ticksSinceLanding = Math.Min(_ticksSinceLanding + 1, Never);
        _ticksSinceJumpPressedInAir = Math.Min(_ticksSinceJumpPressedInAir + 1, Never);
        _ticksSinceDodgeEnded = Math.Min(_ticksSinceDodgeEnded + 1, Never);

        // A chain of jumps ends when the player stays on the ground longer than the perfect window:
        // bonus and exhaustion are over. It ends on the ground, not at the moment of landing - a timed press
        // shortly after landing still belongs to the chain, and so does brushing the edge of a platform for a
        // few ticks while still rising (code review finding). The bar refills only while standing like this.
        var chainOver = IsGrounded && _ticksOnGround > _perfectWindowTicks;
        if (chainOver)
        {
            HasPerfectJumpBonus = false;
            IsExhausted = false;
        }

        // No refilling during a dodge either - otherwise standing on the ground would pay part of it back.
        // Nor while the blade is still charging (it pays bit by bit); once it is fully charged, holding on is free
        // and the bar refills again (found while playing). The same once the charge has stalled because the stamina
        // could not pay the next bit - e.g. after a dodge or dash while holding (issue #413).
        Stamina.SetRecharging(chainOver && !IsDodging && !MeleeCharge.IsPaying);
        Stamina.Tick();
    }

    /// <summary>
    /// The velocity the player wants for this tick, before walls and gravity: walking, a jump press, a dash or dodge
    /// (which override walking), the jump itself and the pull of Gorm's chain - in this order.
    /// </summary>
    private (Vector3 Horizontal, float Vertical) Steer(PlayerInput input)
    {
        var horizontal = Walk(input);

        // Vertical: jump on a fresh press while standing. Gravity is applied in MoveVertically.
        var vertical = Velocity.Y;
        var jumpPressed = RegisterJumpPress(input);

        StartEvade(input);
        OverrideByDashOrDodge(input, ref horizontal, ref vertical);
        TryJump(jumpPressed, ref horizontal, ref vertical);

        if (IsPulled)
        {
            horizontal = _pullVelocity;
            _pullTicksLeft--;
        }

        return (horizontal, vertical);
    }

    /// <summary>
    /// Horizontal: accelerate towards the wished velocity. In the air: less steering strength,
    /// but a slightly higher top speed - more with the perfect jump bonus, less when exhausted.
    /// </summary>
    private Vector3 Walk(PlayerInput input)
    {
        const float Dt = SimulationClock.SecondsPerTick;
        var acceleration = _player.Acceleration * (IsGrounded ? 1f : _player.AirControl);
        var targetVelocity = WishDirection(input) * TopSpeed(IsGrounded);
        return MoveTowards(new Vector3(Velocity.X, 0f, Velocity.Z), targetVelocity, acceleration * Dt);
    }

    /// <summary>
    /// Reads the jump button. A fresh press in the air, at the end of a dash or at the end of a dodge is remembered
    /// for later (a buffered or queued jump). Returns whether the button was freshly pressed in this tick.
    /// </summary>
    private bool RegisterJumpPress(PlayerInput input)
    {
        var jumpPressed = input.Jump && !_jumpWasHeld;
        _jumpWasHeld = input.Jump;

        if (jumpPressed && !IsGrounded && !IsDashing && !IsDodging)
        {
            // Pressed in the air: remember when - if we land soon, the jump happens then ("buffered").
            _ticksSinceJumpPressedInAir = 0;
        }

        if (jumpPressed && IsDashing && _dashTicksLeft <= _perfectWindowTicks)
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

        if (jumpPressed && IsDodging && _dodgeTicksLeft <= _perfectWindowTicks)
        {
            // Pressed at the end of a dodge: jump as soon as it is over, as a timed jump - the same rhythm
            // as pressing shortly before landing. Pressed earlier in the dodge, the press is simply ignored.
            _perfectJumpQueued = true;
        }

        return jumpPressed;
    }

    /// <summary>Shift on a fresh press: dodge on the ground, dash in the air. Both override walking and jumping.</summary>
    private void StartEvade(PlayerInput input)
    {
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
    }

    /// <summary>While dashing or dodging, their own speed and direction replace walking (and, for a dash, falling).</summary>
    private void OverrideByDashOrDodge(PlayerInput input, ref Vector3 horizontal, ref float vertical)
    {
        if (IsDashing)
        {
            // Flat dashes have no Y; a dash in the looking direction also rises or sinks (no gravity meanwhile).
            horizontal = (_dashDirection with { Y = 0f }) * _dashSpeed;
            vertical = _dashDirection.Y * _dashSpeed;
        }

        if (IsDodging)
        {
            // Fully steerable: the dodge always goes where the player walks. Without keys it keeps its last direction.
            var wish = WishDirection(input);
            if (wish.LengthSquared() > 0f)
            {
                _dodgeDirection = Vector3.Normalize(wish);
            }

            horizontal = _dodgeDirection * _player.DodgeSpeed;
        }
    }

    /// <summary>
    /// Jumps if standing (not dashing or dodging) and the button was freshly pressed or a timed jump is queued.
    /// Pays stamina and sets bonus or exhaustion; the current speed changes with the new speed factor.
    /// </summary>
    private void TryJump(bool jumpPressed, ref Vector3 horizontal, ref float vertical)
    {
        var jumpNow = !IsDashing && !IsDodging && IsGrounded && (jumpPressed || _perfectJumpQueued);
        if (!jumpNow)
        {
            return;
        }

        // Timed: pressed shortly before landing or at the end of a dodge (queued),
        // or shortly after landing or after a dodge.
        var wasQueued = _perfectJumpQueued;
        var afterDodge = _ticksSinceDodgeEnded <= _perfectWindowTicks;
        var timed = wasQueued || afterDodge || _ticksSinceLanding <= _perfectWindowTicks;
        _perfectJumpQueued = false;
        vertical = _player.JumpSpeed;
        _events.Add(new PlayerJumped(Id));

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
            LandingDip.Soften(_player.PerfectJumpDipFactor);
        }
    }

    /// <summary>
    /// Walls first (sideways), then gravity, floor and ceiling (up and down). Returns whether the feet are on the
    /// ground afterwards, the falling speed for the landing dip and how long the stay in the air before lasted.
    /// </summary>
    private (bool Grounded, float FallSpeed, int AirTicksBeforeLanding) MoveThroughLevel()
    {
        const float Dt = SimulationClock.SecondsPerTick;
        var boxes = _boxes().ToList();
        (Position, Velocity) = CharacterCollision.Move(
            _level, Position, Velocity, BodySizes.Player.Radius, Dt, CollisionHeight, bodies: _bodies().ToList(), boxes: boxes);

        // Falling speed at the end of this tick - needed for the landing dip, because landing sets it to 0.
        var fallSpeed = -(Velocity.Y - _player.Gravity * Dt);

        var airTicksBeforeLanding = _ticksInAir;
        var gravity = IsDashing ? 0f : _player.Gravity;
        (Position, Velocity, var grounded) = CharacterCollision.MoveVertically(
            _level, Position, Velocity, BodySizes.Player.Radius, CollisionHeight, gravity, Dt, boxes);
        IsGrounded = grounded;
        _ticksInAir = grounded ? 0 : _ticksInAir + 1;

        // A dash up or down ends where ceiling or floor stop it.
        if (IsDashing && _dashDirection.Y != 0f && Velocity.Y == 0f)
        {
            _dashTicksLeft = 1;
        }
        _ticksOnGround = grounded ? _ticksOnGround + 1 : 0;

        return (grounded, fallSpeed, airTicksBeforeLanding);
    }

    /// <summary>
    /// Gives the air dash back on the ground, then counts down dash and dodge. Ending one falls back to the top speed;
    /// a dodge that ends on the ground opens the window for a perfect jump out of it.
    /// </summary>
    private void CountDownDashAndDodge(bool grounded)
    {
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
                // The dash's own climb ends with it: from here on gravity alone decides (no shooting on upwards).
                Velocity = Velocity with { Y = MathF.Min(Velocity.Y, 0f) };
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
    }

    /// <summary>
    /// Keeps the highest point of a stay in the air; on landing reports the fall height, starts the landing dip,
    /// opens the perfect window (only after a real drop) and queues a jump pressed just before.
    /// </summary>
    private void TrackFallAndLanding(bool wasGrounded, bool grounded, float fallSpeed, int airTicksBeforeLanding)
    {
        if (!grounded)
        {
            _airPeakY = MathF.Max(wasGrounded || float.IsNaN(_airPeakY) ? PreviousPosition.Y : _airPeakY, Position.Y);
        }

        if (wasGrounded || !grounded)
        {
            return;
        }

        var peak = float.IsNaN(_airPeakY) ? PreviousPosition.Y : MathF.Max(_airPeakY, PreviousPosition.Y);
        _events.Add(new PlayerLanded(Id, MathF.Max(0f, peak - Position.Y)));
        _airPeakY = float.NaN;
        LandingDip.Land(fallSpeed);

        // Only real drops open the perfect window (see MinimumAirTicksForLanding).
        var realLanding = airTicksBeforeLanding >= MinimumAirTicksForLanding;
        if (realLanding)
        {
            _ticksSinceLanding = 0;
        }

        var pressedAtDashEnd = _jumpPressedAtDashEnd;
        _jumpPressedAtDashEnd = false;
        if ((realLanding && _ticksSinceJumpPressedInAir <= _perfectWindowTicks) || pressedAtDashEnd)
        {
            // Pressed just before landing (or at the end of the dash): jump again right away in the next tick
            // (after a dash that landed on a step: when the dash is over), as a timed jump.
            // Only a jump the stamina can pay for lands softly.
            _perfectJumpQueued = true;
            _ticksSinceJumpPressedInAir = Never;
            if (Stamina.CanPay)
            {
                LandingDip.Soften(_player.PerfectJumpDipFactor);
            }
        }
    }

    /// <summary>
    /// Blade and pistol: each needs a fresh press and a finished cooldown. They work in every movement
    /// (jumping, dash, dodge) and cost no stamina. Done after moving, so the events start at the eyes'
    /// position of this tick.
    /// </summary>
    private void Attack(PlayerInput input)
    {
        if (IsDead)
        {
            // Dying while charging must not count as "let go" - drop the charge without a swing.
            if (MeleeCharge.IsCharging)
            {
                MeleeCharge.Release();
            }

            return;
        }

        var meleePressed = input.Melee && !_meleeWasHeld;
        var shootPressed = input.Shoot && !_shootWasHeld;
        var reloadPressed = input.Reload && !_reloadWasHeld;
        _meleeWasHeld = input.Melee;
        _shootWasHeld = input.Shoot;
        _reloadWasHeld = input.Reload;

        UseBlade(meleePressed, input.Melee);

        // Mouse wheel or 1 - 3: another ranged weapon (empty places are skipped). Putting one away breaks its reload off.
        SwitchRanged(input);

        UseRanged(reloadPressed, shootPressed);
    }

    /// <summary>Blade: pressing starts charging (only when the cooldown is over), holding charges, letting go swings.</summary>
    private void UseBlade(bool pressed, bool held)
    {
        if (pressed && MeleeCooldown.IsReady)
        {
            MeleeCharge.Start();
        }
        else if (MeleeCharge.IsCharging && held)
        {
            MeleeCharge.Tick(Stamina);
        }
        else if (MeleeCharge.IsCharging)
        {
            var charge = MeleeCharge.Release();
            MeleeCooldown.TryUse();
            _events.Add(new MeleeSwung(EyePosition, View.Forward, charge, Melee));
        }
    }

    /// <summary>R reloads; so does a shot with an empty magazine. While reloading the weapon does not fire.</summary>
    private void UseRanged(bool reloadPressed, bool shootPressed)
    {
        var ranged = Ranged;
        if (reloadPressed || (shootPressed && ranged.Magazine.Rounds == 0))
        {
            if (ranged.Magazine.StartReload())
            {
                _events.Add(new ReloadStarted(Id));
            }
        }
        else if (shootPressed && ranged.Cooldown.IsReady && ranged.Magazine.TryTake())
        {
            // Consecrated ammunition: the weapon is ready again sooner.
            ranged.Cooldown.TryUse(IsConsecrated ? _consecratedFireRate : 1f);
            _events.Add(new ShotFired(EyePosition, View.Forward, IsConsecrated, ranged.Kind));
        }
    }

    private void SwitchRanged(PlayerInput input)
    {
        var target = SelectedSlot;
        if (input.SelectSlot is { } slot && slot >= 0 && slot < RangedSlotCount && _rangedSlots[slot] is not null)
        {
            target = slot;
        }
        else if (input.CycleSlot != 0)
        {
            // Round the places in the wheel's direction until a weapon comes.
            var step = Math.Sign(input.CycleSlot);
            for (var tries = 1; tries < RangedSlotCount; tries++)
            {
                var next = (((SelectedSlot + (step * tries)) % RangedSlotCount) + RangedSlotCount) % RangedSlotCount;
                if (_rangedSlots[next] is not null)
                {
                    target = next;
                    break;
                }
            }
        }

        if (target == SelectedSlot)
        {
            return;
        }

        Ranged.Magazine.CancelReload();
        SelectedSlot = target;
        _events.Add(new WeaponSwitched(Id, target, Ranged.Kind));
    }

    /// <summary>
    /// Starts a dash if allowed: only in the air (on the ground the bar refills - dashing there could be
    /// repeated again and again, found while playing), once per stay in the air, not already dashing, and the
    /// stamina pays one segment (empty bar = no dash). Direction: with only forward held (W), where the player
    /// looks - also up or down (decision record 0007); otherwise flat where the player walks, or forward if not walking.
    /// </summary>
    private void TryStartDash(PlayerInput input)
    {
        var allowed = !IsDashing && !IsDodging && !IsGrounded && !_airDashUsed;
        if (!allowed || !Stamina.TryPay())
        {
            return;
        }

        var wish = WishDirection(input);
        var onlyForward = input.Move.Y > 0f && input.Move.X == 0f;
        _dashDirection = onlyForward ? Vector3.Normalize(View.Forward)
            : wish.LengthSquared() > 0f ? Vector3.Normalize(wish) : View.FlatForward;
        _dashTicksLeft = _dashTicks;
        _perfectJumpQueued = false;
        _ticksSinceJumpPressedInAir = Never;
        _events.Add(new PlayerDashed(Id));

        _airDashUsed = true;
    }

    /// <summary>
    /// Starts a dodge if allowed: on the ground, not already dodging, and the stamina pays
    /// <see cref="PlayerBalance.DodgeStaminaSegments"/> segments. Direction: where the player walks, or forward.
    /// </summary>
    private void TryStartDodge(PlayerInput input)
    {
        if (IsDodging || !Stamina.TryPay(_player.DodgeStaminaSegments))
        {
            return;
        }

        var wish = WishDirection(input);
        _dodgeDirection = wish.LengthSquared() > 0f ? Vector3.Normalize(wish) : View.FlatForward;
        _dodgeTicksLeft = _dodgeTicks;
        _perfectJumpQueued = false;
        _events.Add(new PlayerDodged(Id));

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
        var air = grounded || IsExhausted ? 1f : _player.AirSpeedMultiplier;
        return _player.MaxSpeed * air * SpeedFactor;
    }

    // 1 normally, e.g. 1.2 with the perfect jump bonus or 0.8 when exhausted.
    private float SpeedFactor =>
        HasPerfectJumpBonus ? 1f + _player.PerfectJumpSpeedBonus
        : IsExhausted ? 1f - _player.ExhaustedJumpSpeedPenalty
        : 1f;

    /// <summary>
    /// Eye position for drawing, blended between the previous and the latest tick, lowered by the landing dip.
    /// <paramref name="alpha"/> 0 = previous tick, 1 = latest tick.
    /// </summary>
    public Vector3 InterpolatedEyePosition(float alpha) =>
        Vector3.Lerp(PreviousPosition, Position, Math.Clamp(alpha, 0f, 1f))
        + new Vector3(0f, Crouch.EyeHeight(Crouch.Interpolated(alpha)) - LandingDip.Interpolated(alpha), 0f);

    /// <summary>How far the view tilts down because of the landing dip, in radians (for drawing).</summary>
    public float InterpolatedViewTilt(float alpha) => LandingDip.Interpolated(alpha) * _player.LandingTiltPerMetre;

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
