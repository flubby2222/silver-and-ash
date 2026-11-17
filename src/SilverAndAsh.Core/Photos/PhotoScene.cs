namespace SilverAndAsh.Core.Photos;

/// <summary>
/// One fixed picture of the game for testers and designers (<c>--photo &lt;name&gt;</c>, issue #224).
/// </summary>
/// <param name="Name">Name on the command line and of the image files, e.g. "ghoul-4m".</param>
/// <param name="Description">One line on what the picture should show (for <c>--photo list</c>).</param>
/// <param name="Build">Builds the scene in a <see cref="PhotoStudio"/>; the same studio always gives the same scene.</param>
public sealed record PhotoScene(string Name, string Description, Func<PhotoStudio, PhotoSetup> Build);
