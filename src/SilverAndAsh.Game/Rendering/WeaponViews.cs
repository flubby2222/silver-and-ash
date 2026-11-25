using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

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
/// The weapons in the hands: the silver blade in the left hand, the pistol in the right. Both are the designer's models
/// with their hands (<see cref="WeaponModels"/>, issue #249); the club, the hunter's axe and the whip are models held
/// and swung like the blade (issues #403, #776, #786; the whip's lash see <see cref="WhipPose"/>). Rifle and blunderbuss - and a weapon whose model is missing - are placeholder boxes without a hand.
/// Movement by code, all of it soft so the player seems to glide through the world: a slow, gentle bob while walking,
/// hands that trail behind when looking around or walking sideways ("weapon sway"), a dip with the landing nod,
/// a swing of the blade, recoil and a muzzle flash for the pistol. Models and boxes move the same way.
/// <para>
/// The models are shrunk towards the eye (<see cref="WeaponPose.ShrinkTowardsEye"/>): the picture stays the same, but
/// everything drawn stays within <see cref="WeaponPose.Reach"/> (30 cm) of the eyes, tested - only the sleeves run on
/// out of the picture. A sleeve has two parts (issue #265): the cuff hangs rigidly on the weapon, the forearm turns so it
/// always points at the shoulder (<see cref="WeaponPose.ForearmTurn"/>). The player keeps at least 35 cm away from
/// every wall (its radius), so the weapons never disappear into a wall. The boxes are drawn as they always
/// were (the rifle's barrel reaches about 48 cm, as before).
/// </para>
/// </summary>
internal sealed class WeaponView(WeaponModels? models = null, LevelShader? shader = null) : IWeaponView
{
    private const float RecoilSeconds = 0.18f;
    private const float FlashSeconds = 0.06f;

    // Bob: how far the hands move while walking, and how many bob cycles per metre walked. Small and slow = gliding.
    private const float BobAmount = 0.004f;
    private const float BobCyclesPerMetre = 0.3f;

    // Sway: how far the hands trail behind per radian of turning, the most they trail, and how quickly they catch up
    // (per second - higher = stiffer). Also how far they lean against walking sideways or forward, per m/s.
    private const float TurnSwayPerRadian = 0.05f;
    private const float MaxTurnSway = 0.025f;
    private const float SwayCatchUp = WeaponPose.WindUpCatchUp;
    private const float WalkSwayPerSpeed = 0.0016f;

    // Landing: the hands sink this much further than the eyes per metre of landing dip, and tilt down a little.
    private const float DipFollow = 0.35f;
    private const float DipTiltDegreesPerMetre = 60f;

    private static readonly Color Silver = new(206, 210, 222, 255);
    private static readonly Color DarkSilver = new(132, 136, 150, 255);
    private static readonly Color Wood = new(84, 56, 38, 255);
    private static readonly Color Iron = new(58, 56, 64, 255);
    private static readonly Color MuzzleFlash = new(255, 232, 170, 255);
    private static readonly Color HolyFlash = new(225, 240, 255, 255);
    private static readonly Color HolyGlow = new(235, 245, 255, 255);

    // Resting places in camera space: x = right, y = up, z = forward (metres).
    private static readonly Vector3 BladeHand = new(-0.11f, -0.095f, 0.19f);
    private static readonly Vector3 PistolHand = new(0.095f, -0.08f, 0.18f);

    private float _swing = 1f;      // 0 = swing starts, 1 = finished
    private float _swingSeconds = WeaponPose.FixedSwingSeconds; // the melee weapon's (the axe's from its cooldown)
    private float _windUp;          // 0 = blade at rest, 1 = fully drawn back for a charged swing
    private bool _fullyCharged;
    private float _time;
    private float _recoil = 1f;     // 0 = shot fired, 1 = settled
    private float _flash;           // 1 = bright, fading to 0
    private bool _holyFlash;        // the last shot was consecrated: white-blue flash
    private bool _consecrated;      // consecrated ammunition loaded: the barrel shimmers
    private WeaponKind _melee;      // which melee weapon is in the left hand
    private WeaponKind _ranged;     // which ranged weapon is in the right hand
    private readonly ReloadMotion _reload = new();
    private float _bobPhase;
    private float _bobStrength;
    private Vector2 _turnSway;      // x = right, y = up, in metres
    private Vector2 _walkSway;
    private float _dip;             // landing dip of the eyes, followed softly
    private float? _lastYaw;
    private float? _lastPitch;
    private readonly Matrix4x4[] _pistolTurns = new Matrix4x4[WeaponPose.PistolParts.Count];
    private readonly Matrix4x4[] _pistolFrames = new Matrix4x4[WeaponPose.PistolParts.Count];
    private readonly Matrix4x4[] _oneTurn = [Matrix4x4.Identity];
    private readonly Matrix4x4[] _oneFrame = new Matrix4x4[1];
    private readonly Matrix4x4[] _sleeveTurns = [Matrix4x4.Identity, Matrix4x4.Identity];
    private readonly Matrix4x4[] _sleeveFrames = new Matrix4x4[WeaponPose.HandParts.Count];
    private readonly Matrix4x4[] _whipTurns = new Matrix4x4[WhipPose.Parts.Count];
    private readonly Matrix4x4[] _whipFrames = new Matrix4x4[WhipPose.Parts.Count];
    private readonly Matrix4x4[] _lashFrames = new Matrix4x4[WhipPose.Parts.Count];
    private TexturedModel? _lash;   // the whip whose lash is still to be drawn in the world's depth
    private float _whirl;           // the whip's loop whirling while charging (turns, 0..1)
    private Matrix4x4 _loopMotion;  // the movements for the whip's loop at rest: shrunk a little more (WhipPose.LoopNearer)

    public void OnEvent(GameEvent gameEvent)
    {
        switch (gameEvent)
        {
            case MeleeSwung:
                _swing = 0f;
                _whirl = 0f;
                break;
            case ReloadFinished:
                // The reload ran to its end: the click was the last of it, nothing to fade out.
                _reload.Reset();
                break;
            case ShotFired shot:
                _recoil = 0f;
                _flash = 1f;
                _holyFlash = shot.Consecrated;
                break;
        }
    }

    public void Update(float frameSeconds, Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        // The heavy axe's swing takes a share of its cooldown (issue #776); blade and club keep their fixed time.
        _swingSeconds = WeaponPose.SwingSeconds(player.Melee, player.MeleeCooldown.DurationTicks * SimulationClock.SecondsPerTick);
        _swing = MathF.Min(1f, _swing + frameSeconds / _swingSeconds);
        _recoil = MathF.Min(1f, _recoil + frameSeconds / RecoilSeconds);
        _flash = MathF.Max(0f, _flash - frameSeconds / FlashSeconds);
        _consecrated = player.IsConsecrated;
        _melee = player.Melee;
        _ranged = player.Ranged.Kind;

        // Reloading: the motion follows the magazine of the weapon in the hand, and only if that weapon has a motion of
        // its own; a weapon switch starts from rest, a cancel fades out in a tenth of a second.
        var magazine = player.Ranged.Magazine;
        _reload.Step(_ranged, magazine is { IsReloading: true }, magazine.ReloadProgress, frameSeconds);

        // Bob only while walking on the ground; fades in and out instead of switching on and off.
        var walking = player.IsGrounded ? Math.Clamp(player.HorizontalSpeed / player.WalkSpeed, 0f, 1f) : 0f;
        _bobStrength += (walking - _bobStrength) * MathF.Min(1f, frameSeconds * 8f);
        _bobPhase += player.HorizontalSpeed * frameSeconds * BobCyclesPerMetre * 2f * MathF.PI;

        // Every soft movement uses the same "catch up a part of the way each frame" smoothing, so nothing jumps.
        var catchUp = 1f - MathF.Exp(-SwayCatchUp * frameSeconds);
        _time += frameSeconds;

        // Charging the blade: drawn back a little at once, further the longer it is held.
        var charge = player.MeleeCharge;
        var windUpTarget = charge.IsCharging ? 0.25f + 0.75f * charge.Fraction : 0f;
        _windUp = WeaponPose.WindUpStep(_windUp, windUpTarget, frameSeconds);
        _fullyCharged = charge.IsCharging && charge.Fraction >= 1f;
        _whirl = WhipPose.WhirlStep(_whirl, _windUp, charge.IsCharging, frameSeconds);

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
        var soft =
            Matrix4x4.CreateRotationX(Deg(_dip * DipTiltDegreesPerMetre)) *
            Matrix4x4.CreateTranslation(bob + sway);
        var camera = CameraToWorld(eye, forward);
        var motion = soft * camera;

        // The models: the same movements, then shrunk towards the eye (out of the walls, the picture unchanged).
        var modelMotion = soft * Matrix4x4.CreateScale(WeaponPose.ShrinkTowardsEye) * camera;
        _loopMotion = soft * Matrix4x4.CreateScale(WeaponPose.ShrinkTowardsEye * WhipPose.LoopNearer) * camera;

        // With a depth of their own: their near plane is far closer to the eyes than the world's (issue #624).
        var world = WeaponDepthPass.Begin();
        DrawBlade(motion, modelMotion);
        DrawPistol(motion, modelMotion);
        WeaponDepthPass.End(world);
        DrawLash();
    }

    private void DrawBlade(Matrix4x4 motion, Matrix4x4 modelMotion)
    {
        // Swing: quickly out across the view from left to right (about a quarter of the time), then calmly back.
        var t = WeaponPose.SwingCurve(_swing);
        var sweep = t * WeaponPose.SwingYawDegrees;        // degrees around the camera's up axis, the tip turning to the right
        var tilt = 20f + t * WeaponPose.SwingRollDegrees;   // blade leans over while swinging

        // Wind-up while charging: the tip turns back to the left, the hand pulls back. Fully charged, it trembles.
        var tremble = _fullyCharged ? MathF.Sin(_time * 45f) * 0.0012f : 0f;

        if (models is not null && models.TryGetMelee(_melee, out var blade, out var bladeHand))
        {
            // The sleeve hangs on the grip; the club, the axe and the whip bend in the cuff (issues #417, #776, #786).
            var pose = WeaponPose.Blade(t, _windUp, tremble);
            var grip = pose * modelMotion;
            var bend = WeaponPose.MeleeBend(_melee, t, _windUp);
            if (models.TryGetStrike(_melee, out var strike))
            {
                DrawWhip(blade, strike, bend * pose, motion, modelMotion, t);
            }
            else
            {
                DrawOnePart(blade, bend * grip);
            }

            DrawSleeve(bladeHand, grip, WeaponPose.BladeForearm(t, _windUp, tremble, bladeHand.Rig.Pivots[1]));
            return;
        }

        var windUp = new Vector3(-0.03f * _windUp + tremble, 0.015f * _windUp, -0.04f * _windUp);

        var hand =
            Matrix4x4.CreateRotationZ(Deg(-tilt)) *
            Matrix4x4.CreateRotationY(Deg(sweep - 35f * _windUp)) *
            Matrix4x4.CreateTranslation(BladeHand + windUp + new Vector3(t * 0.06f, t * 0.04f, 0f)) *
            motion;

        if (_melee == WeaponKind.Club)
        {
            // The club: a wooden handle thickening into a heavy head with iron bands.
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0f, -0.01f) * hand, new Vector3(0.022f, 0.022f, 0.1f), Wood);
            var head = Matrix4x4.CreateRotationX(Deg(-25f)) * Matrix4x4.CreateTranslation(0f, 0.03f, 0.1f) * hand;
            ShadedBox.Draw(head, new Vector3(0.038f, 0.038f, 0.11f), Wood);
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0f, -0.02f) * head, new Vector3(0.041f, 0.041f, 0.008f), DarkSilver);
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0f, 0.03f) * head, new Vector3(0.041f, 0.041f, 0.008f), DarkSilver);
            return;
        }

        // Handle, guard and the long silver blade pointing forward and a little up.
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0f, -0.03f) * hand, new Vector3(0.022f, 0.022f, 0.07f), Wood);
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0f, 0.01f) * hand, new Vector3(0.055f, 0.012f, 0.012f), DarkSilver);
        ShadedBox.Draw(
            Matrix4x4.CreateRotationX(Deg(-25f)) * Matrix4x4.CreateTranslation(0f, 0.035f, 0.08f) * hand,
            new Vector3(0.016f, 0.005f, 0.15f), Silver);
    }

    // The whip (issue #786): the rest file with its loop, whirling while charging, drawn whole with the weapons (the loop
    // a little nearer, WhipPose.LoopNearer); or the strike file while a swing is out, its lash rolling out. Then only the
    // handle is drawn with the weapons: the lash reaches metres into the world, so it is posed unshrunk (the same
    // picture) and drawn after the weapons' depth, in the world's (DrawLash).
    private void DrawWhip(TexturedModel rest, TexturedModel strike, Matrix4x4 pose, Matrix4x4 motion, Matrix4x4 modelMotion, float t)
    {
        if (!WhipPose.ShowsStrike(_swing, t))
        {
            WhipPose.RestTurns(_whirl, _whipTurns);
            rest.Rig.Pose(_whipTurns, pose * modelMotion, _whipFrames);
            rest.DrawPart(0, _whipFrames[0]);
            rest.Rig.Pose(_whipTurns, pose * _loopMotion, _lashFrames);
            for (var part = 1; part < _lashFrames.Length; part++)
            {
                rest.DrawPart(part, _lashFrames[part]);
            }

            return;
        }

        WhipPose.LashTurns(_swing, _whipTurns);
        strike.Rig.Pose(_whipTurns, pose * modelMotion, _whipFrames);
        strike.DrawPart(0, _whipFrames[0]);
        strike.Rig.Pose(_whipTurns, pose * motion, _lashFrames);
        _lash = strike;
    }

    // The whip's lash, in the world's depth: walls in front of it hide it.
    private void DrawLash()
    {
        if (_lash is null)
        {
            return;
        }

        for (var part = 1; part < _lashFrames.Length; part++)
        {
            _lash.DrawPart(part, _lashFrames[part]);
        }

        _lash = null;
    }

    private void DrawPistol(Matrix4x4 motion, Matrix4x4 modelMotion)
    {
        // Recoil: kicked back and up at once, then easing back into place.
        var kick = 1f - EaseOut(_recoil);
        if (_ranged == WeaponKind.Pistol && models is { Pistol: { } pistol, PistolHand: { } pistolHand })
        {
            DrawPistolModel(pistol, pistolHand, WeaponPose.Pistol(kick, _reload.Progress, _reload.Weight) * modelMotion, kick);
            return;
        }

        var hand =
            Matrix4x4.CreateRotationX(Deg(-14f * kick)) *
            Matrix4x4.CreateTranslation(PistolHand + new Vector3(0f, 0.012f * kick, -0.035f * kick)) *
            motion;

        // Consecrated ammunition loaded: the barrel shimmers pale and holy.
        var barrel = _consecrated ? Blend(DarkSilver, HolyGlow, 0.45f + 0.35f * MathF.Sin(_time * 6f)) : DarkSilver;
        var muzzle = 0.14f;
        if (_ranged == WeaponKind.Rifle)
        {
            // The rifle: a wooden stock back towards the shoulder, a long barrel far forward.
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, -0.02f, -0.07f) * hand, new Vector3(0.03f, 0.045f, 0.14f), Wood);
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0f, 0.05f) * hand, new Vector3(0.03f, 0.032f, 0.12f), Iron);
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.006f, 0.2f) * hand, new Vector3(0.014f, 0.014f, 0.2f), barrel);
            muzzle = 0.31f;
        }
        else if (_ranged == WeaponKind.Blunderbuss)
        {
            // The blunderbuss: a short stock, a thick barrel and a wide bell-shaped mouth.
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, -0.02f, -0.05f) * hand, new Vector3(0.032f, 0.05f, 0.1f), Wood);
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.004f, 0.06f) * hand, new Vector3(0.03f, 0.03f, 0.13f), barrel);
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.004f, 0.135f) * hand, new Vector3(0.05f, 0.05f, 0.025f), Iron);
            muzzle = 0.16f;
        }
        else
        {
            // Grip, frame and barrel.
            ShadedBox.Draw(
                Matrix4x4.CreateRotationX(Deg(15f)) * Matrix4x4.CreateTranslation(0f, -0.035f, -0.01f) * hand,
                new Vector3(0.024f, 0.06f, 0.03f), Wood);
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0f, 0.02f) * hand, new Vector3(0.028f, 0.03f, 0.08f), Iron);
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.005f, 0.09f) * hand, new Vector3(0.016f, 0.016f, 0.07f), barrel);
        }

        if (_flash > 0f)
        {
            var size = 0.03f + 0.02f * _flash;
            ShadedBox.Draw(
                Matrix4x4.CreateTranslation(0f, 0.005f, muzzle) * hand, new Vector3(size, size, size), _holyFlash ? HolyFlash : MuzzleFlash);
        }
    }

    private void DrawPistolModel(TexturedModel pistol, TexturedModel hand, Matrix4x4 grip, float kick)
    {
        for (var p = 0; p < _pistolTurns.Length; p++)
        {
            _pistolTurns[p] = Matrix4x4.Identity;
        }

        _pistolTurns[2] = Rig.Lift(WeaponPose.ReloadCockDegrees(_reload.Progress, _reload.Weight, kick));
        pistol.Rig.Pose(_pistolTurns, grip, _pistolFrames);

        pistol.DrawPart(0, _pistolFrames[0]);
        // Consecrated ammunition loaded: the barrel shimmers pale and holy, as with the placeholder.
        if (_consecrated)
        {
            shader?.SetFlash(HolyGlow, 0.45f + 0.35f * MathF.Sin(_time * 6f));
        }

        pistol.DrawPart(1, _pistolFrames[1]);
        if (_consecrated)
        {
            shader?.SetFlash(HolyGlow, 0f);
        }

        pistol.DrawPart(2, _pistolFrames[2]);
        pistol.DrawPart(3, _pistolFrames[3]);
        DrawSleeve(hand, grip, WeaponPose.PistolForearm(kick, _reload.Progress, _reload.Weight, hand.Rig.Pivots[1]));

        // The muzzle flash: full brightness whatever the light, pale blue-white after a consecrated shot.
        if (_flash > 0f)
        {
            if (_holyFlash)
            {
                shader?.SetFlash(HolyFlash, 0.8f);
            }

            pistol.DrawPart(4, _pistolFrames[4], tint: 4f);
            if (_holyFlash)
            {
                shader?.SetFlash(HolyFlash, 0f);
            }
        }
    }

    // A sleeve on the grip: the cuff rigidly, the forearm turned around its pivot towards the shoulder.
    private void DrawSleeve(TexturedModel sleeve, Matrix4x4 grip, Matrix4x4 forearmTurn)
    {
        _sleeveTurns[1] = forearmTurn;
        sleeve.Rig.Pose(_sleeveTurns, grip, _sleeveFrames);
        sleeve.DrawPart(0, _sleeveFrames[0]);
        sleeve.DrawPart(1, _sleeveFrames[1]);
    }

    // A model of one part (the sword) on the grip.
    private void DrawOnePart(TexturedModel model, Matrix4x4 grip)
    {
        model.Rig.Pose(_oneTurn, grip, _oneFrame);
        model.DrawPart(0, _oneFrame[0]);
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

    private static Color Blend(Color from, Color to, float amount) => new(
        (byte)(from.R + (to.R - from.R) * amount),
        (byte)(from.G + (to.G - from.G) * amount),
        (byte)(from.B + (to.B - from.B) * amount),
        (byte)255);

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

    private static float EaseInOut(float t) => t * t * (3f - 2f * t);
}
