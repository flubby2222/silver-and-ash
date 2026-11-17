"""Blaetter der Palettenprobe, Runde 2 (Blender-Python).
blatt.png: Spalten Palette 1, 2, 3, heutiger Stand. Oben die Farbfelder: die Reihen der Umgebung gross (Stein kalt, Stein warm, Holz,
Eisen), die uebrigen klein; weisser Punkt = Katakomben-Auswahl; beim heutigen Stand die Farben von Wand, Boden und Decke je eine Zeile.
Darunter: Saal mit Holztuer, Kisten und Ghul aus 4 m; derselbe aus 10 m; Gang mit Tuer; Bosstor; die Wand aus 2 m.
blatt_texturen.png: Wand a, Boden a, Decke a, Wand b, Wand c, Boden c: heute | 1 | 2 | 3, vierfach vergroessert.
Aufruf: blender --background --python blatt2.py"""
import json
import os

import numpy as np
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
KEYS = ["1_violettgrau", "2_kalt_blau", "3_warm_ocker", "0_heute"]
SCENES = ["saal_4m", "saal_10m", "gang_tuer", "bosstor", "wand_2m"]


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
    im.pixels = np.concatenate([arr[::-1] / 255.0, np.ones((h, w, 1))], axis=2).astype(np.float32).ravel().tolist()
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()


def shrink(a, scale):
    h, w = a.shape[:2]
    nh, nw = int(h * scale), int(w * scale)
    return a[(np.arange(nh) / scale).astype(int).clip(0, h - 1)][:, (np.arange(nw) / scale).astype(int).clip(0, w - 1)]


def swatches(key, width, height):
    out = np.full((height, width, 3), (24, 22, 28), np.float32)
    if key == "0_heute":
        tdir = os.path.join(ROOT, "assets", "textures")
        rows = []
        for name in ("catacomb_wall.png", "catacomb_floor.png", "catacomb_ceiling.png"):
            px = load(os.path.join(tdir, name)).reshape(-1, 3)
            cols = sorted({tuple(int(v) for v in np.round(p)) for p in px}, key=lambda c: 0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2])
            rows.append(cols)
        cw = (width - 16) // 24
        y = 8
        for cols in rows:
            for i, c in enumerate(cols[:24]):
                out[y:y + 30, 8 + i * cw:8 + i * cw + cw - 1] = c
            y += 34
        return out
    data = json.load(open(os.path.join(HERE, "paletten", f"palette_{key}.json"), encoding="utf-8"))
    grid = [[tuple(c) for c in r["farben"]] for r in data["reihen"]]
    names = [r["name"] for r in data["reihen"]]
    sel = {(names.index(n), s) for n, s in data["katakomben"]}
    heights = [24, 24, 24, 24, 9, 9, 9, 9]                         # Umgebung gross, der Rest klein
    cw = (width - 16) // 8
    y = 8
    for r in range(8):
        for s in range(8):
            x0 = 8 + s * cw
            out[y:y + heights[r] - 1, x0:x0 + cw - 2] = grid[r][s]
            if (r, s) in sel and heights[r] > 10:
                out[y + heights[r] - 7:y + heights[r] - 4, x0 + 2:x0 + 5] = (255, 255, 255)
        y += heights[r]
    return out


def main():
    cols = []
    for key in KEYS:
        sw = swatches(key, 427, 150)
        shots = [shrink(load(os.path.join(HERE, "fotos", f"{key}_{s}.png")), 0.5) for s in SCENES]
        cols.append(np.concatenate([sw] + shots, axis=0))
    save(np.concatenate(cols, axis=1), os.path.join(HERE, "blatt.png"))
    rows = []
    for tex in ("catacomb_wall.png", "catacomb_floor.png", "catacomb_ceiling.png", "catacomb_wall_b.png", "catacomb_wall_c.png", "catacomb_floor_c.png"):
        tiles = [load(os.path.join(ROOT, "assets", "textures", tex))] + [load(os.path.join(HERE, "neu", k, "textures", tex)) for k in KEYS[:3]]
        rows.append(np.concatenate([np.kron(t, np.ones((4, 4, 1))) for t in tiles], axis=1))
    save(np.concatenate(rows, axis=0), os.path.join(HERE, "blatt_texturen.png"))


main()
