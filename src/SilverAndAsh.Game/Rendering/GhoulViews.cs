using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core;
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
/// for the ground. Movement by code: the arms reach forward in turn while it crawls; winding up, it rears up and
/// lifts its arms (the warning to step away); the blow is a short lunge. It flashes when hit (blood red for a
/// headshot), flinches back, and at death it sinks and rolls onto its side.
/// </summary>
internal sealed class PlaceholderGhoulView : IGhoulView
{
    private const float FlashSeconds = 0.15f;
    private const float CriticalFlashSeconds = 0.35f;
    private const float FlinchSeconds = 0.2f;
    private const float CollapseSeconds = 0.4f;
    private const float LungeSeconds = 0.2f;
    private const float CrawlCyclesPerMetre = 0.9f;

    private static readonly Color Skin = new(124, 132, 112, 255);
    private static readonly Color Rags = new(62, 52, 54, 255);
    private static readonly Color FlashColor = new(255, 250, 235, 255);
    private static readonly Color CriticalFlashColor = new(230, 40, 50, 255);

    // (size, centre above the feet, tilt forward in degrees, colour) - local space: x right, y up, z forward.
    private static readonly (Vector3 Size, Vector3 Centre, float Tilt, Color Color)[] BodyParts =
    [
        (new(0.46f, 0.34f, 0.40f), new(0f, 0.30f, -0.24f), 0f, Rags),       // hips and bent legs
        (new(0.50f, 0.34f, 0.66f), new(0f, 0.62f, 0.02f), 18f, Rags),       // hunched torso
        (new(0.26f, 0.24f, 0.30f), new(0f, 0.82f, 0.36f), 0f, Skin),        // head, pushed forward
    ];

    // Arms hang from the shoulders (pivot) and are turned around it: x of the shoulder, arm size.
    private static readonly Vector3 ArmSize = new(0.10f, 0.62f, 0.10f);
    private const float ShoulderHeight = 0.66f;
    private const float ShoulderForward = 0.30f;

    private readonly Dictionary<int, State> _states = [];
    private float _frameSeconds;

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
            case GhoulStruck struck:
                StateOf(struck.GhoulId).Lunge = 1f;
                break;
        }
    }

    public void Update(float frameSeconds)
    {
        _frameSeconds = frameSeconds;
        foreach (var state in _states.Values)
        {
            state.Lunge = MathF.Max(0f, state.Lunge - frameSeconds / LungeSeconds);
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

        // Crawling: the arms reach forward in turn, faster the faster it moves.
        var speed = new Vector2(ghoul.Velocity.X, ghoul.Velocity.Z).Length();
        state.CrawlPhase += speed * _frameSeconds * CrawlCyclesPerMetre * 2f * MathF.PI;
        var crawl = MathF.Min(1f, speed / Tuning.Ghoul.Speed);

        // Winding up: rears up and lifts both arms. Blow: a short lunge forward.
        var windUp = ghoul.WindUpProgress;
        var lunge = state.Lunge;

        // Flinch: jerks back and up a little. Death: sinks and rolls onto its side.
        var flinch = state.Flinch * state.Flinch;
        var collapse = 1f - (1f - state.Collapse) * (1f - state.Collapse);
        var body =
            Matrix4x4.CreateRotationX(Deg(-12f * flinch - 22f * windUp + 14f * lunge)) *
            Matrix4x4.CreateTranslation(0f, 0f, 0.25f * lunge) *
            Matrix4x4.CreateRotationZ(Deg(75f * collapse)) *
            Matrix4x4.CreateTranslation(0f, -0.2f * collapse, -0.06f * flinch) *
            Matrix4x4.CreateRotationY(yaw) *
            Matrix4x4.CreateTranslation(ghoul.Position);

        var flashColor = state.FlashIsCritical ? CriticalFlashColor : FlashColor;
        Color Shown(Color color) => state.Flash > 0f ? Blend(color, flashColor, state.Flash) : color;

        foreach (var (size, centre, tilt, color) in BodyParts)
        {
            ShadedBox.Draw(Matrix4x4.CreateRotationX(Deg(tilt)) * Matrix4x4.CreateTranslation(centre) * body, size, Shown(color));
        }

        // Arms: turned around the shoulder. Resting they reach down and forward; crawling they swing in turn;
        // winding up they are lifted high; in the blow they slam forward.
        foreach (var side in (ReadOnlySpan<float>)[-1f, 1f])
        {
            var swing = MathF.Sin(state.CrawlPhase + (side > 0f ? MathF.PI : 0f)) * 28f * crawl;
            var armTilt = 25f + swing + 115f * windUp + 55f * lunge;
            var arm =
                Matrix4x4.CreateTranslation(0f, -ArmSize.Y / 2f, 0f) *
                Matrix4x4.CreateRotationX(Deg(-armTilt)) *
                Matrix4x4.CreateTranslation(0.30f * side, ShoulderHeight, ShoulderForward) *
                body;
            ShadedBox.Draw(arm, ArmSize, Shown(Skin));
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

        public float Lunge { get; set; }

        public float CrawlPhase { get; set; }
    }
}
