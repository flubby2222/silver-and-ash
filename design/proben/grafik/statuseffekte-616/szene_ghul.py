"""Szene fuer das Probenblatt Statuseffekte (Issue #616): Gang mit Steinwaenden, Ghule aus assets/models/ghoul.glb.

Wird von statuseffekte.py benutzt (laeuft in Blender). Spielbedingungen wie in der Art Bible: 854 x 480, 70 Grad
vertikal, Augen auf 1,6 m, Laterne und Nebel nach den Formeln des Shaders. Die Koernung (15 Bit, Bayer 4 x 4) setzt
statuseffekte.py am Ende, nach den Effekten. Das Skript aendert nichts am Spiel; es liest nur das Modell.
"""

import math
import os
import sys
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import bpy  # noqa: E402
from bpy_extras.object_utils import world_to_camera_view  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from psx import (G, V, FOG_COLOR, FOG_START, FOG_END, stone_textures, build_room, pass_material, write_light,  # noqa: E402
                 setup_render, camera, aim, render_passes, compose, raise_fwd, MATS)

GHOUL_GLB = os.path.join(ROOT, "assets", "models", "ghoul.glb")
W, H, FOV = 854, 480, 70.0
EYE = 1.6
JAW_RESTING = -12.0   # GhoulPose.JawResting: im Ruhezustand haengt der Kiefer ein Stueck offen

_state = {}


def start(room_half=4.0, back=-4.0):
    """Leere Szene mit Gang, Kamera und Steintexturen."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    tmp = os.path.join(tempfile.gettempdir(), "statuseffekte_616")
    os.makedirs(tmp, exist_ok=True)
    stone = stone_textures(tmp)
    room = build_room(stone, half=room_half, back=back)
    setup_render(W, H)
    cam = camera()
    cam.data.angle = math.radians(FOV)
    _state.update(room=room, cam=cam, ghouls=[], tmp=tmp)
    return room


def add_ghoul(x, z=0.0):
    """Ein Ghul in Ruhehaltung (Kiefer halb offen) bei (x, z) in Spiel-Achsen, Blick nach +z (zur Kamera)."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=GHOUL_GLB)
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    tag = f"ghul{len(_state['ghouls'])}"
    for o in objs:
        image = next(n.image for n in o.active_material.node_tree.nodes if n.type == "TEX_IMAGE")
        o.data.materials.clear()
        o.data.materials.append(pass_material(f"{tag}_{o.name}", image))
        o["smooth"] = True
        home = o.matrix_world.copy()
        shift = Matrix.Translation(G(V(x, 0, z)))
        if o.name == "jaw":
            o.matrix_world = shift @ home @ raise_fwd(JAW_RESTING)
        else:
            o.matrix_world = shift @ home
    bpy.context.view_layer.update()
    for o in objs:
        write_light(o)
    _state["ghouls"].append(objs)
    return objs


def bounds(objs):
    """Achsengerechte Huelle des Ghuls in Spiel-Achsen (x, y oben, z vorn): (min, max)."""
    pts = []
    for o in objs:
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            pts.append((w.x, w.z, -w.y))
    a = np.array(pts)
    return a.min(axis=0), a.max(axis=0)


def project(point):
    """Spiel-Achsen -> Pixel (x, y von oben) und Abstand zur Kamera in Metern."""
    sc, cam = bpy.context.scene, _state["cam"]
    co = world_to_camera_view(sc, cam, G(point))
    dist = (cam.location - G(point)).length
    return co.x * W, (1.0 - co.y) * H, dist


def fog_amount(d):
    t = min(max((d - FOG_START) / (FOG_END - FOG_START), 0.0), 1.0)
    return t * t * (3 - 2 * t)


def render(cam_z, cam_x=0.0, tag="f"):
    """Rendert Raum und jeden Ghul einzeln. Liefert das Bild (Zeilen von oben, gefaerbt mit Laterne und Nebel, noch
    ohne Koernung) und je Ghul die Maske."""
    cam = _state["cam"]
    cam.data.angle = math.radians(FOV)
    aim(cam, G(V(cam_x, EYE, cam_z)), G(V(cam_x, EYE, cam_z - 10)))
    scene = bpy.context.scene
    room, ghouls = _state["room"], _state["ghouls"]
    for g in ghouls:
        for o in g:
            o.hide_render = True
    p_room = render_passes(f"{tag}_raum", visible=set(room))
    img = compose(p_room, "nebel")[::-1].copy()
    masks, dists = [], []
    for i, g in enumerate(ghouls):
        p = render_passes(f"{tag}_g{i}", visible=set(g))
        col = compose(p, "nebel")[::-1]
        m = (p["base"][..., 3] > 0.5)[::-1]
        img = np.where(m[..., None], col, img)
        masks.append(m)
    return img, masks
