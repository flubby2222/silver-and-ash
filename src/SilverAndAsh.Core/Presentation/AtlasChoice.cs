namespace SilverAndAsh.Core.Presentation;

/// <summary>Which parts table the game draws from, and how much finer its picture is than the canvas (1 or 2).</summary>
public readonly record struct AtlasFile(string Table, int Scale);

/// <summary>
/// Picks the parts atlas of the HUD or the menus (issue #409): the atlas in double resolution
/// (<c>&lt;name&gt;_x2.json</c> and <c>&lt;name&gt;_x2.png</c>, issue #402) when both files are there, otherwise the
/// atlas of today (<c>&lt;name&gt;.json</c>). Pure - the caller says which files exist.
/// </summary>
public static class AtlasChoice
{
    /// <summary>How much finer the double atlas is than the canvas.</summary>
    public const int DoubleScale = 2;

    /// <summary>The atlas to draw <paramref name="name"/> (e.g. <c>hud_parts</c>) from, in <paramref name="directory"/>.</summary>
    public static AtlasFile Pick(string directory, string name, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        var fine = Path.Combine(directory, name + "_x2");
        return exists(fine + ".json") && exists(fine + ".png")
            ? new AtlasFile(fine + ".json", DoubleScale)
            : new AtlasFile(Path.Combine(directory, name + ".json"), 1);
    }
}
