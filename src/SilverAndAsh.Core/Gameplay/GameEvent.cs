using System.Numerics;

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

/// <summary>The pistol was fired from <paramref name="Origin"/> (the eyes) in <paramref name="Direction"/>.</summary>
public sealed record ShotFired(Vector3 Origin, Vector3 Direction) : GameEvent;

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

/// <summary>Ghoul <paramref name="GhoulId"/> reached 0 health and dies.</summary>
public sealed record GhoulDied(int GhoulId) : GameEvent;

/// <summary>Training dummy <paramref name="DummyId"/> reached 0 health and falls over.</summary>
public sealed record DummyKnockedDown(int DummyId) : GameEvent;

/// <summary>Training dummy <paramref name="DummyId"/> stands up again with full health.</summary>
public sealed record DummyStoodUp(int DummyId) : GameEvent;
