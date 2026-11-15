namespace SilverAndAsh.Core.World;

/// <summary>
/// What kind of door it is - decides how it looks and how it opens. The grate of an arena is no kind but a state
/// any door can be in (<c>Door.IsBarred</c>).
/// </summary>
public enum DoorKind
{
    /// <summary>A wooden door: F opens it.</summary>
    Normal,

    /// <summary>The door of the boss room: large, skulls, red light. F opens it.</summary>
    Boss,

    /// <summary>An iron door with a padlock - needs a key, which is used up. Once unlocked it stays open.</summary>
    Locked,
}
