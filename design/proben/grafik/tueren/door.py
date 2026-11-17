"""Tueren (Issue #245), Probestueck: Normal, Boss, Locked, Cracked. Ein Skript, vier Modelle + Texturen.

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/tueren/door.py

Alles per Skript, nichts heruntergeladen. Spiel-Achsen (x seitlich, y oben, z vorn), Meter. Ursprung: Mitte des Durchgangs
auf dem Boden; die Tuerflaeche liegt in der x/y-Ebene, die Dicke laeuft in z. Der Durchgang ist die Zelle des Spiels:
2 m breit, 3,5 m hoch (Cell.DefaultCeilingHeight). Die Tuer fuellt die Breite ganz aus (ein Spalt von 1 cm zwischen den
Fluegeln), sonst sieht man ins Mauerwerk.
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
W, H = 2.0, 3.5                      # Durchgang (Meter)
HW = W / 2

# Atlas 128 x 128 (Pixel, y von unten), fuer alle Tuerarten gleich geteilt, je Art anders gemalt.
RECTS = {
    "planks": (0, 0, 44, 128),       # Bretter eines Fluegels (1 m x 3,5 m): drei Bretter, Maserung senkrecht
    "plate": (44, 0, 108, 64),       # genietete Eisenplatte (Locked), sonst Eisenfeld
    "iron": (44, 64, 76, 96),        # Baender, Beschlag
    "edge": (76, 64, 108, 96),       # Kanten (Hirnholz / Eisenkante)
    "bone": (108, 0, 128, 32),       # Schaedel
    "glow": (108, 32, 128, 64),      # roter Spalt (Boss)
    "lock": (44, 96, 108, 128),      # Schlossplatte mit Schluesselloch (Locked)
    "dark": (108, 64, 128, 128),     # Rest: dunkel (Kappen, Risse)
}
UV = rects_from_pixels(RECTS, TEX)

KINDS = ["normal", "boss", "locked", "cracked"]

PAL = {
    "normal": {"wood": (96, 80, 66), "iron": (66, 68, 78)},                # graues, altes Holz, Eisenbaender
    "boss": {"wood": (46, 38, 44), "iron": (58, 54, 66)},                  # fast schwarzes Holz, dunkles Eisen
    "locked": {"wood": (54, 56, 66), "iron": (64, 66, 76)},                # Eisentuer
}
BONE = np.array((200, 190, 166), float)
BRASS = np.array((158, 126, 66), float)
RED = np.array((176, 30, 34), float)
DARK = np.array((16, 12, 16), float)


# --- Texturen -----------------------------------------------------------------------------------------------------

def painters(kind):
    if kind == "cracked":
        return {name: (lambda U, V_: np.broadcast_to(np.array((28, 24, 30), float), U.shape + (3,)).copy() *
                       (0.8 + 0.4 * vnoise(U.shape[0], U.shape[1], 4, 4, 9))[..., None]) for name in RECTS}
    P = PAL[kind]
    wood, iron = np.array(P["wood"], float), np.array(P["iron"], float)
    seed = KINDS.index(kind) * 11

    def planks(U, V_):
        h, w = U.shape
        rng = np.random.default_rng(100 + seed)
        n = 3
        idx = np.minimum(np.floor(U * n), n - 1).astype(int)
        f = (U * n) % 1.0
        col = np.broadcast_to(wood, (h, w, 3)).copy()
        col *= (0.82 + 0.32 * rng.random(n))[idx][..., None]                              # Ton je Brett
        col *= (0.86 + 0.28 * vnoise(h, w, 3, 16, 200 + seed))[..., None]                   # lange Maserung
        col *= (0.93 + 0.14 * vnoise(h, w, 20, 3, 300 + seed))[..., None]
        # Astloecher: dunkle Ovale
        for k in range(4):
            cu, cv = 0.1 + 0.8 * rng.random(), 0.1 + 0.8 * rng.random()
            col = mix(col, wood * 0.4, (blob(U, V_, cu, cv, 0.05, 0.02) > 0.3).astype(float) * 0.9)
        col = mix(col, DARK * 1.4, ((f < 0.07) | (f > 0.95)).astype(float) * 0.9)           # Fugen
        col = mix(col, wood * 1.3, ((f > 0.07) & (f < 0.13)).astype(float) * 0.5)           # helle Kante rechts der Fuge
        # eine Quernaht je Brett (Stoss), versetzt
        for k, vv in enumerate((0.31, 0.64, 0.82)):
            sel = (idx == k) & (np.abs(V_ - vv) < 0.006)
            col = mix(col, DARK * 1.4, sel.astype(float))
        return col

    def plate(U, V_):
        h, w = U.shape
        col = np.broadcast_to(iron, (h, w, 3)).copy()
        col *= (0.84 + 0.3 * vnoise(h, w, 6, 6, 400 + seed))[..., None]
        edge = (np.minimum(np.minimum(U, 1 - U), np.minimum(V_, 1 - V_)) < 0.06)
        col = mix(col, iron * 0.45, edge.astype(float))                                      # dunkle Fuge
        col = mix(col, iron * 1.5 + 12, ((np.minimum(U, V_) > 0.06) & (np.minimum(U, V_) < 0.11)).astype(float) * 0.5)
        for cu in (0.14, 0.86):                                                              # Nieten in den Ecken und mittig
            for cv in (0.12, 0.5, 0.88):
                d = blob(U, V_, cu, cv, 0.07, 0.07)
                col = mix(col, iron * 1.9 + 14, (d > 0.4).astype(float))
                col = mix(col, iron * 0.4, ((d > 0.05) & (d <= 0.4)).astype(float) * 0.7)
        col = mix(col, np.array((92, 62, 46), float), np.clip((vnoise(h, w, 5, 5, 500 + seed) - 0.62) * 3, 0, 0.5))   # Rost
        return col

    def iron_band(U, V_):
        h, w = U.shape
        col = np.broadcast_to(iron, (h, w, 3)).copy()
        col *= (0.8 + 0.4 * np.abs(np.sin(V_ * 3.1)))[..., None]
        col *= (0.9 + 0.2 * vnoise(h, w, 6, 6, 600 + seed))[..., None]
        for k in range(4):                                                                   # Nietkoepfe in einer Reihe
            d = blob(U, V_, (k + 0.5) / 4, 0.5, 0.09, 0.2)
            col = mix(col, iron * 1.8 + 12, (d > 0.35).astype(float))
        return col

    def edge(U, V_):
        h, w = U.shape
        col = np.broadcast_to(wood * 0.7 if kind != "locked" else iron * 0.7, (h, w, 3)).copy()
        col *= (0.8 + 0.4 * vnoise(h, w, 4, 4, 700 + seed))[..., None]
        return col

    def bone(U, V_):
        h, w = U.shape
        col = np.broadcast_to(BONE, (h, w, 3)).copy() * (0.88 + 0.2 * vnoise(h, w, 4, 3, 800))[..., None]
        for cu in (0.3, 0.7):
            col = mix(col, DARK, (blob(U, V_, cu, 0.58, 0.14, 0.12) > 0.2).astype(float))   # Augenhoehlen
        col = mix(col, DARK, (blob(U, V_, 0.5, 0.36, 0.06, 0.08) > 0.3).astype(float))     # Nase
        col = mix(col, DARK, (((np.abs(V_ - 0.13) < 0.07) & (((U * 6) % 1.0) < 0.25) & (U > 0.18) & (U < 0.82))).astype(float))
        return col

    def glow(U, V_):
        h, w = U.shape
        col = np.broadcast_to(RED, (h, w, 3)).copy() * (0.85 + 0.3 * vnoise(h, w, 4, 2, 900))[..., None]
        return mix(col, np.array((226, 80, 60), float), (np.abs(U - 0.5) < 0.18).astype(float) * 0.7)

    def lock(U, V_):
        h, w = U.shape
        col = np.broadcast_to(BRASS, (h, w, 3)).copy() * (0.82 + 0.32 * vnoise(h, w, 5, 8, 1000))[..., None]
        col = mix(col, BRASS * 0.45, (np.minimum(np.minimum(U, 1 - U), np.minimum(V_, 1 - V_)) < 0.07).astype(float))
        col = mix(col, DARK, (blob(U, V_, 0.5, 0.62, 0.07, 0.12) > 0.3).astype(float))     # Schluesselloch: Kreis
        col = mix(col, DARK, ((np.abs(U - 0.5) < 0.025) & (V_ > 0.15) & (V_ < 0.6)).astype(float))   # und Schlitz
        for cu in (0.12, 0.88):
            col = mix(col, BRASS * 1.4, (blob(U, V_, cu, 0.5, 0.05, 0.12) > 0.4).astype(float))
        return col

    def dark(U, V_):
        return np.broadcast_to(DARK, U.shape + (3,)).copy()

    return {"planks": planks, "plate": plate, "iron": iron_band, "edge": edge, "bone": bone, "glow": glow, "lock": lock,
            "dark": dark}


# --- Geometrie ----------------------------------------------------------------------------------------------------

def strap(part, x_hinge, side, y, length, half, z0, z1, rect="iron"):
    """Beschlagband: vom Rand des Fluegels (x_hinge) um `length` zur Mitte, endet spitz. Durch den Fluegel (z0..z1)."""
    xs = x_hinge
    xe = x_hinge - side * length
    pts = [(xs, y - half, z0), (xe + side * 0.12, y - half * 0.55, z0), (xe, y, z0), (xe + side * 0.12, y + half * 0.55, z0),
           (xs, y + half, z0)]
    part.prism(pts, (0, 0, z1 - z0), rect)


def ring(part, cx, cy, z, radius, width, depth, rect="iron", segs=8):
    """Zugring: Achteck aus Stuecken, zur Tuer hin auf einer kleinen Platte."""
    r0, r1 = radius - width / 2, radius + width / 2
    for k in range(segs):
        a0, a1 = 2 * math.pi * k / segs, 2 * math.pi * (k + 1) / segs
        pts = [(cx + r0 * math.cos(a0), cy + r0 * math.sin(a0), z), (cx + r1 * math.cos(a0), cy + r1 * math.sin(a0), z),
               (cx + r1 * math.cos(a1), cy + r1 * math.sin(a1), z), (cx + r0 * math.cos(a1), cy + r0 * math.sin(a1), z)]
        part.prism(pts, (0, 0, depth), rect)


def leaf(name, kind, side, thick):
    """Ein Fluegel; Drehpunkt an der Aussenkante (side = +1: Scharnier bei +x). Beide Seiten gleich beschlagen."""
    px = side * (HW - 0.005)
    inner = side * 0.005                                         # Spalt in der Mitte: 1 cm
    part = Part(name, (px, 0, 0), UV)
    lo, hi = sorted((px, inner))
    t = thick / 2
    part.box((lo, 0, -t), (hi, H, t), lambda n: "planks" if abs(n.z) > 0.9 else "edge")
    prod = 0.025
    if kind == "normal":
        for face in (-1, 1):
            z0, z1 = sorted((face * (t - 0.005), face * (t + prod)))
            for y in (0.55, 1.75, 2.95):
                strap(part, px - side * 0.005, side, y, 0.85, 0.075, z0, z1)
            rx = px - side * 0.76                                                            # Ring haengt an einer Platte
            ring(part, rx, 1.15, z0, 0.1, 0.03, z1 - z0)
            part.box((rx - 0.045, 1.25, z0), (rx + 0.045, 1.32, z1), "iron")
    if kind == "boss":
        for face in (-1, 1):
            zf = face * t
            z0, z1 = (zf, zf + face * 0.04) if face > 0 else (zf - 0.04, zf)
            # Eisenrahmen: Kopf- und Fussleiste und die Leiste an der Aussenkante, dazu zwei Querbalken
            part.box((lo, H - 0.22, z0), (hi, H - 0.04, z1), "iron")
            part.box((lo, 0.04, z0), (hi, 0.26, z1), "iron")
            ox0, ox1 = (px - side * 0.13, px) if side > 0 else (px, px + 0.13 + 0.0)
            part.box((min(ox0, ox1), 0.26, z0), (max(ox0, ox1), H - 0.22, z1), "iron")
            for y in (1.1, 2.3):
                part.box((lo, y - 0.07, z0), (hi, y + 0.07, z1), "iron")
            # Schaedel auf den Fluegel (zwei je Seite), mit Kiefer
            cx = px - side * 0.5
            for y in (2.75, 1.7):
                zs, ze = (zf + face * 0.04, zf + face * 0.14)
                a, b = sorted((zs, ze))
                part.box((cx - 0.17, y - 0.17, a), (cx + 0.17, y + 0.17, b), "bone")
                part.box((cx - 0.11, y - 0.28, a), (cx + 0.11, y - 0.17, b), "bone")
            # Dornen oben, nach aussen
            for yy in (H - 0.5,):
                for k in (-0.3, 0.3):
                    part.spike((cx + k, yy, zf + face * 0.04), (cx + k, yy, zf + face * 0.3), 0.05, "iron")
    return part


def locked_leaf():
    """Eine einzige Eisentuer, Scharnier bei +x. Platten (2 Spalten x 3 Reihen) auf beiden Seiten, drei Querbalken."""
    px = HW - 0.005
    part = Part("leaf", (px, 0, 0), UV)
    t = 0.07
    part.box((-px, 0, -t), (px, H, t), "edge")
    cols, rows = 2, 3
    gap = 0.03
    cw, rh = (2 * px - gap * (cols + 1)) / cols, (H - gap * (rows + 1)) / rows
    for face in (-1, 1):
        for i in range(cols):
            for j in range(rows):
                x0 = -px + gap + i * (cw + gap)
                y0 = gap + j * (rh + gap)
                a, b = (face * t, face * (t + 0.02))
                part.box((x0, y0, min(a, b)), (x0 + cw, y0 + rh, max(a, b)), "plate")
        a, b = (face * (t + 0.02), face * (t + 0.06))
        for y in (0.45, 1.75, 3.05):                                                          # drei Querbalken
            part.box((-px, y - 0.09, min(a, b)), (px, y + 0.09, max(a, b)), "iron")
        for x in (-0.65, 0.65):                                                               # zwei Senkrechte
            part.box((x - 0.07, 0.0, min(a, b)), (x + 0.07, H, max(a, b)), "iron")
    return part


def lock_node():
    """Schloss auf beiden Seiten, in Handhoehe, links der Mitte; Drehpunkt in der Mitte des Schlosskastens."""
    cx, cy = -0.55, 1.1
    part = Part("lock", (cx, cy, 0), UV)
    zt = 0.07 + 0.06
    part.box((cx - 0.2, cy - 0.17, -zt - 0.04), (cx + 0.2, cy + 0.17, zt + 0.04), lambda n: "lock" if abs(n.z) > 0.9 else "iron")
    # Buegel oben (Bogen aus Eckstuecken)
    for face in (-1, 1):
        z0, z1 = sorted((face * (zt + 0.04), face * (zt + 0.09)))
        part.box((cx - 0.12, cy + 0.17, z0), (cx - 0.07, cy + 0.3, z1), "iron")
        part.box((cx + 0.07, cy + 0.17, z0), (cx + 0.12, cy + 0.3, z1), "iron")
        part.box((cx - 0.12, cy + 0.3, z0), (cx + 0.12, cy + 0.35, z1), "iron")
    return part


def cracked_node():
    """Risse im Mauerwerk: duenne dunkle Streifen knapp vor beiden Flaechen der Wandzelle (Zelltiefe 2 m, also z = +-1)."""
    part = Part("cracks", (0, 0, 0), UV)
    lines = [  # (x0, y0, x1, y1, breite)
        (0.00, 1.15, 0.30, 1.45, 0.024), (0.30, 1.45, 0.34, 1.85, 0.018), (0.30, 1.45, 0.62, 1.55, 0.016),
        (0.00, 1.15, -0.25, 1.05, 0.022), (-0.25, 1.05, -0.32, 0.65, 0.02), (-0.25, 1.05, -0.55, 1.2, 0.014),
        (0.00, 1.15, 0.06, 0.70, 0.024), (0.06, 0.70, -0.05, 0.30, 0.018), (0.06, 0.70, 0.30, 0.55, 0.014),
        (0.34, 1.85, 0.2, 2.25, 0.012), (-0.32, 0.65, -0.5, 0.3, 0.012),
    ]
    for face in (-1, 1):
        z = face * (1.0 + 0.012)
        for x0, y0, x1, y1, wd in lines:
            dx, dy = x1 - x0, y1 - y0
            ln = math.hypot(dx, dy)
            nx, ny = -dy / ln * wd / 2, dx / ln * wd / 2
            pts = [(x0 - nx, y0 - ny, z), (x1 - nx, y1 - ny, z), (x1 + nx, y1 + ny, z), (x0 + nx, y0 + ny, z)]
            part.prism(pts, (0, 0, face * 0.004), "dark")
    return part


def glow_node():
    """Roter Spalt zwischen den Boss-Fluegeln: ein Streifen genau in der Mitte, hinter der Fuge (laesst sich einfaerben)."""
    part = Part("glow", (0, 0, 0), UV)
    part.box((-0.05, 0.3, -0.16), (0.05, H - 0.3, 0.16), "glow")
    return part


def parts_for(kind):
    if kind == "normal":
        return [leaf("leaf_a", "normal", +1, 0.14), leaf("leaf_b", "normal", -1, 0.14)]
    if kind == "boss":
        return [glow_node(), leaf("leaf_a", "boss", +1, 0.26), leaf("leaf_b", "boss", -1, 0.26)]
    if kind == "locked":
        return [locked_leaf(), lock_node()]
    return [cracked_node()]


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for kind in KINDS:
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
