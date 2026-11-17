using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

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
        const string map = "##.P.##\n###.###\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#..T..#\n#.....#";
        var bossCells = Enumerable.Range(2, 10).SelectMany(z => Enumerable.Range(1, 5).Select(x => (x, z))).ToList();
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 7, 1, 0, [(2, 0), (3, 0), (4, 0)], [], []),
            new(1, RoomKind.Boss, "hall", 0, 2, 7, 10, 1, bossCells, [], []),
        ];
        LevelDoor[] doors = [new(3, 1, 0, 1, RoomSide.South, DoorKind.Boss)];
        var world = new GameWorld(LevelParser.Parse(map), 1, Balance.Default, doors, rooms);
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
