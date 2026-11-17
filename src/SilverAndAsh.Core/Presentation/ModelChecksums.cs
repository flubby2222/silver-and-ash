using System.Globalization;
using System.Security.Cryptography;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The checked-in list of the model files (assets/models/models.sha256): for every file its size and SHA-256.
/// Before a model goes to raylib, the game compares the file on disk with the list. Raylib crashes in native code on
/// some broken files, where no catch helps - a damaged or half-copied file must never reach it. A file whose size and
/// checksum match is taken as whole: it is byte for byte the one in the repository.
/// <para>
/// One line per file: <c>sha256 size name</c>, separated by spaces, e.g.
/// <c>ee18…bbf6 58976 ghoul.glb</c>. Empty lines and lines starting with # are left out.
/// Written by tools/models/checksums.py.
/// </para>
/// </summary>
public sealed class ModelChecksums
{
    /// <summary>Name of the list next to the models.</summary>
    public const string FileName = "models.sha256";

    private readonly Dictionary<string, (long Size, string Sha256)> _entries;

    private ModelChecksums(Dictionary<string, (long Size, string Sha256)> entries) => _entries = entries;

    /// <summary>The file names in the list.</summary>
    public IReadOnlyCollection<string> Names => _entries.Keys;

    /// <summary>Reads the list.</summary>
    /// <exception cref="InvalidDataException">A line is not "sha256 size name", or a name is there twice.</exception>
    public static ModelChecksums Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var entries = new Dictionary<string, (long, string)>(StringComparer.Ordinal);
        var number = 0;
        foreach (var raw in text.Split('\n'))
        {
            number++;
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 3
                || fields[0].Length != 64 || !fields[0].All(char.IsAsciiHexDigit)
                || !long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var size))
            {
                throw new InvalidDataException($"Line {number} of the checksum list is not 'sha256 size name'.");
            }

            if (!entries.TryAdd(fields[2], (size, fields[0].ToLowerInvariant())))
            {
                throw new InvalidDataException($"The checksum list names '{fields[2]}' twice.");
            }
        }

        return new ModelChecksums(entries);
    }

    /// <summary>The line of the list for <paramref name="name"/> with this content.</summary>
    public static string LineFor(string name, ReadOnlySpan<byte> content) =>
        $"{Convert.ToHexStringLower(SHA256.HashData(content))} {content.Length} {name}";

    /// <summary>
    /// Compares the file at <paramref name="path"/> with its entry (by its file name). Returns what is wrong, or null
    /// when size and SHA-256 match.
    /// </summary>
    public string? Check(string path)
    {
        var name = Path.GetFileName(path);
        if (!_entries.TryGetValue(name, out var expected))
        {
            return $"'{name}' is not in the checksum list";
        }

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return $"'{name}' is missing";
            }

            if (info.Length != expected.Size)
            {
                return $"'{name}' is {info.Length} bytes, the checksum list says {expected.Size}";
            }

            using var stream = info.OpenRead();
            var actual = Convert.ToHexStringLower(SHA256.HashData(stream));
            return actual == expected.Sha256 ? null : $"the SHA-256 of '{name}' does not match the checksum list";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"'{name}' could not be read ({exception.Message})";
        }
    }
}
