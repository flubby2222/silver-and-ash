using System.Globalization;
using System.Numerics;
using System.Reflection;
using Raylib_cs;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;
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
            RunGame(level, displayVersion: version.Split('+')[0], log);
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

static void RunGame(Level level, string displayVersion, FileLog? log)
{
    // No MSAA (edge smoothing): the hard pixel edges of the low resolution are wanted.
    Raylib.SetConfigFlags(ConfigFlags.VSyncHint | ConfigFlags.ResizableWindow);
    Raylib.InitWindow(WindowWidth, WindowHeight, "Silver & Ash");
    BringWindowOnScreen(log);

    // Esc must not close the game any more - it releases the mouse (later: opens the pause menu).
    Raylib.SetExitKey(KeyboardKey.Null);

    try
    {
        // Balancing values from assets/balance.json - a broken file only costs warnings in the log, never the game.
        var balancePath = Path.Combine(AppContext.BaseDirectory, "assets", "balance.json");
        var balance = LoadBalance(balancePath, log);

        // The whole simulation: level, player, enemies. A new seed every game, so the bats fly differently each time;
        // it goes into the log, so a strange run can be repeated.
        var gameWorld = new GameWorld(level, Random.Shared.Next(), balance);
        log?.Info($"Seed: {gameWorld.Seed}.");
        var player = gameWorld.Player;
        IDummyView dummyView = new PlaceholderDummyView();
        IGhoulView ghoulView = new PlaceholderGhoulView();
        IBatView batView = new PlaceholderBatView();
        var projectileView = new ProjectileView();
        var pickupView = new PickupView();
        var impactMarks = new ImpactMarks();
        IWeaponView weaponView = new PlaceholderWeaponView();
        var damageFeedback = new DamageFeedback();
        var input = new RaylibInput();
        input.CaptureMouse();

#if DEBUG
        var reloadedNoticeSeconds = 0f;
#endif

        // A new world (restart or reloaded values) gets fresh views, so no flash, corpse or impact mark of the
        // old run is left over.
        void StartFresh(GameWorld world)
        {
            gameWorld = world;
            player = gameWorld.Player;
            dummyView = new PlaceholderDummyView();
            ghoulView = new PlaceholderGhoulView();
            batView = new PlaceholderBatView();
            projectileView = new ProjectileView();
            pickupView = new PickupView();
            impactMarks = new ImpactMarks();
            weaponView = new PlaceholderWeaponView();
            damageFeedback = new DamageFeedback();
        }

        // Choosing a level-up bonus: was the window open last frame, and which boon is waiting for the next tick?
        var wasChoosing = false;
        var choosingSeconds = 0f;
        Boon? chosenBoon = null;

        var accumulator = new TickAccumulator();
        using var picture = new LowResolutionTarget(LookSettings.RenderWidth, LookSettings.RenderHeight);

        // Lantern light and fog: a small program for the graphics card (assets/shaders/).
        using var levelShader = new LevelShader(Path.Combine(AppContext.BaseDirectory, "assets", "shaders"));

        // The level geometry is built once here and sent to the graphics card.
        using var levelRenderer = new LevelRenderer(level, levelShader.Shader);

        // Game loop: runs once per drawn frame until the window is closed (X button).
        while (!Raylib.WindowShouldClose())
        {
            // A level up waits for a bonus: show the cursor so the boxes can be clicked; capture it again afterwards.
            var choosing = player.PendingBoons > 0;
            if (choosing && !wasChoosing && input.IsMouseCaptured)
            {
                input.ReleaseMouse();
            }
            else if (!choosing && wasChoosing)
            {
                input.CaptureMouse();
            }

            wasChoosing = choosing;
            choosingSeconds = choosing ? choosingSeconds + Raylib.GetFrameTime() : 0f;

            // 1. Input: looking around happens every frame, so it feels immediate.
            var playerInput = input.Read(choosing);
            player.Look(playerInput);
            if (choosing)
            {
                chosenBoon ??= BoonWindow.Pick(player.CurrentOffer, choosingSeconds);
            }

            // 2. Simulation: as many fixed ticks as fit into the real time of the last frame.
            //    A chosen bonus is kept until a tick has taken it - not every frame has a tick.
            var ticks = accumulator.Advance(Raylib.GetFrameTime());
            for (var i = 0; i < ticks; i++)
            {
                gameWorld.Tick(playerInput with { ChooseBoon = chosenBoon });
                chosenBoon = null;

                // Events must be read after EVERY tick - the next tick clears them.
                foreach (var gameEvent in gameWorld.Events)
                {
                    dummyView.OnEvent(gameEvent, player.Position);
                    ghoulView.OnEvent(gameEvent);
                    batView.OnEvent(gameEvent);
                    projectileView.OnEvent(gameEvent);
                    impactMarks.OnEvent(gameEvent);
                    weaponView.OnEvent(gameEvent);
                    damageFeedback.OnEvent(gameEvent);
                }
            }

            // Dead long enough: start the level again with a fresh world.
            if (gameWorld.ShouldRestart)
            {
                StartFresh(gameWorld.Restarted());
                log?.Info($"Restart, seed: {gameWorld.Seed}.");
            }

#if DEBUG
            // Test key: F5 reads the balancing file again and starts the level with its values.
            if (Raylib.IsKeyPressed(KeyboardKey.F5))
            {
                StartFresh(new GameWorld(level, Random.Shared.Next(), LoadBalance(balancePath, log)));
                log?.Info($"Balancing reloaded, seed: {gameWorld.Seed}.");
                reloadedNoticeSeconds = 2f;
            }

            reloadedNoticeSeconds = MathF.Max(0f, reloadedNoticeSeconds - Raylib.GetFrameTime());
#endif

            dummyView.Update(Raylib.GetFrameTime());
            ghoulView.Update(Raylib.GetFrameTime());
            batView.Update(Raylib.GetFrameTime());
            projectileView.Update(Raylib.GetFrameTime());
            pickupView.Update(Raylib.GetFrameTime());
            impactMarks.Update(Raylib.GetFrameTime());
            weaponView.Update(Raylib.GetFrameTime(), player);
            damageFeedback.Update(Raylib.GetFrameTime());

            // 3. Rendering: draw the current state, blended between the last two ticks (no stutter
            //    on monitors faster than 60 Hz). No game rules in here.
            var eye = player.InterpolatedEyePosition(accumulator.Alpha);

            // Looking direction, tilted down a little right after landing (the "nod"), jolted when hit.
            var forward = player.View.ForwardTiltedDown(player.InterpolatedViewTilt(accumulator.Alpha));
            var jolt = damageFeedback.Jolt();
            forward += player.View.FlatRight * jolt.X + Vector3.UnitY * jolt.Y;
            var camera = new Camera3D
            {
                Position = eye,
                Target = eye + forward,
                Up = Vector3.UnitY,
                FovY = LookSettings.FieldOfViewDegrees,
                Projection = CameraProjection.Perspective,
            };

            // 3a. The 3D world goes into the small low-resolution picture ...
            levelShader.SetCameraPosition(eye);
            picture.BeginDrawing();
            Raylib.ClearBackground(LookSettings.BackgroundColor);
            Raylib.BeginMode3D(camera);
            levelRenderer.Draw();
            levelShader.BeginFigures();
            foreach (var dummy in gameWorld.Dummies)
            {
                dummyView.Draw(dummy);
            }

            foreach (var ghoul in gameWorld.Ghouls)
            {
                ghoulView.Draw(ghoul);
            }

            foreach (var bat in gameWorld.Bats)
            {
                batView.Draw(bat, eye);
            }

            projectileView.Draw(gameWorld.Projectiles, accumulator.Alpha);
            pickupView.Draw(gameWorld.Pickups, accumulator.Alpha);

            impactMarks.Draw();

            // Last: the weapons in the hands, in front of everything else.
            weaponView.Draw(camera.Position, camera.Target - camera.Position);

            levelShader.EndFigures();
            Raylib.EndMode3D();
            picture.EndDrawing();

            // 3b. ... which is scaled up onto the screen. The HUD is drawn afterwards at full resolution (sharp).
            Raylib.BeginDrawing();
            Raylib.ClearBackground(LookSettings.BarColor);
            picture.DrawToScreen();
            damageFeedback.Draw();
            DrawHud(input.IsMouseCaptured || choosing, displayVersion);
            DrawStaminaBar(player);
            CombatHud.Draw(player);
            if (choosing)
            {
                BoonWindow.Draw(player, gameWorld.Balance.Boons);
            }
#if DEBUG
            DrawDebugInfo(player);
            if (reloadedNoticeSeconds > 0f)
            {
                Raylib.DrawText("Balancing reloaded", 20, 160, 20, new Color(230, 190, 90, 255));
            }
#endif
            Raylib.EndDrawing();
        }
    }
    finally
    {
        Raylib.CloseWindow();
    }
}

// Started on a monitor left of the main monitor, Raylib/GLFW put the window far outside every monitor
// (found while testing v0.3.0). If that happens, move it to the middle of the main monitor.
static void BringWindowOnScreen(FileLog? log)
{
    var position = Raylib.GetWindowPosition();
    var window = new DesktopRect((int)position.X, (int)position.Y, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());

    // GLFW always lists the main monitor first.
    var monitors = Enumerable.Range(0, Raylib.GetMonitorCount())
        .Select(i =>
        {
            var corner = Raylib.GetMonitorPosition(i);
            return new DesktopRect((int)corner.X, (int)corner.Y, Raylib.GetMonitorWidth(i), Raylib.GetMonitorHeight(i));
        })
        .ToList();
    if (monitors.Count == 0)
    {
        return;
    }

    if (WindowPlacement.CorrectedPosition(window, monitors, monitors[0]) is var (x, y))
    {
        log?.Warning($"Window was off-screen at {window.X}, {window.Y} - moved to {x}, {y} on the main monitor.");
        Raylib.SetWindowPosition(x, y);
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

// Reads the balancing file; every problem with it goes into the log as a warning.
static Balance LoadBalance(string path, FileLog? log)
{
    var result = BalanceFile.Load(path);
    foreach (var warning in result.Warnings)
    {
        log?.Warning(warning);
    }

    log?.Info($"Balancing values loaded from '{path}' ({result.Warnings.Count} warnings).");
    return result.Balance;
}

// Stamina for jumps: one segment per jump at the bottom centre. A segment fills up from left to right.
static void DrawStaminaBar(Player player)
{
    var segments = player.Stamina.Segments;
    const int SegmentWidth = 48;
    const int Gap = 4;
    const int Height = 8;

    var totalWidth = segments * SegmentWidth + (segments - 1) * Gap;
    var left = (Raylib.GetScreenWidth() - totalWidth) / 2;
    var top = Raylib.GetScreenHeight() - 40;

    // Red background while exhausted: the player should see at once why they are slow.
    var background = player.IsExhausted ? new Color(110, 25, 30, 220) : new Color(40, 35, 45, 200);
    var fill = new Color(200, 205, 220, 255);

    for (var i = 0; i < segments; i++)
    {
        var x = left + i * (SegmentWidth + Gap);
        var segmentFill = Math.Clamp(player.Stamina.Fraction * segments - i, 0f, 1f);

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

    var chargeText = player.BladeCharge.IsCharging
        ? string.Create(CultureInfo.InvariantCulture, $"   Charge {player.BladeCharge.Fraction * 100f:0} %")
        : string.Empty;
    var weaponsText = $"Blade {CooldownText(player.BladeCooldown)}   Pistol {CooldownText(player.PistolCooldown)}{chargeText}";
    Raylib.DrawText(weaponsText, 20, 135, 20, new Color(200, 200, 210, 255));

    if (player.IsDashing)
    {
        Raylib.DrawText("DASH", 20, 110, 20, new Color(140, 200, 230, 255));
    }
    else if (player.IsDodging)
    {
        Raylib.DrawText("DODGE", 20, 110, 20, new Color(140, 230, 160, 255));
    }
}

// "ready" or the seconds left, e.g. "0.25 s".
static string CooldownText(Cooldown cooldown) =>
    cooldown.IsReady
        ? "ready"
        : string.Create(CultureInfo.InvariantCulture, $"{cooldown.RemainingSeconds:0.00} s");
#endif
