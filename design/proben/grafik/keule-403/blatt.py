"""Probenblatt Keule (Issue #403): Keule und Klinge aus denselben vier Ansichten im selben Massstab.

Laedt die fertigen Dateien assets/models/club.glb, club_hand.glb, blade.glb, blade_hand.glb (nicht die Bauskripte),
rendert wie die anderen Probestuecke (tools/models/psx.py: ein Abtastwert je Pixel, harte Pixel, Licht je Flaeche wie im
Spiel) und schreibt in diesen Ordner:
    blatt.png                 Keule oben, Klinge unten; Spalten vorn, Seite, schraeg, Ich-Sicht (Ausschnitt, 2-fach)
    keule-ich-sicht-854.png   Spielaufloesung 854 x 480, Nebel und 15-Bit-Koernung, Ruhehaltung mit Aermel
    klinge-ich-sicht-854.png  dasselbe mit der Klinge zum Vergleich
    keule-<ansicht>.png       die drei Einzelansichten der Keule (vorn, seite, schraeg), 4-fach hochskaliert
    textur.png                club.png 4-fach vergroessert

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/keule-403/blatt.py
"""

import math
import os
import sys
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import blf  # noqa: E402
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
import imbuf  # noqa: E402
from psx import (V, G, FOG_COLOR, setup_render, camera, aim, render_passes, compose, save_png, load_png,  # noqa: E402
                 pass_material, write_light, stone_textures, build_room, MATS)

MODELS = os.path.join(ROOT, "assets", "models")
EYE = V(0, 1.6, -6.0)
BLADE_HAND = (0.11, -0.095, 0.19)    # Spielachsen (x seitlich, -x = rechts im Bild), wie tools/models/blade.py (C(-0.11, ...))
TILE = 200
UPSCALE = 2


HOME = {}


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()


def load(name):
    """glb laden; Rueckgabe Objekte nach Namen. Material -> Durchgangs-Material mit derselben Textur, flach beleuchtet."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(MODELS, name))
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in objs:
        image = next(n.image for n in o.active_material.node_tree.nodes if n.type == "TEX_IMAGE")
        o.data.materials.clear()
        o.data.materials.append(pass_material(f"{name}_{o.name}", image))
        o["smooth"] = not o.name.startswith(("club", "sword"))
        HOME[o.name] = o.matrix_world.copy()
    return objs


def place(objs, matrix):
    for o in objs:
        o.matrix_world = matrix @ HOME[o.name]
    bpy.context.view_layer.update()
    for o in objs:
        write_light(o)


def hand_matrix():
    """Ruhehaltung wie tools/models/blade.py (hand_matrix(0, 0)), aber ohne Skalierung (in der Datei eingebacken)."""
    roll = Matrix.Rotation(math.radians(-20), 4, "Y")
    return Matrix.Translation(G(EYE + V(*BLADE_HAND))) @ roll


def render_tile(visible, cam, loc, target, fov_deg, size, mode):
    setup_render(*size)
    cam.data.angle = math.radians(fov_deg)
    aim(cam, loc, target)
    return compose(render_passes("blatt", visible), mode)[::-1]


def up(a, f):
    return np.repeat(np.repeat(a, f, axis=0), f, axis=1)


# Ausschnitt der Ich-Sicht (Zeilen von oben, Spalten) um die Waffe; Hoehe 200 Zeilen = 400 Zeilen nach 2-fach
CROP = (205, 405, 80, 420)


def views():
    """Die drei Einzelansichten: Kamera um den Mittelpunkt der Waffe, 20 Grad Blickwinkel, 1,25 m Abstand."""
    centre = V(0, 0.03, 0.07)
    out = {}
    for name, d in (("vorn", V(0.0, 0.18, 1.0)), ("seite", V(1.0, 0.0, 0.0)), ("schraeg", V(0.8, 0.55, 0.75))):
        out[name] = (G(centre + d.normalized() * 1.25), G(centre))
    return out


def text(img, s, x, y, size=18):
    """Beschriftung (nur fuer das Blatt) mit der Blender-Schrift in das Bild stempeln. y von oben."""
    f = blf.load("C:/Windows/Fonts/arial.ttf")
    blf.size(f, size)
    w, h = blf.dimensions(f, s)
    W, H = int(w) + 8, int(size * 1.6)
    ib = imbuf.new((W, H))
    blf.color(f, 1, 1, 1, 1)
    blf.position(f, 4, int(size * 0.4), 0)
    with blf.bind_imbuf(f, ib):
        blf.draw_buffer(f, s)
    tmp = os.path.join(tempfile.gettempdir(), "keule_txt.png")
    ib.filepath = tmp
    imbuf.write(ib)
    im = bpy.data.images.load(tmp)
    im.colorspace_settings.name = "Non-Color"
    a = np.array(im.pixels[:], np.float32).reshape(H, W, 4)[::-1, :, 3]
    bpy.data.images.remove(im)
    hh, ww = img.shape[:2]
    ye, xe = min(hh, y + H), min(ww, x + W)
    m = (a[:ye - y, :xe - x] > 0.5)[..., None]
    img[y:ye, x:xe] = np.where(m, 235.0, img[y:ye, x:xe])


def main():
    stone_dir = os.path.join(tempfile.gettempdir(), "keule_stein")
    os.makedirs(stone_dir, exist_ok=True)
    clear()
    club = load("club.glb")
    club_hand = load("club_hand.glb")
    blade = load("blade.glb")
    blade_hand = load("blade_hand.glb")
    cam = camera()
    weapons = {"keule": (club, club_hand), "klinge": (blade, blade_hand)}

    # Einzelansichten (Zeile 0 oben)
    tiles = {}
    for who, (w, _h) in weapons.items():
        for name, (loc, target) in views().items():
            place(w, Matrix())
            tile = render_tile(w, cam, loc, target, 20, (TILE, TILE), "ansicht")
            tiles[(who, name)] = up(tile, UPSCALE)
            if who == "keule":
                save_png(up(tile, 4)[::-1], os.path.join(HERE, f"keule-{name}.png"))

    # Ich-Sicht in Spielaufloesung: Raum, Nebel, Koernung
    room = build_room(stone_textures(stone_dir))
    crops = {}
    for who, (w, h) in weapons.items():
        place(w + h, hand_matrix())
        shot = render_tile(room + w + h, cam, G(EYE), G(EYE + V(0, 0, 1)), 70, (854, 480), "spiel")
        save_png(shot[::-1], os.path.join(HERE, f"{who}-ich-sicht-854.png"))
        crops[who] = up(shot[CROP[0]:CROP[1], CROP[2]:CROP[3]], 2)

    # Blatt: zwei Zeilen (Keule, Klinge), Spalten vorn, Seite, schraeg, Ich-Sicht
    gap, label, tw = 8, 26, TILE * UPSCALE
    cw = crops["keule"].shape[1]
    sheet = np.zeros((2 * (tw + label + gap) + label, 3 * (tw + gap) + cw, 3)) + FOG_COLOR
    text(sheet, "Silver & Ash - Keule (Worn), Probe zu Issue 403: oben Keule, unten Klinge, gleicher Massstab", 6, 2, 16)
    for r, who in enumerate(("keule", "klinge")):
        y0 = label + r * (tw + label + gap)
        title = "Keule" if who == "keule" else "Klinge"
        for c, name in enumerate(("vorn", "seite", "schraeg")):
            x0 = c * (tw + gap)
            sheet[y0 + label:y0 + label + tw, x0:x0 + tw] = tiles[(who, name)]
            text(sheet, f"{title}: {name}", x0 + 4, y0, 16)
        x0 = 3 * (tw + gap)
        crop = crops[who]
        sheet[y0 + label:y0 + label + crop.shape[0], x0:x0 + crop.shape[1]] = crop
        text(sheet, f"{title}: Ich-Sicht, Ausschnitt 2-fach", x0 + 4, y0, 16)
    save_png(sheet[::-1], os.path.join(HERE, "blatt.png"))
    save_png(up(load_png(os.path.join(MODELS, "club.png")), 4), os.path.join(HERE, "textur.png"))


main()
