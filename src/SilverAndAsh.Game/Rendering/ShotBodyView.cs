using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Physics;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Test display for photos (issue #433): the bodies a shot can hit (<see cref="ITarget.ShotBodies"/>) as wire cylinders
/// over the figures - bright where they are in front of the figure, faint where it hides them - to check that they fit
/// the models. Never in a normal game.
/// </summary>
internal static class ShotBodyView
{
    private const int Slices = 16;

    private static readonly Color Wire = new(90, 255, 120, 255);
    private static readonly Color HiddenWire = new(90, 255, 120, 70);

    /// <summary>Draws the shot bodies of all targets that can be hit. Call between BeginMode3D and EndMode3D, last.</summary>
    public static void Draw(IEnumerable<ITarget> targets)
    {
        var bodies = targets.Where(target => target.IsHittable).SelectMany(target => target.ShotBodies).ToList();

        // First faint, through everything; then bright, only where nothing stands in front.
        Rlgl.DrawRenderBatchActive();
        Rlgl.DisableDepthTest();
        DrawAll(bodies, HiddenWire);
        Rlgl.DrawRenderBatchActive();
        Rlgl.EnableDepthTest();
        DrawAll(bodies, Wire);
        Rlgl.DrawRenderBatchActive();
    }

    private static void DrawAll(IEnumerable<CylinderBody> bodies, Color color)
    {
        foreach (var body in bodies)
        {
            Raylib.DrawCylinderWires(body.Position, body.Radius, body.Radius, body.Height, Slices, color);
        }
    }
}
