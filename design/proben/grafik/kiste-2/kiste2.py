"""Kiste im neuen Stil - Probestueck 4 fuer Silver & Ash.

Ein Kistenmodell fuer alle vier Stufen wie Probestueck 2 (Kasten, Deckel mit Scharnier hinten, Schloss vorn,
Formmerkmal je Stufe auf dem Deckel), jetzt PS1-nah: Textur 64 x 64 je Stufe mit begrenzter Palette, ohne
Filterung; Holzbretter, Eisenbaender mit Nieten, Messingschloss. Harte Kanten (Holz und Metall), Licht je Flaeche.
    Beggar  - schlicht
    Guard   - Eckbeschlaege, eiserner Tragegriff
    King    - Eckbeschlaege, Kronen-Aufsatz
    Vampire - Eckbeschlaege, vier Dornen an den Deckelecken
Farben unveraendert aus ChestView.cs.

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/kiste-2/kiste2.py
"""

import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "gemeinsam"))
import bpy  # noqa: E402
from mathutils import Matrix  # noqa: E402
from psx import (V, G, Part, rects_from_pixels, vnoise, blob, mix, paint_atlas, save_image,  # noqa: E402
                 export_material, stone_textures, build_room, pass_material, write_light, raise_fwd,
                 setup_render, camera, aim, render_passes, compose, save_png, load_png, zoom, side_by_side)

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
OLD_DIR = os.path.join(os.path.dirname(OUT_DIR), "kiste")
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

def painters(body, band):
    body, band = np.array(body, float), np.array(band, float)

    def planks(U, V_):
        h, w = U.shape
        col = np.broadcast_to(body, (h, w, 3)).copy()
        row = np.floor(V_ * 4)
        grain = vnoise(h, w, 8, 3, 11) * 0.5 + vnoise(h, w, 32, 6, 12) * 0.5
        col *= (0.86 + 0.26 * grain)[..., None]
        col *= (0.92 + 0.16 * np.random.default_rng(13).random(5))[row.astype(int)][..., None]
        f = (V_ * 4) % 1.0
        col = mix(col, DARK, ((f < 0.07) | (f > 0.97)).astype(float) * 0.85)      # Fugen
        col *= (1 + 0.12 * ((f > 0.86) & (f < 0.97)))[..., None]                   # Oberkante heller
        for cu in (0.06, 0.94):                                                    # Nagelkoepfe
            for k in range(4):
                col = mix(col, DARK, (blob(U, V_, cu, (k + 0.5) / 4, 0.018, 0.035) > 0.3).astype(float) * 0.8)
        col = mix(col, DARK * 2, np.clip((0.18 - V_) * 3, 0, 0.5))               # Schmutz unten
        return col

    def iron(U, V_):
        h, w = U.shape
        col = np.broadcast_to(band, (h, w, 3)).copy()
        col *= (0.85 + 0.3 * vnoise(h, w, 20, 4, 21))[..., None]
        col *= (1 - 0.35 * (np.abs(U - 0.5) > 0.38))[..., None]                   # Kanten dunkler
        rivet = ((V_ * 6) % 1.0)
        r = blob(U, rivet, 0.5, 0.5, 0.22, 0.18)
        col = mix(col, band * 1.9 + 20, (r > 0.45).astype(float))
        col = mix(col, DARK, ((r > 0.05) & (r <= 0.45) & (rivet < 0.5)).astype(float) * 0.6)
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
    tex = paint_atlas(RECTS, painters(*TIERS[tier]), TEX, colors=24, seed=7)
    image = save_image(f"chest_{tier}", tex, os.path.join(OUT_DIR, f"textur_{tier}.png"))
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
    coll = bpy.data.collections.new("Kisten")
    bpy.context.scene.collection.children.link(coll)
    chests = {}
    for tier in TIERS:
        objs, image = build_chest(tier, coll)
        print("Dreiecke", tier, sum(len(o.data.polygons) for o in objs), {o.name: len(o.data.polygons) for o in objs})
        place(objs, 0)
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs:
            o.select_set(True)
        bpy.ops.export_scene.gltf(filepath=os.path.join(OUT_DIR, f"chest_{tier}.glb"), export_format="GLB",
                                  use_selection=True, export_yup=True, export_normals=True,
                                  export_vertex_color="NONE", export_materials="EXPORT", export_texcoords=True)
        mat = pass_material(f"pass_{tier}", image)
        for o in objs:
            o.data.materials[0] = mat
            o.name = f"{tier}_{o.name}"
        chests[tier] = objs

    cam = camera()
    king = chests["king"]

    # Vier Ansichten der King-Kiste.
    setup_render(640, 640)
    cam.data.angle_y = math.radians(30)
    place(king, 0)
    for o in king:
        write_light(o)
    target = G(V(0, 0.4, 0))
    for name, loc in {"ansicht_vorn": V(0, 0.9, 3.4), "ansicht_seite": V(3.4, 0.9, 0),
                      "ansicht_hinten": V(0, 0.9, -3.4), "ansicht_schraeg_oben": V(-2.0, 2.6, 2.2)}.items():
        aim(cam, G(loc), target)
        save_png(compose(render_passes(name, visible=king), "ansicht"), os.path.join(OUT_DIR, name + ".png"))

    stone = stone_textures(os.path.join(os.path.dirname(OUT_DIR), "gemeinsam"))
    room = build_room(stone, half=4.5)
    setup_render(854, 480)
    cam.data.angle_y = math.radians(70)

    row = []
    for tier, x in zip(TIERS, (-2.4, -0.8, 0.8, 2.4)):
        place(chests[tier], x)
        row += chests[tier]
    for o in row:
        write_light(o)
    for d in (4, 10):
        aim(cam, G(V(0, 1.6, d)), G(V(0, 0.35, 0)))
        passes = render_passes(f"stufen_{d}", visible=row + room)
        save_png(compose(passes, "nebel"), os.path.join(OUT_DIR, f"stufen_{d}m.png"))
        if d == 10:
            save_png(compose(passes, "dither"), os.path.join(OUT_DIR, "stufen_10m_dither.png"))

    shots = []
    for lid in (0, 110):
        place(king, 0, lid)
        for o in king:
            write_light(o)
        aim(cam, G(V(0, 1.6, 4)), G(V(0, 0.35, 0)))
        shots.append(compose(render_passes(f"king_{lid}", visible=king + room), "nebel"))
    save_png(side_by_side(shots), os.path.join(OUT_DIR, "king_geschlossen_offen_4m.png"))

    # Erste Kiste gegen neue: Reihe aus 4 m, Ausschnitt vergroessert.
    old = load_png(os.path.join(OLD_DIR, "stufen_4m.png"))
    new = load_png(os.path.join(OUT_DIR, "stufen_4m.png"))
    save_png(np.concatenate([zoom(old, (190, 310), (150, 710), 2), np.zeros((6, 1120, 3)),
                             zoom(new, (190, 310), (150, 710), 2)], axis=0),
             os.path.join(OUT_DIR, "vergleich_alt_neu_4m.png"))


main()
