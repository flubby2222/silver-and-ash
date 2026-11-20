using System.Globalization;
using System.Numerics;
using System.Text;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>The rooms of the findings for issue #535: how enemies deal with height differences today.</summary>
public enum HeightCase
{
    /// <summary>The player stands on a higher floor, one metre behind the edge; the enemy comes from below.</summary>
    StepUp,

    /// <summary>The enemy starts on the higher floor, the player stands below.</summary>
    StepDown,

    /// <summary>The player stands on a podium of one cell (2 x 2 m) in the middle of a flat room.</summary>
    Podium,

    /// <summary>A trench one cell wide across the whole room lies between player and enemy.</summary>
    Trench,

    /// <summary>The enemy starts in a pit of 3 x 3 cells, the player stands on the floor outside.</summary>
    InPit,

    /// <summary>Like <see cref="StepUp"/>, but a staircase of 0.25 m steps (one cell wide) leads up along one wall.</summary>
    StepUpWithStairs,
}

/// <summary>What one enemy did in one room, measured tick by tick.</summary>
public sealed record HeightObservation(
    HeightCase Case,
    float Height,
    string Enemy,
    int? FirstHitTick,
    int Hits,
    float MovedMetres,
    float EndDistance,
    float EndHeightAboveStart,
    int Reversals,
    float LastSecondMetres,
    int TicksInsideFloor,
    int DiveMisses,
    int Bites)
{
    public bool Reached => FirstHitTick is not null;

    public string Line() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Case,-17} {Height,5:0.00} {Enemy,-6} hit@{FirstHitTick?.ToString(CultureInfo.InvariantCulture) ?? "-",5} hits {Hits,2} moved {MovedMetres,6:0.0} end {EndDistance,5:0.0} dy {EndHeightAboveStart,5:0.00} rev {Reversals,3} last1s {LastSecondMetres,5:0.00} inFloor {TicksInsideFloor,4} misses {DiveMisses,2} bites {Bites,2}");
}

/// <summary>
/// Builds the test rooms of issue #535 as plain data and drives the real <see cref="GameWorld"/> tick by tick with
/// <see cref="Balance.Default"/>. The player stands still; the enemy is woken by a hit of 0 damage at the start.
/// </summary>
public static class HeightFindingsKit
{
    /// <summary>Room: 10 x 7 cells (20 x 14 m). Ceilings as in generated rooms.</summary>
    public const int Width = 10;

    public const int Depth = 7;

    public const float Ceiling = 6f;

    public static readonly (int X, int Z) PlayerCell = (3, 3);

    public static readonly (int X, int Z) EnemyCell = (8, 3);

    /// <summary>The pit of <see cref="HeightCase.InPit"/>: cells 7..9 x 2..4 - the enemy starts in its middle.</summary>
    public static readonly (int X, int Z) PitEnemyCell = (8, 3);

    public static float[,] Floors(HeightCase room, float height)
    {
        var floors = new float[Width, Depth];
        for (var x = 0; x < Width; x++)
        {
            for (var z = 0; z < Depth; z++)
            {
                floors[x, z] = room switch
                {
                    HeightCase.StepUp => x <= 3 ? height : 0f,
                    HeightCase.StepDown => x >= 6 ? height : 0f,
                    HeightCase.Podium => (x, z) == PlayerCell ? height : 0f,
                    HeightCase.Trench => x == 5 ? -height : 0f,
                    HeightCase.InPit => x >= 7 && z >= 2 && z <= 4 ? -height : 0f,
                    HeightCase.StepUpWithStairs => StairsFloor(x, z, height),
                    _ => throw new ArgumentOutOfRangeException(nameof(room)),
                };
            }
        }

        return floors;
    }

    // High side x <= 3; in row z = 0 a staircase of 0.25 m steps leads down from x = 4 towards the enemy.
    private static float StairsFloor(int x, int z, float height)
    {
        if (x <= 3)
        {
            return height;
        }

        if (z != 0)
        {
            return 0f;
        }

        var stepsDown = x - 3;
        return MathF.Max(0f, height - (stepsDown * 0.25f));
    }

    public static Level Build(HeightCase room, float height, bool bat)
    {
        var floors = Floors(room, height);
        var cells = new Cell[Width, Depth];
        for (var x = 0; x < Width; x++)
        {
            for (var z = 0; z < Depth; z++)
            {
                cells[x, z] = new Cell(false, floors[x, z], Ceiling);
            }
        }

        var enemy = room == HeightCase.InPit ? PitEnemyCell : EnemyCell;
        return new Level(
            cells, PlayerCell.X, PlayerCell.Z,
            ghoulCells: bat ? null : [enemy],
            batCells: bat ? [enemy] : null);
    }

    /// <summary>Runs the room for <paramref name="seconds"/> (or until the player dies) and measures the enemy.</summary>
    public static HeightObservation Observe(HeightCase room, float height, bool bat, int seconds)
    {
        var world = new GameWorld(Build(room, height, bat));
        var ticks = seconds * SimulationClock.TicksPerSecond;
        Func<Vector3> position = bat ? () => world.Bats[0].Position : () => world.Ghouls[0].Position;
        if (bat)
        {
            world.Bats[0].TakeDamage(0, []);
        }
        else
        {
            world.Ghouls[0].TakeDamage(0, []);
        }

        var start = position();
        var track = new List<Vector3> { start };
        int? firstHit = null;
        var hits = 0;
        var insideFloor = 0;
        var misses = 0;
        var bites = 0;
        for (var tick = 1; tick <= ticks && !world.Player.IsDead; tick++)
        {
            world.Tick(PlayerInput.None);
            var hitNow = world.Events.OfType<PlayerHit>().Count();
            if (hitNow > 0)
            {
                hits += hitNow;
                firstHit ??= tick;
            }

            misses += world.Events.OfType<BatDiveMissed>().Count();
            bites += world.Events.OfType<BatBit>().Count();
            var now = position();
            if (now.Y < world.Level.CellAt(now).FloorHeight - 0.001f)
            {
                insideFloor++;
            }

            track.Add(now);
        }

        var moved = 0f;
        for (var i = 1; i < track.Count; i++)
        {
            moved += Flat(track[i] - track[i - 1]);
        }

        var lastSecond = track.Skip(Math.Max(0, track.Count - SimulationClock.TicksPerSecond - 1)).ToList();
        var lastSecondMetres = 0f;
        for (var i = 1; i < lastSecond.Count; i++)
        {
            lastSecondMetres += Flat(lastSecond[i] - lastSecond[i - 1]);
        }

        var end = track[^1];
        return new HeightObservation(
            room, height, bat ? "bat" : "ghoul", firstHit, hits, moved, Flat(end - world.Player.Position),
            end.Y - start.Y, Reversals(track.Skip(Math.Max(0, track.Count - (2 * SimulationClock.TicksPerSecond))).ToList()),
            lastSecondMetres, insideFloor, misses, bites);
    }

    /// <summary>
    /// How often the flat movement turns back (direction changes by more than 120 degrees from one tick to the next)
    /// - a measure of jitter. Tiny moves (under 1 mm per tick) are ignored.
    /// </summary>
    public static int Reversals(IReadOnlyList<Vector3> track)
    {
        var count = 0;
        Vector2? previous = null;
        for (var i = 1; i < track.Count; i++)
        {
            var step = new Vector2(track[i].X - track[i - 1].X, track[i].Z - track[i - 1].Z);
            if (step.Length() < 0.001f)
            {
                continue;
            }

            var direction = Vector2.Normalize(step);
            if (previous is { } before && Vector2.Dot(before, direction) < -0.5f)
            {
                count++;
            }

            previous = direction;
        }

        return count;
    }

    public static string Table(IEnumerable<HeightObservation> observations)
    {
        var text = new StringBuilder();
        foreach (var observation in observations)
        {
            text.AppendLine(observation.Line());
        }

        return text.ToString();
    }

    private static float Flat(Vector3 v) => new Vector2(v.X, v.Z).Length();
}
