"""Rechnet die vorhandenen Texturen und Modell-Texturen des Spiels auf eine Palette um (nur zum Ansehen, Probestueck).
Naechste Farbe in Lab (Helligkeit doppelt gewichtet), ohne Dithering in der Textur. Umgebung (Wand-, Boden-, Deckentexturen,
Tueren, Risswand) nimmt nur die Katakomben-Auswahl der Palette, alles andere (Figuren, Waffen, Kisten, Aermel) die ganzen 64 Farben.

Aufruf: blender --background --python umrechnen.py -- <palette_key | alle>
Schreibt nach design/proben/grafik/palette/umgerechnet/<key>/{textures,models} (Kopien; die Originale bleiben unberuehrt) und
meldet je Textur, wie viele Farben vorher und nachher vorkommen.
"""
import json
import os
import struct
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
import bpy  # noqa: E402

TMP = os.path.join(HERE, "_tmp")
ENV_NAMES = ("catacomb_", "door_", "cracked_wall_")


def lab(rgb):
    c = np.asarray(rgb, float) / 255.0
    lin = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    m = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = lin @ m.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], axis=-1)


def load_palette(key):
    data = json.load(open(os.path.join(HERE, "paletten", f"palette_{key}.json"), encoding="utf-8"))
    rows = {r["name"]: r["farben"] for r in data["reihen"]}
    full = [c for r in data["reihen"] for c in r["farben"]]
    sel = [rows[name][step] for name, step in data["katakomben"]]
    return np.array(full, float), np.array(sel, float)


def remap(img, pal):
    """img (h, w, 3) 0..255 -> naechste Palettenfarbe je Pixel (Lab, Helligkeit doppelt)."""
    flat = img.reshape(-1, 3)
    uniq, inv = np.unique(flat, axis=0, return_inverse=True)
    lu, lp = lab(uniq), lab(pal)
    w = np.array([2.0, 1.0, 1.0])
    d = (((lu[:, None, :] - lp[None, :, :]) * w) ** 2).sum(-1)
    return pal[d.argmin(1)][inv.reshape(-1)].reshape(img.shape)


def read_png(path):
    im = bpy.data.images.load(path)
    w, h = im.size
    a = np.array(im.pixels[:], dtype=float).reshape(h, w, 4)
    bpy.data.images.remove(im)
    return np.round(a[..., :3] * 255.0)[::-1]                     # Zeile 0 oben


def write_png(arr, path):
    h, w = arr.shape[:2]
    im = bpy.data.images.new("t", w, h)
    rgba = np.concatenate([arr[::-1] / 255.0, np.ones((h, w, 1))], axis=2)
    im.pixels = rgba.ravel().tolist()
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)


def process_png(src, dst, pal):
    a = read_png(src)
    b = remap(a, pal)
    write_png(b, dst)
    return len({tuple(p) for p in a.reshape(-1, 3).astype(int)}), len({tuple(p) for p in b.reshape(-1, 3).astype(int)})


def patch_glb(src, dst, pal):
    """Ersetzt die eingebetteten PNG-Bilder einer glb durch umgerechnete; baut den BIN-Block neu auf."""
    data = open(src, "rb").read()
    jlen, = struct.unpack_from("<I", data, 12)
    js = json.loads(data[20:20 + jlen])
    boff = 20 + jlen
    blen, = struct.unpack_from("<I", data, boff)
    bin_ = data[boff + 8:boff + 8 + blen]
    views = js["bufferViews"]
    chunks = [bin_[v.get("byteOffset", 0):v.get("byteOffset", 0) + v["byteLength"]] for v in views]
    counts = []
    for img in js.get("images", []):
        i = img["bufferView"]
        os.makedirs(TMP, exist_ok=True)
        p0, p1 = os.path.join(TMP, "a.png"), os.path.join(TMP, "b.png")
        open(p0, "wb").write(chunks[i])
        counts.append(process_png(p0, p1, pal))
        chunks[i] = open(p1, "rb").read()
    out = bytearray()
    for v, c in zip(views, chunks):
        while len(out) % 4:
            out.append(0)
        v["byteOffset"] = len(out)
        v["byteLength"] = len(c)
        out += c
    while len(out) % 4:
        out.append(0)
    js["buffers"][0]["byteLength"] = len(out)
    jb = json.dumps(js, separators=(",", ":")).encode()
    jb += b" " * (-len(jb) % 4)
    total = 12 + 8 + len(jb) + 8 + len(out)
    blob = struct.pack("<4sII", b"glTF", 2, total) + struct.pack("<I4s", len(jb), b"JSON") + jb + struct.pack("<I4s", len(out), b"BIN\0") + bytes(out)
    open(dst, "wb").write(blob)
    return counts


def main():
    arg = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "alle"
    keys = [f[len("palette_"):-5] for f in sorted(os.listdir(os.path.join(HERE, "paletten"))) if f.endswith(".json")] if arg == "alle" else [arg]
    for key in keys:
        full, sel = load_palette(key)
        base = os.path.join(HERE, "umgerechnet", key)
        os.makedirs(os.path.join(base, "textures"), exist_ok=True)
        os.makedirs(os.path.join(base, "models"), exist_ok=True)
        tdir = os.path.join(ROOT, "assets", "textures")
        for f in sorted(os.listdir(tdir)):
            if f.endswith(".png"):
                n = process_png(os.path.join(tdir, f), os.path.join(base, "textures", f), sel)
                print(f"TEX {key} {f} Farben {n[0]} -> {n[1]}")
        mdir = os.path.join(ROOT, "assets", "models")
        for f in sorted(os.listdir(mdir)):
            pal = sel if f.startswith(ENV_NAMES) else full
            if f.endswith(".glb"):
                c = patch_glb(os.path.join(mdir, f), os.path.join(base, "models", f), pal)
                print(f"GLB {key} {f} Farben {[(a, b) for a, b in c]}")
            elif f.endswith(".png"):
                n = process_png(os.path.join(mdir, f), os.path.join(base, "models", f), pal)
    import shutil
    shutil.rmtree(TMP, ignore_errors=True)


main()
