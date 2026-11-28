using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Consumables lying in the level, floating a hand's width above the floor and turning slowly - easy to spot in the
/// dark: a blood vial pulsing red, or three cartridges of consecrated ammunition with glowing silver tips. Drawn
/// between two ticks so a falling one moves smoothly. Silver lies flat on the floor as a small stack of coins.
/// Equipment floats and shimmers in the colour of its tier (issue #825). Pure presentation.
/// </summary>
internal sealed class PickupView
{
    private static readonly Color Glass = new(120, 14, 26, 255);
    private static readonly Color Blood = new(230, 40, 50, 255);
    private static readonly Color Neck = new(180, 170, 175, 255);
    private static readonly Color Cork = new(110, 74, 46, 255);
    private static readonly Color Brass = new(176, 140, 72, 255);
    private static readonly Color SilverTip = new(200, 206, 220, 255);
    private static readonly Color HolyGlow = new(245, 250, 255, 255);
    private static readonly Color Coin = new(150, 156, 170, 255);
    private static readonly Color CoinGlint = new(240, 244, 255, 255);
    private static readonly Color KeyGold = new(196, 150, 60, 255);
    private static readonly Color KeyGlow = new(255, 214, 120, 255);

    private float _time;

    public void Update(float frameSeconds) => _time += frameSeconds;

    /// <summary>Draws all pickups. <paramref name="alpha"/> blends between the last two ticks.</summary>
    public void Draw(IReadOnlyList<Pickup> pickups, float alpha)
    {
        foreach (var pickup in pickups)
        {
            var bottom = Vector3.Lerp(pickup.PreviousPosition, pickup.Position, alpha);
            var phase = _time * 2.5f + pickup.Id;
            var hover = 0.12f + MathF.Sin(phase) * 0.04f;
            var place =
                Matrix4x4.CreateRotationY(_time * 1.8f + pickup.Id) *
                Matrix4x4.CreateTranslation(bottom + new Vector3(0f, hover, 0f));

            var pulse = 0.5f + 0.5f * MathF.Sin(phase * 2f);
            switch (pickup.Kind)
            {
                case ConsumableKind.BloodVial:
                    DrawVial(place, pulse);
                    break;
                case ConsumableKind.ConsecratedAmmo:
                    DrawCartridges(place, pulse);
                    break;
            }
        }
    }

    /// <summary>
    /// Loot lying in the level. Silver: a small stack of coins on the floor (one coin per silver, at most five),
    /// turning slowly, its top coin glinting now and then so it catches the eye in the dark. A key: golden, floating
    /// and turning like a consumable.
    /// </summary>
    public void DrawLoot(IReadOnlyList<Loot> loot, float alpha)
    {
        foreach (var piece in loot)
        {
            var bottom = Vector3.Lerp(piece.PreviousPosition, piece.Position, alpha);
            if (piece.Kind == LootKind.Key)
            {
                DrawKey(bottom, piece.Id);
                continue;
            }

            var place = Matrix4x4.CreateRotationY(_time * 0.8f + piece.Id) * Matrix4x4.CreateTranslation(bottom);
            var glint = MathF.Max(0f, MathF.Sin(_time * 3f + piece.Id * 1.7f));
            var coins = Math.Min(piece.Amount, 5);
            for (var coin = 0; coin < coins; coin++)
            {
                // Each coin a little off the one below, like a careless stack.
                var shift = new Vector3(MathF.Sin(coin * 2.3f) * 0.02f, 0.015f + coin * 0.025f, MathF.Cos(coin * 2.3f) * 0.02f);
                var colour = coin == coins - 1 ? Blend(Coin, CoinGlint, glint * glint) : Coin;
                ShadedBox.Draw(Matrix4x4.CreateTranslation(shift) * place, new Vector3(0.11f, 0.02f, 0.11f), colour);
            }
        }
    }

    /// <summary>
    /// Equipment lying in the level (issues #317, #825): it floats freely, turning slowly, and shimmers in the colour of
    /// its tier so it can be found - the piece lights up in steps, a glow of dots pulses on the floor below it and small
    /// sparks rise round it. No plate under it. The throwing knives are three knives, tips together at the bottom; any
    /// other weapon is a bar - long and thin for a melee weapon, short and thick for a ranged one - and an item a small
    /// box, until they have models of their own. How it pulses is <see cref="LootShimmer"/>, driven by the world's
    /// <paramref name="ticks"/> (a photo of the same tick looks the same); here it is only drawn. Call between
    /// BeginFigures and EndFigures.
    /// </summary>
    /// <param name="knife">The model of one throwing knife; without it the throwing knives are a bar like the others.</param>
    public void DrawEquipment(
        IReadOnlyList<EquipmentDrop> drops, float alpha, Level level, long ticks, LevelShader shader, TexturedModel? knife)
    {
        if (drops.Count == 0)
        {
            return;
        }

        var seconds = (ticks + alpha) * (double)SimulationClock.SecondsPerTick;
        DrawFloorGlows(drops, level, seconds, shader);
        foreach (var drop in drops)
        {
            var bottom = Vector3.Lerp(drop.PreviousPosition, drop.Position, alpha);
            var flash = LootShimmer.FlashLevel(seconds, drop.Id);
            var turn = Matrix4x4.CreateRotationY(LootShimmer.TurnOf(seconds, drop.Id));
            var bob = new Vector3(0f, LootShimmer.BobOf(seconds, drop.Id), 0f);
            var tier = TierColours.Of(drop.Piece.Tier, (float)seconds);
            shader.SetGlow(flash);
            if (knife is not null && drop is WeaponDrop { Kind: WeaponKind.ThrowingKnives })
            {
                DrawKnives(knife, turn * Matrix4x4.CreateTranslation(bottom + bob), tier, flash, shader);
                continue;
            }

            var place = turn * Matrix4x4.CreateTranslation(bottom + bob + new Vector3(0f, LootShimmer.BoxHeight, 0f));
            ShadedBox.Draw(place, BoxOf(drop), Blend(Black, tier, LootShimmer.BoxBrightness(flash)));
        }

        // The sparks shine by themselves, all in one go.
        shader.SetGlow(1f);
        foreach (var drop in drops)
        {
            var bottom = Vector3.Lerp(drop.PreviousPosition, drop.Position, alpha);
            var tier = TierColours.Of(drop.Piece.Tier, (float)seconds);
            foreach (var spark in LootShimmer.Sparks(seconds, drop.Id))
            {
                ShadedBox.DrawGlowing(Matrix4x4.CreateTranslation(bottom + spark.Offset), new Vector3(spark.Edge), tier);
            }
        }

        shader.SetGlow(0f);
    }

    private static readonly Color Black = new(0, 0, 0, 255);
    private static readonly Vector3 MeleeBar = new(0.8f, 0.05f, 0.08f);
    private static readonly Vector3 RangedBar = new(0.55f, 0.09f, 0.12f);
    private static readonly Vector3 ItemBox = new(0.16f, 0.16f, 0.16f);

    private readonly Matrix4x4[] _knifeTurn = [Matrix4x4.Identity];
    private readonly Matrix4x4[] _knifeFrame = new Matrix4x4[1];

    // The floor glow of every lying piece, laid onto the floor once and kept while the piece lies where it lay.
    private readonly Dictionary<int, (Vector3 At, IReadOnlyList<GlowDisc> Discs)> _glows = [];
    private Level? _glowLevel;

    private static Vector3 BoxOf(EquipmentDrop drop) => drop switch
    {
        WeaponDrop { Info.Hand: WeaponHand.Melee } => MeleeBar,
        WeaponDrop => RangedBar,
        _ => ItemBox,
    };

    // The three knives of the throwing knives (form a2 of the trial piece #819), their surface blended toward the
    // colour of the tier with the flash.
    private void DrawKnives(TexturedModel knife, Matrix4x4 place, Color tier, float flash, LevelShader shader)
    {
        shader.SetFlash(tier, LootShimmer.FlashAmount(flash));
        for (var index = 0; index < ThrowingKnivesPose.Count; index++)
        {
            knife.Rig.Pose(_knifeTurn, LootShimmer.KnifeInPiece(index) * place, _knifeFrame);
            knife.DrawPart(0, _knifeFrame[0]);
        }

        shader.SetFlash(Color.White, 0f);
    }

    // The glow on the floor below every piece that lies still: whole discs of dots, the wider ones thinner, unlit like
    // the frost disc. Each piece's glow lies at its own small height, so two glows that overlap do not flicker.
    private void DrawFloorGlows(IReadOnlyList<EquipmentDrop> drops, Level level, double seconds, LevelShader shader)
    {
        if (!ReferenceEquals(level, _glowLevel) || _glows.Count > drops.Count * 2)
        {
            _glows.Clear();
            _glowLevel = level;
        }

        shader.SetGlow(1f);
        shader.SetPatternOnly(true);
        foreach (var drop in drops)
        {
            // Still falling: no floor of its own yet.
            if (drop.Position != drop.PreviousPosition)
            {
                continue;
            }

            if (!_glows.TryGetValue(drop.Id, out var glow) || glow.At != drop.Position)
            {
                glow = (drop.Position, LootShimmer.GlowDiscs(level, drop.Position, drop.Position.Y));
                _glows[drop.Id] = glow;
            }

            var colour = TierColours.Of(drop.Piece.Tier, (float)seconds);
            var share = LootShimmer.GlowShare(drop.Piece.Tier, seconds, drop.Id);
            var lift = LootShimmer.GlowLiftOf(drop.Id);
            foreach (var disc in glow.Discs)
            {
                shader.SetPattern(colour, share * disc.Part);
                Triangles(disc.Triangles, colour, lift);
            }
        }

        shader.SetPattern(Black, 0f);
        shader.SetPatternOnly(false);
        shader.SetGlow(0f);
    }

    private static void Triangles(IReadOnlyList<DiscTriangle> triangles, Color colour, float lift)
    {
        Rlgl.Begin(DrawMode.Triangles);
        Rlgl.Color4ub(colour.R, colour.G, colour.B, colour.A);
        foreach (var triangle in triangles)
        {
            Rlgl.Vertex3f(triangle.A.X, triangle.A.Y + lift, triangle.A.Z);
            Rlgl.Vertex3f(triangle.B.X, triangle.B.Y + lift, triangle.B.Z);
            Rlgl.Vertex3f(triangle.C.X, triangle.C.Y + lift, triangle.C.Z);
        }

        Rlgl.End();
    }

    /// <summary>An old key standing upright: a ring bow on top, a shaft, a bit at the bottom - glowing gold.</summary>
    private void DrawKey(Vector3 bottom, int id)
    {
        var hover = 0.18f + MathF.Sin(_time * 2.5f + id) * 0.04f;
        var place = Matrix4x4.CreateRotationY(_time * 1.8f + id) * Matrix4x4.CreateTranslation(bottom + new Vector3(0f, hover, 0f));
        var gold = Blend(KeyGold, KeyGlow, 0.5f + 0.5f * MathF.Sin(_time * 5f + id));
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.32f, 0f) * place, new Vector3(0.12f, 0.025f, 0.03f), gold);  // bow top
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.22f, 0f) * place, new Vector3(0.12f, 0.025f, 0.03f), gold);  // bow bottom
        ShadedBox.Draw(Matrix4x4.CreateTranslation(-0.05f, 0.27f, 0f) * place, new Vector3(0.025f, 0.1f, 0.03f), gold); // bow left
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0.05f, 0.27f, 0f) * place, new Vector3(0.025f, 0.1f, 0.03f), gold);  // bow right
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.12f, 0f) * place, new Vector3(0.03f, 0.18f, 0.03f), gold);    // shaft
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0.04f, 0.05f, 0f) * place, new Vector3(0.06f, 0.04f, 0.03f), gold);  // bit
    }

    /// <summary>A red glass vial: the blood inside pulses, dark glass around a glowing core.</summary>
    private static void DrawVial(Matrix4x4 place, float pulse)
    {
        var core = Blend(Glass, Blood, 0.4f + 0.6f * pulse);
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.08f, 0f) * place, new Vector3(0.14f, 0.16f, 0.14f), Glass);   // body
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.07f, 0f) * place, new Vector3(0.1f, 0.12f, 0.15f), core);     // blood
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.19f, 0f) * place, new Vector3(0.06f, 0.06f, 0.06f), Neck);    // neck
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.235f, 0f) * place, new Vector3(0.075f, 0.035f, 0.075f), Cork); // cork
    }

    /// <summary>Three upright cartridges side by side: brass cases, silver tips glowing pale and holy.</summary>
    private static void DrawCartridges(Matrix4x4 place, float pulse)
    {
        var tip = Blend(SilverTip, HolyGlow, 0.3f + 0.7f * pulse);
        foreach (var x in (ReadOnlySpan<float>)[-0.06f, 0f, 0.06f])
        {
            ShadedBox.Draw(Matrix4x4.CreateTranslation(x, 0.06f, 0f) * place, new Vector3(0.04f, 0.12f, 0.04f), Brass);  // case
            ShadedBox.Draw(Matrix4x4.CreateTranslation(x, 0.145f, 0f) * place, new Vector3(0.032f, 0.05f, 0.032f), tip); // tip
        }
    }

    private static Color Blend(Color from, Color to, float amount) => new(
        (byte)(from.R + (to.R - from.R) * amount),
        (byte)(from.G + (to.G - from.G) * amount),
        (byte)(from.B + (to.B - from.B) * amount),
        (byte)255);
}
