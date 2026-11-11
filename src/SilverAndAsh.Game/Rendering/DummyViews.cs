using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// How training dummies are shown. Behind this interface so the placeholder can later be replaced by real
/// models (v0.10) without touching the game loop (rule from CLAUDE.md: figures behind an exchangeable interface).
/// </summary>
internal interface IDummyView
{
    /// <summary>Reacts to something that happened in the simulation (hit, knocked down, stood up).</summary>
    void OnEvent(GameEvent gameEvent, Vector3 playerPosition);

    /// <summary>Advances the animations by the real time of the last frame.</summary>
    void Update(float frameSeconds);

    /// <summary>Draws one dummy. Call between BeginMode3D and EndMode3D.</summary>
    void Draw(TrainingDummy dummy);
}

/// <summary>
/// Placeholder dummy made of boxes: a wooden post on a foot, a straw body with arms and a sack head.
/// Movement by code: it flashes bright when hit, tips over away from the player at 0 health and swings back up.
/// </summary>
internal sealed class PlaceholderDummyView : IDummyView
{
    private const float FlashSeconds = 0.15f;
    private const float FallDegreesPerSecond = 300f;
    private const float RiseDegreesPerSecond = 150f;
    private const float LyingDegrees = 85f;

    private static readonly Color Wood = new(96, 66, 44, 255);
    private static readonly Color Straw = new(176, 146, 92, 255);
    private static readonly Color Sack = new(150, 128, 96, 255);
    private static readonly Color FlashColor = new(255, 250, 235, 255);

    // (size, centre above the feet, colour) of every box of the figure.
    private static readonly (Vector3 Size, Vector3 Centre, Color Color)[] Parts =
    [
        (new(0.5f, 0.08f, 0.5f), new(0f, 0.04f, 0f), Wood),     // foot
        (new(0.12f, 1.1f, 0.12f), new(0f, 0.6f, 0f), Wood),     // post
        (new(0.5f, 0.65f, 0.3f), new(0f, 1.1f, 0f), Straw),     // body
        (new(1.0f, 0.1f, 0.1f), new(0f, 1.3f, 0f), Wood),       // arms
        (new(0.3f, 0.3f, 0.3f), new(0f, 1.6f, 0f), Sack),       // head
    ];

    private readonly Dictionary<int, State> _states = [];

    public void OnEvent(GameEvent gameEvent, Vector3 playerPosition)
    {
        switch (gameEvent)
        {
            case DummyHit hit:
                StateOf(hit.DummyId).Flash = 1f;
                break;
            case DummyKnockedDown down:
                StateOf(down.DummyId).IsDown = true;
                StateOf(down.DummyId).PlayerPositionWhenFallen = playerPosition;
                break;
            case DummyStoodUp up:
                StateOf(up.DummyId).IsDown = false;
                break;
        }
    }

    public void Update(float frameSeconds)
    {
        foreach (var state in _states.Values)
        {
            state.Flash = MathF.Max(0f, state.Flash - frameSeconds / FlashSeconds);
            state.TiltDegrees = state.IsDown
                ? MathF.Min(LyingDegrees, state.TiltDegrees + FallDegreesPerSecond * frameSeconds)
                : MathF.Max(0f, state.TiltDegrees - RiseDegreesPerSecond * frameSeconds);
        }
    }

    public void Draw(TrainingDummy dummy)
    {
        var state = StateOf(dummy.Id);
        var placement = Matrix4x4.CreateTranslation(dummy.Position);

        if (state.TiltDegrees > 0f)
        {
            // Tip over around the foot, away from where the player stood when it fell.
            var away = dummy.Position - state.PlayerPositionWhenFallen;
            away.Y = 0f;
            away = away.LengthSquared() > 0.0001f ? Vector3.Normalize(away) : Vector3.UnitZ;
            var axis = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, away));
            placement = Matrix4x4.CreateFromAxisAngle(axis, state.TiltDegrees * MathF.PI / 180f) * placement;
        }

        foreach (var (size, centre, color) in Parts)
        {
            var shown = state.Flash > 0f ? Blend(color, FlashColor, state.Flash) : color;
            ShadedBox.Draw(Matrix4x4.CreateTranslation(centre) * placement, size, shown);
        }
    }

    private State StateOf(int id)
    {
        if (!_states.TryGetValue(id, out var state))
        {
            state = new State();
            _states[id] = state;
        }

        return state;
    }

    private static Color Blend(Color from, Color to, float amount) => new(
        (byte)(from.R + (to.R - from.R) * amount),
        (byte)(from.G + (to.G - from.G) * amount),
        (byte)(from.B + (to.B - from.B) * amount),
        (byte)255);

    private sealed class State
    {
        public float Flash { get; set; }

        public bool IsDown { get; set; }

        public float TiltDegrees { get; set; }

        public Vector3 PlayerPositionWhenFallen { get; set; }
    }
}
