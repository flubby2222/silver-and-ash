using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

public class SurfaceVariantsTests
{
    private const int ManySeeds = 200;

    private static readonly IReadOnlyList<RoomTemplate> Rooms = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    private static readonly LayoutBalance Layout = Balance.Default.Layout;

    private static readonly Lazy<GeneratedLevel[]> ManyLevels =
        new(() => Enumerable.Range(0, ManySeeds).Select(seed => LevelGenerator.Generate(Rooms, seed, Layout)).ToArray());

    [Fact]
    public void The_same_seed_gives_the_same_choice()
    {
        var first = SurfaceVariants.For(LevelGenerator.Generate(Rooms, 77, Layout));
        var second = SurfaceVariants.For(LevelGenerator.Generate(Rooms, 77, Layout));

        Assert.Equal(first.Rooms, second.Rooms);
    }

    [Fact]
    public void Different_seeds_give_different_choices()
    {
        var choices = ManyLevels.Value.Select(level => string.Join(";", SurfaceVariants.For(level).Rooms)).Distinct().Count();

        Assert.True(choices > ManySeeds * 9 / 10, $"Only {choices} different choices from {ManySeeds} seeds.");
    }

    [Fact]
    public void Rooms_of_one_level_do_not_all_look_the_same()
    {
        // With five or more rooms, three variants and three kinds of surface, a level where every room looks the same
        // would mean the room number does not take part in the choice.
        var levels = ManyLevels.Value.Where(level => level.Rooms.Count(room => room.Kind != RoomKind.Corridor) >= 5).ToList();
        Assert.NotEmpty(levels);
        Assert.All(levels, level =>
        {
            var looks = SurfaceVariants.For(level);
            var distinct = level.Rooms.Where(room => room.Kind != RoomKind.Corridor).Select(room => looks.Rooms[room.Index]).Distinct().Count();
            Assert.True(distinct > 1, $"Seed {level.Seed}: every room looks the same.");
        });
    }

    [Theory]
    [InlineData(SurfaceKind.Wall)]
    [InlineData(SurfaceKind.Floor)]
    [InlineData(SurfaceKind.Ceiling)]
    public void Every_variant_of_every_surface_turns_up_and_none_is_rare(SurfaceKind kind)
    {
        // Treasure, secret and start rooms may show all three on the floor too.
        var counts = ManyLevels.Value
            .SelectMany(level => level.Rooms.Where(room => SurfaceVariants.AllowsCobbles(room.Kind))
                .Select(room => SurfaceVariants.For(level).Rooms[room.Index].For(kind)))
            .GroupBy(variant => variant)
            .ToDictionary(group => group.Key, group => group.Count());
        var total = counts.Values.Sum();

        Assert.All(Enum.GetValues<TextureVariant>(), variant =>
            Assert.True(counts.GetValueOrDefault(variant) > total / 5, $"{kind} {variant}: {counts.GetValueOrDefault(variant)} of {total}."));
    }

    [Fact]
    public void Cobbles_lie_only_in_start_treasure_and_secret_rooms()
    {
        var kindsWithCobbles = new HashSet<RoomKind>();
        foreach (var level in ManyLevels.Value)
        {
            var looks = SurfaceVariants.For(level);
            foreach (var room in level.Rooms.Where(room => looks.Rooms[room.Index].Floor == TextureVariant.C))
            {
                kindsWithCobbles.Add(room.Kind);
            }
        }

        Assert.Equal(new[] { RoomKind.Start, RoomKind.Treasure, RoomKind.Secret }.Order(), kindsWithCobbles.Order());
    }

    [Fact]
    public void Combat_boss_and_corridor_floors_still_show_both_other_variants()
    {
        var floors = ManyLevels.Value
            .SelectMany(level => level.Rooms.Where(room => room.Kind is RoomKind.Combat or RoomKind.Boss or RoomKind.Corridor)
                .Select(room => SurfaceVariants.For(level).Rooms[room.Index].Floor))
            .Distinct()
            .Order();

        Assert.Equal([TextureVariant.A, TextureVariant.B], floors);
    }

    [Fact]
    public void A_corridor_looks_like_the_room_it_begins_at_except_for_cobbles()
    {
        var corridors = 0;
        foreach (var level in ManyLevels.Value)
        {
            var looks = SurfaceVariants.For(level);
            foreach (var corridor in level.Rooms.Where(room => room.Kind == RoomKind.Corridor))
            {
                // The generator joins a corridor first to the room it was placed from.
                var first = level.Doors.First(door => door.RoomA == corridor.Index || door.RoomB == corridor.Index);
                var origin = first.RoomA == corridor.Index ? first.RoomB : first.RoomA;
                var room = looks.Rooms[origin];
                var own = looks.Rooms[corridor.Index];

                Assert.Equal(room.Wall, own.Wall);
                Assert.Equal(room.Ceiling, own.Ceiling);
                if (room.Floor != TextureVariant.C)
                {
                    Assert.Equal(room.Floor, own.Floor);
                }

                corridors++;
            }
        }

        Assert.True(corridors > ManySeeds, $"Only {corridors} corridors in {ManySeeds} levels.");
    }

    [Fact]
    public void All_surfaces_of_one_room_show_its_choice_and_door_cells_the_inner_room()
    {
        var level = ManyLevels.Value[3];
        var looks = SurfaceVariants.For(level);

        foreach (var room in level.Rooms)
        {
            Assert.All(room.FloorCells, cell => Assert.All(Enum.GetValues<SurfaceKind>(), kind =>
                Assert.Equal(looks.Rooms[room.Index].For(kind), looks.At(cell.X, cell.Z, kind))));
        }

        Assert.All(level.Doors, door => Assert.All(Enum.GetValues<SurfaceKind>(), kind =>
            Assert.Equal(looks.Rooms[door.RoomA].For(kind), looks.At(door.X, door.Z, kind))));
    }

    [Fact]
    public void Every_face_of_a_generated_level_takes_the_variant_of_its_own_room()
    {
        // Walls between two rooms belong to the room they face; so do floors and ceilings to the room they lie in.
        var level = ManyLevels.Value[5];
        var looks = SurfaceVariants.For(level);
        var roomOf = level.Rooms.SelectMany(room => room.FloorCells.Select(cell => (cell, room.Index)))
            .Concat(level.Doors.Select(door => ((door.X, door.Z), door.RoomA)))
            .GroupBy(pair => pair.Item1)
            .ToDictionary(group => group.Key, group => group.First().Item2);

        Assert.All(LevelGeometry.Build(level.Level), face =>
        {
            var cell = LevelGeometry.CellOf(face);
            Assert.True(roomOf.ContainsKey(cell), $"Face {face} belongs to cell {cell}, which is in no room.");
            Assert.Equal(looks.Rooms[roomOf[cell]].For(face.Kind), looks.At(cell.X, cell.Z, face.Kind));
        });
    }

    [Fact]
    public void Cells_of_no_room_and_hand_made_levels_show_variant_a()
    {
        var looks = SurfaceVariants.For(ManyLevels.Value[0]);

        Assert.All(Enum.GetValues<SurfaceKind>(), kind =>
        {
            Assert.Equal(TextureVariant.A, looks.At(-5, -5, kind));
            Assert.Equal(TextureVariant.A, SurfaceVariants.Plain.At(1, 1, kind));
        });
    }

    [Fact]
    public void The_choice_does_not_shift_the_level_a_seed_gives()
    {
        // The variants come from the seed alone, not from the generator's random numbers: building the level, then
        // the variants, then the level again gives the same level - and a run of level 0 gets the same rooms as the
        // generator alone.
        var before = LevelGenerator.Generate(Rooms, 4711, Layout);
        _ = SurfaceVariants.For(before);
        var after = LevelGenerator.Generate(Rooms, 4711, Layout);

        Assert.Equal(Describe(before), Describe(after));

        var run = new LevelZero(Rooms).NewRun(4711, Balance.Default);
        Assert.Equal(SurfaceVariants.For(before).Rooms, run.Surfaces.Rooms);
    }

    [Fact]
    public void A_restarted_run_keeps_its_look()
    {
        var run = new LevelZero(Rooms).NewRun(12, Balance.Default);

        Assert.Same(run.Surfaces, run.Restarted().Surfaces);
    }

    [Fact]
    public void A_hand_made_world_shows_variant_a()
    {
        var world = new GameWorld(LevelParser.Parse(
            """
            ####
            #P.#
            ####
            """));

        Assert.Same(SurfaceVariants.Plain, world.Surfaces);
    }

    [Fact]
    public void Each_variant_has_its_own_file_in_the_checked_in_list()
    {
        var set = LevelTextureSet.Catacombs;
        Assert.Equal("catacomb_wall.png", set.FileFor(SurfaceKind.Wall, TextureVariant.A));
        Assert.Equal("catacomb_floor_b.png", set.FileFor(SurfaceKind.Floor, TextureVariant.B));
        Assert.Equal("catacomb_ceiling_c.png", set.FileFor(SurfaceKind.Ceiling, TextureVariant.C));

        var folder = RepositoryFiles.Find("assets/textures");
        var list = ModelChecksums.Parse(File.ReadAllText(Path.Combine(folder, LevelTextureSet.ChecksumFileName)));
        foreach (var layer in Enum.GetValues<LevelLayer>())
        {
            foreach (var kind in Enum.GetValues<SurfaceKind>())
            {
                Assert.All(Enum.GetValues<TextureVariant>(), variant => Assert.Contains(LevelTextureSet.For(layer).FileFor(kind, variant), list.Names));
            }
        }
    }

    private static string Describe(GeneratedLevel generated) =>
        string.Join("|", generated.Rooms.Select(room => $"{room.Kind}:{room.TemplateName}@{room.X},{room.Z}"))
        + "/" + string.Join("|", generated.Doors.Select(door => $"{door.X},{door.Z}:{door.RoomA}-{door.RoomB}"));
}
