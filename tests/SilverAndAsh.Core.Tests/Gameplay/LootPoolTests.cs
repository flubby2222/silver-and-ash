using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class LootPoolTests
{
    private static readonly LootBalance Values = Balance.Default.Loot;

    /// <summary>Never a higher tier (to test which piece a tier gives).</summary>
    private static readonly LootBalance NeverHigher = Values with { HigherTierChance = 0f };

    private static readonly Equipment Club = Equipment.Of(WeaponKind.Club);
    private static readonly Equipment Rifle = Equipment.Of(WeaponKind.Rifle);
    private static readonly Equipment AshenHeart = Equipment.Of(ItemKind.AshenHeart);

    [Fact]
    public void Every_weapon_and_every_item_is_equipment_once_with_its_own_tier()
    {
        Assert.Equal(Enum.GetValues<WeaponKind>().Length + Enum.GetValues<ItemKind>().Length, Equipment.All.Count);
        Assert.Equal(Equipment.All.Count, Equipment.All.Distinct().Count());
        foreach (var kind in Enum.GetValues<WeaponKind>())
        {
            Assert.Equal(Weapons.Info(kind).Tier, Equipment.Of(kind).Tier);
            Assert.True(Equipment.Of(kind).IsWeapon);
        }

        foreach (var kind in Enum.GetValues<ItemKind>())
        {
            Assert.Equal(Items.Info(kind).Tier, Equipment.Of(kind).Tier);
            Assert.False(Equipment.Of(kind).IsWeapon);
        }
    }

    [Fact]
    public void Every_tier_holds_at_least_one_item()
    {
        foreach (var tier in Enum.GetValues<ItemTier>())
        {
            Assert.Contains(Equipment.All, piece => piece.Tier == tier && !piece.IsWeapon);
        }
    }

    [Fact]
    public void A_new_pool_holds_everything()
    {
        var pool = new LootPool(1, Values);

        Assert.Equal(Equipment.All, pool.Left);
    }

    [Fact]
    public void A_draw_is_never_below_the_least_tier()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            foreach (var tier in Enum.GetValues<ItemTier>())
            {
                var drawn = new LootPool(seed, Values).Draw(tier);

                Assert.NotNull(drawn);
                Assert.True(drawn.Tier >= tier, $"Seed {seed}: {drawn} ({drawn.Tier}) from a draw of at least {tier}.");
            }
        }
    }

    [Fact]
    public void Drawing_until_nothing_is_left_gives_every_piece_exactly_once_and_then_nothing()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var pool = new LootPool(seed, Values);
            var drawn = new List<Equipment>();
            while (pool.Draw(ItemTier.Worn) is { } piece)
            {
                drawn.Add(piece);
                Assert.False(pool.Contains(piece));
                Assert.True(drawn.Count <= Equipment.All.Count, $"Seed {seed}: more draws than pieces - a piece came twice.");
            }

            Assert.Equal(Equipment.All.Count, drawn.Count);
            Assert.Equal(Equipment.All.ToHashSet(), drawn.ToHashSet());
            Assert.Empty(pool.Left);
            Assert.Null(pool.Draw(ItemTier.Worn));
        }
    }

    [Fact]
    public void A_used_up_kind_falls_back_to_the_other_kind_of_the_same_tier()
    {
        // Every item and the two start weapons are gone: whatever the roll wanted, a Worn draw can only give the Club.
        Equipment[] startWeapons = [Equipment.Of(WeaponKind.SilverBlade), Equipment.Of(WeaponKind.Pistol)];
        for (var seed = 0; seed < 50; seed++)
        {
            var pool = new LootPool(seed, NeverHigher, Enum.GetValues<ItemKind>().Select(Equipment.Of).Concat(startWeapons));

            Assert.Equal(Club, pool.Draw(ItemTier.Worn));
        }
    }

    [Fact]
    public void A_used_up_tier_falls_back_to_the_next_higher_one_never_to_a_lower_one()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var allButWornAndRifle = Equipment.All.Where(piece => piece.Tier != ItemTier.Worn && piece != Rifle);
            var forgedEmpty = new LootPool(seed, NeverHigher, allButWornAndRifle);
            // Only Worn pieces and the Rifle (Forged) are left: a Forged draw must give the Rifle, not a Worn piece.
            Assert.Equal(Rifle, forgedEmpty.Draw(ItemTier.Forged));

            var onlyWorn = new LootPool(seed, NeverHigher, Equipment.All.Where(piece => piece.Tier != ItemTier.Worn));
            // Nothing from Forged up is left: there is nothing - the caller gives extra silver.
            Assert.Null(onlyWorn.Draw(ItemTier.Forged));
            Assert.Equal(Equipment.All.Count(piece => piece.Tier == ItemTier.Worn), onlyWorn.Left.Count);

            var onlyVampiric = new LootPool(seed, NeverHigher, Equipment.All.Where(piece => piece.Tier != ItemTier.Vampiric));
            // The Worn tier is used up: a Worn draw rises until it finds something.
            Assert.Equal(ItemTier.Vampiric, onlyVampiric.Draw(ItemTier.Worn)!.Tier);
        }
    }

    [Fact]
    public void A_higher_tier_roll_into_a_used_up_tier_falls_back_to_the_chests_own_tier()
    {
        // Review of #319: only the two Royal items are left; a King's Chest (Royal) that rolls "one tier higher" finds
        // Vampiric empty - it must still give a Royal piece, not nothing (extra silver).
        var always = Values with { HigherTierChance = 1f };
        for (var seed = 0; seed < 50; seed++)
        {
            var onlyRoyal = new LootPool(seed, always, Equipment.All.Where(piece => piece.Tier != ItemTier.Royal));

            Assert.Equal(ItemTier.Royal, onlyRoyal.Draw(ItemTier.Royal)?.Tier);
            Assert.Equal(ItemTier.Royal, onlyRoyal.Draw(ItemTier.Royal)?.Tier);
            Assert.Null(onlyRoyal.Draw(ItemTier.Royal));
        }
    }

    [Fact]
    public void A_higher_tier_roll_into_a_used_up_tier_never_skips_the_chests_own_tier()
    {
        // Royal is used up, Forged is full: a Forged chest that rolls "one tier higher" gives Forged, not Vampiric.
        var always = Values with { HigherTierChance = 1f };
        for (var seed = 0; seed < 50; seed++)
        {
            var royalGone = new LootPool(seed, always, Equipment.All.Where(piece => piece.Tier == ItemTier.Royal));

            Assert.Equal(ItemTier.Forged, royalGone.Draw(ItemTier.Forged)!.Tier);
        }
    }

    [Fact]
    public void Only_if_the_chests_own_tier_is_used_up_too_the_draw_rises_further()
    {
        // Forged and Royal are used up: a Forged chest rolling higher (Royal, empty) goes back to Forged (empty), then
        // up to Vampiric - the only tier with anything left.
        var always = Values with { HigherTierChance = 1f };
        for (var seed = 0; seed < 50; seed++)
        {
            var pool = new LootPool(seed, always, Equipment.All.Where(piece => piece.Tier is ItemTier.Forged or ItemTier.Royal));

            Assert.Equal(ItemTier.Vampiric, pool.Draw(ItemTier.Forged)!.Tier);
        }
    }

    [Fact]
    public void A_vampiric_draw_never_rises_and_stays_vampiric()
    {
        var always = Values with { HigherTierChance = 1f };
        for (var seed = 0; seed < 50; seed++)
        {
            Assert.Equal(ItemTier.Vampiric, new LootPool(seed, always).Draw(ItemTier.Vampiric)!.Tier);
            Assert.Equal(ItemTier.Forged, new LootPool(seed, always).Draw(ItemTier.Worn)!.Tier);
        }
    }

    [Fact]
    public void Over_many_draws_items_and_higher_tiers_come_as_often_as_the_balance_says()
    {
        // One Worn draw from each of 4000 fresh pools (fixed seeds 0..3999, so the result is always the same). In a
        // fresh pool the Worn and Forged tiers hold both kinds, so the shares show directly. Tolerances are five
        // standard deviations of the binomial count: item share 0.7 -> sqrt(0.7 * 0.3 / 4000) = 0.0072, times 5 =
        // 0.036; higher tier 0.1 -> sqrt(0.1 * 0.9 / 4000) = 0.0047, times 5 = 0.024. A correct pool misses that
        // with less than one chance in a million; a wrong share (e.g. 0.5 or 0.2) misses it by far.
        const int draws = 4000;
        var items = 0;
        var higher = 0;
        for (var seed = 0; seed < draws; seed++)
        {
            var drawn = new LootPool(seed, Values).Draw(ItemTier.Worn)!;
            items += drawn.IsWeapon ? 0 : 1;
            higher += drawn.Tier > ItemTier.Worn ? 1 : 0;
        }

        Assert.InRange(items / (float)draws, Values.ItemShare - 0.036f, Values.ItemShare + 0.036f);
        Assert.InRange(higher / (float)draws, Values.HigherTierChance - 0.024f, Values.HigherTierChance + 0.024f);
    }

    [Fact]
    public void The_same_seed_gives_the_same_loot_in_the_same_order()
    {
        Assert.Equal(DrawAll(7), DrawAll(7));
        Assert.NotEqual(DrawAll(7), DrawAll(8));
    }

    [Fact]
    public void Taking_removes_without_chance_and_only_once()
    {
        var pool = new LootPool(1, Values);

        Assert.True(pool.Take(Rifle));
        Assert.False(pool.Contains(Rifle));
        Assert.False(pool.Take(Rifle));
        Assert.Equal(Equipment.All.Count - 1, pool.Left.Count);
    }

    [Fact]
    public void A_broken_ashen_heart_put_back_can_be_found_again()
    {
        var pool = new LootPool(1, Values, [AshenHeart]);
        Assert.False(pool.Contains(AshenHeart));

        Assert.True(pool.PutBack(AshenHeart));

        Assert.True(pool.Contains(AshenHeart));
        Assert.Equal(Equipment.All, pool.Left);
        Assert.False(pool.PutBack(AshenHeart));
    }

    [Fact]
    public void A_piece_from_the_middle_goes_back_to_its_own_place()
    {
        var pool = new LootPool(1, Values, [Rifle]);

        Assert.True(pool.PutBack(Rifle));

        Assert.Equal(Equipment.All, pool.Left);
    }

    [Fact]
    public void Putting_back_gives_the_same_draws_as_if_it_had_never_left()
    {
        var untouched = new LootPool(3, Values);
        var putBack = new LootPool(3, Values, [AshenHeart]);
        putBack.PutBack(AshenHeart);

        for (var i = 0; i < Equipment.All.Count; i++)
        {
            Assert.Equal(untouched.Draw(ItemTier.Worn), putBack.Draw(ItemTier.Worn));
        }
    }

    [Fact]
    public void The_world_holds_one_pool_without_the_weapons_a_player_starts_with()
    {
        var world = new GameWorld(LevelParser.Parse("P"), 5);

        Assert.False(world.LootPool.Contains(Equipment.Of(world.Player.Melee)));
        foreach (var weapon in world.Player.RangedSlots.OfType<RangedWeapon>())
        {
            Assert.False(world.LootPool.Contains(Equipment.Of(weapon.Kind)));
        }

        Assert.Contains(Club, world.LootPool.Left);
        Assert.Contains(Rifle, world.LootPool.Left);
        Assert.Same(world.LootPool, world.LootPool);
    }

    [Fact]
    public void Two_worlds_with_the_same_seed_draw_the_same_loot()
    {
        var first = new GameWorld(LevelParser.Parse("P"), 11);
        var second = new GameWorld(LevelParser.Parse("P"), 11);

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(first.LootPool.Draw(ItemTier.Worn), second.LootPool.Draw(ItemTier.Worn));
        }
    }

    [Fact]
    public void Drawing_loot_never_shifts_what_kills_drop()
    {
        var piles = 0;
        for (var seed = 0; seed < 20; seed++)
        {
            var without = SilverAfterKillingEverything(seed, drawFirst: false);
            Assert.Equal(without, SilverAfterKillingEverything(seed, drawFirst: true));
            piles += without.Count;
        }

        Assert.True(piles > 0, "No kill dropped silver, so the test would show nothing.");
    }

    private static List<Equipment> DrawAll(int seed)
    {
        var pool = new LootPool(seed, Values);
        var drawn = new List<Equipment>();
        while (pool.Draw(ItemTier.Worn) is { } piece)
        {
            drawn.Add(piece);
            Assert.True(drawn.Count <= Equipment.All.Count, $"Seed {seed}: more draws than pieces - a piece came twice.");
        }

        return drawn;
    }

    private static List<(System.Numerics.Vector3, int)> SilverAfterKillingEverything(int seed, bool drawFirst)
    {
        var world = new GameWorld(LevelParser.Parse("PGGGG\nBBBBB"), seed);
        if (drawFirst)
        {
            for (var i = 0; i < 5; i++)
            {
                world.LootPool.Draw(ItemTier.Worn);
            }
        }

        foreach (var target in world.Targets.ToList())
        {
            target.TakeDamage(10_000, []);
        }

        world.Tick(PlayerInput.None);
        return world.SilverPiles.Select(pile => (pile.Position, pile.Amount)).ToList();
    }
}
