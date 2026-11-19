using System.Numerics;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// The door models in assets/models as the game draws them (issue #429: the head panel in the arch stood still while
/// the leaves opened).
/// </summary>
public class DoorModelTests
{
    private static readonly IReadOnlyList<RoomTemplate> Templates = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    // The middle of the passage, around the door plane: what a player walks through.
    private const float Doorway = 0.3f;

    [Theory]
    [InlineData(DoorKind.Normal)]
    [InlineData(DoorKind.Locked)]
    [InlineData(DoorKind.Boss)]
    public void Every_part_of_the_file_is_one_the_game_moves(DoorKind kind)
    {
        var file = GlbScene.Read(File.ReadAllBytes(Model(kind)));

        // A part the rig does not know would not be drawn at all; one the rig knows but does not turn would stand still.
        Assert.Equal(DoorPose.Parts(kind).Select(part => part.Name).Order(), file.Select(part => part.Name).Order());
    }

    [Theory]
    [InlineData(DoorKind.Normal)]
    [InlineData(DoorKind.Locked)]
    [InlineData(DoorKind.Boss)]
    public void Opened_by_a_player_the_whole_door_up_to_the_top_of_its_arch_leaves_the_doorway(DoorKind kind)
    {
        // The regular way: a player opens the door (F, a key for a locked one) and the world ticks until it is fully open.
        var studio = new PhotoStudio(Templates, Balance.Default);
        var world = studio.DoorHall(kind, open: true);
        var door = world.Doors[0];
        for (var tick = 0; tick < 600 && door.OpenFraction < 1f; tick++)
        {
            world.Tick(PlayerInput.None);
        }

        Assert.Equal(1f, door.OpenFraction);

        // Posed as DoorView poses it, in the file's space (origin: middle of the passage on the floor).
        var bytes = File.ReadAllBytes(Model(kind));
        var file = GlbScene.Read(bytes);
        var corners = GlbVertices.Read(bytes);
        var parts = DoorPose.Parts(kind);
        var rig = new Rig(parts, parts.Select(part => file.Single(p => p.Name == part.Name).Pivot).ToArray());
        var turns = new Matrix4x4[parts.Count];
        var frames = new Matrix4x4[parts.Count];
        DoorPose.Turns(kind, door.OpenFraction, door.OpenedFromLowSide, turns);
        rig.Pose(turns, Matrix4x4.Identity, frames);

        var highest = 0f;
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].Name == DoorPose.Lock && !DoorPose.ShowsLock(door.IsOpen))
            {
                continue;
            }

            foreach (var corner in corners[parts[i].Name])
            {
                var at = Vector3.Transform(corner, frames[i]);
                Assert.False(
                    MathF.Abs(at.X) < Doorway && MathF.Abs(at.Z) < Doorway,
                    $"{parts[i].Name} of the open {kind} door still stands in the doorway at {at}.");
                highest = MathF.Max(highest, at.Y);
            }
        }

        // The head panel up in the pointed arch (above 2.9 m) is still there - it swung open with the leaves.
        Assert.True(highest > 2.9f, $"The open {kind} door reaches only {highest} m high: its head panel is missing.");
    }

    private static string Model(DoorKind kind) => RepositoryFiles.Find($"assets/models/{DoorPose.FileName(kind)}");
}
