using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Something that happened in the simulation and that the presentation may want to show or play
/// (a swing, a muzzle flash, later a sound). Core only reports it; it never draws or plays anything itself.
/// This keeps logic and presentation apart, and a second computer can show the same events later
/// (rule 5 of decision record 0003, online co-op).
/// </summary>
public abstract record GameEvent;

/// <summary>
/// The melee weapon <paramref name="Weapon"/> (the silver blade unless said otherwise) was swung from
/// <paramref name="Origin"/> (the eyes) in <paramref name="Direction"/>, charged by <paramref name="Charge"/>
/// (0 = not at all, 1 = fully).
/// </summary>
public sealed record MeleeSwung(
    Vector3 Origin, Vector3 Direction, float Charge = 0f, WeaponKind Weapon = WeaponKind.SilverBlade) : GameEvent;

/// <summary>
/// The ranged weapon <paramref name="Weapon"/> (the pistol unless said otherwise) was fired from
/// <paramref name="Origin"/> (the eyes) in <paramref name="Direction"/> - with consecrated ammunition if
/// <paramref name="Consecrated"/>.
/// </summary>
public sealed record ShotFired(
    Vector3 Origin, Vector3 Direction, bool Consecrated = false, WeaponKind Weapon = WeaponKind.Pistol) : GameEvent;

/// <summary>Player <paramref name="PlayerId"/> started reloading (R, or a shot with an empty magazine).</summary>
public sealed record ReloadStarted(int PlayerId) : GameEvent;

/// <summary>Player <paramref name="PlayerId"/>'s magazine is full again.</summary>
public sealed record ReloadFinished(int PlayerId) : GameEvent;

/// <summary>Player <paramref name="PlayerId"/> took the ranged weapon <paramref name="Kind"/> from place <paramref name="Slot"/> into the hand.</summary>
public sealed record WeaponSwitched(int PlayerId, int Slot, WeaponKind Kind) : GameEvent;

/// <summary>
/// Player <paramref name="PlayerId"/> dropped <paramref name="Kind"/> from the inventory at <paramref name="Position"/> (#193);
/// a ranged one comes as <paramref name="Ranged"/>, with its magazine.
/// </summary>
public sealed record WeaponDropped(int PlayerId, WeaponKind Kind, Vector3 Position, RangedWeapon? Ranged = null) : GameEvent;

/// <summary>
/// A shot hit a wall, floor or ceiling at <paramref name="Point"/>; <paramref name="Normal"/> points out of the surface.
/// </summary>
public sealed record BulletImpact(Vector3 Point, Vector3 Normal) : GameEvent;

/// <summary>
/// Training dummy <paramref name="DummyId"/> took <paramref name="Damage"/>; <paramref name="Critical"/> for a critical
/// hit such as a headshot.
/// </summary>
public sealed record DummyHit(int DummyId, int Damage, bool Critical = false) : GameEvent;

/// <summary>
/// Target <paramref name="TargetId"/> (a dummy, ghoul, bat or Gorm) was hit by player <paramref name="PlayerId"/> with
/// <paramref name="Weapon"/> (issue #267). Comes in the same tick right after the target's own hit event
/// (<see cref="DummyHit"/>, <see cref="GhoulHit"/>, <see cref="BatHit"/>, <see cref="GormHit"/>), which says how much
/// damage it took but not what with - the blade sound must not play for a pistol hit.
/// </summary>
public sealed record TargetHitBy(int TargetId, int PlayerId, Weapon Weapon) : GameEvent;

/// <summary>Ghoul <paramref name="GhoulId"/> took <paramref name="Damage"/>; <paramref name="Critical"/> for a headshot.</summary>
public sealed record GhoulHit(int GhoulId, int Damage, bool Critical = false) : GameEvent;

/// <summary>Projectile <paramref name="ProjectileId"/> was fired from <paramref name="Origin"/>.</summary>
public sealed record ProjectileFired(int ProjectileId, Vector3 Origin) : GameEvent;

/// <summary>Projectile <paramref name="ProjectileId"/> hit player <paramref name="PlayerId"/> at <paramref name="Point"/>.</summary>
public sealed record ProjectileHitPlayer(int ProjectileId, int PlayerId, Vector3 Point) : GameEvent;

/// <summary>Projectile <paramref name="ProjectileId"/> hit a wall, the floor or the ceiling at <paramref name="Point"/>.</summary>
public sealed record ProjectileImpact(int ProjectileId, Vector3 Point) : GameEvent;

/// <summary>Bat <paramref name="BatId"/> took <paramref name="Damage"/>.</summary>
public sealed record BatHit(int BatId, int Damage) : GameEvent;

/// <summary>Bat <paramref name="BatId"/> reached 0 health and tumbles to the floor.</summary>
public sealed record BatDied(int BatId) : GameEvent;

/// <summary>A killed enemy dropped pickup <paramref name="PickupId"/> of <paramref name="Kind"/> at <paramref name="Position"/>.</summary>
public sealed record PickupDropped(int PickupId, ConsumableKind Kind, Vector3 Position) : GameEvent;

/// <summary>A killed enemy dropped silver pile <paramref name="PileId"/> worth <paramref name="Amount"/> at <paramref name="Position"/>.</summary>
public sealed record SilverDropped(int PileId, int Amount, Vector3 Position) : GameEvent;

/// <summary>Player <paramref name="PlayerId"/> picked up silver pile <paramref name="PileId"/> worth <paramref name="Amount"/>.</summary>
public sealed record SilverTaken(int PileId, int PlayerId, int Amount) : GameEvent;

/// <summary>A key was dropped as loot <paramref name="LootId"/> at <paramref name="Position"/> (by a killed enemy or a cleared room).</summary>
public sealed record KeyDropped(int LootId, Vector3 Position) : GameEvent;

/// <summary>Player <paramref name="PlayerId"/> picked up the key <paramref name="LootId"/>.</summary>
public sealed record KeyTaken(int LootId, int PlayerId) : GameEvent;

/// <summary>Player <paramref name="PlayerId"/> used a key on the locked door <paramref name="DoorId"/> (it opens in the same tick).</summary>
public sealed record DoorUnlocked(int DoorId, int PlayerId) : GameEvent;

/// <summary>The chest <paramref name="ChestId"/> of a cleared combat room starts rising from the floor.</summary>
public sealed record ChestRose(int ChestId) : GameEvent;

/// <summary>Player <paramref name="PlayerId"/> used a key on the locked chest <paramref name="ChestId"/> (it opens in the same tick).</summary>
public sealed record ChestUnlocked(int ChestId, int PlayerId) : GameEvent;

/// <summary>Player <paramref name="PlayerId"/> opened the chest <paramref name="ChestId"/> of <paramref name="Tier"/>; its contents jump out.</summary>
public sealed record ChestOpened(int ChestId, int PlayerId, ChestTier Tier) : GameEvent;

/// <summary>Player <paramref name="PlayerId"/> picked up pickup <paramref name="PickupId"/> of <paramref name="Kind"/>.</summary>
public sealed record PickupTaken(int PickupId, int PlayerId, ConsumableKind Kind) : GameEvent;

/// <summary>
/// A chest or Gorm let the equipment <paramref name="DropId"/> - <paramref name="Piece"/> - fall at
/// <paramref name="Position"/> (issue #318). It lies there glowing in its tier's colour until a player takes it with F.
/// </summary>
public sealed record EquipmentDropped(int DropId, Equipment Piece, Vector3 Position) : GameEvent;

/// <summary>
/// Player <paramref name="PlayerId"/> took the equipment <paramref name="DropId"/> - <paramref name="Piece"/> - with F
/// at <paramref name="Position"/> (issue #317). The game celebrates it by <see cref="Tier"/>: a card, and more the higher it is.
/// </summary>
public sealed record EquipmentTaken(int DropId, int PlayerId, Equipment Piece, Vector3 Position) : GameEvent
{
    /// <summary>The tier of the piece taken.</summary>
    public ItemTier Tier => Piece.Tier;
}

/// <summary>Player <paramref name="PlayerId"/> gained <paramref name="Amount"/> experience (for a kill).</summary>
public sealed record ExperienceGained(int PlayerId, int Amount) : GameEvent;

/// <summary>Player <paramref name="PlayerId"/> reached level <paramref name="Level"/>.</summary>
public sealed record LevelUp(int PlayerId, int Level) : GameEvent;

/// <summary>Player <paramref name="PlayerId"/> chose <paramref name="Boon"/> after a level up.</summary>
public sealed record BoonChosen(int PlayerId, Boon Boon) : GameEvent;

/// <summary>Player <paramref name="PlayerId"/> got <paramref name="Amount"/> health back.</summary>
public sealed record PlayerHealed(int PlayerId, int Amount) : GameEvent;

/// <summary>Bat <paramref name="BatId"/> starts glowing: it will spit a blood bolt shortly.</summary>
public sealed record BatSpitWindUp(int BatId) : GameEvent;

/// <summary>Bat <paramref name="BatId"/> spits a blood bolt from <paramref name="Origin"/> along <paramref name="Direction"/>.</summary>
public sealed record BatSpat(int BatId, Vector3 Origin, Vector3 Direction) : GameEvent;

/// <summary>Bat <paramref name="BatId"/> screeches: it will dive at the spot where its target is now.</summary>
public sealed record BatScreeched(int BatId) : GameEvent;

/// <summary>Bat <paramref name="BatId"/> bit player <paramref name="PlayerId"/> in its dive.</summary>
public sealed record BatBit(int BatId, int PlayerId) : GameEvent;

/// <summary>Bat <paramref name="BatId"/> dived into empty air.</summary>
public sealed record BatDiveMissed(int BatId) : GameEvent;

/// <summary>Ghoul <paramref name="GhoulId"/> starts drawing back for a blow.</summary>
public sealed record GhoulWindUp(int GhoulId) : GameEvent;

/// <summary>Ghoul <paramref name="GhoulId"/> strikes; <paramref name="Hit"/> tells whether the blow landed.</summary>
public sealed record GhoulStruck(int GhoulId, bool Hit) : GameEvent;

/// <summary>The cracked wall <paramref name="DoorId"/> was broken open by player <paramref name="PlayerId"/>'s charged blade strike.</summary>
public sealed record WallBroken(int DoorId, int PlayerId) : GameEvent;

/// <summary>Door <paramref name="DoorId"/> was opened.</summary>
public sealed record DoorOpened(int DoorId) : GameEvent;

/// <summary>Room <paramref name="RoomIndex"/> became an arena: grates fell at all its doors, its enemies woke up.</summary>
public sealed record RoomSealed(int RoomIndex) : GameEvent;

/// <summary>
/// Wave <paramref name="Wave"/> (0 = the first) of combat room <paramref name="RoomIndex"/> starts coming out of the floor
/// and the ceiling (issue #424): the enemies <paramref name="EnemyIds"/>. No sound yet - a new kind of sound needs a
/// listening test first.
/// </summary>
public sealed record WaveEmerged(int RoomIndex, int Wave, IReadOnlyList<int> EnemyIds) : GameEvent;

/// <summary>All enemies of room <paramref name="RoomIndex"/> are dead: its grates rise.</summary>
public sealed record RoomCleared(int RoomIndex) : GameEvent;

/// <summary>The boss room is cleared: the run is won.</summary>
public sealed record RunWon : GameEvent;

/// <summary>Gorm <paramref name="GormId"/> took <paramref name="Damage"/> (critical: a shot at his head).</summary>
public sealed record GormHit(int GormId, int Damage, bool Critical = false) : GameEvent;

/// <summary>Gorm <paramref name="GormId"/> starts winding up <paramref name="Attack"/> - the warning.</summary>
public sealed record GormWindUp(int GormId, GormAttack Attack) : GameEvent;

/// <summary>Gorm's hammer struck the floor: its shock wave starts.</summary>
public sealed record GormSlammed(int GormId) : GameEvent;

/// <summary>The shock wave hit player <paramref name="PlayerId"/> (who was on the ground).</summary>
public sealed record ShockwaveHit(int PlayerId) : GameEvent;

/// <summary>Gorm's chain starts its sweep - the end of his wind-up (issue #298, for the sound of the chain).</summary>
public sealed record GormSwingStarted(int GormId) : GameEvent;

/// <summary>Gorm's chain swung round; <paramref name="Hit"/> tells whether it hit anyone. Comes at the end of the sweep.</summary>
public sealed record GormSwung(int GormId, bool Hit) : GameEvent;

/// <summary>Gorm threw his hook from <paramref name="From"/> along <paramref name="Direction"/>.</summary>
public sealed record GormThrew(int GormId, Vector3 From, Vector3 Direction) : GameEvent;

/// <summary>The hook hit player <paramref name="PlayerId"/>: they are pulled to Gorm.</summary>
public sealed record HookHit(int PlayerId) : GameEvent;

/// <summary>The hook hit a wall or pillar at <paramref name="At"/> (or flew its range) and is drawn back.</summary>
public sealed record HookStopped(Vector3 At) : GameEvent;

/// <summary>Gorm's hammer blow hit nobody: the club is stuck - he takes double damage for a while.</summary>
public sealed record ClubStuck(int GormId) : GameEvent;

/// <summary>Gorm pulled his club free again.</summary>
public sealed record ClubFreed(int GormId) : GameEvent;

/// <summary>Gorm fell to half his health: he roars (head back, the ground trembles) - phase 2 begins.</summary>
public sealed record GormRoared(int GormId) : GameEvent;

/// <summary>A stone comes loose from the ceiling: its shadow appears at <paramref name="At"/> on the floor.</summary>
public sealed record StoneFalling(Vector3 At) : GameEvent;

/// <summary>The stone struck the floor at <paramref name="At"/>.</summary>
public sealed record StoneLanded(Vector3 At) : GameEvent;

/// <summary>A falling stone hit player <paramref name="PlayerId"/>.</summary>
public sealed record StoneHit(int PlayerId) : GameEvent;

/// <summary>Gorm <paramref name="GormId"/> is dead.</summary>
public sealed record GormDied(int GormId) : GameEvent;

/// <summary>
/// Gorm <paramref name="GormId"/> put a heavy foot down while walking (issue #298): one every
/// <see cref="Gorm.StepLength"/> he covers. Only for the sound - it changes nothing.
/// </summary>
public sealed record GormStepped(int GormId) : GameEvent;

/// <summary>A player entered the boss room while Gorm lives: the boss fight begins (his health bar shows).</summary>
public sealed record BossFightStarted : GameEvent;

/// <summary>The spiral staircase starts rising in the middle of the boss room.</summary>
public sealed record StairsRose : GameEvent;

/// <summary>Player <paramref name="PlayerId"/> climbed the stairs: the run is won.</summary>
public sealed record StairsClimbed(int PlayerId) : GameEvent;

/// <summary>Player <paramref name="PlayerId"/> stepped onto teleporter <paramref name="TeleporterId"/> for the first time: it is unlocked for all.</summary>
public sealed record TeleporterUnlocked(int TeleporterId, int PlayerId, System.Numerics.Vector3 Position) : GameEvent;

/// <summary>
/// Player <paramref name="PlayerId"/> travelled from teleporter <paramref name="FromId"/> (at <paramref name="From"/>) to
/// teleporter <paramref name="ToId"/> (at <paramref name="To"/>).
/// </summary>
public sealed record PlayerTeleported(
    int PlayerId, int FromId, int ToId, System.Numerics.Vector3 From, System.Numerics.Vector3 To) : GameEvent;

/// <summary>Every player is dead before the run was won.</summary>
public sealed record RunLost : GameEvent;

/// <summary>
/// Player <paramref name="PlayerId"/> set down <paramref name="Foot"/>: they walked one step length on the ground
/// (issue #267). Never in the air or while standing.
/// </summary>
public sealed record PlayerStepped(int PlayerId, Foot Foot) : GameEvent;

/// <summary>Player <paramref name="PlayerId"/> took <paramref name="Damage"/>.</summary>
public sealed record PlayerHit(int PlayerId, int Damage) : GameEvent;

/// <summary>Player <paramref name="PlayerId"/> reached 0 health.</summary>
public sealed record PlayerDied(int PlayerId) : GameEvent;

/// <summary>Ghoul <paramref name="GhoulId"/> reached 0 health and dies.</summary>
public sealed record GhoulDied(int GhoulId) : GameEvent;

/// <summary>Training dummy <paramref name="DummyId"/> reached 0 health and falls over.</summary>
public sealed record DummyKnockedDown(int DummyId) : GameEvent;

/// <summary>Training dummy <paramref name="DummyId"/> stands up again with full health.</summary>
public sealed record DummyStoodUp(int DummyId) : GameEvent;

/// <summary>Player <paramref name="PlayerId"/> jumped off the ground (every jump, also a queued or perfect one; issue #291).</summary>
public sealed record PlayerJumped(int PlayerId) : GameEvent;

/// <summary>Player <paramref name="PlayerId"/> started an air dash (issue #291).</summary>
public sealed record PlayerDashed(int PlayerId) : GameEvent;

/// <summary>Player <paramref name="PlayerId"/> started a dodge on the ground (issue #291).</summary>
public sealed record PlayerDodged(int PlayerId) : GameEvent;

/// <summary>
/// Player <paramref name="PlayerId"/> came down on the ground after a stay in the air (issue #291).
/// <paramref name="FallHeight"/>: metres from the highest point of that stay down to where the feet are now -
/// small for a step down, about the jump height for a normal jump. Every landing comes; whether it is loud
/// enough to be heard is up to the presentation.
/// </summary>
public sealed record PlayerLanded(int PlayerId, float FallHeight) : GameEvent;

/// <summary>Player <paramref name="PlayerId"/>'s consecrated ammunition ran out by itself (issue #291).</summary>
public sealed record ConsecrationEnded(int PlayerId) : GameEvent;

/// <summary>Ghoul <paramref name="GhoulId"/> noticed player <paramref name="PlayerId"/> and starts chasing (once per noticing; issue #291).</summary>
public sealed record GhoulNoticed(int GhoulId, int PlayerId) : GameEvent;

/// <summary>
/// Bat <paramref name="BatId"/> beat its wings once, its body's middle at <paramref name="At"/> (issue #297): every
/// <see cref="Bat.FlapInterval"/> while it is awake and alive, never while it hangs still unnoticed. Only for the
/// sound - which flaps are near enough to be heard is up to the presentation.
/// </summary>
public sealed record BatFlapped(int BatId, Vector3 At) : GameEvent;
