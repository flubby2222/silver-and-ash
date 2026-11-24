using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// The one choice of the file of a piece drawn in the variant of the walls around it (issue #587). Only the cases the
/// game really asks: both flags come from the same set of loaded files, so for <see cref="TextureVariant.A"/> they are
/// always equal.
/// </summary>
public class WallVariantChoiceTests
{
    [Theory]
    [InlineData(TextureVariant.A, true)]
    [InlineData(TextureVariant.B, true)]
    [InlineData(TextureVariant.B, false)]
    [InlineData(TextureVariant.C, true)]
    [InlineData(TextureVariant.C, false)]
    public void A_loaded_variant_is_drawn_as_it_is(TextureVariant wanted, bool variantALoaded)
    {
        Assert.Equal(wanted, WallVariantChoice.Pick(wanted, wantedLoaded: true, variantALoaded));
    }

    [Theory]
    [InlineData(TextureVariant.B)]
    [InlineData(TextureVariant.C)]
    public void A_variant_without_its_file_falls_back_to_variant_a(TextureVariant wanted)
    {
        Assert.Equal(TextureVariant.A, WallVariantChoice.Pick(wanted, wantedLoaded: false, variantALoaded: true));
    }

    [Theory]
    [InlineData(TextureVariant.A)]
    [InlineData(TextureVariant.B)]
    [InlineData(TextureVariant.C)]
    public void Without_its_file_and_without_variant_a_nothing_is_drawn(TextureVariant wanted)
    {
        Assert.Null(WallVariantChoice.Pick(wanted, wantedLoaded: false, variantALoaded: false));
    }
}
