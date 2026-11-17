"""Lokale Foto-Hilfe fuer das Treppen-Probestueck (kein Spielcode, wird nach dem Fotografieren aus Git wiederhergestellt).

Schreibt:
  * PhotoStudio.StairsLook(): ein Saal (9 x 13 Zellen, Hoehe SA_HALL_H, Standard 6 m) mit der stehenden Treppe in der Mitte (wie im
    Musterraum: ShowroomExhibits), der Spieler SA_STAIRS_DIST Meter davor (gerade Zahl), Blick auf die Treppe; SA_CAM = px,pz,tx,ty,tz
    (Meter von der Treppenmitte) fuer eine eigene Kamera.
  * PhotoScenes: Szene "stairs-view".
  * StairsView.Draw: ist SA_STAIRS gesetzt (Dateiname ohne .glb in assets/models), wird dieses Modell statt der Kasten-Treppe gezeichnet
    (Knoten "stairs" und "glow"; "glow" pulsiert wie das Licht von heute).
  * SurfaceVariants.At: erzwingt die Wandvariante SA_FORCE_WALL (a, b, c).
Aufruf: python patch_lokal7.py apply   (zuruecksetzen: git checkout src)
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
STUDIO = os.path.join(ROOT, "src", "SilverAndAsh.Core", "Photos", "PhotoStudio.cs")
SCENES = os.path.join(ROOT, "src", "SilverAndAsh.Core", "Photos", "PhotoScenes.cs")
VARIANTS = os.path.join(ROOT, "src", "SilverAndAsh.Core", "Presentation", "SurfaceVariants.cs")
VIEW = os.path.join(ROOT, "src", "SilverAndAsh.Game", "Rendering", "StairsView.cs")

METHOD = r'''
    public GameWorld StairsLook()
    {
        var metres = int.Parse(Environment.GetEnvironmentVariable("SA_STAIRS_DIST") ?? "4", CultureInfo.InvariantCulture);
        var hall = float.Parse(Environment.GetEnvironmentVariable("SA_HALL_H") ?? "6", CultureInfo.InvariantCulture);
        const int Width = 9;
        const int Depth = 14;
        var cells = new Cell[Width, Depth];
        for (var x = 0; x < Width; x++)
        {
            for (var z = 0; z < Depth; z++)
            {
                var border = x == 0 || z == 0 || x == Width - 1 || z == Depth - 1;
                cells[x, z] = border ? Cell.Wall : new Cell(false, 0f, hall);
            }
        }

        var playerRow = 4 + (metres / 2);
        var level = new Level(cells, 4, playerRow);
        var stairs = level.CellCenter(4, 4);
        var exhibits = new ShowroomExhibits(stairs, [], [], []);
        var world = new GameWorld(level, Seed, Balance) { Showroom = exhibits };
        world.Player.PlaceAt(level.CellCenter(4, playerRow));
        Face(world.Player, stairs + new Vector3(0f, 1.6f, 0f));
        var cam = Environment.GetEnvironmentVariable("SA_CAM");
        if (cam is not null)
        {
            var v = cam.Split(',').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            world.Player.PlaceAt(new Vector3(stairs.X + v[0], 0f, stairs.Z + v[1]));
            Face(world.Player, new Vector3(stairs.X + v[2], v[3], stairs.Z + v[4]));
        }

        return world;
    }
'''

ENTRY = '''        new("stairs-view", "Local probe: the standing staircase in a hall (SA_STAIRS_DIST, SA_HALL_H, SA_CAM, SA_STAIRS).",
            studio => new PhotoSetup(studio.StairsLook())),
'''

VIEW_CODE = r'''
    private static TexturedModel? _probeModel;
    private static bool _probeTried;

    private static bool DrawProbe(Stairs stairs, Matrix4x4 place, float seconds)
    {
        var name = Environment.GetEnvironmentVariable("SA_STAIRS");
        if (name is null)
        {
            return false;
        }

        if (!_probeTried)
        {
            _probeTried = true;
            var path = Path.Combine(AppContext.BaseDirectory, "assets", "models", name + ".glb");
            _probeModel = TexturedModel.TryLoad(path, new[] { new SilverAndAsh.Core.Presentation.RigPart("stairs", null), new SilverAndAsh.Core.Presentation.RigPart("glow", null) }, null);
        }

        if (_probeModel is null)
        {
            return false;
        }

        var turns = new Matrix4x4[] { Matrix4x4.Identity, Matrix4x4.Identity };
        var frames = new Matrix4x4[2];
        _probeModel.Rig.Pose(turns, place, frames);
        _probeModel.DrawPart(0, frames[0]);
        _probeModel.DrawPart(1, frames[1], stairs.CanInteract ? 0.65f + (0.35f * MathF.Sin(seconds * 2f)) : 1f);
        return true;
    }

    public static void Draw('''


def main():
    if sys.argv[1:] != ["apply"]:
        print(__doc__)
        return
    s = open(STUDIO, encoding="utf-8").read()
    if "StairsLook()" not in s:
        i = s.index("    /// <summary>\n    /// Turns <paramref name=\"player\"/>")
        s = s[:i] + METHOD + "\n" + s[i:]
        open(STUDIO, "w", encoding="utf-8").write(s)
    t = open(SCENES, encoding="utf-8").read()
    if "stairs-view" not in t:
        marker = '        new("ghoul-4m",'
        t = t.replace(marker, ENTRY + marker, 1)
        open(SCENES, "w", encoding="utf-8").write(t)
    v = open(VARIANTS, encoding="utf-8").read()
    if "SA_FORCE_WALL" not in v:
        v = v.replace("    public TextureVariant At(int x, int z, SurfaceKind kind) =>\n",
                      "    public TextureVariant At(int x, int z, SurfaceKind kind) =>\n"
                      "        kind == SurfaceKind.Wall && Environment.GetEnvironmentVariable(\"SA_FORCE_WALL\") is { } forced\n"
                      "            ? Enum.Parse<TextureVariant>(forced, true) :\n", 1)
        open(VARIANTS, "w", encoding="utf-8").write(v)
    w = open(VIEW, encoding="utf-8").read()
    if "DrawProbe" not in w:
        w = w.replace("    public static void Draw(", VIEW_CODE, 1)
        w = w.replace("        var place = Matrix4x4.CreateTranslation(stairs.Position - new Vector3(0f, (1f - rise) * Stairs.Height, 0f));\n",
                      "        var place = Matrix4x4.CreateTranslation(stairs.Position - new Vector3(0f, (1f - rise) * Stairs.Height, 0f));\n"
                      "        if (DrawProbe(stairs, place, seconds))\n        {\n            return;\n        }\n", 1)
        open(VIEW, "w", encoding="utf-8").write(w)
    print("ok")


main()
