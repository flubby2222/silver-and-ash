"""Bestandsaufnahme vor Runde 2 der Throwing Knives (Issue #204): alle Modelle des Spiels auf Uebersichtsblaettern.

Liest jede glb aus assets/models (nur gelesen) und rendert sie mit EINER Kamera-Richtung und EINEM Licht (psx: Ansicht ohne
Nebel). Schreibt nach runde-2/bibliothek/:
    bibliothek-waffen.png          je Waffe: Seite, oben, schraeg, Ich-Sicht 854 x 480 mit Aermel (Ruhehaltung nachgestellt), Textur
    bibliothek-kisten-tueren.png   Kisten, Tueren, Rahmen, Fallgatter, Teleporter
    bibliothek-gegner.png          Gegner und ihre Effekte
    bibliothek-requisiten.png      alles andere
    bestand-zahlen.txt             gemessene Zahlen der Waffen (Farben, Helligkeit, Rot-Anteil, Texeldichte), dazu Entwurf a und b

Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python bibliothek.py
"""

import math
import os
import sys
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE)))))
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import blf  # noqa: E402
import bpy  # noqa: E402
import imbuf  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from psx import (V, G, FOG_COLOR, setup_render, camera, render_passes, compose, save_png, pass_material, write_light,  # noqa: E402
                 stone_textures, build_room, MATS, raise_fwd)

MODELS = os.path.join(ROOT, "assets", "models")
OUT = os.path.join(HERE, "bibliothek")
EYE = V(0, 1.6, -6.0)
HOME = {}
AWAY = Matrix.Translation(Vector((0, 0, -60)))
VIEW_DIR = V(-0.60, 0.42, -0.68)              # eine Richtung fuer alle: schraeg von vorn-rechts oben
FOV = 30.0


def load(path, tag):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in [o for o in objs if o.name.startswith("flash")]:          # Muendungsfeuer der Pistole: im Spiel nur beim Schuss
        objs.remove(o)
        bpy.data.objects.remove(o)
    bpy.context.view_layer.update()
    for o in objs:
        image = next(n.image for n in o.active_material.node_tree.nodes if n.type == "TEX_IMAGE")
        o["texture"] = image.name
        o.data.materials.clear()
        o.data.materials.append(pass_material(f"{tag}_{o.name}", image))
        o["smooth"] = bool(any(p.use_smooth for p in o.data.polygons))
        HOME[o.name] = o.matrix_world.copy()
        write_light(o)
    return objs


def place(objs, matrix):
    for o in objs:
        o.matrix_world = matrix @ HOME[o.name]
    bpy.context.view_layer.update()
    for o in objs:
        write_light(o)


def bounds(objs):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def aim_up(cam, loc, target, up):
    fwd = (target - loc).normalized()
    zc = -fwd
    xc = up.cross(zc).normalized()
    yc = zc.cross(xc)
    m = Matrix(((xc.x, yc.x, zc.x), (xc.y, yc.y, zc.y), (xc.z, yc.z, zc.z)))
    cam.location = loc
    cam.rotation_euler = m.to_euler()


def passes(visible, cam, loc, target, fov, size, up=None):
    setup_render(*size)
    cam.data.angle = math.radians(fov)
    aim_up(cam, loc, target, up if up is not None else G(V(0, 1, 0)))
    return render_passes("bib", visible)


def fitted(objs, cam, direction, size, up=None, radius=None):
    """Kamera so weit weg, dass die Kugel um den Rahmen des Modells ins Bild passt (Blender-Richtung direction)."""
    lo, hi = bounds(objs)
    centre = (lo + hi) / 2
    r = radius or max((hi - lo).length / 2, 0.02)
    dist = r / math.sin(math.radians(FOV / 2)) * 1.04
    return compose(passes(objs, cam, centre + direction.normalized() * dist, centre, FOV, size, up), "ansicht")[::-1]


def text(img, s, x, y, size=13):
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
    tmp = os.path.join(tempfile.gettempdir(), "bib_txt.png")
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


def texture_pixels(name):
    im = bpy.data.images[name]
    w, h = im.size
    return np.array(im.pixels[:], np.float32).reshape(h, w, 4)[::-1, :, :3] * 255


def texel_density(objs):
    """Texel je Meter (Dateimass), ueber die Flaeche gemittelt."""
    tex_area, world_area = 0.0, 0.0
    for o in objs:
        im = bpy.data.images[o["texture"]]
        uv = o.data.uv_layers.active.data
        s = o.matrix_world.to_scale()
        for p in o.data.polygons:
            pts = [uv[i].uv for i in p.loop_indices]
            a = 0.0
            for i in range(1, len(pts) - 1):
                a += abs((pts[i].x - pts[0].x) * (pts[i + 1].y - pts[0].y) - (pts[i + 1].x - pts[0].x) * (pts[i].y - pts[0].y)) / 2
            tex_area += a * im.size[0] * im.size[1]
            world_area += p.area * (abs(s.x * s.y * s.z) ** (2 / 3))
    return math.sqrt(tex_area / world_area) if world_area > 0 else 0.0


def colour_stats(p):
    """Zahlen aus dem unbeleuchteten Farbdurchgang einer Ansicht."""
    base = p["base"]
    m = base[..., 3] > 0.5
    c = base[..., :3][m] * 255
    if len(c) == 0:
        return "leer"
    lum = c @ np.array((0.299, 0.587, 0.114))
    red = (c[:, 0] >= 140) & (c[:, 1] < 70) & (c[:, 2] < 70)        # Blutrot ab Stufe 3 (150, 20, 30)
    warm = (c[:, 0] > c[:, 2] * 1.25) & ~red
    cool = c[:, 2] > c[:, 0] * 1.1
    q = np.percentile(lum, (10, 50, 90))
    mean = c.mean(axis=0)
    return (f"Mittel RGB {mean[0]:.0f},{mean[1]:.0f},{mean[2]:.0f}; Helligkeit 10/50/90 % = {q[0]:.0f}/{q[1]:.0f}/{q[2]:.0f}; "
            f"hell (>170) {100 * (lum > 170).mean():.0f} %, dunkel (<50) {100 * (lum < 50).mean():.0f} %; "
            f"blutrot (ab Stufe 3) {100 * red.mean():.1f} %, warm {100 * warm.mean():.0f} %, kuehl {100 * cool.mean():.0f} %")


def top_colours(pix, n=6):
    c, counts = np.unique(pix.reshape(-1, 3).round().astype(int), axis=0, return_counts=True)
    order = np.argsort(-counts)[:n]
    return len(c), ", ".join(f"{tuple(int(v) for v in c[i])} {100 * counts[i] / counts.sum():.0f}%" for i in order)


GROUPS = [
    ("kisten-tueren", "Kisten, Tueren, Rahmen, Fallgatter, Teleporter", ("chest_", "door_", "portcullis", "teleporter_")),
    ("gegner", "Gegner und ihre Effekte", ("bat", "ghoul", "gorm", "crossbow_skeleton_", "crypt_warden_", "shock_wave")),
]
WEAPONS = ["blade", "pistol", "club", "hunters_axe", "whip_rest", "whip_strike", "stake_crossbow_loaded", "stake_crossbow_relaxed"]
HANDS = ["blade_hand", "pistol_hand", "club_hand"]
# Ruhehaltung in der Ich-Sicht, nachgestellt (Spielachsen, rechts im Bild = -x): Waffe, Aermel, Hand, Drehungen
LEFT, RIGHT, XBOW = (0.11, -0.095, 0.19), (-0.095, -0.08, 0.18), (-0.090, -0.105, 0.170)


def rot(deg, axis):
    return Matrix.Rotation(math.radians(deg), 4, axis)


def at(p):
    return Matrix.Translation(G(EYE + V(*p)))


def bend():
    """Keule, Axt und Peitsche knicken am Griff: 35 Grad hoch, 10 Grad auswaerts (WeaponPose.ClubBend)."""
    pivot = Matrix.Translation(Vector((0, 0.06 * 1.8, 0)))
    return pivot @ rot(10, "Z") @ raise_fwd(35) @ pivot.inverted()


FIRST = {
    "blade": ("blade_hand", at(LEFT) @ rot(-20, "Y"), at(LEFT) @ rot(-20, "Y")),
    "pistol": ("pistol_hand", at(RIGHT) @ rot(15, "Y"), at(RIGHT) @ rot(15, "Y")),
    "club": ("club_hand", at(LEFT) @ rot(-20, "Y") @ bend(), at(LEFT) @ rot(-20, "Y")),
    "hunters_axe": ("club_hand", at(LEFT) @ rot(-20, "Y") @ bend(), at(LEFT) @ rot(-20, "Y")),
    "whip_rest": ("club_hand", at(LEFT) @ rot(-20, "Y") @ bend(), at(LEFT) @ rot(-20, "Y")),
    "whip_strike": ("club_hand", at(LEFT) @ rot(-20, "Y") @ bend(), at(LEFT) @ rot(-20, "Y")),
    "stake_crossbow_loaded": ("pistol_hand", at(XBOW) @ rot(8, "Y") @ rot(4, "Z") @ raise_fwd(4), at(XBOW)),
    "stake_crossbow_relaxed": ("pistol_hand", at(XBOW) @ rot(8, "Y") @ rot(4, "Z") @ raise_fwd(4), at(XBOW)),
}


def tris(objs):
    return sum(len(p.vertices) - 2 for o in objs for p in o.data.polygons)


def grid_sheet(title, entries, cam, path, cols=6, tile=236):
    lab = 34
    rows = (len(entries) + cols - 1) // cols
    sheet = np.zeros((30 + rows * (tile + lab + 6), cols * (tile + 6), 3)) + FOG_COLOR
    text(sheet, title, 6, 4, 16)
    for i, (name, objs) in enumerate(entries):
        x, y = (i % cols) * (tile + 6), 30 + (i // cols) * (tile + lab + 6)
        lo, hi = bounds(objs)
        d = hi - lo
        paste(sheet, fitted(objs, cam, G(VIEW_DIR), (tile, tile)), x, y + lab)
        text(sheet, name, x + 2, y, 13)
        text(sheet, f"{tris(objs)} Dreiecke, {d.x:.2f} x {d.z:.2f} x {d.y:.2f} m", x + 2, y + 16, 11)
    save_png(sheet[::-1], path)


def main():
    os.makedirs(OUT, exist_ok=True)
    stone_dir = os.path.join(tempfile.gettempdir(), "bib_stein")
    os.makedirs(stone_dir, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    names = sorted(f[:-4] for f in os.listdir(MODELS) if f.endswith(".glb"))
    models = {n: load(os.path.join(MODELS, n + ".glb"), n) for n in names}
    probe = os.path.dirname(HERE)
    extra = {"entwurf a (verworfen)": load(os.path.join(probe, "messer-a-aderlass.glb"), "a"),
             "entwurf b (verworfen)": load(os.path.join(probe, "messer-b-kreuzblatt.glb"), "b")}
    for key, fname in (("entwurf c (Runde 2)", "messer-c-silberblatt.glb"), ("entwurf d (Runde 2)", "messer-d-jagdeisen.glb"),
                       ("entwurf e (Runde 2)", "messer-e-schwarzruecken.glb")):
        if os.path.exists(os.path.join(HERE, fname)):
            extra[key] = load(os.path.join(HERE, fname), key[8])
    cam = camera()
    room = build_room(stone_textures(stone_dir))
    lines = []

    # --- Gruppenblaetter ---
    used = set(WEAPONS) | set(HANDS)
    for key, title, prefixes in GROUPS:
        chosen = [n for n in names if n not in used and n.startswith(prefixes)]
        used |= set(chosen)
        grid_sheet(f"Silver & Ash - Modell-Bibliothek: {title} (eine Kamera-Richtung, ein Licht, je Modell eingepasst)",
                   [(n, models[n]) for n in chosen], cam, os.path.join(OUT, f"bibliothek-{key}.png"))
    rest = [n for n in names if n not in used]
    grid_sheet("Silver & Ash - Modell-Bibliothek: Requisiten und Bausteine (eine Kamera-Richtung, ein Licht, je Modell eingepasst)",
               [(n, models[n]) for n in rest], cam, os.path.join(OUT, "bibliothek-requisiten.png"))

    # --- Waffen ---
    tw, th, lab = 300, 200, 34
    cols = [("Seite (vorn = rechts)", 300), ("von oben (vorn = rechts)", 300), ("schraeg", 200), ("Ich-Sicht 854 x 480 (halbe Groesse)", 427), ("Textur (vierfach)", 200)]
    sheet = np.zeros((30 + len(WEAPONS) * (240 + lab + 6), sum(w + 6 for _t, w in cols), 3)) + FOG_COLOR
    text(sheet, "Silver & Ash - Modell-Bibliothek: Waffen im Spiel (eine Kamera, ein Licht; Ich-Sicht mit Aermel in Ruhehaltung, nachgestellt)", 6, 4, 16)
    hidden = [o for n in WEAPONS + HANDS for o in models[n]]
    for r, n in enumerate(WEAPONS):
        objs = models[n]
        y = 30 + r * (240 + lab + 6)
        lo, hi = bounds(objs)
        d = hi - lo
        radius = (hi - lo).length / 2
        tex = texture_pixels(objs[0]["texture"])
        count, top = top_colours(tex)
        density = texel_density(objs)
        text(sheet, f"{n}: {tris(objs)} Dreiecke, {d.x:.2f} x {d.z:.2f} x {d.y:.2f} m (Breite x Hoehe x Laenge, Dateimass), "
                    f"Textur {tex.shape[1]} x {tex.shape[0]}, {count} Farben, {density:.0f} Texel je m", 4, y, 13)
        x = 0
        centre = (lo + hi) / 2
        dist = radius / math.sin(math.radians(FOV / 2)) * 1.04
        side = passes(objs, cam, centre + G(V(-1, 0, 0)) * dist, centre, FOV, (300, 240))
        paste(sheet, compose(side, "ansicht")[::-1], x, y + lab)
        x += 306
        top_p = passes(objs, cam, centre + G(V(0, 1, 0)) * dist, centre, FOV, (300, 240), G(V(1, 0, 0)))
        paste(sheet, compose(top_p, "ansicht")[::-1], x, y + lab)
        x += 306
        paste(sheet, fitted(objs, cam, G(VIEW_DIR), (200, 240)), x, y + lab)
        x += 206
        hand, weapon_m, hand_m = FIRST[n]
        place(hidden, AWAY)
        place(objs, weapon_m)
        place(models[hand], hand_m)
        setup_render(854, 480)
        cam.data.angle = math.radians(70)
        aim_up(cam, G(EYE), G(EYE + V(0, 0, 1)), G(V(0, 1, 0)))
        full = compose(render_passes("bib", list(room) + objs + models[hand]), "spiel")[::-1]
        save_png(full[::-1], os.path.join(OUT, f"ich-sicht-{n}.png"))
        paste(sheet, scale_to(full, 240, 427), x, y + lab)
        place(objs, Matrix())
        place(models[hand], Matrix())
        x += 433
        f = 200 // max(tex.shape[0], tex.shape[1]) or 1
        big = np.repeat(np.repeat(tex, f, axis=0), f, axis=1) if tex.shape[0] <= 200 else scale_to(tex, 200, 200)
        paste(sheet, big[:240, :200], x, y + lab)
        lines.append(f"{n}: {tris(objs)} Dreiecke; Masse {d.x:.3f} x {d.z:.3f} x {d.y:.3f} m; Textur {tex.shape[1]}x{tex.shape[0]}, {count} Farben, "
                     f"{density:.0f} Texel/m")
        lines.append(f"    Textur, haeufigste Farben: {top}")
        lines.append(f"    Seitenansicht: {colour_stats(side)}")
        lines.append(f"    von oben:      {colour_stats(top_p)}")
    save_png(sheet[::-1], os.path.join(OUT, "bibliothek-waffen.png"))
    for n, objs in extra.items():
        lo, hi = bounds(objs)
        d = hi - lo
        centre = (lo + hi) / 2
        dist = (hi - lo).length / 2 / math.sin(math.radians(FOV / 2)) * 1.04
        tex = texture_pixels(objs[0]["texture"])
        count, top = top_colours(tex)
        side = passes(objs, cam, centre + G(V(-1, 0, 0)) * dist, centre, FOV, (300, 240))
        lines.append(f"{n}: {tris(objs)} Dreiecke; Masse {d.x:.3f} x {d.z:.3f} x {d.y:.3f} m; Textur {tex.shape[1]}x{tex.shape[0]}, {count} Farben, "
                     f"{texel_density(objs):.0f} Texel/m")
        lines.append(f"    Textur, haeufigste Farben: {top}")
        lines.append(f"    Seitenansicht: {colour_stats(side)}")
    with open(os.path.join(OUT, "bestand-zahlen.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("Gemessen von bibliothek.py aus dem unbeleuchteten Farbdurchgang (Seite = Blick entlang x, oben = Blick entlang y).\n")
        f.write("\n".join(lines) + "\n")
    print("\n".join(lines))
    print("BIBLIOTHEK geschrieben")


if __name__ == "__main__":
    main()
