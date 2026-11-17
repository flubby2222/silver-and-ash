"""Lokale Foto-Hilfe fuer das Tuerrahmen-Probestueck (kein Spielcode, wird nach dem Fotografieren aus Git wiederhergestellt).

Schreibt:
  * PhotoStudio.FrameView(): die Tuerhalle von main (DoorHall), nur mit Umgebungsvariablen
      SA_DOOR_KIND (Normal, Locked, Boss), SA_DOOR_DIST (Meter vor der Tuer, gerade), SA_DOOR_OPEN (1 = geoeffnet, ganz),
      SA_SIDEWAYS (1 = Tuer in einer Wand, die von Ost nach West laeuft, um 90 Grad gedreht),
      SA_HALL_H (Deckenhoehe der Halle ausser der Tuerzelle, z. B. 6), SA_CAM (px,pz,tx,ty,tz in Metern von der Tuermitte:
      eigene Kamera, z. B. fuer die Nahaufnahme), SA_BACK (1 = Blick von der anderen Seite, Spiegelung der Halle).
  * PhotoScenes: Szene "frame-view" (mit 600 Ticks bis ganz offen, wenn SA_DOOR_OPEN=1).
  * SurfaceVariants.At: erzwingt die Wandvariante SA_FORCE_WALL (a, b, c) fuer alle Waende.
Aufruf: python patch_lokal6.py apply   (zuruecksetzen: git checkout src)
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
STUDIO = os.path.join(ROOT, "src", "SilverAndAsh.Core", "Photos", "PhotoStudio.cs")
SCENES = os.path.join(ROOT, "src", "SilverAndAsh.Core", "Photos", "PhotoScenes.cs")
VARIANTS = os.path.join(ROOT, "src", "SilverAndAsh.Core", "Presentation", "SurfaceVariants.cs")
POSE = os.path.join(ROOT, "src", "SilverAndAsh.Core", "Presentation", "DoorPose.cs")

METHOD = r'''
    public GameWorld FrameView()
    {
        var kind = Enum.Parse<DoorKind>(Environment.GetEnvironmentVariable("SA_DOOR_KIND") ?? "Normal", true);
        var metres = int.Parse(Environment.GetEnvironmentVariable("SA_DOOR_DIST") ?? "4", CultureInfo.InvariantCulture);
        var open = Environment.GetEnvironmentVariable("SA_DOOR_OPEN") == "1";
        var hall = float.Parse(Environment.GetEnvironmentVariable("SA_HALL_H") ?? "3.5", CultureInfo.InvariantCulture);
        var sideways = Environment.GetEnvironmentVariable("SA_SIDEWAYS") == "1";
        var world = DoorHall(kind, sideways, open, Math.Max(2, metres / 2));
        if (hall > 3.6f)
        {
            // Rooms are 6 m high, the door cell stays at the low standard height - as the level generator builds them.
            var old = world.Level;
            var cells = new Cell[old.Width, old.Depth];
            for (var x = 0; x < old.Width; x++)
            {
                for (var z = 0; z < old.Depth; z++)
                {
                    var c = old[x, z];
                    var isDoor = world.Doors.Any(d => d.X == x && d.Z == z);
                    cells[x, z] = isDoor ? Cell.Floor : c.IsSolid ? c : new Cell(false, c.FloorHeight, hall);
                }
            }

            var rebuilt = new Level(cells, old.PlayerStartX, old.PlayerStartZ);
            var doors = world.Doors.Select(d => new LevelDoor(d.X, d.Z, 0, 1, d.Side, d.Kind)).ToArray();
            world = new GameWorld(rebuilt, Seed, Balance, doors);
            var d0 = world.Doors[0];
            var dm = (d0.BoxMin + d0.BoxMax) / 2f;
            world.Player.PlaceAt(rebuilt.CellCenter(4, 6 - Math.Max(2, metres / 2)));
            Face(world.Player, dm);
            if (open)
            {
                var start = world.Player.Position;
                world.Player.PlaceAt(rebuilt.CellCenter(4, 5));
                Face(world.Player, dm);
                if (kind == DoorKind.Locked) { world.Player.AddKeys(1); }
                world.Tick(PlayerInput.None with { Interact = true });
                world.Player.PlaceAt(start);
                Face(world.Player, dm);
            }
        }

        var cam = Environment.GetEnvironmentVariable("SA_CAM");
        if (cam is not null)
        {
            var v = cam.Split(',').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            var door = world.Doors[0];
            var mid = (door.BoxMin + door.BoxMax) / 2f;
            world.Player.PlaceAt(new Vector3(mid.X + v[0], 0f, mid.Z + v[1]));
            Face(world.Player, new Vector3(mid.X + v[2], v[3], mid.Z + v[4]));
        }

        return world;
    }
'''

ENTRY = '''        new("frame-view", "Local probe: the door hall with env switches (SA_DOOR_KIND, SA_DOOR_DIST, SA_DOOR_OPEN, SA_HALL_H, SA_CAM).",
            studio => new PhotoSetup(studio.FrameView())
            {
                Ticks = Environment.GetEnvironmentVariable("SA_DOOR_OPEN") == "1" ? 600 : 0,
                StopWhen = Environment.GetEnvironmentVariable("SA_DOOR_OPEN") == "1" ? Swung(1f) : null,
            }),
'''


def main():
    if sys.argv[1:] != ["apply"]:
        print(__doc__)
        return
    s = open(STUDIO, encoding="utf-8").read()
    if "FrameView()" not in s:
        i = s.index("    /// <summary>\n    /// Turns <paramref name=\"player\"/>")
        s = s[:i] + METHOD + "\n" + s[i:]
        open(STUDIO, "w", encoding="utf-8").write(s)
    t = open(SCENES, encoding="utf-8").read()
    if "frame-view" not in t:
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
    d = open(POSE, encoding="utf-8").read()
    if "SA_NEWPORTAL" not in d:
        extra = "\n".join([
            '    private static readonly RigPart[] DoubleDoorX = [new(Portal, null), new(LeafA, Portal), new(LeafB, Portal), '
            'new("portal_front", Portal), new("portal_back", Portal)];',
            '    private static readonly RigPart[] LockedDoorX = [new(Portal, null), new(Leaf, Portal), new(Lock, Leaf), '
            'new("portal_front", Portal), new("portal_back", Portal)];',
            '    private static bool NewPortal => Environment.GetEnvironmentVariable("SA_NEWPORTAL") == "1";',
            "", ""])
        d = d.replace("    private static readonly RigPart[] CrackedWall", extra + "    private static readonly RigPart[] CrackedWall", 1)
        d = d.replace("DoorKind.Normal or DoorKind.Boss => DoubleDoor,", "DoorKind.Normal or DoorKind.Boss => NewPortal ? DoubleDoorX : DoubleDoor,", 1)
        d = d.replace("DoorKind.Locked => LockedDoor,", "DoorKind.Locked => NewPortal ? LockedDoorX : LockedDoor,", 1)
        open(POSE, "w", encoding="utf-8").write(d)
    print("ok")


main()
