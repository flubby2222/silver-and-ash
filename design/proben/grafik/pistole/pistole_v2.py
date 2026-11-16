"""Pistole des Jaegers im Bild - Probestueck 5, Fassung 2 (Ich-Sicht neu) fuer Silver & Ash.

Fassung 2: Die Waffe zeigt mit dem Lauf in die Tiefe auf die Bildmitte (Ziel 1,5 m voraus), ist zum Auge hin
gekippt (man blickt auf Hahn, Schloss und Laufruecken), 1,35-mal so gross wie zuerst und liegt in einer Hand im
Handschuh mit Aermel. Neu: silberne Griffplatte oben hinter dem Lauf. Fassung 1: pistole.py.


Eine alte Steinschlosspistole mit Silberbeschlaegen aus der Ich-Sicht, PS1-nah: Textur 128 x 128 mit begrenzter
Palette ohne Filterung, Gouraud auf Schaft, harte Kanten an Lauf (achtkantig) und Beschlaegen.
Lage, Groesse und Rueckstoss wie der Platzhalter in WeaponViews.cs: Griff bei (0,095, -0,08, 0,18) im
Kameraraum (x rechts, y oben, z vorn), Rueckstoss 14 Grad hoch, 1,2 cm hoch und 3,5 cm zurueck.

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/pistole/pistole.py
"""

import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "gemeinsam"))
import bpy  # noqa: E402
from mathutils import Matrix  # noqa: E402
from arm import build_hand as build_glove_hand, paint_glove2, paint_cuff, paint_sleeve  # noqa: E402
from psx import (V, G, Part, tube, rects_from_pixels, vnoise, blob, mix, paint_atlas, save_image,  # noqa: E402
                 export_material, stone_textures, build_room, pass_material, write_light, pose, raise_fwd,
                 setup_render, camera, aim, render_passes, compose, save_png, load_png, side_by_side, zoom)

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
TEX = 128

WOOD = np.array((84, 56, 38), float)          # Nussbaum, wie der Platzhalter
IRON = np.array((58, 56, 64), float)          # gebraeuntes Eisen, wie der Platzhalter
SILVER = np.array((206, 210, 222), float)     # Silber des Jaegers: hell, kuehl, fast weiss
TARNISH = np.array((92, 92, 104), float)      # angelaufenes Silber in Vertiefungen
STEEL = np.array((112, 114, 124), float)      # Hahn und Batterie
DARK = np.array((14, 12, 14), float)
FLASH = np.array((255, 232, 170), float)      # Muendungsfeuer wie im Platzhalter

RECTS = {
    "wood": (0, 64, 64, 128),
    "iron": (64, 96, 128, 128),
    "silver": (64, 64, 128, 96),
    "bore": (0, 48, 16, 64),
    "steel": (16, 48, 48, 64),
    "flash": (48, 48, 64, 64),
    "glove": (0, 16, 64, 48),
    "cuff": (0, 0, 64, 16),
    "sleeve": (64, 0, 128, 48),
}
UV = rects_from_pixels(RECTS, TEX)

PISTOL_HAND = (0.095, -0.08, 0.18)            # Kameraraum, aus WeaponViews.cs
AIM_DISTANCE = 1000.0                         # Lauf parallel zur Blickachse: er laeuft im Bild auf die Bildmitte zu
CANT = 15.0                                   # Grad: Oberseite zum Auge hin gekippt
SCALE = 1.8                                   # echte Groesse einer Reiterpistole samt Hand (Modell ist halb so gross gebaut)
EYE = V(0, 1.6, -6.0)                          # Kamera im Gang, Blick nach +z


def C(xr, yu, zf):
    """Kameraraum (x rechts, y oben, z vorn) -> Spiel-Achsen relativ zur Kamera mit Blick nach +z.
    Im Spiel ist rechts = -x (WeaponViews.CameraToWorld); hier ohne Spiegelung gebaut."""
    return V(-xr, yu, zf)


# --- Textur -------------------------------------------------------------------------------------------------------

def paint_wood(U, V_):
    h, w = U.shape
    col = np.broadcast_to(WOOD, (h, w, 3)).copy()
    grain = vnoise(h, w, 60, 5, 1) * 0.6 + vnoise(h, w, 12, 3, 2) * 0.4          # Maserung entlang des Schafts
    col *= (0.8 + 0.42 * grain)[..., None]
    top = 1 - np.abs(np.cos(np.pi * U))
    col *= (0.8 + 0.3 * top)[..., None]                                            # abgegriffen oben heller
    col = mix(col, WOOD * 1.5, np.clip((vnoise(h, w, 8, 8, 3) - 0.7) * 3, 0, 0.5))
    rng = np.random.default_rng(4)
    for _ in range(14):                                                            # Kratzer
        u, v, a = rng.random(), rng.random(), rng.uniform(0, math.pi)
        t = np.linspace(-0.04, 0.04, 30)
        for du, dv in zip(t * math.cos(a), t * math.sin(a) * 2):
            col = mix(col, WOOD * 1.6, (blob(U, V_, u + du, v + dv, 0.006, 0.006) > 0.2).astype(float))
    # Silberdraht-Einlage: Ranke auf beiden Flanken.
    for cu in (0.25, 0.75):
        curve = cu + 0.05 * np.sin(V_ * 18)
        col = mix(col, SILVER * 0.9, ((np.abs(U - curve) < 0.008) & (V_ > 0.25) & (V_ < 0.75)).astype(float))
    return col


def paint_iron(U, V_):
    h, w = U.shape
    col = np.broadcast_to(IRON, (h, w, 3)).copy()
    col *= (0.8 + 0.4 * vnoise(h, w, 40, 16, 11))[..., None]                       # Narben, Rost
    col = mix(col, np.array((80, 58, 44.0)), np.clip((vnoise(h, w, 10, 6, 12) - 0.6) * 2.5, 0, 0.5))
    flat = ((U * 8) % 1.0)
    col *= (1 + 0.18 * (flat > 0.85))[..., None]                                   # Kanten der acht Flaechen
    top = np.abs(U - 0.5) < 0.07                                                   # Silberlinien oben
    col = mix(col, SILVER, (top & (np.abs(((U - 0.5) / 0.07)) > 0.6)).astype(float) * 0.9)
    col = mix(col, SILVER, ((V_ < 0.12) & (((V_ * 40) % 1.0) < 0.5)).astype(float) * 0.8)   # Ring am Pulversack
    return col


def paint_silver(U, V_):
    h, w = U.shape
    col = np.broadcast_to(SILVER, (h, w, 3)).copy()
    col = mix(col, TARNISH, np.clip(vnoise(h, w, 6, 6, 21) * 1.2 - 0.3, 0, 0.8))   # angelaufen
    col *= (0.85 + 0.25 * np.abs(np.sin(U * 9 + V_ * 3)))[..., None]              # Glanz
    col = mix(col, SILVER * 1.15, ((np.abs(V_ - 0.5 - 0.1 * np.sin(U * 12)) < 0.03)).astype(float) * 0.7)
    return col


def paint_steel(U, V_):
    h, w = U.shape
    col = np.broadcast_to(STEEL, (h, w, 3)).copy()
    col *= (0.75 + 0.45 * vnoise(h, w, 8, 8, 31))[..., None]
    return col


PAINTERS = {"glove": paint_glove2, "cuff": paint_cuff, "sleeve": paint_sleeve, "wood": paint_wood, "iron": paint_iron, "silver": paint_silver, "steel": paint_steel,
            "bore": lambda U, V_: np.broadcast_to(DARK, U.shape + (3,)).copy(),
            "flash": lambda U, V_: mix(np.broadcast_to(FLASH, U.shape + (3,)).copy(), np.array((255, 255, 240.0)),
                                       blob(U, V_, 0.5, 0.5, 0.6, 0.6))}


# --- Geometrie ----------------------------------------------------------------------------------------------------

def build_stock():
    part = Part("stock", (0, 0, 0), UV)
    # Griff: vom Schloss nach unten hinten, Kolben verdickt (Vogelkopf).
    grip = [C(0, 0.004, 0.024), C(0, -0.012, 0.008), C(0, -0.032, -0.006), C(0, -0.052, -0.016), C(0, -0.066, -0.020)]
    part.loft(tube(grip, [(0.011, 0.012, 0.012), (0.010, 0.012, 0.012), (0.011, 0.014, 0.013),
                          (0.013, 0.017, 0.015), (0.0135, 0.018, 0.016)], 8, ref_up=V(0, 0, 1)), "wood")
    # Schaft unter dem Lauf bis kurz vor die Muendung.
    # Letzter Ring dicht am vorletzten: die Kappe bleibt stumpf statt spitz.
    fore = [C(0, -0.002, 0.012), C(0, -0.004, 0.06), C(0, -0.005, 0.123), C(0, -0.005, 0.125)]
    part.loft(tube(fore, [(0.0105, 0.010, 0.006), (0.009, 0.009, 0.005), (0.0075, 0.007, 0.004),
                          (0.0068, 0.0064, 0.0036)], 8), "wood")
    return part


def build_barrel():
    part = Part("barrel", (0, 0, 0), UV, parent="stock")
    pts = [C(0, 0.007, 0.02), C(0, 0.007, 0.09), C(0, 0.007, 0.152)]
    part.loft(tube(pts, [(0.0088, 0.0088, 0.0088), (0.0078, 0.0078, 0.0078), (0.0072, 0.0072, 0.0072)], 8),
              "iron", cap_end=False)
    # Muendung: Silberring und dunkle Bohrung.
    part.loft(tube([C(0, 0.007, 0.147), C(0, 0.007, 0.158)], [(0.0092, 0.0092, 0.0092)] * 2, 8), "silver",
              cap_end=False)
    part.loft(tube([C(0, 0.007, 0.158), C(0, 0.007, 0.1585)], [(0.0045, 0.0045, 0.0045)] * 2, 8), "bore")
    return part


def build_fittings():
    part = Part("fittings", (0, 0, 0), UV, parent="stock")
    # Kolbenkappe aus Silber mit kurzem Sporn.
    part.loft(tube([C(0, -0.063, -0.019), C(0, -0.072, -0.022)], [(0.0145, 0.019, 0.017), (0.012, 0.014, 0.013)], 8,
                   ref_up=V(0, 0, 1)), "silver")
    part.spike((C(0, -0.07, -0.03)), (C(0, -0.076, -0.044)), 0.006, "silver")
    # Seitenplatte links (zur Kamera hin) mit spitzen Enden.
    x = -0.0115
    # Fassung 2: kleiner, sie stand in der Ich-Sicht als heller Fluegel im Bild.
    pts = [C(x, -0.003, -0.004), C(x, 0.003, 0.0), C(x, 0.004, 0.024), C(x, -0.001, 0.032), C(x, -0.005, 0.016)]
    part.prism(pts, (0.0015, 0, 0), "silver")
    # Schlossplatte rechts.
    x = 0.0115
    pts = [C(x, -0.004, -0.002), C(x, 0.004, 0.0), C(x, 0.006, 0.026), C(x, -0.002, 0.032), C(x, -0.005, 0.012)]
    part.prism(pts, (-0.0015, 0, 0), "silver")
    # Abzugsbuegel aus Silber.
    guard = [C(0, -0.008, 0.036), C(0, -0.022, 0.028), C(0, -0.027, 0.014), C(0, -0.024, 0.0), C(0, -0.014, -0.006)]
    part.loft(tube(guard, [(0.0028, 0.0022, 0.0022)] * 5, 4, ref_up=V(1, 0, 0)), "silver")
    # Abzug.
    part.prism([C(-0.002, -0.006, 0.016), C(-0.002, -0.02, 0.012), C(-0.002, -0.017, 0.008), C(-0.002, -0.006, 0.01)],
               (0.004, 0, 0), "steel")
    # Ladestock unter dem Schaft, Silberspitze.
    part.loft(tube([C(0, -0.010, 0.04), C(0, -0.010, 0.042), C(0, -0.010, 0.148), C(0, -0.010, 0.15)],
                   [(0.0022, 0.0022, 0.0022)] * 4, 4), "wood")
    part.loft(tube([C(0, -0.010, 0.15), C(0, -0.010, 0.155), C(0, -0.010, 0.156)], [(0.003, 0.003, 0.003)] * 3, 4),
              "silver")
    # Griffplatte (Schwanzschraube) oben auf dem Griff hinter dem Lauf: Zeichnung fuer den Blick von hinten oben.
    part.box((-0.0045, 0.0125, -0.006), (0.0045, 0.0165, 0.022), "silver")
    # Pfanne und Batterie (Feuerstahl) oben rechts vor dem Hahn.
    part.box((-0.0125, 0.008, 0.016), (-0.004, 0.011, 0.03), "steel")
    part.prism([C(0.006, 0.011, 0.024), C(0.006, 0.011, 0.031), C(0.006, 0.025, 0.029), C(0.006, 0.023, 0.023)],
               (-0.0035, 0, 0), "steel")
    return part


HAMMER_PIVOT = C(0.007, 0.010, 0.004)


def build_cock():
    part = Part("cock", HAMMER_PIVOT, UV, parent="stock")
    # Hahn: geschwungener Hals mit Backen, die den Feuerstein halten.
    x = 0.009
    raw = [(0.008, -0.004), (0.016, -0.006), (0.028, 0.0), (0.034, 0.008), (0.03, 0.016),
           (0.024, 0.012), (0.02, 0.004), (0.012, 0.006)]
    k = 0.72                                                                       # kleiner als zuerst
    pts = [C(x, 0.008 + (y - 0.008) * k, z * k) for y, z in raw]
    part.prism(pts, (-0.004, 0, 0), "steel")
    part.box((-0.0095, 0.008 + 0.022 * k, 0.008 * k), (-0.0035, 0.008 + 0.028 * k, 0.02 * k), "iron")   # Stein
    return part


def build_hand():
    part = Part("arm", (0, 0, 0), UV, parent="stock")
    # Zweite Fassung der Hand: Finger einzeln um den Griff, Zeigefinger am Abzug, Daumen links, Stulpe.
    build_glove_hand(part, +1, top=(0, -0.006, 0.006), bottom=(0, -0.052, -0.015), outer=(1, 0, 0),
                     radius=0.0185, finger_ts=(0.014, 0.026, 0.037),
                     wrist=(0.016, -0.046, -0.034), elbow=(0.10, -0.17, -0.26),
                     thumb=((0.004, -0.014, -0.010), (-0.011, -0.004, 0.0), (-0.0125, 0.004, 0.016)),
                     index=((0.017, -0.006, 0.002), (0.012, -0.011, 0.016), (0.003, -0.015, 0.018)))
    return part


def build_flash():
    part = Part("flash", (0, 0, 0), UV, parent="stock")
    m = C(0, 0.007, 0.162)
    for d in (C(0, 0, 0.06), C(0.02, 0.016, 0.03), C(-0.02, 0.016, 0.03), C(0.02, -0.016, 0.03),
              C(-0.02, -0.016, 0.03), C(0, 0.03, 0.02), C(0, -0.03, 0.02)):
        part.spike(tuple(m), tuple(m + d), 0.011, "flash")
    return part


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    tex = paint_atlas(RECTS, PAINTERS, TEX, colors=32, seed=5)
    image = save_image("pistol", tex, os.path.join(OUT_DIR, "pistole_textur_v3.png"))
    coll = bpy.data.collections.new("Pistole")
    bpy.context.scene.collection.children.link(coll)
    mat = export_material("pistol", image)
    model = [build_stock().build(coll, mat, smooth=True), build_barrel().build(coll, mat, smooth=False),
             build_fittings().build(coll, mat, smooth=False), build_cock().build(coll, mat, smooth=False)]
    hand = build_hand().build(coll, mat, smooth=True)
    flash = build_flash().build(coll, mat, smooth=False)
    print("Dreiecke:", sum(len(o.data.polygons) for o in model), {o.name: len(o.data.polygons) for o in model},
          "Muendungsfeuer:", len(flash.data.polygons), "Hand und Aermel:", len(hand.data.polygons))

    pose(model, {})
    bpy.ops.object.select_all(action="DESELECT")
    for o in model:
        o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(OUT_DIR, "pistole_v3.glb"), export_format="GLB",
                              use_selection=True, export_yup=True, export_normals=True,
                              export_vertex_color="NONE", export_materials="EXPORT", export_texcoords=True)

    pmat = pass_material("pistol_pass", image)
    everything = model + [hand, flash]
    for o in everything:
        o.data.materials[0] = pmat
    cam = camera()

    def lit(objs):
        for o in objs:
            write_light(o)
        attr = flash.data.color_attributes.get("Light") or flash.data.color_attributes.new("Light", "FLOAT_COLOR", "CORNER")
        for d in attr.data:
            d.color = (1, 1, 1, 1)                                                 # Feuer leuchtet selbst

    # Freie Ansicht von hinten oben (so sieht der Spieler die Waffe): Zeichnung auf Ruecken und Oberseite pruefen.
    setup_render(640, 640)
    cam.data.angle_y = math.radians(30)
    pose(model + [hand], {})
    lit(model + [hand])
    aim(cam, G(C(0.05, 0.22, -0.28)), G(C(0, -0.01, 0.05)))
    save_png(compose(render_passes("hinten_oben", visible=model + [hand]), "ansicht"),
             os.path.join(OUT_DIR, "ansicht_hinten_oben_v3.png"))

    # Ich-Sicht im Gang: Augen auf 1,6 m, Blick geradeaus.
    stone = stone_textures(os.path.join(os.path.dirname(OUT_DIR), "gemeinsam"))
    room = build_room(stone)
    setup_render(854, 480)
    cam.data.angle_y = math.radians(70)
    aim(cam, G(EYE), G(EYE + V(0, 0, 1)))
    d = V(0, 0, AIM_DISTANCE) - V(*PISTOL_HAND)                                    # Kameraraum
    yaw = math.atan2(-d.x, d.z)                    # Lauf zur Bildmitte: nach links = +x in Blender = positive Drehung
    pitch = math.degrees(math.atan2(d.y, math.hypot(d.x, d.z)))
    orient = (Matrix.Rotation(yaw, 4, "Z") @ raise_fwd(pitch) @ Matrix.Rotation(math.radians(CANT), 4, "Y")
              @ Matrix.Scale(SCALE, 4))
    shots = {}
    for state in ("ruhig", "rueckstoss"):
        if state == "ruhig":
            root = Matrix.Translation(G(EYE + C(*PISTOL_HAND))) @ orient
            angles, show = {}, model + [hand]
        else:
            root = Matrix.Translation(G(EYE + C(*PISTOL_HAND) + C(0, 0.012, -0.035))) @ orient @ raise_fwd(14)
            angles, show = {"cock": -38}, model + [hand, flash]
        pose(everything, angles, root=root)
        lit(everything)
        passes = render_passes(f"ich_{state}", visible=show + room)
        for mode in ("spiel",):                         # entschieden: Nebel mit Koernung
            shots[(state, mode)] = compose(passes, mode)
            save_png(shots[(state, mode)], os.path.join(OUT_DIR, f"ich_sicht_{state}_v3.png"))
    # Hand alt (Fassung 2) gegen neu: Ausschnitt unten rechts, doppelt vergroessert.
    old = load_png(os.path.join(OUT_DIR, "ich_sicht_ruhig_nebel_v2.png"))
    new = shots[("ruhig", "spiel")]
    save_png(side_by_side([zoom(old, (0, 240), (480, 854), 2), zoom(new, (0, 240), (480, 854), 2)]),
             os.path.join(OUT_DIR, "vergleich_hand.png"))


main()
