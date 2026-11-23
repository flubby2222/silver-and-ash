using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// How much the free-standing columns of a level have taken (issue #590): every blow of Gorm (chain swing or shock wave)
/// that strikes a column counts once per attack; after <see cref="HitsToBreak"/> blows it breaks. Part of the game
/// state of one world - the level itself stays unchanged; the world leaves the broken columns out of the level it
/// plays in.
/// </summary>
public sealed class ColumnWear
{
    private readonly Dictionary<Column, int> _hits = [];
    private readonly HashSet<Column> _broken = [];

    /// <summary>Creates the wear of a level whose columns break after <paramref name="hitsToBreak"/> blows (at least 1).</summary>
    public ColumnWear(int hitsToBreak)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(hitsToBreak, 1);
        HitsToBreak = hitsToBreak;
    }

    /// <summary>Blows a column takes before it breaks (<see cref="ColumnsBalance.HitsToBreak"/>).</summary>
    public int HitsToBreak { get; }

    /// <summary>The columns that have broken.</summary>
    public IReadOnlyCollection<Column> Broken => _broken;

    /// <summary>Blows <paramref name="column"/> has taken so far.</summary>
    public int HitsOn(Column column) => _hits.GetValueOrDefault(column);

    /// <summary>True once <paramref name="column"/> has broken.</summary>
    public bool IsBroken(Column column) => _broken.Contains(column);

    /// <summary>
    /// One blow on <paramref name="column"/>: it counts, reports <see cref="ColumnHit"/> and - with its last blow -
    /// breaks and reports <see cref="ColumnBroke"/>. A broken column takes no more blows. <paramref name="height"/>
    /// is the world height the blow struck at (where it is heard).
    /// </summary>
    public void Strike(Column column, float height, ICollection<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (IsBroken(column))
        {
            return;
        }

        var hits = HitsOn(column) + 1;
        _hits[column] = hits;
        var place = new Vector3(column.X, height, column.Z);
        events.Add(new ColumnHit(column, hits, HitsToBreak) { Place = place });
        if (hits >= HitsToBreak)
        {
            _broken.Add(column);
            events.Add(new ColumnBroke(column) { Place = place });
        }
    }
}
