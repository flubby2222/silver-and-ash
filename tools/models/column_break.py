"""Zerbrechende Saeule fuer das Spiel (Issue #601): vier Zustaende, das fallende Modell und die Bahnen seiner Teile.

Erzeuger fuer das Spiel. Uebergabe aus dem abgenommenen Probestueck design/proben/grafik/saeule-bruch-594/saeule_bruch_r3.py
auf dem Branch feat/saeule-bruch-probe-594 (Stand cd7cb9e, Urteil in #594: Z0 bis Z3 "passt so", Fall "passt", Staub
weicher). Geometrie, Atlanten, Bahnen und Zeitplan sind unveraendert; geaendert sind nur:
  - die Dateinamen und Teilnamen (englisch) und der Zielordner assets/models/,
  - neu: das fallende Modell column_falling.glb (Z2 in die bewegten Stuecke geteilt, wie in der Animation des Probestuecks)
    und die Datei src/SilverAndAsh.Core/Presentation/ColumnFallTable.cs mit Lage und Drehung jedes Stuecks je Tick, dem
    Tick des Modelltauschs und den Staub-Anlaessen (Tick, Ort, Groesse). Den Staub selbst zeichnet das Spiel als weiche
    Wolke (ColumnDust), nicht mehr als kantige Oktaeder.
Bibliothek: tools/models/column_base.py (Kopie von saeule_basis_539.py, Saeule #539).

Zustaende (Spiel-Achsen: x seitlich, y oben, z vorn; Meter; Ursprung Mitte der Saeule auf dem Boden; 6 m hoch):
  column_intact   Z0 heil                (= Saeule aus #539, 12-Eck, Eckenhalbmesser 0,4375 im Schaft)
  column_hit1     Z1 nach dem 1. Treffer  ein Keil aus der Seite (Richtung 20 Grad von +x nach vorn), Schutt, Risse
  column_hit2     Z2 nach dem 2. Treffer  Keil bis ueber die Mitte (Taille 0,3 m), Oberteil schief, mehr Schutt, mehr Risse
  column_broken   Z3 zerbrochen           Stumpf (hoechstens 0,85 m) und flacher Schutt (hoechstens 0,4 m)
  column_falling  Z2 in Teilen: base (Stumpf und Schutt, steht), waist, lower, upper_1, upper_2, capital (je mit dem
                  Schwerpunkt seiner Ecken als Drehpunkt, wie die Koerper der Simulation)

Aufbau: jedes Teil ist ein konvexer, rundum geschlossener Koerper (Ringe verbinden, mit Ebenen beschneiden); die
Teile einer Saeule duerfen sich durchdringen, teilen aber nie eine Ecke (Kanten bleiben je Koerper zweiflaechig).
Die alte Steinflaeche nimmt die Textur aus #539; frische Bruchflaechen sind heller (Stein-Stufen 11 bis 13),
Schutt mittel (7 bis 11). Alle Farben stammen aus der Stein-Reihe der Hauptpalette.

Aufruf (aus dem Repository-Wurzelverzeichnis), danach python tools/models/checksums.py und
python tools/models/check_closed.py:
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/column_break.py
Die Probebilder des Probestuecks (Modi ansichten|blatt|anim, nach -- angehaengt) landen in einem Temp-Ordner.
Zwei Laeufe liefern gleiche Dateien (feste Samen).
"""

import math
import os
import random
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import column_base as B  # noqa: E402
import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Matrix  # noqa: E402
from psx import (V, G, Part, save_image, pass_material, setup_render, camera, aim, render_passes, compose,  # noqa: E402
                 export_material, export_glb, write_light, MATS)
from bpy_extras.object_utils import world_to_camera_view  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MODE = ARGS[0] if ARGS else "modelle"
QUICK = "quick" in ARGS
OUT = os.environ.get("S594_OUT", os.path.join(B.TMP, "bilder"))
MODELS = B.MODELS
TABLE = os.path.join(B.ROOT, "src", "SilverAndAsh.Core", "Presentation", "ColumnFallTable.cs")
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


def crack(idx, x, y, length, drift, seed, direction=1, thin=False, branch=True):
    """Gezackter Riss: zieht von (x, y) um length Pixel nach oben (direction 1) oder unten (-1), mit Knicken. Runde 3:
    duenne Risse (nur ein Pixel) und kurze Seitenaeste, die an einem Knick abzweigen."""
    rng = random.Random(seed)
    cx, cy = x, y
    left = length
    sh = None if thin else 5
    while left > 0:
        seg = min(left, rng.randint(4, 9))
        nx = cx + drift * seg * 0.35 + rng.randint(-3, 3)
        ny = cy + direction * seg
        draw_line(idx, cx, cy, nx, ny, shoulder=sh)
        if branch and seg >= 6 and rng.random() < 0.45:
            bx = nx + rng.choice((-1, 1)) * rng.randint(5, 9)
            draw_line(idx, nx, ny, bx, ny + direction * rng.randint(3, 6), shoulder=None)
        cx, cy = int(nx), int(ny)
        left -= seg


def _row(y):
    """Texturzeile (0 = Schaftanfang bei 0,5 m) zu einer Hoehe in Metern; 64 Zeilen je Meter."""
    return int(round((y - 0.5) * 64))


def cracks_for(state):
    """Risse, die an der Bruchkante beginnen (oberhalb der Oberkante nach oben, unterhalb der Unterkante nach unten),
    dazu feine Haarrisse. Spalten: Treffer-Richtung liegt bei etwa Spalte 10 (20 Grad), 16 Spalten je Seitenflaeche."""
    if state == 0:
        return []
    if state == 3:
        return [(20, _row(0.62), 12, 0.3, 9, 1, False), (110, _row(0.6), 10, -0.3, 10, 1, False),
                (150, _row(0.64), 9, 0.2, 40, 1, True)]
    Y1, Y2, _ = Z1_Y if state == 1 else Z2_Y
    k = FACET_SPEC[state]["k"]
    lo, up = _row(Y1 - k * R), _row(Y2 + k * R)
    big = state == 2
    out = []
    xs = (2, 10, 18, 27, 36, 187, 178) if big else (4, 12, 22, 33)
    for i, x in enumerate(xs):
        out.append((x % B.STRIP, up - 2, 18 + 9 * (i % 3) + (10 if big else 0), 0.3 * (-1) ** i, 100 + i, 1, i % 3 == 2))
        out.append(((x + 5) % B.STRIP, lo + 2, 12 + 8 * (i % 2) + (8 if big else 0), -0.3 * (-1) ** i, 200 + i, -1, i % 3 != 0))
    for i in range(7 if big else 3):                  # Haarrisse in der Naehe, nicht am Bruch
        out.append(((16 * i + 41) % B.STRIP, _row(Y2 + 0.1 + 0.35 * i), 22 + 4 * i, 0.5 * (-1) ** i, 300 + i, 1, True))
    return out


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
    for (x, y, ln, drift, seed, direction, thin) in cracks_for(state):
        crack(strip, x, y, ln, drift, seed, direction, thin)
    full[0:320, 0:B.STRIP] = STEIN[strip]
    dev = B.grain_of("a")
    rng = np.random.default_rng(594)
    # Runde 3: Bruchflaeche rauh statt glatt: grobe Flecken (8 x 8 Zellen), Poren, helle Kanten-Splitter, Adern
    coarse = np.kron(rng.integers(-2, 2, (8, 8)), np.ones((8, 8), int))
    br = np.clip(10 + coarse + np.clip(dev, -1, 1), 7, 13)
    for _ in range(46):
        br[rng.integers(1, 63), rng.integers(1, 63)] = rng.choice((7, 8, 8, 13))
    for _ in range(4):                                     # Adern: dunkle, gezackte Linien quer ueber das Feld
        x, y = int(rng.integers(0, 64)), int(rng.integers(0, 64))
        for _ in range(int(rng.integers(14, 30))):
            br[y % 64, x % 64] = 7
            x += int(rng.integers(-1, 2)) + 1
            y += int(rng.integers(-1, 2))
    full[0:64, 216:280] = STEIN[br]
    sc = 9 + dev
    for _ in range(18):
        sc[rng.integers(0, 64), rng.integers(0, 64)] = 6
    full[64:128, 216:280] = STEIN[np.clip(sc, 0, 14)]
    return full, base


# --- Koerper ---------------------------------------------------------------------------------------------------------

def ring(Rr, y, n=N):
    return [V(Rr * math.cos(2 * math.pi * k / n), y, Rr * math.sin(2 * math.pi * k / n)) for k in range(n)]


def dented_ring(Rr, y, dents, n=N):
    """Ring, in dem einzelne Ecken nach innen und in der Hoehe verschoben sind (abgeplatzte Trommelkanten).
    dents: {(runde(y, 4), k): (nach_innen, dy)}"""
    pts = ring(Rr, y, n)
    hit = set()
    for k in range(n):
        d = dents.get((round(y, 4), k))
        if d:
            inn, dy = d
            p = pts[k]
            s = (Rr - inn) / Rr
            pts[k] = V(p.x * s, p.y + dy, p.z * s)
            hit.add(k)
    return pts, hit


def loft_solid(rings, dents=None):
    """Geschlossener Koerper aus Ringen (y, Halbmesser), wie in #539. Eine Flaeche = dict(pts, tag, hint).
    dents (Runde 3): Ecken, die an einem Ring eingedrueckt sind; die beruehrten Vierecke werden zu zwei Dreiecken."""
    dents = dents or {}
    ends = {round(rings[0][0], 4), round(rings[-1][0], 4)}
    dents = {key: v for key, v in dents.items() if key[0] not in ends}      # Endringe bleiben glatt (Deckel)
    faces = []
    for (y0, R0), (y1, R1) in zip(rings, rings[1:]):
        field = B.field_of(y0, y1)
        a, ha = dented_ring(R0, y0, dents)
        b, hb = dented_ring(R1, y1, dents)
        dy, dR = y1 - y0, R1 - R0
        for k in range(N):
            k2 = (k + 1) % N
            quad = [a[k], a[k2], b[k2], b[k]]
            cx = sum(p.x for p in quad) / 4
            cz = sum(p.z for p in quad) / 4
            hint = V(dy * cx, -dR * math.hypot(cx, cz), dy * cz)
            hit_a = {k, k2} & ha
            hit_b = {k, k2} & hb
            if not hit_a and not hit_b:
                faces.append(dict(pts=quad, tag=("side", k, field), hint=hint))
                continue
            # Diagonale durch die eingedrueckte Ecke: Dreieck-Paare mit sichtbarem Knick
            if (k in ha) or (k2 in hb):
                tris = [[a[k], a[k2], b[k2]], [a[k], b[k2], b[k]]]
            else:
                tris = [[a[k], a[k2], b[k]], [a[k2], b[k2], b[k]]]
            for t in tris:
                faces.append(dict(pts=t, tag=("side", k, field), hint=hint))
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


def prism(y0, y1, Rr=R, dents=None, mids=()):
    """Zwoelfeck-Saeule von y0 bis y1; mids = zusaetzliche Ringe (Trommelfugen) dazwischen."""
    return loft_solid([(y0, Rr)] + [(m, Rr) for m in mids if y0 < m < y1] + [(y1, Rr)], dents)


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
# Runde 3: "wie herausgesaegt" war der Befund. Jetzt bestehen Ober- und Unterkante des Bruchs aus 4 bis 5 schraegen
# Facetten mit eigener Steigung, eigener Hoehe und eigener Richtung; die Rueckwand der Taille ist keine senkrechte
# Platte mehr, sondern ein flacher Grat aus vier geneigten Ebenen. Die Zahlenwerte kommen aus einem festen Samen;
# find_facets() nimmt den ersten Samen, bei dem alle Teile geschlossen sind und jede Facette sichtbar bleibt.
FACET_SPEC = {   # Zustand -> (Hauptsteigung k, Facetten unten, Facetten oben, Steigungsbereich)
    1: dict(k=0.60, n_low=4, n_up=4, slope=(0.25, 1.45)),
    2: dict(k=0.80, n_low=5, n_up=5, slope=(0.25, 1.60)),
}
DENTS_LOW = {(0.5, 1): (0.08, -0.09), (0.5, 2): (0.10, -0.12), (0.5, 3): (0.07, -0.05),
             (1.0, 2): (0.08, 0.09), (1.0, 3): (0.10, -0.12), (1.0, 1): (0.06, -0.08)}
DENTS_UP = {(3.5, 2): (0.05, -0.07), (3.5, 3): (0.065, 0.06), (3.5, 4): (0.04, -0.05),
            (CB, 3): (0.09, -0.10), (CB, 2): (0.07, -0.05), (CB, 4): (0.07, -0.08),
            (round(CB + S, 4), 3): (0.04, -0.04)}
MIDS_LOW = (1.0,)
MIDS_UP = ()
_FACET_CACHE = {}


CAP_RINGS = [(CB, R), (CB + S, R + S), (CB + 2 * S, R + S), (CB + 2 * S, R), (CB + 3 * S, R), (CB + 4 * S, R + S),
             (CB + 5 * S, R), (CB + 6 * S, R + S), (CB + 8 * S, R + S)]


def low_body(Y1):
    """Sockel und Schaft bis Y1+0,55 in einem Stueck (vor dem Beschneiden)."""
    return loft_solid([(0.0, R + S), (0.4375, R + S), (0.5, R)] + [(m, R) for m in MIDS_LOW] + [(Y1 + 0.55, R)], DENTS_LOW)


def up_body(Y2):
    """Oberteil und Kapitell in einem Stueck (vor dem Beschneiden)."""
    return loft_solid([(Y2 - 0.55, R)] + [(m, R) for m in MIDS_UP] + CAP_RINGS, DENTS_UP)


def _is_closed(faces):
    p = Part("x", (0, 0, 0), {"all": (0, 0, 1, 1)})
    for f in faces:
        if "uvs" not in f:
            f = dict(f, uvs=uv_faces(f))
        emit(p, [f])
    return B.edge_report(p) == (0, 0)


def _facet_list(seed, n, spread, slope):
    rng = random.Random(seed)
    out = []
    for i in range(n):
        base = -spread + 2 * spread * (i + 0.5) / n
        out.append((base + rng.uniform(-11, 11), rng.uniform(*slope), rng.uniform(-0.18, 0.26)))
    return out


def find_facets(state, which):
    """(Facettenliste, Samen) fuer 'unten' oder 'oben'; Pruefung: Teil geschlossen, jede Facette erzeugt eine Flaeche."""
    key = (state, which)
    if key in _FACET_CACHE:
        return _FACET_CACHE[key]
    sp = FACET_SPEC[state]
    Y1, Y2, _rw = Z1_Y if state == 1 else Z2_Y
    n = sp["n_low"] if which == "unten" else sp["n_up"]
    for seed in range(1, 400):
        fl = _facet_list(seed * 7 + (3 if which == "unten" else 11) + state, n, 78, sp["slope"])
        ok = True
        if which == "unten":
            variants = [(low_body(Y1), _planes(sp["k"], Y1, fl, +1)),
                        (prism(0.78, Y1 + 0.55, mids=MIDS_LOW, dents=DENTS_LOW), _planes(sp["k"], Y1, fl, +1))]
        else:
            variants = [(up_body(Y2), _planes(sp["k"], Y2, fl, -1) + [chip_plane()]),
                        (prism(Y2 - 0.55, 3.85, mids=MIDS_UP, dents=DENTS_UP), _planes(sp["k"], Y2, fl, -1))]
        for body, pl in variants:
            res = clip_all(body, pl)
            cuts = sum(1 for f in res if f["tag"][0] == "cut")
            ok = ok and cuts >= n + 1 and _is_closed(res)
        if ok:
            _FACET_CACHE[key] = (fl, seed)
            return _FACET_CACHE[key]
    raise RuntimeError("keine geschlossene Facettenfolge %s" % (key,))


def _planes(k, Y, facets, sign):
    """sign +1: Unterteil (y + k*x <= Y - off), sign -1: Oberteil (-y + k*x <= -Y - off)."""
    planes = [(V(k * CDIR.x, sign, k * CDIR.z), sign * Y)]
    for deg, kf, off in facets:
        d = _dir(deg)
        planes.append((V(kf * d.x, sign, kf * d.z), sign * (Y - off) if sign > 0 else -(Y + off)))
    return planes


def lower_planes(state, Y1):
    return _planes(FACET_SPEC[state]["k"], Y1, find_facets(state, "unten")[0], +1)


def upper_planes(state, Y2):
    return _planes(FACET_SPEC[state]["k"], Y2, find_facets(state, "oben")[0], -1)


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
    k = FACET_SPEC[state]["k"]
    L = clip_all(prism(0.78, Y1 + 0.55, mids=MIDS_LOW, dents=DENTS_LOW), lower_planes(state, Y1))
    W = clip_all(prism(Y1 - k * rw - 0.3, Y2 + k * rw + 0.3, wr), wall_planes(rw, (Y1 + Y2) / 2))
    U = clip_all(prism(Y2 - 0.55, 3.85, mids=MIDS_UP, dents=DENTS_UP), upper_planes(state, Y2))
    return L, W, U


def wall_planes(rw, yc):
    """Rueckwand der Taille: kein senkrechter Schnitt, sondern ein flacher Grat aus geneigten Ebenen (oben/unten
    zuruecktretend, seitlich in zwei Richtungen abgeschraegt)."""
    t = 0.28
    pl = [(V(CDIR.x, t, CDIR.z), rw + t * yc), (V(CDIR.x, -t, CDIR.z), rw - t * yc)]
    for deg, tt, dd in ((44, 0.18, 0.10), (-40, -0.15, 0.13)):
        d = _dir(deg)
        pl.append((V(d.x, tt, d.z), rw + dd + tt * yc))
    return pl


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


STUMP_DENTS = {(0.5, 0): (0.05, -0.05), (0.5, 2): (0.06, -0.06), (0.5, 3): (0.045, -0.04), (0.5, 5): (0.05, -0.05),
               (0.5, 8): (0.04, -0.03)}


def stump_z3():
    """Stumpf mit gezackter Bruchkante: Hauptstumpf (schraeg abgebrochen, Hoehe 0,5 bis 0,74 m) plus ein stehengebliebener
    Zahn (bis 0,84 m) auf der Rueckseite; beide mit mehreren Facetten, sie durchdringen sich. Die Facettenzahlen
    wackeln leicht je Samen; genommen wird der erste Samen mit geschlossenem Koerper."""
    base = ((V(0.9, 1.0, 0.15), 0.72), (V(-0.6, 1.0, 1.0), 0.70), (V(0.1, 1.0, -1.0), 0.60),
            (V(-1.1, 1.0, -0.2), 0.58), (V(0.5, 1.0, 1.2), 0.56))
    for seed in range(0, 300):
        rng = random.Random(seed)
        f = loft_solid([(0.0, R + S), (0.4375, R + S), (0.5, R), (0.95, R)], STUMP_DENTS)
        for n, d in base:
            j = (0.0, 0.0, 0.0) if seed == 0 else (rng.uniform(-.06, .06), 0, rng.uniform(-.06, .06))
            f = clip(f, V(n.x + j[0], 1.0, n.z + j[2]), d + (0.0 if seed == 0 else rng.uniform(-.03, .03)))
        if _is_closed(f):
            return f
    raise RuntimeError("Stumpf nicht geschlossen")


def stump_tooth():
    """Zahn: schmales, hohes Stueck am Rand, oben schraeg und zweifach angeschnitten."""
    f = loft_solid([(0.4, R + 0.006), (0.84, R + 0.006)])
    f = clip(f, V(0.80, 0.0, 0.45), -0.12)            # nur die Rueckseite (Richtung -x, -z)
    f = clip(f, V(-0.5, 0.0, 0.87), 0.16)             # schmal: Zahn, kein Block
    f = clip(f, V(0.5, 0.0, -0.87), 0.12)
    for n, d in ((V(-0.8, 1.0, 1.39), 0.82), (V(0.8, 1.0, -1.39), 0.82), (V(0.9, 1.0, 0.5), 0.40)):
        f = clip(f, n, d)
    return f


Z1_Y = (1.78, 2.04, 0.22)        # kleiner Ausbruch, Saeule steht gerade
Z2_Y = (1.50, 2.50, 0.09)        # tiefer Keil (etwa 40 Prozent der Schaftdicke), Oberteil leicht geneigt
Z2_LEAN = (-3.5, 0.02)


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
            low = clip_all(low_body(Y1), lower_planes(state, Y1))
            pieces["unten"] = xf_faces(low, Matrix.Identity(3), V(0, 0, 0), V(0, 0, 0))
        pieces["W"] = xf_faces(W, Matrix.Identity(3), V(0, 0, 0), V(0, 0, 0))
        ang, dx = (0.0, 0.0) if state == 1 else Z2_LEAN
        pivot = (Y1 + Y2) / 2
        chip = [chip_plane()] if state == 2 else []
        if split_upper:
            ysplit = 3.8
            U1 = clip_all(prism(Y2 - 0.55, ysplit + 0.05, mids=MIDS_UP, dents=DENTS_UP), upper_planes(state, Y2))
            U2 = prism(ysplit - 0.05, CB + 0.06)
            pieces["U1"] = lean(U1, ang, pivot, dx)
            pieces["U2"] = lean(U2, ang, pivot, dx)
            pieces["K"] = lean(clip_all(capital(), chip), ang, pivot, dx)
        else:       # Oberteil und Kapitell in einem Stueck
            up = clip_all(up_body(Y2), upper_planes(state, Y2) + chip)
            pieces["oben"] = lean(up, ang, pivot, dx)
        for i, (cx, cz, size, yaw, seed, kind) in enumerate(RUBBLE[state]):
            pieces["s%d" % i] = rubble(cx, cz, size, yaw, seed, kind=kind)
        return pieces
    pieces["stumpf"] = xf_faces(stump_z3(), Matrix.Identity(3), V(0, 0, 0), V(0, 0, 0))
    pieces["zahn"] = xf_faces(stump_tooth(), Matrix.Identity(3), V(0, 0, 0), V(0, 0, 0))
    for i, (cx, cz, size, yaw, seed, kind) in enumerate(RUBBLE[3]):
        pieces["s%d" % i] = rubble(cx, cz, size, yaw, seed, kind=kind)
    return pieces


P_, Y_ = "prism", "pyramid"
# (x, z, Groesse, Drehung, Samen, Art): grosse Brocken, mittlere Splitter und winzige Spitzen gemischt
RUBBLE = {
    1: [(0.62, 0.26, (0.11, 0.075, 0.09), 25, 1, P_), (0.82, 0.08, (0.06, 0.04, 0.05), 70, 2, Y_),
        (0.55, 0.52, (0.04, 0.03, 0.04), 5, 3, Y_), (0.98, 0.36, (0.05, 0.035, 0.045), 40, 4, Y_)],
    2: [(0.72, 0.28, (0.22, 0.14, 0.17), 25, 11, P_), (1.05, 0.55, (0.13, 0.09, 0.11), 60, 12, P_),
        (0.5, 0.8, (0.09, 0.07, 0.09), 5, 13, Y_), (1.0, -0.12, (0.08, 0.06, 0.09), 80, 14, P_),
        (-0.62, 0.52, (0.07, 0.05, 0.07), 40, 15, Y_), (1.3, 0.3, (0.05, 0.04, 0.05), 30, 16, Y_),
        (0.9, 0.95, (0.04, 0.03, 0.04), 65, 17, Y_), (0.35, 0.55, (0.035, 0.03, 0.035), 10, 18, Y_),
        (1.16, 0.04, (0.04, 0.03, 0.035), 50, 19, Y_)],
    3: [(0.78, 0.10, (0.19, 0.11, 0.15), 25, 21, P_), (0.52, 0.66, (0.13, 0.09, 0.12), 70, 22, Y_),
        (1.02, -0.30, (0.16, 0.10, 0.2), -15, 23, P_), (0.15, 0.88, (0.15, 0.1, 0.13), 40, 24, Y_),
        (-0.66, 0.60, (0.12, 0.08, 0.1), 10, 25, P_), (-0.88, -0.2, (0.18, 0.11, 0.12), 55, 26, Y_),
        (-0.1, -0.84, (0.14, 0.09, 0.16), -30, 27, P_), (0.7, -0.74, (0.11, 0.08, 0.1), 80, 28, Y_),
        (1.2, 0.46, (0.09, 0.06, 0.08), 0, 29, P_), (0.4, 0.4, (0.04, 0.03, 0.04), 20, 30, Y_),
        (-0.3, 0.7, (0.04, 0.03, 0.04), 60, 31, Y_), (0.95, 0.75, (0.05, 0.035, 0.05), 35, 32, Y_)],
}
NAMES = {0: "Z0 heil", 1: "Z1 nach 1. Schlag", 2: "Z2 nach 2. Schlag", 3: "Z3 zerbrochen"}
FILES = {0: "column_intact", 1: "column_hit1", 2: "column_hit2", 3: "column_broken"}


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
    d = MODELS
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


# --- Das fallende Modell und seine Bahnen fuers Spiel (Issue #601) ---------------------------------------------------
FALL_FILE = "column_falling"
FALL_PARTS = [("W", "waist"), ("L", "lower"), ("U1", "upper_1"), ("U2", "upper_2"), ("K", "capital")]
STEP = 3                 # wie animation(): der Modelltausch liegt auf einem Vielfachen von 3 Ticks


def emit_at(part, faces, pivot):
    """Wie emit, die Ecken aber relativ zum Drehpunkt des Teils (der Knoten in der Datei steht dort)."""
    return emit(part, [dict(f, pts=[p - pivot for p in f["pts"]]) for f in faces])


def swap_tick(movers):
    """Der Tick des Modelltauschs auf Z3, wie in animation(): 9 Ticks nach der letzten Landung, auf STEP gerundet."""
    swap = max(b.landed for b in movers.values() if b.landed is not None) + 9
    return int(math.ceil(swap / STEP) * STEP)


def export_falling():
    """column_falling.glb: base (Stumpf und Schutt aus Z2, steht) und die fuenf bewegten Stuecke, je mit dem
    Schwerpunkt c0 seiner Ecken als Drehpunkt. Atlas wie Z2."""
    _, fixed, static, movers = simulate(120)
    pieces = build_state(2, split_upper=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.data.collections.new("Saeule")
    bpy.context.scene.collection.children.link(coll)
    tex = tex_of(2)
    assert B.only_stein(tex), "Farbe ausserhalb der Stein-Reihe"
    img = save_image(FALL_FILE, tex, os.path.join(MODELS, FALL_FILE + ".png"))
    mat = export_material("m", img)
    objs = []
    base = Part("base", (0, 0, 0), {"all": (0.0, 0.0, 1.0, 1.0)})
    for nm, faces in list(fixed.items()) + list(static.items()):
        emit(base, faces)
    parts = [base]
    for key, name in FALL_PARTS:
        b = movers[key]
        part = Part(name, tuple(b.c0), {"all": (0.0, 0.0, 1.0, 1.0)})
        emit_at(part, pieces[key], b.c0)
        parts.append(part)
    for part in parts:
        open_e, multi = B.edge_report(part)
        print("FALLEND %s: %d Dreiecke, offene Kanten %d, mehrfach %d" % (part.name, B.triangles(part), open_e, multi))
        objs.append(part.build(coll, mat, smooth=False))
    export_glb(objs, os.path.join(MODELS, FALL_FILE + ".glb"))
    return movers


def _f(x):
    v = round(x, 5)
    return ("%.5f" % (0.0 if v == 0 else v)).rstrip("0").rstrip(".") + "f"


def write_table(movers):
    """ColumnFallTable.cs: je Tick 0 bis vor dem Tausch und je bewegtem Stueck die Drehung (Quaternion x, y, z, w) und
    die Mitte (x, y, z) in den Achsen der Saeule; dazu die Drehpunkte, der Tausch-Tick und die Staub-Anlaesse."""
    swap = swap_tick(movers)
    events = dust_events(movers, swap)
    nl = "\r\n"
    out = []
    out.append("// <auto-generated>")
    out.append("// Written by tools/models/column_break.py (issue #601) - do not edit by hand; run the script again.")
    out.append("// </auto-generated>")
    out.append("namespace SilverAndAsh.Core.Presentation;")
    out.append("")
    out.append("/// <summary>The paths of the falling column's pieces, baked from the probe's simulation (issue #594).</summary>")
    out.append("internal static class ColumnFallTable")
    out.append("{")
    out.append("    /// <summary>Tick after the break at which the falling model gives way to the broken one.</summary>")
    out.append("    public const int SwapTick = %d;" % swap)
    out.append("")
    out.append("    /// <summary>The moving pieces, in the order of <see cref=\"Poses\"/>.</summary>")
    out.append("    public static IReadOnlyList<string> Parts { get; } = [%s];" %", ".join('"%s"' % n for _, n in FALL_PARTS))
    out.append("")
    out.append("    /// <summary>Per piece its pivot in the model file (x, y, z): the middle of its corners.</summary>")
    piv = []
    for key, _ in FALL_PARTS:
        c0 = movers[key].c0
        piv += [_f(c0.x), _f(c0.y), _f(c0.z)]
    out.append("    public static ReadOnlySpan<float> Pivots => [%s];" % ", ".join(piv))
    out.append("")
    out.append("    /// <summary>Per tick 0 .. SwapTick - 1, per piece: turn as quaternion (x, y, z, w), then its middle (x, y, z).</summary>")
    out.append("    public static ReadOnlySpan<float> Poses =>")
    out.append("    [")
    for t in range(swap):
        row = []
        for key, _ in FALL_PARTS:
            rot, c = movers[key].track[t]
            q = rot.to_quaternion()
            if q.w < 0:
                q.negate()
            row += [_f(q.x), _f(q.y), _f(q.z), _f(q.w), _f(c.x), _f(c.y), _f(c.z)]
        out.append("        %s, // tick %d" % (", ".join(row), t))
    out.append("    ];")
    out.append("")
    out.append("    /// <summary>Dust: per occasion its tick, place (x, y, z), size in metres and number of puffs.</summary>")
    out.append("    public static ReadOnlySpan<float> Dust =>")
    out.append("    [")
    for (t0, pos, rmax, n, label) in events:
        out.append("        %d, %s, %s, %s, %s, %d, // %s" % (t0, _f(pos.x), _f(pos.y), _f(pos.z), _f(rmax), n,
                                                       label.replace("Reissen", "tear").replace("Knirschen", "grind")
                                                       .replace("Landung", "landing").replace("Modelltausch", "swap")))
    out.append("    ];")
    out.append("}")
    with open(TABLE, "w", encoding="ascii", newline="") as f:
        f.write(nl.join(out))
    print("TABELLE", TABLE, "Tausch", swap, "Landungen", {k: b.landed for k, b in movers.items()})


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
# Runde 3: langsamer und wuchtiger. Ablauf (Ticks bei 60/s): Knirschen und Halten, langsames Kippen mit wachsender
# Winkelgeschwindigkeit, Reissen der Taille, freier Fall, schwere Landungen ohne Springen (leichtes Rutschen), Staub.
G_STYL = 30.0
DT = 1.0 / 60.0
T_GRIND, T_TILT, T_SHEAR, T_FREE = 2, 10, 28, 34          # Knirschen ab, Kippen ab, Taille reisst, Oberteil frei
TILT_MAX = 14.0                                            # Grad bis zum Loesen, mit dem Quadrat der Zeit
HINGE = V(-0.2, 2.0, 0.0)                                  # Drehpunkt: die Bruchkante auf der Seite gegen den Schlag
RELEASE = {"W": (T_SHEAR, V(0.30, 0.0, 0.08), V(0.15, 0.0, -1.0)), "L": (36, V(0.35, 0.0, 0.1), V(0.2, 0.1, -0.8)),
           "U1": (T_FREE, None, None), "U2": (T_FREE + 1, None, None), "K": (T_FREE + 2, None, None)}
EXTRA = {"U1": (V(1.8, 0, 0.05), V(0, 0, 0)), "U2": (V(2.6, 0, 0.1), V(0.0, 0.1, -0.1)), "K": (V(3.4, 0, 0.2), V(0.2, 0.3, -0.4))}
FLAT_RATE = 1.7          # rad/s: nach der Landung kippt das Stueck bis es liegt
SLIDE = 0.6              # Anteil der Waagerechtgeschwindigkeit nach der Landung (kein Springen)


def tilt_angle(t):
    if t < T_TILT:
        return 0.0
    return math.radians(TILT_MAX) * (min(t, T_FREE) - T_TILT) ** 2 / float(T_FREE - T_TILT) ** 2


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
        self.land_pos = None
        self.track = []          # je Tick (Drehung, Mitte) - fuer die Tabelle des Spiels (Issue #601)

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


def _hinge_pose(b, t):
    th = tilt_angle(t)
    R = Matrix.Rotation(-th, 3, "Z")
    b.c = HINGE + R @ (b.c0 - HINGE)
    b.rot = R
    if t < T_TILT:        # Knirschen: kleines Zittern
        b.c = b.c0 + V(0.012 * math.sin(t * 2.3), 0.004 * math.sin(t * 3.1 + 1.0), 0)


def _free_start(b, nm, t):
    rt, v0, w0 = RELEASE[nm]
    if v0 is None:        # Oberteil: Geschwindigkeit aus der Kippbewegung am Drehpunkt
        om = (tilt_angle(t) - tilt_angle(t - 1)) / DT
        w = V(0, 0, -om)
        r = b.c - HINGE
        v = w.cross(r)
        dv, dw = EXTRA[nm]
        b.v, b.w = v + dv, w + dw
    else:
        b.v, b.w = v0.copy(), w0.copy()


def _contact(b, t):
    low = b.ymin()
    if low >= 0.0:
        return
    b.c.y += -low
    if b.landed is None:
        b.landed = t
        lp = min((b.world(p) for f in b.faces for p in f["pts"]), key=lambda q: q.y)
        b.land_pos = V(b.c.x, 0.0, b.c.z)
        b.v = V(b.v.x * SLIDE, 0.0, b.v.z * SLIDE)
        b.w = b.w * 0.3
    b.v.y = 0.0
    axis = b.rot @ V(0, 1, 0)
    if abs(axis.y) > 0.2 and b.name != "W":
        b.w = V(0, 0, -FLAT_RATE if axis.x > 0 else FLAT_RATE)
    else:
        b.w = b.w * 0.8
    b.v = V(b.v.x * 0.88, 0.0, b.v.z * 0.88)
    if abs(axis.y) <= 0.2 and b.v.length < 0.04 or t - b.landed > 40:
        b.rest = True
        b.v, b.w = V(0, 0, 0), V(0, 0, 0)


def simulate(ticks):
    """Deterministische Starrkoerper-Bahnen fuer Z2 -> Bruch. Gibt je Tick die Teile in Pose zurueck."""
    pieces = build_state(2, split_upper=True)
    static = {k: v for k, v in pieces.items() if k.startswith("s") and k != "stumpf"}
    movers = {k: Body(k, v) for k, v in pieces.items() if k in RELEASE}
    fixed = {k: v for k, v in pieces.items() if k == "stumpf"}
    history = []
    for t in range(ticks + 1):
        for nm, b in movers.items():
            rt = RELEASE[nm][0]
            if t < rt:
                if nm in ("U1", "U2", "K") or (nm == "W" and t >= T_TILT):
                    _hinge_pose(b, t)
                continue
            if t == rt:
                _free_start(b, nm, t)
            if b.rest:
                continue
            b.v.y -= G_STYL * DT
            b.c = b.c + b.v * DT
            ang = b.w.length * DT
            if ang > 1e-9:
                b.rot = Matrix.Rotation(ang, 3, b.w.normalized()) @ b.rot
            _contact(b, t)
        for b in movers.values():
            b.track.append((b.rot.copy(), b.c.copy()))
        history.append({nm: b.posed_faces() for nm, b in movers.items()})
    return history, fixed, static, movers


# --- Staub: kantige Wolken aus abgeflachten Oktaedern, Farben aus der Schutt-Flaeche der Palette ---------------------
def octa(c, r, yaw):
    rx, ry = r, r * 0.7
    a = [V(rx, 0, 0), V(0, 0, rx), V(-rx, 0, 0), V(0, 0, -rx)]
    a = [Matrix.Rotation(yaw, 3, "Y") @ p for p in a]
    top, bot = V(0, ry, 0), V(0, -ry * 0.6, 0)
    polys = []
    for i in range(4):
        p, q = a[i], a[(i + 1) % 4]
        polys.append(([p, q, top], ("cap", "oben")))
        polys.append(([q, p, bot], ("cap", "oben")))
    faces = convex_faces(polys)
    out = []
    for f in faces:
        g = dict(f)
        g["pts"] = [c + p for p in f["pts"]]
        out.append(g)
    return xf_faces(out, Matrix.Identity(3), V(0, 0, 0), V(0, 0, 0))


DUST_LIFE = 30
DUST_GROW = 7


def dust_events(movers, swap):
    ev = [(T_GRIND, V(0.3, 2.05, 0.15), 0.12, 3, "Knirschen"), (T_TILT + 4, V(0.3, 2.05, 0.15), 0.15, 3, "Knirschen"),
          (T_SHEAR, V(0.3, 2.0, 0.1), 0.34, 6, "Reissen")]
    size = {"W": (0.26, 4), "L": (0.28, 5), "U1": (0.42, 7), "U2": (0.36, 6), "K": (0.26, 4)}
    for nm, b in movers.items():
        if b.landed is not None:
            r, n = size[nm]
            ev.append((b.landed, V(b.land_pos.x, 0.12, b.land_pos.z), r, n, "Landung " + nm))
    ev.append((swap, V(0.4, 0.4, 0.0), 0.45, 7, "Modelltausch"))
    return ev


def dust_pieces(events, t):
    out = {}
    for ei, (t0, pos, rmax, n, _) in enumerate(events):
        a = t - t0
        if a < 0 or a >= DUST_LIFE:
            continue
        rng = random.Random(900 + ei)
        for k in range(int(n * 1.6)):
            ang = rng.uniform(0, 2 * math.pi)
            sp = rng.uniform(1.6, 3.6)
            rr = rmax * 1.5 * rng.uniform(0.6, 1.1)
            vel = V(math.cos(ang) * sp, rng.uniform(0.8, 1.6), math.sin(ang) * sp)
            off = V(math.cos(ang) * rr * 0.9, rng.uniform(0, 0.25), math.sin(ang) * rr * 0.9)
            s = a / float(DUST_GROW) if a < DUST_GROW else 1.0 - (a - DUST_GROW) / float(DUST_LIFE - DUST_GROW)
            r = rr * max(s, 0.05)
            p = pos + off + vel * (a * DT * 0.7)
            out["d%d_%d" % (ei, k)] = octa(p, r, rng.uniform(0, 3.0))
    return out


def anim_frames(ticks_shown, swap_tick):
    last = max(ticks_shown)
    history, fixed, static, movers = simulate(last)
    events = dust_events(movers, swap_tick)
    size = (560, 640) if not QUICK else (186, 213)
    frames = []
    z3 = build_state(3)
    for t in ticks_shown:
        if t < swap_tick:
            pieces = dict(fixed)
            pieces.update(static)
            pieces.update(history[t])
            pieces.update(dust_pieces(events, t))
            items = [("floor",), ("state", 2, 0.0, 0.0, pieces, "a%d" % t)]
        else:
            pieces = dict(z3)
            pieces.update(dust_pieces(events, t))
            items = [("floor",), ("state", 3, 0.0, 0.0, pieces, "b%d" % t)]
        im = shot(items, (1.5, 2.6, 11.0), (1.5, 2.5, 0), 42, size, "hell")
        frames.append((t, im))
    return frames, movers, events


PHASES = [(0, "Treffer 3: Knirschen"), (T_TILT, "Kippt langsam"), (T_SHEAR, "Reissen"), (T_FREE, "Sturz"),
          (None, "Z3: Stumpf und Schutt")]


def phase_of(t, swap):
    if t >= swap:
        return "Z3 Stumpf, Schutt, Staub (Modelltausch)"
    name = PHASES[0][1]
    for start, nm in PHASES[:-1]:
        if t >= start:
            name = nm
    if name == "Sturz" and t >= swap - 14:
        name = "Landung, Rutschen"
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
    history, fixed, static, movers = simulate(120)
    landed = {nm: b.landed for nm, b in movers.items()}
    rested = {nm: b.rest for nm, b in movers.items()}
    print("LANDUNG (Tick):", landed, "liegt:", rested)
    swap = max(v for v in landed.values() if v is not None) + 9
    swap = int(math.ceil(swap / step) * step)
    ticks = list(range(0, swap + 10 * step + 1, step))
    frames, _, events = anim_frames(ticks, swap)
    dp = dust_pieces(events, 63)
    print("DEBUG dust", len(dp), sum(len(v) for v in dp.values()), [e[0] for e in events])
    for k_, v_ in list(dp.items())[:3]:
        print("DEBUG", k_, [tuple(round(c_, 2) for c_ in q) for q in v_[0]["pts"]])
    print("STAUB (Tick, Ort, Anlass):", [(e[0], e[4]) for e in events], "TAUSCH", swap)
    pick = [0, 12, 24, 33, 42, 51, 60, swap]
    pick = [t for t in pick if t in ticks]
    strip = B.hcat([im for t, im in frames if t in pick])
    for i, t in enumerate(pick):
        B.text(strip, "Tick %d" % t, 6 + i * (frames[0][1].shape[1] + 6), 4, 20)
    B.png(strip, os.path.join(OUT, "bruch-posen-r3.png"))
    hold = [frames[-1]] * 5
    write_gif(frames + hold, swap, os.path.join(OUT, "bruch-animation-r3.gif"), 5)
    return landed, swap


def main():
    if MODE in ("alles", "modelle"):
        export_models()
        write_table(export_falling())
    os.makedirs(OUT, exist_ok=True)
    if MODE in ("alles", "ansichten"):
        for s in range(4):
            B.png(four_views(s), os.path.join(OUT, "ansichten-z%d-r3.png" % s))
        B.png(four_views(3, close=True), os.path.join(OUT, "ansichten-z3-nah-r3.png"))
        for d in (6, 10):
            B.png(eye_row(float(d), "spiel"), os.path.join(OUT, "spiel-%dm-r3.png" % d))
        B.png(eye_row(10.0, "hell"), os.path.join(OUT, "klar-10m-r3.png"))
    if MODE in ("alles", "blatt"):
        B.png(sheet(), os.path.join(OUT, "blatt-zustaende-r3.png"))
    if MODE in ("alles", "anim"):
        print("ANIMATION", animation())


main()
