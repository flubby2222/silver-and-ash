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

    /// <summary>
    /// A tally of the places a mode skips for one known reason, counted as "<paramref name="skipped"/> (skipped)", whose
    /// number is measured (issue #688): <see cref="Exactly.Verify"/> makes it a finding unless exactly
    /// <paramref name="perSide"/> places on each side of the middle and none in the middle were skipped so. A filter that
    /// swallows more places (or fewer) than it did is seen, not hidden among other skipped places.
    /// </summary>
    public Exactly Expect(string context, string skipped, int perSide) => new(this, context, skipped, perSide);

    public sealed class Places(Report report, string context, string skipped, int minPerSide)
    {
        private readonly Sides _played = new();

        /// <summary>A place <paramref name="across"/> metres beside the middle was played (the middle itself counts for neither side).</summary>
        public void Played(float across) => _played.Add(across);

        public void Skipped() => report.Count($"{skipped} (skipped)");

        public void Verify() => report.Check(
            _played.Below < minPerSide || _played.Above < minPerSide,
            $"{context}: fewer than {minPerSide} place(s) played on a side of the middle ({_played.Below} and {_played.Above}), the rest skipped as \"{skipped}\" - the mode checks (almost) nothing");
    }

    public sealed class Exactly(Report report, string context, string skipped, int perSide)
    {
        private readonly Sides _skipped = new();

        /// <summary>A place <paramref name="across"/> metres beside the middle was skipped for this reason.</summary>
        public void Skipped(float across)
        {
            report.Count($"{skipped} (skipped)");
            _skipped.Add(across);
        }

        public void Verify()
        {
            // The count line stands there even at 0, so the number can be read and compared (issue #688).
            report._counts.TryAdd($"{report._section}: {skipped} (skipped)", 0);
            report.Check(
                _skipped.Below != perSide || _skipped.Above != perSide || _skipped.Middle != 0,
                $"{context}: {_skipped.Below}, {_skipped.Middle} and {_skipped.Above} place(s) below, in and above the middle skipped as \"{skipped}\", measured {perSide}, 0 and {perSide} - the reason skips other places than measured");
        }
    }

    // Places counted by their side of the middle; within 1 cm of it they count as the middle.
    private sealed class Sides
    {
        public int Below { get; private set; }

        public int Middle { get; private set; }

        public int Above { get; private set; }

        public void Add(float across)
        {
            if (across < -0.01f)
            {
                Below++;
            }
            else if (across > 0.01f)
            {
                Above++;
            }
            else
            {
                Middle++;
            }
        }
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
