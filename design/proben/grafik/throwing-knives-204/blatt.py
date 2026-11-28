"""Vergleichsblatt der Throwing Knives (Issue #204): Entwurf a und b nebeneinander.

Liest die beiden glb (messer_a.py und messer_b.py muessen vorher gelaufen sein) und schreibt:
    blatt.png                       Vergleichsblatt a | b
    ansichten/<a|b>-<ansicht>.png   je Entwurf: seite, vorn, hinten, schraeg (einzelnes Messer), ich-sicht-854x480,
                                    steckt-854x480 (Sicht des Werfers), steckt-nah (von der Seite), flug-854x480,
                                    klein-854x480 (Messer quer in 2, 4 und 8 m)

Beide Entwuerfe laufen durch dieselbe Schleife: Aermel, Licht, Kamera, Gang und Ziel sind gleich, nur die Waffe wechselt.
Ziel ist der vorhandene Ghul (assets/models/ghoul.glb, nur gelesen), Aermel und Klinge kommen ebenfalls aus assets/models.

Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python blatt.py
"""

import math
import os
import sys
import tempfile

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from messer_gemeinsam import (HERE, MODELS, SCALE, bpy, blf, imbuf, Matrix, Vector, V, G)  # noqa: E402
from psx import (FOG_COLOR, setup_render, camera, render_passes, compose, save_png, pass_material, write_light,  # noqa: E402
                 stone_textures, build_room, MATS, raise_fwd)

DESIGNS = [("a", "a Aderlass", "messer-a-aderlass.glb", 168),
           ("b", "b Kreuzblatt", "messer-b-kreuzblatt.glb", 172)]

EYE = V(0, 1.6, -6.0)
FOV = 70.0
GAME = (854, 480)
FOCAL = GAME[1] / 2 / math.tan(math.radians(FOV / 2))          # Brennweite in Pixeln (Bildhoehe bestimmt den Winkel)
HOME = {}
# Rechte Hand (Spielachsen: rechts im Bild = -x). Der Aermel sitzt wie bei der Pistole, die Messer stehen aus der Manschette.
HAND = (-0.100, -0.095, 0.205)
LEFT = (0.11, -0.095, 0.19)                                    # Klinge in der linken Hand, wie im Spiel
# Haltung je Entwurf: (Neigung der Spitze nach vorn-oben in Grad, Kippen im Bild, Faecherwinkel der einzelnen Messer)
POSES = {"a": (44.0, 6.0, (-17.0, 0.0, 17.0)),
         "b": (44.0, 6.0, (0.0,))}
GRIP = 0.050 * SCALE                                           # so tief sitzt der Griff in der Manschette (Dateimass)
FAN_PIVOT = 0.085 * SCALE                                      # Faecher dreht um einen Punkt nahe am Knauf
AWAY = Matrix.Translation(Vector((0, 0, -40)))


def load(name, tag, base=None, smooth=False):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(base or HERE, name))
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in objs:
        image = next(n.image for n in o.active_material.node_tree.nodes if n.type == "TEX_IMAGE")
        o.data.materials.clear()
        o.data.materials.append(pass_material(f"{tag}_{o.name}", image))
        o["smooth"] = smooth
        HOME[o.name] = o.matrix_world.copy()
    return objs


def copies(objs, n):
    """n Exemplare desselben Messers (das erste ist das geladene)."""
    out = [list(objs)]
    for i in range(1, n):
        group = []
        for o in objs:
            c = o.copy()
            c.data = o.data
            bpy.context.scene.collection.objects.link(c)
            HOME[c.name] = HOME[o.name].copy()
            group.append(c)
        out.append(group)
    return out


def place(objs, matrix):
    for o in objs:
        o.matrix_world = matrix @ HOME[o.name]
    bpy.context.view_layer.update()
    for o in objs:
        write_light(o)


def at(p):
    return Matrix.Translation(G(EYE + V(*p)))


def world(p):
    return Matrix.Translation(G(V(*p)))


def rot(deg, axis):
    return Matrix.Rotation(math.radians(deg), 4, axis)


def held(key, angle):
    """Messer in der Hand: breite Seite zur Kamera, Spitze nach vorn-oben, Griff in der Manschette.
    Blender-Achsen des Modells: Spitze -Y, Klingenbreite Z, Dicke X."""
    pitch, roll, _fan = POSES[key]
    fan = Matrix.Translation(Vector((0, FAN_PIVOT, 0))) @ rot(angle, "Z") @ Matrix.Translation(Vector((0, -FAN_PIVOT, 0)))
    return (at(HAND) @ rot(roll, "Y") @ raise_fwd(pitch) @ Matrix.Translation(Vector((0, -GRIP, 0)))
            @ fan @ rot(90, "Y"))


def thrown(p, pitch=0.0, yaw=0.0, spin=0.0):
    """Messer in der Welt in Spielmass (1/1,8): Spitze nach vorn (+z), dann um die Querachse gedreht (spin),
    geneigt (pitch hebt die Spitze) und seitlich gedreht (yaw)."""
    return world(p) @ rot(yaw, "Z") @ raise_fwd(pitch + spin) @ Matrix.Scale(1 / SCALE, 4)


def aim_up(cam, loc, target, up):
    fwd = (target - loc).normalized()
    zc = -fwd
    xc = up.cross(zc).normalized()
    yc = zc.cross(xc)
    m = Matrix(((xc.x, yc.x, zc.x), (xc.y, yc.y, zc.y), (xc.z, yc.z, zc.z)))
    cam.location = loc
    cam.rotation_euler = m.to_euler()


def shot(visible, cam, loc, target, fov, size, mode, up=None):
    setup_render(*size)
    cam.data.angle = math.radians(fov)
    aim_up(cam, loc, target, up if up is not None else G(V(0, 1, 0)))
    return compose(render_passes("blatt", visible), mode)[::-1]


def player(visible, cam):
    return shot(visible, cam, G(EYE), G(EYE + V(0, 0, 1)), FOV, GAME, "spiel")


def pixel(p):
    """Spielpunkt -> Pixel im Spielbild der Kamera in EYE (+x liegt links im Bild)."""
    d = V(*p) - EYE
    return int(GAME[0] / 2 - d.x / d.z * FOCAL), int(GAME[1] / 2 - d.y / d.z * FOCAL)


def crop(img, centre, w, h):
    x = int(np.clip(centre[0] - w // 2, 0, img.shape[1] - w))
    y = int(np.clip(centre[1] - h // 2, 0, img.shape[0] - h))
    return img[y:y + h, x:x + w]


def text(img, s, x, y, size=16):
    """Beschriftung mit der Blender-Schrift; y von oben. Die Groesse wird geprueft (ein vertauschter Aufruf frass einmal 20 GB)."""
    assert 8 <= size <= 40 and isinstance(s, str) and 0 <= x < 20000 and 0 <= y < 20000, (s, x, y, size)
    f = blf.load("C:/Windows/Fonts/arial.ttf")
    blf.size(f, size)
    w, h = blf.dimensions(f, s)
    W, H = int(w) + 8, int(size * 1.6)
    ib = imbuf.new((W, H))
    blf.color(f, 1, 1, 1, 1)
    blf.position(f, 4, int(size * 0.4), 0)
    with blf.bind_imbuf(f, ib):
        blf.draw_buffer(f, s)
    tmp = os.path.join(tempfile.gettempdir(), "messer_txt.png")
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


def paste(sheet, img, x, y):
    h, w = img.shape[:2]
    sheet[y:y + h, x:x + w] = img[:sheet.shape[0] - y, :sheet.shape[1] - x][:h, :w]


def scale_to(a, h, w):
    return a[np.linspace(0, a.shape[0] - 1, h).astype(int)][:, np.linspace(0, a.shape[1] - 1, w).astype(int)]


def up_n(a, f):
    return np.repeat(np.repeat(a, f, axis=0), f, axis=1)


def hit_point(ghoul, x, height):
    """Erster Treffer eines Strahls von vorn auf den Ghul in der Hoehe height (Spielachsen)."""
    best = None
    for o in ghoul:
        inv = o.matrix_world.inverted()
        origin, direction = inv @ G(V(x, height, EYE.z)), (inv.to_3x3() @ G(V(0, 0, 1))).normalized()
        ok, loc, _n, _i = o.ray_cast(origin, direction)
        if ok:
            p = o.matrix_world @ loc
            if best is None or p.y > best.y:                     # Blender y = -vorn: der groesste Wert liegt am naechsten
                best = p
    assert best is not None, "Strahl trifft den Ghul nicht"
    return V(best.x, best.z, -best.y)


CENTRE = V(0, 0, 0.033 * SCALE)
TARGET_Z = EYE.z + 3.5                                          # Ziel 3,5 m vor dem Auge
STUCK_PITCH, STUCK_YAW, STUCK_DEPTH = -24.0, 20.0, 0.055        # Griff haengt nach oben-aussen, 5,5 cm der Klinge im Ziel


def main():
    stone_dir = os.path.join(tempfile.gettempdir(), "messer_stein")
    os.makedirs(stone_dir, exist_ok=True)
    out = os.path.join(HERE, "ansichten")
    os.makedirs(out, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    knives = {k: copies(load(f, k), 3) for k, _l, f, _t in DESIGNS}
    sleeve = load("pistol_hand.glb", "pistolh", MODELS, smooth=True)
    blade = load("blade.glb", "blade", MODELS, smooth=True) + load("blade_hand.glb", "bladeh", MODELS, smooth=True)
    ghoul = load("ghoul.glb", "ghoul", MODELS, smooth=True)
    cam = camera()
    room = build_room(stone_textures(stone_dir))
    everything = [o for group in knives.values() for g in group for o in g] + sleeve + blade + ghoul
    place(everything, AWAY)

    def park(objs):
        place(objs, AWAY)

    views = {  # Name -> (Richtung vom Mittelpunkt zur Kamera, Abstand, Bildgroesse)
        "seite": (V(-1, 0.0, 0), 0.47, (640, 250)),
        "vorn": (V(0.25, 0.25, 1), 0.56, (212, 212)),
        "hinten": (V(0.25, 0.25, -1), 0.56, (212, 212)),
        "schraeg": (V(-0.55, 0.70, -0.75), 0.80, (212, 212)),
    }
    tiles = {}
    for k, _label, _f, _t in DESIGNS:
        one = knives[k][0]
        # --- einzelnes Messer, vier Ansichten ---
        place(one, Matrix())
        for name, (d, dist, size) in views.items():
            img = shot(one, cam, G(CENTRE + d.normalized() * dist), G(CENTRE), 33, size, "ansicht")
            tiles[(k, name)] = img
            save_png(img[::-1], os.path.join(out, f"{k}-{name}.png"))
        park(one)

        # --- Ich-Sicht: Aermel und Klinge wie im Spiel, nur die Messer wechseln ---
        fan = POSES[k][2]
        for group, angle in zip(knives[k], fan):
            place(group, held(k, angle))
        place(sleeve, at(HAND))
        place(blade, at(LEFT) @ rot(-20, "Y"))
        shown = [o for g in knives[k][:len(fan)] for o in g]
        img = player(list(room) + shown + sleeve + blade, cam)
        tiles[(k, "ich")] = img
        save_png(img[::-1], os.path.join(out, f"{k}-ich-sicht-854x480.png"))
        park(shown + sleeve + blade)

        # --- steckt im Ziel: Ghul 3,5 m vor dem Auge, Messer in der Brust ---
        place(ghoul, world((0, 0, TARGET_Z)))
        top = max((o.matrix_world @ Vector(c)).z for o in ghoul for c in o.bound_box)
        hit = hit_point(ghoul, 0.07, top * 0.70)
        m = thrown(hit, STUCK_PITCH, STUCK_YAW)
        tip = (m @ Vector((0, -0.17 * SCALE, 0)) - m @ Vector((0, 0, 0)))            # Klingenansatz -> Spitze (Blender)
        m = Matrix.Translation(tip.normalized() * STUCK_DEPTH - tip) @ m
        place(one, m)
        img = player(list(room) + ghoul + one, cam)
        save_png(img[::-1], os.path.join(out, f"{k}-steckt-854x480.png"))
        tiles[(k, "steckt")] = up_n(crop(img, pixel(hit + V(0, 0.02, 0)), 106, 120), 3)
        near = shot(list(room) + ghoul + one, cam, G(hit + V(-0.62, 0.16, -0.72)), G(hit + V(0, 0.0, -0.05)), 40, (318, 360), "spiel")
        tiles[(k, "nah")] = near
        save_png(near[::-1], os.path.join(out, f"{k}-steckt-nah.png"))
        park(ghoul + one)

        # --- Flug: drei Augenblicke eines Wurfs (1,5 m, 3 m, 5 m), das Messer dreht sich um die Querachse ---
        marks = []
        for group, (dist, spin, dx, dy) in zip(knives[k], ((1.5, 35.0, -0.10, -0.16), (3.0, 150.0, -0.05, -0.12), (5.0, 275.0, 0.0, -0.10))):
            p = EYE + V(dx, dy, dist)
            place(group, thrown(p, spin=spin))
            marks.append(p)
        flying = [o for g in knives[k] for o in g]
        img = player(list(room) + flying, cam)
        save_png(img[::-1], os.path.join(out, f"{k}-flug-854x480.png"))
        tiles[(k, "flug")] = up_n(crop(img, (440, 264), 134, 120), 3)

        # --- klein: dasselbe Messer quer zur Blickrichtung in 2 m, 4 m und 8 m (Lesbarkeit des Umrisses) ---
        for group, (dist, dx) in zip(knives[k], ((2.0, 0.30), (4.0, 0.0), (8.0, -0.55))):
            place(group, thrown(EYE + V(dx, -0.12, dist), pitch=-35.0, yaw=90.0))
        img = player(list(room) + flying, cam)
        save_png(img[::-1], os.path.join(out, f"{k}-klein-854x480.png"))
        tiles[(k, "klein")] = up_n(crop(img, (GAME[0] // 2 - 16, GAME[1] // 2 + 22), 118, 180), 2)
        park(flying)

    # --- Blatt ---
    W, gap, lab = 640, 12, 26
    cw = W + gap
    rows_h = [250, 212, 360, 360, 360]
    sheet = np.zeros((34 + sum(h + lab + gap for h in rows_h) + 4, 2 * cw - gap, 3)) + FOG_COLOR
    text(sheet, "Silver & Ash - Throwing Knives (Issue 204), Stufe Vampiric: Entwurf a und b (Aermel, Licht, Kamera, Gang, Ziel gleich)", 6, 4, 18)
    ys, y = [], 34
    for h in rows_h:
        ys.append(y)
        y += h + lab + gap
    for c, (k, label, _f, tri) in enumerate(DESIGNS):
        x0 = c * cw
        text(sheet, f"{label} ({tri} Dreiecke je Messer): einzelnes Messer von der Seite", x0 + 4, ys[0], 16)
        paste(sheet, tiles[(k, "seite")], x0, ys[0] + lab)
        text(sheet, f"{label}: von vorn, von hinten, schraeg von oben", x0 + 4, ys[1], 16)
        for i, name in enumerate(("vorn", "hinten", "schraeg")):
            paste(sheet, tiles[(k, name)], x0 + i * 214, ys[1] + lab)
        text(sheet, f"{label}: Ich-Sicht 854 x 480 (hier auf 640 x 360), links die Klinge", x0 + 4, ys[2], 16)
        paste(sheet, scale_to(tiles[(k, "ich")], 360, 640), x0, ys[2] + lab)
        text(sheet, f"{label}: steckt im Ziel - Sicht des Werfers aus 3,5 m (dreifach) / von der Seite", x0 + 4, ys[3], 16)
        paste(sheet, tiles[(k, "steckt")], x0, ys[3] + lab)
        paste(sheet, tiles[(k, "nah")], x0 + 322, ys[3] + lab)
        text(sheet, f"{label}: im Flug aus Sicht des Werfers (dreifach) / quer in 2, 4, 8 m (zweifach)", x0 + 4, ys[4], 16)
        paste(sheet, tiles[(k, "flug")], x0, ys[4] + lab)
        paste(sheet, tiles[(k, "klein")], x0 + 404, ys[4] + lab)
    save_png(sheet[::-1], os.path.join(HERE, "blatt.png"))
    print("BLATT geschrieben:", sheet.shape[1], "x", sheet.shape[0])


if __name__ == "__main__":
    main()
