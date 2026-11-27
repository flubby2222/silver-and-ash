using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.BotRun;

/// <summary>
/// What the fight bot has seen so far (issue #809) - the state of one run, kept by its <see cref="FightBot"/>. It holds
/// only what a player at the screen has: the enemies in plain sight (a clear line from the eyes to the middle of the
/// body, the same line as <see cref="LineOfSight"/> draws for the shot), the last spot where he saw each enemy, the keys
/// he has seen, and how far he has searched a room. Where an enemy stands out of sight, the bot does not read.
/// </summary>
internal sealed class FightMemory(GameWorld world)
{
    // An enemy outside the bot's room is seen from this far at most.
    private const float SightRange = 12f;

    // The bot has come to a remembered spot or a search cell when it is this close (m, flat).
    private const float ArriveDistance = 1f;

    // The bot looks round the room (marks the cells it sees as searched) this often (ticks).
    private const int LookEvery = 10;

    // A search cell is given up when the bot has not come to it in this many ticks.
    private const int SearchPatience = 300;

    // The height above the floor at which a cell, a key or a vial is looked at.
    private const float LookHeight = 0.9f;

    private readonly Dictionary<ITarget, Vector3> _lastSeen = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<int> _keysSeen = [];
    private readonly Dictionary<int, HashSet<(int X, int Z)>> _searched = [];
    private (int X, int Z)? _searchCell;
    private int _searchRoom = -1;
    private long _searchSince;

    /// <summary>The enemies in sight this tick that can be hurt, the nearest first.</summary>
    public IReadOnlyList<ITarget> Seen { get; private set; } = [];

    /// <summary>Looks round: who is in sight now, where they were seen, which keys are in sight. Once per tick.</summary>
    public void Look(Player player, RoomState room)
    {
        var inSight = world.Targets
            .Where(each => each is Ghoul or Bat or Gorm)
            .Where(each => IsInReach(player, room, each) && Sees(player, each))
            .ToList();
        foreach (var enemy in inSight)
        {
            // A corpse in sight is a dead enemy: nothing to go back to.
            if (enemy.IsHittable)
            {
                _lastSeen[enemy] = enemy.Body.Position;
            }
            else
            {
                _lastSeen.Remove(enemy);
            }
        }

        Seen = [.. inSight.Where(each => each.IsHittable).OrderBy(each => DistanceTo(player, each))];
        ForgetSpotsReached(player);
        foreach (var key in world.Loot.Where(each => each.Kind == LootKind.Key && !each.IsTaken && Sees(player, each.Position)))
        {
            _keysSeen.Add(key.Id);
        }
    }

    /// <summary>True when the bot has seen this key (it lies in sight now, or did once).</summary>
    public bool KnowsKey(Loot key) => _keysSeen.Contains(key.Id);

    /// <summary>The nearest spot where an enemy was last seen, or null. The bot calls it only with no enemy in sight.</summary>
    public Vector3? LastSeenSpot(Player player) => _lastSeen.Values
        .OrderBy(spot => Play.Distance(player.Position, spot))
        .Cast<Vector3?>()
        .FirstOrDefault();

    /// <summary>
    /// Where to look next in an arena that is not cleared yet (the bot knows that much: its doors are shut), or null in
    /// any other room. The fixed rule: the middle of the room first, then the cell nearest to the bot of those it has
    /// neither seen nor come to. Cells it cannot walk to count as searched. When every cell is searched, it starts over.
    /// </summary>
    public Vector3? SearchPoint(Player player, RoomState room)
    {
        if (!room.IsArena || room.IsCleared)
        {
            return null;
        }

        if (!_searched.TryGetValue(room.Index, out var done))
        {
            done = _searched[room.Index] = [];
        }

        if (world.Ticks % LookEvery == 0)
        {
            MarkSeenCells(player, room, done);
        }

        if (_searchRoom != room.Index)
        {
            _searchRoom = room.Index;
            _searchCell = null;
        }

        var here = Play.Cell(player.Position);
        if (_searchCell is { } current && !done.Contains(current)
            && Play.Distance(player.Position, CellPoint(current)) > ArriveDistance
            && world.Ticks - _searchSince < SearchPatience)
        {
            return CellPoint(current);
        }

        if (_searchCell is { } finished)
        {
            done.Add(finished);
        }

        _searchCell = NextCell(room, done, here) ?? RestartedCell(room, done, here);
        _searchSince = world.Ticks;
        return _searchCell is { } next ? CellPoint(next) : null;
    }

    // Forgets a remembered spot the bot stands at: the enemy is not there any more (or it would be in sight).
    private void ForgetSpotsReached(Player player)
    {
        var reached = _lastSeen
            .Where(each => !Seen.Contains(each.Key) && Play.Distance(player.Position, each.Value) < ArriveDistance)
            .Select(each => each.Key)
            .ToList();
        foreach (var enemy in reached)
        {
            _lastSeen.Remove(enemy);
        }
    }

    // The cells of the room in plain sight from the eyes now count as searched: an enemy standing there would be seen.
    private void MarkSeenCells(Player player, RoomState room, HashSet<(int X, int Z)> done)
    {
        foreach (var cell in room.Cells.Where(each => !done.Contains(each)).ToList())
        {
            if (Sees(player, CellPoint(cell)))
            {
                done.Add(cell);
            }
        }
    }

    private (int X, int Z)? NextCell(RoomState room, HashSet<(int X, int Z)> done, (int X, int Z) here)
    {
        var middle = MiddleCell(room);
        var open = room.Cells.Where(each => !done.Contains(each))
            .OrderBy(each => each == middle ? 0 : 1)
            .ThenBy(each => Play.Distance(CellPoint(each), CellPoint(here)))
            .ThenBy(each => each.X)
            .ThenBy(each => each.Z);
        foreach (var cell in open)
        {
            if (Pathfinder.FindPath(world.Level, here, cell) is not null)
            {
                return cell;
            }

            done.Add(cell);
        }

        return null;
    }

    private (int X, int Z)? RestartedCell(RoomState room, HashSet<(int X, int Z)> done, (int X, int Z) here)
    {
        done.Clear();
        return NextCell(room, done, here);
    }

    // The cell of the room nearest to the mean of its cells.
    private static (int X, int Z) MiddleCell(RoomState room)
    {
        var meanX = room.Cells.Average(each => (float)each.X);
        var meanZ = room.Cells.Average(each => (float)each.Z);
        return room.Cells
            .OrderBy(each => ((each.X - meanX) * (each.X - meanX)) + ((each.Z - meanZ) * (each.Z - meanZ)))
            .ThenBy(each => each.X)
            .ThenBy(each => each.Z)
            .First();
    }

    private Vector3 CellPoint((int X, int Z) cell) => world.Level.CellCenter(cell.X, cell.Z);

    // The enemies of the room the bot stands in, or close by.
    private static bool IsInReach(Player player, RoomState room, ITarget enemy) =>
        room.Cells.Contains(Play.Cell(enemy.Body.Position)) || DistanceTo(player, enemy) <= SightRange;

    private static float DistanceTo(Player player, ITarget target) =>
        MathF.Max(0f, Play.Distance(player.Position, target.Body.Position) - target.Body.Radius);

    // The enemy's middle can be seen from the eyes: nothing solid in between (a bat may hover over a pedestal).
    private bool Sees(Player player, ITarget enemy) => LineOfSight.IsClear(
        world.Level, player.EyePosition, enemy.Body.Position + new Vector3(0f, enemy.Body.Height / 2f, 0f));

    private bool Sees(Player player, Vector3 floorPoint) => LineOfSight.IsClear(
        world.Level, player.EyePosition, floorPoint + new Vector3(0f, LookHeight, 0f));
}
