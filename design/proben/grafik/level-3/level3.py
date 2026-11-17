"""Probestueck 10: je zwei weitere Varianten fuer Wand, Boden und Decke der Katakomben (Issue #244).

Aufruf (aus dem Repository-Wurzelverzeichnis):
    blender.exe --background --python design/proben/grafik/level-3/level3.py   (Blender 5.3)

Schreibt nach assets/textures/: catacomb_<wall|floor|ceiling>_b.png und _c.png (64x64, hoechstens 24 Farben,
nahtlos, 1 m je Kachel). Die vorhandenen Texturen (Fassung 2, tools/textures/catacomb.py) bleiben unveraendert und
sind Variante "a". Die Hilfsfunktionen (Rauschen, Mauerlagen, Fugen) kommen aus diesem Skript, ohne es zu aendern.

Regel: gleiche mittlere Helligkeit und gleicher Farbton wie Variante a, damit keine Kante auffaellt; unterschiedlich
sind nur Steingroesse, Fugenbild und flache Flecken, nie Kontrast oder Farbe. Alles per Skript, nichts heruntergeladen.
"""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
src = open(os.path.join(ROOT, "tools", "textures", "catacomb.py"), encoding="utf-8").read()
src = src.replace("\nmain()\n", "\n").replace("__file__", repr(os.path.join(ROOT, "tools", "textures", "catacomb.py")))
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
from psx import save_png  # noqa: E402
exec(compile(src, "catacomb.py", "exec"))      # liefert tnoise, courses, soft_joints, finish, cracks, paint_* ...


def paint_wall_b():
    """Behauene Quader: groessere, gleichmaessigere Bloecke, schmale Fugen, kaum Fleckung."""
    n = SIZE * PAINT
    rng = np.random.default_rng(111)
    ids, dx, dy = courses(n, [21, 22, 21], (20, 33), 112)
    tone = rng.uniform(0.93, 1.07, ids.max() + 1)
    hue = rng.normal(0, 1.8, (ids.max() + 1, 3))
    img = np.array(WORLD_WALL, float) * tone[ids][..., None] + hue[ids]
    img *= (0.92 + 0.14 * tnoise(n, 6, 113))[..., None]
    img *= (0.95 + 0.1 * tnoise(n, 32, 114))[..., None]
    img = soft_joints(img, dx, dy, n, 141, depth=0.17, width=0.9)
    return finish(img, WORLD_WALL, 117)


def paint_wall_c():
    """Alter Bruchstein wie a, aber kleinteiliger und mit Feuchtigkeit: weiche, dunkle Laufspuren von oben."""
    n = SIZE * PAINT
    rng = np.random.default_rng(121)
    ids, dx, dy = courses(n, [10, 14, 11, 15, 14], (9, 19), 122)
    tone = rng.uniform(0.93, 1.07, ids.max() + 1)
    hue = rng.normal(0, 1.6, (ids.max() + 1, 3))
    img = np.array(WORLD_WALL, float) * tone[ids][..., None] + hue[ids]
    img *= (0.9 + 0.2 * tnoise(n, 8, 123))[..., None]
    img = soft_joints(img, dx, dy, n, 241, depth=0.2)
    streak = np.clip((tnoise_xy(n, 2, 7, 125) - 0.5) * 2.4, 0, 1)
    img *= (1 - 0.13 * streak)[..., None]
    img[..., 1] += 2 * streak
    return finish(img, WORLD_WALL, 127)


def cobbles(n, seed):
    """Pflaster aus kleinen, rundlichen Steinen (Voronoi-Zellen, periodisch). Rueckgabe: Zellennummer, Randabstand."""
    rng = np.random.default_rng(seed)
    k = 7
    pts = np.array([(i + rng.uniform(0.15, 0.85), j + rng.uniform(0.15, 0.85)) for i in range(k) for j in range(k)]) / k * n
    ys, xs = np.mgrid[0:n, 0:n]
    best = np.full((n, n), 1e9)
    second = np.full((n, n), 1e9)
    which = np.zeros((n, n), int)
    for idx, (py, px) in enumerate(pts):
        d = np.full((n, n), 1e9)
        for oy in (-n, 0, n):
            for ox in (-n, 0, n):
                d = np.minimum(d, np.hypot(ys - (py + oy), xs - (px + ox)))
        closer = d < best
        second = np.where(closer, best, np.minimum(second, d))
        which = np.where(closer, idx, which)
        best = np.where(closer, d, best)
    return which, (second - best) / PAINT


def paint_floor_b():
    """Grosse Platten, je 1 m x 0,5 m, in Bahnen versetzt; ausgetretene Mitte, wenige Risse - sehr ruhig."""
    n = SIZE * PAINT
    rng = np.random.default_rng(211)
    ids, dx, dy = courses(n, [32, 32], (64, 65), 212)
    tone = rng.uniform(0.92, 1.08, ids.max() + 1)
    img = np.array(WORLD_FLOOR, float) * tone[ids][..., None]
    img *= (0.92 + 0.14 * tnoise(n, 5, 213))[..., None]
    img *= (0.95 + 0.08 * tnoise(n, 32, 214))[..., None]
    img = soft_joints(img, dx, dy, n, 251, depth=0.2, width=1.3)
    cracks(img, rng, 2, n, 0.86)
    return finish(img, WORLD_FLOOR, 217)


def paint_floor_c():
    """Kopfsteinpflaster aus runden Steinen, Fugen dunkel aber flach, Toene wenig verschieden."""
    n = SIZE * PAINT
    rng = np.random.default_rng(221)
    which, edge = cobbles(n, 222)
    tone = rng.uniform(0.9, 1.1, which.max() + 1)
    img = np.array(WORLD_FLOOR, float) * tone[which][..., None]
    img *= (0.92 + 0.14 * tnoise(n, 8, 223))[..., None]
    shade = np.clip(edge / 3.0, 0, 1)                      # Rundung: zum Rand hin dunkler
    img *= (0.80 + 0.2 * shade)[..., None]
    return finish(img, WORLD_FLOOR, 227)


def paint_ceiling_b():
    """Glatte, grosse Deckenplatten (fast ohne Fugen), nur Russ."""
    n = SIZE * PAINT
    rng = np.random.default_rng(311)
    ids, dx, dy = courses(n, [32, 32], (64, 65), 312)
    tone = rng.uniform(0.95, 1.05, ids.max() + 1)
    img = np.array(WORLD_CEILING, float) * tone[ids][..., None]
    img *= (0.9 + 0.16 * tnoise(n, 4, 313))[..., None]
    img = soft_joints(img, dx, dy, n, 361, depth=0.1)
    img *= (0.82 + 0.25 * tnoise(n, 3, 314))[..., None]
    return finish(img, np.asarray(WORLD_CEILING, float) * CEILING_DARKER, 317)


def paint_ceiling_c():
    """Kleine Ziegel im Laeuferverband, Russ und ein Wasserfleck."""
    n = SIZE * PAINT
    rng = np.random.default_rng(321)
    ids, dx, dy = courses(n, [8] * 8, (14, 22), 322)
    tone = rng.uniform(0.92, 1.08, ids.max() + 1)
    img = np.array(WORLD_CEILING, float) * tone[ids][..., None]
    img *= (0.9 + 0.16 * tnoise(n, 8, 323))[..., None]
    img = soft_joints(img, dx, dy, n, 371, depth=0.12, width=0.9)
    img *= (0.82 + 0.25 * tnoise(n, 3, 324))[..., None]
    return finish(img, np.asarray(WORLD_CEILING, float) * CEILING_DARKER, 327)


def main3():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    os.makedirs(OUT_DIR, exist_ok=True)
    for name, paint in (("catacomb_wall_b", paint_wall_b), ("catacomb_wall_c", paint_wall_c),
                        ("catacomb_floor_b", paint_floor_b), ("catacomb_floor_c", paint_floor_c),
                        ("catacomb_ceiling_b", paint_ceiling_b), ("catacomb_ceiling_c", paint_ceiling_c)):
        arr = paint()
        save_image(name, arr, os.path.join(OUT_DIR, name + ".png"))
        print("Textur:", name, "mittel", arr.mean(axis=(0, 1)).round(1), "Farben", len({tuple(p) for p in arr.reshape(-1, 3)}))
    # Vorschau: alle neun nebeneinander, 3x3 gekachelt, 4-fach vergroessert
    cols = []
    for kind in ("wall", "floor", "ceiling"):
        row = []
        for suf in ("", "_b", "_c"):
            a = np.asarray(bpy.data.images.load(os.path.join(OUT_DIR, f"catacomb_{kind}{suf}.png")).pixels[:], float)
            a = a.reshape(SIZE, SIZE, 4)[::-1, :, :3] * 255
            row.append(np.repeat(np.repeat(np.tile(a, (2, 2, 1)), 3, 0), 3, 1))
        cols.append(np.concatenate(row, axis=1))
    save_png(np.concatenate(cols, axis=0), os.path.join(HERE, "level3_texturen.png"))


main3()
