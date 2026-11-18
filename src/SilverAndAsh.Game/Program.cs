using System.Globalization;
using System.Numerics;
using System.Reflection;
using Raylib_cs;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Diagnostics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.Showroom;
using SilverAndAsh.Core.World;
using SilverAndAsh.Game.Audio;
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
    int? look = null;
    PhotoRequest? photos = null;
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

#if DEBUG
        // Test option: --give club - every run (and restart) starts with these weapons, to try them before they can
        // be found. Only in the debug version.
        if (options.Give is { Count: > 0 } give)
        {
            var plainNew = newWorld;
            var plainNext = nextWorld;
            newWorld = (worldSeed, balance) => WithGivenWeapons(plainNew(worldSeed, balance), give);
            nextWorld = world => WithGivenWeapons(plainNext(world), give);
            log?.Info($"Every run starts with: {string.Join(", ", give)}.");
        }

        // Test option: --showroom - instead of a run, the showroom: every door, chest, enemy and loot side by side in
        // every wall variant, for designers and code sessions (issue #283, docs/musterraum.md). Only in the debug
        // version, like --give: the player there cannot die and has every weapon.
        if (options.Showroom)
        {
            var showroom = ShowroomLayout.Build();
            newWorld = (_, balance) => showroom.NewWorld(balance);
            nextWorld = world => showroom.NewWorld(world.Balance);
            log?.Info($"Showroom: {showroom.Placed.Count} pieces in {showroom.Sections.Count} sections.");
        }

        // Test option: --look 3 draws the world in one of the picture variants of issue #234 (decision record 0010).
        look = options.Look;
        if (look is not null)
        {
            log?.Info($"Picture variant {look}.");
        }

        // Test option: --photo <scene> takes fixed pictures for testers and designers and quits (issue #224,
        // docs/foto-skript.md). Only in the debug version, like --give.
        if (options.Photo is { } photo)
        {
            if (photo == LaunchOptions.ListPhotos)
            {
                foreach (var scene in PhotoScenes.All)
                {
                    Console.WriteLine($"{scene.Name,-16} {scene.Description}");
                }

                newWorld = null;
            }
            else
            {
                var folder = Path.GetFullPath(options.PhotoFolder ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SilverAndAsh", "photos"));
                var compare = photo == LaunchOptions.ComparePhotos;
                var scenes = photo == LaunchOptions.AllPhotos ? PhotoScenes.All
                    : compare ? PhotoScenes.LookComparison
                    : [PhotoScenes.Find(photo)!];
                photos = new PhotoRequest(
                    scenes, folder, RoomTemplateParser.LoadAll(Path.Combine(AppContext.BaseDirectory, "assets", "rooms")), compare);
                log?.Info($"Photo mode: {scenes.Count} scenes into '{folder}'.");
            }
        }

        // Test option: --clip <scene> films a motion scene into numbered pictures, one folder per scene, and quits
        // (issue #286, docs/foto-skript.md); tools/clip.ps1 puts them together. Only in the debug version, like --photo.
        if (options.Clip is { } clip)
        {
            if (clip == LaunchOptions.ListPhotos)
            {
                foreach (var scene in MotionScenes.All)
                {
                    Console.WriteLine($"{scene.Name,-20} {scene.FramesPerSecond,3:0} fps  {scene.Description}");
                }

                newWorld = null;
            }
            else
            {
                var folder = Path.GetFullPath(options.PhotoFolder ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SilverAndAsh", "clips"));
                IReadOnlyList<MotionScene> clips = clip == LaunchOptions.AllPhotos ? MotionScenes.All : [MotionScenes.Find(clip)!];
                photos = new PhotoRequest(
                    [], folder, RoomTemplateParser.LoadAll(Path.Combine(AppContext.BaseDirectory, "assets", "rooms")), Compare: false)
                {
                    Clips = clips,
                };
                log?.Info($"Clip mode: {clips.Count} motion scenes into '{folder}'.");
            }
        }

        // Test option: --bench <scene> measures how long a frame of this photo scene takes, without VSync (issue #233,
        // docs/foto-skript.md). Only in the debug version, like --photo.
        if (options.Bench is { } bench)
        {
            photos = new PhotoRequest(
                [PhotoScenes.Find(bench)!], Folder: string.Empty,
                RoomTemplateParser.LoadAll(Path.Combine(AppContext.BaseDirectory, "assets", "rooms")), Compare: false, Bench: true);
            log?.Info($"Bench mode: scene '{bench}', {PhotoRequest.WarmUpFrames} frames to warm up, {PhotoRequest.MeasuredFrames} measured.");
        }
#else
        if (options.Showroom)
        {
            throw new ArgumentException("'--showroom' works only in the debug version (dotnet run).");
        }

        if (options.Bench is not null)
        {
            throw new ArgumentException("'--bench' works only in the debug version (dotnet run).");
        }

        if (options.Photo is not null)
        {
            throw new ArgumentException("'--photo' works only in the debug version (dotnet run).");
        }

        if (options.Clip is not null)
        {
            throw new ArgumentException("'--clip' works only in the debug version (dotnet run).");
        }

        if (options.Look is not null)
        {
            throw new ArgumentException("'--look' works only in the debug version (dotnet run).");
        }
#endif
    }
    catch (Exception exception) when (exception is ArgumentException or LevelFormatException or IOException or UnauthorizedAccessException or InvalidOperationException)
    {
        log?.Error("Could not load the level.", exception);
        Console.Error.WriteLine($"Could not load the level: {exception.Message}");
        Environment.ExitCode = 1;

        // No game after a failed start - also when the level was already loaded (e.g. '--photo' in the release version).
        newWorld = null;
    }

    if (newWorld is not null && nextWorld is not null)
    {
        try
        {
            // Shown in the corner without the commit id ("0.1.0+abc123..." -> "0.1.0").
            RunGame(newWorld, nextWorld, seed, displayVersion: version.Split('+')[0], log, photos, LookVariants.Get(look));
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
// look: how the 3D world is drawn - the standard variant 2 (issue #237) unless --look chose another (debug version, issue #234).
static void RunGame(
    Func<int, Balance, GameWorld> newWorld, Func<GameWorld, GameWorld> nextWorld, int? seed, string displayVersion, FileLog? log,
    PhotoRequest? photos, LookVariant look)
{
    // No MSAA (edge smoothing): the hard pixel edges of the low resolution are wanted. The bench (--bench) draws as
    // fast as it can: no VSync, and no frame rate limit is set anywhere.
    Raylib.SetConfigFlags(photos is { Bench: true } ? ConfigFlags.ResizableWindow : ConfigFlags.VSyncHint | ConfigFlags.ResizableWindow);
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

        // Which sound the events of a tick ask for (issue #267). Its random choice of variants starts from the world's seed.
        var soundCues = new SoundCues(gameWorld.Seed);
        IDummyView dummyView = new PlaceholderDummyView();
        // Ghouls: the designer's model once it is loaded (after the shader, below), the placeholder until then - and for
        // good if the model is missing or broken.
        Func<IGhoulView> newGhoulView = () => new PlaceholderGhoulView();
        IGhoulView ghoulView = newGhoulView();
        // Gorm likewise: the model once loaded (below), the placeholder until then or for good.
        Func<IGormView> newGormView = () => new PlaceholderGormView();
        IGormView gormView = newGormView();
        // Bats likewise: the model once loaded (below), the placeholder until then or for good.
        Func<IBatView> newBatView = () => new PlaceholderBatView();
        IBatView batView = newBatView();
        var projectileView = new ProjectileView();
        var pickupView = new PickupView();
        var impactMarks = new ImpactMarks();
        var wallBreakView = new WallBreakView();
        // Weapons: the designer's models once they are loaded (after the shader, below), boxes until then.
        Func<IWeaponView> newWeaponView = () => new WeaponView();
        IWeaponView weaponView = newWeaponView();
        var damageFeedback = new DamageFeedback();
        var equipmentCard = new EquipmentCard();
        var input = new RaylibInput();

        // Title screen first; during a run Esc opens the menu (the world stands still - alone).
        var onTitle = true;
        var menuOpen = false;
        var mapKeptOpen = false;
        var quit = false;
        var titleMenu = new MenuView();
        titleMenu.Open();
        var escMenu = new MenuView();

#if DEBUG
        var reloadedNoticeSeconds = 0f;
#endif

        // The level name ("THE CATACOMBS") shows for a few seconds whenever a run starts.
        var levelNameSeconds = 0f;

        // The showroom (--showroom, issue #283): the labels of its pieces, built once it is shown; L hides and shows them.
        // The camera of the last 3D picture places them on the screen.
        IReadOnlyList<ShowroomLabel>? showroomLabels = null;
        var showroomLabelsShown = true;
        var lastCamera = default(Camera3D);

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
            soundCues = new SoundCues(gameWorld.Seed);
            dummyView = new PlaceholderDummyView();
            ghoulView = newGhoulView();
            gormView = newGormView();
            batView = newBatView();
            projectileView = new ProjectileView();
            pickupView = new PickupView();
            impactMarks = new ImpactMarks();
            wallBreakView = new WallBreakView();
            weaponView = newWeaponView();
            damageFeedback = new DamageFeedback();
            equipmentCard = new EquipmentCard();
        }

        // Choosing a level-up bonus: is the window open (X), and which boon is waiting for the next tick?
        var boonOpen = false;
        var boonOpenSeconds = 0f;
        Boon? chosenBoon = null;

        // The inventory (G): Core keeps whether it is open; G and a click wait for the next tick, like a chosen bonus.
        var inventoryToggle = false;
        var inventoryOpenSeconds = 0f;
        InventoryClick? inventoryClick = null;

        // F pressed in a frame without a tick must not get lost: it waits for the next tick, like a chosen bonus.
        // So do 1 / 2 / 3 and the mouse wheel (switching the ranged weapon).
        var interactPressed = false;
        int? slotChosen = null;
        var wheelSteps = 0;
        var seconds = 0f;

        var accumulator = new TickAccumulator();
        using var picture = new LowResolutionTarget(look.RenderWidth, look.RenderHeight);

        // Lantern light and fog: a small program for the graphics card (assets/shaders/), for everything in the 3D world.
        using var levelShader = new LevelShader(Path.Combine(AppContext.BaseDirectory, "assets", "shaders"));
        levelShader.Apply(look);

        // The models of the designers (assets/models/, docs/modelle.md). Missing or broken: a warning in the log and
        // the placeholder - the game goes on.
        using var ghoulModel = TexturedModel.TryLoad(
            Path.Combine(AppContext.BaseDirectory, "assets", "models", "ghoul.glb"), GhoulPose.Parts, log);
        if (ghoulModel is not null)
        {
            newGhoulView = () => new ModelGhoulView(ghoulModel, levelShader);
            ghoulView = newGhoulView();
        }

        using var batModel = TexturedModel.TryLoad(
            Path.Combine(AppContext.BaseDirectory, "assets", "models", "bat.glb"), BatPose.Parts, log);
        if (batModel is not null)
        {
            newBatView = () => new ModelBatView(batModel, levelShader);
            batView = newBatView();
        }

        using var gormModel = TexturedModel.TryLoad(
            Path.Combine(AppContext.BaseDirectory, "assets", "models", "gorm.glb"), GormPose.Parts, log,
            [ModelGormView.Phase2Texture]);
        if (gormModel is not null)
        {
            newGormView = () => new ModelGormView(gormModel, levelShader);
            gormView = newGormView();
        }

        // The shock wave of Gorm's hammer as breaking stone (issue #299); without its model the plain ring.
        using var shockwaveView = ShockwaveView.Load(Path.Combine(AppContext.BaseDirectory, "assets", "models"), log);

        // One door model per kind (issue #245); a kind without one keeps the placeholder boxes.
        using var doorView = DoorView.Load(Path.Combine(AppContext.BaseDirectory, "assets", "models"), log);

        // One chest model per tier (issue #249); a tier without one keeps the placeholder boxes.
        using var chestView = ChestView.Load(Path.Combine(AppContext.BaseDirectory, "assets", "models"), log);

        // The spiral staircase of every wall variant (issue #300); without one the placeholder boxes.
        using var stairsView = StairsView.Load(Path.Combine(AppContext.BaseDirectory, "assets", "models"), log);

        // Pistol and silver blade with their hands (issue #249); a weapon without its two files keeps the boxes.
        using var weaponModels = WeaponModels.Load(Path.Combine(AppContext.BaseDirectory, "assets", "models"), log);
        newWeaponView = () => new WeaponView(weaponModels, levelShader);
        weaponView = newWeaponView();

        // The level geometry is built here and sent to the graphics card - again only when a run brings a new layout.
        // The stones of walls, floors and ceilings (assets/textures/, issue #233). Only level 0, the catacombs, exists yet.
        // Missing or broken: a warning in the log and plain surfaces as before. Each room shows its own variant of the
        // three (issue #244); a missing variant falls back to the first.
        using var levelTextures = LevelTextures.Load(
            Path.Combine(AppContext.BaseDirectory, "assets", "textures"), LevelTextureSet.For(LevelLayer.Catacombs), log);
        using var levelRenderer = new LevelRenderer(gameWorld.LevelAsBuilt, gameWorld.Surfaces, levelShader.Shader, levelTextures);
        var renderedLevel = gameWorld.LevelAsBuilt;
        var renderedSurfaces = gameWorld.Surfaces;

        // Events must be read after EVERY tick - the next tick clears them.
        void ReadEvents()
        {
            foreach (var gameEvent in gameWorld.Events)
            {
                dummyView.OnEvent(gameEvent, player.Position);
                ghoulView.OnEvent(gameEvent);
                gormView.OnEvent(gameEvent);
                batView.OnEvent(gameEvent);
                projectileView.OnEvent(gameEvent);
                impactMarks.OnEvent(gameEvent);
                wallBreakView.OnEvent(gameEvent, gameWorld.Doors, player.Position, gameWorld.Surfaces);
                weaponView.OnEvent(gameEvent);
                damageFeedback.OnEvent(gameEvent);
                equipmentCard.OnEvent(gameEvent, player.Id);
                if (gameEvent is PlayerHit hit && hit.PlayerId == player.Id)
                {
                    boonOpen = false;
                }
            }
        }

        void UpdateViews(float frameSeconds)
        {
            dummyView.Update(frameSeconds);
            ghoulView.Update(frameSeconds);
            batView.Update(frameSeconds);
            projectileView.Update(frameSeconds);
            pickupView.Update(frameSeconds);
            impactMarks.Update(frameSeconds);
            wallBreakView.Update(frameSeconds);
            weaponView.Update(frameSeconds, player);
            damageFeedback.Update(frameSeconds);
            equipmentCard.Update(frameSeconds);
        }

        // 3a. The 3D world goes into the small low-resolution picture, blended between the last two ticks by alpha.
        void DrawWorld(float alpha, float frameSeconds)
        {
            var eye = player.InterpolatedEyePosition(alpha);

            // Looking direction, tilted down a little right after landing (the "nod"), jolted when hit.
            var forward = player.View.ForwardTiltedDown(player.InterpolatedViewTilt(alpha));
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
            lastCamera = camera;

            levelShader.SetCameraPosition(eye);
            picture.BeginDrawing();
            Raylib.ClearBackground(look.BackgroundColor);
            Raylib.BeginMode3D(camera);
            if (!ReferenceEquals(renderedLevel, gameWorld.LevelAsBuilt) || !ReferenceEquals(renderedSurfaces, gameWorld.Surfaces))
            {
                levelRenderer.Rebuild(gameWorld.LevelAsBuilt, gameWorld.Surfaces);
                renderedLevel = gameWorld.LevelAsBuilt;
                renderedSurfaces = gameWorld.Surfaces;
            }

            levelRenderer.Draw();
            levelShader.BeginFigures();
            var viewCone = new ViewCone(eye, forward, LookSettings.FieldOfViewDegrees, (float)look.RenderWidth / look.RenderHeight);
            doorView.Draw(gameWorld.Doors, seconds, viewCone, gameWorld.Surfaces, levelTextures);
            chestView.Draw(gameWorld.Chests, seconds);
            stairsView.Draw(gameWorld.Stairs, seconds, viewCone, gameWorld.Surfaces, levelTextures);
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
                gormView.Draw(gorm, frameSeconds);
                gormView.DrawEffects(gorm);
                shockwaveView.Draw(gorm);
            }

            foreach (var bat in gameWorld.Bats)
            {
                batView.Draw(bat, eye);
            }

            projectileView.Draw(gameWorld.Projectiles, alpha);
            pickupView.Draw(gameWorld.Pickups, alpha);
            pickupView.DrawLoot(gameWorld.Loot, alpha);
            pickupView.DrawEquipment(gameWorld.EquipmentDrops, alpha);

            impactMarks.Draw();
            wallBreakView.Draw(doorView);

            // Last: the weapons in the hands, in front of everything else.
            weaponView.Draw(camera.Position, camera.Target - camera.Position);

            levelShader.EndFigures();
            Raylib.EndMode3D();
            picture.EndDrawing();
        }

        // 3b. The small picture scaled up onto the screen, the HUD on top at full resolution (sharp). Between
        // Raylib.BeginDrawing and EndDrawing.
        // debugInfo: the frame rate and the test display, both only in the debug version (issue #340) - not in photos.
        void DrawScreen(float frameSeconds, bool frozen, bool showMap, bool mapKeptOpen, bool debugInfo)
        {
            Raylib.ClearBackground(LookSettings.BarColor);
            picture.DrawToScreen();
            if (endSummary is null)
            {
                damageFeedback.Draw();
            }

            if (gameWorld.IsShowroom && showroomLabelsShown && endSummary is null && !frozen)
            {
                showroomLabels ??= ShowroomLayout.Build().Labels;
                ShowroomLabelView.Draw(showroomLabels, gameWorld.Level, lastCamera, picture.Width, picture.Height);
            }

            DrawHud(displayVersion, fps: debugInfo);
            DrawStaminaBar(player);
            CombatHud.Draw(player);
            BossBar.Draw(gameWorld);
            if (!frozen)
            {
                equipmentCard.Draw();
            }

            if (!boonOpen && !player.IsInventoryOpen && !frozen)
            {
                InteractionPrompt.Draw(gameWorld.InteractionTarget(player), player);
            }

            levelNameSeconds = MathF.Max(0f, levelNameSeconds - frameSeconds);
            if (!frozen)
            {
                LevelBanner.Draw(gameWorld.LevelName, levelNameSeconds);
            }

            if (!frozen && !boonOpen && !player.IsInventoryOpen && showMap)
            {
                MapView.Draw(gameWorld, player, mapKeptOpen, (float)Raylib.GetTime());
            }

            if (player.IsInventoryOpen && !frozen)
            {
                InventoryWindow.Draw(player);
            }
            else if (boonOpen && !frozen)
            {
                BoonWindow.Draw(player, gameWorld.Balance.Boons);
            }
            else if (!frozen)
            {
                BoonWindow.DrawHint(player.PendingBoons, seconds);
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
            if (debugInfo)
            {
                DrawDebugInfo(player);
                if (reloadedNoticeSeconds > 0f)
                {
                    Raylib.DrawText("Balancing reloaded", 20, 160, 20, new Color(230, 190, 90, 255));
                }
            }
#endif
        }

        // Photo mode (--photo, issue #224): every scene is built in Core, ticked like the game ticks, drawn with the
        // same code as above and saved twice - the small picture and the whole window with HUD. Then the game ends.
        // With --photo compare every scene is taken in every picture variant, then put together 2 x 2 (issue #234).
        if (photos is not null)
        {
            var studio = new PhotoStudio(photos.Templates, balance);
            if (!photos.Bench)
            {
                Directory.CreateDirectory(photos.Folder);
            }
            IReadOnlyList<LookVariant> looks = photos.Compare ? LookVariants.All : [look];
            foreach (var (variant, scene) in looks.SelectMany(variant => photos.Scenes.Select(scene => (variant, scene))))
            {
                if (variant != look)
                {
                    look = variant;
                    levelShader.Apply(look);
                    picture.Resize(look.RenderWidth, look.RenderHeight);
                }

                var suffix = photos.Compare ? $"-look{look.Number}" : string.Empty;
                var setup = scene.Build(studio);
                StartFresh(setup.World);
                if (setup.IsModelProbe)
                {
                    // The single target of the model probe: the loaded model, as in the game.
                    ghoulView = newGhoulView();
                }

                // One frame per tick: views and banners age exactly as in a game running at 60 frames per second.
                var ticks = setup.Run(afterTick: () =>
                {
                    ReadEvents();
                    UpdateViews(SimulationClock.SecondsPerTick);
                    seconds += SimulationClock.SecondsPerTick;
                    levelNameSeconds = MathF.Max(0f, levelNameSeconds - SimulationClock.SecondsPerTick);
                });

                // The cursor in the corner, so no box of the inventory is drawn as "under the mouse".
                Raylib.SetMousePosition(0, 0);
                if (photos.Bench)
                {
                    // The simulation stands still; every frame is drawn in full, as in the game: world, then screen.
                    var times = new List<double>(PhotoRequest.MeasuredFrames);
                    for (var frame = 0; frame < PhotoRequest.WarmUpFrames + PhotoRequest.MeasuredFrames; frame++)
                    {
                        var start = System.Diagnostics.Stopwatch.GetTimestamp();
                        DrawWorld(alpha: 1f, SimulationClock.SecondsPerTick);
                        Raylib.BeginDrawing();
                        DrawScreen(SimulationClock.SecondsPerTick, frozen: false, showMap: setup.MapOpen, mapKeptOpen: setup.MapOpen, debugInfo: false);
                        Raylib.EndDrawing();
                        if (frame >= PhotoRequest.WarmUpFrames)
                        {
                            times.Add(System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                        }
                    }

                    var statistics = FrameTimeStatistics.From(times);
                    log?.Info($"Bench '{scene.Name}' after {ticks} ticks, {Raylib.GetScreenWidth()} x {Raylib.GetScreenHeight()}: {statistics}");
                    Console.WriteLine($"{scene.Name}: {statistics}");
                    continue;
                }

                DrawWorld(alpha: 1f, SimulationClock.SecondsPerTick);
                Raylib.BeginDrawing();
                DrawScreen(SimulationClock.SecondsPerTick, frozen: false, showMap: setup.MapOpen, mapKeptOpen: setup.MapOpen, debugInfo: false);
                Rlgl.DrawRenderBatchActive();
                var screenPath = Path.Combine(photos.Folder, $"{scene.Name}{suffix}-screen.png");
                var screen = Raylib.LoadImageFromScreen();
                Raylib.ExportImage(screen, screenPath);
                Raylib.UnloadImage(screen);
                Raylib.EndDrawing();

                var picturePath = Path.Combine(photos.Folder, $"{scene.Name}{suffix}.png");
                picture.SaveAs(picturePath);
                log?.Info($"Photo '{scene.Name}' after {ticks} ticks: {picturePath}, {screenPath}");
                Console.WriteLine($"{scene.Name}: {picturePath} ({ticks} ticks)");
            }

            // Clip mode (--clip, issue #286): the same as a photo, but a picture of the whole window before the first
            // tick and after every n-th one, into <folder>/<scene>/frame-0000.png and on.
            foreach (var clip in photos.Clips)
            {
                var setup = clip.Build(studio);
                StartFresh(setup.World);
                var clipFolder = Path.Combine(photos.Folder, clip.Name);
                Directory.CreateDirectory(clipFolder);
                foreach (var old in Directory.EnumerateFiles(clipFolder, "frame-*.png"))
                {
                    // Pictures of an earlier, longer take would end up in the clip.
                    File.Delete(old);
                }

                // The views take the world as it is, without time passing: the first picture comes before any tick, and
                // fresh views would still show the boxes instead of the weapons in hand.
                UpdateViews(0f);
                Raylib.SetMousePosition(0, 0);
                var frames = 0;
                var taken = setup.Film(
                    clip.FrameEvery,
                    frame: _ =>
                    {
                        DrawWorld(alpha: 1f, SimulationClock.SecondsPerTick);
                        Raylib.BeginDrawing();
                        DrawScreen(SimulationClock.SecondsPerTick, frozen: false, showMap: setup.MapOpen, mapKeptOpen: setup.MapOpen, debugInfo: false);
                        Rlgl.DrawRenderBatchActive();
                        var screen = Raylib.LoadImageFromScreen();
                        Raylib.ExportImage(screen, Path.Combine(clipFolder, $"frame-{frames++:0000}.png"));
                        Raylib.UnloadImage(screen);
                        Raylib.EndDrawing();
                    },
                    afterTick: () =>
                    {
                        ReadEvents();
                        UpdateViews(SimulationClock.SecondsPerTick);
                        seconds += SimulationClock.SecondsPerTick;
                        levelNameSeconds = MathF.Max(0f, levelNameSeconds - SimulationClock.SecondsPerTick);
                    });
                var fps = clip.FramesPerSecond.ToString("0", CultureInfo.InvariantCulture);
                log?.Info($"Clip '{clip.Name}': {taken.Count} pictures over {taken[^1]} ticks, {fps} per second: {clipFolder}");
                Console.WriteLine($"{clip.Name}: {clipFolder} ({taken.Count} pictures, {fps} fps)");
            }

            if (photos.Compare)
            {
                foreach (var scene in photos.Scenes)
                {
                    var comparePath = Path.Combine(photos.Folder, $"{scene.Name}-compare.png");
                    ComparisonSheet.Save(
                        [.. LookVariants.All.Select(variant =>
                            (Path.Combine(photos.Folder, $"{scene.Name}-look{variant.Number}-screen.png"), variant.Label))],
                        comparePath);
                    log?.Info($"Comparison '{scene.Name}': {comparePath}");
                    Console.WriteLine($"{scene.Name}: {comparePath}");
                }
            }

            return;
        }

        // Sound (issue #267) - only here, after photo and bench mode: those never open a sound device. No device, a
        // missing or broken file: a warning in the log, the game runs silent or without that sound.
        using var sounds = SoundPlayer.Open(Path.Combine(AppContext.BaseDirectory, "assets", "audio"), log);

        // Which sound pack plays (issue #290): K switches between sound pack 1 and the old sounds. Presentation only,
        // never part of the world.
        var soundPacks = new SoundPackSwitch();

        // The menu click (issue #291): its own choice of variants, so the run's sounds do not depend on the menus.
        var menuCues = new SoundCues(0);
        void PlayMenuClick(MenuView menu)
        {
            if (menu.Clicked)
            {
                sounds.Play(menuCues.For(SoundKind.MenuClick, soundPacks.Current));
            }
        }

        // Game loop: runs once per drawn frame until the window is closed (X button) or "Quit" is chosen.
        while (!Raylib.WindowShouldClose() && !quit)
        {
            seconds += Raylib.GetFrameTime();

            // N mutes the game and turns it on again - anywhere, also on the title screen. Pure presentation, not tick input.
            if (Raylib.IsKeyPressed(KeyboardKey.N))
            {
                sounds.ToggleMute();
            }

            // K switches the sound pack (issue #290), so the human can compare old and new in the game.
            if (Raylib.IsKeyPressed(KeyboardKey.K))
            {
                soundPacks.Toggle();
                log?.Info($"Sound pack: {soundPacks.Current.Name}.");
            }

            soundPacks.Update(Raylib.GetFrameTime());

            // L hides and shows the labels of the showroom (issue #283). Presentation only, never part of the world.
            if (gameWorld.IsShowroom && Raylib.IsKeyPressed(KeyboardKey.L))
            {
                showroomLabelsShown = !showroomLabelsShown;
            }

            // The music (issue #271): the title theme on the title screen, the catacomb music in a run (held in the Esc
            // menu), none on the end screen - as it stood after the latest frame.
            sounds.Update(SoundBoard.MusicFor(new MusicSituation(onTitle, menuOpen, RunOver: endSummary is not null)), Raylib.GetFrameTime());

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

                PlayMenuClick(titleMenu);

                Raylib.BeginDrawing();
                TitleScreen.Draw(titleMenu, displayVersion);
                SoundPackNotice.Draw(soundPacks);
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

                PlayMenuClick(escMenu);

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

                PlayMenuClick(endMenu);
            }

            // The mouse is captured only while playing: free in the menu, on the end screen and while the bonus choice
            // is open (a click chooses; you walk on with the keys; a hit closes it).
            var frozen = menuOpen || endSummary is not null;
            var inventoryOpen = player.IsInventoryOpen;
            var capture = !frozen && !onTitle && !boonOpen && !inventoryOpen;
            if (capture && !input.IsMouseCaptured)
            {
                input.CaptureMouse();
            }
            else if (!capture && input.IsMouseCaptured)
            {
                input.ReleaseMouse();
            }

            // 1. Input: looking around happens every frame, so it feels immediate.
            var playerInput = input.Read(keysWithFreeMouse: (boonOpen || inventoryOpen) && !frozen);
            player.Look(playerInput, settings.MouseSensitivity);
            interactPressed |= playerInput.Interact;
            slotChosen = playerInput.SelectSlot ?? slotChosen;
            wheelSteps += playerInput.CycleSlot;

            // The bonus choice (decision record 0008): X opens and closes it while boons wait; it stays open while
            // walking - only a hit closes it (see the events below). Several level ups come one after the other.
            if (!frozen && !inventoryOpen && Raylib.IsKeyPressed(KeyboardKey.X) && player.PendingBoons > 0)
            {
                boonOpen = !boonOpen;
            }

            if (player.PendingBoons == 0)
            {
                boonOpen = false;
            }

            boonOpenSeconds = boonOpen ? boonOpenSeconds + Raylib.GetFrameTime() : 0f;
            if (boonOpen && !frozen)
            {
                chosenBoon ??= BoonWindow.Pick(player.CurrentOffer, boonOpenSeconds);
            }

            // The inventory (decision record 0008): G opens and closes it - not while the bonus choice is open. Moving or a
            // hit closes it (Core). A click is picked only once it has been open for a moment.
            if (!frozen && !boonOpen && Raylib.IsKeyPressed(KeyboardKey.G))
            {
                inventoryToggle = !inventoryToggle;
            }

            inventoryOpenSeconds = inventoryOpen ? inventoryOpenSeconds + Raylib.GetFrameTime() : 0f;
            if (inventoryOpen && !frozen)
            {
                inventoryClick ??= InventoryWindow.Pick(inventoryOpenSeconds);
            }

            // 2. Simulation: as many fixed ticks as fit into the real time of the last frame.
            //    A chosen bonus is kept until a tick has taken it - not every frame has a tick.
            var ticks = accumulator.Advance(Raylib.GetFrameTime());
            for (var i = 0; i < ticks; i++)
            {
                gameWorld.Tick(playerInput with
                {
                    ChooseBoon = chosenBoon, Interact = interactPressed, Paused = frozen,
                    SelectSlot = slotChosen, CycleSlot = Math.Sign(wheelSteps),
                    ToggleInventory = inventoryToggle, InventoryClick = inventoryClick,
                });
                chosenBoon = null;
                inventoryToggle = false;
                inventoryClick = null;
                interactPressed = false;
                slotChosen = null;
                wheelSteps = 0;
                ReadEvents();
                // Heard at this player's eyes: far-off wing beats stay silent (issue #297, no spatial sound yet).
                sounds.Play(soundCues.For(gameWorld.Events, soundPacks.Current, player.EyePosition));
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

            UpdateViews(Raylib.GetFrameTime());

            // 3. Rendering: draw the current state, blended between the last two ticks (no stutter
            //    on monitors faster than 60 Hz). No game rules in here.
            DrawWorld(accumulator.Alpha, Raylib.GetFrameTime());

            // 3b. ... which is scaled up onto the screen, the HUD on top. The map while Tab is held, or kept open with M (to have it while walking) - the game goes on meanwhile
            // (no pause: online co-op).
            if (!frozen && !boonOpen && !player.IsInventoryOpen && Raylib.IsKeyPressed(KeyboardKey.M))
            {
                mapKeptOpen = !mapKeptOpen;
            }

            Raylib.BeginDrawing();
            DrawScreen(
                Raylib.GetFrameTime(), frozen, showMap: mapKeptOpen || Raylib.IsKeyDown(KeyboardKey.Tab), mapKeptOpen, debugInfo: true);
            SoundPackNotice.Draw(soundPacks);
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
static void DrawHud(string displayVersion, bool fps)
{
    var screenWidth = Raylib.GetScreenWidth();
    var screenHeight = Raylib.GetScreenHeight();
    var textColor = new Color(200, 200, 210, 255);

    Raylib.DrawText("Silver & Ash", 20, 20, 30, textColor);

    // The frame rate is a developer display: only in the debug build (issue #340).
    if (fps && BuildInfo.ShowsDeveloperDisplays)
    {
        Raylib.DrawFPS(20, screenHeight - 30);
    }

    var versionText = $"v{displayVersion}";
    var versionWidth = Raylib.MeasureText(versionText, 20);
    Raylib.DrawText(versionText, screenWidth - versionWidth - 20, screenHeight - 30, 20, new Color(120, 120, 130, 255));
}

#if DEBUG
// Test option --give: a melee weapon goes into the hand (the one before into the bag), a ranged one into the first free
// place, or into the bag when all are taken. Only in the debug version, like the option itself.
static GameWorld WithGivenWeapons(GameWorld world, IReadOnlyList<WeaponKind> give)
{
    foreach (var kind in give)
    {
        if (Weapons.Info(kind).Hand == WeaponHand.Melee)
        {
            world.Player.Stow(world.Player.Melee);
            world.Player.EquipMelee(kind);
            continue;
        }

        var free = world.Player.RangedSlots.ToList().FindIndex(weapon => weapon is null);
        if (free >= 0)
        {
            world.Player.Equip(free, kind);
        }
        else
        {
            world.Player.Stow(kind);
        }
    }

    return world;
}
#endif

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

    var chargeText = player.MeleeCharge.IsCharging
        ? string.Create(CultureInfo.InvariantCulture, $"   Charge {player.MeleeCharge.Fraction * 100f:0} %")
        : string.Empty;
    var weaponsText = $"{Weapons.Info(player.Melee).Name} {CooldownText(player.MeleeCooldown)}   {player.Ranged.Info.Name} {CooldownText(player.Ranged.Cooldown)}{chargeText}";
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

/// <summary>What the photo mode (--photo) is to take: these scenes, saved into this folder, built from these room templates.</summary>
/// <remarks>Compare: take every scene in every picture variant and put them together 2 x 2 (--photo compare, issue #234).</remarks>
/// <remarks>Bench: take no picture but draw the scene again and again and print the frame times (--bench, issue #233).</remarks>
internal sealed record PhotoRequest(
    IReadOnlyList<PhotoScene> Scenes, string Folder, IReadOnlyList<RoomTemplate> Templates, bool Compare, bool Bench = false)
{
    /// <summary>The motion scenes to film (--clip, issue #286), after the photo scenes; usually there are only these.</summary>
    public IReadOnlyList<MotionScene> Clips { get; init; } = [];

    /// <summary>Frames drawn before measuring (shaders, textures and caches settle).</summary>
    public const int WarmUpFrames = 100;

    /// <summary>Frames measured by --bench.</summary>
    public const int MeasuredFrames = 1000;
}
