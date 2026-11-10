namespace SilverAndAsh.Core.World;

/// <summary>A level file could not be read. The message says what is wrong and where.</summary>
public sealed class LevelFormatException : FormatException
{
    /// <summary>Creates the exception for a problem in a specific line (1-based), or 0 for the whole file.</summary>
    public LevelFormatException(int lineNumber, string problem)
        : base(lineNumber > 0 ? $"Line {lineNumber}: {problem}" : problem)
    {
        LineNumber = lineNumber;
    }

    /// <summary>The line (1-based) where the problem is, or 0 if it concerns the whole file.</summary>
    public int LineNumber { get; }
}
