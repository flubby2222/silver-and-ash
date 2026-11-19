"""Probenblatt Keule (Issue #414): die geschlossene, gerade Keule von allen sechs Seiten und eine Nahansicht des Uebergangs Griff-Kopf.

Laedt die fertigen Dateien assets/models/club.glb und club_hand.glb, rendert wie die anderen Probestuecke
(tools/models/psx.py) und schreibt blatt.png in diesen Ordner: oben rechts, links, oben; Mitte vorn (Kopfende), hinten
(Knauf), unten; rechts die Nahansicht des Uebergangs.

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/keule-414/blatt.py
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
    """Kamera um den Mittelpunkt der Keule (Dateimass), 20 Grad Blickwinkel, 1,25 m Abstand; Nahansicht des Uebergangs."""
    centre = V(0, 0.0, 0.07)
    out = {}
    for name, d in (("rechts (+x)", V(1, 0, 0)), ("links (-x)", V(-1, 0, 0)), ("oben (+y)", V(0.15, 1, 0)),
                    ("unten (-y)", V(0.15, -1, 0)), ("vorn (Kopfende)", V(0, 0, 1)), ("hinten (Knauf)", V(0, 0, -1))):
        out[name] = (G(centre + d.normalized() * 1.25), G(centre), 20)
    junction = V(0, 0, 0.075)
    out["Nahansicht Griff-Kopf"] = (G(junction + V(1, 0.25, 0.35).normalized() * 0.45), G(junction), 20)
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
    clear()
    club = load("club.glb")
    club_hand = load("club_hand.glb")
    cam = camera()
    tiles = {}
    for name, (loc, target, fov) in views().items():
        place(club, Matrix())
        tiles[name] = up(render_tile(club, cam, loc, target, fov, (TILE, TILE), "ansicht"), UPSCALE)
    gap, label, tw = 8, 26, TILE * UPSCALE
    names = list(views())
    big = tiles[names[6]]
    sheet = np.zeros((2 * (tw + label + gap) + label, 3 * (tw + gap) + big.shape[1], 3)) + FOG_COLOR
    text(sheet, "Silver & Ash - Keule (Worn), Probe zu Issue 414: sechs Seiten und Nahansicht des Uebergangs Griff-Kopf", 6, 2, 16)
    for k, name in enumerate(names[:6]):
        r, c = divmod(k, 3)
        x0, y0 = c * (tw + gap), label + r * (tw + label + gap)
        sheet[y0 + label:y0 + label + tw, x0:x0 + tw] = tiles[name]
        text(sheet, name, x0 + 4, y0, 16)
    x0, y0 = 3 * (tw + gap), label
    sheet[y0 + label:y0 + label + big.shape[0], x0:x0 + big.shape[1]] = big[:sheet.shape[0] - y0 - label]
    text(sheet, names[6], x0 + 4, y0, 16)
    save_png(sheet[::-1], os.path.join(HERE, "blatt.png"))


main()
