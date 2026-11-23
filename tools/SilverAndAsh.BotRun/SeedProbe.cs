using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.BotRun;

/// <summary>
/// Mode "seeds": builds the level of every seed twice (<see cref="LevelZero.Build"/>, as a new run does) and compares
/// both - the same seed must give the same level, cell by cell, with the same rooms, doors, enemies, chests, teleporters
/// and well. A seed that cannot be built is a finding. It also counts how many tries the generator needed per seed.
/// </summary>
internal static class SeedProbe
{
    private static string? _attemptsUnavailable;

    public static void Run(LevelZero zero, int seed, Report report)
    {
        GeneratedLevel first;
        try
        {
            first = zero.Build(seed, Play.Balance);
        }
        catch (InvalidOperationException e)
        {
            report.Finding($"seed {seed}: no level built: {e.Message}");
            return;
        }

        report.Count("levels built");
        var a = Fingerprint(first);
        var b = Fingerprint(zero.Build(seed, Play.Balance));
        if (a != b)
        {
            report.Finding($"seed {seed}: two builds differ ({FirstDifference(a, b)})");
        }

        if (_attemptsUnavailable is null)
        {
            try
            {
                var (tries, level) = GeneratorTries.Count(zero, seed);
                report.Count($"generator tries {tries,2}");
                if (Fingerprint(level) != Fingerprint(LevelGenerator.Generate(GeneratorTries.Templates(zero), seed, Play.Balance.Layout)))
                {
                    _attemptsUnavailable = "the counted level differs from LevelGenerator.Generate";
                }
            }
            catch (Exception e) when (e is MissingCoreMember or TargetInvocationException or InvalidCastException)
            {
                _attemptsUnavailable = e.Message;
            }
        }
    }

    /// <summary>The generator tries are counted through its private parts; says so if that no longer works.</summary>
    public static void PrintAttemptNote(Report report)
    {
        if (_attemptsUnavailable is not null)
        {
            Console.Error.WriteLine($"note: generator tries not counted - {_attemptsUnavailable}");
            report.Count("generator tries NOT counted (see note)");
        }
    }

    /// <summary>Everything a generated level is made of, as text: equal text = equal level.</summary>
    public static string Fingerprint(GeneratedLevel level)
    {
        var text = new StringBuilder();
        var map = level.Level;
        text.Append(CultureInfo.InvariantCulture, $"seed {level.Seed} size {map.Width}x{map.Depth} start {map.PlayerStartX},{map.PlayerStartZ}\n");
        for (var z = 0; z < map.Depth; z++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var cell = map[x, z];
                text.Append(CultureInfo.InvariantCulture, $"{(cell.IsSolid ? '#' : '.')}{cell.FloorHeight:R}/{cell.CeilingHeight:R} ");
            }

            text.Append('\n');
        }

        foreach (var room in level.Rooms)
        {
            text.Append(CultureInfo.InvariantCulture, $"room {room.Index} {room.Kind} {room.TemplateName} {room.X},{room.Z} {room.Width}x{room.Depth} d{room.DistanceFromStart}")
                .Append(" floor ").AppendJoin(';', room.FloorCells)
                .Append(" ghouls ").AppendJoin(';', room.GhoulSlots)
                .Append(" bats ").AppendJoin(';', room.BatSlots)
                .Append(" chests ").AppendJoin(';', room.ChestSlots ?? [])
                .Append(" boss ").AppendJoin(';', room.BossSlots ?? [])
                .Append('\n');
        }

        text.AppendJoin('\n', level.Doors).Append('\n');
        text.AppendJoin('\n', level.Chests ?? []).Append('\n');
        text.AppendJoin('\n', level.Teleporters ?? []).Append('\n');
        text.Append(level.Well).Append('\n');
        text.Append("ghouls ").AppendJoin(';', map.GhoulPositions).Append('\n');
        text.Append("bats ").AppendJoin(';', map.BatPositions).Append('\n');
        text.Append("boss ").AppendJoin(';', map.BossPositions).Append('\n');
        text.Append("columns ").AppendJoin(';', map.Columns).Append('\n');
        text.Append("cutouts ").AppendJoin(';', map.WallCutouts).Append('\n');
        return text.ToString();
    }

    private static string FirstDifference(string a, string b)
    {
        var linesA = a.Split('\n');
        var linesB = b.Split('\n');
        for (var i = 0; i < Math.Min(linesA.Length, linesB.Length); i++)
        {
            if (linesA[i] != linesB[i])
            {
                return $"line {i + 1}: '{Cut(linesA[i])}' vs '{Cut(linesB[i])}'";
            }
        }

        return $"{linesA.Length} vs {linesB.Length} lines";
    }

    private static string Cut(string line) => line.Length <= 120 ? line : line[..120] + "...";

    /// <summary>
    /// Counts the tries of <see cref="LevelGenerator.Generate"/> by doing what it does with its private parts (the
    /// builder, the room variants). Only a probe: the caller checks that the level it gets is the one Generate builds.
    /// </summary>
    private static class GeneratorTries
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;

        public static IReadOnlyList<RoomTemplate> Templates(LevelZero zero) =>
            typeof(LevelZero).GetField("_templates", Hidden)?.GetValue(zero) as IReadOnlyList<RoomTemplate>
            ?? throw new MissingCoreMember("LevelZero._templates");

        public static (int Tries, GeneratedLevel Level) Count(LevelZero zero, int seed)
        {
            var generator = typeof(LevelGenerator);
            var variant = generator.GetNestedType("Variant", Hidden) ?? throw new MissingCoreMember("LevelGenerator.Variant");
            var builder = generator.GetNestedType("Builder", Hidden) ?? throw new MissingCoreMember("LevelGenerator.Builder");
            var allOf = variant.GetMethod("AllOf", Hidden) ?? throw new MissingCoreMember("LevelGenerator.Variant.AllOf");
            var tryBuild = builder.GetMethod("TryBuild", Hidden) ?? throw new MissingCoreMember("LevelGenerator.Builder.TryBuild");
            var toLevel = builder.GetMethod("ToLevel", Hidden) ?? throw new MissingCoreMember("LevelGenerator.Builder.ToLevel");
            var maxTries = generator.GetField("MaxTries", Hidden)?.GetRawConstantValue() as int? ?? throw new MissingCoreMember("LevelGenerator.MaxTries");

            // the catalog exactly as Generate makes it: variants grouped by kind, in template order, plus the antechamber
            var listType = typeof(List<>).MakeGenericType(variant);
            var catalog = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(RoomKind), listType))!;
            IList Variants(IEnumerable<RoomTemplate> templates)
            {
                var list = (IList)Activator.CreateInstance(listType)!;
                foreach (var template in templates)
                {
                    foreach (var one in (IEnumerable)allOf.Invoke(null, [template])!)
                    {
                        list.Add(one);
                    }
                }

                return list;
            }

            foreach (var group in Templates(zero).GroupBy(template => template.Kind))
            {
                catalog[group.Key] = Variants(group);
            }

            catalog[RoomKind.Antechamber] = Variants([Antechamber.Template]);
            var random = new Random(seed);
            for (var attempt = 1; attempt <= maxTries; attempt++)
            {
                var build = Activator.CreateInstance(builder, Hidden, null, [catalog, random, Play.Balance.Layout], CultureInfo.InvariantCulture)!;
                if ((bool)tryBuild.Invoke(build, null)!)
                {
                    return (attempt, (GeneratedLevel)toLevel.Invoke(build, [seed])!);
                }
            }

            throw new InvalidOperationException($"no level within {maxTries} tries");
        }
    }
}
