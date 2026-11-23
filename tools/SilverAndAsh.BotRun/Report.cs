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
