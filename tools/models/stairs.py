"""Treppe zur naechsten Ebene (Issue #300): die Wendeltreppe aus Stein, je Wandvariante a, b, c.

Erzeuger fuer das Spiel. Uebergabe aus dem Probestueck treppe (Branch claude/design-probe-ghul-45a8c4, Commit 6ea274a,
design/proben/grafik/treppe/treppe.py), Entwurf 1 "Wendeltreppe", den der Mensch am 2026-11-17 gewaehlt hat. Bereinigt: nur
dieser Entwurf, Kacheln aus assets/textures/ (die das Spiel zeichnet), Werkstein aus der Reihe der Hauptpalette. Erzeugt nur
Modelle und Texturen nach assets/models/, keine Probebilder, dazu die Liste der gelesenen Kacheln
(tools/models/stairs_sources.sha256).

Aufruf (aus dem Repository-Wurzelverzeichnis), danach python tools/models/checksums.py:
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/stairs.py

Alles per Skript, nichts heruntergeladen. Spiel-Achsen (x seitlich, y oben, z vorn), Meter. Ursprung: Mitte am Boden.
Die Treppe steht im Kasten von Stairs (+-1,2 m, so hoch wie der Raum: 6 m, Issue #723; vorher 4 m): zwei achteckige Sockelstufen,
eine achteckige Spindel bis 5,5 m, 20 Stufen zu 0,25 m und 45 Grad (zweieinhalb Umdrehungen; Stufenhoehe und Radius wie vorher,
nur mehr Stufen), ein Kapitell aus Werkstein (5,5 bis 5,75) und die Lampe (5,75 bis 6). Die Hoehe steht hier als HEIGHT und
muss RoomTemplate.RoomCeilingHeight entsprechen (ein Test prueft es).

Je Wandvariante v eine Datei stairs_spiral_<v>.glb mit zwei Knoten, beide fest und ohne Eltern:
  stairs   der Stein: Wandkachel der Variante auf den Seiten, Bodenkachel derselben Variante auf allen nach oben zeigenden
           Flaechen, Werkstein am Kapitell.
  glow     die Lampe oben (Messing/Gold, Verlauf von dunkel nach hell); das Spiel laesst nur sie pulsieren.
Atlas 256 x 512 (Issue #416): Wandkachel 2 x 6 mal nebeneinander, Bodenkachel und Werkstein-Kachel je 2 x 2 mal (jede Kachel
64 x 64 = 1 m), dazu der Messing-Verlauf 2 x 2 mal. Keine neuen Farben:
nur die der beiden Kacheln, die das Spiel ohnehin zeichnet, dazu Werkstein und Messing. Weil die Kacheln im Spiel je um 24 Farben
haben, sind das zusammen gut 50 (das Probestueck hatte Kacheln mit 6 bis 8 Farben und kam auf 25); die Grenze von 32 Farben
der art-bible gilt hier deshalb nicht fuer den ganzen Atlas.

Steinflaechen bekommen ihre Textur-Koordinaten aus der Position wie SurfaceTexturing (eine 64-px-Kachel je Meter, u am
Wandverlauf, v mit der Hoehe). Sie werden nie geteilt (Issue #416): laeuft eine Flaeche ueber eine Metergrenze, laeuft die
Koordinate in die Kopie der Kachel im Atlas weiter. So hat die Treppe keine T-Stellen, jede Kante gehoert zu genau zwei
Dreiecken (306 statt 552 Dreiecke). Die Stufen sind Keile bis zur Achse (innen von der Spindel verdeckt), je 0,5 mm
voneinander abgesetzt, damit sich benachbarte Stufen nicht an einer Kante beruehren. Die Treppe steht auf einer Zellmitte (ungerade Meter) und
wird nicht gedreht, also laufen Fugen und Platten im Raster weiter. Aendert sich eine Kachel, passt die Treppe nicht mehr; ein
Test vergleicht stairs_sources.sha256 mit assets/textures/textures.sha256 und schlaegt fehl, bis dieses Skript neu gelaufen ist.
"""
import hashlib
import json
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from psx import V, G, Part, save_image, export_material, export_glb, report_parts  # noqa: E402

TOOLS = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(TOOLS))
OUT_DIR = os.path.join(ROOT, "assets", "models")
TEXTURES = os.path.join(ROOT, "assets", "textures")
PALETTE = os.path.join(ROOT, "assets", "palette", "hauptpalette.json")
SOURCES = os.path.join(TOOLS, "stairs_sources.sha256")

W, H = 256, 512
# Atlasfelder in Pixeln (y von unten); jedes Feld ist die Kachel mehrfach nebeneinander, damit keine Flaeche an einer Metergrenze
# geteilt werden muss (emit): Wand 2 x 6 Kacheln (die Spindel ist 5,25 m hoch), Boden und Werkstein 2 x 2, Lampe 2 x 2 Verlaeufe.
REGIONS = {"stone": (0, 128, 128, 512), "glow": (0, 0, 128, 64), "floor": (128, 0, 256, 128), "werk": (128, 128, 256, 256)}
TILES = {"stone": (64, 64), "floor": (64, 64), "werk": (64, 64), "glow": (64, 32)}                # Pixel je Kachel (= je Meter)
UV = {}                                                                             # Part verlangt die Rechtecke, emit nimmt REGIONS
HEIGHT = 6.0                                                                        # = RoomTemplate.RoomCeilingHeight = Stairs.Height
GAP = 0.0005                                                                        # Fuge unter jeder Stufe: sonst beruehren sich die Keile nur an Kanten (mehr als zwei Flaechen je Kante)
STEPS = 20                                                                          # 0,5 + 0,25 * 19 = 5,25: die letzte Stufe liegt 0,25 unter dem Kapitell, wie vorher
WALL = {"a": "catacomb_wall.png", "b": "catacomb_wall_b.png", "c": "catacomb_wall_c.png"}
FLOOR = {"a": "catacomb_floor.png", "b": "catacomb_floor_b.png", "c": "catacomb_floor_c.png"}


def palette_rows():
    """Werkstein (4 Stufen) und Messing/Gold aus assets/palette/hauptpalette.json."""
    with open(PALETTE, encoding="utf-8") as f:
        data = json.load(f)
    rows = {r["name"]: r["farben"] for r in data["grund"] + data["akzent"]}
    return rows["Werkstein"], rows["Messing/Gold"]


def load(name):
    """Kachel als ganze Zahlen 0..255, Zeile 0 oben (wie das Bild aussieht)."""
    im = bpy.data.images.load(os.path.join(TEXTURES, name))
    w, h = im.size
    return np.round(np.array(im.pixels[:]).reshape(h, w, 4)[::-1, :, :3] * 255).astype(int)


def werk_of(tile, werkstein):
    """Dieselbe Kachel in den vier Stufen der Reihe Werkstein (nach der Helligkeit der Farben in Viertel geteilt)."""
    uniq = sorted({tuple(c) for c in tile.reshape(-1, 3)}, key=lambda c: np.dot(c, [0.3, 0.59, 0.11]))
    rank = {c: min(3, int(4 * i / len(uniq))) for i, c in enumerate(uniq)}
    return np.array([werkstein[rank[tuple(c)]] for c in tile.reshape(-1, 3)]).reshape(tile.shape)


def atlas(variant, werkstein, messing):
    wall, floor = load(WALL[variant]), load(FLOOR[variant])
    a = np.zeros((H, W, 3))
    for name, t in (("stone", wall), ("floor", floor), ("werk", werk_of(wall, werkstein))):
        x0, y0, x1, y1 = REGIONS[name]
        a[y0:y1, x0:x1] = np.tile(t[::-1], ((y1 - y0) // 64, (x1 - x0) // 64, 1))   # Zeile 0 unten
    x0, y0, x1, y1 = REGIONS["glow"]
    for row in range(y0, y1):                                                       # Verlauf von dunkel (unten) nach hell (oben), je 32 Zeilen
        a[row, x0:x1] = messing[3 + min(4, ((row - y0) % 32) * 5 // 32)]
    used = {tuple(p) for p in a.reshape(-1, 3).astype(int)}
    allowed = {tuple(c) for t in (wall, floor) for c in t.reshape(-1, 3)} | {tuple(c) for c in werkstein} | {tuple(c) for c in messing}
    allowed.add((0, 0, 0))                                                          # die ungenutzten Felder des Atlas
    assert used <= allowed, f"{variant}: Farben ausserhalb von Kacheln, Werkstein und Messing: {sorted(used - allowed)}"
    return a, len(used)


# --- Steinflaechen mit Welt-UV (wie die Tuerrahmen in door.py) ---------------------------------------------------------------

def emit(part, pts, hint, field, fit=False):
    """Konvexe ebene Flaeche mit Normale nach `hint`; Waende: u am Wandverlauf, v mit der Hoehe; nach oben zeigende Steinflaechen
    nehmen die Bodenkachel (u = x, v = z). Eine Kachel je Meter. Die Flaeche wird nie geteilt: laeuft sie ueber eine Metergrenze,
    liegt die Kachel im Atlas mehrfach nebeneinander (REGIONS), und die Textur-Koordinate laeuft ueber die Kopie weiter. So
    haengt jede Kante an genau zwei Dreiecken (Regel 21) und die Treppe braucht keine Schnitt-Dreiecke. `fit=True` (verdeckte
    Flaechen: Unterseite auf dem Boden, Kappen der Spindel) legt die Flaeche ohne Raster auf eine Kachel."""
    P = [V(*p) for p in pts]
    n = None
    for i in range(1, len(P) - 1):
        c = (P[i] - P[0]).cross(P[i + 1] - P[0])
        if c.length > 1e-9:
            n = c
            break
    if n is None:
        return
    if n.dot(V(*hint)) < 0:
        P.reverse()
        n = -n
    n = n.normalized()
    ax = max(range(3), key=lambda i: abs(n[i]))
    if ax == 2:
        pu, pv = (lambda p: p.x if n.z > 0 else -p.x), (lambda p: p.y)
    elif ax == 0:
        pu, pv = (lambda p: -p.z if n.x > 0 else p.z), (lambda p: p.y)
    else:
        pu, pv = (lambda p: p.x if n.y > 0 else -p.x), (lambda p: p.z)
        if n.y > 0 and field == "stone":
            field = "floor"
    x0, y0, x1, y1 = REGIONS[field]
    tw, th = TILES[field]
    us, vs = [pu(p) for p in P], [pv(p) for p in P]
    if fit:
        i0, j0 = min(us), min(vs)
        su, sv = tw / max(max(us) - i0, 1e-6), th / max(max(vs) - j0, 1e-6)
        su = sv = min(su, sv, tw)
    else:
        i0, j0 = math.floor(min(us) + 1e-9), math.floor(min(vs) + 1e-9)
        su, sv = tw, th
        assert max(us) - i0 <= (x1 - x0) / tw + 1e-9 and max(vs) - j0 <= (y1 - y0) / th + 1e-9, f"{field}: Flaeche passt nicht in die Kachelreihe"
    uvs = [((x0 + min(max((u - i0) * su, 0.02), x1 - x0 - 0.02)) / W, (y0 + min(max((v - j0) * sv, 0.02), y1 - y0 - 0.02)) / H)
           for u, v in zip(us, vs)]
    part._face([part.bm.verts.new(G(p - part.pivot)) for p in P], uvs)


def prism(part, pts, y0, y1, field, skip=(), top=True, bottom=True, fit=()):
    """Geschlossenes Prisma aus einem konvexen Grundriss (x, z); Seitenkanten in `skip` entfallen (innen liegend). `fit` nennt
    die Kappen ("top", "bottom"), die verdeckt liegen. Eine Kappe, die nur als Ring oder Faecher gebaut wird, schaltet man mit
    top=False oder bottom=False ab und baut sie selbst (ring, fan)."""
    cx, cz = sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts)
    for i, (a, b) in enumerate(zip(pts, pts[1:] + pts[:1])):
        if i in skip:
            continue
        mx, mz = (a[0] + b[0]) / 2 - cx, (a[1] + b[1]) / 2 - cz
        emit(part, [(a[0], y0, a[1]), (b[0], y0, b[1]), (b[0], y1, b[1]), (a[0], y1, a[1])], (mx, 0, mz), field)
    if top:
        emit(part, [(p[0], y1, p[1]) for p in pts], (0, 1, 0), field, "top" in fit)
    if bottom:
        emit(part, [(p[0], y0, p[1]) for p in pts], (0, -1, 0), field, "bottom" in fit)


def fan(part, pts, y, field):
    """Obere Kappe aus Dreiecken zur Mitte: jedes liegt in einer 2 x 2 Kachelreihe (ein ganzes Achteck mit mehr als 2 m passt nicht)."""
    for a, b in zip(pts, pts[1:] + pts[:1]):
        emit(part, [(0.0, y, 0.0), (a[0], y, a[1]), (b[0], y, b[1])], (0, 1, 0), field)


def ring(part, outer, inner, y, field):
    """Obere Kappe als Ring zwischen zwei gleich gedrehten Vielecken (aussen Sockel, innen die Stufe darueber)."""
    for k in range(len(outer)):
        k1 = (k + 1) % len(outer)
        emit(part, [(outer[k][0], y, outer[k][1]), (outer[k1][0], y, outer[k1][1]), (inner[k1][0], y, inner[k1][1]),
                    (inner[k][0], y, inner[k][1])], (0, 1, 0), field)


def octagon(circ, rot=22.5):
    return [(circ * math.cos(math.radians(rot + 45 * k)), circ * math.sin(math.radians(rot + 45 * k))) for k in range(8)]


def spiral():
    st, gl = Part("stairs", (0, 0, 0), UV), Part("glow", (0, 0, 0), UV)
    low, up = octagon(1.1875), octagon(1.0625)
    prism(st, low, 0.0, 0.125, "stone", top=False, fit=("bottom",))                 # Sockel, zwei Stufen: unten Ring bis zur oberen Stufe,
    ring(st, low, up, 0.125, "stone")                                               # die ihre Unterseite im Ring hat (ein Koerper)
    prism(st, up, 0.125, 0.25, "stone", top=False, bottom=False)
    fan(st, up, 0.25, "stone")
    prism(st, octagon(0.3125), 0.25, HEIGHT - 0.5, "stone", fit=("top", "bottom"))  # Spindel
    r_out, rise, n = 1.0625, 0.25, STEPS
    for i in range(n):                                                              # 20 Stufen, 45 Grad je Stufe
        a0, a1 = math.radians(45 * i), math.radians(45 * (i + 1))
        pts = [(0.0, 0.0), (r_out * math.cos(a0), r_out * math.sin(a0)), (r_out * math.cos(a1), r_out * math.sin(a1))]
        top = 0.5 + rise * i
        prism(st, pts, top - rise + GAP, top, "stone")                              # Keil bis zur Achse (innen von der Spindel verdeckt)
    prism(st, octagon(0.5), HEIGHT - 0.5, HEIGHT - 0.25, "werk")                    # Kapitell: Werkstein, nur hier
    prism(gl, octagon(0.3125), HEIGHT - 0.25, HEIGHT, "glow")                       # Lampe
    return [st, gl]


def build(variant, werkstein, messing):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.data.collections.new("Treppe")
    bpy.context.scene.collection.children.link(coll)
    name = f"stairs_spiral_{variant}"
    tex, colours = atlas(variant, werkstein, messing)
    image = save_image(name, tex, os.path.join(OUT_DIR, name + ".png"))
    mat = export_material(name, image)
    objs = [p.build(coll, mat, smooth=False) for p in spiral()]
    export_glb(objs, os.path.join(OUT_DIR, name + ".glb"))
    report_parts(name, objs)
    print(f"TEXTUR {name} {W}x{H} Farben {colours}")


def write_sources():
    """SHA-256 und Groesse jeder gelesenen Kachel (wie door_frame_sources.sha256)."""
    lines = ["# sha256 size name - the wall and floor textures tools/models/stairs.py built the stairs from"]
    for name in sorted(list(WALL.values()) + list(FLOOR.values())):
        with open(os.path.join(TEXTURES, name), "rb") as f:
            data = f.read()
        lines.append(f"{hashlib.sha256(data).hexdigest()} {len(data)} {name}")
    with open(SOURCES, "w", encoding="ascii", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("Quellen:", os.path.relpath(SOURCES, ROOT))


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    werkstein, messing = palette_rows()
    for variant in "abc":
        build(variant, werkstein, messing)
    write_sources()


main()
