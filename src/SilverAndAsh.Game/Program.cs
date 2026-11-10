using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core;

const int ScreenWidth = 1280;
const int ScreenHeight = 720;

Raylib.SetConfigFlags(ConfigFlags.VSyncHint | ConfigFlags.Msaa4xHint);
Raylib.InitWindow(ScreenWidth, ScreenHeight, "Silver & Ash");

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

Raylib.CloseWindow();
