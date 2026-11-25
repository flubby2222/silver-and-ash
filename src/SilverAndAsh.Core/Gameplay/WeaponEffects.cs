using System.Collections.Frozen;
using System.Collections.ObjectModel;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Weapons with a status effect (issue #505, skills draft section 5): which found weapon carries which effect, and
/// whether a hit sets it off. It belongs to the world, like the loot pool: every weapon lies in a run only once, so its
/// effect is rolled once, when it is first found, and stays with it - also when a player drops it and takes it again.
/// <para>
/// A share of found weapons (<see cref="StatusEffectsBalance.WeaponShare"/>, the same for every tier) carries one effect
/// from <see cref="Choosable"/>, all equally likely. The weapons a run starts with never carry one. A hit sets the
/// effect off with the chance of the weapon's tier (<see cref="StatusEffectsBalance.WeaponHitChance"/>).
/// </para>
/// <para>
/// Both rolls have generators of their own, started from the world's seed (online co-op rule 3): finding and hitting
/// shift neither each other nor the loot, the silver or the pellets.
/// </para>
/// </summary>
public sealed class WeaponEffects
{
    /// <summary>The effects a found weapon can carry. A new effect is one more entry here (and in <see cref="Names"/>).</summary>
    public static ReadOnlyCollection<StatusKind> Choosable { get; } = Array.AsReadOnly([StatusKind.Marked, StatusKind.CleansingFlame]);

    /// <summary>The name of every effect as the game shows it.</summary>
    private static readonly FrozenDictionary<StatusKind, string> Names = new Dictionary<StatusKind, string>
    {
        [StatusKind.Marked] = "Marked",
        // Short for Cleansing Flame (issue #507, provisional): the full name does not fit the pickup toast's frame and the
        // inventory box behind a weapon's name ("Silver Blade - Cleansing Flame").
        [StatusKind.CleansingFlame] = "Flame",
    }.ToFrozenDictionary();

    private readonly StatusEffectsBalance _values;
    private readonly FrozenSet<WeaponKind> _startingWeapons;
    private readonly Random _findRandom;
    private readonly Random _hitRandom;
    private readonly Dictionary<WeaponKind, StatusKind?> _rolled = [];

    /// <summary>
    /// The effects of a run with the values <paramref name="values"/> and generators started from <paramref name="seed"/>.
    /// <paramref name="startingWeapons"/> (the weapons every player starts with) never carry an effect.
    /// </summary>
    public WeaponEffects(int seed, StatusEffectsBalance values, IEnumerable<WeaponKind> startingWeapons)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(startingWeapons);
        _values = values;
        _startingWeapons = startingWeapons.ToFrozenSet();
        _findRandom = new Random(unchecked((seed * 4513) ^ 0x3FFEC7));
        _hitRandom = new Random(unchecked((seed * 8191) ^ 0x2B1E57));
    }

    /// <summary>The effect weapon <paramref name="kind"/> carries in this run, or null.</summary>
    public StatusKind? Of(WeaponKind kind) => _rolled.GetValueOrDefault(kind);

    /// <summary>
    /// Rolls whether the just found weapon <paramref name="kind"/> carries an effect, and which - and keeps it. A weapon
    /// already rolled keeps what it had (it is the same weapon again). A starting weapon never gets one.
    /// </summary>
    public StatusKind? RollFind(WeaponKind kind)
    {
        if (_rolled.TryGetValue(kind, out var known))
        {
            return known;
        }

        // Both rolls come first and always, so whether a weapon is a starting one never changes how many numbers a find uses.
        var carries = _findRandom.NextDouble() < _values.WeaponShare;
        var which = Choosable[_findRandom.Next(Choosable.Count)];
        var effect = carries && !_startingWeapons.Contains(kind) ? which : (StatusKind?)null;
        _rolled[kind] = effect;
        return effect;
    }

    /// <summary>
    /// The effect that one hit of weapon <paramref name="kind"/> sets off, or null: a weapon without an effect never
    /// rolls; one with an effect rolls once with the chance of its tier.
    /// </summary>
    public StatusKind? RollHit(WeaponKind kind)
    {
        if (Of(kind) is not { } effect)
        {
            return null;
        }

        return _hitRandom.NextDouble() < _values.WeaponHitChance.Of(Weapons.Info(kind).Tier) ? effect : null;
    }

    /// <summary>The name of <paramref name="kind"/> as the game shows it ("Marked").</summary>
    public static string NameOf(StatusKind kind) => Names[kind];

    /// <summary>
    /// <paramref name="name"/> with <paramref name="effect"/> after it ("Club - Marked"), or the name alone. A plain
    /// hyphen, because the pixel font has no dash.
    /// </summary>
    public static string Label(string name, StatusKind? effect) =>
        effect is { } kind ? $"{name} - {NameOf(kind)}" : name;
}
