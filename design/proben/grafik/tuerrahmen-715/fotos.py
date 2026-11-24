"""Fotos des Tuerrahmens (Issue #715): Tuer mit Steinrahmen, geschlossen, aus drei Blickwinkeln.

Liest die Modelle aus einem Ordner (Umgebungsvariable T715_MODELS, sonst assets/models) und schreibt PNG nach T715_OUT.
Blickwinkel: oben (von oben in die Oeffnung), schraeg (von vorn), fuss (nah am Fuss des Pfostens).
T715_KINDS waehlt die Tuerarten (normal, locked, boss; Standard: alle drei).
Aufruf (aus dem Wurzelverzeichnis):
    T715_OUT=<ordner> "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/tuerrahmen-715/fotos.py
Nur Bilder; nichts wird ins Spiel uebernommen.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import bpy  # noqa: E402
import numpy as np  # noqa: E402
from psx import (V, G, Part, setup_render, camera, aim, render_passes, save_png, pass_material, write_light,  # noqa: E402
                 MATS)

MODELS = os.environ.get("T715_MODELS", os.path.join(ROOT, "assets", "models"))
OUT = os.environ.get("T715_OUT", os.path.join(HERE, "out"))
KINDS = os.environ.get("T715_KINDS", "normal,locked,boss").split(",")
BG = np.array((30, 26, 34), float)
SIZE = (854, 480)


def load(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.type != "MESH":
            continue
        img = None
        for m in o.data.materials:
            for n in m.node_tree.nodes:
                if n.type == "TEX_IMAGE":
                    img = n.image
        o.data.materials.clear()
        o.data.materials.append(pass_material("m_" + o.name, img))
    return new


def scene(kind):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    objs = []
    objs += load(os.path.join(MODELS, "door_%s.glb" % kind))
    objs += load(os.path.join(MODELS, "door_frame_%s_a.glb" % kind))
    # Wand links und rechts (z -1..1) und Boden aus der Wand-Kachel des Rahmen-Atlas
    img = bpy.data.images.load(os.path.join(MODELS, "door_frame_%s_a.png" % kind))
    coll = bpy.context.scene.collection
    rects = {"all": (0.0, 0.0, 0.5, 1.0)}
    hw = 1.0 if kind != "boss" else 2.0
    h = 3.5 if kind != "boss" else 6.0
    for sx in (1, -1):
        p = Part("wand", (0, 0, 0), rects)
        lo, hi = sorted((sx * hw, sx * (hw + 3)))
        p.box((lo, 0, -1), (hi, h, 1), "all")
        objs.append(p.build(coll, pass_material("w%d" % sx, img), smooth=False))
    p = Part("boden", (0, 0, 0), rects)
    p.box((-5, -0.1, -6), (5, 0, 6), "all")
    objs.append(p.build(coll, pass_material("boden", img), smooth=False))
    for o in objs:
        if o.type == "MESH":
            o["smooth"] = False
    bpy.context.view_layer.update()
    for o in objs:
        if o.type == "MESH":
            write_light(o)


def shot(loc, tgt, fov):
    cam = camera()
    setup_render(*SIZE)
    cam.data.angle = math.radians(fov)
    aim(cam, G(V(*loc)), G(V(*tgt)))
    p = render_passes("t715")
    base, light = p["base"][..., :3] * 255, p["light"][..., :1]
    cov = p["base"][..., 3:4] > 0.5
    img = np.where(cov, np.clip(base * light * 1.3, 0, 255), BG)
    bpy.data.objects.remove(cam)
    return np.round(img[::-1])


VIEWS = {
    "oben": ((0.0, 6.0, 2.2), (0.0, 1.0, 0.0), 60),
    "schraeg": ((3.2, 1.7, 4.5), (0.0, 1.5, 0.0), 55),
    "fuss": ((1.1, 0.7, 2.0), (0.0, 0.5, 0.0), 65),
}


def main():
    os.makedirs(OUT, exist_ok=True)
    for kind in KINDS:
        scene(kind)
        for name, (loc, tgt, fov) in VIEWS.items():
            if kind == "boss":
                loc = tuple(c * 1.8 for c in loc)
                tgt = tuple(c * 1.8 for c in tgt)
            save_png(shot(loc, tgt, fov)[::-1], os.path.join(OUT, "door-%s-%s.png" % (kind, name)))


main()
