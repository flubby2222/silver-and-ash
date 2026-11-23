using System.Text.Json;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Issue #609: the colours of the environment's effects (dust of the column and of the cracked wall, spark and mark of a
/// bullet impact) and of the column's stand-in are steps of a ramp of the main palette, not colours of their own - and
/// never of the ramp Blutrot.
/// </summary>
public class EffectColoursTests
{
    public static TheoryData<string> Names() => [.. EffectColours.All.Select(entry => entry.Name)];

    [Theory]
    [MemberData(nameof(Names))]
    public void An_effect_colour_is_a_step_of_its_ramp(string name)
    {
        var (_, ramp, colour) = EffectColours.All.Single(entry => entry.Name == name);

        Assert.True(Ramp(ramp).Contains(colour), $"The {name} ({colour.R}, {colour.G}, {colour.B}) is no step of the ramp {ramp}.");
    }

    [Fact]
    public void Every_effect_colour_is_listed_once_and_none_is_red()
    {
        Assert.Contains(("wall break dust", EffectColours.Stone, EffectColours.WallBreakDust), EffectColours.All);
        Assert.Contains(("column stand-in", EffectColours.Stone, EffectColours.ColumnStandIn), EffectColours.All);
        Assert.Contains(("column stand-in edge", EffectColours.Stone, EffectColours.ColumnStandInEdge), EffectColours.All);
        Assert.Contains(("column dust", EffectColours.Stone, ColumnDust.Colour), EffectColours.All);
        Assert.Contains(("impact spark", EffectColours.Brass, EffectColours.ImpactSpark), EffectColours.All);
        Assert.Contains(("impact mark", EffectColours.Stone, EffectColours.ImpactMark), EffectColours.All);
        Assert.Equal(EffectColours.All.Count, EffectColours.All.Select(entry => entry.Name).Distinct().Count());

        var red = Ramp("Blutrot");
        Assert.All(EffectColours.All, entry => Assert.DoesNotContain(entry.Colour, red));
    }

    [Fact]
    public void The_colours_before_issue_609_are_in_no_ramp()
    {
        // Counter-proof: the wall's dust, the stand-in and the impact had colours of their own; the check has to find them.
        var stone = Ramp(EffectColours.Stone);
        var brass = Ramp(EffectColours.Brass);

        Assert.DoesNotContain(((byte)170, (byte)156, (byte)146), stone);
        Assert.DoesNotContain(((byte)112, (byte)106, (byte)98), stone);
        Assert.DoesNotContain(((byte)58, (byte)54, (byte)50), stone);
        Assert.DoesNotContain(((byte)255, (byte)236, (byte)190), brass);
        Assert.DoesNotContain(((byte)36, (byte)30, (byte)36), stone);
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

    // The steps of the ramp called name, from the ground or the accent ramps of the main palette.
    private static HashSet<(byte R, byte G, byte B)> Ramp(string name)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(RepositoryFiles.Find("assets/palette/hauptpalette.json")));
        var ramp = json.RootElement.GetProperty("grund").EnumerateArray()
            .Concat(json.RootElement.GetProperty("akzent").EnumerateArray())
            .Single(entry => entry.GetProperty("name").GetString() == name)
            .GetProperty("farben").EnumerateArray()
            .Select(colour => colour.EnumerateArray().Select(channel => (byte)channel.GetInt32()).ToArray())
            .Select(channels => (channels[0], channels[1], channels[2]))
            .ToHashSet();
        Assert.True(name == EffectColours.Stone ? ramp.Count == 15 : ramp.Count >= 4, $"The ramp {name} has {ramp.Count} steps.");
        return ramp;
    }
}
