using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// How ghouls are shown. Behind this interface so the placeholder can later be replaced by real models (v0.10)
/// without touching the game loop.
/// </summary>
internal interface IGhoulView
{
    /// <summary>Reacts to something that happened in the simulation (hit, died).</summary>
    void OnEvent(GameEvent gameEvent);

    /// <summary>Advances the animations by the real time of the last frame.</summary>
    void Update(float frameSeconds);

    /// <summary>Draws one ghoul (nothing once its body is gone). Call between BeginMode3D and EndMode3D.</summary>
    void Draw(Ghoul ghoul);
}

/// <summary>
/// Placeholder ghoul made of boxes: crouched hips, a hunched torso, the head pushed forward, long arms reaching
/// for the ground. Movement by code: it flashes when hit (blood red for a headshot), flinches back, and at death
/// it sinks and rolls onto its side.
/// </summary>
internal sealed class PlaceholderGhoulView : IGhoulView
{
    private const float FlashSeconds = 0.15f;
    private const float CriticalFlashSeconds = 0.35f;
    private const float FlinchSeconds = 0.2f;
    private const float CollapseSeconds = 0.4f;

    private static readonly Color Skin = new(124, 132, 112, 255);
    private static readonly Color Rags = new(62, 52, 54, 255);
    private static readonly Color FlashColor = new(255, 250, 235, 255);
    private static readonly Color CriticalFlashColor = new(230, 40, 50, 255);

    // (size, centre above the feet, tilt forward in degrees, colour) - local space: x right, y up, z forward.
    private static readonly (Vector3 Size, Vector3 Centre, float Tilt, Color Color)[] Parts =
    [
        (new(0.46f, 0.34f, 0.40f), new(0f, 0.30f, -0.24f), 0f, Rags),       // hips and bent legs
        (new(0.50f, 0.34f, 0.66f), new(0f, 0.62f, 0.02f), 18f, Rags),       // hunched torso
        (new(0.26f, 0.24f, 0.30f), new(0f, 0.82f, 0.36f), 0f, Skin),        // head, pushed forward
        (new(0.10f, 0.62f, 0.10f), new(-0.30f, 0.36f, 0.30f), -25f, Skin),  // left arm, reaching down
        (new(0.10f, 0.62f, 0.10f), new(0.30f, 0.36f, 0.30f), -25f, Skin),   // right arm
    ];

    private readonly Dictionary<int, State> _states = [];

    public void OnEvent(GameEvent gameEvent)
    {
        switch (gameEvent)
        {
            case GhoulHit hit:
                var state = StateOf(hit.GhoulId);
                state.Flash = 1f;
                state.FlashIsCritical = hit.Critical;
                state.Flinch = 1f;
                break;
            case GhoulDied died:
                StateOf(died.GhoulId).IsDead = true;
                break;
        }
    }

    public void Update(float frameSeconds)
    {
        foreach (var state in _states.Values)
        {
            var flashSeconds = state.FlashIsCritical ? CriticalFlashSeconds : FlashSeconds;
            state.Flash = MathF.Max(0f, state.Flash - frameSeconds / flashSeconds);
            state.Flinch = MathF.Max(0f, state.Flinch - frameSeconds / FlinchSeconds);
            if (state.IsDead)
            {
                state.Collapse = MathF.Min(1f, state.Collapse + frameSeconds / CollapseSeconds);
            }
        }
    }

    public void Draw(Ghoul ghoul)
    {
        ArgumentNullException.ThrowIfNull(ghoul);
        if (ghoul.IsGone)
        {
            return;
        }

        var state = StateOf(ghoul.Id);
        var yaw = MathF.Atan2(ghoul.Facing.X, ghoul.Facing.Z);

        // Flinch: jerks back and up a little. Death: sinks and rolls onto its side.
        var flinch = state.Flinch * state.Flinch;
        var collapse = 1f - (1f - state.Collapse) * (1f - state.Collapse);
        var body =
            Matrix4x4.CreateRotationX(Deg(-12f * flinch)) *
            Matrix4x4.CreateRotationZ(Deg(75f * collapse)) *
            Matrix4x4.CreateTranslation(0f, -0.2f * collapse, -0.06f * flinch) *
            Matrix4x4.CreateRotationY(yaw) *
            Matrix4x4.CreateTranslation(ghoul.Position);

        var flashColor = state.FlashIsCritical ? CriticalFlashColor : FlashColor;
        foreach (var (size, centre, tilt, color) in Parts)
        {
            var shown = state.Flash > 0f ? Blend(color, flashColor, state.Flash) : color;
            ShadedBox.Draw(Matrix4x4.CreateRotationX(Deg(tilt)) * Matrix4x4.CreateTranslation(centre) * body, size, shown);
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

        public bool FlashIsCritical { get; set; }

        public float Flinch { get; set; }

        public bool IsDead { get; set; }

        public float Collapse { get; set; }
    }
}
