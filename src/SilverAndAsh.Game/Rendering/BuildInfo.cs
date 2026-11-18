namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Which build this is. Developer displays (frame rate, test values) show only in the debug build (issue #340); asking
/// this property instead of wrapping code in <c>#if DEBUG</c> keeps the same code compiled in Debug and Release.
/// </summary>
internal static class BuildInfo
{
    /// <summary>True in the debug build (<c>dotnet run</c>), false in the released game.</summary>
    public static bool ShowsDeveloperDisplays { get; } =
#if DEBUG
        true;
#else
        false;
#endif
}
