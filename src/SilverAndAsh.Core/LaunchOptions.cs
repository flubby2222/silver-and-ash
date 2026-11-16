using System.Globalization;
using System.Text.RegularExpressions;

namespace SilverAndAsh.Core;

/// <summary>
/// Options given when starting the game, e.g. <c>--level heights</c> or <c>--level generated --seed 123</c>.
/// </summary>
/// <param name="LevelName">Name of the level file in assets/levels/ without ".txt", or <see cref="GeneratedLevelName"/>.</param>
/// <param name="Seed">Seed chosen with <c>--seed</c>, or null for a new one every game.</param>
/// <param name="Give">
/// Weapons to start every run with, chosen with <c>--give club</c> (as often as wanted) - for trying weapons out
/// before they can be found. The game honours it only in its debug version.
/// </param>
/// <param name="Photo">
/// <c>--photo &lt;scene&gt;</c>: take the picture of this photo scene (or <see cref="AllPhotos"/>, or <see cref="ListPhotos"/>
/// to name them) and quit - for testers and designers (issue #224). Only in the debug version. Null: play.
/// </param>
/// <param name="PhotoFolder"><c>--out &lt;folder&gt;</c>: where the pictures go; null for the standard folder.</param>
public sealed partial record LaunchOptions(
    string LevelName, int? Seed = null, IReadOnlyList<Gameplay.WeaponKind>? Give = null, string? Photo = null, string? PhotoFolder = null)
{
    /// <summary><c>--photo all</c> takes every photo scene, one after the other.</summary>
    public const string AllPhotos = "all";

    /// <summary><c>--photo list</c> names the photo scenes instead of taking pictures.</summary>
    public const string ListPhotos = "list";

    /// <summary>Level that is played when no <c>--level</c> is given: a generated level 0.</summary>
    public const string DefaultLevelName = GeneratedLevelName;

    /// <summary>This "level name" builds a level from the room templates instead of loading a file.</summary>
    public const string GeneratedLevelName = "generated";

    /// <summary>True if the level is built by the level generator.</summary>
    public bool IsGenerated => LevelName == GeneratedLevelName;

    /// <summary>
    /// Reads the command line. Unknown options are errors - better a clear message than silently ignoring a typo.
    /// </summary>
    /// <exception cref="ArgumentException">If the command line is invalid; the message says what is wrong.</exception>
    public static LaunchOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var levelName = DefaultLevelName;
        int? seed = null;
        var give = new List<Gameplay.WeaponKind>();
        string? photo = null;
        string? photoFolder = null;

        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--level":
                    if (i + 1 >= args.Count)
                    {
                        throw new ArgumentException("'--level' needs a level name, e.g. '--level heights'.");
                    }

                    levelName = args[++i];

                    // Only simple names: no folders, no "..", so nothing outside assets/levels can be loaded.
                    if (!SafeLevelName().IsMatch(levelName))
                    {
                        throw new ArgumentException(
                            $"Invalid level name '{levelName}'. Allowed: lowercase letters, digits and '-'.");
                    }

                    // Windows treats some names as devices (e.g. "con" is the console), even with ".txt" behind them.
                    if (WindowsDeviceNames.Contains(levelName))
                    {
                        throw new ArgumentException($"'{levelName}' is a reserved name on Windows and cannot be a level.");
                    }

                    break;

                case "--seed":
                    if (i + 1 >= args.Count)
                    {
                        throw new ArgumentException("'--seed' needs a number, e.g. '--seed 123'.");
                    }

                    var text = args[++i];
                    if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
                    {
                        throw new ArgumentException($"Invalid seed '{text}'. A seed is a whole number, e.g. '--seed 123'.");
                    }

                    seed = number;
                    break;

                case "--give":
                    var known = string.Join(", ", Enum.GetNames<Gameplay.WeaponKind>()).ToLowerInvariant();
                    if (i + 1 >= args.Count)
                    {
                        throw new ArgumentException($"'--give' needs a weapon, e.g. '--give club'. Known: {known}.");
                    }

                    var name = args[++i];
                    if (!Enum.TryParse<Gameplay.WeaponKind>(name, ignoreCase: true, out var weapon) || int.TryParse(name, out _))
                    {
                        throw new ArgumentException($"Unknown weapon '{name}'. Known: {known}.");
                    }

                    give.Add(weapon);
                    break;

                case "--photo":
                    var scenes = string.Join(", ", Photos.PhotoScenes.All.Select(scene => scene.Name));
                    if (i + 1 >= args.Count)
                    {
                        throw new ArgumentException($"'--photo' needs a scene, '{AllPhotos}' or '{ListPhotos}'. Scenes: {scenes}.");
                    }

                    photo = args[++i];
                    if (photo is not (AllPhotos or ListPhotos) && Photos.PhotoScenes.Find(photo) is null)
                    {
                        throw new ArgumentException($"Unknown photo scene '{photo}'. Scenes: {scenes}.");
                    }

                    break;

                case "--out":
                    if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]))
                    {
                        throw new ArgumentException("'--out' needs a folder for the pictures, e.g. '--out photos'.");
                    }

                    photoFolder = args[++i];
                    break;

                default:
                    throw new ArgumentException(
                        $"Unknown option '{args[i]}'. Known: --level <name>, --seed <number>, --give <weapon>, --photo <scene>, --out <folder>.");
            }
        }

        if (photoFolder is not null && photo is null)
        {
            throw new ArgumentException("'--out' only goes with '--photo'.");
        }

        return new LaunchOptions(levelName, seed, give, photo, photoFolder);
    }

    /// <summary>The level file for these options inside <paramref name="levelDirectory"/>.</summary>
    public string LevelPath(string levelDirectory) => Path.Combine(levelDirectory, LevelName + ".txt");

    // \z = real end of the text. ($ would also accept a line break at the end.)
    [GeneratedRegex(@"^[a-z0-9-]+\z")]
    private static partial Regex SafeLevelName();

    private static readonly HashSet<string> WindowsDeviceNames =
    [
        "con", "prn", "aux", "nul",
        "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
        "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9",
    ];
}
