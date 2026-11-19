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

    /// <summary>The silver blade: a quarter more reach than the club (playtest of 19.11.2026, #425).</summary>
    public MeleeBalance Blade { get; init; } = new() { Range = 2.5f };

    /// <summary>The club (decision record 0008): slower than the blade, harder, and it knocks enemies back.</summary>
    public MeleeBalance Club { get; init; } = new()
    {
        Cooldown = 0.55f,
        Damage = 50,
        Knockback = 3f,
        ChargedKnockback = 6f,
    };

    /// <summary>The values of melee weapon <paramref name="kind"/>.</summary>
    public MeleeBalance Melee(Gameplay.WeaponKind kind) => kind switch
    {
        Gameplay.WeaponKind.Club => Club,
        _ => Blade,
    };

    /// <summary>The pistol with silver bullets.</summary>
    public RangedBalance Pistol { get; init; } = new();

    /// <summary>The rifle (Forged, decision record 0008): heavy, aimed shots that go through one enemy into the next.</summary>
    public RangedBalance Rifle { get; init; } = new()
    {
        Damage = 80,
        HeadshotMultiplier = 3f,
        Range = 80f,
        Cooldown = 1.4f,
        MagazineSize = 4,
        ReloadTime = 2.5f,
        Pierce = 1,
    };

    /// <summary>The blunderbuss (Forged, decision record 0008): a cone of pellets, devastating up close, useless far away.</summary>
    public RangedBalance Blunderbuss { get; init; } = new()
    {
        Damage = 12,
        HeadshotMultiplier = 1f,
        Range = 12f,
        Cooldown = 0.5f,
        MagazineSize = 1,
        ReloadTime = 1.8f,
        Pellets = 9,
        Spread = 20f,
    };

    /// <summary>The values of ranged weapon <paramref name="kind"/>.</summary>
    public RangedBalance Ranged(Gameplay.WeaponKind kind) => kind switch
    {
        Gameplay.WeaponKind.Rifle => Rifle,
        Gameplay.WeaponKind.Blunderbuss => Blunderbuss,
        _ => Pistol,
    };

    /// <summary>The training dummy.</summary>
    public DummyBalance Dummy { get; init; } = new();

    /// <summary>The ghoul: a crawling enemy.</summary>
    public GhoulBalance Ghoul { get; init; } = new();

    /// <summary>Gorm, the Chained: the troll, boss of level 0 (decision record 0007).</summary>
    public GormBalance Gorm { get; init; } = new();

    /// <summary>The vampire bat: a small flying enemy.</summary>
    public BatBalance Bat { get; init; } = new();

    /// <summary>The bat's blood bolt: a flying projectile you can dodge.</summary>
    public BloodBoltBalance BloodBolt { get; init; } = new();

    /// <summary>Experience for kills and how much each level needs.</summary>
    public ExperienceBalance Experience { get; init; } = new();

    /// <summary>What the bonuses chosen after a level up give.</summary>
    public BoonBalance Boons { get; init; } = new();

    /// <summary>What killed enemies leave behind.</summary>
    public DropsBalance Drops { get; init; } = new();

    /// <summary>How much silver killed enemies leave behind.</summary>
    public SilverBalance Silver { get; init; } = new();

    /// <summary>How often keys drop.</summary>
    public KeysBalance Keys { get; init; } = new();

    /// <summary>Chests: where they stand, which tier, how often locked, what is inside.</summary>
    public ChestsBalance Chests { get; init; } = new();

    /// <summary>Equipment as loot: how a draw from the loot pool chooses (see <see cref="Gameplay.LootPool"/>).</summary>
    public LootBalance Loot { get; init; } = new();

    /// <summary>Doors: how long they take to swing open.</summary>
    public DoorsBalance Doors { get; init; } = new();

    /// <summary>The Well of Sirius (issue #438): how often a level has one and how wide it stands.</summary>
    public WellBalance Well { get; init; } = new();

    /// <summary>The blood vial: a consumable that heals.</summary>
    public BloodVialBalance BloodVial { get; init; } = new();

    /// <summary>Consecrated ammunition: a consumable that makes the pistol fire faster for a while.</summary>
    public ConsecratedAmmoBalance ConsecratedAmmo { get; init; } = new();

    /// <summary>How a level is put together from room templates.</summary>
    public LayoutBalance Layout { get; init; } = new();

    /// <summary>How many enemies the rooms get, depending on how far they are from the start.</summary>
    public DifficultyBalance Difficulty { get; init; } = new();

    /// <summary>Seconds between the death of all players and the restart of the level.</summary>
    public float RestartDelay { get; init; } = 2f;
}

/// <summary>Experience: what a kill is worth and how much each level needs.</summary>
public sealed record ExperienceBalance
{
    /// <summary>Experience for killing a ghoul (the player who lands the killing hit gets it).</summary>
    public int Ghoul { get; init; } = 30;

    /// <summary>Experience for killing a bat.</summary>
    public int Bat { get; init; } = 20;

    /// <summary>Experience needed from level 0 (the start) to level 1.</summary>
    public int FirstLevel { get; init; } = 100;

    /// <summary>Every further level needs this many times as much as the one before (1.25: 100, 125, 156, 195 ...).</summary>
    public float Growth { get; init; } = 1.25f;
}

/// <summary>
/// The bonuses after a level up: small steps, each counted from the base value - so choosing one three times gives
/// three times its value (not compound interest). A level up offers some of them at random.
/// </summary>
public sealed record BoonBalance
{
    /// <summary>How many different boons a level up offers, drawn at random from the pool.</summary>
    public int Offered { get; init; } = 3;

    /// <summary>Stamina regeneration: the bar refills this much faster per choice (0.05 = +5 % of the base speed).</summary>
    public float StaminaRegen { get; init; } = 0.05f;

    /// <summary>Health: this much more maximum health per choice - and healed at once.</summary>
    public int Health { get; init; } = 5;

    /// <summary>Damage: this much more damage with every weapon per choice (0.05 = +5 % of the base damage).</summary>
    public float Damage { get; init; } = 0.05f;

    /// <summary>Experience: this much more experience per kill per choice (0.025 = +2.5 % of the base experience).</summary>
    public float Experience { get; init; } = 0.025f;
}

/// <summary>How often killed enemies drop a consumable.</summary>
public sealed record DropsBalance
{
    /// <summary>Chance that a killed ghoul drops something: 0 = never, 1 = always.</summary>
    public float GhoulChance { get; init; } = 0.12f;

    /// <summary>Chance that a killed bat drops something: 0 = never, 1 = always.</summary>
    public float BatChance { get; init; } = 0.08f;

    /// <summary>Of all drops, this share is consecrated ammunition (0.3 = 30 %); the rest are blood vials.</summary>
    public float ConsecratedAmmoShare { get; init; } = 0.3f;
}

/// <summary>
/// Silver, the currency of a run: a killed enemy drops silver with its chance, an amount between its minimum and
/// maximum (both included). 0 to 0 = no silver from that kind of enemy.
/// </summary>
public sealed record SilverBalance
{
    /// <summary>Chance that a killed ghoul drops silver: 0 = never, 1 = always.</summary>
    public float GhoulChance { get; init; } = 0.5f;

    /// <summary>Chance that a killed bat drops silver: 0 = never, 1 = always.</summary>
    public float BatChance { get; init; } = 0.5f;

    /// <summary>Least silver a killed ghoul drops.</summary>
    public int GhoulMin { get; init; } = 1;

    /// <summary>Most silver a killed ghoul drops.</summary>
    public int GhoulMax { get; init; } = 3;

    /// <summary>Least silver a killed bat drops.</summary>
    public int BatMin { get; init; } = 1;

    /// <summary>Most silver a killed bat drops.</summary>
    public int BatMax { get; init; } = 3;
}

/// <summary>Keys open locked doors; each fits every lock and is used up. Chances: 0 = never, 1 = always.</summary>
public sealed record KeysBalance
{
    /// <summary>Chance that a cleared combat (or boss) room leaves a key in its middle.</summary>
    public float RoomChance { get; init; } = 0.1f;

    /// <summary>Chance that a killed enemy drops a key.</summary>
    public float EnemyChance { get; init; } = 0.025f;
}

/// <summary>Equipment as loot (decision records 0006 and 0008): how a draw from the loot pool chooses.</summary>
public sealed record LootBalance
{
    /// <summary>Chance that a draw rises one tier above the least tier asked for (a chest's own tier): 0 = never, 1 = always.</summary>
    public float HigherTierChance { get; init; } = 0.1f;

    /// <summary>Chance that a draw is an item rather than a weapon: 0 = always a weapon, 1 = always an item.</summary>
    public float ItemShare { get; init; } = 0.7f;
}

/// <summary>Chests (decision record 0006). Each tier has its own values - see <see cref="ChestTierBalance"/>.</summary>
public sealed record ChestsBalance
{
    /// <summary>Chance that a combat room gets a chest (on one of its chest places): 0 = never, 1 = always.</summary>
    public float CombatRoomChance { get; init; } = 0.25f;

    /// <summary>Fewest chests in a treasure room (never more than it has chest places).</summary>
    public int TreasureChestsMin { get; init; } = 1;

    /// <summary>Most chests in a treasure room.</summary>
    public int TreasureChestsMax { get; init; } = 2;

    /// <summary>Beggar's Chest (wood).</summary>
    public ChestTierBalance Beggar { get; init; } = new()
    {
        CombatShareNear = 0.7f, CombatShareFar = 0.55f, TreasureShare = 0.2f, LockedTreasureShare = 0.15f, SecretShare = 0f,
        LockedChance = 0.1f, SilverMin = 10, SilverMax = 20, BloodVialChance = 0.1f, EquipmentChance = 0.3f,
    };

    /// <summary>Guard's Chest (metal).</summary>
    public ChestTierBalance Guard { get; init; } = new()
    {
        CombatShareNear = 0.27f, CombatShareFar = 0.37f, TreasureShare = 0.45f, LockedTreasureShare = 0.3f, SecretShare = 0.35f,
        LockedChance = 0.35f, SilverMin = 20, SilverMax = 35, BloodVialChance = 0.25f, EquipmentChance = 0.5f,
    };

    /// <summary>King's Chest (gold).</summary>
    public ChestTierBalance King { get; init; } = new()
    {
        CombatShareNear = 0.03f, CombatShareFar = 0.08f, TreasureShare = 0.3f, LockedTreasureShare = 0.45f, SecretShare = 0.5f,
        LockedChance = 0.7f, SilverMin = 40, SilverMax = 60, BloodVialChance = 0.5f, EquipmentChance = 0.8f,
    };

    /// <summary>Vampire's Chest (red).</summary>
    public ChestTierBalance Vampire { get; init; } = new()
    {
        CombatShareNear = 0f, CombatShareFar = 0f, TreasureShare = 0.05f, LockedTreasureShare = 0.1f, SecretShare = 0.15f,
        LockedChance = 1f, SilverMin = 80, SilverMax = 120, BloodVialChance = 1f, EquipmentChance = 1f,
    };

    /// <summary>The values of <paramref name="tier"/>.</summary>
    public ChestTierBalance Of(World.ChestTier tier) => tier switch
    {
        World.ChestTier.Beggar => Beggar,
        World.ChestTier.Guard => Guard,
        World.ChestTier.King => King,
        World.ChestTier.Vampire => Vampire,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown chest tier."),
    };
}

/// <summary>
/// The values of one chest tier. Combat shares: how likely a combat room's chest is of this tier, "near" in the combat
/// room nearest to the start and "far" in the farthest, rising evenly in between. The shares of all tiers need not add
/// up to 1 - each counts in proportion to the others. The same holds for the shares in treasure rooms.
/// </summary>
public sealed record ChestTierBalance
{
    /// <summary>Share of this tier among the chests of the combat room nearest to the start.</summary>
    public float CombatShareNear { get; init; }

    /// <summary>Share of this tier among the chests of the farthest combat room.</summary>
    public float CombatShareFar { get; init; }

    /// <summary>Share of this tier among the chests of a treasure room with an ordinary door.</summary>
    public float TreasureShare { get; init; }

    /// <summary>Share of this tier among the chests of a treasure room behind a locked door.</summary>
    public float LockedTreasureShare { get; init; }

    /// <summary>Share of this tier for the chest of a secret room.</summary>
    public float SecretShare { get; init; }

    /// <summary>Chance that a chest of this tier is locked: 0 = never, 1 = always.</summary>
    public float LockedChance { get; init; }

    /// <summary>Least silver inside.</summary>
    public int SilverMin { get; init; }

    /// <summary>Most silver inside.</summary>
    public int SilverMax { get; init; }

    /// <summary>Chance that a blood vial lies inside too: 0 = never, 1 = always.</summary>
    public float BloodVialChance { get; init; }

    /// <summary>
    /// Chance that a piece of equipment lies inside too (decision record 0008), drawn from the loot pool at least of the
    /// chest's tier: 0 = never, 1 = always. Rolled with the loot pool's own generator when the chest is opened.
    /// </summary>
    public float EquipmentChance { get; init; }
}

/// <summary>Balancing values of consecrated ammunition.</summary>
public sealed record ConsecratedAmmoBalance
{
    /// <summary>The pistol fires this many times as fast while it lasts (2 = half the cooldown). The damage stays.</summary>
    public float FireRateMultiplier { get; init; } = 2f;

    /// <summary>Seconds it lasts - starting at once when picked up. Picking up another starts the time again.</summary>
    public float Duration { get; init; } = 10f;
}

/// <summary>The pattern of a level: how many rooms, how often corridors and loops.</summary>
public sealed record LayoutBalance
{
    /// <summary>Fewest combat rooms in a level. The generator picks a number between this and the maximum.</summary>
    public int CombatRoomsMin { get; init; } = 12;

    /// <summary>Most combat rooms in a level.</summary>
    public int CombatRoomsMax { get; init; } = 14;

    /// <summary>Chance that a room is reached through a corridor instead of a door in a shared wall: 0 = never, 1 = always.</summary>
    public float CorridorChance { get; init; } = 0.6f;

    /// <summary>Most loops (extra connections that make a second way round). 0 = the rooms form a tree.</summary>
    public int MaxLoops { get; init; } = 2;

    /// <summary>Treasure rooms with an ordinary door, per level.</summary>
    public int TreasureRooms { get; init; } = 1;

    /// <summary>Treasure rooms behind a locked door (it needs a key), per level.</summary>
    public int LockedTreasureRooms { get; init; } = 1;

    /// <summary>Secret rooms per level, each behind a cracked wall of a combat room.</summary>
    public int SecretRooms { get; init; } = 1;
}

/// <summary>
/// The spawn budget (issue #157): every combat room buys enemies with a budget - "near" in the combat room nearest to
/// the start, "far" in the farthest, evenly in between; the boss room has its own. Each kind of enemy costs points, and
/// weights (also "near" to "far") decide which kind comes next. Enemies stand on random free floor, keeping away from the
/// doors and from each other. Every combat room gets at least one enemy.
/// </summary>
public sealed record DifficultyBalance
{
    /// <summary>Budget of the combat room nearest to the start.</summary>
    public int BudgetNear { get; init; } = 10;

    /// <summary>Budget of the farthest combat room.</summary>
    public int BudgetFar { get; init; } = 24;

    /// <summary>Budget of the boss room for enemies besides the boss (0 = the boss fights alone).</summary>
    public int BudgetBoss { get; init; }

    /// <summary>What a ghoul costs.</summary>
    public int GhoulCost { get; init; } = 2;

    /// <summary>What a bat costs.</summary>
    public int BatCost { get; init; } = 3;

    /// <summary>Weight of ghouls near the start (the weights of all kinds count in proportion).</summary>
    public float GhoulWeightNear { get; init; } = 0.85f;

    /// <summary>Weight of ghouls far from the start (and in the boss room).</summary>
    public float GhoulWeightFar { get; init; } = 0.6f;

    /// <summary>Weight of bats near the start.</summary>
    public float BatWeightNear { get; init; } = 0.15f;

    /// <summary>Weight of bats far from the start (and in the boss room).</summary>
    public float BatWeightFar { get; init; } = 0.4f;

    /// <summary>Most bats in one room (issue #391): once a room holds this many, its budget buys only ghouls.</summary>
    public int MaxBatsPerRoom { get; init; } = 3;

    /// <summary>
    /// Least distance (metres, cell middle to cell middle) of an enemy from the room's doors - in a combat room also
    /// where it comes out of the floor or the ceiling when the room seals (issue #424).
    /// </summary>
    public float DoorDistance { get; init; } = 4f;

    /// <summary>
    /// Least distance (metres, flat, cell middle to the player's feet) of a combat room's enemy from every player when
    /// it comes out (issue #445). A start cell nearer than this to a player is swapped for the nearest free cell of
    /// the room that keeps this distance and <see cref="DoorDistance"/>; with none, the free cell farthest from all
    /// players. Provisional.
    /// </summary>
    public float PlayerDistance { get; init; } = 3f;

    /// <summary>Least distance (metres) between two enemies of a room.</summary>
    public float Spacing { get; init; } = 2.5f;

    /// <summary>
    /// Seconds an enemy of a combat room takes to come out of the floor (ghoul) or the ceiling (bat) once the room
    /// seals (issue #424). Meanwhile it neither attacks nor can be hit.
    /// </summary>
    public float EmergeTime { get; init; } = 0.8f;
}

/// <summary>
/// The Well of Sirius (issue #438). Price per throw, chances and the most throws are not decided yet; until then using
/// it only turns it from ready to spent.
/// </summary>
public sealed record WellBalance
{
    /// <summary>Chance that a level has a well (at most one). Provisional: 1 = every level, so it can be seen.</summary>
    public float LevelChance { get; init; } = 1f;

    /// <summary>Radius in metres of the circle nobody walks through.</summary>
    public float CollisionRadius { get; init; } = 0.95f;
}

/// <summary>
/// Doors (issue #245): seconds a door takes to swing fully open. Only the picture moves - the passage is free the moment
/// the door is opened. A cracked wall has no time: it breaks at once.
/// </summary>
public sealed record DoorsBalance
{
    /// <summary>Wooden double door.</summary>
    public float NormalOpenTime { get; init; } = 0.9f;

    /// <summary>Locked door (one heavy leaf).</summary>
    public float LockedOpenTime { get; init; } = 1.3f;

    /// <summary>Boss door (two heavy iron leaves).</summary>
    public float BossOpenTime { get; init; } = 2.2f;

    /// <summary>The time for <paramref name="kind"/>; 0 for a cracked wall.</summary>
    public float OpenTime(World.DoorKind kind) => kind switch
    {
        World.DoorKind.Normal => NormalOpenTime,
        World.DoorKind.Locked => LockedOpenTime,
        World.DoorKind.Boss => BossOpenTime,
        World.DoorKind.Cracked => 0f,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}

/// <summary>Balancing values of the blood vial.</summary>
public sealed record BloodVialBalance
{
    /// <summary>Health it gives back - never above the maximum.</summary>
    public int Heal { get; init; } = 30;
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

/// <summary>
/// Balancing values of a melee weapon - the silver blade (<see cref="Balance.Blade"/>) and the club
/// (<see cref="Balance.Club"/>) have the same kind of values (decision record 0008).
/// </summary>
public sealed record MeleeBalance
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

    /// <summary>A hit pushes ghouls and bats this far away (metres; not Gorm) ...</summary>
    public float Knockback { get; init; }

    /// <summary>... and this far when fully charged (in between: linear).</summary>
    public float ChargedKnockback { get; init; }

    /// <summary>The push takes this long (seconds); meanwhile the enemy does nothing else.</summary>
    public float KnockbackTime { get; init; } = 0.25f;
}

/// <summary>
/// Balancing values of a ranged weapon - the pistol (<see cref="Balance.Pistol"/>), the rifle
/// (<see cref="Balance.Rifle"/>) and the blunderbuss (<see cref="Balance.Blunderbuss"/>) have the same kind of values (decision record 0008).
/// </summary>
public sealed record RangedBalance
{
    /// <summary>Damage per hit. A bit less than the blade, but with its slow rate every shot must count.</summary>
    public int Damage { get; init; } = 35;

    /// <summary>Damage multiplier for a hit on the head (critical hit), e.g. 3 = 300 %.</summary>
    public float HeadshotMultiplier { get; init; } = 3f;

    /// <summary>How far a shot reaches, in metres.</summary>
    public float Range { get; init; } = 50f;

    /// <summary>Seconds before the next shot.</summary>
    public float Cooldown { get; init; } = 0.7f;

    /// <summary>Rounds in a full magazine (decision record 0008) ...</summary>
    public int MagazineSize { get; init; } = 7;

    /// <summary>... and the seconds a reload takes. The supply is unlimited.</summary>
    public float ReloadTime { get; init; } = 1.5f;

    /// <summary>How many enemies a shot goes on through after the first (0 = it stops in the first). Walls stop it.</summary>
    public int Pierce { get; init; }

    /// <summary>Bullets (pellets) one shot fires; each hits on its own with <see cref="Damage"/>.</summary>
    public int Pellets { get; init; } = 1;

    /// <summary>Full width of the cone the pellets fly in, in degrees (0 = all straight ahead). The spread comes from the seed.</summary>
    public float Spread { get; init; }
}

/// <summary>Balancing values of the training dummy.</summary>
public sealed record DummyBalance
{
    /// <summary>Health (and the most it can be).</summary>
    public int MaxHealth { get; init; } = 100;

    /// <summary>Seconds it lies on the ground before it stands up again.</summary>
    public float DownTime { get; init; } = 3f;
}

/// <summary>Balancing values of Gorm, the Chained (decision record 0007). His attacks come with issue #165.</summary>
public sealed record GormBalance
{
    /// <summary>His health.</summary>
    public int MaxHealth { get; init; } = 3600;

    /// <summary>Walking speed in m/s - slower than the player.</summary>
    public float Speed { get; init; } = 2.5f;

    /// <summary>He stops walking this close to the player (metres, middle to middle).</summary>
    public float StopDistance { get; init; } = 3f;

    /// <summary>A pistol shot at his head does this many times the damage.</summary>
    public float HeadMultiplier { get; init; } = 1.5f;

    /// <summary>Seconds between path searches.</summary>
    public float RepathTime { get; init; } = 0.5f;

    /// <summary>Least silver he leaves behind.</summary>
    public int SilverMin { get; init; } = 100;

    /// <summary>Most silver he leaves behind.</summary>
    public int SilverMax { get; init; } = 150;

    /// <summary>
    /// He always leaves a piece of equipment (decision record 0008): Vampiric with this chance, else Royal (0 = always
    /// Royal, 1 = always Vampiric). Rolled with the loot pool's own generator.
    /// </summary>
    public float EquipmentVampiricChance { get; init; } = 0.2f;

    /// <summary>Silver he leaves instead of the equipment when the loot pool has nothing of that tier or above (the worth of a Vampire's Chest).</summary>
    public int ExtraSilver { get; init; } = 100;

    /// <summary>Seconds the spiral staircase takes to rise after his death.</summary>
    public float StairsRiseTime { get; init; } = 3f;

    /// <summary>Up to this distance (metres) he attacks with hammer or chain swing ...</summary>
    public float NearRange { get; init; } = 8f;

    /// <summary>... beyond it, up to this distance, he throws his chain.</summary>
    public float FarRange { get; init; } = 30f;

    /// <summary>Seconds of rest after every attack - the moment to strike back.</summary>
    public float PauseTime { get; init; } = 1.2f;

    /// <summary>Warning before the hammer blow (club raised, red circle on the floor), in seconds.</summary>
    public float HammerWindUp { get; init; } = 0.8f;

    /// <summary>Damage of the hammer's shock wave.</summary>
    public int HammerDamage { get; init; } = 40;

    /// <summary>The shock wave runs along the floor this fast (m/s) ...</summary>
    public float ShockwaveSpeed { get; init; } = 8f;

    /// <summary>... this far (metres) ...</summary>
    public float ShockwaveRadius { get; init; } = 12f;

    /// <summary>... and this high (metres): whoever is higher in the air stays unhurt.</summary>
    public float ShockwaveHeight { get; init; } = 0.4f;

    /// <summary>A hammer blow that hits nobody gets the club stuck for this long (seconds) ...</summary>
    public float StuckTime { get; init; } = 2f;

    /// <summary>... and meanwhile he takes this many times the damage.</summary>
    public float StuckDamageMultiplier { get; init; } = 2f;

    /// <summary>Warning before the chain swing (chain spun up), in seconds.</summary>
    public float SwingWindUp { get; init; } = 0.6f;

    /// <summary>Damage of the chain swing.</summary>
    public int SwingDamage { get; init; } = 30;

    /// <summary>
    /// Reach of the chain swing (metres, middle to middle). Farther than <see cref="NearRange"/>: one step back
    /// without a dodge is not enough (issue #430, provisional until the next playtest).
    /// </summary>
    public float SwingReach { get; init; } = 12f;

    /// <summary>The swinging chain passes this high above his feet (metres) - from here ...</summary>
    public float SwingLow { get; init; } = 1.1f;

    /// <summary>... to here: standing or jumping it hits, ducked in a dodge (1 m) not.</summary>
    public float SwingHigh { get; init; } = 1.6f;

    /// <summary>The swing sweeps from his left over the front to his right in this time (seconds) ...</summary>
    public float SwingTime { get; init; } = 0.6f;

    /// <summary>... along this arc (degrees; 180 = the half in front of him - behind him you are safe).</summary>
    public float SwingArc { get; init; } = 180f;

    /// <summary>Warning before the chain throw (the hook glows red), in seconds.</summary>
    public float ThrowWindUp { get; init; } = 0.5f;

    /// <summary>The hook flies this fast (m/s), straight to where the player stood when it was thrown.</summary>
    public float HookSpeed { get; init; } = 18f;

    /// <summary>Damage of the hook.</summary>
    public int ThrowDamage { get; init; } = 20;

    /// <summary>A hit pulls the player to this distance from him (metres) ...</summary>
    public float PullDistance { get; init; } = 3f;

    /// <summary>... within this time (seconds), without control.</summary>
    public float PullTime { get; init; } = 0.4f;

    /// <summary>At or below this share of his health (0.5 = 50 %) phase 2 begins - once.</summary>
    public float Phase2Health { get; init; } = 0.5f;

    /// <summary>Seconds he roars at the start of phase 2 - he does not attack meanwhile.</summary>
    public float RoarTime { get; init; } = 1.5f;

    /// <summary>In phase 2 he is this many times as fast: walking, warnings and pauses (1.3 = 30 % faster).</summary>
    public float Phase2SpeedUp { get; init; } = 1.4f;

    /// <summary>In phase 2 every hammer blow shakes this many stones from the ceiling ...</summary>
    public int StoneCount { get; init; } = 6;

    /// <summary>... each landing at most this far (metres) from the player ...</summary>
    public float StoneSpread { get; init; } = 4f;

    /// <summary>... announced by its shadow on the floor this long (seconds) ...</summary>
    public float StoneWarning { get; init; } = 1f;

    /// <summary>... hitting whoever stands this close (metres) to where it lands ...</summary>
    public float StoneRadius { get; init; } = 1f;

    /// <summary>... with this damage.</summary>
    public int StoneDamage { get; init; } = 25;
}

/// <summary>Balancing values of the ghoul.</summary>
public sealed record GhoulBalance
{
    /// <summary>Health (2 blade hits, 1 pistol headshot, 3 pistol body shots).</summary>
    public int MaxHealth { get; init; } = 100;

    /// <summary>Seconds the dead body lies on the ground before it disappears.</summary>
    public float CorpseTime { get; init; } = 2f;

    /// <summary>It notices a player it can see who is closer than this, in metres.</summary>
    public float SightRange { get; init; } = 12f;

    /// <summary>Crawling speed in m/s - a little slower than the player's 6 m/s: you can get away, but not by strolling.</summary>
    public float Speed { get; init; } = 5f;

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
    public int AttackDamage { get; init; } = 30;

    /// <summary>Seconds of rest after a blow.</summary>
    public float RecoverTime { get; init; } = 0.6f;

    /// <summary>The way to the target is searched again at most this often, in seconds.</summary>
    public float RepathTime { get; init; } = 0.5f;
}

/// <summary>Balancing values of the vampire bat.</summary>
public sealed record BatBalance
{
    /// <summary>Health: one blade hit or one pistol shot.</summary>
    public int MaxHealth { get; init; } = 40;

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
    public float SpitInterval { get; init; } = 1.6f;

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
    public int BiteDamage { get; init; } = 20;

    /// <summary>The way to its target is searched again at most this often, in seconds.</summary>
    public float RepathTime { get; init; } = 0.5f;
}

/// <summary>Balancing values of the bat's blood bolt.</summary>
public sealed record BloodBoltBalance
{
    /// <summary>Speed in m/s - fast, but you see it coming.</summary>
    public float Speed { get; init; } = 12f;

    /// <summary>Damage to the player.</summary>
    public int Damage { get; init; } = 20;

    /// <summary>Radius of the ball in metres.</summary>
    public float Radius { get; init; } = 0.15f;

    /// <summary>Seconds until it disappears if it hits nothing.</summary>
    public float LifeTime { get; init; } = 5f;
}
