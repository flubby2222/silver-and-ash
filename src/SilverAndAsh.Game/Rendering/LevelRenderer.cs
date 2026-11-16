using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Draws a <see cref="Level"/>. The visible faces (from <see cref="LevelGeometry"/>) are turned into one mesh per kind of
/// surface (walls, floors, ceilings) when the level is loaded, each with its own stone texture (issue #233). Each face
/// gets a brightness depending on the direction it faces (flat shading), stored as the colour of its corners - that is
/// what makes corners and edges visible. The texture coordinates come from the world position
/// (<see cref="SurfaceTexturing"/>), so the stones run on across cell borders without a seam.
/// <para>
/// A kind whose texture is missing is drawn plain as before: its base colour times the brightness.
/// </para>
/// </summary>
internal sealed class LevelRenderer : IDisposable
{
    private readonly Shader _shader;
    private readonly LevelTextures _textures;
    private List<Model> _models;

    /// <param name="level">The level to draw.</param>
    /// <param name="shader">Shader for lantern light and fog. The renderer uses it but does not own it.</param>
    /// <param name="textures">The stones of the layer. The renderer uses them but does not own them.</param>
    public LevelRenderer(Level level, Shader shader, LevelTextures textures)
    {
        _shader = shader;
        _textures = textures;
        _models = Load(level, shader, textures);
    }

    /// <summary>Draws <paramref name="level"/> from now on (a new run with a new layout); the old meshes are freed.</summary>
    public void Rebuild(Level level)
    {
        Unload();
        _models = Load(level, _shader, _textures);
    }

    private static List<Model> Load(Level level, Shader shader, LevelTextures textures)
    {
        var models = new List<Model>();
        foreach (var group in LevelGeometry.Build(level).GroupBy(face => face.Kind))
        {
            var texture = textures.For(group.Key);
            var mesh = BuildMesh([.. group], texture is not null, textures.Set.TileSize);

            // Copies the data to the graphics card. From then on drawing costs almost nothing.
            Raylib.UploadMesh(ref mesh, false);
            var model = Raylib.LoadModelFromMesh(mesh);

            // The model's material decides HOW it is drawn - here: with our light-and-fog shader and the stones.
            // (UnloadModel unloads neither shaders nor textures, so LevelShader and LevelTextures stay responsible.)
            ref var material = ref model.MaterialsAsSpan()[0];
            material.Shader = shader;
            if (texture is { } stones)
            {
                Raylib.SetMaterialTexture(ref material, MaterialMapIndex.Albedo, stones);
            }

            models.Add(model);
        }

        return models;
    }

    public void Draw()
    {
        foreach (var model in _models)
        {
            Raylib.DrawModel(model, Vector3.Zero, 1f, Color.White);
        }
    }

    public void Dispose() => Unload();

    private void Unload()
    {
        foreach (var model in _models)
        {
            Raylib.UnloadModel(model);
        }

        _models.Clear();
    }

    private static Mesh BuildMesh(IReadOnlyList<Face> faces, bool textured, float tileSize)
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
        mesh.AllocTexCoords();
        mesh.AllocColors();
        mesh.AllocIndices();

        var vertices = mesh.VerticesAs<Vector3>();
        var normals = mesh.NormalsAs<Vector3>();
        var texCoords = mesh.TexCoordsAs<Vector2>();
        var colors = mesh.ColorsAs<Color>();
        var indices = mesh.IndicesAs<ushort>();

        for (var i = 0; i < faces.Count; i++)
        {
            var face = faces[i];
            var color = Shade(face, textured);
            var v = i * 4;

            vertices[v] = face.A;
            vertices[v + 1] = face.B;
            vertices[v + 2] = face.C;
            vertices[v + 3] = face.D;

            for (var corner = 0; corner < 4; corner++)
            {
                normals[v + corner] = face.Normal;
                colors[v + corner] = color;
                texCoords[v + corner] = SurfaceTexturing.TexCoord(vertices[v + corner], face.Normal, face.Kind, tileSize);
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

    /// <summary>
    /// The brightness of the face's facing direction - as grey for a texture (the texture replaces the base colour, it
    /// is not multiplied with it), or times the base colour of the surface for a plain one.
    /// </summary>
    private static Color Shade(Face face, bool textured)
    {
        var (baseColor, brightness) = face.Kind switch
        {
            SurfaceKind.Floor => (LookSettings.FloorColor, LookSettings.FloorBrightness),
            SurfaceKind.Ceiling => (LookSettings.CeilingColor, LookSettings.CeilingBrightness),
            _ => (LookSettings.WallColor, FlatShading.WallBrightness(face.Normal)),
        };

        if (textured)
        {
            baseColor = Color.White;
        }

        return new Color(
            (byte)(baseColor.R * brightness),
            (byte)(baseColor.G * brightness),
            (byte)(baseColor.B * brightness),
            (byte)255);
    }
}
