namespace SilverAndAsh.Core.World;

/// <summary>
/// Which way the front of a chest looks - the side with the lock; the lid opens towards the back (issue #209). Only
/// along Z: a chest is wider (X) than deep (Z), and its solid body stays the same whichever way it looks.
/// </summary>
public enum ChestFront
{
    /// <summary>The front looks towards +Z (south).</summary>
    PlusZ,

    /// <summary>The front looks towards -Z (north).</summary>
    MinusZ,
}
