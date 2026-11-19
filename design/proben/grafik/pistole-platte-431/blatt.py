"""Vergleichsblatt Pistole (Issue #431): vorher (assets/models) und nachher (dieser Ordner), je hinten, Seite, Ich-Sicht.

Ohne neues Modell entsteht nur die Zeile "vorher". Ausgabe: vergleich.png in diesem Ordner.
Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/pistole-431/blatt.py
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
import imbuf  # noqa: E402
from mathutils import Matrix  # noqa: E402
from psx import (V, G, FOG_COLOR, setup_render, camera, aim, render_passes, compose, save_png,  # noqa: E402
                 pass_material, write_light, MATS)

EYE = V(0, 1.6, -6.0)
HAND = (-0.095, -0.08, 0.18)       # Spielachsen: rechts im Bild = -x
CANT = float(os.environ.get("CANT", "15"))
TILE = 300
HOME = {}
OLD = os.path.join(HERE, "vorher")
NEW = os.path.join(HERE, "nachher")


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    HOME.clear()


def load(folder, name, tag):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(folder, name))
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in objs:
        image = next(n.image for n in o.active_material.node_tree.nodes if n.type == "TEX_IMAGE")
        o.data.materials.clear()
        o.data.materials.append(pass_material(f"{tag}_{name}_{o.name}", image))
        o["smooth"] = o.name.startswith("stock")
        HOME[o.name + tag] = o.matrix_world.copy()
        o["tag"] = tag
    return objs


def place(objs, matrix, tag):
    for o in objs:
        o.matrix_world = matrix @ HOME[o.name + tag]
    bpy.context.view_layer.update()
    for o in objs:
        write_light(o)


def render(visible, cam, loc, target, fov, size):
    setup_render(*size)
    cam.data.angle = math.radians(fov)
    aim(cam, loc, target)
    return compose(render_passes("p431", visible), "ansicht")[::-1]


def up(a, f):
    return np.repeat(np.repeat(a, f, axis=0), f, axis=1)


def text(img, s, x, y, size=16):
    f = blf.load("C:/Windows/Fonts/arial.ttf")
    blf.size(f, size)
    w, h = blf.dimensions(f, s)
    W, H = int(w) + 8, int(size * 1.6)
    ib = imbuf.new((W, H))
    blf.color(f, 1, 1, 1, 1)
    blf.position(f, 4, int(size * 0.4), 0)
    with blf.bind_imbuf(f, ib):
        blf.draw_buffer(f, s)
    tmp = os.path.join(tempfile.gettempdir(), "pistole_txt.png")
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


def hand_matrix():
    roll = Matrix.Rotation(math.radians(CANT), 4, "Y")
    return Matrix.Translation(G(EYE + V(*HAND))) @ roll


def shots(folder, tag, cam):
    """Bilder je Ansicht fuer ein Modell. Hand/Aermel im Bild bei der Ich-Sicht."""
    model = load(folder, "pistol.glb", tag)
    hand = load(folder, "pistol_hand.glb", tag)
    for o in [o for o in model if o.name.startswith("flash")]:
        bpy.data.objects.remove(o)
    model = [o for o in model if o.name != "flash"] if False else [o for o in bpy.data.objects if o.type == "MESH" and o.get("tag") == tag and o.name.split(".")[0] in ("stock", "barrel", "fittings", "cock")]
    c = V(0, 0.0, 0.03)                       # Mitte der Pistole (Dateimass, Spielachsen)
    out = {}
    for name, d in (("hinten", V(0.0, 0.25, -1)), ("Seite", V(-1, 0.1, 0)), ("oben-hinten", V(0.3, 1, -0.6))):
        place(model, Matrix(), tag)
        out[name] = render(model, cam, G(c + d.normalized() * 0.9), G(c), 20, (TILE, TILE))
    place(model + hand, hand_matrix(), tag)
    out["Ich-Sicht"] = render(model + hand, cam, G(EYE), G(EYE + V(0, 0, 1)), 70, (480, 270))
    out["Ich-Sicht"] = out["Ich-Sicht"]
    for o in model + hand:
        o.hide_render = True
    return out


def main():
    clear()
    cam = camera()
    rows = {"vorher": shots(OLD, "alt", cam)}
    rows["nachher"] = shots(NEW, "neu", cam)
    keys = ["hinten", "Seite", "Ich-Sicht"]
    up_t = 2
    gap, label = 6, 24
    tw = TILE * up_t
    wide = 480 * 2
    width = 2 * (tw + gap) + wide
    rowh = tw + label + gap
    sheet = np.zeros((label + rowh * len(rows), width, 3)) + FOG_COLOR
    text(sheet, "Pistole Issue 431: Griffplatte nach vorn", 6, 2)
    for r, (rname, tiles) in enumerate(rows.items()):
        y0 = label + r * rowh
        x = 0
        for k in keys:
            img = up(tiles[k], 2)
            h, w = img.shape[:2]
            sheet[y0 + label:y0 + label + h, x:x + w] = img
            text(sheet, f"{rname}: {k}", x + 4, y0, 16)
            x += w + gap
    save_png(sheet[::-1], os.path.join(HERE, "vergleich.png"))


main()
