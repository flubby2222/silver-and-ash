using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// A model of the designers (assets/models/*.glb, docs/modelle.md): rigid parts with the textures of their materials, turned by code around
/// their pivots and drawn with soft shading through the world shader (lantern, fog - like the level).
/// <para>
/// How it is loaded: raylib's <c>LoadModel</c> reads the meshes and the texture, one mesh per part in the order of the
/// nodes in the file, and moves the corners by the node's translation (so they lie where they stand in the model).
/// It forgets the names and the pivots - <see cref="GlbScene"/> reads those from the same file, and the loader checks
/// that both agree (number of parts, corners, position) before it trusts the order.
/// </para>
/// <para>
/// How it is drawn: like the placeholder boxes, the corners are moved into the world here in C# every frame, so each
/// corner gets the brightness of the direction its normal faces after turning (<see cref="SmoothShading"/>), and the
/// graphics card lets it run across the triangle. The texture is the model's own, without filtering and mipmaps.
/// </para>
/// </summary>
internal sealed class TexturedModel : IDisposable
{
    private const float Tolerance = 0.001f;

    private static readonly FacingBrightness Facing = new(
        LookSettings.WallFacingPlusXBrightness, LookSettings.WallFacingMinusXBrightness,
        LookSettings.FloorBrightness, LookSettings.CeilingBrightness,
        LookSettings.WallFacingPlusZBrightness, LookSettings.WallFacingMinusZBrightness);

    private readonly Model _model;
    private readonly Part[] _parts;
    private readonly Texture2D[] _alternates;
    private readonly Vector3[] _worldCorners;
    private readonly Color[] _cornerColors;

    private TexturedModel(Model model, Rig rig, Part[] parts, Texture2D[] alternates)
    {
        _model = model;
        Rig = rig;
        _parts = parts;
        _alternates = alternates;
        var mostCorners = parts.Max(part => part.Positions.Length);
        _worldCorners = new Vector3[mostCorners];
        _cornerColors = new Color[mostCorners];
    }

    /// <summary>The parts with their pivots from the file, in the order of the rig given to <see cref="TryLoad"/>.</summary>
    public Rig Rig { get; }

    /// <summary>Number of triangles of all parts.</summary>
    public int TriangleCount => _parts.Sum(part => part.Indices.Length / 3);

    /// <summary>
    /// Loads <paramref name="path"/> for a figure made of <paramref name="parts"/>. Missing or broken - or not matching
    /// the parts - it writes a warning to the log and returns null; the caller keeps its placeholder.
    /// Needs the window (the texture goes to the graphics card).
    /// </summary>
    /// <para>
    /// <paramref name="alternates"/>: further pictures next to the model (file names, checked against the same list) that
    /// can stand in for a part's own texture when drawing (<see cref="DrawPart"/>) - Gorm's phase 2. If one of them is
    /// missing or broken, the whole model counts as broken.
    /// </para>
    public static TexturedModel? TryLoad(string path, IReadOnlyList<RigPart> parts, FileLog? log, IReadOnlyList<string>? alternates = null)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        IReadOnlyList<GlbPart> file;
        try
        {
            // First stage: the file must be byte for byte the one in the repository (size and SHA-256 from the checked-in
            // list, docs/modelle.md). Raylib crashes in native code on some broken files - those never reach it.
            var listPath = Path.Combine(Path.GetDirectoryName(path) ?? ".", ModelChecksums.FileName);
            var mismatch = ModelChecksums.Parse(File.ReadAllText(listPath)).Check(path);
            if (mismatch is not null)
            {
                log?.Warning($"Model '{path}': {mismatch} - the placeholder is drawn instead.");
                return null;
            }

            log?.Info($"Model '{path}' matches the checksum list ({System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds:0.0} ms).");

            // Second stage, simple: the description of the file (names, pivots, lengths).
            file = GlbScene.Read(File.ReadAllBytes(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            log?.Warning($"Model '{path}' could not be read ({exception.Message}) - the placeholder is drawn instead.");
            return null;
        }

        var missing = parts.Select(part => part.Name).Except(file.Select(part => part.Name)).ToList();
        if (missing.Count > 0)
        {
            log?.Warning($"Model '{path}' lacks the parts {string.Join(", ", missing)} - the placeholder is drawn instead.");
            return null;
        }

        // Parts the figure does not move are not drawn; a name twice takes the first node. Worth a line in the log.
        var unused = file.Select(part => part.Name).Except(parts.Select(part => part.Name)).ToList();
        var twice = file.GroupBy(part => part.Name).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        if (unused.Count > 0 || twice.Count > 0)
        {
            log?.Warning($"Model '{path}': parts not drawn: {string.Join(", ", unused)}; names twice (first one used): {string.Join(", ", twice)}.");
        }

        var model = Raylib.LoadModel(path);
        var problem = Check(model, file);
        if (problem is not null)
        {
            Unload(model);
            log?.Warning($"Model '{path}': {problem} - the placeholder is drawn instead.");
            return null;
        }

        // Hard pixels as in the PlayStation era (decision record 0010). Raylib makes no mipmaps when loading a model's
        // textures (checked in Check), and point filtering never blends neighbouring texels.
        foreach (var texture in Textures(model))
        {
            Raylib.SetTextureFilter(texture, TextureFilter.Point);
        }

        var extra = LoadAlternates(path, alternates ?? [], log);
        if (extra is null)
        {
            Unload(model);
            return null;
        }

        var meshes = model.MeshesAsSpan();
        var ordered = new Part[parts.Count];
        var pivots = new Vector3[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            var index = file.Select(part => part.Name).ToList().IndexOf(parts[i].Name);
            pivots[i] = file[index].Pivot;
            ordered[i] = Part.Copy(meshes[index], file[index].Pivot, TextureOf(model, index));
        }

        var loaded = new TexturedModel(model, new Rig(parts, pivots), ordered, extra);
        var sizes = string.Join(", ", Textures(model).Select(texture => $"{texture.Width} x {texture.Height}"));
        log?.Info($"Model '{path}' loaded: {parts.Count} parts, {loaded.TriangleCount} triangles, textures {sizes}, {extra.Length} alternate(s).");
        return loaded;
    }

    /// <summary>
    /// Draws the model. <paramref name="frames"/>: per part (order of <see cref="Rig"/>), corner relative to the pivot
    /// -> world, from <see cref="Rig.Pose"/>. Call between <see cref="LevelShader.BeginFigures"/> and EndFigures.
    /// </summary>
    public void Draw(ReadOnlySpan<Matrix4x4> frames)
    {
        if (frames.Length != _parts.Length)
        {
            throw new ArgumentException($"The model has {_parts.Length} parts.", nameof(frames));
        }

        for (var p = 0; p < _parts.Length; p++)
        {
            DrawPart(p, frames[p]);
        }
    }

    /// <summary>
    /// Draws one part only (index in the order of <see cref="Rig"/>) - for parts that are not always there, like the
    /// lock of a chest. <paramref name="tint"/> makes the part brighter or darker (1: as it is; capped at full white).
    /// </summary>
    /// <paramref name="alternate"/>: index of a picture given to <see cref="TryLoad"/> as an alternate, drawn instead of
    /// the part's own texture (null: its own). <paramref name="uvOffset"/> moves its texture coordinates (flowing water).
    public void DrawPart(int index, Matrix4x4 frame, float tint = 1f, int? alternate = null, Vector2 uvOffset = default)
    {
        var part = _parts[index];

        // Each corner once (corners are shared by several triangles): position and brightness in the world.
        for (var i = 0; i < part.Positions.Length; i++)
        {
            _worldCorners[i] = Vector3.Transform(part.Positions[i], frame);
            var brightness = SmoothShading.Brightness(Vector3.TransformNormal(part.Normals[i], frame), Facing) * tint;
            var level = (byte)Math.Clamp(MathF.Round(brightness * 255f), 0f, 255f);
            _cornerColors[i] = new Color(level, level, level, (byte)255);
        }

        // Room for all corners of this part in the current batch; otherwise raylib draws what it has first.
        Rlgl.CheckRenderBatchLimit(part.Indices.Length);
        Rlgl.SetTexture(alternate is { } chosen ? _alternates[chosen].Id : part.Texture.Id);
        Rlgl.Begin(DrawMode.Triangles);
        foreach (var corner in part.Indices)
        {
            var color = _cornerColors[corner];
            var uv = part.TexCoords[corner] + uvOffset;
            var world = _worldCorners[corner];
            Rlgl.Color4ub(color.R, color.G, color.B, color.A);
            Rlgl.TexCoord2f(uv.X, uv.Y);
            Rlgl.Vertex3f(world.X, world.Y, world.Z);
        }

        Rlgl.End();
        // Back to the white default texture: SetTexture(0) would leave the model's texture on the running batch, and
        // the next triangles without a texture of their own (DrawCube ...) would get it.
        Rlgl.SetTexture(Rlgl.GetTextureIdDefault());
    }

    public void Dispose()
    {
        foreach (var texture in _alternates)
        {
            Raylib.UnloadTexture(texture);
        }

        Unload(_model);
    }

    // The alternate pictures next to the model: checked against the checksum list like the model, hard pixels, no
    // mipmaps. Null (and a warning) if one is missing or broken; those loaded so far are unloaded again.
    private static Texture2D[]? LoadAlternates(string path, IReadOnlyList<string> names, FileLog? log)
    {
        var folder = Path.GetDirectoryName(path) ?? ".";
        var loaded = new List<Texture2D>();
        foreach (var name in names)
        {
            var picture = Path.Combine(folder, name);
            string? problem;
            try
            {
                problem = ModelChecksums.Parse(File.ReadAllText(Path.Combine(folder, ModelChecksums.FileName))).Check(picture);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                problem = exception.Message;
            }

            var texture = problem is null ? Raylib.LoadTexture(picture) : default;
            if (problem is null && (!Raylib.IsTextureValid(texture) || texture.Mipmaps != 1))
            {
                problem = "raylib could not load it as a texture without mipmaps";
                if (Raylib.IsTextureValid(texture))
                {
                    Raylib.UnloadTexture(texture);
                }
            }

            if (problem is not null)
            {
                log?.Warning($"Model '{path}': picture '{name}': {problem} - the placeholder is drawn instead.");
                loaded.ForEach(Raylib.UnloadTexture);
                return null;
            }

            Raylib.SetTextureFilter(texture, TextureFilter.Point);
            loaded.Add(texture);
        }

        return [.. loaded];
    }

    // UnloadModel frees meshes and material maps, but not the textures (raylib lets models share them) - those as well.
    private static unsafe void Unload(Model model)
    {
        var textures = new HashSet<uint>();
        foreach (var material in model.MaterialsAsSpan())
        {
            if (material.Maps != null)
            {
                var texture = material.Maps[(int)MaterialMapIndex.Albedo].Texture;
                if (texture.Id != 0 && texture.Id != Rlgl.GetTextureIdDefault() && textures.Add(texture.Id))
                {
                    Raylib.UnloadTexture(texture);
                }
            }
        }

        Raylib.UnloadModel(model);
    }

    // Does what raylib loaded match the description of the file? Returns what is wrong, or null.
    private static string? Check(Model model, IReadOnlyList<GlbPart> file)
    {
        if (!Raylib.IsModelValid(model) || model.MeshCount != file.Count)
        {
            return $"raylib loaded {model.MeshCount} meshes, the file describes {file.Count} parts";
        }

        var meshes = model.MeshesAsSpan();
        for (var i = 0; i < file.Count; i++)
        {
            var mesh = meshes[i];
            if (mesh.VertexCount != file[i].VertexCount || mesh.TriangleCount == 0)
            {
                return $"mesh {i} has {mesh.VertexCount} corners, part '{file[i].Name}' {file[i].VertexCount}";
            }

            if (!HasAllData(mesh))
            {
                return $"part '{file[i].Name}' lacks texture coordinates, normals or indices";
            }

            // Broken data could name corners that are not there - drawing would then crash.
            var vertexCount = mesh.VertexCount;
            foreach (var index in mesh.IndicesAs<ushort>())
            {
                if (index >= vertexCount)
                {
                    return $"part '{file[i].Name}' names corner {index} of {vertexCount}";
                }
            }

            // Raylib moves the corners by the node's translation: the smallest corner must lie at the file's
            // smallest corner plus the pivot. If it does not, the meshes are not where we think they are.
            var lowest = Raylib.GetMeshBoundingBox(mesh).Min;
            if (Vector3.Distance(lowest, file[i].PositionMin + file[i].Pivot) > Tolerance)
            {
                return $"part '{file[i].Name}' does not lie at its pivot (lowest corner {lowest}, expected {file[i].PositionMin + file[i].Pivot})";
            }
        }

        // Every part needs the texture of its material (a model may have several materials - Gorm has two).
        for (var i = 0; i < file.Count; i++)
        {
            var texture = TextureOf(model, i);
            if (!Raylib.IsTextureValid(texture) || texture.Id == Rlgl.GetTextureIdDefault())
            {
                return $"part '{file[i].Name}' has no texture";
            }

            if (texture.Mipmaps != 1)
            {
                return $"the texture of part '{file[i].Name}' has {texture.Mipmaps} mipmap levels, expected none";
            }
        }

        return null;
    }

    // The arrays raylib did not find in the file stay empty (null) - reading them would crash.
    private static unsafe bool HasAllData(Mesh mesh) =>
        mesh.Vertices != null && mesh.Normals != null && mesh.TexCoords != null && mesh.Indices != null;

    // The texture of the material of mesh `mesh`.
    private static unsafe Texture2D TextureOf(Model model, int mesh)
    {
        var material = model.MaterialsAsSpan()[model.MeshMaterialAsSpan()[mesh]];
        return material.Maps[(int)MaterialMapIndex.Albedo].Texture;
    }

    // Each texture the meshes use, once.
    private static List<Texture2D> Textures(Model model)
    {
        var textures = new List<Texture2D>();
        for (var mesh = 0; mesh < model.MeshCount; mesh++)
        {
            var texture = TextureOf(model, mesh);
            if (textures.TrueForAll(known => known.Id != texture.Id))
            {
                textures.Add(texture);
            }
        }

        return textures;
    }

    // One part as copied from raylib: corners relative to the pivot, normals, texture coordinates, triangles, and the
    // texture of its material.
    private sealed record Part(Vector3[] Positions, Vector3[] Normals, Vector2[] TexCoords, ushort[] Indices, Texture2D Texture)
    {
        public static Part Copy(Mesh mesh, Vector3 pivot, Texture2D texture) => new(
            [.. mesh.VerticesAs<Vector3>().ToArray().Select(corner => corner - pivot)],
            mesh.NormalsAs<Vector3>().ToArray(),
            mesh.TexCoordsAs<Vector2>().ToArray(),
            mesh.IndicesAs<ushort>().ToArray(),
            texture);
    }
}
