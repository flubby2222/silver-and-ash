using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// How the weapons in the player's hands are shown. Behind this interface so the placeholder can later be replaced
/// by real models (v0.10) without touching the game loop.
/// </summary>
internal interface IWeaponView
{
    /// <summary>Reacts to something that happened in the simulation (swing, shot).</summary>
    void OnEvent(GameEvent gameEvent);

    /// <summary>Advances the animations by the real time of the last frame.</summary>
    void Update(float frameSeconds, Player player);

    /// <summary>
    /// Draws the weapons in front of the camera. <paramref name="eye"/> and <paramref name="forward"/> are the camera's
    /// position and looking direction. Call between BeginMode3D and EndMode3D.
    /// </summary>
    void Draw(Vector3 eye, Vector3 forward);
}

/// <summary>
/// Placeholder weapons made of boxes: the silver blade in the left hand, the pistol in the right.
/// Movement by code, all of it soft so the player seems to glide through the world: a slow, gentle bob while walking,
/// hands that trail behind when looking around or walking sideways ("weapon sway"), a dip with the landing nod,
/// a swing of the blade, recoil and a muzzle flash for the pistol.
/// <para>
/// Everything stays within about 30 cm of the eyes. The player keeps at least 35 cm away from every wall
/// (its radius), so the weapons can never disappear into a wall.
/// </para>
/// </summary>
internal sealed class PlaceholderWeaponView : IWeaponView
{
    private const float SwingSeconds = 0.28f;
    private const float RecoilSeconds = 0.18f;
    private const float FlashSeconds = 0.06f;

    // Bob: how far the hands move while walking, and how many bob cycles per metre walked. Small and slow = gliding.
    private const float BobAmount = 0.004f;
    private const float BobCyclesPerMetre = 0.3f;

    // Sway: how far the hands trail behind per radian of turning, the most they trail, and how quickly they catch up
    // (per second - higher = stiffer). Also how far they lean against walking sideways or forward, per m/s.
    private const float TurnSwayPerRadian = 0.05f;
    private const float MaxTurnSway = 0.025f;
    private const float SwayCatchUp = 6f;
    private const float WalkSwayPerSpeed = 0.0016f;

    // Landing: the hands sink this much further than the eyes per metre of landing dip, and tilt down a little.
    private const float DipFollow = 0.35f;
    private const float DipTiltDegreesPerMetre = 60f;

    private static readonly Color Silver = new(206, 210, 222, 255);
    private static readonly Color DarkSilver = new(132, 136, 150, 255);
    private static readonly Color Wood = new(84, 56, 38, 255);
    private static readonly Color Iron = new(58, 56, 64, 255);
    private static readonly Color MuzzleFlash = new(255, 232, 170, 255);

    // Resting places in camera space: x = right, y = up, z = forward (metres).
    private static readonly Vector3 BladeHand = new(-0.11f, -0.095f, 0.19f);
    private static readonly Vector3 PistolHand = new(0.095f, -0.08f, 0.18f);

    private float _swing = 1f;      // 0 = swing starts, 1 = finished
    private float _recoil = 1f;     // 0 = shot fired, 1 = settled
    private float _flash;           // 1 = bright, fading to 0
    private float _bobPhase;
    private float _bobStrength;
    private Vector2 _turnSway;      // x = right, y = up, in metres
    private Vector2 _walkSway;
    private float _dip;             // landing dip of the eyes, followed softly
    private float? _lastYaw;
    private float? _lastPitch;

    public void OnEvent(GameEvent gameEvent)
    {
        switch (gameEvent)
        {
            case BladeSwung:
                _swing = 0f;
                break;
            case ShotFired:
                _recoil = 0f;
                _flash = 1f;
                break;
        }
    }

    public void Update(float frameSeconds, Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        _swing = MathF.Min(1f, _swing + frameSeconds / SwingSeconds);
        _recoil = MathF.Min(1f, _recoil + frameSeconds / RecoilSeconds);
        _flash = MathF.Max(0f, _flash - frameSeconds / FlashSeconds);

        // Bob only while walking on the ground; fades in and out instead of switching on and off.
        var walking = player.IsGrounded ? Math.Clamp(player.HorizontalSpeed / Tuning.Player.MaxSpeed, 0f, 1f) : 0f;
        _bobStrength += (walking - _bobStrength) * MathF.Min(1f, frameSeconds * 8f);
        _bobPhase += player.HorizontalSpeed * frameSeconds * BobCyclesPerMetre * 2f * MathF.PI;

        // Every soft movement uses the same "catch up a part of the way each frame" smoothing, so nothing jumps.
        var catchUp = 1f - MathF.Exp(-SwayCatchUp * frameSeconds);

        // Turning: the hands lag behind the view and then glide back to their place.
        var yaw = player.View.YawRadians;
        var pitch = player.View.PitchRadians;
        if (_lastYaw is { } lastYaw && _lastPitch is { } lastPitch)
        {
            var turned = new Vector2(
                MathF.IEEERemainder(yaw - lastYaw, 2f * MathF.PI),
                pitch - lastPitch);
            _turnSway -= turned * TurnSwayPerRadian;
            if (_turnSway.Length() > MaxTurnSway)
            {
                _turnSway = Vector2.Normalize(_turnSway) * MaxTurnSway;
            }
        }

        _lastYaw = yaw;
        _lastPitch = pitch;
        _turnSway -= _turnSway * catchUp;

        // Walking: the hands lean a little against the direction of movement (sideways and forward/back).
        var velocity = player.Velocity;
        var sideways = Vector3.Dot(velocity, player.View.FlatRight);
        var forwards = Vector3.Dot(velocity, player.View.FlatForward);
        var walkTarget = new Vector2(-sideways, -forwards * 0.3f) * WalkSwayPerSpeed;
        _walkSway += (walkTarget - _walkSway) * catchUp;

        // Landing: follow the eyes' nod a little deeper and a little later.
        _dip += (player.LandingDip.Depth - _dip) * MathF.Min(1f, catchUp * 2f);
    }

    public void Draw(Vector3 eye, Vector3 forward)
    {
        // Bob: a small figure eight - side to side once, up and down twice per cycle.
        var bob = new Vector3(MathF.Sin(_bobPhase) * BobAmount, -MathF.Abs(MathF.Cos(_bobPhase)) * BobAmount, 0f)
            * _bobStrength;
        var sway = new Vector3(_turnSway.X + _walkSway.X, _turnSway.Y + _walkSway.Y - _dip * DipFollow, 0f);

        // All soft movements together, applied to both hands in camera space.
        var motion =
            Matrix4x4.CreateRotationX(Deg(_dip * DipTiltDegreesPerMetre)) *
            Matrix4x4.CreateTranslation(bob + sway) *
            CameraToWorld(eye, forward);

        DrawBlade(motion);
        DrawPistol(motion);
    }

    private void DrawBlade(Matrix4x4 motion)
    {
        // Swing: quickly out across the view from left to right (40 % of the time), then calmly back.
        var t = _swing < 0.4f ? EaseOut(_swing / 0.4f) : 1f - EaseInOut((_swing - 0.4f) / 0.6f);
        var sweep = t * 95f;         // degrees around the camera's up axis, the tip turning to the right
        var tilt = 20f + t * 25f;    // blade leans over while swinging

        var hand =
            Matrix4x4.CreateRotationZ(Deg(-tilt)) *
            Matrix4x4.CreateRotationY(Deg(sweep)) *
            Matrix4x4.CreateTranslation(BladeHand + new Vector3(t * 0.06f, t * 0.04f, 0f)) *
            motion;

        // Handle, guard and the long silver blade pointing forward and a little up.
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0f, -0.03f) * hand, new Vector3(0.022f, 0.022f, 0.07f), Wood);
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0f, 0.01f) * hand, new Vector3(0.055f, 0.012f, 0.012f), DarkSilver);
        ShadedBox.Draw(
            Matrix4x4.CreateRotationX(Deg(-25f)) * Matrix4x4.CreateTranslation(0f, 0.035f, 0.08f) * hand,
            new Vector3(0.016f, 0.005f, 0.15f), Silver);
    }

    private void DrawPistol(Matrix4x4 motion)
    {
        // Recoil: kicked back and up at once, then easing back into place.
        var kick = 1f - EaseOut(_recoil);
        var hand =
            Matrix4x4.CreateRotationX(Deg(-14f * kick)) *
            Matrix4x4.CreateTranslation(PistolHand + new Vector3(0f, 0.012f * kick, -0.035f * kick)) *
            motion;

        // Grip, frame and barrel.
        ShadedBox.Draw(
            Matrix4x4.CreateRotationX(Deg(15f)) * Matrix4x4.CreateTranslation(0f, -0.035f, -0.01f) * hand,
            new Vector3(0.024f, 0.06f, 0.03f), Wood);
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0f, 0.02f) * hand, new Vector3(0.028f, 0.03f, 0.08f), Iron);
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.005f, 0.09f) * hand, new Vector3(0.016f, 0.016f, 0.07f), DarkSilver);

        if (_flash > 0f)
        {
            var size = 0.03f + 0.02f * _flash;
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.005f, 0.14f) * hand, new Vector3(size, size, size), MuzzleFlash);
        }
    }

    /// <summary>
    /// Matrix from camera space (x = right, y = up, z = forward) to world positions.
    /// Right = forward x up: with the view along +Z, right is -X (same as the player's walking directions).
    /// </summary>
    private static Matrix4x4 CameraToWorld(Vector3 eye, Vector3 forward)
    {
        var f = Vector3.Normalize(forward);
        var right = Vector3.Normalize(Vector3.Cross(f, Vector3.UnitY));
        var up = Vector3.Cross(right, f);
        return new Matrix4x4(
            right.X, right.Y, right.Z, 0f,
            up.X, up.Y, up.Z, 0f,
            f.X, f.Y, f.Z, 0f,
            eye.X, eye.Y, eye.Z, 1f);
    }

    private static float Deg(float degrees) => degrees * MathF.PI / 180f;

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

    private static float EaseInOut(float t) => t * t * (3f - 2f * t);
}
