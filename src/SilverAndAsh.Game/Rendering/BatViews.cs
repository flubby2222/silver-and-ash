using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// How bats are shown. Behind this interface so the placeholder can later be replaced by real models (v0.10).
/// </summary>
internal interface IBatView
{
    /// <summary>Reacts to something that happened in the simulation (hit, died).</summary>
    void OnEvent(GameEvent gameEvent);

    /// <summary>Advances the animations by the real time of the last frame.</summary>
    void Update(float frameSeconds);

    /// <summary>
    /// Draws one bat (nothing once its body is gone), turned towards <paramref name="lookAt"/> (the player's eyes).
    /// Call between BeginMode3D and EndMode3D.
    /// </summary>
    void Draw(Bat bat, Vector3 lookAt);
}

/// <summary>
/// Placeholder bat made of boxes: a dark body, two pointed ears and two wings that beat fast. It flashes when hit;
/// dead, it folds its wings and tumbles while it falls.
/// </summary>
internal sealed class PlaceholderBatView : IBatView
{
    private const float FlashSeconds = 0.15f;
    private const float WingBeatsPerSecond = 7f;
    private const float TumbleDegreesPerSecond = 540f;

    private static readonly Color Fur = new(52, 40, 46, 255);
    private static readonly Color Wing = new(88, 52, 62, 255);
    private static readonly Color Eye = new(220, 40, 40, 255);
    private static readonly Color FlashColor = new(255, 250, 235, 255);

    private readonly Dictionary<int, State> _states = [];
    private float _time;

    public void OnEvent(GameEvent gameEvent)
    {
        switch (gameEvent)
        {
            case BatHit hit:
                StateOf(hit.BatId).Flash = 1f;
                break;
            case BatDied died:
                StateOf(died.BatId).IsDead = true;
                break;
        }
    }

    public void Update(float frameSeconds)
    {
        _time += frameSeconds;
        foreach (var state in _states.Values)
        {
            state.Flash = MathF.Max(0f, state.Flash - frameSeconds / FlashSeconds);
            if (state.IsDead)
            {
                state.Tumble += TumbleDegreesPerSecond * frameSeconds;
            }
        }
    }

    public void Draw(Bat bat, Vector3 lookAt)
    {
        ArgumentNullException.ThrowIfNull(bat);
        if (bat.IsGone)
        {
            return;
        }

        var state = StateOf(bat.Id);
        var toViewer = (lookAt - bat.Position) with { Y = 0f };
        var yaw = toViewer.LengthSquared() > 0.0001f ? MathF.Atan2(toViewer.X, toViewer.Z) : 0f;

        // Alive: a slight bob with the wing beat. Dead: tumbling (until it lies still, the core lets it fall).
        var beat = MathF.Sin(_time * WingBeatsPerSecond * 2f * MathF.PI + bat.Id);
        var bob = state.IsDead ? 0f : beat * 0.04f;
        var tumble = state.IsDead ? MathF.Min(state.Tumble, 160f) : 0f;
        var body =
            Matrix4x4.CreateRotationX(Deg(tumble)) *
            Matrix4x4.CreateTranslation(0f, 0.2f + bob, 0f) *
            Matrix4x4.CreateRotationY(yaw) *
            Matrix4x4.CreateTranslation(bat.Position);

        Color Shown(Color color) => state.Flash > 0f ? Blend(color, FlashColor, state.Flash) : color;

        ShadedBox.Draw(body, new Vector3(0.2f, 0.18f, 0.24f), Shown(Fur));                                  // body
        ShadedBox.Draw(Matrix4x4.CreateTranslation(-0.06f, 0.13f, 0.05f) * body, new Vector3(0.04f, 0.09f, 0.04f), Shown(Fur));   // ears
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0.06f, 0.13f, 0.05f) * body, new Vector3(0.04f, 0.09f, 0.04f), Shown(Fur));
        ShadedBox.Draw(Matrix4x4.CreateTranslation(-0.05f, 0.03f, 0.125f) * body, new Vector3(0.03f, 0.03f, 0.01f), Eye);         // eyes
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0.05f, 0.03f, 0.125f) * body, new Vector3(0.03f, 0.03f, 0.01f), Eye);

        // Wings: hinged at the body's sides, beating up and down. Dead: folded against the body.
        var flap = state.IsDead ? 70f : beat * 40f;
        foreach (var side in (ReadOnlySpan<float>)[-1f, 1f])
        {
            var wing =
                Matrix4x4.CreateTranslation(0.2f * side, 0f, 0f) *
                Matrix4x4.CreateRotationZ(Deg(flap * side)) *
                Matrix4x4.CreateTranslation(0.1f * side, 0.02f, 0f) *
                body;
            ShadedBox.Draw(wing, new Vector3(0.4f, 0.02f, 0.22f), Shown(Wing));
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

    private static float Deg(float degrees) => degrees * MathF.PI / 180f;

    private static Color Blend(Color from, Color to, float amount) => new(
        (byte)(from.R + (to.R - from.R) * amount),
        (byte)(from.G + (to.G - from.G) * amount),
        (byte)(from.B + (to.B - from.B) * amount),
        (byte)255);

    private sealed class State
    {
        public float Flash { get; set; }

        public bool IsDead { get; set; }

        public float Tumble { get; set; }
    }
}
