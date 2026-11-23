using System.Text.Json;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Issue #609: the colours of the environment's effects (dust of the column and of the cracked wall) and of the column's
/// stand-in are steps of the ramp "Stein" of the main palette, not colours of their own.
/// </summary>
public class EffectColoursTests
{
    public static TheoryData<string> Names() => [.. EffectColours.All.Select(entry => entry.Name)];

    [Theory]
    [MemberData(nameof(Names))]
    public void An_effect_colour_is_a_step_of_the_stone_ramp(string name)
    {
        var colour = EffectColours.All.Single(entry => entry.Name == name).Colour;

        Assert.True(StoneRamp().Contains(colour), $"The {name} ({colour.R}, {colour.G}, {colour.B}) is no step of the ramp Stein.");
    }

    [Fact]
    public void Every_effect_colour_is_listed_once()
    {
        Assert.Contains(("wall break dust", EffectColours.WallBreakDust), EffectColours.All);
        Assert.Contains(("column stand-in", EffectColours.ColumnStandIn), EffectColours.All);
        Assert.Contains(("column stand-in edge", EffectColours.ColumnStandInEdge), EffectColours.All);
        Assert.Contains(("column dust", ColumnDust.Colour), EffectColours.All);
        Assert.Equal(EffectColours.All.Count, EffectColours.All.Select(entry => entry.Name).Distinct().Count());
    }

    [Fact]
    public void The_colours_before_issue_609_are_not_in_the_stone_ramp()
    {
        // Counter-proof: the wall's dust and the stand-in had colours of their own; the check has to find them.
        var ramp = StoneRamp();

        Assert.DoesNotContain(((byte)170, (byte)156, (byte)146), ramp);
        Assert.DoesNotContain(((byte)112, (byte)106, (byte)98), ramp);
        Assert.DoesNotContain(((byte)58, (byte)54, (byte)50), ramp);
    }

    [Fact]
    public void The_level_shader_draws_see_through_only_when_asked_in_both_branches()
    {
        // Dust is drawn with the level shader (issue #609): it keeps the colour's alpha in variant 1 and in the picture
        // variants, but only with seeThrough - everything else stays solid, or the picture's black would show through.
        var compact = string.Concat(File.ReadAllText(RepositoryFiles.Find("assets/shaders/level.fs")).Where(c => !char.IsWhiteSpace(c)));

        Assert.Contains("floatalpha=seeThrough==1?fragColor.a*colDiffuse.a:1.0;", compact, StringComparison.Ordinal);
        Assert.Equal(2, compact.Split("finalColor=vec4(").Length - 1);
        Assert.Contains("finalColor=vec4(mix(lit,fogColor,fog),alpha);", compact, StringComparison.Ordinal);
        Assert.Contains("finalColor=vec4(color,alpha);", compact, StringComparison.Ordinal);
    }

    private static HashSet<(byte R, byte G, byte B)> StoneRamp()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(RepositoryFiles.Find("assets/palette/hauptpalette.json")));
        var ramp = json.RootElement.GetProperty("grund").EnumerateArray()
            .Single(entry => entry.GetProperty("name").GetString() == "Stein")
            .GetProperty("farben").EnumerateArray()
            .Select(colour => colour.EnumerateArray().Select(channel => (byte)channel.GetInt32()).ToArray())
            .Select(channels => (channels[0], channels[1], channels[2]))
            .ToHashSet();
        Assert.Equal(15, ramp.Count);
        return ramp;
    }
}
