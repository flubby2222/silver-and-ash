using SilverAndAsh.Core.Logging;

namespace SilverAndAsh.Core.Tests.Logging;

public class LogFormatterTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 11, 10, 21, 30, 15, 123, TimeSpan.FromHours(1));

    [Theory]
    [InlineData(LogLevel.Info, "2026-11-10 21:30:15.123 +01:00 [INFO ] Game started")]
    [InlineData(LogLevel.Warning, "2026-11-10 21:30:15.123 +01:00 [WARN ] Game started")]
    [InlineData(LogLevel.Error, "2026-11-10 21:30:15.123 +01:00 [ERROR] Game started")]
    public void Formats_timestamp_level_and_message(LogLevel level, string expected)
    {
        Assert.Equal(expected, LogFormatter.Format(Timestamp, level, "Game started"));
    }

    [Fact]
    public void Exception_details_follow_on_the_next_lines()
    {
        var exception = CaptureThrown(() => throw new InvalidOperationException("Boom"));

        var text = LogFormatter.Format(Timestamp, LogLevel.Error, "Crash", exception);
        var lines = text.Split(Environment.NewLine);

        Assert.Equal("2026-11-10 21:30:15.123 +01:00 [ERROR] Crash", lines[0]);
        Assert.Contains("System.InvalidOperationException: Boom", lines[1]);
        Assert.Contains(lines, line => line.TrimStart().StartsWith("at ", StringComparison.Ordinal));
    }

    [Fact]
    public void Inner_exceptions_are_included()
    {
        var exception = new InvalidOperationException("Outer", new FileNotFoundException("Inner file missing"));

        var text = LogFormatter.Format(Timestamp, LogLevel.Error, "Crash", exception);

        Assert.Contains("Outer", text);
        Assert.Contains("Inner file missing", text);
    }

    private static Exception CaptureThrown(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            return exception;
        }

        throw new InvalidOperationException("The action did not throw.");
    }
}
