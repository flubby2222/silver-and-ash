using System.Numerics;
using System.Reflection;
using Raylib_cs;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.World;
using SilverAndAsh.Game.Rendering;

const int ScreenWidth = 1280;
const int ScreenHeight = 720;

var version = typeof(Program).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

// Log files: %LOCALAPPDATA%\SilverAndAsh\logs\ - local only, nothing is sent anywhere.
var logDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SilverAndAsh", "logs");

// A broken log must never stop the game from starting - then we simply play without one.
FileLog? log = null;
try
{
    log = FileLog.Start(logDirectory);
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Could not create log file in '{logDirectory}': {exception.Message}");
}

using (log)
{
    // Last line of defence: crashes on other threads end up in the log, too.
    AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        log?.Error("Unhandled exception - the game is crashing.", args.ExceptionObject as Exception);

    log?.Info($"Silver & Ash {version} started ({Environment.OSVersion}, .NET {Environment.Version}).");

    // Load the level before opening the window: if it is broken, no window flashes up.
    var levelPath = Path.Combine(AppContext.BaseDirectory, "assets", "levels", "test.txt");
    Level? level = null;
    try
    {
        level = LevelParser.ParseFile(levelPath);
        log?.Info($"Level '{levelPath}' loaded: {level.Width} x {level.Depth} cells.");
    }
    catch (Exception exception) when (exception is LevelFormatException or IOException or UnauthorizedAccessException)
    {
        log?.Error($"Could not load level '{levelPath}'.", exception);
        Console.Error.WriteLine($"Could not load level '{levelPath}': {exception.Message}");
        Environment.ExitCode = 1;
    }

    if (level is not null)
    {
        try
        {
            // Shown in the corner without the commit id ("0.1.0+abc123..." -> "0.1.0").
            RunGame(level, displayVersion: version.Split('+')[0]);
            log?.Info("Game ended normally.");
        }
        catch (Exception exception)
        {
            log?.Error("Unhandled exception - the game is crashing.", exception);
            Console.Error.WriteLine(exception);
            Environment.ExitCode = 1;
        }
    }
}

static void RunGame(Level level, string displayVersion)
{
    Raylib.SetConfigFlags(ConfigFlags.VSyncHint | ConfigFlags.Msaa4xHint);
    Raylib.InitWindow(ScreenWidth, ScreenHeight, "Silver & Ash");

    try
    {
        // Temporary overview camera, looking down at the level at an angle.
        // Replaced by the first-person camera in the next issue.
        var levelCentre = new Vector3(level.Width * Level.CellSize / 2f, 0f, level.Depth * Level.CellSize / 2f);
        var camera = new Camera3D
        {
            Position = levelCentre + new Vector3(0f, 26f, 20f),
            Target = levelCentre,
            Up = Vector3.UnitY,
            FovY = 45f,
            Projection = CameraProjection.Perspective,
        };

        var accumulator = new TickAccumulator();

        // Game loop: runs once per drawn frame until the window is closed (Esc or the X button).
        while (!Raylib.WindowShouldClose())
        {
            // 1. Simulation: nothing moves yet - the player arrives with the first-person camera.
            //    The accumulator already keeps the fixed timestep running.
            _ = accumulator.Advance(Raylib.GetFrameTime());

            // 2. Rendering: draw the current state. No game rules in here.
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 16, 24, 255));

            Raylib.BeginMode3D(camera);
            LevelRenderer.Draw(level);
            Raylib.EndMode3D();

            Raylib.DrawText("Silver & Ash", 20, 20, 30, new Color(200, 200, 210, 255));
            Raylib.DrawFPS(20, ScreenHeight - 30);

            var versionText = $"v{displayVersion}";
            var versionWidth = Raylib.MeasureText(versionText, 20);
            Raylib.DrawText(versionText, ScreenWidth - versionWidth - 20, ScreenHeight - 30, 20, new Color(120, 120, 130, 255));

            Raylib.EndDrawing();
        }
    }
    finally
    {
        Raylib.CloseWindow();
    }
}
