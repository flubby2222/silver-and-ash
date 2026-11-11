using System.Numerics;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Something that happened in the simulation and that the presentation may want to show or play
/// (a swing, a muzzle flash, later a sound). Core only reports it; it never draws or plays anything itself.
/// This keeps logic and presentation apart, and a second computer can show the same events later
/// (rule 5 of decision record 0003, online co-op).
/// </summary>
public abstract record GameEvent;

/// <summary>The silver blade was swung from <paramref name="Origin"/> (the eyes) in <paramref name="Direction"/>.</summary>
public sealed record BladeSwung(Vector3 Origin, Vector3 Direction) : GameEvent;

/// <summary>The pistol was fired from <paramref name="Origin"/> (the eyes) in <paramref name="Direction"/>.</summary>
public sealed record ShotFired(Vector3 Origin, Vector3 Direction) : GameEvent;
