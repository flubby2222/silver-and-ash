using System.Numerics;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// A part of an enemy's body that is body from every side (issue #785): a rod from <paramref name="From"/> to
/// <paramref name="To"/> with <paramref name="Radius"/>, in the world. The rime flecks of Grave Chill only sit where the
/// eye sees one of these (<see cref="FrostLook.FleckAt"/>) - so none stands beside the figure, from wherever it is seen.
/// The rods lie inside the model, smaller than it: what a shot hits (<see cref="ITarget.ShotBodies"/>) is wider than
/// the figure and does not do.
/// </summary>
public readonly record struct FrostCore(Vector3 From, Vector3 To, float Radius)
{
    // A rod in the enemy's own space, as the model file has it: forward along its facing and up from its feet.
    private readonly record struct Rod(Vector2 Low, Vector2 High, float Radius);

    // The ghoul (ghoul.glb), crawling: its hunched torso rises from the hip behind its feet to the shoulders, the head
    // hangs in front. Both turn with the torso around its pivot when it rears up for a blow.
    private static readonly Rod[] GhoulRods =
    [
        new(new(-0.22f, 0.44f), new(0.04f, 0.66f), 0.08f),
        new(new(0.32f, 0.80f), new(0.32f, 0.87f), 0.04f),
    ];

    private static readonly Vector2 GhoulTorsoPivot = new(-0.23f, 0.46f);

    // The bat (bat.glb): body and head round the middle of the model, which hangs at half its height above its feet and
    // bobs 4 cm with the wing beat; it always turns to the eye, so the rod stands on its axis.
    private static readonly Rod[] BatRods = [new(new(0f, 0.13f), new(0f, 0.29f), 0.05f)];

    // Gorm (gorm.glb): the barrel of his torso between belly and chest, narrow enough to stay inside when he bends.
    private static readonly Rod[] GormRods = [new(new(0.12f, 1.4f), new(0.12f, 2.2f), 0.3f)];

    /// <summary>
    /// The rod of an enemy without a table of its own: on the axis of its body from a third to two thirds of
    /// <paramref name="height"/> above <paramref name="feet"/>, a quarter of <paramref name="radius"/> thick.
    /// </summary>
    public static FrostCore Middle(Vector3 feet, float height, float radius) =>
        new(feet + new Vector3(0f, height / 3f, 0f), feet + new Vector3(0f, height * 2f / 3f, 0f), radius / 4f);

    /// <summary>The rods of <paramref name="enemy"/> where it is now - one table per kind of enemy.</summary>
    internal static IReadOnlyList<FrostCore> Of(IEnemy enemy)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        return enemy switch
        {
            Ghoul ghoul => Place(GhoulRods, enemy.ShownPosition, ghoul.Facing, GhoulTurn(ghoul)),
            Bat => Place(BatRods, enemy.ShownPosition, Vector3.UnitZ, Matrix4x4.Identity),
            Gorm gorm => Place(GormRods, enemy.ShownPosition, gorm.Facing, Matrix4x4.Identity),
            _ => [Middle(enemy.ShownPosition, enemy.Body.Height, enemy.Body.Radius)],
        };
    }

    // The ghoul's torso reared up for a blow (GhoulPose: the turn of the torso around its pivot, in its own space).
    private static Matrix4x4 GhoulTurn(Ghoul ghoul)
    {
        Span<float> degrees = stackalloc float[GhoulPose.Parts.Count];
        GhoulPose.Angles(new GhoulMotion(ghoul.WindUpProgress, 0f, 0f, 0f), degrees);
        var pivot = new Vector3(0f, GhoulTorsoPivot.Y, GhoulTorsoPivot.X);
        return Matrix4x4.CreateTranslation(-pivot) * Rig.Lift(degrees[0]) * Matrix4x4.CreateTranslation(pivot);
    }

    private static FrostCore[] Place(Rod[] rods, Vector3 feet, Vector3 facing, Matrix4x4 turn)
    {
        var forward = (facing with { Y = 0f }).LengthSquared() > 1e-6f ? Vector3.Normalize(facing with { Y = 0f }) : Vector3.UnitZ;
        Vector3 World(Vector2 own)
        {
            var turned = Vector3.Transform(new Vector3(0f, own.Y, own.X), turn);
            return feet + (forward * turned.Z) + new Vector3(0f, turned.Y, 0f);
        }

        return rods.Select(rod => new FrostCore(World(rod.Low), World(rod.High), rod.Radius)).ToArray();
    }
}
