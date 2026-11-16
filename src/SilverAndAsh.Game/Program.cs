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

// Seconds between clearing the boss room and the victory screen.
const float VictoryScreenDelay = 2f;

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

    // Load the level (or the room templates) before opening the window: if it is broken, no window flashes up.
    // Without options the game plays a generated level 0 - every run a new layout. A fixed one: -- --seed 123
    // The old hand-made levels: dotnet run --project src/SilverAndAsh.Game -- --level test (or heights)
    Func<int, Balance, GameWorld>? newWorld = null;
    Func<GameWorld, GameWorld>? nextWorld = null;
    int? seed = null;
    try
    {
        var options = LaunchOptions.Parse(args);
        seed = options.Seed;
        if (options.IsGenerated)
        {
            var templates = RoomTemplateParser.LoadAll(Path.Combine(AppContext.BaseDirectory, "assets", "rooms"));
            var levelZero = new LevelZero(templates);
            newWorld = levelZero.NewRun;
            nextWorld = levelZero.NextRun;
            log?.Info($"Level 0 from {templates.Count} room templates.");
        }
        else
        {
            var levelPath = options.LevelPath(Path.Combine(AppContext.BaseDirectory, "assets", "levels"));
            var level = LevelParser.ParseFile(levelPath);
            newWorld = (worldSeed, balance) => new GameWorld(level, worldSeed, balance);
            nextWorld = world => world.Restarted();
            log?.Info($"Level '{levelPath}' loaded: {level.Width} x {level.Depth} cells.");
        }
    }
    catch (Exception exception) when (exception is ArgumentException or LevelFormatException or IOException or UnauthorizedAccessException or InvalidOperationException)
    {
        log?.Error("Could not load the level.", exception);
        Console.Error.WriteLine($"Could not load the level: {exception.Message}");
        Environment.ExitCode = 1;
    }

    if (newWorld is not null && nextWorld is not null)
    {
        try
        {
            // Shown in the corner without the commit id ("0.1.0+abc123..." -> "0.1.0").
            RunGame(newWorld, nextWorld, seed, displayVersion: version.Split('+')[0], log);
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

// newWorld builds a world from a seed and balancing values; nextWorld the one after a run ended (new layout on level 0).
static void RunGame(
    Func<int, Balance, GameWorld> newWorld, Func<GameWorld, GameWorld> nextWorld, int? seed, string displayVersion, FileLog? log)
{
    // No MSAA (edge smoothing): the hard pixel edges of the low resolution are wanted.
    Raylib.SetConfigFlags(ConfigFlags.VSyncHint | ConfigFlags.ResizableWindow);
    Raylib.InitWindow(WindowWidth, WindowHeight, "Silver & Ash");
    BringWindowOnScreen(log);

    // Esc must not close the game - it opens the Esc menu.
    Raylib.SetExitKey(KeyboardKey.Null);

    try
    {
        // Balancing values from assets/balance.json - a broken file only costs warnings in the log, never the game.
        var balancePath = Path.Combine(AppContext.BaseDirectory, "assets", "balance.json");
        var balance = LoadBalance(balancePath, log);

        // The player's own settings (mouse sensitivity), kept locally: %LOCALAPPDATA%\SilverAndAsh\settings.json.
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SilverAndAsh", "settings.json");
        var settings = SettingsFile.Load(settingsPath);

        // One line per finished run, also local: %LOCALAPPDATA%\SilverAndAsh\runs.log.
        var runLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SilverAndAsh", "runs.log");

        // The whole simulation: level, player, enemies. "Start" on the title screen begins a run with a new seed
        // (unless --seed is given), so every run looks different; it goes into the log, so a strange run can be
        // repeated. Until then this world only stands in.
        var gameWorld = newWorld(seed ?? Random.Shared.Next(), balance);
        var player = gameWorld.Player;
        IDummyView dummyView = new PlaceholderDummyView();
        IGhoulView ghoulView = new PlaceholderGhoulView();
        var gormView = new GormView();
        IBatView batView = new PlaceholderBatView();
        var projectileView = new ProjectileView();
        var pickupView = new PickupView();
        var impactMarks = new ImpactMarks();
        IWeaponView weaponView = new PlaceholderWeaponView();
        var damageFeedback = new DamageFeedback();
        var input = new RaylibInput();

        // Title screen first; during a run Esc opens the menu (the world stands still - alone).
        var onTitle = true;
        var menuOpen = false;
        var quit = false;
        var titleMenu = new MenuView();
        titleMenu.Open();
        var escMenu = new MenuView();

#if DEBUG
        var reloadedNoticeSeconds = 0f;
#endif

        // The level name ("THE CATACOMBS") shows for a few seconds whenever a run starts.
        var levelNameSeconds = 0f;

        // The end of a run: its numbers while "YOU DIED" / "VICTORY" is shown (null while the run is on), and how long
        // the victory is already there (the screen waits a moment, so the rising grates can be seen).
        RunSummary? endSummary = null;
        var wonSeconds = 0f;
        var endMenu = new MenuView();

        // A new world (restart or reloaded values) gets fresh views, so no flash, corpse or impact mark of the
        // old run is left over.
        void StartFresh(GameWorld world)
        {
            gameWorld = world;
            levelNameSeconds = gameWorld.LevelName is null ? 0f : LevelBanner.Duration;
            endSummary = null;
            wonSeconds = 0f;
            LogRun(gameWorld, log);
            player = gameWorld.Player;
            dummyView = new PlaceholderDummyView();
            ghoulView = new PlaceholderGhoulView();
            gormView = new GormView();
            batView = new PlaceholderBatView();
            projectileView = new ProjectileView();
            pickupView = new PickupView();
            impactMarks = new ImpactMarks();
            weaponView = new PlaceholderWeaponView();
            damageFeedback = new DamageFeedback();
        }

        // Choosing a level-up bonus: how long the window is open, and which boon is waiting for the next tick?
        var choosingSeconds = 0f;
        Boon? chosenBoon = null;

        // F pressed in a frame without a tick must not get lost: it waits for the next tick, like a chosen bonus.
        var interactPressed = false;
        var seconds = 0f;

        var accumulator = new TickAccumulator();
        using var picture = new LowResolutionTarget(LookSettings.RenderWidth, LookSettings.RenderHeight);

        // Lantern light and fog: a small program for the graphics card (assets/shaders/).
        using var levelShader = new LevelShader(Path.Combine(AppContext.BaseDirectory, "assets", "shaders"));

        // The level geometry is built here and sent to the graphics card - again only when a run brings a new layout.
        using var levelRenderer = new LevelRenderer(gameWorld.LevelAsBuilt, levelShader.Shader);
        var renderedLevel = gameWorld.LevelAsBuilt;

        // Game loop: runs once per drawn frame until the window is closed (X button) or "Quit" is chosen.
        while (!Raylib.WindowShouldClose() && !quit)
        {
            seconds += Raylib.GetFrameTime();

            // Title screen: no world runs, the mouse is free.
            if (onTitle)
            {
                if (input.IsMouseCaptured)
                {
                    input.ReleaseMouse();
                }

                switch (TitleScreen.Update(titleMenu))
                {
                    case TitleScreen.Start:
                        StartFresh(newWorld(seed ?? Random.Shared.Next(), balance));
                        onTitle = false;
                        menuOpen = false;
                        break;
                    case TitleScreen.Quit:
                        quit = true;
                        break;
                }

                Raylib.BeginDrawing();
                TitleScreen.Draw(titleMenu, displayVersion);
                Raylib.EndDrawing();
                continue;
            }

            // Esc opens and closes the menu; leaving the window (Alt+Tab) opens it, too. Not on the end screen.
            var wasMenuOpen = menuOpen;
            if (endSummary is not null)
            {
                menuOpen = false;
            }
            else if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                menuOpen = !menuOpen;
            }
            else if (!Raylib.IsWindowFocused())
            {
                menuOpen = true;
            }

            if (menuOpen && !wasMenuOpen)
            {
                escMenu.Open();
            }

            if (menuOpen)
            {
                switch (EscMenu.Update(escMenu, settings, out var changed))
                {
                    case EscMenu.Resume:
                        menuOpen = false;
                        break;
                    case EscMenu.Title:
                        onTitle = true;
                        menuOpen = false;
                        titleMenu.Open();
                        break;
                    case EscMenu.Quit:
                        quit = true;
                        break;
                }

                if (changed != settings)
                {
                    settings = changed;
                    if (!SettingsFile.Save(settings, settingsPath))
                    {
                        log?.Warning($"Could not save the settings to '{settingsPath}'.");
                    }
                }
            }

            if (endSummary is { } summary)
            {
                switch (EndScreen.Update(endMenu, summary))
                {
                    case EndScreen.NewRun:
                        StartFresh(nextWorld(gameWorld));
                        break;
                    case EndScreen.Title:
                        onTitle = true;
                        endSummary = null;
                        titleMenu.Open();
                        break;
                }
            }

            // The mouse is captured only while playing: free in the menu, on the end screen and while a bonus is chosen.
            var choosing = player.PendingBoons > 0;
            var frozen = menuOpen || endSummary is not null;
            var capture = !frozen && !onTitle && !choosing;
            if (capture && !input.IsMouseCaptured)
            {
                input.CaptureMouse();
            }
            else if (!capture && input.IsMouseCaptured)
            {
                input.ReleaseMouse();
            }

            choosingSeconds = choosing && !frozen ? choosingSeconds + Raylib.GetFrameTime() : 0f;

            // 1. Input: looking around happens every frame, so it feels immediate.
            var playerInput = input.Read();
            player.Look(playerInput, settings.MouseSensitivity);
            interactPressed |= playerInput.Interact;
            if (choosing && !frozen)
            {
                chosenBoon ??= BoonWindow.Pick(player.CurrentOffer, choosingSeconds);
            }

            // 2. Simulation: as many fixed ticks as fit into the real time of the last frame.
            //    A chosen bonus is kept until a tick has taken it - not every frame has a tick.
            var ticks = accumulator.Advance(Raylib.GetFrameTime());
            for (var i = 0; i < ticks; i++)
            {
                gameWorld.Tick(playerInput with { ChooseBoon = chosenBoon, Interact = interactPressed, Paused = frozen });
                chosenBoon = null;
                interactPressed = false;

                // Events must be read after EVERY tick - the next tick clears them.
                foreach (var gameEvent in gameWorld.Events)
                {
                    dummyView.OnEvent(gameEvent, player.Position);
                    ghoulView.OnEvent(gameEvent);
                    gormView.OnEvent(gameEvent);
                    batView.OnEvent(gameEvent);
                    projectileView.OnEvent(gameEvent);
                    impactMarks.OnEvent(gameEvent);
                    weaponView.OnEvent(gameEvent);
                    damageFeedback.OnEvent(gameEvent);
                }
            }

            // Dead long enough, or won a moment ago: the end screen with the numbers of the run. They also go into
            // the local run log (one line) and the technical log. The world hands its summary out only once, so
            // going back to the title (which clears the end screen) never logs the same run again.
            wonSeconds = gameWorld.IsWon ? wonSeconds + Raylib.GetFrameTime() : 0f;
            if (endSummary is null && (gameWorld.ShouldRestart || wonSeconds >= VictoryScreenDelay)
                && gameWorld.TakeEndSummary() is { } finished)
            {
                endSummary = finished;
                var line = endSummary.ToLogLine(DateTimeOffset.Now);
                log?.Info($"Run ended: {line}");
                if (!RunLog.Append(runLogPath, line))
                {
                    log?.Warning($"Could not write the run log '{runLogPath}'.");
                }

                endMenu.Open();
            }

#if DEBUG
            // Test key: F5 reads the balancing file again and starts the level with its values.
            if (Raylib.IsKeyPressed(KeyboardKey.F5))
            {
                // The same seed: the same layout again (unless the layout values changed), now with the new values.
                StartFresh(newWorld(gameWorld.Seed, LoadBalance(balancePath, log)));
                log?.Info("Balancing reloaded.");
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
            if (!ReferenceEquals(renderedLevel, gameWorld.LevelAsBuilt))
            {
                levelRenderer.Rebuild(gameWorld.LevelAsBuilt);
                renderedLevel = gameWorld.LevelAsBuilt;
            }

            levelRenderer.Draw();
            levelShader.BeginFigures();
            DoorView.Draw(gameWorld.Doors, seconds);
            ChestView.Draw(gameWorld.Chests, seconds, camera.Position);
            StairsView.Draw(gameWorld.Stairs, seconds);
            foreach (var dummy in gameWorld.Dummies)
            {
                dummyView.Draw(dummy);
            }

            foreach (var ghoul in gameWorld.Ghouls)
            {
                ghoulView.Draw(ghoul);
            }

            if (gameWorld.Gorm is { } gorm)
            {
                gormView.Draw(gorm, Raylib.GetFrameTime());
                gormView.DrawEffects(gorm);
            }

            foreach (var bat in gameWorld.Bats)
            {
                batView.Draw(bat, eye);
            }

            projectileView.Draw(gameWorld.Projectiles, accumulator.Alpha);
            pickupView.Draw(gameWorld.Pickups, accumulator.Alpha);
            pickupView.DrawLoot(gameWorld.Loot, accumulator.Alpha);

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
            if (endSummary is null)
            {
                damageFeedback.Draw();
            }

            DrawHud(displayVersion);
            DrawStaminaBar(player);
            CombatHud.Draw(player);
            BossBar.Draw(gameWorld);
            if (!choosing && !frozen)
            {
                InteractionPrompt.Draw(gameWorld.InteractionTarget(player), player);
            }

            levelNameSeconds = MathF.Max(0f, levelNameSeconds - Raylib.GetFrameTime());
            if (!frozen)
            {
                LevelBanner.Draw(gameWorld.LevelName, levelNameSeconds);
            }

            // The map while M is held - the game goes on meanwhile (no pause: online co-op).
            if (!frozen && !choosing && Raylib.IsKeyDown(KeyboardKey.M))
            {
                MapView.Draw(gameWorld, player);
            }

            if (choosing)
            {
                BoonWindow.Draw(player, gameWorld.Balance.Boons);
            }

            if (menuOpen)
            {
                EscMenu.Draw(escMenu, settings);
            }

            if (endSummary is not null)
            {
                EndScreen.Draw(endMenu, endSummary);
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
static void DrawHud(string displayVersion)
{
    var screenWidth = Raylib.GetScreenWidth();
    var screenHeight = Raylib.GetScreenHeight();
    var textColor = new Color(200, 200, 210, 255);

    Raylib.DrawText("Silver & Ash", 20, 20, 30, textColor);
    Raylib.DrawFPS(20, screenHeight - 30);

    var versionText = $"v{displayVersion}";
    var versionWidth = Raylib.MeasureText(versionText, 20);
    Raylib.DrawText(versionText, screenWidth - versionWidth - 20, screenHeight - 30, 20, new Color(120, 120, 130, 255));
}

// Reads the balancing file; every problem with it goes into the log as a warning.
static void LogRun(GameWorld world, FileLog? log) =>
    log?.Info($"New run, seed {world.Seed}: {world.Rooms.Count} rooms, {world.Doors.Count} doors, "
        + $"{world.Ghouls.Count} ghouls, {world.Bats.Count} bats, {world.Level.Width} x {world.Level.Depth} cells.");

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
