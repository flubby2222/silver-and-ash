"""Blaetter: heutiger Stand und die drei Entwuerfe a, b, c als 2 x 2 (Blender-Python, nur Probebilder)."""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "..", "..", "..", "tools", "models"))
from psx import load_png, save_png  # noqa: E402

ORDER = ("heute", "a", "b", "c")        # links oben, rechts oben, links unten, rechts unten


def img(v, scene):
    return np.asarray(load_png(os.path.join(HERE, "fotos_" + v, scene + ".png")), float)


def grid(tiles, gap=6):
    h, w = tiles[0].shape[:2]
    bar = np.zeros((h, gap, 3))
    hbar = np.zeros((gap, 2 * w + gap, 3))
    top = np.concatenate([tiles[0], bar, tiles[1]], axis=1)
    bottom = np.concatenate([tiles[2], bar, tiles[3]], axis=1)
    return np.concatenate([bottom, hbar, top], axis=0)       # load/save laufen unten -> oben


def crop(a, x0, x1, y0, y1, k=1):
    h = a.shape[0]
    c = a[h - y1:h - y0, x0:x1]
    return np.repeat(np.repeat(c, k, 0), k, 1)


save_png(grid([img(v, "room-corridor") for v in ORDER]), os.path.join(HERE, "blatt_gang.png"))
save_png(grid([crop(img(v, "pistol-recoil"), 430, 854, 190, 480, 2) for v in ORDER]), os.path.join(HERE, "blatt_pistole.png"))
save_png(grid([crop(img(v, "pistol-recoil"), 0, 424, 190, 480, 2) for v in ORDER]), os.path.join(HERE, "blatt_klinge_ruhe.png"))
save_png(grid([crop(img(v, "blade-charged"), 0, 424, 190, 480, 2) for v in ORDER]), os.path.join(HERE, "blatt_klinge_aufgeladen.png"))
