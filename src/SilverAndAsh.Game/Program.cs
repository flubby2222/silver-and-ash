using System.Globalization;
using System.Numerics;
using System.Reflection;
using Raylib_cs;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Diagnostics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
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
    WindowSize? window = null;
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

        // Start option: --room assets/rooms/combat/l-hall.txt - one room template on its own, entered from its first
        // door slot (issue #528, docs/level-format.md); --no-enemies leaves its enemies out.
        RoomPreview? preview = null;
        if (options.Room is { } room)
        {
            preview = new RoomPreview(RoomPreview.Load(room.Path, AppContext.BaseDirectory), room.WithEnemies);
            newWorld = preview.NewWorld;
            nextWorld = preview.NextWorld;
            log?.Info($"Room template '{preview.Template.Name}' on its own{(room.WithEnemies ? string.Empty : ", without enemies")}.");
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

        // Test option: --window 2520x1080 opens the window in another shape, e.g. for photos in 21:9 (issue #350).
        window = options.Window;

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
                    : preview is not null ? [preview.EntrancePhoto(seed ?? 0)]
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

        if (options.Window is not null)
        {
            throw new ArgumentException("'--window' works only in the debug version (dotnet run).");
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
            RunGame(newWorld, nextWorld, seed, displayVersion: version.Split('+')[0], log, photos, LookVariants.Get(look), window);
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
    PhotoRequest? photos, LookVariant look, WindowSize? window)
{
    // No MSAA (edge smoothing): the hard pixel edges of the low resolution are wanted. The bench (--bench) draws as
    // fast as it can: no VSync, and no frame rate limit is set anywhere.
    Raylib.SetConfigFlags(photos is { Bench: true } ? ConfigFlags.ResizableWindow : ConfigFlags.VSyncHint | ConfigFlags.ResizableWindow);
    Raylib.InitWindow(window?.Width ?? WindowWidth, window?.Height ?? WindowHeight, "Silver & Ash");
    BringWindowOnScreen(log);

    // Esc must not close the game - it opens the Esc menu.
    Raylib.SetExitKey(KeyboardKey.Null);

    HudView? hud = null;
    try
    {
        // The game's own pixel font for all texts (issue #367).
        var fontsDirectory = Path.Combine(AppContext.BaseDirectory, "assets", "fonts");
        PixelText.Load(fontsDirectory, log);
        MenuArt.Load(Path.Combine(AppContext.BaseDirectory, "assets", "menu"), log);

        // The HUD from its parts picture (issue #369).
        hud = HudView.Load(Path.Combine(AppContext.BaseDirectory, "assets", "hud"), fontsDirectory, log);
        MenuArt.PlaceUnder(hud);

        // Balancing values from assets/balance.json - a broken file only costs warnings in the log, never the game.
        var balancePath = Path.Combine(AppContext.BaseDirectory, "assets", "balance.json");
        var balance = LoadBalance(balancePath, log);

        // The player's own settings (sensitivity, volumes, full screen; issue #349), kept locally:
        // %LOCALAPPDATA%\SilverAndAsh\settings.json. Photos, the bench and --window keep the window as it is.
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SilverAndAsh", "settings.json");
        var settings = SettingsFile.Load(settingsPath);
        if (photos is null && window is null && FullScreenToggle.Set(settings.FullScreen))
        {
            LogDisplay(log);
        }

        settings = settings with { FullScreen = FullScreenToggle.IsFullScreen };

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
        var screenEffects = new ScreenEffects(player.Id);
        using var screenEffectsView = new ScreenEffectsView();
        var equipmentCard = new EquipmentCard();
        var input = new RaylibInput();

        // Test display of a photo scene (issue #433): what a shot can hit, as wires over the figures.
        var shotBodiesShown = false;

        // Title screen first; during a run Esc opens the menu (the world stands still - alone).
        var onTitle = true;
        var menuOpen = false;
        var quit = false;
        var titleMenu = new MenuView();
        titleMenu.Open();
        var escMenu = new MenuView();

        // The settings and the question before quitting open from the title screen and from the Esc menu (issue #349);
        // menuPage says which page of the open menu is shown.
        var menuPage = MenuPage.Main;
        var settingsMenu = SettingsMenu.NewMenu();
        var settingsDrag = new SettingsSliderDrag();
        var settingsUnsaved = false;
        var quitMenu = new MenuView();

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
            screenEffects = new ScreenEffects(player.Id);
            equipmentCard = new EquipmentCard();
            hud?.Reset();
        }

        // Choosing a level-up bonus: is the window open (X), and which boon is waiting for the next tick?
        var boonOpen = false;
        var boonOpenSeconds = 0f;
        Boon? chosenBoon = null;

        // The destination choice of the teleporters (issue #480, T at an unlocked teleporter); the chosen destination
        // waits for the next tick, like a chosen bonus.
        var travelWindow = new TravelWindow();
        int? travelTo = null;

        // The menu with the map page (Tab) and the equipment page (G), issue #348: Core keeps which page is open; the key
        // and a click wait for the next tick, like a chosen bonus.
        MenuKey? menuKey = null;
        var inventoryOpenSeconds = 0f;
        InventoryClick? inventoryClick = null;

        // F pressed in a frame without a tick must not get lost: it waits for the next tick, like a chosen bonus.
        // So do 1 / 2 / 3 and the mouse wheel (switching the ranged weapon).
        var interactPressed = false;
        int? slotChosen = null;
        var wheelSteps = 0;
        var seconds = 0f;

        var accumulator = new TickAccumulator();
        using var picture = new LowResolutionTarget(look.RenderHeight);

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

        // The Well of Sirius, ready or spent (issue #438); without its model a stone placeholder.
        using var wellView = WellOfSiriusView.Load(Path.Combine(AppContext.BaseDirectory, "assets", "models"), log);

        // The teleporters, locked or unlocked (issue #480); without their models placeholder boxes.
        using var teleporterView = TeleporterView.Load(Path.Combine(AppContext.BaseDirectory, "assets", "models"), log);

        // The crossbow skeleton (issue #497) - for now only as the showroom's figure; without its models placeholder boxes.
        using var crossbowSkeletonView = CrossbowSkeletonViews.Load(Path.Combine(AppContext.BaseDirectory, "assets", "models"), log);

        // The crypt warden (issue #530) - likewise only the showroom's figure for now.
        using var cryptWardenView = CryptWardenViews.Load(Path.Combine(AppContext.BaseDirectory, "assets", "models"), log);

        // The wall pillars (issue #536) - for now only in the showroom; without their models the walls stay bare.
        using var wallPillarView = WallPillarView.Load(Path.Combine(AppContext.BaseDirectory, "assets", "models"), log);

        // The ceiling arches (issue #537) - for now only in the showroom; without their models the corridor stays bare.
        using var ceilingArchView = CeilingArchView.Load(Path.Combine(AppContext.BaseDirectory, "assets", "models"), log);

        // The wall niches (issue #538) - for now only in the showroom; without their models the walls stay bare.
        using var wallNicheView = WallNicheView.Load(Path.Combine(AppContext.BaseDirectory, "assets", "models"), log);

        // The portcullises (issues #540, #558): in the doors of the arenas, and in the showroom falling and lifting in a loop.
        using var portcullisView = PortcullisView.Load(Path.Combine(AppContext.BaseDirectory, "assets", "models"), log);

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
        // A deep wall niche without its model leaves no hole in the wall face (issue #553).
        Level LevelToDraw() => wallNicheView.AsDrawn(gameWorld.LevelAsBuilt, gameWorld.Showroom?.WallNiches ?? [], levelTextures);
        using var levelRenderer = new LevelRenderer(LevelToDraw(), gameWorld.Surfaces, levelShader.Shader, levelTextures);
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
                screenEffects.OnEvent(gameEvent);
                equipmentCard.OnEvent(gameEvent, player.Id);
                hud?.OnEvent(gameEvent, player.Id);
            }

            // Once per tick, like the events: the edge effects count in ticks (issue #370).
            screenEffects.Tick(player);
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
            hud?.Update(frameSeconds);
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

            // The picture follows the window: 480 lines, as wide as the window's shape (issue #350).
            picture.FitToWindow(look.RenderHeight);
            picture.BeginDrawing();
            Raylib.ClearBackground(look.BackgroundColor);
            Raylib.BeginMode3D(camera);
            if (!ReferenceEquals(renderedLevel, gameWorld.LevelAsBuilt) || !ReferenceEquals(renderedSurfaces, gameWorld.Surfaces))
            {
                levelRenderer.Rebuild(LevelToDraw(), gameWorld.Surfaces);
                renderedLevel = gameWorld.LevelAsBuilt;
                renderedSurfaces = gameWorld.Surfaces;
            }

            levelRenderer.Draw();
            levelShader.BeginFigures();
            var viewCone = new ViewCone(eye, forward, LookSettings.FieldOfViewDegrees, (float)picture.Width / picture.Height);
            doorView.Draw(gameWorld.Doors, seconds, viewCone, gameWorld.Surfaces, levelTextures, portcullisView.HasGrate);
            portcullisView.Draw(gameWorld.Doors, viewCone, gameWorld.Surfaces, levelTextures);
            chestView.Draw(gameWorld.Chests, seconds);
            stairsView.Draw(gameWorld.Stairs, seconds, viewCone, gameWorld.Surfaces, levelTextures);
            wellView.Draw(gameWorld.Well, seconds, viewCone);
            teleporterView.Draw(gameWorld.Teleporters, viewCone, levelShader);
            crossbowSkeletonView.Draw(gameWorld.Showroom?.CrossbowSkeletons ?? [], viewCone);
            cryptWardenView.Draw(gameWorld.Showroom?.CryptWardens ?? [], viewCone);
            wallPillarView.Draw(gameWorld.Showroom?.WallPillars ?? [], viewCone, levelTextures);
            ceilingArchView.Draw(gameWorld.Showroom?.CeilingArches ?? [], viewCone, levelTextures);
            wallNicheView.Draw(gameWorld.Showroom?.WallNiches ?? [], viewCone, levelTextures);
            portcullisView.Draw(
                gameWorld.Showroom?.Portcullises ?? [], ShowroomGrateCycle.Height(gameWorld.Ticks, gameWorld.Balance.Grates),
                viewCone, gameWorld.Surfaces, levelTextures, doorView);
            foreach (var dummy in gameWorld.Dummies)
            {
                dummyView.Draw(dummy);
            }

            // Enemies of a combat room wait hidden in the floor or the ceiling until it seals (issue #424).
            foreach (var ghoul in gameWorld.Ghouls.Where(ghoul => !ghoul.Emergence.IsHidden))
            {
                ghoulView.Draw(ghoul);
            }

            if (gameWorld.Gorm is { } gorm)
            {
                gormView.Draw(gorm, frameSeconds);
                gormView.DrawEffects(gorm);
                shockwaveView.Draw(gorm);
            }

            foreach (var bat in gameWorld.Bats.Where(bat => !bat.Emergence.IsHidden))
            {
                batView.Draw(bat, eye);
            }

            projectileView.Draw(gameWorld.Projectiles, alpha);
            pickupView.Draw(gameWorld.Pickups, alpha);
            pickupView.DrawLoot(gameWorld.Loot, alpha);
            pickupView.DrawEquipment(gameWorld.EquipmentDrops, alpha);

            impactMarks.Draw();
            wallBreakView.Draw(doorView);

            // Last: the weapons in the hands, in front of everything else - not in the test display, where they would
            // hide the figure (issue #433).
            if (!shotBodiesShown)
            {
                weaponView.Draw(camera.Position, camera.Target - camera.Position);
            }

            levelShader.EndFigures();
            if (shotBodiesShown)
            {
                ShotBodyView.Draw(gameWorld.Targets);
            }

            Raylib.EndMode3D();

            // The edge effects in the small picture, so they get its big pixels (issue #370); not over the end screen.
            if (endSummary is null)
            {
                screenEffectsView.Draw(screenEffects, picture.Width, picture.Height);
            }

            picture.EndDrawing();
        }

        // 3b. The small picture scaled up onto the screen, the HUD on top at full resolution (sharp). Between
        // Raylib.BeginDrawing and EndDrawing.
        // The open page of the title screen or the Esc menu: its buttons, the settings or the question before quitting.
        void DrawMenuPage(bool onTitle)
        {
            switch (menuPage)
            {
                case MenuPage.Settings:
                    SettingsMenu.Draw(settingsMenu, settings);
                    break;
                case MenuPage.ConfirmQuit:
                    QuitPrompt.Draw(quitMenu);
                    break;
                default:
                    if (onTitle)
                    {
                        TitleScreen.Draw(titleMenu, displayVersion);
                    }
                    else
                    {
                        EscMenu.Draw(escMenu);
                    }

                    break;
            }
        }

        // debugInfo: the frame rate and the test display, both only in the debug version (issue #340) - not in photos.
        void DrawScreen(float frameSeconds, bool frozen, bool debugInfo)
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

            hud?.Draw(gameWorld, player, picture.Width, picture.Height);
            DrawFrameRate(debugInfo);
            if (!frozen)
            {
                equipmentCard.Draw();
            }

            if (!boonOpen && !player.Menu.IsOpen && !travelWindow.IsOpen && !frozen)
            {
                // The travel hint only where F offers nothing, so the two never share the line.
                var target = gameWorld.InteractionTarget(player);
                InteractionPrompt.Draw(target, player);
                if (target is null && gameWorld.TeleporterUnder(player) is not null)
                {
                    TravelWindow.DrawHint();
                }
            }

            levelNameSeconds = MathF.Max(0f, levelNameSeconds - frameSeconds);
            if (!frozen)
            {
                LevelBanner.Draw(gameWorld.LevelName, levelNameSeconds);
            }

            if (!frozen && !boonOpen && player.Menu.Page == PlayerMenuPage.Map)
            {
                MapView.Draw(gameWorld, player, (float)Raylib.GetTime());
            }

            if (player.IsInventoryOpen && !frozen)
            {
                InventoryWindow.Draw(gameWorld, player);
            }
            else if (boonOpen && !frozen)
            {
                BoonWindow.Draw(player, gameWorld.Balance.Boons);
            }
            else if (travelWindow.IsOpen && !frozen)
            {
                travelWindow.Draw(gameWorld, gameWorld.TravelChoices(player));
            }

            if (menuOpen)
            {
                DrawMenuPage(onTitle: false);
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
                    PixelText.Draw("Balancing reloaded", 20, 160, 20, new Color(230, 190, 90, 255));
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
                    picture.FitToWindow(look.RenderHeight);
                }

                var suffix = photos.Compare ? $"-look{look.Number}" : string.Empty;
                var setup = scene.Build(studio);
                StartFresh(setup.World);
                shotBodiesShown = setup.ShowsShotBodies;
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
                        DrawScreen(SimulationClock.SecondsPerTick, frozen: false, debugInfo: false);
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
                DrawPhotoScreen(setup);
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

            // A photo's screen: the game's, or a menu over it (issue #349) - drawn by the same code as in the game.
            void DrawPhotoScreen(PhotoSetup setup)
            {
                titleMenu.Open();
                escMenu.Open();
                settingsMenu.Open();
                quitMenu.Open();
                menuPage = setup.Menu switch
                {
                    PhotoMenu.Settings => MenuPage.Settings,
                    PhotoMenu.Quit => MenuPage.ConfirmQuit,
                    _ => MenuPage.Main,
                };
                if (setup.Menu == PhotoMenu.Title)
                {
                    TitleScreen.Draw(titleMenu, displayVersion);
                }
                else
                {
                    // The destination choice is a window over the game, not a menu (issue #480).
                    if (setup.Menu == PhotoMenu.Travel)
                    {
                        travelWindow.Open(gameWorld.TravelChoices(gameWorld.Player));
                    }

                    menuOpen = setup.Menu is not (PhotoMenu.None or PhotoMenu.Travel);
                    DrawScreen(SimulationClock.SecondsPerTick, frozen: menuOpen, debugInfo: false);
                }

                travelWindow.Close();
                menuOpen = false;
                menuPage = MenuPage.Main;
            }

            // Clip mode (--clip, issue #286): the same as a photo, but a picture of the whole window before the first
            // tick and after every n-th one, into <folder>/<scene>/frame-0000.png and on.
            foreach (var clip in photos.Clips)
            {
                var setup = clip.Build(studio);
                StartFresh(setup.World);
                shotBodiesShown = setup.ShowsShotBodies;
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
                        DrawScreen(SimulationClock.SecondsPerTick, frozen: false, debugInfo: false);
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

        sounds.SetVolumes(settings.MasterVolume, settings.EffectsVolume, settings.AmbienceVolume);

        // A changed setting counts at once (issue #349) and is written to the file, so it also counts after a restart -
        // during a drag on a slider only once, when the drag ends (issue #426).
        void ChangeSettings(GameSettings changed)
        {
            if (changed != settings)
            {
                settings = changed;
                settingsUnsaved = true;
                sounds.SetVolumes(settings.MasterVolume, settings.EffectsVolume, settings.AmbienceVolume);
                if (FullScreenToggle.Set(settings.FullScreen))
                {
                    AfterDisplaySwitch();
                }
            }

            SaveSettingsWhenDue();
        }

        void SaveSettingsWhenDue()
        {
            if (!settingsDrag.WriteNow(settingsUnsaved))
            {
                return;
            }

            settingsUnsaved = false;
            if (!SettingsFile.Save(settings, settingsPath))
            {
                log?.Warning($"Could not save the settings to '{settingsPath}'.");
            }
        }

        // A drag on a slider ends when the settings are not on screen (left with Esc, Back, the window losing focus...)
        // - also with the button still down, so it never carries over into the next visit (issue #426).
        void EndSettingsDragOffPage()
        {
            if (menuPage == MenuPage.Settings && (onTitle || menuOpen))
            {
                return;
            }

            settingsDrag.End();
            SaveSettingsWhenDue();
        }

        // A captured mouse is captured again in the new window, so the view does not jump.
        void AfterDisplaySwitch()
        {
            if (input.IsMouseCaptured)
            {
                input.CaptureMouse();
            }

            LogDisplay(log);
        }

        // The settings or the question before quitting, if one of them is open - the same on the title screen and in
        // the Esc menu. True while one is open: the buttons below it read no input then. Esc going back is the caller's.
        bool UpdateMenuPage()
        {
            switch (menuPage)
            {
                case MenuPage.Settings:
                    var back = SettingsMenu.Update(settingsMenu, settingsDrag, settings, out var changed) == SettingsMenu.Back;
                    PlayMenuClick(settingsMenu);
                    ChangeSettings(changed);
                    menuPage = back ? MenuPage.Main : menuPage;
                    return true;
                case MenuPage.ConfirmQuit:
                    var answer = QuitPrompt.Update(quitMenu);
                    PlayMenuClick(quitMenu);
                    quit = answer == QuitPrompt.Quit;
                    menuPage = answer == QuitPrompt.Cancel ? MenuPage.Main : menuPage;
                    return true;
                default:
                    return false;
            }
        }

        void OpenMenuPage(MenuPage page)
        {
            settingsDrag.End();
            SaveSettingsWhenDue();
            menuPage = page;
            (page == MenuPage.Settings ? settingsMenu : quitMenu).Open();
        }

        // Game loop: runs once per drawn frame until the window is closed (X button) or "Quit" is chosen.
        while (!Raylib.WindowShouldClose() && !quit)
        {
            seconds += Raylib.GetFrameTime();
            EndSettingsDragOffPage();

            // Alt + Enter: window or borderless full screen (issue #350). The settings follow, so the menu shows it and the
            // next start opens the same way (issue #349).
            if (FullScreenToggle.Update())
            {
                AfterDisplaySwitch();
                ChangeSettings(settings with { FullScreen = FullScreenToggle.IsFullScreen });
            }

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

                // Esc leaves the settings or the question before quitting; on the title itself it does nothing.
                if (Raylib.IsKeyPressed(KeyboardKey.Escape))
                {
                    menuPage = MenuPage.Main;
                }
                else if (!UpdateMenuPage())
                {
                    switch (TitleScreen.Update(titleMenu))
                    {
                        case TitleScreen.Start:
                            StartFresh(newWorld(seed ?? Random.Shared.Next(), balance));
                            onTitle = false;
                            menuOpen = false;
                            break;
                        case TitleScreen.Settings:
                            OpenMenuPage(MenuPage.Settings);
                            break;
                        case TitleScreen.Quit:
                            OpenMenuPage(MenuPage.ConfirmQuit);
                            break;
                    }

                    PlayMenuClick(titleMenu);
                }

                Raylib.BeginDrawing();
                TitleScreen.Draw(titleMenu, displayVersion, buttons: menuPage == MenuPage.Main);
                if (menuPage != MenuPage.Main)
                {
                    DrawMenuPage(onTitle: true);
                }

                SoundPackNotice.Draw(soundPacks);
                Raylib.EndDrawing();
                continue;
            }

            // Esc first closes an open overlay (bonus choice, the menu with map and equipment page) and opens the menu only on
            // the next press (issue #346); with the menu open it closes the menu - or first goes back from the settings or the
            // question before quitting (issue #349). Leaving the window (Alt+Tab) opens the menu, too. Not on the end screen.
            var wasMenuOpen = menuOpen;
            var escClosedOverlay = false;
            if (endSummary is not null)
            {
                menuOpen = false;
            }
            else if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                if (!menuOpen && (boonOpen || player.Menu.IsOpen || travelWindow.IsOpen))
                {
                    travelWindow.Close();
                    // The menu lives in Core: it closes with the next tick, like with Tab or G.
                    if (player.Menu.IsOpen)
                    {
                        menuKey = MenuKey.Close;
                    }

                    boonOpen = false;
                    escClosedOverlay = true;
                }
                else if (menuOpen && menuPage != MenuPage.Main)
                {
                    menuPage = MenuPage.Main;
                }
                else
                {
                    menuOpen = !menuOpen;
                }
            }
            else
            {
                // Alt+Tab: the run holds and the mouse is free for the other window (issue #428).
                menuOpen = FocusPause.MenuOpen(menuOpen, Raylib.IsWindowFocused());
            }

            if (menuOpen && !wasMenuOpen)
            {
                escMenu.Open();
                menuPage = MenuPage.Main;
            }

            if (menuOpen && !UpdateMenuPage())
            {
                switch (EscMenu.Update(escMenu))
                {
                    case EscMenu.Resume:
                        menuOpen = false;
                        break;
                    case EscMenu.Settings:
                        OpenMenuPage(MenuPage.Settings);
                        break;
                    case EscMenu.Title:
                        onTitle = true;
                        menuOpen = false;
                        titleMenu.Open();
                        break;
                    case EscMenu.Quit:
                        OpenMenuPage(MenuPage.ConfirmQuit);
                        break;
                }

                PlayMenuClick(escMenu);
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
            // is open (a click chooses; you walk on with the keys; only the player closes it, issue #346).
            var frozen = menuOpen || endSummary is not null;
            var inventoryOpen = player.IsInventoryOpen;

            // Both pages of the menu free the mouse, so it can click the tabs (issue #390).
            var playerMenuOpen = player.Menu.IsOpen;
            var capture = FocusPause.CapturesMouse(menuOpen, endSummary is not null, onTitle, boonOpen || playerMenuOpen || travelWindow.IsOpen);
            if (capture && !input.IsMouseCaptured)
            {
                input.CaptureMouse();
            }
            else if (!capture && input.IsMouseCaptured)
            {
                input.ReleaseMouse();
            }

            // 1. Input: looking around happens every frame, so it feels immediate.
            var playerInput = input.Read(keysWithFreeMouse: (boonOpen || playerMenuOpen || travelWindow.IsOpen) && !frozen);
            player.Look(playerInput, settings.MouseSensitivity);
            interactPressed |= playerInput.Interact;
            slotChosen = playerInput.SelectSlot ?? slotChosen;
            wheelSteps += playerInput.CycleSlot;

            // The bonus choice (decision record 0008): X opens and closes it while boons wait; it stays open while walking
            // and when hit - only X or Esc close it (issue #346). Several level ups come one after the other. A dead
            // player takes no boon (Core), so the window closes and X stays locked - display only.
            if (!frozen && !player.Menu.IsOpen && !travelWindow.IsOpen && !escClosedOverlay && !player.IsDead && Raylib.IsKeyPressed(KeyboardKey.X)
                && player.PendingBoons > 0)
            {
                boonOpen = !boonOpen;
            }

            if (player.PendingBoons == 0 || player.IsDead)
            {
                boonOpen = false;
            }

            boonOpenSeconds = boonOpen ? boonOpenSeconds + Raylib.GetFrameTime() : 0f;
            if (boonOpen && !frozen)
            {
                chosenBoon ??= BoonWindow.Pick(player.CurrentOffer, boonOpenSeconds);
            }

            // The menu (issue #348): Tab opens the map page, G the equipment page (the inventory of decision record 0008);
            // the same key closes, the other turns the page - not while the bonus choice is open. Only the player closes it,
            // neither moving nor a hit (issue #346). Not Tab with Alt held: Alt+Tab leaves the window. A click is picked
            // only once the equipment page has been open for a moment.
            if (!frozen && !boonOpen && !travelWindow.IsOpen && !escClosedOverlay)
            {
                if (Raylib.IsKeyPressed(KeyboardKey.G))
                {
                    menuKey ??= MenuKey.Equipment;
                }
                else if (Raylib.IsKeyPressed(KeyboardKey.Tab) && !Raylib.IsKeyDown(KeyboardKey.LeftAlt)
                    && !Raylib.IsKeyDown(KeyboardKey.RightAlt))
                {
                    menuKey ??= MenuKey.Map;
                }
                else if (playerMenuOpen)
                {
                    // A click on a tab turns to its page (issue #390) - as input in the tick, like the keys.
                    menuKey ??= MenuTabs.Pick();
                }
            }

            // The destination choice (issue #480): T at an unlocked teleporter, not over another window. Which destinations
            // there are and whether they are free comes from Core; the travel goes into the tick as input.
            if (!frozen && !boonOpen && !player.Menu.IsOpen && !escClosedOverlay)
            {
                var choices = gameWorld.TravelChoices(player);
                travelWindow.Toggle(choices);
                travelTo ??= travelWindow.Update(gameWorld, choices, Raylib.GetFrameTime());
            }
            else
            {
                // Under another window, the menu or the end screen it closes - so it never stays over a new run.
                travelWindow.Close();
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
                    PressMenu = menuKey, InventoryClick = inventoryClick, TeleportTo = travelTo,
                });
                chosenBoon = null;
                travelTo = null;
                menuKey = null;
                inventoryClick = null;
                interactPressed = false;
                slotChosen = null;
                wheelSteps = 0;
                ReadEvents();
                // Heard by this player (issue #491): from the side it comes from, quieter with distance; far-off wing
                // beats stay silent (issue #297).
                sounds.Play(soundCues.For(
                    gameWorld.Events, soundPacks.Current, SoundListener.Of(player), gameWorld.Balance.Hearing));
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

            // 3b. ... which is scaled up onto the screen, the HUD on top.
            Raylib.BeginDrawing();
            DrawScreen(Raylib.GetFrameTime(), frozen, debugInfo: true);
            SoundPackNotice.Draw(soundPacks);
            Raylib.EndDrawing();
        }

        // Closed in the middle of a drag: the dragged value is still written.
        settingsDrag.End();
        SaveSettingsWhenDue();
    }
    finally
    {
        hud?.Dispose();
        PixelText.Unload();
        MenuArt.Unload();
        Raylib.CloseWindow();
    }
}

// The window or full screen as it is now, with the monitor's size - after every switch (issues #349, #350).
static void LogDisplay(FileLog? log)
{
    var monitor = Raylib.GetCurrentMonitor();
    log?.Info(
        $"{(FullScreenToggle.IsFullScreen ? "Full screen" : "Window")}: "
        + $"{Raylib.GetScreenWidth()} x {Raylib.GetScreenHeight()}, monitor "
        + $"{Raylib.GetMonitorWidth(monitor)} x {Raylib.GetMonitorHeight(monitor)}.");
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

// The frame rate is a developer display: only in the debug build (issue #340), at the left edge above the items.
static void DrawFrameRate(bool fps)
{
    if (fps && BuildInfo.ShowsDeveloperDisplays)
    {
        Raylib.DrawFPS(20, Raylib.GetScreenHeight() / 2);
    }
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

    PixelText.Draw(speedText, 20, 60, 20, color);
    if (player.HasPerfectJumpBonus)
    {
        PixelText.Draw("PERFECT", 20, 85, 20, bonusColor);
    }
    else if (player.IsExhausted)
    {
        PixelText.Draw("EXHAUSTED", 20, 85, 20, exhaustedColor);
    }

    var chargeText = player.MeleeCharge.IsCharging
        ? string.Create(CultureInfo.InvariantCulture, $"   Charge {player.MeleeCharge.Fraction * 100f:0} %")
        : string.Empty;
    var weaponsText = $"{Weapons.Info(player.Melee).Name} {CooldownText(player.MeleeCooldown)}   {player.Ranged.Info.Name} {CooldownText(player.Ranged.Cooldown)}{chargeText}";
    PixelText.Draw(weaponsText, 20, 135, 20, new Color(200, 200, 210, 255));

    if (player.IsDashing)
    {
        PixelText.Draw("DASH", 20, 110, 20, new Color(140, 200, 230, 255));
    }
    else if (player.IsDodging)
    {
        PixelText.Draw("DODGE", 20, 110, 20, new Color(140, 230, 160, 255));
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
