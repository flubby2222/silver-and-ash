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
/// The silver blade was swung from <paramref name="Origin"/> (the eyes) in <paramref name="Direction"/>, charged by
/// <paramref name="Charge"/> (0 = not at all, 1 = fully).
/// </summary>
public sealed record BladeSwung(Vector3 Origin, Vector3 Direction, float Charge = 0f) : GameEvent;

/// <summary>
/// The pistol was fired from <paramref name="Origin"/> (the eyes) in <paramref name="Direction"/> - with consecrated
/// ammunition if <paramref name="Consecrated"/>.
/// </summary>
public sealed record ShotFired(Vector3 Origin, Vector3 Direction, bool Consecrated = false) : GameEvent;

/// <summary>
/// A shot hit a wall, floor or ceiling at <paramref name="Point"/>; <paramref name="Normal"/> points out of the surface.
/// </summary>
public sealed record BulletImpact(Vector3 Point, Vector3 Normal) : GameEvent;

/// <summary>
/// Training dummy <paramref name="DummyId"/> took <paramref name="Damage"/>; <paramref name="Critical"/> for a critical
/// hit such as a headshot.
/// </summary>
public sealed record DummyHit(int DummyId, int Damage, bool Critical = false) : GameEvent;

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

/// <summary>Door <paramref name="DoorId"/> was opened.</summary>
public sealed record DoorOpened(int DoorId) : GameEvent;

/// <summary>Room <paramref name="RoomIndex"/> became an arena: grates fell at all its doors, its enemies woke up.</summary>
public sealed record RoomSealed(int RoomIndex) : GameEvent;

/// <summary>All enemies of room <paramref name="RoomIndex"/> are dead: its grates rise.</summary>
public sealed record RoomCleared(int RoomIndex) : GameEvent;

/// <summary>The boss room is cleared: the run is won.</summary>
public sealed record RunWon : GameEvent;

/// <summary>Every player is dead before the run was won.</summary>
public sealed record RunLost : GameEvent;

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
