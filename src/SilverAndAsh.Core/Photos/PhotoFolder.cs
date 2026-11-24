namespace SilverAndAsh.Core.Photos;

/// <summary>
/// The folder of <c>--photo ... --out &lt;folder&gt;</c> must be empty or not there yet (issue #639): pictures of an
/// earlier run lying next to the new ones are easily compared with them by mistake. The standard folder (no
/// <c>--out</c>) is not checked; there every scene simply overwrites its own two files.
/// </summary>
public static class PhotoFolder
{
    /// <summary>Throws if <paramref name="folder"/> exists and holds anything (a file or a folder).</summary>
    /// <exception cref="PhotoFolderNotEmptyException">The folder is not empty; the message names it and what to do.</exception>
    public static void EnsureEmpty(string folder)
    {
        if (!Directory.Exists(folder) || !Directory.EnumerateFileSystemEntries(folder).Any())
        {
            return;
        }

        throw new PhotoFolderNotEmptyException(
            $"The folder for the pictures is not empty: '{folder}'. Pictures of an earlier run would lie next to the new ones - " +
            "choose a new folder with '--out' or empty this one first.");
    }
}
