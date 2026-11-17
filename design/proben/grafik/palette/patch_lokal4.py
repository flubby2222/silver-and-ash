"""Lokale Foto-Hilfe fuer die Palettenprobe (kein Spielcode, wird aus Git wiederhergestellt): Foto-Szene "palette-hall" - ein Saal
mit Ghul, vier Kisten (alle Stufen) und einer Holztuer am Ende, Spieler 4 m oder 10 m vor dem Ghul (SA_PAL_DIST).
Aufruf: python patch_lokal4.py apply"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
STUDIO = os.path.join(ROOT, "src", "SilverAndAsh.Core", "Photos", "PhotoStudio.cs")
SCENES = os.path.join(ROOT, "src", "SilverAndAsh.Core", "Photos", "PhotoScenes.cs")

METHOD = r'''
    public GameWorld PaletteHall()
    {
        var metres = int.Parse(Environment.GetEnvironmentVariable("SA_PAL_DIST") ?? "4", CultureInfo.InvariantCulture);
        const int Width = 9;
        const int GhoulRow = 7;
        const int DoorRow = 10;
        var text = new StringBuilder();
        for (var z = 0; z < 13; z++)
        {
            for (var x = 0; x < Width; x++)
            {
                var wall = x == 0 || x == Width - 1 || z == 0 || z == 12 || (z == DoorRow && x != 4) || (z == 11 && x != 4);
                text.Append(wall ? '#' : x == 4 && z == 2 ? 'P' : x == 4 && z == GhoulRow ? 'G' : '.');
            }

            text.Append('\n');
        }

        ChestTier[] tiers = [ChestTier.Beggar, ChestTier.Guard, ChestTier.King, ChestTier.Vampire];
        int[] columns = [2, 3, 5, 6];
        var chests = tiers
            .Select((tier, index) => new ChestPlan(columns[index], GhoulRow, 0, tier, IsLocked: true, Silver: 0,
                HasBloodVial: false, RisesWhenCleared: false, ChestFront.MinusZ))
            .ToList();
        var doors = new List<LevelDoor> { new(4, DoorRow, 0, 1, RoomSide.North, DoorKind.Normal) };
        var level = LevelParser.Parse(text.ToString());
        var world = new GameWorld(level, Seed, Balance, doors, chests: chests);
        var row = GhoulRow - (metres / 2);
        world.Player.PlaceAt(level.CellCenter(4, row));
        Face(world.Player, Middle(world.Targets.First().Body));
        return world;
    }
'''
ENTRY = '''        new("palette-hall", "Local probe: ghoul, four chests, wooden door (SA_PAL_DIST).",
            studio => new PhotoSetup(studio.PaletteHall())),
'''


def main():
    if sys.argv[1:] != ["apply"]:
        print(__doc__)
        return
    s = open(STUDIO, encoding="utf-8").read()
    if "PaletteHall()" not in s:
        i = s.index("    /// <summary>\n    /// Turns <paramref name=\"player\"/>")
        s = s[:i] + METHOD + "\n" + s[i:]
        open(STUDIO, "w", encoding="utf-8").write(s)
    t = open(SCENES, encoding="utf-8").read()
    if "palette-hall" not in t:
        marker = '        new("ghoul-4m",'
        t = t.replace(marker, ENTRY + marker, 1)
        open(SCENES, "w", encoding="utf-8").write(t)
    print("ok")


main()
