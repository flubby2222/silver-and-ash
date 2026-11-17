"""Tueren Runde 2 (Issue #245): Spitzbogen, neue Bosstuer in zwei Entwuerfen (A: Stufenportal, B: Fallgitter).

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/tueren2/door2.py

Alles per Skript, nichts heruntergeladen. Spiel-Achsen (x seitlich, y oben, z vorn), Meter. Ursprung: Mitte des Durchgangs
auf dem Boden, Tuerflaeche in der x/y-Ebene, Dicke in z. Durchgang = Zelle des Spiels: 2 m breit, 3,5 m hoch.

Aufbau jeder Tuer: Knoten "portal" (fest: Steinfeld ueber dem Bogen, Pfosten, Kopffeld) und die beweglichen Teile. Die
beweglichen Teile sind unterhalb des Bogenansatzes rechteckig, damit sie ohne Durchdringung schwingen koennen; der Spitzbogen
entsteht aus dem festen Kopffeld. Jedes Teil ist ein eigener, nicht verschachtelter Knoten mit einem Mesh.
"""
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE)))), "tools", "models"))
import bpy  # noqa: E402
from psx import (V, Part, rects_from_pixels, vnoise, blob, mix, paint_atlas, save_image,  # noqa: E402
                 export_material, export_glb, report_parts)

OUT_DIR = os.path.join(HERE, "modelle")
TEX = 128
H = 3.5                                # Zellhoehe
Z_FACE = 0.97                          # Vorderflaeche des Steins (die Zelle ist +-1 tief)

RECTS = {
    "planks": (0, 0, 40, 128),         # Bretter (1 m x 3,5 m)
    "plate": (40, 0, 72, 32),          # genietete Eisenplatte
    "iron": (72, 0, 104, 32),          # Baender, Beschlag
    "edge": (104, 0, 128, 32),         # Kanten
    "lock": (40, 32, 88, 64),          # Schlossplatte
    "dark": (88, 32, 104, 64),         # dunkel
    "glass": (104, 32, 128, 64),       # leuchtendes Glas
    "stone": (40, 64, 104, 128),       # Stein wie die Wand (Bogenfeld)
    "arch": (104, 64, 128, 128),       # heller Werkstein (ein Wolbstein, jedes Stueck bekommt das ganze Feld)
}
UV = rects_from_pixels(RECTS, TEX)

PAL = {
    "normal": {"wood": (96, 80, 66), "iron": (112, 116, 130), "stone": (122, 104, 112), "arch": (176, 168, 156)},
    "locked": {"wood": (54, 56, 66), "iron": (104, 108, 122), "stone": (122, 104, 112), "arch": (176, 168, 156)},
    "boss_a": {"wood": (40, 34, 42), "iron": (112, 108, 126), "stone": (122, 104, 112), "arch": (196, 188, 172)},
    "boss_b": {"wood": (40, 34, 42), "iron": (118, 122, 136), "stone": (122, 104, 112), "arch": (196, 188, 172)},
}
BRASS = np.array((158, 126, 66), float)
DARK = np.array((16, 12, 16), float)
GLOW = np.array((226, 92, 52), float)


# --- Texturen -----------------------------------------------------------------------------------------------------

def painters(kind):
    P = PAL[kind]
    wood, iron, stone, arch = (np.array(P[k], float) for k in ("wood", "iron", "stone", "arch"))
    seed = list(PAL).index(kind) * 11

    def planks(U, V_):
        h, w = U.shape
        rng = np.random.default_rng(100 + seed)
        n = 3
        idx = np.minimum(np.floor(U * n), n - 1).astype(int)
        f = (U * n) % 1.0
        col = np.broadcast_to(wood, (h, w, 3)).copy()
        col *= (0.82 + 0.32 * rng.random(n))[idx][..., None]
        col *= (0.86 + 0.28 * vnoise(h, w, 3, 16, 200 + seed))[..., None]
        col *= (0.93 + 0.14 * vnoise(h, w, 20, 3, 300 + seed))[..., None]
        for k in range(4):
            cu, cv = 0.1 + 0.8 * rng.random(), 0.1 + 0.8 * rng.random()
            col = mix(col, wood * 0.4, (blob(U, V_, cu, cv, 0.05, 0.02) > 0.3).astype(float) * 0.9)
        col = mix(col, DARK * 1.4, ((f < 0.07) | (f > 0.95)).astype(float) * 0.9)
        col = mix(col, wood * 1.3, ((f > 0.07) & (f < 0.13)).astype(float) * 0.5)
        for k, vv in enumerate((0.31, 0.64, 0.82)):
            sel = (idx == k) & (np.abs(V_ - vv) < 0.006)
            col = mix(col, DARK * 1.4, sel.astype(float))
        return col

    def plate(U, V_):
        h, w = U.shape
        col = np.broadcast_to(iron * 0.8, (h, w, 3)).copy()
        col *= (0.84 + 0.3 * vnoise(h, w, 6, 6, 400 + seed))[..., None]
        edge = (np.minimum(np.minimum(U, 1 - U), np.minimum(V_, 1 - V_)) < 0.07)
        col = mix(col, iron * 0.35, edge.astype(float))
        col = mix(col, iron * 1.4 + 14, ((np.minimum(U, V_) > 0.07) & (np.minimum(U, V_) < 0.13)).astype(float) * 0.55)
        for cu in (0.15, 0.85):
            for cv in (0.15, 0.5, 0.85):
                d = blob(U, V_, cu, cv, 0.08, 0.08)
                col = mix(col, iron * 1.7 + 20, (d > 0.4).astype(float))
                col = mix(col, iron * 0.35, ((d > 0.05) & (d <= 0.4)).astype(float) * 0.7)
        col = mix(col, np.array((92, 62, 46), float), np.clip((vnoise(h, w, 5, 5, 500 + seed) - 0.64) * 3, 0, 0.45))
        return col

    def iron_band(U, V_):
        h, w = U.shape
        col = np.broadcast_to(iron, (h, w, 3)).copy()
        col *= (0.8 + 0.4 * np.abs(np.sin(V_ * 3.1)))[..., None]
        col *= (0.9 + 0.2 * vnoise(h, w, 6, 6, 600 + seed))[..., None]
        col = mix(col, iron * 1.35 + 14, ((V_ > 0.78) & (V_ < 0.92)).astype(float) * 0.7)          # helle Kante: Glanz oben
        for k in range(4):
            d = blob(U, V_, (k + 0.5) / 4, 0.5, 0.09, 0.2)
            col = mix(col, iron * 1.6 + 20, (d > 0.35).astype(float))
        return col

    def edge(U, V_):
        h, w = U.shape
        base = wood * 0.7 if kind in ("normal", "boss_a", "boss_b") else iron * 0.6
        col = np.broadcast_to(base, (h, w, 3)).copy()
        return col * (0.8 + 0.4 * vnoise(h, w, 4, 4, 700 + seed))[..., None]

    def glass(U, V_):
        h, w = U.shape
        col = np.broadcast_to(GLOW, (h, w, 3)).copy() * (0.85 + 0.25 * vnoise(h, w, 4, 3, 900 + seed))[..., None]
        col = mix(col, np.array((250, 200, 120), float), np.clip(blob(U, V_, 0.5, 0.5, 0.6, 0.7), 0, 1) * 0.55)   # Kern heller
        return mix(col, DARK, (((U * 3) % 1.0 < 0.07) | (np.abs(V_ - 0.5) < 0.04)).astype(float) * 0.85)       # Bleiruten

    def lock(U, V_):
        h, w = U.shape
        col = np.broadcast_to(BRASS, (h, w, 3)).copy() * (0.82 + 0.32 * vnoise(h, w, 5, 8, 1000))[..., None]
        col = mix(col, BRASS * 0.45, (np.minimum(np.minimum(U, 1 - U), np.minimum(V_, 1 - V_)) < 0.07).astype(float))
        col = mix(col, DARK, (blob(U, V_, 0.5, 0.62, 0.07, 0.12) > 0.3).astype(float))
        col = mix(col, DARK, ((np.abs(U - 0.5) < 0.025) & (V_ > 0.15) & (V_ < 0.6)).astype(float))
        for cu in (0.12, 0.88):
            col = mix(col, BRASS * 1.4, (blob(U, V_, cu, 0.5, 0.05, 0.12) > 0.4).astype(float))
        return col

    def dark(U, V_):
        return np.broadcast_to(DARK, U.shape + (3,)).copy()

    def stone_p(U, V_):
        """Mauerwerk wie die Wand: Quader in zwei Reihen, dunkle Fuge, leichte Fleckung."""
        h, w = U.shape
        col = np.broadcast_to(stone, (h, w, 3)).copy() * (0.86 + 0.26 * vnoise(h, w, 6, 6, 1100 + seed))[..., None]
        rows = 4
        r = np.floor(V_ * rows).astype(int)
        fy = (V_ * rows) % 1.0
        fx = ((U * 2) + 0.5 * (r % 2)) % 1.0
        col = mix(col, stone * 0.45, ((fy < 0.09) | (fx < 0.05)).astype(float))
        col = mix(col, stone * 1.18, ((fy > 0.82) & (fy < 0.95) & (fx > 0.06)).astype(float) * 0.5)
        return col

    def arch_p(U, V_):
        """Ein Wolbstein aus hellem Stein: Fuge am Rand, Lichtkante oben, ein paar dunkle Flecken."""
        h, w = U.shape
        col = np.broadcast_to(arch, (h, w, 3)).copy() * (0.88 + 0.22 * vnoise(h, w, 5, 3, 1200 + seed))[..., None]
        edge = (np.minimum(np.minimum(U, 1 - U), np.minimum(V_, 1 - V_)) < 0.07)
        col = mix(col, arch * 0.5, edge.astype(float))
        col = mix(col, arch * 1.1 + 6, ((V_ > 0.8) & (V_ < 0.92) & (U > 0.1) & (U < 0.9)).astype(float) * 0.5)
        return col

    return {"planks": planks, "plate": plate, "iron": iron_band, "edge": edge, "lock": lock, "dark": dark, "glass": glass,
            "stone": stone_p, "arch": arch_p}


# --- Geometrie-Bausteine ------------------------------------------------------------------------------------------

def arc(hw, spring, apex, n=6):
    """Rechte Haelfte eines Spitzbogens von (hw, spring) bis zum Scheitel (0, apex): zwei Kreise mit den Mittelpunkten auf der
    Ansatzlinie, Radius R = (Stich^2 + hw^2) / (2 hw) (gleichseitig bei Stich = 1,73 hw)."""
    rise = apex - spring
    R = (rise * rise + hw * hw) / (2 * hw)
    cx = hw - R
    a_end = math.atan2(rise, R - hw)
    return [(cx + R * math.cos(a_end * k / n), spring + R * math.sin(a_end * k / n)) for k in range(n + 1)]


def extrude(part, pts, z0, z1, rect):
    part.prism([(x, y, z0) for x, y in pts], (0, 0, z1 - z0), rect)


def head_panel(part, hw, spring, apex, z0, z1, rect, n=6):
    """Kopffeld im Bogen (konvex), in zwei Haelften (links/rechts), damit die Textur nicht ueber 2 m gestreckt wird."""
    right = arc(hw, spring, apex, n)
    extrude(part, [(0, spring)] + right, z0, z1, rect)
    extrude(part, [(0, spring)] + [(-x, y) for x, y in right], z0, z1, rect)


def spandrel(part, hw, spring, apex, outer_x, top, z0, z1, rect, n=6):
    """Bogenfeld: die Ecken ueber dem Bogen bis zur Zellkante outer_x / Decke top, beidseitig (Faecher von der Ecke aus)."""
    right = arc(hw, spring, apex, n)
    ring = [(outer_x, spring)] + right + [(0, top)]
    ring = [p for i, p in enumerate(ring) if i == 0 or abs(p[0] - ring[i - 1][0]) + abs(p[1] - ring[i - 1][1]) > 1e-6]
    corner = (outer_x, top)
    for s in (1, -1):
        pts = [(s * x, y) for x, y in ring]
        c = (s * corner[0], corner[1])
        for a, b in zip(pts, pts[1:]):
            extrude(part, [c, a, b] if s > 0 else [c, b, a], z0, z1, rect)


def order(part, hw_o, hw_i, spring, apex_o, apex_i, z, rect, n=6):
    """Ein Bogenlauf (Archivolte): Pfosten unter dem Ansatz und ein Ring zwischen Aussen- und Innenbogen, durch z0..z1 (|z|)."""
    for s in (1, -1):
        lo, hi = sorted((s * hw_i, s * hw_o))
        part.box((lo, 0, -z), (hi, spring, z), rect)
    ao, ai = arc(hw_o, spring, apex_o, n), arc(hw_i, spring, apex_i, n)
    for s in (1, -1):
        for j in range(n):
            q = [(ao[j][0], ao[j][1]), (ao[j + 1][0], ao[j + 1][1]), (ai[j + 1][0], ai[j + 1][1]), (ai[j][0], ai[j][1])]
            extrude(part, [(s * x, y) for x, y in q] if s > 0 else [(s * x, y) for x, y in reversed(q)], -z, z, rect)


def strap(part, x_hinge, side, y, length, half, z0, z1, rect="iron"):
    xe = x_hinge - side * length
    pts = [(x_hinge, y - half), (xe + side * 0.12, y - half * 0.55), (xe, y), (xe + side * 0.12, y + half * 0.55), (x_hinge, y + half)]
    part.prism([(x, y_, z0) for x, y_ in pts], (0, 0, z1 - z0), rect)


def ring_pull(part, cx, cy, z, radius, width, depth, rect="iron", segs=8):
    r0, r1 = radius - width / 2, radius + width / 2
    for k in range(segs):
        a0, a1 = 2 * math.pi * k / segs, 2 * math.pi * (k + 1) / segs
        pts = [(cx + r0 * math.cos(a0), cy + r0 * math.sin(a0), z), (cx + r1 * math.cos(a0), cy + r1 * math.sin(a0), z),
               (cx + r1 * math.cos(a1), cy + r1 * math.sin(a1), z), (cx + r0 * math.cos(a1), cy + r0 * math.sin(a1), z)]
        part.prism(pts, (0, 0, depth), rect)


def faces(t, proud):
    """z-Bereiche der beiden Seiten eines Bauteils der Dicke t, `proud` ueber der Flaeche."""
    return [tuple(sorted((f * (t / 2 - 0.005), f * (t / 2 + proud)))) for f in (-1, 1)]


def leaf_rect(name, side, x_j, thick, top, kind, inner=0.005):
    """Rechteckiger Fluegel; Scharnierkante bei side * (x_j - thick/2 - 0.005); der Fluegel schwingt dort um y. Der Pfosten
    (Teil von portal) fuellt den Rest bis x_j. Beschlag wie in Runde 1, hellere Baender."""
    proud = 0.03
    px = side * (x_j - thick / 2 - proud - 0.005)
    part = Part(name, (px, 0, 0), UV)
    t = thick / 2
    lo, hi = sorted((px, side * inner))
    part.box((lo, 0, -t), (hi, top, t), lambda n: "planks" if abs(n.z) > 0.9 else "edge")
    for z0, z1 in faces(thick, 0.025):
        for y in (0.45, 1.0, top - 0.25):
            strap(part, px, side, y, 0.8 if kind != "boss_a" else 0.5, 0.075, z0, z1)
        if kind == "normal":
            rx = px - side * 0.7
            ring_pull(part, rx, 1.15, z0, 0.1, 0.03, z1 - z0)
            part.box((rx - 0.045, 1.25, z0), (rx + 0.045, 1.32, z1), "iron")
    return part


# --- Tuerarten ----------------------------------------------------------------------------------------------------

SPRING_N = 2.0
APEX_N = 3.45
LEAF_TOP_N = 1.94
POST_N = 0.105                  # Pfosten an der Scharnierseite (Normal): Dicke 0,14 / 2 + Beschlag 0,03 + Luft
POST_L = 0.135                  # Locked: Dicke 0,14 / 2 + Platten und Baender 0,06 + Luft


def portal_normal(kind):
    part = Part("portal", (0, 0, 0), UV)
    t = 0.07
    x_j = 1.0
    POST = POST_N if kind == "normal" else POST_L
    # Pfosten (Rest bis zur Zellkante, an der Scharnierseite) und Bogenfeld
    for s in (1, -1):
        lo, hi = sorted((s * (x_j - POST), s * x_j))
        part.box((lo, 0, -0.1), (hi, LEAF_TOP_N, 0.1), "iron")
    spandrel(part, 1.0, SPRING_N, APEX_N, 1.0, H, -Z_FACE, Z_FACE, "stone")
    # Kopffeld (Holz oder Eisenplatten) in den Bogen, Querband auf der Fuge zu den Fluegeln
    head_panel(part, 1.0, SPRING_N, APEX_N, -t, t, "planks" if kind == "normal" else "plate")
    for z0, z1 in faces(0.14, 0.025):
        part.box((-1.0, LEAF_TOP_N, z0), (1.0, SPRING_N + 0.06, z1), "iron")
        if kind == "normal":
            for s in (1, -1):                                                           # zwei Baender im Kopf, spitz zulaufend
                strap(part, s * 0.99, s, 2.5, 0.9, 0.065, z0, z1)
    return part


def locked_leaf():
    x_j, thick = 1.0, 0.14
    px = x_j - POST_L
    part = Part("leaf", (px, 0, 0), UV)
    t = thick / 2
    part.box((-0.995, 0, -t), (px, LEAF_TOP_N, t), "edge")
    cols, rows, gap = 2, 3, 0.03
    width = px + 0.995
    cw, rh = (width - gap * (cols + 1)) / cols, (LEAF_TOP_N - gap * (rows + 1)) / rows
    for z0, z1 in faces(thick, 0.02):
        for i in range(cols):
            for j in range(rows):
                x0 = -0.995 + gap + i * (cw + gap)
                y0 = gap + j * (rh + gap)
                part.box((x0, y0, z0), (x0 + cw, y0 + rh, z1), "plate")
        za, zb = (z1, z1 + 0.04) if z1 > 0 else (z0 - 0.04, z0)
        for y in (0.4, 1.0, 1.55):
            part.box((-0.995, y - 0.09, za), (px, y + 0.09, zb), "iron")
        for x in (-0.65, 0.5):
            part.box((x - 0.07, 0.0, za), (x + 0.07, LEAF_TOP_N, zb), "iron")
    return part


def lock_node():
    cx, cy = -0.4, 1.0
    part = Part("lock", (cx, cy, 0), UV, parent="leaf")
    zt = 0.07 + 0.06
    part.box((cx - 0.2, cy - 0.17, -zt - 0.04), (cx + 0.2, cy + 0.17, zt + 0.04), lambda n: "lock" if abs(n.z) > 0.9 else "iron")
    for face in (-1, 1):
        z0, z1 = sorted((face * (zt + 0.04), face * (zt + 0.09)))
        part.box((cx - 0.12, cy + 0.17, z0), (cx - 0.07, cy + 0.3, z1), "iron")
        part.box((cx + 0.07, cy + 0.17, z0), (cx + 0.12, cy + 0.3, z1), "iron")
        part.box((cx - 0.12, cy + 0.3, z0), (cx + 0.12, cy + 0.35, z1), "iron")
    return part


# Boss A: Stufenportal. Torbogen hw 0.66 (Fluegel darin), drei Laeufe nach aussen, Kopffeld mit Scheibe.
A_SPRING = 1.9
A_ORDERS = [(1.0, 3.45), (0.89, 3.30), (0.78, 3.12), (0.66, 2.85)]       # (hw, Scheitel) von aussen nach innen
A_Z = [0.97, 0.65, 0.33]


def boss_a_portal():
    part = Part("portal", (0, 0, 0), UV)
    spandrel(part, 1.0, A_SPRING, 3.45, 1.0, H, -Z_FACE, Z_FACE, "stone")
    for k in range(3):
        (hw_o, ap_o), (hw_i, ap_i) = A_ORDERS[k], A_ORDERS[k + 1]
        order(part, hw_o, hw_i, A_SPRING, ap_o, ap_i, A_Z[k], "arch")
    hw, apex = A_ORDERS[3]
    # Pfosten fuer die Scharniere (bis zum Rand des Torbogens) und Kopffeld mit leuchtender Scheibe
    for s in (1, -1):
        lo, hi = sorted((s * (hw - 0.135), s * hw))
        part.box((lo, 0, -0.11), (hi, 1.88, 0.11), "iron")
    head_panel(part, hw, A_SPRING, apex, -0.1, 0.1, "planks")
    for z0, z1 in faces(0.2, 0.03):
        part.box((-hw, 1.88, z0), (hw, A_SPRING + 0.06, z1), "iron")
    cy = 2.38
    for f in (-1, 1):
        z0, z1 = sorted((f * 0.095, f * 0.14))
        pts = [(0.21 * math.cos(2 * math.pi * k / 10), cy + 0.21 * math.sin(2 * math.pi * k / 10)) for k in range(10)]
        extrude(part, pts, z0, z1, "glass")
    return part


def boss_b_portal():
    part = Part("portal", (0, 0, 0), UV)
    z = Z_FACE
    for s in (1, -1):                                                           # Pfosten neben dem Gitter
        lo, hi = sorted((s * 0.8, s * 1.0))
        part.box((lo, 0, -z), (hi, 1.75, z), "arch")
    part.box((-1.0, 1.75, -z), (1.0, H, z), "arch")
    part.box((-0.8, 1.745, -0.07), (0.8, 1.75, 0.07), "dark")                    # Schlitz, in dem das Gitter verschwindet                            # Sturz mit Schlitz fuer das Gitter (Vollkoerper)
    # Spitzbogenfenster als leuchtende Platte auf beiden Seiten des Sturzes
    for f in (-1, 1):
        a, b = sorted((f * z, f * (z + 0.012)))
        for s_, (hw, sp, ap, rect) in enumerate([(0.36, 2.16, 3.34, "dark"), (0.29, 2.2, 3.16, "glass")]):
            a2, b2 = (a, b) if s_ == 0 else sorted((f * (z + 0.012), f * (z + 0.024)))
            head_panel(part, hw, sp, ap, a2, b2, rect)
    return part


def grate():
    part = Part("grate", (0, 0, 0), UV)
    for x in np.linspace(-0.72, 0.72, 10):
        part.box((x - 0.022, 0.14, -0.03), (x + 0.022, 1.7, 0.03), "iron")
        part.spike((x, 0.14, 0.0), (x, 0.0, 0.0), 0.03, "iron")
    for y in (0.45, 0.95, 1.5):
        part.box((-0.78, y - 0.03, -0.045), (0.78, y + 0.03, 0.045), "iron")
    return part


def parts_for(kind):
    if kind == "normal":
        return [portal_normal("normal"), leaf_rect("leaf_a", +1, 1.0, 0.14, LEAF_TOP_N, "normal"),
                leaf_rect("leaf_b", -1, 1.0, 0.14, LEAF_TOP_N, "normal")]
    if kind == "locked":
        return [portal_normal("locked"), locked_leaf(), lock_node()]
    if kind == "boss_a":
        return [boss_a_portal(), leaf_rect("leaf_a", +1, 0.66, 0.2, 1.88, "boss_a"), leaf_rect("leaf_b", -1, 0.66, 0.2, 1.88, "boss_a")]
    return [boss_b_portal(), grate()]


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for kind in PAL:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        coll = bpy.data.collections.new("Tuer")
        bpy.context.scene.collection.children.link(coll)
        tex = paint_atlas(RECTS, painters(kind), TEX, colors=32, seed=7)
        image = save_image(f"door_{kind}", tex, os.path.join(OUT_DIR, f"door_{kind}.png"))
        mat = export_material(f"door_{kind}", image)
        objs = [p.build(coll, mat, smooth=False) for p in parts_for(kind)]
        export_glb(objs, os.path.join(OUT_DIR, f"door_{kind}.glb"))
        report_parts(f"door_{kind}", objs)
        print(f"TEXTUR door_{kind} {TEX}x{TEX} Farben {len({tuple(p) for p in tex.reshape(-1, 3).astype(int)})}")


main()
