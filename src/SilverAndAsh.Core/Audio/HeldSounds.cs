namespace SilverAndAsh.Core.Audio;

/// <summary>
/// Which sounds the game holds while the world stands still (issue #776) - the decision only, without a sound device:
/// the player of the sounds pauses and resumes what this says. Entering the stillness holds the sounds that play;
/// leaving it lets exactly those go on, except one stopped meanwhile (a new run stops the charge sound of the old one):
/// a stopped sound must stay silent, it must not be resumed.
/// </summary>
public sealed class HeldSounds
{
    private readonly List<string> _held = [];

    /// <summary>The files held now.</summary>
    public IReadOnlyList<string> Held => _held;

    /// <summary>
    /// Once per frame: while <paramref name="frozen"/>, the files of <paramref name="playing"/> (the ones that play now)
    /// to pause - once, when the stillness begins; when it ends, the held files to resume. Empty otherwise.
    /// </summary>
    public HoldStep Frame(IEnumerable<string> playing, bool frozen)
    {
        ArgumentNullException.ThrowIfNull(playing);
        if (frozen && _held.Count == 0)
        {
            _held.AddRange(playing.Distinct(StringComparer.Ordinal));
            return new HoldStep([.. _held], []);
        }

        if (!frozen && _held.Count > 0)
        {
            var resume = _held.ToList();
            _held.Clear();
            return new HoldStep([], resume);
        }

        return new HoldStep([], []);
    }

    /// <summary>
    /// <paramref name="file"/> is stopped: it is no longer held, so leaving the stillness does not resume it. True if it
    /// was held - then the player must take it out of its pause before stopping it, since a stop does not reach a
    /// paused sound.
    /// </summary>
    public bool Stop(string file) => _held.Remove(file);
}

/// <summary>What to do with the held sounds in one frame (<see cref="HeldSounds.Frame"/>): files to pause, files to resume.</summary>
public readonly record struct HoldStep(IReadOnlyList<string> Pause, IReadOnlyList<string> Resume);
