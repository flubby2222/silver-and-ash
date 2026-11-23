using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

public class WallNicheFillingLookTests
{
    private const float Tolerance = 0.0001f;

    // The opening of a deep niche is 1 m wide (tools/models/wall_niche_deep.py): a filling stays between its sides.
    private const float OpeningHalfWidth = 0.5f;

    // A room of three by three cells under the corridor ceiling, as in WallNicheLookTests.
    private const string Room = "#####\n#...#\n#.P.#\n#...#\n#####";

    [Theory]
    [InlineData(WallNicheFilling.Bottles, "wall_niche_bottles.glb", "bottles")]
    [InlineData(WallNicheFilling.Candles, "wall_niche_candles.glb", "candles")]
    [InlineData(WallNicheFilling.Skulls, "wall_niche_skulls.glb", "skulls")]
    public void Every_filling_has_its_file_with_one_fixed_part(WallNicheFilling filling, string file, string part)
    {
        Assert.Equal(file, WallNicheFillingLook.FileName(filling));
        var parts = Assert.Single(WallNicheFillingLook.PartsOf(filling));
        Assert.Equal(new RigPart(part, null), parts);
    }

    [Fact]
    public void An_empty_pocket_has_no_file_and_no_parts()
    {
        Assert.Null(WallNicheFillingLook.FileName(WallNicheFilling.None));
        Assert.Empty(WallNicheFillingLook.PartsOf(WallNicheFilling.None));
    }

    [Fact]
    public void Bottles_lie_on_the_sill_candles_and_skulls_stand_on_the_shelf()
    {
        Assert.Equal(WallNicheFillingLook.SillTop, WallNicheFillingLook.StandHeight(WallNicheFilling.Bottles));
        Assert.Equal(WallNicheFillingLook.ShelfTop, WallNicheFillingLook.StandHeight(WallNicheFilling.Candles));
        Assert.Equal(WallNicheFillingLook.ShelfTop, WallNicheFillingLook.StandHeight(WallNicheFilling.Skulls));
    }

    [Theory]
    [InlineData(WallNicheForm.Flat, WallNicheFilling.None, true)]
    [InlineData(WallNicheForm.Flat, WallNicheFilling.Bottles, false)]
    [InlineData(WallNicheForm.Flat, WallNicheFilling.Candles, false)]
    [InlineData(WallNicheForm.Flat, WallNicheFilling.Skulls, false)]
    [InlineData(WallNicheForm.Deep, WallNicheFilling.None, true)]
    [InlineData(WallNicheForm.Deep, WallNicheFilling.Bottles, true)]
    [InlineData(WallNicheForm.Deep, WallNicheFilling.Candles, false)]
    [InlineData(WallNicheForm.Deep, WallNicheFilling.Skulls, false)]
    [InlineData(WallNicheForm.DeepWithShelf, WallNicheFilling.None, true)]
    [InlineData(WallNicheForm.DeepWithShelf, WallNicheFilling.Bottles, true)]
    [InlineData(WallNicheForm.DeepWithShelf, WallNicheFilling.Candles, true)]
    [InlineData(WallNicheForm.DeepWithShelf, WallNicheFilling.Skulls, true)]
    public void Only_a_pocket_takes_a_filling_and_only_a_shelf_takes_candles_and_skulls(WallNicheForm form, WallNicheFilling filling, bool fits)
    {
        Assert.Equal(fits, WallNicheFillingLook.Fits(form, filling));
    }

    [Theory]
    [InlineData(WallNicheForm.Flat, WallNicheFilling.Bottles)]
    [InlineData(WallNicheForm.Deep, WallNicheFilling.Candles)]
    [InlineData(WallNicheForm.Deep, WallNicheFilling.Skulls)]
    public void A_filling_the_niche_does_not_take_is_not_placed(WallNicheForm form, WallNicheFilling filling)
    {
        Assert.Throws<ArgumentException>(() => WallNicheFillingLook.Place(form, filling, Vector3.Zero, Vector3.UnitZ));
    }

    [Fact]
    public void An_unknown_filling_has_no_file()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WallNicheFillingLook.FileName((WallNicheFilling)99));
    }

    [Theory]
    [InlineData(2, 1, RoomSide.North)]
    [InlineData(2, 3, RoomSide.South)]
    [InlineData(1, 2, RoomSide.West)]
    [InlineData(3, 2, RoomSide.East)]
    public void A_filling_stands_on_its_sill_or_shelf_against_the_back_of_the_pocket_also_on_a_raised_floor(int x, int z, RoomSide wall)
    {
        // Floor raised by 1.5 m, the clear height of the corridor kept: the filling goes up with the niche.
        var level = Raised(LevelParser.Parse(Room), 1.5f);
        var (niche, facing, _) = WallNicheLook.Place(level, x, z, wall);
        Assert.Equal(1.5f, niche.Y, Tolerance);

        foreach (var (form, filling) in new[]
        {
            (WallNicheForm.Deep, WallNicheFilling.Bottles), (WallNicheForm.DeepWithShelf, WallNicheFilling.Bottles),
            (WallNicheForm.DeepWithShelf, WallNicheFilling.Candles), (WallNicheForm.DeepWithShelf, WallNicheFilling.Skulls),
        })
        {
            var (position, looking) = WallNicheFillingLook.Place(form, filling, niche, facing);
            Assert.Equal(facing, looking);
            Assert.Equal(niche.Y + WallNicheFillingLook.StandHeight(filling), position.Y, Tolerance);
            var offset = position - niche;
            Assert.Equal(-WallNicheLook.PocketDepth, Vector3.Dot(offset, facing), Tolerance);
            Assert.Equal(0f, Vector3.Dot(offset, Vector3.Cross(Vector3.UnitY, facing)), Tolerance);
        }
    }

    [Fact]
    public void The_sill_and_the_shelf_of_the_deep_niche_model_are_where_the_fillings_stand()
    {
        var shelf = Parts(WallNicheLook.FileName(WallNicheForm.DeepWithShelf, WallNicheSize.Corridor, TextureVariant.A));
        Assert.Equal(WallNicheFillingLook.SillTop, shelf[WallNicheLook.Sill].PositionMax.Y, Tolerance);
        Assert.Equal(WallNicheFillingLook.ShelfBottom, shelf[WallNicheLook.Shelf].PositionMin.Y, Tolerance);
        Assert.Equal(WallNicheFillingLook.ShelfTop, shelf[WallNicheLook.Shelf].PositionMax.Y, Tolerance);
        Assert.Equal(WallNicheFillingLook.FrameFront, shelf[WallNicheLook.Frame].PositionMax.Z, Tolerance);

        var deep = Parts(WallNicheLook.FileName(WallNicheForm.Deep, WallNicheSize.Corridor, TextureVariant.A));
        Assert.Equal(WallNicheFillingLook.SillTop, deep[WallNicheLook.Sill].PositionMax.Y, Tolerance);
        Assert.Equal(WallNicheFillingLook.FrameFront, deep[WallNicheLook.Frame].PositionMax.Z, Tolerance);
    }

    [Theory]
    [InlineData(WallNicheForm.Deep, WallNicheFilling.Bottles, RoomSide.South)]
    [InlineData(WallNicheForm.Deep, WallNicheFilling.Bottles, RoomSide.West)]
    [InlineData(WallNicheForm.DeepWithShelf, WallNicheFilling.Bottles, RoomSide.North)]
    [InlineData(WallNicheForm.DeepWithShelf, WallNicheFilling.Bottles, RoomSide.East)]
    [InlineData(WallNicheForm.DeepWithShelf, WallNicheFilling.Candles, RoomSide.South)]
    [InlineData(WallNicheForm.DeepWithShelf, WallNicheFilling.Skulls, RoomSide.West)]
    public void A_placed_filling_stays_in_its_pocket_and_reaches_no_further_than_the_frame(
        WallNicheForm form, WallNicheFilling filling, RoomSide wall)
    {
        // The file's corners as the game sets them (ModelPlacement: turned about y to the facing, then moved), in a niche
        // on a raised floor; measured against the deep niche's own model, set the same way.
        var level = Raised(LevelParser.Parse(Room), 1.5f);
        var (x, z) = wall switch
        {
            RoomSide.North => (2, 1),
            RoomSide.South => (2, 3),
            RoomSide.West => (1, 2),
            _ => (3, 2),
        };
        var (niche, facing, _) = WallNicheLook.Place(level, x, z, wall);
        var (position, looking) = WallNicheFillingLook.Place(form, filling, niche, facing);
        var corners = Corners(WallNicheFillingLook.FileName(filling)!, position, looking);
        var model = Parts(WallNicheLook.FileName(form, WallNicheSize.Corridor, TextureVariant.A));
        var along = Vector3.Cross(Vector3.UnitY, facing);

        foreach (var corner in corners)
        {
            var local = corner - niche;
            var height = local.Y;
            var outOfWall = Vector3.Dot(local, facing);
            var side = Vector3.Dot(local, along);

            // Nothing over the frame into the corridor, nothing through the back of the pocket, nothing into its sides.
            Assert.True(outOfWall <= model[WallNicheLook.Frame].PositionMax.Z + Tolerance, $"{filling} reaches {outOfWall:0.0000} m out of the wall.");
            Assert.True(outOfWall >= -WallNicheLook.PocketDepth - Tolerance, $"{filling} reaches {-outOfWall:0.0000} m into the wall.");
            Assert.True(MathF.Abs(side) <= OpeningHalfWidth + Tolerance, $"{filling} reaches {side:0.0000} m to the side.");
            Assert.True(height >= WallNicheFillingLook.StandHeight(filling) - Tolerance, $"{filling} sinks to {height:0.0000} m.");

            // Bottles lie under the shelf, if there is one.
            if (filling == WallNicheFilling.Bottles && form == WallNicheForm.DeepWithShelf)
            {
                Assert.True(height <= model[WallNicheLook.Shelf].PositionMin.Y + Tolerance, $"The bottles reach {height:0.0000} m into the shelf.");
            }
        }
    }

    [Fact]
    public void The_bottles_end_flush_with_the_frame_and_lie_in_the_left_corner()
    {
        // The designer's open point (issue #553): the bottles are 0.36 m long, the pocket 0.3 m deep - their corks end
        // just behind the front of the frame, not in front of it. Seen from the corridor, the stack lies on the left.
        var corners = GlbVertices.Read(File.ReadAllBytes(Model(WallNicheFillingLook.FileName(WallNicheFilling.Bottles)!)))["bottles"];
        var front = corners.Max(corner => corner.Z) - WallNicheLook.PocketDepth;
        Assert.InRange(front, WallNicheFillingLook.FrameFront - 0.005f, WallNicheFillingLook.FrameFront);
        Assert.True(corners.Min(corner => corner.X) < -OpeningHalfWidth + 0.01f);
        Assert.True(corners.Max(corner => corner.X) < 0f);
    }

    [Fact]
    public void Without_its_model_a_deep_niche_leaves_the_wall_face_closed()
    {
        var level = LevelParser.Parse(Room);
        var (position, facing, size) = WallNicheLook.Place(level, 2, 3, RoomSide.South);
        var (otherPosition, otherFacing, _) = WallNicheLook.Place(level, 1, 2, RoomSide.West);
        var deep = new ExhibitWallNiche(TextureVariant.A, size, position, facing) { Form = WallNicheForm.Deep };
        var other = new ExhibitWallNiche(TextureVariant.A, size, otherPosition, otherFacing) { Form = WallNicheForm.DeepWithShelf };
        var flat = new ExhibitWallNiche(TextureVariant.A, size, position, facing);
        var holed = level.WithWallCutouts([WallNicheLook.HoleOf(deep)!.Value, WallNicheLook.HoleOf(other)!.Value]);

        // Every niche drawn: the level as it is, the very same.
        Assert.Same(holed, WallNicheLook.WithoutHolesOfUndrawn(holed, [deep, other, flat], _ => true));

        // One deep niche not drawn: its hole is gone, the other stays.
        var drawn = WallNicheLook.WithoutHolesOfUndrawn(holed, [deep, other, flat], niche => niche != deep);
        Assert.Equal([WallNicheLook.HoleOf(other)!.Value], drawn.WallCutouts);

        // A flat niche leaves no hole, so it closes none.
        Assert.Same(holed, WallNicheLook.WithoutHolesOfUndrawn(holed, [flat], _ => false));
    }

    [Theory]
    [InlineData(2, 1, RoomSide.North)]
    [InlineData(2, 3, RoomSide.South)]
    [InlineData(1, 2, RoomSide.West)]
    [InlineData(3, 2, RoomSide.East)]
    public void The_hole_of_a_niche_is_the_one_of_its_cell_and_wall(int x, int z, RoomSide wall)
    {
        var level = LevelParser.Parse(Room);
        var (position, facing, size) = WallNicheLook.Place(level, x, z, wall);
        var niche = new ExhibitWallNiche(TextureVariant.B, size, position, facing) { Form = WallNicheForm.Deep };
        Assert.Equal(WallNicheLook.Cutout(WallNicheForm.Deep, x, z, wall), WallNicheLook.HoleOf(niche));
        Assert.Null(WallNicheLook.HoleOf(niche with { Form = WallNicheForm.Flat }));
    }

    private static string Model(string file)
    {
        var path = RepositoryFiles.Find($"assets/models/{file}");
        Assert.True(File.Exists(path), $"{path} is missing.");
        return path;
    }

    private static Dictionary<string, GlbPart> Parts(string file) =>
        GlbScene.Read(File.ReadAllBytes(Model(file))).ToDictionary(part => part.Name);

    // The corners of a filling's file as ModelPlacement sets them: turned from +z to facing about y, then moved.
    private static IEnumerable<Vector3> Corners(string file, Vector3 position, Vector3 facing)
    {
        var place = Matrix4x4.CreateRotationY(MathF.Atan2(facing.X, facing.Z)) * Matrix4x4.CreateTranslation(position);
        return GlbVertices.Read(File.ReadAllBytes(Model(file))).Values.SelectMany(part => part).Select(corner => Vector3.Transform(corner, place));
    }

    // A copy of level with every open cell raised by lift, its clear height kept.
    private static Level Raised(Level level, float lift)
    {
        var cells = new Cell[level.Width, level.Depth];
        for (var x = 0; x < level.Width; x++)
        {
            for (var z = 0; z < level.Depth; z++)
            {
                var cell = level[x, z];
                cells[x, z] = cell.IsSolid ? cell : cell with { FloorHeight = cell.FloorHeight + lift, CeilingHeight = cell.CeilingHeight + lift };
            }
        }

        return new Level(cells, 1, 1, null, [], [], []);
    }
}
