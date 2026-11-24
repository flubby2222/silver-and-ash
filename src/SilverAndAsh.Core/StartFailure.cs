using SilverAndAsh.Core.Photos;

namespace SilverAndAsh.Core;

/// <summary>
/// The line the game prints when it cannot start (a wrong option, a broken level, a full photo folder). Most failures
/// mean the level could not be loaded; a refused <c>--out</c> folder has nothing to do with the level and keeps its own
/// words (issue #651). A new kind of failure gets its own entry in <see cref="Describe"/>.
/// </summary>
public static class StartFailure
{
    /// <summary>What stands before the message of every failure without its own words.</summary>
    public const string LevelPrefix = "Could not load the level: ";

    /// <summary>The line for <paramref name="exception"/> on the console.</summary>
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            PhotoFolderNotEmptyException => exception.Message,
            _ => LevelPrefix + exception.Message,
        };
    }
}
