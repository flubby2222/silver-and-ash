using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Gorm as the designer's model (assets/models/gorm.glb, issue #249): iron mask, a heavy pale chain, the club, and in
/// phase 2 his body texture with the red marks (gorm_body_phase2.png). The movements of the placeholder stay, in the
/// designer's poses (<see cref="GormPose"/>): hammer = club raised high (plus the red circle on the floor); swing =
/// the chain whirled above his head, as long as it reaches; throw = the hook glows red. The chain is laid link by link
/// (<see cref="GormChain"/>) and always ends where it hits in the game: sweeping, as far as the swing reaches at the height
/// it hits; thrown, at the hook's place in the simulation. A stuck club glows while he bends forward, he flashes when
/// hit (red for his head), roars into phase 2 and falls onto his back when he dies.
/// </summary>
internal sealed class ModelGormView(TexturedModel model, LevelShader shader) : IGormView
{
    /// <summary>The picture of phase 2 next to the model - give it to <see cref="TexturedModel.TryLoad"/> as alternate 0.</summary>
    public const string Phase2Texture = "gorm_body_phase2.png";

    private const float FlashSeconds = 0.12f;
    private const float FallSeconds = 1.2f;

    // Turns of the whirled chain while he winds up the swing; it ends where the sweep begins (his left).
    private const float WhirlTurns = 2f;

    // At rest the chain hangs from his fist to the floor and lies there this far (metres), out and forward.
    private const float DragLength = 1.1f;

    private static readonly Color FlashColor = new(255, 250, 235, 255);
    private static readonly Color CriticalFlashColor = new(230, 40, 50, 255);
    private static readonly Color Warning = new(200, 30, 30, 255);
    private static readonly Color Glow = new(255, 120, 60, 255);
    private static readonly Vector3 DragDirection = Vector3.Normalize(new Vector3(-0.5f, 0f, 0.85f));

    private static readonly int ChainForearm = GormPose.IndexOf(GormPose.ChainForearm);
    private static readonly int ClubIndex = GormPose.IndexOf(GormPose.Club);
    private static readonly int LinkIndex = GormPose.IndexOf(GormPose.ChainLink);
    private static readonly int HookIndex = GormPose.IndexOf(GormPose.Hook);
    private static readonly HashSet<int> Phase2Parts =
        [GormPose.IndexOf(GormPose.Torso), GormPose.IndexOf(GormPose.Head), GormPose.IndexOf(GormPose.Jaw)];

    private readonly GormJoint[] _joints = new GormJoint[GormPose.Parts.Count];
    private readonly Matrix4x4[] _turns = new Matrix4x4[GormPose.Parts.Count];
    private readonly Matrix4x4[] _frames = new Matrix4x4[GormPose.Parts.Count];
    private readonly List<Matrix4x4> _links = [];

    private float _flash;
    private bool _flashIsCritical;
    private bool _dead;
    private float _fall;
    private float _stridePhase;
    private float _time;

    public void OnEvent(GameEvent gameEvent)
    {
        switch (gameEvent)
        {
            case GormHit hit:
                _flash = 1f;
                _flashIsCritical = hit.Critical;
                break;
            case GormDied:
                _dead = true;
                break;
        }
    }

    public void Draw(Gorm gorm, float frameSeconds)
    {
        ArgumentNullException.ThrowIfNull(gorm);
        _flash = MathF.Max(0f, _flash - frameSeconds / FlashSeconds);
        _time += frameSeconds;
        var dead = _dead || gorm.IsDead;
        if (dead)
        {
            _fall = MathF.Min(1f, _fall + frameSeconds / FallSeconds);
        }

        // As the placeholder: the stride runs with his speed; dead he falls onto his back.
        var speed = new Vector2(gorm.Velocity.X, gorm.Velocity.Z).Length();
        _stridePhase += speed * frameSeconds * 1.6f * MathF.PI;
        var motion = GormMotion.Of(gorm, _stridePhase, MathF.Min(1f, speed));
        if (dead)
        {
            motion = motion with { State = GormState.Recovering, Attack = null, SwingAngle = null, Stride = 0f };
        }

        var fall = 1f - (1f - _fall) * (1f - _fall);
        var figure =
            Matrix4x4.CreateRotationX(-1.45f * fall) *
            Matrix4x4.CreateTranslation(0f, 0.45f * fall, 0f) *
            Matrix4x4.CreateRotationY(MathF.Atan2(gorm.Facing.X, gorm.Facing.Z)) *
            Matrix4x4.CreateTranslation(gorm.Position);

        GormPose.Joints(motion, _joints);
        GormPose.Turns(_joints, _turns);
        model.Rig.Pose(_turns, figure, _frames);

        // A hit flash tints everything and wins over the glow of the club and the hook.
        var flashing = _flash > 0f;
        if (flashing)
        {
            shader.SetFlash(_flashIsCritical ? CriticalFlashColor : FlashColor, _flash);
        }

        // Phase 2 changes the texture of his body, head and jaw: the red marks.
        int? skin = gorm.IsInPhase2 ? 0 : null;
        for (var part = 0; part < GormPose.BodyPartCount; part++)
        {
            if (part == ClubIndex && !flashing && gorm.State == GormState.Stuck && !dead)
            {
                // The stuck club glows - the moment to hit him (as the placeholder).
                var pulse = MathF.Sin(_time * 8f);
                shader.SetFlash(Glow, 0.3f + (0.35f * pulse * pulse));
                model.DrawPart(part, _frames[part]);
                shader.SetFlash(Glow, 0f);
                continue;
            }

            model.DrawPart(part, _frames[part], alternate: Phase2Parts.Contains(part) ? skin : null);
        }

        var hookGlows = !flashing && !dead && gorm is { State: GormState.WindingUp, Attack: GormAttack.Throw };
        DrawChain(gorm, motion, dead, hookGlows);

        if (flashing)
        {
            shader.SetFlash(FlashColor, 0f);
        }
    }

    public void DrawEffects(Gorm gorm)
    {
        ArgumentNullException.ThrowIfNull(gorm);
        if (!gorm.IsDead)
        {
            GormFloorEffects.Draw(gorm);
        }
    }

    // The chain from his fist, link by link, with the hook at its end - as far as it reaches in the game.
    private void DrawChain(Gorm gorm, GormMotion motion, bool dead, bool hookGlows)
    {
        var fist = Vector3.Transform(GormPose.Fist, _frames[ChainForearm]);
        Matrix4x4 hook;
        _links.Clear();

        if (!dead && gorm.SwingDirection is { } sweep)
        {
            // The sweep: stretched out to exactly where the swing reaches, at the middle of the height it hits.
            var end = GormChain.SwingEnd(gorm.Position, sweep, gorm.SwingArea);
            var along = Vector3.Normalize(end - fist);
            GormChain.Lay(fist, GormChain.HookEye(end, along), _links);
            hook = GormChain.HookAt(end, along, Vector3.UnitY);
        }
        else if (!dead && motion is { State: GormState.WindingUp, Attack: GormAttack.Swing, Progress: >= GormPose.SwingArmRaised })
        {
            // Arm raised: whirled above his head, as long as the swing will reach; two turns, ending at his left where
            // the sweep begins. (While he raises the arm the chain still hangs, as at rest.)
            var whirl = (motion.Progress - GormPose.SwingArmRaised) / (1f - GormPose.SwingArmRaised);
            var angle = (-MathF.PI / 2f) - ((1f - whirl) * WhirlTurns * 2f * MathF.PI);
            var right = new Vector3(-gorm.Facing.Z, 0f, gorm.Facing.X);
            var direction = (gorm.Facing * MathF.Cos(angle)) + (right * MathF.Sin(angle));
            var end = GormChain.WhirlEnd(gorm.Position, fist, direction, gorm.SwingArea.Reach);
            var along = Vector3.Normalize(end - fist);
            GormChain.Lay(fist, GormChain.HookEye(end, along), _links);
            hook = GormChain.HookAt(end, along, Vector3.UnitY);
        }
        else if (!dead && gorm.Hook is { } flying)
        {
            // Thrown: the chain runs from his fist to the hook, whose middle sits where the simulation has it.
            var along = Vector3.Normalize(flying - fist);
            var tip = flying + (along * (GormChain.HookLength / 2f));
            GormChain.Lay(fist, GormChain.HookEye(tip, along), _links);
            hook = GormChain.HookAt(tip, along, Vector3.UnitY);
        }
        else if (hookGlows)
        {
            // Ready to throw: the hook hangs short below his fist and glows (forward over the floor while the fist is low).
            hook = GormChain.Ready(fist, gorm.Position.Y, gorm.Facing, _links);
        }
        else
        {
            // At rest: down from his fist to the floor, then lying there, out and forward.
            var floor = (fist with { Y = gorm.Position.Y + 0.03f });
            var drag = Vector3.Normalize(Vector3.TransformNormal(DragDirection, Matrix4x4.CreateRotationY(MathF.Atan2(gorm.Facing.X, gorm.Facing.Z))));
            var tip = floor + (drag * DragLength);
            var eye = GormChain.HookEye(tip, drag);
            if (fist.Y - floor.Y > GormChain.LinkLength)
            {
                GormChain.Lay(fist, floor, _links);
                GormChain.Lay(floor, eye, _links, _links.Count);
            }
            else
            {
                GormChain.Lay(fist, eye, _links);
            }

            hook = GormChain.HookAt(tip, drag, Vector3.UnitY);
        }

        foreach (var link in _links)
        {
            model.DrawPart(LinkIndex, link);
        }

        if (hookGlows)
        {
            var pulse = MathF.Sin(_time * 12f);
            shader.SetFlash(Warning, 0.4f + (0.5f * pulse * pulse));
            model.DrawPart(HookIndex, hook);
            shader.SetFlash(Warning, 0f);
        }
        else
        {
            model.DrawPart(HookIndex, hook);
        }
    }
}
