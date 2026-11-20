"""Prueft, dass am Lebensbalken bei JEDER erreichbaren Laenge keine Niete angeschnitten ist (Issue #412, Nachbesserung zu PR #488).

Aufruf (Blender dient nur als Python mit numpy; nichts wird heruntergeladen):
  "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/hud/hud_nieten_pruefen.py [-- <hud-Ordner>]

Ohne Ordner prueft es assets/hud. Das Spiel kuerzt frame_life von rechts (HudLayout.CutFrame) auf die Laenge des Balkens; steht dort
eine Niete, bleibt ein Stummel neben dem Endstueck. Das Skript
  1. liest die Laengen aus assets/balance.json (Startleben, Schritt je Upgrade, Deckel; keine festen Zahlen),
  2. rechnet je Laenge und je Atlas (einfach und doppelt) die sichtbare Breite von frame_life genau wie CutFrame,
  3. sucht die Nieten im Bild selbst (Muster der Halbkugel aus hud_nieten.py, Radius 3) und meldet jede, deren Spalten teils
     links, teils rechts vom Schnitt liegen.
Es beendet mit Fehlercode 1, wenn auch nur eine Laenge eine halbe Niete zeigt, und auch, wenn es gar keine Niete findet.
"""
import sys, os, re, importlib.util
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
RIM = 3                  # HudLayout.Rim in Pixeln des einfachen Atlas
R = 3                    # Radius der Lebensbalken-Nieten im doppelten Raster


def load(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, name + ".py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def balance(text):
    def find(section, key):
        m = re.search(r'"%s"\s*:\s*\{[^}]*?"%s"\s*:\s*(\d+)' % (section, key), text)
        if m is None:
            sys.exit('balance.json: "%s.%s" nicht gefunden' % (section, key))
        return int(m.group(1))
    return find("player", "maxHealth"), find("boons", "health"), find("boons", "healthCap")


def rivets(frame, dome):
    """Mitten aller Nieten (Muster der Halbkugel, Radius 3) im doppelten Bild frame (H x W x 4)."""
    n = 2 * R + 1
    want = np.zeros((n, n, 4), np.uint8)
    mask = dome >= 0
    for j in range(n):
        for i in range(n):
            if mask[j, i]:
                want[j, i, :3] = SIL[dome[j, i]]
                want[j, i, 3] = 255
    found = []
    h, w = frame.shape[:2]
    for cy in range(R, h - R):
        for cx in range(R, w - R):
            win = frame[cy - R:cy + R + 1, cx - R:cx + R + 1]
            if np.array_equal(win[mask], want[mask]):
                found.append((cx, cy))
    return found


def main():
    global SIL
    nieten = load("hud_nieten")
    SIL = nieten.SIL
    lebens = load("hud_lebensbalken")
    dome = nieten.dome_levels(R)
    folder = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1 else os.path.join(REPO, "assets", "hud")
    start, step, cap = balance(open(os.path.join(REPO, "assets", "balance.json"), encoding="utf-8").read())
    lengths = list(range(start, cap + 1, step))
    print("Laengen:", start, "bis", lengths[-1], "in Schritten von", step, "(%d Stueck)" % len(lengths))

    sheets = {}
    for name, scale in (("hud_parts_x2", 2), ("hud_parts", 1)):
        import json
        table = {p["name"]: p for p in json.load(open(os.path.join(folder, name + ".json"), encoding="utf-8"))["parts"]}
        atlas = lebens.read_png(os.path.join(folder, name + ".png"))
        p = table["frame_life"]
        sheets[scale] = (atlas[p["y"]:p["y"] + p["h"], p["x"]:p["x"] + p["w"]], p, table)

    x2, p2, _ = sheets[2]
    x1, p1, _ = sheets[1]
    fails = []
    if not np.array_equal(x1, x2[::2, ::2]):
        fails.append("frame_life im einfachen Atlas ist nicht jedes zweite Pixel des doppelten")
    centres = rivets(x2, dome)
    print("Nieten auf frame_life (doppeltes Raster, x):", sorted({c[0] for c in centres}))
    if not centres:
        fails.append("keine Niete auf frame_life gefunden (Muster oder Lage stimmen nicht)")

    for scale, (img, p, _) in sheets.items():
        # Spalten je Niete: im doppelten Raster x - 3 bis x + 3, im einfachen jede zweite davon (halbe Lage)
        every = 2 // scale
        spans = []
        for cx, _cy in centres:
            cols = [c // every for c in range(cx - R, cx + R + 1) if c % every == 0]
            spans.append((cx, cols))
        for length in lengths:
            body_w = p["body"][2]
            visible = p["w"] - (body_w - (length * scale + 2 * RIM * scale))
            for cx, cols in spans:
                shown = [c for c in cols if c < visible]
                if shown and len(shown) < len(cols):
                    fails.append("Leben %d, Raster x%d: Niete bei x=%d (doppeltes Raster) angeschnitten, Schnitt bei Spalte %d, sichtbar %d von %d Spalten"
                                 % (length, scale, cx, visible, len(shown), len(cols)))
    for f in fails:
        print("FEHLER:", f)
    if fails:
        print("%d Fehler" % len(fails))
        sys.exit(1)
    print("OK: bei allen %d Laengen und beiden Atlanten ist jede Niete ganz oder gar nicht zu sehen" % len(lengths))


main()
