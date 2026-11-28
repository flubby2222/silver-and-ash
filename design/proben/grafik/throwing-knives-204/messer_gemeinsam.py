"""Gemeinsame Bausteine der Probestuecke Throwing Knives (Issue #204): Entwurf a und b.

Wird von messer_a.py, messer_b.py (je ein Entwurf -> eine glb mit EINEM Messer) und blatt.py (Bilder, Vergleichsblatt) benutzt.
Alles per Blender-Python und NumPy, nichts heruntergeladen. Farben nur aus assets/palette/hauptpalette.json.

Aufbau der Modelle (wie die vorhandenen Waffen):
  Ein Knoten "knife", Ursprung = Klingenansatz (dort greift die Manschette, dort dreht das Messer im Flug).
  Achse +z = Spitze (Wurfrichtung), Klinge breit in y, duenn in x. Massstab 1,8 eingebacken wie bei allen Waffen in der Hand
  (Dateimass = 1,8 x Skriptmass); als Geschoss in der Welt zeichnet das Blatt dieselbe Datei mit 1/1,8, also in Skriptmass.
  Textur 128 x 128 ohne Filterung, flach schattiert, keine Hand modelliert.

Stufe Vampiric: geschwaerztes Eisen (Reihe Eisen 0-3), geschliffene Schneide in Silber (Eisen 5-7) und Blutrot (Reihe Blutrot,
Anker 3 und 5). Die Anker der anderen Stufen (Holz 3, Eisen 4, Messing 5) kommen in der Textur nicht vor.
"""

import json
import math
import os
import sys
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import blf  # noqa: E402,F401
import bmesh  # noqa: E402
import bpy  # noqa: E402
import imbuf  # noqa: E402,F401
from mathutils import Matrix, Vector  # noqa: E402,F401
from psx import (V, G, Part, frames, rects_from_pixels, vnoise, save_image, export_material,  # noqa: E402,F401
                 pose, bake_scale, export_glb)

MODELS = os.path.join(ROOT, "assets", "models")
TEX = 128
SCALE = 1.8

# --- Palette (nur Hauptpalette) ---------------------------------------------------------------------------------------

_PAL = json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8"))
_ROWS = {r["name"]: np.array(r["farben"], float) for r in _PAL["akzent"] + _PAL["grund"]}
BLACK_IRON = _ROWS["Eisen"][1:4]          # geschwaerztes Eisen (ohne den Forged-Anker Eisen 4)
DARK_IRON = _ROWS["Eisen"][0:3]
SILVER = _ROWS["Eisen"][5:8]              # Schneide: hell, kuehl - das Silber des Jaegers
BLOOD = _ROWS["Blutrot"][2:6]             # enthaelt die Vampiric-Anker Blutrot 3 (150,20,30) und 5 (200,30,40)
BLOOD_DEEP = _ROWS["Blutrot"][1:4]
LEATHER = _ROWS["Holz"][0:3]              # dunkles Leder (ohne den Worn-Anker Holz 3)

RECTS = {
    "blade_a": (0, 0, 64, 48),
    "blade_b": (64, 0, 128, 48),
    "dark": (0, 48, 64, 80),
    "silver": (64, 48, 128, 80),
    "wrap": (0, 80, 64, 128),
    "red": (64, 80, 96, 128),
    "leather": (96, 80, 128, 128),
}
UV = rects_from_pixels(RECTS, TEX)


def step(ramp, field):
    """Feld 0..1 in harten Stufen auf die Rampe legen: nur Palettenfarben, kein Mischen, kein Dithering."""
    i = np.clip((np.asarray(field) * len(ramp)).astype(int), 0, len(ramp) - 1)
    return ramp[i]


def _edge_distance(U):
    """Die Klinge ist eine Raute: Grat bei u = 0 und 0,5, Schneide bei u = 0,25 und 0,75. 0 = Schneide, 1 = Grat."""
    return np.minimum(np.abs(U - 0.25), np.abs(U - 0.75)) * 4


def blade_painter(seed, edge=0.34, fuller=None):
    """Geschwaerzter Koerper, geschliffene Silberschneide; fuller=(v0, v1, Breite): blutrote Rinne am Grat."""
    def paint(U, V_):
        h, w = U.shape
        d = _edge_distance(U)
        col = step(BLACK_IRON, 0.15 + 0.6 * vnoise(h, w, 6, 3, seed))
        col = np.where((d < edge)[..., None], step(SILVER, 0.35 + 0.5 * (1 - d / edge)), col)
        if fuller:
            v0, v1, width = fuller
            inside = (d > 1 - width) & (V_ > v0) & (V_ < v1)
            rim = (d > 1 - width - 0.08) & (V_ > v0 - 0.03) & (V_ < v1 + 0.03) & ~inside
            col = np.where(rim[..., None], DARK_IRON[0], col)
            col = np.where(inside[..., None], step(BLOOD, 0.3 + 0.65 * vnoise(h, w, 5, 2, seed + 1)), col)
        return col
    return paint


def dark_painter(U, V_):
    h, w = U.shape
    f = 0.2 + 0.6 * vnoise(h, w, 5, 5, 407)
    f = np.where(np.abs(V_ - 0.5) < 0.08, 0.95, f)                        # abgegriffene Kante
    return step(BLACK_IRON, f)


def silver_painter(U, V_):
    h, w = U.shape
    return step(SILVER, 0.3 + 0.6 * vnoise(h, w, 4, 12, 408))


def wrap_painter(U, V_):
    """Blutrote Schnurwicklung: schraege Windungen, dunkle Fugen."""
    h, w = U.shape
    turn = (V_ * 7 + U * 1.0) % 1.0
    f = np.where(turn < 0.22, 0.0, 0.35 + 0.6 * np.sin((turn - 0.22) / 0.78 * math.pi))
    return np.where((turn < 0.22)[..., None], BLOOD_DEEP[0], step(BLOOD, f))


def red_painter(U, V_):
    """Blutrotes Email (Stein, Knaufscheibe): hell in der Mitte, dunkler Rand."""
    h, w = U.shape
    r = np.hypot(U - 0.5, V_ - 0.5) * 2
    return step(BLOOD, np.clip(1.0 - 0.75 * r + 0.15 * (vnoise(h, w, 4, 4, 409) - 0.5), 0, 0.99))


def leather_painter(U, V_):
    h, w = U.shape
    turn = (V_ * 6 + U * 1.0) % 1.0
    return step(LEATHER, np.where(turn < 0.18, 0.0, 0.4 + 0.5 * vnoise(h, w, 4, 6, 410)))


def make_atlas(painters):
    atlas = np.zeros((TEX, TEX, 3)) + DARK_IRON[0]
    for name, (x0, y0, x1, y1) in RECTS.items():
        pw, ph = x1 - x0, y1 - y0
        U, V_ = np.meshgrid((np.arange(pw) + 0.5) / pw, (np.arange(ph) + 0.5) / ph)
        atlas[y0:y1, x0:x1] = painters[name](U, V_)
    return atlas


def base_painters():
    return {"blade_a": blade_painter(401), "blade_b": blade_painter(403, edge=0.30, fuller=(0.10, 0.74, 0.34)),
            "dark": dark_painter, "silver": silver_painter, "wrap": wrap_painter, "red": red_painter,
            "leather": leather_painter}


# --- Geometrie --------------------------------------------------------------------------------------------------------

def circle(n, phase=0.0):
    return [(math.cos(phase + 2 * math.pi * k / n), math.sin(phase + 2 * math.pi * k / n)) for k in range(n)]


PROF_RECT = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
PROF_DIAMOND = [(-1, 0), (0, -1), (1, 0), (0, 1)]          # Grat links, Schneide unten, Grat rechts, Schneide oben


def sweep(part, centres, sizes, rect, profile=None, up=V(0, 1, 0)):
    """Geschlossener Koerper um eine Mittellinie: je Mittelpunkt ein Ring (Vieleck), Kappen als ebene Vielecke.
    sizes[i] = (halbe Breite, halbe Hoehe); profile: Liste (a, b) im Einheitsquadrat (Standard: Rechteck)."""
    profile = profile or PROF_RECT
    n = len(profile)
    u0, v0, u1, v1 = part.rects[rect]
    rings = []
    for (axis, side, upv), c, (w, h) in zip(frames([V(*p) for p in centres], up), [V(*p) for p in centres], sizes):
        rings.append([part.bm.verts.new(G(c + side * (w * a) + upv * (h * b) - part.pivot)) for a, b in profile])
    count = len(rings)
    faces = []
    for i in range(count - 1):
        for k in range(n):
            q = (rings[i][k], rings[i][(k + 1) % n], rings[i + 1][(k + 1) % n], rings[i + 1][k])
            uvs = ((u0 + (u1 - u0) * k / n, v0 + (v1 - v0) * i / (count - 1)),
                   (u0 + (u1 - u0) * (k + 1) / n, v0 + (v1 - v0) * i / (count - 1)),
                   (u0 + (u1 - u0) * (k + 1) / n, v0 + (v1 - v0) * (i + 1) / (count - 1)),
                   (u0 + (u1 - u0) * k / n, v0 + (v1 - v0) * (i + 1) / (count - 1)))
            faces.append(part._face(q, uvs))
    for ring in (rings[0], list(reversed(rings[-1]))):
        uvs = [(u0 + (u1 - u0) * (0.5 + 0.4 * a), v0 + (v1 - v0) * (0.5 + 0.4 * b)) for a, b in profile]
        faces.append(part._face(ring, uvs if ring is rings[0] else list(reversed(uvs))))
    bmesh.ops.recalc_face_normals(part.bm, faces=faces)
    return faces


def rod(part, p0, p1, r, rect, sides=6, r1=None, up=V(0, 1, 0)):
    """Runder Stab (Vieleck) von p0 nach p1, an den Enden geschlossen."""
    return sweep(part, [p0, p1], [(r, r), (r if r1 is None else r1, r if r1 is None else r1)], rect, circle(sides), up)


def disc_x(part, c, r, half, rect, sides=8):
    """Scheibe quer (Achse x), Mittelpunkt c, halbe Dicke half: liegt in der Klingenebene."""
    return sweep(part, [(c[0] - half, c[1], c[2]), (c[0] + half, c[1], c[2])], [(r, r)] * 2, rect,
                 circle(sides, math.pi / sides), up=V(0, 1, 0))


def ring_yz(part, c, radius, thick, rect, segments=10):
    """Geschlossener Ring (Torus mit Rautenquerschnitt) in der Klingenebene y-z um den Mittelpunkt c."""
    u0, v0, u1, v1 = part.rects[rect]
    c = V(*c)
    verts = []
    for i in range(segments):
        th = 2 * math.pi * i / segments
        radial = V(0, math.sin(th), math.cos(th))
        verts.append([part.bm.verts.new(G(c + radial * (radius + thick * a) + V(1, 0, 0) * (thick * b) - part.pivot))
                      for a, b in PROF_DIAMOND])
    faces = []
    for i in range(segments):
        a, b = verts[i], verts[(i + 1) % segments]
        for k in range(4):
            uvs = ((u0 + (u1 - u0) * k / 4, v0 + (v1 - v0) * i / segments),
                   (u0 + (u1 - u0) * (k + 1) / 4, v0 + (v1 - v0) * i / segments),
                   (u0 + (u1 - u0) * (k + 1) / 4, v0 + (v1 - v0) * (i + 1) / segments),
                   (u0 + (u1 - u0) * k / 4, v0 + (v1 - v0) * (i + 1) / segments))
            faces.append(part._face((a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k]), uvs))
    bmesh.ops.recalc_face_normals(part.bm, faces=faces)
    return faces


def closed_report(parts):
    """Vor dem Bauen: jede Kante gehoert zu genau zwei Flaechen? (offen, mehr als zwei, lose, Kanten)"""
    out = [0, 0, 0, 0]
    for p in parts:
        bm = p.bm
        out[0] += sum(1 for e in bm.edges if len(e.link_faces) == 1)
        out[1] += sum(1 for e in bm.edges if len(e.link_faces) > 2)
        out[2] += sum(1 for e in bm.edges if len(e.link_faces) == 0)
        out[3] += len(bm.edges)
    return out


def export_design(key, label, fname, builder):
    """builder() -> Part "knife". Schreibt <fname> und die Textur als PNG neben das Skript."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    knife = builder()
    open_e, multi, loose, edges = closed_report([knife])
    print(f"PRUEFUNG {label}: Kanten {edges}, offene Kanten {open_e}, Kanten mit mehr als zwei Flaechen {multi}, lose Kanten {loose}")
    assert open_e == 0 and multi == 0 and loose == 0, "Modell nicht geschlossen"
    tex = make_atlas(base_painters())
    colors = len({tuple(c) for c in tex.reshape(-1, 3)})
    assert colors <= 32, colors
    palette = {tuple(c) for row in _ROWS.values() for c in row}
    assert all(tuple(c) in palette for c in {tuple(c) for c in tex.reshape(-1, 3)}), "Farbe ausserhalb der Hauptpalette"
    image = save_image("messer_" + key, tex, os.path.join(HERE, "textur.png"))
    coll = bpy.data.collections.new("Messer")
    bpy.context.scene.collection.children.link(coll)
    mat = export_material("messer_" + key, image)
    objs = [knife.build(coll, mat, smooth=False)]
    tris = len(objs[0].data.polygons)
    pose(objs, {}, root=Matrix.Scale(SCALE, 4))
    bake_scale(objs)
    export_glb(objs, os.path.join(HERE, fname))
    co = [v.co for v in objs[0].data.vertices]
    print(f"MODELL {label}: {tris} Dreiecke je Messer, {colors} Farben, Textur {TEX}x{TEX}, "
          f"Laenge {(max(c.y for c in co) - min(c.y for c in co)) / SCALE:.3f} m, "
          f"Breite {(max(c.z for c in co) - min(c.z for c in co)) / SCALE:.3f} m (Spielmass, Datei 1,8-fach)")
    return tris


def new_knife():
    return Part("knife", (0, 0, 0), UV)
