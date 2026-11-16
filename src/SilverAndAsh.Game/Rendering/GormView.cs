using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Placeholder Gorm made of boxes (decision record 0007): a hulking troll, 3.2 m tall, grey-green skin and rags, a
/// wooden club with iron bands in the right hand and a rusty chain in the left. He sways as he walks, flashes when hit
/// (red for a shot at his head) and falls onto his back when he dies. His head matches his hit zone. Every attack is
/// announced clearly: hammer = club raised high and a red circle on the floor; chain swing = the chain spun up round
/// his head; chain throw = the hook glows red. Then the shock wave runs as a pale ring along the floor, the hook flies
/// on its chain, and a stuck club glows while he bends forward. Pure presentation - a real model comes with v0.13.
/// </summary>
internal sealed class GormView
{
    private const float FlashSeconds = 0.12f;
    private const float FallSeconds = 1.2f;

    private static readonly Color Skin = new(96, 116, 88, 255);
    private static readonly Color Rags = new(70, 58, 52, 255);
    private static readonly Color Wood = new(104, 72, 44, 255);
    private static readonly Color Iron = new(70, 72, 80, 255);
    private static readonly Color Rust = new(120, 66, 40, 255);
    private static readonly Color FlashColor = new(255, 250, 235, 255);
    private static readonly Color CriticalFlashColor = new(230, 40, 50, 255);
    private static readonly Color Warning = new(200, 30, 30, 255);
    private static readonly Color Wave = new(220, 210, 190, 255);
    private static readonly Color Glow = new(255, 120, 60, 255);

    // (size, centre above the feet, colour) - local space: x right, y up, z forward. Height 3.2 m.
    private static readonly (Vector3 Size, Vector3 Centre, Color Color)[] BodyParts =
    [
        (new(0.50f, 1.20f, 0.55f), new(-0.38f, 0.60f, 0f), Rags),   // left leg
        (new(0.50f, 1.20f, 0.55f), new(0.38f, 0.60f, 0f), Rags),    // right leg
        (new(1.50f, 1.35f, 1.05f), new(0f, 1.85f, 0.05f), Skin),    // torso, hunched
        (new(1.40f, 0.35f, 0.95f), new(0f, 1.25f, 0f), Rags),       // loincloth
        (new(0.70f, 0.60f, 0.70f), new(0f, 2.90f, 0.35f), Skin),    // head, forward (= hit zone)
    ];

    private float _flash;
    private bool _flashIsCritical;
    private bool _dead;
    private float _fall;
    private float _walkPhase;
    private float _glowPhase;

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
        _glowPhase += frameSeconds;
        if (_dead || gorm.IsDead)
        {
            _fall = MathF.Min(1f, _fall + frameSeconds / FallSeconds);
        }

        var speed = new Vector2(gorm.Velocity.X, gorm.Velocity.Z).Length();
        _walkPhase += speed * frameSeconds * 1.6f;
        var sway = MathF.Sin(_walkPhase * MathF.PI) * MathF.Min(1f, speed);

        var yaw = MathF.Atan2(gorm.Facing.X, gorm.Facing.Z);
        var fall = 1f - (1f - _fall) * (1f - _fall);
        var progress = gorm.StateProgress;
        var hammerUp = gorm is { State: GormState.WindingUp, Attack: GormAttack.Hammer } ? progress : 0f;
        var spinning = gorm is { State: GormState.WindingUp, Attack: GormAttack.Swing };
        var hookGlows = gorm is { State: GormState.WindingUp, Attack: GormAttack.Throw };
        var stuck = gorm.State == GormState.Stuck;

        // Stuck: bent forward over the club, head down (as his hit zone).
        var bend = stuck ? 0.45f : 0f;
        var body =
            Matrix4x4.CreateTranslation(0f, -0.3f * bend, 0f) *
            Matrix4x4.CreateRotationX(bend) *
            Matrix4x4.CreateRotationZ(0.06f * sway) *
            Matrix4x4.CreateRotationX(-1.45f * fall) *
            Matrix4x4.CreateTranslation(0f, 0.45f * fall, 0f) *
            Matrix4x4.CreateRotationY(yaw) *
            Matrix4x4.CreateTranslation(gorm.Position);

        var flashColor = _flashIsCritical ? CriticalFlashColor : FlashColor;
        Color Shown(Color color) => _flash > 0f ? Blend(color, flashColor, _flash) : color;

        foreach (var (size, centre, color) in BodyParts)
        {
            ShadedBox.Draw(Matrix4x4.CreateTranslation(centre) * body, size, Shown(color));
        }

        // Arms hang from the shoulders; the right one carries the club, the left one the chain.
        foreach (var side in (ReadOnlySpan<float>)[-1f, 1f])
        {
            // Right arm: lifted high over the head while the hammer winds up, slammed down on the floor while stuck.
            // Left arm: raised for spinning the chain or throwing the hook.
            var lift = side > 0f
                ? (2.6f * hammerUp) + (stuck ? 1.2f : 0f)
                : (spinning ? 2.4f : 0f) + (hookGlows ? 1.4f * progress : 0f);
            var arm =
                Matrix4x4.CreateTranslation(0f, -0.7f, 0f) *
                Matrix4x4.CreateRotationX(-0.25f - 0.15f * sway * side - lift) *
                Matrix4x4.CreateTranslation(0.95f * side, 2.4f, 0.1f) *
                body;
            ShadedBox.Draw(arm, new Vector3(0.38f, 1.4f, 0.38f), Shown(Skin));

            var hand = Matrix4x4.CreateTranslation(0f, -0.75f, 0f) * arm;
            if (side > 0f)
            {
                // The club: a thick wooden head with iron bands on a handle.
                ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, -0.2f, 0.25f) * hand, new Vector3(0.14f, 0.14f, 0.7f), Wood);
                var clubColor = stuck ? Blend(Wood, Glow, 0.5f + 0.5f * MathF.Sin(_glowPhase * 8f)) : Wood;
                ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, -0.2f, 0.85f) * hand, new Vector3(0.42f, 0.42f, 0.6f), clubColor);
                ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, -0.2f, 0.85f) * hand, new Vector3(0.46f, 0.46f, 0.08f), Iron);
            }
            else
            {
                // The chain: a few rusty links hanging down to a hook.
                if (spinning)
                {
                    // The chain whirls round above his head, faster as the swing comes nearer.
                    var angle = _glowPhase * (6f + 10f * progress);
                    for (var link = 0; link < 8; link++)
                    {
                        var spot = new Vector3(MathF.Cos(angle) * 0.25f * (link + 1), 0f, MathF.Sin(angle) * 0.25f * (link + 1));
                        ShadedBox.Draw(Matrix4x4.CreateTranslation(spot) * hand, new Vector3(0.12f, 0.12f, 0.12f), Rust);
                    }
                }
                else if (gorm.Hook is null)
                {
                    for (var link = 0; link < 4; link++)
                    {
                        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, -0.15f - (link * 0.18f), 0f) * hand, new Vector3(0.09f, 0.16f, 0.05f), Rust);
                    }

                    var hookColor = hookGlows ? Blend(Iron, Warning, 0.4f + 0.6f * MathF.Sin(_glowPhase * 12f) * MathF.Sin(_glowPhase * 12f)) : Iron;
                    ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, -0.9f, 0.06f) * hand, new Vector3(0.14f, 0.26f, 0.22f), hookColor);
                }
            }
        }
    }

    /// <summary>
    /// What lies on the floor and in the air around him: the red warning circle before the hammer, the shock wave's
    /// ring, and the flying hook on its chain. Drawn even after the body has fallen (nothing then).
    /// </summary>
    public void DrawEffects(Gorm gorm)
    {
        ArgumentNullException.ThrowIfNull(gorm);
        if (gorm.IsDead)
        {
            return;
        }

        if (gorm is { State: GormState.WindingUp, Attack: GormAttack.Hammer })
        {
            // The circle the wave will cover, pulsing ever brighter.
            var strength = 0.4f + (0.6f * gorm.StateProgress);
            Ring(gorm.Position, gorm.ShockwaveReach, Blend(new Color(60, 10, 10, 255), Warning, strength), 0.06f);
        }

        if (gorm.Shockwave is { } wave)
        {
            Ring(wave.Origin, MathF.Max(0.3f, wave.Radius), Wave, 0.35f);
        }

        if (gorm.Hook is { } hook)
        {
            var from = gorm.Hand;
            var length = Vector3.Distance(from, hook);
            var links = Math.Max(1, (int)(length / 0.3f));
            for (var link = 0; link <= links; link++)
            {
                var spot = Vector3.Lerp(from, hook, link / (float)links);
                ShadedBox.Draw(Matrix4x4.CreateTranslation(spot), new Vector3(0.08f, 0.08f, 0.08f), Rust);
            }

            ShadedBox.Draw(Matrix4x4.CreateTranslation(hook), new Vector3(0.22f, 0.22f, 0.22f), Warning);
        }
    }

    // A flat ring of small boxes on the floor.
    private static void Ring(Vector3 centre, float radius, Color color, float height)
    {
        var pieces = Math.Max(12, (int)(radius * 10f));
        for (var piece = 0; piece < pieces; piece++)
        {
            var angle = piece * 2f * MathF.PI / pieces;
            var spot = centre + new Vector3(MathF.Cos(angle) * radius, (height / 2f) + 0.02f, MathF.Sin(angle) * radius);
            var place = Matrix4x4.CreateRotationY(-angle) * Matrix4x4.CreateTranslation(spot);
            ShadedBox.Draw(place, new Vector3(0.12f, height, 2f * MathF.PI * radius / pieces), color);
        }
    }

    private static Color Blend(Color from, Color to, float amount) => new(
        (byte)(from.R + (to.R - from.R) * amount),
        (byte)(from.G + (to.G - from.G) * amount),
        (byte)(from.B + (to.B - from.B) * amount),
        (byte)255);
}
