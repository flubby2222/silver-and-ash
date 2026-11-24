using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Presentation;

public class StairsLookTests
{
    private const float Tolerance = 0.0001f;

    private static readonly IReadOnlyList<RoomTemplate> Rooms = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    private static readonly Lazy<GeneratedLevel[]> ManyLevels = new(() => Enumerable.Range(0, 150)
        .Select(seed => LevelGenerator.Generate(Rooms, seed, Balance.Default.Layout))
        .ToArray());

    public static TheoryData<TextureVariant> EveryVariant() => new(TextureVariant.A, TextureVariant.B, TextureVariant.C);

    [Theory]
    [InlineData(TextureVariant.A, "stairs_spiral_a.glb")]
    [InlineData(TextureVariant.B, "stairs_spiral_b.glb")]
    [InlineData(TextureVariant.C, "stairs_spiral_c.glb")]
    public void Every_wall_variant_has_its_own_file(TextureVariant variant, string file)
    {
        Assert.Equal(file, StairsLook.FileName(variant));
    }

    [Fact]
    public void An_unknown_variant_has_no_file()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StairsLook.FileName((TextureVariant)7));
    }

    [Fact]
    public void The_parts_are_the_stone_and_the_lamp()
    {
        Assert.Equal([new RigPart("stairs", null), new RigPart("glow", null)], StairsLook.Parts);
    }

    [Theory]
    [MemberData(nameof(EveryVariant))]
    public void Every_file_fills_the_box_of_the_stairs_with_the_lamp_on_top(TextureVariant variant)
    {
        var path = RepositoryFiles.Find($"assets/models/{StairsLook.FileName(variant)}");
        Assert.True(File.Exists(path), $"{path} is missing.");
        var parts = GlbScene.Read(File.ReadAllBytes(path)).ToDictionary(part => part.Name);

        Assert.Equal(StairsLook.Parts.Select(part => part.Name).Order(), parts.Keys.Order());
        foreach (var part in parts.Values)
        {
            // Both stand in the file where they belong, round the middle, inside the box the game gives the stairs.
            Assert.Equal(Vector3.Zero, part.Pivot);
            Assert.InRange(part.PositionMin.X, -Stairs.Radius, 0f);
            Assert.InRange(part.PositionMax.X, 0f, Stairs.Radius);
            Assert.InRange(part.PositionMin.Z, -Stairs.Radius, 0f);
            Assert.InRange(part.PositionMax.Z, 0f, Stairs.Radius);
        }

        // The stone stands on the floor; the lamp lies on top of it and ends at the top of the box.
        var stone = parts[StairsLook.Stone];
        var glow = parts[StairsLook.Glow];
        Assert.Equal(0f, stone.PositionMin.Y, Tolerance);
        Assert.Equal(stone.PositionMax.Y, glow.PositionMin.Y, Tolerance);
        Assert.Equal(Stairs.Height, glow.PositionMax.Y, Tolerance);
    }

    [Fact]
    public void The_stairs_are_as_high_as_the_ceiling_of_the_boss_room()
    {
        // Issue #723: the room is 6 m high and the staircase led up to 4 m only - a podium in the middle of the room.
        // Its box reaches the ceiling over its whole footprint, in every generated boss room.
        foreach (var generated in ManyLevels.Value)
        {
            var boss = Assert.Single(generated.Rooms, room => room.Kind == RoomKind.Boss);
            var (x, z) = Stairs.CellIn(boss.FloorCells);
            var middle = generated.Level.CellCenter(x, z);

            foreach (var corner in new[] { new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f), new Vector3(-1f, 0f, 1f), new Vector3(1f, 0f, 1f), Vector3.Zero })
            {
                var cell = generated.Level.CellAt(middle + (corner * (Stairs.Radius - 0.01f)));
                Assert.Equal(Stairs.Height, cell.CeilingHeight, Tolerance);
            }
        }
    }

    [Theory]
    [MemberData(nameof(EveryVariant))]
    public void Every_model_of_the_stairs_reaches_the_ceiling_of_the_boss_room_and_no_further(TextureVariant variant)
    {
        // Issue #727: compares the file itself with the room it stands in - not two names of the same number.
        var path = RepositoryFiles.Find($"assets/models/{StairsLook.FileName(variant)}");
        var top = GlbScene.Read(File.ReadAllBytes(path)).Max(part => part.PositionMax.Y);

        foreach (var generated in ManyLevels.Value)
        {
            var boss = Assert.Single(generated.Rooms, room => room.Kind == RoomKind.Boss);
            var (x, z) = Stairs.CellIn(boss.FloorCells);
            var ceiling = generated.Level.CellAt(generated.Level.CellCenter(x, z)).CeilingHeight;
            Assert.Equal(ceiling, top, 0.001f);
        }
    }

    [Fact]
    public void The_lamp_of_the_placeholder_lies_on_top_of_it_under_the_ceiling()
    {
        // Issue #727: the lamp used to reach from 6.0 to 6.1 m, through the ceiling of the boss room.
        var bottom = StairsLook.PlaceholderLampMiddle - (StairsLook.PlaceholderLampThickness / 2f);
        var top = StairsLook.PlaceholderLampMiddle + (StairsLook.PlaceholderLampThickness / 2f);

        Assert.True(top <= Stairs.Height + Tolerance, $"The lamp of the placeholder ends at {top} m, above the stairs ({Stairs.Height} m).");
        Assert.Equal(Stairs.Height, top, Tolerance);
        Assert.True(bottom > 0f);
    }

    [Fact]
    public void In_a_hand_made_level_the_stairs_show_variant_a()
    {
        Assert.Equal(TextureVariant.A, StairsLook.Variant(SurfaceVariants.Plain, new Vector3(7f, 0f, 9f)));
    }

    [Fact]
    public void The_stairs_show_the_wall_variant_of_the_boss_room()
    {
        var seen = new HashSet<TextureVariant>();
        foreach (var generated in ManyLevels.Value)
        {
            var surfaces = SurfaceVariants.For(generated);
            var boss = Assert.Single(generated.Rooms, room => room.Kind == RoomKind.Boss);
            var (x, z) = Stairs.CellIn(boss.FloorCells);
            var position = generated.Level.CellCenter(x, z);

            var variant = StairsLook.Variant(surfaces, position);

            Assert.Equal(surfaces.Rooms[boss.Index].Wall, variant);
            seen.Add(variant);
        }

        // The seeds really cover all three variants.
        Assert.Equal(3, seen.Count);
    }

    [Fact]
    public void The_stairs_rise_in_the_cell_the_look_is_worked_out_for()
    {
        // The same small boss fight as in GormTests: a boss room 5 wide in rows 2-11, Gorm in row 10.
        var bossCells = Enumerable.Range(2, 10).SelectMany(z => Enumerable.Range(1, 5).Select(x => (x, z))).ToList();
        var world = GormHallWorld();
        world.Tick(PlayerInput.None with { Interact = true });
        for (var tick = 0; tick < 3 * SimulationClock.TicksPerSecond && !world.Rooms[1].IsSealed; tick++)
        {
            world.Tick(PlayerInput.None with { Move = new Vector2(0f, 1f) });
        }

        world.Gorm!.TakeDamage(10_000, []);
        world.Tick(PlayerInput.None);

        var stairs = Assert.IsType<Stairs>(world.Stairs);
        var (x, z) = Stairs.CellIn(bossCells);
        Assert.Equal(world.Level.CellCenter(x, z), stairs.Position);
        Assert.Equal((x, z), Level.ToGrid(stairs.Position.X, stairs.Position.Z));
    }

    [Fact]
    public void The_cell_of_the_stairs_does_not_depend_on_the_order_of_the_cells()
    {
        // A 4 x 4 room has four cells equally near its middle: the one with the smaller Z, then the smaller X wins.
        var cells = Enumerable.Range(0, 4).SelectMany(z => Enumerable.Range(0, 4).Select(x => (x, z))).ToList();

        Assert.Equal((1, 1), Stairs.CellIn(cells));
        Assert.Equal((1, 1), Stairs.CellIn(Enumerable.Reverse(cells)));
    }

    [Fact]
    public void Of_two_cells_equally_near_the_middle_the_smaller_z_wins_before_the_smaller_x()
    {
        // (0, 1) and (1, 0) lie equally near their middle (0.5, 0.5): Z decides, not X.
        Assert.Equal((1, 0), Stairs.CellIn([(0, 1), (1, 0)]));
        Assert.Equal((1, 0), Stairs.CellIn([(1, 0), (0, 1)]));
    }

    [Theory]
    [InlineData(TextureVariant.B, true, true, TextureVariant.B)]
    [InlineData(TextureVariant.B, true, false, TextureVariant.B)]
    [InlineData(TextureVariant.C, false, true, TextureVariant.A)]
    [InlineData(TextureVariant.A, true, true, TextureVariant.A)]
    [InlineData(TextureVariant.C, false, false, null)]
    [InlineData(TextureVariant.A, false, false, null)]
    public void A_missing_model_falls_back_to_variant_a_then_to_the_placeholder(
        TextureVariant wanted, bool wantedLoaded, bool variantALoaded, TextureVariant? expected)
    {
        Assert.Equal(expected, StairsLook.Pick(wanted, wantedLoaded, variantALoaded));
    }

    [Fact]
    public void Rising_it_comes_up_out_of_the_floor_fast_at_first_and_slowly_at_the_end()
    {
        Assert.Equal(Stairs.Height, StairsLook.Sink(0f), Tolerance);
        Assert.Equal(0f, StairsLook.Sink(1f), Tolerance);
        Assert.Equal(Stairs.Height / 4f, StairsLook.Sink(0.5f), Tolerance);

        var before = float.MaxValue;
        for (var step = 0; step <= 20; step++)
        {
            var sink = StairsLook.Sink(step / 20f);
            Assert.True(sink < before, "The stairs sank back.");
            before = sink;
        }

        // The first half of the time brings it three quarters of the way up.
        Assert.True(Stairs.Height - StairsLook.Sink(0.5f) > 0.7f * Stairs.Height);
    }

    [Fact]
    public void The_lamp_is_lit_only_once_the_stairs_stand()
    {
        Assert.False(StairsLook.ShowsGlow(canInteract: false));
        Assert.True(StairsLook.ShowsGlow(canInteract: true));
    }

    [Fact]
    public void The_lamp_pulses_from_dark_to_full_and_back()
    {
        var tints = Enumerable.Range(0, 400).Select(step => StairsLook.GlowTint(step / 100f)).ToList();

        Assert.All(tints, tint => Assert.InRange(tint, 0f, 1f));
        Assert.True(tints.Min() < 0.05f);
        Assert.True(tints.Max() > 0.95f);
        Assert.Equal(StairsLook.GlowTint(0.3f), StairsLook.GlowTint(0.3f + MathF.PI), 0.001f);
    }

    [Fact]
    public void The_stairs_were_built_on_the_wall_and_floor_textures_in_the_game()
    {
        // tools/models/stairs.py builds the stairs on the wall and floor textures and notes which ones it read. Once one
        // of them changes, the joints of the stairs no longer match the room: run the script again.
        var built = Lines(RepositoryFiles.Find("tools/models/stairs_sources.sha256"));
        var current = Lines(RepositoryFiles.Find("assets/textures/textures.sha256"));

        Assert.Equal(6, built.Count);
        foreach (var line in built)
        {
            Assert.True(
                current.Contains(line),
                $"The stairs were built on '{line}', which is no longer in assets/textures. " +
                "Run tools/models/stairs.py again (see assets/HERKUNFT.md).");
        }
    }

    private static List<string> Lines(string path) => File.ReadAllLines(path)
        .Select(line => line.Trim())
        .Where(line => line.Length > 0 && !line.StartsWith('#'))
        .ToList();
}
