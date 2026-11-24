namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The one rule for a model that comes in one file per wall variant and is drawn in the variant of the walls around it
/// (issue #587): the ceiling arches, wall pillars, wall niches, door frames, the guide rings of the portcullises and the
/// spiral staircase. Each of them used to have its own copy of <see cref="Pick"/>.
/// </summary>
public static class WallVariantChoice
{
    /// <summary>
    /// The variant whose file to draw: <paramref name="wanted"/> (the variant the level really draws on the walls there)
    /// if its file was loaded, else variant A if that one was, else none - then the piece is left out or a placeholder
    /// stands in, as the piece decides.
    /// </summary>
    public static TextureVariant? Pick(TextureVariant wanted, bool wantedLoaded, bool variantALoaded) =>
        wantedLoaded ? wanted : variantALoaded ? TextureVariant.A : null;
}
