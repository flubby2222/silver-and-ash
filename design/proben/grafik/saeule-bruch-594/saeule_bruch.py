"""Probestueck zerbrechende Saeule (Issue #594, Urteil #590): vier Zustaende, Bruch-Animation, Blatt.

Alles entsteht aus diesem Skript (und dem Bibliotheks-Skript saeule_basis_539.py, einer Kopie des angenommenen
Probestuecks #539). Nichts wird ins Spiel uebernommen.

Zustaende (Spiel-Achsen: x seitlich, y oben, z vorn; Meter; Ursprung Mitte der Saeule auf dem Boden):
  Z0 heil                (= Saeule aus #539, 12-Eck, Eckenhalbmesser 0,4375 im Schaft)
  Z1 nach dem 1. Treffer  ein Keil aus der Seite (Richtung 20 Grad von +x nach vorn) auf 1,2 bis 2,5 m, Schutt, Risse
  Z2 nach dem 2. Treffer  Keil bis ueber die Mitte (Taille 0,3 m), Oberteil schief, mehr Schutt, mehr Risse
  Z3 zerbrochen           Stumpf (hoechstens 0,85 m) und flacher Schutt (hoechstens 0,4 m); keine Deckenreste

Aufbau: jedes Teil ist ein konvexer, rundum geschlossener Koerper (Ringe verbinden, mit Ebenen beschneiden); die
Teile einer Saeule duerfen sich durchdringen, teilen aber nie eine Ecke (Kanten bleiben je Koerper zweiflaechig).
Die alte Steinflaeche nimmt die Textur aus #539; frische Bruchflaechen sind heller (Stein-Stufen 11 bis 13),
Schutt mittel (7 bis 11). Alle Farben stammen aus der Stein-Reihe der Hauptpalette.

Aufruf (aus dem Repository-Wurzelverzeichnis):
    blender --background --python design/proben/grafik/saeule-bruch-594/saeule_bruch.py -- [alles|modelle|ansichten|blatt|anim]
Zwei Laeufe liefern gleiche Dateien (feste Samen).
"""

import math
import os
import random
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import saeule_basis_539 as B  # noqa: E402
import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Matrix  # noqa: E402
from psx import (V, G, Part, save_image, pass_material, setup_render, camera, aim, render_passes, compose,  # noqa: E402
                 export_material, export_glb, write_light, MATS)
from bpy_extras.object_utils import world_to_camera_view  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MODE = ARGS[0] if ARGS else "alles"
QUICK = "quick" in ARGS
OUT = os.environ.get("S594_OUT", HERE)
FFMPEG = os.environ.get("FFMPEG", "ffmpeg")

N = 12
S = B.S
R = B.R_SHAFT
CB = B.CB
STEIN = B.STEIN
THETA = math.radians(20.0)
CDIR = V(math.cos(THETA), 0.0, math.sin(THETA))          # Richtung der Treffer (Keil-Seite)

# --- Atlas -----------------------------------------------------------------------------------------------------------
# #539 (216 x 384) wird rechts um 64 Spalten erweitert: "bruch" (frische Bruchflaeche) und "schutt".
AW, AH = 280, 384
FIELDS = {"bruch": (216, 0, 280, 64), "schutt": (216, 64, 280, 128)}


def field_uv(name):
    x0, y0, x1, y1 = FIELDS[name]
    return ((x0 + 1.0) / AW, (y0 + 1.0) / AH, (x1 - 1.0) / AW, (y1 - 1.0) / AH)


def draw_line(idx, x0, y0, x1, y1, core=2, shoulder=5):
    """Dunkler Riss als Linie (Stein-Stufe core, Schatten daneben); x rundum periodisch im Schaftstreifen."""
    steps = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
    for i in range(steps + 1):
        t = i / steps
        x, y = int(round(x0 + (x1 - x0) * t)), int(round(y0 + (y1 - y0) * t))
        if 0 <= y < 320:
            idx[y, x % B.STRIP] = core
            if shoulder is not None and idx[y, (x + 1) % B.STRIP] != core:
                idx[y, (x + 1) % B.STRIP] = shoulder


def crack(idx, x, y, length, drift, seed, direction=1):
    """Gezackter Riss: zieht von (x, y) um length Pixel nach oben (direction 1) oder unten (-1), mit Knicken."""
    rng = random.Random(seed)
    cx, cy = x, y
    left = length
    while left > 0:
        seg = min(left, rng.randint(5, 11))
        nx = cx + drift * seg * 0.35 + rng.randint(-3, 3)
        ny = cy + direction * seg
        draw_line(idx, cx, cy, nx, ny)
        cx, cy = int(nx), int(ny)
        left -= seg


CRACKS = {
    0: [],
    1: [(10, 118, 34, 0.5, 1, 1), (10, 52, 24, -0.4, 2, -1), (62, 150, 30, 0.2, 3, 1)],
    2: [(10, 118, 44, 0.5, 1, 1), (10, 52, 34, -0.4, 2, -1), (62, 150, 40, 0.2, 3, 1), (150, 96, 36, -0.3, 4, 1),
        (30, 210, 50, 0.4, 5, 1), (120, 60, 30, 0.3, 6, 1), (178, 188, 44, -0.5, 7, 1), (96, 250, 38, 0.2, 8, -1)],
    3: [(20, 10, 14, 0.3, 9, 1), (110, 12, 12, -0.3, 10, 1)],
}


def atlas_state(state):
    """(384, 280, 3) Farbfeld, Zeile 0 unten. Alt: der #539-Atlas, dazu Risse je Zustand und die zwei neuen Felder."""
    tex, base = B.column_atlas("a")
    idx = np.zeros((AH, AW), int) + 2
    full = np.zeros((AH, AW, 3))
    full[:, :216] = tex
    full[:, 216:] = STEIN[2]
    # Risse: im Streifen die Stufen aus dem Atlas lesen, Risse als Stufen einschreiben, zurueck in Farben
    strip = np.zeros((320, B.STRIP), int)
    for y in range(320):
        for x in range(B.STRIP):
            strip[y, x] = int(((STEIN - tex[y, x]) ** 2).sum(1).argmin())
    for (x, y, ln, drift, seed, direction) in CRACKS[state]:
        crack(strip, x, y, ln, drift, seed, direction)
    full[0:320, 0:B.STRIP] = STEIN[strip]
    dev = B.grain_of("a")
    rng = np.random.default_rng(594)
    br = 12 + np.clip(dev, -1, 1)
    for _ in range(14):                                    # kleine Poren in der frischen Flaeche
        br[rng.integers(2, 62), rng.integers(2, 62)] = 9
    full[0:64, 216:280] = STEIN[br]
    sc = 9 + dev
    for _ in range(18):
        sc[rng.integers(0, 64), rng.integers(0, 64)] = 6
    full[64:128, 216:280] = STEIN[np.clip(sc, 0, 14)]
    return full, base


# --- Koerper ---------------------------------------------------------------------------------------------------------

def ring(Rr, y, n=N):
    return [V(Rr * math.cos(2 * math.pi * k / n), y, Rr * math.sin(2 * math.pi * k / n)) for k in range(n)]


def loft_solid(rings):
    """Geschlossener Koerper aus Ringen (y, Halbmesser), wie in #539. Eine Flaeche = dict(pts, tag, hint)."""
    faces = []
    for (y0, R0), (y1, R1) in zip(rings, rings[1:]):
        field = B.field_of(y0, y1)
        a, b = ring(R0, y0), ring(R1, y1)
        dy, dR = y1 - y0, R1 - R0
        for k in range(N):
            k2 = (k + 1) % N
            quad = [a[k], a[k2], b[k2], b[k]]
            cx = sum(p.x for p in quad) / 4
            cz = sum(p.z for p in quad) / 4
            hint = V(dy * cx, -dR * math.hypot(cx, cz), dy * cz)
            faces.append(dict(pts=quad, tag=("side", k, field), hint=hint))
    top_field = "oben" if rings[-1][0] >= CB + 7 * S else "bruch"
    faces.append(dict(pts=ring(rings[-1][1], rings[-1][0]), tag=("cap", top_field), hint=V(0, 1, 0)))
    faces.append(dict(pts=ring(rings[0][1], rings[0][0]), tag=("cap", "bruch"), hint=V(0, -1, 0)))
    return faces


def box_solid(lo, hi, field="schutt"):
    x0, y0, z0 = lo
    x1, y1, z1 = hi
    c = [V(x, y, z) for z in (z0, z1) for y in (y0, y1) for x in (x0, x1)]   # index = x + 2y + 4z
    quads = [((0, 1, 3, 2), V(0, 0, -1)), ((4, 6, 7, 5), V(0, 0, 1)), ((0, 2, 6, 4), V(-1, 0, 0)),
             ((1, 5, 7, 3), V(1, 0, 0)), ((0, 4, 5, 1), V(0, -1, 0)), ((2, 3, 7, 6), V(0, 1, 0))]
    return [dict(pts=[c[i] for i in q], tag=("cap", field), hint=h) for q, h in quads]


def dedupe(pts):
    out = []
    for p in pts:
        if not out or (p - out[-1]).length > 1e-7:
            out.append(p)
    while len(out) > 1 and (out[0] - out[-1]).length <= 1e-7:
        out.pop()
    return out


def clip(faces, nrm, d, field="bruch"):
    """Behaelt n.p <= d (n beliebig lang). Die neue Schnittflaeche ist eine frische Bruchflaeche."""
    out, cut = [], []
    for f in faces:
        pts = f["pts"]
        sd = [nrm.dot(p) - d for p in pts]
        res = []
        for i in range(len(pts)):
            j = (i + 1) % len(pts)
            if sd[i] <= 0:
                res.append(pts[i])
                if abs(sd[i]) < 1e-9:
                    cut.append(pts[i])
            if (sd[i] < 0 < sd[j]) or (sd[j] < 0 < sd[i]):
                t = sd[i] / (sd[i] - sd[j])
                q = pts[i] + (pts[j] - pts[i]) * t
                res.append(q)
                cut.append(q)
        res = dedupe(res)
        if len(res) >= 3:
            out.append(dict(f, pts=res))
    uniq = []
    for q in cut:
        if not any((q - u).length < 1e-6 for u in uniq):
            uniq.append(q)
    if len(uniq) >= 3:
        n = nrm.normalized()
        c = sum(uniq, V(0, 0, 0)) / len(uniq)
        e1 = (uniq[0] - c).normalized()
        e2 = n.cross(e1)
        uniq.sort(key=lambda q: math.atan2((q - c).dot(e2), (q - c).dot(e1)))
        out.append(dict(pts=uniq, tag=("cut", field), hint=n))
    return out


def prism(y0, y1, Rr=R):
    return loft_solid([(y0, Rr), (y1, Rr)])


def convex_faces(polys):
    """Flaechen eines konvexen Koerpers aus (Punktliste, Tag); die Aussenrichtung kommt vom Schwerpunkt."""
    allp = [p for pts, _ in polys for p in pts]
    c = sum(allp, V(0, 0, 0)) / len(allp)
    out = []
    for pts, tag in polys:
        fc = sum(pts, V(0, 0, 0)) / len(pts)
        out.append(dict(pts=list(pts), tag=tag, hint=fc - c))
    return out


def shard_solid(rng, size, kind):
    """Schuttstueck mit wenigen Flaechen: 'prism' = dreieckiger Keil (8 Dreiecke, schraege Oberseite), 'pyramid' =
    Tetraeder (4 Dreiecke)."""
    hx, hy, hz = size
    base = [V(-hx, 0, -hz * rng.uniform(0.3, 0.9)), V(hx, 0, -hz * rng.uniform(0.1, 0.8)), V(hx * rng.uniform(-0.5, 0.5), 0, hz)]
    if kind == "pyramid":
        c = sum(base, V(0, 0, 0)) / 3
        apex = V(c.x + rng.uniform(-0.3, 0.3) * hx, 2 * hy, c.z + rng.uniform(-0.3, 0.3) * hz)
        a, b, d = base
        return convex_faces([([a, d, b], ("cap", "schutt")), ([a, b, apex], ("cap", "schutt")),
                             ([b, d, apex], ("cap", "schutt")), ([d, a, apex], ("cap", "schutt"))])
    ts = rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6)
    top = [V(p.x, 2 * hy * (1 + ts[0] * p.x / hx + ts[1] * p.z / hz), p.z) for p in base]
    a, b, d = base
    A, Bt, D = top
    return convex_faces([([a, d, b], ("cap", "schutt")), ([A, Bt, D], ("cut", "bruch")), ([a, b, Bt, A], ("cap", "schutt")),
                         ([b, d, D, Bt], ("cap", "schutt")), ([d, a, A, D], ("cap", "schutt"))])


def rubble(cx, cz, size, yaw, seed, cuts=2, kind="prism"):
    """Ein Schuttbrocken (flach liegend, mit der tiefsten Ecke auf dem Boden), gedreht."""
    rng = random.Random(seed)
    hx, hy, hz = size
    if kind in ("prism", "pyramid"):
        faces = shard_solid(rng, size, kind)
        cuts = 0
    else:
        faces = box_solid((-hx, -hy, -hz), (hx, hy, hz))
    for _ in range(cuts):
        n = V(rng.uniform(-1, 1), rng.uniform(0.2, 1.0), rng.uniform(-1, 1)).normalized()
        sup = abs(n.x) * hx + abs(n.y) * hy + abs(n.z) * hz
        faces = clip(faces, n, sup * rng.uniform(0.5, 0.72))
    tilt = Matrix.Rotation(math.radians(rng.uniform(-9, 9)), 3, "X") @ Matrix.Rotation(math.radians(rng.uniform(-9, 9)), 3, "Z")
    rot = Matrix.Rotation(math.radians(yaw), 3, "Y") @ tilt
    ymin = min((rot @ p).y for f in faces for p in f["pts"])
    move = V(cx, -ymin - 0.01, cz)
    return xf_faces(faces, rot, V(0, 0, 0), move)


def xf_faces(faces, rot, pivot, move):
    out = []
    for f in faces:
        g = dict(f)
        g["uvs"] = f.get("uvs") or uv_faces(f)          # UV vor dem Drehen aus den Urspruengen
        g["pts"] = [rot @ (p - pivot) + pivot + move for p in f["pts"]]
        g["hint"] = rot @ f["hint"]
        out.append(g)
    return out


# --- UV --------------------------------------------------------------------------------------------------------------

def side_uv(p, k, field):
    phi = math.degrees(math.atan2(p.z, p.x))
    d = (phi - k * 360.0 / N + 180.0) % 360.0 - 180.0
    u = (k + d / (360.0 / N)) / N
    lo, hi = {"sockel": (0.0, 0.5), "schaft": (0.5, CB), "kapitell": (CB, CB + 0.5)}[field]
    v = B.v_of(field, min(max(p.y, lo), hi))
    return (u * B.STRIP / AW, v)


def planar_uv(pts, n, field):
    u0, v0, u1, v1 = field_uv(field)
    n = n.normalized()
    if abs(n.y) > 0.7:
        e1, e2 = V(1, 0, 0), V(0, 0, 1)
    else:
        e1 = V(0, 1, 0).cross(n).normalized()
        e2 = n.cross(e1)
    a = [p.dot(e1) for p in pts]
    b = [p.dot(e2) for p in pts]
    c = sum(pts, V(0, 0, 0)) / len(pts)
    h = (math.sin(c.x * 12.9898 + c.y * 78.233 + c.z * 37.719) * 43758.5453) % 1.0
    h2 = (math.sin(c.x * 39.346 + c.y * 11.135 + c.z * 83.155) * 24634.6345) % 1.0
    ea, eb = max(a) - min(a), max(b) - min(b)
    oa, ob = h * max(0.0, 0.95 - ea), h2 * max(0.0, 0.95 - eb)
    out = []
    for x, y in zip(a, b):
        s = min(max(oa + (x - min(a)), 0.0), 1.0)
        t = min(max(ob + (y - min(b)), 0.0), 1.0)
        out.append((u0 + (u1 - u0) * s, v0 + (v1 - v0) * t))
    return out


def uv_faces(f):
    kind = f["tag"][0]
    if kind == "side":
        return [side_uv(p, f["tag"][1], f["tag"][2]) for p in f["pts"]]
    field = f["tag"][1]
    if field == "oben":
        x0, y0, x1, y1 = B.RECT_PX["oben"]
        u, v = (x0 + x1) / 2 / AW, (y0 + y1) / 2 / AH
        return [(u, v)] * len(f["pts"])
    return planar_uv(f["pts"], f["hint"], field)


# --- Zustaende -------------------------------------------------------------------------------------------------------
K_SLOPE = 0.8


def _dir(deg):
    a = math.atan2(CDIR.z, CDIR.x) + math.radians(deg)
    return V(math.cos(a), 0.0, math.sin(a))


# Runde 2: Bruchflaechen unregelmaessig. Je Zustand eine Liste (Winkel zur Trefferrichtung, Steigung, Versatz) fuer die
# Zusatzebenen des Unterteils (unten) und des Oberteils (oben); die Hauptebene bekommt einen kleinen Versatz.
FACETS = {
    1: dict(k=0.55, unten=[(32, 0.75, 0.0), (-26, 0.6, 0.0)], oben=[(-34, 0.65, 0.0), (22, 0.65, 0.0)], main_off=0.03),
    2: dict(k=0.8, unten=[(34, 0.95, 0.0)], oben=[(-32, 0.9, 0.0)], main_off=0.05),
}


def lower_planes(state, Y1):
    fc = FACETS[state]
    k = fc["k"]
    planes = [(V(k * CDIR.x, 1.0, k * CDIR.z), Y1 - fc["main_off"])]
    for deg, kf, off in fc["unten"]:
        d = _dir(deg)
        planes.append((V(kf * d.x, 1.0, kf * d.z), Y1 - off))
    return planes


def upper_planes(state, Y2):
    fc = FACETS[state]
    k = fc["k"]
    planes = [(V(k * CDIR.x, -1.0, k * CDIR.z), -Y2 - fc["main_off"])]
    for deg, kf, off in fc["oben"]:
        d = _dir(deg)
        planes.append((V(kf * d.x, -1.0, kf * d.z), -Y2 - off))
    return planes


def clip_all(faces, planes):
    for n, d in planes:
        faces = clip(faces, n, d)
    return faces


def chip_plane(deg=160, d=9.55):
    """Abgeplatzte Ecke am Kapitell: kleine Ebene mit grossem y-Anteil, trifft nur den oberen Rand."""
    dd = _dir(deg)
    return (V(dd.x, 1.6, dd.z), d)


def notch_pieces(state, Y1, Y2, rw, wr=R - 0.005):
    """Keil: unteres Teil L (Oberseite faellt nach aussen), Taille W (nur bis rw), oberes Teil U (Unterseite steigt nach
    aussen). L und U sind ganze Zwoelfecke; die Bruchflaechen entstehen durch das Beschneiden (mehrere Facetten)."""
    k = FACETS[state]["k"]
    L = clip_all(prism(0.78, Y1 + 0.55), lower_planes(state, Y1))
    W = clip(prism(Y1 - k * rw - 0.3, Y2 + k * rw + 0.3, wr), CDIR, rw)
    U = clip_all(prism(Y2 - 0.55, CB + 0.06), upper_planes(state, Y2))
    return L, W, U


def stump_solid(y_top=1.0):
    return loft_solid([(0.0, R + S), (0.4375, R + S), (0.5, R), (y_top, R)])


def base_stump(y_top=0.85):
    return loft_solid([(0.0, R + S), (0.4375, R + S), (0.5, R), (y_top, R)])


def capital():
    return loft_solid([(CB, R), (CB + S, R + S), (CB + 2 * S, R + S), (CB + 2 * S, R), (CB + 3 * S, R), (CB + 4 * S, R + S),
                       (CB + 5 * S, R), (CB + 6 * S, R + S), (CB + 8 * S, R + S)])


def whole_column():
    prof = B.profile()
    return loft_solid([(y, R + o) for y, o in prof])


def lean(faces, angle_deg, pivot_y, dx=0.0):
    rot = Matrix.Rotation(math.radians(angle_deg), 3, "Z")
    return xf_faces(faces, rot, V(0, pivot_y, 0), V(dx, 0, 0))


def stump_z3():
    f = stump_solid(1.0)
    f = clip(f, V(0.35, 1.0, 0.0), 0.72)
    f = clip(f, V(0.0, 1.0, -0.25), 0.66)
    f = clip(f, V(-0.4, 1.0, 0.3), 0.86)
    return f


Z1_Y = (1.75, 2.05, 0.18)        # kleiner Ausbruch, Saeule steht gerade
Z2_Y = (1.40, 2.60, -0.12)      # grosser Keil bis ueber die halbe Schaftdicke
Z2_LEAN = (-5.5, 0.05)


def build_state(state, split_upper=False):
    """Teile eines Zustands: dict Name -> Flaechenliste (schon mit UV und Pose). split_upper: nur fuer die Animation
    (Oberteil in zwei Stuecke geschnitten, sonst dieselbe Form)."""
    pieces = {}
    if state == 0:
        pieces["saeule"] = xf_faces(whole_column(), Matrix.Identity(3), V(0, 0, 0), V(0, 0, 0))
        return pieces
    if state in (1, 2):
        Y1, Y2, rw = Z1_Y if state == 1 else Z2_Y
        L, W, U = notch_pieces(state, Y1, Y2, rw)
        if split_upper:
            pieces["stumpf"] = xf_faces(base_stump(), Matrix.Identity(3), V(0, 0, 0), V(0, 0, 0))
            pieces["L"] = xf_faces(L, Matrix.Identity(3), V(0, 0, 0), V(0, 0, 0))
        else:       # Sockel und Unterteil in einem Stueck (spart die Doppelflaechen)
            low = clip_all(loft_solid([(0.0, R + S), (0.4375, R + S), (0.5, R), (Y1 + 0.55, R)]), lower_planes(state, Y1))
            pieces["unten"] = xf_faces(low, Matrix.Identity(3), V(0, 0, 0), V(0, 0, 0))
        pieces["W"] = xf_faces(W, Matrix.Identity(3), V(0, 0, 0), V(0, 0, 0))
        ang, dx = (0.0, 0.0) if state == 1 else Z2_LEAN
        pivot = (Y1 + Y2) / 2
        chip = [chip_plane()] if state == 2 else []
        if split_upper:
            ysplit = 3.8
            U1 = clip_all(prism(Y2 - 0.55, ysplit + 0.05), upper_planes(state, Y2))
            U2 = prism(ysplit - 0.05, CB + 0.06)
            pieces["U1"] = lean(U1, ang, pivot, dx)
            pieces["U2"] = lean(U2, ang, pivot, dx)
            pieces["K"] = lean(clip_all(capital(), chip), ang, pivot, dx)
        else:       # Oberteil und Kapitell in einem Stueck
            cap = [(CB, R), (CB + S, R + S), (CB + 2 * S, R + S), (CB + 2 * S, R), (CB + 3 * S, R), (CB + 4 * S, R + S),
                   (CB + 5 * S, R), (CB + 6 * S, R + S), (CB + 8 * S, R + S)]
            up = clip_all(loft_solid([(Y2 - 0.55, R)] + cap), upper_planes(state, Y2) + chip)
            pieces["oben"] = lean(up, ang, pivot, dx)
        for i, (cx, cz, size, yaw, seed) in enumerate(RUBBLE[state]):
            pieces["s%d" % i] = rubble(cx, cz, size, yaw, seed, kind=("prism", "pyramid")[i % 2])
        return pieces
    pieces["stumpf"] = xf_faces(stump_z3(), Matrix.Identity(3), V(0, 0, 0), V(0, 0, 0))
    for i, (cx, cz, size, yaw, seed) in enumerate(RUBBLE[3]):
        pieces["s%d" % i] = rubble(cx, cz, size, yaw, seed, kind=("prism", "pyramid")[i % 2])
    return pieces


RUBBLE = {
    1: [(0.62, 0.26, (0.1, 0.07, 0.09), 25, 1), (0.8, 0.1, (0.07, 0.05, 0.06), 70, 2)],
    2: [(0.72, 0.28, (0.2, 0.13, 0.15), 25, 11), (1.05, 0.55, (0.12, 0.09, 0.1), 60, 12), (0.5, 0.8, (0.1, 0.08, 0.1), 5, 13),
        (1.0, -0.12, (0.09, 0.07, 0.1), 80, 14), (-0.62, 0.52, (0.1, 0.07, 0.09), 40, 15),
        (1.3, 0.3, (0.13, 0.09, 0.11), 30, 16), (0.9, 0.95, (0.1, 0.07, 0.09), 65, 17)],
    3: [(0.78, 0.10, (0.19, 0.11, 0.15), 25, 21), (0.52, 0.66, (0.13, 0.09, 0.12), 70, 22), (1.02, -0.30, (0.16, 0.10, 0.2), -15, 23),
        (0.15, 0.88, (0.15, 0.1, 0.13), 40, 24), (-0.66, 0.60, (0.12, 0.08, 0.1), 10, 25), (-0.88, -0.2, (0.18, 0.11, 0.12), 55, 26),
        (-0.1, -0.84, (0.14, 0.09, 0.16), -30, 27), (0.7, -0.74, (0.11, 0.08, 0.1), 80, 28), (1.2, 0.46, (0.09, 0.06, 0.08), 0, 29)],
}
NAMES = {0: "Z0 heil", 1: "Z1 nach 1. Schlag", 2: "Z2 nach 2. Schlag", 3: "Z3 zerbrochen"}
FILES = {0: "saeule_z0_heil", 1: "saeule_z1_treffer1", 2: "saeule_z2_treffer2", 3: "saeule_z3_bruch"}


# --- In ein Teil schreiben -------------------------------------------------------------------------------------------

def emit(part, faces):
    n_tri = 0
    for f in faces:
        pts = dedupe(f["pts"])
        uvs = f["uvs"]
        if len(pts) != len(f["pts"]):
            continue
        nrm = V(0, 0, 0)
        for i in range(1, len(pts) - 1):
            nrm += (pts[i] - pts[0]).cross(pts[i + 1] - pts[0])
        if nrm.length < 1e-9:
            continue
        if nrm.dot(f["hint"]) < 0:
            pts = pts[::-1]
            uvs = uvs[::-1]
        verts = [part.bm.verts.new(G(p)) for p in pts]
        part._face(verts, uvs)
        n_tri += len(pts) - 2
    return n_tri


def state_part(state, pieces=None, name=None):
    part = Part(name or FILES[state], (0, 0, 0), {"all": (0.0, 0.0, 1.0, 1.0)})
    for faces in (pieces or build_state(state)).values():
        emit(part, faces)
    return part


def piece_report(state):
    rows = []
    for nm, faces in build_state(state).items():
        p = Part("x", (0, 0, 0), {"all": (0, 0, 1, 1)})
        t = emit(p, faces)
        rows.append((nm, t, B.edge_report(p)))
    return rows


# --- Modelle ---------------------------------------------------------------------------------------------------------
_TEX = {}


def tex_of(state):
    if state not in _TEX:
        _TEX[state] = atlas_state(state)[0]
    return _TEX[state]


def export_models():
    d = os.path.join(OUT, "modelle-r2")
    os.makedirs(d, exist_ok=True)
    report = []
    for state in range(4):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        coll = bpy.data.collections.new("Saeule")
        bpy.context.scene.collection.children.link(coll)
        tex = tex_of(state)
        assert B.only_stein(tex), "Farbe ausserhalb der Stein-Reihe"
        img = save_image(FILES[state], tex, os.path.join(d, FILES[state] + ".png"))
        part = state_part(state)
        open_e, multi = B.edge_report(part)
        tris = B.triangles(part)
        ys = [v.co.z for v in part.bm.verts]            # Blender-z = Spiel-y (oben)
        obj = part.build(coll, export_material("m", img), smooth=False)
        export_glb([obj], os.path.join(d, FILES[state] + ".glb"))
        report.append((state, tris, open_e, multi, B.colours_of(tex), max(ys) if state == 3 else None))
    for r in report:
        print("MODELL Z%d: %d Dreiecke, offene Kanten %d, mehrfach %d, Farben %d, Hoehe(Z3) %s" % r)
    for state in range(4):
        for row in piece_report(state):
            print("TEIL Z%d %s: %d Dreiecke, offen/mehrfach %s" % ((state,) + row))
    return report


# --- Szenen ----------------------------------------------------------------------------------------------------------

def img_state(state):
    key = ("z", state)
    if key not in B.IMGS:
        B.IMGS[key] = save_image("z%d" % state, tex_of(state), os.path.join(B.TMP, "z%d.png" % state))
    return B.IMGS[key]


def add_state(coll, state, x, z, pieces=None, tag=""):
    part = state_part(state, pieces, "s%d%s" % (state, tag))
    obj = part.build(coll, pass_material("m_%d%s_%g_%g" % (state, tag, x, z), img_state(state)), smooth=False)
    obj.matrix_world = Matrix.Translation(G(V(x, 0, z)))
    return [obj]


def add_gorm(coll, x, z):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(B.MODELS, "gorm.glb"))
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in objs:
        img = None
        for m in o.data.materials:
            for nd in m.node_tree.nodes:
                if nd.type == "TEX_IMAGE":
                    img = nd.image
        o.data.materials.clear()
        o.data.materials.append(pass_material("g_" + o.name, img))
        o.matrix_world = Matrix.Translation(G(V(x, 0, z))) @ o.matrix_world
        for c in list(o.users_collection):
            if c is not coll:
                c.objects.unlink(o)
        coll.objects.link(o)
    return objs


def build(items):
    coll = B.new_scene()
    objs = []
    for it in items:
        k = it[0]
        if k == "state":
            objs += add_state(coll, it[1], it[2], it[3], it[4] if len(it) > 4 else None, it[5] if len(it) > 5 else "")
        elif k == "gorm":
            objs += add_gorm(coll, it[1], it[2])
        elif k == "floor":
            objs += B.add_floor(coll)
        elif k == "ceil":
            objs += B.add_ceiling(coll)
        elif k == "wall":
            objs += B.add_wall(coll, "a", it[1], it[2], B.HEIGHT, it[3])
        elif k == "fig":
            objs += B.add_figure(coll, it[1], it[2])
    B.finish(objs)
    return objs


def shot(items, loc, tgt, fov, size, mode="hell", ortho=None, anchors=None):
    build(items)
    cam = camera()
    setup_render(*size)
    if ortho:
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = ortho
    else:
        cam.data.angle = math.radians(fov)
    aim(cam, G(V(*loc)), G(V(*tgt)))
    bpy.context.view_layer.update()
    pts = []
    for a in anchors or []:
        c = world_to_camera_view(bpy.context.scene, cam, G(V(*a)))
        pts.append((c.x * size[0], (1 - c.y) * size[1]))
    p = render_passes("s594")
    if mode == "hell":
        base, light = p["base"][..., :3] * 255, p["light"][..., :1]
        cov = p["base"][..., 3:4] > 0.5
        img = np.where(cov, np.clip(base * light * 1.3, 0, 255), B.BG)
    else:
        img = compose(p, "spiel")
    img = np.round(img[::-1])
    return (img, pts) if anchors is not None else img


def text(img, s, x, y, size=18, color=(235, 230, 240)):
    assert 8 <= size <= 40 and len(s) < 160, (size, s)
    B.text(img, s, max(0, int(x)), max(0, int(y)), size, color)


def zoom_nn(img, f):
    return np.repeat(np.repeat(img, f, axis=0), f, axis=1)


# --- Ansichten -------------------------------------------------------------------------------------------------------

def view_single(state, which, size, scale, cy, extra=None):
    items = [("floor",), ("state", state, 0.0, 0.0)] + (extra or [])
    if which == "vorn":
        return shot(items, (0, cy, 10), (0, cy, 0), 0, size, "hell", ortho=scale)
    if which == "seite":
        return shot(items, (10, cy, 0), (0, cy, 0), 0, size, "hell", ortho=scale)
    if which == "hinten":
        return shot(items, (0, cy, -10), (0, cy, 0), 0, size, "hell", ortho=scale)
    d = scale * 1.05
    return shot(items, (d * 0.7, cy + d * 0.45, d * 0.8), (0, cy * 0.9, 0), 38, size, "hell")


def four_views(state, close=False):
    size = (360, 640) if not close else (480, 480)
    scale, cy = (B.HEIGHT + 0.5, B.HEIGHT / 2) if not close else (2.6, 0.75)
    if QUICK:
        size = (size[0] // 3, size[1] // 3)
    imgs = []
    for w in ("vorn", "seite", "hinten", "schraeg-oben"):
        im = view_single(state, w, size, scale, cy)
        B.text(im, "%s: %s%s" % (NAMES[state], w, " (nah)" if close else ""), 6, 4, 16)
        imgs.append(im)
    return B.hcat(imgs)


def eye_row(dist, mode, size=(854, 480), states=(0, 1, 2, 3), spacing=2.2, gorm=False, back=-4.0):
    xs = [(i - (len(states) - 1) / 2) * spacing for i in range(len(states))]
    items = [("floor",), ("ceil",), ("wall", -30, 30, back)]
    items += [("state", s, x, 0.0) for s, x in zip(states, xs)]
    if gorm:
        items.append(("gorm", xs[-1] + spacing * 1.35, 0.0))
    return shot(items, (0.0, 1.6, dist), (0.0, 1.6, 0.0), 70, size, mode)


def sheet():
    """Blatt: vier Zustaende nebeneinander mit Gorm als Massstab; oben klar (ohne Nebel), unten Spielbedingungen."""
    states = (0, 1, 2, 3)
    sp = 2.3
    xs = [-4.6 + i * sp for i in range(4)]
    gx = xs[-1] + 2.9
    items = [("floor",), ("ceil",), ("wall", -30, 30, -4.0)]
    items += [("state", s, x, 0.0) for s, x in zip(states, xs)] + [("gorm", gx, 0.0)]
    cx = (xs[0] + gx) / 2
    size = (1800, 640) if not QUICK else (600, 214)
    anchors = [(x, 0.0, 0.0) for x in xs + [gx]]
    img, pts = shot(items, (cx, 1.6, 8.5), (cx, 2.4, 0), 45, size, "hell", anchors=anchors)
    labels = [NAMES[s] for s in states] + ["Gorm (3,1 m)"]
    for (px, py), lb in zip(pts, labels):
        text(img, lb, px - 8 * len(lb) / 2 - 4, min(py + 10, size[1] - 34), 20)
    text(img, "klar (ohne Nebel), Augenhoehe 1,6 m, 8,5 m Abstand, Saeulen 6 m hoch", 8, 6, 18)
    # Spielbedingungen: 854 x 480, 70 Grad senkrecht, Nebel, Koernung; ganzzahlig vergroessert
    g_items = items
    gsize = (854, 480) if not QUICK else (284, 160)
    g, pts2 = shot(g_items, (cx, 1.6, 10.0), (cx, 1.6, 0), 70, gsize, "spiel", anchors=anchors)
    g = zoom_nn(g, 2 if not QUICK else 2)
    for (px, py), lb in zip(pts2, [NAMES[s].split(" ")[0] for s in states] + ["Gorm"]):
        text(g, lb, px * 2 - 10, min(py * 2 + 8, g.shape[0] - 36), 22)
    text(g, "Spielbedingungen (854 x 480, 70 Grad, Nebel und Koernung), 10 m Abstand, 2-fach vergroessert", 8, 6, 18)
    w = max(img.shape[1], g.shape[1])

    def pad(a):
        return a if a.shape[1] == w else np.concatenate([a, np.tile(B.BG, (a.shape[0], w - a.shape[1], 1))], 1)
    return np.concatenate([pad(img), np.tile(B.BG * 0.5, (6, w, 1)), pad(g)], 0)


# --- Animation ---------------------------------------------------------------------------------------------------------
G_STYL = 30.0          # stilisierte Schwerkraft (m/s^2), schneller als echt: der Sturz soll in unter einer Sekunde enden
DT = 1.0 / 60.0
RELEASE = {"W": (6, V(0.45, 0, 0.15), V(0.3, 0.0, -1.5)), "L": (9, V(0.75, 0, 0.15), V(0.2, 0.2, -1.4)),
           "U1": (7, V(1.3, 0, 0.4), V(0.0, 0.4, -2.2)), "U2": (9, V(0.95, 0, 0.35), V(0.5, 0.3, -1.8)),
           "K": (12, V(1.5, 0, 0.5), V(1.2, 0.8, -3.0))}
RUCK = 6                # Ticks des Rucks: das Oberteil rutscht 5 cm zur Treffer-Seite


class Body:
    def __init__(self, name, faces):
        self.name, self.faces = name, faces
        pts = [p for f in faces for p in f["pts"]]
        self.c0 = sum(pts, V(0, 0, 0)) / len(pts)
        self.c = self.c0.copy()
        self.rot = Matrix.Identity(3)
        self.v, self.w = V(0, 0, 0), V(0, 0, 0)
        self.rest = False
        self.landed = None

    def world(self, p):
        return self.rot @ (p - self.c0) + self.c

    def ymin(self):
        return min(self.world(p).y for f in self.faces for p in f["pts"])

    def posed_faces(self):
        out = []
        for f in self.faces:
            g = dict(f)
            g["pts"] = [self.world(p) for p in f["pts"]]
            g["hint"] = self.rot @ f["hint"]
            out.append(g)
        return out


def simulate(ticks):
    """Deterministische Starrkoerper-Bahnen fuer Z2 -> Bruch. Gibt je Tick die Teile in Pose zurueck."""
    pieces = build_state(2, split_upper=True)
    static = {k: v for k, v in pieces.items() if k.startswith("s") and k != "stumpf"}
    movers = {k: Body(k, v) for k, v in pieces.items() if k in RELEASE}
    fixed = {k: v for k, v in pieces.items() if k == "stumpf"}
    history = []
    for t in range(ticks + 1):
        for nm, b in movers.items():
            rt, v0, w0 = RELEASE[nm]
            if t < rt:
                b.c = b.c0 + V(0.05 * min(t / RUCK, 1.0), 0, 0)
                b.rot = Matrix.Rotation(math.radians(-1.2 * min(t / RUCK, 1.0)), 3, "Z")
            elif t == rt:
                b.v, b.w = v0.copy(), w0.copy()
            if t >= rt and not b.rest:
                b.v.y -= G_STYL * DT
                b.c = b.c + b.v * DT
                ang = b.w.length * DT
                if ang > 1e-9:
                    b.rot = Matrix.Rotation(ang, 3, b.w.normalized()) @ b.rot
                low = b.ymin()
                if low < 0.0:
                    b.c.y += -low
                    if b.landed is None:
                        b.landed = t
                    if abs(b.v.y) > 2.0:
                        b.v = V(b.v.x * 0.5, -b.v.y * 0.2, b.v.z * 0.5)
                        b.w = b.w * 0.4
                    else:
                        b.rest = True
                        b.v, b.w = V(0, 0, 0), V(0, 0, 0)
        history.append({nm: b.posed_faces() for nm, b in movers.items()})
    return history, fixed, static, movers


def anim_frames(ticks_shown, swap_tick):
    last = max(ticks_shown)
    history, fixed, static, movers = simulate(last)
    size = (560, 640) if not QUICK else (186, 213)
    frames = []
    z3 = build_state(3)
    for t in ticks_shown:
        if t < swap_tick:
            pieces = dict(fixed)
            pieces.update(static)
            pieces.update(history[t])
            items = [("floor",), ("wall", -30, 30, -5.0), ("state", 2, 0.0, 0.0, pieces, "a%d" % t)]
        else:
            items = [("floor",), ("wall", -30, 30, -5.0), ("state", 3, 0.0, 0.0, z3, "b")]
        im = shot(items, (1.5, 2.6, 11.0), (1.5, 2.5, 0), 42, size, "hell")
        frames.append((t, im))
    return frames, movers


PHASES = [(0, "Treffer 3: Ruck"), (7, "Reissen"), (13, "Sturz"), (25, "Aufprall"), (None, "Z3: Stumpf und Schutt")]


def phase_of(t, swap):
    if t >= swap:
        return "Z3 Stumpf und Schutt (Modelltausch)"
    name = PHASES[0][1]
    for start, nm in PHASES[:-1]:
        if t >= start:
            name = nm
    return name


def write_gif(frames, swap, path, delay_cs):
    d = os.path.join(B.TMP, "gifframes")
    os.makedirs(d, exist_ok=True)
    for f in os.listdir(d):
        os.remove(os.path.join(d, f))
    for i, (t, im) in enumerate(frames):
        im = im.copy()
        text(im, "Tick %d  (%.2f s)" % (t, t / 60.0), 8, 6, 22)
        text(im, phase_of(t, swap), 8, im.shape[0] - 34, 20)
        B.png(im, os.path.join(d, "f%03d.png" % i))
    pal = os.path.join(d, "pal.png")
    rate = 100.0 / delay_cs
    subprocess.check_call([FFMPEG, "-y", "-v", "error", "-framerate", str(rate), "-i", os.path.join(d, "f%03d.png"),
                           "-vf", "palettegen=max_colors=64:stats_mode=full", pal])
    subprocess.check_call([FFMPEG, "-y", "-v", "error", "-framerate", str(rate), "-i", os.path.join(d, "f%03d.png"),
                           "-i", pal, "-lavfi", "paletteuse=dither=none", "-loop", "0", path])


def animation():
    step = 3
    ticks = list(range(0, 61, step))
    history, fixed, static, movers = simulate(90)
    landed = {nm: b.landed for nm, b in movers.items()}
    print("LANDUNG (Tick):", landed)
    swap = max(v for v in landed.values() if v is not None) + 3
    swap = int(math.ceil(swap / step) * step)
    ticks = list(range(0, swap + 4 * step + 1, step))
    frames, _ = anim_frames(ticks, swap)
    # Standbilder der Posen (Blatt)
    pick = [0, 6, 12, 21, 30, 42, swap]
    pick = [t for t in pick if t in ticks]
    strip = B.hcat([im for t, im in frames if t in pick])
    for i, t in enumerate(pick):
        B.text(strip, "Tick %d" % t, 6 + i * (frames[0][1].shape[1] + 6), 4, 20)
    B.png(strip, os.path.join(OUT, "bruch-posen-r2.png"))
    hold = [frames[-1]] * 5
    write_gif(frames + hold, swap, os.path.join(OUT, "bruch-animation-r2.gif"), 5)
    return landed, swap


def main():
    if MODE in ("alles", "modelle"):
        export_models()
    if MODE in ("alles", "ansichten"):
        for s in range(4):
            B.png(four_views(s), os.path.join(OUT, "ansichten-z%d-r2.png" % s))
        B.png(four_views(3, close=True), os.path.join(OUT, "ansichten-z3-nah-r2.png"))
        for d in (6, 10):
            B.png(eye_row(float(d), "spiel"), os.path.join(OUT, "spiel-%dm-r2.png" % d))
        B.png(eye_row(10.0, "hell"), os.path.join(OUT, "klar-10m-r2.png"))
    if MODE in ("alles", "blatt"):
        B.png(sheet(), os.path.join(OUT, "blatt-zustaende-r2.png"))
    if MODE in ("alles", "anim"):
        print("ANIMATION", animation())


main()
