namespace SilverAndAsh.Core.Gameplay;

/// <summary>The kinds of status effect an enemy can carry (issue #504). A new kind is a new entry here.</summary>
public enum StatusKind
{
    /// <summary>Marked: the enemy takes more damage from every hit while it lasts (<see cref="MarkedBonus"/>).</summary>
    Marked,
}

/// <summary>
/// One status effect an enemy carries: how many ticks are left, which player put it on, and whether a synergy made it
/// (not used yet - the skills that set it come later).
/// </summary>
public readonly record struct StatusEffect(int TicksLeft, int SourcePlayerId, bool Synergy);

/// <summary>
/// The status effects one enemy carries (issue #504). Every kind at most once; different kinds live side by side. The
/// world counts them down once per tick; an effect that runs out is gone and reported.
/// </summary>
public sealed class StatusEffects
{
    private readonly Dictionary<StatusKind, StatusEffect> _effects = [];

    /// <summary>True while an effect of this kind lasts.</summary>
    public bool Has(StatusKind kind) => _effects.ContainsKey(kind);

    /// <summary>The effect of this kind, or null if there is none.</summary>
    public StatusEffect? Get(StatusKind kind) => _effects.TryGetValue(kind, out var effect) ? effect : null;

    /// <summary>
    /// Every effect that lasts, in the order of the kinds - the same order on every machine (online co-op). For the
    /// display above an enemy (issue #506).
    /// </summary>
    public IEnumerable<(StatusKind Kind, StatusEffect Effect)> InOrder() =>
        _effects.OrderBy(entry => entry.Key).Select(entry => (entry.Key, entry.Value));

    /// <summary>
    /// Puts on an effect of <paramref name="kind"/> for <paramref name="ticks"/> ticks. If one of this kind already
    /// lasts, it is refreshed: it then lasts the longer of what is left and the new duration - a shorter one never cuts
    /// it short, and the strength stays the same (there is only one). The application that gives the longer time keeps
    /// the source and the synergy; on a tie the old one stays.
    /// </summary>
    public void Apply(StatusKind kind, int ticks, int sourcePlayerId, bool synergy = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticks);
        if (_effects.TryGetValue(kind, out var current) && current.TicksLeft >= ticks)
        {
            return;
        }

        _effects[kind] = new StatusEffect(ticks, sourcePlayerId, synergy);
    }

    /// <summary>
    /// One tick has passed: every effect has a tick less. One that reaches zero is gone and reports
    /// <see cref="StatusEnded"/> for the enemy <paramref name="targetId"/>.
    /// </summary>
    public void Tick(int targetId, ICollection<GameEvent> events)
    {
        if (_effects.Count == 0)
        {
            return;
        }

        // In the order of the kinds, so the events come in the same order every run (online co-op: every machine alike).
        foreach (var kind in _effects.Keys.Order().ToList())
        {
            var effect = _effects[kind];
            if (effect.TicksLeft > 1)
            {
                _effects[kind] = effect with { TicksLeft = effect.TicksLeft - 1 };
                continue;
            }

            _effects.Remove(kind);
            events.Add(new StatusEnded(targetId, kind));
        }
    }
}
