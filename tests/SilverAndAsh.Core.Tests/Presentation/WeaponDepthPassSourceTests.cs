namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Issue #637: the weapons are drawn with a depth of their own (<see cref="SilverAndAsh.Core.Presentation.WeaponDepth"/>,
/// issue #624), and the world's near and far planes are the ones those tests reckon with. Both happen in the game
/// (WeaponDepthPass, WeaponViews.Draw, Program.RunGame), which no test can start without a window, so this reads their
/// source, as <see cref="RestartViewsTests"/> does: taking the pass out of WeaponViews, or the call of
/// SetWorldClipPlanes out of Program, turns a test red.
/// </summary>
public class WeaponDepthPassSourceTests
{
    private static readonly string Program = Read("src/SilverAndAsh.Game/Program.cs");
    private static readonly string Pass = Read("src/SilverAndAsh.Game/Rendering/WeaponDepthPass.cs");
    private static readonly string Views = Read("src/SilverAndAsh.Game/Rendering/WeaponViews.cs");

    [Fact]
    public void The_game_sets_the_worlds_clip_planes_once_after_opening_the_window_and_before_drawing()
    {
        const string Call = "WeaponDepthPass.SetWorldClipPlanes();";
        var window = Program.IndexOf("Raylib.InitWindow(", StringComparison.Ordinal);
        var call = Program.IndexOf(Call, StringComparison.Ordinal);
        var drawing = Program.IndexOf("Raylib.BeginMode3D(", StringComparison.Ordinal);

        Assert.True(window >= 0 && drawing >= 0, "Program.cs no longer opens the window or draws the world in 3D.");
        Assert.Equal(1, Occurrences(Program, Call));
        Assert.True(window < call && call < drawing, "SetWorldClipPlanes is not called between InitWindow and the first BeginMode3D.");
    }

    [Fact]
    public void SetWorldClipPlanes_hands_raylib_the_planes_the_tests_reckon_with()
    {
        var body = Body(Pass, "public static void SetWorldClipPlanes()");

        Assert.Contains("Rlgl.SetClipPlanes(", body, StringComparison.Ordinal);
        Assert.Contains("WeaponDepth.WorldNearPlane", body, StringComparison.Ordinal);
        Assert.Contains("WeaponDepth.WorldFarPlane", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Both_hands_are_drawn_inside_the_weapons_depth_pass_and_nowhere_else()
    {
        var draw = Body(Views, "public void Draw(Vector3 eye, Vector3 forward)");
        var begin = draw.IndexOf("WeaponDepthPass.Begin()", StringComparison.Ordinal);
        var end = draw.IndexOf("WeaponDepthPass.End(", StringComparison.Ordinal);

        Assert.True(begin >= 0, "WeaponViews.Draw no longer switches to the weapons' depth (WeaponDepthPass.Begin).");
        Assert.True(end > begin, "WeaponViews.Draw no longer switches back to the world's depth after the weapons (WeaponDepthPass.End).");
        foreach (var hand in (string[])["DrawBlade(", "DrawPistol("])
        {
            var at = draw.IndexOf(hand, StringComparison.Ordinal);
            Assert.True(begin < at && at < end, $"{hand}...) is not drawn between WeaponDepthPass.Begin and End.");

            // Called once (in Draw) and declared once: no other way to draw a hand past the pass.
            Assert.Equal(2, Occurrences(Views, hand));
        }
    }

    [Fact]
    public void The_pass_replaces_only_the_depth_row_and_gives_the_world_its_projection_back()
    {
        var begin = Body(Pass, "public static Matrix4x4 Begin()");
        var end = Body(Pass, "public static void End(Matrix4x4 world)");

        Assert.Contains("WeaponDepth.Row(", begin, StringComparison.Ordinal);
        Assert.Contains("weapons.M33 = -a;", begin, StringComparison.Ordinal);
        Assert.Contains("weapons.M34 = -b;", begin, StringComparison.Ordinal);
        Assert.Contains("Rlgl.SetMatrixProjection(weapons);", begin, StringComparison.Ordinal);
        Assert.Contains("return world;", begin, StringComparison.Ordinal);
        Assert.Contains("Rlgl.SetMatrixProjection(world);", end, StringComparison.Ordinal);
    }

    [Fact]
    public void The_game_draws_the_weapons_within_the_3d_picture_of_the_world()
    {
        var begin = Program.IndexOf("Raylib.BeginMode3D(", StringComparison.Ordinal);
        var weapons = Program.IndexOf("weaponView.Draw(", StringComparison.Ordinal);
        var end = Program.IndexOf("Raylib.EndMode3D();", StringComparison.Ordinal);

        Assert.True(begin < weapons && weapons < end, "Program.cs no longer draws the weapons between BeginMode3D and EndMode3D.");
    }

    private static string Read(string path) => File.ReadAllText(RepositoryFiles.Find(path));

    private static int Occurrences(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    // From the method's opening brace (or its => for an expression body) to its end.
    private static string Body(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Not found: {signature}");
        var after = start + signature.Length;
        var arrow = source.IndexOf("=>", after, StringComparison.Ordinal);
        var open = source.IndexOf('{', after);
        if (arrow >= 0 && (open < 0 || arrow < open))
        {
            return source[arrow..(source.IndexOf(';', arrow) + 1)];
        }

        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            depth += source[i] switch { '{' => 1, '}' => -1, _ => 0 };
            if (depth == 0)
            {
                return source[open..(i + 1)];
            }
        }

        throw new InvalidOperationException($"Not closed: {signature}");
    }
}
