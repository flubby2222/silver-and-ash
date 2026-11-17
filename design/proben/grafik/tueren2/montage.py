"""Hilfsskript (mit Blender-Python): Bilder in einem Raster mit Beschriftung. Aufruf:
blender --background --python montage.py -- out.png Spalten Massstab bild1 bild2 ...
Bilder werden mit Naechster-Nachbar verkleinert; ohne Beschriftung (die kommt im Blatt-Skript)."""
import sys
import numpy as np
import bpy

def load(path):
    img = bpy.data.images.load(path)
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    return a[::-1, :, :3]          # Zeile 0 oben

def save(arr, path):
    h, w = arr.shape[:2]
    img = bpy.data.images.new("m", w, h)
    rgba = np.concatenate([arr[::-1], np.ones((h, w, 1), np.float32)], axis=2)
    img.pixels = rgba.ravel().tolist()
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()

def shrink(a, scale):
    h, w = a.shape[:2]
    nh, nw = int(h * scale), int(w * scale)
    ys = (np.arange(nh) / scale).astype(int).clip(0, h - 1)
    xs = (np.arange(nw) / scale).astype(int).clip(0, w - 1)
    return a[ys][:, xs]

args = sys.argv[sys.argv.index("--") + 1:]
out, cols, scale = args[0], int(args[1]), float(args[2])
ims = [shrink(load(p), scale) for p in args[3:]]
h, w = ims[0].shape[:2]
rows = (len(ims) + cols - 1) // cols
sheet = np.zeros((rows * h, cols * w, 3), np.float32)
for i, im in enumerate(ims):
    sheet[(i // cols) * h:(i // cols + 1) * h, (i % cols) * w:(i % cols + 1) * w] = im
save(sheet, out)
