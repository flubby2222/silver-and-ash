using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Placeholder Gorm made of boxes (decision record 0007): a hulking troll, 3.2 m tall, grey-green skin and rags, a
/// wooden club with iron bands in the right hand and a rusty chain in the left. He sways as he walks, flashes when hit
/// (red for a shot at his head) and falls onto his back when he dies. His head matches his hit zone. Every attack is
/// announced clearly: hammer = club raised high and a red circle on the floor; chain swing = the chain spun up round
/// his head, as long as it reaches; chain throw = the hook glows red. Then the swing sweeps round as a rusty band at
/// chest height, the shock wave runs as a pale ring along the floor, the hook flies
/// on its chain, and a stuck club glows while he bends forward. At half health he roars, head thrown back and arms
/// raised (phase 2); falling stones cast a dark shadow that grows on the floor until the stone drops into it.
/// Pure presentation - a real model comes with v0.13.
/// </summary>
internal sealed class GormView
{
    private const float FlashSeconds = 0.12f;
    private const float FallSeconds = 1.2f;
    private const float SwingSeconds = 0.3f;

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
    private static readonly Color Shadow = new(12, 10, 10, 255);
    private static readonly Color Stone = new(96, 92, 88, 255);

    // Index of the head in BodyParts and the neck it tips back on while roaring.
    private const int Head = 4;
    private static readonly Vector3 Neck = new(0f, 2.6f, 0.15f);

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
    private float _swing;

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
            case GormSwung:
                _swing = 1f;
                break;
        }
    }

    public void Draw(Gorm gorm, float frameSeconds)
    {
        ArgumentNullException.ThrowIfNull(gorm);
        _flash = MathF.Max(0f, _flash - frameSeconds / FlashSeconds);
        _glowPhase += frameSeconds;
        _swing = MathF.Max(0f, _swing - frameSeconds / SwingSeconds);
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

        // Roaring: leaning back, head in the neck, both arms up - quickly in, held, then down again.
        var roar = gorm.State == GormState.Roaring ? MathF.Min(1f, MathF.Min(progress, 1f - progress) * 6f) : 0f;

        // Stuck: bent forward over the club, head down (as his hit zone).
        var bend = (stuck ? 0.45f : 0f) - (0.25f * roar);
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

        for (var part = 0; part < BodyParts.Length; part++)
        {
            var (size, centre, color) = BodyParts[part];
            var place = part == Head
                ? Matrix4x4.CreateTranslation(centre - Neck) * Matrix4x4.CreateRotationX(-0.8f * roar) * Matrix4x4.CreateTranslation(Neck)
                : Matrix4x4.CreateTranslation(centre);
            ShadedBox.Draw(place * body, size, Shown(color));
        }

        // Arms hang from the shoulders; the right one carries the club, the left one the chain.
        foreach (var side in (ReadOnlySpan<float>)[-1f, 1f])
        {
            // Right arm: lifted high over the head while the hammer winds up, slammed down on the floor while stuck.
            // Left arm: raised for spinning the chain or throwing the hook.
            var lift = (2.2f * roar) + (side > 0f
                ? (2.6f * hammerUp) + (stuck ? 1.2f : 0f)
                : (spinning ? 2.4f : 0f) + (hookGlows ? 1.4f * progress : 0f));
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
                    // The chain whirls round above his head, as long as the swing reaches (so you see how far to
                    // stay away), faster as the swing comes nearer. It hangs from his raised hand, level with the floor.
                    var angle = _glowPhase * (6f + 10f * progress);
                    var reach = gorm.SwingArea.Reach;
                    var links = Math.Max(8, (int)(reach / 0.4f));
                    var from = hand.Translation;
                    var outwards = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                    for (var link = 1; link <= links; link++)
                    {
                        var spot = from + (outwards * (reach * link / links));
                        ShadedBox.Draw(Matrix4x4.CreateTranslation(spot), new Vector3(0.14f, 0.14f, 0.14f), Rust);
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
    /// ring, the flying hook on its chain, and the falling stones with their shadows. Drawn even after the body has
    /// fallen (nothing then).
    /// </summary>
    public void DrawEffects(Gorm gorm)
    {
        ArgumentNullException.ThrowIfNull(gorm);
        if (gorm.IsDead)
        {
            return;
        }

        foreach (var stone in gorm.Stones)
        {
            // The shadow grows to the stone's full reach; in the last part of the warning the stone drops into it.
            var shadow = gorm.StoneReach * (0.35f + (0.65f * stone.Progress));
            Raylib.DrawCylinder(stone.At + new Vector3(0f, 0.02f, 0f), shadow, shadow, 0.03f, 20, Shadow);
            const float DropFrom = 0.6f;
            if (stone.Progress > DropFrom)
            {
                var t = (stone.Progress - DropFrom) / (1f - DropFrom);
                var height = (Cell.DefaultCeilingHeight - 0.3f) * (1f - (t * t));
                var spin = Matrix4x4.CreateRotationY(stone.At.X) * Matrix4x4.CreateRotationX(stone.At.Z);
                ShadedBox.Draw(spin * Matrix4x4.CreateTranslation(stone.At + new Vector3(0f, 0.3f + height, 0f)), new Vector3(0.6f, 0.5f, 0.55f), Stone);
            }
        }

        if (gorm is { State: GormState.WindingUp, Attack: GormAttack.Hammer })
        {
            // The circle the wave will cover, pulsing ever brighter.
            var strength = 0.4f + (0.6f * gorm.StateProgress);
            Ring(gorm.Position, gorm.ShockwaveReach, Blend(new Color(60, 10, 10, 255), Warning, strength), 0.06f);
        }

        if (_swing > 0f)
        {
            // The swing itself: for a moment a band all round him, exactly as far and as high as it hits.
            var (reach, low, high) = gorm.SwingArea;
            Ring(gorm.Position + new Vector3(0f, low, 0f), reach, Rust, high - low);
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
