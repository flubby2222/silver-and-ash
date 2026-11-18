using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>Equipment on the floor, taken with F (decision record 0008, issue #317).</summary>
public class EquipmentPickupTests
{
    private const string Map = "...\n.P.\n...\n...\n...";

    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly PlayerInput Toggle = PlayerInput.None with { ToggleInventory = true };

    /// <summary>A world whose floor holds <paramref name="weapons"/> and <paramref name="items"/> (at the player's feet unless placed).</summary>
    private static GameWorld World(ExhibitWeapon[]? weapons = null, ExhibitItem[]? items = null) =>
        new(LevelParser.Parse(Map), 1)
        {
            Showroom = new ShowroomExhibits(null, [], [], weapons ?? [], items ?? []),
        };

    private static Vector3 Feet => LevelParser.Parse(Map).CellCenter(1, 1);

    /// <summary>The cell two rows ahead of the player (who looks along +Z): in the way when walking forward.</summary>
    private static Vector3 Ahead => LevelParser.Parse(Map).CellCenter(1, 3);

    private static void LookAtFeet(Player player) =>
        player.Look(PlayerInput.None with { Look = new Vector2(0f, 85f * MathF.PI / 180f / ControlSettings.MouseSensitivity) });

    private static void Run(GameWorld world, PlayerInput input, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++)
        {
            world.Tick(input);
        }
    }

    private static void FillBag(Player player)
    {
        while (player.HasBagRoom)
        {
            Assert.True(player.Stow(WeaponKind.Club));
        }
    }

    [Fact]
    public void A_weapon_on_the_floor_is_offered_with_its_name_and_tier()
    {
        var world = World(weapons: [new ExhibitWeapon(WeaponKind.Rifle, Feet)]);
        LookAtFeet(world.Player);

        var target = world.InteractionTarget(world.Player);

        Assert.Same(world.EquipmentDrops[0], target);
        Assert.Equal(new InteractionOffer("Take Rifle (Forged)", true, ItemTier.Forged), target!.OfferFor(world.Player));
    }

    [Fact]
    public void F_takes_a_weapon_into_the_bag_and_tells_who_took_what()
    {
        var world = World(weapons: [new ExhibitWeapon(WeaponKind.Rifle, Feet)]);
        LookAtFeet(world.Player);
        var drop = world.EquipmentDrops[0];

        world.Tick(PressF);

        Assert.Empty(world.EquipmentDrops);
        Assert.Equal(WeaponKind.Rifle, world.Player.Bag[0]);
        var magazine = world.Player.BagRanged(0)!.Magazine;
        Assert.Equal(magazine.Size, magazine.Rounds);
        var taken = Assert.Single(world.Events.OfType<EquipmentTaken>());
        Assert.Equal(new EquipmentTaken(drop.Id, world.Player.Id, Equipment.Of(WeaponKind.Rifle), drop.Position), taken);
        Assert.Equal(ItemTier.Forged, taken.Tier);
    }

    [Fact]
    public void F_takes_an_item_and_it_needs_no_place_in_the_bag()
    {
        var world = World(items: [new ExhibitItem(ItemKind.Leech, Feet)]);
        FillBag(world.Player);
        LookAtFeet(world.Player);
        Assert.Equal(new InteractionOffer("Take Leech (Vampiric)", true, ItemTier.Vampiric), world.EquipmentDrops[0].OfferFor(world.Player));

        world.Tick(PressF);

        Assert.Empty(world.EquipmentDrops);
        Assert.Equal([ItemKind.Leech], world.Player.Items);
        var taken = Assert.Single(world.Events.OfType<EquipmentTaken>());
        Assert.Equal(Equipment.Of(ItemKind.Leech), taken.Piece);
        Assert.Equal(ItemTier.Vampiric, taken.Tier);
        Assert.Equal(world.Player.Id, taken.PlayerId);
    }

    [Fact]
    public void With_a_full_bag_a_weapon_stays_lying_and_the_prompt_says_why()
    {
        var world = World(weapons: [new ExhibitWeapon(WeaponKind.Rifle, Feet)]);
        FillBag(world.Player);
        LookAtFeet(world.Player);
        var bag = world.Player.Bag;

        var target = world.InteractionTarget(world.Player);
        world.Tick(PressF);

        Assert.Equal(new InteractionOffer("Inventory full", false, ItemTier.Forged), target!.OfferFor(world.Player));
        Assert.Single(world.EquipmentDrops);
        Assert.Equal(bag, world.Player.Bag);
        Assert.DoesNotContain(world.Events, gameEvent => gameEvent is EquipmentTaken);
    }

    [Fact]
    public void Walking_over_equipment_takes_nothing()
    {
        var world = World([new ExhibitWeapon(WeaponKind.Rifle, Ahead)], [new ExhibitItem(ItemKind.HuntersBoots, Ahead)]);
        var events = new List<GameEvent>();

        for (var tick = 0; tick < 2 * SimulationClock.TicksPerSecond; tick++)
        {
            world.Tick(Forward);
            events.AddRange(world.Events);
        }

        Assert.True(world.Player.Position.Z > Ahead.Z, "The player never walked over the equipment.");
        Assert.Equal(2, world.EquipmentDrops.Count);
        Assert.Empty(world.Player.Items);
        Assert.All(world.Player.Bag, Assert.Null);
        Assert.DoesNotContain(events, gameEvent => gameEvent is EquipmentTaken);
    }

    [Fact]
    public void A_dropped_ranged_weapon_comes_back_with_its_magazine()
    {
        var world = new GameWorld(LevelParser.Parse(Map), 1);
        world.Player.Equip(1, WeaponKind.Rifle);
        var rifle = world.Player.RangedSlots[1]!;
        rifle.Magazine.TryTake();
        world.Tick(Toggle);
        world.Tick(PlayerInput.None with { InventoryClick = new InventoryClick(InventoryPlace.Ranged, 1, Drop: true) });
        world.Tick(Toggle);
        Run(world, PlayerInput.None, SimulationClock.TicksPerSecond);
        LookAtFeet(world.Player);

        world.Tick(PressF);

        Assert.Empty(world.EquipmentDrops);
        Assert.Equal(WeaponKind.Rifle, world.Player.Bag[0]);
        Assert.Same(rifle, world.Player.BagRanged(0));
        Assert.Equal(rifle.Magazine.Size - 1, rifle.Magazine.Rounds);
    }

    [Fact]
    public void A_piece_is_taken_only_once()
    {
        // Online co-op: whoever presses F first gets it - it leaves the floor at once, so nobody can take it again.
        var world = World(items: [new ExhibitItem(ItemKind.IronFlask, Feet)]);
        LookAtFeet(world.Player);
        var events = new List<GameEvent>();

        world.Tick(PressF);
        events.AddRange(world.Events);
        world.Tick(PressF);
        events.AddRange(world.Events);

        Assert.Single(events.OfType<EquipmentTaken>());
        Assert.Equal([ItemKind.IronFlask], world.Player.Items);
        Assert.Null(world.InteractionTarget(world.Player));
    }

    [Fact]
    public void Equipment_falls_to_the_floor_and_stays_offered()
    {
        var above = Feet + new Vector3(0f, 1f, 0f);
        var world = World(items: [new ExhibitItem(ItemKind.SilverRosary, above)]);

        Run(world, PlayerInput.None, SimulationClock.TicksPerSecond);

        Assert.Equal(Feet.Y, world.EquipmentDrops[0].Position.Y, 3);
        Assert.True(world.EquipmentDrops[0].CanInteract);
    }
}
