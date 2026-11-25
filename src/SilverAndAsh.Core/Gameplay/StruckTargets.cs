namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Every target one swing or one shot struck, once each and in the order it was first struck (issue #505: a weapon's
/// effect rolls once per target, also when several pellets of a blunderbuss shot hit it), with the damage it took from
/// all of that swing or shot together (issue #510: what Leech takes its share of).
/// </summary>
internal sealed class StruckTargets
{
    private readonly List<ITarget> _targets = [];
    private readonly Dictionary<ITarget, int> _damage = new(ReferenceEqualityComparer.Instance);

    /// <summary>The targets, in the order they were first struck.</summary>
    public IReadOnlyList<ITarget> Targets => _targets;

    /// <summary><paramref name="target"/> took <paramref name="damage"/> more from this swing or shot.</summary>
    public void Add(ITarget target, int damage)
    {
        if (_damage.TryGetValue(target, out var before))
        {
            _damage[target] = before + damage;
            return;
        }

        _targets.Add(target);
        _damage[target] = damage;
    }

    /// <summary>All the damage <paramref name="target"/> took from this swing or shot (0 if it was not struck).</summary>
    public int DamageTo(ITarget target) => _damage.GetValueOrDefault(target);
}
