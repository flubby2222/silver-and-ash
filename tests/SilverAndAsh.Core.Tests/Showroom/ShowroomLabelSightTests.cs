using System.Numerics;
using SilverAndAsh.Core.Showroom;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Showroom;

public class ShowroomLabelSightTests
{
    private static readonly ShowroomLayout Layout = ShowroomLayout.Build();

    private static Vector3 EyeAt(int x, int z) => Layout.Level.CellCenter(x, z) + new Vector3(0f, 1.6f, 0f);

    [Fact]
    public void The_labels_of_the_own_part_are_seen()
    {
        var section = Layout.Sections[0];
        var eye = EyeAt(section.CorridorX, section.SouthRow);

        // Every label of this part near enough to be shown at all.
        var own = Layout.Labels.Where(label =>
            Level.ToGrid(label.Position.X, label.Position.Z) is var (x, z)
            && x >= section.CorridorX && x < section.CorridorX + section.Length
            && z >= section.DoorRow && z <= section.SouthRow
            && Vector3.Distance(label.Position, eye) <= ShowroomLabelSight.MaxDistance).ToList();
        Assert.True(own.Count >= 2, $"Only {own.Count} labels near enough - the test would prove little.");
        foreach (var label in own)
        {
            Assert.True(ShowroomLabelSight.Sees(Layout.Level, eye, label.Position), label.Text);
        }
    }

    [Fact]
    public void Labels_behind_a_wall_or_too_far_away_are_not_seen()
    {
        var a = Layout.Sections[0];
        var b = Layout.Sections[1];
        var eye = EyeAt(a.CorridorX + 2, a.SouthRow);

        // Straight south, behind the wall between section A and section B.
        var behindWall = Layout.Level.CellCenter(a.CorridorX + 2, b.NorthRow) + new Vector3(0f, 1.6f, 0f);
        Assert.True(Vector3.Distance(eye, behindWall) <= ShowroomLabelSight.MaxDistance);
        Assert.False(ShowroomLabelSight.Sees(Layout.Level, eye, behindWall));

        var farAway = eye + new Vector3(ShowroomLabelSight.MaxDistance + 1f, 0f, 0f);
        Assert.False(ShowroomLabelSight.Sees(Layout.Level, eye, farAway));
    }
}
