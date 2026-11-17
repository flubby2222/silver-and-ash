"""Lokale Foto-Hilfe fuer das Tuer-Probestueck Runde 2 (kein Spielcode, wird nach dem Fotografieren aus Git wiederhergestellt).

Schreibt: eine Foto-Szene "door-view" (Umgebungsvariablen SA_DOOR_KIND, SA_DOOR_DIST, SA_DOOR_BACK) in PhotoStudio/PhotoScenes
und eine Tuer-Ansicht mit den Modellen in DoorView (SA_DOOR_OPEN = 0..1: Winkel der Fluegel; SA_DOOR_UNLOCKED fuer Locked).
Aufruf: python patch_lokal.py apply   (zum Zuruecksetzen: git checkout src)
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))))
if os.path.basename(ROOT) != "design-probe-ghul-45a8c4":
    ROOT = os.getcwd()

STUDIO = os.path.join(ROOT, "src", "SilverAndAsh.Core", "Photos", "PhotoStudio.cs")
SCENES = os.path.join(ROOT, "src", "SilverAndAsh.Core", "Photos", "PhotoScenes.cs")
VIEW = os.path.join(ROOT, "src", "SilverAndAsh.Game", "Rendering", "DoorView.cs")

STUDIO_METHOD = r'''
    public GameWorld DoorView()
    {
        var kind = Enum.Parse<DoorKind>(Environment.GetEnvironmentVariable("SA_DOOR_KIND") ?? "Normal", true);
        var metres = int.Parse(Environment.GetEnvironmentVariable("SA_DOOR_DIST") ?? "4", CultureInfo.InvariantCulture);
        var back = Environment.GetEnvironmentVariable("SA_DOOR_BACK") == "1";
        var half = metres / 2;
        var door = half + 1;
        var length = metres + 3;
        var text = new StringBuilder();
        for (var z = 0; z < length; z++)
        {
            for (var x = 0; x < 3; x++)
            {
                text.Append(x == 1 && z == 1 ? 'P' : x == 1 && z > 0 && z < length - 1 ? '.' : '#');
            }

            text.Append('\n');
        }

        var ew = Environment.GetEnvironmentVariable("SA_DOOR_EW") == "1";
        var textEw = new StringBuilder();
        if (ew)
        {
            for (var zz = 0; zz < 3; zz++)
            {
                for (var xx = 0; xx < length; xx++)
                {
                    textEw.Append(zz == 1 && xx == 1 ? 'P' : zz == 1 && xx > 0 && xx < length - 1 ? '.' : '#');
                }

                textEw.Append('\n');
            }
        }

        var plan = ew ? new LevelDoor(door, 1, 0, 1, RoomSide.East, kind) : new LevelDoor(1, door, 0, 1, RoomSide.North, kind);
        var level = LevelParser.Parse(ew ? textEw.ToString() : text.ToString());
        var world = new GameWorld(level, Seed, Balance, [plan]);
        var row = back ? door + half : door - half;
        world.Player.PlaceAt(ew ? level.CellCenter(row, 1) : level.CellCenter(1, row));
        Face(world.Player, (ew ? level.CellCenter(door, 1) : level.CellCenter(1, door)) + new Vector3(0f, 1.3f, 0f));
        return world;
    }
'''

SCENE_ENTRY = '''        new("door-view", "Local probe: one door (SA_DOOR_KIND, SA_DOOR_DIST, SA_DOOR_BACK).",
            studio => new PhotoSetup(studio.DoorView())),
'''

VIEW_CODE = r'''
    private static readonly Dictionary<string, SilverAndAsh.Game.Rendering.TexturedModel?> Models = [];

    private static readonly Dictionary<string, (string Name, string? Parent)[]> PartNames = new()
    {
        ["door_normal"] = [("portal", null), ("leaf_a", null), ("leaf_b", null)],
        ["door_locked"] = [("portal", null), ("leaf", null), ("lock", "leaf")],
        ["door_boss_a"] = [("portal", null), ("leaf_a", null), ("leaf_b", null)],
        ["door_boss_b"] = [("portal", null), ("grate", null)],
        ["door_cracked"] = [("cracks", null)],
    };

    private static string FileFor(DoorKind kind) => kind switch
    {
        DoorKind.Normal => "door_normal",
        DoorKind.Locked => "door_locked",
        DoorKind.Boss => "door_boss_" + (Environment.GetEnvironmentVariable("SA_DOOR_VARIANT") ?? "a"),
        _ => "door_cracked",
    };

    private static bool DrawModel(Door door, Matrix4x4 place)
    {
        var file = FileFor(door.Kind);
        if (!Models.TryGetValue(file, out var model))
        {
            var path = Path.Combine(AppContext.BaseDirectory, "assets", "models", file + ".glb");
            var parts = PartNames[file].Select(p => new SilverAndAsh.Core.Presentation.RigPart(p.Name, p.Parent)).ToList();
            Models[file] = model = SilverAndAsh.Game.Rendering.TexturedModel.TryLoad(path, parts, null);
        }

        if (model is null)
        {
            return false;
        }

        var open = float.Parse(Environment.GetEnvironmentVariable("SA_DOOR_OPEN") ?? "0", System.Globalization.CultureInfo.InvariantCulture);
        var sign = Environment.GetEnvironmentVariable("SA_DOOR_BACK") == "1" ? -1f : 1f;
        var names = PartNames[file];
        var turns = new Matrix4x4[names.Length];
        var frames = new Matrix4x4[names.Length];
        var angle = open * (file == "door_locked" ? 80f : 90f) * MathF.PI / 180f * sign;
        for (var i = 0; i < names.Length; i++)
        {
            turns[i] = names[i].Name switch
            {
                "leaf_a" or "leaf" => Matrix4x4.CreateRotationY(angle),
                "leaf_b" => Matrix4x4.CreateRotationY(-angle),
                "grate" => Matrix4x4.CreateTranslation(0f, open * 1.8f, 0f),
                _ => Matrix4x4.Identity,
            };
        }

        var floor = Matrix4x4.CreateTranslation(0f, -(door.BoxMax.Y - door.BoxMin.Y) / 2f, 0f) * place;
        model.Rig.Pose(turns, floor, frames);
        for (var i = 0; i < names.Length; i++)
        {
            if (names[i].Name == "lock" && open > 0f)
            {
                continue;
            }

            model.DrawPart(i, frames[i]);
        }

        return true;
    }
'''


def main():
    if sys.argv[1:] != ["apply"]:
        print(__doc__)
        return
    s = open(STUDIO, encoding="utf-8").read()
    if "DoorView()" not in s:
        i = s.index("    /// <summary>\n    /// Turns <paramref name=\"player\"/>")
        s = s[:i] + STUDIO_METHOD + "\n" + s[i:]
        open(STUDIO, "w", encoding="utf-8").write(s)
    t = open(SCENES, encoding="utf-8").read()
    if "door-view" not in t:
        marker = '        new("ghoul-4m",'
        t = t.replace(marker, SCENE_ENTRY + marker, 1)
        open(SCENES, "w", encoding="utf-8").write(t)
    v = open(VIEW, encoding="utf-8").read()
    if "DrawModel(" not in v:
        v = v.replace("    /// <summary>Draws all closed doors.", VIEW_CODE + "\n    /// <summary>Draws all closed doors.", 1)
        v = v.replace("        foreach (var door in doors.Where(door => door.Blocks))",
                      "        foreach (var door in doors.Where(door => door.Blocks || Environment.GetEnvironmentVariable(\"SA_DOOR_OPEN\") is not null))", 1)
        v = v.replace("            var depth = alongX ? size.X : size.Z;\n",
                      "            var depth = alongX ? size.X : size.Z;\n            if (Environment.GetEnvironmentVariable(\"SA_DOOR_OPEN\") is not null && DrawModel(door, place))\n            {\n                continue;\n            }\n\n", 1)
        open(VIEW, "w", encoding="utf-8").write(v)
    print("ok")


main()
