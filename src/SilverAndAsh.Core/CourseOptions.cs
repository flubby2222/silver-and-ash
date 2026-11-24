namespace SilverAndAsh.Core;

/// <summary>
/// The start option for the test course (issue #718, docs/pruefstrecke.md): <c>--course</c> walks through every station
/// whose look or sound still waits for a judgement, one after the other, with the real rules. Like <c>--showroom</c> it
/// has its own level and every weapon, so only <c>--look</c> goes with it. Read apart from the other options, before
/// them (see <see cref="Extract"/>).
/// </summary>
public static class CourseOptions
{
    /// <summary>The option itself.</summary>
    public const string Option = "--course";

    /// <summary>
    /// Takes <c>--course</c> out of <paramref name="args"/>. Returns the other arguments, in their order, and whether it
    /// was there.
    /// </summary>
    /// <exception cref="ArgumentException">If <c>--course</c> comes twice.</exception>
    public static (IReadOnlyList<string> Others, bool Course) Extract(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var rest = args.Where(arg => arg != Option).ToList();
        if (args.Count - rest.Count > 1)
        {
            throw new ArgumentException($"'{Option}' may come only once.");
        }

        return (rest, rest.Count < args.Count);
    }

    /// <summary>
    /// Checks that <paramref name="options"/> fit together with the test course: with <c>--course</c> nothing but
    /// <c>--look</c> may be given.
    /// </summary>
    /// <exception cref="ArgumentException">If they do not fit; the message says why.</exception>
    public static void Check(LaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Course)
        {
            return;
        }

        // Every other option at its default - also one added later, as the record compares them all. Only the list of
        // weapons to give is compared by its length, as a list is equal only to itself.
        var others = options with { Look = null, Course = false, Give = [] };
        if (options.Give is { Count: > 0 } || others != new LaunchOptions(LaunchOptions.DefaultLevelName, Give: others.Give))
        {
            throw new ArgumentException(
                $"'{Option}' has its own level and every weapon; only '--look' goes with it. Its pictures: '--photo course-1-arms' and the like.");
        }
    }
}
