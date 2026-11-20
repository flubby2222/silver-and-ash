"""Wandpfeiler (Issue #536, Baustein aus der Formenlehre Gotik #281): Pfeiler vor der Wand fuer Gang (3,5 m) und Raum (6 m).

Erzeuger fuer das Spiel. Uebergabe aus dem Probestueck design/proben/grafik/wandpfeiler-536/wandpfeiler.py (Branch
feat/wandpfeiler-probe-536): gleiche Geometrie, nur die Fassungen "raum" (hier room) und "gang" (corridor). Anders als das
Probestueck liest der Erzeuger die auf die Hauptpalette umgerechneten Waende (#542); darum hebt er die Wandstufen nur um
LIFT = 2 statt 5 und prueft, dass die Fugen sichtbar bleiben (check_joints). Die
tragende Fassung "gang_traeger" kommt mit dem Gurtbogen (#537). Erzeugt nur Modelle und Texturen nach assets/models/, keine
Probebilder, dazu die Liste der gelesenen Wand-Texturen (tools/models/wall_pillar_sources.sha256).

Aufruf (aus dem Repository-Wurzelverzeichnis), danach python tools/models/checksums.py:
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/wall_pillar.py

Alles per Skript, nichts heruntergeladen. Spiel-Achsen (x seitlich, y oben, z vorn), Meter. Ursprung: Mitte des Pfeilers
auf dem Boden, Wandflaeche z = 0, der Pfeiler steht nach +z vor; die Rueckseite liegt 1/8 m in der Wand (BACK).

Dateien: wall_pillar_<fassung>_<v>.glb und .png, Fassung room oder corridor, v die Wandvariante a, b, c. Ein Knoten
"pillar", ein Netz, rundum geschlossen. Atlas 128 x 64: links die echte Wand-Textur der Variante (bitgleich, nur gelesen),
rechts dieselbe Kachel um LIFT Stufen der Reihe Stein heller, oben gekappt (pillar_tile); der Pfeiler nutzt nur das
rechte Feld.

  room      Sockel 0 bis 0,5, Schaft bis 5,5 (Tiefe 0,25), Kapitell 5,5 bis 6,0; Sockel und Kapitell Tiefe 0,3125
  corridor  Schaft Tiefe 0,0625, Sockel und Kapitell 0,125; Kapitell 3,0 bis 3,5
Schaft 0,5 breit, Sockel und Kapitell 0,625 breit.

Textur-Koordinaten aus der Weltposition wie SurfaceTexturing (eine 64-px-Kachel je Meter, an den Metergrenzen geteilt): steht
die Pfeilermitte auf einer ganzen Meterzahl entlang der Wand (im Spiel jede Zellmitte), laufen die Fugen im Raster der Wand
weiter. Aendert sich eine Wand-Textur oder die Palette, passt der Pfeiler nicht mehr; ein Test vergleicht
wall_pillar_sources.sha256 mit assets/textures/textures.sha256 und schlaegt fehl, bis dieses Skript neu gelaufen ist.
Zwei Laeufe liefern gleiche Dateien (kein Zufall).
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
SOURCES = os.path.join(TOOLS, "wall_pillar_sources.sha256")
WALL_FILES = {"a": "catacomb_wall.png", "b": "catacomb_wall_b.png", "c": "catacomb_wall_c.png"}
LIFT = 2                                           # Stein-Stufen heller als die Wand
MIN_STEPS = 5                                      # Stein-Stufen im Pfeiler-Feld mindestens
MIN_JOINT = 2                                      # Fuge mindestens so viele Stufen dunkler als die Flaeche
FRAME_SIZE = (128, 64)
FRAME_RECTS = {"stone": (0, 0, 64, 64), "pillar": (64, 0, 128, 64)}
FRAME_UV = rects_from_pixels(FRAME_RECTS, FRAME_SIZE)
S = 0.0625                                         # 1/16

# Fassung: (Schafttiefe, Kapitell-Unterkante); Oberkante = Kapitell-Unterkante + 0,5 = Deckenhoehe
SIZES = {
    "room": (0.25, 5.5),
    "corridor": (0.0625, 3.0),
}
BACK = -0.125                                      # Rueckseite 1/8 m in der Wand


# --- Palette und Texturen --------------------------------------------------------------------------------------------

def stein_row():
    with open(PALETTE, encoding="utf-8") as f:
        data = json.load(f)
    rows = {r["name"]: np.array(r["farben"], int) for r in data["grund"] + data["akzent"]}
    return rows["Stein"]


def load_tile(name):
    """Wand-Kachel 64 x 64, Zeile 0 oben, ganze Zahlen 0..255."""
    im = bpy.data.images.load(os.path.join(TEXTURES, name))
    w, h = im.size
    a = np.array(im.pixels[:], dtype=float).reshape(h, w, 4)[..., :3] * 255.0
    bpy.data.images.remove(im)
    return np.round(a[::-1]).astype(int)


def steps_of(tile, stein):
    """Stein-Stufe (Index in der Reihe Stein) jedes Bildpunkts, flach."""
    flat = tile.reshape(-1, 3)
    return np.array([int(((stein - p) ** 2).sum(1).argmin()) for p in flat])


def pillar_tile(tile, stein):
    """Dieselbe Kachel um LIFT Stein-Stufen heller: jede Stufe der Wand rueckt um LIFT hinauf, oben an der hellsten Stufe
    gekappt. Nur die seltenen hellsten Lichter fallen zusammen; Fugen (dunkle Stufen) und Quaderflaechen (mittlere Stufen)
    behalten ihren Abstand. Die umgerechneten Waende (#542) reichen schon bis zur obersten Stufe; eine lineare Stauchung
    in den oberen Rest (wie im Probestueck mit 5 Stufen) drueckte Wand B auf drei Stufen und loeschte ihre Fugen."""
    top = len(stein) - 1
    return np.array([stein[min(i + LIFT, top)] for i in steps_of(tile, stein)]).reshape(tile.shape)


def check_joints(name, tile, lifted, stein):
    """Fugen bleiben sichtbar: das Pfeiler-Feld hat mindestens MIN_STEPS Stufen, die Fuge (10. Perzentil der Stufen) liegt
    mindestens MIN_JOINT Stufen und nicht weniger als auf der Wand unter der Flaeche (Median), und der Pfeiler ist heller."""
    wall, pillar = steps_of(tile, stein), steps_of(lifted, stein)
    count = len(set(pillar.tolist()))
    w_joint, w_face = np.percentile(wall, 10, method="lower"), np.percentile(wall, 50, method="lower")
    p_joint, p_face = np.percentile(pillar, 10, method="lower"), np.percentile(pillar, 50, method="lower")
    print(f"FUGEN {name}: Stufen {count}, Wand Fuge {w_joint} Flaeche {w_face}, Pfeiler Fuge {p_joint} Flaeche {p_face}")
    assert count >= MIN_STEPS, f"{name}: Pfeiler-Feld hat nur {count} Stein-Stufen (mindestens {MIN_STEPS})"
    assert p_face - p_joint >= max(MIN_JOINT, w_face - w_joint), \
        f"{name}: Fuge {p_joint} zu nah an der Flaeche {p_face} (Wand {w_joint}/{w_face}, mindestens {MIN_JOINT})"
    assert p_face > w_face, f"{name}: Pfeiler-Flaeche {p_face} nicht heller als die Wand {w_face}"


def atlas(variant, stein):
    tile = load_tile(WALL_FILES[variant])
    lifted = pillar_tile(tile, stein)
    check_joints(variant, tile, lifted, stein)
    w, h = FRAME_SIZE
    out = np.zeros((h, w, 3))
    for name, t in (("stone", tile), ("pillar", lifted)):
        x0, y0, x1, y1 = FRAME_RECTS[name]
        out[y0:y1, x0:x1] = t[::-1]                                 # Feld: Zeile 0 unten
    return out


def colours_of(tex):
    return len({tuple(p) for p in np.round(tex).reshape(-1, 3).astype(int)})


# --- Flaechen mit Welt-UV (wie door.py: eine Kachel je Meter, an den Metergrenzen geteilt) -----------------------------

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
    """Konvexe ebene Flaeche mit Normale nach `hint`; Textur-Koordinaten aus der Welt, an den Metergrenzen geteilt."""
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
    ax = max(range(3), key=lambda i: abs(round(n[i], 6)))
    if ax == 2:
        pu, pv = (lambda p: p.x if n.z > 0 else -p.x), (lambda p: p.y)
    elif ax == 0:
        pu, pv = (lambda p: -p.z if n.x > 0 else p.z), (lambda p: p.y)
    else:
        pu, pv = (lambda p: p.x if n.y > 0 else -p.x), (lambda p: p.z)
    poly = [(pu(p), pv(p), p) for p in P]
    i0, i1 = math.floor(min(q[0] for q in poly) + 1e-9), math.ceil(max(q[0] for q in poly) - 1e-9)
    j0, j1 = math.floor(min(q[1] for q in poly) + 1e-9), math.ceil(max(q[1] for q in poly) - 1e-9)
    u0, v0, u1, v1 = part.rects[field]
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


def G_inv(p):
    """Blender-Achsen -> Spiel-Achsen."""
    return V(p[0], p[2], -p[1])


def raw(part, pts, hint, field):
    """Verdeckte Flaeche (Rueckseite, Boden) in einem Stueck, ohne Teilung an den Metergrenzen. Liegen auf einer Kante
    Ecken der geteilten Nachbarflaechen, werden sie eingefuegt (sonst entstuende an der Teilung eine offene Kante)."""
    P = [V(*p) for p in pts]
    flip = (P[1] - P[0]).cross(P[2] - P[0]).dot(V(*hint)) < 0
    others = [G_inv(v.co) for v in part.bm.verts]
    withs = []
    for i, a in enumerate(P):
        b = P[(i + 1) % len(P)]
        withs.append(a)
        ab = b - a
        L2 = ab.dot(ab)
        if L2 < 1e-12:
            continue
        hits = []
        for q in others:
            t = (q - a).dot(ab) / L2
            if 1e-6 < t < 1 - 1e-6 and (a + ab * t - q).length < 1e-6:
                hits.append((t, q))
        for t, q in sorted(hits, key=lambda h: h[0]):
            if not withs or (withs[-1] - q).length > 1e-6:
                withs.append(q)
    P = withs
    if flip:
        P.reverse()
    u0, v0, u1, v1 = part.rects[field]
    verts = [part.bm.verts.new(G(p - part.pivot)) for p in P]
    part._face(verts, [(u0 + (u1 - u0) * 0.5, v0 + (v1 - v0) * 0.5)] * len(verts))


# --- Pfeiler ---------------------------------------------------------------------------------------------------------

def profile(cb):
    """(y, o) von unten nach oben; o = Vorsprung vor dem Schaft je Seite und nach vorn (0 oder 1/16)."""
    pts = [(0.0, S), (0.4375, S), (0.5, 0.0),                       # Sockel 0,5 hoch, oben Fase 1/16
           (cb, 0.0),                                               # Schaft
           (cb + S, S), (cb + 2 * S, S),                            # Platte unten: Fase 1/16, senkrecht 1/16
           (cb + 2 * S, 0.0), (cb + 3 * S, 0.0),                    # Kehle 1/16
           (cb + 4 * S, S), (cb + 5 * S, 0.0),                      # Wulst (Raute) 1/8
           (cb + 6 * S, S), (cb + 8 * S, S)]                        # Platte oben: Fase 1/16, senkrecht 1/8
    out = []
    for p in pts:
        if not out or out[-1] != p:
            out.append(p)
    return out


def build_pillar(size):
    """Ein Netz: Ringe (Rechtecke) entlang des Profils, rundum geschlossen, auch die Rueckseite in der Wand."""
    depth, cb = SIZES[size]
    part = Part("pillar", (0, 0, 0), FRAME_UV)
    rings = [(y, 0.25 + o, depth + o) for y, o in profile(cb)]     # y, halbe Breite, Tiefe vorn
    for (y0, h0, f0), (y1, h1, f1) in zip(rings, rings[1:]):
        dx, dy, dz = h1 - h0, y1 - y0, f1 - f0
        for s in (1, -1):                                           # Seiten
            emit(part, [(s * h0, y0, BACK), (s * h0, y0, f0), (s * h1, y1, f1), (s * h1, y1, BACK)], (s * dy, -dx, 0), "pillar")
        emit(part, [(-h0, y0, f0), (h0, y0, f0), (h1, y1, f1), (-h1, y1, f1)], (0, -dz, dy), "pillar")
    y, h, f = rings[-1]
    emit(part, [(-h, y, BACK), (h, y, BACK), (h, y, f), (-h, y, f)], (0, 1, 0), "pillar")
    right = [(h, y, BACK) for y, h, f in rings]
    left = [(-h, y, BACK) for y, h, f in reversed(rings)]
    raw(part, right + left, (0, 0, -1), "pillar")                   # Rueckseite: ein Vieleck in der Wand
    y, h, f = rings[0]
    raw(part, [(-h, y, BACK), (h, y, BACK), (h, y, f), (-h, y, f)], (0, -1, 0), "pillar")
    return part


def edge_report(part):
    """Kanten (Ecken gleicher Lage zusammengelegt): offene Kanten und Kanten mit mehr als zwei Flaechen."""
    keys, edges = {}, {}
    for f in part.bm.faces:
        ids = [keys.setdefault(tuple(round(c * 1e5) for c in v.co), len(keys)) for v in f.verts]
        for a, b in zip(ids, ids[1:] + ids[:1]):
            k = (min(a, b), max(a, b))
            edges[k] = edges.get(k, 0) + 1
    return sum(1 for n in edges.values() if n == 1), sum(1 for n in edges.values() if n > 2)


# --- Ausgabe ------------------------------------------------------------------------------------------------------

def write(size, variant, stein):
    name = f"wall_pillar_{size}_{variant}"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.data.collections.new("Pillar")
    bpy.context.scene.collection.children.link(coll)
    tex = atlas(variant, stein)
    assert colours_of(tex) <= 40, f"{name}: {colours_of(tex)} Farben (hoechstens 40)"
    image = save_image(name, tex, os.path.join(OUT_DIR, name + ".png"))
    part = build_pillar(size)
    open_edges, multi = edge_report(part)
    assert open_edges == 0 and multi == 0, f"{name}: {open_edges} offene Kanten, {multi} mehrfach"
    obj = part.build(coll, export_material(name, image), smooth=False)
    export_glb([obj], os.path.join(OUT_DIR, name + ".glb"))
    report_parts(name, [obj])
    print(f"TEXTUR {name} {FRAME_SIZE[0]}x{FRAME_SIZE[1]} Farben {colours_of(tex)}")


def write_sources():
    """SHA-256 und Groesse jeder gelesenen Wand-Textur (wie door_frame_sources.sha256)."""
    lines = ["# sha256 size name - the wall textures tools/models/wall_pillar.py built the wall pillars from"]
    for name in sorted(WALL_FILES.values()):
        with open(os.path.join(TEXTURES, name), "rb") as f:
            data = f.read()
        lines.append(f"{hashlib.sha256(data).hexdigest()} {len(data)} {name}")
    with open(SOURCES, "w", encoding="ascii", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("Quellen:", os.path.relpath(SOURCES, ROOT))


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    stein = stein_row()
    for size in SIZES:
        for variant in WALL_FILES:
            write(size, variant, stein)
    write_sources()


main()
