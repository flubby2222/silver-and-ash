using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Placeholder Gorm made of boxes (decision record 0007): a hulking troll, 3.2 m tall, grey-green skin and rags, a
/// wooden club with iron bands in the right hand and a rusty chain in the left. He sways as he walks, flashes when hit
/// (red for a shot at his head) and falls onto his back when he dies. His head matches his hit zone. Pure
/// presentation - a real model comes with v0.13.
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
        if (_dead || gorm.IsDead)
        {
            _fall = MathF.Min(1f, _fall + frameSeconds / FallSeconds);
        }

        var speed = new Vector2(gorm.Velocity.X, gorm.Velocity.Z).Length();
        _walkPhase += speed * frameSeconds * 1.6f;
        var sway = MathF.Sin(_walkPhase * MathF.PI) * MathF.Min(1f, speed);

        var yaw = MathF.Atan2(gorm.Facing.X, gorm.Facing.Z);
        var fall = 1f - (1f - _fall) * (1f - _fall);
        var body =
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
            var arm =
                Matrix4x4.CreateTranslation(0f, -0.7f, 0f) *
                Matrix4x4.CreateRotationX(-0.25f - 0.15f * sway * side) *
                Matrix4x4.CreateTranslation(0.95f * side, 2.4f, 0.1f) *
                body;
            ShadedBox.Draw(arm, new Vector3(0.38f, 1.4f, 0.38f), Shown(Skin));

            var hand = Matrix4x4.CreateTranslation(0f, -0.75f, 0f) * arm;
            if (side > 0f)
            {
                // The club: a thick wooden head with iron bands on a handle.
                ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, -0.2f, 0.25f) * hand, new Vector3(0.14f, 0.14f, 0.7f), Wood);
                ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, -0.2f, 0.85f) * hand, new Vector3(0.42f, 0.42f, 0.6f), Wood);
                ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, -0.2f, 0.85f) * hand, new Vector3(0.46f, 0.46f, 0.08f), Iron);
            }
            else
            {
                // The chain: a few rusty links hanging down to a hook.
                for (var link = 0; link < 4; link++)
                {
                    ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, -0.15f - (link * 0.18f), 0f) * hand, new Vector3(0.09f, 0.16f, 0.05f), Rust);
                }

                ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, -0.9f, 0.06f) * hand, new Vector3(0.08f, 0.2f, 0.18f), Iron);
            }
        }
    }

    private static Color Blend(Color from, Color to, float amount) => new(
        (byte)(from.R + (to.R - from.R) * amount),
        (byte)(from.G + (to.G - from.G) * amount),
        (byte)(from.B + (to.B - from.B) * amount),
        (byte)255);
}
