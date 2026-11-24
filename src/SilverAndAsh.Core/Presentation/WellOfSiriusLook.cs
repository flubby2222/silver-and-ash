using System.Numerics;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// Sirius' Relief (issue #438): two models from tools/models/well_of_sirius.py on one texture,
/// assets/models/well_of_sirius_bereit.glb (ready, with water) and well_of_sirius_verbraucht.glb (spent, dry). Neither
/// part moves in its file; the well stands in the file space of <see cref="WellOfSirius"/> (origin on the floor in its
/// middle, never turned).
/// <para>
/// The water flows by changing its texture coordinates: every <see cref="FrameSeconds"/> the water surfaces
/// (<see cref="WaterBasin"/>, <see cref="WaterBowl1"/>, <see cref="WaterBowl2"/>) go one frame on in the atlas (water0..3,
/// 16 px apart in a 64 px wide texture: a quarter of its width), the falls (<see cref="Falls"/>, fall0..3) one frame of
/// 8 px (an eighth). The model holds frame 0.
/// </para>
/// </summary>
public static class WellOfSiriusLook
{
    public const string Body = "body";
    public const string Column = "column";
    public const string WaterBasin = "water_basin";
    public const string WaterBowl1 = "water_bowl1";
    public const string WaterBowl2 = "water_bowl2";
    public const string Falls = "falls";

    /// <summary>Seconds each frame of the water shows.</summary>
    public const float FrameSeconds = 0.15f;

    /// <summary>Frames of the water before it starts again.</summary>
    public const int Frames = 4;

    /// <summary>Texture width one water frame moves on (16 of 64 px).</summary>
    public const float WaterStep = 0.25f;

    /// <summary>Texture width one fall frame moves on (8 of 64 px).</summary>
    public const float FallStep = 0.125f;

    private static readonly RigPart[] Ready =
    [
        new(Body, null), new(Column, null), new(WaterBasin, null), new(WaterBowl1, null), new(WaterBowl2, null), new(Falls, null),
    ];

    private static readonly RigPart[] Spent = [new(Body, null), new(Column, null)];

    /// <summary>The parts of the model for a ready (<paramref name="spent"/> false) or spent well, in the order of the rig.</summary>
    public static IReadOnlyList<RigPart> Parts(bool spent) => spent ? Spent : Ready;

    /// <summary>The file of the ready or spent well.</summary>
    public static string FileName(bool spent) => spent ? "well_of_sirius_verbraucht.glb" : "well_of_sirius_bereit.glb";

    /// <summary>The frame of the water (0..3) at <paramref name="seconds"/>, counted from any start; before it, frame 0.</summary>
    /// A tiny allowance keeps rounding from holding a frame one tick too long (27 ticks of 1/60 s are 0.45 s, as a float a
    /// hair less).
    public static int Frame(float seconds) =>
        seconds <= 0f || !float.IsFinite(seconds) ? 0 : (int)MathF.Floor((seconds / FrameSeconds) + 1e-4f) % Frames;

    /// <summary>True for the parts whose texture flows: the three water surfaces and the falls.</summary>
    public static bool Flows(string part) => part is WaterBasin or WaterBowl1 or WaterBowl2 or Falls;

    /// <summary>
    /// How far the texture coordinates of <paramref name="part"/> move in <paramref name="frame"/>: along the width only,
    /// a water step for the surfaces, a fall step for the falls, nothing for stone.
    /// </summary>
    public static Vector2 UvOffset(string part, int frame)
    {
        if (frame is < 0 or >= Frames)
        {
            throw new ArgumentOutOfRangeException(nameof(frame), frame, null);
        }

        if (!Flows(part))
        {
            return Vector2.Zero;
        }

        return new Vector2(frame * (part == Falls ? FallStep : WaterStep), 0f);
    }
}
