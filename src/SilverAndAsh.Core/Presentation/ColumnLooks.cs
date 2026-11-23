using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// What the columns of one world look like (issue #601), learnt from its events: a <see cref="ColumnHit"/> picks the
/// stage by the blows taken, a <see cref="ColumnBroke"/> starts the fall. The side of the blows is turned towards whoever
/// struck the first one and stays so. Pure presentation, one per world: a restart starts a new one, and every column is
/// intact again.
/// </summary>
public sealed class ColumnLooks
{
    private readonly Dictionary<Column, Seen> _columns = [];

    private sealed class Seen
    {
        public int Hits;
        public int HitsToBreak = int.MaxValue;
        public float Yaw;
        public long? BrokeAt;
    }

    /// <summary>The columns that have broken, in the order they broke - the world leaves them out of its level.</summary>
    public IEnumerable<Column> Broken =>
        _columns.Where(pair => pair.Value.BrokeAt is not null).OrderBy(pair => pair.Value.BrokeAt).Select(pair => pair.Key);

    /// <summary>
    /// Takes in one event of the tick that brought the world to <paramref name="tick"/> ticks (<see cref="GameWorld.Ticks"/>
    /// after it). <paramref name="striker"/>: where whoever strikes columns stood (Gorm), null if unknown.
    /// </summary>
    public void OnEvent(GameEvent gameEvent, long tick, Vector3? striker)
    {
        switch (gameEvent)
        {
            case ColumnHit hit:
                var seen = SeenOf(hit.Column, striker);
                seen.Hits = Math.Max(seen.Hits, hit.Hits);
                seen.HitsToBreak = hit.HitsToBreak;
                break;
            case ColumnBroke broke:
                SeenOf(broke.Column, striker).BrokeAt ??= tick;
                break;
        }
    }

    /// <summary>How <paramref name="column"/> looks when the world stands at <paramref name="tick"/> ticks.</summary>
    public ColumnLook Of(Column column, long tick)
    {
        if (!_columns.TryGetValue(column, out var seen))
        {
            return ColumnLook.Intact;
        }

        if (seen.BrokeAt is { } brokeAt)
        {
            var since = (int)Math.Clamp(tick - brokeAt, 0, int.MaxValue);
            return new ColumnLook(ColumnBreakLook.StageAfterBreak(since), seen.Yaw, since);
        }

        return new ColumnLook(ColumnBreakLook.StageOf(seen.Hits, seen.HitsToBreak), seen.Yaw);
    }

    private Seen SeenOf(Column column, Vector3? striker)
    {
        if (!_columns.TryGetValue(column, out var seen))
        {
            seen = new Seen { Yaw = striker is { } where ? ColumnBreakLook.YawToward(column.Centre, where) : 0f };
            _columns[column] = seen;
        }

        return seen;
    }
}
