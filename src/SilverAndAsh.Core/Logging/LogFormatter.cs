using System.Globalization;
using System.Text;

namespace SilverAndAsh.Core.Logging;

/// <summary>Turns a log entry into one line of text (plus exception details, if any).</summary>
public static class LogFormatter
{
    /// <summary>
    /// Formats an entry as <c>2026-11-10 21:30:15.123 +01:00 [INFO ] message</c>.
    /// Exception details (type, message, stack trace, inner exceptions) follow on the next lines.
    /// </summary>
    public static string Format(DateTimeOffset timestamp, LogLevel level, string message, Exception? exception = null)
    {
        var builder = new StringBuilder()
            .Append(timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture))
            .Append(" [")
            .Append(Label(level))
            .Append("] ")
            .Append(message);

        if (exception is not null)
        {
            // Exception.ToString() contains type, message, stack trace and all inner exceptions.
            builder.AppendLine().Append(exception);
        }

        return builder.ToString();
    }

    /// <summary>Fixed-width labels keep the log columns aligned.</summary>
    private static string Label(LogLevel level) => level switch
    {
        LogLevel.Info => "INFO ",
        LogLevel.Warning => "WARN ",
        LogLevel.Error => "ERROR",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown log level."),
    };
}
