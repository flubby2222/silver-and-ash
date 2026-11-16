"""Ghul, zweiter Entwurf (Probestueck 3) fuer Silver & Ash.

Ein ausgemergelter Mensch auf allen vieren: lange Glieder, eingefallener Brustkorb, Rippen, Wirbelsaeule,
Schulterblaetter, kleiner Kopf mit tiefen Augenhoehlen und haengendem Kiefer. PS1-naher Stil nach
design/grundlagen/grundlagen-grafik.md: um 1000 Dreiecke, eine 128 x 128-Textur ohne Filterung mit begrenzter
Palette, Licht je Ecke (Gouraud), im Spielbild 15-Bit-Farbe mit geordnetem Dithering.

Alles entsteht hier: Geometrie, UVs, Textur (Rauschen, Verlaeufe, gemalte Formen), Export und Bilder.
Nichts heruntergeladen.

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/ghul-2/ghul2.py

Koordinaten: Punkte in Spiel-Achsen (x seitlich, y oben, z vorn, Meter, Ursprung zwischen den Fuessen),
G() rechnet in Blender-Achsen um (Blick nach -y). Export mit +Y oben = Spiel-Achsen.
"""

import math
import os

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
OLD_DIR = os.path.join(os.path.dirname(OUT_DIR), "ghul")

# --- Farben (sRGB) ------------------------------------------------------------------------------------------------

SKIN = np.array((126, 134, 112), float)       # fahle, gruenliche Haut (Richtung des Platzhalters)
SKIN_PALE = np.array((164, 164, 136), float)   # ueber Knochen gespannte Haut: Schaedel, Gelenke
ROT = np.array((92, 96, 66), float)            # Flecken, Verwesung (oliv, kein Rot)
SHADOW = np.array((40, 34, 38), float)         # Falten, Rippenzwischenraeume
HOLLOW = np.array((22, 16, 20), float)         # Augenhoehlen, Schlund
TEETH = np.array((172, 160, 118), float)
GUM = np.array((74, 56, 54), float)            # Zahnfleisch, dunkel und braun (Rot ist reserviert)
EYE = np.array((176, 172, 136), float)         # trueber Augapfel, winzig
HAIR = np.array((38, 32, 30), float)
NAIL = np.array((120, 108, 76), float)
DIRT = np.array((70, 60, 50), float)
RAGS = np.array((84, 100, 118), float)         # Lumpen Blaugrau (angenommen beim ersten Ghul)

# --- Werte aus dem Spiel (LookSettings.cs, level.fs) --------------------------------------------------------------

FOG_COLOR = np.array((16, 11, 20), float)
FOG_START, FOG_END, LANTERN_MINIMUM = 3.25, 18.2, 0.35
FLOOR_B, CEILING_B, WALL_PX, WALL_MX, WALL_PZ, WALL_MZ = 0.9, 0.55, 1.0, 0.62, 0.82, 0.72
WORLD_WALL, WORLD_FLOOR, WORLD_CEILING = (122, 104, 112), (84, 72, 78), (64, 56, 66)

DITHER_START = 9.0  # Sichtgrenze per Dithering: Pixel fallen ab hier weg, ab FOG_END ist alles weg
TEX = 128           # Texturgroesse
PAINT = 4           # gemalt in vierfacher Groesse, dann verkleinert (weicher, "gemalter" Eindruck)
PALETTE = 32        # Farben in der fertigen Textur


def G(p):
    """Spiel-Achsen (x, oben, vorn) -> Blender-Achsen (x, -vorn, oben)."""
    return Vector((p[0], -p[2], p[1]))


def V(x, y, z):
    return Vector((x, y, z))


# --- Textur-Atlas: Bereiche in Pixeln (x0, y0, x1, y1), y von unten ------------------------------------------------

RECTS = {
    "torso": (0, 64, 64, 128),
    "head": (64, 80, 128, 128),
    "jaw": (64, 64, 128, 80),
    "upper_arm": (0, 16, 32, 64),
    "forearm": (32, 16, 64, 64),
    "thigh": (64, 16, 96, 64),
    "shin": (96, 16, 128, 64),
    "hand": (0, 0, 32, 16),
    "finger": (32, 0, 48, 16),
    "foot": (48, 0, 80, 16),
    "rags": (80, 0, 112, 16),
    "neck": (112, 0, 128, 16),
}


def uv_rect(name):
    x0, y0, x1, y1 = RECTS[name]
    inset = 0.5   # halber Pixel Rand, damit ohne Filterung nichts vom Nachbarbereich hereinblutet
    return ((x0 + inset) / TEX, (y0 + inset) / TEX, (x1 - inset) / TEX, (y1 - inset) / TEX)


# --- Geometrie ------------------------------------------------------------------------------------------------------

class Part:
    """Ein starres Teil; Ursprung = Drehpunkt. parent: Teil, an dem es haengt (fuer Posen)."""

    def __init__(self, name, pivot, parent=None):
        self.name, self.pivot, self.parent = name, V(*pivot), parent
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.verify()

    def _face(self, verts, uvs):
        f = self.bm.faces.new(verts)
        for loop, uv in zip(f.loops, uvs):
            loop[self.uv].uv = uv
        return f

    def loft(self, rings, rect, cap_start=True, cap_end=True, closed=False):
        """Ringe gleicher Eckenzahl verbinden. UV: u = rundherum (0 unten), v = entlang. Kappen als Faecher."""
        u0, v0, u1, v1 = uv_rect(rect)
        n = len(rings[0])
        count = len(rings) + (1 if closed else 0)
        verts = [[self.bm.verts.new(G(p - self.pivot)) for p in ring] for ring in rings]
        faces = []

        def uv(k, i):
            return (u0 + (u1 - u0) * k / n, v0 + (v1 - v0) * i / (count - 1))

        for i in range(count - 1):
            a, b = verts[i], verts[(i + 1) % len(verts)]
            for k in range(n):
                faces.append(self._face((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]),
                                        (uv(k, i), uv(k + 1, i), uv(k + 1, i + 1), uv(k, i + 1))))
        if not closed:
            for ring, idx, cap in ((verts[0], 0, cap_start), (verts[-1], count - 1, cap_end)):
                if not cap:
                    continue
                centre = sum((v.co for v in ring), Vector()) / n
                tip = rings[0 if idx == 0 else -1]
                axis = (sum(tip, V(0, 0, 0)) / n) - (sum(rings[1 if idx == 0 else -2], V(0, 0, 0)) / n)
                pole = self.bm.verts.new(centre + G(axis.normalized() * 0.25 * axis.length) - G(V(0, 0, 0)))
                for k in range(n):
                    faces.append(self._face((ring[k], ring[(k + 1) % n], pole),
                                            (uv(k, idx), uv(k + 1, idx), (uv(k, idx)[0] + (u1 - u0) / (2 * n), uv(k, idx)[1]))))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        return faces

    def build(self, collection, material):
        bmesh.ops.triangulate(self.bm, faces=list(self.bm.faces))
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        mesh.shade_smooth()
        mesh.materials.append(material)
        obj = bpy.data.objects.new(self.name, mesh)
        obj.matrix_world = Matrix.Translation(G(self.pivot))
        collection.objects.link(obj)
        obj["pivot"] = list(self.pivot)
        obj["parent"] = self.parent or ""
        return obj


def frames(centres, ref_up):
    """Achse, Seite und Oben je Ring - Oben wird von Ring zu Ring mitgefuehrt (keine Verdrehung)."""
    out = []
    up = ref_up.normalized()
    for i in range(len(centres)):
        a = centres[min(i + 1, len(centres) - 1)] - centres[max(i - 1, 0)]
        axis = a.normalized()
        up = (up - axis * up.dot(axis)).normalized()
        side = axis.cross(up).normalized()
        out.append((axis, side, up))
    return out


def tube(centres, shapes, sides, ref_up=V(0, 1, 0), mods=None):
    """Ringe um eine Mittellinie. shapes[i] = (halbe Breite, Tiefe unten, Hoehe oben).
    Ecke k=0 liegt unten, k=sides/2 oben. mods(i, k) -> Faktor fuer den Abstand (Beulen, Dellen)."""
    rings = []
    for i, ((axis, side, up), c, (w, b, t)) in enumerate(zip(frames(centres, ref_up), centres, shapes)):
        ring = []
        for k in range(sides):
            th = 2 * math.pi * k / sides
            f = mods(i, k) if mods else 1.0
            down = -math.cos(th)
            ring.append(c + side * (w * math.sin(th) * f) + up * (down * (t if down > 0 else b) * f))
        rings.append(ring)
    return rings


# Masse (Spiel-Achsen). Koerper 1 m hoch, Radius 0,4, Kopfzone ab 0,68 m Hoehe, ab 0,19 m vor der Mitte.
HIP = V(0, 0.56, -0.24)
NECK = V(0, 0.68, 0.24)
HEAD_C = V(0, 0.81, 0.34)            # Mitte des Schaedels
JAW_HINGE = V(0, 0.76, 0.33)
SHOULDER = V(0.165, 0.68, 0.17)
HIP_JOINT = V(0.095, 0.55, -0.24)


def build_torso():
    part = Part("torso", HIP)
    # Becken, eingefallener Bauch, Rippenkorb mit Buckel, Schultern; k=sides/2 ist die Wirbelsaeule.
    centres = [V(0, 0.55, -0.33), V(0, 0.57, -0.24), V(0, 0.62, -0.12), V(0, 0.68, -0.02),
               V(0, 0.70, 0.07), V(0, 0.69, 0.15), V(0, 0.68, 0.22)]
    shapes = [(0.10, 0.06, 0.05), (0.13, 0.08, 0.07), (0.085, 0.05, 0.075), (0.13, 0.09, 0.09),
              (0.15, 0.10, 0.085), (0.17, 0.07, 0.07), (0.07, 0.045, 0.05)]
    n = 10

    def mods(i, k):
        if k == n // 2:                       # Wirbelsaeule als Grat
            return 1.14
        if i == 5 and k in (4, 6):            # Schulterblaetter stehen vor
            return 1.12
        if i in (3, 4) and k in (2, 8):       # Rippenbogen an den Flanken
            return 1.06
        return 1.0

    part.loft(tube(centres, shapes, n, mods=mods), "torso")
    # Hals: duenn, lang, schraeg nach vorn oben in den Schaedel.
    neck = [V(0, 0.675, 0.18), V(0, 0.70, 0.25), V(0, 0.735, 0.30)]
    part.loft(tube(neck, [(0.035, 0.035, 0.03)] * 3, 6), "neck")
    # Lumpen um die Hueften: geschlossene Huelle (aussen, ausgefranster Saum, innen), schief haengend.
    top = tube([V(0, 0.575, -0.25), V(0, 0.565, -0.19)], [(0.145, 0.095, 0.08)] * 2, 10)
    outer_top = top[0]
    rng = np.random.default_rng(3)
    hem = []
    for k, p in enumerate(outer_top):
        drop = 0.14 + 0.07 * rng.random() + (0.05 if k in (2, 3) else 0.0)    # ein Fetzen haengt tiefer
        hem.append(p + V(0, -drop, -0.03) + (p - V(0, 0.575, -0.25)) * 0.15)
    inner_hem = [V(0, 0.575, -0.25) + (h - V(0, 0.575, -0.25)) * 0.93 for h in hem]
    inner_top = [V(0, 0.575, -0.25) + (p - V(0, 0.575, -0.25)) * 0.93 for p in outer_top]
    part.loft([outer_top, hem, inner_hem, inner_top], "rags", closed=True)
    return part


def build_head():
    part = Part("head", NECK, parent="torso")
    # Schaedel als stehender Loft vom Oberkiefer zum Scheitel; Gesicht vorn (k = n/2), leicht nach oben geneigt
    # (er schaut zum Spieler hoch). Augen tief eingesunken, Wangen hohl, Kopf um 10 Grad zur Seite geneigt.
    n = 10
    tilt, roll = math.radians(14), math.radians(10)
    up = V(math.sin(roll), math.cos(roll) * math.cos(tilt), -math.sin(tilt) * math.cos(roll)).normalized()
    fwd = V(0, math.sin(tilt), math.cos(tilt))
    fwd = (fwd - up * fwd.dot(up)).normalized()
    side = up.cross(fwd).normalized()
    # (Hoehe ueber Mitte, halbe Breite, vorn, hinten)
    rows = [(-0.085, 0.040, 0.070, 0.025), (-0.045, 0.058, 0.078, 0.060), (-0.005, 0.068, 0.074, 0.080),
            (0.035, 0.071, 0.084, 0.086), (0.080, 0.068, 0.066, 0.090), (0.118, 0.046, 0.038, 0.062)]

    def dent(i, k):
        if i == 2 and k in (4, 6):           # Augenhoehlen
            return 0.80
        if i == 1 and k in (3, 7):           # hohle Wangen
            return 0.84
        if i == 1 and k == 5:                 # Nasenruecken
            return 1.04
        return 1.0

    rings = []
    for i, (h, w, front, back) in enumerate(rows):
        c = HEAD_C + up * h
        ring = []
        for k in range(n):
            th = 2 * math.pi * k / n           # k=0 hinten, k=5 vorn
            depth = -math.cos(th)
            d = (front if depth > 0 else back) * dent(i, k)
            ring.append(c + side * (w * math.sin(th) * dent(i, k)) + fwd * (depth * d))
        rings.append(ring)
    part.loft(rings, "head")
    return part


def build_jaw():
    part = Part("jaw", JAW_HINGE, parent="head")
    # Unterkiefer, schief und offen haengend.
    centres = [V(0.0, 0.765, 0.305), V(0.004, 0.735, 0.355), V(0.010, 0.715, 0.400)]
    part.loft(tube(centres, [(0.052, 0.018, 0.012), (0.045, 0.02, 0.012), (0.025, 0.018, 0.01)], 6), "jaw")
    return part


def build_arm(s, reach):
    """s = Seite (+1/-1). reach verlaengert den Griff nach vorn (Asymmetrie)."""
    sh = V(s * SHOULDER.x, SHOULDER.y, SHOULDER.z)
    elbow = V(s * 0.30, 0.40, 0.22)
    wrist = V(s * 0.235, 0.05, 0.40 + reach)
    upper = Part(f"upper_arm_{'px' if s > 0 else 'nx'}", sh, parent="torso")
    pts = [sh + V(0, 0.01, 0), sh + (elbow - sh) * 0.45, elbow]
    upper.loft(tube(pts, [(0.045, 0.045, 0.045), (0.034, 0.034, 0.034), (0.04, 0.04, 0.04)], 6, ref_up=V(0, 0, 1)),
               "upper_arm")
    lower = Part(f"forearm_{'px' if s > 0 else 'nx'}", elbow, parent=upper.name)
    pts = [elbow + (elbow - sh).normalized() * 0.02, elbow + (wrist - elbow) * 0.4, wrist]
    lower.loft(tube(pts, [(0.036, 0.036, 0.036), (0.03, 0.026, 0.026), (0.024, 0.02, 0.02)], 6, ref_up=V(0, 0, 1)),
               "forearm")
    # Hand: flacher Handteller, fuenf lange, gekruemmte Finger, die Spitzen auf dem Boden.
    knuckle = wrist + V(s * 0.005, -0.03, 0.085)
    lower.loft(tube([wrist, wrist + (knuckle - wrist) * 0.5, knuckle],
                    [(0.03, 0.012, 0.016), (0.042, 0.012, 0.016), (0.046, 0.01, 0.013)], 6, ref_up=V(0, 1, 0)), "hand")
    for f, spread in enumerate((-0.036, -0.012, 0.012, 0.036)):
        base = knuckle + V(spread, 0.002, 0.0)
        length = 0.09 + (0.015 if f in (1, 2) else 0.0)
        out = V(spread * 0.6, 0, 0)
        mid = base + V(0, 0.028, length * 0.55) + out
        tip = base + V(0, -0.028, length) + out * 1.6
        lower.loft(tube([base, mid, tip], [(0.009, 0.009, 0.009), (0.008, 0.008, 0.008), (0.004, 0.004, 0.004)], 3,
                        ref_up=V(0, 1, 0)), "finger")
    thumb_base = wrist + V(-s * 0.03, -0.005, 0.03)
    lower.loft(tube([thumb_base, thumb_base + V(-s * 0.03, 0.0, 0.035), thumb_base + V(-s * 0.035, -0.035, 0.06)],
                    [(0.01, 0.01, 0.01), (0.008, 0.008, 0.008), (0.004, 0.004, 0.004)], 3, ref_up=V(0, 1, 0)), "finger")
    return [upper, lower]


def build_leg(s):
    hip = V(s * HIP_JOINT.x, HIP_JOINT.y, HIP_JOINT.z)
    knee = V(s * 0.15, 0.09, 0.0)        # Knie unter dem Bauch, Oberschenkel schraeg nach vorn
    ankle = V(s * 0.15, 0.14, -0.34)
    thigh = Part(f"thigh_{'px' if s > 0 else 'nx'}", hip, parent=None)
    thigh.loft(tube([hip + V(0, 0.02, 0), hip + (knee - hip) * 0.5, knee],
                    [(0.052, 0.052, 0.052), (0.036, 0.036, 0.036), (0.045, 0.045, 0.045)], 6, ref_up=V(0, 0, 1)), "thigh")
    shin = Part(f"shin_{'px' if s > 0 else 'nx'}", knee, parent=thigh.name)
    shin.loft(tube([knee + V(0, 0.0, -0.01), knee + (ankle - knee) * 0.5, ankle],
                   [(0.04, 0.04, 0.04), (0.03, 0.03, 0.034), (0.024, 0.024, 0.024)], 6, ref_up=V(0, 1, 0)), "shin")
    # Fuss steht auf den Zehen, Ferse oben.
    toe = V(s * 0.155, 0.0, -0.42)
    shin.loft(tube([ankle, ankle + (toe - ankle) * 0.5 + V(0, 0, 0.01), toe],
                   [(0.026, 0.02, 0.02), (0.034, 0.016, 0.018), (0.036, 0.01, 0.01)], 6, ref_up=V(0, 0, -1)), "foot")
    return [thigh, shin]


def build_model(material):
    coll = bpy.data.collections.new("Ghul2")
    bpy.context.scene.collection.children.link(coll)
    parts = [build_torso(), build_head(), build_jaw()]
    parts += build_arm(1, 0.0) + build_arm(-1, 0.05)
    parts += build_leg(1) + build_leg(-1)
    return [p.build(coll, material) for p in parts]


# --- Textur --------------------------------------------------------------------------------------------------------

def vnoise(h, w, cy, cx, seed):
    """Wertrauschen: Zufallsgitter cy x cx, bilinear auf h x w vergroessert, Werte 0..1."""
    g = np.random.default_rng(seed).random((cy + 1, cx + 1))
    ys = np.linspace(0, cy, h)
    xs = np.linspace(0, cx, w)
    y0 = np.clip(ys.astype(int), 0, cy - 1)
    x0 = np.clip(xs.astype(int), 0, cx - 1)
    fy = (ys - y0)[:, None]
    fx = (xs - x0)[None, :]
    a = g[y0][:, x0]
    b = g[y0][:, x0 + 1]
    c = g[y0 + 1][:, x0]
    d = g[y0 + 1][:, x0 + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def blob(U, V_, cu, cv, ru, rv):
    """1 in der Mitte einer Ellipse, weich nach aussen 0."""
    return np.clip(1 - ((U - cu) / ru) ** 2 - ((V_ - cv) / rv) ** 2, 0, 1)


def mix(base, color, amount):
    return base * (1 - amount[..., None]) + color * amount[..., None]


def skin(U, V_, seed, pale=0.0):
    h, w = U.shape
    col = np.broadcast_to(SKIN + (SKIN_PALE - SKIN) * pale, (h, w, 3)).copy()
    n1 = vnoise(h, w, 6, 6, seed)
    n2 = vnoise(h, w, 18, 18, seed + 1)
    col *= (0.9 + 0.12 * n1 + 0.08 * n2)[..., None]
    rot = np.clip((vnoise(h, w, 5, 5, seed + 2) - 0.6) * 4, 0, 0.6)
    col = mix(col, ROT, rot)
    # Licht von oben eingebacken: Unterseite (u um 0/1) dunkler, Oberseite (u = 0.5) heller.
    top = 1 - np.abs(np.cos(np.pi * U))
    col *= (0.84 + 0.24 * top)[..., None]
    return col


def joints(col, V_, strength=0.3):
    near = np.exp(-(V_ / 0.12) ** 2) + np.exp(-((1 - V_) / 0.12) ** 2)
    return col * (1 - strength * np.clip(near, 0, 1))[..., None]


def paint_torso(U, V_):
    col = skin(U, V_, 10)
    side = np.abs(np.sin(2 * np.pi * U))                     # Flanken
    # Rippen: helle Boegen, dunkle Zwischenraeume, an Flanken und Bauch, nicht ueber der Wirbelsaeule.
    rib_zone = ((V_ > 0.42) & (V_ < 0.86)).astype(float) * np.clip(1 - np.abs(U - 0.5) / 0.12, 0, 1) ** 0
    rib_zone *= np.clip((np.abs(U - 0.5) - 0.06) / 0.08, 0, 1)
    phase = ((V_ - 0.42) / 0.44 * 6 + 0.25 * np.sin(2 * np.pi * U * 2)) % 1.0
    dark = ((phase > 0.55) & (phase < 0.92)).astype(float) * rib_zone * (0.5 + 0.4 * side)
    light = ((phase > 0.08) & (phase < 0.35)).astype(float) * rib_zone * 0.35
    col = mix(col, SHADOW, dark * 0.75)
    col = mix(col, SKIN_PALE * 1.05, light)
    # Wirbelsaeule: Reihe heller Knoten mit dunklen Luecken.
    spine = np.clip(1 - np.abs(U - 0.5) / 0.035, 0, 1) * (V_ > 0.12) * (V_ < 0.95)
    knob = (((V_ * 16) % 1.0) < 0.55).astype(float)
    col = mix(col, SKIN_PALE * 1.08, spine * knob * 0.8)
    col = mix(col, SHADOW, spine * (1 - knob) * 0.6)
    # Schulterblaetter: hell, mit dunklem Rand unten.
    for cu in (0.5 - 0.13, 0.5 + 0.13):
        b = blob(U, V_, cu, 0.86, 0.07, 0.07)
        col = mix(col, SKIN_PALE, np.clip(b * 2, 0, 0.6))
        rim = blob(U, V_, cu, 0.80, 0.08, 0.05) * (1 - np.clip(b * 3, 0, 1))
        col = mix(col, SHADOW, np.clip(rim * 2, 0, 0.6))
    # Eingefallener Bauch und Taille, Schmutz zum Becken hin.
    col *= (1 - 0.22 * np.exp(-((V_ - 0.28) / 0.08) ** 2))[..., None]
    col = mix(col, DIRT, np.clip((0.15 - V_) * 4, 0, 0.5))
    return col


def paint_head(U, V_):
    col = skin(U, V_, 20, pale=0.9)
    h, w = U.shape
    # Strähniges, verfilztes Haar hinten und oben.
    strands = vnoise(h, w, 3, 40, 21) * 0.7 + vnoise(h, w, 6, 12, 22) * 0.3
    hair_zone = np.clip((V_ - 0.52) * 4, 0, 1) * np.clip((np.abs(U - 0.5) - 0.15) * 6, 0, 1)
    col = mix(col, HAIR, ((strands > 0.52) * hair_zone).astype(float) * 0.95)
    # Stirnschatten, tiefe Augenhoehlen (schmal, schraeg, nicht rund), darin ein winziger trueber Augapfel.
    col *= (1 - 0.25 * blob(U, V_, 0.5, 0.56, 0.2, 0.06))[..., None]
    for sgn in (-1, 1):
        cu = 0.5 + sgn * 0.085
        slant = (U - cu) * sgn * 0.6
        sock = blob(U, V_ - slant, cu, 0.43, 0.055, 0.075)
        col = mix(col, HOLLOW, np.clip(sock * 2.5, 0, 1))
        col = mix(col, EYE, (blob(U, V_, cu + sgn * 0.006, 0.425, 0.012, 0.022) > 0.3).astype(float))
        col = mix(col, SHADOW, np.clip(blob(U, V_, 0.5 + sgn * 0.15, 0.24, 0.05, 0.1) * 1.4, 0, 0.7))
    # Nase: nur zwei dunkle Schlitze.
    for sgn in (-1, 1):
        col = mix(col, HOLLOW, (blob(U, V_, 0.5 + sgn * 0.018, 0.25, 0.011, 0.04) > 0.2).astype(float))
    # Oberlippe weggefault: Zahnfleisch und obere Zahnreihe, unregelmaessig.
    mouth = (np.abs(U - 0.5) < 0.13) & (V_ < 0.15)
    col = mix(col, GUM, (mouth & (V_ > 0.09)).astype(float))
    teeth_n = vnoise(1, w, 1, 30, 23)[0]
    tooth = ((((U - 0.37) / 0.26 * 9) % 1.0) < 0.62) & (teeth_n[None, :] > 0.25)
    col = mix(col, TEETH, (mouth & (V_ <= 0.09) & tooth).astype(float))
    col = mix(col, HOLLOW, (mouth & (V_ <= 0.09) & ~tooth).astype(float))
    return col


def paint_jaw(U, V_):
    col = skin(U, V_, 30, pale=0.55)
    inside = np.abs(U - 0.5) < 0.2                            # Oberseite = Mundhoehle
    col = mix(col, HOLLOW, inside.astype(float))
    tooth = (((U - 0.3) / 0.4 * 7) % 1.0 < 0.6) & (V_ > 0.55)
    col = mix(col, TEETH, (inside & tooth & (np.abs(U - 0.5) < 0.17)).astype(float))
    return col


def paint_limb(seed, pale=0.0, dirt_end=0.0, knuckles=False):
    def painter(U, V_):
        col = joints(skin(U, V_, seed, pale), V_, 0.28)
        # Sehnen und Knochen: helle Linie oben entlang, dunkle Rinne daneben.
        col = mix(col, SKIN_PALE, np.clip(1 - np.abs(U - 0.5) / 0.04, 0, 1) * 0.45)
        col = mix(col, SHADOW, np.clip(1 - np.abs(U - 0.62) / 0.03, 0, 1) * 0.35)
        if dirt_end:
            col = mix(col, DIRT, np.clip((V_ - (1 - dirt_end)) / dirt_end, 0, 1) * 0.7)
        if knuckles:
            col = mix(col, SKIN_PALE, (((U * 4) % 1.0 < 0.4) & (V_ > 0.8)).astype(float) * 0.5)
        return col
    return painter


def paint_finger(U, V_):
    col = paint_limb(60, pale=0.2, dirt_end=0.5)(U, V_)
    col = mix(col, NAIL, ((V_ > 0.85) & (np.abs(U - 0.5) < 0.3)).astype(float))
    return col


def paint_rags(U, V_):
    h, w = U.shape
    col = np.broadcast_to(RAGS, (h, w, 3)).copy()
    folds = 0.88 + 0.12 * np.cos(2 * np.pi * U * 9 + 2 * vnoise(h, w, 2, 8, 70))
    stains = np.clip((vnoise(h, w, 3, 10, 71) - 0.55) * 3, 0, 0.6)
    col *= folds[..., None]
    col = mix(col, DIRT * 0.9, stains)
    col *= (0.7 + 0.3 * V_)[..., None]                        # zum Saum hin dunkler
    return col


PAINTERS = {
    "torso": paint_torso, "head": paint_head, "jaw": paint_jaw,
    "upper_arm": paint_limb(40), "forearm": paint_limb(41, dirt_end=0.25),
    "thigh": paint_limb(50), "shin": paint_limb(51, dirt_end=0.2),
    "hand": paint_limb(42, pale=0.15, dirt_end=0.8, knuckles=True), "finger": paint_finger,
    "foot": paint_limb(52, dirt_end=0.9), "rags": paint_rags, "neck": paint_limb(43, pale=0.1),
}


def quantize(img, colors, seed=1):
    """Begrenzte Palette (k-Means), wie eine Textur mit Farbtabelle."""
    px = img.reshape(-1, 3)
    rng = np.random.default_rng(seed)
    centres = px[rng.choice(len(px), colors, replace=False)].copy()
    for _ in range(12):
        d = ((px[:, None, :] - centres[None, :, :]) ** 2).sum(-1)
        idx = d.argmin(1)
        for c in range(colors):
            sel = px[idx == c]
            if len(sel):
                centres[c] = sel.mean(0)
    return np.round(centres[idx]).reshape(img.shape)


def paint_atlas():
    size = TEX * PAINT
    atlas = np.zeros((size, size, 3))
    for name, (x0, y0, x1, y1) in RECTS.items():
        h, w = (y1 - y0) * PAINT, (x1 - x0) * PAINT
        U, V_ = np.meshgrid((np.arange(w) + 0.5) / w, (np.arange(h) + 0.5) / h)
        atlas[y0 * PAINT:y1 * PAINT, x0 * PAINT:x1 * PAINT] = PAINTERS[name](U, V_)
    small = atlas.reshape(TEX, PAINT, TEX, PAINT, 3).mean(axis=(1, 3))
    small = quantize(np.clip(small, 0, 255), PALETTE)
    return np.clip(small, 0, 255)


def save_texture(arr, path):
    img = bpy.data.images.new("ghul2_texture", TEX, TEX)
    rgba = np.concatenate([arr / 255.0, np.ones((TEX, TEX, 1))], axis=2)
    img.pixels = rgba.ravel()                                  # Zeile 0 = unten, wie die UVs
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    img.reload()
    return img


# --- Licht je Ecke (Gouraud) ---------------------------------------------------------------------------------------

def to_game(v):
    return V(v.x, v.z, -v.y)


def brightness(n):
    """Stufenlos: die sechs Helligkeiten des Spiels nach den Quadraten der Normalen gemischt (je Ecke)."""
    g = to_game(n)
    return (g.x * g.x * (WALL_PX if g.x > 0 else WALL_MX) + g.y * g.y * (FLOOR_B if g.y > 0 else CEILING_B)
            + g.z * g.z * (WALL_PZ if g.z > 0 else WALL_MZ))


def flat_brightness(n):
    g = to_game(n)
    flat = max(abs(g.x), abs(g.z))
    if abs(g.y) > flat:
        return FLOOR_B if g.y > 0 else CEILING_B
    if abs(g.x) > abs(g.z):
        return WALL_PX if g.x > 0 else WALL_MX
    return WALL_PZ if g.z > 0 else WALL_MZ


def write_light(obj, smooth=True):
    """Attribut 'Light' (Float, Ecken): Helligkeit aus der Welt-Normale nach der aktuellen Pose."""
    mesh = obj.data
    rot = obj.matrix_world.to_3x3()
    attr = mesh.color_attributes.get("Light") or mesh.color_attributes.new("Light", "FLOAT_COLOR", "CORNER")
    normals = mesh.corner_normals if smooth else None
    for poly in mesh.polygons:
        for li in poly.loop_indices:
            n = (rot @ (normals[li].vector if smooth else poly.normal)).normalized()
            b = brightness(n) if smooth else flat_brightness(n)
            attr.data[li].color = (b, b, b, 1.0)


# --- Posen ---------------------------------------------------------------------------------------------------------

def raise_fwd(deg):
    """Drehung um x: positiv hebt das vordere Ende (unten -> vorn -> oben). In Blender negativ um x."""
    return Matrix.Rotation(math.radians(-deg), 4, "X")


def pose(objs, angles):
    """angles: Teilname -> Grad (raise_fwd). Teile haengen an ihren Eltern; Ursprung = Drehpunkt."""
    by = {o.name: o for o in objs}
    done = {}

    def world(name):
        if name in done:
            return done[name]
        o = by[name]
        pivot = Vector(list(o["pivot"]))
        parent = o["parent"]
        if parent:
            pm = world(parent)
            m = pm @ Matrix.Translation(G(pivot) - G(Vector(list(by[parent]["pivot"])))) @ raise_fwd(angles.get(name, 0))
        else:
            m = Matrix.Translation(G(pivot)) @ raise_fwd(angles.get(name, 0))
        done[name] = m
        return m

    for o in objs:
        o.matrix_world = world(o.name)
    bpy.context.view_layer.update()


REST = {"jaw": -12}
WIND_UP = {"torso": 34, "head": -24, "jaw": -38,
           "upper_arm_px": 120, "upper_arm_nx": 128, "forearm_px": -25, "forearm_nx": -20}


# --- Raum, Material, Rendern ---------------------------------------------------------------------------------------

def build_room():
    coll = bpy.data.collections.new("Raum")
    bpy.context.scene.collection.children.link(coll)
    objs, step = [], 0.25

    def grid(name, color, corner, du, dv, nu, nv):
        bm = bmesh.new()
        verts = [[bm.verts.new(corner + du * (i * step) + dv * (j * step)) for j in range(nv + 1)] for i in range(nu + 1)]
        for i in range(nu):
            for j in range(nv):
                bm.faces.new((verts[i][j], verts[i + 1][j], verts[i + 1][j + 1], verts[i][j + 1]))
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        col = mesh.color_attributes.new("Base", "FLOAT_COLOR", "CORNER")
        for d in col.data:
            d.color = (color[0] / 255, color[1] / 255, color[2] / 255, 1.0)   # rohe sRGB-Werte, nicht linear
        obj = bpy.data.objects.new(name, mesh)
        coll.objects.link(obj)
        objs.append(obj)

    half, height, back, front = 2.5, 3.5, -12.0, 24.0
    length = int((front - back) / step)
    x, y, z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
    grid("floor", WORLD_FLOOR, Vector((-half, back, 0)), x, y, int(2 * half / step), length)
    grid("ceiling", WORLD_CEILING, Vector((-half, back, height)), y, x, length, int(2 * half / step))
    grid("wall_left", WORLD_WALL, Vector((-half, back, 0)), y, z, length, int(height / step))
    grid("wall_right", WORLD_WALL, Vector((half, back, 0)), z, y, int(height / step), length)
    for obj in objs:
        poly = obj.data.polygons[0]
        if poly.normal.dot(Vector((0, 0, height / 2)) - poly.center) < 0:
            obj.data.flip_normals()
        write_light(obj, smooth=False)
    return objs


def pass_material(name, base_node):
    """Emissions-Material mit drei Ausgaengen (Grundfarbe, Licht, Entfernung), je Durchgang umgesteckt."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    if base_node == "texture":
        base = nt.nodes.new("ShaderNodeTexImage")
        base.image = bpy.data.images["ghul2_texture"]
        base.interpolation = "Closest"
        base.image.colorspace_settings.name = "Non-Color"
        base_out = base.outputs["Color"]
    else:
        base = nt.nodes.new("ShaderNodeAttribute")
        base.attribute_name = "Base"
        base_out = base.outputs["Color"]
    light = nt.nodes.new("ShaderNodeAttribute")
    light.attribute_name = "Light"
    cam = nt.nodes.new("ShaderNodeCameraData")
    mat["outs"] = 0
    MATS[mat.name] = (nt, emit, {"base": base_out, "light": light.outputs["Color"], "dist": cam.outputs["View Distance"]})
    return mat


MATS = {}


def set_pass(which):
    for nt, emit, outs in MATS.values():
        for link in list(emit.inputs["Color"].links):
            nt.links.remove(link)
        nt.links.new(outs[which], emit.inputs["Color"])


def setup_render(w, h):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 1
    scene.cycles.use_denoising = False
    scene.cycles.pixel_filter_type = "BOX"
    scene.cycles.filter_width = 0.01
    scene.cycles.max_bounces = 0
    scene.render.film_transparent = True
    scene.render.resolution_x, scene.render.resolution_y = w, h
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "OPEN_EXR"
    scene.render.image_settings.color_depth = "32"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("Leer")
    scene.world.color = (0, 0, 0)


TMP = os.path.join(os.environ.get("TEMP", OUT_DIR), "ghul2_passes")


def render_passes(tag):
    """Drei Durchgaenge als Float-Bilder: Grundfarbe (sRGB 0..255), Licht (0..1), Entfernung (m) + Deckung."""
    os.makedirs(TMP, exist_ok=True)
    scene = bpy.context.scene
    result = {}
    for which in ("base", "light", "dist"):
        set_pass(which)
        path = os.path.join(TMP, f"{tag}_{which}.exr")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(path, check_existing=False)
        w, h = img.size
        result[which] = np.array(img.pixels[:]).reshape(h, w, 4)
        bpy.data.images.remove(img)
    return result


BAYER4 = (np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) + 0.5) / 16


def compose(p, mode, base_scale=1.0):
    """mode: 'ansicht' (ohne Nebel), 'nebel' (wie das Spiel heute), 'dither' (15 Bit, Sichtgrenze per Muster)."""
    base = p["base"][..., :3] * 255 * base_scale
    light = p["light"][..., :1]
    d = p["dist"][..., :1]
    covered = p["base"][..., 3:4] > 0.5
    h, w = d.shape[:2]
    if mode == "ansicht":
        col = base * light
        return np.where(covered, col, FOG_COLOR)
    lantern = 1 - (1 - LANTERN_MINIMUM) * np.clip(d / FOG_END, 0, 1)
    col = base * light * lantern
    t = np.clip((d - FOG_START) / (FOG_END - FOG_START), 0, 1)
    fog = t * t * (3 - 2 * t)
    if mode == "nebel":
        col = col * (1 - fog) + FOG_COLOR * fog
        return np.where(covered, col, FOG_COLOR)
    # Dither: Farbe auf 5 Bit je Kanal mit geordnetem 4x4-Muster; Sichtgrenze: Pixel faellt weg, wenn der
    # Nebelanteil die Schwelle des Musters uebersteigt. Laterne bleibt, Nebelfarbe wird nicht beigemischt.
    ys, xs = np.mgrid[0:h, 0:w]
    thr = BAYER4[ys % 4, xs % 4][..., None]
    col = np.floor((col + (thr - 0.5) * 8) / 8) * 8 + 4
    # Eigene, spaetere Kurve: Mit der Nebelkurve (ab 3,25 m) fehlten bei 10 m schon 43 % der Pixel, und duenne
    # Glieder zerfielen. Linear von DITHER_START bis FOG_END.
    cut = np.clip((d - DITHER_START) / (FOG_END - DITHER_START), 0, 1)
    gone = cut > thr
    return np.where(covered & ~gone, np.clip(col, 0, 255), FOG_COLOR)


def save_png(arr, path):
    h, w = arr.shape[:2]
    img = bpy.data.images.new(os.path.basename(path), w, h)
    rgba = np.concatenate([np.clip(arr, 0, 255) / 255.0, np.ones((h, w, 1))], axis=2)
    img.pixels = rgba.ravel()
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    print("Bild:", path)


def load_png(path):
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    arr = np.array(img.pixels[:]).reshape(h, w, 4)[..., :3] * 255
    bpy.data.images.remove(img)
    return arr


def aim(cam, location, target):
    cam.location = location
    cam.rotation_euler = (target - location).to_track_quat("-Z", "Y").to_euler()


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)

    texture = paint_atlas()
    tex_path = os.path.join(OUT_DIR, "ghul2_textur.png")
    save_texture(texture, tex_path)

    # Export-Material: Textur ohne Filterung als Grundfarbe (glTF: Sampler NEAREST).
    export_mat = bpy.data.materials.new("ghoul")
    export_mat.use_nodes = True
    bsdf = export_mat.node_tree.nodes["Principled BSDF"]
    tex_node = export_mat.node_tree.nodes.new("ShaderNodeTexImage")
    tex_node.image = bpy.data.images["ghul2_texture"]
    tex_node.interpolation = "Closest"
    export_mat.node_tree.links.new(tex_node.outputs["Color"], bsdf.inputs["Base Color"])

    model = build_model(export_mat)
    pose(model, REST)
    tris = {o.name: len(o.data.polygons) for o in model}
    print("Dreiecke:", sum(tris.values()), tris)

    bpy.ops.object.select_all(action="DESELECT")
    for o in model:
        o.select_set(True)
    pose(model, {})
    bpy.ops.export_scene.gltf(filepath=os.path.join(OUT_DIR, "ghul2.glb"), export_format="GLB", use_selection=True,
                              export_yup=True, export_normals=True, export_vertex_color="NONE",
                              export_materials="EXPORT", export_texcoords=True)

    # Render-Materialien.
    tex_img = bpy.data.images["ghul2_texture"]
    ghoul_mat = pass_material("ghoul_pass", "texture")
    for o in model:
        o.data.materials[0] = ghoul_mat

    cam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera"))
    bpy.context.scene.collection.objects.link(cam)
    bpy.context.scene.camera = cam
    cam.data.sensor_fit = "VERTICAL"

    # Vier Ansichten (ohne Nebel, Licht je Ecke), 640 x 640.
    setup_render(640, 640)
    cam.data.angle_y = math.radians(30)
    pose(model, REST)
    for o in model:
        write_light(o)
    target = G(V(0, 0.48, 0.05))
    views = {"ansicht_vorn": V(0, 0.6, 3.3), "ansicht_seite": V(3.3, 0.6, 0.05),
             "ansicht_hinten": V(0, 0.6, -3.2), "ansicht_schraeg_oben": V(-2.0, 2.5, 2.2)}
    for name, loc in views.items():
        aim(cam, G(loc), target)
        save_png(compose(render_passes(name), "ansicht"), os.path.join(OUT_DIR, name + ".png"))

    # Spielbedingungen: 854 x 480, 70 Grad vertikal, Augen 1,6 m.
    room = build_room()
    room_mat = pass_material("room_pass", "attribute")
    for o in room:
        o.data.materials.append(room_mat)
    setup_render(854, 480)
    cam.data.angle_y = math.radians(70)
    cam.data.clip_end = 100
    shots = [("ruhe", REST, 4), ("ausholen", WIND_UP, 4), ("ruhe", REST, 10)]
    rest4 = None
    for state, angles, d in shots:
        pose(model, angles)
        for o in model:
            write_light(o)
        aim(cam, G(V(0, 1.6, d)), G(V(0, 0.5, 0)))
        passes = render_passes(f"{state}_{d}")
        for mode in ("nebel", "dither"):
            arr = compose(passes, mode)
            save_png(arr, os.path.join(OUT_DIR, f"spiel_{d}m_{state}_{mode}.png"))
            if state == "ruhe" and d == 4 and mode == "nebel":
                rest4 = arr
    pose(model, REST)

    # Alt und neu nebeneinander: Ausschnitt um den Ghul aus 4 m (Nebel), dreifach vergroessert.
    old = load_png(os.path.join(OLD_DIR, "spiel_4m_blaugrau.png"))
    crops = []
    for arr in (old, rest4):
        c = arr[150:330, 307:547]                             # Zeilen von unten gezaehlt
        crops.append(np.repeat(np.repeat(c, 3, axis=0), 3, axis=1))
    gap = np.zeros((crops[0].shape[0], 6, 3))
    save_png(np.concatenate([crops[0], gap, crops[1]], axis=1), os.path.join(OUT_DIR, "vergleich_alt_neu_4m.png"))


main()
