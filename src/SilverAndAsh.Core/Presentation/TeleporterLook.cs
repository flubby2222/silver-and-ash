using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The teleporter "horn crystal" (issues #436, #480): two models from tools/models/teleporter.py with the same shape,
/// assets/models/teleporter_locked.glb (dark crystal) and teleporter_unlocked.glb (bright crystal). Neither part moves in
/// its file; the teleporter stands with its origin on the floor in the middle of its cell, never turned. The crystal of an
/// unlocked teleporter shines by itself: no shading by direction, no lantern fall-off (<see cref="Glows"/>).
/// </summary>
public static class TeleporterLook
{
    public const string Body = "body";
    public const string Crystal = "crystal";

    /// <summary>Height of the crystal's middle above the floor (tools/models/teleporter.py, CRYSTAL_Y).</summary>
    public const float CrystalHeight = 2.1f;

    /// <summary>How bright a glowing part stays at any distance (the floor for the lantern; 1 = full light).</summary>
    public const float Glow = 1f;

    private static readonly RigPart[] All = [new(Body, null), new(Crystal, null)];

    /// <summary>The parts of both models, in the order of the rig.</summary>
    public static IReadOnlyList<RigPart> Parts => All;

    /// <summary>The file of the locked or unlocked teleporter.</summary>
    public static string FileName(bool unlocked) => unlocked ? "teleporter_unlocked.glb" : "teleporter_locked.glb";

    /// <summary>True for the part that shines by itself: the crystal, and only once the teleporter is unlocked.</summary>
    public static bool Glows(string part, bool unlocked) => unlocked && part == Crystal;

    /// <summary>The name of the place a teleporter marks, as the travel choice shows it.</summary>
    public static string PlaceName(TeleporterSite site) => site switch
    {
        TeleporterSite.Start => "Start Hall",
        TeleporterSite.BossGate => "Boss Gate",
        _ => throw new ArgumentOutOfRangeException(nameof(site), site, null),
    };
}
