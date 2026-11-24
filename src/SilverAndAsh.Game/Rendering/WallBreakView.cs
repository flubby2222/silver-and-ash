using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The cracked wall breaking (issue #185): stone chunks burst out of it, away from whoever struck, tumble to the floor,
/// bounce once or twice and sink away; a soft cloud of dust drifts out and fades (<see cref="WallBreakDust"/>, drawn like
/// the column's by <see cref="DustCloudView"/>). Pure presentation - Core only reports
/// <see cref="WallBroken"/>, and which wall it was. Its own random numbers, seeded by wall and tick: nothing here changes the game.
/// <para>
/// Each chunk is the stone of the cracked wall's model (issue #246, <see cref="CrackedWallLook.Chunk"/>), stretched to
/// its size and a little darker or lighter, in the wall variant of the face the player struck - the stone of the wall
/// they were looking at. Without that model, plain boxes in the wall colour as before.
/// </para>
/// </summary>
internal sealed class WallBreakView
{
    private const int ChunkCount = 26;
    private const float ChunkSeconds = 2.6f;
    private const float SinkSeconds = 0.6f;
    private const float Gravity = 14f;

    // A step of the ramp Stein (EffectColours, issue #609).
    private static readonly Color Dust = PaletteColour.Opaque(EffectColours.WallBreakDust);

    private readonly List<Chunk> _chunks = [];
    private readonly List<Cloud> _clouds = [];
    private readonly List<DustPuff> _puffs = [];

    private sealed class Chunk
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 Spin;
        public Vector3 Angle;
        public Vector3 Size;
        public Color Color;
        public float Tint;
        public TextureVariant Variant;
        public float Floor;
        public float Age;
    }

    // The dust of one broken wall (WallBreakDust): which wall, its box, away from the striker, and the tick it broke.
    private readonly record struct Cloud(int Wall, Vector3 BoxMin, Vector3 BoxMax, Vector3 Away, long BrokeAt);

    /// <summary>
    /// Starts the effect when a wall breaks. <paramref name="striker"/> is where the player stood;
    /// <paramref name="surfaces"/> the wall variants of the level (the chunks are of the face on the striker's side);
    /// <paramref name="ticks"/> the tick of the world it broke in (the dust counts from there).
    /// </summary>
    public void OnEvent(GameEvent gameEvent, IReadOnlyList<Door> doors, Vector3 striker, SurfaceVariants surfaces, long ticks)
    {
        if (gameEvent is not WallBroken broken || broken.DoorId < 0 || broken.DoorId >= doors.Count)
        {
            return;
        }

        var wall = doors[broken.DoorId];
        var variant = StruckVariant(wall, striker, surfaces);
        var centre = (wall.BoxMin + wall.BoxMax) / 2f;
        var size = wall.BoxMax - wall.BoxMin;
        var away = (centre - striker) with { Y = 0f };
        away = away.LengthSquared() > 0.0001f ? Vector3.Normalize(away) : Vector3.UnitZ;
        var side = new Vector3(-away.Z, 0f, away.X);

        // Seeded by the wall and the tick it broke in (issue #637): the same break throws the same chunks, so a photo
        // scene comes out the same picture each time.
        var random = new Random(ChunkSeed(broken.DoorId, ticks));
        float Range(float min, float max) => min + (random.NextSingle() * (max - min));

        for (var i = 0; i < ChunkCount; i++)
        {
            // Most fly on, away from the blow; a few spray back towards the player.
            var backwards = random.NextSingle() < 0.25f ? -0.6f : 1f;
            var spot = wall.BoxMin + new Vector3(random.NextSingle() * size.X, (0.15f + (random.NextSingle() * 0.8f)) * size.Y, random.NextSingle() * size.Z);
            var shade = 0.75f + (0.35f * random.NextSingle());
            _chunks.Add(new Chunk
            {
                Position = spot,
                Velocity = (away * backwards * Range(2f, 5.5f)) + (side * Range(-2.5f, 2.5f)) + new Vector3(0f, Range(0.5f, 4f), 0f),
                Spin = new Vector3(Range(-9f, 9f), Range(-9f, 9f), Range(-9f, 9f)),
                Size = new Vector3(Range(0.12f, 0.38f), Range(0.1f, 0.3f), Range(0.12f, 0.34f)),
                Color = Scale(LookSettings.WallColor, shade),

                // The stone may only get darker (by up to a quarter): the same spread of shades, moved below 1.
                Tint = 0.75f + ((shade - 0.75f) / 0.35f * 0.25f),
                Variant = variant,
                Floor = wall.BoxMin.Y,
            });
        }

        _clouds.Add(new Cloud(broken.DoorId, wall.BoxMin, wall.BoxMax, away, ticks));
    }

    public void Update(float frameSeconds)
    {
        foreach (var chunk in _chunks)
        {
            chunk.Age += frameSeconds;
            chunk.Velocity.Y -= Gravity * frameSeconds;
            chunk.Position += chunk.Velocity * frameSeconds;
            chunk.Angle += chunk.Spin * frameSeconds;

            // On the floor: bounce with a fraction of the speed, rolling slower each time.
            var bottom = chunk.Floor + (chunk.Size.Y / 2f);
            if (chunk.Position.Y < bottom && chunk.Age < ChunkSeconds - SinkSeconds)
            {
                chunk.Position.Y = bottom;
                chunk.Velocity = new Vector3(chunk.Velocity.X * 0.5f, MathF.Abs(chunk.Velocity.Y) * 0.3f, chunk.Velocity.Z * 0.5f);
                chunk.Spin *= 0.5f;
            }
        }

        _chunks.RemoveAll(chunk => chunk.Age >= ChunkSeconds);
    }

    /// <summary>
    /// Draws chunks and dust as they are at <paramref name="ticks"/> ticks of the world - the dust a soft, see-through
    /// cloud with the level <paramref name="shader"/> (issues #609, #614). Call between BeginFigures and EndFigures, after
    /// the opaque world.
    /// </summary>
    public void Draw(DoorView stones, LevelShader shader, long ticks)
    {
        foreach (var chunk in _chunks)
        {
            // At the end they sink into the floor instead of popping away.
            var sink = MathF.Max(0f, chunk.Age - (ChunkSeconds - SinkSeconds)) / SinkSeconds;
            var place = Matrix4x4.CreateRotationX(chunk.Angle.X) * Matrix4x4.CreateRotationY(chunk.Angle.Y) * Matrix4x4.CreateRotationZ(chunk.Angle.Z)
                * Matrix4x4.CreateTranslation(chunk.Position - new Vector3(0f, sink * chunk.Size.Y, 0f));
            // The stone model is about ChunkSize on each edge: stretched to the chunk's size.
            if (!stones.DrawChunk(chunk.Variant, Matrix4x4.CreateScale(chunk.Size / CrackedWallLook.ChunkSize) * place, chunk.Tint))
            {
                ShadedBox.Draw(place, chunk.Size, chunk.Color);
            }
        }

        _clouds.RemoveAll(cloud => ticks - cloud.BrokeAt >= WallBreakDust.Ticks);
        _puffs.Clear();
        foreach (var cloud in _clouds)
        {
            WallBreakDust.Puffs((int)(ticks - cloud.BrokeAt), cloud.Wall, cloud.BoxMin, cloud.BoxMax, cloud.Away, _puffs);
        }

        DustCloudView.Draw(shader, _puffs, Dust);
    }

    // The variant of the face nearer to the striker - the face they hit.
    private static TextureVariant StruckVariant(Door wall, Vector3 striker, SurfaceVariants surfaces)
    {
        var (frontCell, backCell) = CrackedWallLook.FaceCells(wall.X, wall.Z, wall.Side);
        var (front, back) = CrackedWallLook.Variants(surfaces, wall.X, wall.Z, wall.Side);
        var toFront = Vector2.DistanceSquared(new Vector2(striker.X, striker.Z), Middle(frontCell));
        var toBack = Vector2.DistanceSquared(new Vector2(striker.X, striker.Z), Middle(backCell));
        return toFront <= toBack ? front : back;
    }

    private static Vector2 Middle((int X, int Z) cell) => new((cell.X + 0.5f) * Level.CellSize, (cell.Z + 0.5f) * Level.CellSize);

    // A seed of its own for each wall and tick; fixed arithmetic, not string.GetHashCode (that changes with every start).
    private static int ChunkSeed(int wall, long ticks) => unchecked((wall * 7919) + (int)ticks);

    private static Color Scale(Color color, float factor) => new(
        (byte)Math.Clamp(color.R * factor, 0f, 255f),
        (byte)Math.Clamp(color.G * factor, 0f, 255f),
        (byte)Math.Clamp(color.B * factor, 0f, 255f),
        (byte)255);
}
