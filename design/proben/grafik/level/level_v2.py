"""Level-Texturen der Katakomben - Probestueck 8, Fassung 2 (leise Fugen) fuer Silver & Ash (Issue #233).

Fassung 2 nach dem Urteil der Leitung: Fugen nur wenig dunkler als der Stein, unregelmaessig, ausgebrochen oder
zugesetzt; Steine staerker versetzt; Decke dunkler und ruhiger. Bilder alt gegen neu, mit Ghul und Kiste aus der
Uebergabe (design/uebergabe/assets/models). Fassung 1: level.py.


Wand, Boden und Decke, je 64 x 64 Pixel (1 m je Wiederholung), hoechstens 32 Farben (hier 24), ohne Filterung,
nahtlos kachelbar: Fugen, Rauschen, Risse und Flecken laufen ueber den Rand und kommen gegenueber wieder herein.
Die mittlere Helligkeit jeder Textur entspricht der heutigen Flaechenfarbe des Spiels (LookSettings).

    Wand  - Katakombenmauer: unregelmaessige Bruchsteinlagen, tiefe Fugen, Feuchtigkeitsspuren
    Boden - abgetretene Steinplatten verschiedener Groesse, Schmutz in den Fugen
    Decke - kleines Gewoelbe-Mauerwerk, Russ

Bilder: die drei Texturen 3 x 3 gekachelt (Naht-Probe), ein langer Gang (20 m Wand im Bild), ein Kampfraum mit
Saeule. Alles mit Nebel und 15-Bit-Koernung (Entscheidung vom 2026-11-16).

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/level/level_v2.py
"""

import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "gemeinsam"))
import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from psx import (WORLD_WALL, WORLD_FLOOR, WORLD_CEILING, quantize, save_image, pass_material, write_light,  # noqa: E402
                 setup_render, camera, aim, render_passes, compose, save_png, side_by_side, G, V)

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
SIZE = 64
PAINT = 4
COLORS = 24


# --- Kachelbares Rauschen -----------------------------------------------------------------------------------------

def tnoise(n, cells, seed):
    """Wertrauschen n x n, periodisch: das Gitter wiederholt sich nach cells Zellen in beide Richtungen."""
    g = np.random.default_rng(seed).random((cells, cells))
    t = np.arange(n) / n * cells
    i0 = np.floor(t).astype(int) % cells
    i1 = (i0 + 1) % cells
    f = t - np.floor(t)
    f = f * f * (3 - 2 * f)
    fy, fx = f[:, None], f[None, :]
    a, b = g[i0][:, i0], g[i0][:, i1]
    c, d = g[i1][:, i0], g[i1][:, i1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def tnoise_xy(n, cy, cx, seed):
    """Periodisch mit verschiedenen Zellzahlen je Richtung (z. B. lange senkrechte Laufspuren)."""
    g = np.random.default_rng(seed).random((cy, cx))
    ty, tx = np.arange(n) / n * cy, np.arange(n) / n * cx
    y0, x0 = np.floor(ty).astype(int) % cy, np.floor(tx).astype(int) % cx
    y1, x1 = (y0 + 1) % cy, (x0 + 1) % cx
    fy, fx = (ty - np.floor(ty))[:, None], (tx - np.floor(tx))[None, :]
    a, b = g[y0][:, x0], g[y0][:, x1]
    c, d = g[y1][:, x0], g[y1][:, x1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def courses(n, heights, width_range, seed):
    """Mauerlagen: je Zeile Steine verschiedener Breite, die Lagen versetzt, alles periodisch ueber n Pixel.
    Rueckgabe: Stein-Nummer, Abstand zur naechsten Fuge waagerecht und senkrecht (in gemalten Pixeln)."""
    rng = np.random.default_rng(seed)
    p = n // SIZE
    ids = np.zeros((n, n), int)
    dx = np.zeros((n, n))
    dy = np.zeros((n, n))
    y = 0
    stone = 0
    for row, h in enumerate(heights):
        rows = slice(y * p, (y + h) * p)
        # Fugen in dieser Lage: zufaellige Breiten, die genau SIZE ergeben, um einen Versatz verschoben.
        cuts, x = [], 0
        while x < SIZE:
            w = int(rng.integers(*width_range))
            if SIZE - (x + w) < width_range[0]:
                w = SIZE - x
            cuts.append(x)
            x += w
        shift = int(rng.integers(0, SIZE))
        cuts = sorted((c + shift) % SIZE for c in cuts)
        xs = np.arange(n) / p
        idx = np.searchsorted(cuts, xs, side="right") - 1          # -1 = vor der ersten Fuge: gehoert zum letzten
        idx = idx % len(cuts)
        left = np.array([cuts[i] for i in idx])
        right = np.array([cuts[(i + 1) % len(cuts)] for i in idx])
        dist_l = (xs - left) % SIZE
        dist_r = (right - xs) % SIZE
        ids[rows] = (stone + idx)[None, :]
        dx[rows] = np.minimum(dist_l, dist_r)[None, :]
        ly = (np.arange((y + h) * p - y * p) / p)
        dy[rows] = np.minimum(ly, h - ly)[:, None]
        # Oberkante (fuer Licht von oben): Abstand nach oben
        stone += len(cuts)
        y += h
    return ids, dx, dy


def finish(img, base, seed):
    small = img.reshape(SIZE, PAINT, SIZE, PAINT, 3).mean(axis=(1, 3))
    small *= np.asarray(base, float).mean() / small.mean()       # mittlere Helligkeit = heutige Flaechenfarbe
    return np.clip(quantize(np.clip(small, 0, 255), COLORS, seed), 0, 255)


def cracks(img, rng, count, n, strength=0.55):
    p = n // SIZE
    for _ in range(count):
        x, y = rng.uniform(0, n), rng.uniform(0, n)
        ang = rng.uniform(0, 2 * math.pi)
        for _ in range(int(rng.integers(20, 60))):
            ang += rng.normal(0, 0.35)
            x, y = (x + math.cos(ang) * p * 0.6) % n, (y + math.sin(ang) * p * 0.6) % n
            img[int(y) % n:int(y) % n + 2, int(x) % n:int(x) % n + 2] *= strength


# --- Raeume -------------------------------------------------------------------------------------------------------

def quad_grid(coll, name, tex_mat, corner, du, dv, length_u, length_v, step=0.25, flip_to=None):
    """Flaeche aus kleinen Vierecken (fuer Laterne und Nebel je Ecke wie im Spiel), UV in Metern (1 m = 1 Kachel)."""
    nu, nv = int(round(length_u / step)), int(round(length_v / step))
    bm = bmesh.new()
    uv = bm.loops.layers.uv.verify()
    verts = [[bm.verts.new(corner + du * (i * step) + dv * (j * step)) for j in range(nv + 1)] for i in range(nu + 1)]
    for i in range(nu):
        for j in range(nv):
            f = bm.faces.new((verts[i][j], verts[i + 1][j], verts[i + 1][j + 1], verts[i][j + 1]))
            for loop, (a, b) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
                loop[uv].uv = (a * step, b * step)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    obj["smooth"] = False
    coll.objects.link(obj)
    if flip_to is not None:
        poly = mesh.polygons[0]
        if poly.normal.dot(flip_to - poly.center) < 0:
            mesh.flip_normals()
    mesh.materials.append(tex_mat)
    return obj


def box_room(mats, x0, x1, z0, z1, height, inside=True):
    """Raum (Blender-Koordinaten x, y; z oben). inside: Flaechen zeigen nach innen."""
    coll = bpy.data.collections.new("Raum")
    bpy.context.scene.collection.children.link(coll)
    X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
    c = Vector(((x0 + x1) / 2, (z0 + z1) / 2, height / 2))
    w, d = x1 - x0, z1 - z0
    objs = [
        quad_grid(coll, "boden", mats["floor"], Vector((x0, z0, 0)), X, Y, w, d, flip_to=c),
        quad_grid(coll, "decke", mats["ceiling"], Vector((x0, z0, height)), X, Y, w, d, flip_to=c),
        quad_grid(coll, "wand_a", mats["wall"], Vector((x0, z0, 0)), Y, Z, d, height, flip_to=c),
        quad_grid(coll, "wand_b", mats["wall"], Vector((x1, z0, 0)), Y, Z, d, height, flip_to=c),
        quad_grid(coll, "wand_c", mats["wall"], Vector((x0, z0, 0)), X, Z, w, height, flip_to=c),
        quad_grid(coll, "wand_d", mats["wall"], Vector((x0, z1, 0)), X, Z, w, height, flip_to=c),
    ]
    return objs


def pillar(mats, cx, cy, half, height):
    """Viereckige Saeule; Flaechen zeigen nach aussen."""
    coll = bpy.data.collections.new("Saeule")
    bpy.context.scene.collection.children.link(coll)
    X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
    objs = []
    for name, corner, du in (("s1", Vector((cx - half, cy - half, 0)), X), ("s2", Vector((cx + half, cy - half, 0)), Y),
                             ("s3", Vector((cx + half, cy + half, 0)), -X), ("s4", Vector((cx - half, cy + half, 0)), -Y)):
        o = quad_grid(coll, name, mats["wall"], corner, du, Z, 2 * half, height)
        poly = o.data.polygons[0]
        if poly.normal.dot(poly.center - Vector((cx, cy, poly.center.z))) < 0:
            o.data.flip_normals()
        objs.append(o)
    return objs


def tiled_preview(arr, path):
    big = np.tile(arr, (3, 3, 1))
    big = np.repeat(np.repeat(big, 3, axis=0), 3, axis=1)
    save_png(big, path)


# --- Fassung 2: leise, unregelmaessige Fugen ------------------------------------------------------------------------
# Regel der Leitung: Die Umgebung ist ruhiger als die Figuren. Fugen nur wenig dunkler als der Stein (etwa ein
# Drittel des alten Abstands), unregelmaessig, stellenweise ausgebrochen oder zugesetzt; Steine dafuer etwas
# staerker in Ton und Helligkeit versetzt; Decke deutlich dunkler und ruhiger als die Wand.

def soft_joints(img, dx, dy, n, seed, depth, width=1.1):
    """Fugen: im Mittel nur depth dunkler (0,22 = 22 %), mit Rauschen in Tiefe und Breite; zugesetzte Stellen
    (keine Fuge) und ausgebrochene Stellen (breiter, etwas dunkler)."""
    var = tnoise(n, 12, seed)
    filled = tnoise(n, 7, seed + 1) > 0.72                         # zugesetzt: Moertel buendig, kaum zu sehen
    broken = tnoise(n, 9, seed + 2) < 0.2                          # ausgebrochen: Kante abgeplatzt
    w = width + 1.2 * broken
    joint = ((dx < w) | (dy < w * 0.9)) & ~filled
    amount = depth * (0.6 + 0.8 * var) * (1 + 0.5 * broken)
    img[joint] *= (1 - amount[joint])[..., None]
    near = ((dx < w + 1.5) | (dy < w + 1.2)) & ~joint             # sanfte Kantenabrundung statt harter Linie
    img[near] *= (1 - 0.35 * amount[near])[..., None]
    return img


def paint_wall():
    n = SIZE * PAINT
    rng = np.random.default_rng(11)
    # Drei bis fuenf Steine je Lage und Meter: mit nur zwei, drei Steinen zog sich ein einzelner dunkler Stein
    # beim Kacheln als Band durch die ganze Wand (Fehler schon in Fassung 1).
    ids, dx, dy = courses(n, [13, 19, 12, 20], (11, 24), 12)
    base = np.array(WORLD_WALL, float)
    tone = rng.uniform(0.88, 1.12, ids.max() + 1)                 # Steine leicht versetzt
    hue = rng.normal(0, 2.5, (ids.max() + 1, 3))
    img = base * tone[ids][..., None] + hue[ids]
    img *= (0.88 + 0.22 * tnoise(n, 8, 13))[..., None]
    img *= (0.93 + 0.12 * tnoise(n, 32, 14))[..., None]
    img = soft_joints(img, dx, dy, n, 41, depth=0.22)
    damp = np.clip((tnoise_xy(n, 2, 16, 15) - 0.55) * 3, 0, 1) * tnoise_xy(n, 4, 4, 16)
    img *= (1 - 0.18 * damp)[..., None]
    img[..., 1] += 5 * damp
    # Keine Risse: auf kleinen Steinen wiederholten sie sich jeden Meter als auffaelliges Zeichen.
    return finish(img, WORLD_WALL, 17)


def paint_floor():
    n = SIZE * PAINT
    rng = np.random.default_rng(21)
    ids, dx, dy = courses(n, [32, 32], (22, 44), 22)
    base = np.array(WORLD_FLOOR, float)
    tone = rng.uniform(0.86, 1.14, ids.max() + 1)
    img = base * tone[ids][..., None]
    img *= (0.88 + 0.22 * tnoise(n, 6, 23))[..., None]
    img *= (0.94 + 0.1 * tnoise(n, 32, 24))[..., None]
    worn = np.clip(np.minimum(dx, dy) / 6, 0, 1)
    img *= (0.92 + 0.12 * worn)[..., None]
    img = soft_joints(img, dx, dy, n, 51, depth=0.22, width=1.4)
    grime = np.clip((tnoise(n, 10, 25) - 0.62) * 2.5, 0, 0.4)
    img = img * (1 - grime[..., None]) + np.array((52, 44, 44.0)) * grime[..., None]
    cracks(img, rng, 5, n, 0.82)
    return finish(img, WORLD_FLOOR, 27)


CEILING_DARKER = 0.72      # Decke: mittlere Helligkeit 72 % der heutigen Deckenfarbe - verschwindet nach oben


def paint_ceiling():
    n = SIZE * PAINT
    rng = np.random.default_rng(31)
    ids, dx, dy = courses(n, [16] * 4, (20, 36), 32)
    base = np.array(WORLD_CEILING, float)
    tone = rng.uniform(0.92, 1.08, ids.max() + 1)
    img = base * tone[ids][..., None]
    img *= (0.9 + 0.16 * tnoise(n, 8, 33))[..., None]
    img = soft_joints(img, dx, dy, n, 61, depth=0.14)
    img *= (0.82 + 0.25 * tnoise(n, 3, 34))[..., None]             # Russ, weich
    return finish(img, np.asarray(WORLD_CEILING, float) * CEILING_DARKER, 37)


# --- Figuren aus der Uebergabe ------------------------------------------------------------------------------------

MODELS = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(OUT_DIR))), "uebergabe", "assets", "models")


def import_figure(path, smooth):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in objs:
        img = o.data.materials[0].node_tree.nodes["Image Texture"].image if o.data.materials else None
        o.data.materials.clear()
        o.data.materials.append(pass_material("fig_" + o.name, img))
        o["smooth"] = smooth
        o["rest"] = [list(r) for r in o.matrix_world]
    return objs


def place(objs, pos_game, eye_game, extra=None):
    """Figur bei pos (Spiel-Achsen) aufstellen, zum Auge gedreht; extra: Teilname -> Matrix (lokal)."""
    to = eye_game - pos_game
    yaw = math.atan2(to.x, to.z)
    root = Matrix.Translation(G(pos_game)) @ Matrix.Rotation(yaw, 4, "Z")
    for o in objs:
        m = root @ Matrix([list(r) for r in o["rest"]])
        if extra and o.name.split(".")[0] in extra:
            m = m @ extra[o.name.split(".")[0]]
        o.matrix_world = m
    bpy.context.view_layer.update()
    for o in objs:
        write_light(o)


def textures_and_mats(paint, suffix):
    out = {}
    for key, fn in (("wall", paint[0]), ("floor", paint[1]), ("ceiling", paint[2])):
        name = {"wall": "catacomb_wall", "floor": "catacomb_floor", "ceiling": "catacomb_ceiling"}[key]
        arr = fn() if callable(fn) else fn
        img = save_image(name + suffix, arr, os.path.join(OUT_DIR, name + suffix + ".png"))
        out[key] = (pass_material("mat_" + key + suffix, img), arr)
    return out


def load_old(name):
    img = bpy.data.images.load(os.path.join(OUT_DIR, name + ".png"), check_existing=False)
    w, h = img.size
    arr = np.array(img.pixels[:]).reshape(h, w, 4)[..., :3] * 255
    bpy.data.images.remove(img)
    return arr


def set_room_material(objs, mats):
    for o in objs:
        key = "floor" if o.name.startswith("boden") else "ceiling" if o.name.startswith("decke") else "wall"
        o.data.materials[0] = mats[key][0]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    old = textures_and_mats((load_old("catacomb_wall"), load_old("catacomb_floor"), load_old("catacomb_ceiling")),
                            "_alt")
    new = textures_and_mats((paint_wall, paint_floor, paint_ceiling), "_v2")
    for key in ("wall", "floor", "ceiling"):
        tiled_preview(new[key][1], os.path.join(OUT_DIR, f"kacheln_catacomb_{key}_v2.png"))
    cam = camera()
    setup_render(854, 480)
    cam.data.angle_y = math.radians(70)

    hall = box_room({k: v[0] for k, v in new.items()}, -2.5, 2.5, -30.0, 4.0, 3.5)
    room = box_room({k: v[0] for k, v in new.items()}, 40.0, 52.0, 0.0, 12.0, 3.5)   # 12 x 12 m fuer 10 m Abstand
    col = pillar({k: v[0] for k, v in new.items()}, 46.0, 6.0, 0.45, 3.5)
    for o in hall + room + col:
        write_light(o)
    ghoul = import_figure(os.path.join(MODELS, "ghoul.glb"), True)
    chest = import_figure(os.path.join(MODELS, "chest_king.glb"), False)
    jaw_rest = {"jaw": Matrix.Rotation(math.radians(12), 4, "X")}               # Ruhehaltung: Maul haengt offen

    def shot(name, scene, eye, target, figures=None):
        aim(cam, G(eye), G(target))
        result = {}
        for label, mats in (("alt", old), ("neu", new)):
            set_room_material(scene, mats)
            result[label] = compose(render_passes(f"{name}_{label}", visible=scene + (figures or [])), "spiel")
        return result

    # Ohne Figuren, neue Fassung.
    r = shot("gang", hall, V(1.0, 1.6, -2.5), V(-1.6, 1.3, 9.0))
    save_png(r["neu"], os.path.join(OUT_DIR, "gang_lang_v2.png"))
    r = shot("kampfraum", room + col, V(40.8, 1.6, -0.8), V(46.4, 1.2, -6.4))
    save_png(r["neu"], os.path.join(OUT_DIR, "kampfraum_v2.png"))

    # Mit Ghul und King-Kiste aus 4 m und 10 m, alt und neu nebeneinander.
    hall_eye = V(0.0, 1.6, -2.0)
    room_eye = V(40.8, 1.6, -0.8)
    room_dir = Vector((10.2, 0, -5.2)).normalized()
    for d in (4, 10):
        g = hall_eye + V(0, -1.6, d)
        place(ghoul, g, hall_eye, jaw_rest)
        place(chest, g + V(-1.3, 0, 0.6), hall_eye)
        r = shot(f"gang_fig_{d}", hall, hall_eye, g + V(0, 0.6, 0), ghoul + chest)
        save_png(side_by_side([r["alt"], r["neu"]]), os.path.join(OUT_DIR, f"vergleich_gang_{d}m.png"))
        g = room_eye + V(0, -1.6, 0) + V(room_dir.x, 0, room_dir.z) * d
        side = V(room_dir.z, 0, -room_dir.x)
        place(ghoul, g, room_eye, jaw_rest)
        place(chest, g + side * 1.3 + V(room_dir.x, 0, room_dir.z) * 0.6, room_eye)
        r = shot(f"raum_fig_{d}", room + col, room_eye, g + V(0, 0.6, 0), ghoul + chest)
        save_png(side_by_side([r["alt"], r["neu"]]), os.path.join(OUT_DIR, f"vergleich_kampfraum_{d}m.png"))
    for key in ("wall", "floor", "ceiling"):
        os.remove(os.path.join(OUT_DIR, f"catacomb_{key}_alt.png"))           # nur Arbeitskopie der ersten Fassung


main()
