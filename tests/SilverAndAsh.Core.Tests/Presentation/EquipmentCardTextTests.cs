using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// The texts of the equipment card (issue #791): an item whose effect is in the game says what it does and nothing
/// else - no "No effect yet" under the effect line.
/// </summary>
public class EquipmentCardTextTests
{
    public static TheoryData<ItemKind> EveryItem() => [.. Enum.GetValues<ItemKind>()];

    [Theory]
    [MemberData(nameof(EveryItem))]
    public void Every_item_of_the_game_has_its_effect(ItemKind kind)
    {
        Assert.True(Items.HasEffect(kind));
    }

    [Theory]
    [MemberData(nameof(EveryItem))]
    public void No_card_text_of_an_item_with_an_effect_says_the_effect_comes_later(ItemKind kind)
    {
        var piece = Equipment.Of(kind);

        Assert.Null(EquipmentCardText.Note(piece));
        Assert.All(CardTexts(piece), text => Assert.DoesNotContain("No effect yet", text));
        Assert.Equal(Items.Info(kind).Effect, EquipmentCardText.Effect(piece));
    }

    [Fact]
    public void A_weapon_card_has_no_note()
    {
        Assert.All(Equipment.All.Where(piece => piece.IsWeapon), piece => Assert.Null(EquipmentCardText.Note(piece)));
    }

    [Fact]
    public void The_tier_line_names_tier_and_kind()
    {
        Assert.Equal("Vampiric item", EquipmentCardText.TierLine(Equipment.Of(ItemKind.Leech)));
        Assert.Equal("Worn weapon", EquipmentCardText.TierLine(Equipment.Of(WeaponKind.Club)));
    }

    private static string[] CardTexts(Equipment piece) =>
        [EquipmentCardText.TierLine(piece), EquipmentCardText.Effect(piece), EquipmentCardText.Note(piece) ?? string.Empty];
}
