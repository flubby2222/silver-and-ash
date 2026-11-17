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
TEX = 64

TIERS = {
    #           Koerper           Baender/Beschlaege
    "beggar": ((110, 76, 46), (56, 44, 36)),
    "guard": ((92, 112, 140), (52, 58, 70)),
    "king": ((214, 170, 64), (140, 100, 36)),
    "vampire": ((175, 25, 35), (40, 10, 14)),    # pulsiert im Spiel 150..200; hier die Mitte
}
BRASS = np.array((176, 140, 72), float)
DARK = np.array((18, 14, 14), float)

# Atlas 64 x 64 (Pixel, y von unten).
RECTS = {
    "planks": (0, 32, 64, 64),     # Bretter (Kasten und Deckel)
    "band": (0, 0, 16, 32),        # Eisenband mit Nieten
    "lock": (16, 16, 32, 32),      # Messingplatte mit Schluesselloch
    "inside": (16, 0, 32, 16),     # Innenseite: dunkles Holz
    "metal": (32, 0, 64, 32),      # Zierrat in Koerperfarbe (Krone, Dornen)
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

def painters(body, band, tier="beggar"):
    body, band = np.array(body, float), np.array(band, float)
    seed = {"beggar": 1, "guard": 2, "king": 3, "vampire": 4}[tier]

    def planks(U, V_):
        """Vier Bretter uebereinander (v), Maserung entlang u. Fassung 3 (Issue #258): kraeftiger Kontrast, je Brett
        eigener Ton, Astloecher, Stossfugen, tiefe Fugen mit heller Kante, Schmutz unten, Abnutzung."""
        h, w = U.shape
        rng = np.random.default_rng(100 + seed)
        col = np.broadcast_to(body, (h, w, 3)).copy()
        row = np.minimum(np.floor(V_ * 4), 3).astype(int)
        f = (V_ * 4) % 1.0
        tone = (0.78 + 0.36 * rng.random(4))[row]
        col *= tone[..., None]
        warp = vnoise(h, w, 12, 3, 11 + seed)
        ring = np.abs(np.sin((f * 5.0 + 3.0 * warp + row * 1.7) * math.pi))           # Jahresringe als Streifen
        coarse = vnoise(h, w, 14, 4, 21 + seed)
        col *= (0.62 + 0.42 * ring + 0.3 * coarse)[..., None] ** 0.9 * 1.12
        # Stossfugen: je Brett eine senkrechte Fuge an anderer Stelle
        for r in range(4):
            x = rng.uniform(0.2, 0.8)
            m = (row == r) & (np.abs(U - x) < 0.012)
            col = mix(col, DARK, m.astype(float) * 0.85)
        # Astloecher
        for _ in range(3):
            r = int(rng.integers(0, 4))
            ku, kv = rng.uniform(0.15, 0.85), (r + 0.5) / 4
            k = blob(U, V_, ku, kv, 0.045, 0.05)
            col = mix(col, DARK * 1.6, (k > 0.35).astype(float) * 0.9)
            col = mix(col, body * 1.25, ((k > 0.12) & (k <= 0.35)).astype(float) * 0.7)
        # Fugen zwischen den Brettern: tief, mit heller Kante darunter
        col = mix(col, DARK, ((f < 0.09) | (f > 0.96)).astype(float) * 0.92)
        col *= (1 + 0.28 * ((f >= 0.09) & (f < 0.2)))[..., None]
        # Naegel an beiden Seiten
        for cu in (0.05, 0.95):
            for k in range(4):
                col = mix(col, band * 1.7 + 25, (blob(U, V_, cu, (k + 0.55) / 4, 0.02, 0.04) > 0.35).astype(float) * 0.9)
        # Abnutzung: heller an den Kanten links und rechts, Schmutz unten, dunkle Flecken
        col *= (1 + 0.22 * ((U < 0.03) | (U > 0.97)))[..., None]
        col = mix(col, DARK * 2.5, np.clip((0.22 - V_) * 3.2, 0, 0.6))
        col = mix(col, DARK * 2, np.clip((vnoise(h, w, 5, 6, 61 + seed) - 0.62) * 3, 0, 0.55))
        if tier == "king":                                                            # vergoldet: Leisten, Zierfelder
            col = mix(col, body * 1.15 + 18, (np.abs(U - 0.5) < 0.02).astype(float) * 0.6)
            for cu in (0.2, 0.35, 0.65, 0.8):
                col = mix(col, DARK * 2.2, ((np.abs(U - cu) < 0.018) & (f > 0.15) & (f < 0.9)).astype(float) * 0.55)
        if tier == "vampire":                                                         # Blut: Laufspuren von oben
            drip = np.clip((vnoise(h, w, 2, 14, 81) - 0.55) * 4, 0, 1) * np.clip(1.3 - V_ * 1.6, 0, 1)
            col = mix(col, np.array((110.0, 8, 16)), drip * 0.8)
        if tier == "guard":                                                           # Wache: kaltes Stahlblech statt Holz
            col = mix(col, band * 2.0 + 30, ((f > 0.2) & (f < 0.45)).astype(float) * 0.32)   # Glanzband je Brett
        return col

    def iron(U, V_):
        h, w = U.shape
        col = np.broadcast_to(band, (h, w, 3)).copy()
        col *= (0.8 + 0.4 * vnoise(h, w, 20, 4, 21))[..., None]
        edge = np.minimum(U, 1 - U)
        col *= (1 + 0.5 * (edge < 0.1))[..., None]                                    # heller Rand: Kante lesbar
        col *= (1 - 0.5 * (edge > 0.46) * 0)[..., None]
        col *= (1 - 0.35 * (np.abs(U - 0.5) > 0.4) * (edge > 0.1))[..., None]
        rivet = ((V_ * 5) % 1.0)
        r = blob(U, rivet, 0.5, 0.5, 0.34, 0.2)
        col = mix(col, band * 2.1 + 40, (r > 0.4).astype(float))                      # Niete: heller Kopf
        col = mix(col, DARK, ((r > 0.08) & (r <= 0.4) & (rivet > 0.5)).astype(float) * 0.8)  # Schatten darunter
        col = mix(col, np.array((120.0, 60, 30)), np.clip((vnoise(h, w, 10, 3, 22 + seed) - 0.6) * 3, 0, 0.5))  # Rost
        return col

    def lock(U, V_):
        h, w = U.shape
        col = np.broadcast_to(BRASS, (h, w, 3)).copy()
        col *= (0.85 + 0.25 * vnoise(h, w, 6, 6, 31))[..., None]
        edge = (U < 0.1) | (U > 0.9) | (V_ < 0.1) | (V_ > 0.9)
        col = mix(col, DARK, edge.astype(float) * 0.75)                           # dunkler Rand trennt vom Gold
        col *= (1 + 0.2 * ((V_ > 0.75) & ~edge))[..., None]
        hole = (blob(U, V_, 0.5, 0.6, 0.12, 0.12) > 0.1) | ((np.abs(U - 0.5) < 0.05) & (V_ > 0.28) & (V_ < 0.6))
        col = mix(col, DARK * 0.5, hole.astype(float))
        for cu, cv in ((0.2, 0.2), (0.8, 0.2), (0.2, 0.8), (0.8, 0.8)):
            col = mix(col, BRASS * 1.3, (blob(U, V_, cu, cv, 0.06, 0.06) > 0.3).astype(float))
        return col

    def inside(U, V_):
        h, w = U.shape
        col = np.broadcast_to(body * 0.5 + DARK * 0.5, (h, w, 3)).copy()
        col *= (0.8 + 0.3 * vnoise(h, w, 4, 12, 41))[..., None]
        col = mix(col, DARK, (((U * 3) % 1.0) < 0.08).astype(float))
        return col

    def metal(U, V_):
        h, w = U.shape
        col = np.broadcast_to(body, (h, w, 3)).copy()
        col *= (0.8 + 0.4 * np.abs(np.sin(V_ * 7 + U * 2)))[..., None]            # Glanzstreifen
        col *= (0.9 + 0.2 * vnoise(h, w, 10, 10, 51))[..., None]
        return col

    return {"planks": planks, "band": iron, "lock": lock, "inside": inside, "metal": metal}


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


def build_lid(tier):
    part = Part("lid", HINGE, UV, parent="base")
    profile = [(-HW - OVER, y + BASE_H, z) for z, y in lid_profile()]
    part.prism(profile, (2 * (HW + OVER), 0, 0), under_inside("planks"))
    for s in (-1, 1):
        band = [(s * BAND_X - BAND_HALF, y + BASE_H, z) for z, y in lid_profile(PROUD)]
        part.prism(band, (2 * BAND_HALF, 0, 0), under_inside("band"))
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
    tex = paint_atlas(RECTS, painters(*TIERS[tier], tier=tier), TEX, colors=32, seed=7)
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
