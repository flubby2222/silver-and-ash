namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// The equipment still to be found in a run (decision records 0006 and 0008): every weapon and item lies in it once,
/// and whatever is drawn leaves it - so there are no doubles. It belongs to the world, not to a player: in co-op all
/// players find from the same pool.
/// <para>
/// A draw asks for a least tier (a chest's own tier). With a small chance it rises one tier; then it is an item or a
/// weapon by their shares (<see cref="LootBalance"/>). If that kind is used up in the tier, the other kind of the same
/// tier is taken; if the whole tier is used up, the next higher one; if everything from there up is used up, there is
/// nothing - the caller then gives extra silver instead.
/// </para>
/// <para>
/// All chance comes from the pool's own generator, started from the world's seed (online co-op rule 3): the same seed
/// gives the same loot, and a draw never shifts what silver, keys or fights roll.
/// </para>
/// </summary>
public sealed class LootPool
{
    private readonly List<Equipment> _left;
    private readonly LootBalance _values;
    private readonly Random _random;

    /// <summary>
    /// A full pool with the values <paramref name="values"/> and its generator started from <paramref name="seed"/>,
    /// without <paramref name="alreadyInRun"/> (e.g. the weapons every player starts with).
    /// </summary>
    public LootPool(int seed, LootBalance values, IEnumerable<Equipment>? alreadyInRun = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = values;
        _random = new Random(unchecked((seed * 6007) ^ 0x7EA5_0194));
        _left = [.. Equipment.All];
        foreach (var taken in alreadyInRun ?? [])
        {
            Take(taken);
        }
    }

    /// <summary>The equipment still in the pool, in the order of <see cref="Equipment.All"/>.</summary>
    public IReadOnlyList<Equipment> Left => _left.AsReadOnly();

    /// <summary>True while <paramref name="equipment"/> can still be drawn.</summary>
    public bool Contains(Equipment equipment) => _left.Contains(equipment);

    /// <summary>
    /// Draws one piece of at least <paramref name="leastTier"/> and takes it out of the pool - or returns null if
    /// nothing from that tier up is left (the caller gives extra silver then).
    /// </summary>
    public Equipment? Draw(ItemTier leastTier)
    {
        // Both rolls come first and always, so how full the pool is never changes how many numbers a draw uses.
        var tier = leastTier;
        if (_random.NextDouble() < _values.HigherTierChance && tier < ItemTier.Vampiric)
        {
            tier++;
        }

        var wantItem = _random.NextDouble() < _values.ItemShare;
        for (; tier <= ItemTier.Vampiric; tier++)
        {
            var wanted = InTier(tier, wantItem);
            var choice = wanted.Count > 0 ? wanted : InTier(tier, !wantItem);
            if (choice.Count > 0)
            {
                var drawn = choice[_random.Next(choice.Count)];
                _left.Remove(drawn);
                return drawn;
            }
        }

        return null;
    }

    /// <summary>
    /// Takes <paramref name="equipment"/> out of the pool without chance (the weapons a run starts with; later the
    /// merchant's wares, which chests and the boss never give). Returns false if it was not in the pool.
    /// </summary>
    public bool Take(Equipment equipment)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        return _left.Remove(equipment);
    }

    /// <summary>
    /// Puts <paramref name="equipment"/> back into the pool, so it can be found again - the Ashen Heart once it broke
    /// (decision record 0008). Returns false if it was still in the pool.
    /// </summary>
    public bool PutBack(Equipment equipment)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        if (_left.Contains(equipment))
        {
            return false;
        }

        // Back at its place in the list, so the order (and with it every later draw) only depends on what is left.
        var place = _left.Count(left => IndexOf(left) < IndexOf(equipment));
        _left.Insert(place, equipment);
        return true;
    }

    private List<Equipment> InTier(ItemTier tier, bool items) =>
        _left.Where(left => left.Tier == tier && left.IsWeapon != items).ToList();

    private static int IndexOf(Equipment equipment)
    {
        for (var i = 0; i < Equipment.All.Count; i++)
        {
            if (Equipment.All[i] == equipment)
            {
                return i;
            }
        }

        throw new ArgumentException($"{equipment} is no equipment of the game.", nameof(equipment));
    }
}
