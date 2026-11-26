"""Vergleichsblatt der Stake Crossbow (Issue #203), Runde 1: A, B, C nebeneinander.

Liest die drei glb (armbrust_a.py, armbrust_b.py, armbrust_c.py muessen vorher gelaufen sein) und schreibt:
    blatt.png                    Vergleichsblatt (Seite, schraeg von hinten oben, Ich-Sicht, kleine HUD-Fassung, Silhouetten, Bestand)
    ansichten/<a|b|c>-<ansicht>.png   je Entwurf vorn, Seite, hinten, schraeg oben (je 400 x 300) und Ich-Sicht in Spielaufloesung 854 x 480

Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python blatt.py
"""

import math
import os
import sys
import tempfile

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from armbrust_gemeinsam import (HERE, MODELS, SILVER, bpy, blf, imbuf, Matrix, Vector, V, G)  # noqa: E402
from psx import (FOG_COLOR,setup_render, camera, render_passes, compose, coverage, save_png, pass_material, write_light,  # noqa: E402
                 stone_textures, build_room, MATS, raise_fwd)

DESIGNS = [("a", "A Eisenwinde", "armbrust-a-eisen.glb"),
           ("b", "B Rippenbogen", "armbrust-b-knochen.glb"),
           ("c", "C Schlanke Dohle", "armbrust-c-jagd.glb")]

EYE = V(0, 1.6, -6.0)
HOME = {}
# Haltung in der rechten Hand (Spielachsen: rechts im Bild = -x): Griff etwas tiefer als die Pistole, damit der Bogen
# die Bildmitte frei laesst; Pflock zeigt leicht nach oben und zur Mitte.
HAND = (-0.090, -0.105, 0.170)
PITCH, YAW, ROLL = 4.0, 4.0, 8.0
PISTOL_HAND = (-0.095, -0.08, 0.18)


def load(name, tag, base=None):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(base or HERE, name))
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in objs:
        image = next(n.image for n in o.active_material.node_tree.nodes if n.type == "TEX_IMAGE")
        o.data.materials.clear()
        o.data.materials.append(pass_material(f"{tag}_{o.name}", image))
        o["smooth"] = tag in ("pistol", "pistolh", "bladeh", "clubh") or o.name.startswith("pistol")
        o["tag"] = tag
        HOME[o.name] = o.matrix_world.copy()
    return objs


def place(objs, matrix):
    for o in objs:
        o.matrix_world = matrix @ HOME[o.name]
    bpy.context.view_layer.update()
    for o in objs:
        write_light(o)


def at(hand):
    return Matrix.Translation(G(EYE + V(*hand)))


def held():
    return at(HAND) @ Matrix.Rotation(math.radians(ROLL), 4, "Y") @ Matrix.Rotation(math.radians(YAW), 4, "Z") @ raise_fwd(PITCH)


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


def mask(visible, cam, loc, target, fov, size):
    setup_render(*size)
    cam.data.angle = math.radians(fov)
    aim_up(cam, loc, target, G(V(0, 1, 0)))
    return coverage(render_passes("maske", visible))[::-1]


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
    tmp = os.path.join(tempfile.gettempdir(), "armbrust_txt.png")
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


CENTRE = V(0, 0.03, 0.09)


def main():
    stone_dir = os.path.join(tempfile.gettempdir(), "armbrust_stein")
    os.makedirs(stone_dir, exist_ok=True)
    os.makedirs(os.path.join(HERE, "ansichten"), exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    models = {k: load(f, k) for k, _l, f in DESIGNS}
    pistol = load("pistol.glb", "pistol", MODELS)
    pistol_hand = load("pistol_hand.glb", "pistolh", MODELS)
    club, club_hand = load("club.glb", "club", MODELS), load("club_hand.glb", "clubh", MODELS)
    blade, blade_hand = load("blade.glb", "blade", MODELS), load("blade_hand.glb", "bladeh", MODELS)
    cam = camera()
    room = build_room(stone_textures(stone_dir))
    W, H = 640, 400
    views = {  # Name -> (Richtung vom Mittelpunkt zur Kamera, Hoch)
        "seite": (V(-1, 0.08, 0), G(V(0, 1, 0))),
        "vorn": (V(0, 0.15, 1), G(V(0, 1, 0))),
        "hinten": (V(0, 0.2, -1), G(V(0, 1, 0))),
        "schraeg": (V(-0.55, 0.70, -0.75), G(V(0, 1, 0))),
    }
    tiles, firsts, masks, icons = {}, {}, {}, {}
    for k, label, _f in DESIGNS:
        place(models[k], Matrix())
        for name, (d, up) in views.items():
            img = shot(models[k], cam, G(CENTRE + d.normalized() * 0.74), G(CENTRE), 33, (W if name in ("seite", "schraeg") else 400, 400 if name in ("seite", "schraeg") else 300), "ansicht", up)
            tiles[(k, name)] = img
            save_png(img[::-1], os.path.join(HERE, "ansichten", f"{k}-{name}.png"))
        # kleine HUD-Fassung: 38 x 38 Pixel, Seitenansicht leicht von oben, harte Pixel
        icons[k] = shot(models[k], cam, G(CENTRE + V(-0.9, 0.35, 0.0).normalized() * 0.70), G(CENTRE + V(0, 0, 0.02)), 33, (38, 38), "ansicht")
    # Ich-Sicht
    pose_sleeve = at(HAND) @ Matrix.Rotation(math.radians(ROLL), 4, "Y")
    for k, label, _f in DESIGNS:
        place(models[k], held())
        place(pistol_hand, at(HAND))
        keep = list(room) + models[k] + pistol_hand
        full = shot(keep, cam, G(EYE), G(EYE + V(0, 0, 1)), 70, (854, 480), "spiel")
        firsts[k] = full
        save_png(full[::-1], os.path.join(HERE, "ansichten", f"{k}-ich-sicht-854x480.png"))
        masks[k] = mask(list(models[k]), cam, G(EYE), G(EYE + V(0, 0, 1)), 70, (854, 480))
        place(models[k], Matrix.Translation(G(V(0, -20, 0))))
    # Bestand zum Vergleich
    ref = {}
    place(pistol, at(PISTOL_HAND))
    place(pistol_hand, at(PISTOL_HAND))
    ref["pistol"] = shot(list(room) + pistol + pistol_hand, cam, G(EYE), G(EYE + V(0, 0, 1)), 70, (854, 480), "spiel")
    masks["pistol"] = mask(list(pistol), cam, G(EYE), G(EYE + V(0, 0, 1)), 70, (854, 480))
    save_png(ref["pistol"][::-1], os.path.join(HERE, "ansichten", "bestand-pistole-854x480.png"))
    place(pistol, Matrix.Translation(G(V(0, -20, 0))))
    place(pistol_hand, Matrix.Translation(G(V(0, -20, 0))))
    place(club, at((0.11, -0.095, 0.19)) @ Matrix.Rotation(math.radians(-20), 4, "Y") @ Matrix.Rotation(math.radians(10), 4, "Z") @ raise_fwd(35))
    place(club_hand, at((0.11, -0.095, 0.19)) @ Matrix.Rotation(math.radians(-20), 4, "Y"))
    ref["club"] = shot(list(room) + club + club_hand, cam, G(EYE), G(EYE + V(0, 0, 1)), 70, (854, 480), "spiel")
    masks["club"] = mask(list(club), cam, G(EYE), G(EYE + V(0, 0, 1)), 70, (854, 480))
    place(club, Matrix.Translation(G(V(0, -20, 0))))
    place(club_hand, Matrix.Translation(G(V(0, -20, 0))))
    place(blade, at((0.11, -0.095, 0.19)) @ Matrix.Rotation(math.radians(-20), 4, "Y"))
    place(blade_hand, at((0.11, -0.095, 0.19)) @ Matrix.Rotation(math.radians(-20), 4, "Y"))
    ref["blade"] = shot(list(room) + blade + blade_hand, cam, G(EYE), G(EYE + V(0, 0, 1)), 70, (854, 480), "spiel")
    masks["blade"] = mask(list(blade), cam, G(EYE), G(EYE + V(0, 0, 1)), 70, (854, 480))

    # --- Blatt ---
    gap, lab = 10, 26
    cw = W + gap
    thumb_h = 160
    rows_h = [400, 400, 360, 190, 360]
    sheet_h = lab + sum(h + lab + gap for h in rows_h) + 4
    sheet = np.zeros((sheet_h, 3 * cw, 3)) + FOG_COLOR
    text(sheet, "Silver & Ash - Stake Crossbow (Issue 203), Runde 1: drei Entwuerfe A, B, C (jeder mit eingelegtem Holzpflock)", 6, 2, 18)
    ys = []
    y = lab
    for h in rows_h:
        ys.append(y)
        y += h + lab + gap
    titles = ["Seite (Schussrichtung nach rechts)", "schraeg von hinten oben", "Ich-Sicht, Haltung in der rechten Hand (halbe Spielgroesse, Ausschnitt unten)",
              "kleine HUD-Fassung (38 x 38, vierfach) und Silhouette in der Ich-Sicht (Drittel-Groesse)", "Bestand zum Vergleich (Pistole, Klinge, Keule)"]
    for r, t in enumerate(titles):
        pass
    for c, (k, label, _f) in enumerate(DESIGNS):
        x0 = c * cw
        tri = {"a": 428, "b": 444, "c": 400}[k]
        text(sheet, f"{label} ({tri} Dreiecke): Seite, Schussrichtung nach rechts", x0 + 4, ys[0], 16)
        paste(sheet, tiles[(k, "seite")], x0, ys[0] + lab)
        text(sheet, f"{label}: schraeg von hinten oben", x0 + 4, ys[1], 16)
        paste(sheet, tiles[(k, "schraeg")], x0, ys[1] + lab)
        text(sheet, f"{label}: Ich-Sicht (Spielbild halbe Groesse, unterer Teil)", x0 + 4, ys[2], 16)
                # Mittelstreifen: Zeilen 60..240 der halben Groesse, ganze Breite 427 -> vergroessert 1,5 auf 640
        strip = scale_to(firsts[k], 360, 640)
        paste(sheet, strip, x0, ys[2] + lab)
        text(sheet, f"{label}: HUD 38x38 und Silhouette (Drittel)", x0 + 4, ys[3], 16)
        icon = np.zeros((46, 46, 3)) + FOG_COLOR
        icon[4:42, 4:42] = icons[k]
        icon[2:4, 2:44] = SILVER[1]
        icon[44:46, 2:44] = SILVER[1]
        icon[2:46, 2:4] = SILVER[1]
        icon[2:46, 44:46] = SILVER[1]
        icon = icon[:, :]
        paste(sheet, up_n(icon, 3), x0, ys[3] + lab)
        # Silhouette: Maske der Waffe (weiss) mit der Pistole (grau) darunter, Drittel-Groesse
        for i, (mk, shade) in enumerate((((k, 235.0), ("pistol", 120.0)))):
            m = masks[mk]
            h2, w2 = m.shape[0] // 3, m.shape[1] // 3
            small = m[:h2 * 3, :w2 * 3].reshape(h2, 3, w2, 3).mean(axis=(1, 3))
            if i == 0:
                tile = np.zeros((h2, w2, 3)) + FOG_COLOR
            tile = np.where((small > 0.3)[..., None], shade, tile)
        paste(sheet, tile, x0 + 170, ys[3] + lab)
    # Bestand
    for c, (key, name) in enumerate((("pistol", "Pistole"), ("blade", "Klinge"), ("club", "Keule"))):
        x0 = c * cw
        text(sheet, f"Bestand {name}: Ich-Sicht", x0 + 4, ys[4], 16)
        paste(sheet, scale_to(ref[key], 360, 640), x0, ys[4] + lab)
    save_png(sheet[::-1], os.path.join(HERE, "blatt.png"))


if __name__ == "__main__":
    main()
