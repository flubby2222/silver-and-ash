namespace SilverAndAsh.Core;

/// <summary>
/// The start options for walking through one room template (issue #528): <c>--room &lt;file&gt;</c> chooses the template,
/// <c>--no-enemies</c> leaves its enemies out, and <c>--photo entrance</c> takes the picture from its doorway instead.
/// Read apart from the other options, before them (see <see cref="Extract"/>).
/// </summary>
/// <param name="Path">The template file, e.g. "assets/rooms/combat/l-hall.txt".</param>
/// <param name="WithEnemies">False with <c>--no-enemies</c>: the room stays empty of enemies, to look at it in peace.</param>
public sealed record RoomOptions(string Path, bool WithEnemies = true)
{
    /// <summary><c>--photo entrance</c> (only with <c>--room</c>) takes the picture from the room's doorway.</summary>
    public const string EntrancePhoto = "entrance";

    /// <summary>
    /// Takes <c>--room &lt;file&gt;</c> and <c>--no-enemies</c> out of <paramref name="args"/>. Returns the other
    /// arguments, in their order, and the room options - null if there is no <c>--room</c>.
    /// </summary>
    /// <exception cref="ArgumentException">If <c>--room</c> has no file, comes twice, or <c>--no-enemies</c> comes without it.</exception>
    public static (IReadOnlyList<string> Others, RoomOptions? Room) Extract(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var rest = new List<string>();
        string? path = null;
        var noEnemies = false;
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] == "--no-enemies")
            {
                noEnemies = true;
            }
            else if (args[i] != "--room")
            {
                rest.Add(args[i]);
            }
            else if (path is not null)
            {
                throw new ArgumentException("'--room' may come only once - one room at a time.");
            }
            else if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("'--room' needs a room template file, e.g. '--room assets/rooms/combat/l-hall.txt'.");
            }
            else
            {
                path = args[++i];
            }
        }

        if (noEnemies && path is null)
        {
            throw new ArgumentException("'--no-enemies' only goes with '--room <file>'.");
        }

        return (rest, path is null ? null : new RoomOptions(path, !noEnemies));
    }

    /// <summary>
    /// Checks that <paramref name="options"/> fit together with their room: a room is not a level, not the showroom, not
    /// a motion scene or bench; with a room the only photo is the entrance, and the entrance needs a room.
    /// </summary>
    /// <exception cref="ArgumentException">If they do not fit; the message says why.</exception>
    public static void Check(LaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Room is null)
        {
            if (options.Photo == EntrancePhoto)
            {
                throw new ArgumentException($"'--photo {EntrancePhoto}' needs a room: '--room <file> --photo {EntrancePhoto}'.");
            }

            return;
        }

        if (!options.IsGenerated || options.Showroom || options.Clip is not null || options.Bench is not null)
        {
            throw new ArgumentException("'--room' is a level of its own; it does not go with '--level', '--showroom', '--clip' or '--bench'.");
        }

        if (options.Photo is not null && options.Photo != EntrancePhoto)
        {
            throw new ArgumentException($"With '--room' the only photo is '--photo {EntrancePhoto}'.");
        }
    }
}
