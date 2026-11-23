using System.Numerics;

namespace SilverAndAsh.Core.Presentation;

/// <summary>What stands in the pocket of a deep wall niche (issue #553): nothing, or one of the fillings the human took.</summary>
public enum WallNicheFilling
{
    /// <summary>An empty pocket.</summary>
    None,

    /// <summary>Seven green bottles lying in a stack of 3-2-2, necks with corks to the front, pushed into the left corner.</summary>
    Bottles,

    /// <summary>Five burnt-down candles with lumps of wax, not lit (the light comes later).</summary>
    Candles,

    /// <summary>Three skulls, one on top of the other two.</summary>
    Skulls,
}

/// <summary>
/// The fillings of a deep wall niche (issue #553, <see cref="WallNicheLook"/>). Pure decoration - no body, no collision.
/// One model per filling, assets/models/wall_niche_{bottles,candles,skulls}.glb, built by tools/models/wall_niche_filling.py.
/// <para>
/// File space: origin in the middle of the niche on what the filling stands on, the back of the pocket at z = 0, the
/// filling built toward +z. <see cref="Place"/> stands it on the sill or the shelf and pushes it the depth of the pocket
/// into the wall, so its back touches the back of the pocket.
/// </para>
/// </summary>
public static class WallNicheFillingLook
{
    /// <summary>The top of the sill - the floor of the pocket - above the niche's floor, in metres.</summary>
    public const float SillTop = 0.5f;

    /// <summary>The underside of the stone shelf above the niche's floor, in metres.</summary>
    public const float ShelfBottom = 0.875f;

    /// <summary>The top of the stone shelf above the niche's floor, in metres.</summary>
    public const float ShelfTop = 1f;

    /// <summary>How far the frame of a deep niche stands out from the wall, in metres: no filling may reach further.</summary>
    public const float FrameFront = 0.0625f;

    private static readonly WallNicheForm[] DeepForms = [WallNicheForm.Deep, WallNicheForm.DeepWithShelf];
    private static readonly WallNicheForm[] ShelfForms = [WallNicheForm.DeepWithShelf];

    // Filling -> its part and file (null: nothing to draw), the height it stands on and the forms that take it. Bottles lie
    // on the sill - under the shelf, if there is one; candles and skulls stand on the shelf. A new filling is one more line here.
    private static readonly (WallNicheFilling Filling, string? Name, float Stand, WallNicheForm[] Forms)[] Fillings =
    [
        (WallNicheFilling.None, null, 0f, [WallNicheForm.Flat, .. DeepForms]),
        (WallNicheFilling.Bottles, "bottles", SillTop, DeepForms),
        (WallNicheFilling.Candles, "candles", ShelfTop, ShelfForms),
        (WallNicheFilling.Skulls, "skulls", ShelfTop, ShelfForms),
    ];

    /// <summary>The file of <paramref name="filling"/>, e.g. wall_niche_bottles.glb, or null for an empty pocket.</summary>
    public static string? FileName(WallNicheFilling filling) =>
        FillingOf(filling).Name is { } name ? $"wall_niche_{name}.glb" : null;

    /// <summary>The parts of <paramref name="filling"/> in the order of its rig: one fixed part, none for an empty pocket.</summary>
    public static IReadOnlyList<RigPart> PartsOf(WallNicheFilling filling) =>
        FillingOf(filling).Name is { } name ? [new RigPart(name, null)] : [];

    /// <summary>How high above the niche's floor <paramref name="filling"/> stands: on the sill or on the shelf.</summary>
    public static float StandHeight(WallNicheFilling filling) => FillingOf(filling).Stand;

    /// <summary>
    /// True if a niche of <paramref name="form"/> takes <paramref name="filling"/>: a flat niche has no pocket, and only
    /// one with a shelf has something for candles and skulls to stand on.
    /// </summary>
    public static bool Fits(WallNicheForm form, WallNicheFilling filling) => FillingOf(filling).Forms.Contains(form);

    /// <summary>
    /// Where <paramref name="filling"/> stands in a niche of <paramref name="form"/> at <paramref name="nichePosition"/>
    /// (on the floor at the wall's face) looking along <paramref name="facing"/>: raised onto the sill or the shelf and
    /// pushed <see cref="WallNicheLook.PocketDepth"/> into the wall, looking the same way as the niche.
    /// </summary>
    /// <exception cref="ArgumentException">If the niche does not take the filling (<see cref="Fits"/>).</exception>
    public static (Vector3 Position, Vector3 Facing) Place(
        WallNicheForm form, WallNicheFilling filling, Vector3 nichePosition, Vector3 facing)
    {
        if (!Fits(form, filling))
        {
            throw new ArgumentException($"A niche of form {form} takes no {filling}.", nameof(filling));
        }

        var position = nichePosition + new Vector3(0f, StandHeight(filling), 0f) - (facing * WallNicheLook.PocketDepth);
        return (position, facing);
    }

    private static (WallNicheFilling Filling, string? Name, float Stand, WallNicheForm[] Forms) FillingOf(WallNicheFilling filling)
    {
        foreach (var entry in Fillings)
        {
            if (entry.Filling == filling)
            {
                return entry;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(filling), filling, null);
    }
}
