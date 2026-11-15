namespace SilverAndAsh.Core.World;

/// <summary>What kind of door it is - decides how it looks and how it opens.</summary>
public enum DoorKind
{
    /// <summary>A wooden door: F opens it.</summary>
    Normal,

    /// <summary>The door of the boss room: large, skulls, red light. F opens it.</summary>
    Boss,

    /// <summary>An iron door with a padlock - needs a key (keys come in v0.9; not used yet).</summary>
    Locked,

    /// <summary>The lowered grate of an arena (next issue; not used yet).</summary>
    ArenaGate,
}
