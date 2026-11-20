using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

public class WellOfSiriusLookTests
{
    private const float Tolerance = 0.001f;

    [Fact]
    public void Ready_and_spent_have_their_own_file()
    {
        Assert.Equal("well_of_sirius_bereit.glb", WellOfSiriusLook.FileName(spent: false));
        Assert.Equal("well_of_sirius_verbraucht.glb", WellOfSiriusLook.FileName(spent: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Every_file_has_exactly_its_parts_standing_on_the_floor_inside_the_well(bool spent)
    {
        var path = RepositoryFiles.Find($"assets/models/{WellOfSiriusLook.FileName(spent)}");
        Assert.True(File.Exists(path), $"{path} is missing.");
        var parts = GlbScene.Read(File.ReadAllBytes(path)).ToDictionary(part => part.Name);

        Assert.Equal(WellOfSiriusLook.Parts(spent).Select(part => part.Name).Order(), parts.Keys.Order());
        Assert.All(WellOfSiriusLook.Parts(spent), part => Assert.Null(part.Parent));
        foreach (var part in parts.Values)
        {
            // Round its middle, never higher than the well, within the cell (1 m each way from its middle).
            Assert.Equal(Vector3.Zero, part.Pivot);
            Assert.InRange(part.PositionMin.Y, -Tolerance, WellOfSirius.Height);
            Assert.InRange(part.PositionMax.Y, 0f, WellOfSirius.Height + Tolerance);
            Assert.InRange(part.PositionMin.X, -1f, 0f);
            Assert.InRange(part.PositionMax.X, 0f, 1f);
            Assert.InRange(part.PositionMin.Z, -1f, 0f);
            Assert.InRange(part.PositionMax.Z, 0f, 1f);
        }

        Assert.Equal(0f, parts[WellOfSiriusLook.Body].PositionMin.Y, Tolerance);
    }

    [Fact]
    public void The_spent_well_has_no_water()
    {
        Assert.DoesNotContain(WellOfSiriusLook.Parts(spent: true), part => WellOfSiriusLook.Flows(part.Name));
        Assert.Equal(4, WellOfSiriusLook.Parts(spent: false).Count(part => WellOfSiriusLook.Flows(part.Name)));
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(0.149f, 0)]
    [InlineData(0.151f, 1)]
    [InlineData(0.31f, 2)]
    [InlineData(0.46f, 3)]
    [InlineData(0.61f, 0)]
    [InlineData(1.06f, 3)]
    public void The_water_goes_one_frame_on_every_150_ms_and_starts_again_after_four(float seconds, int frame)
    {
        Assert.Equal(frame, WellOfSiriusLook.Frame(seconds));
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Before_any_time_or_with_no_real_time_the_water_shows_frame_0(float seconds)
    {
        Assert.Equal(0, WellOfSiriusLook.Frame(seconds));
    }

    [Fact]
    public void In_the_game_loop_the_frames_follow_each_other_once_per_150_ms()
    {
        // As the game counts: seconds from ticks of 1/60 s. Nine ticks make 150 ms.
        var frames = Enumerable.Range(0, 37).Select(tick => WellOfSiriusLook.Frame(tick / (float)SimulationClock.TicksPerSecond)).ToList();

        // Each frame lasts nine ticks, then the next; after frame 3 comes frame 0.
        var changes = Enumerable.Range(1, frames.Count - 1).Where(tick => frames[tick] != frames[tick - 1]).ToList();
        Assert.Equal([9, 18, 27, 36], changes);
        Assert.Equal([0, 1, 2, 3, 0], changes.Prepend(0).Select(tick => frames[tick]));
    }

    [Theory]
    [InlineData(WellOfSiriusLook.WaterBasin, 1, 0.25f)]
    [InlineData(WellOfSiriusLook.WaterBowl1, 2, 0.5f)]
    [InlineData(WellOfSiriusLook.WaterBowl2, 3, 0.75f)]
    [InlineData(WellOfSiriusLook.Falls, 1, 0.125f)]
    [InlineData(WellOfSiriusLook.Falls, 3, 0.375f)]
    [InlineData(WellOfSiriusLook.WaterBasin, 0, 0f)]
    public void Water_moves_16_px_and_the_falls_8_px_of_the_64_px_texture_per_frame(string part, int frame, float u)
    {
        Assert.Equal(new Vector2(u, 0f), WellOfSiriusLook.UvOffset(part, frame));
    }

    [Theory]
    [InlineData(WellOfSiriusLook.Body)]
    [InlineData(WellOfSiriusLook.Column)]
    public void Stone_never_moves_its_texture(string part)
    {
        Assert.Equal(Vector2.Zero, WellOfSiriusLook.UvOffset(part, 3));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void A_frame_outside_0_to_3_is_refused(int frame)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WellOfSiriusLook.UvOffset(WellOfSiriusLook.Falls, frame));
    }
}
