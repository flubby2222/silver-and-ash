"""Level-Texturen der Katakomben - Probestueck 8 fuer Silver & Ash (Issue #233).

Wand, Boden und Decke, je 64 x 64 Pixel (1 m je Wiederholung), hoechstens 32 Farben (hier 24), ohne Filterung,
nahtlos kachelbar: Fugen, Rauschen, Risse und Flecken laufen ueber den Rand und kommen gegenueber wieder herein.
Die mittlere Helligkeit jeder Textur entspricht der heutigen Flaechenfarbe des Spiels (LookSettings).

    Wand  - Katakombenmauer: unregelmaessige Bruchsteinlagen, tiefe Fugen, Feuchtigkeitsspuren
    Boden - abgetretene Steinplatten verschiedener Groesse, Schmutz in den Fugen
    Decke - kleines Gewoelbe-Mauerwerk, Russ

Bilder: die drei Texturen 3 x 3 gekachelt (Naht-Probe), ein langer Gang (20 m Wand im Bild), ein Kampfraum mit
Saeule. Alles mit Nebel und 15-Bit-Koernung (Entscheidung vom 2026-11-16).

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/level/level.py
"""

import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "gemeinsam"))
import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
from psx import (WORLD_WALL, WORLD_FLOOR, WORLD_CEILING, quantize, save_image, pass_material, write_light,  # noqa: E402
                 setup_render, camera, aim, render_passes, compose, save_png, G, V)

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


def paint_wall():
    n = SIZE * PAINT
    rng = np.random.default_rng(11)
    ids, dx, dy = courses(n, [13, 19, 12, 20], (14, 40), 12)
    base = np.array(WORLD_WALL, float)
    tone = rng.uniform(0.9, 1.1, ids.max() + 1)                     # ruhige Toene: keine Einzelsteine, die sich je Meter wiederholen
    hue = rng.normal(0, 4, (ids.max() + 1, 3))
    img = base * tone[ids][..., None] + hue[ids]
    img *= (0.84 + 0.3 * tnoise(n, 8, 13))[..., None]               # Bruchstein: grobe Unebenheit
    img *= (0.9 + 0.18 * tnoise(n, 32, 14))[..., None]              # Koernung des Steins
    # Kanten: abgerundete Bruchsteine, Licht von oben (oben hell, unten Schatten), tiefe dunkle Fugen.
    ys = (np.arange(n) // PAINT)[:, None] * np.ones((1, n))
    edge = np.clip(1 - np.minimum(dx, dy) / 2.5, 0, 1)
    img *= (1 - 0.35 * edge)[..., None]
    joint = (dx < 1.1) | (dy < 1.0)
    img[joint] = img[joint] * 0.32
    # Feuchte Laufspuren: senkrecht, lang, kachelbar.
    damp = np.clip((tnoise_xy(n, 2, 16, 15) - 0.55) * 3, 0, 1) * tnoise_xy(n, 4, 4, 16)
    img *= (1 - 0.28 * damp)[..., None]
    img[..., 1] += 6 * damp                                          # ein Hauch kaltes Gruen-Grau (Salpeter)
    cracks(img, rng, 3, n, 0.7)                                      # wenige, schwache Risse: Wiederholung faellt weniger auf
    return finish(img, WORLD_WALL, 17)


def paint_floor():
    n = SIZE * PAINT
    rng = np.random.default_rng(21)
    ids, dx, dy = courses(n, [32, 32], (22, 44), 22)                # grosse Platten, anders als die Wand
    base = np.array(WORLD_FLOOR, float)
    tone = rng.uniform(0.82, 1.18, ids.max() + 1)
    img = base * tone[ids][..., None]
    img *= (0.86 + 0.26 * tnoise(n, 6, 23))[..., None]
    img *= (0.92 + 0.14 * tnoise(n, 32, 24))[..., None]
    worn = np.clip(np.minimum(dx, dy) / 6, 0, 1)                     # abgetreten: Mitte heller, Kanten dunkler
    img *= (0.82 + 0.25 * worn)[..., None]
    joint = (dx < 1.4) | (dy < 1.4)
    img[joint] = img[joint] * 0.38
    grime = np.clip((tnoise(n, 10, 25) - 0.6) * 3, 0, 0.6)
    img = img * (1 - grime[..., None]) + np.array((40, 34, 32.0)) * grime[..., None]
    cracks(img, rng, 9, n, 0.5)
    for _ in range(14):                                               # Splitter und Geroell
        x, y = int(rng.uniform(0, n)), int(rng.uniform(0, n))
        img[y:y + 3, x:x + 4] = img[y:y + 3, x:x + 4] * 1.35
    return finish(img, WORLD_FLOOR, 27)


def paint_ceiling():
    n = SIZE * PAINT
    rng = np.random.default_rng(31)
    ids, dx, dy = courses(n, [16] * 4, (20, 36), 32)                # nicht zu klein: sonst flimmert die Decke
    base = np.array(WORLD_CEILING, float)
    tone = rng.uniform(0.86, 1.14, ids.max() + 1)
    img = base * tone[ids][..., None]
    img *= (0.88 + 0.24 * tnoise(n, 8, 33))[..., None]
    joint = (dx < 0.9) | (dy < 0.9)
    img[joint] = img[joint] * 0.45
    soot = tnoise(n, 3, 34)                                          # Russ von Fackeln
    img *= (0.72 + 0.4 * soot)[..., None]
    return finish(img, WORLD_CEILING, 37)


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


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    textures = {"wall": paint_wall(), "floor": paint_floor(), "ceiling": paint_ceiling()}
    names = {"wall": "catacomb_wall", "floor": "catacomb_floor", "ceiling": "catacomb_ceiling"}
    mats = {}
    for key, arr in textures.items():
        img = save_image(names[key], arr, os.path.join(OUT_DIR, names[key] + ".png"))
        mats[key] = pass_material("mat_" + key, img)
        tiled_preview(arr, os.path.join(OUT_DIR, f"kacheln_{names[key]}.png"))
    cam = camera()
    setup_render(854, 480)
    cam.data.angle_y = math.radians(70)

    # Langer Gang: 5 m breit, 3,5 m hoch, 34 m lang; Blick schraeg an der linken Wand entlang.
    hall = box_room(mats, -2.5, 2.5, -30.0, 4.0, 3.5)          # Blender-y = -Spiel-z: Gang von z -4 bis 30
    for o in hall:
        write_light(o)
    eye = G(V(1.0, 1.6, -2.5))
    aim(cam, eye, G(V(-1.6, 1.3, 9.0)))
    save_png(compose(render_passes("gang", visible=hall), "spiel"), os.path.join(OUT_DIR, "gang_lang.png"))
    aim(cam, eye, G(V(0.0, 1.2, 10.0)))
    save_png(compose(render_passes("gang_mitte", visible=hall), "spiel"), os.path.join(OUT_DIR, "gang_mitte.png"))

    # Kampfraum: 9 x 9 m, 3,5 m hoch, Saeule 0,9 m in der Mitte; Blick aus einer Ecke.
    room = box_room(mats, 40.0, 49.0, 0.0, 9.0, 3.5)
    col = pillar(mats, 44.5, 4.5, 0.45, 3.5)
    for o in room + col:
        write_light(o)
    aim(cam, Vector((40.8, 0.8, 1.6)), Vector((45.2, 5.2, 1.2)))
    save_png(compose(render_passes("kampfraum", visible=room + col), "spiel"), os.path.join(OUT_DIR, "kampfraum.png"))


main()
