using Raylib_cs;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Draws dust as a soft cloud (<see cref="DustCloud"/>): every puff as faint, see-through balls inside one another that do
/// not hide one another, lit, fogged and in the picture variant by the level shader like the stone around them (issue
/// #609). The one routine for all dust - of a breaking column and of a breaking wall (issue #614).
/// </summary>
internal static class DustCloudView
{
    /// <summary>Draws <paramref name="puffs"/> in <paramref name="colour"/>. Call between BeginFigures and EndFigures, after everything solid.</summary>
    public static void Draw(LevelShader shader, IReadOnlyCollection<DustPuff> puffs, Color colour)
    {
        if (puffs.Count == 0)
        {
            return;
        }

        using var seeThrough = shader.BeginSeeThrough();
        foreach (var puff in puffs)
        {
            foreach (var (scale, alpha) in DustCloud.Shells)
            {
                Raylib.DrawSphereEx(puff.Position, puff.Radius * scale, 6, 10, colour with { A = (byte)DustCloud.Alpha(puff, alpha) });
            }
        }
    }
}
