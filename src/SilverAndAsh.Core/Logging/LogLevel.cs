namespace SilverAndAsh.Core.Logging;

/// <summary>How important a log entry is.</summary>
public enum LogLevel
{
    /// <summary>Normal events, e.g. "game started".</summary>
    Info,

    /// <summary>Something unexpected that the game could recover from.</summary>
    Warning,

    /// <summary>Something went wrong, e.g. a crash.</summary>
    Error,
}
