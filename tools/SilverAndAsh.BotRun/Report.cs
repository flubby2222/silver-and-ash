namespace SilverAndAsh.BotRun;

/// <summary>
/// What a bot run found: one line per finding at once (so a long run shows them early), plus counters of what was
/// played through. Counters are printed sorted, without times, so two runs (e.g. head against main) can be diffed.
/// </summary>
internal sealed class Report
{
    private readonly SortedDictionary<string, int> _counts = new(StringComparer.Ordinal);
    private string _section = "";

    public int Findings { get; private set; }

    /// <summary>Counters from here on belong to <paramref name="mode"/>.</summary>
    public void Section(string mode) => _section = mode;

    public void Finding(string what)
    {
        Findings++;
        Count("findings");
        Console.WriteLine($"FINDING [{_section}] {what}");
    }

    public void Count(string what)
    {
        var key = $"{_section}: {what}";
        _counts[key] = _counts.GetValueOrDefault(key) + 1;
    }

    /// <summary>A finding when <paramref name="bad"/> holds; returns <paramref name="bad"/>.</summary>
    public bool Check(bool bad, string what)
    {
        if (bad)
        {
            Finding(what);
        }

        return bad;
    }

    /// <summary>Runs one scenario; an exception from the game is a finding and the next scenario still runs.</summary>
    public void Try(string context, Action scenario)
    {
        try
        {
            scenario();
        }
        catch (Exception e) when (e is not MissingCoreMember)
        {
            var cause = e is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : e;
            Finding($"{context}: threw {cause.GetType().Name}: {cause.Message}");
        }
    }

    /// <summary>
    /// A tally of the places a mode plays round the middle of something (e.g. a portal) and the ones it skips. Skipped
    /// places are counted as "<paramref name="skipped"/> (skipped)"; <see cref="Places.Verify"/> makes it a finding if fewer
    /// than <paramref name="minPerSide"/> places were played on either side of the middle - a mode that skips every place
    /// must fail loudly, not end with "0 finding(s)" (PR #641).
    /// </summary>
    public Places Tally(string context, string skipped, int minPerSide = 1) => new(this, context, skipped, minPerSide);

    public sealed class Places(Report report, string context, string skipped, int minPerSide)
    {
        private int _below;
        private int _above;

        /// <summary>A place <paramref name="across"/> metres beside the middle was played (the middle itself counts for neither side).</summary>
        public void Played(float across)
        {
            if (across < -0.01f)
            {
                _below++;
            }
            else if (across > 0.01f)
            {
                _above++;
            }
        }

        public void Skipped() => report.Count($"{skipped} (skipped)");

        public void Verify() => report.Check(
            _below < minPerSide || _above < minPerSide,
            $"{context}: fewer than {minPerSide} place(s) played on a side of the middle ({_below} and {_above}), the rest skipped as \"{skipped}\" - the mode checks (almost) nothing");
    }

    public void Print(string mode, int from, int to)
    {
        Console.WriteLine();
        foreach (var (what, count) in _counts)
        {
            Console.WriteLine($"  {what}: {count}");
        }

        Console.WriteLine($"SUMMARY mode {mode}, seeds {from}..{to}: {Findings} finding(s)");
    }
}
