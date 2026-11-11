using System.Numerics;

namespace SilverAndAsh.Core.World;

/// <summary>
/// Finds a way through a level's grid of cells - for enemies that chase the player around walls and corners.
/// Uses A* ("A star"), the standard path search in games. Pure logic, no enemy-specific code: the ghoul uses it now,
/// other enemies can later.
/// </summary>
public static class Pathfinder
{
    /// <summary>
    /// The cells from <paramref name="start"/> to <paramref name="goal"/> (both included), or null if there is no way.
    /// A step to a neighbour (also diagonal) is allowed if the neighbour is not a wall and its floor is at most
    /// <paramref name="stepHeight"/> higher; going down is always allowed (into a pit, yes - out of it only by steps).
    /// Diagonal steps never cut across a wall's corner.
    /// </summary>
    public static IReadOnlyList<(int X, int Z)>? FindPath(
        Level level, (int X, int Z) start, (int X, int Z) goal, float stepHeight = Tuning.Player.StepHeight)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (!IsWalkable(level, start) || !IsWalkable(level, goal))
        {
            return null;
        }

        // A*: always continue with the cell whose "way so far + estimated rest" is smallest. The estimate (octile
        // distance) never overestimates, so the first time the goal is taken from the queue, its way is the shortest.
        var open = new PriorityQueue<(int X, int Z), float>();
        var cameFrom = new Dictionary<(int X, int Z), (int X, int Z)>();
        var costSoFar = new Dictionary<(int X, int Z), float> { [start] = 0f };
        open.Enqueue(start, Estimate(start, goal));

        while (open.TryDequeue(out var current, out _))
        {
            if (current == goal)
            {
                return Rebuild(cameFrom, start, goal);
            }

            foreach (var (next, stepCost) in Neighbours(level, current, stepHeight))
            {
                var cost = costSoFar[current] + stepCost;
                if (!costSoFar.TryGetValue(next, out var known) || cost < known)
                {
                    costSoFar[next] = cost;
                    cameFrom[next] = current;
                    open.Enqueue(next, cost + Estimate(next, goal));
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Turns a cell path into the points to walk to: instead of stepping from cell centre to cell centre, it goes
    /// straight to the farthest point of the path that can be reached in a straight line by a body of
    /// <paramref name="radius"/>. Returns the world positions to walk to (the start itself is not included).
    /// </summary>
    public static IReadOnlyList<Vector3> Smooth(
        Level level, IReadOnlyList<(int X, int Z)> path, float radius, float stepHeight = Tuning.Player.StepHeight)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(path);
        if (path.Count == 0)
        {
            return [];
        }

        if (path.Count == 1)
        {
            return [level.CellCenter(path[0].X, path[0].Z)];
        }

        // "String pulling": from where we are, look for the farthest cell of the path we can walk to in a straight line.
        var points = new List<Vector3>();
        var from = 0;
        while (from < path.Count - 1)
        {
            var origin = level.CellCenter(path[from].X, path[from].Z);
            var to = from + 1;
            for (var candidate = path.Count - 1; candidate > from + 1; candidate--)
            {
                if (CanWalkStraight(level, origin, level.CellCenter(path[candidate].X, path[candidate].Z), radius, stepHeight))
                {
                    to = candidate;
                    break;
                }
            }

            points.Add(level.CellCenter(path[to].X, path[to].Z));
            from = to;
        }

        return points;
    }

    /// <summary>
    /// True if a body of <paramref name="radius"/> can walk in a straight line from <paramref name="from"/> to
    /// <paramref name="to"/>: no wall in the way and never a floor rising more than <paramref name="stepHeight"/>.
    /// </summary>
    public static bool CanWalkStraight(
        Level level, Vector3 from, Vector3 to, float radius, float stepHeight = Tuning.Player.StepHeight)
    {
        ArgumentNullException.ThrowIfNull(level);
        const float SampleStep = 0.1f;
        var flat = new Vector2(to.X - from.X, to.Z - from.Z);
        var samples = Math.Max(1, (int)MathF.Ceiling(flat.Length() / SampleStep));
        var floor = level.CellAt(from).FloorHeight;

        // The body's outline is checked at its middle and the four corners of a square around it.
        ReadOnlySpan<Vector3> corners =
        [
            Vector3.Zero, new(radius, 0f, radius), new(radius, 0f, -radius), new(-radius, 0f, radius), new(-radius, 0f, -radius),
        ];

        for (var i = 0; i <= samples; i++)
        {
            var point = Vector3.Lerp(from, to, (float)i / samples);
            foreach (var corner in corners)
            {
                var cell = level.CellAt(point + corner);
                if (cell.IsSolid || cell.FloorHeight - floor > stepHeight)
                {
                    return false;
                }
            }

            floor = level.CellAt(point).FloorHeight;
        }

        return true;
    }

    private static bool IsWalkable(Level level, (int X, int Z) cell) =>
        level.IsInside(cell.X, cell.Z) && !level[cell.X, cell.Z].IsSolid;

    /// <summary>A neighbour can be entered if it is walkable and its floor is not too high above this one.</summary>
    private static bool CanStep(Level level, (int X, int Z) from, (int X, int Z) to, float stepHeight) =>
        IsWalkable(level, to) && level[to.X, to.Z].FloorHeight - level[from.X, from.Z].FloorHeight <= stepHeight;

    /// <summary>The eight neighbours that can be entered, with the length of the step (1 straight, about 1.41 diagonal).</summary>
    private static IEnumerable<((int X, int Z) Cell, float Cost)> Neighbours(Level level, (int X, int Z) cell, float stepHeight)
    {
        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0)
                {
                    continue;
                }

                var next = (cell.X + dx, cell.Z + dz);
                if (!CanStep(level, cell, next, stepHeight))
                {
                    continue;
                }

                if (dx != 0 && dz != 0)
                {
                    // Diagonal: both straight neighbours on the way must be enterable too - no cutting across a corner.
                    if (!CanStep(level, cell, (cell.X + dx, cell.Z), stepHeight) || !CanStep(level, cell, (cell.X, cell.Z + dz), stepHeight))
                    {
                        continue;
                    }

                    yield return (next, MathF.Sqrt(2f));
                }
                else
                {
                    yield return (next, 1f);
                }
            }
        }
    }

    /// <summary>Octile distance: straight steps plus diagonal steps, as if there were no walls.</summary>
    private static float Estimate((int X, int Z) from, (int X, int Z) to)
    {
        var dx = Math.Abs(from.X - to.X);
        var dz = Math.Abs(from.Z - to.Z);
        return Math.Max(dx, dz) + (MathF.Sqrt(2f) - 1f) * Math.Min(dx, dz);
    }

    private static List<(int X, int Z)> Rebuild(
        Dictionary<(int X, int Z), (int X, int Z)> cameFrom, (int X, int Z) start, (int X, int Z) goal)
    {
        var path = new List<(int X, int Z)> { goal };
        var current = goal;
        while (current != start)
        {
            current = cameFrom[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}
