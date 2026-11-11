using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Draws a <see cref="Level"/>. The visible faces (from <see cref="LevelGeometry"/>) are turned into ONE mesh
/// with a colour per corner (vertex colours) when the level is loaded. Each face gets a brightness depending
/// on the direction it faces (flat shading) - that is what makes corners and edges visible.
/// </summary>
internal sealed class LevelRenderer : IDisposable
{
    private Model _model;

    /// <param name="level">The level to draw.</param>
    /// <param name="shader">Shader for lantern light and fog. The renderer uses it but does not own it.</param>
    public LevelRenderer(Level level, Shader shader)
    {
        var mesh = BuildMesh(LevelGeometry.Build(level));

        // Copies the data to the graphics card. From then on drawing costs almost nothing.
        Raylib.UploadMesh(ref mesh, false);
        _model = Raylib.LoadModelFromMesh(mesh);

        // The model's material decides HOW it is drawn - here: with our light-and-fog shader.
        // (UnloadModel does not unload shaders, so LevelShader stays responsible for it.)
        _model.MaterialsAsSpan()[0].Shader = shader;
    }

    public void Draw() => Raylib.DrawModel(_model, Vector3.Zero, 1f, Color.White);

    public void Dispose() => Raylib.UnloadModel(_model);

    private static Mesh BuildMesh(IReadOnlyList<Face> faces)
    {
        // Each face = 4 corners (vertices) and 2 triangles. Indices say which corners form a triangle,
        // so corners shared by both triangles are stored only once.
        var vertexCount = faces.Count * 4;
        if (vertexCount > ushort.MaxValue)
        {
            throw new InvalidOperationException(
                $"The level is too large for one mesh ({vertexCount} vertices, at most {ushort.MaxValue}).");
        }

        var mesh = new Mesh(vertexCount, faces.Count * 2);
        mesh.AllocVertices();
        mesh.AllocNormals();
        mesh.AllocColors();
        mesh.AllocIndices();

        var vertices = mesh.VerticesAs<Vector3>();
        var normals = mesh.NormalsAs<Vector3>();
        var colors = mesh.ColorsAs<Color>();
        var indices = mesh.IndicesAs<ushort>();

        for (var i = 0; i < faces.Count; i++)
        {
            var face = faces[i];
            var color = Shade(face);
            var v = i * 4;

            vertices[v] = face.A;
            vertices[v + 1] = face.B;
            vertices[v + 2] = face.C;
            vertices[v + 3] = face.D;

            for (var corner = 0; corner < 4; corner++)
            {
                normals[v + corner] = face.Normal;
                colors[v + corner] = color;
            }

            // Two triangles: A-B-C and A-C-D (both counter-clockwise, like the face itself).
            var t = i * 6;
            indices[t] = (ushort)v;
            indices[t + 1] = (ushort)(v + 1);
            indices[t + 2] = (ushort)(v + 2);
            indices[t + 3] = (ushort)v;
            indices[t + 4] = (ushort)(v + 2);
            indices[t + 5] = (ushort)(v + 3);
        }

        return mesh;
    }

    /// <summary>Base colour of the surface times the brightness of its facing direction.</summary>
    private static Color Shade(Face face)
    {
        var (baseColor, brightness) = face.Kind switch
        {
            SurfaceKind.Floor => (LookSettings.FloorColor, LookSettings.FloorBrightness),
            SurfaceKind.Ceiling => (LookSettings.CeilingColor, LookSettings.CeilingBrightness),
            _ => (LookSettings.WallColor, FlatShading.WallBrightness(face.Normal)),
        };

        return new Color(
            (byte)(baseColor.R * brightness),
            (byte)(baseColor.G * brightness),
            (byte)(baseColor.B * brightness),
            (byte)255);
    }
}
