"""Lokale Foto-Hilfe fuer das Probestueck rissige Wand (kein Spielcode, wird nach dem Fotografieren aus Git wiederhergestellt).

Schreibt: Foto-Szene "wall-view" (SA_WALL_VAR a/b/c, SA_WALL_DIST Meter, SA_WALL_BACK 1 = aus dem Geheimraum) in
PhotoStudio/PhotoScenes; eine erzwungene Wandvariante in SurfaceVariants; und in DoorView das Zeichnen der Risse-Modelle
(SA_CRACK = <var>_<staerke>, z. B. a_dezent; ohne Variable: nichts Neues).
Aufruf: python patch_lokal3.py apply   (zuruecksetzen: git checkout src)
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
STUDIO = os.path.join(ROOT, "src", "SilverAndAsh.Core", "Photos", "PhotoStudio.cs")
SCENES = os.path.join(ROOT, "src", "SilverAndAsh.Core", "Photos", "PhotoScenes.cs")
VIEW = os.path.join(ROOT, "src", "SilverAndAsh.Game", "Rendering", "DoorView.cs")
VARIANTS = os.path.join(ROOT, "src", "SilverAndAsh.Core", "Presentation", "SurfaceVariants.cs")

STUDIO_METHOD = r'''
    public GameWorld WallView()
    {
        var variant = Enum.Parse<TextureVariant>(Environment.GetEnvironmentVariable("SA_WALL_VAR") ?? "A", true);
        var metres = float.Parse(Environment.GetEnvironmentVariable("SA_WALL_DIST") ?? "4", CultureInfo.InvariantCulture);
        var back = Environment.GetEnvironmentVariable("SA_WALL_BACK") == "1";
        const int Width = 7;
        const int WallRow = 6;
        var text = new StringBuilder();
        for (var z = 0; z < WallRow + 3; z++)
        {
            for (var x = 0; x < Width; x++)
            {
                var floor = (z >= 1 && z < WallRow && x > 0 && x < Width - 1) || (z == WallRow && x == 3) || (z == WallRow + 1 && x == 3);
                text.Append(x == 3 && z == 1 ? 'P' : floor ? '.' : '#');
            }

            text.Append('\n');
        }

        var plan = new LevelDoor(3, WallRow, 0, 1, RoomSide.North, DoorKind.Cracked);
        var level = LevelParser.Parse(text.ToString());
        var world = new GameWorld(level, Seed, Balance, [plan]) { Surfaces = SurfaceVariants.ForcedWallOf(variant) };
        var faceZ = WallRow * Level.CellSize;
        if (back)
        {
            world.Player.PlaceAt(level.CellCenter(3, WallRow + 1));
            Face(world.Player, new Vector3(7f, 1.3f, faceZ));
        }
        else
        {
            var px = 7f;
            world.Player.PlaceAt(new Vector3(px, 0f, faceZ - metres));
            Face(world.Player, new Vector3(px, 1.3f, faceZ));
        }

        return world;
    }
'''

SCENE_ENTRY = '''        new("wall-view", "Local probe: a cracked wall (SA_WALL_VAR, SA_WALL_DIST, SA_WALL_BACK).",
            studio => new PhotoSetup(studio.WallView())),
'''

VIEW_CODE = r'''
    private static readonly Dictionary<string, SilverAndAsh.Game.Rendering.TexturedModel?> CrackModels = [];

    private static bool DrawCrack(Door door, Matrix4x4 place)
    {
        var name = Environment.GetEnvironmentVariable("SA_CRACK");
        if (name is null)
        {
            return false;
        }

        if (!CrackModels.TryGetValue(name, out var model))
        {
            var path = Path.Combine(AppContext.BaseDirectory, "assets", "models", "crack_" + name + ".glb");
            var parts = new[] { "chunk", "debris", "panel" }.Select(n => new SilverAndAsh.Core.Presentation.RigPart(n, null)).ToList();
            CrackModels[name] = model = SilverAndAsh.Game.Rendering.TexturedModel.TryLoad(path, parts, null);
        }

        if (model is null)
        {
            return false;
        }

        var turns = new Matrix4x4[3] { Matrix4x4.Identity, Matrix4x4.Identity, Matrix4x4.Identity };
        var frames = new Matrix4x4[3];
        var floor = Matrix4x4.CreateTranslation(0f, -(door.BoxMax.Y - door.BoxMin.Y) / 2f, 0f) * place;
        model.Rig.Pose(turns, floor, frames);
        model.DrawPart(1, frames[1]);
        model.DrawPart(2, frames[2]);
        return true;
    }
'''


def main():
    if sys.argv[1:] != ["apply"]:
        print(__doc__)
        return
    s = open(STUDIO, encoding="utf-8").read()
    if "WallView()" not in s:
        i = s.index("    /// <summary>\n    /// Turns <paramref name=\"player\"/>")
        s = s[:i] + STUDIO_METHOD + "\n" + s[i:]
        open(STUDIO, "w", encoding="utf-8").write(s)
    t = open(SCENES, encoding="utf-8").read()
    if "wall-view" not in t:
        marker = '        new("ghoul-4m",'
        t = t.replace(marker, SCENE_ENTRY + marker, 1)
        open(SCENES, "w", encoding="utf-8").write(t)
    sv = open(VARIANTS, encoding="utf-8").read()
    if "ForcedWallOf" not in sv:
        sv = sv.replace("    public static SurfaceVariants Plain { get; } = new([], new());",
                        "    public static SurfaceVariants Plain { get; } = new([], new());\n\n    public TextureVariant? ForcedWall { get; init; }\n\n    public static SurfaceVariants ForcedWallOf(TextureVariant variant) => new([], new()) { ForcedWall = variant };", 1)
        sv = sv.replace("    public TextureVariant At(int x, int z, SurfaceKind kind) =>\n",
                        "    public TextureVariant At(int x, int z, SurfaceKind kind) => ForcedWall is { } forced && kind == SurfaceKind.Wall ? forced :\n", 1)
        open(VARIANTS, "w", encoding="utf-8").write(sv)
    v = open(VIEW, encoding="utf-8").read()
    if "DrawCrack(" not in v:
        v = v.replace("    /// <summary>Draws all closed doors.", VIEW_CODE + "\n    /// <summary>Draws all closed doors.", 1)
        v = v.replace("            if (!door.IsOpen && door.Kind == DoorKind.Cracked)\n            {\n",
                      "            if (!door.IsOpen && door.Kind == DoorKind.Cracked)\n            {\n                if (DrawCrack(door, place))\n                {\n                    continue;\n                }\n\n", 1)
        open(VIEW, "w", encoding="utf-8").write(v)
    print("ok")


main()
