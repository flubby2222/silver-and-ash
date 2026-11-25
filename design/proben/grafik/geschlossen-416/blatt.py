"""Vorher/Nachher-Blaetter zu Issue #416 (Teil Gorm und Pistole): dieselben Ansichten, dasselbe Licht, dieselbe Aufloesung.

Links der Stand vor dem Schliessen (Commit 8fc1773, per `git show` geholt), rechts der Stand in assets/models/.
Je Modell eine Datei: blatt-gorm.png (vier Ansichten) und blatt-pistole.png (drei Ansichten). Unter jeder Zeile steht,
wie viele Pixel sich unterscheiden (Summe der Kanalunterschiede ueber 8, als Anzahl Pixel mit Unterschied).

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/geschlossen-416/blatt.py
"""

import math
import os
import subprocess
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

MODELS = os.path.join(ROOT, "assets", "models")
BEFORE = "8fc1773"          # main vor diesem PR
TILE = (220, 165)
UPSCALE = 2

# (Titel, Spielachsen: Auge, Ziel, Blickwinkel in Grad)
GORM_VIEWS = [
    ("Halsring von unten vorn (Spielsicht von unten)", V(0.5, 0.9, 2.2), V(0, 2.45, 0.3), 45),
    ("Rechte Handschelle von innen vorn", V(0.1, 0.5, 1.3), V(1.0, 0.75, 0.52), 38),
    ("Linke Handschelle mit Kette von aussen unten", V(-2.1, 0.3, 1.3), V(-1.0, 0.8, 0.52), 38),
    ("Eisenbaender der Keule von aussen oben", V(2.0, 1.0, 1.9), V(1.05, 0.3, 1.1), 40),
]
PISTOL_VIEWS = [
    ("Muendung von vorn, leicht von oben", V(0.07, 0.13, 0.62), V(0, 0.013, 0.285), 20),
    ("Muendung von der Seite", V(0.55, 0.05, 0.40), V(0, 0.013, 0.26), 22),
    ("Muendung von schraeg vorn unten", V(-0.12, -0.06, 0.55), V(0, 0.013, 0.285), 20),
]
HOME = {}


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    HOME.clear()


def load(path, tag):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in objs:
        image = next(n.image for n in o.active_material.node_tree.nodes if n.type == "TEX_IMAGE")
        o.data.materials.clear()
        o.data.materials.append(pass_material(f"{tag}_{o.name}", image))
        # wie im Erzeuger: weich schattiert ausser Keule, Lauf, Beschlaege, Hahn
        o["smooth"] = o.name not in ("club", "barrel", "fittings", "cock", "flash")
        HOME[o.name] = o.matrix_world.copy()
    return objs


def old_file(name):
    data = subprocess.run(["git", "show", f"{BEFORE}:assets/models/{name}"], cwd=ROOT, capture_output=True, check=True).stdout
    path = os.path.join(tempfile.gettempdir(), f"vorher-{BEFORE}-{name}")
    with open(path, "wb") as f:
        f.write(data)
    return path


def render_view(objs, cam, eye, target, fov):
    for o in objs:
        o.matrix_world = HOME[o.name]
    bpy.context.view_layer.update()
    for o in objs:
        write_light(o)
    setup_render(*TILE)
    cam.data.angle = math.radians(fov)
    aim(cam, G(eye), G(target))
    return compose(render_passes("blatt", objs), "ansicht")[::-1]


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
    tmp = os.path.join(tempfile.gettempdir(), "blatt416_txt.png")
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


def sheet(model_file, views, out_name, title):
    tiles = []
    for tag, path in (("vorher", old_file(model_file)), ("nachher", os.path.join(MODELS, model_file))):
        clear()
        objs = load(path, tag)
        if model_file == "pistol.glb":
            for o in objs:
                if o.name == "flash":
                    o.hide_render = True
            objs = [o for o in objs if o.name != "flash"]
        cam = camera()
        tiles.append([render_view(objs, cam, eye, target, fov) for _, eye, target, fov in views])
    tw, th = TILE[0] * UPSCALE, TILE[1] * UPSCALE
    gap, label = 8, 26
    rows = len(views)
    sheet_img = np.zeros((label + rows * (th + label + gap), 2 * tw + gap, 3)) + FOG_COLOR
    text(sheet_img, title, 6, 2)
    diffs = []
    for r, (name, _, _, _) in enumerate(views):
        a, b = tiles[0][r], tiles[1][r]
        n = int((np.abs(a - b).sum(axis=2) > 8).sum())
        diffs.append(n)
        y0 = label + r * (th + label + gap)
        text(sheet_img, f"{name}  |  links vorher, rechts nachher  |  {n} von {a.shape[0] * a.shape[1]} Pixeln anders", 6, y0)
        sheet_img[y0 + label:y0 + label + th, 0:tw] = up(a, UPSCALE)
        sheet_img[y0 + label:y0 + label + th, tw + gap:] = up(b, UPSCALE)
    save_png(sheet_img[::-1], os.path.join(HERE, out_name))
    print("BLATT", out_name, diffs)


sheet("gorm.glb", GORM_VIEWS, "blatt-gorm.png", "Silver & Ash - Gorm, Issue 416: geschlossene Teile (Halsring, Handschellen, Keulenbaender)")
sheet("pistol.glb", PISTOL_VIEWS, "blatt-pistole.png", "Silver & Ash - Pistole, Issue 416: geschlossener Lauf und Muendungsring")
