using System.Globalization;

namespace SilverAndAsh.Core.Diagnostics;

/// <summary>
/// One enemy standing still without an attack while it hunts a player (bot mode <c>stuck</c>, issue #591): its kind,
/// the seed, the place (e.g. "room 4 at the door"), its id, how many ticks so far, and from how many it hangs.
/// </summary>
public sealed record StillSpell(string Kind, int Seed, string Place, int EnemyId, int Ticks, int Threshold)
{
    /// <summary>It has stood still for the threshold or longer: a finding.</summary>
    public bool Hangs => Ticks >= Threshold;
}

/// <summary>
/// Over a whole bot run, per kind of enemy: the longest still spell that stayed below the threshold, with its seed,
/// enemy and place (issue #699), so a kind that comes close to hanging can be replayed without searching the seeds.
/// </summary>
public sealed class LongestStillSpells
{
    private readonly Dictionary<string, StillSpell> _longest = new(StringComparer.Ordinal);

    /// <summary>
    /// Takes a spell below the threshold. Of two equally long spells the first one stays, so the lowest seed of a run
    /// is named.
    /// </summary>
    /// <exception cref="ArgumentException">If the spell hangs - that is a finding, not a candidate for the longest.</exception>
    public void Record(StillSpell spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        if (spell.Hangs)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"{spell.Kind} {spell.EnemyId} (seed {spell.Seed}, {spell.Place}) stood still {spell.Ticks} ticks and hangs from {spell.Threshold}: a finding, not a candidate for the longest spell."),
                nameof(spell));
        }

        if (!_longest.TryGetValue(spell.Kind, out var longest) || spell.Ticks > longest.Ticks)
        {
            _longest[spell.Kind] = spell;
        }
    }

    /// <summary>The longest spell of <paramref name="kind"/> recorded so far, or null.</summary>
    public StillSpell? Longest(string kind) => _longest.GetValueOrDefault(kind);

    /// <summary>
    /// The line the bot prints for <paramref name="kind"/> after the last seed, e.g. "stuck: ghoul: 412 watched, longest
    /// still without an attack 188 ticks (ghoul 17, seed 11, room 4 at the door), hangs from 192 ticks (4 short)".
    /// </summary>
    public string Line(string kind, int watched, int threshold) => Longest(kind) is { } longest
        ? string.Create(CultureInfo.InvariantCulture, $"stuck: {kind}: {watched} watched, longest still without an attack {longest.Ticks} ticks ({longest.Kind} {longest.EnemyId}, seed {longest.Seed}, {longest.Place}), hangs from {threshold} ticks ({threshold - longest.Ticks} short)")
        : string.Create(CultureInfo.InvariantCulture, $"stuck: {kind}: {watched} watched, longest still without an attack 0 ticks, hangs from {threshold} ticks");
}
