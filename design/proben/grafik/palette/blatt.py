"""Baut die Blaetter der Palettenprobe (Blender-Python, nur numpy + bpy-Bilder).
blatt.png: Spalten = Palette 1, 2, 3, heutiger Stand; obere Zeile die Farbfelder (8 Reihen x 8 Stufen, Punkt = Katakomben-Auswahl),
darunter die Szenen (Saal 4 m, Saal 10 m, Bosstor, Gorm 12 m).
blatt_texturen.png: Wand, Boden und Decke (Variante A) vorher/nachher, vierfach vergroessert.
Aufruf: blender --background --python blatt.py
"""
import json
import os

import numpy as np
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
KEYS = ["1_violettgrau", "2_kalt_blau", "3_warm_ocker", "0_heute"]
SCENES = ["saal_4m", "saal_10m", "bosstor", "gorm_12m"]


def load(path):
    im = bpy.data.images.load(path)
    w, h = im.size
    a = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1, :, :3] * 255
    bpy.data.images.remove(im)
    return a


def save(arr, path):
    arr = np.clip(arr, 0, 255)
    h, w = arr.shape[:2]
    im = bpy.data.images.new("m", w, h)
    rgba = np.concatenate([arr[::-1] / 255.0, np.ones((h, w, 1))], axis=2).astype(np.float32)
    im.pixels = rgba.ravel().tolist()
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()


def shrink(a, scale):
    h, w = a.shape[:2]
    nh, nw = int(h * scale), int(w * scale)
    ys = (np.arange(nh) / scale).astype(int).clip(0, h - 1)
    xs = (np.arange(nw) / scale).astype(int).clip(0, w - 1)
    return a[ys][:, xs]


def swatches(key, width, height):
    out = np.full((height, width, 3), (24, 22, 28), np.float32)
    if key == "0_heute":
        cols = []
        tdir = os.path.join(ROOT, "assets", "textures")
        seen = set()
        for f in sorted(os.listdir(tdir)):
            if f.endswith(".png"):
                for p in load(os.path.join(tdir, f)).reshape(-1, 3):
                    seen.add(tuple(int(v) for v in np.round(p)))
        cols = sorted(seen, key=lambda c: 0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2])
        n = len(cols)
        grid = [[None] * 8 for _ in range(8)]
        step = max(1, n // 64)
        for i, c in enumerate(cols[::step][:64]):
            grid[i // 8][i % 8] = c
        sel = set()
    else:
        data = json.load(open(os.path.join(HERE, "paletten", f"palette_{key}.json"), encoding="utf-8"))
        grid = [[tuple(c) for c in r["farben"]] for r in data["reihen"]]
        names = [r["name"] for r in data["reihen"]]
        sel = {(names.index(n), s) for n, s in data["katakomben"]}
    cw, ch = (width - 16) // 8, (height - 16) // 8
    for r in range(8):
        for s in range(8):
            c = grid[r][s]
            if c is None:
                continue
            y0, x0 = 8 + r * ch, 8 + s * cw
            out[y0:y0 + ch - 2, x0:x0 + cw - 2] = c
            if (r, s) in sel:
                out[y0 + ch - 7:y0 + ch - 4, x0 + 2:x0 + 5] = (255, 255, 255)
    return out


def main():
    scale = 0.5
    cols = []
    for key in KEYS:
        sw = swatches(key, 427, 200)
        shots = [shrink(load(os.path.join(HERE, "fotos", f"{key}_{s}.png")), scale) for s in SCENES]
        cols.append(np.concatenate([sw] + shots, axis=0))
    save(np.concatenate(cols, axis=1), os.path.join(HERE, "blatt.png"))
    # Texturen vorher/nachher (Wand, Boden, Decke A), vierfach
    rows = []
    for tex in ("catacomb_wall.png", "catacomb_floor.png", "catacomb_ceiling.png"):
        tiles = [load(os.path.join(ROOT, "assets", "textures", tex))]
        for key in KEYS[:3]:
            tiles.append(load(os.path.join(HERE, "umgerechnet", key, "textures", tex)))
        rows.append(np.concatenate([np.kron(t, np.ones((4, 4, 1))) for t in tiles], axis=1))
    save(np.concatenate(rows, axis=0), os.path.join(HERE, "blatt_texturen.png"))


main()
