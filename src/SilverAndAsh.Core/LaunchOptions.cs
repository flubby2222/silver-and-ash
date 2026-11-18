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
/// <param name="Look">
/// <c>--look &lt;1-4&gt;</c>: draw the world in this picture variant (issue #234, decision record 0010 - an open question
/// to be decided with pictures). Only in the debug version. Null: the standard picture (variant 2, issue #237); 1 is the picture without grain.
/// </param>
/// <param name="Bench">
/// <c>--bench &lt;scene&gt;</c>: build this photo scene, draw it a fixed number of times without VSync and print the frame
/// times (issue #233) - to see whether a new model or texture costs frame rate. Only in the debug version. Null: play.
/// </param>
/// <param name="Clip">
/// <c>--clip &lt;scene&gt;</c>: film this motion scene into numbered pictures (or <see cref="AllPhotos"/>, or
/// <see cref="ListPhotos"/> to name them) and quit (issue #286). Only in the debug version. Null: play.
/// </param>
/// <param name="Showroom">
/// <c>--showroom</c>: walk through the showroom instead of playing - every door, chest, enemy and loot side by side
/// in every wall variant (issue #283, docs/musterraum.md). Only in the debug version.
/// </param>
/// <param name="Window">
/// <c>--window &lt;width&gt;x&lt;height&gt;</c>: open the window in this size instead of 1280 x 720 - for photos and tries
/// in other window shapes, e.g. 21:9 (issue #350). Only in the debug version. Null: the standard size.
/// </param>
public sealed partial record LaunchOptions(
    string LevelName, int? Seed = null, IReadOnlyList<Gameplay.WeaponKind>? Give = null, string? Photo = null, string? PhotoFolder = null,
    int? Look = null, string? Bench = null, string? Clip = null, bool Showroom = false, WindowSize? Window = null)
{
    /// <summary>Smallest window <c>--window</c> accepts, in pixels per side.</summary>
    public const int SmallestWindowSide = 200;

    /// <summary>Largest window <c>--window</c> accepts, in pixels per side (8K).</summary>
    public const int LargestWindowSide = 7680;

    /// <summary><c>--photo compare</c> takes the comparison scenes in every picture variant, plus one 2 x 2 sheet per scene.</summary>
    public const string ComparePhotos = "compare";

    /// <summary>Number of picture variants <c>--look</c> can choose (1 is the game as it is).</summary>
    public const int LookVariantCount = 4;

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
        int? look = null;
        string? bench = null;
        string? clip = null;
        var showroom = false;
        WindowSize? window = null;
        var levelOrSeed = false;

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
                    levelOrSeed = true;

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
                    levelOrSeed = true;
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
                        throw new ArgumentException(
                            $"'--photo' needs a scene, '{AllPhotos}', '{ComparePhotos}' or '{ListPhotos}'. Scenes: {scenes}.");
                    }

                    photo = args[++i];
                    if (photo is not (AllPhotos or ListPhotos or ComparePhotos) && Photos.PhotoScenes.Find(photo) is null)
                    {
                        throw new ArgumentException($"Unknown photo scene '{photo}'. Scenes: {scenes}.");
                    }

                    break;

                case "--bench":
                    var benchScenes = string.Join(", ", Photos.PhotoScenes.All.Select(scene => scene.Name));
                    if (i + 1 >= args.Count || Photos.PhotoScenes.Find(args[i + 1]) is null)
                    {
                        throw new ArgumentException($"'--bench' needs one photo scene, e.g. '--bench room-combat'. Scenes: {benchScenes}.");
                    }

                    bench = args[++i];
                    break;

                case "--clip":
                    var clips = string.Join(", ", Photos.MotionScenes.All.Select(scene => scene.Name));
                    if (i + 1 >= args.Count)
                    {
                        throw new ArgumentException($"'--clip' needs a motion scene, '{AllPhotos}' or '{ListPhotos}'. Scenes: {clips}.");
                    }

                    clip = args[++i];
                    if (clip is not (AllPhotos or ListPhotos) && Photos.MotionScenes.Find(clip) is null)
                    {
                        throw new ArgumentException($"Unknown motion scene '{clip}'. Scenes: {clips}.");
                    }

                    break;

                case "--out":
                    if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]))
                    {
                        throw new ArgumentException("'--out' needs a folder for the pictures, e.g. '--out photos'.");
                    }

                    photoFolder = args[++i];
                    break;

                case "--look":
                    if (i + 1 >= args.Count
                        || !int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var variant)
                        || variant is < 1 or > LookVariantCount)
                    {
                        throw new ArgumentException($"'--look' needs a picture variant from 1 to {LookVariantCount}, e.g. '--look 3'.");
                    }

                    look = variant;
                    i++;
                    break;

                case "--showroom":
                    showroom = true;
                    break;

                case "--window":
                    window = ParseWindowSize(i + 1 < args.Count ? args[i + 1] : null);
                    i++;
                    break;

                default:
                    throw new ArgumentException(
                        $"Unknown option '{args[i]}'. Known: --level <name>, --seed <number>, --give <weapon>, --photo <scene>, --bench <scene>, --clip <scene>, --out <folder>, --look <1-4>, --showroom, --window <width>x<height>.");
            }
        }

        if (photoFolder is not null && photo is null && clip is null)
        {
            throw new ArgumentException("'--out' only goes with '--photo' or '--clip'.");
        }

        if (clip is not null && (photo is not null || bench is not null))
        {
            throw new ArgumentException("'--clip' cannot be combined with '--photo' or '--bench' - one after the other.");
        }

        if (bench is not null && photo is not null)
        {
            throw new ArgumentException("'--bench' and '--photo' cannot be combined - measure and take pictures one after the other.");
        }

        if (showroom && (levelOrSeed || give.Count > 0 || photo is not null || bench is not null || clip is not null))
        {
            throw new ArgumentException(
                "'--showroom' has its own room and every weapon; only '--look' goes with it. Its pictures: '--photo showroom-a-corridor' and the like.");
        }

        if (look is not null && photo == ComparePhotos)
        {
            throw new ArgumentException($"'--photo {ComparePhotos}' takes every picture variant; leave out '--look'.");
        }

        return new LaunchOptions(levelName, seed, give, photo, photoFolder, look, bench, clip, showroom, window);
    }

    private static WindowSize ParseWindowSize(string? text)
    {
        var error = new ArgumentException(
            $"'--window' needs a size like '2520x1080', each side from {SmallestWindowSide} to {LargestWindowSide} pixels.");
        var match = text is null ? null : WindowSizeText().Match(text);
        if (match is not { Success: true }
            || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var width)
            || !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var height)
            || width is < SmallestWindowSide or > LargestWindowSide
            || height is < SmallestWindowSide or > LargestWindowSide)
        {
            throw error;
        }

        return new WindowSize(width, height);
    }

    [GeneratedRegex(@"^([0-9]{1,5})x([0-9]{1,5})\z")]
    private static partial Regex WindowSizeText();

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

/// <summary>A window size in pixels, chosen with <c>--window</c> (issue #350).</summary>
public readonly record struct WindowSize(int Width, int Height);
