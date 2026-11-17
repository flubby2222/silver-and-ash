using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The cracked wall breaking (issue #185): stone chunks burst out of it, away from whoever struck, tumble to the floor,
/// bounce once or twice and sink away; a cloud of dust rises and fades. Pure presentation - Core only reports
/// <see cref="WallBroken"/>, and which wall it was. Its own random numbers: nothing here changes the game.
/// <para>
/// Each chunk is the stone of the cracked wall's model (issue #246, <see cref="CrackedWallLook.Chunk"/>), stretched to
/// its size and a little darker or lighter, in the wall variant of the face the player struck - the stone of the wall
/// they were looking at. Without that model, plain boxes in the wall colour as before.
/// </para>
/// </summary>
internal sealed class WallBreakView
{
    private const int ChunkCount = 26;
    private const int PuffCount = 12;
    private const float ChunkSeconds = 2.6f;
    private const float SinkSeconds = 0.6f;
    private const float PuffSeconds = 1.8f;
    private const float Gravity = 14f;

    private static readonly Color Dust = new(170, 156, 146, 255);

    private readonly Random _random = new();
    private readonly List<Chunk> _chunks = [];
    private readonly List<Puff> _puffs = [];

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

    private sealed class Puff
    {
        public Vector3 Position;
        public Vector3 Drift;
        public float Size;
        public float Age;
        public float Delay;
    }

    /// <summary>
    /// Starts the effect when a wall breaks. <paramref name="striker"/> is where the player stood;
    /// <paramref name="surfaces"/> the wall variants of the level (the chunks are of the face on the striker's side).
    /// </summary>
    public void OnEvent(GameEvent gameEvent, IReadOnlyList<Door> doors, Vector3 striker, SurfaceVariants surfaces)
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

        for (var i = 0; i < ChunkCount; i++)
        {
            // Most fly on, away from the blow; a few spray back towards the player.
            var backwards = _random.NextSingle() < 0.25f ? -0.6f : 1f;
            var spot = wall.BoxMin + new Vector3(_random.NextSingle() * size.X, (0.15f + (_random.NextSingle() * 0.8f)) * size.Y, _random.NextSingle() * size.Z);
            var shade = 0.75f + (0.35f * _random.NextSingle());
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

        for (var i = 0; i < PuffCount; i++)
        {
            _puffs.Add(new Puff
            {
                Position = wall.BoxMin + new Vector3(_random.NextSingle() * size.X, _random.NextSingle() * size.Y * 0.7f, _random.NextSingle() * size.Z),
                Drift = (away * Range(0.2f, 0.9f)) + (side * Range(-0.4f, 0.4f)) + new Vector3(0f, Range(0.2f, 0.6f), 0f),
                Size = Range(0.4f, 0.8f),
                Delay = Range(0f, 0.25f),
            });
        }
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

        foreach (var puff in _puffs)
        {
            puff.Age += frameSeconds;
            if (puff.Age > puff.Delay)
            {
                puff.Position += puff.Drift * frameSeconds;
                puff.Drift *= MathF.Max(0f, 1f - (1.2f * frameSeconds));
            }
        }

        _chunks.RemoveAll(chunk => chunk.Age >= ChunkSeconds);
        _puffs.RemoveAll(puff => puff.Age >= puff.Delay + PuffSeconds);
    }

    /// <summary>Draws chunks and dust. Call between BeginMode3D and EndMode3D, after the opaque world.</summary>
    public void Draw(DoorView stones)
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

        foreach (var puff in _puffs.Where(puff => puff.Age > puff.Delay))
        {
            // Grows and thins out.
            var life = (puff.Age - puff.Delay) / PuffSeconds;
            var size = puff.Size * (1f + (1.6f * life));
            var alpha = (byte)(150f * (1f - life) * (1f - life));
            Raylib.DrawCube(puff.Position, size, size, size, Dust with { A = alpha });
        }
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

    private float Range(float min, float max) => min + (_random.NextSingle() * (max - min));

    private static Color Scale(Color color, float factor) => new(
        (byte)Math.Clamp(color.R * factor, 0f, 255f),
        (byte)Math.Clamp(color.G * factor, 0f, 255f),
        (byte)Math.Clamp(color.B * factor, 0f, 255f),
        (byte)255);
}
