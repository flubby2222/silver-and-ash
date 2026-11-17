"""Kiste in vier Stufen (Beggar, Guard, King, Vampire)

Erzeuger fuer das Spiel (Uebergabe aus dem Probestueck kiste-2, Branch claude/design-probe-ghul-45a8c4).
Erzeugt nur Modell und Textur nach assets/models/, keine Probebilder; die Bilder entstehen im Probestueck-Skript.

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/chest.py

Herkunft: alles per Skript, nichts heruntergeladen. Teile, Drehpunkte und Haltungen: design/uebergabe/README.md.
"""


import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "..", "tools", "models"))
import bpy  # noqa: E402
from mathutils import Matrix  # noqa: E402
from psx import export_glb, report_parts  # noqa: E402
from psx import (V, G, Part, rects_from_pixels, vnoise, blob, mix, paint_atlas, save_image,  # noqa: E402
                 export_material, stone_textures, build_room, pass_material, write_light, raise_fwd,
                 setup_render, camera, aim, render_passes, compose, save_png, load_png, zoom, side_by_side)

# Modelle und Texturen landen in assets/models (vom Skript in tools/models aus zwei Ebenen hoch).
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "modelle")
TEX = 128

# Runde 2 (Leitung): gedeckte Toene, die in die Katakomben gehoeren; die Stufenfarbe ist nur noch Akzent (hoechstens
# etwa ein Fuenftel der Flaeche). Je Stufe: Holz, Metall der Baender, Akzent.
TIERS = {
    "beggar": {"wood": (100, 92, 84), "band": (92, 62, 46), "accent": (128, 80, 50)},       # graues Holz, Rost
    "guard": {"wood": (62, 46, 36), "band": (64, 66, 78), "accent": (132, 152, 180)},       # dunkle Eiche, Eisen, Stahl
    "king": {"wood": (58, 38, 30), "band": (150, 118, 60), "accent": (206, 164, 70)},       # poliertes Holz, Messing, Gold
    "vampire": {"wood": (50, 42, 54), "band": (70, 66, 80), "accent": (156, 24, 38)},       # schwarz gebeizt, Rot als Siegel
}
BRASS = np.array((176, 140, 72), float)
DARK = np.array((18, 14, 14), float)

# Atlas 128 x 128 (Pixel, y von unten). Der Deckel hat eigene Felder, weil seine Bretter ueber die Woelbung laufen.
RECTS = {
    "planks": (0, 64, 128, 128),    # Bretter des Kastens: vier Reihen, 128 x 64
    "lidplanks": (0, 0, 64, 64),    # Bretter des Deckels: zehn Reihen entlang der Woelbung
    "lidband": (64, 0, 80, 64),     # Band ueber die Woelbung
    "band": (80, 32, 96, 64),       # Band am Kasten
    "metal": (80, 0, 96, 32),       # Zierrat (Henkel, Krone, Dornen)
    "lock": (96, 32, 128, 64),      # Schlossplatte
    "inside": (96, 0, 128, 32),     # Innenseite
}
UV = rects_from_pixels(RECTS, TEX)

WIDTH, HEIGHT, DEPTH = 1.0, 0.7, 0.7
BASE_H = HEIGHT * 0.65
HW, HD = WIDTH / 2, DEPTH / 2
OVER, PROUD = 0.015, 0.012
HINGE = (0.0, BASE_H, -HD - OVER)
LOCK_PIVOT = (0.0, BASE_H + 0.015, HD + 0.04)
BAND_X, BAND_HALF = 0.3, 0.04


# --- Textur -------------------------------------------------------------------------------------------------------

def painters(tier):
    P = TIERS[tier]
    wood, band, accent = (np.array(P[k], float) for k in ("wood", "band", "accent"))
    seed = {"beggar": 1, "guard": 2, "king": 3, "vampire": 4}[tier]

    def boards(U, V_, rows):
        """Bretter in `rows` Reihen uebereinander (v), Maserung entlang u. Wenige grosse Formen: dunkle Fugen mit
        heller Oberkante, je Brett eigener Ton, zwei bis drei lange Maserlinien, kein Rauschen."""
        h, w = U.shape
        rng = np.random.default_rng(200 + seed * 7 + rows)
        row = np.minimum(np.floor(V_ * rows), rows - 1).astype(int)
        f = (V_ * rows) % 1.0
        col = np.broadcast_to(wood, (h, w, 3)).copy()
        col *= (0.84 + 0.3 * rng.random(rows))[row][..., None]                           # Ton je Brett
        col *= (0.94 + 0.12 * vnoise(h, w, 6, 3, 300 + seed))[..., None]                 # sehr weiche Fleckung
        for r in range(rows):                                                            # lange Maserlinien
            for k in range(3):
                fp = rng.uniform(0.2, 0.8)
                wob = 0.035 * np.sin(U * 2 * math.pi * rng.uniform(0.6, 1.4) + rng.uniform(0, 6))
                m = (row == r) & (np.abs(f - fp - wob) < 0.035)
                col *= (1 - 0.28 * m)[..., None]
        if tier == "king":                                                               # poliert: Glanzbahn je Brett
            col = mix(col, wood * 1.55 + 14, ((f > 0.5) & (f < 0.64)).astype(float) * 0.45)
        if tier == "beggar" and rows == 4:                                               # rissig: Risse quer durch die Bretter
            for r in range(rows):
                if rng.random() < 0.7:
                    x = rng.uniform(0.1, 0.9)
                    xs = x + 0.04 * np.cumsum(rng.normal(0, 1, 64)) / 8
                    for fy in range(64):
                        ff = (fy + 0.5) / 64
                        m = (row == r) & (np.abs(f - (0.1 + 0.8 * ff)) < 0.04) & (np.abs(U - xs[fy]) < 0.012)
                        col = mix(col, DARK * 1.3, m.astype(float) * 0.9)
        col = mix(col, DARK, ((f < 0.08) | (f > 0.975)).astype(float) * 0.95)             # Fugen
        col *= (1 + (0.8 if tier == "vampire" else 0.32) * ((f >= 0.08) & (f < 0.17)))[..., None]   # helle Kante unter der Fuge (Vampire kraeftiger: die Form muss stehen)
        if rows == 4:
            col = mix(col, DARK * 2.2, np.clip((0.18 - V_) * 2.0, 0, 0.4))               # Schmutz unten (Kasten)
        if tier == "king" and rows == 4:                                                 # Zierkante: Messinglinien
            col = mix(col, accent, ((np.abs(V_ - 0.25) < 0.025) | (np.abs(V_ - 0.75) < 0.025)).astype(float) * 0.75)
        return col

    def planks(U, V_):
        return boards(U, V_, 4)

    def lidplanks(U, V_):
        return boards(U, V_, 10)

    def make_band(rivets):
        def iron(U, V_):
            h, w = U.shape
            edge = np.minimum(U, 1 - U)
            col = np.broadcast_to(band, (h, w, 3)).copy()
            col *= (0.88 + 0.22 * vnoise(h, w, 16, 3, 400 + seed))[..., None]
            col *= (1 + 0.45 * (edge < 0.12))[..., None]                                 # heller Rand: Kante lesbar
            col *= (1 - 0.4 * ((edge >= 0.12) & (edge < 0.2)))[..., None]
            rv = (V_ * rivets) % 1.0
            big = 0.34 if tier in ("guard", "king") else 0.24
            r = blob(U, rv, 0.5, 0.5, big, big * 0.8)
            head = band * 1.9 + 28 if tier != "vampire" else band * 1.5 + 10
            col = mix(col, head, (r > 0.42).astype(float))                               # Nietenkopf
            col = mix(col, DARK, ((r > 0.1) & (r <= 0.42) & (rv > 0.5)).astype(float) * 0.7)
            if tier == "beggar":                                                         # schmales rostiges Band im Holz
                strap = (U > 0.28) & (U < 0.72)
                col = mix(wood * 0.8, col, strap.astype(float))
                col = mix(col, np.array((130, 70, 40.0)), np.clip((vnoise(h, w, 8, 3, 410) - 0.55) * 3, 0, 0.5) * strap)
            if tier == "guard":                                                          # kuehler Stahl als Akzent
                col = mix(col, accent, ((np.abs(U - 0.5) < 0.05) & (np.abs(rv - 0.5) > 0.3)).astype(float) * 0.35)
            if tier == "king":                                                           # Messing: Perlen
                col = mix(col, accent, (blob(U, rv, 0.5, 0.5, 0.18, 0.1) > 0.5).astype(float) * 0.6)
            if tier == "vampire":                                                        # breites Blutrot, schwarze Raender
                col = mix(col, accent, (np.abs(U - 0.5) < 0.3).astype(float) * 0.95)
                col = mix(col, band * 2.0 + 20, ((np.abs(U - 0.5) > 0.3) & (np.abs(U - 0.5) < 0.38)).astype(float) * 0.8)
            return col
        return iron

    def lock(U, V_):
        h, w = U.shape
        base = {"beggar": band * 0.7, "guard": band, "king": accent * 0.9, "vampire": band}[tier]
        col = np.broadcast_to(base, (h, w, 3)).copy()
        col *= (0.85 + 0.25 * vnoise(h, w, 6, 6, 31 + seed))[..., None]
        edge = (U < 0.1) | (U > 0.9) | (V_ < 0.1) | (V_ > 0.9)
        col = mix(col, DARK, edge.astype(float) * 0.75)
        col *= (1 + 0.25 * ((V_ > 0.75) & ~edge))[..., None]
        hole = (blob(U, V_, 0.5, 0.6, 0.12, 0.12) > 0.1) | ((np.abs(U - 0.5) < 0.05) & (V_ > 0.28) & (V_ < 0.6))
        if tier == "vampire":                                                            # Siegel: tiefrot
            col = mix(col, accent, (blob(U, V_, 0.5, 0.6, 0.3, 0.3) > 0.45).astype(float) * 0.9)
        col = mix(col, DARK * 0.5, hole.astype(float))
        for cu, cv in ((0.2, 0.2), (0.8, 0.2), (0.2, 0.8), (0.8, 0.8)):
            col = mix(col, base * 1.5 + 14, (blob(U, V_, cu, cv, 0.06, 0.06) > 0.3).astype(float))
        return col

    def inside(U, V_):
        h, w = U.shape
        col = np.broadcast_to(wood * 0.45 + DARK * 0.5, (h, w, 3)).copy()
        col *= (0.8 + 0.3 * vnoise(h, w, 4, 12, 41))[..., None]
        return mix(col, DARK, (((U * 3) % 1.0) < 0.08).astype(float))

    def metal(U, V_):
        h, w = U.shape
        col = np.broadcast_to(band if tier != "king" else accent * 0.85, (h, w, 3)).copy()
        col *= (0.75 + 0.45 * np.abs(np.sin(V_ * 7 + U * 2)))[..., None]
        col *= (0.9 + 0.2 * vnoise(h, w, 10, 10, 51))[..., None]
        if tier == "vampire":                                                            # geschwaerztes Eisen mit Glanzkante
            col = mix(col, band * 2.3 + 30, (((V_ * 3) % 1.0) < 0.22).astype(float) * 0.85)
        return col

    return {"planks": planks, "lidplanks": lidplanks, "band": make_band(5), "lidband": make_band(14),
            "lock": lock, "inside": inside, "metal": metal}


# --- Geometrie ----------------------------------------------------------------------------------------------------

def lid_profile(grow=0.0):
    z, top, edge, flat = HD + OVER + grow, HEIGHT - BASE_H + grow, 0.11 + grow * 0.4, 0.23 + grow * 0.4
    return [(-z, 0.0), (z, 0.0), (z, edge), (flat, top), (-flat, top), (-z, edge)]


def under_inside(rect):
    """Unterseite (Normale nach unten) bekommt die Innenseite."""
    return lambda n: "inside" if n.y < -0.9 else rect


def build_base(tier):
    part = Part("base", (0, 0, 0), UV)
    # Kasten: Oberseite ist der Innenraum.
    part.box((-HW, 0, -HD), (HW, BASE_H, HD), lambda n: "inside" if n.y > 0.9 else "planks")
    for s in (-1, 1):
        x = s * BAND_X
        part.box((x - BAND_HALF, 0.0, -HD - PROUD), (x + BAND_HALF, BASE_H, HD + PROUD), "band")
    part.box((-0.22, BASE_H - 0.05, -HD - 0.03), (0.22, BASE_H, -HD), "band")       # Scharnierleiste hinten
    if tier != "beggar":
        for sx in (-1, 1):
            for sz in (-1, 1):
                cx, cz, e, w = sx * HW, sz * HD, PROUD, 0.09
                tri = [(cx + sx * e, 0.0, cz + sz * e), (cx - sx * w, 0.0, cz + sz * e), (cx + sx * e, 0.0, cz - sz * w)]
                part.prism(tri, (0, BASE_H, 0), "band")
    return part


def relayout_lid(part, name, grow, width, x0):
    """Setzt die UV der Deckelflaechen mit dem Rechteck `name` neu: u quer (x), v nach der Bogenlaenge des Profils
    (hinten unten ueber die Woelbung nach vorn unten), damit die Bretter ueber die Woelbung laufen statt gestreckt."""
    prof = lid_profile(grow)                                       # (z, y) Punkte 0..5
    order = [1, 2, 3, 4, 5, 0]                                     # Aussenseite ohne Unterseite
    seg = [math.hypot(prof[b][0] - prof[a][0], prof[b][1] - prof[a][1]) for a, b in zip(order, order[1:])]
    arc = {order[0]: 0.0}
    for k in range(1, len(order)):
        arc[order[k]] = arc[order[k - 1]] + seg[k - 1]
    total = arc[order[-1]]
    u0, v0, u1, v1 = part.rects[name]
    for f in part.bm.faces:
        cu = sum(l[part.uv].uv[0] for l in f.loops) / len(f.loops)
        cv = sum(l[part.uv].uv[1] for l in f.loops) / len(f.loops)
        if not (u0 <= cu <= u1 and v0 <= cv <= v1):
            continue
        pts = {}
        for l in f.loops:
            co = l.vert.co                                         # Blender-Achsen relativ zum Drehpunkt
            z, y = -co.y + part.pivot.z, co.z + part.pivot.y - BASE_H      # Spiel-Achsen: z und Hoehe ueber dem Kasten
            pts.setdefault((round(z, 5), round(y, 5)), []).append(l)
        if len(pts) != 2:
            continue                                               # Stirnflaechen bleiben wie sie sind
        for (z, y), loops in pts.items():
            idx = min(arc, key=lambda i: math.hypot(prof[i][0] - z, prof[i][1] - y))
            for l in loops:
                x = l.vert.co.x + part.pivot.x
                l[part.uv].uv = (u0 + (u1 - u0) * (x - x0) / width, v0 + (v1 - v0) * arc[idx] / total)



def build_lid(tier):
    part = Part("lid", HINGE, UV, parent="base")
    profile = [(-HW - OVER, y + BASE_H, z) for z, y in lid_profile()]
    part.prism(profile, (2 * (HW + OVER), 0, 0), under_inside("lidplanks"))
    relayout_lid(part, "lidplanks", 0.0, 2 * (HW + OVER), -HW - OVER)
    for s in (-1, 1):
        band = [(s * BAND_X - BAND_HALF, y + BASE_H, z) for z, y in lid_profile(PROUD)]
        part.prism(band, (2 * BAND_HALF, 0, 0), under_inside("lidband"))
        relayout_lid(part, "lidband", PROUD, 2 * BAND_HALF, s * BAND_X - BAND_HALF)
    top = HEIGHT + PROUD
    if tier == "guard":
        for x in (-0.19, 0.19):
            part.box((x - 0.025, top - 0.01, -0.025), (x + 0.025, top + 0.1, 0.025), "band")
        part.box((-0.23, top + 0.1, -0.03), (0.23, top + 0.15, 0.03), "band")
    if tier == "king":
        part.box((-0.25, top - 0.01, -0.035), (0.25, top + 0.07, 0.035), "metal")
        for x, h in ((-0.17, 0.13), (0.0, 0.21), (0.17, 0.13)):
            part.spike((x, top + 0.07, 0.0), (x, top + 0.07 + h, 0.0), 0.06, "metal", sides=3)
    if tier == "vampire":
        for sx in (-1, 1):
            for sz in (-1, 1):
                part.spike((sx * (HW - 0.06), top - 0.02, sz * 0.17), (sx * (HW + 0.04), top + 0.2, sz * 0.24),
                           0.045, "metal")
    return part


def build_lock():
    part = Part("lock", LOCK_PIVOT, UV, parent="base")
    z0 = HD + OVER
    part.box((-0.08, BASE_H - 0.17, z0), (0.08, BASE_H + 0.03, z0 + 0.04), "lock")
    return part


def build_chest(tier, collection):
    tex = paint_atlas(RECTS, painters(tier), TEX, colors=32, seed=7)
    image = save_image(f"chest_{tier}", tex, os.path.join(OUT_DIR, f"chest_{tier}.png"))
    mat = export_material(f"chest_{tier}", image)
    objs = [p.build(collection, mat, smooth=False) for p in (build_base(tier), build_lid(tier), build_lock())]
    return objs, image


def place(objs, x, lid_open=0.0):
    """Kiste bei x aufstellen; Deckel um lid_open Grad nach hinten klappen (um das Scharnier)."""
    root = Matrix.Translation(G(V(x, 0, 0)))
    for o in objs:
        pivot = V(*list(o["pivot"]))
        m = root @ Matrix.Translation(G(pivot))
        if o.name.endswith("lid"):
            m = m @ raise_fwd(lid_open)
        o.matrix_world = m
    bpy.context.view_layer.update()


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    os.makedirs(OUT_DIR, exist_ok=True)
    coll = bpy.data.collections.new("Kisten")
    bpy.context.scene.collection.children.link(coll)
    for tier in TIERS:
        objs, _ = build_chest(tier, coll)
        place(objs, 0)
        export_glb(objs, os.path.join(OUT_DIR, f"chest_{tier}.glb"))
        report_parts(f"chest_{tier}", objs)
        for o in objs:
            o.name = f"{tier}_{o.name}"
            o.data.name = o.name


main()
