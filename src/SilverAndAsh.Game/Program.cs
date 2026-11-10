using System.Numerics;
using System.Reflection;
using Raylib_cs;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Logging;

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

    try
    {
        RunGame();
        log?.Info("Game ended normally.");
    }
    catch (Exception exception)
    {
        log?.Error("Unhandled exception - the game is crashing.", exception);
        Console.Error.WriteLine(exception);
        Environment.ExitCode = 1;
    }
}

static void RunGame()
{
    Raylib.SetConfigFlags(ConfigFlags.VSyncHint | ConfigFlags.Msaa4xHint);
    Raylib.InitWindow(ScreenWidth, ScreenHeight, "Silver & Ash");

    try
    {
        var camera = new Camera3D
        {
            Position = new Vector3(4f, 3f, 4f),
            Target = Vector3.Zero,
            Up = Vector3.UnitY,
            FovY = 45f,
            Projection = CameraProjection.Perspective,
        };

        var cube = new SpinningCube();
        var accumulator = new TickAccumulator();

        // Game loop: runs once per drawn frame until the window is closed (Esc or the X button).
        while (!Raylib.WindowShouldClose())
        {
            // 1. Simulation: run as many fixed ticks as fit into the real time of the last frame.
            var ticks = accumulator.Advance(Raylib.GetFrameTime());
            for (var i = 0; i < ticks; i++)
            {
                cube.Tick();
            }

            // 2. Rendering: draw the current state. No game rules in here.
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 16, 24, 255));

            Raylib.BeginMode3D(camera);
            Raylib.DrawGrid(10, 1f);

            Rlgl.PushMatrix();
            Rlgl.Rotatef(cube.RotationRadians * (180f / MathF.PI), 0f, 1f, 0f);
            Raylib.DrawCube(new Vector3(0f, 1f, 0f), 2f, 2f, 2f, new Color(140, 20, 30, 255));
            Raylib.DrawCubeWires(new Vector3(0f, 1f, 0f), 2f, 2f, 2f, new Color(200, 200, 210, 255));
            Rlgl.PopMatrix();

            Raylib.EndMode3D();

            Raylib.DrawText("Silver & Ash", 20, 20, 30, new Color(200, 200, 210, 255));
            Raylib.DrawFPS(20, ScreenHeight - 30);

            Raylib.EndDrawing();
        }
    }
    finally
    {
        Raylib.CloseWindow();
    }
}
