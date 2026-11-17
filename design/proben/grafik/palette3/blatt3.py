"""Blaetter Runde 3 (Blender-Python): blatt.png (Spalten heute, A, B; oben kleine Farbfelder: links die Grund-Reihen, rechts die Akzent-Reihen;
Zeilen Saal 4 m, grosse Wandflaeche 6 m, Holztuer im Gang, Bosstor, Wand mit Riss 2 m), blatt_kacheln.png (Wand a und Boden a heute | A | B,
je 4 x 4 gekachelt: so sieht man die Wiederholung), kennzahlen (Ghul-Wand, Ghul-Boden).
Aufruf: blender --background --python blatt3.py"""
import json
import os

import numpy as np
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
KEYS = ["0_heute", "A", "B"]
SCENES = ["saal_4m", "wand_6m", "gang_tuer", "bosstor", "wand_2m"]


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


def lab(rgb):
    c = np.asarray(rgb, float) / 255.0
    lin = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    m = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = lin @ m.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], axis=-1)


def swatches(key, width, height):
    out = np.full((height, width, 3), (24, 22, 28), np.float32)
    if key == "0_heute":
        rows = []
        for name in ("catacomb_wall.png", "catacomb_floor.png", "catacomb_ceiling.png"):
            px = load(os.path.join(ROOT, "assets", "textures", name)).reshape(-1, 3)
            rows.append(sorted({tuple(int(v) for v in np.round(p)) for p in px}, key=lambda c: 0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2]))
        cw = (width - 16) // 24
        for r, cols in enumerate(rows):
            for i, c in enumerate(cols[:24]):
                out[8 + r * 14:8 + r * 14 + 12, 8 + i * cw:8 + i * cw + cw - 1] = c
        return out
    data = json.load(open(os.path.join(HERE, "paletten", "palette_stein.json"), encoding="utf-8"))
    half = (width - 24) // 2
    for side, group in ((0, data["grund"]), (1, data["akzent"])):
        x0 = 8 + side * (half + 8)
        cw = half // 15
        for r, row in enumerate(group):
            for i, c in enumerate(row["farben"]):
                out[8 + r * 14:8 + r * 14 + 12, x0 + i * cw:x0 + i * cw + cw - 1] = c
    return out


def main():
    cols = []
    for key in KEYS:
        sw = swatches(key, 427, 60)
        shots = [shrink(load(os.path.join(HERE, "fotos", f"{key}_{s}.png")), 0.5) for s in SCENES]
        cols.append(np.concatenate([sw] + shots, axis=0))
    save(np.concatenate(cols, axis=1), os.path.join(HERE, "blatt.png"))
    rows = []
    for tex in ("catacomb_wall.png", "catacomb_floor.png"):
        tiles = [load(os.path.join(ROOT, "assets", "textures", tex))] + [load(os.path.join(HERE, "neu", k, "textures", tex)) for k in ("A", "B")]
        rows.append(np.concatenate([np.kron(np.tile(t, (4, 4, 1)), np.ones((2, 2, 1))) for t in tiles], axis=1))
    save(np.concatenate(rows, axis=0), os.path.join(HERE, "blatt_kacheln.png"))
    ghoul = load(os.path.join(ROOT, "assets", "models", "ghoul.png")).reshape(-1, 3)
    L = lab(ghoul)
    g = L[L[:, 0] > 25].mean(0)
    for key in KEYS:
        base = os.path.join(ROOT, "assets", "textures") if key == "0_heute" else os.path.join(HERE, "neu", key, "textures")
        wl = lab(load(os.path.join(base, "catacomb_wall.png")).reshape(-1, 3)).mean(0)
        fl = lab(load(os.path.join(base, "catacomb_floor.png")).reshape(-1, 3)).mean(0)
        print(f"KENNZAHL {key}: Ghul-Wand dE {np.linalg.norm(g - wl):.1f}  Ghul-Boden dE {np.linalg.norm(g - fl):.1f}")


main()
