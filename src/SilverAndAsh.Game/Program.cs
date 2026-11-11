using System.Globalization;
using System.Numerics;
using System.Reflection;
using Raylib_cs;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.World;
using SilverAndAsh.Game.Input;
using SilverAndAsh.Game.Rendering;

// Starting size of the window. It can be resized; the 3D picture keeps its shape (black bars if needed).
const int WindowWidth = 1280;
const int WindowHeight = 720;

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
    AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        log?.Error("Unhandled exception - the game is crashing.", e.ExceptionObject as Exception);

    log?.Info($"Silver & Ash {version} started ({Environment.OSVersion}, .NET {Environment.Version}).");

    // Load the level before opening the window: if it is broken, no window flashes up.
    // Choose another level with: dotnet run --project src/SilverAndAsh.Game -- --level heights
    Level? level = null;
    try
    {
        var options = LaunchOptions.Parse(args);
        var levelPath = options.LevelPath(Path.Combine(AppContext.BaseDirectory, "assets", "levels"));
        level = LevelParser.ParseFile(levelPath);
        log?.Info($"Level '{levelPath}' loaded: {level.Width} x {level.Depth} cells.");
    }
    catch (Exception exception) when (exception is ArgumentException or LevelFormatException or IOException or UnauthorizedAccessException)
    {
        log?.Error("Could not load the level.", exception);
        Console.Error.WriteLine($"Could not load the level: {exception.Message}");
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
    // No MSAA (edge smoothing): the hard pixel edges of the low resolution are wanted.
    Raylib.SetConfigFlags(ConfigFlags.VSyncHint | ConfigFlags.ResizableWindow);
    Raylib.InitWindow(WindowWidth, WindowHeight, "Silver & Ash");

    // Esc must not close the game any more - it releases the mouse (later: opens the pause menu).
    Raylib.SetExitKey(KeyboardKey.Null);

    try
    {
        var player = new Player(level);
        var input = new RaylibInput();
        input.CaptureMouse();

        var accumulator = new TickAccumulator();
        using var world = new LowResolutionTarget(LookSettings.RenderWidth, LookSettings.RenderHeight);

        // Lantern light and fog: a small program for the graphics card (assets/shaders/).
        using var levelShader = new LevelShader(Path.Combine(AppContext.BaseDirectory, "assets", "shaders"));

        // The level geometry is built once here and sent to the graphics card.
        using var levelRenderer = new LevelRenderer(level, levelShader.Shader);

        // Game loop: runs once per drawn frame until the window is closed (X button).
        while (!Raylib.WindowShouldClose())
        {
            // 1. Input: looking around happens every frame, so it feels immediate.
            var playerInput = input.Read();
            player.Look(playerInput);

            // 2. Simulation: as many fixed ticks as fit into the real time of the last frame.
            var ticks = accumulator.Advance(Raylib.GetFrameTime());
            for (var i = 0; i < ticks; i++)
            {
                player.Tick(playerInput);
            }

            // 3. Rendering: draw the current state, blended between the last two ticks (no stutter
            //    on monitors faster than 60 Hz). No game rules in here.
            var eye = player.InterpolatedEyePosition(accumulator.Alpha);
            var camera = new Camera3D
            {
                Position = eye,
                // Looking direction, tilted down a little right after landing (the "nod").
                Target = eye + player.View.ForwardTiltedDown(player.InterpolatedViewTilt(accumulator.Alpha)),
                Up = Vector3.UnitY,
                FovY = LookSettings.FieldOfViewDegrees,
                Projection = CameraProjection.Perspective,
            };

            // 3a. The 3D world goes into the small low-resolution picture ...
            levelShader.SetCameraPosition(eye);
            world.BeginDrawing();
            Raylib.ClearBackground(LookSettings.BackgroundColor);
            Raylib.BeginMode3D(camera);
            levelRenderer.Draw();
            Raylib.EndMode3D();
            world.EndDrawing();

            // 3b. ... which is scaled up onto the screen. The HUD is drawn afterwards at full resolution (sharp).
            Raylib.BeginDrawing();
            Raylib.ClearBackground(LookSettings.BarColor);
            world.DrawToScreen();
            DrawHud(input.IsMouseCaptured, displayVersion);
            DrawStaminaBar(player);
#if DEBUG
            DrawDebugInfo(player);
#endif
            Raylib.EndDrawing();
        }
    }
    finally
    {
        Raylib.CloseWindow();
    }
}

// Texts on top of the 3D world, in full window resolution - always sharp, whatever the window size.
static void DrawHud(bool isMouseCaptured, string displayVersion)
{
    var screenWidth = Raylib.GetScreenWidth();
    var screenHeight = Raylib.GetScreenHeight();
    var textColor = new Color(200, 200, 210, 255);

    Raylib.DrawText("Silver & Ash", 20, 20, 30, textColor);
    Raylib.DrawFPS(20, screenHeight - 30);

    if (!isMouseCaptured)
    {
        const string Hint = "Click to look around";
        var hintWidth = Raylib.MeasureText(Hint, 24);
        Raylib.DrawText(Hint, (screenWidth - hintWidth) / 2, screenHeight / 2 + 40, 24, textColor);
    }

    var versionText = $"v{displayVersion}";
    var versionWidth = Raylib.MeasureText(versionText, 20);
    Raylib.DrawText(versionText, screenWidth - versionWidth - 20, screenHeight - 30, 20, new Color(120, 120, 130, 255));
}

// Stamina for perfect jumps: one segment per jump at the bottom centre. A segment fills up from left to right.
static void DrawStaminaBar(Player player)
{
    const int Segments = Tuning.Player.StaminaPerfectJumps;
    const int SegmentWidth = 48;
    const int Gap = 4;
    const int Height = 8;

    var totalWidth = Segments * SegmentWidth + (Segments - 1) * Gap;
    var left = (Raylib.GetScreenWidth() - totalWidth) / 2;
    var top = Raylib.GetScreenHeight() - 40;

    // Red background while exhausted: the player should see at once why they are slow.
    var background = player.IsExhausted ? new Color(110, 25, 30, 220) : new Color(40, 35, 45, 200);
    var fill = new Color(200, 205, 220, 255);

    for (var i = 0; i < Segments; i++)
    {
        var x = left + i * (SegmentWidth + Gap);
        var segmentFill = Math.Clamp(player.Stamina.Fraction * Segments - i, 0f, 1f);

        Raylib.DrawRectangle(x, top, SegmentWidth, Height, background);
        Raylib.DrawRectangle(x, top, (int)MathF.Round(SegmentWidth * segmentFill), Height, fill);
    }
}

#if DEBUG
// Test display, only in the debug build (dotnet run) - the released game does not show it.
static void DrawDebugInfo(Player player)
{
    var speedText = string.Create(CultureInfo.InvariantCulture, $"Speed {player.HorizontalSpeed:0.00} m/s");
    var bonusColor = new Color(230, 190, 90, 255);
    var exhaustedColor = new Color(220, 80, 80, 255);
    var color = player.HasPerfectJumpBonus ? bonusColor
        : player.IsExhausted ? exhaustedColor
        : new Color(200, 200, 210, 255);

    Raylib.DrawText(speedText, 20, 60, 20, color);
    if (player.HasPerfectJumpBonus)
    {
        Raylib.DrawText("PERFECT", 20, 85, 20, bonusColor);
    }
    else if (player.IsExhausted)
    {
        Raylib.DrawText("EXHAUSTED", 20, 85, 20, exhaustedColor);
    }
}
#endif
