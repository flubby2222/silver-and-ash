using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// What a start of the game plays (issue #534): the worlds of a run and of the run after it, chosen by the start options.
/// Only what is played is loaded - <c>--room &lt;file&gt;</c> reads that one template and none of level 0, so a broken
/// template elsewhere never stops it.
/// </summary>
/// <param name="NewWorld">Builds a world from a seed and balancing values.</param>
/// <param name="NextWorld">The world after a run ended.</param>
/// <param name="Description">One line for the log: what is played.</param>
public sealed record StartWorlds(Func<int, Balance, GameWorld> NewWorld, Func<GameWorld, GameWorld> NextWorld, string Description)
{
    /// <summary>The room template played on its own (<c>--room</c>), or null.</summary>
    public RoomPreview? Preview { get; init; }

    /// <summary>
    /// True: the game opens on the title screen. A room preview (<c>--room</c>) starts in the room at once, so whoever
    /// looks at a template sees it without a detour (issue #534).
    /// </summary>
    public bool OpensOnTitle => Preview is null;

    /// <summary>
    /// The worlds for <paramref name="options"/>. <paramref name="baseDirectory"/> is the game's own folder, which holds
    /// a copy of assets/.
    /// </summary>
    /// <exception cref="LevelFormatException">If what is played is no valid level or template.</exception>
    /// <exception cref="FileNotFoundException">If the room template named by <c>--room</c> does not exist.</exception>
    public static StartWorlds For(LaunchOptions options, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        if (options.Room is { } room)
        {
            return OfRoom(room, baseDirectory);
        }

        return options.IsGenerated ? OfLevelZero(baseDirectory) : OfLevelFile(options, baseDirectory);
    }

    // --room <file>: that one template, entered from its first door slot (issue #528); --no-enemies leaves its enemies out.
    private static StartWorlds OfRoom(RoomOptions room, string baseDirectory)
    {
        var preview = new RoomPreview(RoomPreview.Load(room.Path, baseDirectory), room.WithEnemies);
        var enemies = room.WithEnemies ? string.Empty : ", without enemies";
        return new StartWorlds(preview.NewWorld, preview.NextWorld, $"Room template '{preview.Template.Name}' on its own{enemies}.")
        {
            Preview = preview,
        };
    }

    // No options: a generated level 0 - every run a new layout.
    private static StartWorlds OfLevelZero(string baseDirectory)
    {
        var templates = RoomTemplateParser.LoadAll(Path.Combine(baseDirectory, "assets", "rooms"));
        var levelZero = new LevelZero(templates);
        return new StartWorlds(levelZero.NewRun, levelZero.NextRun, $"Level 0 from {templates.Count} room templates.");
    }

    // --level test: one of the old hand-made levels.
    private static StartWorlds OfLevelFile(LaunchOptions options, string baseDirectory)
    {
        var levelPath = options.LevelPath(Path.Combine(baseDirectory, "assets", "levels"));
        var level = LevelParser.ParseFile(levelPath);
        return new StartWorlds(
            (seed, balance) => new GameWorld(level, seed, balance),
            world => world.Restarted(),
            $"Level '{levelPath}' loaded: {level.Width} x {level.Depth} cells.");
    }
}
