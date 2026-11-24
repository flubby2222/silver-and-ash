namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// How long the level's name stays in big letters when a run starts, and how it counts down (issue #736). The game
/// draws it (LevelBanner); the photos that must be free of it wait as long (CourseScenes.BannerTicks).
/// </summary>
public static class LevelNameBanner
{
    /// <summary>How long the level's name stays, in seconds.</summary>
    public const float Seconds = 3.5f;

    /// <summary>The seconds left after a frame of <paramref name="frameSeconds"/>: never below 0.</summary>
    public static float After(float secondsLeft, float frameSeconds) => MathF.Max(0f, secondsLeft - frameSeconds);

    /// <summary>True while the name is still drawn, however faint.</summary>
    public static bool IsShown(float secondsLeft) => secondsLeft > 0f;
}
