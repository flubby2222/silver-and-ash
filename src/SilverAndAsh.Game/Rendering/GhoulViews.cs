using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// How ghouls are shown. Behind this interface so the model (<see cref="ModelGhoulView"/>) and the placeholder
/// (<see cref="PlaceholderGhoulView"/>, when the model cannot be loaded) can be swapped without touching the game loop.
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
/// What every ghoul view keeps per ghoul between frames: the flash and flinch of a hit, the blow, the collapse after
/// death and where in the crawling cycle it is. Shared by the model and the placeholder, so both move alike.
/// </summary>
internal sealed class GhoulAnimations
{
    private const float FlashSeconds = 0.15f;
    private const float CriticalFlashSeconds = 0.35f;
    private const float FlinchSeconds = 0.2f;
    private const float CollapseSeconds = 0.4f;
    private const float LungeSeconds = 0.2f;
    private const float CrawlCyclesPerMetre = 0.9f;

    /// <summary>The flash of a normal hit.</summary>
    public static readonly Color FlashColor = new(255, 250, 235, 255);

    /// <summary>The flash of a critical hit (headshot): blood red.</summary>
    public static readonly Color CriticalFlashColor = new(230, 40, 50, 255);

    private readonly Dictionary<int, GhoulAnimation> _states = [];
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

    /// <summary>
    /// The state of <paramref name="ghoul"/> for this frame. Call once per frame and ghoul, when drawing it: it moves the
    /// crawling cycle on - faster the faster the ghoul moves.
    /// </summary>
    public GhoulAnimation Advance(Ghoul ghoul)
    {
        var state = StateOf(ghoul.Id);
        var speed = new Vector2(ghoul.Velocity.X, ghoul.Velocity.Z).Length();
        state.CrawlPhase += speed * _frameSeconds * CrawlCyclesPerMetre * 2f * MathF.PI;
        state.Crawl = MathF.Min(1f, speed / ghoul.Speed);
        return state;
    }

    private GhoulAnimation StateOf(int id)
    {
        if (!_states.TryGetValue(id, out var state))
        {
            state = new GhoulAnimation();
            _states[id] = state;
        }

        return state;
    }
}

/// <summary>The animation state of one ghoul (see <see cref="GhoulAnimations"/>).</summary>
internal sealed class GhoulAnimation
{
    /// <summary>1 right after a hit, falling to 0.</summary>
    public float Flash { get; set; }

    public bool FlashIsCritical { get; set; }

    /// <summary>1 right after a hit, falling to 0 faster.</summary>
    public float Flinch { get; set; }

    public bool IsDead { get; set; }

    /// <summary>0 alive, rising to 1 after death.</summary>
    public float Collapse { get; set; }

    /// <summary>1 right when it strikes, falling to 0.</summary>
    public float Lunge { get; set; }

    /// <summary>Where in the crawling cycle it is, radians.</summary>
    public float CrawlPhase { get; set; }

    /// <summary>How much it crawls, 0 (still) .. 1 (full speed).</summary>
    public float Crawl { get; set; }

    /// <summary>The flash colour of the last hit.</summary>
    public Color FlashColor => FlashIsCritical ? GhoulAnimations.CriticalFlashColor : GhoulAnimations.FlashColor;

    /// <summary>
    /// The colour the eyes of <paramref name="ghoul"/> glow in, or null when they do not glow: while it announces a
    /// blow, the step of the blood red ramp <see cref="GhoulEyes"/> picks (issue #447).
    /// </summary>
    public static Color? EyeGlow(Ghoul ghoul) => GhoulEyes.GlowStep(ghoul) switch
    {
        // Rows of "Blutrot" in assets/palette/hauptpalette.json.
        3 => new Color(150, 20, 30, 255),
        5 => new Color(200, 30, 40, 255),
        6 => new Color(225, 58, 48, 255),
        null => null,
        var step => throw new InvalidOperationException($"No colour for blood red step {step}."),
    };

    /// <summary>The flinch eased: a fast jerk, a soft return.</summary>
    public float EasedFlinch => Flinch * Flinch;

    /// <summary>The collapse eased: falls fast, settles softly.</summary>
    public float EasedCollapse => 1f - (1f - Collapse) * (1f - Collapse);
}

/// <summary>
/// The ghoul as the grafikdesigner modelled it (assets/models/ghoul.glb): a gaunt human crawling on all fours, its
/// parts turned by code (<see cref="GhoulPose"/>). Crawling, the arms reach forward in turn and the legs push; winding
/// up, it rears up around the hip, lifts its arms and opens its jaw wide (the warning to step away); the blow throws
/// the torso and arms forward. It flashes when hit (blood red for a headshot), flinches back, and at death it sinks
/// and rolls onto its side - the same movements as the placeholder. While it winds up, two small boxes in its eye
/// sockets glow blood red (issue #447); they turn with the head.
/// </summary>
internal sealed class ModelGhoulView(TexturedModel model, LevelShader shader) : IGhoulView
{
    // The eye sockets, as tools/models/ghoul.py builds the head: centre (0, 0.81, 0.34), tilted up 14 degrees and
    // rolled 10 degrees to the side; the sockets are the dents of the ring 0.005 below the centre, 0.032 to each side.
    private static readonly Vector3 HeadCentre = new(0f, 0.81f, 0.34f);
    private static readonly Vector3 EyeSize = new(0.024f, 0.018f, 0.012f);   // across, up, deep - small, inside the dent
    private static readonly Matrix4x4[] EyeBoxes = EyeBoxesInFigure();

    private readonly GhoulAnimations _animations = new();
    private readonly int _head = IndexOfHead(model);
    private readonly float[] _degrees = new float[GhoulPose.Parts.Count];
    private readonly Matrix4x4[] _turns = new Matrix4x4[GhoulPose.Parts.Count];
    private readonly Matrix4x4[] _frames = new Matrix4x4[GhoulPose.Parts.Count];

    public void OnEvent(GameEvent gameEvent) => _animations.OnEvent(gameEvent);

    public void Update(float frameSeconds) => _animations.Update(frameSeconds);

    public void Draw(Ghoul ghoul)
    {
        ArgumentNullException.ThrowIfNull(ghoul);
        if (ghoul.IsGone)
        {
            return;
        }

        var state = _animations.Advance(ghoul);

        // The parts: crawling, winding up and the blow (GhoulPose, tested in Core).
        GhoulPose.Angles(new GhoulMotion(ghoul.WindUpProgress, state.Lunge, state.CrawlPhase, state.Crawl), _degrees);
        for (var i = 0; i < _degrees.Length; i++)
        {
            _turns[i] = Rig.Lift(_degrees[i]);
        }

        // The whole figure, as the placeholder: the blow carries it forward; a hit jerks it back and up a little;
        // at death it sinks and rolls onto its side. Then turned to where it faces and put where it is.
        var flinch = state.EasedFlinch;
        var collapse = state.EasedCollapse;
        var figure =
            Matrix4x4.CreateRotationX(Deg(-12f * flinch)) *
            Matrix4x4.CreateTranslation(0f, 0f, 0.25f * state.Lunge) *
            Matrix4x4.CreateRotationZ(Deg(75f * collapse)) *
            Matrix4x4.CreateTranslation(0f, -0.2f * collapse, -0.06f * flinch) *
            Matrix4x4.CreateRotationY(MathF.Atan2(ghoul.Facing.X, ghoul.Facing.Z)) *
            Matrix4x4.CreateTranslation(ghoul.ShownPosition);

        model.Rig.Pose(_turns, figure, _frames);

        if (state.Flash > 0f)
        {
            shader.SetFlash(state.FlashColor, state.Flash);
            model.Draw(_frames);
            shader.SetFlash(state.FlashColor, 0f);
        }
        else
        {
            model.Draw(_frames);
        }

        if (GhoulAnimation.EyeGlow(ghoul) is { } glow)
        {
            // From the figure's own space to "relative to the head pivot", then with the head into the world.
            var toHead = Matrix4x4.CreateTranslation(-model.Rig.Pivots[_head]) * _frames[_head];
            foreach (var eye in EyeBoxes)
            {
                ShadedBox.DrawGlowing(eye * toHead, EyeSize, glow);
            }
        }
    }

    private static int IndexOfHead(TexturedModel model)
    {
        var index = model.Rig.Names.ToList().IndexOf(GhoulPose.Head);
        return index >= 0 ? index : throw new InvalidOperationException("The ghoul model has no head.");
    }

    // The two eye boxes in the figure's own space, turned like the head (same axes as the model script).
    private static Matrix4x4[] EyeBoxesInFigure()
    {
        var tilt = Deg(14f);
        var roll = Deg(10f);
        var up = Vector3.Normalize(new Vector3(MathF.Sin(roll), MathF.Cos(roll) * MathF.Cos(tilt), -MathF.Sin(tilt) * MathF.Cos(roll)));
        var forward = new Vector3(0f, MathF.Sin(tilt), MathF.Cos(tilt));
        forward = Vector3.Normalize(forward - up * Vector3.Dot(forward, up));
        var side = Vector3.Normalize(Vector3.Cross(up, forward));

        var socketRow = HeadCentre + up * -0.005f;
        return [Box(-1f), Box(1f)];

        Matrix4x4 Box(float sign)
        {
            var centre = socketRow + side * (0.032f * sign) + forward * 0.052f;
            return new Matrix4x4(
                side.X, side.Y, side.Z, 0f,
                up.X, up.Y, up.Z, 0f,
                forward.X, forward.Y, forward.Z, 0f,
                centre.X, centre.Y, centre.Z, 1f);
        }
    }

    private static float Deg(float degrees) => degrees * MathF.PI / 180f;
}

/// <summary>
/// Placeholder ghoul made of boxes, drawn when the model cannot be loaded: crouched hips, a hunched torso, the head
/// pushed forward, long arms reaching for the ground. Movement by code: the arms reach forward in turn while it
/// crawls; winding up, it rears up and lifts its arms (the warning to step away); the blow is a short lunge. It
/// flashes when hit (blood red for a headshot), flinches back, and at death it sinks and rolls onto its side. While it
/// winds up, its eyes glow blood red.
/// </summary>
internal sealed class PlaceholderGhoulView : IGhoulView
{
    private static readonly Color Skin = new(124, 132, 112, 255);
    private static readonly Color Rags = new(62, 52, 54, 255);

    // (size, centre above the feet, tilt forward in degrees, colour) - local space: x right, y up, z forward.
    private static readonly (Vector3 Size, Vector3 Centre, float Tilt, Color Color)[] BodyParts =
    [
        (new(0.46f, 0.34f, 0.40f), new(0f, 0.30f, -0.24f), 0f, Rags),       // hips and bent legs
        (new(0.50f, 0.34f, 0.66f), new(0f, 0.62f, 0.02f), 18f, Rags),       // hunched torso
        (new(0.26f, 0.24f, 0.30f), new(0f, 0.82f, 0.36f), 0f, Skin),        // head, pushed forward (= its hit zone, BodySizes.Ghoul)
    ];

    // Arms hang from the shoulders (pivot) and are turned around it: x of the shoulder, arm size.
    private static readonly Vector3 ArmSize = new(0.10f, 0.62f, 0.10f);
    private const float ShoulderHeight = 0.66f;
    private const float ShoulderForward = 0.30f;

    private readonly GhoulAnimations _animations = new();

    public void OnEvent(GameEvent gameEvent) => _animations.OnEvent(gameEvent);

    public void Update(float frameSeconds) => _animations.Update(frameSeconds);

    public void Draw(Ghoul ghoul)
    {
        ArgumentNullException.ThrowIfNull(ghoul);
        if (ghoul.IsGone)
        {
            return;
        }

        var state = _animations.Advance(ghoul);
        var yaw = MathF.Atan2(ghoul.Facing.X, ghoul.Facing.Z);

        // Crawling: the arms reach forward in turn, faster the faster it moves.
        var crawl = state.Crawl;

        // Winding up: rears up and lifts both arms. Blow: a short lunge forward.
        var windUp = ghoul.WindUpProgress;
        var lunge = state.Lunge;

        // Flinch: jerks back and up a little. Death: sinks and rolls onto its side.
        var flinch = state.EasedFlinch;
        var collapse = state.EasedCollapse;
        var body =
            Matrix4x4.CreateRotationX(Deg(-12f * flinch - 22f * windUp + 14f * lunge)) *
            Matrix4x4.CreateTranslation(0f, 0f, 0.25f * lunge) *
            Matrix4x4.CreateRotationZ(Deg(75f * collapse)) *
            Matrix4x4.CreateTranslation(0f, -0.2f * collapse, -0.06f * flinch) *
            Matrix4x4.CreateRotationY(yaw) *
            Matrix4x4.CreateTranslation(ghoul.ShownPosition);

        Color Shown(Color color) => state.Flash > 0f ? Blend(color, state.FlashColor, state.Flash) : color;

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

        // Announcing a blow: two eyes glow blood red on the front of the head (issue #447).
        if (GhoulAnimation.EyeGlow(ghoul) is { } glow)
        {
            foreach (var side in (ReadOnlySpan<float>)[-1f, 1f])
            {
                ShadedBox.DrawGlowing(Matrix4x4.CreateTranslation(0.06f * side, 0.86f, 0.51f) * body, PlaceholderEyeSize, glow);
            }
        }
    }

    // Half inside the front of the head box (front face at z = 0.36 + 0.30 / 2 = 0.51).
    private static readonly Vector3 PlaceholderEyeSize = new(0.05f, 0.03f, 0.02f);

    private static float Deg(float degrees) => degrees * MathF.PI / 180f;

    private static Color Blend(Color from, Color to, float amount) => new(
        (byte)(from.R + (to.R - from.R) * amount),
        (byte)(from.G + (to.G - from.G) * amount),
        (byte)(from.B + (to.B - from.B) * amount),
        (byte)255);
}
