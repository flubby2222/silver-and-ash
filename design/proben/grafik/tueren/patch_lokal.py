"""Lokale Foto-Hilfe fuer das Tuer-Probestueck (kein Spielcode, wird nach dem Fotografieren aus Git wiederhergestellt).

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

        var plan = new LevelDoor(1, door, 0, 1, RoomSide.North, kind);
        var level = LevelParser.Parse(text.ToString());
        var world = new GameWorld(level, Seed, Balance, [plan]);
        var row = back ? door + half : door - half;
        world.Player.PlaceAt(level.CellCenter(1, row));
        Face(world.Player, level.CellCenter(1, door) + new Vector3(0f, 1.3f, 0f));
        return world;
    }
'''

SCENE_ENTRY = '''        new("door-view", "Local probe: one door (SA_DOOR_KIND, SA_DOOR_DIST, SA_DOOR_BACK).",
            studio => new PhotoSetup(studio.DoorView())),
'''

VIEW_CODE = r'''
    private static readonly Dictionary<DoorKind, SilverAndAsh.Game.Rendering.TexturedModel?> Models = [];

    private static readonly Dictionary<DoorKind, string[]> PartNames = new()
    {
        [DoorKind.Normal] = ["leaf_a", "leaf_b"],
        [DoorKind.Boss] = ["glow", "leaf_a", "leaf_b"],
        [DoorKind.Locked] = ["leaf", "lock"],
        [DoorKind.Cracked] = ["cracks"],
    };

    private static SilverAndAsh.Game.Rendering.TexturedModel? ModelFor(DoorKind kind)
    {
        if (!Models.TryGetValue(kind, out var model))
        {
            var path = Path.Combine(AppContext.BaseDirectory, "assets", "models", $"door_{kind.ToString().ToLowerInvariant()}.glb");
            var parts = PartNames[kind].Select(n => new SilverAndAsh.Core.Presentation.RigPart(n, null)).ToList();
            Models[kind] = model = SilverAndAsh.Game.Rendering.TexturedModel.TryLoad(path, parts, null);
        }

        return model;
    }

    private static bool DrawModel(Door door, Matrix4x4 place)
    {
        var model = ModelFor(door.Kind);
        if (model is null)
        {
            return false;
        }

        var open = float.Parse(Environment.GetEnvironmentVariable("SA_DOOR_OPEN") ?? "0", System.Globalization.CultureInfo.InvariantCulture);
        var unlocked = Environment.GetEnvironmentVariable("SA_DOOR_UNLOCKED") == "1";
        var names = PartNames[door.Kind];
        var turns = new Matrix4x4[names.Length];
        var frames = new Matrix4x4[names.Length];
        var angle = open * 100f * MathF.PI / 180f;
        for (var i = 0; i < names.Length; i++)
        {
            turns[i] = names[i] switch
            {
                "leaf_a" or "leaf" => Matrix4x4.CreateRotationY(-angle),
                "leaf_b" => Matrix4x4.CreateRotationY(angle),
                _ => Matrix4x4.Identity,
            };
        }

        // The origin of the model is the middle of the doorway on the floor.
        var floor = Matrix4x4.CreateTranslation(0f, -(door.BoxMax.Y - door.BoxMin.Y) / 2f, 0f) * place;
        model.Rig.Pose(turns, floor, frames);
        for (var i = 0; i < names.Length; i++)
        {
            if (names[i] == "lock" && (unlocked || open > 0f))
            {
                continue;
            }

            if (names[i] == "glow" && open > 0f)
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
