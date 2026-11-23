using System.Text.RegularExpressions;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Issue #611: a restart (or reloaded values, or a photo scene) builds every view of the world anew, so no broken
/// column, flash, corpse or impact mark of the old run is left over. The views live in the game's
/// <c>Program.RunGame</c>, which no test can start without a window, so this reads its source: every view declared
/// next to the first world is made again in <c>StartFresh</c> the same way, and no other place swaps the world.
/// </summary>
public partial class RestartViewsTests
{
    private static readonly string Source = File.ReadAllText(RepositoryFiles.Find("src/SilverAndAsh.Game/Program.cs"));

    [Fact]
    public void StartFresh_makes_every_view_of_the_world_anew_as_the_first_world_got_it()
    {
        var views = ViewsOfTheFirstWorld();
        var startFresh = StartFreshBody();

        // The block is found and holds the views of issue #601 and its neighbours - else the test proves nothing.
        Assert.Contains("columnLooks", views.Keys);
        Assert.Contains("wallBreakView", views.Keys);
        Assert.Contains("ghoulView", views.Keys);

        var missing = views
            .Where(view => !startFresh.Contains($"{view.Key} = {view.Value};", StringComparison.Ordinal))
            .Select(view => $"{view.Key} = {view.Value};")
            .ToList();
        Assert.True(missing.Count == 0, $"StartFresh does not make anew: {string.Join(" ", missing)}");
    }

    [Fact]
    public void Only_StartFresh_swaps_the_world_so_every_new_world_gets_fresh_views()
    {
        var swaps = WorldSwap().Matches(Source).Count;
        var inStartFresh = WorldSwap().Matches(StartFreshBody()).Count;

        Assert.Equal(1, inStartFresh);
        Assert.Equal(inStartFresh, swaps);
    }

    // The views made for the first world: every local between the player and the input whose value is a new object or
    // comes from a factory (newGhoulView()), as name -> what it is made of. using-declarations (window resources that
    // last the whole game) and the factories themselves are not views of one world.
    private static Dictionary<string, string> ViewsOfTheFirstWorld()
    {
        var start = Source.IndexOf("var player = gameWorld.Player;", StringComparison.Ordinal);
        var end = Source.IndexOf("var input = new RaylibInput();", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "Program.cs no longer declares the views between the player and the input.");
        return ViewDeclaration().Matches(Source[start..end]).ToDictionary(match => match.Groups["name"].Value, match => match.Groups["made"].Value);
    }

    private static string StartFreshBody()
    {
        var start = Source.IndexOf("void StartFresh(GameWorld world)", StringComparison.Ordinal);
        Assert.True(start >= 0, "Program.cs has no StartFresh any more.");

        // From its opening brace to the brace that closes it.
        var open = Source.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < Source.Length; i++)
        {
            depth += Source[i] switch { '{' => 1, '}' => -1, _ => 0 };
            if (depth == 0)
            {
                return Source[open..(i + 1)];
            }
        }

        throw new InvalidOperationException("StartFresh is not closed.");
    }

    [GeneratedRegex(@"^[ \t]*(?:var|I\w+)[ \t]+(?<name>\w+)[ \t]*=[ \t]*(?<made>new \w+\([^;]*\)|\w+\(\));", RegexOptions.Multiline)]
    private static partial Regex ViewDeclaration();

    // An assignment to the world (not its declaration "var gameWorld = ...").
    [GeneratedRegex(@"^[ \t]*gameWorld[ \t]*=[^=]", RegexOptions.Multiline)]
    private static partial Regex WorldSwap();
}
