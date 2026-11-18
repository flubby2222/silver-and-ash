"""Treppe zur naechsten Ebene (Issue #300): die Wendeltreppe aus Stein, je Wandvariante a, b, c.

Erzeuger fuer das Spiel. Uebergabe aus dem Probestueck treppe (Branch claude/design-probe-ghul-45a8c4, Commit 6ea274a,
design/proben/grafik/treppe/treppe.py), Entwurf 1 "Wendeltreppe", den der Mensch am 2026-11-17 gewaehlt hat. Bereinigt: nur
dieser Entwurf, Kacheln aus assets/textures/ (die das Spiel zeichnet), Werkstein aus der Reihe der Hauptpalette. Erzeugt nur
Modelle und Texturen nach assets/models/, keine Probebilder, dazu die Liste der gelesenen Kacheln
(tools/models/stairs_sources.sha256).

Aufruf (aus dem Repository-Wurzelverzeichnis), danach python tools/models/checksums.py:
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/stairs.py

Alles per Skript, nichts heruntergeladen. Spiel-Achsen (x seitlich, y oben, z vorn), Meter. Ursprung: Mitte am Boden.
Die Treppe steht im Kasten von Stairs (+-1,2 m, 4 m hoch): zwei achteckige Sockelstufen, eine achteckige Spindel bis 3,5 m,
12 Stufen zu 0,25 m und 45 Grad (anderthalb Umdrehungen), ein Kapitell aus Werkstein (3,5 bis 3,75) und die Lampe (3,75 bis 4).

Je Wandvariante v eine Datei stairs_spiral_<v>.glb mit zwei Knoten, beide fest und ohne Eltern:
  stairs   der Stein: Wandkachel der Variante auf den Seiten, Bodenkachel derselben Variante auf allen nach oben zeigenden
           Flaechen, Werkstein am Kapitell.
  glow     die Lampe oben (Messing/Gold, Verlauf von dunkel nach hell); das Spiel laesst nur sie pulsieren.
Atlas 128 x 128: Wandkachel, Bodenkachel und Werkstein-Kachel (je 64 x 64 = 1 m), dazu der Messing-Verlauf. Keine neuen Farben:
nur die der beiden Kacheln, die das Spiel ohnehin zeichnet, dazu Werkstein und Messing. Weil die Kacheln im Spiel je um 24 Farben
haben, sind das zusammen gut 50 (das Probestueck hatte Kacheln mit 6 bis 8 Farben und kam auf 25); die Grenze von 32 Farben
der art-bible gilt hier deshalb nicht fuer den ganzen Atlas.

Steinflaechen bekommen ihre Textur-Koordinaten aus der Position wie SurfaceTexturing (eine 64-px-Kachel je Meter, u am
Wandverlauf, v mit der Hoehe) und sind an den Metergrenzen geteilt. Die Treppe steht auf einer Zellmitte (ungerade Meter) und
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
from psx import V, G, Part, rects_from_pixels, save_image, export_material, export_glb, report_parts  # noqa: E402

TOOLS = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(TOOLS))
OUT_DIR = os.path.join(ROOT, "assets", "models")
TEXTURES = os.path.join(ROOT, "assets", "textures")
PALETTE = os.path.join(ROOT, "assets", "palette", "hauptpalette.json")
SOURCES = os.path.join(TOOLS, "stairs_sources.sha256")

W = H = 128
RECTS = {"stone": (0, 64, 64, 128), "floor": (64, 64, 128, 128), "werk": (0, 0, 64, 64), "glow": (64, 16, 128, 48)}
UV = rects_from_pixels(RECTS, (W, H))
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
        x0, y0, x1, y1 = RECTS[name]
        a[y0:y1, x0:x1] = t[::-1]                                                   # Zeile 0 unten
    x0, y0, x1, y1 = RECTS["glow"]
    for row in range(y0, y1):                                                       # Verlauf von dunkel (unten) nach hell (oben)
        a[row, x0:x1] = messing[3 + min(4, (row - y0) * 5 // (y1 - y0))]
    used = {tuple(p) for p in a.reshape(-1, 3).astype(int)}
    allowed = {tuple(c) for t in (wall, floor) for c in t.reshape(-1, 3)} | {tuple(c) for c in werkstein} | {tuple(c) for c in messing}
    allowed.add((0, 0, 0))                                                          # die ungenutzten Felder des Atlas
    assert used <= allowed, f"{variant}: Farben ausserhalb von Kacheln, Werkstein und Messing: {sorted(used - allowed)}"
    return a, len(used)


# --- Steinflaechen mit Welt-UV (wie die Tuerrahmen in door.py) ---------------------------------------------------------------

def _clip(poly, axis, bound, keep_ge):
    out = []
    for i, cur in enumerate(poly):
        prev = poly[i - 1]
        c_in = cur[axis] >= bound - 1e-9 if keep_ge else cur[axis] <= bound + 1e-9
        p_in = prev[axis] >= bound - 1e-9 if keep_ge else prev[axis] <= bound + 1e-9
        if c_in != p_in:
            t = (bound - prev[axis]) / (cur[axis] - prev[axis])
            out.append((prev[0] + t * (cur[0] - prev[0]), prev[1] + t * (cur[1] - prev[1]), prev[2] + (cur[2] - prev[2]) * t))
        if c_in:
            out.append(cur)
    return out


def emit(part, pts, hint, field):
    """Konvexe ebene Flaeche mit Normale nach `hint`; Waende: u am Wandverlauf, v mit der Hoehe; nach oben zeigende Steinflaechen
    nehmen die Bodenkachel (u = x, v = z). Eine Kachel je Meter, an den Metergrenzen geteilt."""
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
    poly = [(pu(p), pv(p), p) for p in P]
    i0, i1 = math.floor(min(q[0] for q in poly) + 1e-9), math.ceil(max(q[0] for q in poly) - 1e-9)
    j0, j1 = math.floor(min(q[1] for q in poly) + 1e-9), math.ceil(max(q[1] for q in poly) - 1e-9)
    u0, v0, u1, v1 = UV[field]
    for i in range(i0, max(i1, i0 + 1)):
        for j in range(j0, max(j1, j0 + 1)):
            cell = _clip(_clip(_clip(_clip(poly, 0, i, True), 0, i + 1, False), 1, j, True), 1, j + 1, False)
            cell = [q for k, q in enumerate(cell) if k == 0 or abs(q[0] - cell[k - 1][0]) + abs(q[1] - cell[k - 1][1]) > 1e-7]
            if len(cell) >= 2 and abs(cell[0][0] - cell[-1][0]) + abs(cell[0][1] - cell[-1][1]) < 1e-7:
                cell = cell[:-1]
            if len(cell) < 3:
                continue
            area = sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(cell, cell[1:] + cell[:1])) / 2
            if abs(area) < 1e-6:
                continue
            verts = [part.bm.verts.new(G(q[2] - part.pivot)) for q in cell]
            uvs = [(u0 + (u1 - u0) * min(max(q[0] - i, 0), 1), v0 + (v1 - v0) * min(max(q[1] - j, 0), 1)) for q in cell]
            part._face(verts, uvs)


def prism(part, pts, y0, y1, field, skip=(), top=True):
    """Prisma aus einem konvexen Grundriss (x, z); Seitenkanten in `skip` entfallen (innen liegend)."""
    cx, cz = sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts)
    for i, (a, b) in enumerate(zip(pts, pts[1:] + pts[:1])):
        if i in skip:
            continue
        mx, mz = (a[0] + b[0]) / 2 - cx, (a[1] + b[1]) / 2 - cz
        emit(part, [(a[0], y0, a[1]), (b[0], y0, b[1]), (b[0], y1, b[1]), (a[0], y1, a[1])], (mx, 0, mz), field)
    if top:
        emit(part, [(p[0], y1, p[1]) for p in pts], (0, 1, 0), field)


def octagon(circ, rot=22.5):
    return [(circ * math.cos(math.radians(rot + 45 * k)), circ * math.sin(math.radians(rot + 45 * k))) for k in range(8)]


def spiral():
    st, gl = Part("stairs", (0, 0, 0), UV), Part("glow", (0, 0, 0), UV)
    prism(st, octagon(1.1875), 0.0, 0.125, "stone")                                 # Sockel, zwei Stufen
    prism(st, octagon(1.0625), 0.125, 0.25, "stone")
    prism(st, octagon(0.3125), 0.25, 3.5, "stone", top=False)                       # Spindel
    r_in, r_out, rise, n = 0.25, 1.0625, 0.25, 12
    for i in range(n):                                                              # 12 Stufen, 45 Grad je Stufe
        a0, a1 = math.radians(45 * i), math.radians(45 * (i + 1))
        pts = [(r_in * math.cos(a0), r_in * math.sin(a0)), (r_out * math.cos(a0), r_out * math.sin(a0)),
               (r_out * math.cos(a1), r_out * math.sin(a1)), (r_in * math.cos(a1), r_in * math.sin(a1))]
        top = 0.5 + rise * i
        prism(st, pts, top - 0.25, top, "stone", skip=(3,), top=True)
    prism(st, octagon(0.5), 3.5, 3.75, "werk")                                      # Kapitell: Werkstein, nur hier
    prism(gl, octagon(0.3125), 3.75, 4.0, "glow")                                   # Lampe
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
