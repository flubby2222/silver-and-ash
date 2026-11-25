namespace SilverAndAsh.Core.Photos;

/// <summary>
/// The smoke test of the game (<c>--smoke</c>, issue #329): every photo scene is drawn once in a hidden window, and the run
/// fails if a scene throws or one of its pictures is a single colour. Everything here is plain logic without Raylib, so
/// that tests can check it: reading the option, judging the pictures, and the summary with the exit code. The game only
/// draws and hands the pixels over.
/// </summary>
public static class SmokeCheck
{
    /// <summary>The option itself.</summary>
    public const string Option = "--smoke";

    /// <summary>Exit code when every scene was drawn and no picture is a single colour.</summary>
    public const int PassedExitCode = 0;

    /// <summary>Exit code when at least one scene failed, or no scene was drawn at all.</summary>
    public const int FailedExitCode = 1;

    /// <summary>
    /// Exit code when the hidden window could not be opened (e.g. no OpenGL 3.3 on a machine without a graphics card) -
    /// nothing was drawn, so nothing is said about the scenes.
    /// </summary>
    public const int NoWindowExitCode = 2;

    private const int BytesPerPixel = 4;

    /// <summary>
    /// Takes <c>--smoke</c> out of <paramref name="args"/>. Returns the other arguments and whether it was there. The smoke
    /// test goes alone: it takes every photo scene in the standard picture and window, so no other option fits.
    /// </summary>
    /// <exception cref="ArgumentException">If <c>--smoke</c> comes twice or together with another option.</exception>
    public static (IReadOnlyList<string> Others, bool Smoke) Extract(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var rest = args.Where(arg => arg != Option).ToList();
        var count = args.Count - rest.Count;
        if (count > 1)
        {
            throw new ArgumentException($"'{Option}' may come only once.");
        }

        if (count == 1 && rest.Count > 0)
        {
            throw new ArgumentException($"'{Option}' goes alone - it draws every photo scene in the standard window. Found also: {string.Join(" ", rest)}.");
        }

        return (rest, count == 1);
    }

    /// <summary>
    /// True if every pixel of <paramref name="rgba"/> (4 bytes per pixel: red, green, blue, alpha) has the same red, green
    /// and blue - also for a picture without pixels. Alpha is left out: a picture that differs only there looks like one
    /// colour on the screen.
    /// </summary>
    /// <exception cref="ArgumentException">If the length is no whole number of pixels.</exception>
    public static bool IsSingleColour(ReadOnlySpan<byte> rgba)
    {
        if (rgba.Length % BytesPerPixel != 0)
        {
            throw new ArgumentException($"A picture has {BytesPerPixel} bytes per pixel, but these are {rgba.Length} bytes.", nameof(rgba));
        }

        for (var i = BytesPerPixel; i < rgba.Length; i += BytesPerPixel)
        {
            if (rgba[i] != rgba[0] || rgba[i + 1] != rgba[1] || rgba[i + 2] != rgba[2])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Judges the two pictures of a scene: the 3D world in game resolution (<paramref name="world"/>) and the whole window
    /// (<paramref name="screen"/>), both RGBA. Null if both show more than one colour, else what is wrong.
    /// </summary>
    public static string? Judge(ReadOnlySpan<byte> world, ReadOnlySpan<byte> screen)
    {
        var flat = new List<string>(2);
        if (IsSingleColour(world))
        {
            flat.Add($"the 3D picture is a single colour{ColourOf(world)}");
        }

        if (IsSingleColour(screen))
        {
            flat.Add($"the window is a single colour{ColourOf(screen)}");
        }

        return flat.Count == 0 ? null : string.Join("; ", flat);
    }

    /// <summary>What a scene that threw <paramref name="exception"/> failed with: its type and message.</summary>
    public static string Crashed(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return $"threw {exception.GetType().Name}: {exception.Message}";
    }

    /// <summary>
    /// The result of a whole run: one line per failed scene (in their order), then a summary line, and the exit code -
    /// <see cref="PassedExitCode"/> only if at least one scene was drawn and none failed.
    /// </summary>
    public static SmokeSummary Summarise(IReadOnlyList<SmokeOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        var failed = outcomes.Where(outcome => !outcome.Passed).ToList();
        var lines = failed.Select(outcome => $"FAILED {outcome.Scene}: {outcome.Failure}").ToList();
        if (outcomes.Count == 0)
        {
            lines.Add("SMOKE no photo scene was drawn - nothing checked.");
            return new SmokeSummary(lines, FailedExitCode);
        }

        lines.Add($"SMOKE {outcomes.Count} scene(s) drawn, {failed.Count} failed.");
        return new SmokeSummary(lines, failed.Count == 0 ? PassedExitCode : FailedExitCode);
    }

    // The colour of the first pixel, to say which colour it is (black = nothing drawn, the background colour = no world).
    private static string ColourOf(ReadOnlySpan<byte> rgba) =>
        rgba.Length < BytesPerPixel ? " (no pixels)" : $" ({rgba[0]}, {rgba[1]}, {rgba[2]})";
}

/// <summary>How one photo scene fared in the smoke test: <see cref="Failure"/> is null if it passed.</summary>
public sealed record SmokeOutcome(string Scene, string? Failure)
{
    /// <summary>True if the scene was drawn without an exception and none of its pictures is a single colour.</summary>
    public bool Passed => Failure is null;
}

/// <summary>What the smoke test prints at the end, and the exit code of the game.</summary>
public sealed record SmokeSummary(IReadOnlyList<string> Lines, int ExitCode);
