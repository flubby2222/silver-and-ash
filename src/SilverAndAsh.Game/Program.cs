using System.Numerics;
using System.Reflection;
using Raylib_cs;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.World;
using SilverAndAsh.Game.Input;
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

    // Esc must not close the game any more - it releases the mouse (later: opens the pause menu).
    Raylib.SetExitKey(KeyboardKey.Null);

    try
    {
        var player = new Player(level);
        var input = new RaylibInput();
        input.CaptureMouse();

        var accumulator = new TickAccumulator();

        // Game loop: runs once per drawn frame until the window is closed (X button).
        while (!Raylib.WindowShouldClose())
        {
            // 1. Input: looking around happens every frame, so it feels immediate.
            var playerInput = input.Read();
            player.Look(playerInput);

            // 2. Simulation: fixed ticks. Nothing moves yet - walking follows in the next issue.
            _ = accumulator.Advance(Raylib.GetFrameTime());

            // 3. Rendering: draw the current state. No game rules in here.
            var eye = player.EyePosition;
            var camera = new Camera3D
            {
                Position = eye,
                Target = eye + player.View.Forward,
                Up = Vector3.UnitY,
                FovY = 70f,
                Projection = CameraProjection.Perspective,
            };

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 16, 24, 255));

            Raylib.BeginMode3D(camera);
            LevelRenderer.Draw(level);
            Raylib.EndMode3D();

            Raylib.DrawText("Silver & Ash", 20, 20, 30, new Color(200, 200, 210, 255));
            Raylib.DrawFPS(20, ScreenHeight - 30);

            if (!input.IsMouseCaptured)
            {
                const string Hint = "Klicken, um dich umzusehen";
                var hintWidth = Raylib.MeasureText(Hint, 24);
                Raylib.DrawText(Hint, (ScreenWidth - hintWidth) / 2, ScreenHeight / 2 + 40, 24, new Color(200, 200, 210, 255));
            }

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
