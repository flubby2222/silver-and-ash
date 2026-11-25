namespace SilverAndAsh.Core.Tests;

/// <summary>
/// A folder for room templates below the test output (never the system temp folder), deleted afterwards.
/// <c>new TemplateFolder("combat")</c> holds templates of one kind; <c>new TemplateFolder("assets/rooms/combat")</c> is a
/// game folder (<see cref="Root"/>) with a copy of assets/rooms.
/// </summary>
internal sealed class TemplateFolder : IDisposable
{
    public TemplateFolder(string kind)
    {
        Root = Path.Combine(AppContext.BaseDirectory, "room-preview-tests", Guid.NewGuid().ToString("N"));
        Kind = Path.Combine(Root, kind);
        Directory.CreateDirectory(Kind);
    }

    /// <summary>The whole folder, above <see cref="Kind"/>.</summary>
    public string Root { get; }

    /// <summary>The folder the templates go into.</summary>
    public string Kind { get; }

    /// <summary>Writes <paramref name="text"/> as the template <paramref name="name"/> and returns its full path.</summary>
    public string Write(string name, string text)
    {
        var file = Path.Combine(Kind, name);
        File.WriteAllText(file, text);
        return file;
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
